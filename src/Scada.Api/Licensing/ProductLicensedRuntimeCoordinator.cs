using Scada.Core.Abstractions;
using Scada.Core.Alarms;
using Scada.Core.Commands;
using Scada.Core.Events;
using Scada.Core.InternalMemory;
using Scada.Core.Product.Licensing;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.DriverHost.Runtime;
using Scada.Drivers.Abstractions;
using Scada.Engineering.Contracts;
using Scada.Engineering.Validation;

namespace Scada.Api.Licensing;

public enum ProductRuntimeLifecycleState
{
    Idle,
    Running,
    DemoExpired
}

public sealed record ProductRuntimeEntitlementStatus(
    ProductRuntimeLifecycleState State,
    LicenseState? ActiveLicenseState,
    LicenseTier? ActiveTier,
    int? MaximumTags,
    DateTimeOffset? DemoStartedAtUtc,
    DateTimeOffset? DemoExpiresAtUtc,
    TimeSpan? DemoRemaining,
    string? LastDiagnostic);

public interface IProductRuntimeStatusProvider
{
    ProductRuntimeEntitlementStatus GetProductRuntimeStatus();
}

/// <summary>
/// Product-owned runtime boundary. Entitlement is evaluated before the existing
/// transactional runtime coordinator is entered, so a denied Run never stages,
/// commits or replaces the currently active runtime. Demo expiry disposes the
/// active runtime and swaps in a fresh empty coordinator so a later explicit Run
/// starts a new session without restarting the EliteSCADA host process.
/// </summary>
public sealed class ProductLicensedRuntimeCoordinator :
    IEngineeringRuntimeCoordinator,
    IGatewayRuntimeDiagnosticsProvider,
    IProductRuntimeStatusProvider
{
    public const string EntitlementDeniedIssueCode = "PRODUCT_RUN_ENTITLEMENT_DENIED";
    public const string DemoExpiredDiagnostic = "Demo Run session expired after its continuous runtime allowance.";

    private readonly Func<IEngineeringRuntimeCoordinator> _innerFactory;
    private readonly IProductRunEntitlementProvider _entitlements;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _activationGate = new(1, 1);
    private readonly object _statusGate = new();
    private IEngineeringRuntimeCoordinator _inner;
    private CancellationTokenSource? _demoExpiryCancellation;
    private Task? _demoExpiryTask;
    private long _activationGeneration;
    private ProductRuntimeLifecycleState _lifecycleState = ProductRuntimeLifecycleState.Idle;
    private RunEntitlementDecision? _activeDecision;
    private long? _demoStartedTimestamp;
    private DateTimeOffset? _demoStartedAtUtc;
    private TimeSpan? _demoDuration;
    private string? _lastDiagnostic;
    private bool _disposed;

    public ProductLicensedRuntimeCoordinator(
        IEngineeringRuntimeCoordinator initialInner,
        Func<IEngineeringRuntimeCoordinator> innerFactory,
        IProductRunEntitlementProvider entitlements,
        TimeProvider? timeProvider = null)
    {
        _inner = initialInner ?? throw new ArgumentNullException(nameof(initialInner));
        _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
        _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private IEngineeringRuntimeCoordinator Current => Volatile.Read(ref _inner);

    public RuntimeDescriptor Describe() => Current.Describe();
    public IReadOnlyCollection<TagDefinition> Tags() => Current.Tags();
    public IReadOnlyCollection<TagValue> CurrentValues() => Current.CurrentValues();
    public IReadOnlyCollection<AlarmDefinition> AlarmDefinitions() => Current.AlarmDefinitions();
    public IReadOnlyCollection<AlarmInstance> Alarms(bool activeOnly = false) => Current.Alarms(activeOnly);
    public IReadOnlyCollection<CommandDefinition> Commands() => Current.Commands();
    public IReadOnlyCollection<ClientMemoryRuntimeSource> ClientMemorySources() => Current.ClientMemorySources();
    public bool TryGetTag(Guid tagId, out TagDefinition? tag) => Current.TryGetTag(tagId, out tag);
    public bool TryGetTagByPath(string path, out TagDefinition? tag) => Current.TryGetTagByPath(path, out tag);
    public bool TryGetCurrent(Guid tagId, out TagValue? value) => Current.TryGetCurrent(tagId, out value);
    public bool TryGetCommand(Guid commandId, out CommandDefinition? command) => Current.TryGetCommand(commandId, out command);
    public bool IsServerMemoryTag(Guid tagId) => Current.IsServerMemoryTag(tagId);

    public IReadOnlyCollection<GatewayRouteRuntimeDiagnostic> GatewayDiagnostics() =>
        Current is IGatewayRuntimeDiagnosticsProvider diagnostics
            ? diagnostics.GatewayDiagnostics()
            : Array.Empty<GatewayRouteRuntimeDiagnostic>();

    public ValueTask<bool> AcknowledgeAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Current.AcknowledgeAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> ShelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Current.ShelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask<bool> UnshelveAlarmAsync(
        Guid alarmId,
        string user,
        CancellationToken cancellationToken = default) =>
        Current.UnshelveAlarmAsync(alarmId, user, cancellationToken);

    public ValueTask WriteAsync(Guid tagId, object? value, CancellationToken cancellationToken = default) =>
        Current.WriteAsync(tagId, value, cancellationToken);

    public ValueTask ResetServerMemoryRetainedValueAsync(
        Guid tagId,
        CancellationToken cancellationToken = default) =>
        Current.ResetServerMemoryRetainedValueAsync(tagId, cancellationToken);

    public ValueTask ExecuteCommandAsync(Guid commandId, CancellationToken cancellationToken = default) =>
        Current.ExecuteCommandAsync(commandId, cancellationToken);

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
        if (string.IsNullOrWhiteSpace(projectKey))
            throw new ArgumentException("Project key is required.", nameof(projectKey));
        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision));
        ArgumentNullException.ThrowIfNull(package);

        await _activationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            var decision = _entitlements.EvaluateRun(package.Tags.Count);
            if (!decision.Allowed)
            {
                var diagnostic = decision.Diagnostic ?? "EliteSCADA product entitlement denied Runtime activation.";
                SetLastDiagnostic(diagnostic);
                return new RuntimeActivationResult(
                    projectKey.Trim(),
                    revision,
                    false,
                    Array.Empty<EngineeringDriverIssue>(),
                    new[]
                    {
                        new RuntimeActivationIssue(
                            EntitlementDeniedIssueCode,
                            diagnostic,
                            IsError: true)
                    });
            }

            var inner = Current;
            RuntimeActivationResult result;
            if (commitAsync is null)
            {
                result = await inner.ActivateAsync(projectKey, revision, package, cancellationToken);
            }
            else
            {
                result = await inner.ActivateAsync(
                    projectKey,
                    revision,
                    package,
                    commitAsync,
                    cancellationToken);
            }

            if (!result.Activated)
            {
                SetLastDiagnostic(
                    result.RuntimeIssues.FirstOrDefault(issue => issue.IsError)?.Message ??
                    result.CompilationIssues.FirstOrDefault(issue => issue.IsError)?.Message);
                return result;
            }

            CancelDemoExpiryLocked();
            var generation = ++_activationGeneration;
            StartActiveStatus(decision);

            if (decision.LicenseState == LicenseState.Demo &&
                decision.MaximumContinuousRun is { } demoDuration)
            {
                var cancellation = new CancellationTokenSource();
                _demoExpiryCancellation = cancellation;
                _demoExpiryTask = ExpireDemoAsync(inner, generation, demoDuration, cancellation.Token);
            }

            return result;
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private async Task ExpireDemoAsync(
        IEngineeringRuntimeCoordinator expectedInner,
        long expectedGeneration,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await _activationGate.WaitAsync(CancellationToken.None);
        try
        {
            if (_disposed ||
                expectedGeneration != _activationGeneration ||
                !ReferenceEquals(Current, expectedInner) ||
                cancellationToken.IsCancellationRequested)
                return;

            var replacement = _innerFactory();
            Volatile.Write(ref _inner, replacement);
            _demoExpiryCancellation = null;
            _demoExpiryTask = null;

            string diagnostic = DemoExpiredDiagnostic;
            try
            {
                await expectedInner.DisposeAsync();
            }
            catch (Exception ex)
            {
                diagnostic += $" Runtime stop reported: {ex.Message}";
            }

            lock (_statusGate)
            {
                _lifecycleState = ProductRuntimeLifecycleState.DemoExpired;
                _lastDiagnostic = diagnostic;
            }
        }
        finally
        {
            _activationGate.Release();
        }
    }

    public ProductRuntimeEntitlementStatus GetProductRuntimeStatus()
    {
        lock (_statusGate)
        {
            TimeSpan? remaining = null;
            DateTimeOffset? expiresAtUtc = null;
            if (_lifecycleState == ProductRuntimeLifecycleState.Running &&
                _activeDecision?.LicenseState == LicenseState.Demo &&
                _demoStartedTimestamp.HasValue &&
                _demoDuration.HasValue &&
                _demoStartedAtUtc.HasValue)
            {
                var elapsed = _timeProvider.GetElapsedTime(_demoStartedTimestamp.Value);
                remaining = _demoDuration.Value - elapsed;
                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;
                expiresAtUtc = _demoStartedAtUtc.Value + _demoDuration.Value;
            }

            return new ProductRuntimeEntitlementStatus(
                _lifecycleState,
                _activeDecision?.LicenseState,
                _activeDecision?.Tier,
                _activeDecision?.MaximumTags,
                _demoStartedAtUtc,
                expiresAtUtc,
                remaining,
                _lastDiagnostic);
        }
    }

    private void StartActiveStatus(RunEntitlementDecision decision)
    {
        lock (_statusGate)
        {
            _lifecycleState = ProductRuntimeLifecycleState.Running;
            _activeDecision = decision;
            _lastDiagnostic = null;
            if (decision.LicenseState == LicenseState.Demo && decision.MaximumContinuousRun.HasValue)
            {
                _demoStartedTimestamp = _timeProvider.GetTimestamp();
                _demoStartedAtUtc = _timeProvider.GetUtcNow();
                _demoDuration = decision.MaximumContinuousRun;
            }
            else
            {
                _demoStartedTimestamp = null;
                _demoStartedAtUtc = null;
                _demoDuration = null;
            }
        }
    }

    private void SetLastDiagnostic(string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
            return;
        lock (_statusGate)
            _lastDiagnostic = diagnostic;
    }

    private void CancelDemoExpiryLocked()
    {
        var cancellation = _demoExpiryCancellation;
        _demoExpiryCancellation = null;
        _demoExpiryTask = null;
        cancellation?.Cancel();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProductLicensedRuntimeCoordinator));
    }

    public async ValueTask DisposeAsync()
    {
        Task? expiryTask;
        await _activationGate.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            expiryTask = _demoExpiryTask;
            CancelDemoExpiryLocked();
            await Current.DisposeAsync();
            lock (_statusGate)
            {
                _lifecycleState = ProductRuntimeLifecycleState.Idle;
                _activeDecision = null;
                _demoStartedTimestamp = null;
                _demoStartedAtUtc = null;
                _demoDuration = null;
            }
        }
        finally
        {
            _activationGate.Release();
        }

        if (expiryTask is not null)
        {
            try
            {
                await expiryTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        // The coordinator singleton is exposed through multiple DI service contracts.
        // Keeping this process-lifetime gate alive makes repeated/concurrent disposal
        // safe instead of letting a later alias call WaitAsync on a disposed semaphore.
    }
}

