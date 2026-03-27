namespace Modbus.Core.Domain.Enums;

/// <summary>
/// Word order for 32-bit values spanning two 16-bit Modbus registers.
/// BigEndian = high word at lower address (most common in energy meters).
/// </summary>
public enum WordOrder
{
    BigEndian,
    LittleEndian
}
