using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Scada.Drivers.HomeAssistant;

public sealed class HomeAssistantAuthenticationException : IOException
{
    public HomeAssistantAuthenticationException(string message) : base(message) { }
}

public interface IHomeAssistantClient : IAsyncDisposable
{
    bool Connected { get; }
    bool Authenticated { get; }
    string? HomeAssistantVersion { get; }
    ValueTask ConnectAndAuthenticateAsync(ReadOnlyMemory<byte> accessToken, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<HomeAssistantState>> GetStatesAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<HomeAssistantRegistryDisplayEntry>> GetEntityRegistryForDisplayAsync(CancellationToken cancellationToken = default);
    ValueTask<int> SubscribeStateChangedAsync(CancellationToken cancellationToken = default);
    ValueTask CallServiceAsync(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? serviceData = null,
        CancellationToken cancellationToken = default);
    ValueTask<HomeAssistantStateChangedEvent> ReceiveStateChangedAsync(CancellationToken cancellationToken = default);
}

public sealed class HomeAssistantWebSocketClient : IHomeAssistantClient
{
    private readonly HomeAssistantConnectionSettings _settings;
    private readonly ClientWebSocket _socket = new();
    private readonly HomeAssistantRequestCorrelator _correlator = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly Channel<HomeAssistantStateChangedEvent> _stateEvents =
        Channel.CreateBounded<HomeAssistantStateChangedEvent>(
            new BoundedChannelOptions(HomeAssistantContract.StateEventQueueCapacity)
            {
                SingleReader = false,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
    private CancellationTokenSource? _receivePumpCts;
    private Task? _receivePumpTask;
    private bool _disposed;

    public HomeAssistantWebSocketClient(HomeAssistantConnectionSettings settings)
    {
        _settings = settings;
        _settings.Validate();
    }

    public bool Connected => _socket.State == WebSocketState.Open;
    public bool Authenticated { get; private set; }
    public string? HomeAssistantVersion { get; private set; }

    public async ValueTask ConnectAndAuthenticateAsync(
        ReadOnlyMemory<byte> accessToken,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (Authenticated) return;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveConnectTimeout);
        await _socket.ConnectAsync(_settings.WebSocketUri, timeout.Token).ConfigureAwait(false);

        var required = HomeAssistantProtocol.ParseHandshake(await ReceiveFrameAsync(timeout.Token).ConfigureAwait(false));
        if (required.Kind != HomeAssistantHandshakeKind.AuthRequired)
            throw new HomeAssistantAuthenticationException("Home Assistant did not request authentication.");
        HomeAssistantVersion = required.Version;

        await SendAsync(HomeAssistantProtocol.BuildAuth(accessToken.Span), timeout.Token).ConfigureAwait(false);
        var auth = HomeAssistantProtocol.ParseHandshake(await ReceiveFrameAsync(timeout.Token).ConfigureAwait(false));
        HomeAssistantVersion = auth.Version ?? HomeAssistantVersion;
        if (auth.Kind == HomeAssistantHandshakeKind.AuthInvalid)
            throw new HomeAssistantAuthenticationException("Home Assistant authentication failed.");
        if (auth.Kind != HomeAssistantHandshakeKind.AuthOk)
            throw new HomeAssistantAuthenticationException("Home Assistant authentication did not complete.");

        Authenticated = true;
        _receivePumpCts = new CancellationTokenSource();
        _receivePumpTask = ReceivePumpAsync(_receivePumpCts.Token);
    }

    public async ValueTask<IReadOnlyList<HomeAssistantState>> GetStatesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync("get_states", null, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant get_states failed.");
        return HomeAssistantProtocol.ParseStates(result.Result
            ?? throw new FormatException("Home Assistant get_states returned no result."));
    }

    public async ValueTask<IReadOnlyList<HomeAssistantRegistryDisplayEntry>> GetEntityRegistryForDisplayAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync(
            "config/entity_registry/list_for_display",
            null,
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant entity registry display inventory failed.");
        return HomeAssistantRegistryDisplayParser.Parse(result.Result
            ?? throw new FormatException("Home Assistant entity registry display inventory returned no result."));
    }

    public async ValueTask<int> SubscribeStateChangedAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendCommandAsync(
            "subscribe_events",
            new Dictionary<string, object?> { ["event_type"] = "state_changed" },
            cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant state_changed subscription failed.");
        return result.Id;
    }

    public async ValueTask CallServiceAsync(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? serviceData = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Home Assistant service domain is required.", nameof(domain));
        if (string.IsNullOrWhiteSpace(service)) throw new ArgumentException("Home Assistant service name is required.", nameof(service));
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("Home Assistant target entity id is required.", nameof(entityId));

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["domain"] = domain,
            ["service"] = service,
            ["target"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["entity_id"] = entityId
            }
        };
        if (serviceData is not null && serviceData.Count > 0)
            arguments["service_data"] = serviceData;

        var result = await SendCommandAsync("call_service", arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new IOException("Home Assistant service action failed.");
    }

    public async ValueTask<HomeAssistantStateChangedEvent> ReceiveStateChangedAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await _stateEvents.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<HomeAssistantCommandResult> SendCommandAsync(
        string type,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!Authenticated)
            throw new InvalidOperationException("Home Assistant client is not authenticated.");

        var pending = _correlator.Create();
        try
        {
            await SendAsync(HomeAssistantProtocol.BuildCommand(pending.Id, type, arguments), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _correlator.TryFail(pending.Id, ex);
            throw;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.EffectiveRequestTimeout);
        try
        {
            return await pending.Completion.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var failure = new TimeoutException(
                $"Home Assistant request timed out after {_settings.EffectiveRequestTimeout}.");
            _correlator.TryFail(pending.Id, failure);
            throw failure;
        }
        catch
        {
            _correlator.TryCancel(pending.Id, cancellationToken);
            throw;
        }
    }

    private async Task ReceivePumpAsync(CancellationToken cancellationToken)
    {
        Exception? terminal = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var frame = await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false);

                if (HomeAssistantProtocol.TryParseStateChangedEvent(frame, out var changed) && changed is not null)
                {
                    await _stateEvents.Writer.WriteAsync(changed, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    var result = HomeAssistantProtocol.ParseResult(frame);
                    _correlator.TryComplete(result);
                }
                catch (FormatException)
                {
                    // Other valid Home Assistant event/result-independent message.
                    // This bridge only consumes state_changed and command results.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            terminal = ex;
            _correlator.FailAll(ex);
        }
        finally
        {
            _stateEvents.Writer.TryComplete(terminal);
        }
    }

    private async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async ValueTask<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new IOException("Home Assistant WebSocket closed.");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new IOException("Home Assistant returned a non-text WebSocket message.");
            stream.Write(buffer, 0, result.Count);
            if (stream.Length > HomeAssistantContract.MaximumFrameBytes)
                throw new IOException("Home Assistant WebSocket message exceeded the maximum allowed size.");
            if (result.EndOfMessage)
                return stream.ToArray();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Authenticated = false;

        var pumpCts = _receivePumpCts;
        _receivePumpCts = null;
        if (pumpCts is not null)
            pumpCts.Cancel();

        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "stop", CancellationToken.None).ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
        }

        if (_receivePumpTask is not null)
        {
            try { await _receivePumpTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            _receivePumpTask = null;
        }

        pumpCts?.Dispose();
        _correlator.FailAll(new ObjectDisposedException(nameof(HomeAssistantWebSocketClient)));
        _stateEvents.Writer.TryComplete();
        _socket.Dispose();
        _sendGate.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
