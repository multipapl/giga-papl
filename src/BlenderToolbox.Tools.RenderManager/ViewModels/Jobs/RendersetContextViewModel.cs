using BlenderToolbox.Tools.RenderManager.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class RendersetContextViewModel : ObservableObject
{
    public RendersetContextViewModel(RendersetContextSnapshot snapshot, bool isSelected)
    {
        Index = snapshot.Index;
        Name = snapshot.Name.Trim();
        RenderType = snapshot.RenderType.Trim();
        CameraName = snapshot.CameraName.Trim();
        OutputFolderHint = snapshot.OutputFolderHint.Trim();
        IncludeInRenderAll = snapshot.IncludeInRenderAll;
        IsSelected = isSelected;
    }

    public int Index { get; }

    public string Name { get; }

    public string RenderType { get; }

    public string CameraName { get; }

    public string OutputFolderHint { get; }

    public bool IncludeInRenderAll { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Context {Index + 1}" : Name;

    public string RenderTypeDisplay => string.IsNullOrWhiteSpace(RenderType) ? "unknown" : RenderType;

    public string CameraDisplay => string.IsNullOrWhiteSpace(CameraName) ? "No camera" : CameraName;

    [ObservableProperty]
    private bool isSelected;
}
