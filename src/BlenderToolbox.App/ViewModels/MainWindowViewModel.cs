using System.Collections.ObjectModel;
using BlenderToolbox.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(IEnumerable<IToolDefinition> tools)
    {
        Tools = new ObservableCollection<IToolDefinition>(tools);
        SelectedTool = Tools.FirstOrDefault();
    }

    public ObservableCollection<IToolDefinition> Tools { get; }

    public string WindowTitle => SelectedTool is null
        ? "Giga Papl"
        : $"Giga Papl - {SelectedTool.DisplayName}";

    [ObservableProperty]
    private IToolDefinition? selectedTool;

    partial void OnSelectedToolChanged(IToolDefinition? value)
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    public void SaveState()
    {
        foreach (var tool in Tools.OfType<IStatefulTool>())
        {
            tool.SaveState();
        }
    }
}
