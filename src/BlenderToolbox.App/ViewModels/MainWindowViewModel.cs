using System.Collections.ObjectModel;
using BlenderToolbox.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IEnumerable<IToolDefinition> tools, SettingsScreenViewModel settingsScreen)
    {
        Tools = new ObservableCollection<IToolDefinition>(tools);
        SettingsScreen = settingsScreen;
        AppSurfaces = new ObservableCollection<SettingsScreenViewModel>([settingsScreen]);
        SelectedTool = Tools.FirstOrDefault();
        SelectedSurface = SelectedTool;
    }

    public ObservableCollection<SettingsScreenViewModel> AppSurfaces { get; }

    public ObservableCollection<IToolDefinition> Tools { get; }

    public SettingsScreenViewModel SettingsScreen { get; }

    public string WindowTitle => SelectedSurface is null
        ? "Giga Papl"
        : $"Giga Papl - {SelectedSurfaceDisplayName}";

    public string SelectedSurfaceDescription => SelectedSurface switch
    {
        IToolDefinition tool => tool.Description,
        SettingsScreenViewModel settings => settings.Description,
        _ => string.Empty,
    };

    public string SelectedSurfaceDisplayName => SelectedSurface switch
    {
        IToolDefinition tool => tool.DisplayName,
        SettingsScreenViewModel settings => settings.DisplayName,
        _ => string.Empty,
    };

    [ObservableProperty]
    private IToolDefinition? selectedTool;

    [ObservableProperty]
    private SettingsScreenViewModel? selectedAppSurface;

    [ObservableProperty]
    private object? selectedSurface;

    partial void OnSelectedAppSurfaceChanged(SettingsScreenViewModel? value)
    {
        if (value is not null)
        {
            selectedTool = null;
            OnPropertyChanged(nameof(SelectedTool));
            SelectedSurface = value;
        }

        RefreshSelectedSurfaceText();
    }

    partial void OnSelectedSurfaceChanged(object? value)
    {
        RefreshSelectedSurfaceText();
    }

    partial void OnSelectedToolChanged(IToolDefinition? value)
    {
        if (value is not null)
        {
            selectedAppSurface = null;
            OnPropertyChanged(nameof(SelectedAppSurface));
            SelectedSurface = value;
        }

        RefreshSelectedSurfaceText();
    }

    public void SaveState()
    {
        foreach (var tool in Tools.OfType<IStatefulTool>())
        {
            tool.SaveState();
        }
    }

    private void RefreshSelectedSurfaceText()
    {
        OnPropertyChanged(nameof(SelectedSurfaceDisplayName));
        OnPropertyChanged(nameof(SelectedSurfaceDescription));
        OnPropertyChanged(nameof(WindowTitle));
    }
}
