# Bags — Asset Specifications

## What goes here
Bag icon sprites. A bag defines the backpack grid the player arranges items on.

## Files required for launch (per GDD §23)
| Filename | Dimensions | Format | Notes |
|----------|------------|--------|-------|
| bag_<snake_case>.png | varies | PNG, transparent | Size scales with the bag's grid footprint |

10-15 bag icons required for launch. Exact roster defined by `BagData` assets under
`Assets/_Project/ScriptableObjects/Bags/`.

## File naming convention
`bag_<snake_case>.png` matching the `BagData.Id` field.

## Production order (priority)
1. Starting bag the player begins a run with
2. Common shop bags
3. Rare / large-grid bags last

## Specifications
- Color space: sRGB
- DPI: 72
- Bit depth: 8-bit RGBA
- Transparent background
- Dimensions vary by bag grid size — keep cell aspect square; suggested 64px per grid cell
- Icon occupies ~90% of frame

## Placeholder status
No image placeholders exist. Bags currently render via code-built grid visuals (flat
colored cells). Real bag icons drop in here later without code changes.

## Integration notes
1. Drop file into this folder
2. Open the matching BagData asset in Inspector (`Assets/_Project/ScriptableObjects/Bags/<bag_id>.asset`)
3. Drag the new sprite into the `Sprite` field
4. Save

## GDD reference
§23 (Asset Budget)
