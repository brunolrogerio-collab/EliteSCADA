namespace Scada.Drivers.Modbus;

internal static class ModbusRtuCrc
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x0001) != 0
                    ? checked((ushort)((crc >> 1) ^ 0xA001))
                    : checked((ushort)(crc >> 1));
        }
        return crc;
    }

    public static bool IsValid(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4) return false;
        var expected = Compute(frame[..^2]);
        return frame[^2] == (byte)(expected & 0xFF) &&
               frame[^1] == (byte)(expected >> 8);
    }

    public static byte[] Frame(byte unitId, ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length is < 1 or > 253) throw new ArgumentOutOfRangeException(nameof(pdu));
        var result = new byte[pdu.Length + 3];
        result[0] = unitId;
        pdu.CopyTo(result.AsSpan(1));
        var crc = Compute(result.AsSpan(0, result.Length - 2));
        result[^2] = (byte)(crc & 0xFF);
        result[^1] = (byte)(crc >> 8);
        return result;
    }
}
