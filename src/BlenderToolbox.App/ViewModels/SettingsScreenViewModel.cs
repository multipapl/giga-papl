using System.Diagnostics;
using System.IO;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Models;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderToolbox.App.ViewModels;

public partial class SettingsScreenViewModel : ObservableObject
{
    private readonly IFilePickerService _filePickerService;
    private readonly GlobalSettingsService _globalSettingsService;
    private bool _isLoading;

    public SettingsScreenViewModel(GlobalSettingsService globalSettingsService, IFilePickerService filePickerService)
    {
        _globalSettingsService = globalSettingsService;
        _filePickerService = filePickerService;

        ThemeOverrides = Enum.GetValues<ThemeOverride>();
        LogFolder = _globalSettingsService.LogFolder;
        AppDataFolder = _globalSettingsService.AppDataFolder;
        LoadFromCurrent();
    }

    public string Description => "Application-wide settings shared by Blender tools.";

    public string DisplayName => "Settings";

    public IReadOnlyList<ThemeOverride> ThemeOverrides { get; }

    public object? View { get; set; }

    [ObservableProperty]
    private string appDataFolder = string.Empty;

    [ObservableProperty]
    private string blenderExecutablePath = string.Empty;

    [ObservableProperty]
    private string blenderPathHelper = string.Empty;

    [ObservableProperty]
    private StatusTone blenderPathTone = StatusTone.Neutral;

    [ObservableProperty]
    private string logFolder = string.Empty;

    [ObservableProperty]
    private string saveError = string.Empty;

    [ObservableProperty]
    private ThemeOverride themeOverride = ThemeOverride.Auto;

    partial void OnBlenderExecutablePathChanged(string value)
    {
        if (_isLoading)
        {
            return;
        }

        Persist();
    }

    partial void OnThemeOverrideChanged(ThemeOverride value)
    {
        if (_isLoading)
        {
            return;
        }

        Persist();
    }

    [RelayCommand]
    private void BrowseBlender()
    {
        var selectedPath = _filePickerService.PickFile(
            "Executable files|*.exe|All files|*.*",
            Path.GetDirectoryName(BlenderExecutablePath),
            "Choose Blender executable");

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            BlenderExecutablePath = selectedPath;
        }
    }

    [RelayCommand]
    private void OpenAppDataFolder()
    {
        OpenFolder(AppDataFolder, createIfMissing: true);
    }

    [RelayCommand]
    private void RevealLogFolder()
    {
        OpenFolder(LogFolder, createIfMissing: true);
    }

    private void LoadFromCurrent()
    {
        _isLoading = true;
        try
        {
            var settings = _globalSettingsService.Current;
            BlenderExecutablePath = settings.BlenderExecutablePath ?? string.Empty;
            ThemeOverride = settings.ThemeOverride;
            RefreshBlenderPathHelper();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Persist()
    {
        try
        {
            _globalSettingsService.Save(new GlobalSettings
            {
                BlenderExecutablePath = BlenderExecutablePath.Trim(),
                ThemeOverride = ThemeOverride,
                LogsExpanded = _globalSettingsService.Current.LogsExpanded,
            });
            SaveError = string.Empty;
            RefreshBlenderPathHelper();
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
    }

    private void RefreshBlenderPathHelper()
    {
        if (string.IsNullOrWhiteSpace(BlenderExecutablePath))
        {
            BlenderPathHelper = "Blender executable is not configured.";
            BlenderPathTone = StatusTone.Error;
            return;
        }

        if (!File.Exists(BlenderExecutablePath.Trim()))
        {
            BlenderPathHelper = "Blender executable was not found.";
            BlenderPathTone = StatusTone.Error;
            return;
        }

        BlenderPathHelper = "Changes apply immediately to all tools.";
        BlenderPathTone = StatusTone.Success;
    }

    private static void OpenFolder(string folderPath, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        if (createIfMissing)
        {
            Directory.CreateDirectory(folderPath);
        }

        if (!Directory.Exists(folderPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(folderPath)
        {
            UseShellExecute = true,
        });
    }
}
