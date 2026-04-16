# Render Manager Spec

## Purpose

This document defines a new `Render Manager` tool for the existing `BlenderToolbox` desktop app.

The tool is based on analysis of the reference app in `_ref/B-renderon`, but it is not intended to clone that product 1:1.
The goal is to reuse the useful workflow ideas, trim the parts that do not fit this repo, and design a simpler tool that matches the current toolbox principles:

- keep tools isolated
- keep UI simple
- prefer vertical slices
- avoid speculative architecture

## Reference Summary

The reference app appears to be a Windows desktop render manager built with `Python 3.11`, `PyQt5`, and `PyInstaller`.
From static analysis, it includes these feature groups:

- render queue and job states
- Blender version management
- scene, camera, view layer, and collection targeting
- output naming patterns and output path rewriting
- GPU device selection and distribution
- scheduler and watch folders
- render estimation, timeout detection, and progress parsing
- viewers, sound, skins, translations, and other UX extras

Open scripts shipped next to the executable confirm several integration techniques that are directly usable as design reference:

- `UseOutputPath.py`
- `UseViewlayer.py`
- `UseCollections.py`
- `QuitBlender.py`

## Product Position In This Repo

`Render Manager` should be added as a separate tool inside the toolbox shell, not as a special mode of another tool.

Recommended project shape:

- `src/BlenderToolbox.Tools.RenderManager/`
- optional shared contracts only if they are truly reusable
- no dependency on `LazyFrameRename`
- no dependency on `SplitByContext`

## Goals

- Add a production-oriented render queue for `.blend` files.
- Let the user launch headless Blender renders with repeatable settings.
- Make per-job targeting explicit: scene, camera, view layer, collection set, frame range, output pattern.
- Keep the first version small enough to be maintainable in `WPF` and `.NET 8`.
- Reuse Blender Python helper scripts where that is simpler than reverse-engineering `.blend` internals in C#.

## Non-Goals

- Do not replicate the full B-renderon feature surface.
- Do not build a farm manager or network render system.
- Do not add heavy theme, skin, translation, or media-viewer subsystems.
- Do not optimize for multi-user collaboration.
- Do not depend on undocumented behavior inside the reference app.

## Recommended Scope

## V1 Included

- Single-machine render queue.
- Add one or many `.blend` files to queue.
- Per-job config:
  - optional Blender executable override; empty inherits global Settings
  - render mode: `Animation`, `Frame Range`, `Single Frame`
  - scene
  - camera
  - view layer
  - output path pattern
  - output file name pattern
- Queue actions:
  - add
  - remove
  - duplicate
  - reorder by drag and drop
  - enable or disable job
  - start full queue
  - stop after current frame
- Runtime feedback:
  - current state
  - stdout and stderr log
  - frame progress when detectable
  - elapsed time
  - estimated remaining time when enough data exists
- Persist queue to local JSON.
- Persist tool settings to local JSON.
- Generate and pass helper Python scripts to Blender at runtime.

## V1 Explicitly Excluded

- watch folders
- scheduler/calendar tasks
- shutdown or sleep-prevention automation
- custom image/video viewers
- sound notifications
- skin editor
- UI translation system
- parallel GPU auto-splitting across multiple Blender instances
- advanced device discovery UI parity with the reference app
- full compositing auto-rewrite logic beyond simple supported cases

## V1.5 Or Optional

- named render presets
- recent jobs
- reopen output folder
- open rendered frame
- retry failed job
- skip already rendered frames
- timeout detection for stalled renders
- automatic Blender executable discovery

## V2 Candidates

- GPU allocation profiles
- scheduled queue start
- watch folder ingestion
- batch job templates
- frame chunk splitting
- render statistics dashboard
- EXR-aware output inspection

## Recommended Feature Cuts From The Reference App

The reference app is feature-rich, but this toolbox should start with a narrower product.

Cut these from the first implementation:

- skins and appearance customization
- multilingual UI
- media playback/viewer routing
- advanced scheduler
- watch folder trees
- aggressive Windows integration
- automatic parallel GPU distribution
- complex toolbar or table customization
- extensive popup-driven workflows

Keep these ideas:

- queue-first workflow
- per-job Blender version selection
- simple scene and view-layer targeting
- output pattern templating
- helper scripts for Blender-side adjustments
- log-driven progress parsing
- stop/resume-safe queue orchestration

## Additions Recommended For This Toolbox

The new tool should add a few repo-specific improvements instead of copying the reference behavior as-is.

- stronger separation between UI, queue state, and Blender invocation
- deterministic JSON schemas for settings and queue
- generated helper scripts as embedded resources or templates owned by this repo
- compact blend metadata card with manual `Update` action, live logs, and a latest-frame preview instead of a permanent command-preview panel
- structured error model instead of parsing only free-form text
- easier future testability for process building and output parsing

