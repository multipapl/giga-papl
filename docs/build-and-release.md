# Build And Release

## Local Run

```powershell
dotnet run --project .\src\BlenderToolbox.App\BlenderToolbox.App.csproj
```

## Validate Before Commit

```powershell
dotnet build .\BlenderToolbox.sln
dotnet test .\BlenderToolbox.sln
```

## Share With Someone

Найпростіший варіант: опублікувати self-contained Windows build.

```powershell
dotnet publish .\src\BlenderToolbox.App\BlenderToolbox.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

Результат буде в `src\BlenderToolbox.App\bin\Release\net8.0-windows\win-x64\publish\`.

## Notes

- Для першого поширення цього достатньо.
- Інсталятор не обов’язковий одразу.
- Якщо треба “скинути комусь один файл”, `PublishSingleFile=true` підходить добре.
