# UI Text — Asset Specifications

## What goes here
UI string data — button labels, tooltip headers, menu copy, and other interface text.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| ui_text.json | — | JSON or CSV | ~50 UI strings |

## File naming convention
`ui_text.<ext>` — ext is `json` or `csv`.

## Schema (per entry)
- `string_id` — stable unique key referenced by UI code
- `text` — the displayed English string

## Production order (priority)
1. Combat HUD and core navigation strings
2. Shop and inventory strings
3. Settings and menu copy last

## Specifications
- Encoding: UTF-8
- ~50 entries
- English only — no localization for launch; the `string_id` indirection leaves room
  for it later
- Keep labels short enough for code-built UI layout

## Placeholder status
No final UI string file is committed. UI text is currently hardcoded in code-built UI.
Real string data drops in here later; UI code can migrate to `string_id` lookups
without layout changes.

## Integration notes
1. Drop the file into this folder
2. Point the UI-text loader at this file
3. UI code resolves display text by `string_id`
4. Save

## GDD reference
§23 (Asset Budget)
