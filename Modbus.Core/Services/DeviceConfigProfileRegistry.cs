// ┌─────────────────────────────────────────────────────────────────────────────┐
// │  HOW TO FILL IN REGISTERS                                                   │
// │                                                                             │
// │  Whole register (16-bit):                                                   │
// │      AddrKe = 40005                                                         │
// │                                                                             │
// │  Multi-word (Float32, IP, MAC, string…):                                    │
// │      AddrTp         = new RegisterField(40001, WordCount: 2)                │
// │      AddrIpAddress  = new RegisterField(40010, WordCount: 2)                │
// │      AddrMacAddress = new RegisterField(30100, WordCount: 3)  // FC04       │
// │                                                                             │
// │  Bit-field (bits dentro de um registro compartilhado):                      │
// │      AddrCurrentInvert = new RegisterField(40007, BitOffset: 15, BitWidth: 1)│
// │      AddrBaudrate      = new RegisterField(40007, BitOffset: 0,  BitWidth: 3)│
// │      AddrTl            = new RegisterField(40006, BitOffset: 0,  BitWidth: 8)│
// │      AddrTi            = new RegisterField(40006, BitOffset: 8,  BitWidth: 8)│
// │                                                                             │
// │  Input register read-only (FC04):  endereço 3xxxx                          │
// │  Holding register leitura/escrita (FC03): endereço 4xxxx                   │
// │                                                                             │
// │  Registros não presentes no modelo: deixar null                             │
// └─────────────────────────────────────────────────────────────────────────────┘

namespace Modbus.Core.Services;

public static class DeviceConfigProfileRegistry
{
    // ── KS-3000 (0xF2) ───────────────────────────────────────────────────────
    public static readonly DeviceConfigProfile Ks3000 = new()
    {
        // ── Geral ─────────────────────────────────────────────────────────────
        AddrSlaveId       = null,
        AddrTp            = new RegisterField(40001, WordCount: 2),
        AddrTc            = new RegisterField(40003, WordCount: 2),
        AddrKe            = 40005,
        AddrTl            = new RegisterField(40006, BitOffset: 8, BitWidth: 8),
        AddrTi            = new RegisterField(40006, BitOffset: 0, BitWidth: 8),
        AddrCurrentInvert = new RegisterField(40007, BitOffset: 15, BitWidth: 1),
        AddrSeqPf         = 42901,
        AddrHourmeterThr  = new RegisterField(40161, WordCount: 2),

        // ── Ethernet ──────────────────────────────────────────────────────────
        AddrDhcp          = null,   // KS-3000 não tem Ethernet
        AddrIpAddress     = null,
        AddrSubnetMask    = null,
        AddrGateway       = null,
        AddrMacAddress    = null,
        AddrDnsEnabled    = null,
        AddrDnsServer     = null,

        // ── Wireless ──────────────────────────────────────────────────────────
        // Bits D8 (WiFi disabled) + D9 (Bluetooth disabled) of register 40020.
        // Combined value (0..3) maps to WiFi+BT / BT / WiFi / Disabled — see WirelessModeOptions in VM.
        AddrWirelessMode  = new RegisterField(40020, BitOffset: 8, BitWidth: 2),
        AddrSsid          = new RegisterField(43121, WordCount: 15),
        AddrWifiPassword  = new RegisterField(43161, WordCount: 15),
        AddrModuleVersion = new RegisterField(39511, WordCount: 2),
        AddrWifiDhcp      = new RegisterField(40007, BitOffset: 11, BitWidth: 1),
        AddrWifiIp        = new RegisterField(43101, WordCount: 2),
        AddrWifiMask      = new RegisterField(43103, WordCount: 2),
        AddrWifiGateway   = new RegisterField(43105, WordCount: 2),
        AddrWifiMac       = new RegisterField(39501, WordCount: 3),
        AddrWifiDnsEnabled= new RegisterField(40007, BitOffset: 14, BitWidth: 1),
        AddrWifiDns       = new RegisterField(43107, WordCount: 2),
        AddrBtDescription = new RegisterField(43001, WordCount: 8),
        AddrBtPassword    = new RegisterField(43011, WordCount: 8),
        AddrBtMac         = new RegisterField(39507, WordCount: 3),

        // ── SNTP ──────────────────────────────────────────────────────────────
        AddrSntpEnabled   = new RegisterField(40007, BitOffset: 12, BitWidth: 1),
        AddrTimezone      = 43201,
        AddrSyncInterval  = 43202,
        AddrNtpServer     = new RegisterField(43205, WordCount: 16),
        // ── IoT ───────────────────────────────────────────────────────────────
        AddrIotEnabled    = new RegisterField(40007, BitOffset: 13, BitWidth: 1),
        AddrSendInterval  = 42101,
        AddrSendOnHour    = new RegisterField(40020, BitOffset: 6, BitWidth: 1),
        // Bits D11-D12 of register 40020: 00=Padrão/AWS, 01=IBM, 10=Azure, 11=Losant/Wegnology
        AddrMqttBroker    = new RegisterField(40020, BitOffset: 11, BitWidth: 2),
        AddrMqttUrl       = new RegisterField(43461, WordCount: 35),
        AddrMqttDescId    = new RegisterField(43553, WordCount: 13),
        AddrMqttPort      = new RegisterField(43496, WordCount: 3),
        AddrMqttTopic     = new RegisterField(43566, WordCount: 30),
        AddrMqttUser      = new RegisterField(43499, WordCount: 19),
        AddrMqttToken     = new RegisterField(43518, WordCount: 35),
        AddrKeepAlive     = new RegisterField(40007, BitOffset: 10, BitWidth: 1),
        AddrTls           = new RegisterField(40020, BitOffset: 10, BitWidth: 1),

        // Grandezas selecionadas para envio MQTT/LoRa
        AddrGrandezasSlots1to20  = new RegisterField(42102, WordCount: 20),
        AddrGrandezasSlots21to50 = new RegisterField(42201, WordCount: 30),
        AddrStorageMode          = null, // KS-3000 is always circular — no register needed

        // ── Relógio ───────────────────────────────────────────────────────────
        // [42001] high=centésimo BCD, low=segundo BCD
        // [42002] high=minuto BCD,    low=hora BCD
        // [42003] high=dia semana raw (01=dom..07=sáb), low=dia BCD
        // [42004] high=mês BCD,       low=ano BCD (ex: 0x10 = 2010)
        AddrClockTime     = new RegisterField(42001, WordCount: 2),
        AddrClockDate     = new RegisterField(42003, WordCount: 2),

        // ── Entradas e Saídas ─────────────────────────────────────────────────
        AddrDebounceEdp   = 40171,

        // IoT buffer / mass-memory reset coil — KS-3000 legacy default (firmware < 6.0,
        // no mass memory): doc coil 91 → wire 90. Firmware >= 6.0 has mass memory and uses
        // wire 79 instead; the firmware split is resolved in GetIotBufferResetCoil.
        IotBufferResetCoil = 90,
    };

