using System.IO;
using BlenderToolbox.Core.Abstractions;
using Microsoft.Win32;

namespace BlenderToolbox.App.Services;

public sealed class FilePickerService : IFilePickerService
{
    public string? PickFile(string filter, string? initialDirectory = null, string? title = null)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
            Title = title ?? "Select file",
            Multiselect = false,
            CheckFileExists = true,
            CheckPathExists = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