## User Stories

1. As a user, I can add several `.blend` files and render them one by one without reopening Blender manually.
2. As a user, I can override scene, camera, or view layer per queue item.
3. As a user, I can send Blender a helper script that changes output path naming before render starts.
4. As a user, I can render a full animation, a frame range, or a single frame.
5. As a user, I can reload blend defaults and inspect logs when something fails.
6. As a user, I can stop after the current frame instead of killing Blender immediately.
7. As a user, I can reopen my queue on the next app launch.

## Functional Requirements

## Queue Model

Each queue item must contain:

- unique id
- enabled flag
- source blend path
- optional Blender executable override
- render mode
- frame selection data
- scene name
- camera name
- view layer name
- output naming config
- status
- timestamps
- progress counters
- last known output path
- last error summary

Suggested statuses:

- `Pending`
- `Inspecting`
- `Ready`
- `Rendering`
- `Stopping`
- `Completed`
- `Failed`
- `Canceled`
- `Skipped`

## Blender Discovery And Selection

The app supports:

- one global Blender executable path in `Settings`
- per-job override in Render Manager

The global settings file is:

```json
{
  "BlenderExecutablePath": "C:/Program Files/Blender Foundation/Blender 4.5/blender.exe",
  "ThemeOverride": "Auto",
  "LogsExpanded": true
}
```

## Blend Inspection

Before rendering, the tool should be able to inspect a `.blend` and extract lightweight metadata:

- scenes
- cameras
- view layers
- collections
- original output path
- maybe render frame start/end/step

Recommended implementation:

- run Blender headless with a repo-owned Python inspection script
- return JSON to stdout or temp file

This is safer and cheaper than parsing all `.blend` structure directly in C#.

## Render Modes

Supported render modes in V1:

- `Animation`
  - uses scene frame start/end/step or explicit override
- `Frame Range`
  - explicit start/end/step
- `Single Frame`
  - one frame number

Deferred:

- arbitrary sparse frame list
- auto-split into frame chunks
- viewport render modes

## Output Naming

The tool should support a controlled token-based naming system inspired by the reference app.

Suggested tokens:

- `[BLEND_NAME]`
- `[BLEND_PATH]`
- `[SCENE_NAME]`
- `[CAMERA_NAME]`
- `[VIEWLAYER_NAME]`
- `[FRAME]`
- `[JOB_INDEX]`
- `[ORIGINAL_OUTPUT_PATH]`
- `[ORIGINAL_OUTPUT_NAME]`

Rules:

- show live preview for path and filename
- validate invalid path characters
- keep path and filename templates separate
- support optional node output rewrite only if the helper script can do it reliably

## View Layers

The reference app exposes collection toggles and view-layer compositing behavior.
For this toolbox, keep it simpler.

V1:

- select one view layer
- apply selected view layer through a helper Python script

Deferred:

- multiple view layers per job
- advanced compositing rewrite modes
- collection-token UI parity with the reference app

## Device Selection

The reference app has substantial GPU handling, including device discovery and parallel distribution.
That is too much for V1.

V1:

- default device behavior is whatever the selected Blender installation uses
- no device-mode UI is exposed

V1.5:

- simple GPU backend selector like `CUDA`, `OPTIX`, `HIP`, `OneAPI`

Deferred:

- per-device multi-select UI
- occupancy tracking
- automatic GPU split scheduling

## Process Launch

Each job builds a Blender command from:

- executable path
- blend path
- background flag
- output path arguments
- frame arguments
- render arguments
- helper scripts

The tool must provide:

- resolved final command logging at launch time
- process cancellation
- stdout and stderr capture
- job exit code handling

## Progress And Logging

The tool should parse Blender output and derive:

- current frame
- rendered frame count
- elapsed time
- rough ETA when enough frame timings exist
- last saved output frame path for detail-panel preview

The UI must preserve raw logs even if parsed progress fails.

Recommended log policy:

- one session log per job run
- one aggregate tool log for diagnostics

## Persistence

Persist separately:

- global app settings
- Render Manager tool settings
- saved queue
- optional preset definitions

Recommended files:

- `%LocalAppData%/BlenderToolbox/global.json`
- `%LocalAppData%/BlenderToolbox/RenderManager/settings.json`
- `%LocalAppData%/BlenderToolbox/RenderManager/queue.json`
- `%LocalAppData%/BlenderToolbox/RenderManager/presets.json`

## UX Spec

## Main Screen

Recommended layout:

- top toolbar with queue actions
- left or top form for selected item settings
- main grid with queue items
- bottom log panel

Queue columns:

- enabled
- name
- blend file
- mode
- scene
- camera
- view layer
- frames
- output
- status
- progress
- elapsed
- ETA

## Core Actions

