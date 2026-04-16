using System.Text;
using System.Text.Json;
using BlenderToolbox.Tools.RenderManager.Models;
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

        if (job.HasCollectionOverride)
        {
            var collections = RenderCollectionOverrideParser.Parse(job.CollectionOverrides);
            builder.AppendLine();
            builder.AppendLine($"excluded_collections = set({Serialize(collections)})");
            builder.AppendLine("def walk_layer_collections(layer_collection):");
            builder.AppendLine("    yield layer_collection");
            builder.AppendLine("    for child in layer_collection.children:");
            builder.AppendLine("        yield from walk_layer_collections(child)");
            builder.AppendLine("for view_layer in scene.view_layers:");
            builder.AppendLine("    for layer_collection in walk_layer_collections(view_layer.layer_collection):");
            builder.AppendLine("        collection = getattr(layer_collection, 'collection', None)");
            builder.AppendLine("        if collection and collection.name in excluded_collections:");
            builder.AppendLine("            layer_collection.exclude = True");
        }

        if (!string.IsNullOrWhiteSpace(job.ResolvedOutputPattern) &&
            (job.UsesOutputFallback || job.HasOutputPathOverride || job.HasOutputNameOverride))
        {
            builder.AppendLine();
            builder.AppendLine($"scene.render.filepath = {Serialize(job.ResolvedOutputPattern)}");
        }

        if (job.HasOutputFormatOverride)
        {
            builder.AppendLine();
            builder.AppendLine($"scene.render.image_settings.file_format = {Serialize(job.OutputFormat)}");
        }

        if (job.DeviceMode == RenderDeviceMode.ForceCpu)
        {
            builder.AppendLine();
            builder.AppendLine("if scene.render.engine == 'CYCLES' and hasattr(scene, 'cycles'):");
            builder.AppendLine("    scene.cycles.device = 'CPU'");
        }

        return builder.ToString();
    }

    private static bool ShouldCreateOverrideScript(RenderQueueItemViewModel job)
    {
        return job.HasSceneOverride ||
               job.HasCameraOverride ||
               job.HasViewLayerOverride ||
               job.HasCollectionOverride ||
               job.HasOutputFormatOverride ||
               job.HasOutputNameOverride ||
               job.HasOutputPathOverride ||
               (job.UsesOutputFallback && !string.IsNullOrWhiteSpace(job.ResolvedOutputPattern)) ||
               job.DeviceMode != RenderDeviceMode.Default;
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value);
    }
}
