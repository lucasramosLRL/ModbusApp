using Modbus.Core.Domain.ValueObjects;

namespace Modbus.Core.Services.Scanning;

public class DeviceScanResult
{
    public byte SlaveId { get; init; }
    public byte? DeviceCode { get; init; }
    public string? ModelName { get; init; }
    public byte? FirmwareVersion { get; init; }
    public uint? SerialNumber { get; init; }
    public required string SuggestedName { get; init; }

    public TcpConfig? Tcp { get; init; }
    public RtuConfig? Rtu { get; init; }

    public string FirmwareVersionText => FirmwareVersion.HasValue
        ? $"v{FirmwareVersion.Value / 10}.{FirmwareVersion.Value % 10}"
        : "—";

    public string SerialNumberText => SerialNumber.HasValue
        ? $"{SerialNumber.Value:D8}"
        : "—";
}
