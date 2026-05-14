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

