# VFX — Asset Specifications

## What goes here
Particle textures and status-effect overlay sprites used by combat and ability effects.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| vfx_particle_<name>.png | 64×64–128×128 | PNG, transparent | Particle system textures |
| vfx_status_<name>.png | 128×128–256×256 | PNG, transparent | Status-effect overlays (burn, poison, etc.) |

## File naming convention
`vfx_<category>_<name>.png` — category is one of: particle, status.

## Production order (priority)
1. Core combat hit / death particles
2. Status-effect overlays for shipped status effects
3. Ambient and polish VFX last

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Particle textures: soft-edged, designed for additive or alpha blending
- Status overlays: tileable or full-frame as the effect requires

## Placeholder status
No image placeholders exist. VFX currently render via code (flat-colored shapes / no
textures). Real particle and overlay art drops in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Assign particle textures to the relevant Particle System material
3. Assign status overlays to the effect's renderer
4. Save

## GDD reference
§23 (Asset Budget)
