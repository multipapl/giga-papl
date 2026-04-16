# RenderSet Render Manager Specification

Це implementation spec для інтеграції RenderSet batch-рендеру в існуючий Render Manager.

Базовий API reference лежить у [renderset-integration.md](./renderset-integration.md). Цей документ описує не всі можливості RenderSet, а тільки потрібний product scope для програми.

## 1. Product Scope

RenderSet у Render Manager є **batch-режимом для `.blend` файлів, які вже повністю налаштовані в Blender/RenderSet**.

Render Manager має:

- прочитати RenderSet contexts з `.blend`;
- показати contexts у Details panel з чекбоксами;
- дати користувачу вибрати contexts для поточного batch;
- запустити кілька `.blend` файлів підряд;
- показати global/job/context progress;
- підʼєднати RenderSet output до існуючих Preview / Open Frame / Open Folder механік.

Render Manager не має:

- редагувати RenderSet contexts у `.blend`;
- міняти RenderSet output/templates/render settings;
- застосовувати свої frame/output/scene/camera/view-layer overrides у RenderSet режимі;
- викликати `bpy.ops.renderset.render_all_renderset_contexts` у background;
- додавати RenderSet-specific retry, preview mode, split workflow або context management tools у цьому етапі.

## 2. Current Code Findings

Relevant current files:

- `src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderManagerViewModel.cs`
  - owns queue commands, Update/inspection, process launch, stdout parsing, global progress, preview updates, Open Frame/Open Folder.
- `src/BlenderToolbox.Tools.RenderManager/Services/BlendInspectionService.cs`
  - launches Blender in background and returns `BlendInspectionSnapshot`.
  - Current inspection already has the right place to include RenderSet probing, so `Update` should parse RenderSet info too.
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderCommandBuilder.cs`
  - currently builds normal Blender render commands with optional override script and `--render-anim` / `-a` / `--render-frame`.
  - RenderSet needs a separate command branch: `--background <blend> --python <renderset_driver.py> -- <json>`.
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderOverrideScriptBuilder.cs`
  - applies app-level scene/camera/view-layer/output overrides.
  - Must not run for RenderSet jobs.
- `src/BlenderToolbox.Tools.RenderManager/Services/BlenderOutputParser.cs`
  - parses Blender frame/sample/saved output.
  - Should be extended or paired with a RenderSet marker parser.
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/JobRuntimeViewModel.cs`
  - owns `ProgressValue`, `ProgressText`, `LastKnownOutputPath`, preview status.
  - Needs RenderSet output folder/runtime context fields.
- `src/BlenderToolbox.Tools.RenderManager/Views/RenderManagerView.xaml`
  - already has global progress, per-job progress, Latest Frame card, and an empty RenderSet expander.
  - The empty RenderSet expander is the target UI surface.
- `src/BlenderToolbox.Tools.SplitByContext`
  - already owns split-by-context as a separate tool. RenderSet batch rendering must not depend on it.

## 3. User Workflow

1. User adds one or more `.blend` files to Render Manager.
2. Existing automatic inspection runs.
3. User can click existing `Update`; this reloads normal blend metadata and RenderSet metadata in the same inspection pass.
4. In Details -> RenderSet:
   - `Use RenderSet` is off by default.
   - When enabled, the context list becomes active.
   - Context checkboxes default to RenderSet's `include_in_render_all`.
   - User can change checkboxes in Render Manager without writing anything back to `.blend`.
5. User clicks `Start`.
6. Queue runs enabled jobs in order.
7. For a RenderSet job:
   - one Blender process is launched for that `.blend`;
   - selected contexts are rendered sequentially inside that process;
   - after the file finishes, Render Manager moves to the next queue job.
8. Preview/Open Frame/Open Folder use the final paths returned by the RenderSet driver, not the temporary RenderSet folder.

## 4. Data Model

Add RenderSet inspection models under `src/BlenderToolbox.Tools.RenderManager/Models`.

```csharp
public sealed class RendersetInspectionSnapshot
{
    public bool HasRenderset { get; set; }
    public List<RendersetContextSnapshot> Contexts { get; set; } = [];
    public DateTimeOffset ProbedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class RendersetContextSnapshot
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IncludeInRenderAll { get; set; }
    public string RenderType { get; set; } = string.Empty;
    public string CameraName { get; set; } = string.Empty;
    public string OutputFolderHint { get; set; } = string.Empty;
}
```

Extend `BlendInspectionSnapshot`:

```csharp
public RendersetInspectionSnapshot Renderset { get; set; } = new();
public long BlendFileSizeBytes { get; set; }
public DateTimeOffset? BlendFileLastWriteUtc { get; set; }
```

Extend `RenderQueueItem`:

```csharp
public bool UseRenderset { get; set; }
public List<string> SelectedRendersetContextNames { get; set; } = [];
public string LastKnownOutputFolderPath { get; set; } = string.Empty;
```

Notes:

- `UseRenderset` default is `false`.
- `SelectedRendersetContextNames` is app state only. It is never written into `.blend`.
- `custom_name` is the primary context identity because the guide says it is unique and more stable than index.
- If a context name disappears after Update, drop it from the selection.
- If a new context appears after Update, default its selected state from `include_in_render_all`.

## 5. ViewModels

Add `JobRendersetViewModel` under `ViewModels/Jobs`.

Suggested responsibilities:

- `bool UseRenderset`
- `ObservableCollection<RendersetContextViewModel> Contexts`
- `IReadOnlyList<string> SelectedContextNames`
- `bool HasContexts`
- `string SummaryText`
- `string CurrentContextName`
- `double ContextProgressValue`
- `string ContextProgressText`
- selection reconciliation after inspection

Add it to `RenderJobViewModel` beside `Header`, `Blender`, `Frames`, `Output`, `Targeting`, `Runtime`.

`RenderQueueItemViewModel` must forward the new flattened properties needed by `RenderManagerViewModel`, tests, and XAML.

Important UI enablement rule:

- Blender executable override remains usable in RenderSet mode.
- Frame controls, output path/name controls, scene/camera/view-layer controls are disabled or visually marked inactive when `UseRenderset == true`.
- Runtime command building ignores those overrides even if older queue JSON still contains them.

## 6. Inspection / Update

Extend `BlendInspectionService.BuildScript()` instead of creating a separate user-facing scan action.

The existing `Update` command should:

- inspect normal blend defaults as today;
- detect `scene.renderset_contexts`;
- return RenderSet contexts in `BlendInspectionSnapshot.Renderset`;
- cache enough file identity data to avoid unnecessary automatic re-probes later.

Probe behavior:

```python
contexts = getattr(scene, "renderset_contexts", None)
if contexts is not None:
    for i, c in enumerate(contexts):
        cam = c.get_camera() if hasattr(c, "get_camera") else None
        output_hint = ""
        if hasattr(c, "generate_output_folder_path"):
            try:
                output_hint = c.generate_output_folder_path()
            except Exception:
                output_hint = ""
