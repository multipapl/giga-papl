# Blender Toolbox

Standalone Windows desktop toolbox for small Blender production utilities.

## Stack

- `C#`
- `.NET 8`
- `WPF`

## Current Tools

- `Lazy Frame Rename`
- `Render Manager`
- `Split By Context`

## Quick Use

1. Start the app:

```powershell
dotnet run --project .\src\BlenderToolbox.App\BlenderToolbox.App.csproj
```

2. Open `Settings` from the `APP` section and set the shared Blender executable path.
3. Open `Render Manager`, click `Add Blend`, and choose one or more `.blend` files.
4. Select a job in the queue. The job name is the blend file name.
5. Use the top action row:
   - `Start` renders enabled jobs.
   - `Stop` finishes the current frame, then pauses the queue.
   - `Resume` continues a paused queue.
   - `Reset` clears runtime state for the selected job.
   - `Update` reloads metadata from the selected `.blend` file.
6. In the detail panel, values inherit from the `.blend` file by default. Changing an output, frame, Blender executable, scene, camera, or view layer creates an override. Use the circular reset icon to return that field to the blend/global default.

## Run

```powershell
dotnet run --project .\src\BlenderToolbox.App\BlenderToolbox.App.csproj
```

## Validate

```powershell
dotnet build .\BlenderToolbox.sln
dotnet test .\BlenderToolbox.sln
```

## Project Layout

```text
src/
  BlenderToolbox.App/
  BlenderToolbox.Core/
  BlenderToolbox.Tools.LazyFrameRename/
  BlenderToolbox.Tools.RenderManager/
  BlenderToolbox.Tools.SplitByContext/

tests/
  BlenderToolbox.Core.Tests/
```

## Principles

- Keep tools isolated.
- Keep UI simple and maintainable.
- Prefer vertical slices over speculative architecture.
- Move shared logic to `Core` only when at least two tools need it.
