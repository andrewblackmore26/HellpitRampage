# UI — Asset Specifications

## What goes here
UI element art — panels, frames, buttons, and decorative icons used across menus,
the shop, and the combat HUD.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| ui_panel_<name>.png | varies | PNG, transparent | 9-slice; mark slice borders |
| ui_button_<state>.png | varies | PNG, transparent | 9-slice; normal/hover/pressed |
| ui_frame_<name>.png | varies | PNG, transparent | 9-slice where it scales |
| ui_icon_<name>.png | 64×64 | PNG, transparent | Fixed-size decorative icons |

~40 UI elements required for launch. Sizes are mixed; use 9-slice for any element that
stretches.

## File naming convention
`ui_<category>_<name>.png` — category is one of: panel, button, frame, icon.

## Production order (priority)
1. Combat HUD elements (seen every round)
2. Shop and inventory panels
3. Main menu and settings decoration last

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- 9-slice elements: keep slice borders consistent; note border insets per file
- Fixed icons: full-bleed within their frame

## Placeholder status
No image placeholders exist. UI is currently code-built with flat colors — no panel,
button, or frame art is committed. Real UI art drops in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Set the importer Sprite Mode and 9-slice borders for stretchable elements
3. Assign the sprite to the relevant `Image` component (or code-built UI binding)
4. Save

## GDD reference
§23 (Asset Budget)
