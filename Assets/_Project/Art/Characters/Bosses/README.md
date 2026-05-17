# Bosses — Asset Specifications

## What goes here
Boss enemy sprites. Bosses are large, distinct encounters that punctuate a run.

## Files required for launch (per GDD §19, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| boss_<snake_case>.png | 256×256 | PNG, transparent | One file per boss |

6 boss sprites required for launch. Exact roster defined by `EnemyData` assets
under `Assets/_Project/ScriptableObjects/Enemies/`.

## File naming convention
`boss_<snake_case>.png` matching the `EnemyData.Id` field.

## Production order (priority)
1. First boss the player encounters (earliest round)
2. Mid-run bosses, in encounter order
3. Final boss (round 30) last

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Character occupies ~80% of frame
- Facing-down (toward camera) idle pose
- Future: attack/phase poses reserved as `boss_<name>_<pose>.png`

## Placeholder status
No image placeholders exist. The round-30 placeholder boss currently has NO sprite —
it reuses the basic-enemy prefab scaled up and tinted dark red in code. Real boss art
replaces that placeholder; sprites drop in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Open the matching EnemyData asset in Inspector (`Assets/_Project/ScriptableObjects/Enemies/<boss_id>.asset`)
3. Drag the new sprite into the `Sprite` field
4. Save

## GDD reference
§19 (Biomes & Enemy Roster), §23 (Asset Budget)
