using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using Google.Protobuf;
using Scada.Drivers.ESPHome.Protocol;
using Scada.Drivers.ESPHome.Protocol.Generated;

namespace Scada.Drivers.ESPHome;

public static class EspHomeEntityMessageId
{
    public const ushort ListEntitiesRequest = 11;
    public const ushort BinarySensorInfo = 12;
    public const ushort CoverInfo = 13;
    public const ushort FanInfo = 14;
    public const ushort LightInfo = 15;
    public const ushort SensorInfo = 16;
    public const ushort SwitchInfo = 17;
    public const ushort TextSensorInfo = 18;
    public const ushort ListEntitiesDone = 19;
    public const ushort SubscribeStates = 20;
    public const ushort BinarySensorState = 21;
    public const ushort CoverState = 22;
    public const ushort FanState = 23;
    public const ushort LightState = 24;
    public const ushort SensorState = 25;
    public const ushort SwitchState = 26;
    public const ushort TextSensorState = 27;
    public const ushort CoverCommand = 30;
    public const ushort FanCommand = 31;
    public const ushort LightCommand = 32;
    public const ushort SwitchCommand = 33;
    public const ushort ClimateInfo = 46;
    public const ushort ClimateState = 47;
    public const ushort NumberInfo = 49;
    public const ushort NumberState = 50;
    public const ushort NumberCommand = 51;
    public const ushort SelectInfo = 52;
    public const ushort SelectState = 53;
    public const ushort SelectCommand = 54;
    public const ushort LockInfo = 58;
    public const ushort LockState = 59;
    public const ushort ButtonInfo = 61;
    public const ushort ButtonCommand = 62;
}

public sealed class EspHomeNativeClient : IEspHomeNativeClient
{
    private readonly EspHomeConnectionSettings _settings;
    private readonly EspHomeNoiseKeyProvider _keyProvider;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Queue<EspHomeStateUpdate> _pendingStates = new();
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private EspHomeNoiseSession? _noise;
    private EspHomeNativeInventory? _inventory;
    private bool _disposed;

    public EspHomeNativeClient(
        EspHomeConnectionSettings settings,
        EspHomeNoiseKeyProvider keyProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    }

    public bool Connected => _tcp?.Connected == true && _stream is not null;

