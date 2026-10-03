using System.Buffers.Binary;
using System.Globalization;
using Scada.Core.Tags;

namespace Scada.Drivers.Modbus;

public enum ModbusServerClientAccess
{
    ReadOnly,
    ReadWrite
}

public sealed record ModbusHoldingRegisterRange(ushort Start, ushort EndInclusive)
{
    public int Count => EndInclusive - Start + 1;
    public bool Contains(int start, int count) =>
        count > 0 && start >= Start && start + count - 1 <= EndInclusive;
}

public static class ModbusServerRangeCodec
{
    public const string VersionPrefix = "v1:";
    public const int MaximumRanges = 32;
    public const int MaximumTotalRegisters = 16384;

    public static bool TryParse(
        string? raw,
        out IReadOnlyCollection<ModbusHoldingRegisterRange> ranges,
        out string? error)
    {
        ranges = Array.Empty<ModbusHoldingRegisterRange>();
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "At least one Holding Register range is required.";
            return false;
        }

        var text = raw.Trim();
        if (!text.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Holding Register ranges must use the versioned '{VersionPrefix}' format.";
            return false;
        }

        var entries = text[VersionPrefix.Length..]
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length is < 1 or > MaximumRanges)
        {
            error = $"Holding Register range count must be from 1 to {MaximumRanges}.";
            return false;
        }

        var parsed = new List<ModbusHoldingRegisterRange>(entries.Length);
        foreach (var entry in entries)
        {
            var dash = entry.IndexOf('-');
            if (dash <= 0 || dash == entry.Length - 1 ||
                !ushort.TryParse(entry[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out var start) ||
                !ushort.TryParse(entry[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var end))
            {
                error = $"Invalid Holding Register range '{entry}'. Use start-end with 0..65535 addresses.";
                return false;
            }
            if (start > end)
            {
                error = $"Holding Register range '{entry}' has start greater than end.";
                return false;
            }
            parsed.Add(new ModbusHoldingRegisterRange(start, end));
        }

        var ordered = parsed.OrderBy(range => range.Start).ThenBy(range => range.EndInclusive).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Start <= ordered[index - 1].EndInclusive)
            {
                error = $"Holding Register ranges {ordered[index - 1].Start}-{ordered[index - 1].EndInclusive} and {ordered[index].Start}-{ordered[index].EndInclusive} overlap.";
                return false;
            }
        }

        if (ordered.Sum(range => range.Count) > MaximumTotalRegisters)
        {
            error = $"Configured Holding Register ranges exceed the bounded total of {MaximumTotalRegisters} registers.";
            return false;
        }

        ranges = ordered;
        return true;
    }

    public static string Format(IEnumerable<ModbusHoldingRegisterRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        return VersionPrefix + string.Join(
            ";",
            ranges.OrderBy(range => range.Start)
                .Select(range => $"{range.Start.ToString(CultureInfo.InvariantCulture)}-{range.EndInclusive.ToString(CultureInfo.InvariantCulture)}"));
    }
}

public sealed record ModbusServerPoint(ModbusPoint Point, ModbusServerClientAccess ClientAccess)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Point);
        Point.Validate();
        if (Point.Area != ModbusDataArea.HoldingRegister)
            throw new ArgumentException("Initial Modbus Server points must use Holding Registers.");
        if (!Enum.IsDefined(ClientAccess)) throw new ArgumentOutOfRangeException(nameof(ClientAccess));
    }
}

public sealed record ModbusServerTagUpdate(TagDefinition Tag, object? EngineeringValue);

public enum ModbusServerRequestFailure
{
    UnsupportedFunction,
    IllegalAddress,
    IllegalValue,
    ReadOnly,
    ServerFailure,
    UnitMismatch
}

public sealed class ModbusServerRequestException : IOException
{
    public ModbusServerRequestException(
        byte exceptionCode,
        ModbusServerRequestFailure failure,
        string message) : base(message)
    {
        ExceptionCode = exceptionCode;
        Failure = failure;
    }

    public byte ExceptionCode { get; }
    public ModbusServerRequestFailure Failure { get; }
}

public sealed class ModbusServerRegisterMap
{
    private readonly ushort[] _registers = new ushort[ushort.MaxValue + 1];
    private readonly IReadOnlyCollection<ModbusHoldingRegisterRange> _ranges;
    private readonly IReadOnlyList<ModbusServerPoint> _points;
    private readonly IReadOnlyDictionary<Guid, ModbusServerPoint> _byTagId;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ModbusServerRegisterMap(
        IEnumerable<ModbusHoldingRegisterRange> ranges,
        IEnumerable<ModbusServerPoint> points)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(points);
        _ranges = ranges.OrderBy(range => range.Start).ToArray();
        _points = points.OrderBy(point => point.Point.Address).ToArray();
        if (_ranges.Count == 0) throw new ArgumentException("At least one Holding Register range is required.", nameof(ranges));
        if (_ranges.Count > ModbusServerRangeCodec.MaximumRanges) throw new ArgumentOutOfRangeException(nameof(ranges));
        if (_ranges.Sum(range => range.Count) > ModbusServerRangeCodec.MaximumTotalRegisters) throw new ArgumentOutOfRangeException(nameof(ranges));

