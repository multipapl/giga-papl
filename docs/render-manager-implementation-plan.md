# Render Manager Implementation Plan

## Goal

Implement a new `Render Manager` tool inside the existing `BlenderToolbox` app.

The short-term goal is to reproduce the reference workflow from `_ref/B-renderon` as closely as practical inside the current `C#`, `.NET 8`, `WPF` toolbox architecture.
The medium-term goal is to trim unused functionality only after parity is reached and validated in real use.

This means:

- we optimize for feature coverage first
- we still keep the implementation maintainable in the toolbox codebase
- we prefer repo-owned helper scripts and deterministic models over opaque behavior

## Ground Rules

- New tool is a separate project: `src/BlenderToolbox.Tools.RenderManager`.
- No runtime dependency on `_ref/B-renderon`.
- Reference app is used as behavior reference, not as a binary dependency.
- Blender-side behavior is implemented through repo-owned Python helper scripts.
- Shared abstractions move to `Core` only if at least two tools benefit.

## Reference Feature Inventory

Based on analysis of the reference app, the functional surface is roughly:

- render queue and per-item state
- Blender executable/version selection
- blend inspection: scenes, cameras, view layers, collections, outputs
- render modes: animation, frames, script mode, viewport mode
- output naming and output-path rewriting
- collection overrides
- view-layer selection and compositing handling
- GPU device discovery and selection
- optional parallel GPU splitting
- log parsing and progress estimation
- recent queue, queue persistence, interrupted job handling
- scheduler and task system
- watch folders
- viewers, sounds, skins, translations, small UX extras

## Delivery Strategy

We should not try to ship the full parity surface in one pass.
Instead, implement it in waves that preserve a working tool at each checkpoint.

Recommended implementation order:

1. scaffold and persistence
2. queue model and UI shell
3. Blender process foundation
4. blend inspection
5. single-job render execution
6. queue orchestration
7. targeting and output overrides
8. progress and reliability features
9. advanced parity features
10. cleanup and reduction

## Parity Tiers

## Tier A: Core Parity

These features should be implemented first because they define the tool's main value:

- queue of render jobs
- per-job Blender executable
- add `.blend` files
- inspect scenes, cameras, view layers, collections
- render modes: animation, frame range, single frame
- output path and naming overrides
- extra Blender args
- start, stop, reorder, duplicate, enable or disable queue items
- stdout and stderr capture
- progress parsing
- saved settings and queue persistence

## Tier B: Extended Parity

These features are still important if the goal is to mirror the reference app closely:

- collection override editor
- compositing handling options for selected view layer
- render timeout or stall detection
- interrupted-job detection and restart behavior
- retry failed jobs
- output folder and rendered frame opening
- recent queue state
- multiple Blender versions manager
- simple device backend selection

## Tier C: Advanced Parity

These features can wait until the core tool is proven:

- detailed GPU device discovery and selection
- parallel GPU split mode
- scheduler and saved tasks
- watch folders
- force-close or stop-after-current-frame strategies
- estimator view and statistics

## Tier D: Lowest Priority

These features should be intentionally last, even if the reference app has them:

- skins
- translation system
- custom media viewers
- sound playback
- cosmetic UI extras

## Architecture Target

Recommended project structure:

```text
src/
  BlenderToolbox.Tools.RenderManager/
    Contracts/
    Models/
    Services/
    ViewModels/
    Views/
    Resources/
      Scripts/
```

Recommended internal service map:

- `RenderManagerSettingsStore`
- `RenderQueueStore`
- `BlendInspectionService`
- `RenderScriptCatalog`
- `RenderCommandBuilder`
- `RenderProcessService`
- `RenderQueueService`
- `RenderProgressParser`
- `OutputNamingService`
- `RenderValidationService`
- `TaskSchedulerStore` later
- `WatchFolderService` later

## Core Models

The first implementation should establish stable models early.

Suggested models:

- `RenderManagerSettings`
- `BlenderExecutableEntry`
- `RenderQueueItem`
- `RenderJobTargeting`
- `RenderMode`
- `FrameSelection`
- `BlendInspectionRequest`
- `BlendInspectionResult`
- `BlendSceneInfo`
- `BlendViewLayerInfo`
- `BlendCollectionInfo`
- `OutputNamingTemplate`
- `RenderLaunchPlan`
- `RenderProgressSnapshot`
- `RenderRunLog`
- `RenderErrorInfo`

## Workstream Plan

## Workstream 1: Tool Scaffold

Deliverables:

