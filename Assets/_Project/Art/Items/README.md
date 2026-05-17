# Items — Asset Specifications

## What goes here
Item icon sprites shown on the backpack grid and in the shop. One per item.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| item_<snake_case>.png | 128×128 | PNG, transparent | One file per item |

50 item icons required for launch. Exact roster defined by `ItemData` assets under
`Assets/_Project/ScriptableObjects/Items/`.

## File naming convention
`item_<snake_case>.png` matching the `ItemData.Id` field.

## Production order (priority)
1. Starter / common items the player sees first
2. Mid-tier shop items
3. Rare and synergy-keystone items last

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Icon occupies ~90% of a single grid cell's worth of frame
- Multi-cell items: the icon is drawn within the item's footprint by existing code; the
  source PNG is still a single 128×128 icon

## Placeholder status
No image placeholders exist. Items currently render via code `PlaceholderColor` —
flat colored rectangles sized to the item's grid shape. Real icons drop in here later
without code changes.

## Integration notes
1. Drop file into this folder
2. Open the matching ItemData asset in Inspector (`Assets/_Project/ScriptableObjects/Items/<item_id>.asset`)
3. Drag the new sprite into the `Sprite` field
4. Save

## GDD reference
§23 (Asset Budget)
