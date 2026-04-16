using BlenderToolbox.Core.Abstractions;
using BlenderToolbox.Tools.RenderManager.Models;
using BlenderToolbox.Tools.RenderManager.Services;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Core.Tests;

public sealed class RenderManagerArchitectureTests
{
    [Fact]
    public void RenderOutputTemplateService_ExpandsKnownTokens()
    {
        var service = new RenderOutputTemplateService();
        var context = new RenderQueueItemViewModelLike(
            BlendDirectory: @"Q:\shots\ep01",
            BlendFileName: "shot010",
            OutputPathTemplate: @"[BLEND_PATH]\renders\[SCENE_NAME]",
            OutputFileNameTemplate: @"[BLEND_NAME]_[CAMERA_NAME]_[FRAME]",
            ResolvedSceneName: "Scene_Main",
            ResolvedCameraName: "Cam_A",
            ResolvedViewLayerName: "Layer_01",
            QueueIndex: 3,
            Inspection: new BlendInspectionSnapshot
            {
                ResolvedOutputPath = @"Q:\shots\ep01\renders\legacy_####.png",
                RawOutputPath = @"Q:\shots\ep01\renders\legacy_####.png",
            });

        Assert.Equal(@"Q:\shots\ep01\renders\Scene_Main", service.ResolveOutputDirectory(context));
        Assert.Equal("shot010_Cam_A_####", service.ResolveOutputName(context));
        Assert.Equal(@"Q:\shots\ep01\renders\Scene_Main\shot010_Cam_A_####", service.BuildOutputPattern(context));
    }

    [Fact]
    public void RenderJobValidationService_FlagsMissingOverrides()
    {
        using var temp = new TempFilesScope();
        var job = RenderQueueItemViewModel.CreateNew(temp.BlendPath);
        job.BlenderExecutablePath = temp.BlenderPath;
        job.SceneOverrideEnabled = true;
        job.SceneName = "MissingScene";
        job.ApplyInspection(new BlendInspectionSnapshot
        {
            AvailableScenes = ["Scene_A"],
            ResolvedOutputPath = Path.Combine(temp.RootPath, "renders", "shot_####.png"),
            RawOutputPath = Path.Combine(temp.RootPath, "renders", "shot_####.png"),
        });

        var validation = new RenderJobValidationService().Validate(job, string.Empty);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Scene override", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderCommandBuilder_BuildsFrameRangeCommandAndScript()
    {
        using var temp = new TempFilesScope();
        var paths = new RenderManagerPaths($"BlenderToolbox.Tests.{Guid.NewGuid():N}");
        var builder = new RenderCommandBuilder(paths, new RenderOverrideScriptBuilder());
        var job = RenderQueueItemViewModel.CreateNew(temp.BlendPath);
        job.BlenderExecutablePath = temp.BlenderPath;
        job.Mode = RenderMode.FrameRange;
        job.FrameOverrideEnabled = true;
        job.StartFrame = "10";
        job.EndFrame = "20";
        job.Step = "2";
        job.SceneOverrideEnabled = true;
        job.SceneName = "Scene_A";
        job.CameraOverrideEnabled = true;
        job.CameraName = "Camera_A";
        job.OutputPathOverrideEnabled = true;
        job.OutputPathTemplate = Path.Combine(temp.RootPath, "renders");
        job.OutputFileNameOverrideEnabled = true;
        job.OutputFileNameTemplate = "shot_[FRAME]";

        var plan = builder.Build(job, temp.BlenderPath, default);

        Assert.Equal(temp.BlenderPath, plan.ExecutablePath);
        Assert.Contains("--background", plan.Arguments);
        Assert.Contains("--scene", plan.Arguments);
        Assert.Contains("Scene_A", plan.Arguments);
        Assert.Contains("-s", plan.Arguments);
        Assert.Contains("10", plan.Arguments);
        Assert.Contains("-e", plan.Arguments);
        Assert.Contains("20", plan.Arguments);
        Assert.Contains("-j", plan.Arguments);
        Assert.Contains("2", plan.Arguments);
        Assert.Contains("-a", plan.Arguments);
        Assert.Contains("--python", plan.Arguments);
        Assert.True(File.Exists(plan.OverrideScriptPath));
    }

    [Fact]
    public void RenderManagerStores_RoundTripSettingsAndQueue()
    {
        var backingStore = new MemorySettingsStore();
        var settingsStore = new RenderManagerSettingsStore(backingStore);
        var queueStore = new RenderQueueStore(backingStore);

        settingsStore.Save(new RenderManagerSettings
        {
            LastBlendDirectory = @"Q:\shots",
            LastBlenderDirectory = @"C:\Blender",
        });

        queueStore.Save(new RenderQueueState
        {
            SelectedJobId = "job-2",
            Items =
            [
                new RenderQueueItem { Id = "job-1", Name = "One" },
                new RenderQueueItem { Id = "job-2", Name = "Two", OutputFileNameTemplate = "two_[FRAME]" },
            ],
        });

        var loadedSettings = settingsStore.Load();
        var loadedQueue = queueStore.Load();

        Assert.Equal(@"Q:\shots", loadedSettings.LastBlendDirectory);
        Assert.Equal("job-2", loadedQueue.SelectedJobId);
        Assert.Equal(2, loadedQueue.Items.Count);
        Assert.Equal("two_[FRAME]", loadedQueue.Items[1].OutputFileNameTemplate);
    }

    private sealed class MemorySettingsStore : IJsonSettingsStore
    {
        private readonly Dictionary<string, string> _storage = new(StringComparer.OrdinalIgnoreCase);

        public T Load<T>(string fileName) where T : new()
        {
            return _storage.TryGetValue(fileName, out var json)
                ? System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? new T()
                : new T();
        }

        public void Save<T>(string fileName, T settings)
        {
            _storage[fileName] = System.Text.Json.JsonSerializer.Serialize(settings);
        }
    }

    private sealed class TempFilesScope : IDisposable
    {
        public TempFilesScope()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "giga-papl-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
            BlenderPath = Path.Combine(RootPath, "blender.exe");
            BlendPath = Path.Combine(RootPath, "scene.blend");
            File.WriteAllText(BlenderPath, "fake");
            File.WriteAllText(BlendPath, "fake");
        }

        public string BlendPath { get; }

        public string BlenderPath { get; }

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
                // best effort cleanup for test temp files
            }
        }
    }
}
