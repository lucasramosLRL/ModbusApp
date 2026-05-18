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
        AddrTl            = new RegisterField(40006, BitOffset: 0, BitWidth: 8),
        AddrTi            = new RegisterField(40006, BitOffset: 8, BitWidth: 8),
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
        AddrClockSource   = null,

        // ── Entradas e Saídas ─────────────────────────────────────────────────
        AddrDebounceEdp   = null,
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
        AddrClockSource   = null,

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
