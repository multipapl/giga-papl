# Tools

## Lazy Frame Rename

Purpose:

- batch rename an image sequence
- preserve the starting frame number
- support manual mode and subfolders mode

Key files:

- `src/BlenderToolbox.Tools.LazyFrameRename/Services/FrameRenameService.cs`
- `src/BlenderToolbox.Tools.LazyFrameRename/ViewModels/LazyFrameRenameViewModel.cs`
- `src/BlenderToolbox.Tools.LazyFrameRename/Views/LazyFrameRenameView.xaml`

## Render Manager

Purpose:

- queue headless Blender renders from multiple `.blend` files
- inspect each blend file and inherit its default frame range, output, scene, camera, and view layer
- manage per-job frame, output path/name, Blender executable, scene, camera, and view-layer overrides
- use the global Blender executable path, with an optional per-job executable override
- start, soft-stop after the current frame, resume, reset, remove, and drag-reorder jobs
- show job logs, blend metadata reload state, queue progress, per-job progress, and whole-job ETA
- decode and show the latest saved frame for the selected job
- keep logs collapsible and persist the expanded state in `global.json`

Basic workflow:

1. Configure the shared Blender executable in `Settings`.
2. Click `Add Blend` and select one or more `.blend` files.
3. Select a queue row. The job name is the blend file name; there is no separate editable job-name field.
4. Use `Start`, `Stop`, `Resume`, `Reset`, and `Update` from the top action row.
5. Use `Remove` for selected jobs and drag rows in the queue to reorder them.

Queue behavior:

- Only enabled rows run.
- `Stop` requests a soft stop after the current frame.
- `Resume` continues from the current or next frame instead of restarting the whole job.
- `Reset` clears runtime state for the selected job, including progress and status.
- `Update` reloads blend metadata for the selected job.
- `Add Empty` is intentionally not present; every job is backed by a blend file.

Detail panel:

- The header shows the blend file name, blend path, inspection status, enabled toggle, `Browse Blend`, and `Retry`.
- The `Render` block is collapsed by default and contains Blender executable override, frames, output path, and render name.
- The `Blend File` block is collapsed by default and contains scene, camera, and view-layer targeting.
- The `RenderSet` block is a reserved empty placeholder.
- Logs live below the details and can be collapsed.

Override behavior:

- Fields show the value inherited from the blend/global default by default.
- Editing an overridable value stores an override and visually marks the field.
- The circular reset button restores that field to the inherited default.
- Frame controls default to `FrameRange`; the UI supports `FrameRange` and `SingleFrame`.
- Output format is inherited from the blend file and is not exposed as a separate override.
- Blender executable override shows the global Settings path when the job does not have its own path.

Interaction notes:

- Mouse wheel input inside the detail panel always scrolls the detail panel.
- Combo boxes do not consume the wheel to change selection while scrolling.
- Scene changes re-scope camera and view-layer choices without dropping stored out-of-list values.

Key files:

- `src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderManagerViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderQueueItemViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/RenderJobViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/JobFramesViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/JobOutputViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/JobTargetingViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/BlendInspectionService.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/BlenderOutputParser.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderCommandBuilder.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderOutputTemplateService.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderResumePlanner.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderEtaCalculator.cs`
- `src/BlenderToolbox.Tools.RenderManager/Views/RenderManagerView.xaml`

## Split By Context

Purpose:

- run a headless split for a `.blend` file
- use the global Blender executable path from Settings
- generate a temporary Python script
- log Blender stdout and stderr to a local log file

Key files:

- `src/BlenderToolbox.Tools.SplitByContext/Services/SplitByContextService.cs`
- `src/BlenderToolbox.Tools.SplitByContext/ViewModels/SplitByContextViewModel.cs`
- `src/BlenderToolbox.Tools.SplitByContext/Views/SplitByContextView.xaml`

## Settings

Purpose:

- configure the shared Blender executable path used by Render Manager and Split By Context
- configure the app theme override: `Auto`, `Light`, or `Dark`
- reveal the Render Manager log folder and app data folder

Key files:

- `src/BlenderToolbox.Core/Services/GlobalSettingsService.cs`
- `src/BlenderToolbox.App/ViewModels/SettingsScreenViewModel.cs`
- `src/BlenderToolbox.App/Views/SettingsScreenView.xaml`

## Tool Isolation Rule

- tools may depend on `Core`
- tools should not depend on one another
- move shared logic into `Core` only when at least two tools genuinely need it
