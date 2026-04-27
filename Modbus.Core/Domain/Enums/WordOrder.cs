namespace Modbus.Core.Domain.Enums;

/// <summary>
/// Word order for 32-bit values spanning two 16-bit Modbus registers.
/// BigEndian    = ABCD — high word at lower address (most common).
/// LittleEndian = CDAB — low word at lower address.
/// ByteSwapped  = DCBA — bytes reversed from standard IEEE 754 (KS-3000 style).
/// UseSqpf      = sentinel: resolve from device SQPF register at poll time (Float32 input only).
/// </summary>
public enum WordOrder
{
    BigEndian,
    LittleEndian,
    ByteSwapped,
    UseSqpf
}
