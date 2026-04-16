# Render Manager - Future Plans & Ideas

Captured 2026-04-15 from user feedback after first functional test.

**Priority**: get basic render execution working first, then iterate on these.

---

## 1. Override / Modifier Architecture

The app should be built around the concept of **overrides**. Each render job field
inherits its value from the `.blend` file by default and can be explicitly overridden:

- **Output path** - use the path set in the blend file, or override with a custom path.
- **Output filename** - same principle. Output format is inherited from the `.blend` file.
- **Frame range** - render what the blend file specifies, or override start/end/step.
- **Camera, scene, view layer** - inherit or override.

**Fool-proofing**: if the blend file has no output path set (Blender default `/tmp/`),
fall back to `<blend_directory>/renders/<blend_name>_####`. Log this decision clearly.

UI hint: each overridable field should have a checkbox on the left.
Unchecked = inherit from the `.blend` file.
Checked = enable the field or dropdown and use the override value.

Current implementation note:

- scene, camera, and view-layer lists are populated through blend inspection
- changing scene re-scopes camera and view-layer lists without clearing a stored out-of-list value
- selectors are disabled while inspection is pending, running, or failed

## 2. Simplified Toolbar

The only queue-level actions needed:

| Action | Notes |
|--------|-------|
| **Start** | Begin rendering the queue from the first enabled/pending job |
| **Stop** | Request stop after the current frame, then mark the job as canceled |
| **Resume** | Continue from the paused/canceled job |
| **Add .blend** | File picker to add a new job |
| **Remove** | Remove selected job(s) from the queue |
| **Reorder** | Drag & drop rows in the queue grid |

Start / Stop / Resume buttons must be visually prominent (larger, colored, separated
from the rest of the toolbar).

## 3. Global Blender Executable Setting

Status: implemented.

The Blender executable path is shared by Split by Context and Render Manager through
the app-level `Settings` screen. Lazy Frame Rename does not use Blender and is
unaffected.

Each render job retains a per-job override field: if populated, that job uses its own
Blender; if empty, it inherits the global default. Same override/modifier pattern as #1.

## 4. No Command Preview Panel

The detail panel does not need a dedicated command preview block.

- keep the UI focused on job data, overrides, logs, and the latest-frame preview
- if launch diagnostics are needed later, prefer structured logging over a permanent preview widget

## 5. Render Preview (Last Frame)

Status: implemented in the current prototype.

What now exists:

- parse `Saved: '<path>'` from Blender stdout
- show the last rendered frame in a `Latest Frame` preview block in the detail panel
- update after each frame completes, not real-time, just the latest saved file
- support common formats: PNG, EXR (tone-mapped preview), JPEG, TIFF

Follow-up polish that may still be useful later:

- add explicit preview reload action if users want it
- evaluate whether preview caching or temp thumbnail files are needed for very long renders
- improve placeholder and error messaging for partially written files

## 6. Comprehensive Logging

### What to log

- full Blender stdout/stderr (every line)
- app-level events: job started, stopped, resumed, completed, failed, skipped
- override decisions: "Output path not set in blend file, using fallback X"
- timing: per-frame render time, total job time, total queue time

### Where to store

Current app data location: `%LocalAppData%/BlenderToolbox/RenderManager/`

Proposal:

- **Per-job log files**: `%LocalAppData%/BlenderToolbox/RenderManager/logs/<job_id>.log`
- **In-memory log**: kept in `RenderQueueItemViewModel.LogOutput` for UI display
- consider log rotation or max-size limits for long animation renders

### Current storage locations

- Global settings: `%LocalAppData%/BlenderToolbox/global.json`
- Render Manager settings: `%LocalAppData%/BlenderToolbox/RenderManager/settings.json`
- Queue state: `%LocalAppData%/BlenderToolbox/RenderManager/queue.json`

## 7. Renderset Integration (Backlog)

[Renderset](https://blendermarket.com/products/renderset) is a Blender addon that
manages multiple render contexts (camera + scene + output combos) within one blend file.

If a blend file has Renderset installed and has multiple active contexts, the Render
Manager could detect this and render each context using Renderset's headless CLI
capabilities (if available).

**TODO**: research Renderset's headless rendering API / CLI flags. This is an advanced
feature - implement after core rendering is solid.

---

*This document captures ideas for future development. Implementation order will follow
user priorities and the milestones defined in `render-manager-implementation-plan.md`.*