    // ── Konect 120 (0xF3) ────────────────────────────────────────────────────
    public static readonly DeviceConfigProfile Konect120 = new()
    {
        // ── Geral ─────────────────────────────────────────────────────────────
        AddrSlaveId       = null,
        AddrTp            = new RegisterField(40001, WordCount: 2),
        AddrTc            = new RegisterField(40003, WordCount: 2),
        AddrKe            = 40005,
        AddrTl            = new RegisterField(40006, BitOffset: 8, BitWidth: 8),
        AddrTi            = new RegisterField(40006, BitOffset: 0, BitWidth: 8),
        AddrCurrentInvert = new RegisterField(40007, BitOffset: 15, BitWidth: 1),
        AddrSeqPf         = 42901,
        AddrHourmeterThr  = new RegisterField(40161, WordCount: 2),

        // ── Ethernet ──────────────────────────────────────────────────────────
        AddrDhcp          = new RegisterField(40007, BitOffset: 11, BitWidth: 1),
        AddrIpAddress     = new RegisterField(43101, WordCount: 2),
        AddrSubnetMask    = new RegisterField(43103, WordCount: 2),
        AddrGateway       = new RegisterField(43105, WordCount: 2),
        AddrMacAddress    = new RegisterField(39501, WordCount: 3),
        AddrDnsEnabled    = new RegisterField(40007, BitOffset: 14, BitWidth: 1),
        AddrDnsServer     = new RegisterField(43117, WordCount: 2),

        // ── Wireless ──────────────────────────────────────────────────────────
        // Bits D8 (WiFi disabled) + D9 (Bluetooth disabled) of register 40020.
        // Combined value (0..3) maps to WiFi+BT / BT / WiFi / Disabled — see WirelessModeOptions in VM.
        AddrWirelessMode  = new RegisterField(40020, BitOffset: 8, BitWidth: 2),
        AddrSsid          = new RegisterField(43121, WordCount: 15),
        AddrWifiPassword  = new RegisterField(43161, WordCount: 15),
        AddrModuleVersion = new RegisterField(39511, WordCount: 2),
        AddrWifiDhcp      = new RegisterField(40007, BitOffset: 5, BitWidth: 1),
        AddrWifiIp        = new RegisterField(43111, WordCount: 2),
        AddrWifiMask      = new RegisterField(43123, WordCount: 2),
        AddrWifiGateway   = new RegisterField(43125, WordCount: 2),
        AddrWifiMac       = new RegisterField(39504, WordCount: 3),
        AddrWifiDnsEnabled= new RegisterField(40007, BitOffset: 14, BitWidth: 1),
        AddrWifiDns       = new RegisterField(43117, WordCount: 2),
        AddrBtDescription = new RegisterField(43001, WordCount: 8),
        AddrBtPassword    = new RegisterField(43011, WordCount: 8),
        AddrBtMac         = new RegisterField(39507, WordCount: 3),

        // ── SNTP ──────────────────────────────────────────────────────────────
        AddrSntpEnabled   = new RegisterField(40007, BitOffset: 12, BitWidth: 1),
        AddrTimezone      = 43201,
        AddrSyncInterval  = 43202,
        AddrNtpServer     = new RegisterField(43205, WordCount: 16),

        // ── IoT ───────────────────────────────────────────────────────────────
        AddrIotEnabled    = new RegisterField(40007, BitOffset: 13, BitWidth: 1),
        AddrSendInterval  = 42101,
        AddrSendOnHour    = new RegisterField(40020, BitOffset: 6, BitWidth: 1),
        // Bits D11-D12 of register 40020: 00=Padrão/AWS, 01=IBM, 10=Azure, 11=Losant/Wegnology
        AddrMqttBroker    = new RegisterField(40020, BitOffset: 11, BitWidth: 2),
        AddrMqttUrl       = new RegisterField(43461, WordCount: 35),
        AddrMqttDescId    = new RegisterField(43553, WordCount: 13),
        AddrMqttPort      = new RegisterField(43496, WordCount: 3),
        AddrMqttTopic     = new RegisterField(43566, WordCount: 30),
        AddrMqttUser      = new RegisterField(43499, WordCount: 19),
        AddrMqttToken     = new RegisterField(43518, WordCount: 35),
        AddrKeepAlive     = new RegisterField(40007, BitOffset: 10, BitWidth: 1),
        AddrTls           = new RegisterField(40020, BitOffset: 10, BitWidth: 1),

        // Grandezas selecionadas para envio MQTT/LoRa
        AddrGrandezasSlots1to20  = new RegisterField(42102, WordCount: 20),
        AddrGrandezasSlots21to50 = new RegisterField(42201, WordCount: 30),
        // D9 of reg 40007: 0 = circular, 1 = linear
        AddrStorageMode          = new RegisterField(40007, BitOffset: 9, BitWidth: 1),

        // ── Relógio ───────────────────────────────────────────────────────────
        // [42001] high=centésimo BCD, low=segundo BCD
        // [42002] high=minuto BCD,    low=hora BCD
        // [42003] high=dia semana raw (01=dom..07=sáb), low=dia BCD
        // [42004] high=mês BCD,       low=ano BCD (ex: 0x10 = 2010)
        AddrClockTime     = new RegisterField(42001, WordCount: 2),
        AddrClockDate     = new RegisterField(42003, WordCount: 2),

        // ── Entradas e Saídas ─────────────────────────────────────────────────
        AddrDebounceEdp   = 40171,

        // IoT buffer / mass-memory reset coil — Konect 120 doc coil 80 → wire 79.
        IotBufferResetCoil = 79,
    };

