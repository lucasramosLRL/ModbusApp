namespace Modbus.Core.Protocol.Models;

public class ReportSlaveIdData
{
    /// <summary>
    /// Raw bytes returned in the Server ID field. Content is fully vendor-specific.
    /// Conventionally: [0] = echoed slave address, [1] = run indicator, [2+] = ASCII model/serial info.
    /// </summary>
    public byte[] RawData { get; init; } = [];

    /// <summary>
    /// Conventionally the second byte of RawData. 0xFF = running, 0x00 = stopped.
    /// May not be valid for all devices.
    /// </summary>
    public byte RunIndicatorStatus { get; init; }

    public bool IsRunning => RunIndicatorStatus == 0xFF;
}
