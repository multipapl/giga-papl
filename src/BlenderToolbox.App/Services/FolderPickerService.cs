using System.IO;
using BlenderToolbox.Core.Abstractions;
using Microsoft.Win32;

namespace BlenderToolbox.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
    public string? PickFolder(string? initialDirectory = null, string? title = null)
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
            Multiselect = false,
            Title = title ?? "Select folder",
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
