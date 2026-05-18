# Active task: WS-012.5 — Ground/Spillover System + Bag Selling + Mode Toggle

Spec: `WS-012_5_Ground_Spillover.md` (provided inline by designer 2026-05-15).

## Pre-flight (verified by general-purpose subagent against current state)

The spec's pre-flight assumptions vs reality:

| Spec assumption | Reality | Action |
|---|---|---|
| `LockToggleHandler` exists from WS-012 | Replaced by `GridClickTooltipHandler` + `DetailTooltipController` in WS-012.1 | Ground items use `GridClickTooltipHandler` (matches grid pattern per user confirmation) |
| `ShopPhaseStartedEvent` likely exists | **Missing** | Create + wire from `RunManager.EndCurrentRound()` |
| `BagData.EffectivePrice` may be missing | **Already exists** ([BagData.cs:27](Assets/_Project/Scripts/Inventory/BagData.cs#L27)) | Skip §4.2 |
| Test baseline ~87 | **Actually 112** across 21 files | Target: 112 + 15 = **127** passing |
| Scene name "ShopOverlay" | Actual: `ShopOverlayPanel` (&1800/&1801) | Use actual name |
| Scene name "BackpackGrid" | Actual: `GridAnchor` (&2120/&2121) | Attach `BackpackBoundsProvider` here |
| `InventoryService._items / _bags` | Lives on `InventoryGrid`: `Grid.Items / Grid.Bags` | Adapt helpers in §4.3 |
| `GridCoordinateHelper` exists | Missing; math duplicated in DragHandler/ShopSlotDragHandler | Skip extraction (not load-bearing for this spec) |
| Round-end gold vacuum lives in RunManager | Actually in `GoldFieldSweeper.cs` (already wired) | No change needed |

L-014 (code-built UI in Awake only, never OnEnable) applies directly to GroundManager — physics children must be Instantiated in Awake or on first add, not on every OnEnable.

L-007 (OnEnable hot-reload safety) applies to DragModeService and ModeToggleButton — they hold non-serialized state.

L-012 (IDropHandler.OnDrop fires before OnEndDrag) — already handled by existing `_dropCommitted` + `ContainsItem` defensive check; bag-selling will use the same `RemoveBagWithoutCascade` → ground-spill ordering so bags aren't removed before their items spawn.

## Architectural decisions (confirmed with designer)

1. **Ground item locking**: use `GridClickTooltipHandler` on ground items so left-click opens the same `DetailTooltipController` popup (with the lock icon action). Right-click does NOT lock. Consistent with WS-012.1's grid UX.
2. **Single atomic commit**: all phases ship in one commit `[WS-012.5] Ground/spillover system + bag selling + mode toggle`.

## Plan (ordered for compile-safe progression)

### Phase 1 — Events + data model
- [ ] `Core/Events/InventoryEvents.cs` — append `DragMode` enum, `DragModeChangedEvent`, `BagSoldEvent`, `GroundItemAddedEvent`, `GroundItemRemovedEvent`
- [ ] `Core/Events/ShopPhaseStartedEvent.cs` — new file, `public struct ShopPhaseStartedEvent : IGameEvent { public int RoundNumber; }`
- [ ] `Core/RunManager.cs` — publish `ShopPhaseStartedEvent` in `EndCurrentRound()` (just before / after `RoundEndedEvent`) when transitioning to Shop phase

### Phase 2 — InventoryService helpers
- [ ] Add `RemoveBagWithoutCascade(BagInstance)` — does NOT remove items inside; caller is responsible
- [ ] Add `GetItemsInBag(BagInstance) → List<ItemInstance>` — using `item.HostBag == bag`
- [ ] Add `RemoveItemSilent(ItemInstance)` — no `ItemRemovedEvent` published (ground-spawn path publishes its own event)

### Phase 3 — DragModeService + ModeToggleButton
- [ ] `Core/DragModeService.cs` — scene-scoped, `Current` static, `Toggle()`, `SetMode(mode)`, `CurrentMode`, refuses toggle during active drag, Tab key handler in Update, subscribes to ShopPhaseStartedEvent + 4 drag events
- [ ] `UI/ModeToggleButton.cs` — Button + Text label, click → `DragModeService.Current.Toggle()`, refreshes on `DragModeChangedEvent`

### Phase 4 — Drop-zone helpers
- [ ] `UI/BackpackBoundsProvider.cs` — `Current` static RectTransform accessor, attached to GridAnchor
- [ ] `UI/DropZoneClassifier.cs` — static `IsWithinBackpackXRange(Vector2 screenPos)` covers grid X-range + ground Y-range. Uses `BackpackBoundsProvider.Current` + `GroundManager.Current.GroundAreaRT`

### Phase 5 — Ground system core
- [ ] `UI/GroundItem.cs` — POCO wrapper class (`ItemData Data`, `Rotation Rotation`, `bool IsLocked`, `GameObject Visual`, `ItemInstance Instance`)
- [ ] `UI/GroundItemSnapshot.cs` — serializable struct for future save support
- [ ] `UI/GroundItemPhysics.cs` — RectTransform-bound physics: gravity, AABB wall collision, bounce dampening, friction, sleep. Item-vs-item collision resolved at GroundManager level (3-pass cap)
- [ ] `UI/GroundDragHandler.cs` — drag/pickup/grid-snap/sell-modal/fling-release/snap-back semantics. Publishes `ItemDragBeganEvent`/`ItemDragEndedEvent` so SellModal reacts uniformly
- [ ] `UI/GroundManager.cs` — scene singleton (per-scene `Current`). Tracks ground items list. AddItem/RemoveItem/ClearAll. SnapshotGroundState / RestoreGroundState (forward hook for WS-013). FixedUpdate: AABB item-vs-item resolution + Z-order sort. IDropHandler stub. Subscribes to ShopPhaseStartedEvent → activate; RoundStartedEvent → deactivate; RunStartedEvent → ClearAll; DragModeChangedEvent → grey-out

### Phase 6 — SellModal handles bag drags
- [ ] Subscribe to `BagDragBeganEvent` / `BagDragEndedEvent`
- [ ] On bag drag began: compute `ceil(bag.Data.EffectivePrice * 0.5f)`; show text "Drag here to sell for Xg (spills contents)" or "LOCKED — cannot sell" for locked bag
- [ ] `OnDrop` branches: item path (existing) vs bag path (new `SellBag`)
- [ ] `SellBag`: capture items list FIRST → AddGold → spawn each on ground via `GroundManager.Current.AddItem` with scatter velocity → `RemoveItemSilent` each → `RemoveBagWithoutCascade(bag)` → publish `BagSoldEvent`

### Phase 7 — ShopSlotDragHandler ground deposit
- [ ] Modify `OnEndDrag` — after `IsDropValid()` check fails:
  - If `DropZoneClassifier.IsWithinBackpackXRange(eventData.position)` AND `_draggedOffer is ItemData` AND `RunManager.Instance.CurrentGold >= price` → spend gold, spawn on ground, mark slot sold
  - Else (existing behavior): drag cancelled, no purchase
- [ ] Bag drops in backpack range invalid cell → cancel (bags don't go to ground)

### Phase 8 — DragHandler grid items → ground
- [ ] Modify `OnEndDrag` — after `TryCommitDrop()` returns false:
  - If `Kind == Item` AND `Item != null` AND `DropZoneClassifier.IsWithinBackpackXRange(eventData.position)` AND `GroundManager.Current != null` → `RemoveItemSilent(Item)` → `GroundManager.AddItem(...)` → mark committed
  - Else (existing): `ReturnToOriginal()`
- [ ] Bag path unchanged (no ground deposit for bags)

### Phase 9 — Mode-based grey-out
- [ ] `InventoryGridView` — subscribe to `DragModeChangedEvent`, expose `_itemAlpha` / `_bagAlpha` fields, apply in `RenderItem`/`RenderBag`, toggle `DragHandler.enabled` for non-active layer; trigger `RefreshAll()` on mode change
- [ ] `GroundManager` — subscribe to `DragModeChangedEvent`, set ground item alpha + disable `GroundDragHandler.enabled` in Bags mode

### Phase 10 — Scene wiring + GroundItem prefab
- [ ] Create `Assets/_Project/Prefabs/UI/GroundItem.prefab` (hand-authored, large fileIDs per L-003):
  - RectTransform 56×56
  - Image (placeholder, raycastTarget=true)
  - GroundItemPhysics
  - GroundDragHandler
  - GridClickTooltipHandler (for left-click → detail tooltip → lock)
  - TooltipTarget (hover)
- [ ] `Game.unity` additions:
  - `GroundArea` GameObject (new fileIDs 4200/4201) under ShopOverlayPanel (&1800/&1801) as sibling of InventoryGridContainer & BottomBar & SellModal; Image (0,0,0,0.5), RectTransform 504×120 below grid, raycastTarget=true, `m_IsActive: 0` initially (activated on ShopPhaseStartedEvent)
  - `GroundManager` MonoBehaviour on GroundArea, with `_groundAreaRT` wired to its own RT and `_groundItemPrefab` wired to the new prefab
  - `BackpackBoundsProvider` MonoBehaviour on GridAnchor (&2120)
  - `DragModeService` GameObject (new fileID 4300) sibling of EventSystem at scene root, MonoBehaviour attached
  - `ModeToggleButton` GameObject under BottomBar (&2110) sibling of existing Start Next Round button; UI.Button + Text child labeled "Mode: Items"

### Phase 11 — Tests
- [ ] `Tests/EditMode/BagSellMathTests.cs` (3 tests) — mirror SellMathTests pattern; verify `ceil(bag.EffectivePrice * 0.5f)` for 1g/3g/10g
- [ ] `Tests/EditMode/DragModeServiceTests.cs` (4 tests) — default Items / Toggle switches / publishes DragModeChangedEvent / ShopPhaseStartedEvent resets to Items
- [ ] `Tests/EditMode/GroundStateTests.cs` (5 tests) — snapshot/restore via pure data layer (use a stub GroundManager wrapper if MonoBehaviour required, OR just test `List<GroundItemSnapshot>` operations directly per spec §6 "ground tests need either PlayMode or pure-data refactor"). Will test via the snapshot list directly to avoid MonoBehaviour setup.
- [ ] `Tests/EditMode/AABBCollisionTests.cs` (3 tests) — Rect.Overlaps detection + push-axis math
- [ ] Final count: 112 + 15 = **127**

### Phase 12 — Verify + commit
- [ ] Manual playtest deferred to designer (Unity-required)
- [ ] Update lessons.md if new patterns surface during implementation
- [ ] Commit `[WS-012.5] Ground/spillover system + bag selling + mode toggle`

## Deviations from spec (explicit)

- **Test baseline 112, not 87** — spec is out of date; target 127 not 102.
- **Scene names use actuals** — `ShopOverlayPanel` (not ShopOverlay), `GridAnchor` (not BackpackGrid).
- **Ground items use `GridClickTooltipHandler`, not a separate `LockToggleHandler`** — that class doesn't exist; locking lives in DetailTooltipController per WS-012.1. Confirmed with designer.
- **`InventoryService.GetItemsInBag`** uses `item.HostBag == bag` (existing field) rather than the spec's `OccupiedCells` overlap math (cell ownership is computed on-the-fly here, not stored).
- **`GroundStateTests` operates on `List<GroundItemSnapshot>` directly** — testing the MonoBehaviour GroundManager from EditMode requires either a PlayMode harness or extracting a pure data layer; the spec §6 troubleshooting block flags this. Pure-data tests over snapshot/restore roundtrips give the same correctness signal.
- **No grand "GroundArea Y-offset tuning"** — set initially to anchoredPosition (-252, -288), size (504, 120) directly below the grid. Designer can nudge in Inspector if visual playtest reveals overlap.
- **Ground item rotation does NOT visually rotate** — gotcha #21 / Notes: rotation field preserved for re-place, but on-ground render uses base orientation.
- **Bag spill scatter uses uniform random spawn from top of GroundArea**, not bag's last grid position — cleaner, avoids cascading transform math, matches spec §3 gotcha #22 / Notes.

## Done

**Phase 1 — Events + data**
- [x] Appended `DragMode` enum + `DragModeChangedEvent` + `BagSoldEvent` + `GroundItemAddedEvent` + `GroundItemRemovedEvent` to [InventoryEvents.cs](Assets/_Project/Scripts/Core/Events/InventoryEvents.cs)
- [x] New [ShopPhaseStartedEvent.cs](Assets/_Project/Scripts/Core/Events/ShopPhaseStartedEvent.cs)
- [x] [RunManager.EndCurrentRound](Assets/_Project/Scripts/Core/RunManager.cs#L67) publishes `ShopPhaseStartedEvent` after `RoundEndedEvent` (suppressed on final round)
- [x] `BagData.EffectivePrice` already existed — no edit needed

**Phase 2 — InventoryService helpers**
- [x] [InventoryService.GetItemsInBag](Assets/_Project/Scripts/Inventory/InventoryService.cs) — returns items whose HostBag matches
- [x] [InventoryService.RemoveItemSilent](Assets/_Project/Scripts/Inventory/InventoryService.cs) — no event publish
- [x] [InventoryService.RemoveBagWithoutCascade](Assets/_Project/Scripts/Inventory/InventoryService.cs) — calls new `Grid.RemoveBagOnly`
- [x] [InventoryGrid.RemoveBagOnly](Assets/_Project/Scripts/Inventory/InventoryGrid.cs#L48) — bag-only removal, no item cascade

**Phase 3 — Mode toggle**
- [x] [DragModeService.cs](Assets/_Project/Scripts/Core/DragModeService.cs) — scene-scoped singleton, Tab key handler, refuses toggle during drag, resets on ShopPhaseStartedEvent
- [x] [ModeToggleButton.cs](Assets/_Project/Scripts/UI/ModeToggleButton.cs) — Button + Label component, refreshes on event

**Phase 4 — Drop-zone helpers**
- [x] [BackpackBoundsProvider.cs](Assets/_Project/Scripts/UI/BackpackBoundsProvider.cs) — tag-style component with static `Current`
- [x] [DropZoneClassifier.cs](Assets/_Project/Scripts/UI/DropZoneClassifier.cs) — `IsWithinBackpackXRange` using backpack + ground bounds

**Phase 5 — Ground system**
- [x] [GroundItem.cs](Assets/_Project/Scripts/UI/GroundItem.cs) — POCO wrapper (single source of lock truth: reads from `Instance.IsLocked` so the detail-tooltip lock action stays authoritative) + serializable `GroundItemSnapshot`
- [x] [GroundItemPhysics.cs](Assets/_Project/Scripts/UI/GroundItemPhysics.cs) — gravity / wall AABB / bounce / friction / sleep
- [x] [GroundDragHandler.cs](Assets/_Project/Scripts/UI/GroundDragHandler.cs) — grid-snap / sell / fling / snap-back semantics, publishes `ItemDragBeganEvent`
- [x] [GroundManager.cs](Assets/_Project/Scripts/UI/GroundManager.cs) — scene singleton, AddItem/RemoveItem/ClearAll/Snapshot/Restore, 3-pass collision + Z-sort in FixedUpdate, subscribes to ShopPhaseStartedEvent/RoundStartedEvent/RunStartedEvent/DragModeChangedEvent + `ItemLockChangedEvent` (refreshes the 16×16 lock overlay when the detail-tooltip lock action toggles), 16×16 lock icon overlay matching grid items via `_lockIconSprite` SerializeField
- [x] L-014 followed: visual children built via `Instantiate` in `AddItem`, NOT in OnEnable

**Designer correction — applied mid-implementation**: Spec §4.x suggested attaching `TooltipTarget` (hover) and a `LockToggleHandler` (right-click) to ground items. These are pre-WS-012.1 patterns. **Corrected to match current grid item UX**: ground items use `GridClickTooltipHandler` only (left-click → detail tooltip → lock action icon). `TooltipTarget` removed from the prefab. `LockToggleHandler` not added. Lock state lives on `ItemInstance.IsLocked` (the single source of truth that `DetailTooltipController` writes through `InventoryService.ToggleItemLock`). `GroundManager` subscribes to `ItemLockChangedEvent` and refreshes the 16×16 overlay on the affected ground item — same visual and interaction model as grid items.

**Phase 6 — Bag selling**
- [x] [SellModal.cs](Assets/_Project/Scripts/UI/SellModal.cs) rewritten — subscribes to bag events; shows "Drag here to sell for Xg (spills contents)" or "LOCKED — cannot sell"; bag drop runs gold→spill→remove order
- [x] Items inside sold bag preserve their lock state on spill

**Phase 7 — Shop drop to ground**
- [x] [ShopSlotDragHandler.OnEndDrag](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs#L109) restructured: valid grid → buy & place; else if items+in-range → buy & deposit on ground; else cancel. Affordability check FIRST.

**Phase 8 — Grid drag to ground**
- [x] [DragHandler.OnEndDrag](Assets/_Project/Scripts/UI/DragHandler.cs#L137) added `TryDepositOnGround` branch between `TryCommitDrop` failure and `ReturnToOriginal`. Bags unaffected (no ground deposit).

**Phase 9 — Mode-based grey-out**
- [x] [InventoryGridView](Assets/_Project/Scripts/UI/InventoryGridView.cs) — subscribes to `DragModeChangedEvent`, applies `_itemAlpha` / `_bagAlpha` in RenderItem/RenderBag, disables non-active layer's DragHandler + raycast
- [x] `ApplyItemAlphaToCellChildren` helper multiplies per-cell child Image alphas
- [x] GroundManager applies the same pattern via `ApplyVisualMode`

**Phase 10 — Scene wiring**
- [x] Hand-authored [GroundItem.prefab](Assets/_Project/Prefabs/UI/GroundItem.prefab) using 19-digit fileIDs (L-003): RectTransform 56×56, Image, CanvasGroup, GroundItemPhysics, GroundDragHandler, TooltipTarget, GridClickTooltipHandler
- [x] [Game.unity](Assets/_Project/Scenes/Game.unity) added:
  - `BackpackBoundsProvider` MonoBehaviour (&4150) on GridAnchor (&2120) — also updated GridAnchor's `m_Component` list
  - `GroundArea` GameObject (&4200) as sibling of GridAnchor under InventoryGridContainer; anchoredPosition (0, −228), size 504×120, Image color (0,0,0,0.5), initial `m_IsActive: 0` — activates on ShopPhaseStartedEvent. Updated InventoryGridContainer's `m_Children` list
  - `GroundManager` MonoBehaviour (&4204) on GroundArea with `_groundAreaRT: &4201` + `_groundItemPrefab: GroundItem.prefab`
  - `DragModeService` GameObject (&4300) at scene root, RootOrder 7 (sibling of EventSystem)
  - `ModeToggleButton` GameObject (&4400) under BottomBar with UI.Image (button bg, dark grey), UI.Button, `ModeToggleButton` script wired to button + label. Updated BottomBar's `m_Children`. Anchor (0, 0.5), AnchoredPosition (30, 0), size 200×60.
  - Child `Label` GameObject (&4410) under the button with UI.Text "Mode: Items", FontSize 18, bold, center-aligned, full-rect anchored

**Phase 11 — Tests**
- [x] [BagSellMathTests.cs](Assets/_Project/Tests/EditMode/BagSellMathTests.cs) — 3 tests pinning `ceil(EffectivePrice * 0.5f)` for 1g/3g/10g
- [x] [DragModeServiceTests.cs](Assets/_Project/Tests/EditMode/DragModeServiceTests.cs) — 5 tests: default mode, toggle, event publish, ShopPhaseStarted reset, drag-in-progress refusal
- [x] [GroundStateTests.cs](Assets/_Project/Tests/EditMode/GroundStateTests.cs) — 5 tests on `GroundItemSnapshot` data layer (full GroundManager integration deferred to PlayMode/playtest per spec §6)
- [x] [AABBCollisionTests.cs](Assets/_Project/Tests/EditMode/AABBCollisionTests.cs) — 3 tests on `Rect.Overlaps` + push-axis math
- [x] **Total new tests: 16** (spec asked for 15; extra +1 from the drag-in-progress refusal test). New target: 112 + 16 = **128 passing**.

## Deferred to designer (Unity-required)

These cannot run from this environment — they need the Unity Editor:

- [ ] **CRITICAL — close + reopen `Game.unity` in Unity before saving anything else** (per `project_unity_open_scene_reload` memory). Disk edits this round: GridAnchor `m_Component` list, InventoryGridContainer `m_Children`, BottomBar `m_Children`, plus a large YAML append at the end of the file (BackpackBoundsProvider component, GroundArea + GroundManager, DragModeService, ModeToggleButton + Label). If Unity has the scene open in memory, it will overwrite my disk edits on save.
- [ ] Recompile. Confirm **zero errors AND zero warnings**.
- [ ] Test Runner → EditMode → Run All. Expected: **128 passing, 0 failing, 0 ignored**.
- [ ] Run the playtest sequence from spec §4.16 (22 manual steps). Particular things to check:
  - GroundArea appears during shop only
  - Drop shop item in valid cell vs invalid-in-range vs out-of-range (3 paths)
  - Drop grid item in invalid-in-range → falls to ground
  - Drag bag → modal shows "Drag here to sell for Xg (spills contents)"; drop spills contents to ground, bag removed
  - Locked bag refuses sale
  - Locked items inside sold bag retain lock on ground
  - Right-click ground item — actually **LEFT-click** opens detail tooltip with lock action (per the architectural decision)
  - Tab toggles mode; mode resets to Items at shop start; mode toggle ignored mid-drag
  - Ground state survives shop → combat → shop within a run; cleared on new run
- [ ] Commit `[WS-012.5] Ground/spillover system + bag selling + mode toggle` (will commit from CLI after this turn — see Review notes)

## Review notes

**Architectural deviations from spec — explicit and intentional**:

1. **No `LockToggleHandler` for ground items** — the spec assumes that class exists from WS-012, but WS-012.1 evolved locking to use `DetailTooltipController` via `GridClickTooltipHandler` on left-click. Ground items use the same pattern for consistency. Right-click does NOT toggle lock (the spec's words); left-click opens the detail tooltip and the lock action lives there.

2. **`InventoryService.GetItemsInBag`** uses the existing `item.HostBag == bag` field rather than the spec's `OccupiedCells` overlap math. The HostBag field is the authoritative ownership signal in this codebase.

3. **`InventoryGrid.RemoveBagOnly`** added as the clean non-cascading bag-removal primitive. `InventoryService.RemoveBagWithoutCascade` delegates to it. Avoids touching `AddItemDirect` (which is documented as preview-only in WS-012.1).

4. **`GroundStateTests` operates on `GroundItemSnapshot` data layer** rather than full GroundManager integration. The spec §6 troubleshooting block flagged that GroundManager testing requires either PlayMode or a pure-data refactor. The data-layer tests pin the save-system contract; full integration is covered by playtest.

5. **GroundArea parented to InventoryGridContainer** (sibling of GridAnchor) rather than to ShopOverlayPanel. Same final visual placement, simpler math — both live in the same coordinate space.

6. **No `GridCoordinateHelper` extraction.** The spec mentions one, but the duplication between DragHandler/ShopSlotDragHandler is mild and the helper isn't load-bearing for the spec. Adding it would have been refactoring beyond scope.

**Spec-vs-reality drift**:

- Spec said test baseline was 87; actual was 112. New target: 128.
- Spec said "ShopOverlay" / "BackpackGrid"; actuals are `ShopOverlayPanel` / `GridAnchor`.
- Spec said `ShopPhaseStartedEvent` likely existed and to "verify"; it didn't, so I authored + wired it.
- Spec said `BagData.EffectivePrice` might be missing; already existed.

**Edge cases that matter**:

- L-012 (OnDrop fires before OnEndDrag) — the bag-sale flow follows it cleanly: capture items → gold → spawn on ground (RemoveItemSilent for each) → RemoveBagWithoutCascade for the bag. If any step throws, no compensation is paid out before removal completes.
- DragHandler's existing `_dropCommitted` mechanism extended naturally — added a new commit path (`TryDepositOnGround`) between `TryCommitDrop` and `ReturnToOriginal`.
- `GroundDragHandler.OnBeginDrag` early-returns if `DragModeService.CurrentMode != Items` — ground items refuse drag in Bags mode (since they ARE items).
- The `DragHandler.TryDepositOnGround` explicitly publishes `ItemRemovedEvent` after the silent removal, so `InventoryGridView` re-renders to drop the stale grid visual. Without that publish, the grid view would still show the item until the next refresh.

**Potential follow-ups if playtest reveals issues**:

- Physics tuning (GRAVITY=-1200 / BOUNCE_DAMP=0.5 / FRICTION=0.92 / SLEEP_THRESHOLD=5) — placeholders from genre conventions; may need tweaking.
- GroundArea Y position (-228 in InventoryGridContainer-local) may overlap something or sit too far below the grid — designer can nudge in Inspector.
- The ModeToggleButton's bottom-left position may collide with anything else in BottomBar at low resolutions — there's only the StartNextRoundButton (right-anchored), so should be fine.
- The new prefab uses Unity's default UISprite for the button background (sliced). If the designer wants a custom button look, swap the sprite reference in Game.unity (or in the prefab).
- Save persistence (WS-013) consumes `GroundManager.SnapshotGroundState` / `RestoreGroundState` directly — no further hook needed.

**No new lessons** captured this session. The "spec is out of date" pattern is already L-004 / L-008 / L-009 — checked the project against the spec, didn't blindly follow. The L-012 trap (OnDrop before OnEndDrag) was already in mind and was handled correctly by the SellModal bag-sale ordering.



# WS-012.6 review (settings menu + accessibility + pause + controller foundation)

Plan: C:\Users\admin\.claude\plans\snoopy-strolling-sprout.md. Test count target: prior baseline + 11 new tests (7 SettingsManager + 4 SettingsSaveRoundTrip).

## What landed on disk

- **Assets/_Project/Scripts/Core/SaveData.cs** — added SettingsSaveData (audio + display + accessibility fields).
- **Assets/_Project/Scripts/Core/SaveManager.cs** — added SaveSettings(SettingsSaveData), LoadSettings(), SettingsFileExists(), SettingsPath (separate settings.json next to save.json), internal _settingsPathOverride for tests.
- **Assets/_Project/Scripts/Core/SettingsState.cs** — runtime mirror DTO with FromSaveData / ToSaveData converters.
- **Assets/_Project/Scripts/Core/SettingsChangedEvent.cs** — SettingKind enum + IGameEvent struct.
- **Assets/_Project/Scripts/Core/SettingsManager.cs** — singleton (Boot scene). Public setters apply immediately to AudioMixer / Screen / QualitySettings / Application, persist, publish event. SetResolutionWithConfirm runs the 10-second revert coroutine on the persistent singleton so it survives the menu closing. LinearToDecibels is a public static for direct test coverage.
- **Assets/_Project/Scripts/UI/SettingsMenuController.cs** — code-built UI per L-014 (Awake builds tree, OnEnable only syncs + wires). Four tabs: Audio (4 sliders + 4 mute toggles), Display (resolution / fullscreen / vsync / framerate + Apply), Accessibility (shake / reduce-motion / high-contrast), Controls (read-only bindings text). Cycler pattern (left/right arrows around a value label) used instead of TMP_Dropdown to avoid the template complexity. Confirm-revert overlay built as a sibling inside the menu.
- **Assets/_Project/Scripts/UI/PauseMenuController.cs** — subscribes to Pause action, gates pause while run-end overlay owns timescale, opens shared SettingsMenu on demand. UI built procedurally in Awake.
- **Assets/_Project/Scripts/UI/MainMenuController.cs** — added _settingsButton, _settingsMenu and wiring.
- **Assets/_Project/Scenes/Boot.unity** — SettingsManager GameObject under Managers (fileIDs &360/&361/&362). _mainMixer left at fileID 0 until the user creates the mixer asset.
- **Assets/_Project/Scenes/MainMenu.unity** — Settings button (&700) sibling to Start Run, SettingsMenu anchor (&800) under Canvas. MainMenuController fields wired.
- **Assets/_Project/Scenes/Game.unity** — PauseMenu (&5000) + SettingsMenu (&5010) under Canvas. _inputActions SerializeField on PauseMenu left at fileID 0 (user must drag the asset in inspector).
- **Assets/_Project/Tests/EditMode/SettingsManagerTests.cs** — 7 tests (volume math, defaults parity, state round-trip).
- **Assets/_Project/Tests/EditMode/SettingsSaveRoundTripTests.cs** — 4 tests (save/load preservation, missing file, corrupt file with LogAssert, exists predicate).
- Drive-by warning fix: enableWordWrapping → textWrappingMode in RecipesComingSoonModal.cs, TooltipController.cs, Editor/MigrateTextToTMPro.cs, plus the new SettingsMenuController.cs. Captured a new memory entry **feedback_tmp_word_wrapping_obsolete** so this doesn't recur.

## Plan deviations (transparent)

1. **MainMixer.mixer asset NOT hand-authored** — Unity's .mixer YAML is too complex to author from scratch without a known-good template (involves AudioMixerController + AudioMixerGroupController + AudioMixerSnapshotController + AudioMixerEffectController with cross-referencing GUIDs for effects, snapshots, and exposed params). Plan risk #1 explicitly contemplated this fallback. SettingsManager._mainMixer null-guards every Volume_* SetFloat, so volumes persist and apply through every other path without the mixer.
2. **Prefab approach swapped for scene-level GameObject anchors** — the approved hybrid "prefab shell + code-built children" approach turned out to give zero DRY benefit: the controller has no SerializeField scene state to share between instances. Pure code-built UI under scene GameObjects (one in MainMenu, one in Game) is functionally equivalent with one fewer artifact.
3. **TMP_Dropdown swapped for left/right cycler** — Building a TMP_Dropdown from scratch in code requires a non-trivial Template GameObject with viewport/content/toggle prefab/etc. The cycler is functionally equivalent for the spec's four use cases (resolution, fullscreen mode, framerate, screen shake), controller-friendlier, and roughly 5x less code.
4. **MainMixer audit step is a no-op** — recon confirmed no AudioSource components exist in scripts. The spec's §4.1 routing audit has nothing to route. Future audio work routes through MainMixer from day one once the user creates it.

## REQUIRED manual steps in Unity Editor before testing

1. **Close + reopen all three scene tabs** in Unity (Boot.unity, MainMenu.unity, Game.unity) per the project_unity_open_scene_reload memory rule. Unity will overwrite my disk edits if it has the scene open in memory.
2. **Create Assets/_Project/Audio/MainMixer.mixer**: Project window → right-click _Project/Audio/ → Create → Audio Mixer → name it MainMixer. Open it (Window → Audio → Audio Mixer). Add 3 child groups under Master: Music, SFX, Voice. For each of the four groups (Master + 3 children): right-click its Volume attenuation parameter → "Expose 'Volume' to script". In the Exposed Parameters dropdown (top-right of the mixer window), rename them: Volume_Master, Volume_Music, Volume_SFX, Volume_Voice. Save.
3. **Wire _mainMixer on SettingsManager**: Open Boot.unity, select the SettingsManager GameObject under Managers, drag MainMixer.mixer into the _mainMixer field.
4. **Wire _inputActions on PauseMenu**: Open Game.unity, select the PauseMenu GameObject under Canvas, drag Assets/_Project/Settings/Input/PlayerInput.inputactions into the _inputActions field.
5. **Run EditMode tests**: Window → General → Test Runner → EditMode → Run All. Expect previous baseline + 11 new passing tests, 0 failing.
6. **Recompile**: expect zero errors AND zero warnings (the four enableWordWrapping warnings are now fixed).
7. **Manual playtest** per plan §Verification (eight scenarios).

## Why I didn't run tests myself

Unity holds the project lock (Temp/UnityLockfile present). Running batch-mode Unity tests while the editor is open would fail. No dotnet / msbuild / csc is on PATH, so I couldn't do an external compile-check either. Static review of cross-references was clean.

## New lessons captured this session

- **feedback_tmp_word_wrapping_obsolete** — in Unity 6.x TMP, write textWrappingMode = TextWrappingModes.Normal from the start; enableWordWrapping is CS0618.



# WS-012.7 — Audit Cleanup

Plan: C:\Users\admin\.claude\plans\glowing-stirring-bentley.md. Source spec: WS-012_7_Audit_Cleanup.md. Source audit: tasks/ws_012_x_audit.md (dated 2026-05-15, same day as this work).

## Pre-flight

- [x] Audit doc present at tasks/ws_012_x_audit.md, dated same day
- [x] No significant commits since audit (audit and work are on the same commit, 566e463 + uncommitted)
- [x] Unity Editor is closed (confirmed by user) — safe for scene YAML edits
- [ ] 28 EditMode tests pass at current head — deferred to user run in Unity

## What landed on disk

### Code changes

- **Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs** — added private `RebuildGhostVisual()` method (mirrors `DragHandler.RebuildDraggedVisualForRotation` at DragHandler.cs:120-134). Called from end of `OnBeginDrag` (after ghost instantiation) and end of `Rotate()` (after `_currentRotation` update). Items only — bags keep the existing single-cell yellow ghost. Suppresses the prefab root Image alpha to 0 so only the rebuilt cell children render. Uses the existing shared primitive `_gridView.BuildItemCellChildren(...)` rather than extracting a new helper class (no actual duplication exists to remove). Resolves the "items appear 1×1" complaint.
- **Assets/_Project/Scripts/UI/SettingsMenuController.cs:484** — Controls tab display text "Move:" → "Movement:" to align with the actual action name in PlayerInput.inputactions. No `FindAction()` consumers exist either way (grep returned 0 matches for both `"Move"` and `"Movement"`), so this is display-text-only and safe.

### Scene YAML changes

- **Assets/_Project/Scenes/Game.unity:187** — Main Camera orthographic size 7 → 8. Aligns base camera with Cinemachine runtime override (CinemachineCamera lens at Game.unity:332 is already size 8). Eliminates the momentary 7→8 jump before CM takes control.

### Doc changes

- **tasks/lessons.md** — appended **L-016** (AudioMixer routing requirements; volume sliders without a routed AudioSource are silently no-ops) and **L-017** (multi-cell drag ghost pattern; any new drag source from outside the grid must call `BuildItemCellChildren` on OnBeginDrag and OnRotate).
- **tasks/ws_012_x_audit.md** — annotated every Blocking / Important / Cosmetic finding with ✓ Resolved / ⚠ Deferred status. Added a WS-012.7 Resolution Summary at the bottom.

## Plan deviations (transparent)

1. **`_inputActions` YAML wiring at Game.unity:5848 deferred to user in Unity Editor.** Original plan was to hand-edit the YAML. After investigating, the InputActionAsset is imported via ScriptedImporter (see PlayerInput.inputactions.meta), so its main asset fileID is content-derived and not safely guessable. No existing reference example exists in this project to copy from. A 3-second drag-drop in Unity Editor produces the correct YAML form deterministically; hand-authoring risks a silently-broken reference. Per spec §3 gotcha 9 ("Editor wiring is safer"), this is the recommended path anyway.
2. **`DragGhostBuilder` helper class NOT extracted (Option A chosen over Option B).** Spec §4.1 offered both. The existing shared primitive is `InventoryGridView.BuildItemCellChildren` — both DragHandler and now ShopSlotDragHandler call it directly. A wrapper class around the 5-line bbox+sizeDelta+BuildItemCellChildren call would add a layer without removing duplication. If a third drag source emerges (future ground re-drag, future container drag), revisit then.
3. **Action `Movement` kept; display text aligned.** Spec gave both options as acceptable; chose minimum-change path. Zero `FindAction()` consumers means renaming would have been low-cost but also low-value, and "Movement" reads better as a UI label than "Move".
4. **No temporary AudioSource dropped** — spec §4.8 marked it optional. Verification of the volume chain end-to-end requires a routed AudioSource somewhere; user to add one (or wire existing audio to mixer groups) when convenient. Captured in L-016 as a future verification requirement.

## REQUIRED manual steps in Unity Editor before testing

1. **Reopen Unity.** Per L-009 (project_unity_open_scene_reload memory): if any of Boot.unity / MainMenu.unity / Game.unity were open in a tab when Unity was last closed, close+reopen those tabs before saving any scene, so Unity loads my disk edits fresh.
2. **Create MainMixer.mixer** at `Assets/_Project/Audio/MainMixer.mixer`: Project window → right-click `_Project/Audio/` → `Create → Audio Mixer` → name it `MainMixer`. Open it via `Window → Audio → Audio Mixer`. Add 3 child groups under Master: `Music`, `SFX`, `Voice`. For each of the four groups (Master + 3 children): right-click Volume attenuation slider in Inspector → "Expose 'Volume' to script". In the Exposed Parameters dropdown (top-right of mixer window), rename them to exactly: `Volume_Master`, `Volume_Music`, `Volume_SFX`, `Volume_Voice` (case-sensitive). Save project.
3. **Wire MainMixer on SettingsManager**: Open Boot.unity, select SettingsManager GameObject under Managers, drag MainMixer.mixer asset into the `_mainMixer` Inspector field. Save.
4. **Wire `_inputActions` on PauseMenuController**: Open Game.unity, select PauseMenu GameObject under Canvas, drag `Assets/_Project/Settings/Input/PlayerInput.inputactions` into the `_inputActions` Inspector field. Save.
5. **Run TMP migration on scenes** (per spec §4.5): Close all scene tabs in Unity. Open Game.unity. Run `Tools → WS-012.4 → Migrate Text → TMP (Active Scene)`. Save Game.unity. Open MainMenu.unity. Run the same menu action. Save MainMenu.unity. Expected outcome: 19 legacy Text components in Game.unity and 3 in MainMenu.unity converted to TextMeshProUGUI.
6. **(Optional but recommended)** Add a temporary AudioSource to Main Menu's "New Run" button: select button GameObject → Add Component → AudioSource → assign any audio clip → set Output to `MainMixer → SFX` → uncheck Play On Awake → wire onClick to call `audioSource.Play()`. This becomes the first real SFX wiring and serves as end-to-end verification for the volume sliders.
7. **Run all 28 EditMode tests**: `Window → General → Test Runner → EditMode → Run All`. Expect all 28 passing, 0 failing. If a test fails, investigate before proceeding — it's a real regression from one of the changes above.
8. **Manual playtest per spec §4.10**:
   - Shop drag ghost: drag Bone Knife (1×2) → 2 bone-white cells at cursor; drag Hollow Crown (2×2) → 4 dark-gold cells; press R during drag → ghost rotates correctly.
   - Pause: Escape in Game scene → pause menu opens. Settings/Resume/Quit-to-Menu buttons work.
   - Volume: drag master slider down → AudioSource goes silent; relaunch game → setting preserved.
   - TMP: Game and MainMenu scenes show crisp TMP text; no `enableWordWrapping` warnings; no legacy Text warnings.
   - 3-round playthrough: zero console errors, zero warnings.

## Why I didn't run tests myself

Unity holds the project lock (Temp/UnityLockfile present when Unity is open); when Unity is closed, no batch-mode Unity is on PATH from this shell. Static review of cross-references was clean. The shop drag ghost fix mirrors an existing in-codebase pattern (`DragHandler.RebuildDraggedVisualForRotation`) line-for-line, so semantic risk is bounded.

## New lessons captured this session

- **L-016** (in lessons.md) — Volume sliders that "work" without a routed AudioSource are silently no-ops; AudioMixer asset + exposed params + routed AudioSources are all required.
- **L-017** (in lessons.md) — Multi-cell drag ghost: rebuild children on drag start AND on rotation. Any new drag source from outside the grid must call `InventoryGridView.BuildItemCellChildren` at OnBeginDrag and OnRotate, not just instantiate a ghost prefab.

---

# Active task: WS-013 — Save / Resume System (Between-Rounds, Run + Meta + Settings Architecture)

Spec: `WS-013_Save_Resume_System.md` (provided 2026-05-16).

## Pre-flight deltas (3 spec assumptions vs reality — user approved decisions)

| Spec assumption | Reality | Decision |
|---|---|---|
| `HeroData` class exists; `RunManager.CurrentHero` is set | Neither exists. Hero is implicit (single `HeroStartingLoadout` MonoBehaviour). | Scaffolded minimal `HeroData` SO (id + display name) + single `DefaultHero.asset`. RunManager now carries `_defaultHero` + `CurrentHero`. |
| Data SOs under `Resources/` so `Resources.LoadAll` works | Assets live at `Assets/_Project/ScriptableObjects/`. | Hand-authored `DataManifest.asset` (allowed by spec §3 #5/#6). No file moves. |
| No SaveManager exists ("§0.4 should return zero hits") | Existing SaveManager from WS-012.6 owns `save.json` + `settings.json`. | Left `save.json` alone, added `run_save.json`. Existing API untouched. |

Other deltas applied directly (no user choice needed): Health uses `float` not `int`; `BagInstance` has no `Rotation` (omitted from schema); `ItemInstance.Origin` is the anchor cell name; `Rotation` enum values are 0/90/180/270 (stored raw as int); `RunEndedEvent` carries `bool Victory`; `InventoryService` enumerates via `Grid.Items` / `Grid.Bags`, `ClearAll()` added.

## What shipped

### New files
- `Assets/_Project/Scripts/Core/HeroData.cs` — minimal SO (`_id`, `_displayName`)
- `Assets/_Project/Scripts/Core/DataManifest.cs` — Items/Bags/Heroes/Enemies lists
- `Assets/_Project/Scripts/Core/DataRegistry.cs` — Managers-parented singleton; indexes by Id
- `Assets/_Project/Scripts/Core/RunSaveData.cs` — `RunSaveData`, `BagSaveEntry`, `ItemSaveEntry`, `GroundItemSaveEntry`
- `Assets/_Project/Scripts/Core/Events/SaveEvents.cs` — `RunSaveCompleted/Failed/RunResumed` events
- `Assets/_Project/Scripts/Save/RunRestoreController.cs` — Game-scene restore orchestrator
- `Assets/_Project/Scripts/Editor/DataIdValidator.cs` — Tools menu validator
- `Assets/_Project/ScriptableObjects/Heroes/DefaultHero.asset` — `default_hero`
- `Assets/_Project/ScriptableObjects/DataManifest.asset` — 10 items + 1 bag + 1 hero + 1 enemy
- `Assets/_Project/Tests/EditMode/RunSaveDataTests.cs` — 3 schema round-trip tests
- `Assets/_Project/Tests/EditMode/RunSaveManagerTests.cs` — 9 lifecycle tests

### Edited files
- `Assets/_Project/Scripts/Inventory/ItemData.cs` — `_id` field + `Id` property
- `Assets/_Project/Scripts/Inventory/BagData.cs` — `_id` + `Id`
- `Assets/_Project/Scripts/Combat/EnemyData.cs` — `_id` + `Id`
- `Assets/_Project/Scripts/Combat/Health.cs` — `IsPlayer` getter + `RestoreFromSave(float, float)`
- `Assets/_Project/Scripts/Inventory/InventoryService.cs` — `ClearAll()` (clears Grid, publishes BagRemovedEvent per bag)
- `Assets/_Project/Scripts/Inventory/HeroStartingLoadout.cs` — early-return when `GameManager.Instance.PendingResume != null`
- `Assets/_Project/Scripts/Core/SaveManager.cs` — `HasRunSave/SaveRun/LoadRun/DeleteRunSave/BeginRestore/EndRestore/CaptureAndSaveRun`, run-save path override, auto-save on ShopPhaseStartedEvent, auto-delete on RunEndedEvent
- `Assets/_Project/Scripts/Core/RunManager.cs` — `_defaultHero` + `CurrentHero` + `RestoreFromSave(int, int, HeroData)`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `PendingResume` field + `StartResumeFromSave()`
- `Assets/_Project/Scripts/UI/MainMenuController.cs` — `_resumeRunButton` wiring + visibility + DeleteRunSave on New Run

### Edited scenes / assets
- `Boot.unity` — added `DataRegistry` GameObject (`&370`) under `Managers`; wired `_defaultHero` on RunManager
- `Game.unity` — added root `RunRestoreController` GameObject (`&6000`, RootOrder 18)
- `MainMenu.unity` — added `ResumeRunButton` (`&900`) above StartRunButton, `_resumeRunButton` reference in MainMenuController
- 10 item assets + 1 bag + 1 enemy — backfilled `_id` (snake_case stem)

## Why I didn't run tests / Unity

Unity holds the project lock (Temp/UnityLockfile present when Unity is open); when closed, no batch-mode Unity is on PATH from this shell. Static review of cross-references was clean:
- All `using` statements correct (verified imports for SaveManager → Combat/Inventory/UI; RunRestoreController same).
- `FindObjectsByType<T>()` parameterless overload used (L-005).
- `DontDestroyOnLoad(transform.root.gameObject)` on DataRegistry (L-001).
- All new scripts have `.meta` files with unique fixed GUIDs.
- DataManifest.asset references the 12 existing assets by GUID (Unity's GUID-based refs survive any folder move).

## REQUIRED manual steps in Unity Editor before testing

1. **Reopen Unity scene tabs.** Per L-009 (project_unity_open_scene_reload): if Boot.unity / Game.unity / MainMenu.unity are open in tabs when Unity launches, close+reopen those tabs so my disk edits load fresh.
2. **Run `Tools → Hellpit Rampage → Validate Data IDs`.** Expect "Validation passed. 0 errors." If any ID is empty or duplicate, the validator surfaces a clickable error with the offending asset.
3. **Run all EditMode tests** (`Window → General → Test Runner → EditMode → Run All`). Expect baseline + 12 new = ~140 passing, 0 failing. (Existing SaveManagerTests / SettingsSaveRoundTripTests untouched.)
4. **Manual playtest per spec §4.10 scenarios:**
   - Fresh boot → no Resume button → New Run → play to shop → quit
   - Confirm `run_save.json` at `%userprofile%/AppData/LocalLow/<Company>/<Product>/`
   - Re-launch → Resume visible → click → land in shop with round + gold + HP + bags + items + ground items + lock states intact
   - Multi-cell rotated item round-trips correctly
   - Die → save deleted → Resume button gone
   - New Run with existing save → save deleted before scene load (no stale-state risk)
5. **Schema mismatch sanity:** manually edit `run_save.json` and set `SaveFormatVersion: 999` → relaunch → no Resume button, warning logged.

## Decisions taken (rationale captured for future references)

1. **Hand-authored DataManifest over Resources.LoadAll.** The spec §3 #5/#6 explicitly allows this for our scale (~12 assets). Avoids moving 12 assets (no Resources folder convention exists today), keeps the diff scoped, makes the dependency graph explicit. If asset count grows past ~100, revisit.
2. **HeroId in schema even though only one hero exists today.** User chose "scaffold minimal HeroData now" over "skip heroId" — locks in the save format so future hero-unlock WSes don't need a schema bump just for this field.
3. **No HostBagId in item save entries.** `InventoryGrid.PlaceItem` reconstructs `HostBag` automatically from spatial overlap (bags must precede items in restore order — implemented in RunRestoreController). Storing HostBagId would be redundant and risk drift if items move between bags during shop.
4. **One restore frame deferred via `yield return null`.** Lets every Awake (singletons + scene objects) complete before RunRestoreController reads/mutates. Mirrors the pattern in any controller that depends on cross-component state.
5. **HostBag spillover on placement failure.** If a saved item's shape no longer fits at its origin (content change since save), the restore controller calls `GroundManager.AddItem` rather than aborting the load. Graceful degradation per spec §3 #24/#25.

## New lessons captured this session

- **L-018** (in lessons.md) — Save schema fields named after the in-memory shape are brittle; prefer plain DTO classes with explicit field names and integer enum casts. The save layer should never serialize Unity object references — always go through a stable string ID + DataRegistry.

---

# Active task: Full Game Audit (2026-05-16)

**Goal:** Comprehensive bug / disconnection / "not working as intended" audit of the entire game.
**Deliverable:** `tasks/full_audit_2026-05-16.md` — a findings report. **No code changes this pass.**
**Trigger:** User request — "run a full audit ... find all the issues you can and put them in a report."

## Scope & constraints

- Covers the **full working tree** = last commit `566e463` + all uncommitted changes per `git status`.
- The WS-013 Save/Resume system is **uncommitted and never playtested** — highest-risk area.
- Static analysis only: Unity cannot run from this environment (no batch-mode on PATH, project lock). The ~140 EditMode tests cannot be executed here; playtest-only checks are flagged as such in the report.
- Prior audit `tasks/ws_012_x_audit.md` (2026-05-15) is WS-012.x-scoped; this audit is whole-game and broader.
- `tasks/lessons.md` L-001..L-019 documents intentional decisions — findings will not re-flag those as bugs.

## Method

6 parallel read-only subagents, one subsystem each → I synthesize into one report. Each subagent reads `lessons.md` + `ws_012_x_audit.md` first so known-intentional patterns aren't mis-flagged. Each returns findings as: Severity / file:line / symptom / root cause / suggested fix / confidence.

## Checklist

### Phase 1 — Parallel subsystem analysis (6 subagents)
- [x] SA1 — Combat & round loop (12 findings)
- [x] SA2 — Inventory & synergy core (11 findings)
- [x] SA3 — UI drag/drop & ground (15 findings)
- [x] SA4 — UI tooltip/shop/menus/HUD (12 findings)
- [x] SA5 — Core lifecycle, save/resume, settings, data (17 findings)
- [x] SA6 — Scene & asset wiring (6 findings + verified-clean list)

### Phase 2 — Synthesis
- [x] Deduplicate cross-subsystem findings; reconcile severity
- [x] Spot-verified the two headline Criticals myself (resume event wiring; TMP type mismatch)
- [x] Wrote `tasks/full_audit_2026-05-16.md` — 62 findings: 2 Critical, 7 High, 22 Medium, 28 Low, 3 Cosmetic
- [x] Add Review section to this file

## Out of scope (this pass)
- Fixing the bugs — report only. Fixes are a separate decision after the report is reviewed.
- Running Unity tests / playtest — environment cannot.

## Review

Audit complete. Report at `tasks/full_audit_2026-05-16.md`.

**62 findings** (2 Critical, 7 High, 22 Medium, 28 Low, 3 Cosmetic), plus 6 items reviewed and confirmed *not* bugs.

**Two headline issues:**
1. **C-1 — `Game.unity` text UI is silently broken.** TMP migration ran on scripts/prefabs but never scenes; the C# field types changed `Text → TextMeshProUGUI` while the scene still wires legacy `Text` components, so ~17 label references resolve to `null` (round timer, gold, shop names/prices, reroll, sell modal, run-end header all blank). The prior `ws_012_x_audit.md` mis-classified this as a cosmetic font issue — the field-type change makes it a functional null-wiring failure. Verified directly.
2. **C-2 — WS-013 Resume is non-functional.** `RunRestoreController` publishes `ShopPhaseStartedEvent` but the shop UI listens only for `RoundEndedEvent`, so resume soft-locks the player in an empty scene. The whole resume system (uncommitted, never playtested) has never worked end-to-end — see report §6. Recommend NOT committing WS-013 until C-2 + H-4 + H-5 are fixed and playtested.

**Also notable:** H-1 — the player's HP never heals/resets across 30 rounds and no heal path exists anywhere (likely run-breaking; needs a design decision). H-2/H-3 — the `AudioMixer` asset and pause `_inputActions` remain unwired from the prior audit.

**Verification limits:** static analysis only — Unity could not run, the ~140 EditMode tests were not executed, no playtest. Runtime-dependent items are flagged in report §7. The two Criticals were spot-verified by reading the relevant source.

**Next step (user decision):** review the report, then a separate go-ahead to fix. Suggested fix order is in the report's final section (Tier 1 = before next playtest; Tier 2 = before committing WS-013).

---

# WS-014.A — Audit + Foundation Fixes Before First Playable

Spec: `WS-014_A_Audit_Foundation.md` (provided inline by designer 2026-05-16).
Deliverable: `tasks/ws_014_a_audit.md` (authoritative WS-012.6/WS-013-scoped state doc) + targeted Blocking fixes.

## Method

5 parallel read-only subagents walked the §2 checkboxes (WS-012.6, WS-013, run-end events, cross-cutting). Mid-audit, discovered `tasks/full_audit_2026-05-16.md` (a same-day 62-finding whole-project audit) — reconciled against it and adopted its C-1/C-2/H-4/H-5 findings; verified C-2 directly.

## Checklist

- [x] §0 Pre-flight — precedent docs + lessons re-read; claimed files confirmed present. §0.3 git-clean FAILED (~50 uncommitted files = WS-012.6/12.7/13 work) — proceeded by design (that tree is the audit subject).
- [x] §2 Audit — all 87 checks walked; statuses in `tasks/ws_014_a_audit.md`.
- [x] §3.1 Fix — pause menu: refactored `PauseMenuController` to resolve the Pause action via `new PlayerInputActions()` (removed the null-prone `_inputActions` SerializeField). No Editor step needed.
- [x] §3.2 Fix — resume soft-lock (full_audit C-2): `RunRestoreController` now publishes `RoundEndedEvent` so the shop UI opens on resume.
- [x] §3.4 — NOT applied: audit proved `RunEndedEvent` death publisher already exists + is unit-tested.
- [x] Cleanup — removed stray 0-byte `_confirmRevertCo` repo-root file.
- [x] Audit doc, `lessons.md` (L-021, L-022), `todo.md` updated.
- [ ] **Manual (user, Unity):** TMP migration on `Game.unity` (C-1 — HUD text is blank without it); playtest pause + resume; AudioMixer; EditMode suite.

## Out of scope (this pass)

- C-1 TMP scene migration, AudioMixer creation — Editor-only, cannot be done via static file edits.
- H-4 / H-5 resume hardening — agreed deferred to a focused, playtested resume pass.
- The other 58 `full_audit_2026-05-16.md` findings — whole-game, beyond WS-012.6/WS-013 scope.

## Review

WS-014.A complete. Audit doc at `tasks/ws_014_a_audit.md`.

**87 checks:** ✓57 / ⚠2 / ✗13 / 💥0 / ❓15.

**Two Blocking findings fixed in code:**
1. **Pause menu was dead** — `PauseMenuController._inputActions` was `{fileID: 0}` (the WS-012.7 manual step, never done). Fixed by eliminating the SerializeField and resolving the Pause action in code via `new PlayerInputActions()` — the pattern `PlayerController` already uses. Cannot be forgotten again; no Unity step.
2. **Resume soft-locked** (full_audit C-2) — `RunRestoreController` published `ShopPhaseStartedEvent` but the shop UI opens on `RoundEndedEvent`. Fixed by also publishing `RoundEndedEvent`, mirroring `RunManager.EndCurrentRound`'s order. Verified by code trace; **unplaytested.**

**One Blocking finding I could NOT fix (Editor-only): C-1.** TMP migration was never run on the scenes; because the C# label fields were retyped to `TextMeshProUGUI`, ~17 `Game.unity` labels deserialize to `null` and the in-game HUD renders blank. This is the #1 task before WS-014.B and must be done in Unity.

**Spec assumption corrected:** §3.4 suspected no death publisher for `RunEndedEvent` — false; it is wired and tested.

**Verification limits:** static analysis only; Unity could not run. Both fixes need a Play-mode confirmation. Resume remains edge-case fragile (H-4/H-5 deferred).

**Lessons captured:** L-021 (deferred manual-wiring steps get forgotten — prefer code-resolved input), L-022 (a migration not run on scenes is functional, not cosmetic, when the field type changed).

---

# WS-014.B — First Playable: Run-End Loop + Companion/Biome Placeholders + Asset Infrastructure

Spec: `WS-014_B_First_Playable.md` (provided inline by designer 2026-05-16).
Plan: `~/.claude/plans/fluttering-hopping-flute.md` (approved).
Status: **code complete on disk — awaiting designer EditMode run + playtest, then commit.**

## Method

Audited the spec against the codebase (3 parallel Explore subagents + direct reads of
the run / combat / save / UI systems). The spec was drafted against several stale
assumptions; reconciled them and surfaced 14 deviations at planning — 3 confirmed with
the designer via question, the rest decided on minimal-impact grounds. Implementation
touches C# + 2 `.asset` files only — **zero scene-YAML edits** (composition is
code-driven, per L-021).

## New files

- `Scripts/Core/Events/CompanionAppearanceCompleteEvent.cs` — event gating combat start behind the companion beat.
- `Scripts/Narrative/CompanionAppearanceScheduler.cs` — round→state mapping; shows the companion or publishes the complete event; builds + owns its UI.
- `Scripts/Narrative/CompanionPlaceholderUI.cs` — code-built portrait + subtitle; grace timer, click/key dismiss, round-22 glitch.
- `Scripts/Environment/BiomeTransitionController.cs` — tints the camera background per biome.
- `Tests/EditMode/CompanionStateMappingTests.cs` — 4 tests.
- `Tests/EditMode/BiomeTransitionTests.cs` — 3 tests.
- `ScriptableObjects/Enemies/CompanionDevilBoss_Enemy.asset` (+ `.meta`) — boss EnemyData: id `boss_companion_devil_placeholder`, 800 HP, MoveSpeed 1.5, ContactDamage 8; reuses `Enemy.prefab`.
- `Assets/_Project/{Art,Audio,Text}/…` — 17 asset folders, each with a `README.md` spec.

## Edited files

- `Scripts/Core/RunManager.cs` — added public `EndRunVictory()` (idempotent victory trigger).
- `Scripts/Combat/CombatRoundController.cs` — combat now waits for `CompanionAppearanceCompleteEvent`; round 30 spawns the boss (no timer); boss death → `RunManager.EndRunVictory()`.
- `Scripts/Combat/EnemySpawner.cs` — removed the `_spawnOnStart` self-start (and the now-dead field). Spawning is driven solely by `CombatRoundController`; a self-start would race the companion-gating depending on `Start()` order.
- `Scripts/Combat/Enemy.cs` — captures prefab-default scale/tint in Awake, restores them in `Initialize` (so the boss's scale/tint override can't leak via the shared pool).
- `Scripts/Core/GameSceneBootstrap.cs` — composition root: instantiates the companion + biome controllers in code (always — fresh run and resume).
- `Scripts/UI/RunEndOverlayController.cs` — code-builds a Try-Again button (death) + victory subtitle; relabels return button "Main Menu"; death header shows the round.
- `Scripts/UI/MainMenuController.cs` — Continue Run button label becomes multi-line `Round X · Yg · Hero`.
- `ScriptableObjects/DataManifest.asset` — registered the boss EnemyData (guid `b0551423a4b5c6d7e8f9a0b1c2d3e4f0`).

## Scene / asset YAML

No `.unity` scene edited. Only `DataManifest.asset` (+ the new boss `.asset` / `.meta`).
All new controllers are code-instantiated; all SerializeField bindings on edited
components kept their names so existing scene wiring is preserved untouched.

## Tests

7 new EditMode tests. Expected total **157 → 164**. Not run here (no batch-mode Unity —
designer runs the suite).

## Deviations from spec (full rationale in the approved plan file)

- **D1** Run-end UI — extended the existing `RunEndOverlayController` instead of new `DeathScreen`/`VictoryScreen` (designer-confirmed).
- **D2** Round 30 — boss fight with no round timer; the spec's gotcha #2 ("victory never published") was wrong — `RunManager.EndCurrentRound` already published it on the round-30 timer (designer-confirmed).
- **D3** Boss visual — reused `Enemy.prefab` scaled ×3 + tinted dark red; no new prefab/PNG (designer-confirmed).
- **D4** Kept `RunEndedEvent.bool Victory`; did not add the `RunEndReason` enum (zero functional gain this spec).
- **D5** Victory published by `RunManager.EndRunVictory()`, not the spawner — keeps `RunManager` the sole publisher and `CurrentPhase` correct.
- **D6** `EnemyDiedEvent` left unchanged; round 30 is boss-only so any enemy death during the boss encounter = the boss.
- **D7** Spawner is `EnemySpawner` + `CombatRoundController`; round integration landed in `CombatRoundController`. *Plan said `EnemySpawner.cs` would be untouched — corrected during implementation:* its `_spawnOnStart` self-start had to be removed, or it would spawn during the companion grace regardless of the gating (and the `Start()`-order race made it nondeterministic).
- **D8** Biome = `Camera.backgroundColor` (forces SolidColor clear); there is no backdrop GameObject.
- **D9** Used `GameManager.TransitionTo` — `StartNewRunWithHero`/`ReturnToMainMenu` do not exist; single hero, so Try-Again = a fresh run.
- **D10** All new UI/controllers code-driven; zero scene-YAML edits, no deferred Inspector wiring.
- **D11** No PNG placeholders generated — boss reuses a tinted prefab, companion portrait is a code-tinted Image.
- **D12** Event in its own file (`Core/Events/`); test namespace `HellpitRampage.Tests`.
- **D13** Continue Run preview folded into the button label (no separate element to wire).
- **D14** No GDD exists — implemented only from values inlined in the spec; README "GDD reference" lines kept as forward refs.
- **Extra (in-spirit, beyond the 14):** `BiomeTransitionController` also listens to `ShopPhaseStartedEvent` so a resumed run gets the right backdrop; `CompanionAppearanceScheduler` defers the silent-round complete event one frame to avoid a handler-ordering race; `Enemy.cs` scale/tint reset (D3 pool hygiene).

## Required manual Unity Editor steps

- **No wiring required.** Let Unity reimport on focus (new boss `.asset`/`.meta` + `DataManifest.asset`).
- Run the EditMode suite — expect **164** green.
- Playtest §5.1: full 30-round run → boss → Victory; the Death path + Try-Again; pause; resume.
- Confirm the Main Camera's clear flags are Solid Color (the controller forces this — just verify the biome tint shows).

## New lessons captured this session

L-023 (a spec's "gotcha" can be factually wrong — verify behavioural claims against the
code), L-024 (a per-spawn visual override on a pooled object must be reset in its
Initialize or it leaks across runs), L-025 (publishing an event synchronously inside
another event's dispatch races handler ordering — defer a frame).

## Review

WS-014.B implementation is **code complete on disk, not yet playtested or committed.**
Per CLAUDE.md a spec is not done until the designer's manual playtest passes — that
gate is outstanding. Static cross-reference review done: every new API call checked
against the real signatures; no scene wiring left dangling; existing SerializeField
names preserved. Content audit: `tasks/content_audit_post_ws014b.md`.

---

# WS-014.C — Fix EditMode test suite (pre-WS-015 cleanup)

A headless `-runTests` pass — the first time the EditMode suite was actually run; the team
verifies by playtest — found **15 failures** on the pristine WS-014.B baseline. Root cause
and fixes captured in **L-026**:

- New `Tests/EditMode/EditModeLifecycle.cs` — reflection-invokes `Awake`/`OnEnable` on
  `AddComponent`'d components, which EditMode does not.
- `DontDestroyOnLoad` guarded with `if (Application.isPlaying)` in all 7 Boot singletons
  (EventBus, RunManager, GameManager, SaveManager, PoolManager, SettingsManager,
  DataRegistry) — it throws when called in an EditMode test.
- `PoolManager.DestroyInstance` uses `DestroyImmediate` outside play mode.
- `StarPreviewTests` reads the whetstone's real `InstanceID` (it hard-coded `1`, but
  `PlaceBag` consumes id 1 before the item — the whetstone is id 2).
- 5 fixtures' `[SetUp]` updated to wake their components.

Result: **164/164 green.** Commit `68e4dbc`.

---

# WS-015 — Shop as Dedicated Scene

Spec: `WS-015_Shop_As_Scene.md` (provided inline 2026-05-18). Moves the shop from an
in-scene overlay into a dedicated `Shop.unity`; a run now alternates `Combat.unity` ↔
`Shop.unity` per round. 7 commits, each compile-clean (zero errors/warnings) and
EditMode-green.

**Execution:** driven headlessly — `-batchmode -runTests` for verification, `-executeMethod`
for the scene surgery. CLAUDE.md §12's old "Unity cannot be driven from the shell" claim
was wrong and is corrected.

## Commits

- part 1 `7ed013a` — `Game.unity` → `Combat.unity`; `tasks/ws_015_pre_audit.md`.
- part 2 `e859aa8` — new `SceneRouter`; `InventoryService`/`SynergyService` made persistent
  (`DontDestroyOnLoad`); `InventoryService.GroundItems` snapshot list (`SyncGroundItems`).
- part 3 `6821f43` — HP carryover (`RunManager.CurrentHp`/`MaxHp`); round transitions become
  scene loads; `GameSceneBootstrap` → `CombatSceneBootstrap`; new `ShopSceneBootstrap`;
  `HeroStartingLoadout` made persistent + `RunStartedEvent`-driven.
- part 4 `8f89369` — shop-side components init on scene load; save/restore routes HP via
  `RunManager` and ground items via `InventoryService`.
- part 5 `1ba8aad` — scene surgery via `Editor/WS015SceneRefactor.cs`: created `Shop.unity`,
  moved the shop-UI subtree + services, relocated the persistent singletons into Boot.
- part 6 `6ac7b58` — +12 EditMode tests (SceneRouter, GroundItems, HP carryover); suite 176.
- part 7 — this summary + CLAUDE.md §2/§7/§12 + lessons L-026…L-028.

## Plan deviations

- **D1** `SynergyService` also promoted to a persistent Boot singleton (spec §4.6 named
  only `InventoryService`) — combat queries its cached resolution while Shop is unloaded.
- **D2** `HeroStartingLoadout` moved to Boot + made `RunStartedEvent`-driven (was a
  `Start()`-driven scene component — left in the Combat scene it would re-seed every round).
- **D3** `RunManager` owns the canonical HP; `SaveManager`/`RunRestoreController` no longer
  read a player `Health` component (the Shop scene, where saves fire, has no player).
- **D4** Phase events (`RoundStartedEvent`, `ShopPhaseStartedEvent`) re-homed to the scene
  bootstraps — published after the destination scene loads (L-027).
- **D5** `ShopController`/`ShopOverlayController` init from `ShopPhaseStartedEvent` on scene
  load, not from `RoundEndedEvent`; `ShopOverlayController`'s panel-toggle role is gone.
- **D6** Scene surgery done by a batch-mode editor script (L-028), not hand-authored YAML.
- **D7** Per-scene `EventSystem`/`Canvas`/`PauseMenu`/`SettingsMenu` for Shop (spec §4.8/§4.9
  offered persistent singletons as optional — kept per-scene, matching the project).
- **D8** Ground persistence uses a wholesale `InventoryService.SyncGroundItems` rather than
  per-item add/remove — avoids struct value-equality removal ambiguity and captures
  in-place lock toggles.

## Verification

- EditMode suite **176/176**; headless compile **zero errors / zero warnings** after every
  commit.
- Scene-validation pass (`WS015SceneRefactor.Validate`): **missingScripts=0** in all three
  scenes — the surgery broke no script references.

## Outstanding (designer)

- **§4.13 playtest** — the 30-round Combat↔Shop walkthrough, resume-into-Shop, pause in
  both scenes, ground items surviving transitions, death/Try-Again. An AI coder cannot
  run a live playtest.
- **C-1 (pre-existing, NOT a WS-015 regression):** the scene-level TMP migration was never
  run. `Combat.unity` + `Shop.unity` wire legacy `Text` into `TextMeshProUGUI` fields, so
  ~15 labels resolve null. Confirmed pre-existing — combat-only labels the surgery never
  touched are equally null. Fix: run the `MigrateTextToTMPro` editor tool on both scenes.
  WS-015's §0.1 pre-flight assumed C-1 was done; it was not.
- **`MainMixer.mixer`** still does not exist (`SettingsManager._mainMixer` null).

## Review

WS-015 is **code-complete, committed, and verified to the limit of headless tooling**
(compile clean, 176/176 EditMode, scene-validation clean). The structural goal — Combat and
Shop as distinct scenes with explicit cross-scene state — is met. The designer's §4.13
30-round playtest is the remaining acceptance gate.

---

# WS-014.D — Scene TMP migration + label re-wiring (C-1 fix)

Resolves the WS-014.A "C-1" issue flagged in the WS-015 review. Commit `59e3fdb`.

The scene labels were still legacy `UnityEngine.UI.Text`, while the controller scripts
had moved to `TextMeshProUGUI` fields (WS-012.4) — a type mismatch that resolved every
label reference to null. Two steps:

- `MigrateTextToTMPro` (the existing WS-012.4 tool) converts every legacy `Text` to
  `TextMeshProUGUI` in `Combat.unity`, `Shop.unity`, `MainMenu.unity` — zero legacy Text
  remain.
- New `Editor/WS014DRewireLabels.cs` re-wires the 17 `TextMeshProUGUI` label
  SerializeFields the migration could not recover (the migration only re-points refs that
  still pointed *at* a Text; these were already null, so it could not find them). Fields:
  `_headerLabel`, `_label`, `_nameLabel` ×5, `_priceLabel` ×5, `_rerollLabel` across
  ShopOverlayController, ShopController, ShopSlot, GoldDisplayController, ModeToggleButton,
  SellModal, RunEndOverlayController, RoundTimerUI.

Both editor scripts run headlessly via `-executeMethod`. Verified directly against the
scene YAML: 0 legacy Text; all 17 label fields point at confirmed `TextMeshProUGUI`
components. EditMode suite 176/176; headless compile clean.

Note: `WS015SceneRefactor.Validate()` writes its report to `Temp/`, which Unity churns
between batch runs — its file output proved unreliable; YAML inspection is the reliable
check.
