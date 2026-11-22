using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;

namespace Scada.DriverHost.Sidecars;

public sealed record NativeZigbeeObservation(
    string IeeeAddress,
    int Endpoint,
    bool Value,
    string Source,
    DateTimeOffset ObservedAt)
{
    public string AddressKey => $"{IeeeAddress}/{Endpoint}";
}

public interface INativeZigbeeSidecarSession : IAsyncDisposable
{
    event Action<NativeZigbeeObservation>? ObservationReceived;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> ReadStateAsync(ZigbeeNativePoint point, CancellationToken cancellationToken = default);
    ValueTask<bool> WriteAndReadBackAsync(ZigbeeNativePoint point, bool value, CancellationToken cancellationToken = default);
    CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics();
}

/// <summary>
/// Local loopback control channel between the .NET Runtime driver and its one
/// managed Node sidecar process. The random per-start token authenticates the
/// child handshake; only fixed on/off read and write operations are accepted.
/// </summary>
public sealed class ZigbeeNativeRpcServer : IAsyncDisposable
{
    private readonly object _sync = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptTask;
    private RpcConnection? _active;
    private string? _token;
    private int _port;
    private int _disposed;

    public event Action<NativeZigbeeObservation>? ObservationReceived;

    public string Token => Volatile.Read(ref _token)
        ?? throw new InvalidOperationException("Native Zigbee RPC server is not started.");