- `Add Blend`
- `Duplicate`
- `Remove`
- `Update`
- `Start`
- `Stop`
- `Resume`
- `Open Output Folder`

## Editing Model

Recommended behavior:

- single selected item shows full editor
- multi-select supports only limited batch changes
- invalid job fields should be highlighted before render starts

## Error UX

Show errors at three levels:

- validation errors before launch
- process launch errors
- Blender runtime errors

Avoid modal popups for every warning.
Prefer inline status plus expandable log details.

## Technical Design

## Proposed Project Structure

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

Suggested service split:

- `BlendInspectionService`
- `RenderCommandBuilder`
- `RenderQueueService`
- `RenderProcessService`
- `RenderProgressParser`
- `RenderSettingsStore`
- `RenderQueueStore`
- `OutputNamingService`

## Key Models

- `RenderQueueItem`
- `RenderJobSettings`
- `BlendInspectionResult`
- `RenderLaunchPlan`
- `RenderRunState`
- `RenderProgressSnapshot`
- `RenderManagerSettings`

## Blender Helper Scripts

This tool should own a small set of repo-local helper scripts, generated or copied at runtime:

- inspect blend metadata
- apply output path overrides
- apply view layer selection
- optional force quit on selected completion behavior

All helper scripts must be versioned in this repo.
Do not depend on files from `_ref`.

## Validation Rules

Before job start, validate:

- blend file exists
- Blender executable exists
- output template resolves to a valid path
- selected scene exists if specified
- selected camera exists if specified
- selected view layer exists if specified
- frame values are valid

If inspection metadata is stale, the tool may require re-inspection.

## Testing Strategy

Unit tests:

- command builder
- output token expansion
- queue serialization
- progress parser
- validation rules

Integration tests:

- helper script generation
- process start with mocked Blender output
- queue persistence round trip

Manual test matrix:

- animation render
- single frame render
- missing blend
- missing Blender path
- invalid view layer
- stop during render
- reopen app with saved queue

## Implementation Phases

## Phase 1

- project scaffold
- settings store
- queue model
- main view shell
- add and remove jobs

## Phase 2

- Blender executable config
- blend inspection script
- item editor for scene, camera, and view layer
- automatic blend metadata reload

## Phase 3

- actual render launch
- log capture
- progress parser
- stop behavior

## Phase 4

- output naming templates
- collection overrides, if they return as an explicit user need
- queue persistence

## Phase 5

- refinement
- retry and reopen output folder
- tests and docs

## Open Decisions

These decisions should be made before implementation starts:

1. Should `Frame Range` support sparse lists in V1 or only start/end/step?
2. Should output-path rewriting also touch compositor `OUTPUT_FILE` nodes in V1?
3. Should collection overrides return at all; if yes, should they be include-based, exclude-based, or both?
4. Should the tool keep one active render at a time only, or allow limited local parallelism later?
5. Should Blend inspection be automatic on add, or manual via `Update`?

## Recommended Default Answers

To keep the first version small:

1. start with start/end/step only
2. support node rewrite only after the basic output rewrite is stable
3. use exclude-based overrides first because it maps well to Blender helper scripting
4. allow only one active render at a time
5. inspect automatically on add, with manual re-inspect action

## Success Criteria

The first release is successful if:

- the tool can render queued `.blend` jobs reliably on one machine
- common targeting overrides work without editing the blend manually
- failures are diagnosable from the UI log
- the implementation stays isolated and understandable
- the tool remains notably simpler than the reference app while preserving its most valuable workflow shortcuts

## Current Implementation Note

Date: `2026-04-16`

`Render Manager` is now a working in-repo tool with real Blender process execution and blend inspection.

What the current implementation already covers:

- tool project scaffold
- shell registration
- global Settings screen with shared Blender path and theme override
- persisted Render Manager settings
- persisted queue
- queue actions for add, duplicate, reset, remove, drag-reorder, and enable or disable
- real `Start / Stop / Resume` process execution; Stop requests halt after the current frame
- automatic and manual blend inspection with scene, camera, view-layer, frame, and output metadata
- buffered live log output plus per-job log files
- parsed per-job frame progress
- queue-wide discrete progress by finished jobs
- whole-job ETA based on average completed frame time
- resume from the current or next frame instead of restarting the whole job
- latest-frame preview decode is lazy and only runs for the selected job

What is still not at release quality:

- the planned sub-VM split is now present under `ViewModels/Jobs`; `RenderQueueItemViewModel` remains as a thin compatibility adapter while older services and commands move over incrementally
- old saved queue entries may not contain newer runtime fields until they are rerun
- UI polish and documentation still need cleanup passes

Practical implication:

- the tool is usable for local queue rendering and iteration
- the next implementation steps should focus on selector responsiveness, UI cleanup, and broader override architecture
