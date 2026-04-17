using System.IO;

namespace BlenderToolbox.Tools.RenderManager.Services;

public static class RenderPreviewFileFinder
{
    public static string FindLatestPreviewableFile(IReadOnlyList<string> folders)
    {
        var normalizedFolders = NormalizeExistingFolders(folders);
        if (normalizedFolders.Count == 0)
        {
            return string.Empty;
        }

        var directCandidate = FindLatestPreviewableFile(normalizedFolders, SearchOption.TopDirectoryOnly);
        return string.IsNullOrWhiteSpace(directCandidate)
            ? FindLatestPreviewableFile(normalizedFolders, SearchOption.AllDirectories)
            : directCandidate;
    }

    public static bool IsPreviewableImagePath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".exr", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindLatestPreviewableFile(IReadOnlyList<string> folders, SearchOption searchOption)
    {
        FileInfo? latest = null;

        foreach (var folder in folders)
        {
            var candidate = FindLatestPreviewableFile(folder, searchOption);
            if (candidate is null)
            {
                continue;
            }

            if (latest is null || candidate.LastWriteTimeUtc > latest.LastWriteTimeUtc)
            {
                latest = candidate;
            }
        }

        return latest?.FullName ?? string.Empty;
    }

    private static FileInfo? FindLatestPreviewableFile(string folder, SearchOption searchOption)
    {
        FileInfo? latest = null;

        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.*", searchOption))
            {
                if (!IsPreviewableImagePath(path))
                {
                    continue;
                }

                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    continue;
                }

                if (latest is null || info.LastWriteTimeUtc > latest.LastWriteTimeUtc)
                {
                    latest = info;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return latest;
    }

    private static List<string> NormalizeExistingFolders(IReadOnlyList<string> folders)
    {
        var normalizedFolders = new List<string>();

        foreach (var folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            var trimmed = folder.Trim();
            try
            {
                if (Directory.Exists(trimmed))
                {
                    normalizedFolders.Add(trimmed);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return normalizedFolders
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
