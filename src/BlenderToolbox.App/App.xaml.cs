using System.Windows;
using BlenderToolbox.App.Services;
using Microsoft.Win32;

namespace BlenderToolbox.App;

public partial class App : Application
{
    private ThemeManager? _themeManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _themeManager = new ThemeManager(Resources.MergedDictionaries);
        _themeManager.ApplySystemTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            Dispatcher.Invoke(() => _themeManager?.ApplySystemTheme());
        }
    }
}
