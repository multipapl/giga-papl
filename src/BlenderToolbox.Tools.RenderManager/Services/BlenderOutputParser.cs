using System.Text.RegularExpressions;

namespace BlenderToolbox.Tools.RenderManager.Services;

public readonly record struct BlenderProgress(
    int FrameNumber,
    int SampleCurrent,
    int SampleTotal,
    string TimeElapsed,
    string TimeRemaining,
    string Status,
    int AnimationStartFrame,
    int AnimationEndFrame,
    string? SavedPath,
    bool IsFrameFinished,
    bool IsBlenderQuit);

public static partial class BlenderOutputParser
{
    // Blender stdout patterns:
    //   Fra:81 Mem:4226.40M (Peak 29605.64M) | Time:00:13.32 | ... | Scene, ViewLayer | Sample 17/128
    //   Fra:81 ... | Time:02:23.66 | Remaining:01:48.62 | ... | Sample 64/128
    //   Fra:81 ... | Finished
    //   Saved: 'C:\path\to\file.exr'
    //   Blender quit

    [GeneratedRegex(@"Fra:\s*(\d+)")]
    private static partial Regex FrameRegex();

    [GeneratedRegex(@"Sample\s+(\d+)/(\d+)")]
    private static partial Regex SampleRegex();

    [GeneratedRegex(@"Time:\s*([\d:]+\.\d+)")]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"Remaining:\s*([\d:]+\.\d+)")]
    private static partial Regex RemainingRegex();

    [GeneratedRegex(@"\|\s*Finished\s*$")]
    private static partial Regex FinishedRegex();

    [GeneratedRegex(@"Saved:\s+'(.+?)'")]
    private static partial Regex SavedRegex();

    [GeneratedRegex(@"Rendering animation \(frames\s+(-?\d+)\.\.(-?\d+)\)")]
    private static partial Regex AnimationRangeRegex();

    public static BlenderProgress? TryParse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        if (line.Contains("Blender quit", StringComparison.Ordinal))
        {
            return new BlenderProgress(0, 0, 0, "", "", "Blender quit", 0, 0, null, false, true);
        }

        var savedMatch = SavedRegex().Match(line);
        if (savedMatch.Success)
        {
            return new BlenderProgress(0, 0, 0, "", "", "Saved", 0, 0, savedMatch.Groups[1].Value, false, false);
        }

        var animationRangeMatch = AnimationRangeRegex().Match(line);
        if (animationRangeMatch.Success)
        {
            return new BlenderProgress(
                0,
                0,
                0,
                "",
                "",
                "Animation range",
                int.Parse(animationRangeMatch.Groups[1].Value),
                int.Parse(animationRangeMatch.Groups[2].Value),
                null,
                false,
                false);
        }

        var frameMatch = FrameRegex().Match(line);
        if (!frameMatch.Success)
        {
            return null;
        }

        var frameNumber = int.Parse(frameMatch.Groups[1].Value);
        int sampleCur = 0, sampleTotal = 0;
        string timeElapsed = "", timeRemaining = "", status = "";
        var isFinished = false;

        var sampleMatch = SampleRegex().Match(line);
        if (sampleMatch.Success)
        {
            sampleCur = int.Parse(sampleMatch.Groups[1].Value);
            sampleTotal = int.Parse(sampleMatch.Groups[2].Value);
            status = $"Sample {sampleCur}/{sampleTotal}";
        }

        var timeMatch = TimeRegex().Match(line);
        if (timeMatch.Success)
        {
            timeElapsed = timeMatch.Groups[1].Value;
        }

        var remMatch = RemainingRegex().Match(line);
        if (remMatch.Success)
        {
            timeRemaining = remMatch.Groups[1].Value;
        }

        if (FinishedRegex().IsMatch(line))
        {
            isFinished = true;
            status = "Finished";
        }

        if (string.IsNullOrEmpty(status))
        {
            var lastPipe = line.LastIndexOf('|');
            if (lastPipe >= 0)
            {
                status = line[(lastPipe + 1)..].Trim();
            }
        }

        return new BlenderProgress(
            frameNumber, sampleCur, sampleTotal,
            timeElapsed, timeRemaining, status,
            0, 0, null, isFinished, false);
    }
}