    public int Port => Volatile.Read(ref _port) is var port && port > 0
        ? port
        : throw new InvalidOperationException("Native Zigbee RPC server is not started.");

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_sync)
        {
            if (_listener is not null)
                throw new InvalidOperationException("Native Zigbee RPC server is already started.");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            var lifetime = new CancellationTokenSource();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _listener = listener;
            _lifetime = lifetime;
            _token = token;
            _port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptTask = AcceptLoopAsync(listener, lifetime.Token);
        }
    }

    public async ValueTask<bool> ReadStateAsync(
        string ieeeAddress,
        int endpoint,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("read", ieeeAddress, endpoint, null, cancellationToken).ConfigureAwait(false);
        if (!response.Ok)
            throw new InvalidOperationException(response.ErrorCode ?? "ZIGBEE_READ_FAILED");
        if (!response.Value.HasValue)
            throw new InvalidOperationException("ZIGBEE_READBACK_MISSING");
        return response.Value.Value;
    }

    public async ValueTask<bool> WriteAndReadBackAsync(
        string ieeeAddress,
        int endpoint,
        bool value,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync("write", ieeeAddress, endpoint, value, cancellationToken).ConfigureAwait(false);
        if (!response.Ok)
            throw new InvalidOperationException(response.ErrorCode ?? "ZIGBEE_WRITE_FAILED");
        if (!response.Accepted)
            throw new InvalidOperationException("ZIGBEE_WRITE_NOT_ACCEPTED");
        if (!response.Value.HasValue)
            throw new InvalidOperationException("ZIGBEE_READBACK_MISSING");
        return response.Value.Value;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        TcpListener? listener;
        CancellationTokenSource? lifetime;
        Task? acceptTask;
        RpcConnection? active;
        lock (_sync)
        {
            listener = _listener;
            lifetime = _lifetime;
            acceptTask = _acceptTask;
            active = _active;
            _listener = null;
            _lifetime = null;
            _acceptTask = null;
            _active = null;
            _port = 0;
            _token = null;
            lifetime?.Cancel();
            listener?.Stop();
        }

        if (active is not null)
            await active.DisposeAsync().ConfigureAwait(false);

        if (acceptTask is not null)
        {
            try { await acceptTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) when (lifetime?.IsCancellationRequested == true) { }
        }
        lifetime?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAsync().ConfigureAwait(false);
    }

    private async Task<RpcResponse> SendAsync(
        string method,
        string ieeeAddress,
        int endpoint,
        bool? value,
        CancellationToken cancellationToken)
    {
        var connection = Volatile.Read(ref _active)
            ?? throw new InvalidOperationException("ZIGBEE_CONTROL_CHANNEL_UNAVAILABLE");
        return await connection.SendAsync(method, ieeeAddress, endpoint, value, cancellationToken).ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { return; }

            try
            {
                await HandleConnectionAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (IOException) { }
            catch (JsonException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        client.NoDelay = true;
        await using var connection = new RpcConnection(client, OnObservation);
        using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        JsonDocument hello;
        try { hello = await connection.ReadHandshakeAsync(handshakeTimeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (handshakeTimeout.IsCancellationRequested) { return; }
        using (hello)
        {
            if (cancellationToken.IsCancellationRequested || !IsValidHandshake(hello, Token))
                return;
        }

        lock (_sync)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            _active = connection;
        }

        await connection.WriteWelcomeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.ReadLoopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_active, connection))
                    _active = null;
            }
        }
    }

    private static bool IsValidHandshake(JsonDocument hello, string expectedToken)
    {
        var root = hello.RootElement;
        if (!TryString(root, "type", out var type) || type != "hello" ||
            !TryString(root, "token", out var token) ||
            !TryString(root, "artifactId", out var artifactId) || artifactId != "elitescada.zigbee.native" ||
            !TryString(root, "artifactVersion", out var artifactVersion) || artifactVersion != "1.0.0" ||
            !root.TryGetProperty("schemaVersion", out var schemaVersion) || !schemaVersion.TryGetInt32(out var schema) || schema != 1)
            return false;

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token),
                Encoding.UTF8.GetBytes(expectedToken));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void OnObservation(NativeZigbeeObservation observation)
    {
        var handlers = ObservationReceived;
        if (handlers is null) return;
        foreach (Action<NativeZigbeeObservation> handler in handlers.GetInvocationList())
        {
            try { handler(observation); }
            catch { }
        }
    }

    private static bool TryString(JsonElement root, string property, out string value)
    {
        value = string.Empty;
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(property, out var element) &&
               element.ValueKind == JsonValueKind.String &&
               (value = element.GetString() ?? string.Empty).Length > 0;
    }

    private sealed record RpcResponse(bool Ok, bool Accepted, bool? Value, string? ErrorCode);

    private sealed class RpcConnection : IAsyncDisposable
    {
        private static readonly Regex IeeePattern = new("^[0-9a-f]{16}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private const int MaximumFrameLength = 65_536;
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcResponse>> _pending = new(StringComparer.Ordinal);
        private readonly Action<NativeZigbeeObservation> _onObservation;
        private int _disposed;

        public RpcConnection(TcpClient client, Action<NativeZigbeeObservation> onObservation)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = false };
            _onObservation = onObservation;
        }

        public async ValueTask<JsonDocument> ReadHandshakeAsync(CancellationToken cancellationToken)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("Zigbee sidecar closed before authentication.");
            if (line.Length > MaximumFrameLength) throw new IOException("Zigbee sidecar handshake exceeded the frame limit.");
            return JsonDocument.Parse(line);
        }

        public async ValueTask WriteWelcomeAsync(CancellationToken cancellationToken)
        {
            await WriteJsonAsync(new { type = "welcome", schemaVersion = 1 }, cancellationToken).ConfigureAwait(false);
        }

        public async Task<RpcResponse> SendAsync(
            string method,
            string ieeeAddress,
            int endpoint,
            bool? value,
            CancellationToken cancellationToken)
        {
            var ieee = NormalizeIeee(ieeeAddress);
            if (endpoint is < 1 or > 240)
                throw new ArgumentOutOfRangeException(nameof(endpoint));
            if (method is not "read" and not "write")
                throw new ArgumentOutOfRangeException(nameof(method));
            if (method == "write" && !value.HasValue)
                throw new ArgumentException("A Boolean value is required for a Native Zigbee write.", nameof(value));

            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_pending.TryAdd(id, completion))
                throw new InvalidOperationException("ZIGBEE_REQUEST_ID_COLLISION");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await WriteJsonAsync(new
                {
                    type = "request",
                    id,
                    method,
                    ieee,
                    endpoint,
                    value
                }, timeout.Token).ConfigureAwait(false);
                try
                {
                    return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("Native Zigbee device readback timed out.");
                }
            }
            finally
            {
                _pending.TryRemove(id, out _);
            }
        }

        public async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) break;
                    if (line.Length > MaximumFrameLength)
                        throw new IOException("Native Zigbee sidecar frame exceeded the supported limit.");

                    using var message = JsonDocument.Parse(line);
                    var root = message.RootElement;
                    if (!TryString(root, "type", out var type))
                        throw new JsonException("Native Zigbee sidecar message type is missing.");
                    if (type == "response")
                        HandleResponse(root);
                    else if (type == "report")
                        HandleReport(root);
                    else
                        throw new JsonException("Native Zigbee sidecar message type is not allowed.");
                }
            }
            finally
            {
                FailPending(new IOException("Native Zigbee sidecar control connection closed."));
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
            FailPending(new IOException("Native Zigbee sidecar control connection was stopped."));
            _client.Dispose();
            _reader.Dispose();
            _writer.Dispose();
            _writeGate.Dispose();
            return ValueTask.CompletedTask;
        }

        private async ValueTask WriteJsonAsync<T>(T value, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(value);
            if (json.Length > MaximumFrameLength)
                throw new InvalidOperationException("Native Zigbee control request exceeded the frame limit.");
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        private void HandleResponse(JsonElement root)
        {
            if (!TryString(root, "id", out var id) || !_pending.TryRemove(id, out var completion))
                return;

            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
            var accepted = root.TryGetProperty("accepted", out var acceptedElement) && acceptedElement.ValueKind == JsonValueKind.True;
            bool? value = root.TryGetProperty("value", out var valueElement)
                ? valueElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                }
                : null;
            var error = TryString(root, "errorCode", out var errorCode) && Regex.IsMatch(errorCode, "^[A-Z0-9_]{1,96}$")
                ? errorCode
                : null;
            completion.TrySetResult(new RpcResponse(ok, accepted, value, error));
        }

        private void HandleReport(JsonElement root)
        {
            if (!TryString(root, "ieee", out var ieee) ||
                !root.TryGetProperty("endpoint", out var endpointElement) || !endpointElement.TryGetInt32(out var endpoint) || endpoint is < 1 or > 240 ||
                !root.TryGetProperty("value", out var valueElement) || valueElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False ||
                !TryString(root, "source", out var source) ||
                !TryString(root, "observedAt", out var observedAtText))
            {
                return;
            }

            if (source is not "device-report" and not "device-readback")
                return;
            if (!DateTimeOffset.TryParse(observedAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var observedAt))
                observedAt = DateTimeOffset.UtcNow;

            string normalizedIeee;
            try { normalizedIeee = NormalizeIeee(ieee); }
            catch (ArgumentException) { return; }
            _onObservation(new NativeZigbeeObservation(
                normalizedIeee,
                endpoint,
                valueElement.ValueKind == JsonValueKind.True,
                source,
                observedAt));
        }

        private void FailPending(Exception exception)
        {
            foreach (var pair in _pending.ToArray())
            {
                if (_pending.TryRemove(pair.Key, out var completion))
                    completion.TrySetException(exception);
            }
        }

        private static string NormalizeIeee(string ieeeAddress)
        {
            var value = ieeeAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ieeeAddress[2..] : ieeeAddress;
            if (!IeeePattern.IsMatch(value))
                throw new ArgumentException("IEEE address must contain 16 hexadecimal digits.", nameof(ieeeAddress));
            return value.ToLowerInvariant();
        }
    }

}

