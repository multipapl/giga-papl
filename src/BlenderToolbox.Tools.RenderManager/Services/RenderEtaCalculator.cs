namespace BlenderToolbox.Tools.RenderManager.Services;

public static class RenderEtaCalculator
{
    public static string BuildEtaText(
        int totalFrames,
        int completedFrames,
        double completedFrameRenderSeconds,
        double currentFrameFraction = 0)
    {
        if (totalFrames <= 0 || completedFrames <= 0 || completedFrameRenderSeconds <= 0)
        {
            return string.Empty;
        }

        var clampedCurrentFrameFraction = Math.Clamp(currentFrameFraction, 0, 0.999);
        var averageFrameSeconds = completedFrameRenderSeconds / completedFrames;
        var remainingFrames = Math.Max(0, totalFrames - completedFrames - clampedCurrentFrameFraction);
        if (remainingFrames <= 0)
        {
            return string.Empty;
        }

        return FormatDuration(TimeSpan.FromSeconds(averageFrameSeconds * remainingFrames));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }
}
