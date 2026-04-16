# Architecture

## Solution Shape

- `src/BlenderToolbox.App`
  - WPF shell
  - theme loading
  - tool registration
- `src/BlenderToolbox.Core`
  - shared abstractions
  - shared services
  - shared presentation enums
- `src/BlenderToolbox.Tools.LazyFrameRename`
  - isolated tool module
- `src/BlenderToolbox.Tools.RenderManager`
  - isolated render queue tool
  - Blender inspection, command building, validation, preview loading, queue orchestration
  - job view-models are split under `ViewModels/Jobs`
  - `RenderQueueItemViewModel` remains as a compatibility adapter while older services move to the split model
- `src/BlenderToolbox.Tools.SplitByContext`
  - isolated tool module
- `tests/BlenderToolbox.Core.Tests`
  - non-UI tests for services, parsing, stores, and queue helpers

## Tool Contract

Each tool provides:

- `IToolDefinition`
  - `Id`
  - `DisplayName`
  - `Description`
  - `View`
- optional `IStatefulTool`
  - `SaveState()`

## Adding A New Tool

1. Create a dedicated project: `src/BlenderToolbox.Tools.<Name>`.
2. Add `Models`, `Services`, `ViewModels`, and `Views`.
3. Implement `IToolDefinition`.
4. If the tool has local settings, implement `IStatefulTool`.
5. Register the tool in `MainWindow.xaml.cs`.

## Current Limits

- Tool registration is still static in `MainWindow.xaml.cs`.
- This is acceptable for the current repo size because it keeps startup simple and explicit.
- If the tool list grows significantly, registration can move into a small `ToolCatalog` without introducing a plugin system.
- Render Manager is a local desktop queue runner, not a farm scheduler or multi-machine render manager.
- RenderSet research exists in docs, but RenderSet execution is not part of the active implementation.
