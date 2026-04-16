using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.ViewModels;
using BlenderToolbox.Tools.RenderManager.ViewModels.Jobs;

namespace BlenderToolbox.Core.Tests;

public sealed class RenderQueueItemCleanupBehaviorTests
{
    [Theory]
    [InlineData(InspectionState.NotInspected, "Blend defaults are not inspected yet.", "Waiting for inspection...", false)]
    [InlineData(InspectionState.Inspecting, "Inspecting blend...", "Inspecting blend...", false)]
    [InlineData(InspectionState.Failed, "Inspection failed. Click Update.", "Inspection failed. Click Update.", false)]
    [InlineData(InspectionState.Ready, "Inspected ", "Blender Default = from blend:", true)]
    public void InspectionStateControlsHintsAndSelectorAvailability(
        InspectionState state,
        string expectedSummary,
        string expectedHint,
        bool expectedSelectorsEnabled)
    {
        var job = CreateInspectedJob();
        job.HasSceneOverride = true;
        job.HasCameraOverride = true;
        job.HasViewLayerOverride = true;

        if (state == InspectionState.NotInspected)
        {
            job.Inspection = null;
        }

        job.InspectionState = state;

        Assert.Contains(expectedSummary, job.InspectionSummary);
        Assert.Contains(expectedHint, job.SceneHint);
        Assert.Contains(expectedHint, job.CameraHint);
        Assert.Contains(expectedHint, job.ViewLayerHint);
        Assert.Equal(expectedSelectorsEnabled, job.IsInspectionReady);
        Assert.Equal(expectedSelectorsEnabled, job.IsSceneSelectorEnabled);
        Assert.Equal(expectedSelectorsEnabled, job.IsCameraSelectorEnabled);
        Assert.Equal(expectedSelectorsEnabled, job.IsViewLayerSelectorEnabled);
    }

    [Fact]
    public void SceneChangePreservesCameraAndViewLayerSelections()
    {
        var job = CreateInspectedJob();
        job.CameraName = "Camera_A";
        job.ViewLayerName = "Layer_A";

        job.SceneName = "Scene_B";

        Assert.Equal("Camera_A", job.CameraName);
        Assert.Equal("Layer_A", job.ViewLayerName);
        Assert.Equal(["Camera_B"], job.AvailableCameraNames);
        Assert.Equal(["Layer_B"], job.AvailableViewLayerNames);
        Assert.Contains("is not in scene", job.CameraHint);
        Assert.Contains("is not in scene", job.ViewLayerHint);
    }

    [Fact]
    public void TargetingComboSelectionUsesBlenderDefaultAsNoOverride()
    {
        var job = CreateInspectedJob();

        Assert.Equal(JobTargetingViewModel.BlenderDefaultSelection, job.Targeting.SceneSelection);
        Assert.Equal(
            [JobTargetingViewModel.BlenderDefaultSelection, "Scene_A", "Scene_B"],
            job.Targeting.AvailableSceneOptions);

        job.Targeting.SceneSelection = "Scene_B";

        Assert.True(job.HasSceneOverride);
        Assert.Equal("Scene_B", job.SceneName);
        Assert.Equal("Scene_B", job.Targeting.SceneSelection);

        job.Targeting.SceneSelection = JobTargetingViewModel.BlenderDefaultSelection;

        Assert.False(job.HasSceneOverride);
        Assert.Equal(string.Empty, job.SceneName);
        Assert.Equal(JobTargetingViewModel.BlenderDefaultSelection, job.Targeting.SceneSelection);
    }

    [Fact]
    public void OutputOverridesFollowEmptyValueAsBlenderDefault()
    {
        var job = RenderJobViewModel.CreateNew(@"Q:\shots\scene.blend");

        Assert.False(job.Output.HasOutputPathOverride);
        Assert.False(job.Output.HasOutputNameOverride);
        Assert.Equal(JobOutputViewModel.BlenderDefaultLabel, job.OutputPathModeText);
        Assert.Equal(JobOutputViewModel.BlenderDefaultLabel, job.OutputNameModeText);

        job.Output.OutputPathTemplate = @"Q:\renders";
        job.Output.OutputFileNameTemplate = "shot_[FRAME]";

        Assert.True(job.Output.HasOutputPathOverride);
        Assert.True(job.Output.HasOutputNameOverride);
        Assert.Equal("Output path overridden", job.OutputPathModeText);
        Assert.Equal("Render name overridden", job.OutputNameModeText);

        job.Output.OutputPathTemplate = string.Empty;
        job.Output.OutputFileNameTemplate = string.Empty;
        var roundTripped = job.ToModel();

        Assert.False(job.Output.HasOutputPathOverride);
        Assert.False(job.Output.HasOutputNameOverride);
        Assert.False(roundTripped.OutputPathOverrideEnabled);
        Assert.False(roundTripped.OutputFileNameOverrideEnabled);
    }

    [Fact]
    public void FlushLogBufferAppendsBufferedLinesOnlyWhenPending()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        job.LogOutput = "existing log";

        Assert.False(job.FlushLogBuffer());
        Assert.Equal("existing log", job.LogOutput);

