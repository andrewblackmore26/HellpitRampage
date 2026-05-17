# Companion Dialogue — Asset Specifications

## What goes here
Companion dialogue lines as structured data. Drives the companion's text through a run
and the betrayal arc.

## Files required for launch (per GDD §7)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| companion_dialogue.json | — | JSON or CSV | ~250 dialogue lines |

## File naming convention
`companion_dialogue.<ext>` — ext is `json` or `csv`. Split by act/state only if the
file grows unwieldy.

## Schema (per line)
- `line_id` — stable unique key; matches the voice file name
- `state` — companion portrait state: composed / concerned / pleading / glitched / devil / final
- `round_tag` — round or round-range the line is eligible for
- `tone` — delivery hint (calm, anxious, menacing, etc.)
- `text` — the line itself

## Production order (priority)
1. composed-state early-run lines
2. concerned / pleading mid-run lines
3. glitched / devil / final climax lines

## Specifications
- Encoding: UTF-8
- Line count target: ~250
- Round-tagged so the dialogue system can pick state- and round-appropriate lines
- Tone written occult-horror, terse — no flourish

## Placeholder status
No final dialogue is committed. The companion currently shows minimal or placeholder
text in code. Real dialogue data drops in here later; the dialogue system reads this
file without code changes.

## Integration notes
1. Drop the file into this folder
2. Point the dialogue loader at this file
3. The dialogue system filters by `state` + `round_tag` at runtime
4. Save

## GDD reference
§7 (Companion — Villain Arc)
