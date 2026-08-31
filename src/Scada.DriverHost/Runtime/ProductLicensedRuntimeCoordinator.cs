using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Product;
using Scada.Core.Tags;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;

namespace Scada.DriverHost.Runtime;

public sealed record ProductRuntimeLicenseStatus(
    ProductLicenseSnapshot License,
    bool RuntimeActive,
    DateTimeOffset? RuntimeStartedAtUtc,
    DateTimeOffset? RuntimeExpiresAtUtc,
    string? LastRuntimeMessage = null,
    string? LastRuntimeIssueCode = null);

public interface IProductRuntimeLicenseStatusProvider
{
    ProductRuntimeLicenseStatus LicenseStatus();
}

/// <summary>
/// Product-level runtime gate. Engineering remains unrestricted by the Demo TAG count;
/// runtime activation is evaluated here so every Driver and every activation route gets
/// the same license behavior.
/// </summary>
public sealed class ProductLicensedRuntimeCoordinator :
    IEngineeringRuntimeCoordinator,
    IGatewayRuntimeDiagnosticsProvider,
    IProductRuntimeLicenseStatusProvider
{
    private readonly Func<IEngineeringRuntimeCoordinator> _runtimeFactory;
    private readonly IProductLicenseService _licenseService;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IEngineeringRuntimeCoordinator _inner;
    private CancellationTokenSource? _continuousRuntimeCts;
    private long _runtimeGeneration;
    private ProductRuntimeLicenseStatus _status;
    private bool _disposed;

    public ProductLicensedRuntimeCoordinator(
        Func<IEngineeringRuntimeCoordinator> runtimeFactory,
        IProductLicenseService licenseService,
        TimeProvider? timeProvider = null)
    {
        _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
        _licenseService = licenseService ?? throw new ArgumentNullException(nameof(licenseService));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _inner = CreateRuntime();
        _status = new ProductRuntimeLicenseStatus(_licenseService.Current(), false, null, null);
    }

    public RuntimeDescriptor Describe() => Current().Describe();
    public IReadOnlyCollection<TagDefinition> Tags() => Current().Tags();
    public IReadOnlyCollection<TagValue> CurrentValues() => Current().CurrentValues();
    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => Current().AlarmDefinitions();
    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => Current().Alarms(activeOnly);
    public IReadOnlyCollection<CommandDefinition> Commands() => Current().Commands();
    public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => Current().ClientMemorySources();
    public bool TryGetTag(Guid tagId, out TagDefinition? tag) => Current().TryGetTag(tagId, out tag);
    public bool TryGetTagByPath(string path, out TagDefinition? tag) => Current().TryGetTagByPath(path, out tag);
    public bool TryGetCurrent(Guid tagId, out TagValue? value) => Current().TryGetCurrent(tagId, out value);
    public bool TryGetCommand(Guid commandId, out CommandDefinition? command) => Current().TryGetCommand(commandId, out command);
    public bool IsServerMemoryTag(Guid tagId) => Current().IsServerMemoryTag(tagId);

    public IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> GatewayDiagnostics() =>
        Current() is IGatewayRuntimeDiagnosticsProvider provider
            ? provider.GatewayDiagnostics()
            : Array.Empty<GatewayRouteRuntimeDiagnostic>();

    public ValueTask<bool> AcknowledgeAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        Current().AcknowledgeAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> ShelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        Current().ShelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> UnshelveAlarmAsync(Guid alarmId, string user, CancellationToken cancellationToken = default) =>
        Current().UnshelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        Current().WriteAsync(tagId, value, cancellationToken);

    public ValueTask ResetServerMemoryRetainedValueAsync(Guid tagId, CancellationToken cancellationToken = default) =>
        Current().ResetServerMemoryRetainedValueAsync(tagId, cancellationToken);

    public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        Current().ExecuteCommandAsync(commandId, cancellationToken);

    public ProductRuntimeLicenseStatus LicenseStatus()
    {
        var current = Volatile.Read(ref _status);
        var license = _licenseService.Current();
        return current with { License = license };
    }

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        CancellationToken cancellationToken = default) =>
        ActivateCoreAsync(projectKey, revision, package, null, cancellationToken);

    public Task<RuntimeActivationResult> ActivateAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task> commitAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commitAsync);
        return ActivateCoreAsync(projectKey, revision, package, commitAsync, cancellationToken);
    }

    private async Task<RuntimeActivationResult> ActivateCoreAsync(
        string projectKey,
        long revision,
        EngineeringPackage package,
        Func<RuntimeActivationCommitContext, CancellationToken, Task>? commitAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            var permit = _licenseService.EvaluateRuntime(package.Tags.Count);
            if (!permit.Allowed)
            {
                Volatile.Write(ref _status, new ProductRuntimeLicenseStatus(
                    permit.License,
                    DescribeActiveUnsafe(),
                    Volatile.Read(ref _status).RuntimeStartedAtUtc,
                    Volatile.Read(ref _status).RuntimeExpiresAtUtc,
                    permit.Message,
                    permit.IssueCode));

                return new RuntimeActivationResult(
                    projectKey.Trim(),
                    revision,
                    false,
                    Array.Empty<Scada.DriverHost.Engineering.EngineeringDriverIssue>(),
                    new[] { new RuntimeActivationIssue(
                        permit.IssueCode ?? ProductLicensePolicy.InvalidLicenseIssueCode,
                        permit.Message ?? ProductLicensePolicy.InvalidLicenseMessage(),
                        projectKey) });
            }

            var inner = Current();
            var result = commitAsync is null
                ? await inner.ActivateAsync(projectKey, revision, package, cancellationToken)
                : await inner.ActivateAsync(projectKey, revision, package, commitAsync, cancellationToken);

            if (!result.Activated)
                return result;

            CancelContinuousTimerUnsafe();
            var startedAt = result.ActivatedAtUtc ?? _timeProvider.GetUtcNow();
            var generation = Interlocked.Increment(ref _runtimeGeneration);
            DateTimeOffset? expiresAt = null;
            if (permit.MaxContinuousRuntime is { } duration)
            {
                expiresAt = startedAt + duration;
                var cts = new CancellationTokenSource();
                _continuousRuntimeCts = cts;
                _ = ExpireDemoRuntimeAsync(generation, duration, cts.Token);
            }

            Volatile.Write(ref _status, new ProductRuntimeLicenseStatus(
                permit.License,
                true,
                startedAt,
                expiresAt));
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ExpireDemoRuntimeAsync(long generation, TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, _timeProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_disposed || generation != Volatile.Read(ref _runtimeGeneration))
                return;

            var previous = Current();
            var replacement = CreateRuntime();
            Volatile.Write(ref _inner, replacement);
            try
            {
                await previous.DisposeAsync();
            }
            finally
            {
                _continuousRuntimeCts?.Dispose();
                _continuousRuntimeCts = null;
            }

            Volatile.Write(ref _status, new ProductRuntimeLicenseStatus(
                _licenseService.Current(),
                false,
                null,
                null,
                ProductLicensePolicy.DemoRuntimeExpiredMessage(),
                ProductLicensePolicy.DemoRuntimeExpiredIssueCode));
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool DescribeActiveUnsafe() => Current().Describe().Revision.HasValue;

    private IEngineeringRuntimeCoordinator Current() => Volatile.Read(ref _inner);

    private IEngineeringRuntimeCoordinator CreateRuntime() =>
        _runtimeFactory() ?? throw new InvalidOperationException("Runtime factory returned null.");

    private void CancelContinuousTimerUnsafe()
    {
        var previous = _continuousRuntimeCts;
        _continuousRuntimeCts = null;
        if (previous is null) return;
        previous.Cancel();
        previous.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ProductLicensedRuntimeCoordinator));
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            CancelContinuousTimerUnsafe();
            await Current().DisposeAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
