using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Tools.RenderManager.Services;

public readonly record struct RenderResumePlan(
    int ResumeStartFrame,
    int CompletedFrameCount,
    bool IsPartialFrameResume)
{
    public bool HasResumeStartFrame => ResumeStartFrame > 0;
}

public static class RenderResumePlanner
{
    public static RenderResumePlan Create(RenderQueueItemViewModel job)
    {
        var completedFrames = Math.Max(0, job.ResumeCompletedFrameCount);

        return job.Mode switch
        {
            RenderMode.Animation => new RenderResumePlan(
                DetermineResumeFrame(job.LastReportedFrameNumber, job.LastCompletedFrameNumber, step: 1, configuredStartFrame: 0),
                completedFrames,
                IsPartialFrame(job)),
            RenderMode.FrameRange => new RenderResumePlan(
                DetermineFrameRangeResumeFrame(job),
                completedFrames,
                IsPartialFrame(job)),
            RenderMode.SingleFrame => new RenderResumePlan(
                ParsePositiveOrDefault(job.SingleFrame, 1),
                0,
                false),
            _ => default,
        };
    }

    private static int DetermineFrameRangeResumeFrame(RenderQueueItemViewModel job)
    {
        var configuredStartFrame = ParsePositiveOrDefault(job.StartFrame, 0);
        var configuredEndFrame = ParsePositiveOrDefault(job.EndFrame, 0);
        var step = ParsePositiveOrDefault(job.Step, 1);
        var resumeFrame = DetermineResumeFrame(
            job.LastReportedFrameNumber,
            job.LastCompletedFrameNumber,
            step,
            configuredStartFrame);

        if (configuredEndFrame > 0 && resumeFrame > configuredEndFrame)
        {
            return configuredEndFrame;
        }

        return resumeFrame;
    }

    private static int DetermineResumeFrame(
        int lastReportedFrameNumber,
        int lastCompletedFrameNumber,
        int step,
        int configuredStartFrame)
    {
        if (lastReportedFrameNumber > lastCompletedFrameNumber)
        {
            return lastReportedFrameNumber;
        }

        if (lastCompletedFrameNumber > 0)
        {
            return lastCompletedFrameNumber + Math.Max(1, step);
        }

        return configuredStartFrame;
    }

    private static bool IsPartialFrame(RenderQueueItemViewModel job)
    {
        return job.LastReportedFrameNumber > 0 && job.LastReportedFrameNumber > job.LastCompletedFrameNumber;
    }

    private static int ParsePositiveOrDefault(string? value, int fallback)
    {
        return int.TryParse(value?.Trim(), out var parsedValue) && parsedValue > 0
            ? parsedValue
            : fallback;
    }
}
