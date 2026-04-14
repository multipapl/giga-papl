namespace BlenderToolbox.Tools.LazyFrameRename.Models;

public sealed record RenameExecutionResult(
    int TotalFilesRenamed,
    IReadOnlyList<string> ProcessedFolders);
