# Enemies — Asset Specifications

## What goes here
Standard (non-boss) enemy sprites spawned in combat waves. One per enemy type.

## Files required for launch (per GDD §19, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| enemy_<snake_case>.png | 128×128 | PNG, transparent | One file per enemy type |

15-18 enemy sprites required for launch. Exact roster defined by `EnemyData` assets
under `Assets/_Project/ScriptableObjects/Enemies/`.

## File naming convention
`enemy_<snake_case>.png` matching the `EnemyData.Id` field.

## Production order (priority)
1. Earliest-biome enemies (Outskirts spawn pool) first
2. Inner Town enemies second
3. Depths enemies last
Within a biome, produce the most-frequently-spawned enemy first.

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Character occupies ~80% of frame
- Facing-down (toward camera) idle pose
- Future: walking/attacking poses reserved as `enemy_<name>_walk.png`, `enemy_<name>_attack.png`

## Placeholder status
No image placeholders exist. Enemies currently render via existing code (placeholder
prefab visuals). Real sprites drop in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Open the matching EnemyData asset in Inspector (`Assets/_Project/ScriptableObjects/Enemies/<enemy_id>.asset`)
3. Drag the new sprite into the `Sprite` field
4. Save

## GDD reference
§19 (Biomes & Enemy Roster), §23 (Asset Budget)
