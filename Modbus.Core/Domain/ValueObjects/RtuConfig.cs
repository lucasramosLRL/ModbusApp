using Modbus.Core.Domain.Enums;

namespace Modbus.Core.Domain.ValueObjects;

public class RtuConfig
{
    public required string PortName { get; set; }
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
}