        var orderedRanges = _ranges.ToArray();
        for (var index = 1; index < orderedRanges.Length; index++)
            if (orderedRanges[index].Start <= orderedRanges[index - 1].EndInclusive)
                throw new ArgumentException("Holding Register ranges may not overlap.", nameof(ranges));

        foreach (var point in _points)
        {
            point.Validate();
            if (!IsAllowed(point.Point.Address, point.Point.RegisterCount))
                throw new ArgumentException($"TAG '{point.Point.Tag.Path}' span is outside configured Holding Register ranges.", nameof(points));
        }

        for (var index = 1; index < _points.Count; index++)
        {
            var previous = _points[index - 1].Point;
            var current = _points[index].Point;
            if (current.Address < previous.EndAddressExclusive)
                throw new ArgumentException($"Modbus Server TAG spans '{previous.Tag.Path}' and '{current.Tag.Path}' overlap.", nameof(points));
        }

        if (_points.Select(point => point.Point.Tag.Id).Distinct().Count() != _points.Count)
            throw new ArgumentException("Each Modbus Server point must reference a unique TAG ID.", nameof(points));
        _byTagId = _points.ToDictionary(point => point.Point.Tag.Id);
    }

    public int RegisterCount => _ranges.Sum(range => range.Count);
    public int TagCount => _points.Count;

    public async ValueTask<ushort[]> ReadAsync(
        ushort address,
        ushort quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity is < 1 or > 125)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "FC03 quantity must be from 1 to 125.");
        if (!IsAllowed(address, quantity))
            throw new ModbusServerRequestException(0x02, ModbusServerRequestFailure.IllegalAddress, "Requested Holding Register span is outside configured ranges.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _registers.AsSpan(address, quantity).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<ModbusServerTagUpdate> WriteInternalAsync(
        Guid tagId,
        object? engineeringValue,
        CancellationToken cancellationToken = default)
    {
        if (!_byTagId.TryGetValue(tagId, out var serverPoint))
            throw new KeyNotFoundException($"Modbus Server TAG '{tagId}' is not mapped.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var point = serverPoint.Point;
            if (point.AddressSelector is not null)
            {
                _registers[point.Address] = ModbusValueCodec.ApplyRegisterBit(
                    point,
                    _registers[point.Address],
                    engineeringValue);
            }
            else
            {
                var encoded = ModbusValueCodec.EncodeRegisters(point, engineeringValue);
                encoded.CopyTo(_registers, point.Address);
            }
            return new ModbusServerTagUpdate(point.Tag, ModbusValueCodec.NormalizeEngineeringValue(engineeringValue));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<IReadOnlyCollection<ModbusServerTagUpdate>> WriteClientAsync(
        ushort address,
        IReadOnlyList<ushort> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 123)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "Client write quantity must be from 1 to 123.");
        if (!IsAllowed(address, values.Count))
            throw new ModbusServerRequestException(0x02, ModbusServerRequestFailure.IllegalAddress, "Client write span is outside configured ranges.");

        var endExclusive = address + values.Count;
        var touched = _points
            .Where(serverPoint =>
                serverPoint.Point.Address < endExclusive &&
                serverPoint.Point.EndAddressExclusive > address)
            .ToArray();

        if (touched.Length == 0)
            throw new ModbusServerRequestException(0x02, ModbusServerRequestFailure.IllegalAddress, "Client write does not address a mapped TAG.");

        foreach (var serverPoint in touched)
        {
            var point = serverPoint.Point;
            if (point.Address < address || point.EndAddressExclusive > endExclusive)
                throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, $"Client write would partially update multi-register TAG '{point.Tag.Path}'.");
            if (serverPoint.ClientAccess != ModbusServerClientAccess.ReadWrite)
                throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.ReadOnly, $"TAG '{point.Tag.Path}' is read-only to external Modbus clients.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            for (var index = 0; index < values.Count; index++)
                _registers[address + index] = values[index];

            var updates = new List<ModbusServerTagUpdate>(touched.Length);
            foreach (var serverPoint in touched)
            {
                var point = serverPoint.Point;
                var span = _registers.AsSpan(point.Address, point.RegisterCount);
                updates.Add(new ModbusServerTagUpdate(
                    point.Tag,
                    ModbusValueCodec.DecodeRegisters(point, span)));
            }
            return updates;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsAllowed(int address, int count) =>
        count > 0 && _ranges.Any(range => range.Contains(address, count));
}

public enum ModbusServerOperationKind
{
    Read,
    Write,
    Rejected
}

public sealed record ModbusServerProtocolResult(
    byte[] ResponsePdu,
    ModbusServerOperationKind Kind,
    ModbusServerRequestFailure? Failure = null);

public sealed class ModbusServerProtocolHandler
{
    private readonly byte _unitId;
    private readonly ModbusServerRegisterMap _map;
    private readonly Func<IReadOnlyCollection<ModbusServerTagUpdate>, CancellationToken, ValueTask> _publishExternalUpdates;

