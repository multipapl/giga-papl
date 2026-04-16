# Toolbox

Standalone Windows desktop toolbox for small production-oriented utilities.

## Stack

- `C#`
- `.NET 8`
- `WPF`

## Current Tools

- `Lazy Frame Rename`
- `Render Manager`
- `Split By Context`

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
