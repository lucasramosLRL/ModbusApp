using Modbus.Core.Services.Scanning;

namespace Modbus.Desktop.ViewModels;

public class ScanResultViewModel
{
    public DeviceScanResult Result { get; }

    public ScanResultViewModel(DeviceScanResult result)
    {
        Result = result;
    }

    public string DisplayName => Result.SuggestedName;
    public string ModelText => Result.ModelName ?? "Unknown model";
    public string SlaveIdText => $"Slave {Result.SlaveId}";
    public string FirmwareText => Result.FirmwareVersionText;
    public string SerialText => Result.SerialNumberText;
    public string AddressText => Result.Tcp?.IpAddress ?? Result.Rtu?.PortName ?? "—";
}
