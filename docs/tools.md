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
- manage per-job output settings and frame selection
- start, stop, resume, reset, duplicate, remove, and reorder jobs
- show job logs, blend metadata reload state, queue progress, per-job progress, and whole-job ETA

Key files:

- `src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderManagerViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderQueueItemViewModel.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/BlenderOutputParser.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderResumePlanner.cs`
- `src/BlenderToolbox.Tools.RenderManager/Services/RenderEtaCalculator.cs`
- `src/BlenderToolbox.Tools.RenderManager/Views/RenderManagerView.xaml`

## Split By Context

Purpose:

- run a headless split for a `.blend` file
- generate a temporary Python script
- log Blender stdout and stderr to a local log file

Key files:

- `src/BlenderToolbox.Tools.SplitByContext/Services/SplitByContextService.cs`
- `src/BlenderToolbox.Tools.SplitByContext/ViewModels/SplitByContextViewModel.cs`
- `src/BlenderToolbox.Tools.SplitByContext/Views/SplitByContextView.xaml`

## Tool Isolation Rule

- tools may depend on `Core`
- tools should not depend on one another
- move shared logic into `Core` only when at least two tools genuinely need it
