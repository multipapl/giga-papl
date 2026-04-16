using System.IO;
using System.Text.RegularExpressions;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderOutputTemplateService
{
    private const string FramePlaceholder = "####";

    public string ResolveOutputDirectory(RenderQueueItemViewModelLike job)
    {
        var defaults = ResolveBlendOutputDefaults(job);
        var template = string.IsNullOrWhiteSpace(job.OutputPathTemplate)
            ? defaults.Directory
            : ExpandTemplate(job.OutputPathTemplate, job, defaults.Directory, defaults.Name, sanitizePathTokens: false);

        return template.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public string ResolveOutputName(RenderQueueItemViewModelLike job)
    {
        var defaults = ResolveBlendOutputDefaults(job);
        var template = string.IsNullOrWhiteSpace(job.OutputFileNameTemplate)
            ? defaults.Name
            : ExpandTemplate(job.OutputFileNameTemplate, job, defaults.Directory, defaults.Name, sanitizePathTokens: true);

        return SanitizeFileNameFragment(template.Trim());
    }

    public bool UsesFallback(RenderQueueItemViewModelLike job)
    {
        return ResolveBlendOutputDefaults(job).UsesFallback;
    }

    public string ResolveOriginalOutputName(RenderQueueItemViewModelLike job)
    {
        return ResolveBlendOutputDefaults(job).Name;
    }

    public string BuildOutputPattern(RenderQueueItemViewModelLike job)
    {
        var directory = ResolveOutputDirectory(job);
        var fileName = ResolveOutputName(job);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalized = fileName.Contains('#', StringComparison.Ordinal)
            ? fileName
            : fileName.Replace("[FRAME]", FramePlaceholder, StringComparison.OrdinalIgnoreCase);

        if (!normalized.Contains('#', StringComparison.Ordinal))
        {
            normalized = normalized.EndsWith("_", StringComparison.Ordinal) ||
                         normalized.EndsWith("-", StringComparison.Ordinal) ||
                         normalized.EndsWith(".", StringComparison.Ordinal)
                ? normalized + FramePlaceholder
                : normalized + "_" + FramePlaceholder;
        }

        return Path.Combine(directory, normalized);
    }

    public string BuildOriginalOutputDirectoryHint(RenderQueueItemViewModelLike job)
    {
        var defaults = ResolveBlendOutputDefaults(job);
        return $"{(defaults.UsesFallback ? "Empty = fallback" : "Empty = from blend")}: {defaults.Directory}";
    }

    public string BuildOriginalOutputNameHint(RenderQueueItemViewModelLike job)
    {
        var defaults = ResolveBlendOutputDefaults(job);
        return $"{(defaults.UsesFallback ? "Empty = fallback" : "Empty = from blend")}: {defaults.Name}";
    }

    private static OutputDefaults ResolveBlendOutputDefaults(RenderQueueItemViewModelLike job)
    {
        if (job.Inspection is null || LooksLikeDefaultBlendOutput(job.Inspection.RawOutputPath))
        {
            return BuildFallbackOutputDefaults(job);
        }

        var resolvedOutputPath = job.Inspection.ResolvedOutputPath.Trim();
        if (string.IsNullOrWhiteSpace(resolvedOutputPath))
        {
            return BuildFallbackOutputDefaults(job);
        }

        if (EndsWithDirectorySeparator(job.Inspection.RawOutputPath) || EndsWithDirectorySeparator(job.Inspection.ResolvedOutputPath))
        {
            return new OutputDefaults(
                resolvedOutputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                job.BlendFileName,
                false);
        }

        var directory = Path.GetDirectoryName(resolvedOutputPath) ?? string.Empty;
        var name = StripOutputName(Path.GetFileName(resolvedOutputPath));
        if (string.IsNullOrWhiteSpace(directory))
        {
            return BuildFallbackOutputDefaults(job);
        }

        return new OutputDefaults(directory, string.IsNullOrWhiteSpace(name) ? job.BlendFileName : name, false);
    }

    private static OutputDefaults BuildFallbackOutputDefaults(RenderQueueItemViewModelLike job)
    {
        var directory = string.IsNullOrWhiteSpace(job.BlendDirectory)
            ? string.Empty
            : Path.Combine(job.BlendDirectory, "renders");
        return new OutputDefaults(directory, job.BlendFileName, true);
    }

    private static string ExpandTemplate(
        string template,
        RenderQueueItemViewModelLike job,
        string originalOutputDirectory,
        string originalOutputName,
        bool sanitizePathTokens)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["[BLEND_NAME]"] = SanitizeValue(job.BlendFileName, sanitizePathTokens),
            ["[BLEND_PATH]"] = job.BlendDirectory,
            ["[SCENE_NAME]"] = SanitizeValue(job.ResolvedSceneName, sanitizePathTokens),
            ["[CAMERA_NAME]"] = SanitizeValue(job.ResolvedCameraName, sanitizePathTokens),
            ["[VIEWLAYER_NAME]"] = SanitizeValue(job.ResolvedViewLayerName, sanitizePathTokens),
            ["[FRAME]"] = FramePlaceholder,
            ["[JOB_INDEX]"] = Math.Max(1, job.QueueIndex).ToString(),
            ["[ORIGINAL_OUTPUT_PATH]"] = originalOutputDirectory,
            ["[ORIGINAL_OUTPUT_NAME]"] = SanitizeValue(originalOutputName, sanitizePathTokens),
        };

        var resolved = template;
        foreach (var replacement in replacements)
        {
            resolved = resolved.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
        }

        return resolved;
    }

    private static string SanitizeValue(string value, bool sanitize)
    {
        return sanitize ? SanitizeFileNameFragment(value) : value.Trim();
    }

    private static string SanitizeFileNameFragment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        return Regex.Replace(value.Trim(), $"[{invalidChars}]", "_");
    }

    private static bool EndsWithDirectorySeparator(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               (value.EndsWith(Path.DirectorySeparatorChar) || value.EndsWith(Path.AltDirectorySeparatorChar));
    }

    private static bool LooksLikeDefaultBlendOutput(string? rawOutputPath)
    {
        var normalized = (rawOutputPath ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/');

        return string.IsNullOrWhiteSpace(normalized) ||
               string.Equals(normalized, "/tmp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "//tmp", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "//", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripOutputName(string fileName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        return nameWithoutExtension.Replace("#", string.Empty).Trim();
    }

    private readonly record struct OutputDefaults(string Directory, string Name, bool UsesFallback);
}

public readonly record struct RenderQueueItemViewModelLike(
    string BlendDirectory,
    string BlendFileName,
    string OutputPathTemplate,
    string OutputFileNameTemplate,
    string ResolvedSceneName,
    string ResolvedCameraName,
    string ResolvedViewLayerName,
    int QueueIndex,
    BlendInspectionSnapshot? Inspection);
