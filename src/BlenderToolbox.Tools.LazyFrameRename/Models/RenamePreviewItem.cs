namespace BlenderToolbox.Tools.LazyFrameRename.Models;

public sealed record RenamePreviewItem(
    string FolderPath,
    string OldName,
    string NewName);
