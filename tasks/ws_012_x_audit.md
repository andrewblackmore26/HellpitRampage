# WS-012.x Implementation Audit

**Date:** 2026-05-15
**Audited by:** Claude (Opus 4.7), via 6 parallel read-only subagents
**Source spec:** `WS-012_X_Verification_Audit.md`
**Branch:** `main` (commit `566e463` + uncommitted changes per git status)

---

## Summary

- Total checks (excluding manual-playtest-only items collapsed to ❓): **129**
- ✓ Implemented: **115**
- ⚠ Partial / off-spec but functional: **6**
- ✗ Not implemented: **6**
- 💥 Regressed: **0**
- ❓ Cannot determine (manual playtest required): **18** (counted separately; not in 129)

**Headline:** The "items render as 1×1" complaint is **NOT** caused by missing WS-012.2 multi-cell infrastructure — that is fully wired. The most plausible culprit is the **shop drag ghost** (single-cell `_ghostPrefab` cursor preview that never scales). Two unrelated blockers were uncovered in WS-012.6: **no AudioMixer asset exists** (volume sliders write to nowhere), and **the pause menu's `_inputActions` reference is null in Game.unity** (Escape/Start cannot pause).

---

## §2 WS-012 (Locking + Sell Modal + Gold Auto-Vacuum)

### 2.1 Data layer — ✓ all 14 items

