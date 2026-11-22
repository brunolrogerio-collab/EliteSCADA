using System.Buffers.Binary;

namespace Scada.Drivers.Modbus;

/// <summary>
/// Transport-neutral Modbus PDU builder/validator shared by TCP, RTU and Server transports.
/// MBAP and RTU CRC/unit framing deliberately stay outside this type.
/// </summary>
internal static class ModbusPduCodec
{
    public const byte ReadCoils = 0x01;
    public const byte ReadDiscreteInputs = 0x02;
    public const byte ReadHoldingRegisters = 0x03;
    public const byte ReadInputRegisters = 0x04;
    public const byte WriteSingleCoil = 0x05;
    public const byte WriteSingleRegister = 0x06;
    public const byte WriteMultipleRegisters = 0x10;

    public static byte[] BuildReadRequest(byte function, ushort address, ushort quantity)
    {
        if (function is not (ReadCoils or ReadDiscreteInputs or ReadHoldingRegisters or ReadInputRegisters))
            throw new ArgumentOutOfRangeException(nameof(function));
        var maximum = function is ReadCoils or ReadDiscreteInputs ? 2000 : 125;
        if (quantity < 1 || quantity > maximum) throw new ArgumentOutOfRangeException(nameof(quantity));

        var pdu = new byte[5];
        pdu[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), quantity);
        return pdu;
    }

    public static byte[] BuildWriteSingleCoilRequest(ushort address, bool value)
    {
        var pdu = new byte[5];
        pdu[0] = WriteSingleCoil;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value ? (ushort)0xFF00 : (ushort)0x0000);
        return pdu;
    }

    public static byte[] BuildWriteSingleRegisterRequest(ushort address, ushort value)
    {
        var pdu = new byte[5];
        pdu[0] = WriteSingleRegister;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);
        return pdu;
    }

    public static byte[] BuildWriteMultipleRegistersRequest(ushort address, IReadOnlyList<ushort> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values));

        var pdu = new byte[6 + values.Count * 2];
        pdu[0] = WriteMultipleRegisters;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), address);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), checked((ushort)values.Count));
        pdu[5] = checked((byte)(values.Count * 2));
        for (var index = 0; index < values.Count; index++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + index * 2, 2), values[index]);
        return pdu;
    }

    public static bool[] DecodeBitReadResponse(ReadOnlySpan<byte> response, byte expectedFunction, ushort quantity)
    {
        ValidateFunction(response, expectedFunction);
        if (response.Length < 2) throw new IOException("Modbus bit response is truncated.");
        var byteCount = response[1];
        var expectedBytes = (quantity + 7) / 8;
        if (byteCount != expectedBytes || response.Length != byteCount + 2)
            throw new IOException("Modbus bit response byte count is invalid.");

        var result = new bool[quantity];
        for (var index = 0; index < result.Length; index++)
            result[index] = (response[2 + index / 8] & (1 << (index % 8))) != 0;
        return result;
    }

    public static ushort[] DecodeRegisterReadResponse(ReadOnlySpan<byte> response, byte expectedFunction, ushort quantity)
    {
        ValidateFunction(response, expectedFunction);
        if (response.Length < 2) throw new IOException("Modbus register response is truncated.");
        var byteCount = response[1];
        if (byteCount != quantity * 2 || response.Length != byteCount + 2)
            throw new IOException("Modbus register response byte count is invalid.");

        var result = new ushort[quantity];
        for (var index = 0; index < result.Length; index++)
            result[index] = BinaryPrimitives.ReadUInt16BigEndian(response.Slice(2 + index * 2, 2));
        return result;
    }

    public static void ValidateWriteEchoResponse(ReadOnlySpan<byte> response, ReadOnlySpan<byte> requestPdu, byte function)
    {
        ValidateFunction(response, function);
        if (!response.SequenceEqual(requestPdu))
            throw new IOException($"Modbus FC{function:X2} response does not match the request.");
    }

    public static void ValidateWriteMultipleResponse(ReadOnlySpan<byte> response, ushort address, ushort quantity)
    {
        ValidateFunction(response, WriteMultipleRegisters);
        if (response.Length != 5 ||
            BinaryPrimitives.ReadUInt16BigEndian(response.Slice(1, 2)) != address ||
            BinaryPrimitives.ReadUInt16BigEndian(response.Slice(3, 2)) != quantity)
            throw new IOException("Modbus FC16 response does not match the request.");
    }

    public static void ThrowIfException(ReadOnlySpan<byte> response)
    {
        if (response.Length == 0) throw new IOException("Modbus response PDU is empty.");
        if ((response[0] & 0x80) == 0) return;
        var exceptionCode = response.Length > 1 ? response[1] : (byte)0;
        throw new ModbusProtocolException((byte)(response[0] & 0x7F), exceptionCode);
    }

    public static byte[] BuildExceptionResponse(byte function, byte exceptionCode) =>
        new[] { checked((byte)(function | 0x80)), exceptionCode };

    public static void ValidateFunction(ReadOnlySpan<byte> response, byte expectedFunction)
    {
        ThrowIfException(response);
        if (response.Length == 0 || response[0] != expectedFunction)
            throw new IOException($"Unexpected Modbus function code in response. Expected 0x{expectedFunction:X2}.");
    }
}