    public ModbusServerProtocolHandler(
        byte unitId,
        ModbusServerRegisterMap map,
        Func<IReadOnlyCollection<ModbusServerTagUpdate>, CancellationToken, ValueTask> publishExternalUpdates)
    {
        if (unitId > 247) throw new ArgumentOutOfRangeException(nameof(unitId));
        _unitId = unitId;
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _publishExternalUpdates = publishExternalUpdates ?? throw new ArgumentNullException(nameof(publishExternalUpdates));
    }

    public byte UnitId => _unitId;

    public async ValueTask<ModbusServerProtocolResult> HandleAsync(
        byte unitId,
        ReadOnlyMemory<byte> pdu,
        CancellationToken cancellationToken = default)
    {
        if (unitId != _unitId)
            return Failure(pdu.Span, new ModbusServerRequestException(0x0B, ModbusServerRequestFailure.UnitMismatch, $"Unit ID {unitId} is not served by this endpoint."));
        if (pdu.Length == 0)
            return new ModbusServerProtocolResult(ModbusPduCodec.BuildExceptionResponse(0, 0x03), ModbusServerOperationKind.Rejected, ModbusServerRequestFailure.IllegalValue);

        try
        {
            return pdu.Span[0] switch
            {
                ModbusPduCodec.ReadHoldingRegisters => await ReadHoldingAsync(pdu, cancellationToken),
                ModbusPduCodec.WriteSingleRegister => await WriteSingleAsync(pdu, cancellationToken),
                ModbusPduCodec.WriteMultipleRegisters => await WriteMultipleAsync(pdu, cancellationToken),
                _ => throw new ModbusServerRequestException(
                    0x01,
                    ModbusServerRequestFailure.UnsupportedFunction,
                    $"Modbus Server function 0x{pdu.Span[0]:X2} is not supported.")
            };
        }
        catch (ModbusServerRequestException ex)
        {
            return Failure(pdu.Span, ex);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Failure(
                pdu.Span,
                new ModbusServerRequestException(0x04, ModbusServerRequestFailure.ServerFailure, ex.Message));
        }
    }

    private async ValueTask<ModbusServerProtocolResult> ReadHoldingAsync(
        ReadOnlyMemory<byte> pdu,
        CancellationToken cancellationToken)
    {
        if (pdu.Length != 5)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "FC03 request PDU must contain address and quantity.");
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(1, 2));
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(3, 2));
        var values = await _map.ReadAsync(address, quantity, cancellationToken);
        var response = new byte[2 + values.Length * 2];
        response[0] = ModbusPduCodec.ReadHoldingRegisters;
        response[1] = checked((byte)(values.Length * 2));
        for (var index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + index * 2, 2), values[index]);
        return new ModbusServerProtocolResult(response, ModbusServerOperationKind.Read);
    }

    private async ValueTask<ModbusServerProtocolResult> WriteSingleAsync(
        ReadOnlyMemory<byte> pdu,
        CancellationToken cancellationToken)
    {
        if (pdu.Length != 5)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "FC06 request PDU must contain address and value.");
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(1, 2));
        var value = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(3, 2));
        var updates = await _map.WriteClientAsync(address, new[] { value }, cancellationToken);
        await _publishExternalUpdates(updates, cancellationToken);
        return new ModbusServerProtocolResult(pdu.ToArray(), ModbusServerOperationKind.Write);
    }

    private async ValueTask<ModbusServerProtocolResult> WriteMultipleAsync(
        ReadOnlyMemory<byte> pdu,
        CancellationToken cancellationToken)
    {
        if (pdu.Length < 6)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "FC16 request PDU is truncated.");
        var address = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(1, 2));
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(3, 2));
        var byteCount = pdu.Span[5];
        if (quantity is < 1 or > 123 || byteCount != quantity * 2 || pdu.Length != 6 + byteCount)
            throw new ModbusServerRequestException(0x03, ModbusServerRequestFailure.IllegalValue, "FC16 quantity/byte count is invalid.");

        var values = new ushort[quantity];
        for (var index = 0; index < values.Length; index++)
            values[index] = BinaryPrimitives.ReadUInt16BigEndian(pdu.Span.Slice(6 + index * 2, 2));

        var updates = await _map.WriteClientAsync(address, values, cancellationToken);
        await _publishExternalUpdates(updates, cancellationToken);

        var response = new byte[5];
        response[0] = ModbusPduCodec.WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(3, 2), quantity);
        return new ModbusServerProtocolResult(response, ModbusServerOperationKind.Write);
    }

    private static ModbusServerProtocolResult Failure(
        ReadOnlySpan<byte> pdu,
        ModbusServerRequestException exception)
    {
        var function = pdu.Length == 0 ? (byte)0 : pdu[0];
        return new ModbusServerProtocolResult(
            ModbusPduCodec.BuildExceptionResponse(function, exception.ExceptionCode),
            ModbusServerOperationKind.Rejected,
            exception.Failure);
    }
}
