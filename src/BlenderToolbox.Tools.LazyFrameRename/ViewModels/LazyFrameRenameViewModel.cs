using System.Collections.ObjectModel;
using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Core.Presentation;
using BlenderToolbox.Tools.LazyFrameRename.Models;
using BlenderToolbox.Tools.LazyFrameRename.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BlenderToolbox.Tools.LazyFrameRename.ViewModels;

public partial class LazyFrameRenameViewModel : ObservableObject
{
    private const string SettingsFileName = "lazy-frame-rename.json";

    private readonly IFolderPickerService _folderPickerService;
    private readonly FrameRenameService _frameRenameService;
    private readonly IJsonSettingsStore _settingsStore;

    public LazyFrameRenameViewModel(
        FrameRenameService frameRenameService,
        IJsonSettingsStore settingsStore,
        IFolderPickerService folderPickerService)
    {
        _frameRenameService = frameRenameService;
        _settingsStore = settingsStore;
        _folderPickerService = folderPickerService;

        LoadSettings();
        SetStatus("Ready", StatusTone.Neutral);
    }

    public ObservableCollection<FolderPathItemViewModel> ManualFolders { get; } = [];

    public bool IsManualDigitsMode => !IsDigitsAuto;

    public bool IsManualMode => Mode == RenameMode.Manual;

    public bool IsSubfoldersMode => Mode == RenameMode.Subfolders;

    [ObservableProperty]
    private string digitsValue = "4";

    [ObservableProperty]
    private string frameName = string.Empty;

    [ObservableProperty]
    private bool isDigitsAuto = true;

    [ObservableProperty]
    private RenameMode mode = RenameMode.Manual;

    [ObservableProperty]
    private string parentFolder = string.Empty;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private StatusTone statusTone = StatusTone.Neutral;

    public void SaveSettings()
    {
        var settings = new LazyFrameRenameSettings
        {
            Mode = Mode,
            ManualFolders = ManualFolders
                .Select(static folder => folder.Path.Trim())
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .ToList(),
            ParentFolder = ParentFolder.Trim(),
            FrameName = FrameName.Trim(),
            DigitsAuto = IsDigitsAuto,
            DigitsValue = string.IsNullOrWhiteSpace(DigitsValue) ? "4" : DigitsValue.Trim(),
        };

        _settingsStore.Save(SettingsFileName, settings);
    }

    partial void OnIsDigitsAutoChanged(bool value)
    {
        OnPropertyChanged(nameof(IsManualDigitsMode));
    }

    partial void OnModeChanged(RenameMode value)
    {
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(IsSubfoldersMode));

        if (value == RenameMode.Manual && ManualFolders.Count == 0)
        {
            ManualFolders.Add(new FolderPathItemViewModel());
        }
    }

    [RelayCommand]
    private void AddFolder()
    {
        ManualFolders.Add(new FolderPathItemViewModel());
    }

    [RelayCommand]
    private void BrowseFolder(FolderPathItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        var selectedPath = _folderPickerService.PickFolder(folder.Path, "Choose a folder to rename");
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            folder.Path = selectedPath;
        }
    }

    [RelayCommand]
    private void BrowseParentFolder()
    {
        var selectedPath = _folderPickerService.PickFolder(ParentFolder, "Choose the parent folder");
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            ParentFolder = selectedPath;
        }
    }

    [RelayCommand]
    private void EnableAutoDigits()
    {
        IsDigitsAuto = true;
    }

    [RelayCommand]
    private void EnableManualDigits()
    {
        IsDigitsAuto = false;
    }

    [RelayCommand]
    private void RemoveFolder(FolderPathItemViewModel? folder)
    {
        if (folder is null)
        {
            return;
        }

        ManualFolders.Remove(folder);
    }

    [RelayCommand]
    private void RenameFiles()
    {
        try
        {
            var request = BuildRequest();
            var result = _frameRenameService.RenameFiles(request);
            SetStatus(
                $"Done - {result.TotalFilesRenamed} file(s) renamed across {result.ProcessedFolders.Count} folder(s).",
                StatusTone.Success);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, StatusTone.Error);
        }
    }

    [RelayCommand]
    private void SetManualMode()
    {
        Mode = RenameMode.Manual;
    }

    [RelayCommand]
    private void SetSubfoldersMode()
    {
        Mode = RenameMode.Subfolders;
    }

    private RenameRequest BuildRequest()
    {
        int? digitsOverride = null;
        if (!IsDigitsAuto)
        {
            if (!int.TryParse(DigitsValue, out var parsedDigits) || parsedDigits <= 0)
            {
                throw new InvalidOperationException("Frame digits must be a positive whole number.");
            }

            digitsOverride = parsedDigits;
        }

        return new RenameRequest
        {
            Mode = Mode,
            ManualFolders = ManualFolders
                .Select(static folder => folder.Path.Trim())
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .ToList(),
            ParentFolder = ParentFolder.Trim(),
            CustomPrefix = FrameName.Trim(),
            DigitsOverride = digitsOverride,
        };
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load<LazyFrameRenameSettings>(SettingsFileName);

        Mode = settings.Mode;
        ParentFolder = settings.ParentFolder ?? string.Empty;
        FrameName = settings.FrameName ?? string.Empty;
        IsDigitsAuto = settings.DigitsAuto;
        DigitsValue = string.IsNullOrWhiteSpace(settings.DigitsValue) ? "4" : settings.DigitsValue;

        ManualFolders.Clear();
        foreach (var folderPath in settings.ManualFolders.Where(static path => !string.IsNullOrWhiteSpace(path)))
        {
            ManualFolders.Add(new FolderPathItemViewModel { Path = folderPath });
        }

        if (ManualFolders.Count == 0)
        {
            ManualFolders.Add(new FolderPathItemViewModel());
        }
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusMessage = message;
        StatusTone = tone;
    }
}
