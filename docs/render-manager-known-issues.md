# Render Manager — known issues / deferred fixes

Станом на поточну версію відкладені пункти з цього файлу закриті.

## Закрито

- `BlendInspectionSnapshot.BlendFileSizeBytes` і `BlendInspectionSnapshot.BlendFileLastWriteUtc`
  тепер використовуються для stale-detection: якщо blend-файл не змінив розмір або `mtime`,
  повторний `Update` переюзує наявний snapshot і не запускає Blender.
- `JobRendersetViewModel.SelectedContextNames` має єдине джерело істини:
  повертається кеш `_selectedContextNames`, а прямі мутації `Contexts` і зміни `IsSelected`
  синхронізують кеш.
- Пошук RenderSet preview винесено в `RenderPreviewFileFinder`: спочатку перевіряється
  передана output-папка без рекурсії, рекурсивний обхід лишається fallback-ом, а вибір
  latest-файлу більше не сортує весь список кандидатів.
