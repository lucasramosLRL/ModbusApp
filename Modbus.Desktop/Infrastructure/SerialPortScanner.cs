using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Modbus.Desktop.Infrastructure;

internal static class SerialPortScanner
{
    // Linux paths to probe when GetPortNames() returns nothing
    private static readonly string[] LinuxPatterns =
    [
        "/dev/ttyS*",
        "/dev/ttyUSB*",
        "/dev/ttyACM*",
        "/dev/ttyAMA*",
        "/dev/rfcomm*"
    ];

    public static string[] GetPortNames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return GetWindowsPortNames();

        // On Linux, GetPortNames() only returns ports that exist in /dev.
        // Enumerate explicitly so we can union with its results and cover all patterns.
        var ports = new SortedSet<string>(SerialPort.GetPortNames(), StringComparer.Ordinal);

        foreach (var pattern in LinuxPatterns)
        {
            var dir     = Path.GetDirectoryName(pattern)!;
            var search  = Path.GetFileName(pattern);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, search))
                ports.Add(file);
        }

        return [.. ports];
    }

    private static string[] GetWindowsPortNames()
    {
        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Primary source: registry (covers all ports including COM10+)
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is not null)
            {
                foreach (var valueName in key.GetValueNames())
                {
                    if (key.GetValue(valueName) is string portName)
                        ports.Add(portName.Trim());
                }
            }
        }
        catch { /* registry unavailable */ }

        // Always also query the standard API — catches adapters the registry may miss
        try
        {
            foreach (var p in SerialPort.GetPortNames())
                ports.Add(p.Trim());
        }
        catch { }

        // Natural numeric sort: COM2 before COM10
        return [.. ports.OrderBy(p =>
        {
            var m = System.Text.RegularExpressions.Regex.Match(p, @"\d+$");
            return m.Success ? int.Parse(m.Value) : 0;
        })];
    }
}
