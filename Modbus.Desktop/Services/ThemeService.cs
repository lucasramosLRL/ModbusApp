using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace Modbus.Desktop.Services;

public enum AppTheme { Dark, Light }

/// <summary>
/// Singleton that holds the selected UI theme, persists it to disk, and applies it
/// live by switching <see cref="Application.RequestedThemeVariant"/>. Mirrors the
/// <see cref="LocalizationService"/> / <see cref="RtuSettingsService"/> pattern.
/// Theme-variant colors live in /Themes/Colors.axaml (DynamicResource brushes).
/// </summary>
public partial class ThemeService : ObservableObject
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ModbusApp", "theme.txt");

    public static ThemeService Instance { get; } = new();

    private AppTheme _currentTheme;

    private ThemeService()
    {
        // Read saved preference into the backing field directly so we don't
        // trigger Save()/Apply() side-effects during construction.
        _currentTheme = LoadPreference();
    }

    public AppTheme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (!SetProperty(ref _currentTheme, value)) return;
            SavePreference(value);
            Apply();
        }
    }

    /// <summary>Applies the current theme to the running application. Safe to call at startup.</summary>
    public void Apply()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = _currentTheme == AppTheme.Light
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
    }

    private static AppTheme LoadPreference()
    {
        try
        {
            if (File.Exists(PreferencePath))
            {
                var saved = File.ReadAllText(PreferencePath).Trim();
                if (Enum.TryParse<AppTheme>(saved, out var theme))
                    return theme;
            }
        }
        catch { /* silently use default */ }
        return AppTheme.Dark;
    }

    private static void SavePreference(AppTheme theme)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
            File.WriteAllText(PreferencePath, theme.ToString());
        }
        catch { /* non-critical */ }
    }
}
