using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Core.Tests;

public sealed class RenderManagerTests
{
    [Fact]
    public void BlenderOutputParser_ParsesCyclesProgressLine()
    {
        var progress = BlenderOutputParser.TryParse(
            "00:03.579  render           | Fra: 1 | Remaining: 00:10.41 | Mem: 1039M | Sample 1/256");

        Assert.NotNull(progress);
        Assert.Equal(1, progress.Value.FrameNumber);
        Assert.Equal(1, progress.Value.SampleCurrent);
        Assert.Equal(256, progress.Value.SampleTotal);
        Assert.Equal(string.Empty, progress.Value.TimeElapsed);
        Assert.Equal("00:10.41", progress.Value.TimeRemaining);
        Assert.Equal("Sample 1/256", progress.Value.Status);
    }

    [Fact]
    public void BlenderOutputParser_ParsesSavedLine()
    {
        var progress = BlenderOutputParser.TryParse(@"00:10.032  render           | Saved: 'Q:\renders\shot_0081.exr'");

        Assert.NotNull(progress);
        Assert.Equal(@"Q:\renders\shot_0081.exr", progress.Value.SavedPath);
        Assert.False(progress.Value.IsFrameFinished);
    }

    [Fact]
    public void BlenderOutputParser_ParsesFinishedLineWithPrefixedTimestamp()
    {
        var progress = BlenderOutputParser.TryParse(
            "00:09.766  render           | Fra: 1 | Mem: 1039M | Finished");

        Assert.NotNull(progress);
        Assert.Equal(1, progress.Value.FrameNumber);
        Assert.True(progress.Value.IsFrameFinished);
        Assert.Equal("Finished", progress.Value.Status);
    }

    [Fact]
    public void BlenderOutputParser_ParsesAnimationFrameRange()
    {
        var progress = BlenderOutputParser.TryParse(
            "00:02.938  render           | Rendering animation (frames 1..10)");

        Assert.NotNull(progress);
        Assert.Equal(1, progress.Value.AnimationStartFrame);
        Assert.Equal(10, progress.Value.AnimationEndFrame);
    }

    [Fact]
    public void RenderQueueItemViewModel_ResetRuntimeState_ClearsRuntimeFieldsAndRestoresReadyStatus()
    {
        var job = RenderQueueItemViewModel.CreateNew(
            @"Q:\shots\scene.blend",
            @"C:\Program Files\Blender Foundation\Blender 4.0\blender.exe",
            @"[BLEND_PATH]\renders",
            "[BLEND_NAME]_[FRAME]");

        job.ProgressValue = 64;
        job.ProgressText = "Frame 12/20";
        job.ElapsedText = "02:14";
        job.EtaText = "00:45";
        job.CompletedFrameRenderSeconds = 96;
        job.ResumeCompletedFrameCount = 11;
        job.LastReportedFrameNumber = 12;
        job.LastCompletedFrameNumber = 11;
        job.LastKnownOutputPath = @"Q:\shots\renders\scene_0012.png";
        job.LastErrorSummary = "Blender exit code: 1";
        job.LastStartedUtc = DateTimeOffset.Now.AddMinutes(-3);
        job.LastCompletedUtc = DateTimeOffset.Now.AddMinutes(-1);
        job.LogOutput = "previous log";
        job.Status = RenderJobStatus.Failed;

        job.ResetRuntimeState();

        Assert.Equal(RenderJobStatus.Ready, job.Status);
        Assert.Equal(0, job.ProgressValue);
        Assert.Equal("Waiting", job.ProgressText);
        Assert.Equal(string.Empty, job.ElapsedText);
        Assert.Equal(string.Empty, job.EtaText);
        Assert.Equal(0, job.CompletedFrameRenderSeconds);
        Assert.Equal(0, job.ResumeCompletedFrameCount);
        Assert.Equal(0, job.LastReportedFrameNumber);
        Assert.Equal(0, job.LastCompletedFrameNumber);
        Assert.Equal(string.Empty, job.LastKnownOutputPath);
        Assert.Equal(string.Empty, job.LastErrorSummary);
        Assert.Null(job.LastStartedUtc);
        Assert.Null(job.LastCompletedUtc);
        Assert.Contains("Queue item reset.", job.LogOutput);
    }

    [Fact]
    public void RenderResumePlanner_ResumesPartialFrameFromCurrentFrame()
    {
        var job = RenderQueueItemViewModel.CreateNew(
            @"Q:\shots\scene.blend",
            @"C:\Program Files\Blender Foundation\Blender 4.0\blender.exe",
            @"[BLEND_PATH]\renders",
            "[BLEND_NAME]_[FRAME]");

        job.Mode = RenderMode.FrameRange;
        job.StartFrame = "10";
        job.EndFrame = "20";
        job.Step = "2";
        job.ResumeCompletedFrameCount = 3;
        job.LastCompletedFrameNumber = 14;
        job.LastReportedFrameNumber = 16;

        var plan = RenderResumePlanner.Create(job);

        Assert.Equal(16, plan.ResumeStartFrame);
        Assert.Equal(3, plan.CompletedFrameCount);
        Assert.True(plan.IsPartialFrameResume);
    }

    [Fact]
    public void RenderResumePlanner_ResumesAfterCompletedFrameFromNextStep()
    {
        var job = RenderQueueItemViewModel.CreateNew(
            @"Q:\shots\scene.blend",
            @"C:\Program Files\Blender Foundation\Blender 4.0\blender.exe",
            @"[BLEND_PATH]\renders",
            "[BLEND_NAME]_[FRAME]");

        job.Mode = RenderMode.FrameRange;
        job.StartFrame = "10";
        job.EndFrame = "20";
        job.Step = "2";
        job.ResumeCompletedFrameCount = 3;
        job.LastCompletedFrameNumber = 14;
        job.LastReportedFrameNumber = 14;

        var plan = RenderResumePlanner.Create(job);

        Assert.Equal(16, plan.ResumeStartFrame);
        Assert.Equal(3, plan.CompletedFrameCount);
        Assert.False(plan.IsPartialFrameResume);
    }

    [Fact]
    public void RenderEtaCalculator_UsesAverageCompletedFrameTimeForWholeJob()
    {
        var etaText = RenderEtaCalculator.BuildEtaText(
            totalFrames: 10,
            completedFrames: 4,
            completedFrameRenderSeconds: 80,
            currentFrameFraction: 0.5);

        Assert.Equal("01:50", etaText);
    }

    [Fact]
    public void RenderEtaCalculator_ReturnsEmptyWhenAverageCannotBeComputedYet()
    {
        var etaText = RenderEtaCalculator.BuildEtaText(
            totalFrames: 10,
            completedFrames: 0,
            completedFrameRenderSeconds: 0,
            currentFrameFraction: 0.5);

        Assert.Equal(string.Empty, etaText);
    }
}
