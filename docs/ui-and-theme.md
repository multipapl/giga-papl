# UI And Theme

## Theme Sources

Base styles and tokens live in:

- `src/BlenderToolbox.App/Themes/Design.xaml`
- `src/BlenderToolbox.App/Themes/Controls.xaml`
- `src/BlenderToolbox.App/Themes/Theme.Dark.xaml`
- `src/BlenderToolbox.App/Themes/Theme.Light.xaml`

## Theme Behavior

- The main window opens maximized by default.
- The app has a global `Settings` screen under the sidebar `APP` section.
- `Theme` supports `Auto`, `Light`, and `Dark`.
- `Auto` follows the Windows app theme preference.
- `Light` and `Dark` pin the respective XAML theme and ignore Windows theme changes until the user switches back to `Auto`.
- Theme changes save to `global.json` and apply immediately.

## Global Settings

Stored in `%LocalAppData%/BlenderToolbox/global.json`:

```json
{
  "BlenderExecutablePath": "C:/Program Files/Blender Foundation/Blender 4.5/blender.exe",
  "ThemeOverride": "Auto",
  "LogsExpanded": true
}
```

The Settings screen currently owns:

- Blender executable path
- theme override
- log folder reveal
- app data folder reveal

## UI Rules

- Do not hardcode colors in tool XAML.
- Do not hardcode radii or button/list styles locally unless the control genuinely needs a local variant.
- Use shared styles:
  - `PanelCardStyle`
  - `InsetPanelCardStyle`
  - `PrimaryButtonStyle`
  - `SecondaryButtonStyle`
  - `DangerButtonStyle`
  - `HelperTextStyle`
  - `SectionTitleTextStyle`
  - `FieldLabelTextStyle`
- Expander headers and tooltips are styled globally and must use dynamic theme resources.
- Tool-local override styles may exist when they reflect real state, such as inherited versus overridden values.
