namespace BlenderToolbox.Core.Abstractions;

public interface IFolderPickerService
{
    string? PickFolder(string? initialDirectory = null, string? title = null);
}
