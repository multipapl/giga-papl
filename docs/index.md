# Toolbox Docs

Short entry point for people and agents working in this repo.

## Read First

- [Architecture](./architecture.md)
- [Tools](./tools.md)
- [UI And Theme](./ui-and-theme.md)
- [Build And Release](./build-and-release.md)

## Reference Only

- [Renderset Integration](./renderset-integration.md) is research material, not an active implemented feature.

## Current State

- Stack: `C#`, `.NET 8`, `WPF`
- Shell: one desktop app, left navigation, one active tool view
- Current tools:
  - `Lazy Frame Rename`
  - `Render Manager`
  - `Split By Context`
- Shared tool contract: `IToolDefinition` with optional `IStatefulTool`
- Shared app settings live in `%LocalAppData%/BlenderToolbox/global.json`.
- Render Manager queue state lives in `%LocalAppData%/BlenderToolbox/RenderManager/queue.json`.

## Working Rules

- Do not add heavy framework dependencies without a clear need.
- A new tool should be a separate project or at least a separate module without depending on other tools.
- Keep visual tokens and shared styles out of tool-local XAML where possible.
- Move truly shared settings out of tool-local settings stores when they become cross-tool concerns.
