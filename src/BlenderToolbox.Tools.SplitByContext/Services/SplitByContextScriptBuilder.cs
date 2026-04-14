namespace BlenderToolbox.Tools.SplitByContext.Services;

public sealed class SplitByContextScriptBuilder
{
    public string BuildScript()
    {
        return """
            import bpy
            import os

            def sanitize_name(value):
                value = (value or "").strip()
                if not value:
                    return "Context"

                for source in (" ", "/", "\\"):
                    value = value.replace(source, "_")

                return value

            print("Starting context split...")

            scene = bpy.context.scene
            contexts = getattr(scene, "renderset_contexts", None)
            if contexts is None:
                raise RuntimeError("renderset_contexts was not found on the active scene.")

            basename, ext = os.path.splitext(bpy.data.filepath)
            if ext.lower() != ".blend":
                raise RuntimeError("The current file is not a .blend file.")

            for index in range(len(contexts)):
                scene.renderset_context_index = index
                context = contexts[index]

                if hasattr(context, "include_in_render_all") and not context.include_in_render_all:
                    continue

                active_camera = scene.camera
                if active_camera:
                    context_name = sanitize_name(active_camera.name)
                else:
                    context_name = f"Context{index}"

                output_path = f"{basename}_{context_name}{ext}"
                print(f"SAVING::{output_path}")
                bpy.ops.wm.save_as_mainfile(filepath=output_path)

            print("Context split completed.")
            """;
    }
}
