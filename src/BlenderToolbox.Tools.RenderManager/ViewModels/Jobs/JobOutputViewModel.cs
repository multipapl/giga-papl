using CommunityToolkit.Mvvm.ComponentModel;

namespace BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

public partial class JobOutputViewModel : ObservableObject
{
    public const string BlenderDefaultLabel = "Blender Default";

    public bool HasOutputNameOverride
    {
        get => !string.IsNullOrWhiteSpace(OutputFileNameTemplate);
        set => SetOutputNameOverride(value, OutputFileNameTemplate);
    }

    public bool HasOutputPathOverride
    {
        get => !string.IsNullOrWhiteSpace(OutputPathTemplate);
        set => SetOutputPathOverride(value, OutputPathTemplate);
    }

    public void SetOutputNameOverride(bool enabled, string resolvedOutputName)
    {
        if (enabled == HasOutputNameOverride)
        {
            return;
        }

        OutputFileNameTemplate = enabled ? resolvedOutputName : string.Empty;
        OutputFileNameOverrideEnabled = HasOutputNameOverride;
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    public void SetOutputPathOverride(bool enabled, string resolvedOutputDirectory)
    {
        if (enabled == HasOutputPathOverride)
        {
            return;
        }

        OutputPathTemplate = enabled ? resolvedOutputDirectory : string.Empty;
        OutputPathOverrideEnabled = HasOutputPathOverride;
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }

    [ObservableProperty]
    private bool outputFileNameOverrideEnabled;

    [ObservableProperty]
    private string outputFileNameTemplate = string.Empty;

    [ObservableProperty]
    private bool outputPathOverrideEnabled;

    [ObservableProperty]
    private string outputPathTemplate = string.Empty;

    partial void OnOutputFileNameOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    partial void OnOutputFileNameTemplateChanged(string value)
    {
        SyncOutputNameOverrideFlag();
        OnPropertyChanged(nameof(HasOutputNameOverride));
    }

    partial void OnOutputPathOverrideEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }

    partial void OnOutputPathTemplateChanged(string value)
    {
        SyncOutputPathOverrideFlag();
        OnPropertyChanged(nameof(HasOutputPathOverride));
    }

    private void SyncOutputNameOverrideFlag()
    {
        var hasOverride = HasOutputNameOverride;
        if (OutputFileNameOverrideEnabled != hasOverride)
        {
            OutputFileNameOverrideEnabled = hasOverride;
        }
    }

    private void SyncOutputPathOverrideFlag()
    {
        var hasOverride = HasOutputPathOverride;
        if (OutputPathOverrideEnabled != hasOverride)
        {
            OutputPathOverrideEnabled = hasOverride;
        }
    }
}
