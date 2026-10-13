using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scada.Core.Tags;
using Scada.DriverHost.Engineering;
using Scada.Drivers.Abstractions;

namespace Scada.DriverHost.Sidecars;

public sealed record ZWaveJsPoint(
    TagDefinition Tag,
    string PortableAddress,
    int NodeId,
    int Endpoint,
    int CommandClass,
    object Property,
    object? PropertyKey,
    bool Writable);

public sealed record ZWaveJsObservation(Guid TagId, JsonElement Value, DateTimeOffset ObservedAt);

public interface IZWaveJsSidecarSession : IAsyncDisposable
{
    event Action<ZWaveJsObservation>? ObservationReceived;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    ValueTask<JsonElement> ReadAsync(ZWaveJsPoint point, CancellationToken cancellationToken = default);
    ValueTask<JsonElement> WriteAndReadBackAsync(ZWaveJsPoint point, object value, CancellationToken cancellationToken = default);
    CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics();
}

/// <summary>Authenticated, bounded JSON-lines channel from the managed local Z-Wave child.</summary>
public sealed class ZWaveJsRpcServer : IAsyncDisposable
{
    private const int MaximumFrameLength = 65_536;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(45);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcReply>> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _sync = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptTask;
    private RpcConnection? _active;
    private string? _token;
    private int _port;
    private int _disposed;

