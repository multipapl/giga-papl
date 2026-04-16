using System.IO;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Core.Services;
using BlenderToolbox.Tools.SplitByContext.Models;
using BlenderToolbox.Tools.SplitByContext.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderToolbox.Tools.SplitByContext.ViewModels;

public partial class SplitByContextViewModel : ObservableObject
{
    private const string SettingsFileName = "split-by-context.json";

    private readonly IFilePickerService _filePickerService;
    private readonly GlobalSettingsService _globalSettingsService;
    private readonly IJsonSettingsStore _settingsStore;
    private readonly SplitByContextService _splitByContextService;

    public SplitByContextViewModel(
        SplitByContextService splitByContextService,
        IJsonSettingsStore settingsStore,
        GlobalSettingsService globalSettingsService,
        IFilePickerService filePickerService)
    {
        _splitByContextService = splitByContextService;
        _settingsStore = settingsStore;
        _globalSettingsService = globalSettingsService;
        _filePickerService = filePickerService;
        _globalSettingsService.Changed += OnGlobalSettingsChanged;

        LoadSettings();
        SetStatus("Ready", StatusTone.Neutral);
    }

    public string BlenderExecutablePath => _globalSettingsService.Current.BlenderExecutablePath.Trim();

    public string BlenderHelperText => string.IsNullOrWhiteSpace(BlenderExecutablePath)
        ? "Blender is not configured. Open Settings."
        : $"Blender: {BlenderExecutablePath}";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string logFilePath = string.Empty;

    [ObservableProperty]
    private string sceneFilePath = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private StatusTone statusTone = StatusTone.Neutral;

    public void SaveSettings()
    {
        var settings = new SplitByContextSettings
        {
            SceneFilePath = SceneFilePath.Trim(),
        };

        _settingsStore.Save(SettingsFileName, settings);
    }

    [RelayCommand]
    private void BrowseSceneFile()
    {
        var selectedPath = _filePickerService.PickFile(
            "Blend files|*.blend|All files|*.*",
            Path.GetDirectoryName(SceneFilePath),
            "Choose the scene file");

        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SceneFilePath = selectedPath;
        }
    }

    [RelayCommand]
    private async Task SplitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SetStatus("Working...", StatusTone.Neutral);

            var result = await _splitByContextService.SplitAsync(new SplitByContextRequest
            {
                ExecutablePath = BlenderExecutablePath,
                SceneFilePath = SceneFilePath.Trim(),
            });

            LogFilePath = result.LogFilePath;
            var summary = result.CreatedFiles.Count == 0
                ? $"Finished. Check the log for details: {result.LogFilePath}"
                : $"Created {result.CreatedFiles.Count} file(s). Log: {result.LogFilePath}";

            SetStatus(summary, StatusTone.Success);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, StatusTone.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load<SplitByContextSettings>(SettingsFileName);
        SceneFilePath = settings.SceneFilePath ?? string.Empty;
    }

    private void OnGlobalSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BlenderExecutablePath));
        OnPropertyChanged(nameof(BlenderHelperText));
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusMessage = message;
        StatusTone = tone;
    }
}
