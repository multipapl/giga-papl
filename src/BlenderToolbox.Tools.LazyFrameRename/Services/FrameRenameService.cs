using System.IO;
using System.Text.RegularExpressions;
using BlenderToolbox.Tools.LazyFrameRename.Models;

namespace BlenderToolbox.Tools.LazyFrameRename.Services;

public sealed class FrameRenameService
{
    private static readonly Regex TrailingDigitsRegex = new(@"(\d+)$", RegexOptions.Compiled);

    public FrameInfo ExtractFrameInfo(string fileName, int? digitsOverride = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A file name is required.", nameof(fileName));
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        if (digitsOverride is not null)
        {
            if (digitsOverride <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(digitsOverride), "Frame digits must be greater than zero.");
            }

            if (name.Length < digitsOverride)
            {
                throw new ArgumentException($"'{fileName}' does not contain {digitsOverride} trailing characters to trim.");
            }

            var suffix = name[^digitsOverride.Value..];
            if (!int.TryParse(suffix, out var startNumber))
            {
                throw new ArgumentException($"The last {digitsOverride} characters in '{fileName}' are not numeric.");
            }

            return new FrameInfo(name[..^digitsOverride.Value], extension, startNumber, digitsOverride.Value);
        }

        var match = TrailingDigitsRegex.Match(name);
        if (!match.Success)
        {
            throw new ArgumentException($"No trailing digits were found in '{fileName}'.");
        }

        return new FrameInfo(
            name[..match.Index],
            extension,
            int.Parse(match.Value),
            match.Value.Length);
    }

    public IReadOnlyList<RenamePreviewItem> BuildRenamePlan(
        IEnumerable<string> fileNames,
        string folderPath,
        string customPrefix = "",
        int? digitsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(fileNames);

        var orderedFiles = fileNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedFiles.Count == 0)
        {
            return [];
        }

        var frameInfo = ExtractFrameInfo(orderedFiles[0], digitsOverride);
        var plannedItems = new List<RenamePreviewItem>(orderedFiles.Count);

        for (var index = 0; index < orderedFiles.Count; index++)
        {
            var originalName = orderedFiles[index];
            var sourceName = Path.GetFileNameWithoutExtension(originalName);
            var extension = Path.GetExtension(originalName);

            string baseName;
            if (!string.IsNullOrWhiteSpace(customPrefix))
            {
                baseName = customPrefix;
            }
            else if (digitsOverride is not null)
            {
                if (sourceName.Length < digitsOverride)
                {
                    throw new ArgumentException($"'{originalName}' does not contain {digitsOverride} trailing characters to trim.");
                }

                baseName = sourceName[..^digitsOverride.Value];
            }
            else
            {
                var match = TrailingDigitsRegex.Match(sourceName);
                if (!match.Success)
                {
                    throw new ArgumentException($"No trailing digits were found in '{originalName}'.");
                }

                baseName = sourceName[..match.Index];
            }

            var nextFrameNumber = frameInfo.StartNumber + index;
            var newName = $"{baseName}{nextFrameNumber.ToString().PadLeft(frameInfo.Padding, '0')}{extension}";
            plannedItems.Add(new RenamePreviewItem(folderPath, originalName, newName));
        }

        EnsureNoDuplicateTargets(plannedItems);
        return plannedItems;
    }

    public RenameExecutionResult RenameFiles(RenameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var folders = ResolveFolders(request);
        if (folders.Count == 0)
        {
            throw new InvalidOperationException("No valid folders were selected.");
        }

        var totalRenamedFiles = 0;
        foreach (var folder in folders)
        {
            var fileNames = Directory
                .EnumerateFiles(folder)
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToList();

            var renamePlan = BuildRenamePlan(fileNames, folder, request.CustomPrefix, request.DigitsOverride);
            totalRenamedFiles += ExecuteFolderRename(renamePlan);
        }

        return new RenameExecutionResult(totalRenamedFiles, folders);
    }

    private static IReadOnlyList<string> ResolveFolders(RenameRequest request)
    {
        if (request.Mode == RenameMode.Subfolders)
        {
            if (!Directory.Exists(request.ParentFolder))
            {
                return [];
            }

            return Directory
                .EnumerateDirectories(request.ParentFolder)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return request.ManualFolders
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ExecuteFolderRename(IReadOnlyList<RenamePreviewItem> renamePlan)
    {
        if (renamePlan.Count == 0)
        {
            return 0;
        }

        var stagedMoves = renamePlan
            .Where(static item => !string.Equals(item.OldName, item.NewName, StringComparison.OrdinalIgnoreCase))
            .Select(static item => new StagedMove(item.FolderPath, item.OldName, item.NewName))
            .ToList();

        if (stagedMoves.Count == 0)
        {
            return 0;
        }

        try
        {
            foreach (var move in stagedMoves)
            {
                File.Move(move.OriginalPath, move.TempPath);
                move.CurrentPath = move.TempPath;
            }

            foreach (var move in stagedMoves)
            {
                File.Move(move.TempPath, move.TargetPath);
                move.CurrentPath = move.TargetPath;
            }
        }
        catch
        {
            RollBack(stagedMoves);
            throw;
        }

        return stagedMoves.Count;
    }

    private static void EnsureNoDuplicateTargets(IEnumerable<RenamePreviewItem> plannedItems)
    {
        var duplicateTarget = plannedItems
            .GroupBy(static item => item.NewName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateTarget is not null)
        {
            throw new InvalidOperationException($"The rename plan would create duplicate files named '{duplicateTarget.Key}'.");
        }
    }

    private static void RollBack(IEnumerable<StagedMove> stagedMoves)
    {
        foreach (var move in stagedMoves.Reverse())
        {
            if (!File.Exists(move.CurrentPath))
            {
                continue;
            }

            if (string.Equals(move.CurrentPath, move.OriginalPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Move(move.CurrentPath, move.OriginalPath);
            move.CurrentPath = move.OriginalPath;
        }
    }

    private sealed class StagedMove
    {
        public StagedMove(string folderPath, string oldName, string newName)
        {
            OriginalPath = Path.Combine(folderPath, oldName);
            TargetPath = Path.Combine(folderPath, newName);
            TempPath = Path.Combine(folderPath, $".bltbx_tmp_{Guid.NewGuid():N}{Path.GetExtension(oldName)}");
            CurrentPath = OriginalPath;
        }

        public string OriginalPath { get; }

        public string TempPath { get; }

        public string TargetPath { get; }

        public string CurrentPath { get; set; }
    }
}
