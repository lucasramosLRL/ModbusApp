using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Core.Domain.Enums;
using Modbus.Core.Domain.ValueObjects;
using System;
using System.IO;
using System.Text.Json;

namespace Modbus.Desktop.Services;

public partial class RtuSettingsService : ObservableObject
{
    public static RtuSettingsService Instance { get; } = new();

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModbusApp", "rtu-settings.json");

    [ObservableProperty] private string _portName  = "";
    [ObservableProperty] private int    _baudRate  = 9600;
    [ObservableProperty] private int    _dataBits  = 8;
    [ObservableProperty] private Parity _parity    = Parity.None;
    [ObservableProperty] private StopBits _stopBits = StopBits.One;

    private RtuSettingsService() => Load();

    partial void OnPortNameChanged(string value)   => Save();
    partial void OnBaudRateChanged(int value)       => Save();
    partial void OnDataBitsChanged(int value)       => Save();
    partial void OnParityChanged(Parity value)      => Save();
    partial void OnStopBitsChanged(StopBits value)  => Save();

    /// <summary>
    /// Human-readable summary of the current config, e.g. "COM5  9600  8N2".
    /// Empty string when no port has been selected yet.
    /// </summary>
    public string Summary => string.IsNullOrEmpty(PortName)
        ? ""
        : $"{PortName}  {BaudRate}  {DataBits}{ParityChar}{StopBitsChar}";

    private string ParityChar => Parity switch
    {
        Parity.Even => "E",
        Parity.Odd  => "O",
        _           => "N"
    };

    private string StopBitsChar => StopBits == StopBits.Two ? "2" : "1";

    public RtuConfig ToRtuConfig() => new()
    {
        PortName = PortName,
        BaudRate = BaudRate,
        DataBits = DataBits,
        Parity   = Parity,
        StopBits = StopBits
    };

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(SettingsPath));
            if (dto is null) return;
            // Set backing fields directly — Load() must not trigger Save() or notifications.
#pragma warning disable MVVMTK0034
            _portName  = dto.PortName  ?? "";
            _baudRate  = dto.BaudRate  > 0 ? dto.BaudRate : 9600;
            _dataBits  = dto.DataBits  > 0 ? dto.DataBits : 8;
            _parity    = Enum.TryParse<Parity>(dto.Parity, out var p)     ? p : Parity.None;
            _stopBits  = Enum.TryParse<StopBits>(dto.StopBits, out var sb) ? sb : StopBits.One;
#pragma warning restore MVVMTK0034
        }
        catch { /* silently use defaults */ }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var dto = new Dto
            {
                PortName = PortName,
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity   = Parity.ToString(),
                StopBits = StopBits.ToString()
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(dto));
        }
        catch { /* non-critical */ }

        OnPropertyChanged(nameof(Summary));
    }

    private sealed class Dto
    {
        public string? PortName  { get; set; }
        public int     BaudRate  { get; set; }
        public int     DataBits  { get; set; }
        public string? Parity    { get; set; }
        public string? StopBits  { get; set; }
    }
}
