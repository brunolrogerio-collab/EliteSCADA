using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;

namespace Scada.DriverHost.Sidecars;

public sealed record ManagedSidecarProcessStartSpec(
    string FileName,
    IReadOnlyList<string>? Arguments = null,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyCollection<string>? SensitiveValues = null,
    string? GracefulStopInput = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FileName) ||
            !string.Equals(FileName, FileName.Trim(), StringComparison.Ordinal) ||
            FileName.Length > 1024 ||
            FileName.Any(character => character is '\r' or '\n' or '\0'))
        {
            throw new ArgumentException("Sidecar executable path is invalid.", nameof(FileName));
        }

        if (Arguments is { Count: > 256 })
            throw new ArgumentOutOfRangeException(nameof(Arguments), "Sidecar argument count exceeds the supported limit.");
        if (Arguments is not null && Arguments.Any(argument =>
                argument is null ||
                argument.Length > 8192 ||
                argument.Any(character => character is '\r' or '\n' or '\0')))
        {
            throw new ArgumentException("Sidecar argument is invalid.", nameof(Arguments));
        }

        if (WorkingDirectory is not null &&
            (string.IsNullOrWhiteSpace(WorkingDirectory) ||
             WorkingDirectory.Length > 1024 ||
             WorkingDirectory.Any(character => character is '\r' or '\n' or '\0')))
        {
            throw new ArgumentException("Sidecar working directory is invalid.", nameof(WorkingDirectory));
        }

        if (Environment is { Count: > 128 })
            throw new ArgumentOutOfRangeException(nameof(Environment), "Sidecar environment exceeds the supported setting count.");
        if (Environment is not null)
        {
            foreach (var pair in Environment)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) ||
                    pair.Key.Length > 256 ||
                    pair.Key.Contains('=') ||
                    pair.Key.Any(char.IsControl))
                {
                    throw new ArgumentException("Sidecar environment key is invalid.", nameof(Environment));
                }

                if (pair.Value is null ||
                    pair.Value.Length > 1_048_576 ||
                    pair.Value.Contains('\0'))
                {
                    throw new ArgumentException("Sidecar environment value is invalid.", nameof(Environment));
                }
            }
        }

        if (GracefulStopInput is not null &&
            (GracefulStopInput.Length > 1024 ||
             GracefulStopInput.Any(character => character is '\r' or '\n' or '\0')))
        {
            throw new ArgumentException("Sidecar graceful-stop input is invalid.", nameof(GracefulStopInput));
        }
    }
}

public interface IManagedSidecarResolvedProcessFactory
{
    ValueTask<IManagedSidecarProcess> StartAsync(
        ManagedSidecarDefinition definition,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> protectedSettings,
        CancellationToken cancellationToken = default);
}

public interface IManagedSidecarProcessOutput
{
    IReadOnlyCollection<string> SanitizedOutput { get; }
}

public sealed class ManagedSidecarProtectedMaterialProcessFactory : IManagedSidecarProcessFactory
{
    private readonly CommunicationDriverRuntimeServices _services;
    private readonly IManagedSidecarResolvedProcessFactory _inner;

