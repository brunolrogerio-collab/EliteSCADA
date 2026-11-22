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

public sealed record KnxIpObservation(Guid TagId, object Value, DateTimeOffset ObservedAt);

public interface IKnxIpSidecarSession : IAsyncDisposable
{
    event Action<KnxIpObservation>? ObservationReceived;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<object> ReadAsync(KnxIpPoint point, CancellationToken cancellationToken = default);
    ValueTask<object> WriteAndReadBackAsync(KnxIpPoint point, object value, CancellationToken cancellationToken = default);
    CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics();
}

/// <summary>Authenticated loopback JSON-lines control channel for the local XKNX child.</summary>
public sealed class KnxIpRpcServer : IAsyncDisposable
{
    private const int MaximumFrameLength = 65_536;
    private readonly object _sync = new();
    private readonly IReadOnlyDictionary<Guid, KnxIpPoint> _points;
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptTask;
    private RpcConnection? _active;
    private string? _token;
    private int _port;
    private int _disposed;

    public KnxIpRpcServer(IReadOnlyCollection<KnxIpPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = points.ToDictionary(point => point.Tag.Id);
    }

    public event Action<KnxIpObservation>? ObservationReceived;
    public string Token => Volatile.Read(ref _token) ?? throw new InvalidOperationException("KNX/IP RPC is not started.");
    public int Port => Volatile.Read(ref _port) is var port && port > 0 ? port : throw new InvalidOperationException("KNX/IP RPC is not started.");

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_sync)
        {
            if (_listener is not null) throw new InvalidOperationException("KNX/IP RPC server is already started.");
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            var lifetime = new CancellationTokenSource();
            _listener = listener;
            _lifetime = lifetime;
            _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _acceptTask = AcceptLoopAsync(listener, lifetime.Token);
        }
    }

    public ValueTask<object> ReadAsync(KnxIpPoint point, CancellationToken cancellationToken = default) =>
        SendAsync("read", point, null, cancellationToken);

    public ValueTask<object> WriteAndReadBackAsync(KnxIpPoint point, object value, CancellationToken cancellationToken = default) =>
        SendAsync("write", point, value, cancellationToken);

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
        if (active is not null) await active.DisposeAsync().ConfigureAwait(false);
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

    private async ValueTask<object> SendAsync(string method, KnxIpPoint point, object? value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);
        if (!_points.TryGetValue(point.Tag.Id, out var configured) || configured != point)
            throw new InvalidOperationException("KNX_IP_POINT_NOT_CONFIGURED");
        if (method == "write" && (!point.Writable || value is null)) throw new InvalidOperationException("KNX_IP_WRITE_NOT_ALLOWED");
        var connection = Volatile.Read(ref _active) ?? throw new IOException("KNX_IP_CONTROL_CHANNEL_UNAVAILABLE");
        return await connection.SendAsync(method, point, value, cancellationToken).ConfigureAwait(false);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { return; }
            try { await HandleConnectionAsync(client, cancellationToken).ConfigureAwait(false); }
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
        await using var connection = new RpcConnection(client, _points, OnObservation);
        using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var hello = await connection.ReadHandshakeAsync(handshakeTimeout.Token).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested || !IsValidHandshake(hello, Token)) return;
        lock (_sync)
        {
            if (cancellationToken.IsCancellationRequested) return;
            _active = connection;
        }
        await connection.WriteWelcomeAsync(cancellationToken).ConfigureAwait(false);
        try { await connection.ReadLoopAsync(cancellationToken).ConfigureAwait(false); }
        finally
        {
            lock (_sync) if (ReferenceEquals(_active, connection)) _active = null;
        }
    }

    private void OnObservation(KnxIpObservation observation)
    {
        if (ObservationReceived is not { } handlers) return;
        foreach (Action<KnxIpObservation> handler in handlers.GetInvocationList())
        {
            try { handler(observation); }
            catch { }
        }
    }

    private static bool IsValidHandshake(JsonDocument hello, string expectedToken)
    {
        var root = hello.RootElement;
        if (!TryString(root, "type", out var type) || type != "hello" ||
            !TryString(root, "token", out var token) ||
            !TryString(root, "artifactId", out var artifactId) || artifactId != "elitescada.knx.ip" ||
            !TryString(root, "artifactVersion", out var artifactVersion) || artifactVersion != "1.0.0" ||
            !TryString(root, "runtimeVersion", out var runtimeVersion) || runtimeVersion != "3.13.x" ||
            !root.TryGetProperty("schemaVersion", out var schemaVersion) || !schemaVersion.TryGetInt32(out var schema) || schema != 1)
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expectedToken));
    }

    private static bool TryString(JsonElement root, string property, out string value)
    {
        value = string.Empty;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var element) &&
               element.ValueKind == JsonValueKind.String && (value = element.GetString() ?? string.Empty).Length > 0;
    }

    private sealed class RpcConnection : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly IReadOnlyDictionary<Guid, KnxIpPoint> _points;
        private readonly Action<KnxIpObservation> _onObservation;
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private readonly ConcurrentDictionary<string, PendingRequest> _pending = new(StringComparer.Ordinal);
        private int _disposed;

        public RpcConnection(TcpClient client, IReadOnlyDictionary<Guid, KnxIpPoint> points, Action<KnxIpObservation> onObservation)
        {
            _client = client;
            _stream = client.GetStream();
            _reader = new StreamReader(_stream, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = false };
            _points = points;
            _onObservation = onObservation;
        }

        public async ValueTask<JsonDocument> ReadHandshakeAsync(CancellationToken cancellationToken)
        {
            var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? throw new IOException("KNX/IP sidecar closed before authentication.");
            if (line.Length > MaximumFrameLength) throw new IOException("KNX/IP sidecar handshake exceeded the frame limit.");
            return JsonDocument.Parse(line);
        }

        public ValueTask WriteWelcomeAsync(CancellationToken cancellationToken) => WriteJsonAsync(new { type = "welcome", schemaVersion = 1 }, cancellationToken);

        public async Task<object> SendAsync(string method, KnxIpPoint point, object? value, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid().ToString("N");
            var completion = new PendingRequest(method == "write",
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously));
            if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("KNX_IP_REQUEST_ID_COLLISION");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                await WriteJsonAsync(new { type = "request", id, method, tagId = point.Tag.Id, value }, timeout.Token).ConfigureAwait(false);
                try { return await completion.Completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("KNX/IP protected group value operation timed out."); }
            }
            finally { _pending.TryRemove(id, out _); }
        }

        public async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                    if (line is null) break;
                    if (line.Length > MaximumFrameLength) throw new IOException("KNX/IP sidecar frame exceeded the supported limit.");
                    using var message = JsonDocument.Parse(line);
                    var root = message.RootElement;
                    if (!TryString(root, "type", out var type)) throw new JsonException("KNX/IP sidecar message type is missing.");
                    if (type == "response") HandleResponse(root);
                    else if (type == "report") HandleReport(root);
                    else throw new JsonException("KNX/IP sidecar message type is not allowed.");
                }
            }
            finally { FailPending(new IOException("KNX/IP sidecar control connection closed.")); }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
            FailPending(new IOException("KNX/IP sidecar control connection was stopped."));
            _client.Dispose();
            _reader.Dispose();
            _writer.Dispose();
            _writeGate.Dispose();
            return ValueTask.CompletedTask;
        }

        private async ValueTask WriteJsonAsync<T>(T value, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(value);
            if (json.Length > MaximumFrameLength) throw new InvalidOperationException("KNX/IP control request exceeded the frame limit.");
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally { _writeGate.Release(); }
        }

        private void HandleResponse(JsonElement root)
        {
            if (!TryString(root, "id", out var id) || !_pending.TryRemove(id, out var pending)) return;
            var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
            if (!ok)
            {
                var errorCode = TryString(root, "errorCode", out var code) && Regex.IsMatch(code, "^[A-Z0-9_]{1,96}$") ? code : "KNX_IP_OPERATION_FAILED";
                pending.Completion.TrySetException(new InvalidOperationException(errorCode));
                return;
            }
            if (pending.RequiresAcceptedWrite &&
                (!root.TryGetProperty("accepted", out var accepted) || accepted.ValueKind != JsonValueKind.True))
            {
                pending.Completion.TrySetException(new InvalidOperationException("KNX_IP_WRITE_NOT_ACCEPTED"));
                return;
            }
            if (!root.TryGetProperty("value", out var valueElement) || !TryParseValue(valueElement, out var value))
            {
                pending.Completion.TrySetException(new InvalidOperationException("KNX_IP_READBACK_MISSING_OR_INVALID"));
                return;
            }
            pending.Completion.TrySetResult(value);
        }

        private void HandleReport(JsonElement root)
        {
            if (!TryString(root, "tagId", out var tagIdText) || !Guid.TryParseExact(tagIdText, "D", out var tagId) ||
                !_points.TryGetValue(tagId, out var point) ||
                !TryString(root, "source", out var source) || source != "data-secure-status" ||
                !root.TryGetProperty("secure", out var secure) || secure.ValueKind != JsonValueKind.True ||
                !root.TryGetProperty("value", out var valueElement) || !TryParseValue(valueElement, out var value) || !MatchesType(point, value))
                return;
            var observedAt = root.TryGetProperty("observedAt", out var timeElement) && timeElement.ValueKind == JsonValueKind.String &&
                            DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
                ? time : DateTimeOffset.UtcNow;
            _onObservation(new KnxIpObservation(tagId, value, observedAt));
        }

        private static bool TryParseValue(JsonElement element, out object value)
        {
            value = false;
            if (element.ValueKind is JsonValueKind.True or JsonValueKind.False) { value = element.GetBoolean(); return true; }
            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var number) && double.IsFinite(number)) { value = number; return true; }
            return false;
        }

        private static bool MatchesType(KnxIpPoint point, object value) =>
            point.Dpt is "1.001" or "1.002" ? value is bool : value is double;

        private void FailPending(Exception exception)
        {
            foreach (var pair in _pending.ToArray())
                if (_pending.TryRemove(pair.Key, out var pending)) pending.Completion.TrySetException(exception);
        }

        private sealed record PendingRequest(bool RequiresAcceptedWrite, TaskCompletionSource<object> Completion);
    }
}