        job.AppendBufferedLogLine("first buffered line");
        job.AppendBufferedLogLine("second buffered line");

        Assert.Equal("existing log", job.LogOutput);

        Assert.True(job.FlushLogBuffer());
        Assert.Equal(
            $"existing log{Environment.NewLine}first buffered line{Environment.NewLine}second buffered line",
            job.LogOutput);
        Assert.False(job.FlushLogBuffer());
    }

    [Fact]
    public void CanDecodePreviewNowRequiresSelectionAndRespectsThrottle()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        var now = new DateTimeOffset(2026, 4, 16, 12, 0, 0, TimeSpan.Zero);
        var throttle = TimeSpan.FromSeconds(2);

        Assert.False(job.CanDecodePreviewNow(now, throttle));

        job.IsSelected = true;

        Assert.True(job.CanDecodePreviewNow(now, throttle));
        Assert.False(job.CanDecodePreviewNow(now.AddMilliseconds(1999), throttle));
        Assert.True(job.CanDecodePreviewNow(now.AddSeconds(2), throttle));

        job.IsSelected = false;

        Assert.False(job.CanDecodePreviewNow(now.AddSeconds(4), throttle));
    }

    [Fact]
    public void RenderQueueItemViewModel_StoresStateInSubViewModels()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");

        job.BlenderExecutablePath = @"C:\Blender\blender.exe";
        job.Mode = RenderMode.SingleFrame;
        job.SingleFrame = "42";
        job.OutputFileNameTemplate = "shot_[FRAME]";
        job.SceneName = "Scene_A";
        job.Status = RenderJobStatus.Rendering;

        Assert.Equal("scene", job.EffectiveName);
        Assert.Equal(@"C:\Blender\blender.exe", job.Blender.BlenderExecutablePath);
        Assert.Equal(RenderMode.SingleFrame, job.Frames.Mode);
        Assert.Equal("42", job.Frames.SingleFrame);
        Assert.Equal("shot_[FRAME]", job.Output.OutputFileNameTemplate);
        Assert.Equal("Scene_A", job.Targeting.SceneName);
        Assert.Equal(RenderJobStatus.Rendering, job.Runtime.Status);
    }

    [Fact]
    public void RenderQueueItemViewModel_DoesNotRaiseQueueAggregateNamesForUnrelatedOutputChanges()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        var raised = new List<string>();
        job.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                raised.Add(args.PropertyName);
            }
        };

        job.Output.OutputPathTemplate = @"Q:\renders";

        Assert.Contains(nameof(RenderQueueItemViewModel.OutputPathTemplate), raised);
        Assert.DoesNotContain(nameof(RenderQueueItemViewModel.Name), raised);
        Assert.DoesNotContain(nameof(RenderQueueItemViewModel.BlendFilePath), raised);
        Assert.DoesNotContain(nameof(RenderQueueItemViewModel.Status), raised);
    }

    [Fact]
    public void RenderJobViewModel_RoundTripsSlimModelThroughSubViewModels()
    {
        var model = new RenderQueueItem
        {
            Id = "job-1",
            Name = "Shot 020",
            IsEnabled = false,
            BlendFilePath = @"Q:\shots\scene.blend",
            BlenderExecutablePath = @"C:\Blender\blender.exe",
            Mode = RenderMode.FrameRange,
            FrameOverrideEnabled = true,
            StartFrame = "10",
            EndFrame = "20",
            Step = "2",
            SceneName = "Scene_A",
            SceneOverrideEnabled = true,
            CameraName = "Camera_A",
            CameraOverrideEnabled = true,
            ViewLayerName = "Layer_A",
            ViewLayerOverrideEnabled = true,
            OutputPathTemplate = @"Q:\renders",
            OutputPathOverrideEnabled = true,
            OutputFileNameTemplate = "shot_[FRAME]",
            OutputFileNameOverrideEnabled = true,
            Status = RenderJobStatus.Canceled,
        };

        var job = RenderJobViewModel.FromModel(model);
        var roundTripped = job.ToModel();

        Assert.Equal(string.Empty, job.Header.Name);
        Assert.Equal("scene", job.EffectiveName);
        Assert.Equal(RenderMode.FrameRange, job.Frames.Mode);
        Assert.Equal("Scene_A", job.Targeting.SceneName);
        Assert.Equal(@"Q:\renders", job.Output.OutputPathTemplate);
        Assert.Equal(RenderJobStatus.Canceled, job.Runtime.Status);
        Assert.Equal(model.Id, roundTripped.Id);
        Assert.Equal(string.Empty, roundTripped.Name);
        Assert.Equal(model.Mode, roundTripped.Mode);
        Assert.Equal(model.OutputFileNameTemplate, roundTripped.OutputFileNameTemplate);
        Assert.Equal(model.Status, roundTripped.Status);
    }

    private static RenderQueueItemViewModel CreateInspectedJob()
    {
        var job = RenderQueueItemViewModel.CreateNew(@"Q:\shots\scene.blend");
        job.ApplyInspection(new BlendInspectionSnapshot
        {
            SceneName = "Scene_A",
            CameraName = "Camera_A",
            ViewLayerName = "Layer_A",
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

        return job;
    }
}