- create `BlenderToolbox.Tools.RenderManager` project
- add tool definition implementing `IToolDefinition` and `IStatefulTool`
- register tool in `MainWindow.xaml.cs`
- add placeholder view and view model
- add settings file name and basic state load/save

Done when:

- tool appears in the toolbox
- state survives app restart

## Workstream 2: Queue Domain And Persistence

Deliverables:

- queue item model
- observable queue collection in view model
- JSON persistence for queue and settings
- commands: add, remove, duplicate, reorder, enable or disable
- queue item selection and editing panel

Done when:

- user can build a queue manually
- queue is restored on next launch

## Workstream 3: Blender Integration Foundation

Deliverables:

- common process-launch service for Blender
- temp or app-data working directory for scripts and logs
- command builder with `ArgumentList`
- log capture infrastructure
- command preview string generation

Done when:

- tool can build and display a valid Blender command
- logs are written predictably

## Workstream 4: Blend Inspection

Deliverables:

- repo-owned Python inspection script in `Resources/Scripts`
- service to run Blender headless with the inspection script
- JSON response schema from Blender back to C#
- extraction of:
  - scenes
  - cameras
  - view layers
  - collections
  - render output defaults
  - frame start, end, step if available

Done when:

- adding a blend can automatically inspect it
- selectors for scene, camera, and view layer are filled from inspection result

## Workstream 5: Single Render Execution

Deliverables:

- run one queue item
- render mode support:
  - animation
  - frame range
  - single frame
- status transitions:
  - `Pending`
  - `Inspecting`
  - `Ready`
  - `Rendering`
  - `Completed`
  - `Failed`
  - `Canceled`
- basic cancellation

Done when:

- one `.blend` can be rendered end-to-end from the new tool

## Workstream 6: Queue Orchestration

Deliverables:

- render selected item
- render full queue sequentially
- skip disabled items
- stop queue behavior
- duplicate and retry behavior
- current-item tracking

Done when:

- the queue behaves as the main operating mode of the tool

## Workstream 7: Blender-Side Override Scripts

This workstream reproduces the most important “shortcut through Blender pain” behavior from the reference app.

Deliverables:

- helper script: select scene if needed
- helper script: apply camera override
- helper script: apply view-layer override
- helper script: apply collection overrides
- helper script: apply output path override
- helper script: optionally rewrite compositor output nodes

Notes:

- own these scripts in this repo
- do not copy files directly from `_ref`
- behavior may be inspired by the reference scripts:
  - `UseOutputPath.py`
  - `UseViewlayer.py`
  - `UseCollections.py`
  - `QuitBlender.py`

Done when:

- per-job targeting can alter the render without hand-editing the source blend

## Workstream 8: Progress, ETA, And Reliability

Deliverables:

- parse Blender output for frame progress
- derive rendered frame count
- elapsed time tracking
- rough ETA
- stall or timeout heuristics
- clearer runtime error classification
- preserve raw stdout and stderr even if parsing fails

Done when:

- render feedback is useful during long jobs
- failures are diagnosable without opening external logs manually

## Workstream 9: Device Selection

Deliverables:

- settings model for device strategy
- at minimum:
  - `Default`
  - `ForceCpu`
- optional next step:
  - backend selector such as `CUDA`, `OPTIX`, `HIP`, `OneAPI`
- Blender helper for applying the chosen device mode

Deferred inside this workstream:

- exact per-device occupancy
- auto-splitting jobs across multiple GPUs

Done when:

- user can intentionally choose a simpler device mode per job

## Workstream 10: Extended Parity

Deliverables:

- recent queue behavior
- interrupted-run recovery markers
- retry failed items
- open output folder
- open latest rendered file
- saved presets for naming or job setup
- compositing handling options for view-layer override

Done when:

- the tool covers most day-to-day use from the reference app

## Workstream 11: Scheduler And Watch Folders

Deliverables:

- persisted tasks
- scheduled queue start
- watch folder definition store
- stability check before auto-enqueue
- auto-add blend jobs from watched folders

This is intentionally late because it adds background behavior and increases complexity a lot.

Done when:

- the tool can operate unattended for local workflow automation

## UI Plan

## Main View Layout

Recommended layout for parity-oriented implementation:

- top action bar
- left queue list or grid
- right detail editor for selected item
- bottom console or log panel

Sections in detail editor:

- source blend
- Blender executable
- render mode
- frame settings
- scene and camera
- view layer and collections
- output naming
- extra args
- inspection summary
- command preview

## Subdialogs Or Secondary Panels

These can be separate dialogs or embedded drawers depending on implementation speed:

- Blender versions manager
- collection override editor
- output naming editor
- task or scheduler editor later
- watch folders later

