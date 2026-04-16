# Renderset Integration Guide

Гайд по керуванню аддоном `renderset` (polygoniq, версія 2.2.1) ззовні — з Render Manager або будь-якого іншого оркестратора. Весь нижченаведений матеріал базується на коді аддону у [_ref/renderset_personal/](../_ref/renderset_personal/) та офіційній документації headless-рендеру: https://docs.polygoniq.com/renderset/2.2.1/advanced_topics/headless_rendering/.

Цей документ самодостатній — для стандартних задач інтеграції повертатись до джерел не потрібно.

---

## 1. Ключова ідея

Renderset керується **не через виклик operator-а у `--background`**, а через прямі виклики Python API на об'єктах контексту. Головний batch-оператор модальний (таймер + event loop), у headless режимі не працює.

Робочий патерн:

```
Менеджер  ->  blender.exe --background file.blend --python driver.py -- <args>
              └─ driver.py читає scene.renderset_contexts, фільтрує, викликає .render()
```

---

## 2. Модель даних

Аддон реєструє на `bpy.types.Scene` дві властивості ([__init__.py:2474-2483](../_ref/renderset_personal/__init__.py#L2474-L2483)):

| Властивість | Тип | Призначення |
|-------------|-----|-------------|
| `scene.renderset_contexts` | `CollectionProperty[RendersetContext]` | Усі контексти у сцені. |
| `scene.renderset_context_index` | `IntProperty` | Індекс активного контексту. Зміна значення тригерить `apply()` нового контексту через handler ([__init__.py:2255-2258](../_ref/renderset_personal/__init__.py#L2255-L2258)). |

Клас `RendersetContext` — `PropertyGroup` ([renderset_context.py:235](../_ref/renderset_personal/renderset_context.py#L235)). Найважливіші поля і методи:

| Атрибут / метод | Тип | Коментар |
|-----------------|-----|----------|
| `custom_name` | `str` | Унікальне ім'я контексту. |
| `include_in_render_all` | `bool` | Чекбокс "Render". Default `True`. |
| `render_type` | `"still"` / `"animation"` ([RenderType enum, renderset_context.py:121](../_ref/renderset_personal/renderset_context.py#L121)) | Тип рендеру. |
| `is_animation` | `bool` (property) | Еквівалент `render_type == ANIMATION`. |
| `get_camera()` | `Object \| None` | Камера контексту (резолвиться через UUID). |
| `get_world()` | `World \| None` | World контексту. |
| `render(context, *, execute_post_render_actions=True, override_folder_path=None, time=None)` | `set[str]` | Запускає рендер. Повертає множину вихідних тек. Див. [renderset_context.py:1178](../_ref/renderset_personal/renderset_context.py#L1178). |
| `render_finished(scene, override_folder_path=None)` | `str` | Post-render handler, переносить файли з тимчасової теки у фінальну ([renderset_context.py:1015](../_ref/renderset_personal/renderset_context.py#L1015)). **Обов'язково підключити** до `bpy.app.handlers.render_post` перед викликом `.render()`. |
| `apply(context)` | – | Завантажує налаштування контексту (камеру, world, overrides, render settings) у сцену. Викликається автоматично при зміні `renderset_context_index`. |
| `sync(context)` | – | Зворотне: записує поточний стан сцени назад у контекст. |

За замовчуванням у файлі завжди мінімум один контекст. Критерій "файл з реальним multi-context":
`len(scene.renderset_contexts) > 1` або `sum(c.include_in_render_all for c in ...) > 1`.

---

## 3. Probe: розпізнавання файлів з контекстами

### 3.1 Через Blender у background (рекомендовано)

Скрипт `probe_renderset.py`:

```python
#!/usr/bin/python3
import bpy, json, sys

contexts = []
for i, c in enumerate(bpy.context.scene.renderset_contexts):
    cam = c.get_camera()
    contexts.append({
        "index": i,
        "name": c.custom_name,
        "include_in_render_all": c.include_in_render_all,
        "render_type": c.render_type,
        "camera": cam.name if cam else None,
    })

print("<<RSET_PROBE>>" + json.dumps({
    "count": len(contexts),
    "contexts": contexts,
}) + "<<END>>", flush=True)
```

Запуск:
```
blender.exe --background "path/to/file.blend" --python probe_renderset.py
```

Менеджер парсить stdout по маркерах `<<RSET_PROBE>>…<<END>>` (фільтруючи шум від Blender).

**Кешування.** Probe повільний (холодний старт Blender), тому менеджеру бажано кешувати результат по ключу `(шлях, mtime, розмір)`. Інвалідувати при зміні mtime.

### 3.2 Альтернатива: парсинг `.blend` без запуску Blender

Теоретично можливо через `blender-asset-tracer` або власний парсер — контексти зберігаються як ID-property на `Scene`. Але формат приватний і polygoniq може його міняти між версіями (наявність [renderset_context_old.py](../_ref/renderset_personal/renderset_context_old.py) це підтверджує). **Для продакшн-менеджера краще Probe через Blender** з агресивним кешем.

---

## 4. Рендер усіх відмічених контекстів

Канонічний скрипт з документації (адаптований):

```python
#!/usr/bin/python3
import bpy, datetime

time = datetime.datetime.now()  # спільний timestamp для всієї пачки
render_output_paths = set()

for i in range(len(bpy.context.scene.renderset_contexts)):
    bpy.context.scene.renderset_context_index = i   # тригерить apply()
    rset = bpy.context.scene.renderset_contexts[i]

    if not rset.include_in_render_all:
        continue

    bpy.app.handlers.render_post.append(
        lambda scene, dummy, r=rset: r.render_finished(scene)
    )
    try:
        output_folders = rset.render(bpy.context, time=time)
        render_output_paths |= output_folders
    finally:
        bpy.app.handlers.render_post.clear()

for p in render_output_paths:
    print(f"RENDERED: {p}", flush=True)
```

Нюанси:

- **Присвоєння `renderset_context_index = i` обов'язкове** перед викликом `.render()` — саме воно запускає `apply()`, який завантажує камеру/world/overrides у сцену. Без цього рендерити будеш зі старим станом.
- **`time=time`** — передавай один `datetime` на всю пачку, інакше у шляхах вихідних файлів буде розбіжність таймштампів між контекстами.
- **`r=rset` у лямбді** — захист від late-binding Python. Гарантує що handler отримає правильний контекст, а не той, що виявиться у `rset` на момент виклику handler-а.
- **`render_post.clear()`** — важливо очистити перед наступним контекстом, інакше handler-и накопичуватимуться.

---

## 5. Рендер обраних контекстів

Той самий цикл, але з фільтром. Приклад driver-а, що приймає список імен через argv:

```python
#!/usr/bin/python3
import bpy, datetime, sys

# argv після "--" належать скрипту, не Blender-у
raw = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
wanted_names = set(raw)   # або розпарси як JSON, якщо треба складніші опції

time = datetime.datetime.now()
for i, rset in enumerate(bpy.context.scene.renderset_contexts):
    if wanted_names and rset.custom_name not in wanted_names:
        continue

    bpy.context.scene.renderset_context_index = i
    bpy.app.handlers.render_post.append(
        lambda scene, dummy, r=rset: r.render_finished(scene)
    )
    try:
        paths = rset.render(bpy.context, time=time)
        for p in paths:
            print(f"RENDERED: {p}", flush=True)
    finally:
        bpy.app.handlers.render_post.clear()
```

Запуск:
```
blender.exe -b "file.blend" --python driver.py -- "Context A" "Context B"
```

Якщо імена можуть містити пробіли/лапки — передавай JSON-список одним аргументом:

```
blender.exe -b "file.blend" --python driver.py -- "[\"Context A\", \"Context B\"]"
```

І у driver-і:
```python
import json
wanted_names = set(json.loads(raw[0])) if raw else set()
```

Альтернатива — фільтр за індексами (`set(int(x) for x in raw)`). Індекси стабільні протягом сесії Blender, але `custom_name` — стабільніший ідентифікатор між сесіями та після редагувань списку.

---

## 6. Додаткові можливості `.render()`

Сигнатура: `render(context, execution_context='INVOKE_DEFAULT', execute_post_render_actions=True, override_folder_path=None, time=None)`.

| Параметр | Ефект |
|----------|-------|
| `execute_post_render_actions=False` | Пропустити post-render actions контексту (компресія, копіювання, нотифікації тощо). Корисно коли ними керує зовнішній менеджер. |
| `override_folder_path="X:/tmp/preview"` | Перенаправити вивід в іншу теку. Використовується і для preview-режиму. |
| `time=datetime.datetime.now()` | Заморожений timestamp для шаблонів у шляхах. |

**Preview-рендер.** Внутрішньо `.render()` з `override_folder_path` + викликом `set_preview_settings(context)` дає швидкий preview. Якщо потрібен саме preview — подивись як це робить модальний operator у [__init__.py:842-850](../_ref/renderset_personal/__init__.py#L842-L850) (він читає preview preset з preferences). Для більшості випадків достатньо override folder + зниженого `samples`/`resolution_percentage` перед викликом.

**Per-frame-range.** Можна змінити `scene.frame_start / frame_end / frame_step` **перед** викликом `.render()`. Але **render settings (samples, resolution, camera, engine) міняти після `apply()` немає сенсу** — контекст уже перезаписав їх. Якщо треба інші samples — редагуй сам контекст або використовуй preview-режим.

**Split per-context у окремі файли.** Polygoniq має готовий скрипт `renderset_split_by_context.py` (посилання у документації) — розбиває multi-context `.blend` на N окремих файлів по одному контексту кожен. Корисно коли рендер-ноди не мають аддона. Менеджер може викликати його як окрему операцію.

---

## 7. Корисні non-modal оператори

Ці можна викликати навіть у `--background` (вони не модальні):

| Оператор | Призначення |
|----------|-------------|
| `renderset.renderset_context_list_add_item` | Додати новий контекст програмно. [__init__.py:217](../_ref/renderset_personal/__init__.py#L217) |
| `renderset.renderset_context_list_delete_item` | Видалити контекст. [__init__.py:269](../_ref/renderset_personal/__init__.py#L269) |
| `renderset.render_context_list_move_item` | Переставити у списку. [__init__.py:323](../_ref/renderset_personal/__init__.py#L323) |
| `renderset.renderset_add_context_per_camera` | Згенерувати по контексту на кожну камеру сцени. [__init__.py:389](../_ref/renderset_personal/__init__.py#L389) |
| `renderset.add_context_from_viewport` | Додати контекст з поточного viewport-а. [__init__.py:454](../_ref/renderset_personal/__init__.py#L454) |
| `renderset.batch_rename_contexts` | Масове перейменування. [__init__.py:1304](../_ref/renderset_personal/__init__.py#L1304) |
| `renderset.switch_render_orientation` | Переключити вертикаль/горизонталь. [__init__.py:1279](../_ref/renderset_personal/__init__.py#L1279) |
| `renderset.save_and_pack` | Зберегти + упакувати `.blend`. [__init__.py:1023](../_ref/renderset_personal/__init__.py#L1023) |

Не використовувати у background:

- `renderset.render_all_renderset_contexts` — **модальний**, таймер, event loop. Замість нього — цикл з `.render()`.

---

## 8. Архітектура інтеграції з Render Manager

Рекомендована структура:

```
RenderManager (C#)
│
├── Scanner
│   └── Для кожного .blend у списку запусти probe_renderset.py,
│       парс stdout, закешуй (шлях, mtime) → список контекстів.
│
├── UI
│   └── Дерево: File → Contexts (name, render_type, checked).
│       Користувач обирає: all-checked / specific names / per-file overrides.
│
├── Dispatcher
│   ├── Генерує driver.py (або має один універсальний з argv).
│   ├── Для кожного файлу:
│   │   blender.exe -b <file> --python driver.py -- <json-args>
│   └── Обробляє чергу, concurrency, retries.
│
└── Monitor
    ├── Парс stdout ("RENDERED: <path>", логи Blender).
    ├── Прогрес (per-context, per-frame з render_post).
    └── Збір помилок/stderr.
```

**Стандартний протокол stdout.** Щоб менеджер надійно парсив вивід, driver має друкувати структуровані рядки з префіксами:

```
<<RSET_START>> {"context":"Context A", "index":0}
<<RSET_FRAME>> {"context":"Context A", "frame":12}
<<RSET_DONE>>  {"context":"Context A", "folders":["X:/out/..."]}
<<RSET_ERROR>> {"context":"Context A", "error":"..."}
```

Реалізація — додаткові handler-и на `render_post` та `try/except` навколо `.render()`.

**Ізоляція помилок.** Два варіанти:

- *Окремий процес на файл* — максимальна ізоляція, але дорожче (~5-10с на старт Blender).
- *Один процес на пачку файлів* (скрипт `renderset_render_all_multiple_files.py` у документації polygoniq) — відкриває файли через `bpy.ops.wm.open_mainfile` у циклі. Швидше, але crash в одному файлі вбиває всю пачку.

Для рендер-менеджера раджу **гібрид**: групувати короткі файли в одну сесію, важкі/ризиковані — окремим процесом.

---

## 9. Відомі застереження

1. **Модальний оператор ≠ headless.** Ніколи не викликай `bpy.ops.renderset.render_all_renderset_contexts` з background-скрипта — не спрацює.
2. **`apply()` перезаписує сцену.** Після `scene.renderset_context_index = i` стан сцени належить контексту. Якщо хочеш щось змінити (frame range, output path) — роби це **після** присвоєння індексу, але **до** `.render()`.
3. **`render_finished` — обов'язковий.** Без handler-а файли залишаться у тимчасовій теці, а фінальні шляхи не сформуються.
4. **Handler-и на `render_post` накопичуються.** Очищуй `bpy.app.handlers.render_post.clear()` між контекстами.
5. **Late-binding у лямбдах.** Використовуй `lambda scene, dummy, r=rset: r.render_finished(scene)` а не `lambda scene, dummy: rset.render_finished(scene)`.
6. **Video формат + Still.** Рендер still у відео-контейнер (FFMPEG, AVI) оператор блокує. У власному скрипті ти це можеш не ловити — перевіряй самостійно: якщо `render_type == STILL` і `image_settings.file_format in {'AVI_JPEG','AVI_RAW','FFMPEG'}` — фейли раніше. Див. [__init__.py:638-657](../_ref/renderset_personal/__init__.py#L638-L657).
7. **Версіонування API.** Наявність [renderset_context_old.py](../_ref/renderset_personal/renderset_context_old.py) натякає, що polygoniq ламав формат у минулому. Перевіряй сумісність при апгрейді renderset. Мінімум який зараз підтримується аддоном: Blender 4.2 ([blender_manifest.toml](../_ref/renderset_personal/blender_manifest.toml)).
8. **`use_lock_interface`.** Модальний оператор виставляє `scene.render.use_lock_interface = True`. У background воно не критично, але якщо драйвер хоче писати у сцену з render_post handler-ів — уникай модифікацій ID-класів (прямо зазначено у коментарі [renderset_context.py:1018-1020](../_ref/renderset_personal/renderset_context.py#L1018-L1020)).
9. **Turbo Tools.** Якщо в preferences стоїть `RenderOperator.TURBO_TOOLS`, `.render()` викличе `bpy.ops.threedi.render_*`. У файлі без Turbo Tools це впаде — або передбач перевірку, або форсуй `RenderOperator.STANDARD` у preferences до рендеру.

---

## 10. Швидкий чекліст інтеграції

- [ ] Додати `probe_renderset.py` у `src/RenderManager/Resources/` (або аналогічну теку).
- [ ] Реалізувати кеш probe-результатів по `(path, mtime)`.
- [ ] UI-контрол зі списком контекстів на файл з чекбоксами.
- [ ] `driver.py` з підтримкою `all` / `by-name` / `by-index` режимів через argv.
- [ ] Structured stdout protocol (`<<RSET_*>>` префікси).
- [ ] Черга запусків з concurrency + retries у менеджері.
- [ ] Парсер прогресу (per-frame та per-context).
- [ ] Обробка exit code Blender ≠ 0.

---

## 11. Посилання

- Документація polygoniq: https://docs.polygoniq.com/renderset/2.2.1/advanced_topics/headless_rendering/
- Манифест аддона: [_ref/renderset_personal/blender_manifest.toml](../_ref/renderset_personal/blender_manifest.toml)
- Основний клас контексту: [_ref/renderset_personal/renderset_context.py](../_ref/renderset_personal/renderset_context.py)
- Scene-властивості та модальний оператор: [_ref/renderset_personal/__init__.py](../_ref/renderset_personal/__init__.py)
- Utils доступу: [_ref/renderset_personal/renderset_context_utils.py](../_ref/renderset_personal/renderset_context_utils.py)
