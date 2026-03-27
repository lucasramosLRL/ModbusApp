namespace Modbus.Core.Domain.ValueObjects;

public class TcpConfig
{
    public required string IpAddress { get; set; }
    public int Port { get; set; } = 502;
}