## Testing Plan

## Unit Tests First

Add tests for:

- command builder
- queue store round-trip
- settings store round-trip
- output token expansion
- output parser
- validation rules
- progress parser

## Integration Tests

Add integration coverage for:

- helper script generation
- inspection result parsing
- mocked Blender process output
- cancellation flow

## Manual Test Matrix

Must manually validate:

- render one animation
- render one single frame
- render frame range
- invalid Blender path
- invalid blend path
- missing selected scene
- missing selected camera
- missing selected view layer
- collection override applied
- output path override applied
- stop current render
- resume app with saved queue

## Recommended Milestones

## Milestone 1: Tool Shell

Includes:

- Workstreams 1 and 2

User-visible result:

- a queue editor exists inside the toolbox

## Milestone 2: Blender Core

Includes:

- Workstreams 3 and 4

User-visible result:

- tool can inspect a blend and build a real render command

## Milestone 3: First Real Render

Includes:

- Workstreams 5 and 6

User-visible result:

- queue can execute real renders sequentially

## Milestone 4: Reference-Like Overrides

Includes:

- Workstream 7

User-visible result:

- scene, camera, view layer, collections, and output overrides work

## Milestone 5: Usable Daily Driver

Includes:

- Workstreams 8 through 10

User-visible result:

- tool is realistic for daily use instead of just technical demo

## Milestone 6: Advanced Automation

Includes:

- Workstream 11

User-visible result:

- scheduler and watch-folder automation are available

## Recommended Execution Order In This Repo

For the next actual coding steps, follow this order:

1. scaffold the new tool project and register it
2. add queue models, settings, and persisted state
3. implement Blender command builder and process runner
4. implement blend inspection script and parser
5. render a single job end-to-end
6. add queue execution and stop behavior
7. add output naming and targeting override scripts
8. add progress parsing and ETA
9. add device selection basics
10. add extended parity features
11. add scheduler and watch folders only after daily usage proves they are worth it

## Risks

## Main Technical Risks

- Blender stdout format may vary by version.
- Some reference behaviors depend on helper scripts mutating scene state at runtime.
- Collection and compositing handling can get complicated fast.
- Parallel GPU features can distort the architecture if attempted too early.
- Background automation features like watch folders can create hard-to-debug state.

## Risk Controls

- keep helper scripts small and isolated
- log the final command and helper-script paths
- prefer explicit JSON contracts for inspection results
- keep queue execution single-threaded first
- delay advanced GPU orchestration

## Definition Of Success

The implementation phase is considered successful when:

- the new tool can replace the reference app for ordinary local queue rendering
- the core reference workflow exists inside the C# toolbox
- the code remains readable enough to safely remove or reshape features later
- later “feature pruning” becomes a product decision, not a reverse-engineering problem

## Immediate Next Step

The next practical step should be `Milestone 1: Tool Shell`.

That means:

- create the new project
- wire it into the app
- define queue models
- define queue and settings persistence
- build the first empty UI for queue plus details

Only after that should we start implementing Blender inspection and render execution.

## Current Checkpoint

Date: `2026-04-15`

The repo now contains a working `Render Manager` tool rather than only a `Milestone 1` shell.

Implemented so far:

- new project: `src/BlenderToolbox.Tools.RenderManager`
- tool registration in the app shell
- `IToolDefinition` and `IStatefulTool` integration
- persisted settings JSON and persisted queue JSON under `RenderManager/`
- queue item model and view model
- commands for:
  - add blend
  - add empty job
  - duplicate
  - reset
  - remove
  - move up and down
  - enable or disable
- real `Start / Stop / Resume` render execution
- live command preview and log panel
- parsed per-job frame progress
- queue-wide progress by finished jobs
- whole-job ETA based on average completed frame time
- resume from the stopped frame instead of restarting the whole animation

Current limitations and known issues:

- blend inspection is still not implemented
- camera, view layer, and collection override fields are not yet passed through to Blender
- `Add Blend` currently uses single-file picker behavior, not multi-select
- old saved queue items may not contain all newer runtime fields until they are rerun
- the UI still needs cleanup and documentation polish

Conclusion:

- the tool has moved past shell-only status
- core local rendering behavior is in place
- the next work should focus on override support, inspection, and cleanup

## Recommended Next Actions

Recommended next work:

1. implement blend inspection and populate scene metadata
2. pass camera, view layer, and collection overrides into actual Blender execution
3. decide whether `Add Blend` should support multi-select
4. continue UI cleanup and documentation updates
5. expand tests around orchestration and queue persistence
