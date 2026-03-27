namespace Modbus.Core.Services.Scanning;

public class ScanProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public int Found { get; init; }
    public string CurrentLabel { get; init; } = "";
}