    public event Action<ZWaveJsObservation>? ObservationReceived;
    public string Token => Volatile.Read(ref _token) ?? throw new InvalidOperationException("Z-Wave RPC is not started.");
    public int Port => Volatile.Read(ref _port) is var port && port > 0 ? port : throw new InvalidOperationException("Z-Wave RPC is not started.");
    public bool IsConnected => Volatile.Read(ref _active) is not null;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_sync)
        {
            if (_listener is not null) throw new InvalidOperationException("Z-Wave RPC server is already started.");
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

    public ValueTask<JsonElement> ReadAsync(ZWaveJsPoint point, CancellationToken cancellationToken = default) =>
        SendAsync("read", point, null, cancellationToken);

    public ValueTask<JsonElement> WriteAsync(ZWaveJsPoint point, object value, CancellationToken cancellationToken = default) =>
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
        FailPending(new IOException("Z-Wave sidecar channel stopped."));
        if (acceptTask is not null)
        {
            try { await acceptTask.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (lifetime?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) when (lifetime?.IsCancellationRequested == true) { }
        }
        lifetime?.Dispose();
    }

    private async ValueTask<JsonElement> SendAsync(string operation, ZWaveJsPoint point, object? value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(point);
        var connection = Volatile.Read(ref _active) ?? throw new IOException("Z-Wave sidecar is not connected.");
        var id = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<RpcReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("Duplicate Z-Wave RPC request ID.");
        var dispatchAttempted = false;
        var definitiveReplyReceived = false;
        try
        {
            var frame = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id,
                op = operation,
                tagId = point.Tag.Id,
                address = point.PortableAddress,
                dataType = point.Tag.DataType.ToString(),
                writable = point.Writable && !point.Tag.ReadOnly,
                value
            }, JsonOptions);
            dispatchAttempted = true;
            try { await WriteFrameAsync(connection.Stream, frame, cancellationToken).ConfigureAwait(false); }
            finally { CryptographicOperations.ZeroMemory(frame); }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OperationTimeout);
            RpcReply reply;
            try { reply = await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Z-Wave process-value operation timed out without replay.");
            }
            if (!reply.Success)
            {
                if (operation == "write" && reply.Uncertain)
                    throw new ZWaveJsWriteUnconfirmedException(point.Tag.Id, reply.ErrorCode);
                definitiveReplyReceived = true;
                throw new IOException($"Z-Wave sidecar operation failed ({SafeErrorCode(reply.ErrorCode)}).");
            }
            return reply.Value?.Clone() ?? throw new InvalidDataException("Z-Wave sidecar returned no process value.");
        }
        catch (ZWaveJsWriteUnconfirmedException) { throw; }
        catch (Exception exception) when (operation == "write" && dispatchAttempted && !definitiveReplyReceived &&
                                          exception is IOException or TimeoutException or OperationCanceledException or InvalidDataException)
        {
            throw new ZWaveJsWriteUnconfirmedException(point.Tag.Id, "RPC_OUTCOME_UNKNOWN", exception);
        }
        finally { _pending.TryRemove(id, out _); }
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
            client.NoDelay = true;
            var connection = new RpcConnection(client);
            try
            {
                if (!await AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false))
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                RpcConnection? previous;
                lock (_sync)
                {
                    previous = _active;
                    _active = connection;
                }
                if (previous is not null) await previous.DisposeAsync().ConfigureAwait(false);
                await ReadLoopAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidDataException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (JsonException) { }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_active, connection)) _active = null;
                }
                await connection.DisposeAsync().ConfigureAwait(false);
                FailPending(new IOException("Z-Wave sidecar disconnected; pending operations were not retried."));
            }
        }
    }

    private async Task<bool> AuthenticateAsync(RpcConnection connection, CancellationToken cancellationToken)
    {
        var frame = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
        if (frame is null) return false;
        using var document = JsonDocument.Parse(frame);
        var root = document.RootElement;
        if (!TryString(root, "type", out var type) || type != "hello" ||
            !TryInt(root, "protocolVersion", out var protocolVersion) || protocolVersion != 1 ||
            !TryString(root, "token", out var suppliedToken)) return false;
        var expected = Encoding.UTF8.GetBytes(Token);
        var supplied = Encoding.UTF8.GetBytes(suppliedToken);
        try
        {
            if (expected.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(expected, supplied)) return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(supplied);
        }
        await WriteFrameAsync(connection.Stream,
            JsonSerializer.SerializeToUtf8Bytes(new { type = "welcome", protocolVersion = 1 }, JsonOptions), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task ReadLoopAsync(RpcConnection connection, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (frame is null) return;
            using var document = JsonDocument.Parse(frame);
            var root = document.RootElement;
            if (TryString(root, "type", out var type) && type == "observation")
            {
                if (TryGuid(root, "tagId", out var tagId) && root.TryGetProperty("value", out var value) &&
                    TryDateTimeOffset(root, "observedAt", out var observedAt))
                {
                    ObservationReceived?.Invoke(new ZWaveJsObservation(tagId, value.Clone(), observedAt));
                }
                continue;
            }
            if (!TryString(root, "id", out var id) || !_pending.TryGetValue(id, out var completion)) continue;
            var success = root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
            var uncertain = root.TryGetProperty("uncertain", out var uncertainElement) && uncertainElement.ValueKind == JsonValueKind.True;
            var errorCode = TryString(root, "errorCode", out var code) ? SafeErrorCode(code) : null;
            JsonElement? responseValue = root.TryGetProperty("value", out var result) ? result.Clone() : null;
            completion.TrySetResult(new RpcReply(success, uncertain, errorCode, responseValue));
        }
    }

    private async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        if (payload.Length is 0 or > MaximumFrameLength) throw new InvalidDataException("Z-Wave RPC frame length is invalid.");
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    private void FailPending(Exception exception)
    {
        foreach (var pending in _pending.Values) pending.TrySetException(exception);
        _pending.Clear();
    }

    private static bool TryString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString() ?? string.Empty;
        return value.Length <= 1024;
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        value = 0;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var element) && element.TryGetInt32(out value);
    }

    private static bool TryGuid(JsonElement root, string name, out Guid value)
    {
        value = Guid.Empty;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String && element.TryGetGuid(out value);
    }

    private static bool TryDateTimeOffset(JsonElement root, string name, out DateTimeOffset value)
    {
        value = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var element) && element.TryGetDateTimeOffset(out value);
    }

    private static string SafeErrorCode(string? value) => value is { Length: > 0 and <= 96 } && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
        ? value : "SIDECAR_OPERATION_FAILED";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await StopAsync().ConfigureAwait(false);
        _writeGate.Dispose();
    }

    private sealed record RpcReply(bool Success, bool Uncertain, string? ErrorCode, JsonElement? Value);

    private sealed class RpcConnection(TcpClient client) : IAsyncDisposable
    {
        private readonly FrameReader _reader = new(client.GetStream(), MaximumFrameLength);
        public NetworkStream Stream { get; } = client.GetStream();
        public ValueTask<byte[]?> ReadFrameAsync(CancellationToken cancellationToken) => _reader.ReadAsync(cancellationToken);
        public ValueTask DisposeAsync()
        {
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FrameReader(Stream stream, int maximumLength)
    {
        private readonly byte[] _buffer = new byte[4096];
        private int _offset;
        private int _count;

        public async ValueTask<byte[]?> ReadAsync(CancellationToken cancellationToken)
        {
            var frame = new ArrayBufferWriter<byte>();
            while (true)
            {
                if (_offset == _count)
                {
                    _count = await stream.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                    _offset = 0;
                    if (_count == 0) return frame.WrittenCount == 0 ? null : throw new EndOfStreamException("Truncated Z-Wave RPC frame.");
                }
                var newline = Array.IndexOf(_buffer, (byte)'\n', _offset, _count - _offset);
                var take = newline >= 0 ? newline - _offset : _count - _offset;
                if (take > 0)
                {
                    if (frame.WrittenCount + take > maximumLength) throw new InvalidDataException("Z-Wave RPC frame exceeds the supported limit.");
                    frame.Write(_buffer.AsSpan(_offset, take));
                    _offset += take;
                }
                if (newline >= 0)
                {
                    _offset++;
                    if (frame.WrittenCount == 0) throw new InvalidDataException("Z-Wave RPC frame terminator is invalid.");
                    if (frame.WrittenSpan[^1] == (byte)'\r') return frame.WrittenSpan[..^1].ToArray();
                    return frame.WrittenSpan.ToArray();
                }
                if (frame.WrittenCount > maximumLength) throw new InvalidDataException("Z-Wave RPC frame exceeds the supported limit.");
            }
        }
    }
}

public sealed class ZWaveJsManagedSidecarSession : IZWaveJsSidecarSession
{
    private readonly ManagedSidecarRuntimeBinding _binding;
    private readonly ZWaveJsRpcServer _rpc;
    private int _started;
    private int _disposed;

    public ZWaveJsManagedSidecarSession(ManagedSidecarRuntimeBinding binding, ZWaveJsRpcServer rpc)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
        _rpc.ObservationReceived += ForwardObservation;
    }

    public event Action<ZWaveJsObservation>? ObservationReceived;
    public ValueTask<JsonElement> ReadAsync(ZWaveJsPoint point, CancellationToken cancellationToken = default) => _rpc.ReadAsync(point, cancellationToken);
    public ValueTask<JsonElement> WriteAndReadBackAsync(ZWaveJsPoint point, object value, CancellationToken cancellationToken = default) => _rpc.WriteAsync(point, value, cancellationToken);
    public CommunicationDriverDiagnosticSnapshot GetCommunicationDiagnostics() => _binding.GetCommunicationDiagnostics();

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0) throw new InvalidOperationException("Z-Wave sidecar session is already started.");
        _rpc.Start();
        try { await _binding.StartAsync(cancellationToken).ConfigureAwait(false); }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            await _rpc.StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0) return;
        try { await _binding.StopAsync(cancellationToken).ConfigureAwait(false); }
        finally { await _rpc.StopAsync(CancellationToken.None).ConfigureAwait(false); }
    }

    private void ForwardObservation(ZWaveJsObservation observation) => ObservationReceived?.Invoke(observation);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _rpc.ObservationReceived -= ForwardObservation;
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            await _binding.DisposeAsync().ConfigureAwait(false);
            await _rpc.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class ZWaveJsWriteUnconfirmedException(Guid tagId, string? errorCode, Exception? innerException = null)
    : IOException($"Z-Wave write for TAG '{tagId}' was not confirmed by a fresh process-value report ({errorCode ?? "UNCONFIRMED"}).", innerException)
{
    public Guid TagId { get; } = tagId;
    public string ErrorCode { get; } = errorCode ?? "UNCONFIRMED";
}