```

The probe must not fail the whole inspection if RenderSet is absent or a single context output hint cannot be computed.

Caching:

- Current persisted `Inspection` already acts as a queue-level cache.
- Add `BlendFileLastWriteUtc` and `BlendFileSizeBytes` to detect stale inspection.
- `Update` always forces refresh.
- Auto inspection on add/selection may reuse current snapshot if path, mtime, and size match.

## 7. Render Command

RenderSet jobs need a separate driver script builder, for example:

- `Services/RendersetRenderScriptBuilder.cs`
- optionally `Services/RendersetOutputParser.cs`

For `UseRenderset == false`, current command building remains unchanged.

For `UseRenderset == true`, command shape:

```text
blender.exe --background "<file.blend>" --python "<runtime/renderset_render_<job>.py>" -- "<json args>"
```

Do not add:

- `--scene`
- `--render-anim`
- `-a`
- `--render-frame`
- normal `render_overrides_*.py`

JSON args should include selected context names:

```json
{
  "contexts": ["Context A", "Context B"]
}
```

The driver:

- reads `scene.renderset_contexts`;
- filters by selected names;
- uses one shared `datetime` for all contexts in that file;
- for each selected context:
  - sets `scene.renderset_context_index = i`;
  - prints `<<RSET_START>>`;
  - attaches `rset.render_finished(scene)` to `bpy.app.handlers.render_post`;
  - calls `rset.render(bpy.context, time=batch_time)`;
  - prints `<<RSET_DONE>>` with final output folders;
  - removes its own handler in `finally`;
- on exception prints `<<RSET_ERROR>>` and exits non-zero.

Structured stdout markers:

```text
<<RSET_JOB>> {"TotalContexts":2}
<<RSET_START>> {"Index":0,"Name":"Context A","RenderType":"still","FrameStart":1,"FrameEnd":1,"FrameStep":1}
<<RSET_DONE>> {"Index":0,"Name":"Context A","Folders":["Q:/renders/a"]}
<<RSET_ERROR>> {"Index":0,"Name":"Context A","Error":"..."}
<<RSET_ALL_DONE>> {"Folders":["Q:/renders/a","Q:/renders/b"]}
```

The existing raw Blender stdout should still be logged unchanged.

## 8. Progress

There should be three visible progress levels:

1. Global queue progress
2. Current job progress
3. Current RenderSet context progress

Current global progress is job-status based. It should become progress-value based:

```csharp
GlobalProgressValue = enabledJobs.Average(job => job.ProgressValue)
```

This keeps normal jobs working and makes RenderSet jobs contribute partial progress.

RenderSet job progress:

```text
job.ProgressValue =
    (completedContexts + currentContextFraction) / selectedContextCount * 100