public static class ProductLicensedRuntimeConfiguration
{
    public static void AddProductLicensedRuntimeCoordinator(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(_ => CommunicationDriverRuntimeComposition.BuildForCurrentSchema());
        builder.Services.AddSingleton<EngineeringDataSourceTypeCatalog>(sp =>
            EngineeringDataSourceTypeCatalog.BuildForCurrentSchema(
                sp.GetRequiredService<CommunicationDriverRuntimeComponentRegistry>()));
        builder.Services.AddSingleton<IDataSourceConfigurationValidator>(sp =>
            sp.GetRequiredService<EngineeringDataSourceTypeCatalog>());

        builder.Services.AddSingleton<ProductLicensedRuntimeCoordinator>(sp =>
        {
            var eventBus = sp.GetRequiredService<IScadaEventBus>();
            var activationTimeout = TimeSpan.FromSeconds(Math.Max(
                1,
                builder.Configuration.GetValue<double?>("EngineeringRuntime:ActivationTimeoutSeconds") ?? 10));

            IEngineeringRuntimeCoordinator CreateFresh() =>
                new GatewayEngineeringRuntimeCoordinator(
                    new EngineeringRuntimeCoordinator(
                        eventBus,
                        sp.GetRequiredService<IEngineeringDriverCompiler>(),
                        activationTimeout,
                        sp.GetRequiredService<IServerMemoryRetentionStore>(),
                        protectedMaterialResolver: sp.GetService<ICommunicationDriverProtectedMaterialResolver>()),
                    eventBus);

            return new ProductLicensedRuntimeCoordinator(
                sp.GetRequiredService<GatewayEngineeringRuntimeCoordinator>(),
                CreateFresh,
                sp.GetRequiredService<IProductRunEntitlementProvider>(),
                sp.GetRequiredService<TimeProvider>());
        });

        // These registrations intentionally come after Program's raw runtime registrations.
        // Microsoft DI resolves the last registration for a single service, so all product
        // runtime consumers enter the licensing boundary while the raw concrete coordinator
        // remains available only as the initial inner instance.
        builder.Services.AddSingleton<IEngineeringRuntimeCoordinator>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
        builder.Services.AddSingleton<IGatewayRuntimeDiagnosticsProvider>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
        builder.Services.AddSingleton<IProductRuntimeStatusProvider>(sp =>
            sp.GetRequiredService<ProductLicensedRuntimeCoordinator>());
    }
}
