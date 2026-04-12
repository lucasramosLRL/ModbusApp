namespace Modbus.Desktop.Services.Strings;

internal static class EnglishStrings
{
    public static readonly Dictionary<string, string> All = new()
    {
        // Navigation
        ["NavDevices"]           = "Devices",
        ["NavSettings"]          = "Settings",

        // MainWindow sidebar
        ["AppSubtitle"]          = "Device Monitor",

        // DeviceListView
        ["Devices"]              = "Devices",
        ["AddDevice"]            = "+ Add Device",
        ["Refresh"]              = "Refresh",
        ["NoDevicesFound"]       = "No devices found. Add a device to get started.",
        ["Open"]                 = "Open →",
        ["Delete"]               = "Delete",
        ["NeverSeen"]            = "Never seen",
        ["UnknownModel"]         = "Unknown model",
        ["AddrPrefix"]           = "Addr {0}",

        // Delete dialog
        ["ConfirmDelete"]        = "Confirm Delete",
        ["ConfirmDeleteMsg"]     = "Delete \"{0}\"? This action cannot be undone.",
        ["Cancel"]               = "Cancel",

        // AddDeviceView
        ["Back"]                 = "← Back",
        ["AddDeviceTitle"]       = "Add Device",
        ["TransportType"]        = "Transport Type",
        ["RtuOption"]            = "RTU (Serial Port)",
        ["TcpOption"]            = "TCP (Ethernet / Wi-Fi)",
        ["SerialPortSettings"]   = "Serial Port Settings",
        ["ComPort"]              = "COM Port",
        ["RefreshPortList"]      = "Refresh port list",
        ["BaudRate"]             = "Baud Rate",
        ["DataBits"]             = "Data Bits",
        ["Parity"]               = "Parity",
        ["StopBits"]             = "Stop Bits",
        ["AddressRange"]         = "Address Range",
        ["NetworkSettings"]      = "Network Settings",
        ["NetworkSettingsHint"]  = "Discovery uses UDP broadcast (255.255.255.255). Devices on the local network will respond automatically.",
        ["SearchNetwork"]        = "Search Network",
        ["FoundDevices"]         = "Found Devices",
        ["NoFoundDevicesYet"]    = "No devices found yet. Run a scan or fill in device details manually below.",
        ["DeviceDetails"]        = "Device Details",
        ["Name"]                 = "Name",
        ["DeviceNameWatermark"]  = "e.g. KS-3000 #0012345",
        ["Address"]              = "Address (1–247)",
        ["DeviceIp"]             = "Device IP",
        ["DeviceIpWatermark"]    = "e.g. 192.168.1.42",
        ["SaveDevice"]           = "Save Device",

        // AddDeviceViewModel status messages
        ["Ready"]                = "Ready",
        ["SelectComPortFirst"]   = "Select a COM port first.",
        ["ScanningProgress"]     = "Scanning {0} ({1}/{2})",
        ["ScanDone"]             = "Done — {0} device(s) found.",
        ["ScanNoResponse"]       = "Scan complete. No devices responded.",
        ["ScanCancelled"]        = "Scan cancelled.",
        ["ErrorPrefix"]          = "Error: {0}",
        ["DuplicateSerial"]      = "A device with serial number {0:D7} already exists.",
        ["DeviceSaved"]          = "Device saved. Select another from the list or go back.",

        // DeviceDetailView
        ["Transport"]            = "Transport",
        ["Connection"]           = "Connection",
        ["SlaveAddress"]         = "Address",
        ["Model"]                = "Model",
        ["Unknown"]              = "Unknown",
        ["RegisterValues"]       = "Register Values",
        ["NoRegisterValues"]     = "No register values yet. Values will appear after the first successful poll.",
        ["ColName"]              = "Name",
        ["ColAddress"]           = "Address",
        ["ColType"]              = "Type",
        ["ColValue"]             = "Value",
        ["ColLastUpdated"]       = "Last Updated",

        // DeviceItemViewModel
        ["Connected"]            = "Connected",
        ["Disconnected"]         = "Disconnected",

        // RegisterValueViewModel
        ["RegisterFallback"]     = "Register {0}",

        // SettingsView
        ["SettingsTitle"]        = "Settings",
        ["Language"]             = "Language",
    };
}
