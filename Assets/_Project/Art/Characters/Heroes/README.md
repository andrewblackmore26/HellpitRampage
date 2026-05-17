# Heroes — Asset Specifications

## What goes here
Hero character sprites used in combat. One per playable hero.

## Files required for launch (per GDD §15, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| hero_glass_lamb.png | 256×256 | PNG, transparent | Idle pose facing down; tank archetype |
| hero_husk_crow.png | 256×256 | PNG, transparent | Glass cannon, lean build |
| hero_veil_moth.png | 256×256 | PNG, transparent | Mysterious, robed |
| hero_wickling.png | 256×256 | PNG, transparent | Sustain archetype; warm tone |
| hero_mooneye.png | 256×256 | PNG, transparent | Unlockable chaos hero |

## File naming convention
`hero_<snake_case_name>.png` matching `HeroData.Id` field.

## Production order (priority)
1. hero_glass_lamb (starting hero)
2. hero_husk_crow (first unlock)
3. hero_veil_moth
4. hero_wickling
5. hero_mooneye (gated behind beating final boss)

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Character occupies ~80% of frame
- Facing-down (toward camera) idle pose
- Future: walking/attacking poses reserved as `hero_<name>_walk.png`, `hero_<name>_attack.png`

## Placeholder status
No image placeholders exist. The game currently renders the single hero via existing
code; real sprites drop in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Open the matching HeroData asset in Inspector (`Assets/_Project/ScriptableObjects/Heroes/<hero_id>.asset`)
3. Drag the new sprite into the `Sprite` field
4. Save

## GDD reference
§15 (Heroes — Locked Framework, Abilities Placeholder), §23 (Asset Budget)