public sealed class ZigbeeNativeManagedSidecarSession : INativeZigbeeSidecarSession
{
    private readonly ManagedSidecarRuntimeBinding _binding;
    private readonly ZigbeeNativeRpcServer _rpc;
    private readonly IReadOnlyDictionary<Guid, ZigbeeNativePoint> _pointsByTagId;
    private int _disposed;

    public ZigbeeNativeManagedSidecarSession(
        ManagedSidecarRuntimeBinding binding,
        ZigbeeNativeRpcServer rpc,
        IReadOnlyCollection<ZigbeeNativePoint> points)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
        ArgumentNullException.ThrowIfNull(points);
        _pointsByTagId = points.ToDictionary(point => point.Tag.Id);
        _rpc.ObservationReceived += OnObservation;
    }

    public event Action<NativeZigbeeObservation>? ObservationReceived;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _rpc.Start();
        try
        {
            await _binding.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _rpc.StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Exception? stopFailure = null;
        try { await _binding.StopAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { stopFailure = exception; }
        try { await _rpc.StopAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { stopFailure = stopFailure is null ? exception : new AggregateException(stopFailure, exception); }
        if (stopFailure is not null) throw stopFailure;
    }

    public ValueTask<bool> ReadStateAsync(ZigbeeNativePoint point, CancellationToken cancellationToken = default)
    {
        EnsurePoint(point);
        return _rpc.ReadStateAsync(point.IeeeAddress, point.Endpoint, cancellationToken);
    }

    public ValueTask<bool> WriteAndReadBackAsync(
        ZigbeeNativePoint point,
        bool value,
        CancellationToken cancellationToken = default)
    {
        EnsurePoint(point);
        return _rpc.WriteAndReadBackAsync(point.IeeeAddress, point.Endpoint, value, cancellationToken);
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() =>
        _binding.GetCommunicationDiagnostics();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _rpc.ObservationReceived -= OnObservation;
        Exception? failure = null;
        try { await StopAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        try { await _binding.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        try { await _rpc.DisposeAsync().ConfigureAwait(false); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) throw failure;
    }

    private void OnObservation(NativeZigbeeObservation observation)
    {
        var pointKey = observation.AddressKey;
        if (!_pointsByTagId.Values.Any(point => string.Equals(point.AddressKey, pointKey, StringComparison.Ordinal)))
            return;
        var handlers = ObservationReceived;
        if (handlers is null) return;
        foreach (Action<NativeZigbeeObservation> handler in handlers.GetInvocationList())
        {
            try { handler(observation); }
            catch { }
        }
    }

    private void EnsurePoint(ZigbeeNativePoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (!_pointsByTagId.TryGetValue(point.Tag.Id, out var configured) || configured.AddressKey != point.AddressKey)
            throw new InvalidOperationException("Native Zigbee operation requested for an unconfigured TAG point.");
    }
}