    public ManagedSidecarProtectedMaterialProcessFactory(
        CommunicationDriverRuntimeServices services,
        IManagedSidecarResolvedProcessFactory inner)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _services.Validate();
    }

    public async ValueTask<IManagedSidecarProcess> StartAsync(
        ManagedSidecarDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();

        var protectedSettings = definition.Configuration.ProtectedSettings;
        if (protectedSettings is null || protectedSettings.Count == 0)
        {
            return await _inner.StartAsync(
                definition,
                new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase),
                cancellationToken).ConfigureAwait(false);
        }

        var resolver = _services.ProtectedMaterialResolver
            ?? throw new InvalidOperationException(
                $"Managed sidecar '{definition.InstanceId}' requires the host-owned protected-material resolver.");

        var leases = new List<ICommunicationDriverProtectedMaterialLease>();
        try
        {
            var resolved = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
            foreach (var setting in protectedSettings)
            {
                cancellationToken.ThrowIfCancellationRequested();
                setting.Validate();

                var request = new CommunicationDriverProtectedMaterialRequest(
                    _services.ProjectKey,
                    setting.ResourceId,
                    setting.ResourceKind,
                    setting.Purpose,
                    setting.Reference);
                request.Validate();

                var lease = await resolver.ResolveAsync(request, cancellationToken).ConfigureAwait(false);
                if (lease.Material.IsEmpty)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Managed sidecar protected setting '{setting.SettingKey}' resolved to empty material.");
                }

                leases.Add(lease);
                resolved.Add(setting.SettingKey, lease.Material);
            }

            return await _inner.StartAsync(definition, resolved, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            for (var index = leases.Count - 1; index >= 0; index--)
                await leases[index].DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class SystemManagedSidecarProcessFactory :
    IManagedSidecarProcessFactory,
    IManagedSidecarResolvedProcessFactory
{
    private readonly Func<
        ManagedSidecarDefinition,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>>,
        CancellationToken,
        ValueTask<ManagedSidecarProcessStartSpec>> _startSpecFactory;

    public SystemManagedSidecarProcessFactory(
        Func<
            ManagedSidecarDefinition,
            IReadOnlyDictionary<string, ReadOnlyMemory<byte>>,
            CancellationToken,
            ValueTask<ManagedSidecarProcessStartSpec>> startSpecFactory)
    {
        _startSpecFactory = startSpecFactory ?? throw new ArgumentNullException(nameof(startSpecFactory));
    }

    public ValueTask<IManagedSidecarProcess> StartAsync(
        ManagedSidecarDefinition definition,
        CancellationToken cancellationToken = default) =>
        StartAsync(
            definition,
            new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);

    public async ValueTask<IManagedSidecarProcess> StartAsync(
        ManagedSidecarDefinition definition,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> protectedSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(protectedSettings);
        definition.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        var spec = await _startSpecFactory(definition, protectedSettings, cancellationToken)
            .ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(spec);
        spec.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(spec.WorkingDirectory))
            startInfo.WorkingDirectory = spec.WorkingDirectory;

        if (spec.Arguments is not null)
        {
            foreach (var argument in spec.Arguments)
                startInfo.ArgumentList.Add(argument);
        }

        if (spec.Environment is not null)
        {
            foreach (var pair in spec.Environment)
                startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Managed sidecar '{definition.InstanceId}' process did not start.");
        }
        catch
        {
            process.Dispose();
            throw;
        }

        return new SystemManagedSidecarProcess(
            definition.InstanceId,
            process,
            spec.SensitiveValues,
            spec.GracefulStopInput);
    }

    private sealed class SystemManagedSidecarProcess :
        IManagedSidecarProcess,
        IManagedSidecarProcessOutput
    {
        private const int MaximumCapturedLines = 128;

        private readonly string _instanceId;
        private readonly Process _process;
        private readonly IReadOnlyCollection<string> _sensitiveValues;
        private readonly string? _gracefulStopInput;
        private readonly TaskCompletionSource<ManagedSidecarReadyReport> _ready =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _capturedOutput = [];
        private readonly object _outputSync = new();
        private readonly CancellationTokenSource _pumpLifetime = new();
        private readonly Task _stdoutPump;
        private readonly Task _stderrPump;
        private int _disposed;

        public SystemManagedSidecarProcess(
            string instanceId,
            Process process,
            IReadOnlyCollection<string>? sensitiveValues,
            string? gracefulStopInput)
        {
            _instanceId = instanceId;
            _process = process;
            _sensitiveValues = sensitiveValues?.Where(value => !string.IsNullOrEmpty(value)).ToArray()
                ?? Array.Empty<string>();
            _gracefulStopInput = gracefulStopInput;
            _stdoutPump = PumpAsync(process.StandardOutput, allowReadyEnvelope: true, _pumpLifetime.Token);
            _stderrPump = PumpAsync(process.StandardError, allowReadyEnvelope: false, _pumpLifetime.Token);
            _ = ObserveEarlyExitAsync();
        }

        public string ProcessIdentity => $"pid:{_process.Id}";

        public IReadOnlyCollection<string> SanitizedOutput
        {
            get
            {
                lock (_outputSync)
                    return _capturedOutput.ToArray();
            }
        }

        public async ValueTask<ManagedSidecarReadyReport> WaitForReadyAsync(
            CancellationToken cancellationToken = default) =>
            await _ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        public async ValueTask<int> WaitForExitAsync(
            CancellationToken cancellationToken = default)
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return _process.ExitCode;
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken = default)
        {
            if (_process.HasExited) return;

            try
            {
                if (_gracefulStopInput is not null)
                {
                    await _process.StandardInput.WriteLineAsync(_gracefulStopInput.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _process.StandardInput.Close();
                }

                await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill();
                throw;
            }
            catch (IOException)
            {
                if (!_process.HasExited)
                    throw;
            }
            catch (InvalidOperationException)
            {
                if (!_process.HasExited)
                    throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            if (!_process.HasExited)
                TryKill();

            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            _pumpLifetime.Cancel();
            try
            {
                await Task.WhenAll(_stdoutPump, _stderrPump).ConfigureAwait(false);
            }
            catch
            {
            }

            _pumpLifetime.Dispose();
            _process.Dispose();
        }

        private async Task ObserveEarlyExitAsync()
        {
            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
                if (!_ready.Task.IsCompleted)
                {
                    _ready.TrySetException(new InvalidOperationException(
                        $"Managed sidecar '{_instanceId}' exited before readiness with code {_process.ExitCode}."));
                }
            }
            catch (Exception exception)
            {
                _ready.TrySetException(new InvalidOperationException(
                    $"Managed sidecar '{_instanceId}' readiness observation failed.",
                    exception));
            }
        }

        private async Task PumpAsync(
            StreamReader reader,
            bool allowReadyEnvelope,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) return;

                    if (allowReadyEnvelope && TryParseReady(line, out var ready))
                    {
                        _ready.TrySetResult(ready);
                        continue;
                    }

                    var sanitized = ManagedSidecarLogSanitizer.Sanitize(line, _sensitiveValues);
                    if (sanitized is not null)
                        Capture(sanitized);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private bool TryParseReady(string line, out ManagedSidecarReadyReport ready)
        {
            ready = default!;
            if (string.IsNullOrWhiteSpace(line) || line.Length > 8192)
                return false;

            try
            {
                var envelope = JsonSerializer.Deserialize<ReadyEnvelope>(
                    line,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (envelope is null ||
                    !string.Equals(envelope.Type, "ready", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(envelope.ArtifactId) ||
                    string.IsNullOrWhiteSpace(envelope.ArtifactVersion) ||
                    string.IsNullOrWhiteSpace(envelope.RuntimeVersion) ||
                    envelope.SchemaVersion <= 0)
                {
                    return false;
                }

                var message = ManagedSidecarLogSanitizer.Sanitize(
                    envelope.Message,
                    _sensitiveValues);
                ready = new ManagedSidecarReadyReport(
                    new ManagedSidecarArtifactIdentity(
                        envelope.ArtifactId,
                        envelope.ArtifactVersion,
                        envelope.RuntimeVersion,
                        envelope.SchemaVersion),
                    message);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private void Capture(string line)
        {
            lock (_outputSync)
            {
                if (_capturedOutput.Count == MaximumCapturedLines)
                    _capturedOutput.RemoveAt(0);
                _capturedOutput.Add(line);
            }
        }

        private void TryKill()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private sealed record ReadyEnvelope(
            string Type,
            string ArtifactId,
            string ArtifactVersion,
            string RuntimeVersion,
            int SchemaVersion,
            string? Message);
    }
}
