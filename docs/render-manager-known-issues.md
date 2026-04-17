# Render Manager — known issues / deferred fixes

Дрібні моменти, помічені під час поверхневого ревю, які навмисно лишили на потім.

## 1. Невикористані поля інспекції blend-файлу

`BlendInspectionSnapshot.BlendFileSizeBytes` і `BlendInspectionSnapshot.BlendFileLastWriteUtc`
([Models/BlendInspectionSnapshot.cs:17-19](../src/BlenderToolbox.Tools.RenderManager/Models/BlendInspectionSnapshot.cs#L17-L19))
зараз пишуться в `BlendInspectionService.RunInspectionAsync`, але ніде не читаються.

Призначені для майбутнього stale-detection кешу інспекції (див. розділ
"Caching" у попередній renderset-спеці): автоматично пере-інспектувати blend, якщо змінився
розмір або mtime; інакше переюзати знімок.

**Що зробити:** при наступному запуску `Update`/авто-інспекції порівнювати ці поля з поточним
`FileInfo` blend-файлу і пропускати запуск Blender, якщо все збігається.

## 2. Подвійне джерело істини у `JobRendersetViewModel.SelectedContextNames`

[ViewModels/Jobs/JobRendersetViewModel.cs:21-27](../src/BlenderToolbox.Tools.RenderManager/ViewModels/Jobs/JobRendersetViewModel.cs#L21-L27)

```csharp
public IReadOnlyList<string> SelectedContextNames => Contexts.Count == 0
    ? _selectedContextNames
    : Contexts.Where(...).Select(...).Where(...).ToList();
```

Дві проблеми:
- При непорожньому `Contexts` геттер алокує новий `List<string>` на кожен доступ — а
  `OnPropertyChanged(nameof(SelectedContextNames))` смикається часто (при будь-якій зміні
  collection чи `IsSelected`).
- Подвійне джерело істини: іноді віддається кеш `_selectedContextNames`, іноді — фреш-обчислення.
  `OnContextsCollectionChanged` не викликає `SyncSelectedContextNames`, тому пряма мутація
  `Contexts` ззовні розсинхронізує кеш.

**Що зробити:** завжди повертати `_selectedContextNames` і тримати його актуальним: викликати
`SyncSelectedContextNames` у `OnContextsCollectionChanged` теж.

## 3. `FindLatestPreviewableFile` рекурсивно сканує тек на кожен кадр

[ViewModels/RenderManagerViewModel.cs:1288-1362](../src/BlenderToolbox.Tools.RenderManager/ViewModels/RenderManagerViewModel.cs#L1288-L1362)

`ApplyRendersetFrameFolder` викликається на кожен `<<RSET_FRAME>>` маркер і запускає
`FindLatestPreviewableFile`, який робить `Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)`
+ `new FileInfo(path)` + сортування по `LastWriteTimeUtc`.

Для рендеру з 100 кадрів × 10 контекстів = 1000 повних обходів дерева на UI-потоці.
На великих output-теках це помітно просяде.

**Що зробити:** з'ясувати реальну структуру output-папки RenderSet (чи зберігається кадр
у самій папці `Folder`, чи в підпапці). Якщо файл лежить у переданій папці — досить
`SearchOption.TopDirectoryOnly` або взагалі брати конкретний шлях за патерном
імені кадру замість сканування.
