using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels;
using BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

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
    public void RendersetOutputParser_ParsesDoneMarker()
    {
        var parsed = RendersetOutputParser.TryParse(
            @"<<RSET_DONE>> {""Index"":1,""Name"":""Context B"",""Folders"":[""Q:/renders/b""]}");

        Assert.NotNull(parsed);
        Assert.Equal(RendersetRenderEventKind.Done, parsed.Kind);
        Assert.Equal(1, parsed.Index);
        Assert.Equal("Context B", parsed.Name);
        Assert.Equal(["Q:/renders/b"], parsed.Folders);
    }

    [Fact]
    public void RendersetOutputParser_ParsesFrameMarker()
    {
        var parsed = RendersetOutputParser.TryParse(
            @"<<RSET_FRAME>> {""Index"":0,""Name"":""Context A"",""Frame"":7,""Folder"":""Q:/renders/a""}");

        Assert.NotNull(parsed);
        Assert.Equal(RendersetRenderEventKind.Frame, parsed.Kind);
        Assert.Equal(0, parsed.Index);
        Assert.Equal("Context A", parsed.Name);
        Assert.Equal(7, parsed.Frame);
        Assert.Equal("Q:/renders/a", parsed.Folder);
    }

    [Fact]
    public void RendersetOutputParser_ParsesStartMarker()
    {
        var parsed = RendersetOutputParser.TryParse(
            @"<<RSET_START>> {""Index"":0,""Name"":""Context A"",""RenderType"":""animation"",""FrameStart"":10,""FrameEnd"":20,""FrameStep"":2}");

        Assert.NotNull(parsed);
        Assert.Equal(RendersetRenderEventKind.Start, parsed.Kind);
        Assert.Equal("animation", parsed.RenderType);
        Assert.Equal(10, parsed.FrameStart);
        Assert.Equal(20, parsed.FrameEnd);
        Assert.Equal(2, parsed.FrameStep);
    }

    [Fact]
    public void RenderQueueItemViewModel_ResetRuntimeState_ClearsRuntimeFieldsAndRestoresReadyStatus()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");

        job.ProgressValue = 64;
        job.ProgressText = "Frame 12/20";
        job.ElapsedText = "02:14";
        job.EtaText = "00:45";
        job.CompletedFrameRenderSeconds = 96;
        job.ResumeCompletedFrameCount = 11;
        job.LastReportedFrameNumber = 12;
        job.LastCompletedFrameNumber = 11;
        job.LastKnownOutputPath = @"Q:\shots\renders\scene_0012.png";
        job.LastKnownOutputFolderPath = @"Q:\shots\renders";
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
        Assert.Null(job.PreviewImageSource);
        Assert.Equal("Preview will appear after the first saved frame.", job.PreviewStatusText);
        Assert.Equal(string.Empty, job.LastKnownOutputPath);
        Assert.Equal(string.Empty, job.LastKnownOutputFolderPath);
        Assert.Equal("Waiting for the first saved frame.", job.PreviewPathText);
        Assert.Equal(string.Empty, job.LastErrorSummary);
        Assert.Null(job.LastStartedUtc);
        Assert.Null(job.LastCompletedUtc);
        Assert.Contains("Queue item reset.", job.LogOutput);
    }

    [Fact]
    public void RenderResumePlanner_ResumesPartialFrameFromCurrentFrame()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");

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
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");

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

    [Fact]
    public void RenderQueueItemViewModel_UsesFallbackOutputWhenBlendOutputIsMissing()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");

        job.ApplyInspection(new BlendInspectionSnapshot
        {
            RawOutputPath = "/tmp/",
            ResolvedOutputPath = @"Q:\tmp\",
        });

        Assert.True(job.UsesOutputFallback);
        Assert.Equal(@"Q:\shots\renders", job.ResolvedOutputDirectory);
        Assert.Equal("scene", job.ResolvedOutputName);
        Assert.Equal(@"Q:\shots\renders\scene_####", job.ResolvedOutputPattern);
    }

    [Fact]
    public void RenderQueueItemViewModel_SceneChangeRescopesCameraAndViewLayerLists()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        job.ApplyInspection(new BlendInspectionSnapshot
        {
            SceneName = "Scene_A",
            AvailableScenes = ["Scene_A", "Scene_B"],
            AvailableCameras = ["Camera_A", "Camera_B"],
            AvailableViewLayers = ["Layer_A", "Layer_B"],
            SceneCameras = new Dictionary<string, List<string>>
            {
                ["Scene_A"] = ["Camera_A"],
                ["Scene_B"] = ["Camera_B"],
            },
            SceneViewLayers = new Dictionary<string, List<string>>
            {
                ["Scene_A"] = ["Layer_A"],
                ["Scene_B"] = ["Layer_B"],
            },
        });

        job.SceneName = "Scene_B";

        Assert.Equal(["Camera_B"], job.AvailableCameraNames);
        Assert.Equal(["Layer_B"], job.AvailableViewLayerNames);
    }

    [Fact]
    public void RenderQueueItemViewModel_InvalidCameraSelectionIsPreservedWithHint()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        job.ApplyInspection(new BlendInspectionSnapshot
        {
            SceneName = "Scene_A",
            CameraName = "Camera_A",
            AvailableScenes = ["Scene_A", "Scene_B"],
            SceneCameras = new Dictionary<string, List<string>>
            {
                ["Scene_A"] = ["Camera_A"],
                ["Scene_B"] = ["Camera_B"],
            },
        });

        job.SceneName = "Scene_B";
        job.CameraName = "Camera_A";

        Assert.Equal("Camera_A", job.CameraName);
        Assert.Contains("is not in scene", job.CameraHint);
    }

    [Fact]
    public void JobRendersetViewModel_UpdatesSelectedContextNamesWhenContextsMutateDirectly()
    {
        var renderset = new JobRendersetViewModel();
        var contextA = new RendersetContextViewModel(
            new RendersetContextSnapshot { Index = 0, Name = "Context A" },
            isSelected: true);
        var contextB = new RendersetContextViewModel(
            new RendersetContextSnapshot { Index = 1, Name = "Context B" },
            isSelected: false);

        renderset.Contexts.Add(contextA);
        renderset.Contexts.Add(contextB);

        Assert.Equal(["Context A"], renderset.SelectedContextNames);

        contextB.IsSelected = true;
        renderset.Contexts.Remove(contextA);

        Assert.Equal(["Context B"], renderset.SelectedContextNames);
    }

    [Fact]
    public void RenderPreviewFileFinder_ReturnsLatestTopLevelPreviewableFile()
    {
        using var temp = new TempDirectoryScope();
        var oldPreview = Path.Combine(temp.RootPath, "old.png");
        var latestPreview = Path.Combine(temp.RootPath, "latest.exr");
        var ignoredFile = Path.Combine(temp.RootPath, "notes.txt");
        File.WriteAllText(oldPreview, "old");
        File.WriteAllText(latestPreview, "latest");
        File.WriteAllText(ignoredFile, "ignored");
        File.SetLastWriteTimeUtc(oldPreview, new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(latestPreview, new DateTime(2024, 01, 02, 0, 0, 0, DateTimeKind.Utc));

        var preview = RenderPreviewFileFinder.FindLatestPreviewableFile([temp.RootPath]);

        Assert.Equal(latestPreview, preview);
    }

    [Fact]
    public void RenderPreviewFileFinder_UsesRecursiveFallbackWhenTopLevelHasNoPreview()
    {
        using var temp = new TempDirectoryScope();
        var nestedFolder = Path.Combine(temp.RootPath, "nested");
        Directory.CreateDirectory(nestedFolder);
        File.WriteAllText(Path.Combine(temp.RootPath, "notes.txt"), "ignored");
        var nestedPreview = Path.Combine(nestedFolder, "frame.png");
        File.WriteAllText(nestedPreview, "preview");

        var preview = RenderPreviewFileFinder.FindLatestPreviewableFile([temp.RootPath]);

        Assert.Equal(nestedPreview, preview);
    }

    [Fact]
    public void BlendInspectionService_CanReuseInspectionWhenBlendFileSizeAndTimestampMatch()
    {
        using var temp = new TempDirectoryScope();
        var blendPath = Path.Combine(temp.RootPath, "scene.blend");
        var lastWriteUtc = new DateTime(2024, 01, 01, 12, 0, 0, DateTimeKind.Utc);
        File.WriteAllText(blendPath, "blend");
        File.SetLastWriteTimeUtc(blendPath, lastWriteUtc);
        var info = new FileInfo(blendPath);
        var snapshot = new BlendInspectionSnapshot
        {
            BlendFileSizeBytes = info.Length,
            BlendFileLastWriteUtc = lastWriteUtc,
        };

        Assert.True(BlendInspectionService.CanReuseInspection(snapshot, blendPath));

        File.AppendAllText(blendPath, "changed");
        File.SetLastWriteTimeUtc(blendPath, lastWriteUtc.AddMinutes(1));

        Assert.False(BlendInspectionService.CanReuseInspection(snapshot, blendPath));
    }

    private sealed class TempDirectoryScope : IDisposable
    {
        public TempDirectoryScope()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "giga-papl-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup for test temp files.
            }
        }
    }
}