public sealed class KnxIpManagedSidecarSession : IKnxIpSidecarSession
{
    private readonly ManagedSidecarRuntimeBinding _binding;
    private readonly KnxIpRpcServer _rpc;
    private readonly IReadOnlyDictionary<Guid, KnxIpPoint> _points;
    private int _disposed;

    public KnxIpManagedSidecarSession(ManagedSidecarRuntimeBinding binding, KnxIpRpcServer rpc, IReadOnlyCollection<KnxIpPoint> points)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
        _points = points.ToDictionary(point => point.Tag.Id);
        _rpc.ObservationReceived += OnObservation;
    }

    public event Action<KnxIpObservation>? ObservationReceived;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _rpc.Start();
        try { await _binding.StartAsync(cancellationToken).ConfigureAwait(false); }
        catch { await _rpc.StopAsync().ConfigureAwait(false); throw; }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        Exception? failure = null;
        try { await _binding.StopAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { failure = exception; }
        try { await _rpc.StopAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) throw failure;
    }

    public ValueTask<object> ReadAsync(KnxIpPoint point, CancellationToken cancellationToken = default)
    {
        EnsurePoint(point);
        return _rpc.ReadAsync(point, cancellationToken);
    }

    public ValueTask<object> WriteAndReadBackAsync(KnxIpPoint point, object value, CancellationToken cancellationToken = default)
    {
        EnsurePoint(point);
        return _rpc.WriteAndReadBackAsync(point, value, cancellationToken);
    }

    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => _binding.GetCommunicationDiagnostics();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _rpc.ObservationReceived -= OnObservation;
        Exception? failure = null;
        try { await StopAsync().ConfigureAwait(false); } catch (Exception exception) { failure = exception; }
        try { await _binding.DisposeAsync().ConfigureAwait(false); } catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        try { await _rpc.DisposeAsync().ConfigureAwait(false); } catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        if (failure is not null) throw failure;
    }

    private void OnObservation(KnxIpObservation observation)
    {
        if (!_points.ContainsKey(observation.TagId)) return;
        if (ObservationReceived is not { } handlers) return;
        foreach (Action<KnxIpObservation> handler in handlers.GetInvocationList())
        {
            try { handler(observation); }
            catch { }
        }
    }

    private void EnsurePoint(KnxIpPoint point)
    {
        if (!_points.TryGetValue(point.Tag.Id, out var configured) || configured != point)
            throw new InvalidOperationException("KNX_IP_POINT_NOT_CONFIGURED");
    }
}
