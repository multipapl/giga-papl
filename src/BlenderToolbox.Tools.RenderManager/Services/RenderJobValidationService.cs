using System.IO;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderJobValidationService
{
    public RenderJobValidationResult Validate(RenderQueueItemViewModel job, string globalBlenderPath)
    {
        var result = new RenderJobValidationResult();
        var blenderPath = string.IsNullOrWhiteSpace(job.BlenderExecutablePath)
            ? globalBlenderPath.Trim()
            : job.BlenderExecutablePath.Trim();

        if (string.IsNullOrWhiteSpace(blenderPath) || !File.Exists(blenderPath))
        {
            result.Errors.Add("Configure Blender in Settings.");
        }

        if (string.IsNullOrWhiteSpace(job.BlendFilePath) || !File.Exists(job.BlendFilePath.Trim()))
        {
            result.Errors.Add("Blend file was not found.");
        }

        if (job.Mode == RenderMode.FrameRange)
        {
            ValidateFrameRange(job, result);
        }
        else if (job.Mode == RenderMode.SingleFrame && !TryParseInteger(job.ResolvedSingleFrameText, out _))
        {
            result.Errors.Add("Single frame must be a whole number.");
        }

        if (job.HasSceneOverride && !Contains(job.AvailableSceneNames, job.SceneName))
        {
            result.Errors.Add($"Scene override was not found in the blend: {job.SceneName}");
        }

        if (job.HasCameraOverride && !Contains(job.AvailableCameraNames, job.CameraName))
        {
            result.Errors.Add($"Camera override was not found in the blend: {job.CameraName}");
        }

        if (job.HasViewLayerOverride && !Contains(job.AvailableViewLayerNames, job.ViewLayerName))
        {
            result.Errors.Add($"View layer override was not found in the blend: {job.ViewLayerName}");
        }

        if (string.IsNullOrWhiteSpace(job.ResolvedOutputDirectory))
        {
            result.Errors.Add("Output directory resolved to an empty value.");
        }

        if (string.IsNullOrWhiteSpace(job.ResolvedOutputName))
        {
            result.Errors.Add("Output file name resolved to an empty value.");
        }

        return result;
    }

    private static void ValidateFrameRange(RenderQueueItemViewModel job, RenderJobValidationResult result)
    {
        if (!TryParseInteger(job.ResolvedStartFrameText, out var start))
        {
            result.Errors.Add("Frame range start must be a whole number.");
        }

        if (!TryParseInteger(job.ResolvedEndFrameText, out var end))
        {
            result.Errors.Add("Frame range end must be a whole number.");
        }

        if (!TryParseInteger(job.ResolvedStepText, out var step) || step <= 0)
        {
            result.Errors.Add("Frame step must be a positive whole number.");
        }

        if (result.IsValid && end < start)
        {
            result.Errors.Add("Frame range end must be greater than or equal to start.");
        }
    }

    private static bool Contains(IReadOnlyList<string> values, string candidate)
    {
        return values.Any(value => string.Equals(value, candidate?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseInteger(string? value, out int parsed)
    {
        return int.TryParse(value?.Trim(), out parsed);
    }
}
