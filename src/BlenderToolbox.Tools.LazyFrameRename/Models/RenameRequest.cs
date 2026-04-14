namespace BlenderToolbox.Tools.LazyFrameRename.Models;

public sealed class RenameRequest
{
    public RenameMode Mode { get; init; } = RenameMode.Manual;

    public IReadOnlyList<string> ManualFolders { get; init; } = Array.Empty<string>();

    public string ParentFolder { get; init; } = string.Empty;

    public string CustomPrefix { get; init; } = string.Empty;

    public int? DigitsOverride { get; init; }
}
