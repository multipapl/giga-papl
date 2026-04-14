namespace BlenderToolbox.Tools.LazyFrameRename.Models;

public sealed class LazyFrameRenameSettings
{
    public RenameMode Mode { get; set; } = RenameMode.Manual;

    public List<string> ManualFolders { get; set; } = [];

    public string ParentFolder { get; set; } = string.Empty;

    public string FrameName { get; set; } = string.Empty;

    public bool DigitsAuto { get; set; } = true;

    public string DigitsValue { get; set; } = "4";
}
