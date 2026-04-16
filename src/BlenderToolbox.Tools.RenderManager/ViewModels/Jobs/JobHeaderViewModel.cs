using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobHeaderViewModel : ObservableObject
{
    public string EffectiveName => string.IsNullOrWhiteSpace(Name)
        ? BlendFileName
        : Name.Trim();

    public string BlendDirectory => string.IsNullOrWhiteSpace(BlendFilePath)
        ? string.Empty
        : Path.GetDirectoryName(BlendFilePath.Trim()) ?? string.Empty;

    public string BlendFileName => string.IsNullOrWhiteSpace(BlendFilePath)
        ? "Untitled job"
        : Path.GetFileNameWithoutExtension(BlendFilePath.Trim());

    [ObservableProperty]
    private string blendFilePath = string.Empty;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private string name = string.Empty;

    partial void OnBlendFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(BlendDirectory));
        OnPropertyChanged(nameof(BlendFileName));
        OnPropertyChanged(nameof(EffectiveName));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(EffectiveName));
    }
}
