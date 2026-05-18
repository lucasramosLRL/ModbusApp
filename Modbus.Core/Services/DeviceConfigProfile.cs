using System.Reflection;

namespace Modbus.Core.Services;

/// <summary>
/// Describes the register layout for one device model's configuration screen.
///
/// Each nullable <see cref="RegisterField"/> carries the Modicon address and field type:
///   • new RegisterField(40005)                    — whole 16-bit holding register
///   • new RegisterField(40001, WordCount: 2)      — two consecutive registers (e.g. Float32)
///   • new RegisterField(40007, BitOffset: 12, BitWidth: 1) — single bit inside a shared register
///
/// Modicon convention: 4xxxx = FC03 holding (read/write), 3xxxx = FC04 input (read-only).
/// Null means the field is absent on this model.
/// </summary>
public sealed class DeviceConfigProfile
{
    // ── General ──────────────────────────────────────────────────────────────
    public RegisterField? AddrSlaveId        { get; init; }
    public RegisterField? AddrTp             { get; init; }
    public RegisterField? AddrTc             { get; init; }
    public RegisterField? AddrKe             { get; init; }
    public RegisterField? AddrTl             { get; init; }
    public RegisterField? AddrTi             { get; init; }
    public RegisterField? AddrCurrentInvert  { get; init; }
    public RegisterField? AddrSeqPf          { get; init; }
    public RegisterField? AddrHourmeterThr   { get; init; }  // Float32 → WordCount: 2

    // ── Ethernet ─────────────────────────────────────────────────────────────
    public RegisterField? AddrDhcp           { get; init; }
    public RegisterField? AddrIpAddress      { get; init; }  // WordCount: 2
    public RegisterField? AddrSubnetMask     { get; init; }  // WordCount: 2
    public RegisterField? AddrGateway        { get; init; }  // WordCount: 2
    public RegisterField? AddrMacAddress     { get; init; }  // WordCount: 3, read-only (3xxxx)
    public RegisterField? AddrDnsEnabled     { get; init; }
    public RegisterField? AddrDnsServer      { get; init; }  // WordCount: 2

    // ── Wireless ─────────────────────────────────────────────────────────────
    public RegisterField? AddrWirelessMode   { get; init; }
    public RegisterField? AddrSsid           { get; init; }  // string, N words
    public RegisterField? AddrWifiPassword   { get; init; }  // string, N words
    public RegisterField? AddrModuleVersion  { get; init; }  // string, N words, read-only
    public RegisterField? AddrWifiDhcp       { get; init; }
    public RegisterField? AddrWifiIp         { get; init; }  // WordCount: 2
    public RegisterField? AddrWifiMask       { get; init; }  // WordCount: 2
    public RegisterField? AddrWifiGateway    { get; init; }  // WordCount: 2
    public RegisterField? AddrWifiMac        { get; init; }  // WordCount: 3, read-only
    public RegisterField? AddrWifiDnsEnabled { get; init; }
    public RegisterField? AddrWifiDns        { get; init; }  // WordCount: 2
    public RegisterField? AddrBtDescription  { get; init; }  // string
    public RegisterField? AddrBtPassword     { get; init; }
    public RegisterField? AddrBtMac          { get; init; }  // WordCount: 3, read-only

    // ── SNTP ─────────────────────────────────────────────────────────────────
    public RegisterField? AddrSntpEnabled    { get; init; }
    public RegisterField? AddrTimezone       { get; init; }
    public RegisterField? AddrSyncInterval   { get; init; }
    public RegisterField? AddrNtpServer      { get; init; }  // string

    // ── IoT ──────────────────────────────────────────────────────────────────
    public RegisterField? AddrIotEnabled     { get; init; }
    public RegisterField? AddrSendInterval   { get; init; }
    public RegisterField? AddrSendOnHour     { get; init; }
    public RegisterField? AddrMqttBroker     { get; init; }
    public RegisterField? AddrMqttUrl        { get; init; }  // string
    public RegisterField? AddrMqttDescId     { get; init; }  // string
    public RegisterField? AddrMqttPort       { get; init; }
    public RegisterField? AddrMqttTopic      { get; init; }  // string
    public RegisterField? AddrMqttUser       { get; init; }  // string
    public RegisterField? AddrMqttToken      { get; init; }  // string
    public RegisterField? AddrKeepAlive      { get; init; }
    public RegisterField? AddrKronCloud      { get; init; }
    public RegisterField? AddrTls            { get; init; }

    // ── Relógio ───────────────────────────────────────────────────────────────
    public RegisterField? AddrClockDate      { get; init; }
    public RegisterField? AddrClockTime      { get; init; }
    public RegisterField? AddrClockSource    { get; init; }

    // ── Entradas e Saídas ─────────────────────────────────────────────────────
    public RegisterField? AddrDebounceEdp    { get; init; }

    /// <summary>
    /// All unique Modicon register addresses needed for a bulk read, sorted ascending.
    /// Bit-fields sharing the same register appear only once.
    /// </summary>
    public IReadOnlyList<ushort> AllAddresses =>
        GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.StartsWith("Addr", StringComparison.Ordinal)
                     && p.PropertyType == typeof(RegisterField?))
            .Select(p => (RegisterField?)p.GetValue(this))
            .Where(v => v.HasValue)
            .SelectMany(v => v!.Value.AllAddresses())
            .Distinct()
            .OrderBy(a => a)
            .ToList();
}
