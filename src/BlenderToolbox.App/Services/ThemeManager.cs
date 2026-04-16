using System.Collections.ObjectModel;
using System.Windows;
using BlenderToolbox.Core.Presentation;
using Microsoft.Win32;

namespace BlenderToolbox.App.Services;

public sealed class ThemeManager
{
    private const string DarkThemeSource = "Themes/Theme.Dark.xaml";
    private const string LightThemeSource = "Themes/Theme.Light.xaml";

    private readonly Collection<ResourceDictionary> _mergedDictionaries;
    private ResourceDictionary? _currentThemeDictionary;

    public ThemeManager(Collection<ResourceDictionary> mergedDictionaries)
    {
        _mergedDictionaries = mergedDictionaries;
    }

    public ThemeOverride CurrentOverride { get; private set; } = ThemeOverride.Auto;

    public void ApplyTheme(ThemeOverride themeOverride)
    {
        CurrentOverride = themeOverride;
        var nextThemeSource = themeOverride switch
        {
            ThemeOverride.Light => LightThemeSource,
            ThemeOverride.Dark => DarkThemeSource,
            _ => IsLightThemeEnabled() ? LightThemeSource : DarkThemeSource,
        };

        ApplyThemeSource(nextThemeSource);
    }

    public void ApplySystemTheme()
    {
        var nextThemeSource = IsLightThemeEnabled() ? LightThemeSource : DarkThemeSource;
        ApplyThemeSource(nextThemeSource);
    }

    private void ApplyThemeSource(string nextThemeSource)
    {
        if (string.Equals(_currentThemeDictionary?.Source?.OriginalString, nextThemeSource, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_currentThemeDictionary is not null)
        {
            _mergedDictionaries.Remove(_currentThemeDictionary);
        }

        _currentThemeDictionary = new ResourceDictionary
        {
            Source = new Uri(nextThemeSource, UriKind.Relative),
        };

        _mergedDictionaries.Insert(0, _currentThemeDictionary);
    }

    private static bool IsLightThemeEnabled()
    {
        const string personalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        using var personalizeKey = Registry.CurrentUser.OpenSubKey(personalizeKeyPath);
        var appsUseLightTheme = personalizeKey?.GetValue("AppsUseLightTheme");
        return appsUseLightTheme switch
        {
            int intValue => intValue > 0,
            _ => false,
        };
    }
}
