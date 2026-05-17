# Music — Asset Specifications

## What goes here
Background music tracks — one per scene or biome. All tracks loop seamlessly.

## Files required for launch (per GDD §20, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| music_main_menu.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_boot.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_outskirts.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_inner_town.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_depths.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_shop.<ext> | — | WAV or OGG | Loopable, 2-4 min |
| music_boss.<ext> | — | WAV or OGG | Loopable, 2-4 min |

7 tracks required for launch.

## File naming convention
`music_<scene_or_biome>.<ext>` — ext is `wav` or `ogg`.

## Production order (priority)
1. music_main_menu (first thing the player hears)
2. music_outskirts (first combat biome)
3. music_inner_town, music_depths
4. music_shop, music_boss
5. music_boot

## Specifications
- Format: WAV (lossless source) or OGG (compressed delivery)
- Sample rate: 44.1 kHz
- Bit depth: 16-bit
- Stereo
- Seamless loop — author with matching loop points; no audible seam
- Length: 2-4 minutes per track

## Placeholder status
No audio placeholders are committed. This folder may already contain temporary
placeholder audio; treat it as non-final. The game uses no music until real tracks
drop in here. No code changes are required to swap them.

## Integration notes
1. Drop file into this folder
2. Set the importer Load Type to Streaming for long tracks
3. Wire the clip into the music playback config for its scene/biome
4. Save

## GDD reference
§20 (Audio), §23 (Asset Budget)
