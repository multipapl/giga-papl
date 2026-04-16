using System.Windows;
using BlenderToolbox.App.Services;
using BlenderToolbox.Core.Services;
using Microsoft.Win32;

namespace BlenderToolbox.App;

public partial class App : Application
{
    private ThemeManager? _themeManager;

    public GlobalSettingsService? GlobalSettingsService { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsStore = new JsonSettingsStore("BlenderToolbox");
        GlobalSettingsService = new GlobalSettingsService(settingsStore);
        GlobalSettingsService.MigrateLegacySettings();

        _themeManager = new ThemeManager(Resources.MergedDictionaries);
        _themeManager.ApplyTheme(GlobalSettingsService.Current.ThemeOverride);
        GlobalSettingsService.Changed += OnGlobalSettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        if (GlobalSettingsService is not null)
        {
            GlobalSettingsService.Changed -= OnGlobalSettingsChanged;
        }

        base.OnExit(e);
    }

    private void OnGlobalSettingsChanged(object? sender, EventArgs e)
    {
        if (GlobalSettingsService is null)
        {
            return;
        }

        Dispatcher.Invoke(() => _themeManager?.ApplyTheme(GlobalSettingsService.Current.ThemeOverride));
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Color or UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
        {
            Dispatcher.Invoke(() =>
            {
                if (_themeManager?.CurrentOverride == Core.Presentation.ThemeOverride.Auto)
                {
                    _themeManager.ApplyTheme(Core.Presentation.ThemeOverride.Auto);
                }
            });
        }
    }
}