```

Context progress:

- On `<<RSET_START>>`, set current context name, render type, frame range, and context progress to `0`.
- While that context is active, reuse existing Blender sample/frame parsing to update context progress.
- On `<<RSET_DONE>>`, set context progress to `100`, increment completed context count, and update job progress.

Still contexts:

- If Blender reports samples, context progress follows samples.
- If no useful sample/frame output appears, show indeterminate-like text in WPF terms via `"Rendering context..."` and set to `100` on `DONE`.

Animation contexts:

- Use `FrameStart`, `FrameEnd`, `FrameStep` from `<<RSET_START>>`.
- Existing `BlenderOutputParser` frame/sample parsing can calculate fraction inside the active context.

Resume:

- RenderSet resume is out of scope for MVP.
- If a RenderSet job is canceled and started/resumed again, it reruns selected contexts from the beginning.
- The UI should avoid implying frame-accurate RenderSet resume.

## 9. Output / Preview / Open Buttons

RenderSet writes to a temporary system folder first and then `render_finished()` moves files into final folders. Therefore the app must not use temporary `Saved:` paths as final output for RenderSet jobs.

Add runtime field:

```csharp
public string LastKnownOutputFolderPath { get; set; } = string.Empty;
```

On `<<RSET_DONE>>`:

- read `folders`;
- set `LastKnownOutputFolderPath` to the most useful folder:
  - prefer the newest existing folder from `folders`;
  - otherwise first non-empty folder;
- scan returned folders for newest previewable image file;
- if found, set existing `LastKnownOutputPath` to that file and call existing preview loader;
- if no previewable image exists, keep `LastKnownOutputPath` empty and set preview status to something like `RenderSet output folder is ready. No previewable image was found.`

Open behavior:

- `Open Frame`
  - enabled only when `LastKnownOutputPath` exists as a file.
  - unchanged for normal jobs.
- `Open Folder`
  - normal jobs: current behavior can remain, using last frame folder or resolved output directory.
  - RenderSet jobs: open `LastKnownOutputFolderPath` if it exists.
  - RenderSet jobs before first `<<RSET_DONE>>`: button should be disabled if practical; otherwise command should show `RenderSet output folder is not available yet.`

The queue row `Open` button should follow the same logic. It should not open the app-level resolved output override when `UseRenderset == true`.

## 10. Validation

For normal jobs, existing validation remains.

For RenderSet jobs:

Required:

- Blender executable exists.
- Blend file exists.
- inspection contains RenderSet metadata or can be loaded before render.
- `UseRenderset == true` has at least one selected context.

Ignored:

- frame range validation;
- output directory/name validation;
- scene/camera/view-layer override validation.

Reason: those are owned by RenderSet contexts.

Warnings only:

- no contexts were found after inspection;
- selected context names no longer exist after Update.

No additional preflight checks are required in this phase.

## 11. UI Details

RenderSet expander contents:

```text
[ ] Use RenderSet

When off:
  RenderSet contexts inactive.

When on:
  [x] Context A        still        Camera_Main
  [ ] Context B        animation    Camera_Close
  [x] Context C        still        Camera_Top

Current context
  <progress bar>
  Context A | Frame 3/20 or Rendering context...
```

No separate large `Scan RenderSet` button.

Use existing `Update` command as the scan entry point. A small refresh affordance inside the RenderSet block is optional, but not part of MVP.

When `Use RenderSet` is on:

- Render block:
  - executable override stays active;
  - frame and output controls inactive.
- Blend File block:
  - scene/camera/view-layer controls inactive.
- Context checkboxes stay active when the queue is idle.

## 12. Tests

Add or update unit tests for:

- `BlendInspectionService` script contains RenderSet probing keys and does not require RenderSet to exist.
- RenderSet output parser parses `RSET_START`, `RSET_DONE`, `RSET_ERROR`.
- `RenderCommandBuilder` builds RenderSet command without normal override script and without `--render-anim` / `-a` / `--render-frame`.
- Validation allows RenderSet jobs without output/frame overrides and fails when zero contexts are selected.
- `RenderQueueItemViewModel` round-trips `UseRenderset`, selected context names, and `LastKnownOutputFolderPath`.
- Open Folder decision prefers `LastKnownOutputFolderPath` for RenderSet jobs.
- Global progress averages job `ProgressValue`, so partial RenderSet progress affects the global bar.

## 13. Implementation Order

1. Models and persisted queue fields.
2. `JobRendersetViewModel` and selection reconciliation.
3. Extend `BlendInspectionService` probe and `BlendInspectionSnapshot`.
4. RenderSet expander UI.
5. RenderSet command/script builder.
6. RenderSet stdout parser and progress plumbing.
7. Output folder / preview bridge.
8. Validation branch.
9. Tests and docs updates.

## 14. Acceptance Criteria

- `Use RenderSet` is off by default for new and existing jobs.
- `Update` loads RenderSet contexts for a selected `.blend`.
- Context checkboxes initialize from `include_in_render_all`.
- Enabling RenderSet prevents normal render overrides from affecting execution.
- Starting the queue renders selected RenderSet contexts in file order, one Blender process per `.blend`.
- Multiple `.blend` files run sequentially as a batch.
- Global/job/context progress update during RenderSet execution.
- Logs contain RenderSet markers and Blender stdout.
- Existing preview displays the latest final RenderSet image when one is available.
- `Open Folder` for RenderSet opens the final RenderSet output folder after render and does not open the normal override/fallback folder.
- No `.blend` file is modified by Render Manager during RenderSet batch render.
