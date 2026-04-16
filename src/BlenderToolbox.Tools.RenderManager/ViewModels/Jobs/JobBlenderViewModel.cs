using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobBlenderViewModel : ObservableObject
{
    [ObservableProperty]
    private string blenderExecutablePath = string.Empty;
}
