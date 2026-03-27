namespace Modbus.Core.Protocol.Rtu;

/// <summary>
/// Modbus RTU CRC-16 (polynomial 0xA001, initial value 0xFFFF).
/// CRC bytes are appended LSB first, as required by the Modbus spec.
/// </summary>
internal static class Crc16
{
    internal static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ 0xA001);
                else
                    crc >>= 1;
            }
        }
        return crc;
    }

    /// <summary>Appends the two CRC bytes (LSB first) at the end of <paramref name="frame"/>.</summary>
    internal static void Append(byte[] frame, int messageLength)
    {
        ushort crc = Compute(frame.AsSpan(0, messageLength));
        frame[messageLength]     = (byte)(crc & 0xFF);  // LSB
        frame[messageLength + 1] = (byte)(crc >> 8);    // MSB
    }

    /// <summary>Returns true when the last two bytes of <paramref name="frame"/> match the computed CRC.</summary>
    internal static bool Validate(byte[] frame)
    {
        if (frame.Length < 4) return false;
        ushort computed = Compute(frame.AsSpan(0, frame.Length - 2));
        ushort received = (ushort)(frame[^2] | (frame[^1] << 8));  // LSB first
        return computed == received;
    }
}
