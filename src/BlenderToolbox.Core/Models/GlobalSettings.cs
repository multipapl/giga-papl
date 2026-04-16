using BlenderToolbox.Core.Presentation;

namespace BlenderToolbox.Core.Models;

public sealed class GlobalSettings
{
    public string BlenderExecutablePath { get; set; } = string.Empty;

    public ThemeOverride ThemeOverride { get; set; } = ThemeOverride.Auto;

    public bool LogsExpanded { get; set; } = true;
}
