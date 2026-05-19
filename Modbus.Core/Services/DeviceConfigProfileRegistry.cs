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
        AddrWirelessMode  = null,
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
        AddrBtMac         = new RegisterField(39501, WordCount: 3),

        // ── SNTP ──────────────────────────────────────────────────────────────
        AddrSntpEnabled   = new RegisterField(40007, BitOffset: 12, BitWidth: 1),
        AddrTimezone      = 43201,
        AddrSyncInterval  = 43202,
        AddrNtpServer     = new RegisterField(43205, WordCount: 16),
        // ── IoT ───────────────────────────────────────────────────────────────
        AddrIotEnabled    = new RegisterField(40007, BitOffset: 13, BitWidth: 1),
        AddrSendInterval  = 42101,
        AddrSendOnHour    = new RegisterField(40020, BitOffset: 6, BitWidth: 1),
        AddrMqttBroker    = null,
        AddrMqttUrl       = new RegisterField(43461, WordCount: 35),
        AddrMqttDescId    = new RegisterField(43553, WordCount: 13),
        AddrMqttPort      = new RegisterField(43496, WordCount: 3),
        AddrMqttTopic     = new RegisterField(43566, WordCount: 30),
        AddrMqttUser      = new RegisterField(43499, WordCount: 19),
        AddrMqttToken     = new RegisterField(43518, WordCount: 35),
        AddrKeepAlive     = new RegisterField(40007, BitOffset: 10, BitWidth: 1),
        AddrTls           = new RegisterField(40020, BitOffset: 10, BitWidth: 1),

        // ── Relógio ───────────────────────────────────────────────────────────
        // [42001] high=centésimo BCD, low=segundo BCD
        // [42002] high=minuto BCD,    low=hora BCD
        // [42003] high=dia semana raw (01=dom..07=sáb), low=dia BCD
        // [42004] high=mês BCD,       low=ano BCD (ex: 0x10 = 2010)
        AddrClockTime     = new RegisterField(42001, WordCount: 2),
        AddrClockDate     = new RegisterField(42003, WordCount: 2),

        // ── Entradas e Saídas ─────────────────────────────────────────────────
        AddrDebounceEdp   = 40171,
    };

    // ── Konect 120 (0xF3) ────────────────────────────────────────────────────
    public static readonly DeviceConfigProfile Konect120 = new()
    {
        // ── Geral ─────────────────────────────────────────────────────────────
        AddrSlaveId       = null,
        AddrTp            = null,
        AddrTc            = null,
        AddrKe            = null,
        AddrTl            = null,
        AddrTi            = null,
        AddrCurrentInvert = null,
        AddrSeqPf         = null,
        AddrHourmeterThr  = null,

        // ── Ethernet ──────────────────────────────────────────────────────────
        AddrDhcp          = null,
        AddrIpAddress     = null,   // WordCount: 2
        AddrSubnetMask    = null,   // WordCount: 2
        AddrGateway       = null,   // WordCount: 2
        AddrMacAddress    = null,   // WordCount: 3, FC04 → 3xxxx
        AddrDnsEnabled    = null,
        AddrDnsServer     = null,   // WordCount: 2

        // ── Wireless ──────────────────────────────────────────────────────────
        AddrWirelessMode  = null,
        AddrSsid          = null,   // string → WordCount: N
        AddrWifiPassword  = null,   // string → WordCount: N
        AddrModuleVersion = null,   // string → WordCount: N, FC04 → 3xxxx
        AddrWifiDhcp      = null,
        AddrWifiIp        = null,   // WordCount: 2
        AddrWifiMask      = null,   // WordCount: 2
        AddrWifiGateway   = null,   // WordCount: 2
        AddrWifiMac       = null,   // WordCount: 3, FC04 → 3xxxx
        AddrWifiDnsEnabled= null,
        AddrWifiDns       = null,   // WordCount: 2
        AddrBtDescription = null,   // string → WordCount: N
        AddrBtPassword    = null,
        AddrBtMac         = null,   // WordCount: 3, FC04 → 3xxxx

        // ── SNTP ──────────────────────────────────────────────────────────────
        AddrSntpEnabled   = null,
        AddrTimezone      = null,
        AddrSyncInterval  = null,
        AddrNtpServer     = null,   // string → WordCount: N

        // ── IoT ───────────────────────────────────────────────────────────────
        AddrIotEnabled    = null,
        AddrSendInterval  = null,
        AddrSendOnHour    = null,
        AddrMqttBroker    = null,
        AddrMqttUrl       = null,   // string → WordCount: N
        AddrMqttDescId    = null,   // string → WordCount: N
        AddrMqttPort      = null,
        AddrMqttTopic     = null,   // string → WordCount: N
        AddrMqttUser      = null,   // string → WordCount: N
        AddrMqttToken     = null,   // string → WordCount: N
        AddrKeepAlive     = null,
        AddrKronCloud     = null,
        AddrTls           = null,

        // ── Relógio ───────────────────────────────────────────────────────────
        AddrClockDate     = null,
        AddrClockTime     = null,

        // ── Entradas e Saídas ─────────────────────────────────────────────────
        AddrDebounceEdp   = null,
    };

    private static readonly Dictionary<byte, DeviceConfigProfile> _map = new()
    {
        [0xF2] = Ks3000,
        [0xF3] = Konect120,
    };

    public static DeviceConfigProfile? Get(byte? deviceCode) =>
        deviceCode.HasValue && _map.TryGetValue(deviceCode.Value, out var p) ? p : null;
}
