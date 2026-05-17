# SFX — Asset Specifications

## What goes here
Sound effects, categorized into combat, UI, and ambient.

## Files required for launch (per GDD §20, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| sfx_combat_<name>.wav | — | WAV 44.1kHz/16-bit | Hits, deaths, weapon fire |
| sfx_ui_<name>.wav | — | WAV 44.1kHz/16-bit | Clicks, drags, confirms |
| sfx_ambient_<name>.wav | — | WAV 44.1kHz/16-bit | Biome and atmosphere loops |

~65 SFX required for full launch. SFX are **optional for launch-lite** — the game is
playable without them.

## File naming convention
`sfx_<category>_<name>.wav` — category is one of: combat, ui, ambient.

## Production order (priority)
1. Combat SFX (most-heard — hit, fire, death)
2. UI SFX (click, drag, sell, confirm)
3. Ambient SFX last

## Specifications
- Format: WAV
- Sample rate: 44.1 kHz
- Bit depth: 16-bit
- Mono for positional combat/UI cues; stereo for ambient beds
- Short, tight transients for combat; trim leading silence

## Placeholder status
No audio placeholders are committed. No SFX ship in the project today; combat and UI
play silently. Real SFX drop in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Wire the clip into the relevant SFX trigger / audio event
3. Route through the SFX mixer group once `MainMixer.mixer` exists
4. Save

## GDD reference
§20 (Audio), §23 (Asset Budget)
