using System.Text;
using System.Text.Json;
using BlenderToolbox.Tools.RenderManager.ViewModels;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RenderOverrideScriptBuilder
{
    public string Build(RenderQueueItemViewModel job)
    {
        if (!ShouldCreateOverrideScript(job))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("import bpy");
        builder.AppendLine();
        builder.AppendLine("scene = bpy.context.scene");

        if (job.HasSceneOverride)
        {
            builder.AppendLine();
            builder.AppendLine($"scene_name = {Serialize(job.SceneName)}");
            builder.AppendLine("scene = bpy.data.scenes.get(scene_name)");
            builder.AppendLine("if scene is None:");
            builder.AppendLine("    raise RuntimeError(f'Scene not found: {scene_name}')");
        }

        if (job.HasCameraOverride)
        {
            builder.AppendLine();
            builder.AppendLine($"camera_name = {Serialize(job.CameraName)}");
            builder.AppendLine("camera = bpy.data.objects.get(camera_name)");
            builder.AppendLine("if camera is None or camera.type != 'CAMERA':");
            builder.AppendLine("    raise RuntimeError(f'Camera not found: {camera_name}')");
            builder.AppendLine("scene.camera = camera");
        }

        if (job.HasViewLayerOverride)
        {
            builder.AppendLine();
            builder.AppendLine($"view_layer_name = {Serialize(job.ViewLayerName)}");
            builder.AppendLine("view_layer = scene.view_layers.get(view_layer_name)");
            builder.AppendLine("if view_layer is None:");
            builder.AppendLine("    raise RuntimeError(f'View layer not found: {view_layer_name}')");
            builder.AppendLine("for item in scene.view_layers:");
            builder.AppendLine("    item.use = item.name == view_layer_name");
        }

        if (!string.IsNullOrWhiteSpace(job.ResolvedOutputPattern) &&
            (job.UsesOutputFallback || job.HasOutputPathOverride || job.HasOutputNameOverride))
        {
            builder.AppendLine();
            builder.AppendLine($"scene.render.filepath = {Serialize(job.ResolvedOutputPattern)}");
        }

        return builder.ToString();
    }

    private static bool ShouldCreateOverrideScript(RenderQueueItemViewModel job)
    {
        return job.HasSceneOverride ||
               job.HasCameraOverride ||
               job.HasViewLayerOverride ||
               job.HasOutputNameOverride ||
               job.HasOutputPathOverride ||
               (job.UsesOutputFallback && !string.IsNullOrWhiteSpace(job.ResolvedOutputPattern));
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }
}
