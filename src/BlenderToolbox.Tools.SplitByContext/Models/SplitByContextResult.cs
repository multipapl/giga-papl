namespace BlenderToolbox.Tools.SplitByContext.Models;

public sealed record SplitByContextResult(
    IReadOnlyList<string> CreatedFiles,
    string LogFilePath);