    private static readonly Dictionary<byte, DeviceConfigProfile> _map = new()
    {
        [0xF2] = Ks3000,
        [0xF3] = Konect120,
    };

    public static DeviceConfigProfile? Get(byte? deviceCode) =>
        deviceCode.HasValue && _map.TryGetValue(deviceCode.Value, out var p) ? p : null;

    // KS-3000 (0xF2) firmware that introduced mass memory. FirmwareVersion is the byte
    // form of "vX.Y" encoded as X*10 + Y, so v6.0 → 60.
    public const byte Ks3000MassMemoryMinFirmware = 60;

    /// <summary>
    /// Resolves the IoT buffer / mass-memory reset coil (FC05 wire address) for a device.
    /// For the KS-3000 this depends on firmware: builds &lt; 6.0 have no mass memory and use
    /// doc coil 91 (wire 90); builds &gt;= 6.0 have mass memory and use doc coil 80 (wire 79)
    /// like the other models. Unknown firmware falls back to the legacy coil (90).
    /// Every other model uses the profile's fixed <see cref="DeviceConfigProfile.IotBufferResetCoil"/>.
    /// </summary>
    public static ushort? GetIotBufferResetCoil(byte? deviceCode, byte? firmwareVersion)
    {
        if (deviceCode == 0xF2)
            return firmwareVersion >= Ks3000MassMemoryMinFirmware ? (ushort)79 : (ushort)90;
        return Get(deviceCode)?.IotBufferResetCoil;
    }
}