    public async ValueTask<EspHomeNativeInventory> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_inventory is not null && Connected) return _inventory;
        _settings.Validate();

        var tcp = new TcpClient();
        using (var connectCts = CreateOperationToken(cancellationToken))
            await tcp.ConnectAsync(_settings.Host, _settings.Port, connectCts.Token).ConfigureAwait(false);

        _tcp = tcp;
        _stream = tcp.GetStream();

        try
        {
            if (_settings.EncryptionMode == EspHomeNativeEncryptionMode.Noise)
                await EstablishNoiseAsync(cancellationToken).ConfigureAwait(false);

            await SendMessageAsync(
                EspHomeNativeMessageId.HelloRequest,
                EspHomeNativeHandshake.CreateHello("EliteSCADA ESPHome Native"),
                cancellationToken).ConfigureAwait(false);
            var hello = HelloResponse.Parser.ParseFrom(
                (await ReadExpectedAsync(EspHomeNativeMessageId.HelloResponse, cancellationToken).ConfigureAwait(false)).Payload.Span);
            var version = EspHomeNativeHandshake.Negotiate(hello);

            await SendMessageAsync(
                EspHomeNativeMessageId.DeviceInfoRequest,
                new DeviceInfoRequest(),
                cancellationToken).ConfigureAwait(false);
            var deviceInfo = DeviceInfoResponse.Parser.ParseFrom(
                (await ReadExpectedAsync(EspHomeNativeMessageId.DeviceInfoResponse, cancellationToken).ConfigureAwait(false)).Payload.Span);
            var device = EspHomeDeviceInfoMapper.Map(deviceInfo);

            var entities = new List<EspHomeEntityDescriptor>();
            var unsupported = 0;
            await SendMessageAsync(EspHomeEntityMessageId.ListEntitiesRequest, new ListEntitiesRequest(), cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var frame = await ReadMessageWithTimeoutAsync(cancellationToken).ConfigureAwait(false);
                if (frame.MessageType == EspHomeEntityMessageId.ListEntitiesDone)
                    break;

                if (TryParseEntity(frame, out var entity))
                    entities.Add(entity!);
                else
                    unsupported++;
            }

            _inventory = new EspHomeNativeInventory(version, device, entities, unsupported);
            return _inventory;
        }
        catch
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public ValueTask SubscribeStatesAsync(CancellationToken cancellationToken = default) =>
        SendMessageAsync(EspHomeEntityMessageId.SubscribeStates, new SubscribeStatesRequest(), cancellationToken);

    public async ValueTask<EspHomeStateUpdate> ReceiveStateAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        while (true)
        {
            if (_pendingStates.Count > 0)
                return _pendingStates.Dequeue();

            var frame = await ReadMessageAsync(cancellationToken).ConfigureAwait(false);
            foreach (var update in ParseState(frame))
                _pendingStates.Enqueue(update);
        }
    }

    public async ValueTask SendCommandAsync(EspHomeCommand command, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var address = command.Address;
        switch (command.Kind)
        {
            case EspHomeWriteKind.SwitchState:
                await SendMessageAsync(EspHomeEntityMessageId.SwitchCommand, new SwitchCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    State = Convert.ToBoolean(command.Value, CultureInfo.InvariantCulture)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.LightState:
                await SendMessageAsync(EspHomeEntityMessageId.LightCommand, new LightCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    HasState = true,
                    State = Convert.ToBoolean(command.Value, CultureInfo.InvariantCulture)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.LightBrightness:
                await SendMessageAsync(EspHomeEntityMessageId.LightCommand, new LightCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    HasBrightness = true,
                    Brightness = NormalizePercent(command.Value)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.CoverPosition:
                await SendMessageAsync(EspHomeEntityMessageId.CoverCommand, new CoverCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    HasPosition = true,
                    Position = NormalizePercent(command.Value)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.FanState:
                await SendMessageAsync(EspHomeEntityMessageId.FanCommand, new FanCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    HasState = true,
                    State = Convert.ToBoolean(command.Value, CultureInfo.InvariantCulture)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.FanSpeedLevel:
                await SendMessageAsync(EspHomeEntityMessageId.FanCommand, new FanCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    HasSpeedLevel = true,
                    SpeedLevel = Convert.ToInt32(command.Value, CultureInfo.InvariantCulture)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.NumberState:
                await SendMessageAsync(EspHomeEntityMessageId.NumberCommand, new NumberCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    State = Convert.ToSingle(command.Value, CultureInfo.InvariantCulture)
                }, cancellationToken).ConfigureAwait(false);
                break;
            case EspHomeWriteKind.SelectState:
                await SendMessageAsync(EspHomeEntityMessageId.SelectCommand, new SelectCommandRequest
                {
                    Key = address.Key,
                    DeviceId = address.DeviceId,
                    State = Convert.ToString(command.Value, CultureInfo.InvariantCulture) ?? string.Empty
                }, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException($"ESPHome write kind '{command.Kind}' is not supported.");
        }
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _inventory = null;
        _pendingStates.Clear();
        _noise?.Dispose();
        _noise = null;
        _stream?.Dispose();
        _stream = null;
        _tcp?.Dispose();
        _tcp = null;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        _writeGate.Dispose();
        _disposed = true;
    }

    private async Task EstablishNoiseAsync(CancellationToken cancellationToken)
    {
        var key = await _keyProvider(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("ESPHome Noise mode requires a protected encryption key.");
        using (key)
        {
            _noise = new EspHomeNoiseSession(key);
            var clientFrames = _noise.CreateClientHandshakeFrames();
            await _stream!.WriteAsync(clientFrames, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var serverHello = await ReadNoiseOuterFrameWithTimeoutAsync(cancellationToken).ConfigureAwait(false);
            _ = _noise.AcceptServerHello(serverHello);
            var serverHandshake = await ReadNoiseOuterFrameWithTimeoutAsync(cancellationToken).ConfigureAwait(false);
            _noise.CompleteHandshake(serverHandshake);
        }
    }

    private async ValueTask SendMessageAsync(
        ushort messageType,
        IMessage message,
        CancellationToken cancellationToken)
    {
        if (_stream is null) throw new InvalidOperationException("ESPHome client is not connected.");
        byte[] frame = _noise is null
            ? EspHomePlaintextFrameCodec.Encode(messageType, message)
            : _noise.EncryptFrame(messageType, message.ToByteArray());

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async ValueTask<EspHomePlaintextFrame> ReadExpectedAsync(
        ushort expectedType,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var frame = await ReadMessageWithTimeoutAsync(cancellationToken).ConfigureAwait(false);
            if (frame.MessageType == expectedType)
                return frame;
        }
    }

    private async ValueTask<EspHomePlaintextFrame> ReadMessageWithTimeoutAsync(CancellationToken cancellationToken)
    {
        using var cts = CreateOperationToken(cancellationToken);
        return await ReadMessageAsync(cts.Token).ConfigureAwait(false);
    }

    private async ValueTask<EspHomePlaintextFrame> ReadMessageAsync(CancellationToken cancellationToken)
    {
        if (_stream is null) throw new InvalidOperationException("ESPHome client is not connected.");
        if (_noise is not null)
        {
            var frame = await ReadNoiseOuterFrameAsync(cancellationToken).ConfigureAwait(false);
            var packet = _noise.DecryptFrame(frame);
            return new EspHomePlaintextFrame(packet.MessageType, packet.Payload);
        }

        var marker = await ReadByteAsync(_stream, cancellationToken).ConfigureAwait(false);
        if (marker != 0)
            throw new InvalidDataException($"ESPHome plaintext marker 0x{marker:X2} is invalid.");

        var payloadLength = await ReadVarUInt32Async(_stream, cancellationToken).ConfigureAwait(false);
        if (payloadLength > EspHomePlaintextFrameCodec.DefaultMaximumPayloadBytes)
            throw new InvalidDataException($"ESPHome payload length {payloadLength} exceeds configured maximum.");

        var messageType = await ReadVarUInt32Async(_stream, cancellationToken).ConfigureAwait(false);
        if (messageType > ushort.MaxValue)
            throw new InvalidDataException($"ESPHome message type {messageType} exceeds uint16 range.");

        var payload = new byte[checked((int)payloadLength)];
        await ReadExactlyAsync(_stream, payload, cancellationToken).ConfigureAwait(false);
        return new EspHomePlaintextFrame((ushort)messageType, payload);
    }

    private async ValueTask<byte[]> ReadNoiseOuterFrameWithTimeoutAsync(CancellationToken cancellationToken)
    {
        using var cts = CreateOperationToken(cancellationToken);
        return await ReadNoiseOuterFrameAsync(cts.Token).ConfigureAwait(false);
    }

    private async ValueTask<byte[]> ReadNoiseOuterFrameAsync(CancellationToken cancellationToken)
    {
        var header = new byte[3];
        await ReadExactlyAsync(_stream!, header, cancellationToken).ConfigureAwait(false);
        if (header[0] != EspHomeNoiseContract.FrameMarker)
            throw new InvalidDataException($"ESPHome Noise marker 0x{header[0]:X2} is invalid.");
        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1, 2));
        var frame = new byte[3 + length];
        header.CopyTo(frame, 0);
        await ReadExactlyAsync(_stream!, frame.AsMemory(3, length), cancellationToken).ConfigureAwait(false);
        return frame;
    }

    private static bool TryParseEntity(EspHomePlaintextFrame frame, out EspHomeEntityDescriptor? entity)
    {
        entity = frame.MessageType switch
        {
            EspHomeEntityMessageId.BinarySensorInfo => Binary(ListEntitiesBinarySensorResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.CoverInfo => Cover(ListEntitiesCoverResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.FanInfo => Fan(ListEntitiesFanResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.LightInfo => Light(ListEntitiesLightResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.SensorInfo => Sensor(ListEntitiesSensorResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.SwitchInfo => Switch(ListEntitiesSwitchResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.TextSensorInfo => Text(ListEntitiesTextSensorResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.NumberInfo => Number(ListEntitiesNumberResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.SelectInfo => Select(ListEntitiesSelectResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.ButtonInfo => Button(ListEntitiesButtonResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.ClimateInfo => Climate(ListEntitiesClimateResponse.Parser.ParseFrom(frame.Payload.Span)),
            EspHomeEntityMessageId.LockInfo => Lock(ListEntitiesLockResponse.Parser.ParseFrom(frame.Payload.Span)),
            _ => null
        };
        return entity is not null;
    }

    private static IEnumerable<EspHomeStateUpdate> ParseState(EspHomePlaintextFrame frame)
    {
        var now = DateTimeOffset.UtcNow;
        switch (frame.MessageType)
        {
            case EspHomeEntityMessageId.BinarySensorState:
            {
                var m = BinarySensorStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.BinarySensor, m.DeviceId, m.Key, "state", m.State, m.MissingState, now);
                break;
            }
            case EspHomeEntityMessageId.CoverState:
            {
                var m = CoverStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Cover, m.DeviceId, m.Key, "position", m.Position * 100d, false, now);
                yield return U(EspHomeEntityKind.Cover, m.DeviceId, m.Key, "operation", CoverOperationName(m.CurrentOperation), false, now);
                break;
            }
            case EspHomeEntityMessageId.FanState:
            {
                var m = FanStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Fan, m.DeviceId, m.Key, "state", m.State, false, now);
                yield return U(EspHomeEntityKind.Fan, m.DeviceId, m.Key, "speedLevel", m.SpeedLevel, false, now);
                break;
            }
            case EspHomeEntityMessageId.LightState:
            {
                var m = LightStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Light, m.DeviceId, m.Key, "state", m.State, false, now);
                yield return U(EspHomeEntityKind.Light, m.DeviceId, m.Key, "brightness", m.Brightness * 100d, false, now);
                yield return U(EspHomeEntityKind.Light, m.DeviceId, m.Key, "red", m.Red * 100d, false, now);
                yield return U(EspHomeEntityKind.Light, m.DeviceId, m.Key, "green", m.Green * 100d, false, now);
                yield return U(EspHomeEntityKind.Light, m.DeviceId, m.Key, "blue", m.Blue * 100d, false, now);
                break;
            }
            case EspHomeEntityMessageId.SensorState:
            {
                var m = SensorStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Sensor, m.DeviceId, m.Key, "state", (double)m.State, m.MissingState, now);
                break;
            }
            case EspHomeEntityMessageId.SwitchState:
            {
                var m = SwitchStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Switch, m.DeviceId, m.Key, "state", m.State, m.MissingState, now);
                break;
            }
            case EspHomeEntityMessageId.TextSensorState:
            {
                var m = TextSensorStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.TextSensor, m.DeviceId, m.Key, "state", m.State, m.MissingState, now);
                break;
            }
            case EspHomeEntityMessageId.NumberState:
            {
                var m = NumberStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Number, m.DeviceId, m.Key, "state", (double)m.State, m.MissingState, now);
                break;
            }
            case EspHomeEntityMessageId.SelectState:
            {
                var m = SelectStateResponse.Parser.ParseFrom(frame.Payload.Span);
                yield return U(EspHomeEntityKind.Select, m.DeviceId, m.Key, "state", m.State, m.MissingState, now);
                break;
            }
        }
    }

    private static EspHomeStateUpdate U(
        EspHomeEntityKind kind,
        uint deviceId,
        uint key,
        string field,
        object? value,
        bool missing,
        DateTimeOffset timestamp) =>
        new(new EspHomeEntityAddress(kind, deviceId, key, field), value, missing, timestamp);

    private static EspHomeEntityDescriptor Binary(ListEntitiesBinarySensorResponse m) =>
        new(EspHomeEntityKind.BinarySensor, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass);
    private static EspHomeEntityDescriptor Cover(ListEntitiesCoverResponse m) =>
        new(EspHomeEntityKind.Cover, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass,
            SupportsPosition: m.SupportsPosition, SupportsStop: m.SupportsStop);
    private static EspHomeEntityDescriptor Fan(ListEntitiesFanResponse m) =>
        new(EspHomeEntityKind.Fan, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory,
            SupportsFanSpeed: m.SupportsSpeed, SupportedFanSpeedCount: m.SupportedSpeedCount);
    private static EspHomeEntityDescriptor Light(ListEntitiesLightResponse m)
    {
        var modes = m.SupportedColorModes.ToArray();
        var brightness = modes.Any(x => x is 2 or 3 or 7 or 11 or 19 or 35 or 39 or 47 or 51);
        var rgb = modes.Any(x => x is 35 or 39 or 47 or 51);
        return new(EspHomeEntityKind.Light, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory,
            SupportsBrightness: brightness, SupportsRgb: rgb);
    }
    private static EspHomeEntityDescriptor Sensor(ListEntitiesSensorResponse m) =>
        new(EspHomeEntityKind.Sensor, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass, m.UnitOfMeasurement);
    private static EspHomeEntityDescriptor Switch(ListEntitiesSwitchResponse m) =>
        new(EspHomeEntityKind.Switch, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass);
    private static EspHomeEntityDescriptor Text(ListEntitiesTextSensorResponse m) =>
        new(EspHomeEntityKind.TextSensor, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass);
    private static EspHomeEntityDescriptor Number(ListEntitiesNumberResponse m) =>
        new(EspHomeEntityKind.Number, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass, m.UnitOfMeasurement,
            MinValue: m.MinValue, MaxValue: m.MaxValue, Step: m.Step);
    private static EspHomeEntityDescriptor Select(ListEntitiesSelectResponse m) =>
        new(EspHomeEntityKind.Select, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory,
            Options: m.Options.ToArray());
    private static EspHomeEntityDescriptor Button(ListEntitiesButtonResponse m) =>
        new(EspHomeEntityKind.Button, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory, m.DeviceClass);
    private static EspHomeEntityDescriptor Climate(ListEntitiesClimateResponse m) =>
        new(EspHomeEntityKind.Climate, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory);
    private static EspHomeEntityDescriptor Lock(ListEntitiesLockResponse m) =>
        new(EspHomeEntityKind.Lock, m.Key, m.DeviceId, m.ObjectId, m.Name, m.EntityCategory);

    private static string CoverOperationName(int operation) => operation switch
    {
        1 => "opening",
        2 => "closing",
        _ => "idle"
    };

    private static float NormalizePercent(object? value)
    {
        var percent = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (percent is < 0d or > 100d)
            throw new ArgumentOutOfRangeException(nameof(value), "ESPHome percent value must be from 0 to 100.");
        return (float)(percent / 100d);
    }

    private CancellationTokenSource CreateOperationToken(CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_settings.EffectiveRequestTimeout);
        return cts;
    }

    private static async ValueTask<byte> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var one = new byte[1];
        await ReadExactlyAsync(stream, one, cancellationToken).ConfigureAwait(false);
        return one[0];
    }

    private static async ValueTask<uint> ReadVarUInt32Async(Stream stream, CancellationToken cancellationToken)
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var current = await ReadByteAsync(stream, cancellationToken).ConfigureAwait(false);
            if (shift == 28 && (current & 0xF0) != 0)
                throw new InvalidDataException("ESPHome varint exceeds uint32 range.");
            value |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
        }
        throw new InvalidDataException("ESPHome varint is malformed.");
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("ESPHome TCP stream closed while reading a frame.");
            offset += read;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
