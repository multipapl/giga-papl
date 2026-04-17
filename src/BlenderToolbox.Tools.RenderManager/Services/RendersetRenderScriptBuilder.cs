using System.Text.Json;

namespace BlenderToolbox.Tools.RenderManager.Services;

public sealed class RendersetRenderScriptBuilder
{
    public string BuildScript()
    {
        return """
            import bpy
            import datetime
            import json
            import sys
            import traceback

            def emit(marker, payload):
                print(marker + " " + json.dumps(payload), flush=True)

            raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
            args = json.loads(raw[0]) if raw else {}
            wanted_names = set(args.get("Contexts", []))

            scene = bpy.context.scene
            contexts = getattr(scene, "renderset_contexts", None)
            if contexts is None:
                emit("<<RSET_ERROR>>", {"Index": -1, "Name": "", "Error": "renderset_contexts was not found on the active scene."})
                sys.exit(2)

            selected = []
            for index, context in enumerate(contexts):
                name = getattr(context, "custom_name", "") or f"Context {index + 1}"
                if wanted_names and name not in wanted_names:
                    continue
                selected.append((index, name))

            if not selected:
                emit("<<RSET_ERROR>>", {"Index": -1, "Name": "", "Error": "No RenderSet contexts selected."})
                sys.exit(3)

            emit("<<RSET_JOB>>", {"TotalContexts": len(selected)})
            batch_time = datetime.datetime.now()
            all_folders = set()

            for index, name in selected:
                scene.renderset_context_index = index
                rset = scene.renderset_contexts[index]
                frame_start = int(getattr(scene, "frame_start", 0) or 0)
                frame_end = int(getattr(scene, "frame_end", 0) or 0)
                frame_step = int(getattr(scene, "frame_step", 1) or 1)
                render_type = getattr(rset, "render_type", "") or ""

                emit("<<RSET_START>>", {
                    "Index": index,
                    "Name": name,
                    "RenderType": render_type,
                    "FrameStart": frame_start,
                    "FrameEnd": frame_end,
                    "FrameStep": frame_step,
                })

                def finish_handler(render_scene, dummy=None, current=rset):
                    return current.render_finished(render_scene)

                def frame_emit_handler(
                    render_scene,
                    dummy=None,
                    current=rset,
                    current_index=index,
                    current_name=name,
                ):
                    try:
                        final_folder = current.generate_output_folder_path(
                            time=batch_time,
                            frame_current=render_scene.frame_current,
                            frame_start=render_scene.frame_start,
                            frame_end=render_scene.frame_end,
                            frame_step=render_scene.frame_step,
                        )
                    except Exception:
                        final_folder = ""
                    emit("<<RSET_FRAME>>", {
                        "Index": current_index,
                        "Name": current_name,
                        "Frame": int(render_scene.frame_current),
                        "Folder": final_folder or "",
                    })

                bpy.app.handlers.render_post.append(finish_handler)
                bpy.app.handlers.render_post.append(frame_emit_handler)
                try:
                    folders = set(rset.render(bpy.context, time=batch_time))
                    all_folders |= folders
                    emit("<<RSET_DONE>>", {
                        "Index": index,
                        "Name": name,
                        "Folders": sorted(folders),
                    })
                except Exception as exc:
                    emit("<<RSET_ERROR>>", {
                        "Index": index,
                        "Name": name,
                        "Error": str(exc),
                    })
                    traceback.print_exc()
                    sys.exit(4)
                finally:
                    for handler in (frame_emit_handler, finish_handler):
                        try:
                            bpy.app.handlers.render_post.remove(handler)
                        except ValueError:
                            pass

            emit("<<RSET_ALL_DONE>>", {"Folders": sorted(all_folders)})
            """;
    }

    public string BuildArgumentsJson(IReadOnlyList<string> selectedContextNames)
    {
        var payload = new RendersetRenderArguments
        {
            Contexts = selectedContextNames
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name.Trim())
                .ToList(),
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed class RendersetRenderArguments
    {
        public List<string> Contexts { get; set; } = [];
    }
}
