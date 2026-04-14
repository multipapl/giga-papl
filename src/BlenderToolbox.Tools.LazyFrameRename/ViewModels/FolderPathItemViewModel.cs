using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.LazyFrameRename.ViewModels;

public partial class FolderPathItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string path = string.Empty;
}
