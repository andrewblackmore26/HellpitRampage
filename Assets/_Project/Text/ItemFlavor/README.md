# Item Flavor — Asset Specifications

## What goes here
Flavor text for items — the 1-2 sentence atmospheric description shown in tooltips.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| item_flavor.json | — | JSON or CSV | 50 items × 1-2 flavor sentences |

## File naming convention
`item_flavor.<ext>` — ext is `json` or `csv`.

## Schema (per entry)
- `item_id` — matches `ItemData.Id`
- `flavor` — 1-2 sentence flavor text

## Production order (priority)
1. Starter / common items
2. Mid-tier shop items
3. Rare and synergy-keystone items last
(Match the icon production order in `Art/Items`.)

## Specifications
- Encoding: UTF-8
- 50 entries, one per item
- 1-2 sentences each — terse, not mechanical (mechanics live on `ItemData`)
- Tone guide: occult horror, terse. Dread over gore; suggestion over description.
  No flourish, no copyrighted references.

## Placeholder status
No final flavor text is committed. Tooltips currently show mechanical data only, with
placeholder or empty flavor. Real flavor data drops in here later; the tooltip system
reads this file without code changes.

## Integration notes
1. Drop the file into this folder
2. Point the item-flavor loader at this file
3. The tooltip resolves `flavor` by `item_id` at display time
4. Save

## GDD reference
§23 (Asset Budget)
