using CommunityToolkit.Mvvm.ComponentModel;
using Modbus.Desktop.Services.Strings;
using System;
using System.Collections.Generic;
using System.IO;

namespace Modbus.Desktop.Services;

public enum AppLanguage { English, Portuguese }

public class LocalizationService : ObservableObject
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModbusApp", "lang.txt");

    public static LocalizationService Instance { get; } = new();

    private Dictionary<string, string> _strings = EnglishStrings.All;
    private AppLanguage _currentLanguage = AppLanguage.Portuguese;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (!SetProperty(ref _currentLanguage, value)) return;
            LoadDictionary(value);
            SavePreference(value);
            // "Item[]" invalidates all indexer bindings in the Avalonia binding engine
            OnPropertyChanged("Item[]");
        }
    }

    private LocalizationService()
    {
        // Read saved preference directly into the backing field so we
        // don't trigger side-effects (SavePreference, notifications) on startup.
        _currentLanguage = LoadPreference();
        LoadDictionary(_currentLanguage);
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    private void LoadDictionary(AppLanguage lang) =>
        _strings = lang == AppLanguage.Portuguese
            ? PortugueseStrings.All
            : EnglishStrings.All;

    private static AppLanguage LoadPreference()
    {
        try
        {
            if (File.Exists(PreferencePath))
            {
                var saved = File.ReadAllText(PreferencePath).Trim();
                if (Enum.TryParse<AppLanguage>(saved, out var lang))
                    return lang;
            }
        }
        catch { /* silently use default */ }
        return AppLanguage.Portuguese;
    }

    private static void SavePreference(AppLanguage lang)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, lang.ToString());
        }
        catch { /* non-critical */ }
    }
}
