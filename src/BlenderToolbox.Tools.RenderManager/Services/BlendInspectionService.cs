using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using BlenderToolbox.Tools.RenderManager.Models;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class BlendInspectionService
{
    private const string OutputMarker = "BT_RENDER_MANAGER_INSPECT::";
    private static readonly SemaphoreSlim InspectionSlots = new(2);

    private readonly RenderManagerPaths _paths;

    public BlendInspectionService(RenderManagerPaths paths)
    {
        _paths = paths;
    }

    public async Task<BlendInspectionSnapshot> InspectAsync(
        string blenderExecutablePath,
        string blendFilePath,
        CancellationToken cancellationToken = default)
    {
        await InspectionSlots.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(blenderExecutablePath))
            {
                throw new InvalidOperationException("Blender executable was not found.");
            }

            if (!File.Exists(blendFilePath))
            {
                throw new InvalidOperationException("Blend file was not found.");
            }

            var scriptPath = Path.Combine(_paths.InspectionDirectory, $"inspect_blend_defaults_{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(scriptPath, BuildScript(), Encoding.UTF8, cancellationToken);

            var startInfo = new ProcessStartInfo(blenderExecutablePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(blendFilePath) ?? _paths.InspectionDirectory,
            };

            startInfo.ArgumentList.Add("--background");
            startInfo.ArgumentList.Add(blendFilePath);
            startInfo.ArgumentList.Add("--python");
            startInfo.ArgumentList.Add(scriptPath);

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            }
            catch (TimeoutException)
            {
                KillProcessTree(process);
                throw;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr)
                    ? "Blend inspection failed."
                    : stderr.Trim();
                throw new InvalidOperationException(message);
            }

            var snapshot = TryParse(stdout) ?? TryParse(stderr);
            if (snapshot is null)
            {
                throw new InvalidOperationException("Blend inspection did not return parseable data.");
            }

            snapshot.InspectedAtUtc = DateTimeOffset.UtcNow;
            var blendInfo = new FileInfo(blendFilePath);
            snapshot.BlendFileSizeBytes = blendInfo.Length;
            snapshot.BlendFileLastWriteUtc = blendInfo.LastWriteTimeUtc;
            return snapshot;
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException("Inspection timed out. Click Update.", ex);
        }
        finally
        {
            InspectionSlots.Release();
        }
    }

    private static void KillProcessTree(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // Best effort: inspection failure will be surfaced to the caller.
        }
    }

    private static string BuildScript()
    {
        return """
            import bpy
            import json

            def safe_call(default, callback):
                try:
                    return callback()
                except Exception:
                    return default

            def get_scene_camera_names(scene):
                if scene is None:
                    return sorted({obj.name for obj in bpy.data.objects if obj.type == "CAMERA"})

                return sorted({obj.name for obj in scene.objects if obj.type == "CAMERA"})

            def get_default_view_layer(scene):
                if scene is None or len(scene.view_layers) == 0:
                    return ""

                for layer in scene.view_layers:
                    if getattr(layer, "use", True):
                        return layer.name

                return scene.view_layers[0].name

            def inspect_renderset_contexts(scene):
                contexts = getattr(scene, "renderset_contexts", None)
                if contexts is None:
                    return {
                        "HasRenderset": False,
                        "Contexts": [],
                    }

                items = []
                for index, context in enumerate(contexts):
                    camera = safe_call(None, lambda: context.get_camera()) if hasattr(context, "get_camera") else None
                    output_folder_hint = ""
                    if hasattr(context, "generate_output_folder_path"):
                        output_folder_hint = safe_call("", lambda: context.generate_output_folder_path())

                    items.append({
                        "Index": index,
                        "Name": getattr(context, "custom_name", "") or f"Context {index + 1}",
                        "IncludeInRenderAll": bool(getattr(context, "include_in_render_all", False)),
                        "RenderType": getattr(context, "render_type", "") or "",
                        "CameraName": camera.name if camera else "",
                        "OutputFolderHint": output_folder_hint or "",
                    })

                return {
                    "HasRenderset": len(items) > 0,
                    "Contexts": items,
                }

            scenes = list(bpy.data.scenes)
            scene = bpy.context.scene
            scene_camera_map = {
                item.name: get_scene_camera_names(item)
                for item in scenes
            }
            scene_view_layer_map = {
                item.name: [layer.name for layer in item.view_layers]
                for item in scenes
            }
            all_cameras = sorted({
                camera_name
                for camera_names in scene_camera_map.values()
                for camera_name in camera_names
            })
            all_view_layers = sorted({
                layer_name
                for layer_names in scene_view_layer_map.values()
                for layer_name in layer_names
            })
            scene_collection_map = {
                item.name: sorted({collection.name for collection in item.collection.children_recursive})
                for item in scenes
            }
            all_collections = sorted({
                collection_name
                for collection_names in scene_collection_map.values()
                for collection_name in collection_names
            })
            payload = {
                "AvailableCameras": all_cameras,
                "AvailableCollections": all_collections,
                "AvailableScenes": [item.name for item in scenes],
                "AvailableViewLayers": all_view_layers,
                "CameraName": scene.camera.name if scene and scene.camera else "",
                "FrameEnd": int(scene.frame_end) if scene else 0,
                "FrameStart": int(scene.frame_start) if scene else 0,
                "FrameStep": int(scene.frame_step) if scene else 1,
                "OutputFormat": scene.render.image_settings.file_format if scene else "",
                "RawOutputPath": scene.render.filepath if scene else "",
                "ResolvedOutputPath": bpy.path.abspath(scene.render.filepath) if scene and scene.render.filepath else "",
                "Renderset": inspect_renderset_contexts(scene),
                "SceneCameras": scene_camera_map,
                "SceneCollections": scene_collection_map,
                "SceneName": scene.name if scene else "",
                "SceneViewLayers": scene_view_layer_map,
                "ViewLayerName": get_default_view_layer(scene),
            }

            print("BT_RENDER_MANAGER_INSPECT::" + json.dumps(payload))
            """;
    }

    private static BlendInspectionSnapshot? TryParse(string processOutput)
    {
        if (string.IsNullOrWhiteSpace(processOutput))
        {
            return null;
        }

        foreach (var line in processOutput.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith(OutputMarker, StringComparison.Ordinal))
            {
                continue;
            }

            var json = line[OutputMarker.Length..];
            return JsonSerializer.Deserialize<BlendInspectionSnapshot>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
        }

        return null;
    }
}
