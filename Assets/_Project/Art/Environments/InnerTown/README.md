# Inner Town — Asset Specifications

## What goes here
Background art for the Inner Town biome — the mid-run biome. One backdrop plus optional
ambient overlays.

## Files required for launch (per GDD §19, §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| bg_inner_town.png | 1920×1080 | PNG or JPG | Primary biome backdrop |
| bg_inner_town_overlay.png | 1920×1080 | PNG, transparent | Optional ambient overlay (fog, particles) |

## File naming convention
`bg_<biome>.png` for the backdrop; `bg_<biome>_overlay.png` for ambient overlays.

## Production order (priority)
1. bg_inner_town (core mid-run backdrop)
2. bg_inner_town_overlay (atmospheric polish)

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGB (backdrop) / RGBA (overlay)
- Backdrop: full-frame, no transparency needed (JPG acceptable)
- Overlay: transparent, sits above the backdrop
- Target 16:9; safe-area the focal content for other aspect ratios

## Placeholder status
No image placeholders exist. Biomes currently render as a flat camera background tint
set in code (`BiomeTransitionController`). Real backdrops drop in here later without
code changes.

## Integration notes
1. Drop files into this folder
2. Wire the backdrop into the biome's background renderer / `BiomeTransitionController` config
3. Overlay (if used) is parented above the backdrop
4. Save

## GDD reference
§19 (Biomes & Enemy Roster), §23 (Asset Budget)
