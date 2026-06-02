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

        // Mass-memory reset confirmation (IoT grandezas / send interval change)
        ["CfgMemResetTitle"]     = "Reset mass memory",
        ["CfgMemResetMsg"]       = "Changing the quantities and/or the send interval resets the meter's mass memory, erasing the data already logged. Continue?",
        ["CfgMemResetConfirm"]   = "Continue",

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
        ["DuplicateIp"]          = "A device with IP {0} already exists.",
        ["DuplicateRtuAddress"]  = "A device with RTU address {0} already exists.",
        ["ConnectFailed"]        = "Could not connect to the device. Check the address and settings.",
        ["CheckingAddress"]      = "Checking address availability...",
        ["WritingAddress"]       = "Writing new address to device...",
        ["WaitingForReboot"]     = "Waiting for device reboot (~20 s)...",
        ["AddressOccupied"]      = "Address {0} is already in use on the network.",
        ["AddressChangeTimeout"] = "Device did not respond at address {0}. Please check and try again.",
        ["DeviceSaved"]          = "Device saved. Select another from the list or go back.",

        // DeviceDetailView
        ["Transport"]            = "Transport",
        ["Connection"]           = "Connection",
        ["SlaveAddress"]         = "Address",
        ["Model"]                = "Model",
        ["Unknown"]              = "Unknown",

        // Electrical Readings — Tabs
        ["TabInstantaneous"]     = "Instantaneous",
        ["TabEnergiesDemands"]   = "Energies / Demands",
        ["TabInputsOutputs"]     = "Inputs & Outputs",
        ["TabHourmeter"]         = "Hourmeter",
        ["TabStatus"]            = "Status",
        ["TabComingSoon"]        = "Coming soon",
        ["NoLiveData"]           = "No live data. Waiting for next poll...",

        // Electrical Readings — Groups
        ["GroupVoltages"]        = "Voltages",
        ["GroupCurrents"]        = "Currents",
        ["GroupFrequency"]       = "Frequency",
        ["GroupActivePower"]     = "Active Power",
        ["GroupReactivePower"]   = "Reactive Power",
        ["GroupApparentPower"]   = "Apparent Power",
        ["GroupPowerFactor"]     = "Power Factor",
        ["GroupEnergies"]        = "Energies",
        ["GroupDemands"]         = "Demands",

        // Hourmeter Tab
        ["HmTitle"]              = "Load 1",
        ["HmHourmeter"]          = "Hourmeter",

        // IO Tab
        ["IoSectionInputs"]      = "Digital Inputs",
        ["IoSectionOutputs"]     = "Digital Outputs",
        ["IoStatus"]             = "Status:",
        ["IoCounter"]            = "Counter:",
        ["IoPulseWidth"]         = "Pulse Width:",
        ["IoReset"]              = "Reset",
        ["IoTurnOn"]             = "Turn On",
        ["IoTurnOff"]            = "Turn Off",
        ["IoStateOn"]            = "ON",
        ["IoStateOff"]           = "OFF",

        // Electrical Readings — Register descriptions
        ["RegU0"]                = "Three-phase",
        ["RegU12"]               = "Phase A-B",
        ["RegU23"]               = "Phase B-C",
        ["RegU31"]               = "Phase C-A",
        ["RegU1"]                = "Line 1",
        ["RegU2"]                = "Line 2",
        ["RegU3"]                = "Line 3",
        ["RegI0"]                = "Three-phase",
        ["RegI1"]                = "Line 1",
        ["RegI2"]                = "Line 2",
        ["RegI3"]                = "Line 3",
        ["RegFreq"]              = "Line 1",
        ["RegP0"]                = "Three-phase",
        ["RegP1"]                = "Line 1",
        ["RegP2"]                = "Line 2",
        ["RegP3"]                = "Line 3",
        ["RegQ0"]                = "Three-phase",
        ["RegQ1"]                = "Line 1",
        ["RegQ2"]                = "Line 2",
        ["RegQ3"]                = "Line 3",
        ["RegS0"]                = "Three-phase",
        ["RegS1"]                = "Line 1",
        ["RegS2"]                = "Line 2",
        ["RegS3"]                = "Line 3",
        ["RegFP0"]               = "Three-phase",
        ["RegFP1"]               = "Line 1",
        ["RegFP2"]               = "Line 2",
        ["RegFP3"]               = "Line 3",

        // DeviceHubView
        ["HubReadings"]              = "Readings",
        ["HubReadingsDesc"]          = "Voltages, currents, power and frequency read directly from the meter.",
        ["HubMassMemory"]            = "Mass Memory",
        ["HubMassMemoryDesc"]        = "Measurement history stored internally in the meter.",
        ["HubConfigure"]             = "Configure",
        ["HubConfigureDesc"]         = "Change meter parameters and settings by writing to registers.",

        // Status Tab
        ["StsMeterTitle"]        = "Meter Status",
        ["StsModuleTitle"]       = "Module Status",
        ["StsCodeHeader"]        = "Code - Description",
        ["StsNoErrors"]          = "No active errors.",

        // DeviceItemViewModel
        ["Connected"]            = "Connected",
        ["Disconnected"]         = "Disconnected",

        // RegisterValueViewModel
        ["RegisterFallback"]     = "Register {0}",

        // DeviceConfigureView — side navigation
        ["ConfigureTitle"]       = "Configure",
        ["CfgGeneral"]           = "General",
        ["CfgEthernet"]          = "Ethernet",
        ["CfgWireless"]          = "Wireless",
        ["CfgSntp"]              = "SNTP",
        ["CfgIot"]               = "IoT",
        ["CfgClock"]             = "Clock",
        ["CfgInputsOutputs"]     = "Inputs & Outputs",

        // DeviceConfigureView — device info strip
        ["CfgSerial"]            = "Serial",
        ["CfgFirmware"]          = "Firmware",
        ["CfgHardware"]          = "Hardware",

        // DeviceConfigureView — common fields
        ["CfgEnabled"]           = "Enabled",
        ["CfgDisabled"]          = "Disabled",
        ["CfgManual"]            = "Manual",
        ["CfgUseNow"]            = "Use now",
        ["CfgEditButton"]        = "Edit",
        ["CfgSaveButton"]        = "Save",
        ["CfgCancelButton"]      = "Cancel",
        ["CfgRetryRead"]         = "Retry",
        ["CfgSaveSuccess"]       = "Configuration saved",
        ["CfgSaveError"]         = "Failed to save configuration",
        ["CfgSavePartialReboot"] = "Applied {0} of {1}. Device is rebooting, resuming…",
        ["CfgSaveDeviceDownAfterPartial"] = "Applied {0} of {1}. Device did not come back in time. Wait a moment and retry the missing fields.",
        ["CfgSaveStillIncomplete"] = "Applied {0} of {1} after {2} reboots. Some fields were not written.",
        ["CfgSaveSuccessReadIncomplete"] = "Configuration saved — could not read all fields back to confirm.",
        ["CfgSaveSuccessDeviceRebooting"] = "Configuration saved — the device is rebooting. Click 'Retry' once it is back online to read the values again.",
        ["CfgSecurityNone"]      = "None",
        ["CfgBrokerDefault"]     = "Default",

        // DeviceConfigureView — General
        ["CfgCode"]              = "Code",
        ["CfgDeviceDesc"]        = "Description",
        ["CfgConnectionType"]    = "Connection Type (TL)",
        ["CfgIntegrationTime"]   = "Integration (TI)  0 – 60",
        ["CfgCurrentInvert"]     = "Invert current",
        ["CfgPfSequence"]        = "Float Point Sequence (Seq. PF)",
        ["CfgThreshold"]         = "Threshold  (0.01 – 9999.99)",
        ["CfgConfigPassword"]    = "Configuration password",

        // DeviceConfigureView — Ethernet
        ["CfgDhcp"]              = "Automatic DHCP",
        ["CfgIpAddress"]         = "IP Address",
        ["CfgSubnetMask"]        = "Subnet mask",
        ["CfgGateway"]           = "Default gateway",
        ["CfgDns"]               = "DNS",
        ["CfgMacAddress"]        = "MAC Address",

        // DeviceConfigureView — Wireless
        ["CfgMode"]              = "Mode",
        ["CfgSecurity"]          = "Security",
        ["CfgWifiPassword"]      = "Wi-Fi password",
        ["CfgModuleVersion"]     = "Module version",
        ["CfgBtPassword"]        = "Password",

        // DeviceConfigureView — SNTP
        ["CfgNtpServer"]         = "SNTP Server",
        ["CfgTimezone"]          = "Timezone",
        ["CfgSyncInterval"]      = "Sync interval",
        ["CfgUnitHours"]         = "hour(s)",
        ["CfgUnitMinutes"]       = "minute(s)",

        // DeviceConfigureView — IoT / MQTT
        ["CfgSendInterval"]      = "Send interval",
        ["CfgSendOnHour"]        = "Send on full hour",
        ["CfgMqttBroker"]        = "Broker",
        ["CfgMqttDescId"]        = "Description / ID",
        ["CfgMqttPort"]          = "Port",
        ["CfgMqttTopic"]         = "Topic",
        ["CfgMqttUser"]          = "Username",
        ["CfgMqttToken"]         = "Token / Password",
        ["CfgKeepAlive"]         = "Keep connection alive",
        ["CfgKronCloud"]         = "KronCloud",
        ["CfgTls"]               = "TLS",
        ["CfgClearBuffer"]       = "Clear Buffer",
        ["CfgAvailableRegisters"]= "Available registers",
        ["CfgSelectedRegisters"] = "Selected",
        ["CfgIotTabCerts"]       = "Certificates",
        ["CfgIotTabRetransmit"]  = "Retransmit blocks",
        ["CfgGrandezaCounter"]     = "Selected ({0}/{1})",
        ["CfgGrandezaAdd"]         = "Add highlighted",
        ["CfgGrandezaRemove"]      = "Remove highlighted",
        ["CfgGrandezaClear"]       = "Remove all",
        ["CfgGrandezaLimitReached"]= "Limit of {0} measurements reached.",

        // DeviceConfigureView — Clock
        ["CfgDate"]              = "Date",
        ["CfgTime"]              = "Time",

        // DeviceConfigureView — Inputs & Outputs
        ["CfgDebounceEdp"]       = "Debounce EDPs",

        // SettingsView
        ["SettingsTitle"]        = "Settings",
        ["Language"]             = "Language",
        ["ChangeInSettings"]     = "Change in Settings",
        ["NoPortConfigured"]     = "No COM port configured.",
        ["PortUnavailable"]      = "Port not connected. Make sure the device is plugged in.",
    };
}