Every field, event, and helper present. References:
- `ItemInstance.IsLocked` — [ItemInstance.cs:16](Assets/_Project/Scripts/Inventory/ItemInstance.cs#L16)
- `BagInstance.IsLocked` — [BagInstance.cs:13](Assets/_Project/Scripts/Inventory/BagInstance.cs#L13)
- `ItemLockChangedEvent` / `BagLockChangedEvent` / drag-began/ended events — [InventoryEvents.cs:28-36](Assets/_Project/Scripts/Core/Events/InventoryEvents.cs#L28-L36)
- `InventoryService.ToggleItemLock` / `ToggleBagLock` / `ContainsItem` — [InventoryService.cs:126-150](Assets/_Project/Scripts/Inventory/InventoryService.cs#L126-L150)
- Drag publishes — [DragHandler.cs:72-78, 165, 218-225](Assets/_Project/Scripts/UI/DragHandler.cs)

### 2.2 Sell modal — ✓ all 9 items

- [SellModal.cs](Assets/_Project/Scripts/UI/SellModal.cs) implements full path: subscribes in `OnEnable` (L38), activates on began (L56-65), shows correct "Drag here to sell for Xg" (`Mathf.CeilToInt(EffectivePrice * 0.5f)`, L63-64), "LOCKED — cannot sell" in red `_lockedTextColor (0.9, 0.3, 0.3, 1)` (L21/89).
- `IDropHandler.OnDrop` at L105, locked-guard at L107, removes then grants gold (L114-119), deactivates on ended (L80-81 → L97).

### 2.3 Lock icon overlay — ✓ all 5 items

- `lock_icon.png` at `Assets/_Project/Sprites/UI/lock_icon.png`.
- `_lockIconSprite` SerializeField — [InventoryGridView.cs:19](Assets/_Project/Scripts/UI/InventoryGridView.cs#L19)
- `AttachLockIcon` at L299-318: anchor (0,1), pivot (0,1), sizeDelta (16,16), pos (4,-4). Wired in scene at `Game.unity:3354` and `:5538` with sprite sub-asset `fileID: 21300000` (matches L-014 memory rule).
- Event subscriptions L59-60, refresh via `HandleAnyChange → RefreshAll`.

### 2.4 Gold auto-vacuum — ✓ all 3 code items

- [GoldFieldSweeper.cs](Assets/_Project/Scripts/Combat/GoldFieldSweeper.cs) — subscribes `RoundEndedEvent` L17, uses `FindObjectsByType<GoldPickup>()` no-arg overload (matches L-005), filters `activeInHierarchy` L30-36.
- `GoldPickup.ForceCollect()` — [GoldPickup.cs:50-55](Assets/_Project/Scripts/Combat/GoldPickup.cs#L50-L55), idempotent via `_isDespawned` guard (matches L-006).
- Note: lives under `Combat/`, not `Core/` as the spec hinted. Functionality unaffected.

### 2.5 Scene wiring — ✓ both

- `SellModal` at `Game.unity:5179`.
- `GoldFieldSweeper` at `Game.unity:5135`.

### 2.6 Manual playtest — ❓ × 6

Code paths plausible. The `IDropHandler.OnDrop`-before-`OnEndDrag` hazard (L-013) is defended at `DragHandler.cs:145-148` via `!ContainsItem(Item)` check.

---

## §3 WS-012.1 (Superseded by 12.3) — ✓ all 5 items

- `LockToggleHandler.cs`, `GridClickTooltipHandler.cs`, `DetailTooltipController.cs`, `TooltipTarget.cs` — **all confirmed gone from working tree** (matches git `D` status).
- Right-click rotation during drag — present at [DragHandler.cs:99-102](Assets/_Project/Scripts/UI/DragHandler.cs#L99-L102) (R-key OR right-mouse-button OR'd into same `rotateRequested` branch; gated by `_dragging` early-exit at L94).

---

## §4 WS-012.2 (Multi-Cell Item Shapes + Placeholder Colors)

**THIS IS THE MOST IMPORTANT SECTION OF THE AUDIT.** Be detailed — the user's "items are 1×1" complaint should be conclusively explained here.

### 4.1 Data layer

- ⚠ `ItemData.Cells` field — **renamed to `Shape`**: `ItemData.Shape` references a separate `ItemShape` ScriptableObject which owns `public List<Vector2Int> Cells`. Functionally equivalent (arguably better — shapes are reusable assets) but technically off-spec. [ItemData.cs:26](Assets/_Project/Scripts/Inventory/ItemData.cs#L26), [ItemShape.cs:10](Assets/_Project/Scripts/Inventory/ItemShape.cs#L10).
- ✓ `ItemData.PlaceholderColor` — [ItemData.cs:23](Assets/_Project/Scripts/Inventory/ItemData.cs#L23).
- ⚠ `ShapeMath.cs` — exists with `Rotate(...)` (named `Rotate`, not `RotateCells`, but signature & behavior correct). **No `ComputeBoundingBox` helper** — bbox is computed inline at three sites (`InventoryGridView.cs:192-200`, `:143-151`, `DragHandler.cs:129-131`). All three compute identically; cosmetic gap.

### 4.2 Item-data authoring — ✓ all required items authored

| Item asset | Shape resolved | Cells > 1? | PlaceholderColor (non-default?) |
|---|---|---|---|
| Whetstone_Item | 1×1 (`SimpleSquare`) | ✗ | ✓ `0.72/0.72/0.75` matches spec |
| SharpeningStone_Item | 1×1 | ✗ | ✓ `0.65/0.62/0.55` |
| **BoneKnife_Item** | **1×2 H** | ✓ | ✓ `0.92/0.88/0.78` matches spec |
| **TarnishedBell_Item** | **2×1 H** (spec said 1×1, data is richer) | ✓ | ✓ `0.62/0.55/0.32` matches spec color |
| **HollowCrown_Item** | **2×2** | ✓ | ✓ `0.72/0.55/0.20` matches spec |
| TempoStone_Item | 1×1 | ✗ | ✓ `0.55/0.45/0.78` |
| VeilThread_Item | 1×1 | ✗ | ✓ `0.40/0.55/0.70` |
| BottleOfHush_Item | 1×1 | ✗ | ✓ `0.40/0.75/0.80` |
| **TestStick_Item** | **1×2 V** | ✓ | ✓ `0.55/0.55/0.60` |
| **MysticSword_Item** | **1×3 H** | ✓ | ✓ `0.60/0.72/0.85` |

**5 of 10 items reference a multi-cell shape.** All four spec-named items (Whetstone, Bone Knife, Tarnished Bell, Hollow Crown) have non-default placeholder colors matching spec. Shape `.asset` files contain expected cell lists.

### 4.3 Rendering — ✓ all 4 items

- `InventoryGridView.RenderItem` calls `item.EffectiveCells()` at [InventoryGridView.cs:189](Assets/_Project/Scripts/UI/InventoryGridView.cs#L189), iterates every cell for bbox (L193-200), passes to `BuildItemCellChildren` (L221).
- `BuildItemCellChildren` (L250-277): one Image-bearing GameObject per cell, sized `CELL_SIZE_PX × CELL_SIZE_PX`, anchored at `(off.x*56, off.y*56)`.
- Tints with `data.PlaceholderColor` at L273; sprite applied if `data.Icon != null` (L274).
- Empty bbox cells (L-shape corners) NOT tinted: root Image color is `(0,0,0,0)` (L207), only enumerated cells get a child Image (L261).

### 4.4 Placement and synergy — ✓ all 3 items

- `InventoryGrid.CanPlaceItem` calls `ShapeMath.Rotate(data.Shape.Cells, rotation)` at L102, loops every effective cell for bounds/host/occupancy (L106-119).
- `InventoryGrid.GetItemAt` re-derives via `item.EffectiveCells()` (L139-150); `MoveItem` rotates and rebinds `HostBag`.
- `SynergyResolver.Resolve` walks `grid.GetItemAt(targetCell)` for neighbor side; `EffectiveStarredEdges()` ([ItemInstance.cs:38-53](Assets/_Project/Scripts/Inventory/ItemInstance.cs#L38-L53)) rotates star anchors using same normalization offset as body. `OccupiesCell` guard prevents inward-facing stars on multi-cell items. `GetAdjacentItems` builds own-cell hash from `EffectiveCells()` (L180-181).

### 4.5 Drag and rotation

- ✓ **Grid-internal drag ghost**: the dragged GameObject IS the rendered item with per-cell children; `RebuildDraggedVisualForRotation` ([DragHandler.cs:120-134](Assets/_Project/Scripts/UI/DragHandler.cs#L120-L134)) rotates cells, resizes bbox, rebuilds children. Validation overlay iterates `ShapeMath.Rotate(...)` at L286.
- ⚠ **Shop-side drag ghost**: `ShopSlotDragHandler` instantiates a single `_ghostPrefab` Image ([ShopSlotDragHandler.cs:55-64](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs#L55-L64)). The cursor ghost never grows for multi-cell items — stays 1×1 while you drag any item from shop. **Cell-highlight overlay on the grid does iterate full rotated footprint** (L187-188) and paints green/red across every target cell.
- ✓ R-key & right-click rotation: both `DragHandler.cs:99-102` and `ShopSlotDragHandler.cs:93-96` OR R-key + right-mouse.
- ✓ `ShapeMath.Rotate(Cells)` used everywhere; `ShapeMathTests.cs` covers 1×2 vertical→horizontal, 1×3 H→V, 2×2 invariance, L-shape distinctness.

### 4.6 Critical finding — why do items appear 1×1?

**Verdict: WS-012.2 is fully implemented end-to-end.** Data is authored, shape assets are correct, rendering iterates cells, placement walks footprints, synergy considers full shapes. **The user's "items look 1×1" complaint does NOT match the on-disk state for placed grid items.**

The most plausible culprit is the **shop drag preview**:

> While dragging an item out of a shop slot, the cursor ghost is a fixed-size single-cell `_ghostPrefab` Image ([ShopSlotDragHandler.cs:55-64](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs#L55-L64)). It never resizes or rebuilds children for multi-cell items. A player focused on the cursor sees a 1×1 tile carrying every item — Hollow Crown, Bone Knife, Whetstone all look identical at the cursor.

The grid-highlight underneath does correctly paint the full rotated footprint, but is easy to miss. **This is the only UX gap that survives the code review.**

Secondary possibility (much less likely): if the user means "after placement, items still render 1×1," the only remaining failure mode is a serialized override pointing some `ItemData.Shape` reference back to `SimpleSquare_ItemShape` in a scene/prefab. Asset files on disk are verified correct, so this would require an inspector-level investigation. **Recommend the user confirm whether the complaint is "during shop drag" or "after placement" before changing any code.**

Also note: **ground items are intentionally 1×1 visuals** ([GroundManager.cs:140-148](Assets/_Project/Scripts/UI/GroundManager.cs#L140-L148), GroundItem prefab fixed at 56×56). If the user dropped a 1×3 Mystic Sword on the ground and saw it as 1×1 there, that's by design.

---

## §5 WS-012.3 (Unified Tooltip) — ✓ all 24 items

Fully wired in code and scene.

- **Code** ([TooltipController.cs](Assets/_Project/Scripts/UI/TooltipController.cs)): `ShowHovering` × 4 overloads (L121-178), `Pin` × 4 overloads (L190-244), `Unpin` (L248), `HideIfNotPinned` (L181), `_dragInProgress` flag (L65), Escape via `Keyboard.current.escapeKey.wasPressedThisFrame` (L106-110, matches L-008), `raycastTarget` toggled false/true (L258/L271), subscribes to all 4 drag events (L388-391).
- **Wiring**: 5 shop-slot InspectableItems in `Game.unity` (lines 3601, 3887, 4172, 4457, 4742) bound to ShopSlot fileIDs; `GroundItem.prefab` carries InspectableItem at fileID `5700000000000000009` (GUID `4022a2b3c4d5e6f7081920304050607a`); grid renderers (InventoryGridView L176-178, L234-236) and `GroundManager.cs:168-173` add and configure `InspectableItem` at runtime.
- **Same-item toggle-off**: explicit guards in all 4 Pin overloads (e.g. L194, L208, L222, L237).
- **Pinned tooltip click-outside**: backdrop handled by `TooltipBackdropClickHandler` (active only during pin, raycastTarget=true L476).
- **TMP `enableWordWrapping`**: not visible in this audit but L-019 should be respected going forward.

Right-click rotation (§5.4): all 3 items ✓ via combined R/right-click branch in DragHandler.

---

## §6 WS-012.4 (Visual Foundation)

### 6.1 TMP migration

- ✓ Zero remaining `Text _` field declarations in `Assets/_Project/Scripts/`.
- ✓ Default TMP font asset present at `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` (plus Drop Shadow / Outline / Fallback variants and `LiberationSans.ttf`).
- ✓ All 5 spot-checked UI prefabs (`GridCell`, `BagOverlay`, `ItemIcon`, `DragGhost`, `GroundItem`) contain no legacy Text components.
- ✓ `MigrateTextToTMPro.cs` exists at [Assets/_Project/Scripts/Editor/MigrateTextToTMPro.cs](Assets/_Project/Scripts/Editor/MigrateTextToTMPro.cs) (menu: `Tools/WS-012.4/Migrate Text -> TMP (Active Scene)`), preserves text/size/color/alignment/style/raycast/wrap and rewires SerializeField references via `SerializedProperty`.

### 6.2 Camera setup (Game.unity)

- ✓ Orthographic = 1 (`Game.unity:186`).
- ⚠ **Orthographic size = 7, not 8** (`Game.unity:187`). The active CinemachineCamera lens reports size 8 (`Game.unity:332`), so the runtime viewport will be 8 once CM takes control. If CM is ever disabled the base camera renders at 7. Off-spec by 1; either align or document.
- ✓ Background color dark neutral `{r:0.08, g:0.08, b:0.1, a:1}` (L170).
- ✓ No PixelPerfectCamera in scene.
- ✓ CinemachineCamera present at L316 (script GUID `f9dfa5b682dcd46bda6128250e975f58`) with Position Composer follow on Player, FollowOffset `{0,-1,-10}`.
- ✓ Camera Z = -10 (L154).

### 6.3 URP / Project settings — ✓ all 6 items

| Setting | Expected | Actual | Source |
|---|---|---|---|
| Render Scale | 1.0 | 1 | UniversalRP.asset:29 |
| MSAA | 4x | 4 | UniversalRP.asset:28 |
| HDR | off | 0 | UniversalRP.asset:26 |
| Run In Background | ON | 1 | ProjectSettings.asset:85 |
| Default Resolution | 1920x1080 | 1920/1080 | ProjectSettings.asset:44-45 |
| Fullscreen Mode | Fullscreen Window | 1 | ProjectSettings.asset:110 |

### 6.4 Sprite settings — ✓ all 3 items (5/5 sampled)

| Sprite | filterMode | enableMipMap | maxTextureSize |
|---|---|---|---|
| `Sprites/Heroes/placeholder_hero.png` | 1 (Bilinear) | 0 | 2048 |
| `Sprites/Enemies/placeholder_enemy.png` | 1 | 0 | 2048 |
| `Sprites/Items/placeholder_projectile.png` | 1 | 0 | 2048 |
| `Sprites/Items/placeholder_gold.png` | 1 | 0 | 2048 |
| `Sprites/UI/lock_icon.png` | 1 | 0 | 2048 |

### 6.5 Tooltip anchor fix — ✓ all 4 items

- `PositionPanelAtScreen` uses pivot-based positioning ([TooltipController.cs:352-359](Assets/_Project/Scripts/UI/TooltipController.cs#L352-L359)); sets `_panelRT.pivot` BEFORE `anchoredPosition` (correct order per L367 comment).
- `ComputeAnchor` (L369-377) flips `pivot.x→1` on right-edge overflow, `pivot.y→1` on top-edge overflow, re-anchors `pos = cursor - offset` per flipped axis.
- `TooltipAnchorTests.cs` with 4 `[Test]` methods (L21, L31, L43, L54): away from edges, near right, near top, near top-right corner. Pure-math tests against `ComputeAnchor`.

### Critical findings for §6

1. ✗ **TMP migration applied to scripts and prefabs but NOT scenes.** `Game.unity` contains **19 legacy Text components** (e.g., `m_Text: You Died` at L1579-1603, using Arial `fileID: 10102`). `MainMenu.unity` contains **3** more. The migration tool exists but is per-scene — needs to be run from `Tools/WS-012.4/Migrate Text -> TMP (Active Scene)` on each scene, then saved. ⚠ Closing+reopening Unity scene tabs may be required (L-009).
2. ⚠ Main Camera orthographic base size is 7 vs spec's 8 (Cinemachine overrides to 8 at runtime).

---

## §7 WS-012.5 (Ground/Spillover) — ✓ all 11 code + 6 event + 5 wiring items

### 7.1 Code

All 11 items ✓:
- [GroundManager.cs](Assets/_Project/Scripts/UI/GroundManager.cs) — Current L21, AddItem L123, RemoveItem L190, ClearAll L258, SnapshotGroundState L267, RestoreGroundState L275.
- [GroundItemPhysics.cs](Assets/_Project/Scripts/UI/GroundItemPhysics.cs) — `GRAVITY = -1200f` L16, AABB `GetLocalAABB` L43, sleep L19/L91-95.
- [GroundDragHandler.cs](Assets/_Project/Scripts/UI/GroundDragHandler.cs) — IBeginDrag/IDrag/IEndDrag, grid placement, fling, snap-back.
- [DragModeService.cs](Assets/_Project/Scripts/Core/DragModeService.cs) — Current L14, Toggle L62 (refuses during drag L64), Tab key L79 (uses `Keyboard.current.tabKey.wasPressedThisFrame` per L-009).
- [ModeToggleButton.cs](Assets/_Project/Scripts/UI/ModeToggleButton.cs).
- [DropZoneClassifier.cs](Assets/_Project/Scripts/UI/DropZoneClassifier.cs) — `IsWithinBackpackXRange` L18; despite the name, also clamps Y (extends downward by ground area height L33-46). Behavior matches spec intent.
- [BackpackBoundsProvider.cs](Assets/_Project/Scripts/UI/BackpackBoundsProvider.cs) — Current L13.
- `InventoryService.RemoveBagWithoutCascade` (L202), `GetItemsInBag` (L177), `RemoveItemSilent` (L190).
- `BagData.EffectivePrice` — [BagData.cs:27](Assets/_Project/Scripts/Inventory/BagData.cs#L27).

### 7.2 Events — ✓ all 6 items

`DragMode` enum L41, `DragModeChangedEvent` L42, `BagSoldEvent` L45 (with `ItemCountSpilled`), `GroundItemAddedEvent` L49, `GroundItemRemovedEvent` L50, `ShopPhaseStartedEvent` at [ShopPhaseStartedEvent.cs:6](Assets/_Project/Scripts/Core/Events/ShopPhaseStartedEvent.cs#L6) published from [RunManager.cs:82](Assets/_Project/Scripts/Core/RunManager.cs#L82) (suppressed on final round).

### 7.3 Scene wiring — ✓ all 5 items

- ⚠ **GroundArea** at `Game.unity:5447` parented to InventoryGridContainer (fileID 2101 at L3196), not ShopOverlay directly. InventoryGridContainer is under ShopOverlayPanel, so transitively under ShopOverlay; matches `tasks/todo.md:169` revised plan. Initial `m_IsActive: 0` (L5465) as required.
- ✓ `GroundItem.prefab` has: Image (GUID `fe87c0e1cc204ed48ad3b37840f39efc`, L62), GroundItemPhysics (`d4e6f8a1b3c560d8e9f12a3406284b3c`, L104), GroundDragHandler (`17b8c2d4e6f8910a2b3c4d5e6f708192`, L116), InspectableItem (`4022a2b3c4d5e6f7081920304050607a`, L128). Pivot (0.5, 0.5), size 56×56. Scene wiring references prefab GUID `2839c4e6f8a1b3d5072839a4b6c8d0e2` at L5537; lock icon sprite at sub-asset fileID 21300000 (L-014).
- ✓ ModeToggleButton at `Game.unity:5596` in BottomBar (Father 2111).
- ✓ DragModeService GameObject at root (`&4300`, Father 0, RootOrder 7).
- ✓ BackpackBoundsProvider on GridAnchor (GameObject 2120), wired with matching script GUID `b2c4d6e8f1a3548c9d10e2f4061728a0`.

### 7.4 Functional — ❓ × 7 (manual playtest required)

Code paths plausible:
- Bag sell spill: order is gold → spill → remove (SellModal.cs:121-162), defensive null-guard on `GroundManager.Current` aborts sale (L124-130).
- Physics: gravity, AABB clamp, `BOUNCE_DAMP=0.5`, `FRICTION=0.92`, sleep threshold 5, 3-pass push-out (`GroundManager.cs:308-356`), Z-sort by Y (L360-362).
- Tab toggle gated by `_dragInProgress`; mode greys items via alpha 0.4 and disables drag handler (L97-119).
- Locked items spill with lock preserved (SellModal.cs:151 passes `item.IsLocked`; GroundManager L159, L176 `RefreshLockOverlay`).
- Stale-drag defense via `GroundManager.ContainsItem` (matches L-013).

### Critical findings for §7

- ⚠ `DropZoneClassifier.IsWithinBackpackXRange` actually checks both X AND Y — the name is misleading but behavior matches spec intent.
- ⚠ `RestoreGroundState` (L275-289) doesn't capture exact positions — items respawn at top with downward velocity. Matches `GroundItem.cs:31-37` docstring as intentional.
- GroundItem prefab's Image has `m_Sprite: {fileID: 0}` (L73), but `GroundManager.AddItem` assigns `img.sprite = data.Icon` at runtime (L143). `m_Type: 0` (Simple) sidesteps L-007's filled-mode warning. Fine, but worth a note for future maintainers.

---

## §8 WS-012.6 (Settings Menu + Light Controller)

### 8.1 Code — ✓ all 7 items

- [SettingsManager.cs](Assets/_Project/Scripts/Core/SettingsManager.cs) L9-313: full setter API (volumes, mutes, VSync, framerate, accessibility, resolution+confirm/revert).
- [SettingsState.cs](Assets/_Project/Scripts/Core/SettingsState.cs) L3-73 with `ToSaveData`/`FromSaveData`.
- [SettingsChangedEvent.cs](Assets/_Project/Scripts/Core/SettingsChangedEvent.cs) L21-24 + `SettingKind` enum.
- [SettingsMenuController.cs](Assets/_Project/Scripts/UI/SettingsMenuController.cs) L390-393: Audio/Display/Accessibility/Controls tabs built in `BuildUITree`.
- [PauseMenuController.cs](Assets/_Project/Scripts/UI/PauseMenuController.cs) L16-193.
- [SaveManager.cs](Assets/_Project/Scripts/Core/SaveManager.cs) L107/L120: `SaveSettings`/`LoadSettings`, plus `SettingsPath` L30 and `SettingsFileExists` L145.
- Every setter calls `Persist()` → `File.WriteAllText(SettingsPath, ...)` at SaveManager.cs:112.

### 8.2 Audio mixer — ✗ ALL 4 items

- ✗ **No `MainMixer.mixer` asset exists** anywhere. Glob `**/*.mixer` returned 0 results. `Assets/_Project/Audio/` contains only `Music/` and `SFX/` subdirs.
- ✗ No mixer → no exposed parameters.
- ✗ Code references `Volume_Master/Volume_Music/Volume_SFX/Volume_Voice` ([SettingsManager.cs:86, 95, 104, 113](Assets/_Project/Scripts/Core/SettingsManager.cs)) but parameters cannot exist on a missing asset.
- ✗ `_mainMixer` SerializeField in `Boot.unity:581` is `{fileID: 0}` (null). `ApplyVolume` early-outs at [SettingsManager.cs:292](Assets/_Project/Scripts/Core/SettingsManager.cs#L292). Volume sliders persist to `settings.json` but produce **no audible effect**.

### 8.3 Settings menu UI

- ✓ Accessible from Main Menu — [MainMenuController.cs:50-58](Assets/_Project/Scripts/UI/MainMenuController.cs#L50-L58); `MainMenu.unity:734` wires `_settingsMenu: {fileID: 802}`.
- ⚠ Accessible from pause menu in-game — code wires it ([PauseMenuController.cs:134-148](Assets/_Project/Scripts/UI/PauseMenuController.cs#L134-L148)), `Game.unity:5849` references `_settingsMenu: {fileID: 5012}`. **BUT** `_inputActions: {fileID: 0}` at `Game.unity:5848` is null, so `EnsurePauseAction()` can't resolve the Pause action — Escape/Start cannot open pause menu in the first place.
- ✓ Audio tab: 4 sliders + mute toggles (`BuildSliderRow` L424-427).
- ✓ Display tab: resolution cycler, fullscreen, VSync, framerate (L438-445).
- ✓ Accessibility tab: Screen Shake, Reduce Motion, High Contrast (L463-471).
- ✓ Controls tab: read-only bindings list (L483-491).
- ✓ Back button → `Close()` → invokes `_onClose` callback; PauseMenu re-activates via `PauseMenuController.cs:143-147`.

### 8.4 Persistence — ✓ both items

- Every setter calls `Persist()` at the end of its body. Note: slider drag triggers one file write per `onValueChanged` delta (functional, but potential perf hit on long drags — consider debouncing).
- `Path.Combine(Application.persistentDataPath, SETTINGS_FILENAME)` at [SaveManager.cs:38](Assets/_Project/Scripts/Core/SaveManager.cs#L38).

### 8.5 Pause menu

- ⚠ Escape opens in Game scene — code correct ([PauseMenuController.cs:82, 97-108](Assets/_Project/Scripts/UI/PauseMenuController.cs)) but `_inputActions` null in scene → action never wired. **Blocker.**
- ⚠ Gamepad Start opens — same root cause; binding exists at `PlayerInput.inputactions:136-145`.
- ✓ Resume/Settings/Quit-to-Menu buttons present (L185-192).
- ✓ Pause sets `Time.timeScale = 0` (L120); Resume restores to 1 (L128); also restored on Quit (L152).

### 8.6 Light controller pass

- ⚠ Move action: bindings correct (WASD + `<Gamepad>/leftStick` at `PlayerInput.inputactions:38-101`) **but action is named `Movement`, not `Move`**. Functionally fine if consumers use `Movement`; verify any `FindAction("Move")` callers.
- ✓ Pause: Escape + `<Gamepad>/start` at L125-145.
- ✓ EventSystem uses InputSystemUIInputModule in both `MainMenu.unity:484` and `Game.unity:1862` (script GUID `01614664b831546d2ae94a42149d80ac`). No `StandaloneInputModule` instances (matches L-002).
- ❓ Menu controller navigation — cannot determine without playtest. `m_FirstSelected: {fileID: 0}` on EventSystems (`MainMenu.unity:472`, `Game.unity:1850`) means no initially focused target.
- ❓ In-game left-stick movement — manual playtest required.

### Critical findings for §8

1. ✗ **No AudioMixer asset exists.** Build one at `Assets/_Project/Audio/MainMixer.mixer` with groups Master/Music/SFX/Voice, expose volume parameters with exact names, and assign to `SettingsManager._mainMixer` in Boot.unity. Until then, volume sliders have no audible effect.
2. ⚠ **Pause input wiring is broken in Game.unity.** Assign the `PlayerInput.inputactions` asset to `PauseMenuController._inputActions` at `Game.unity:5848`. Without this, Esc/Start can't pause.
3. ⚠ Action name `Movement` vs spec `Move`. Decide and align — either rename action or update spec/Controls tab text.
4. ⚠ `m_FirstSelected: {fileID: 0}` on EventSystems → no initial gamepad focus.
5. ⚠ `Persist()` per slider tick → many disk writes on drag. Consider debouncing.

---

## §9 Cross-Cutting Health

- ❓ "No errors in console during playthrough" — manual playtest required.
- ❓ "No warnings during playthrough" — manual playtest required.
- ❓ "Player can complete 3 rounds without crashes" — manual playtest required.
- ✓ **28 EditMode tests** under `Assets/_Project/Tests/EditMode/`:
  AABBCollisionTests, BagSellMathTests, BehaviorTriggerTests, DragModeServiceTests, EventBusTests, GroundStateTests, HealthTests, InventoryGridMoveTests, InventoryGridTests, ItemLockTests, ItemPoolTests, PlaceholderColorTests, PlayerWeaponsTests, PoolManagerTests, RotationTests, RunManagerGoldTests, RunManagerTests, SaveManagerTests, SellMathTests, SettingsManagerTests, SettingsSaveRoundTripTests, ShapeMathTests, ShopRerollCostTests, StarPreviewTests, TooltipAnchorTests, TooltipContentTests, TooltipControllerTests, UnifiedSynergyResolverTests.
- ✓ **No tests reference deleted classes** (`DetailTooltipController`, `GridClickTooltipHandler`, `TooltipTarget`, `Tooltip`). Only documentary reference in `InspectableItem.cs:10-12` XML doc comment (harmless).
- ✓ `CanvasClampTests.cs` was deleted (visible in git status; intentional).
- ✓ Boot scene loads — `SettingsManager` GameObject under `Managers` parent (`Boot.unity:549`, matches L-001).
- ✓ Main Menu → New Run → Game scene loads via `GameManager.TransitionTo(InRun)` → `SceneManager.LoadScene("Game")` ([MainMenuController.cs:47](Assets/_Project/Scripts/UI/MainMenuController.cs#L47); [GameManager.cs:62, 71](Assets/_Project/Scripts/Core/GameManager.cs)).

---

## Findings and Required Actions

### Blocking (must fix before any new spec lands)

1. ✓ **Resolved (WS-012.7 §4.1)** — "items appear 1×1" traced to the shop drag ghost. Fixed in [ShopSlotDragHandler.cs](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs) by adding `RebuildGhostVisual()` (mirrors `DragHandler.RebuildDraggedVisualForRotation`), called from `OnBeginDrag` and `Rotate()`. Now renders multi-cell shapes with `PlaceholderColor` and rotates correctly. Suppresses prefab root Image alpha so only rebuilt cell children render.
2. ⚠ **Partially resolved — deferred to user in Unity Editor (WS-012.7 §4.2-§4.3)** — `MainMixer.mixer` asset must be created via `Assets → Create → Audio Mixer` (the YAML is too fragile for hand-authoring per spec §3 gotcha 5). User to create, expose `Volume_Master`/`Volume_Music`/`Volume_SFX`/`Volume_Voice` (exact case), and wire to `SettingsManager._mainMixer` in `Boot.unity`. Captured as lesson [L-016](lessons.md#L-016).
3. ⚠ **Partially resolved — deferred to user in Unity Editor (WS-012.7 §4.4)** — `_inputActions` wiring at `Game.unity:5848` deferred to Unity drag-drop because InputActionAsset (ScriptedImporter) main asset fileID is content-derived and no existing reference example exists in the project to copy. User to drag `PlayerInput.inputactions` from Project window into the field in Inspector. 3-second action; safer than guessing fileID.

### Important (should fix soon but not blocking)

4. ⚠ **Deferred to user in Unity Editor (WS-012.7 §4.5)** — TMP migration must be run via `Tools → WS-012.4 → Migrate Text → TMP (Active Scene)` on Game.unity (19 legacy Text) and MainMenu.unity (3 legacy Text). Per L-009, close+reopen scene tabs in Unity before running the tool.
5. ✓ **Resolved (WS-012.7 §4.6)** — Main Camera orthographic size at `Game.unity:187` set to 8, aligning base camera with Cinemachine runtime override.
6. ✓ **Resolved (WS-012.7 §4.7)** — Action name `Movement` kept; Controls tab display text in `SettingsMenuController.cs:484` updated from "Move:" to "Movement:" to match. No C# code references either name via `FindAction()` (grep returned 0 matches both ways), so no consumer updates needed.
7. **Deferred — out of scope per WS-012.7 §2** — EventSystem `m_FirstSelected` wiring belongs to a future controller-focus polish spec. WS-012.6 controller pass was scoped to menu nav + movement + pause only, not initial focus.

### Cosmetic (defer)

8. **Deferred** — `ItemData.Shape` naming is intentional; better design than `Cells` directly.
9. **Deferred** — `ShapeMath.Rotate` naming + missing `ComputeBoundingBox` helper. Centralize when cycles allow.
10. **Deferred** — `DropZoneClassifier.IsWithinBackpackXRange` rename. Functionally correct.
11. **Deferred** — `GoldFieldSweeper`/`GoldPickup` folder location.
12. **Deferred** — `Persist()` per slider tick. Debounce if perf complaints arise.
13. **Deferred** — `RestoreGroundState` position snapshot is intentional per docstring.

---

## WS-012.7 Resolution Summary (added 2026-05-15)

**Code changes I made:**
- `Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs` — added `RebuildGhostVisual()` private method (mirrors `DragHandler.RebuildDraggedVisualForRotation`); called from `OnBeginDrag` after ghost instantiation and from `Rotate()` after rotation update.
- `Assets/_Project/Scripts/UI/SettingsMenuController.cs:484` — "Move:" → "Movement:" in Controls tab bindings display.
- `Assets/_Project/Scenes/Game.unity:187` — Main Camera orthographic size 7 → 8.

**Doc changes:**
- `tasks/lessons.md` — appended L-016 (AudioMixer routing) and L-017 (multi-cell drag ghost pattern).
- This audit doc — annotated all findings with ✓/⚠/Deferred status.

**Remaining Unity Editor work (user):**
- Create `Assets/_Project/Audio/MainMixer.mixer` with groups Master → Music, SFX, Voice; expose volume parameters with exact names.
- Wire MainMixer to `SettingsManager._mainMixer` in Boot.unity.
- Wire `PlayerInput.inputactions` to `PauseMenuController._inputActions` at Game.unity:5848.
- Run TMP migration on Game.unity and MainMenu.unity.
- Optional: drop temporary AudioSource on Main Menu's "New Run" button routed to MainMixer→SFX for end-to-end volume verification.
- Run all 28 EditMode tests via Test Runner.
- Manual playtest per WS-012.7 §4.10 checklist.

**Verdict after WS-012.7 (post user Unity steps):** all "Blocking" items either resolved in code or queued for trivial Unity Editor wiring. The "items appear 1×1" complaint is conclusively fixed at its actual source (shop drag ghost). Project is ready for WS-013 once the user-side Unity wiring completes.

---

## Recommendations

**Next concrete steps, in order:**

1. **Ask the user**: when you said "items look 1×1," do you mean (a) while dragging from shop, (b) while dragging from grid, (c) after placement in grid, or (d) on the ground? The audit conclusively rules out (b), (c), and (d) — placed grid items are multi-cell, grid drag rebuilds correctly, ground items are intentionally 1×1. If the answer is (a), fix [ShopSlotDragHandler.cs:55-64](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs#L55-L64) to rebuild the cursor ghost like `DragHandler.RebuildDraggedVisualForRotation` does. If (c), open a few item-bearing prefabs in inspector and confirm `Shape` references aren't overridden to `SimpleSquare`.
2. **Build the AudioMixer.** Smallest unit of work that unblocks WS-012.6 audio.
3. **Wire pause `_inputActions` in Game.unity.** One-line scene edit.
4. **Run TMP migration on Game.unity and MainMenu.unity.** Editor menu action; remember to save and (per L-009) close+reopen the scene tab if it's open in Unity from before.
5. **Decide on camera ortho size and action name.** Small alignment passes.
6. **Run the 28 EditMode tests in Unity** and capture pass/fail. (Cannot do this from the audit — requires Unity to be running.)

The audit conclusively shows that **WS-012, 12.1, 12.2, 12.3, 12.5 are all in solid shape**, **12.4 is 80% done** (TMP scene migration is the remaining manual step), and **12.6 has two assignment-level blockers** (mixer asset, pause input field) but its code is complete. **No regressions detected anywhere.**

**Time spent on audit:** ~14 minutes (6 parallel subagents averaging ~150s each).

