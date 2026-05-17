# Companion — Asset Specifications

## What goes here
Companion portrait sprites. The companion is the player's guide and the narrative
villain; each portrait state reflects a stage in the betrayal arc.

## Files required for launch (per GDD §7, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| companion_composed.png | 512×512 | PNG, transparent | Default early-run state |
| companion_concerned.png | 512×512 | PNG, transparent | Mid-run, things going wrong |
| companion_pleading.png | 512×512 | PNG, transparent | Asks the player to trust it |
| companion_glitched.png | 512×512 | PNG, transparent | Mask slipping; visual corruption |
| companion_devil.png | 512×512 | PNG, transparent | True form revealed |
| companion_final.png | 512×512 | PNG, transparent | Final confrontation state |

## File naming convention
`companion_<state>.png` — state is one of: composed, concerned, pleading, glitched,
devil, final.

## Production order (priority)
1. companion_composed (seen first, most screen time)
2. companion_concerned
3. companion_pleading
4. companion_glitched
5. companion_devil
6. companion_final

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Portrait framing — head and shoulders, occupies ~80% of frame
- Future: per-state animation frames reserved as `companion_<state>_<frame>.png`

## Placeholder status
No image placeholders exist. No companion art is committed; the companion is currently
represented in code only. Real portraits drop in here later without code changes.

## Integration notes
1. Drop files into this folder
2. Wire the portrait set into the companion display component in Inspector
3. Each state maps to a narrative trigger point in the run
4. Save

## GDD reference
§7 (Companion — Villain Arc), §23 (Asset Budget)
