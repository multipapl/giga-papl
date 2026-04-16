namespace BlenderToolbox.Core.Abstractions;

public interface IFilePickerService
{
    string? PickFile(string filter, string? initialDirectory = null, string? title = null);

    IReadOnlyList<string> PickFiles(string filter, string? initialDirectory = null, string? title = null);
}
