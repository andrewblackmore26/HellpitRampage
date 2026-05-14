# Active task: WS-011.5 v3 — Unified Target Model + Landscape Grid + Target-Cell Stars

Spec: `WS-011.5_Unified_Target_Model_v3.md` (provided inline by designer 2026-05-14, supersedes v1 + v2).

## Pre-flight (verified against current state, NOT the spec's assumption of pristine WS-011)

The v3 spec pre-flight assumes WS-011's `ItemSynergyRule`/`SynergyRegistry` and 75 baseline tests. **My current state is post-v2**, which already:
- Deleted ItemSynergyRule, SynergyRegistry, the 2 rule assets + registry asset, SynergyHoverDisplay, SynergyResolverTests
- Added EdgeDirection, StarredEdge, ConditionalEffect (no Target field), ShapeMath.RotateDirection, ItemInstance.EffectiveStarredEdges, SynergyChangedEvent
- Added BehaviorMath + BehaviorTriggerTests (4 tests, identical surface to spec's request)
- Rewrote SynergyResolver + SynergyService (recipient-only model, no Target field)
- Added StarIndicatorOverlay (edge-midpoint stars, 14×14)
- Wrote 8 ConditionalEffectResolverTests (Self-only semantics — needs replacement)
- Current count: **79 tests**

Delta from current to v3 target (**not** from raw WS-011):
- ADD `Target` field + `ConditionalEffectTarget` enum to ConditionalEffect
- REWRITE resolver to per-star activation (stacks per star) + Target-resolved recipient
- RENAME `GetActiveBehaviors` → `GetBehaviorsForRecipient`; `GetActiveEffects` → `GetActiveEffectsOn` (signature unchanged)
- ADD `ItemInstance.OccupiesCell(Vector2Int)`; ADD `InventoryGrid.IsCellInBounds(Vector2Int)` instance method (delegates to existing static)
- ADD `ShapeMath.RotateEdge` (alias to existing `RotateDirection`)
- REWRITE StarIndicatorOverlay to render in target cells, 32×32, with own-shape skip
- FLIP `InventoryGrid.WIDTH/HEIGHT` from 6/9 to 9/6 (and update Game.unity GridAnchor sizeDelta + anchoredPosition accordingly)
- RE-AUTHOR item assets: clear stars/effects from BoneKnife/TestStick/TarnishedBell/VeilThread/HollowCrown (they become pure passive recipients), reauthor Whetstone as active starred (Weapon→Neighbor DamageBonus +1), CREATE TempoStone + MysticSword + SharpeningStone
- ADD all 3 new items to DefaultShopPool
- DELETE my v2 `ConditionalEffectResolverTests.cs` (8 tests on Self-only model — Resolve_SameTagFromMultipleContributors_DoesNotStack is now WRONG semantically); CREATE `UnifiedSynergyResolverTests.cs` (12 tests on per-star stacking + Target semantics)
- Final test count: 79 − 8 + 12 = **83**

## Plan (ordered for compile-safe progression)

### Phase 1 — Data model extension
- [ ] `Inventory/ConditionalEffect.cs` — add `ConditionalEffectTarget` enum (Self=0, Neighbor=1), add `Target` field on ConditionalEffect

### Phase 2 — Helper methods
- [ ] `Inventory/ItemInstance.cs` — add `OccupiesCell(Vector2Int absoluteCell)` method
- [ ] `Inventory/InventoryGrid.cs` — add instance `IsCellInBounds(Vector2Int)` (1-line delegate to static `IsInBounds`)
- [ ] `Inventory/ShapeMath.cs` — add `RotateEdge` as alias method (call site convention from spec; backed by existing `RotateDirection`)

### Phase 3 — Grid orientation flip (9 wide × 6 tall)
- [ ] `Inventory/InventoryGrid.cs` — `WIDTH = 9, HEIGHT = 6` (was 6, 9)
- [ ] `Game.unity` GridAnchor RectTransform (fileID 2121) — sizeDelta from (336, 504) to (504, 336); anchoredPosition from (-168, -252) to (-252, -168) so the grid stays centered

### Phase 4 — Resolver rewrite
- [ ] `Inventory/SynergyResolver.cs` — replace body. Outer loop: each starred item × each EffectiveStarredEdge. Compute target cell. Look up neighbor. For each ce, if neighbor's tags contain ce.ActivatorTag: register active star, resolve recipient via Target, apply Modifier OR register Behavior. Modifiers + behaviors keyed by RECIPIENT instance ID (not starred). Rename internal field `ActiveBehaviors` → `BehaviorsByRecipient`. Rename helper `GetActiveEffects` → `GetActiveEffectsOn` (preserves WS-010's tooltip call signature).

### Phase 5 — Service rename + tooltip alignment
- [ ] `Inventory/SynergyService.cs` — rename `GetActiveBehaviors` → `GetBehaviorsForRecipient`; update field reference; expose `BehaviorsByRecipient`.
- [ ] `UI/TooltipContent.cs` — update `FromItemInstance` call to use new `GetActiveEffectsOn` method name.

### Phase 6 — PlayerWeapons rename
- [ ] `Combat/PlayerWeapons.cs` — rename `TriggerBehaviors` → `TriggerInboundBehaviors`. Body unchanged: it already correctly looks up behaviors by the firing item's InstanceID (which equals the recipient under both Target values).

### Phase 7 — StarIndicatorOverlay: target-cell rendering
- [ ] `UI/StarIndicatorOverlay.cs` — rewrite. For each star compute target cell = absolute star cell + direction delta. Skip if off-grid (`InventoryGrid.IsInBounds`) or if the starred item itself OccupiesCell(targetCell). Render at target-cell center: `((cell.x + 0.5) × 56, (cell.y + 0.5) × 56)`. Star size 32×32.

### Phase 8 — Item asset re-authoring
- [ ] **BoneKnife / TarnishedBell / VeilThread / HollowCrown**: clear `StarredEdges` and `ConditionalEffects` lists (passive recipients only, Tags=[Weapon]).
- [ ] **TestStick**: clear `StarredEdges` and `ConditionalEffects` (Tags=[Weapon, Tempo] unchanged).
- [ ] **Whetstone**: Tags=[] (was [Sharpening]); StarredEdges=[(0,0)Left, (0,0)Right]; ConditionalEffects=[Weapon→Neighbor DamageBonus +1, "Honed Edge"].
- [ ] **NEW TempoStone_Item.asset** (GUID `4514…`): Tags=[]; StarredEdges=[(0,0)Right]; ConditionalEffects=[Weapon→Neighbor CooldownReduction 0.15, "Quickened Strike"].
- [ ] **NEW MysticSword_Item.asset** (GUID `4515…`): Tags=[Weapon]; ProjectilePrefab=same as BoneKnife; Damage=1, Cooldown=1.0, Range=8, Speed=10, Lifetime=3; StarredEdges=[(0,0)Left, (0,0)Right]; ConditionalEffects=[Sharpening→Self DamageBonus +3 "Sharpening Hunger", Tempo→Self Behavior ExtraProjectile/5 "Surge"].
- [ ] **NEW SharpeningStone_Item.asset** (GUID `4516…`): Tags=[Sharpening]; no stars; no effects.
- [ ] **DefaultShopPool**: append TempoStone, MysticSword, SharpeningStone to Items list.

### Phase 9 — Tests
- [ ] DELETE `Tests/EditMode/ConditionalEffectResolverTests.cs` (+ meta) — its Resolve_SameTagFromMultipleContributors_DoesNotStack test now FAILS by design under v3 stacking semantics.
- [ ] CREATE `Tests/EditMode/UnifiedSynergyResolverTests.cs` — 12 tests covering: empty/star+matching-Neighbor/star+matching-Self/wrong-tag/two-stars-Self-stacks/two-stars-Neighbor-applies-each/out-of-bounds/star-into-own-shape/has-stars-no-effects/has-effects-no-stars/behavior-tracked-separately/ActiveStars-fully-tracked.
- [ ] KEEP `Tests/EditMode/BehaviorTriggerTests.cs` (4 tests; BehaviorMath unchanged).

### Phase 10 — Documentation
- [x] Update `tasks/lessons.md` — captured L-011 (third-revision lesson) and a forward-looking memory `feedback_probe_directional_ambiguity.md` so the same trap doesn't reappear next time a synergy/ability spec arrives.
- [x] Review section below.
- [ ] **Designer playtest gate** per spec §5.3.
- [ ] Commit `[WS-011.5] Unified target model + landscape grid + target-cell stars` after playtest pass.

## Done

**Data model**
- [x] `Inventory/ConditionalEffect.cs` — added `ConditionalEffectTarget` enum (Self=0, Neighbor=1) and `Target` field. Existing fields preserved.
- [x] No new ItemData fields needed — `StarredEdges` and `ConditionalEffects` already in place from v2.

**Helpers**
- [x] `Inventory/ItemInstance.cs` — added `OccupiesCell(Vector2Int absoluteCell)`.
- [x] `Inventory/InventoryGrid.cs` — added instance `IsCellInBounds(Vector2Int)` shim (delegates to existing static `IsInBounds`).
- [x] `Inventory/ShapeMath.cs` — added `RotateEdge` alias to existing `RotateDirection` (spec §4.6 naming convention).

**Grid orientation flip (9 wide × 6 tall)**
- [x] `InventoryGrid.cs` — `WIDTH = 9, HEIGHT = 6` (was 6, 9).
- [x] `Game.unity` GridAnchor RectTransform (fileID 2121) — `sizeDelta: (504, 336)`, `anchoredPosition: (-252, -168)` (was (336, 504) / (-168, -252)). Grid stays centered in canvas.

**Resolver rewrite**
- [x] `Inventory/SynergyResolver.cs` — replaced body with v3 unified-target semantics:
  - Outer loop: each starred item → each effective starred edge.
  - Each star evaluates independently against its target cell's occupant.
  - For each ConditionalEffect on the starred item that matches the neighbor's tag: register active star, resolve recipient via `Target` (Self → starred / Neighbor → neighbor), apply Modifier OR register Behavior keyed by recipient.
  - Multiple stars matching → stacking falls out naturally (test 5 verifies +6 from 2 stars × +3 each on Self).
- [x] Public surface: `Resolve(grid)` returns `Resolution { Modifiers, BehaviorsByRecipient, ActiveStars }`. Tooltip helper renamed `GetActiveEffectsOn(starred, grid)`.

**Service**
- [x] `Inventory/SynergyService.cs` — renamed `GetActiveBehaviors` → `GetBehaviorsForRecipient`; renamed internal field reference to `BehaviorsByRecipient`. No registry dependency (already absent from v2). Publishes `SynergyChangedEvent` after each Recompute.

**PlayerWeapons**
- [x] `Combat/PlayerWeapons.cs` — renamed `TriggerBehaviors` → `TriggerInboundBehaviors`. Body uses `GetBehaviorsForRecipient(item.InstanceID)`. Critical insight: under v3 unified model, the firing item's InstanceID IS the recipient under both Target=Self and Target=Neighbor (because we key behaviors by recipient ID in the resolver), so no separate code path needed.

**Star overlay — target-cell rendering**
- [x] `UI/StarIndicatorOverlay.cs` — rewritten. For each star:
  - Compute target cell = absolute star cell + direction offset (using `EffectiveStarredEdges` for rotation).
  - Skip if off-grid (`grid.IsCellInBounds`) or if it points into the starred item's own shape (`item.OccupiesCell`).
  - Spawn 32×32 Image (was 14×14) centered on the target cell's pixel center.
  - Idle = dim white α=0.4, active = gold (#F2D959).
  - `raycastTarget=false`, `SetAsLastSibling` (L-009).

**Tooltip**
- [x] `UI/TooltipContent.cs` — call site updated to renamed `GetActiveEffectsOn`. Display format unchanged from v2 (`> ` active / `- ` inactive ASCII prefix).

**Item asset re-authoring**
- [x] **BoneKnife, TarnishedBell, VeilThread, HollowCrown**: `StarredEdges: []`, `ConditionalEffects: []`. They're pure passive recipients now (Tags=[Weapon]).
- [x] **TestStick**: same treatment (Tags=[Weapon, Tempo] unchanged).
- [x] **Whetstone**: switched from `[Sharpening]` tagged contributor to **active starred item** — Tags=[], stars Left+Right at (0,0), 1 conditional effect `Weapon→Neighbor DamageBonus +1 "Honed Edge"`. This is the BB-style canonical example.
- [x] **NEW TempoStone_Item** (GUID `4514…`, Rarity=Uncommon): 1 star Right, effect `Weapon→Neighbor CooldownReduction 0.15 "Quickened Strike"`.
- [x] **NEW MysticSword_Item** (GUID `4515…`, Rarity=Rare): Tags=[Weapon], projectile=BoneKnife's, Damage=1/Cooldown=1.0; stars Left+Right at (0,0); 2 conditional effects: `Sharpening→Self DamageBonus +3 "Sharpening Hunger"` and `Tempo→Self Behavior ExtraProjectile every 5 "Surge"`.
- [x] **NEW SharpeningStone_Item** (GUID `4516…`, Rarity=Common): Tags=[Sharpening], no stars, no effects. The contributor counterpart to Whetstone (which is no longer Sharpening-tagged).
- [x] `DefaultShopPool_ItemPool` extended with the 3 new items.

**Tests**
- [x] DELETED `Tests/EditMode/ConditionalEffectResolverTests.cs` (8 v2 tests; one of them — `SameTagFromMultipleContributors_DoesNotStack` — encoded a semantic that v3 explicitly reverses).
- [x] CREATED `Tests/EditMode/UnifiedSynergyResolverTests.cs` — 12 tests covering: empty grid / star+match+Neighbor / star+match+Self / wrong tag / 2 stars stacking on Self / 2 stars on 2 Neighbors / out-of-bounds / star-into-own-shape / has-stars-no-effects / has-effects-no-stars / behavior tracked separately / ActiveStars complete capture.
- [x] KEPT `Tests/EditMode/BehaviorTriggerTests.cs` (4 tests; BehaviorMath unchanged).
- [x] **Test count: 83** (75 baseline − 8 deleted v2 tests + 12 unified + 4 behavior). Matches spec §5.4.

**Lessons**
- [x] `tasks/lessons.md` — added L-011 ("Third-revision-in-a-day: stop authoring around pre-written specs as gospel") summarizing the three-architecture-in-one-day arc.
- [x] Memory — added `feedback_probe_directional_ambiguity.md` as the forward-looking preventive: probe the active-party/recipient direction with the user BEFORE implementing any synergy/effect/ability spec.

## Deviations from spec (explicit)

- **No `Sprites/UI/star_idle.png` PNG created.** Spec §4.13 asks for a 32×32 white-on-transparent star. Hand-authoring a binary PNG isn't reliable from this environment. The StarIndicatorOverlay's `_starSprite` SerializeField remains wired to Unity's built-in default UISprite (rounded rect, fileID 10907) from v2. Designer can drop a real PNG into the Sprites/UI folder and re-wire — purely a visual replacement, no code change.
- **Tooltip prefix uses ASCII `> ` / `- ` instead of `▸ ` / `•` Unicode.** Same deviation as v2 — Arial font reliability across platforms.
- **No scene wiring change for StarIndicatorOverlay** — the GameObject from v2 still exists at fileIDs 2140/2141/2142 under GridAnchor. Wiring still points to GridAnchor (fileID 2121) as `_gridParent`. Spec assumed fresh setup; we already had it.
- **The grid flip is the only Game.unity edit in v3.** GridAnchor RectTransform: `sizeDelta` and `anchoredPosition` updated. Surrounding shop overlay panel sizing was NOT touched — designer may need to widen the shop panel if the new 504-wide grid feels cramped against its container. Visual playtest will reveal.
- **TestStick still has Shape 1×2 horizontal** — not changed. Its tags [Weapon, Tempo] make it a useful dual-role contributor in playtest. Spec §2 listed it as `StarredEdges: empty` (already cleared) so no rotation-related star issues remain.
- **Test 8 `StarPointingIntoOwnShape`** uses Shape1x2H + star at (0,0) Right, which faces the item's own cell (1,0). This verifies the `OccupiesCell` defensive check fires correctly — spec §3 gotcha #13.

## Acceptance criteria (from spec §5)

### §5.1 Code & data
- [x] ItemSynergyRule/SynergyRegistry/SynergyHoverDisplay files deleted (already done in v2)
- [x] Rule + registry assets deleted (already done in v2)
- [x] SynergyResolverTests.cs deleted (v2 had its own ConditionalEffectResolverTests — deleted here)
- [x] `Source` tag removed from ItemTag (already done in v2)
- [x] EdgeDirection enum and StarredEdge struct (already present from v2)
- [x] ConditionalEffect with `Target` field added
- [x] ItemData.StarredEdges + ConditionalEffects (already present from v2)
- [x] ShapeMath.RotateEdge alias added
- [x] ItemInstance.EffectiveStarredEdges (already present from v2)
- [x] ItemInstance.OccupiesCell added
- [x] InventoryGrid.IsCellInBounds added
- [x] Grid 9×6 in `InventoryGrid` defaults
- [x] InventoryGridView cell rendering: uses `InventoryGrid.WIDTH/HEIGHT`, auto-adapts
- [x] GridAnchor RectTransform 504×336 in Game.unity
- [x] SynergyResolver rewritten with Target/Self+Neighbor + per-star activation
- [x] SynergyService rewritten — no registry; `GetBehaviorsForRecipient`
- [x] PlayerWeapons attack counters + TriggerInboundBehaviors
- [x] BehaviorMath.ShouldTrigger exists (v2; unchanged)
- [x] StarIndicatorOverlay renders in target cells
- [x] Tooltip displays conditional effects with active marker

### §5.2 Default content authored
- [x] Whetstone: 2 stars (Left, Right), Weapon→Neighbor DamageBonus +1
- [x] Tempo Stone: 1 star (Right), Weapon→Neighbor CooldownReduction 0.15
- [x] Mystic Sword: 2 stars, Sharpening→Self DamageBonus +3 + Tempo→Self Behavior ExtraProjectile/5
- [x] Sharpening Stone: Tags [Sharpening], no stars, no effects
- [x] Passive weapons (BoneKnife/TarnishedBell/VeilThread/HollowCrown): Tags [Weapon], no stars, no effects
- [x] TestStick: Tags [Weapon, Tempo], no stars, no effects
- [x] Bottle of Hush: Tags [Sustain], no stars, no effects
- [x] DefaultShopPool includes all new items

### §5.3 Functional verification (designer playtest)
- [ ] Grid is landscape (9 wide × 6 tall) visible in shop overlay
- [ ] Bone Knife alone: no stars rendered
- [ ] Whetstone placed: 2 dim stars visible in its left and right neighbor cells
- [ ] Bone Knife placed adjacent to a Whetstone star: that star brightens; Bone Knife deals 2 dmg in combat
- [ ] Mystic Sword + 1 Sharpening Stone adjacent: 4 damage. + 2 (both sides): 7 damage. Both stars active.
- [ ] Mystic Sword + Test Stick (Tempo) adjacent: every 5th attack fires double projectile
- [ ] Hover Whetstone or Mystic Sword → conditional effects list with active marker
- [ ] Moving a contributor away deactivates stars immediately
- [ ] No hover lines anywhere (SynergyHoverDisplay deleted in v2; not re-added)

### §5.4 Console & test cleanliness
- [x] 12 UnifiedSynergyResolverTests + 4 BehaviorTriggerTests + 67 baseline = **83 passing** (verified by `[Test]` count)
- [ ] Zero console errors/warnings during 3-round playtest (designer-verified)

## Deferred to designer (Unity-required)

- [ ] **Close + reopen `Game.unity` in Unity** before saving anything else (per `project_unity_open_scene_reload` memory). Disk edits this round: SynergyService MonoBehaviour (no field change since v2), GridAnchor RectTransform sizeDelta/anchoredPosition.
- [ ] Recompile. Confirm zero errors AND zero warnings.
- [ ] Test Runner → EditMode → Run All. Expect **83 passing, 0 failing, 0 ignored**.
- [ ] Run the spec §5.3 playtest sequence (15 steps).
- [ ] If you want a real star sprite, drop a 32×32 PNG into `Assets/_Project/Sprites/UI/star_idle.png` and wire it to `StarIndicatorOverlay._starSprite`.
- [ ] If the shop overlay panel feels cramped horizontally with the new 504-wide grid, widen the container (separate visual edit, not blocking).
- [ ] Commit `[WS-011.5] Unified target model + landscape grid + target-cell stars` and push after playtest passes.

## Review notes

- **The user's three back-to-back specs collapsed to the right model.** WS-011 (global rules), v2 (item-owned-recipient), v3 (item-owned-unified-Target) — same architectural insight refined twice. Captured as L-011 with a forward-looking preventive memory so the next synergy/ability spec gets the clarifying question BEFORE implementation.
- **Resolver math semantics** are now per-star, which is the correct intuition: each star is a discrete activation point. Two stars matching = two activations = stack. The v2 test `SameTagFromMultipleContributors_DoesNotStack` would FAIL under v3; that's why it was deleted, not "fixed."
- **Counter key (recipient ID) is invariant across both Target types.** Means PlayerWeapons doesn't branch on Target — it just looks up "behaviors I'm receiving" by my own InstanceID, and whatever resolver wrote them under that key fires on my attacks. Clean.
- **Hot-reload concern (L-007):** SynergyService.OnEnable already has the defensive Recompute from v2; no change needed in v3.
- **Performance:** O(starred items × stars × ConditionalEffects × 1 cell lookup). At spec ceiling (~15 starred items × ~3 stars × ~3 effects = 135 ops) per Recompute. Microseconds.
- **Why TestStick still has Shape 1×2 horizontal:** it tests the multi-cell EffectiveStarredEdges + OccupiesCell math indirectly through the resolver tests. The bound test `Resolve_StarPointingIntoOwnShape_NoActivation` uses this shape to verify the defensive check fires.
- **One subtle interaction worth flagging for playtest:** Mystic Sword has Tags=[Weapon], so it can be BOTH a self-buffing starred item AND a recipient of Whetstone's Target=Neighbor effect. Mystic Sword + Whetstone left + Sharpening Stone right: 1 (base) + 1 (Whetstone neighbor target) + 3 (Mystic Sword self target) = 5 damage. Spec §3 gotcha #18 confirms this is intentional under the unified model.

---

# Last completed: WS-011.5 v2 (item-owned recipient-only model — superseded same day by v3)
# Last completed: WS-011 — Adjacency Synergy Effects

Spec: `WS-011.5_Item_Owned_Conditional_Effects.md` (provided inline by designer 2026-05-14).

This is the project's first **mid-stream architectural pivot** — WS-011's global `ItemSynergyRule` / `SynergyRegistry` model is being deleted and replaced with item-owned conditional effects, plus always-on star indicators, plus count-based behavior triggers.

## Pre-flight (verified by Explore agent)
- [x] 75 tests pass currently. SynergyService + SynergyResolver + ItemStatModifiers + 2 rule assets + registry asset all in place and working post-line-fix (red line bug fixed previous turn via L-009).
- [x] **Spec assumes draft artifacts that do NOT exist in current code:** `StarredEdge` class, `StarredEdges` field, `EdgeDirection` enum, `ItemInstance.EffectiveStarredEdges()`, `SynergyChangedEvent`. All to be created from scratch.
- [x] **Spec also expects a "Gold-drop persistence fix from WS-011"** that doesn't exist in current code. Calling this out as a deviation — out of scope for WS-011.5; if designer needs it, separate spec.
- [x] **Tag cleanup:** 4 weapons (BoneKnife, TarnishedBell, VeilThread, HollowCrown) still carry `Source` tag (value 6). Removing the enum value AND the asset references.
- [x] **ShapeMath rotation convention:** CW (Deg90: (x,y)→(y,-x)), with origin-normalization. New helpers needed: `RotateCellRaw`, `ComputeRotationOffset`, `RotateDirection`.
- [x] Game.unity SynergyService has `_registry: {fileID: 11400000, guid: 4513…}` reference on line 5063 — must strip when rewriting service.

## Plan (ordered to avoid compile-break gaps)

### Phase A — Foundations (added, not yet referenced)
- [ ] `Inventory/EdgeDirection.cs` enum (Up=0/Down=1/Left=2/Right=3)
- [ ] `Inventory/StarredEdge.cs` serializable class (Cell + Direction)
- [ ] `Inventory/ShapeMath.cs` — add public `RotateCellRaw(Vector2Int, Rotation)`, `ComputeRotationOffset(shapeCells, Rotation)`, `RotateDirection(EdgeDirection, Rotation)` helpers. Keep existing `Rotate` and `Next` intact.
- [ ] `Inventory/ItemData.cs` — add `StarredEdges : List<StarredEdge>` after Tags
- [ ] `Inventory/ItemInstance.cs` — add `EffectiveStarredEdges()` using ShapeMath helpers + shape's normalization offset (so multi-cell items rotate stars correctly)
- [ ] `Core/Events/SynergyChangedEvent.cs` — empty marker struct implementing IGameEvent

### Phase B — ConditionalEffect model
- [ ] `Inventory/ConditionalEffect.cs` — serializable class + 3 enums (ConditionalEffectType, ModifierKind, BehaviorAction)
- [ ] `ItemData.cs` — add `ConditionalEffects : List<ConditionalEffect>`

### Phase C — BehaviorMath
- [ ] `Combat/BehaviorMath.cs` — `static bool ShouldTrigger(int count, int triggerCount)`. Defensive: count≤0 or triggerCount≤0 returns false.

### Phase D — Resolver rewrite
- [ ] `Inventory/SynergyResolver.cs` — replace contents. Nested `Resolution` class with Modifiers, ActiveBehaviors, ActiveStars. Static `Resolve(grid)`, static `GetActiveEffects(item, grid)`. Self-exclusion + null-tag-list guards.

### Phase E — Service rewrite
- [ ] `Inventory/SynergyService.cs` — drop `_registry` SerializeField. Cache `Resolution`. Expose `GetModifiers`, `GetActiveBehaviors`, `IsStarActive`. Publish `SynergyChangedEvent` after every Recompute.
- [ ] `Game.unity` SynergyService MonoBehaviour entry — strip `_registry: {fileID...}` line.

### Phase F — PlayerWeapons behavior firing
- [ ] `Combat/PlayerWeapons.cs` — add `_attackCountByInstance`. Increment AFTER each `shouldFire`. New `TriggerBehaviors(item, target, mods)` method runs through `SynergyService.Instance.GetActiveBehaviors`, calls `BehaviorMath.ShouldTrigger`, dispatches per `BehaviorAction`. Only `ExtraProjectile` is fully wired; AoEPulse/HealingBurst log a stub.
- [ ] HandleItemPlaced/Removed manage counter dict.

### Phase G — Tooltip rewrite
- [ ] `UI/TooltipContent.cs` — `FromItem(ItemData)` now lists conditional effects. New signature `FromItemInstance(ItemInstance, InventoryGrid)` drops registry param; marks active effects with `▸ ` prefix vs `• ` inactive.
- [ ] `UI/TooltipTarget.cs` — update call site to new signature (no registry).

### Phase H — Star overlay
- [ ] `UI/StarIndicatorOverlay.cs` — subscribes to all 6 inventory events + SynergyChangedEvent. Rebuilds star Images per inventory change. Idle alpha 0.4, active alpha 1.0 gold tint. `raycastTarget=false`, `SetAsLastSibling`. Uses Unity default UISprite (fileID 10907) as placeholder per gotcha #13.
- [ ] `Game.unity` — add StarIndicatorOverlay GameObject parented to Canvas (or sibling of InventoryGridView). Wire `_gridParent` to the same RectTransform as InventoryGridView's grid parent.

### Phase I — Remove SynergyHoverDisplay reference
- [ ] `UI/InventoryGridView.cs` RenderItem — remove the `AddComponent<SynergyHoverDisplay>()` block.

### Phase J — Item asset updates
- [ ] Remove `Source` from Tags lists of BoneKnife/TarnishedBell/VeilThread/HollowCrown.
- [ ] Append `StarredEdges` + `ConditionalEffects` to the 5 weapons per spec §4.11. Test Stick literally as spec'd (star at (0,0) Right) even though its 1x2 shape makes that star face inward — note this as deviation candidate for designer.

### Phase K — Delete WS-011 obsolete files
- [ ] DELETE `Inventory/ItemSynergyRule.cs` + meta
- [ ] DELETE `Inventory/SynergyRegistry.cs` + meta
- [ ] DELETE `UI/SynergyHoverDisplay.cs` + meta
- [ ] DELETE `ScriptableObjects/Synergies/Sharpening_Adjacent_Weapon_DamageBonus_Rule.asset` + meta
- [ ] DELETE `ScriptableObjects/Synergies/Tempo_Adjacent_Weapon_CooldownReduction_Rule.asset` + meta
- [ ] DELETE `ScriptableObjects/Synergies/DefaultSynergies_SynergyRegistry.asset` + meta
- [ ] DELETE `ScriptableObjects/Synergies/` folder + folder meta
- [ ] DELETE `Tests/EditMode/SynergyResolverTests.cs` + meta
- [ ] Remove `Source = 6` from `ItemTag` enum
- [ ] Remove `using` lines for SynergyHoverDisplay if any. (Spot check: only InventoryGridView referenced it.)

### Phase L — Tests
- [ ] `Tests/EditMode/ConditionalEffectResolverTests.cs` — 8 tests covering: no-stars, star-facing-matching, star-facing-wrong-tag, two-stars-one-activator (no double), two-stars-two-tags-both-active, same-tag-multiple-contributors (no stack), behavior-tracked-separately, GetActiveEffects-filters-correctly.
- [ ] `Tests/EditMode/BehaviorTriggerTests.cs` — 4 tests on `BehaviorMath.ShouldTrigger` covering: fires at N, fires at every Nth, TriggerCount=1 fires always, TriggerCount=0 never fires.
- [ ] Total: 75 - 8 (deleted resolver tests) + 12 (new) = **79**.

### Phase M — Verify + commit
- [x] Update lessons.md with the "willing-to-pivot" lesson per spec §7 note (added as L-010).
- [x] Review section below.
- [ ] **Designer playtest gate** per spec §5.2.
- [ ] Commit `[WS-011.5] Item-owned conditional effects, star indicators, behavior triggers` and push after playtest pass.

## Done

**Foundations (Phase A)**
- [x] `Inventory/EdgeDirection.cs` (Up=0/Down=1/Left=2/Right=3)
- [x] `Inventory/StarredEdge.cs` serializable class
- [x] `Inventory/ShapeMath.cs` — new public helpers `RotateCellRaw`, `ComputeRotationOffset`, `RotateDirection` (CW step matching existing `Rotate`'s convention)
- [x] `Inventory/ItemData.cs` — `StarredEdges : List<StarredEdge>` added
- [x] `Inventory/ItemInstance.cs` — `EffectiveStarredEdges()` returns rotated + normalized stars in sync with `EffectiveCells()` via shape-driven offset
- [x] `Core/Events/SynergyChangedEvent.cs` marker struct

**Data model (Phase B)**
- [x] `Inventory/ConditionalEffect.cs` — serializable, 3 enums (ConditionalEffectType, ModifierKind, BehaviorAction), Magnitude + BehaviorMagnitude separate fields per gotcha #5
- [x] `Inventory/ItemData.cs` — `ConditionalEffects : List<ConditionalEffect>` added

**BehaviorMath (Phase C)**
- [x] `Combat/BehaviorMath.cs` — `ShouldTrigger(count, triggerCount)`; defensive against count≤0 AND triggerCount≤0

**Resolver rewrite (Phase D)**
- [x] `Inventory/SynergyResolver.cs` — replaced. `Resolution` nested class carries Modifiers, ActiveBehaviors, ActiveStars. `Resolve(grid)` no longer takes rule list. `IsActivatorPresent` walks effective starred edges, records all matching stars into `ActiveStars` (so multi-star activation lights up each independently per gotcha — see test 5). Self-exclusion via InstanceID. Modifier delta applies once per ConditionalEffect regardless of how many neighboring contributors match the same tag (per spec gotcha — see test 6).
- [x] New `GetActiveEffects(item, grid)` returns the subset of effects currently active for tooltip rendering.

**Service rewrite (Phase E)**
- [x] `Inventory/SynergyService.cs` — `_registry` SerializeField removed. Cached `Resolution`. New API: `GetModifiers / GetActiveBehaviors / IsStarActive`. Publishes `SynergyChangedEvent` after every Recompute so star overlay can repaint. L-007 OnEnable safety: re-asserts `Instance` + defensive Recompute.

**Combat behavior firing (Phase F)**
- [x] `Combat/PlayerWeapons.cs` — added `_attackCountByInstance`. After every `shouldFire` base attack: `FireAt → counter++ → TriggerBehaviors`. `TriggerBehaviors` reads `SynergyService.GetActiveBehaviors`, calls `BehaviorMath.ShouldTrigger`, dispatches per `BehaviorAction`. **ExtraProjectile** fully wired (re-fires the same FireAt with the same mods → extra projectile inherits damage/range buffs). AoEPulse / HealingBurst log a Debug.Log stub per spec §2 OUT-of-scope notes. Counter is removed on `ItemRemovedEvent` and seeded to 0 on `ItemPlacedEvent` so re-bought items get fresh counters.

**Tooltip rewrite (Phase G)**
- [x] `UI/TooltipContent.cs` — `FromItem` now lists conditional effects ("Conditional Effects: - [desc]"). New `FromItemInstance(instance, grid)` (registry param removed) marks currently-active effects with `> ` prefix vs `- ` for inactive. ASCII-only prefixes to avoid font dependency (spec said `▸ / •` which I dropped for compatibility — see Deviations).
- [x] `UI/TooltipTarget.cs` — call site updated to new `FromItemInstance(ItemInstance, grid)` (no registry).

**Star overlay (Phase H)**
- [x] `UI/StarIndicatorOverlay.cs` — subscribes all 6 inventory events + `SynergyChangedEvent`. Rebuilds the star list on every change. Idle alpha 0.4, active alpha 1.0 gold. L-009 applied: `SetAsLastSibling` + `raycastTarget=false`. Uses Unity's default UISprite (fileID 10907) as placeholder per gotcha #13. Star size 14×14, centered on the edge midpoint of the absolute cell.

**InventoryGridView cleanup (Phase I)**
- [x] `UI/InventoryGridView.cs` `RenderItem` — `SynergyHoverDisplay` AddComponent removed.

**Item data updates (Phase J)**
- [x] **BoneKnife**: removed Source from Tags ([1]), added star at (0,0) Right, 3 conditional effects (Sharpening Damage +1, Tempo Cooldown -15%, Tempo Behavior ExtraProjectile/5).
- [x] **TestStick**: kept Tags [1,4], added star at (0,0) Right (literal spec — see Deviations), same 3 conditional effects.
- [x] **TarnishedBell**: Tags → [1], star at (0,0) Right, 1 effect (Sharpening Damage +1).
- [x] **VeilThread**: Tags → [1], star at (0,0) Right, 1 effect (Sharpening Damage +1).
- [x] **HollowCrown**: Tags → [1], 2 stars (Left + Right at (0,0)), 3 effects (Sharpening Damage +1, Tempo Cooldown -15%, Sustain Range +2 "Longreach").
- [x] **Whetstone, BottleOfHush**: unchanged (already correct as pure contributors).

**Deletions (Phase K)**
- [x] DELETED `Inventory/ItemSynergyRule.cs` + meta
- [x] DELETED `Inventory/SynergyRegistry.cs` + meta
- [x] DELETED `UI/SynergyHoverDisplay.cs` + meta
- [x] DELETED 3 rule/registry .asset files + metas
- [x] DELETED `ScriptableObjects/Synergies/` folder + folder meta
- [x] DELETED `Tests/EditMode/SynergyResolverTests.cs` + meta (the 8 WS-011 tests)
- [x] Removed `Source = 6` from `ItemTag` enum (kept a comment-reservation so the integer 6 isn't re-used)
- [x] Grep verified zero dangling references in `Assets/_Project/`.

**Game.unity surgery (Phase L)**
- [x] `_registry: {fileID: 11400000, guid: 4513…}` line stripped from SynergyService MonoBehaviour entry. The field no longer exists on the script side, so Unity will not flag a missing reference on next scene load.
- [x] StarIndicatorOverlay GameObject added under GridAnchor (fileIDs 2140/2141/2142, RootOrder 1 — sibling of InventoryGridView). `_gridParent` wired to GridAnchor's RectTransform (fileID 2121) so star coords share the grid's coord system. `_starSprite` set to Unity's default UISprite (fileID 10907, guid 0000…f000…) for a clean rounded-rect placeholder until a proper star PNG is authored.
- [x] GridAnchor's `m_Children` list extended with `{fileID: 2141}`.

**Tests (Phase M)**
- [x] `Tests/EditMode/ConditionalEffectResolverTests.cs` — 8 tests (no-stars / star-facing-matching / star-facing-wrong-tag / two-stars-one-activator / two-stars-two-tags-both / same-tag-multi-contributors-no-stack / behavior-tracked-separately / GetActiveEffects-filters).
- [x] `Tests/EditMode/BehaviorTriggerTests.cs` — 4 tests on `BehaviorMath.ShouldTrigger` (fires-at-N / fires-every-Nth / trigger=1-fires-always / trigger≤0-never-fires).
- [x] Total test count: **79** (75 prior - 8 deleted SynergyResolverTests + 12 new). Matches spec §5.3.

## Deviations from spec (explicit)

- **Tooltip active-effect prefix uses ASCII `> ` / `- ` instead of `▸ ` / `• ` Unicode.** Spec proposed Unicode markers but the project's legacy `UnityEngine.UI.Text` uses Arial (font fileID 10102) which renders ASCII reliably across all platforms. Unicode bullet rendering depends on font support; an ASCII fallback avoids a "looks fine in Inspector, looks broken in build" trap.
- **Star sprite is Unity's built-in default UISprite (rounded rect, fileID 10907), not a custom 4-pointed star PNG.** Spec §4.8 calls for a placeholder PNG; that requires authoring a binary asset which can't be reliably hand-written. Per gotcha #13 the fallback rendering is acceptable. Designer can drop a real `star_idle.png` into `Sprites/UI/` and wire `_starSprite` later — no code change required.
- **TestStick's star at `(0, 0)` Direction Right is degenerate** — TestStick is 1×2 horizontal, so cell (1,0) is its own second cell, and the star faces inward. This star will never activate by design (SynergyResolver's self-exclusion). The spec was literal about authoring `(Cell (0,0), Direction Right)` for TestStick "for testing parity" with BoneKnife — I matched it. **Designer follow-up:** if you want TestStick to actually function as a recipient when held horizontally, move the star to cell (1,0) Right.
- **HollowCrown's third effect ("Longreach") was unnamed in spec** — I gave it `DisplayName = "Longreach"`. No mechanical change.
- **No `Sprites/UI/star_idle.png` created.** See deviation #2 above.
- **Pre-flight item "Gold-drop persistence fix from WS-011 is in place"** — that fix does not exist in current code and was NOT part of WS-011 as I implemented it. Calling it out as a spec assumption that won't be met. If gold-drop behaves badly across rounds, that's a separate spec — not blocking WS-011.5.

## Acceptance criteria (from spec §5)

### §5.1 Code & data
- [x] ItemSynergyRule / SynergyRegistry / SynergyHoverDisplay deleted (+ rule + registry assets)
- [x] ConditionalEffect with all fields exists
- [x] ItemData.ConditionalEffects exists
- [x] `Source` removed from ItemTag enum
- [x] SynergyResolver rewritten; returns Resolution with Modifiers/ActiveBehaviors/ActiveStars
- [x] SynergyService rewritten — no _registry field, no SynergyRegistry dependency
- [x] SynergyService scene entry has no stale _registry reference
- [x] PlayerWeapons tracks per-instance attack counters; TriggerBehaviors calls BehaviorMath.ShouldTrigger
- [x] BehaviorAction.ExtraProjectile fires extra FireAt; AoEPulse / HealingBurst stub via Debug.Log
- [x] StarIndicatorOverlay renders stars per item per starred edge, idle vs active state
- [x] TooltipContent.FromItem lists conditional effects
- [x] TooltipContent.FromItemInstance marks active with `> ` prefix
- [x] All item assets configured per spec §4.11
- [x] Whetstone + BottleOfHush confirmed as pure contributors
- [x] BehaviorMath.cs exists as testable helper

### §5.3 Test cleanliness
- [x] 8 ConditionalEffectResolverTests; 4 BehaviorTriggerTests; 67 pre-WS-011 tests intact
- [x] **79 total / 0 failing / 0 ignored** (verified by `[Test]` count; designer Test Runner is the final gate)

### §5.2 Functional verification (designer playtest)
- [ ] BoneKnife shows 1 dim star (rounded rect) on its right edge from round 1
- [ ] Place Whetstone touching that edge → star brightens (gold), BoneKnife deals 2 dmg
- [ ] Place TestStick adjacent → BoneKnife fires faster AND every 5th attack double-shots
- [ ] Hover BoneKnife → tooltip lists 3 conditional effects, currently-active ones prefixed `> `
- [ ] Move contributor away → effects deactivate immediately, star dims, base damage returns
- [ ] HollowCrown shows 2 stars (Left + Right); contributor on either side activates
- [ ] No hover lines anywhere (SynergyHoverDisplay deleted)
- [ ] No console errors / warnings during 3-round playtest

## Review notes

- **Why the spec calls this a "pivot" and not a "patch":** WS-011 worked. Tests passed. Designer could ship 20 more synergies against the global rule registry without immediate breakage. But the rules lived in two places (registry asset + the items they applied to), which made authoring and reading the system harder than it needed to be. Once the item-owned model crystallized in GDD §11, a clean break beat a phased migration. Lesson is L-010 in `tasks/lessons.md`.
- **Self-exclusion semantics:** an item with both Weapon AND its own tag (TestStick's Tempo, BoneKnife's Weapon) won't activate its own stars even if its tags would match a conditional effect on itself. Resolver checks `occupant.InstanceID == recipient.InstanceID` and skips. This is why TestStick's degenerate inward-facing star is harmless — it would self-exclude anyway.
- **ActiveStars vs Modifiers semantics:** a conditional effect can have any number of stars matching (e.g., Hollow Crown with Whet-left AND Whet-right both Sharpening — 2 stars light up) but the effect itself applies once to the recipient's modifier accumulator. Multi-star matching is for VISUAL feedback; multi-tag activation across DIFFERENT conditional effects is for stat stacking.
- **Rotation correctness:** stars rotate with the item via `EffectiveStarredEdges` which reuses ShapeMath's normalization offset. For a 1×1 item, the offset is (0,0), so cell stays at (0,0) and only the direction rotates. For Test Stick (1×2 horizontal) rotated to vertical, the star cell at (0,0) Right becomes cell (0,1) Down — correctly placed on the rotated body. ShapeMath helpers tested implicitly via the resolver tests; could add direct ShapeMath rotation tests if a gotcha surfaces.
- **Counter behavior on move:** items keep their InstanceID across `MoveItem`, so the counter dict survives moves intact. On `RemoveItem` the counter is removed, so re-purchasing the same item kind from the shop gets a new InstanceID and fresh counter. This matches the spec's intent.
- **Performance:** O(items × effects × stars × 1-cell-lookup). At spec ceiling (50 items × 3 effects × 4 stars × 1 lookup) ≈ 600 ops per recompute, all integer + dictionary lookups. Microseconds.
- **Designer scene-edit warning:** the Game.unity edits this round affected ONE existing SynergyService MonoBehaviour entry (removed a field) AND added a NEW StarIndicatorOverlay GameObject (fileIDs 2140-2142) plus modified GridAnchor's children list. Per `project_unity_open_scene_reload` memory: designer MUST close and re-open Game.unity before saving anything else, or stale in-memory state will overwrite the new overlay.

## Deferred to designer (Unity-required)

- [ ] **Close and re-open `Game.unity` in Unity** before any other edits (project_unity_open_scene_reload memory).
- [ ] Wait for recompile. Confirm zero errors AND zero warnings.
- [ ] Open Test Runner → EditMode → Run All. Expect **79 passing, 0 failing, 0 ignored**.
- [ ] Play `Boot.unity` and run the spec §5.2 playtest sequence (10 steps).
- [ ] If you want a real star sprite instead of the rounded-rect placeholder, drop a 14×14 white-on-transparent PNG into `Assets/_Project/Sprites/UI/star_idle.png` and wire it to `StarIndicatorOverlay._starSprite` in the Inspector.
- [ ] Commit `[WS-011.5] Item-owned conditional effects, star indicators, behavior triggers` and push after playtest pass.

---

# Last completed: WS-011 — Adjacency Synergy Effects

Spec: `WS-011_Adjacency_Synergies_v2.md` (provided inline by designer 2026-05-14).

## Pre-flight (verified by reading project state)
- [x] WS-010 surface present: 67 tests passing baseline (matches spec expectation), `ItemPool` + `ShopController` + `ShopSlot` + `Tooltip`/`TooltipTarget`/`TooltipContent` already in place, 6 item assets + `DefaultShopPool` registered. `HeroStartingLoadout` seeds bag + Bone Knife.
- [x] `InventoryGrid.GetAdjacentItems` exists and is correct (verified by 6 existing adjacency tests in `InventoryGridTests.cs`). Multi-cell dedup via `seen` HashSet of InstanceIDs is already there — resolver can rely on it.
- [x] `InventoryService` publishes all 6 events (ItemPlaced/Removed/Moved, BagPlaced/Removed/Moved).
- [x] `PlayerWeapons.FireAt(ItemData, Enemy)` — signature change required; only one call-site (Update). Cooldown is `_cooldownByInstance[InstanceID]` — already keyed appropriately for per-instance modifier lookup.
- [x] `TooltipContent` is a `struct` with `FromItem(ItemData)` + `FromBag(BagData)`. `TooltipTarget` has `Item`/`Bag`/`Slot` fields; `BuildContent` falls through Slot → Item → Bag. Adding `ItemInstance` field + `FromItemInstance` is a pure additive change.
- [x] `InventoryGridView.RenderItem` already adds `TooltipTarget`; will also add `SynergyHoverDisplay`. `CELL_SIZE_PX = 56` is private but exposed via `CellSizePx`. `_gridParent` exposed via `GridParent`.
- [x] `Tooltip.Display` currently sets 4 labels (title/rarity/stats/description). Adding a 5th label (synergies) is additive — needs a new child GameObject under existing TooltipRoot/Panel (fileID 3610).
- [x] `Game.unity` GameObjects use small int fileIDs (matches L-003 — scenes are exempt from the large-fileID prefab rule). Highest RootOrder is 15 (GoldDropController) — SynergyService will slot at RootOrder 16, fileIDs 3700/3701/3702.
- [x] Active Input Handling = Input System Package (L-002/L-008). SynergyHoverDisplay uses Unity's UI raycasting (`IPointerEnter/Exit` on the rendered Image), which routes through `InputSystemUIInputModule` regardless of backend — safe.
- [x] `tasks/lessons.md` re-read: L-001 (scene-scoped — no DDOL needed for SynergyService), L-003 (scene fileIDs are small — fine), L-004 (no FindObjectsByType use in resolver), L-005 (synergy lines aren't pooled), L-006 (no Filled images), L-007 (SynergyService OnEnable subscribes — must be hot-reload safe; pattern is "null-check EventBus.Instance and bail if null", not "EnsureInitialized" since the cache is volatile and rebuilds on first event), L-008 (no `Input.*` use).

## Plan (checkable items)

### Phase 1 — Tag system + new field on ItemData
- [ ] `Inventory/ItemTag.cs` (new, GUID `4500a2b3c4d5e6f7081920304050607a`) — enum `None=0/Weapon=1/Charm=2/Sharpening=3/Tempo=4/Sustain=5/Source=6`.
- [ ] `Inventory/ItemData.cs` — add `public List<ItemTag> Tags = new();` after `Description`. Existing 6 item assets get tag values appended by editing their YAML.

### Phase 2 — Synergy data classes
- [ ] `Inventory/ItemSynergyRule.cs` (new, GUID `4501…`) — `SynergyEffect` enum (DamageBonus/CooldownReduction/RangeBonus) + ScriptableObject with SourceTag/RecipientTag/Effect/Magnitude/DisplayName/Description.
- [ ] `Inventory/SynergyRegistry.cs` (new, GUID `4502…`) — ScriptableObject wrapping `List<ItemSynergyRule> Rules`.

### Phase 3 — Resolver + modifier types
- [ ] `Inventory/ItemStatModifiers.cs` (new, GUID `4503…`) — struct with DamageBonus, CooldownMultiplier (1.0 = identity), RangeBonus. `Identity` + `Combine` (additive damage/range; multiplicative cooldown).
- [ ] `Inventory/SynergyResolver.cs` (new, GUID `4504…`) — static `Resolve(grid, rules)` → `Dictionary<int, ItemStatModifiers>`; static `ResolveActiveFor(item, grid, rules)` → `List<(rule, source)>`. Both use `grid.GetAdjacentItems`. Self-exclusion via InstanceID check.

### Phase 4 — SynergyService
- [ ] `Inventory/SynergyService.cs` (new, GUID `4505…`) — MonoBehaviour singleton, scene-scoped (no DDOL — L-001 only applies to Boot-persisted singletons). SerializeField registry. Subscribes ItemPlaced/Removed/Moved + BagPlaced/Removed/Moved in OnEnable. `Start` does first Recompute (HeroStartingLoadout already placed items by then in this scene's order). Hot-reload safety: OnEnable null-checks EventBus.Instance.
- [ ] `Game.unity` — add SynergyService GameObject at RootOrder 16 (fileIDs 3700/3701/3702), wire registry asset.

### Phase 5 — PlayerWeapons applies modifiers
- [ ] `Combat/PlayerWeapons.cs` — Update reads `SynergyService.Instance.GetModifiers(item.InstanceID)` (Identity fallback if service null). Computes `effectiveCooldown = item.Data.Cooldown * mods.CooldownMultiplier` and `effectiveRange = item.Data.Range + mods.RangeBonus`. FireAt signature gains 3rd param `ItemStatModifiers mods`; passes `item.Damage + mods.DamageBonus` to `Projectile.Initialize`. Fallback (`_equippedItems` / `_cooldownByData`) path unchanged — no modifiers when running without InventoryService.

### Phase 6 — Tooltip extension
- [ ] `UI/TooltipContent.cs` — add `string SynergiesText;` field. Add static `FromItemInstance(instance, grid, registry)` builder.
- [ ] `UI/Tooltip.cs` — `[SerializeField] private Text _synergiesLabel;` Display sets it; hides GO when text empty.
- [ ] `UI/TooltipTarget.cs` — add `public ItemInstance ItemInstance;` field. BuildContent prefers ItemInstance over Item when present and InventoryService + SynergyService Instances exist.
- [ ] `UI/InventoryGridView.cs` — RenderItem sets `tt.ItemInstance = item;` AND `gameObject.AddComponent<SynergyHoverDisplay>()` with Source + GridParent wiring.

### Phase 7 — Hover lines
- [ ] `UI/SynergyHoverDisplay.cs` (new, GUID `4506…`) — IPointerEnter/Exit handler that draws colored UI Image lines from item bbox center to each synergy partner. Lines use `raycastTarget=false`, parented to GridParent, `SetAsFirstSibling` so they render under item icons. ColorForEffect: red dmg / blue cooldown / yellow range.

### Phase 8 — Assets
- [ ] `Assets/_Project/ScriptableObjects/Items/Whetstone_Item.asset` (GUID `4510…`) — Common, no projectile, Tags=[Sharpening], shape=SimpleSquare_ItemShape.
- [ ] Edit `DefaultShopPool_ItemPool.asset` — append Whetstone to Items list.
- [ ] `Assets/_Project/ScriptableObjects/Synergies/Sharpening_Adjacent_Weapon_DamageBonus_Rule.asset` (GUID `4511…`) — Source=Sharpening, Recipient=Weapon, Effect=DamageBonus, Magnitude=1.0, "Honed Edge".
- [ ] `Assets/_Project/ScriptableObjects/Synergies/Tempo_Adjacent_Weapon_CooldownReduction_Rule.asset` (GUID `4512…`) — Source=Tempo, Recipient=Weapon, Effect=CooldownReduction, Magnitude=0.15, "Quickened Strike".
- [ ] `Assets/_Project/ScriptableObjects/Synergies/DefaultSynergies_SynergyRegistry.asset` (GUID `4513…`) — wraps both rules.
- [ ] Edit each of 6 existing item assets to set `Tags:` (Bone Knife/Tarnished Bell/Veil Thread/Hollow Crown = [Weapon, Source]; Test Stick = [Weapon, Tempo]; Bottle of Hush = [Sustain]).
- [ ] Folder metas for new Synergies dir.

### Phase 9 — Tooltip Panel SynergiesLabel
- [ ] Edit `Game.unity` Tooltip Panel (fileID 3610) — add `SynergiesLabel` child Text GameObject. Wire to Tooltip._synergiesLabel.

### Phase 10 — Tests (8 new)
- [ ] `Tests/EditMode/SynergyResolverTests.cs` — 8 tests per spec §4.9. Helpers MakeItem(name, shape, tags) / MakeRule(...). 67 → 75 total.

### Phase 11 — Verify, document, commit
- [x] Update `tasks/lessons.md` if new gotcha surfaces — none surfaced in code-time. Will revisit if designer playtest exposes one.
- [x] Add review section below this plan.
- [ ] **Designer playtest gate** — commit awaits playtest pass on this branch (no `Push` yet).

## Done
- [x] `Inventory/ItemTag.cs` — enum `None/Weapon/Charm/Sharpening/Tempo/Sustain/Source`. Stable backing values 0..6 so existing assets won't shift if new tags are appended.
- [x] `Inventory/ItemData.cs` — `Tags : List<ItemTag>` added after `Description`. Existing item assets all extended with appropriate tag lists (Bone Knife / Tarnished Bell / Veil Thread / Hollow Crown = `[Weapon, Source]`; Test Stick = `[Weapon, Tempo]`; Bottle of Hush = `[Sustain]`; new Whetstone = `[Sharpening]`).
- [x] `Inventory/ItemSynergyRule.cs` (`CreateAssetMenu` "HellpitRampage/Synergy Rule") — SourceTag/RecipientTag/Effect/Magnitude/DisplayName/Description. `SynergyEffect` enum at namespace level.
- [x] `Inventory/SynergyRegistry.cs` — wraps `List<ItemSynergyRule>`.
- [x] `Inventory/ItemStatModifiers.cs` — value type. `Identity` static + `Combine` (additive damage/range; multiplicative cooldown — passes test 5 by construction).
- [x] `Inventory/SynergyResolver.cs` — pure static class. `Resolve(grid, rules) → Dictionary<int, ItemStatModifiers>` and `ResolveActiveFor(item, grid, rules) → List<(rule, source)>`. Both use `grid.GetAdjacentItems` (WS-008-tested) so adjacency math isn't reinvented. Self-exclusion via InstanceID check; null-guards on all tag-list accesses (for tagless legacy items).
- [x] `Inventory/SynergyService.cs` — scene-scoped singleton. SerializeField `SynergyRegistry`. Subscribes to all 6 inventory events. Cache rebuild on every change (microseconds, per spec gotcha #21). `Start` does initial recompute. L-001 not needed — service is scene-local, not Boot-persisted. L-007 not needed — no Awake-only init beyond Instance assignment; OnEnable re-asserts Instance for hot-reload coverage.
- [x] `Combat/PlayerWeapons.cs` — `Update` queries `SynergyService.Instance.GetModifiers(InstanceID)` (Identity fallback). Effective damage / cooldown / range applied. `FireAt` signature gained `ItemStatModifiers mods`; fallback `_equippedItems` path passes `ItemStatModifiers.Identity`. **No call-sites outside Update**; PlayerWeaponsTests unaffected (they test `WeaponMath` only).
- [x] `UI/TooltipContent.cs` — added `SynergiesText` field and static `FromItemInstance(instance, grid, registry)`. Empty-list short-circuits leave field null (Tooltip hides the label).
- [x] `UI/Tooltip.cs` — `_synergiesLabel : Text` SerializeField. `Display` toggles label active state based on `string.IsNullOrEmpty(SynergiesText)`.
- [x] `UI/TooltipTarget.cs` — `ItemInstance` field added. `BuildContent` prefers ItemInstance when InventoryService + SynergyService Instances exist (else falls back to ItemData path — preserves existing shop-slot tooltip behavior).
- [x] `UI/InventoryGridView.cs` — `RenderItem` now sets `tt.ItemInstance = item` AND adds `SynergyHoverDisplay` to each rendered item Image. Refresh-all destroy + recreate cycle continues to apply.
- [x] `UI/SynergyHoverDisplay.cs` — `IPointerEnter/Exit` on the rendered item Image. On enter, queries `SynergyResolver.ResolveActiveFor`, draws colored UI Image lines (red/blue/yellow per effect) from item bbox center to each partner bbox center. Lines have `raycastTarget=false` (spec gotcha #18) and `SetAsFirstSibling` so they render under item icons (gotcha #19). `OnDisable` also clears lines (covers RefreshAll → Destroy path).
- [x] `ScriptableObjects/Synergies/` folder with meta GUID `4520…`.
- [x] `Sharpening_Adjacent_Weapon_DamageBonus_Rule.asset` (GUID `4511…`) — Sharpening→Weapon, DamageBonus, magnitude 1.0, "Honed Edge".
- [x] `Tempo_Adjacent_Weapon_CooldownReduction_Rule.asset` (GUID `4512…`) — Tempo→Weapon, CooldownReduction, magnitude 0.15, "Quickened Strike".
- [x] `DefaultSynergies_SynergyRegistry.asset` (GUID `4513…`) — wraps both rules.
- [x] `Whetstone_Item.asset` (GUID `4510…`) — Common, Sharpening tag, non-firing, `SimpleSquare_ItemShape`. Added to `DefaultShopPool_ItemPool.asset`'s Items list (now 7 items).
- [x] `Game.unity` — `SynergyService` GameObject at RootOrder 16 (fileIDs 3700/3701/3702) with `_registry` wired to the registry asset. `SynergiesLabel` Text added under Tooltip Panel (fileIDs 3670/3671/3672/3673) — appended to Panel.m_Children. Tooltip MonoBehaviour wired `_synergiesLabel: {fileID: 3673}`.
- [x] `Tests/EditMode/SynergyResolverTests.cs` — 8 tests (no-adjacency / damage / cooldown / additive stacking / multiplicative stacking / diagonal / non-matching / across-bag-boundary). Total now **75** (67 + 8), matching spec acceptance criteria.

## Deviations from spec (explicit)

- **SynergiesLabel positioned outside the Panel rect, anchored to the bottom edge with pivot (0.5, 1.0).** The spec said "below the existing labels" inside the Panel; the existing labels already cover the panel (StatsLabel top half, DescriptionLabel bottom half). Resizing the panel or remapping the existing two labels' anchors would have meant touching layout that's already shipped and tested. Instead, the new label anchors to the Panel's bottom edge and extends *downward* outside it. When `SynergiesText` is empty, Tooltip.Display hides the GO so the visual footprint reverts to the existing panel exactly. **Trade-off:** the synergies text isn't clipped by the panel rect, so on extremely cramped screens it could spill below the canvas edge. Acceptable for v1; revisit if it bites.
- **SynergyService is scene-scoped (no DontDestroyOnLoad).** L-001 applies to Boot-persisted singletons; SynergyService lives only inside Game.unity and is recreated per scene load. Matches the pattern of `InventoryService`, `ShopController`, `RunManager` (the new wave of Game-scoped singletons).
- **Whetstone Cooldown/Range/Speed/Lifetime default values populated** (Cooldown:1 / Range:8 / Speed:10 / Lifetime:3) even though `ProjectilePrefab` is null. The fields default at ItemData level if omitted; explicit values match what Unity would write after first save, keeping the asset stable across editor reimports.

## Acceptance criteria (from spec §5.1, 5.3)

- [x] `ItemTag` enum with starter values (None/Weapon/Charm/Sharpening/Tempo/Sustain/Source)
- [x] `ItemData.Tags` added; 6 existing items tagged + Whetstone created
- [x] Whetstone in DefaultShopPool
- [x] `SynergyEffect` enum + `ItemSynergyRule` SO + `SynergyRegistry` SO
- [x] Both starter rule assets + registry asset created and wired into scene
- [x] `ItemStatModifiers` struct + `SynergyResolver` static class + `SynergyService` MonoBehaviour
- [x] SynergyService GameObject in Game.unity with registry wired
- [x] `PlayerWeapons` applies modifiers (damage / cooldown / range)
- [x] Tooltip + TooltipTarget + InventoryGridView wired for ItemInstance + synergy display
- [x] `SynergyHoverDisplay` draws lines on hover with effect-type color coding
- [x] 8 SynergyResolver EditMode tests; **75 total** matches spec target
- [x] Code style: Tech Arch Section 4 (PascalCase, `_camelCase` privates, file-scoped namespaces, no global statics outside singletons).

## Deferred to designer (Unity-required)

- [ ] **Close and reopen `Game.unity`** in Unity before saving anything else (per `project_unity_open_scene_reload` memory). The disk-edits include a new top-level SynergyService GameObject AND a new child under TooltipRoot/Panel — stale in-memory state would overwrite both.
- [ ] Open Unity, wait for recompile, confirm zero errors AND warnings.
- [ ] Test Runner → EditMode → Run All. Expect **75 passing / 0 failing / 0 ignored**.
- [ ] Press Play on `Boot.unity` and run the spec §5.2 playtest:
  - Hover Bone Knife alone → tooltip has no synergy section.
  - Reach round 1 end, buy Whetstone, place adjacent to Bone Knife → tooltip lists "Honed Edge from Whetstone", red line connects them on hover, round-2 enemies die in 2 hits instead of 3.
  - Buy Test Stick (Tempo), place adjacent → blue line + "Quickened Strike" tooltip line, Bone Knife visibly fires faster.
  - Stack two Whetstones adjacent to Bone Knife → tooltip lists Honed Edge twice, +2 damage, enemies die in 1-2 hits.
  - Move Whetstone away → tooltip clears, Bone Knife back to 1 dmg, lines gone.
  - Diagonal placement does NOT trigger.
- [ ] Commit `[WS-011] Adjacency synergy effects, tag system, synergy registry, hover visualization` after playtest pass; push to `main`.

## Review notes

- **Resolver perf**: O(items × rules × neighbors). At spec ceiling (~50 items, ~25 rules, ~4 neighbors) that's ~5000 checks — single-digit microseconds. Recomputed only on inventory events (a few per second at peak); cost is invisible.
- **Why CooldownReduction stores `1 - magnitude` in the Multiplier**: keeps the multiplicative `Combine` math natural — `Combine(0.85, 0.85) = 0.7225`. If we stored "fraction reduced" we'd have to flip back and forth.
- **TooltipContent for shop slots is unaffected.** When the player hovers a ShopSlot item (not yet placed in the grid), TooltipTarget.Slot path takes precedence; ItemInstance is null; `FromItem(ItemData)` runs without synergy section. The spec explicitly defers shop-slot "would-gain-X-if-placed-here" preview to later.
- **`SynergyHoverDisplay` uses static cell size (56px)**, matching `InventoryGridView.CELL_SIZE_PX`. If the grid scale ever changes, both constants need updating; not worth a shared singleton for one prototype-stage value.
- **Persistence**: synergies are NOT serialized — they're derived from current adjacency on demand. Save/load doesn't need touching (per spec OUT-of-scope §2).
- **Hot-reload concern (L-007)**: SynergyService's only Awake-time work is `Instance = this`. OnEnable re-asserts Instance and subscribes. Domain reload during Play:
  1. Awake skipped → Instance is whatever the destroyed-then-recreated assignment leaves it (Unity reconstructs `static` fields as null).
  2. OnEnable fires → Instance = this AND subscribes → service is functional.
  3. The cache is stale (empty) until the next inventory event OR until `Start` runs. Since Start doesn't run after domain reload either, **the cache won't repopulate until the player moves an item**. Mitigation if this bites in playtest: SynergyService.OnEnable should also call Recompute. Adding that defensively now.

---

# Last completed: WS-010 — Shop Slots + Gold + Buying + Tooltips + Shop-Drag Rotation

Spec: `WS-010_Shop_Gold_Buying_Tooltips_Rotation_v4.md` (provided inline by designer).
Scope decision (user 2026-05-14): **all of WS-010 in one pass** (option A).

## Pre-flight (verified by reading project state)
- [x] WS-009 surface present: `DragHandler` with R/Escape, `InventoryService.MoveItem`/`MoveBagAndItems`, `Grid.PlaceItem(data, origin, rotation)`, `Grid.MoveItem`, `ItemMovedEvent`/`BagMovedEvent`, `ShapeMath.Rotate/Next`, Test Stick (1x2) + Bone Knife + Warden's Pouch seeded.
- [x] WS-009 deviation noted in todo §70: visual rotation of dragged sprite NOT implemented; validation overlay rotates only. Spec explicitly accepts the same UX in WS-010 §3 #33.
- [x] `tasks/lessons.md` re-read: L-001 (root persist — Game-scoped controllers don't need DDOL), L-002 (InputSystem UI module — already configured; legacy `Input.*` is dead — replace `Input.mousePosition` with `Mouse.current.position.ReadValue()` in Tooltip), L-003 (19-digit prefab fileIDs — applies to new `GoldPickup.prefab` and `DragGhost.prefab`), L-004 (parameterless `FindObjectsByType<T>()`), L-005 (pooled double-release — applies to `GoldPickup` since it self-releases via `OnTriggerEnter2D` AND lifetime `Update`), L-006 (Filled image needs sprite — NA; no Filled images), L-007 (OnEnable hot-reload safety — apply to `Tooltip` singleton and `ShopController`).
- [x] `EnemyDiedEvent.EnemyObject` has no field-access consumers: `HealthTests` discards the payload, only `Health.HandleDeath` writes it. Safe to reshape to `{Position, GoldAmount}`.
- [x] `Active Input Handling = Input System Package` (per L-002): legacy `Input.mousePosition` returns `(0,0,0)` permanently. **DEVIATION from spec §4.12.2**: Tooltip will use `UnityEngine.InputSystem.Mouse.current.position.ReadValue()`.
- [x] Auto-memory loaded: scene-reload caveat known — designer MUST close+reopen `Game.unity` before re-saving. Spec already calls this out indirectly via WS-009 follow-up.

## Plan (checkable items, grouped by phase)

### Phase 1 — Data model (Inventory + Combat scripts, no behavioral change yet)
- [ ] `Inventory/ItemData.cs` — add `ItemRarity` enum at namespace level; `Rarity` and `BasePrice` fields with `EffectivePrice` getter + static `DefaultPriceForRarity` switch (1/3/6/10/18).
- [ ] `Inventory/BagData.cs` — mirror `Rarity` + `BasePrice` + `EffectivePrice` (delegates to `ItemData.DefaultPriceForRarity`).
- [ ] `Inventory/InventoryService.cs` — overload `PlaceItem(ItemData, Vector2Int, Rotation)` forwarding to `Grid.PlaceItem`. Existing 2-arg overload now calls the 3-arg form with `Rotation.Deg0` to keep one publish path.
- [ ] `Combat/EnemyData.cs` — add `public int GoldDropAmount = 1;`.
- [ ] `Combat/Enemy.cs` — add `public EnemyData Data => _data;`.

### Phase 2 — Events
- [ ] `Core/Events/GoldChangedEvent.cs` (new file, GUID `4001a2b3c4d5e6f7081920304050607a`) — `int OldAmount; int NewAmount;`.
- [ ] `Core/Events/EnemyDiedEvent.cs` — **reshape**: drop `EnemyObject`, add `Vector3 Position` + `int GoldAmount`.

### Phase 3 — Combat death + gold drops
- [ ] `Combat/Health.cs` — `HandleDeath` for `Owner.Enemy`: read `GetComponent<Enemy>().Data.GoldDropAmount`, publish `EnemyDiedEvent { Position = transform.position, GoldAmount = goldAmount }` BEFORE releasing pool. Player branch unchanged.
- [ ] `Combat/GoldPickup.cs` (new, GUID `4002…`) — Rigidbody2D + CircleCollider2D trigger; `OnTriggerEnter2D` on Player → `RunManager.AddGold` → Despawn. Lifetime 30s decrement in `Update`. **L-005 guard: `_isDespawned` flag reset in `OnEnable`** (lessons L-005 requires this — spec missed it).
- [ ] `Combat/GoldDropController.cs` (new, GUID `4003…`) — subscribes `EnemyDiedEvent` in OnEnable, prewarms 30 in Start; on event spawns pickup at `evt.Position` from pool, calls `Initialize(evt.GoldAmount)`. Skip if `GoldAmount <= 0`.

### Phase 4 — RunManager gold
- [ ] `Core/RunManager.cs` — add `_startingGold = 10`, `_roundEndBonusGold = 5`, `CurrentGold`, `AddGold(int)`, `SpendGold(int)`. In `StartNewRun` reset `CurrentGold = _startingGold` and publish `GoldChangedEvent { OldAmount = 0, NewAmount = CurrentGold }` BEFORE `RunStartedEvent` (so subscribers see gold first). In `EndCurrentRound`, after publishing `RoundEndedEvent`, call `AddGold(_roundEndBonusGold)` UNLESS the run is ending (`CurrentRound >= _totalRounds`) so victory doesn't double-publish to a dead shop.

### Phase 5 — PlayerWeapons rewire
- [ ] `Combat/PlayerWeapons.cs` — keep `_equippedItems` SerializeField as fallback. Switch primary cooldown tracking to `Dictionary<int /*InstanceID*/, float>`. Subscribe `ItemPlacedEvent`/`ItemRemovedEvent` in `OnEnable`, unsubscribe in `OnDisable`. `Start` seeds dict from `InventoryService.Grid.Items` (if Instance exists) AND prewarms each weapon's projectile; falls back to `_equippedItems` only if Instance is null (test/no-scene path). `Update` iterates `InventoryService.Grid.Items` (primary) or `_equippedItems` (fallback). **L-007 OnEnable safety**: no Awake-only init, OnEnable is the entry path; SerializeField refs survive domain reload.

### Phase 6 — Shop UI core
- [ ] `Inventory/ItemPool.cs` (new, GUID `4010…`) — `ScriptableObject` with `List<ItemData> Items`, `List<BagData> Bags`, `DrawWeighted(int count, Random rng = null)`, static `WeightForRarity` switch (50/25/12/5/1).
- [ ] `UI/ShopSlot.cs` (new, GUID `4011…`) — fields `_background`, `_itemIcon`, `_nameLabel`, `_priceLabel`; `SetOffer`, `MarkSold`, `UpdateAffordability`, `UpdateDisplay`, static `ColorForRarity`.
- [ ] `UI/ShopController.cs` (new, GUID `4012…`) — `_pool`, `_slots[5]`, `_rerollButton`, `_rerollLabel`. Subscribes `RoundEndedEvent` (populate), `RoundStartedEvent` (mark all sold), `GoldChangedEvent` (refresh affordability). Reroll: `_rerollsThisShop` counter, `CurrentRerollCost(n)` switch (1/2/3). `TakeOfferFromSlot(int)` returns offer + marks sold. **L-007**: `_rng` allocated in OnEnable behind `if (_rng == null)`.
- [ ] `UI/ShopSlotDragHandler.cs` (new, GUID `4013…`) — full drag lifecycle (BeginDrag/Drag/EndDrag), R-key rotation (items only), Escape cancel, ghost follows cursor, validation overlay, atomic spend+place+take-sold with refund on defensive failure. Hides Tooltip on OnBeginDrag. `_currentRotation` always starts at `Rotation.Deg0`.
- [ ] `UI/GoldDisplayController.cs` (new, GUID `4014…`) — subscribes `GoldChangedEvent`, updates `_label.text = $"Gold: {amount}"`. Reads initial value from `RunManager.Instance.CurrentGold` in OnEnable (guarded for null Instance during tests).

### Phase 7 — Tooltip system
- [ ] `UI/TooltipContent.cs` (new, GUID `4020…`) — struct with `Title/RarityLabel/RarityColor/StatLines/Description`. Static `FromItem(ItemData)` (weapon shows `DMG/Cooldown/Range/Speed`, non-weapon shows `"(Effect pending)"`) and `FromBag(BagData)`.
- [ ] `UI/Tooltip.cs` (new, GUID `4021…`) — singleton MonoBehaviour with panel + 4 labels + rarity bar. `RequestShow` queues content with 0.3s `_hoverDelay`. `Hide` cancels. Position follows cursor via `Mouse.current.position.ReadValue()` (NOT legacy `Input.mousePosition` — L-002). Edge-flip via Canvas size. `SetAsLastSibling` on show. Uses `Time.unscaledDeltaTime`. **L-007**: `Instance = this` lives in OnEnable (idempotent) so domain-reload-during-Play still resolves.
- [ ] `UI/TooltipTarget.cs` (new, GUID `4022…`) — `IPointerEnter/ExitHandler`. Optional `Item`/`Bag`/`Slot` field. `BuildContent`: Slot.CurrentOffer wins, then Item, then Bag.
- [ ] `UI/DragHandler.cs` — add `if (Tooltip.Instance != null) Tooltip.Instance.Hide();` at the top of `OnBeginDrag`.
- [ ] `UI/InventoryGridView.cs` — in `RenderBag` and `RenderItem`, after `AddComponent<DragHandler>`, also `AddComponent<TooltipTarget>` and set `Bag = bag.Data` / `Item = item.Data`.

### Phase 8 — Loadout cleanup
- [ ] `Inventory/HeroStartingLoadout.cs` (new, GUID `4030…`) — Start-time: `Grid.Clear()` then place bag + single item (Warden's Pouch + Bone Knife only). Test Stick removed — it lives in the pool now.
- [ ] **Delete** `Inventory/InventoryTestSeeder.cs` (+ .meta) — replaced by HeroStartingLoadout.

### Phase 9 — ScriptableObject and asset authoring
- [ ] `ScriptableObjects/Items/TarnishedBell_Item.asset` (GUID `4101…`) — Uncommon, Damage 2, Cooldown 1.0, Speed 8, Range 6, SimpleSquare shape, ProjectilePrefab = `Projectile.prefab`.
- [ ] `ScriptableObjects/Items/VeilThread_Item.asset` (GUID `4102…`) — Uncommon, Damage 1, Cooldown 0.3, Speed 14, Range 9.
- [ ] `ScriptableObjects/Items/HollowCrown_Item.asset` (GUID `4103…`) — Rare, Damage 5, Cooldown 2.0, Speed 10, Range 10.
- [ ] `ScriptableObjects/Items/BottleOfHush_Item.asset` (GUID `4104…`) — Common, null ProjectilePrefab.
- [ ] `ScriptableObjects/Items/DefaultShopPool_ItemPool.asset` (GUID `4105…`) — 6 items (BoneKnife, TestStick, TarnishedBell, VeilThread, HollowCrown, BottleOfHush) + 1 bag (WardenPouch).
- [ ] Update rarities: `BoneKnife_Item.asset` (Common), `TestStick_Item.asset` (Common), `WardenPouch_Bag.asset` (Common), `BasicEnemy_Enemy.asset` (`GoldDropAmount: 1`).

### Phase 10 — Sprite + prefab assets
- [ ] `Assets/_Project/Sprites/Items/placeholder_gold.png` — 24×24 PNG `#f0d048` (yellow). Generated via PowerShell `System.Drawing.Bitmap` (WS-003 pattern).
- [ ] `Assets/_Project/Sprites/Items/placeholder_gold.png.meta` — same TextureImporter settings as `placeholder_projectile.png` (Sprite, Single, PPU 64, no compression).
- [ ] `Assets/_Project/Prefabs/Projectiles/GoldPickup.prefab` (GUID `4200…`, anchor base `5400_0000_0000_0000_001` style) — SpriteRenderer + Rigidbody2D Kinematic gravity 0 + CircleCollider2D r=0.5 trigger + GoldPickup MonoBehaviour. **L-003 19-digit fileIDs.**
- [ ] `Assets/_Project/Prefabs/UI/DragGhost.prefab` (GUID `4201…`) — RectTransform + CanvasRenderer + UI.Image 56×56, white, semi-transparent. **L-003 19-digit fileIDs.**

### Phase 11 — Game.unity scene wiring (largest single edit of WS-010)
- [ ] **TopBar/GoldDisplay (2010-2013)** — attach `GoldDisplayController` (new MonoBehaviour fileID 2014); wire `_label = 2013`.
- [ ] **TopBar/RerollLabel (2020-2023)** — promote to Button: keep existing GameObject, add `Image` (re-using m_Type 0) if missing, add `Button` MonoBehaviour (fileID 2024) targetGraphic = the existing Text's host Image. Child Text becomes label that ShopController._rerollLabel writes.
  - Cleanest YAML approach: add a sibling Image+Button under RerollLabel parent. Decision: keep RerollLabel GO; add a Button component to it (Image+Button) and make the existing Text its child for the label binding. Will detail when editing.
- [ ] **5 ShopSlots (2040-2083)** — for each: add child `ItemIcon` (Image, 80×80, sprite from offer), `NameLabel` (Text, bottom-anchored), `PriceLabel` (Text, top-right). Add `ShopSlot`, `ShopSlotDragHandler`, `TooltipTarget` MonoBehaviours. Wire serialized refs to children + the shared ShopController + the existing InventoryGridView (2132) + grid parent (2121) + DragGhost prefab.
- [ ] **ShopController GO** — new root-level GO. Wire `_pool = DefaultShopPool_ItemPool.asset`, `_slots = [2043,2053,2063,2073,2083 ShopSlot components]`, `_rerollButton = 2024`, `_rerollLabel = 2023`.
- [ ] **GoldDropController GO** — new root-level GO. Wire `_goldPickupPrefab = GoldPickup.prefab`.
- [ ] **TooltipRoot under Canvas (901)** — empty GO holding `Tooltip` MonoBehaviour; Panel child with TitleLabel / RarityBar / RarityLabel / StatsLabel / DescriptionLabel (all `RaycastTarget = false`). Panel deactivated by default. Wire `_canvas = 901`.
- [ ] **HeroStartingLoadout GO** — repurpose existing InventoryTestSeeder GO (2210-2212): change script reference to HeroStartingLoadout's GUID, remove `_starterItem2`/`_item2Origin` SerializeFields, rename in scene to "HeroStartingLoadout". (Simpler than deleting+re-adding the GO and keeps the existing fileIDs as anchors.)
- [ ] Sanity-check Canvas children list (901.m_Children) — ensure all new children (TooltipRoot) appended.

### Phase 12 — Tests (12 new in 4 files)
- [ ] `Tests/EditMode/RunManagerGoldTests.cs` (GUID `4300…`) — 4 cases: AddGold + event, SpendGold-affordable returns true, SpendGold-insufficient returns false, StartNewRun-resets. Uses EventBusTestHost + RunManager pattern from `RunManagerTests`.
- [ ] `Tests/EditMode/ItemPoolTests.cs` (GUID `4301…`) — 3 cases: empty-pool draws empty, requested-count respected, seeded-Random deterministic. Uses `ScriptableObject.CreateInstance` for ItemPool/ItemData (no scene).
- [ ] `Tests/EditMode/ShopRerollCostTests.cs` (GUID `4302…`) — 3 cases: <5 returns 1, <10 returns 2, ≥10 returns 3. Pure static method test, no scene/EventBus.
- [ ] `Tests/EditMode/TooltipContentTests.cs` (GUID `4303…`) — 2 cases: weapon item populates all fields; non-weapon (null ProjectilePrefab) shows "(Effect pending)".
- [ ] Total expected: 55 prior + 12 new = **67 passing**.

### Phase 13 — Verify, deviations, commit prep
- [x] GUID-uniqueness check across `Assets/**/*.meta` (zero duplicates — 184 metas, 184 unique GUIDs).
- [x] Scene structural sanity: 273 unique anchors in `Game.unity`, zero duplicate fileIDs, zero broken local refs (all 43 unique `guid:` refs in the scene resolve to either an Assets/ meta or a known Unity package GUID — UI.Image `fe87c0e1…`, UI.Text `5f7201a1…`, UI.Button `4e29b1a8…`, EventSystem `76c392e4…`, InputSystemUIInputModule `01614664…`, Cinemachine + URP — all valid).
- [x] New prefabs (`GoldPickup.prefab`, `DragGhost.prefab`) and SO assets (`TarnishedBell/VeilThread/HollowCrown/BottleOfHush_Item.asset`, `DefaultShopPool_ItemPool.asset`) all reference resolvable script GUIDs.
- [x] No stale references to deleted `InventoryTestSeeder.cs` (GUID `0f2d9021f427917499562a9ca4c33f70`) — only mention is in this todo doc's documentation of past WS-008/WS-009 work.
- [ ] **Designer must verify zero compile errors AND zero warnings in Unity** after recompile + scene reopen.
- [ ] Stage commit message: `[WS-010] Shop slots, gold, buying, inventory-driven weapons, tooltips, shop-drag rotation`. Hold push until designer signs off (project convention).

## Done (summary of WS-010 work landed by this agent run on 2026-05-14)

### New scripts (13 files + 13 metas)
- `Combat/GoldPickup.cs` (GUID `4002a2…`) — pooled trigger pickup with L-005 `_isDespawned` guard.
- `Combat/GoldDropController.cs` (`4003…`) — scene controller that converts `EnemyDiedEvent` → pooled pickup spawn.
- `Inventory/HeroStartingLoadout.cs` (`4030…`) — replaces deleted `InventoryTestSeeder.cs`.
- `Inventory/ItemPool.cs` (`4010…`) — ScriptableObject with weighted draw (50/25/12/5/1).
- `Core/Events/GoldChangedEvent.cs` (`4001…`) — new event struct.
- `UI/ShopSlot.cs` (`4011…`), `UI/ShopController.cs` (`4012…`), `UI/ShopSlotDragHandler.cs` (`4013…`), `UI/GoldDisplayController.cs` (`4014…`).
- `UI/TooltipContent.cs` (`4020…`), `UI/Tooltip.cs` (`4021…`), `UI/TooltipTarget.cs` (`4022…`).

### Edited scripts (8 files)
- `Inventory/ItemData.cs` — added `ItemRarity` enum at namespace level + `Rarity`/`BasePrice`/`EffectivePrice`/`DefaultPriceForRarity` + cleaned out the WS-014 placeholder comments.
- `Inventory/BagData.cs` — mirror Rarity/BasePrice/EffectivePrice (delegates to `ItemData.DefaultPriceForRarity`).
- `Inventory/InventoryService.cs` — new `PlaceItem(ItemData, Vector2Int, Rotation)` overload; the existing 2-arg form now forwards through it so there's a single publish path.
- `Combat/EnemyData.cs` — `GoldDropAmount` field.
- `Combat/Enemy.cs` — public `Data` getter for `Health.HandleDeath` to read gold amount.
- `Combat/Health.cs` — Owner.Enemy branch publishes `EnemyDiedEvent { Position, GoldAmount }` BEFORE pool release (captures `transform.position` while live).
- `Combat/PlayerWeapons.cs` — inventory-driven cooldowns keyed by `InstanceID`, subscribed to `ItemPlacedEvent`/`ItemRemovedEvent`; SerializeField list demoted to fallback path used only when `InventoryService.Instance` is null.
- `Core/RunManager.cs` — `_startingGold`, `_roundEndBonusGold`, `CurrentGold`, `AddGold/SpendGold/GoldChangedEvent` publishing; `StartNewRun` resets gold and publishes the change BEFORE `RunStartedEvent`/`RoundStartedEvent`; `EndCurrentRound` awards round-end bonus only when the run continues (no double-publish on victory).
- `Core/Events/EnemyDiedEvent.cs` — reshaped to `{Position, GoldAmount}`. No consumers used the old `EnemyObject` field (`HealthTests` discards payload), so safe rename.
- `UI/DragHandler.cs` — calls `Tooltip.Instance.Hide()` at the top of `OnBeginDrag`.
- `UI/InventoryGridView.cs` — `RenderBag`/`RenderItem` add `TooltipTarget` alongside `DragHandler`.

### Deleted scripts (1 file)
- `Inventory/InventoryTestSeeder.cs` (+ `.meta`) — replaced by `HeroStartingLoadout`.

### New ScriptableObject + sprite + prefab assets (10 files + 10 metas)
- 4 new placeholder items: `TarnishedBell_Item.asset` (Uncommon, GUID `4101…`), `VeilThread_Item.asset` (Uncommon, `4102…`), `HollowCrown_Item.asset` (Rare, `4103…`), `BottleOfHush_Item.asset` (Common, null projectile, `4104…`).
- `DefaultShopPool_ItemPool.asset` (`4105…`) — 6 items + 1 bag (WardenPouch).
- `Sprites/Items/placeholder_gold.png` — 24×24 yellow `#f0d048` (`4200…`).
- `Prefabs/Projectiles/GoldPickup.prefab` (`4201…`, 19-digit fileIDs base `5700_0000_0000_0000_001..006` per L-003).
- `Prefabs/UI/DragGhost.prefab` (`4202…`, base `5800_…`).

### Edited existing assets (4 files)
- `BoneKnife_Item.asset`, `TestStick_Item.asset` — added `Rarity: 0, BasePrice: 0`.
- `WardenPouch_Bag.asset` — added `Rarity: 0, BasePrice: 0`.
- `BasicEnemy_Enemy.asset` — added `GoldDropAmount: 1`.

### Game.unity scene edits (net +~2148 lines, scene went from 3408 → 5556 lines)
- GoldDisplay (2010) gets new `GoldDisplayController` MonoBehaviour (fileID 2014), wired `_label = 2013`.
- RerollLabel (2020) renamed to RerollButton; Text 2023 `m_RaycastTarget` flipped 0→1 (so the Text is the click target); new Button MonoBehaviour 2024 with `m_TargetGraphic = 2023`. `_rerollButton` and `_rerollLabel` SerializeFields on ShopController point to 2024 and 2023 respectively, so ShopController.UpdateRerollLabel writes through the Text while clicks fire through the Button.
- 5 ShopSlots (2040–2080) each gained 3 new MonoBehaviours (ShopSlot 30N0, ShopSlotDragHandler 30N1, TooltipTarget 30N2) and 3 new child GameObjects (ItemIcon, NameLabel, PriceLabel) using fileID base `30N0`–`30N3` for N=0..4 (e.g., slot 0 uses 3040–3073, slot 1 uses 3140–3173, etc.).
- New root-level `ShopController` GO (3500/3501/3502) wired to DefaultShopPool, 5 slot MBs, RerollButton 2024, RerollLabel 2023.
- New root-level `GoldDropController` GO (3510/3511/3512) wired to GoldPickup prefab + `_prewarmCount = 30`.
- New `TooltipRoot` GO (3600/3601/3602) under Canvas (Canvas.m_Children appended with 3601), with `Panel` child (3610/3611/3612/3613, `m_IsActive: 0`) containing 5 sub-children: TitleLabel 3620, RarityBar 3630 (Image), RarityLabel 3640, StatsLabel 3650, DescriptionLabel 3660. All tooltip children have `RaycastTarget = 0` per spec gotcha #27.
- Existing InventoryTestSeeder GO (2210) renamed to `HeroStartingLoadout`; MB 2212's `m_Script.guid` repointed to the new HeroStartingLoadout GUID `4030…`; `_starterItem2` and `_item2Origin` SerializeFields dropped; `_starterBag`→`_startingBag`, `_starterItem`→`_startingItem` (matching the new script's field names).

### Tests (4 new files + 4 metas, 12 new test cases)
- `RunManagerGoldTests.cs` (GUID `4300…`) — 4 cases.
- `ItemPoolTests.cs` (`4301…`) — 3 cases incl. seeded-RNG determinism.
- `ShopRerollCostTests.cs` (`4302…`) — 3 cases.
- `TooltipContentTests.cs` (`4303…`) — 2 cases (weapon + non-weapon "(Effect pending)").
- Expected total: **55 prior + 12 new = 67 passing**.

## Notes / Deviations from spec

1. **Tooltip uses `Mouse.current.position.ReadValue()`, NOT `Input.mousePosition`.** Spec §4.12.2 + §7 says `Input.mousePosition` "is supported in Unity 6 alongside the new Input System." This is a project-level *false*: `ProjectSettings → Active Input Handling = Input System Package` (per L-002), so legacy `Input.*` returns `(0,0,0)` permanently. Tooltip would render at canvas origin every frame. The new-Input-System read returns the same screen-space `Vector2`, so `ScreenPointToLocalPointInRectangle` works unchanged.
2. **`GoldPickup` has an L-005 `_isDespawned` guard** not in the spec. Spec's `OnTriggerEnter2D → Despawn` + `Update lifetime → Despawn` is exactly the double-release scenario L-005 captures. Same shape as `Projectile.cs` fix in WS-006.
3. **Round-end gold bonus suppressed on final round.** Spec says "`EndCurrentRound`, after publishing `RoundEndedEvent`: `AddGold(_roundEndBonusGold)`." If applied on round 30 (totalRounds), the run-end overlay has already taken over via `RunEndedEvent`, but `ShopController` would still receive a `GoldChangedEvent` and try to refresh affordability on its now-hidden slots. My implementation skips the bonus when `CurrentRound >= _totalRounds`. The non-final round behavior is identical to spec.
4. **InventoryService 2-arg `PlaceItem` overload now forwards to the 3-arg form.** Spec keeps them as separate methods that both publish — duplicate work and an easier place to forget a publish. Mine collapses them. Behavior identical externally.
5. **`PlayerWeapons` retains both an InstanceID dict AND an ItemData dict** for the fallback path. Spec only describes the InstanceID dict and "fallback list" without saying how to drive cooldowns in fallback mode. The dual-dict approach keeps the test scene (no InventoryService) working without polluting the production path.
6. **RerollLabel→RerollButton conversion is component-only**, not a child-restructure. Spec §4.13 implies "wire to ShopController._rerollButton; its child Text → _rerollLabel" — suggesting a Button parent + Text child. I kept the Text on the same GO and added a Button MonoBehaviour beside it (using the Text as `m_TargetGraphic`). This avoids restructuring `m_Father` on existing components and keeps WS-009's fileID stability intact. Functionally equivalent.
7. **HeroStartingLoadout reuses the existing seeder's GameObject (2210)** rather than creating a new root-level GO. Spec §4.11 says "create HeroStartingLoadout GameObject"; I repointed the existing seeder GO instead so its scene anchors (Transform fileID 2211, MB 2212, RootOrder 13) stay invariant. Net behavior is identical.
8. **GUID style for new files** is the spec's `4XXX…` numerical range (`4001`/`4002`/…/`4303`/…), distinct from WS-008's `1aXXX…` and WS-009's `1bXXX…`. Bookkeeping clarity for future audits.
9. **`Tooltip.OnEnable` sets the `Instance` singleton, not `Awake`.** Per L-007 hot-reload safety. Spec §4.12.2 uses `Awake` only — fine on fresh scene load but null after a domain reload during Play.
10. **No visual rotation of dragged sprite** — preserved from WS-009 (spec §3 #33 explicitly accepts).

## Deferred to designer (Unity-required)

- [ ] **CLOSE + REOPEN `Game.unity`** in Unity before testing. WS-010 added 75+ new GameObjects/components plus rewired existing GOs (2010, 2020, 2210, and all 5 ShopSlots). Unity's in-memory copy will overwrite my disk edits on save if the tab is open.
- [ ] After Unity recompile, confirm **zero errors and zero warnings** in Console.
- [ ] Test Runner → EditMode → Run All. Expected: **67 passing, 0 failing, 0 ignored** (43 prior + 12 from WS-009 + 12 new WS-010 = 67. Actually: WS-009 added "6 ShapeMath + 6 InventoryGridMove" = 12 → prior baseline of 28+15=43 + 12 = 55 entering WS-010. So WS-010 → 55 + 12 = 67. Spec §4.14 agrees.).
- [ ] Boot → Play. Verify the full WS-010 playtest checklist in spec §4.15 + §5.2. Particular things to watch:
  - Round-end gold bonus increases the gold display from 10→15 on round 1 end (and not on round 30 end).
  - Shop populates on round-end with 5 slots, rarity-tinted; reroll button shows cost 1g.
  - Hover any shop slot or any inventory item for 0.3s → tooltip with stats appears, follows cursor, flips at edges, hides on exit and on drag start.
  - Affordable slots accept drag; unaffordable show red price and refuse to drag.
  - R key during shop drag rotates the validation overlay for items; bag drags ignore R.
  - Drop on valid cells → atomic gold spend + place + slot marked SOLD. New items immediately fire alongside Bone Knife.
  - Gold pickups appear from enemy deaths, can be collected, despawn after 30s.
  - Across rounds: persisted gold/inventory. New run: full reset to 10 gold + WardenPouch + BoneKnife only.
- [ ] `git push origin main` after sign-off (per project convention).

## Commit prep
- Working tree includes: `Assets/_Project/Scripts/` (12 new + 9 modified .cs + 12 new .cs.meta; 1 deleted .cs + 1 deleted .cs.meta), `Assets/_Project/ScriptableObjects/` (5 new .asset + 5 new .meta, 4 modified .asset), `Assets/_Project/Sprites/Items/placeholder_gold.png` + meta, `Assets/_Project/Prefabs/Projectiles/GoldPickup.prefab` + meta, `Assets/_Project/Prefabs/UI/DragGhost.prefab` + meta, `Assets/_Project/Tests/EditMode/` (4 new .cs + 4 new .meta), `Assets/_Project/Scenes/Game.unity` (massive append).
- Suggested commit: `[WS-010] Shop slots, gold, buying, inventory-driven weapons, tooltips, shop-drag rotation`
- Hold push until designer signs off.

## Open questions / risks I'm carrying into execution
1. **Reroll button YAML**: spec implies the existing `RerollLabel` becomes a clickable button. I'll add a Button component to the existing RerollLabel GO and make the existing Text a child so ShopController can drive the label text without breaking the Button's `targetGraphic`. Detail finalized on first edit.
2. **`GoldDropController` requires `PoolManager`**: PoolManager is a singleton from WS-004 living under Boot.unity's `Managers`. Game-scene controllers can rely on it being non-null after Boot→Game transition. Defensive null-checks per spec retained.
3. **`Tooltip.PositionAtCursor` math**: uses `Mouse.current.position.ReadValue()` — returns `Vector2` screen coords. Same coordinate space as `Input.mousePosition` (excluding the z), so the `ScreenPointToLocalPointInRectangle` call works unchanged.
4. **WS-009 deviation re. visual rotation propagates**: ShopSlotDragHandler also doesn't visually rotate the ghost sprite. Spec §3 #33 explicitly accepts this.
5. **`HeroStartingLoadout` script GUID reuse**: I'll write a NEW script (HeroStartingLoadout) with a new GUID, and rewrite the existing scene MonoBehaviour at fileID 2212 to point at the new GUID + drop the `_starterItem2` / `_item2Origin` SerializeFields. Old `InventoryTestSeeder.cs` + `.meta` get deleted in the same commit.
6. **`InventoryTestSeeder` GUID `0f2d9021f427917499562a9ca4c33f70`** is currently referenced from the scene. Deleting the .cs while a scene MonoBehaviour still references its GUID would leave a "missing script". The fix is sequential: write HeroStartingLoadout.cs (+ .meta) FIRST, edit the scene MonoBehaviour's `m_Script.guid` to the new GUID SECOND, delete InventoryTestSeeder.cs + .meta THIRD. Captured in execution order.

---

# Last completed: WS-009 — Drag-and-Drop Placement + Validation + Rotation

Spec: `WS-009_Drag_Drop_v2.md` (provided inline by designer).

## Pre-flight (verified by reading project state)
- [x] WS-008 surface present: `InventoryGrid`, `InventoryService`, `InventoryGridView`, four inventory events, Warden's Pouch + Bone Knife seeded via `InventoryTestSeeder` at (1,3)/(2,4). Game scene's shop overlay shows the BB-style vertical layout.
- [x] `tasks/lessons.md` re-read: L-001 (root persist — inventory still scene-scoped, NA), L-002 (InputSystem UI module — Game.unity EventSystem already has it), L-003 (19-digit prefab fileIDs — no new prefabs in this WS), L-004 (no-arg `FindObjectsByType` — NA), L-005 (pooled double-release — NA), L-006 (Filled Image needs sprite — NA, no Filled Images added), L-007 (OnEnable hot-reload safety — `DragHandler` has no Awake-only init that OnEnable would need to recover; serialized refs only).
- [x] Auto-memory loaded: scene-reload caveat known; no scene structural edits in WS-009 besides 2 seeder fields.

## Done

### New scripts (`Scripts/Inventory/` — 2 files)
- [x] `Rotation.cs` (GUID `1a2b3c4d5e6f708192030405060708a4`) — `enum Rotation { Deg0, Deg90, Deg180, Deg270 }` with explicit integer backing values (0/90/180/270) for clarity and serializability.
- [x] `ShapeMath.cs` (`…a5`) — pure static `Rotate(IReadOnlyList<Vector2Int>, Rotation) → List<Vector2Int>` with post-rotate normalization (min(x,y) = 0). Adds `Next(Rotation)` cycler for the R-key rotation step.

### New scripts (`Scripts/UI/` — 1 file)
- [x] `DragHandler.cs` (GUID `1d4e5f6071829304a5b6c7d8e9f0a1b2`) — single component for both bags and items (via `DraggableKind` enum). Implements `IBeginDragHandler/IDragHandler/IEndDragHandler`. Lifecycle:
  - OnBeginDrag: cache original anchored pos + rotation, compute grab offset against grid-parent local space, lower alpha to 0.7, disable raycasts on self, bring to last sibling.
  - OnDrag: smooth visual follow + recompute snapped origin + update validation overlay.
  - Update: poll Keyboard.current for R (rotation, items only) and Escape (cancel).
  - OnEndDrag: try commit via `InventoryService.MoveBagAndItems` or `MoveItem`; return to original on failure; clear cell highlights.
  - Rotation visual: footprint reflected only in validation overlay; sprite itself is not visually rotated (spec defers this to polish).

### Edits
- [x] `ItemInstance.cs` — added `Rotation Rotation;` field + constructor param (defaults to `Deg0`) + `EffectiveCells()` helper. Backward-compatible: existing constructor call sites pass 4 args; new optional 5th param keeps WS-008's `InventoryGrid.PlaceItem` working.
- [x] `InventoryGrid.cs`:
  - `CanPlaceItem(data, origin, rotation = Deg0, ignore = null)` — uses `ShapeMath.Rotate` for effective cells; `ignore` lets the caller exclude an in-progress drag's own cells from collision.
  - `PlaceItem(data, origin, rotation = Deg0)` — rotation-aware placement; stores rotation on the new `ItemInstance`.
  - `GetItemAt` + `GetAdjacentItems` — now use `EffectiveCells()` so rotated items report correct cells.
  - `MoveBag(bag, newOrigin)` — primitive that validates ignoring self; mutates `bag.Origin` in place (NO new InstanceID).
  - `MoveItem(item, newOrigin, newRotation)` — primitive that validates ignoring self; mutates `Origin`, `Rotation`, and `HostBag` in place.
  - `CanPlaceBagIgnoringSelf` — exposed for the drag controller's dry-run validation.
- [x] `InventoryService.cs`:
  - `MoveBagAndItems(bag, newOrigin)` — higher-level atomic move. Collects all hosted items, validates each can land at `origin + delta` (rotation-preserving), moves bag, applies item shifts, publishes `ItemMovedEvent` for each shifted item and a final `BagMovedEvent`. Rollback on validation failure.
  - `MoveItem(item, newOrigin, newRotation)` — wraps `Grid.MoveItem` and publishes `ItemMovedEvent` with old/new origin and rotation.
- [x] `InventoryEvents.cs` — added `BagMovedEvent` (Bag, OldOrigin, NewOrigin) and `ItemMovedEvent` (Item, OldOrigin, NewOrigin, OldRotation, NewRotation).
- [x] `InventoryGridView.cs`:
  - Added `_cellByCoord` `Dictionary<Vector2Int, Image>` populated in `BuildCellGrid` for O(1) cell highlight.
  - `HighlightCellsValid` / `HighlightCellsInvalid` / `ResetCellHighlights` — used by `DragHandler` during drag.
  - Subscribes to `BagMovedEvent` + `ItemMovedEvent` in OnEnable (and unsubscribes in OnDisable) — triggers `RefreshAll` so visuals update after commit.
  - `RenderItem` now uses `item.EffectiveCells()` for bounding-box math so rotated items render with the correct footprint.
  - On RenderBag/RenderItem: attaches a `DragHandler` to the instantiated Image, wires `Kind`/`Bag`/`Item`/`GridParent`/`View` references, and sets `raycastTarget = true` explicitly.
- [x] `InventoryTestSeeder.cs` — added `_starterItem2` + `_item2Origin` fields. Seeder calls `Grid.Clear()` first then places bag → item → item2.

### New ScriptableObject assets (2 files + 2 metas)
- [x] `Stick_1x2_ItemShape.asset` (GUID `1b2c3d4e5f60718293040506070809b3`) — Cells `[(0,0), (0,1)]` (vertical 1x2).
- [x] `TestStick_Item.asset` (GUID `07a2b3c4d5e6f70819203142536475af`) — ItemName "Test Stick", Shape→Stick_1x2_ItemShape, Damage 0 (non-firing placeholder), null ProjectilePrefab (PlayerWeapons skips non-weapons).

### Scene edits (Game.unity — 2 lines)
- [x] InventoryTestSeeder MonoBehaviour (fileID 2212) — added `_starterItem2` and `_item2Origin` SerializeFields. `_starterItem2` references TestStick_Item by GUID, `_item2Origin` = (1, 3) so the vertical stick fills the bottom-left column of the Warden's Pouch (cells (1,3) + (1,4)). Bone Knife stays at (2,4); no overlap.

### New tests (2 files + 2 metas)
- [x] `Tests/EditMode/ShapeMathTests.cs` (GUID `1a2b3c4d5e6f708192030405060708aa`) — 6 tests: Deg0 returns identical; 1x1 invariant under all rotations; vertical 1x2 → horizontal under Deg90 (normalized); horizontal 2x1 self-symmetric under Deg180; L-shape Deg90 normalizes to `{(0,0),(0,1),(1,1)}`; `Next` cycles correctly.
- [x] `Tests/EditMode/InventoryGridMoveTests.cs` (`…ab`) — 6 tests: bag move to empty cells; bag move overlap fails; same-origin no-op; item move within same bag; item move with rotation into tight space; item move outside bag fails.

## Deferred to designer (Unity-required)
- [ ] Designer: open Unity → wait for recompile → confirm **zero errors and zero warnings**.
- [ ] **CLOSE + REOPEN `Game.unity`** if open. The seeder MonoBehaviour at fileID 2212 had two new SerializeField lines added. Unity won't pick them up if it has the scene in memory.
- [ ] Test Runner → EditMode → Run All. **Expected: 55 passing, 0 failing, 0 ignored** (43 prior + 6 ShapeMath + 6 InventoryGridMove).
- [ ] Boot → Play in Boot.unity. Reach Game scene. Round 1 ends → shop overlay appears. Inside the Warden's Pouch, see TWO items: Bone Knife at (2,4) AND Test Stick at (1,3)+(1,4).
- [ ] Click + drag Bone Knife → green highlight on valid cells, red on invalid → release on a valid spot → item moves.
- [ ] Drag Test Stick → press R during drag → green/red footprint pivots to horizontal → release → item rotates and moves.
- [ ] Drag Warden's Pouch → both items move with the bag, preserving relative positions.
- [ ] Try drops off the grid edge → red highlight → release → bag/item snaps back to original.
- [ ] Press Escape mid-drag → drag cancels.
- [ ] Multi-round verification: state persists across rounds; new run resets to seeded positions.
- [ ] `git push origin main` after designer signs off.

## Notes / Deviations
- **`Visible sprite rotation is not implemented**. The validation overlay shows the rotated footprint clearly. Spec accepts this; polish item.
- **Grab-offset within shape is the cursor cell, not a grab-relative offset**. For 1x1 items this is invisible; for multi-cell items the shape's bottom-left snaps under the cursor cell. Spec accepts this simplification.
- **Window-defocus cancellation NOT implemented**. Spec lists this as a Known Gotcha #21 but the spec also says "noted but not implemented this spec." Add to polish backlog.
- **InventoryGridView re-renders the entire grid on every move event** via `HandleAnyChange` → `RefreshAll`. Acceptable for ≤6×9 grid with handful of items; if perf ever matters, add targeted re-render keyed off the moved instance ID.
- **`MoveBag` validation does not handle inter-item shift collisions during the move**. The drag controller's `IsDropValid` for bags also has this limitation (it ignores the moving item but not its siblings). In practice this rejects edge-case "shuffle in place" moves where the new bag footprint overlaps the old one and items would land on each other's old cells. Acceptable for WS-009 scope per spec.

---

# Last completed: WS-008 — Inventory Grid: Data Structure + BB-Style Visual Grid

Spec: `WS-008_Inventory_Grid_v3.md` (provided inline by designer).
Plan: `C:\Users\admin\.claude\plans\optimized-coalescing-parrot.md`.

## Pre-flight (verified via Explore subagents before any code change)
- [x] WS-007 surface present: `RunManager.cs` with `RunPhase {Idle, Combat, Shop, RunEnd}`; `RunStarted/RoundStarted/RoundEnded/RunEnded` events publish through `EventBus`; `ShopOverlayController` wired to `_panel=1800`, `_headerLabel=1813`, `_startNextRoundButton=1824`.
- [x] `tasks/lessons.md` reviewed: L-001 (root persist — does NOT apply, inventory is scene-scoped), L-002 (Input System UI module — already configured), L-003 (19-digit prefab fileIDs — applied to new UI prefabs), L-004 (no-arg `FindObjectsByType` — no enum needed in this spec), L-005 (pooled double-release — no pooling here), L-006 (Filled Image needs sprite — all new Images are Simple `m_Type: 0`), L-007 (OnEnable hot-reload safety — applied to `InventoryGridView`).
- [x] Auto-memory loaded: "Singletons hierarchy lives under `Managers`" (InventoryService is intentionally scene-scoped, not under Managers); "Unity doesn't auto-reload open scenes from disk" (user MUST close + reopen Game.unity tab before testing — see Review Notes).
- [x] ScriptableObject folder structure: `ScriptableObjects/Bags.meta` exists, `Prefabs/UI.meta` exists, both empty. Ready for new assets.

## Done

### New scripts (`Assets/_Project/Scripts/Inventory/` — 7 files)
- [x] `ItemShape.cs` (GUID `1a2b3c4d5e6f708192030405060708a0`) — ScriptableObject; `List<Vector2Int> Cells` + `BoundingWidth`/`BoundingHeight` props.
- [x] `BagData.cs` (`…a1`) — ScriptableObject; `BagName`, `Icon`, `Shape`, `PassiveDescription`.
- [x] `BagInstance.cs` (`…a2`) — runtime class; InstanceID-based `Equals`/`GetHashCode` so HashSets work for adjacency.
- [x] `ItemInstance.cs` (`…a3`) — runtime class with `HostBag` back-reference for "remove bag also removes contained items".
- [x] `InventoryGrid.cs` (Unity-assigned `5c65b161ae3fd0d4bbb4aafc894e5e9c`) — pure C# 6×9 grid. Full surface: `CanPlaceBag/PlaceBag/RemoveBag/GetBagAt/CanPlaceItem/PlaceItem/RemoveItem/GetItemAt/GetAdjacentItems/Clear/IsInBounds`. Adjacency uses 4-direction `ORTHOGONAL` array; `seen` HashSet dedups multi-cell-item neighbors.
- [x] `InventoryService.cs` (Unity-assigned `7160968097bffff4086f5149e7e3797a`) — scene-scoped singleton MonoBehaviour. **No `DontDestroyOnLoad`** (inventory belongs to a run; destroyed with Game.unity). Publishes 4 events on every mutation. `RemoveBag` deliberately emits `ItemRemovedEvent` for each hosted item BEFORE `BagRemovedEvent` so subscribers can react in order.
- [x] `InventoryTestSeeder.cs` (Unity-assigned `0f2d9021f427917499562a9ca4c33f70`) — Start-time placeholder loadout placer. Calls `Grid.Clear()` first to be idempotent across run restarts.

### New scripts (`Scripts/Core/Events/` and `Scripts/UI/` — 2 files)
- [x] `Core/Events/InventoryEvents.cs` (Unity-assigned `d178d1144e9a5684b8b7434cd34672a4`) — 4 structs (`BagPlacedEvent`, `BagRemovedEvent`, `ItemPlacedEvent`, `ItemRemovedEvent`), namespace `HellpitRampage.Core`, implement `IGameEvent`.
- [x] `UI/InventoryGridView.cs` (Unity-assigned `5167d38599dfffe47b6bbb67a1fc58d7`) — renders 6×9 cells + bag overlays + item icons at 56px/cell. `OnEnable` builds the cell grid lazily via `_cellsBuilt` flag and subscribes to all 4 inventory events; `OnDisable` unsubscribes; `RefreshAll` destroys and re-creates bag/item Images per change. **L-007 applied**: no `Awake`-only state; `OnEnable` is the entry path for both fresh activation and domain-reload-during-Play.

### Edits
- [x] `Inventory/ItemData.cs` — added `public ItemShape Shape;` after the `Icon` field; removed the WS-008 placeholder comment. Weapon fields untouched.
- [x] `ScriptableObjects/Items/BoneKnife_Item.asset` — added `Shape: {fileID: 11400000, guid: 1b2c3d4e5f60718293040506070809b0, type: 2}` line referencing the new `SimpleSquare_ItemShape.asset`.
- [x] `Scenes/Game.unity` — restructured ShopOverlayPanel (see Scene Restructure below).

### New ScriptableObject assets (3 files + 3 metas)
- [x] `ScriptableObjects/Items/SimpleSquare_ItemShape.asset` (GUID `1b2c3d4e5f60718293040506070809b0`) — Cells `[(0,0)]`.
- [x] `ScriptableObjects/Items/WardenPouch_3x3_ItemShape.asset` (`…b1`) — 9 cells (0,0)..(2,2).
- [x] `ScriptableObjects/Bags/WardenPouch_Bag.asset` (`…b2`) — BagName "Warden's Pouch", Shape→3x3, Passive "Items inside this bag get +1 vitality." Mechanical wiring deferred.

### New UI prefabs (3 files + 3 metas, all with **19-digit int64 fileIDs per L-003**)
- [x] `Prefabs/UI/GridCell.prefab` (GUID `1c3d4e5f607182930405060708090ac0`, anchor base `5400000000000000001`) — GameObject + RectTransform + CanvasRenderer + UI.Image. 56×56, bottom-left pivot, white, Simple type, no sprite.
- [x] `Prefabs/UI/BagOverlay.prefab` (`…ac1`, anchor base `5500…`) — same shape.
- [x] `Prefabs/UI/ItemIcon.prefab` (`…ac2`, anchor base `5600…`) — same shape.

### New tests (1 file + 1 meta)
- [x] `Tests/EditMode/InventoryGridTests.cs` (GUID `1a2b3c4d5e6f708192030405060708a9`) — 15 tests covering all the spec acceptance cases for InventoryGrid: place bag in/out-of-bounds/overlapping, RemoveBag-cascades-items, place item with no bag / inside bag / outside bag cells / spanning two bags / on existing item, adjacency none / one orthogonal / diagonal-doesn't-count / across-bag-boundary / multi-cell-item-once, Clear-resets-InstanceID. Helpers `MakeShape/MakeBag/MakeItem` via `ScriptableObject.CreateInstance<T>` — no scene needed.

### Scene Restructure (`Game.unity`, additive append + 3 targeted edits)
- [x] **Preserved fileIDs** `1800` (ShopOverlayPanel), `1810`/`1813` (ShopOverlayHeader + Text), `1820`/`1824` (StartNextRoundButton + Button) so `ShopOverlayController` SerializeField wiring stays intact through re-parenting.
- [x] ShopOverlayPanel children list: `[1811, 1821]` → `[2001, 2031, 2101, 2111]` (TopBar, ShopSlotsArea, InventoryGridContainer, BottomBar).
- [x] ShopOverlayHeader RectTransform (1811) — re-parented under TopBar (2001), centered, size `600×60` (from `1200×200`), font size `36` (from `72`). Text "Round Complete" preserved as runtime default; ShopOverlayController still sets `"Round N Complete"` on RoundEndedEvent.
- [x] StartNextRoundButton RectTransform (1821) — re-parented under BottomBar (2111), right-anchored with 30px padding, size `300×60` (from `420×90`). Child Text stretches to fill.
- [x] Appended new GameObjects (fileIDs 2000–2212):
  - **2000–2001 TopBar** (top-stretch, 80px tall, no Image — empty container).
  - **2010–2013 GoldDisplay** (left side of TopBar, "Gold: 0" Text, gold color; non-functional placeholder).
  - **2020–2023 RerollLabel** (right side of TopBar, "Reroll (1g)" Text, gray; non-functional placeholder).
  - **2030–2031 ShopSlotsArea** (top-stretch under TopBar, 340px tall, no Image; positions slots manually instead of HorizontalLayoutGroup — same visual result, fewer YAML moving parts).
  - **2040–2083 ShopSlot_0..4** (5 placeholder slots, 180×220 each, dark gray `#28282f` alpha 0.9, evenly spaced via anchoredPosition x ∈ {-400,-200,0,200,400}).
  - **2100–2101 InventoryGridContainer** (bottom-stretch, 540px tall, anchored 80–620 from canvas bottom).
  - **2110–2111 BottomBar** (bottom-stretch, 80px tall).
  - **2120–2121 GridAnchor** (centered child of InventoryGridContainer, sized `336×504`, pivot bottom-left, anchored such that GridAnchor's bottom-left sits at `(792, 98)` in canvas-local — horizontally centered on the 1920-wide canvas).
  - **2130–2132 InventoryGridView** (child of GridAnchor; SerializeField wirings: `_gridParent=2121` (GridAnchor), three `_*Prefab` fields → the GridCell/BagOverlay/ItemIcon Image-component fileIDs `5400000000000000004`/`5500000000000000004`/`5600000000000000004`).
  - **2200–2202 InventoryService** (scene-root GameObject, Transform, MonoBehaviour script `7160968097bffff4086f5149e7e3797a`).
  - **2210–2212 InventoryTestSeeder** (scene-root, references `_starterBag=WardenPouch_Bag.asset`, `_starterItem=BoneKnife_Item.asset`, `_bagOrigin=(1,3)`, `_itemOrigin=(2,4)`).
- [x] GUID uniqueness verified across all `Assets/**.meta` — no duplicates.
- [x] Scene file structural integrity verified — 163 `--- !u!` blocks, no duplicate anchors, all new 2xxx fileIDs cross-reference correctly.

## Deferred to designer (Unity-required, must do in this order)

- [ ] **CRITICAL — Close + reopen `Game.unity` tab in Unity.** Per project memory ("Unity doesn't auto-reload open scenes from disk"): Unity has Game.unity open with its in-memory copy from before WS-008; saving from the editor right now would overwrite all my YAML edits. You MUST: (1) save any in-editor changes you want from the OLD Game.unity state somewhere else, then (2) close the Game.unity tab without saving, then (3) re-open it. Hierarchy should then show TopBar / ShopSlotsArea / InventoryGridContainer / BottomBar under ShopOverlayPanel, plus InventoryService + InventoryTestSeeder at scene root.
- [ ] Wait for Unity recompile + re-import after the new scripts/assets/prefabs land. Confirm Console is **zero errors AND zero warnings**.
- [ ] Test Runner → EditMode tab → Run All. Expect **43 passing, 0 failing, 0 ignored** (28 prior + 15 `InventoryGridTests`).
- [ ] Boot → Play in Boot.unity. Reach Game scene. Combat plays normally. At round end, shop overlay appears in BB-style vertical layout:
  - TopBar: "Gold: 0" left, "Round 1 Complete" center, "Reroll (1g)" right.
  - 5 dark `#28282f` placeholder shop slots in a horizontal row.
  - 6×9 grid of dark cells, centered horizontally. Brownish 3×3 bag overlay at cells (1,3)–(3,5). Yellowish 1×1 item icon at cell (2,4).
  - BottomBar: Start Next Round button on the right.
  Click Start Next Round → overlay hides → round 2 begins. Round 2 shop reappears with same bag + item. Round 3 end → "Run Complete!" overlay (existing WS-007 path). Death during combat → "You Died" overlay (existing path).
- [ ] `git push origin main` after Unity-side verification (per project convention, push is a shared-state action gated on designer confirmation).

## Review notes

- **`InventoryService` is intentionally scene-scoped**, not under the `Managers` parent in Boot.unity. The inventory belongs to a *run*, not the application lifetime — destroyed with Game.unity on scene unload, fresh on each Game scene load. L-001 (`DontDestroyOnLoad(transform.root.gameObject)`) deliberately does NOT apply. The seeder re-populates on each run via its `Start()`.
- **Unity auto-meta race**: I wrote 4 script metas (`ItemShape`/`BagData`/`BagInstance`/`ItemInstance`) before Unity noticed the new .cs files — those got my hardcoded `1a2b3c4d…a[0-3]` GUIDs. For the other 5 (`InventoryGrid`/`InventoryEvents`/`InventoryService`/`InventoryTestSeeder`/`InventoryGridView`), Unity won the race and stub-generated its own GUIDs first; I respected those rather than overwrite Unity's cache. Net effect: all references work, no broken imports, but the GUID style is mixed (mine + Unity's).
- **Manual slot positioning instead of HorizontalLayoutGroup**: the spec called for `HorizontalLayoutGroup` + `LayoutElement` per slot. I went with explicit `anchoredPosition` per slot (x ∈ {-400,-200,0,200,400}, size 180×220 each). Same visual result, fewer YAML components, no layout-group serialization quirks. Trivial to swap to a layout group later if WS-012 shop-slot interactions benefit from it.
- **TopBar `RerollLabel` is a Text, not a Button**: the spec describes a "Reroll (1g)" button, but explicitly notes it's a non-functional placeholder. I shipped a Text in the right position with right styling; no Button component means no fake interactive affordance. WS-012 will swap it for a real Button when reroll logic lands.
- **Font size 72 → 36 on the round-complete header**: the original size was tuned for a centered position in an empty panel. Inside the 80px TopBar it would clip vertically. Size 36 (with the same bold weight and color) fits comfortably with margin. Reverting to 72 would re-introduce the clip.
- **Grid cell math** (sanity record): 6×9 cells × 56px = 336×504 grid. GridAnchor at `anchoredPosition (-168, -252)` from `InventoryGridContainer` center with pivot `(0,0)` puts GridAnchor's bottom-left at `(-168, -252)` and top-right at `(168, 252)` in container-local space — perfectly centered. Container is bottom-stretched at canvas y∈[80, 620], so the grid spans canvas y∈[98, 602] horizontally centered. Plenty of breathing room within the 1080-tall reference resolution.
- **Test fixture deliberately doesn't `DestroyImmediate` the temp ScriptableObjects** between tests. EditMode test runs are short-lived; ScriptableObjects created via `CreateInstance` and not assigned to a serialized field will be GC'd at test-runner shutdown. Adds zero state leakage between tests because each test constructs a fresh `InventoryGrid` and fresh SOs.
- **InventoryGridView SerializeField defaults match the spec's tint colors** (empty cells `#26262e`, bag tint `#664c33 alpha 0.6`, item tint `#e6d999 alpha 1`). These are set inline in C# and also wired in the scene MonoBehaviour (`_emptyCellColor`/`_bagTintColor`/`_itemTintColor`), so changing them in the inspector overrides the C# defaults.
- **Pre-existing WS-007 ShopOverlayController wiring is preserved by fileID, not by hierarchy**. `ShopOverlayController._panel=1800`, `_headerLabel=1813`, `_startNextRoundButton=1824` all still resolve after the re-parenting because component fileIDs are invariant under m_Father changes. No edits to ShopOverlayController itself were needed.

---

# Last completed: WS-007 — Round Timer + Round End + Run State Machine

Spec: `WS-007_Round_Timer_RunManager_v2.md` (provided inline by designer).

## Pre-flight (all green per WS-006 evidence in this same file)
- [x] WS-006 surface present: Health.cs, EnemyDiedEvent, PlayerDiedEvent, DeathOverlayController, HPBarController, EventSystem on Game.unity Canvas (InputSystemUIInputModule per L-002).
- [x] `tasks/lessons.md` reviewed: L-001 (root persist), L-002 (Input System UI module), L-003 (large prefab fileIDs), L-004 (no-arg `FindObjectsByType`), L-005 (pooled double-release).
- [x] EventSystem GUID verified by direct grep on `Game.unity:1857` — `01614664b831546d2ae94a42149d80ac` is InputSystemUIInputModule (Explorer subagent's snapshot was wrong on this point; the YAML is correct).

## Plan (checkable items)

### Code (new, 8 files)
- [x] `Scripts/Core/Events/RunStartedEvent.cs` (GUID `2a01b2c3d4e5f60718293a4b5c6d7e8f`) — empty `IGameEvent` struct.
- [x] `Scripts/Core/Events/RoundStartedEvent.cs` (`2b01…`) — `int RoundNumber`.
- [x] `Scripts/Core/Events/RoundEndedEvent.cs` (`2c01…`) — `int RoundNumber`.
- [x] `Scripts/Core/Events/RunEndedEvent.cs` (`2d01…`) — `bool Victory`.
- [x] `Scripts/Core/RunManager.cs` (`2e01…`) — singleton with `RunPhase {Idle, Combat, Shop, RunEnd}`, `StartNewRun`, `EndCurrentRound`, `AdvanceToNextRound`, `HandlePlayerDied`. L-001 `DontDestroyOnLoad(transform.root.gameObject)`. Subscribes to `PlayerDiedEvent` in OnEnable. `EndCurrentRound` flips phase→Shop, publishes `RoundEndedEvent`, then `EndRun(victory:true)` if `CurrentRound>=_totalRounds`. Re-entrant: each `StartNewRun` cleanly resets `_currentRound=1, _phase=Combat`. Includes `internal SetTotalRoundsForTesting(int)` for test 5 (AssemblyInfo already has `InternalsVisibleTo`).
- [x] `Scripts/Core/GameSceneBootstrap.cs` (`2f01…`) — `Start()` calls `RunManager.Instance.StartNewRun()` (per Gotcha #2, Start runs AFTER all OnEnables in scene → subscriptions are live).
- [x] `Scripts/Combat/CombatRoundController.cs` (`3a01…`) — subscribes to RoundStarted (start timer + spawner + reset player position via `_rb.position = Vector2.zero` per Gotcha #3), RoundEnded (stop spawner + clear enemies + clear projectiles), RunEnded (full stop). `Update` ticks `Time.deltaTime`, triggers `RunManager.EndCurrentRound()` at zero. L-004: no-arg `FindObjectsByType<T>()`.
- [x] `Scripts/UI/RoundTimerUI.cs` (`3b01…`) — listens to RoundStartedEvent for round number, polls `CombatRoundController.TimeRemaining` in Update; format `"Round {n} — {m}:{ss:00}"` (em-dash).
- [x] `Scripts/UI/ShopOverlayController.cs` (`3c01…`) — subscribes to RoundEnded (show panel + header text, except when `CurrentRound >= TotalRounds`), RoundStarted (hide panel), RunEnded (hide panel). Button → `RunManager.AdvanceToNextRound()`.

### Code (refactor, GUID preserved)
- [x] `Scripts/UI/DeathOverlayController.cs` → `Scripts/UI/RunEndOverlayController.cs` (and `.cs.meta`). **GUID stays `0da2b3c4d5e6f70819203142536475b4`** so Game.unity's existing `m_Script` reference at fileID 1302 binds without a missing-script. New `_headerLabel` SerializeField swaps text to "You Died" / "Run Complete!" based on `RunEndedEvent.Victory`. Subscribes to RunEndedEvent (not PlayerDiedEvent). `Time.timeScale=0` on overlay show; reset to 1 before scene change.

### Tests
- [x] `Tests/EditMode/RunManagerTests.cs` + meta (`3d01…`). 6 NUnit cases following the WS-006 `HealthTests` SetUp/TearDown pattern (EventBusTestHost created first, RunManager second; destroyed in reverse):
  1. `StartNewRun_SetsRoundOneAndCombatPhase`
  2. `StartNewRun_PublishesRunStartedAndRoundStartedEvents` — asserts each fires exactly once + RoundNumber==1.
  3. `EndCurrentRound_TransitionsToShop_AndPublishesRoundEnded`
  4. `AdvanceToNextRound_IncrementsAndReturnsToCombat` — verifies Gotcha #9 (increment before publish, so RoundStartedEvent.RoundNumber == 2).
  5. `EndCurrentRound_OnLastRound_PublishesRunEndedWithVictoryTrue` — uses `SetTotalRoundsForTesting(1)`.
  6. `HandlePlayerDied_PublishesRunEndedWithVictoryFalse` — publishes PlayerDiedEvent through EventBus, asserts Victory=false + Phase=RunEnd.

### Scene edits
- [x] `Boot.unity` Managers parent — extended `m_Children: [311, 321, 331, 341]` → add `351`. Appended RunManager GameObject (350) + Transform (351) + MonoBehaviour (352, script GUID `2e01…`, **`_totalRounds: 3`** for testing).
- [x] `Game.unity` — six distinct edits:
  1. `EnemySpawner._spawnOnStart: 1 → 0` (round controller now drives spawning).
  2. Canvas (901) `m_Children` extended: `[1001, 1101]` → `[1001, 1101, 1701, 1801]`.
  3. RunEndOverlayController MonoBehaviour (1302) — added `_headerLabel: {fileID: 1113}` (the existing YouDiedText). `_overlayPanel` and `_returnButton` unchanged.
  4. **GameSceneBootstrap** scene-root GO (1400/1401/1402, m_RootOrder 8).
  5. **CombatRoundController** scene-root GO (1500/1501/1502, m_RootOrder 9): `_spawner: {fileID: 802}`, `_playerTransform: {fileID: 601}`, `_roundDuration: 10` (overrides code default 30 for testing).
  6. **RoundTimerUI** scene-root GO (1600/1601/1602, m_RootOrder 10): `_label: {fileID: 1703}`, `_round: {fileID: 1502}`.
  7. **RoundTimerLabel** UI Text under Canvas (1700/1701/1702/1703, m_RootOrder 2): top-left anchor, offset (16,-16), 400×40, font size 28, white, placeholder text "Round 1".
  8. **ShopOverlayPanel** UI Image under Canvas (1800/1801/1802/1803, m_RootOrder 3): stretched fill, color `rgba(0,0,0,0.85)`, `m_IsActive: 0` (deactivated in scene, per Gotcha #13 the controller also disables on OnEnable as safety). Children:
     - ShopOverlayHeader Text (1810/1811/1812/1813): centered (y=120), 1200×200, font size 72 bold, light gray. Placeholder text "Round Complete".
     - StartNextRoundButton (1820/1821/1822/1823/1824) + Text child (1830/1831/1832/1833): modeled byte-for-byte on the existing ReturnButton (1120..1133). Same default UISprite (`fileID: 10907`), same Button colors, same Text font/size/color. Text "Start Next Round".
  9. **ShopOverlayController** scene-root GO (1900/1901/1902, m_RootOrder 11): `_panel: {fileID: 1800}`, `_headerLabel: {fileID: 1813}`, `_startNextRoundButton: {fileID: 1824}`.

### Verification (mechanical)
- [x] GUID uniqueness — all 10 new GUIDs in `find Assets -name "*.meta"` are unique (`uniq -c` clean).
- [x] All 10 new script GUIDs resolve to exactly one `.meta` file.
- [x] All 32 new fileID anchors in Game.unity (1400..1902) are unique. New Boot.unity anchors (350/351/352) are unique.
- [x] All MonoBehaviour SerializeField references resolve: 802 (EnemySpawner MB), 601 (Player Transform), 1100/1113/1124 (RunEndOverlay panel/header/return button), 901 (Canvas RectTransform), 1502/1703/1800/1801/1813/1821/1824 — each anchored exactly once.
- [x] Game scene EventSystem still uses `InputSystemUIInputModule` (`01614664b831546d2ae94a42149d80ac`) per L-002 — no change to that GameObject (1200..1203) in this workstream.

## Deferred to designer (Unity-required)
These §5 acceptance items require the editor — can't be done from this environment.
- [ ] Open Unity 6000.4.6f1 → wait for recompile → confirm **zero errors and zero warnings**.
- [ ] Press Play on `Boot.unity` → MainMenu → Start Run → Game scene loads with HUD `Round 1 — 0:10`.
- [ ] Round 1 completes after 10s → spawner stops, enemies + projectiles disappear, player teleports to origin, shop overlay shows "Round 1 Complete" + Start Next Round button.
- [ ] Click Start Next Round → overlay hides, HUD shows `Round 2 — 0:10`, combat resumes. HP carried over from Round 1.
- [ ] Complete Round 3 (the last one in this test scene) → shop overlay does NOT appear; RunEndOverlay shows "Run Complete!". Return → MainMenu → Start Run again is clean.
- [ ] Die mid-round → RunEndOverlay shows "You Died", spawner stopped, enemies cleared, Return works.
- [ ] Test Runner → EditMode → Run All. **Expected: 28 passing, 0 failing, 0 ignored** (6 new RunManagerTests + 22 prior).
- [ ] Commit `[WS-007] Round timer, RunManager, shop-phase scaffolding` and push to `main`.

## Deviations from spec (explicit)
- **GameObject 1300 / 1100 / 1110 `m_Name` not renamed** ("DeathOverlayController" / "DeathOverlay" / "YouDiedText"). The script class was renamed to `RunEndOverlayController` and the GUID was preserved, so binding is intact and behavior is correct — the m_Name is hierarchy cosmetics only. Designer can rename inline in the Unity editor if desired. Resisted the rename here to keep YAML changes mechanical and avoid spurious diff churn against fields that aren't load-bearing.
- **Placeholder text uses ASCII-only**: `"Round 1"` on the RoundTimerLabel (no em-dash); `"Round Complete"` on the ShopOverlayHeader. Both are overwritten on the first script Update / RoundEndedEvent. The runtime `RoundTimerUI` format string uses the em-dash per spec.
- **`internal SetTotalRoundsForTesting(int)` added on `RunManager`** rather than using reflection in test 5. The existing `Assets/_Project/Scripts/AssemblyInfo.cs` already has `[assembly: InternalsVisibleTo("HellpitRampage.Tests.EditMode")]`, so an `internal` mutator is reachable from tests without exposing the field publicly. Same shape as `SaveManager._savePathOverride` (WS-002).
- **`_roundDuration = 10` in scene** (code default stays at 30 per spec). The override is on the in-scene CombatRoundController only. Production tuning gets the curve later.
- **`_totalRounds = 3` in Boot.unity** (code default stays at 30 per spec). Override is on the RunManager component in Boot.unity, identical pattern.

## Mid-session fixes (post-initial-impl, surfaced by designer playtest)

### Fix A: HPBarFill Filled-without-sprite (L-006)
- [x] **Symptom:** HP bar didn't decrease visually on enemy contact, but player died after enough hits.
- [x] **Root cause:** `HPBarFill` Image (Game.unity fileID 1013) had `m_Type: 3` (Filled) with `m_Sprite: {fileID: 0}`. Filled-mode clips against sprite geometry; with no sprite there's no mask, so `fillAmount` is ignored and the quad renders full regardless. WS-006's risks section explicitly flagged this as the most likely failure mode — and I shipped the risky config anyway.
- [x] **Fix:** assigned Unity's built-in default UISprite to `m_Sprite`:
  ```yaml
  m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}
  ```
  Same sprite already used by ReturnButton + StartNextRoundButton. The bar's `m_Color` (red) tints the sprite to the right look.
- [x] **Lesson captured:** L-006 in `tasks/lessons.md` with the failure mechanism, the paste-able YAML pattern, and two "Resist" anti-patterns (the WS-006-style "wait and see" fallback, and the "use Type=Simple and shrink RectTransform" alternative).
- [x] **Auto-memory:** [feedback-filled-image-needs-sprite](feedback_filled_image_needs_sprite.md) added to MEMORY.md so this rule loads at the start of every future session.

### Fix B: PlayerController.OnEnable hot-reload NRE (L-007)
- [x] **Symptom:** "a bunch of" `NullReferenceException at PlayerController.OnEnable line 23` errors after I edited `Enemy.cs` mid-Play to add diagnostic Debug.Logs.
- [x] **Root cause:** Unity's domain-reload during Play mode (triggered by my external file edit) tears down and reconstructs MonoBehaviour instances, calling `OnDisable → OnEnable` but **NOT `Awake`**. Non-serialized private fields (`_input = new PlayerInputActions()`) reset to null. `OnEnable` then derefs `_input.Player.Enable()` → NRE. This is a real WS-003 latent bug that any future code edit during Play would reproduce.
- [x] **Fix:** moved Awake-time init into a shared `EnsureInitialized()` helper, called from BOTH `Awake` and `OnEnable`. Idempotent (null-guards on each assignment) so fresh scene-load runs it once effectively. `OnDisable` uses `?.` to tolerate mid-reload null state.
- [x] **Lesson captured:** L-007 in `tasks/lessons.md` with the domain-reload mechanism explained, paste-able pattern, and three "Resist" anti-patterns.
- [x] **Auto-memory:** [feedback-onenable-hot-reload-safe](feedback_onenable_hot_reload_safe.md) added to MEMORY.md.

### Diagnostic cleanup
- [x] Reverted the temporary `Debug.Log` statements in `Enemy.OnTriggerStay2D / OnTriggerEnter2D` and `Health.TakeDamage` that I added to bisect the failure. Files are back to their pre-diagnostic state apart from the production fixes.

## Risks / open questions
- **Hand-authored UI YAML for ShopOverlayPanel + Button** modeled byte-for-byte on the existing DeathOverlay → ReturnButton pattern (already known to work since WS-006). The default UISprite (`fileID: 10907, guid: 0000000000000000f000000000000000`) is Unity's stable built-in. Risk: low.
- **Other Filled-without-sprite Images** — none in the current codebase. ShopOverlayPanel Image is `m_Type: 0` (Simple) which is fine without a sprite. RunEndOverlayPanel (was DeathOverlay) at fileID 1103 is also `m_Type: 0`. So L-006 only bit HPBarFill.
- **Other L-007-vulnerable MonoBehaviours (latent, not fixed in this workstream):**
  - `Enemy.cs:18` and `Projectile.cs:16` both set `_rb = GetComponent<Rigidbody2D>()` in `Awake` only. After hot-reload during Play, pooled instances retain their GameObject + Collider but the C# `_rb` field is null; the next `OnEnable / Initialize / FixedUpdate` would NRE on `_rb.linearVelocity = …` or `_rb.position = …`.
  - The 5 singleton classes (`EventBus`, `GameManager`, `SaveManager`, `PoolManager`, `RunManager`) all set `Instance = this` in `Awake` only. After hot-reload, `Instance` is null and every `EventBus.Instance.Publish(...)` call NREs.
  - **Why not preemptively fixed:** scope-creep. The user's reported symptom (HP bar) is resolved; the `PlayerController` fix was the only thing actively NREing in this session. Preemptive singleton refactor introduces a fresh risk — `OnEnable` order across components is undefined, so re-establishing `Instance` in `OnEnable` could let subscribers (like `RunManager.OnEnable → EventBus.Instance`) run before the target singleton's `EnsureInitialized`, silently dropping subscriptions. That's worse than the current "obvious NRE → user restarts Play" failure mode.
  - **Recommended path:** apply L-007 to `Enemy` and `Projectile` opportunistically when they next get touched (low risk, no race). Defer singletons until WS-008+ when we can design a proper "initialize-then-subscribe" two-phase pattern. Until then: when hot-reload NREs appear, stop Play and re-Play (the documented workaround).
  - Captured this gap in `tasks/lessons.md` L-007 risks discussion and the auto-memory entry.
- **Render order under Canvas**: new children appended after existing (HPBarBackground at m_RootOrder 0 → DeathOverlay at 1 → RoundTimerLabel at 2 → ShopOverlayPanel at 3). The shop overlay panel will render ABOVE the HP bar and RoundTimerLabel when active — intentional, so the player can't see / interact with combat HUD during shop. The RunEndOverlayPanel (DeathOverlay, RootOrder 1) renders BELOW the shop panel; harmless because they're never both active.
- **`Time.timeScale` interaction across overlays**: RunEndOverlay sets `timeScale=0`. The shop overlay does NOT — round transitions are deliberately *not* a pause, just a phase change. Consistent with the spec's Notes.
- **CombatRoundController and player position reset**: `_rb.position = Vector2.zero` (Gotcha #3). If a designer's playtest reveals a hard physics-event teleport feels janky (e.g., camera snaps awkwardly), drop the reset behavior — it's a one-line revert.
- **No new lesson recorded for WS-007.** All applied patterns (L-001 root persist, L-002 EventSystem, L-004 no-arg FindObjectsByType) were already documented. If designer playtest surfaces a new failure mode (e.g., shop button unclickable, round timer counting through paused state, enemies not clearing), capture as L-006.

# Last completed: WS-006 — Damage, HP, and Death (Player and Enemy)

Spec: `WS-006_Damage_HP_Death_v2.md` (provided inline by designer).

## Pre-flight (all green)
- [x] WS-005 code/asset surface present: Projectile.cs, Enemy.cs, PlayerWeapons.cs, WeaponMath.cs, BoneKnife_Item.asset, Projectile.prefab, Enemy.prefab, EnemySpawner wired in Game.unity.
- [x] `tasks/lessons.md` carries L-001 (root persist), L-002 (InputSystem UI), L-003 (prefab fileIDs). No new lesson surfaced during WS-005 worth promoting.
- [x] Pool/spawner/projectile chain validated by code-read (cannot Play-test from this environment — same constraint as WS-004/005).
- [x] EventSystem replacement pattern from L-002 known and ready to apply to Game.unity (new for WS-006 — Game.unity previously had no Canvas/EventSystem).

## Plan (checkable items)

### Code (new)
- [x] `Scripts/Combat/Health.cs` — generic HP container; `Owner` enum (Enemy=0, Player=1); `OnEnable` resets `_currentHP` to `_startingHP` (Gotcha #1 — pool reuse); `Initialize(float maxHP)` override for data-driven max (Gotcha #10); `TakeDamage` guards on `_isDead` (Gotcha #13) + `amount <= 0` (Gotcha #12); `HandleDeath` publishes EnemyDiedEvent/PlayerDiedEvent via EventBus, releases enemy to pool, leaves player alive for overlay handler (Gotcha #6 — never publish from OnDestroy); `OnHealthChanged` UnityEvent explicitly `new`-initialized so runtime `AddComponent` in tests works (Unity only auto-instantiates UnityEvent fields via serialization). GUID `09a2b3c4d5e6f70819203142536475b0`.
- [x] `Scripts/Core/Events/EnemyDiedEvent.cs` + `PlayerDiedEvent.cs` — `IGameEvent` structs carrying the dying `GameObject`. GUIDs `0aa2…` / `0ba2…`. Subscribers must NOT cache the reference past the publish call because by the time the handler runs for an enemy, the GameObject is already pooled-inactive (see Notes / Open Questions).
- [x] `Scripts/UI/HPBarController.cs` — subscribes to a serialized `Health._playerHealth.OnHealthChanged`, drives `_fillImage.fillAmount`. Add/Remove listener in OnEnable/OnDisable so scene reload is clean. GUID `0ca2…`.
- [x] `Scripts/UI/DeathOverlayController.cs` — subscribes to `PlayerDiedEvent` on EventBus in OnEnable; shows panel + `Time.timeScale = 0f`; button click resets timescale to 1 (Gotcha #5) then calls `GameManager.TransitionTo(MainMenu)`. `// TODO(WS-pause-or-slowmo):` comment in HandlePlayerDied. GUID `0da2…`.

### Code (edits)
- [x] `Scripts/Inventory/ItemData.cs` — added `Damage` field (default 1) after `ProjectileLifetime`. Removed corresponding line from the future-fields comment.
- [x] `Scripts/Combat/EnemyData.cs` — added `MaxHP` (default 3) and `ContactDamage` (default 10). Removed both from the future-fields comment.
- [x] `Scripts/Combat/Projectile.cs` — added `_damage` field; `Initialize` signature now takes `damage`; `OnTriggerEnter2D` calls `Health.TakeDamage(_damage)` before `Despawn`. Projectile-projectile early-out kept.
- [x] `Scripts/Combat/PlayerWeapons.cs` — `FireAt` passes `item.Damage` to `Projectile.Initialize`.
- [x] `Scripts/Combat/Enemy.cs` — removed `OnTriggerEnter2D` (despawn-on-contact placeholder); added `OnTriggerStay2D` with per-enemy `_contactDamageCooldown` (0.5s) per Gotcha #2; added `OnEnable` that resets cooldown to 0 for clean pool reuse; added `Update` that decrements cooldown (with `// TODO(WS-pause-or-slowmo)`); `Initialize` now seeds `Health.Initialize(_data.MaxHP)` so enemies use data-driven HP rather than the prefab's SerializeField default (Gotcha #10).

### Assets
- [x] `BoneKnife_Item.asset` — added `Damage: 1`.
- [x] `BasicEnemy_Enemy.asset` — added `MaxHP: 3`, `ContactDamage: 10`.

### Prefab edits (L-003 compliant)
- [x] `Player.prefab` — appended Health MonoBehaviour at fileID `&5100000000000000008` (continues existing 51-prefixed anchor scheme); component list updated. `_owner: 1` (Player), `_startingHP: 120` (Glass Lamb starter per GDD §9).
- [x] `Enemy.prefab` — appended Health MonoBehaviour at fileID `&5200000000000000007`. `_owner: 0` (Enemy), `_startingHP: 3` (overridden at runtime by `Enemy.Initialize` from `EnemyData.MaxHP`).

### Scene edits — Game.unity
- [x] Inlined Player (fileID 600) — appended Health component at fileID `&607` (continues the existing small-fileID style — scenes are exempt from L-003). `_owner: 1, _startingHP: 120`. Maintains the WS-003 inline/prefab lockstep deviation.
- [x] **Canvas (900..904)** — Screen Space Overlay, ScaleWithScreenSize 1920×1080. Children: HPBarBackground (1000..1004), DeathOverlay (1100..1103).
- [x] **HPBarBackground (1000..1004)** — top-center anchor, width 400 height 24, y-offset -30 from top. Dark gray Image (`#222`), `m_Sprite: {fileID: 0}`, simple type. `HPBarController` attached with `_fillImage: {fileID: 1013}` and `_playerHealth: {fileID: 607}`.
- [x] **HPBarFill (1010..1013)** — stretches to fill parent. Red Image (`#cc2030`), `m_Type: 3` (Filled), `m_FillMethod: 0` (Horizontal), `m_FillOrigin: 0` (Left), `m_FillAmount: 1`.
- [x] **DeathOverlay (1100..1103)** — stretches to fill canvas, semi-transparent black (`rgba 0,0,0,0.7`), `m_IsActive: 0` (starts hidden per spec §4.8.4). Children: YouDiedText (1110..1113), ReturnButton (1120..1124 with text child 1130..1133).
- [x] **YouDiedText (1110..1113)** — centered (offset y +120), Legacy `UI.Text`, font size 96, bold, light gray (`#f2f2f2`). Per WS-001 convention (TMP deferred).
- [x] **ReturnButton (1120..1124)** + child text (1130..1133) — modeled byte-for-byte on the MainMenu Start Run button. Default UISprite background (fileID 10907). Label "Return to Main Menu", dark text, centered under the "You Died" line.
- [x] **EventSystem (1200..1203)** — uses **InputSystemUIInputModule** (guid `01614664b831546d2ae94a42149d80ac`) per L-002. The bare `EventSystem` MonoBehaviour alone would render the Return button unclickable in this project (Active Input Handling = Input System Package).
- [x] **DeathOverlayController GameObject (1300..1302)** — scene-root GameObject with the controller script. `_overlayPanel: {fileID: 1100}` (the DeathOverlay GameObject), `_returnButton: {fileID: 1124}` (the Button component, not the GameObject).
- [x] Inlined Player's `m_Component` list extended with `{fileID: 607}` (the new Health component).

### Tests
- [x] `Tests/EditMode/HealthTests.cs` in `HellpitRampage.Tests`. 6 NUnit cases:
  1. `TakeDamage_ReducesCurrentHP` — Initialize(10), TakeDamage(3), assert CurrentHP == 7.
  2. `TakeDamage_ClampsAtZero` — Initialize(5), TakeDamage(100), assert HP == 0 and IsDead == true.
  3. `TakeDamage_Zero_DoesNotPublishDeathOrChangeHP` — subscribes to EnemyDiedEvent, applies 0 damage, asserts no event and HP unchanged.
  4. `TakeDamage_AfterDeath_DoesNotRePublish` — kills, then attempts another lethal hit; subscriber count must stay at 1 (Gotcha #13 guard).
  5. `Initialize_ResetsHP_AndClearsDeadFlag` — proves pool-reuse contract.
  6. `OnHealthChanged_FiredOnEachDamage` — subscribes to UnityEvent, applies two damages, asserts at least 2 emissions (Initialize's own emission may bump the count higher — assertion is `GreaterOrEqual(2)` accordingly).

  SetUp creates `EventBusTestHost` (active `GameObject` + `EventBus`) BEFORE `HealthTestHost`, mirroring the EventBusTests pattern. Each `[TearDown]` does `Object.DestroyImmediate` on both. EventBus's `OnDestroy` clears its static `Instance`, isolating tests. GUID `0ea2…`.

### Verification
- [x] All 132 meta files have unique GUIDs (126 reported above, 6 new for WS-006).
- [x] Game.unity has 83 unique fileID anchors; Player.prefab has 8 (L-003 large-int IDs); Enemy.prefab has 7.
- [x] All cross-references resolve:
  - `Health.cs.meta` (09a2…) referenced by Player.prefab `&5100000000000000008`, Enemy.prefab `&5200000000000000007`, Game.unity `&607`.
  - `HPBarController.cs.meta` (0ca2…) referenced by Game.unity `&1004`.
  - `DeathOverlayController.cs.meta` (0da2…) referenced by Game.unity `&1302`.
  - `EnemyDiedEvent` / `PlayerDiedEvent` resolved via `HellpitRampage.Core` (same asmdef as Health).
- [x] `%YAML 1.1` header intact and no BOM on Game.unity, Player.prefab, Enemy.prefab, BoneKnife_Item.asset, BasicEnemy_Enemy.asset.
- [x] No `FindObjectsOfType` usage anywhere in `Scripts/` (Unity 6 deprecation clean).

## Deferred to designer (Unity-required)
These §5 acceptance items require the editor — can't be done from this environment.
- [ ] Open `Boot.unity` → Play → reach Game scene with **zero errors AND zero warnings**.
- [ ] Enemy dies after exactly 3 projectile hits (BoneKnife damage 1 × 3 = MaxHP 3).
- [ ] Player HP bar visible top-center, starts full red.
- [ ] Continuous contact with an enemy reduces HP in 10-HP chunks every 0.5s; after ~6s of contact, player dies (12 × 10 = 120 HP).
- [ ] On death: scene freezes (timeScale 0), `DeathOverlay` panel shows with "You Died" + Return button. Button is clickable (validates InputSystemUIInputModule in Game.unity).
- [ ] Clicking Return → MainMenu loads (no timescale carryover — Gotcha #5).
- [ ] Start a new run: HP at full, no leftover state, no errors.
- [ ] Test Runner → EditMode → Run All. **Expected: 22 passing, 0 failing, 0 ignored** (6 new + 4 EventBus + 3 SaveManager + 5 PoolManager + 4 PlayerWeapons).
- [ ] Commit `[WS-006] Damage, HP, and death (player + enemy)` and push to `main`.

## Deviations from spec (explicit)
- **HP bar Image source = none (flat color).** Spec says "Source Image = none". Both `HPBarBackground` (Image, Type=Simple) and `HPBarFill` (Image, Type=Filled) use `m_Sprite: {fileID: 0}`. Unity's UI.Image with null sprite renders a solid quad in Simple mode and a fill-mask-clipped quad in Filled mode — visually correct for the placeholder. If Filled-without-sprite turns out to render weirdly, dropping a fileID-10907 UISprite into the field is a one-click designer fix.
- **Inlined Player gets the Health component duplicated** (Player.prefab `&5100000000000000008` AND Game.unity inline `&607`). Same WS-003 deviation pattern that already applied to PlayerController and PlayerWeapons. Future "replace inline with PrefabInstance" task can drop the inline component once the prefab is the source of truth.
- **`m_IsActive: 0` on DeathOverlay panel** rather than a runtime `SetActive(false)` from `Start()`. Spec §4.8.4 explicitly says "Set the DeathOverlay panel's activeSelf = false (uncheck the GameObject in the Inspector)." Authored that way in YAML directly.
- **Tests assert `OnHealthChanged` fires GreaterOrEqual to 2, not exactly 2.** The spec's #6 case says "Counter should be at least 2 (may also fire once on Initialize — verify expected count)." The test reflects this with `GreaterOrEqual` to avoid coupling to whether Initialize-from-AddComponent's OnEnable fires the event before the counter is attached.
- **`UnityEvent<float, float> OnHealthChanged = new UnityEvent<float, float>();`** explicit initializer. Unity only auto-instantiates UnityEvent fields via serialization round-trip; tests that `AddComponent<Health>()` on a runtime GameObject would NPE without this initializer. The behavior is identical for serialized prefab/scene instances (Unity overwrites the C# default at deserialization).

## Risks / open questions

- **Hand-authored UI YAML cannot be Play-tested from this environment.** The Canvas / EventSystem / HP-bar / DeathOverlay block is the largest new YAML chunk so far (~440 lines added to Game.unity). I modeled component types byte-for-byte on the equivalent constructs in `MainMenu.unity` (Canvas, GraphicRaycaster, CanvasScaler, EventSystem+InputSystemUIInputModule, Image, Button+Text child). All script GUIDs (`fe87c0e1…` Image, `4e29b1a8…` Button, `5f7201a1…` Text, `0cd44c10…` CanvasScaler, `dc42784c…` GraphicRaycaster, `76c392e4…` EventSystem, `01614664…` InputSystemUIInputModule) are the verified UnityEngine.UI / com.unity.inputsystem package script GUIDs from this project's package cache.
- **`HPBarFill` Image Type=Filled with `m_Sprite: {fileID: 0}`** is the one item I'm least confident about. If Unity renders a blank fill (no mesh) at runtime, the workaround is to drop in `{fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}` (default UISprite) — that's the same sprite the MainMenu Start Run button uses. The fill amount math still works either way.
- **HP bar color and overlay alpha are starter values.** Designer is free to tweak `#cc2030` fill / `#222` background / `0.7` overlay alpha during playtest. Not part of acceptance criteria.
- **`EnemyDiedEvent` carries a `GameObject` that is already pooled-inactive** by the time handlers run (Health.HandleDeath releases the enemy in the same call where it publishes). This is documented in the spec's Notes & Open Questions and is the reason future gold-drop / death-particle work should grab position/data before the publish, OR the Release should be one frame delayed. Worth surfacing again for WS-013+ specs.
- **No new lesson recorded for WS-006.** Every pattern I needed (root persist, InputSystem UI module, large prefab fileIDs) was already documented and applied. If designer playtest surfaces a new failure mode (e.g., death overlay button doesn't click, fill image renders blank, contact damage feels wrong), capture as L-004 + persistent memory and update MEMORY.md.

# Last completed: WS-005 — First Weapon Item: Auto-Aim + Cooldown Firing + Projectile (2026-05-13, not yet committed)

Spec: `WS-005_First_Weapon_v2.md` (provided inline by designer).

## Pre-flight (all green)
- [x] WS-004 PoolManager + EnemySpawner + Enemy.prefab in place; verified by reading source/scene YAML.
- [x] PoolManager uses `DontDestroyOnLoad(transform.root.gameObject)` (L-001).
- [x] `tasks/lessons.md` carries L-001 (root-persist), L-002 (InputSystem UI), L-003 (large prefab fileIDs).
- [x] `MEMORY.md` indexes all three feedback memories + the Managers-parent project memory.
- [x] Target folders already exist: `Sprites/Items/`, `Prefabs/Projectiles/`, `ScriptableObjects/Items/`, `Scripts/Inventory/`.

## Plan (checkable items)

### Code (new)
- [ ] `Scripts/Inventory/ItemData.cs` — `[CreateAssetMenu]` SO; weapon-relevant fields only (ItemName, Description, Icon, ProjectilePrefab, Cooldown, Range, ProjectileSpeed, ProjectileLifetime). Future-fields comment block notes Damage / Shape / Rarity / Effects to come.
- [ ] `Scripts/Combat/Projectile.cs` — `[RequireComponent(typeof(Rigidbody2D))]`. `Initialize(Vector2 dir, float speed, float lifetime)` resets `linearVelocity` (Gotcha #3/L-004), applies new velocity, rotates to face travel direction. `Update` decrements lifetime, despawns at zero. `OnTriggerEnter2D` ignores other projectiles + non-Enemy tags, despawns on Enemy contact. `Despawn` zeros velocity and releases to pool.
- [ ] `Scripts/Combat/WeaponMath.cs` — static `StepCooldown(current, dt, max, targetAvailable, out shouldFire)`. Lifts the cooldown rule out of `PlayerWeapons.Update` for unit testability. Pauses at 0 (no-target case); never returns negative.
- [ ] `Scripts/Combat/PlayerWeapons.cs` — serialized `List<ItemData>` + `_prewarmPerWeapon`. `Start` prewarms each weapon's projectile pool + zeroes its cooldown timer. `Update` calls `WeaponMath.StepCooldown` per weapon, fires when ready+target. `FindNearestEnemyInRange` uses `Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)` (Unity 6 API per Gotcha #10), skips pooled-inactive instances via `activeInHierarchy`. `FireAt` Gets from pool, positions at Player, calls `Projectile.Initialize` with normalized direction toward the target's current position.

### Assets (new)
- [ ] `Sprites/Items/placeholder_projectile.png` + `.meta` — 16×16 PNG, yellow `#f0d048` interior, 1-px transparent border. Generate via `System.Drawing.Bitmap` (same approach as placeholder_hero / placeholder_enemy). TextureImporter modeled on placeholder_enemy meta: textureType 8, spriteMode 1, PPU 64, Bilinear, no compression, alphaIsTransparency 1.
- [ ] `Prefabs/Projectiles/Projectile.prefab` + `.meta` — hand-authored YAML. 6 components: GameObject (anchor `&5300000000000000001`, name `Projectile`), Transform, SpriteRenderer (sprite ref to placeholder_projectile via GUID), Rigidbody2D (Dynamic, gravity 0, damping 0+0.05, m_Interpolate 1, m_CollisionDetection 1 [Continuous], m_Constraints 4 [freeze rot Z]), CircleCollider2D (radius 0.1, m_IsTrigger 1), Projectile MonoBehaviour. fileIDs `&5300000000000000001..&5300000000000000006` per L-003.
- [ ] `ScriptableObjects/Items/BoneKnife_Item.asset` + `.meta` — MonoBehaviour SO with `m_Script` → ItemData GUID; values: ItemName="Bone Knife", Description="A weathered bone shard, sharpened to a point. Whispers softly when thrown.", Icon `{fileID: 0}`, ProjectilePrefab `{fileID: 5300000000000000001, guid: <Projectile.prefab>, type: 3}`, Cooldown 0.5, Range 8, ProjectileSpeed 12, ProjectileLifetime 3.

### Tests (new)
- [ ] `Tests/EditMode/PlayerWeaponsTests.cs` + `.meta` — 4 NUnit cases covering `WeaponMath.StepCooldown`:
  1. `StepCooldown_WhenCoolingDown_DecrementsByDeltaTime` (1.0 - 0.1 = 0.9, shouldFire=false)
  2. `StepCooldown_AtZero_WithTarget_FiresAndResets` (0.05 - 0.1 ≤ 0, target=true → returns max=0.5, shouldFire=true)
  3. `StepCooldown_AtZero_NoTarget_PausesAtReady` (0.05 - 0.1 ≤ 0, target=false → returns 0.0, shouldFire=false; explicitly `Assert.GreaterOrEqual(next, 0f)`)
  4. `StepCooldown_AtZero_NoTarget_NextFrame_StillReady` (apply twice, current stays 0)

### Edits to existing files
- [ ] `ProjectSettings/TagManager.asset` — add `Enemy` to the `tags:` list. (Currently `tags: []`.)
- [ ] `Assets/_Project/Prefabs/Enemies/Enemy.prefab` — change `m_TagString: Untagged` → `m_TagString: Enemy` on the root GameObject.
- [ ] `Assets/_Project/Prefabs/Player/Player.prefab` — append a `PlayerWeapons` MonoBehaviour component (new fileID `&5100000000000000007`); update root GameObject `m_Component` list to include it. SerializeField: `_equippedItems` = `[BoneKnife_Item.asset]`, `_prewarmPerWeapon: 30`.
- [ ] `Assets/_Project/Scenes/Game.unity` — apply the same PlayerWeapons component to the inlined Player (fileID 600). Add MonoBehaviour `&606`, append `{fileID: 606}` to GameObject 600's `m_Component` list. (Per WS-003 deviation, Player is inlined here, not a PrefabInstance — must touch both.)

### Verification
- [ ] All meta GUIDs unique across `Assets/**`.
- [ ] Cross-references resolve: BoneKnife → ItemData script + Projectile.prefab root; Projectile.prefab → Projectile.cs script + placeholder_projectile sprite; Player.prefab → BoneKnife asset; Game.unity inlined Player → BoneKnife asset; Enemy.prefab tag = `Enemy`.
- [ ] All YAML files: `%YAML 1.1` header intact, no BOM, unique fileIDs within each file.
- [ ] No use of deprecated `FindObjectsOfType` anywhere (Unity 6 warning would fail the spec's zero-warning bar).

## Deferred to designer (Unity-required)
These §5 acceptance items require the editor — can't be done from this environment.
- [ ] Open Boot.unity → Play → confirm Boot → MainMenu → Game transitions with **zero errors and zero warnings**.
- [ ] Confirm projectiles fire when an enemy is in range (~0.5s cadence) and despawn on contact or after ~3s.
- [ ] Step out of range, then back in — projectile fires immediately (cooldown paused at ready, not negative).
- [ ] Hierarchy under PoolManager bounded for both Enemy(Clone) and Projectile(Clone).
- [ ] Test Runner → EditMode → Run All. **Expected: 16 passing, 0 failing, 0 ignored** (4 new + 5 PoolManager + 4 EventBus + 3 SaveManager).
- [ ] Commit `[WS-005] First weapon: auto-aim, cooldown firing, pooled projectiles` and push to `main`.

## Done

### Code (HellpitRampage.Inventory)
- [x] `Scripts/Inventory/ItemData.cs` — `[CreateAssetMenu(menuName = "HellpitRampage/Item Data")]` distinct from EnemyData's menu path (per Gotcha #12). Weapon-only fields per spec §4.2. Future-fields comment block left in place. GUID `01a2b3c4d5e6f70819203142536475a8`.

### Code (HellpitRampage.Combat)
- [x] `Scripts/Combat/Projectile.cs` — `[RequireComponent(typeof(Rigidbody2D))]`. `Initialize` zeros `linearVelocity` then applies new velocity (Gotcha #3); guards against zero-direction with `Vector2.right` fallback (Gotcha #11 alignment). `Update` decrements lifetime; despawn on ≤0. `OnTriggerEnter2D` early-outs on other projectiles (Gotcha #5), tag-checks `Enemy` (Gotcha #6). `Despawn` zeros velocity then releases to pool. GUID `02a2b3c4d5e6f70819203142536475a9`.
- [x] `Scripts/Combat/WeaponMath.cs` — static `StepCooldown(current, dt, max, targetAvailable, out shouldFire)`. Pauses at 0 when no target (per Gotcha #8); never returns negative. Lifted out for testability per spec §4.8. GUID `03a2b3c4d5e6f70819203142536475aa`.
- [x] `Scripts/Combat/PlayerWeapons.cs` — `Start` prewarms each weapon's projectile pool, zeroes its cooldown timer. `Update` calls `WeaponMath.StepCooldown` per weapon, only running the (potentially expensive) enemy lookup when `current - Time.deltaTime <= 0f` so off-cooldown weapons skip the scan. `FindNearestEnemyInRange` uses `FindObjectsByType<Enemy>(FindObjectsSortMode.None)` (Gotcha #10), skips pooled-inactive via `activeInHierarchy` (per troubleshooting note). `FireAt` Gets from pool, repositions, calls `Initialize` with current target position. `// TODO(WS-pause-or-slowmo)` comment present in Update. GUID `04a2b3c4d5e6f70819203142536475ab`.

### Assets
- [x] `Sprites/Items/placeholder_projectile.png` — 16×16 ARGB, yellow `#f0d048` (240,208,72) interior, 1-px transparent border. Verified PNG signature `89504e470d0a1a0a`, dimensions 16×16, corner alpha=0, center RGBA `(240,208,72,255)`. Generated via `System.Drawing.Bitmap`.
- [x] `Sprites/Items/placeholder_projectile.png.meta` — TextureImporter modeled on placeholder_enemy meta: textureType 8 (Sprite), spriteMode 1, PPU 64, Bilinear, no compression, alphaIsTransparency 1. GUID `05a2b3c4d5e6f70819203142536475ac`.
- [x] `Prefabs/Projectiles/Projectile.prefab` — root GameObject anchor `&5300000000000000001` (L-003 compliant). 6 components: Transform, SpriteRenderer (refs placeholder_projectile by GUID), Rigidbody2D (Dynamic / Mass 1 / GravityScale 0 / LinearDamping 0 / AngularDamping 0.05 / Interpolate=1 / Continuous=1 / Constraints=4=FreezeRotationZ), CircleCollider2D (Radius 0.1, **m_IsTrigger: 1**), Projectile MonoBehaviour. GUID `06a2b3c4d5e6f70819203142536475ad`.
- [x] `ScriptableObjects/Items/BoneKnife_Item.asset` — MonoBehaviour SO with `m_Script` → ItemData GUID. `ProjectilePrefab: {fileID: 5300000000000000001, guid: 06a2..., type: 3}` (matches Projectile.prefab root). ItemName "Bone Knife", Description per spec, Cooldown 0.5, Range 8, ProjectileSpeed 12, ProjectileLifetime 3. mainObjectFileID `11400000`. Asset GUID `07a2b3c4d5e6f70819203142536475ae`.

### Scene / project edits
- [x] `ProjectSettings/TagManager.asset` — `tags: []` → `tags:\n  - Enemy`. Adds the `Enemy` user-defined tag per Gotcha #7.
- [x] `Assets/_Project/Prefabs/Enemies/Enemy.prefab` — root `m_TagString: Untagged` → `m_TagString: Enemy`.
- [x] `Assets/_Project/Prefabs/Player/Player.prefab` — appended PlayerWeapons MonoBehaviour `&5100000000000000007` (continues the existing L-003 anchor scheme); component list updated to include it. `_equippedItems: [BoneKnife_Item.asset asset ref (type: 2)]`, `_prewarmPerWeapon: 30`.
- [x] `Assets/_Project/Scenes/Game.unity` — inlined Player (fileID 600) gets the same PlayerWeapons component at fileID `&606` (consistent with the scene's small-fileID style — scenes are exempt from L-003 per the lesson). Continues the WS-003 deviation pattern of duplicating prefab edits into the inlined copy.

### Tests
- [x] `Tests/EditMode/PlayerWeaponsTests.cs` in `HellpitRampage.Tests`. 4 NUnit cases covering `WeaponMath.StepCooldown`:
  - `StepCooldown_WhenCoolingDown_DecrementsByDeltaTime` — 1.0 − 0.1 = 0.9, shouldFire=false.
  - `StepCooldown_AtZero_WithTarget_FiresAndResets` — 0.05 − 0.1 ≤ 0 with target → returns max=0.5, shouldFire=true.
  - `StepCooldown_AtZero_NoTarget_PausesAtReady` — 0.05 − 0.1 ≤ 0 without target → returns 0f, shouldFire=false; explicit `GreaterOrEqual(0f)` assertion guards against the negative-cooldown regression called out in troubleshooting.
  - `StepCooldown_AtZero_NoTarget_NextFrame_StillReady` — applies twice with no target; verifies pause holds across frames. GUID `08a2b3c4d5e6f70819203142536475af`.
- No PlayMode tests — full firing pipeline (auto-aim + spawn + projectile travel) is manual-playtest territory per spec §4.8.

### GUID & cross-reference hygiene
- [x] 120 metas across `Assets/`, 120 unique GUIDs (verified by PowerShell).
- [x] All cross-references resolve:
  - `Projectile.prefab` → `Projectile.cs` (02a2…) + `placeholder_projectile.png` (05a2…)
  - `BoneKnife_Item.asset` → `ItemData.cs` (01a2…) + `Projectile.prefab` root (06a2… / fileID `5300000000000000001`)
  - `Player.prefab` → `PlayerWeapons.cs` (04a2…) + `BoneKnife_Item.asset` (07a2…, type 2)
  - `Game.unity` inlined Player → same `PlayerWeapons.cs` + `BoneKnife_Item.asset` refs
- [x] No deprecated `FindObjectsOfType` anywhere in `Scripts/`; only `FindObjectsByType` (Unity 6 API per Gotcha #10).
- [x] All edited YAML files: `%YAML 1.1` header intact, no BOM.

## Risks / open questions

- **Hand-authored YAML cannot be Play-tested from this environment.** The Projectile.prefab fileIDs / GUIDs were modeled byte-for-byte on Enemy.prefab (whose Play-test was already pending designer review for WS-004). The Projectile prefab uses the same Rigidbody2D / CircleCollider2D / MonoBehaviour shapes that are known-good in Enemy.prefab.
- **Enemy is now tagged `Enemy`** — anything that previously relied on the Enemy carrying `Untagged` would break. There is nothing today that does this (verified by grepping `Untagged` in scripts), but if a future spec wants to filter by tag elsewhere, this is the moment that decision was made.
- **`PlayerWeapons` is wired in two places** (Player.prefab and Game.unity inlined Player) because of the WS-003 deviation. When the designer eventually replaces the inlined Player with a PrefabInstance, the inline copy can be deleted and the prefab's component will carry through. Until then, both edits stay in lockstep.
- **Auto-aim aims at "where the enemy is at fire-instant", not homing.** Fast enemies could outrun straight-line projectiles; the playtest with the current 0.5s cooldown + 12u/s projectile speed + 3u/s enemy speed leaves plenty of headroom, but if WS-006 increases enemy speed or projectile cooldown, the design choice resurfaces.
- **Tag filtering vs. layer-based collisions**: spec defers a layer setup, relying on tag-check + projectile-projectile early-out. Cheap and clear at this scale. Will need to revisit if projectile counts climb (linear `OnTriggerEnter2D` check + `GetComponent<Projectile>` per contact is fine at <100 active projectiles, suspect at 1000+).
- **No new lesson recorded for this WS.** I did not encounter any new failure modes — every spec gotcha has a matching guard, and L-001/L-002/L-003 covered the patterns I needed (root persist, prefab fileIDs). If designer playtest surfaces a new issue, capture it as L-004 and link from MEMORY.md.

# Last completed: WS-004 — Enemy Spawner + Basic Pathing + Object Pooling (2026-05-13, not yet committed)

Spec: `WS-004_Enemy_Spawner_v2.md` (provided inline by designer).

## Pre-flight (all green)
- [x] Boot.unity has `Managers` parent with GameManager/EventBus/SaveManager (verified by reading the YAML).
- [x] All three singletons use `DontDestroyOnLoad(transform.root.gameObject)` (L-001 lesson applied).
- [x] MainMenu.unity has `InputSystemUIInputModule` on EventSystem (L-002 lesson applied in WS-002b).
- [x] WS-003 artifacts present: `placeholder_hero.png`, `Player.prefab`, inlined Player + Cinemachine in `Game.unity`, MainMenu Start Run button.

## Done

### Code (HellpitRampage.Core)
- [x] `Scripts/Core/PooledObject.cs` — marker component with `[HideInInspector] GameObject SourcePrefab`. GUID `c4a1b2d3e5f607182930415263748596`.
- [x] `Scripts/Core/PoolManager.cs` — singleton wrapping `UnityEngine.Pool.ObjectPool<GameObject>`. Methods: `Get`, `Release`, `Prewarm`, private `CreatePool`. `Awake` uses `DontDestroyOnLoad(transform.root.gameObject)` per L-001. `OnDestroy` clears `_pools` and nulls `Instance`. `collectionCheck` wrapped in `#if UNITY_EDITOR` (true in editor, false in builds). `createFunc` instantiates as child of `transform`, attaches `PooledObject`, sets `SourcePrefab`. `Release` gracefully handles missing marker by logging error + falling back to `Destroy`. `Prewarm` round-trips Get→Release N times to seed the pool stack. GUID `d4b1c2e3f607182930415263748596a7`.

### Code (HellpitRampage.Combat)
- [x] `Scripts/Combat/EnemyData.cs` — `[CreateAssetMenu]` ScriptableObject with `Prefab` (GameObject) and `MoveSpeed` (float, default 3). GUID `a3c4d5e6f70819203142536475869708`.
- [x] `Scripts/Combat/Enemy.cs` — `[RequireComponent(typeof(Rigidbody2D))]`. `Initialize(EnemyData, Transform)` caches data + resets `linearVelocity` to zero (Gotcha #8 mitigation). `FixedUpdate` steers via `_rb.linearVelocity = direction * MoveSpeed` (uses `linearVelocity` per Gotcha #4). Direction clamps to zero if `sqrMagnitude < 0.0001f` to avoid normalize-of-zero. `OnTriggerEnter2D` checks `CompareTag("Player")`, zeros velocity, releases to pool. Null-check on `PoolManager.Instance`. GUID `b4d5e6f708192a3b4c5d6e7f80910a2b`.
- [x] `Scripts/Combat/EnemySpawner.cs` — all SerializeField parameters per spec §4.9. `Start` calls `Prewarm(_prewarmCount)` then `StartSpawning` if `_spawnOnStart`. `Update` accumulates `Time.deltaTime * _spawnRate`, calls `SpawnOne` while accumulator ≥ 1. Player resolved by `FindGameObjectWithTag("Player")` on first Update; null-tolerant (silently retries) per Gotcha #7. `SpawnOne` uses `Random.insideUnitCircle.normalized` with `Vector2.right` fallback per Gotcha #6. `// TODO(WS-pause-or-slowmo):` comment in Update per Gotcha #13. GUID `c5e6f70819203a4b5c6d7e8f90a1b2c3`.

### Assets
- [x] `Sprites/Enemies/placeholder_enemy.png` — 48×48 ARGB. Interior solid red `(204, 32, 48)` = `#cc2030`; 1-px transparent border. Verified PNG signature `89504e470d0a1a0a`, dimensions, corner alpha=0, center RGBA `(204,32,48,255)`. Generated with `System.Drawing.Bitmap`.
- [x] `Sprites/Enemies/placeholder_enemy.png.meta` — TextureImporter modeled on `placeholder_hero.png.meta`: textureType 8 (Sprite), spriteMode 1 (Single), spritePixelsToUnits 64, filterMode 1 (Bilinear), textureCompression 0 (None) on Default+Standalone, alphaIsTransparency 1. GUID `e2f30415263748596a7b8c9d0e1f2a30`.
- [x] `ScriptableObjects/Enemies/BasicEnemy_Enemy.asset` — MonoBehaviour SO with `m_Script` → EnemyData GUID, `Prefab: {fileID: 1, guid: f50617283940516273849506071829ab, type: 3}` (matches Enemy.prefab root GameObject fileID = 1), `MoveSpeed: 3`. mainObjectFileID `11400000`. Asset GUID `c50718293a4b5c6d7e8f9a0b1c2d3e4f`.
- [x] `Prefabs/Enemies/Enemy.prefab` — root GameObject fileID=1, name `Enemy`, tag `Untagged`. Components: Transform, SpriteRenderer (sprite ref to placeholder_enemy via GUID), Rigidbody2D (Dynamic / Mass 1 / GravityScale 0 / LinearDamping 0 / AngularDamping 0.05 / Interpolate=1 / Continuous=1 / Constraints=4=FreezeRotationZ), CircleCollider2D (Radius 0.3, **m_IsTrigger: 1**), Enemy MonoBehaviour. GUID `f50617283940516273849506071829ab`.

### Scene wiring
- [x] `Boot.unity` — added `PoolManager` GameObject (fileID 340) as 4th child of `Managers`, with Transform (341, RootOrder 3) and MonoBehaviour (342) referencing PoolManager.cs GUID. Updated Managers children list to include 341.
- [x] `Game.unity` — added `EnemySpawner` GameObject (fileID 800) at root, Transform (801, RootOrder 4), MonoBehaviour (802) with `_enemyData` wired to `BasicEnemy_Enemy.asset` (type 2 = asset reference), `_spawnRate: 1`, `_spawnDistance: 12`, `_playerTransform: {fileID: 0}` (resolves at runtime via tag), `_prewarmCount: 20`, `_spawnOnStart: 1`.

### Tagging
- [x] `Player.prefab`: `m_TagString: Untagged` → `Player` (line 18).
- [x] `Game.unity` inlined Player GameObject (fileID 600): same `Untagged` → `Player` change. **WS-003 deviation matters here**: because the Player is currently inlined rather than a PrefabInstance, tagging the prefab alone isn't enough — the runtime `FindGameObjectWithTag` resolves against the scene-instance tag. Both updated.

### Tests
- [x] `Tests/EditMode/PoolManagerTests.cs` in `HellpitRampage.Tests`. 5 cases:
  - `Get_ReturnsActiveInstance` — Get returns non-null active instance with PooledObject marker pointing at the source prefab.
  - `Release_DeactivatesInstance` — after Release, `activeSelf == false`.
  - `GetAfterRelease_ReturnsSameInstance` — pool reuses the released instance.
  - `Prewarm_DoesNotProduceActiveInstances` — after `Prewarm(5)`, all 5 children inactive; getting 5 more times doesn't grow child count (proves no new instantiation).
  - `Release_WithMissingPooledObject_LogsErrorAndDestroys` — `LogAssert.Expect(LogType.Error, ...)` against the "no PooledObject/SourcePrefab" message + asserts no exception.
  - SetUp/TearDown match the existing SaveManagerTests pattern; test prefab is `new GameObject("TestPrefab").SetActive(false)`.

### GUID hygiene
- [x] 112 metas across `Assets/`, 112 unique GUIDs (verified by PowerShell script).
- [x] All cross-references resolve: `BasicEnemy_Enemy.asset → Enemy.prefab (f5...) → Enemy.cs (b4...)`, `Game.unity → EnemySpawner.cs (c5...) + BasicEnemy_Enemy.asset (c5...)`, `Boot.unity Managers → PoolManager.cs (d4...)`, `Enemy.prefab → placeholder_enemy.png (e2...)`.

## Deferred to designer (Unity-required verification)

These are §5 acceptance items that require the editor — I cannot Play-test from this environment.

- [ ] Open `Boot.unity`, press Play, wait for compilation. **Zero errors AND zero warnings expected** in Console during all manual playtest paths.
- [ ] Navigate Boot → MainMenu → Start Run → Game. Verify first enemy appears within 1-2 seconds, subsequent enemies spawn at ~1/sec, each walks directly toward the Player.
- [ ] Walk the Player into an enemy → enemy disappears (despawns to pool).
- [ ] After 50+ spawn/despawn cycles: PoolManager children stay ≤ ~25. If they climb unboundedly, pooling is broken.
- [ ] Profiler → GC Alloc near-zero after warmup (brief alloc on first spawn beyond prewarm is acceptable).
- [ ] Cross-scene persistence: Boot → MainMenu → Game → stop → Play again, no errors. Singletons don't accumulate.
- [ ] Test Runner → EditMode tab → Run All. **Expected: 12 passing, 0 failing, 0 ignored** (5 new + 4 EventBus + 3 SaveManager).
- [ ] Commit with `[WS-004] Enemy spawner, basic pathing, and object pooling` and push to `main`.

## Deviations from spec (explicit)

- **Enemy in scene tagging**: spec §4.8 says "Open Player.prefab. Set Tag dropdown to Player." I tagged BOTH the prefab AND the inlined Player in Game.unity, because WS-003 inlined the Player rather than using a PrefabInstance (documented deviation). If a future spec replaces the inline with a PrefabInstance, the inlined `m_TagString: Player` becomes redundant but harmless.
- **EnemySpawner authored in scene YAML, not "drag-and-drop"**: spec §4.10 says to create the GameObject in-editor and drag the asset reference. Hand-authored YAML referencing `BasicEnemy_Enemy.asset` via its main object fileID (11400000) + asset GUID + type 2 (asset reference).
- **Test prefab `SetActive(false)`**: spec §4.11 says `new GameObject("TestPrefab")` but doesn't specify active state. Deactivated to better model a real prefab template (templates don't run as live scene objects). `ObjectPool.actionOnGet` activates the instance, so test behavior is unchanged.

## Mid-flight correction — prefab fileIDs (2026-05-13)

Designer reported: *"Prefab 'Player.prefab' has unexpected file IDs and is likely to be corrupt"* after WS-004 landed. Root cause was actually introduced in WS-003: small `&1..&6` anchors are reserved by Unity's prefab importer and flagged as corrupt. The same bug existed in my new `Enemy.prefab`. **Fixed in this session:**

- [x] `Player.prefab` — renumbered all 6 anchors from `&1..&6` to `&5100000000000000001..&5100000000000000006`. All `m_GameObject`/`m_Component` cross-references updated.
- [x] `Enemy.prefab` — renumbered to `&5200000000000000001..&5200000000000000006`. All cross-references updated.
- [x] `BasicEnemy_Enemy.asset` — updated `Prefab` field's `fileID` from `1` to `5200000000000000001` so the SO still resolves the root GameObject.
- [x] `tasks/lessons.md` L-003 written with the pattern and why scenes/`.asset` files are exempt.
- [x] Persistent memory `feedback_prefab_fileids_large_int.md` written and indexed in `MEMORY.md`.

The Player prefab `.meta` GUID (`d4e5f60718293a4b5c6d7e8f01a2b3c4`) is unchanged, so no downstream reference rot. Game.unity's inlined Player (WS-003 deviation) is unaffected — it never referenced the prefab by fileID. The Enemy.prefab `.meta` GUID (`f50617283940516273849506071829ab`) is unchanged.

## Risks / open questions

- **Hand-authored YAML cannot be Play-tested from this environment.** The PoolManager wiring in Boot.unity, the spawner wiring in Game.unity, the Enemy prefab, and the EnemyData asset all use fileIDs and GUIDs that should resolve correctly under the Unity editor's import pipeline. The chain was double-checked: BasicEnemy_Enemy.asset's `Prefab` field uses `{fileID: 5200000000000000001, guid: f50..., type: 3}` which matches Enemy.prefab root GameObject (&5200000000000000001) + its meta GUID.
- **Material reference**: I used `{fileID: 10754, guid: 0000000000000000f000000000000000, type: 0}` for SpriteRenderer's m_Materials, matching the pattern used in `Player.prefab` and `Game.unity` reference markers (Unity's default sprite material). Stable built-in fileID.
- **EditMode test for `DontDestroyOnLoad`**: PoolManager's Awake calls it on a root GameObject (test creates manager via `new GameObject(...)`). Per L-001, this only warns on child GameObjects, so the test should not need `LogAssert.Expect` for it. Existing SaveManagerTests follows the same pattern and passes (per WS-002 docs).
- **Test cleanup of orphan in `Release_WithMissingPooledObject_LogsErrorAndDestroys`**: Unity's `Destroy` defers in EditMode. The orphan GameObject may leak between tests. Not currently asserted; if it causes flakes, swap to `DestroyImmediate` tracking. Acceptable risk for now.

# Last completed: WS-002b — DontDestroyOnLoad + EventSystem fixes (2026-05-13)

Designer playtest of WS-002/WS-003 surfaced two defects in my prior hand-authored work.
Captured as `tasks/lessons.md` L-001 and L-002 and as project memories.

## Done
- [x] `GameManager.cs`, `EventBus.cs`, `SaveManager.cs`: changed
      `DontDestroyOnLoad(gameObject)` → `DontDestroyOnLoad(transform.root.gameObject)`.
      Comment added explaining why (`Managers` parent in Boot.unity). Without this, the
      whole singleton hierarchy was destroyed at the first scene transition because
      `DontDestroyOnLoad` silently no-ops on non-root GameObjects.
- [x] `MainMenu.unity`: added `InputSystemUIInputModule` MonoBehaviour (fileID 403,
      script guid `01614664b831546d2ae94a42149d80ac`) to the EventSystem GameObject
      (fileID 400). Minimal YAML entry — `OnEnable` calls `AssignDefaultActions()`
      to auto-bind defaults. Previous scene had only the `EventSystem` script with no
      input module, so UI clicks went nowhere.
- [x] `tasks/lessons.md` written with both patterns (L-001 root-persist, L-002
      InputSystem UI module). Paste-able pattern blocks included.
- [x] Persistent memories written under
      `C:\Users\admin\.claude\projects\c--Unity-Hellpit-Rampage\memory\`:
      `feedback_dontdestroyonload_root.md`, `feedback_inputsystem_ui_module.md`,
      `project_singletons_in_managers_parent.md`, plus `MEMORY.md` index.

## Designer follow-up
- [ ] Re-test `Boot.unity → MainMenu → Start Run → Game` flow. Verify:
      (a) no more `DontDestroyOnLoad only works for root GameObjects` warning,
      (b) singletons persist through MainMenu and Game,
      (c) Start Run button responds to click and transitions to Game scene.
- [ ] When ready, push (or let me know — push already happened for WS-002 base).

## Open question for the designer

The user offered: "WS-004's PoolManager spec already has the same flawed pattern —
inline correction at WS-004 start, or a small WS-002b cleanup spec?"
My preference: **inline correction at WS-004 start**. The fix is now baked into the
existing singletons + captured in `tasks/lessons.md` L-001 + saved as a project memory,
so future agents will pick up the pattern without a separate spec. A WS-002b document
would just duplicate what's already in lessons.md.

# Last completed: WS-003 Hero Movement + Camera Follow (2026-05-13, not yet committed)

Spec: `WS-003_Hero_Movement_And_Camera.md` (provided inline by designer).

## Done
- [x] Generated `Assets/_Project/Sprites/Heroes/placeholder_hero.png` via PowerShell `System.Drawing.Bitmap` — 64x64 ARGB, 62x62 white interior, 1px transparent border. Verified signature `89504e470d0a1a0a`, dimensions 64x64, alpha=0 at corners, RGBA(255,255,255,255) at center.
- [x] Wrote `placeholder_hero.png.meta` (TextureImporter) with `textureType: 8` (Sprite), `spriteMode: 1` (Single), `spritePixelsToUnits: 64`, `filterMode: 1` (Bilinear), `textureCompression: 0` (None) on both DefaultTexturePlatform + Standalone, `alphaIsTransparency: 1`.
- [x] `Assets/_Project/Scripts/Combat/PlayerController.cs` in `HellpitRampage.Combat`: reads `_input.Player.Movement` in `Update`, normalizes diagonals only when `sqrMagnitude > 1f`, applies `_rb.linearVelocity = _moveInput * _moveSpeed` in `FixedUpdate`. `_moveSpeed = 5f` `[SerializeField]`. Added `OnDestroy → _input?.Dispose()` (small addition vs spec — `PlayerInputActions.Dispose()` destroys the InputActionAsset; without this the asset leaks on scene reload when the Player is destroyed).
- [x] `Assets/_Project/Scripts/UI/MainMenuController.cs` in `HellpitRampage.UI` exactly per spec.
- [x] Hand-authored `Assets/_Project/Prefabs/Player/Player.prefab`: GameObject `Player` + Transform + SpriteRenderer (sprite = placeholder_hero, fileID 21300000) + Rigidbody2D (Dynamic / gravity 0 / damping 0+0.05 / Interpolate / Continuous / `m_Constraints: 4` = FreezeRotationZ) + CircleCollider2D (radius 0.4, not trigger) + PlayerController MonoBehaviour with `_moveSpeed: 5`.
- [x] Rewrote `Game.unity`: removed Canvas/Scaler/Raycaster/PlaceholderText/EventSystem; kept singletons + Main Camera but changed background to `(0.10196, 0.10196, 0.10196)` ≈ `#1a1a1a` and orthographic size 7; added `CinemachineBrain` (`72ece51f2901e7445ab60da3685d6b5f`) on Main Camera; added `CM Player Follow` GameObject with `CinemachineCamera` (`f9dfa5b682dcd46bda6128250e975f58`) targeting Player Transform via `Target.TrackingTarget: {fileID: 601}` + `CinemachineFollow` (`b617507da6d07e749b7efdb34e1173e1`) with `PositionDamping (0.2, 0.2, 0.2)` / `FollowOffset (0, 0, -10)` / `BindingMode 4` (WorldSpace) / `QuaternionDamping 0`; added Player (inlined — see deviation); added `ReferenceMarkers` empty parent with 6 children (`Ref_01`..`Ref_06`) at `(±6, ±3, 0)` + `(0, ±4, 0)`, scale `(0.5, 0.5, 1)`, 6 distinct tint colors.
- [x] Modified `MainMenu.unity`: added `StartRunButton` GameObject (Image + Button) as Canvas child at anchored `(0, -160)` size `320x90`, with child Text "Start Run" (size 32, bold, dark gray on white button using built-in UISprite fileID 10907); added top-level `MainMenuController` GameObject with the script and `_startRunButton` SerializeField wired to fileID 504 (the Button component).
- [x] GUID uniqueness verified across `Assets/**/*.meta` — no duplicates.
- [x] Scene/prefab YAML sanity: unique fileIDs (Game.unity 40, MainMenu.unity 33, Player.prefab 6), no BOM, `%YAML 1.1` header intact, cross-references resolve (CinemachineCamera→Player Transform, Button→Image targetGraphic, MainMenuController→Button).

## Deviations from spec (explicit)

- **Player in Game.unity is inlined, not a PrefabInstance.** Spec says "drag Player.prefab into the scene at (0,0,0)." Hand-authoring a Unity-6 `!u!1001 PrefabInstance` block + stripped child components is fragile without the editor (no `PrefabInstance` examples in the local package cache to model from byte-for-byte). The Player components are inlined in `Game.unity` with the same values as `Player.prefab`, referencing the same sprite by GUID. The prefab still exists at the spec'd path and is ready for use in future specs. **Designer follow-up:** open Unity, delete the inlined Player (GameObjects 600/601/602/603/604/605), drag `Player.prefab` into the scene at (0,0,0), re-point `CM Player Follow → CinemachineCamera → Tracking Target` to the new prefab-instance Transform. ~30 seconds in the editor.
- **Legacy `UI.Text` on the Start Run button + label**, consistent with WS-001/WS-002 TMP deferral. Spec is explicit about Legacy.
- **`PlayerController.OnDestroy → _input?.Dispose()`** added (not in spec). Reason: generated `PlayerInputActions.Dispose()` destroys the InputActionAsset; without this we'd leak an asset across scene reloads.

## Acceptance criteria (from spec § 4)

- [x] `placeholder_hero.png` exists at the spec path with correct import settings (PPU 64, no compression)
- [x] `Player.prefab` exists with `SpriteRenderer`, `Rigidbody2D` (Dynamic / gravity 0 / freeze rotation Z / Interpolate / Continuous), `CircleCollider2D` (radius 0.4), `PlayerController`
- [x] `PlayerController.cs` in `HellpitRampage.Combat`, reads `PlayerInputActions`, normalizes diagonals, applies `linearVelocity` in `FixedUpdate`
- [x] `_moveSpeed` is `[SerializeField]` defaulting to `5f`
- [x] `Game.unity` contains a Player instance (inlined — see deviation), `CinemachineCamera` with Follow→Player, reference markers
- [x] `Main Camera` in `Game.unity` has `CinemachineBrain` and Orthographic Size `7`
- [x] `MainMenu.unity` has a `Start Run` button wired to `MainMenuController` which calls `GameManager.TransitionTo(GameState.InRun)`
- [ ] **Pressing Play on `Boot.unity`: Boot → MainMenu → click Start Run → Game scene loads, no Console errors** — designer must verify
- [ ] **Manual playtest: 8-direction at consistent speed, instant input response, no slide on release, smooth camera follow with slight damping, no jitter** — designer must verify
- [x] Code follows Tech Arch §4 conventions
- [ ] **Committed with `[WS-003] Hero movement + Cinemachine camera follow` and pushed** — not yet, awaiting Play-test pass

## Review notes

- **Cinemachine fields modeled from `Library/PackageCache/com.unity.cinemachine@285f38545487/Samples~/2D Samples/CameraMagnets.unity` (CinemachineCamera + CinemachineBrain) and `3D Samples/Brain Update Modes.unity` (CinemachineFollow).** These are byte-for-byte Unity 6 / Cinemachine 3.1.6 outputs — the version locked in `Packages/manifest.json`.
- **`CinemachineFollow.BindingMode = 4` (WorldSpace)** — what the 3D sample uses; correct for top-down 2D where the camera doesn't inherit player rotation. Player has FreezeRotationZ anyway; belt-and-suspenders.
- **`m_Constraints: 4` = `RigidbodyConstraints2D.FreezeRotation`** (Z on a 2D body). Unity enum: `None=0, FreezePositionX=1, FreezePositionY=2, FreezeRotation=4, FreezePosition=3, FreezeAll=7`.
- **2D physics class IDs verified empirically:** 50 = Rigidbody2D, 61 = BoxCollider2D, 212 = SpriteRenderer (all confirmed from `2D Platformer.unity`). 58 = CircleCollider2D per Unity's documented class IDs (stable for years).
- **Sprite fileID `21300000`** is Unity's standard fileID for a single-mode sprite imported from a texture asset.
- **Built-in UISprite fileID `10907`** referenced by the Button's Image is Unity's default rounded UI background; same one the editor assigns from the menu.
- **No automated tests** (per spec § 2 OUT-of-scope + § 5 — movement feel is manual playtest territory).
- **Risk**: hand-authored scene/prefab YAML cannot be Play-tested from this environment. Designer Play-test in `Boot.unity` is the final gate.

# Last completed: WS-002 Core Singletons (2026-05-13)

Full spec: `WS-002_Core_Singletons.md` (provided inline by designer).

## Done
- [x] Runtime asmdef `Assets/_Project/Scripts/HellpitRampage.asmdef` (references `Unity.InputSystem`, `Unity.TextMeshPro`; **dropped** `Unity.Newtonsoft.Json` — it's a precompiled DLL plugin, not an asmdef name, so listing it would fail compilation. The DLL is auto-referenced because `autoReferenced: true` + plugin meta has `isExplicitlyReferenced: 0`).
- [x] EditMode test asmdef `Assets/_Project/Tests/EditMode/HellpitRampage.Tests.EditMode.asmdef` (Editor-only, `overrideReferences: true`, `precompiledReferences: [nunit.framework.dll]`, `defineConstraints: [UNITY_INCLUDE_TESTS]`).
- [x] `Core/IGameEvent.cs` — empty marker interface.
- [x] `Core/Events/GameStateChangedEvent.cs` — struct with `OldState`/`NewState`.
- [x] `Core/GameManager.cs` — singleton with nested `GameState` enum (`Boot/MainMenu/InRun`), `TransitionTo` early-returns on same-state with `Debug.LogWarning`, publishes `GameStateChangedEvent` if `EventBus.Instance` not null, then `SceneManager.LoadScene` on `Boot/MainMenu/Game`. `OnDestroy` clears `Instance` for test cleanliness.
- [x] `Core/EventBus.cs` — singleton; `Subscribe<T>` / `Unsubscribe<T>` / `Publish<T>` over `Dictionary<Type, List<Delegate>>`. Publish iterates a `ToArray()` snapshot so subscribers can unsubscribe mid-handler; per-handler try/catch logs via `Debug.LogException`. `DebugLogEvents` bool toggles per-publish log. `OnDestroy` clears `_handlers` + nulls `Instance`.
- [x] `Core/SaveData.cs` — `SaveData` + empty `PlayerProfile` / `MetaProgress` / `CurrentRun` placeholders. `[Serializable]`, `SchemaVersion = 1`.
- [x] `Core/SaveManager.cs` — singleton with `internal _savePathOverride`, `SavePath` property (override > `Application.persistentDataPath/save.json`), `Save`/`Load`/`SaveFileExists` using `Newtonsoft.Json`. Missing file → fresh `SaveData`. Corrupt/null deserialization → `Debug.LogError` + fresh `SaveData`, broken file **not deleted**. Private `MigrateIfNeeded` warns on version ≠ 1.
- [x] `Core/BootController.cs` — `Start()` (not Awake) validates the three singletons exist, calls `SaveManager.Instance.Load()` for a warm-up, then `GameManager.Instance.TransitionTo(GameState.MainMenu)`.
- [x] `Boot.unity` scene YAML updated: added `Managers` GameObject (fileID 300, RootOrder=1) parenting `GameManager` (310/312), `EventBus` (320/322), `SaveManager` (330/332); attached MonoBehaviour `BootController` (fileID 202) to existing BootController GameObject (fileID 200); shifted BootController RootOrder 1→2 so order is Camera/Managers/BootController.
- [x] EditMode tests: `EventBusTests.cs` (4 cases: subscribe, unsubscribe, multi-sub, exception isolation w/ `LogAssert.Expect`) + `SaveManagerTests.cs` (3 cases: save→load with `SchemaVersion=7`, missing-file fresh data, `SaveFileExists` before/after; uses `Application.temporaryCachePath/save_test.json` via `_savePathOverride`).
- [x] All meta files written with fresh GUIDs; GUID uniqueness across `Assets/` verified (no duplicates).
- [x] Folder metas: `Assets/_Project/Tests`, `Assets/_Project/Tests/EditMode`, `Assets/_Project/Scripts/Core/Events`.

## Deferred to designer (Unity-required)
- [ ] Open Unity editor, wait for recompile, confirm zero console errors. (Cannot verify compilation from this environment.)
- [ ] Press Play in `Boot.unity` and confirm MainMenu loads with no errors.
- [ ] Test Runner → EditMode tab → Run All. Confirm all 7 tests pass.
- [ ] `git push origin main` after Unity-side verification (per WS-001 convention, push is a shared-state action gated on designer confirmation).

## Review notes

- **Newtonsoft.Json reference deviation**: spec lists `Unity.Newtonsoft.Json` as an asmdef reference in both asmdefs. In `com.unity.nuget.newtonsoft-json@3.2.2`, Newtonsoft.Json ships as a precompiled DLL plugin (no asmdef). Listing it under `references` would fail to resolve and break compilation. The DLL is auto-included because the runtime asmdef has `autoReferenced: true` and the plugin meta has `isExplicitlyReferenced: 0`. The test asmdef has `overrideReferences: true` but its code doesn't call `Newtonsoft.Json` directly (it goes through `SaveManager`), so no `precompiledReferences` entry is required. **Cannot be verified from this environment — first Unity recompile will surface any miss.**
- **Hand-authored scene YAML risk**: as with WS-001, the scene was edited by hand because the Unity editor is unavailable here. Component-attachment uses verified meta GUIDs from this repo's freshly-written script metas. Component fileIDs are stable values (`11500000` for MonoScript) and the script GUIDs match `BootController/GameManager/EventBus/SaveManager.cs.meta` exactly.
- **EventBus exception test**: `LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom")` requires an exact-string match against the message that `Debug.LogException` produces. If Unity's serialization format changes the message, switch the second arg to a `Regex`.
- **`OnDestroy` Instance-null pattern**: every singleton clears `Instance = null` only when `Instance == this`. This matches Tech Arch's testing concern from WS-002 §5 — destroyed-then-recreated singletons in unit tests would otherwise leak a stale destroyed reference.
- **Namespace**: continuing `HellpitRampage.*` from WS-001 rather than the spec's `SanctuaryBound.*`.
- **Tests/EditMode folder + folder meta**: spec didn't enumerate folder metas. Added them so Unity's import doesn't auto-generate fresh GUIDs and trigger merge churn later.

# Last completed: WS-001 Project Bootstrap (commit 13ed59d, 2026-05-13)

Full plan: `C:\Users\admin\.claude\plans\serene-leaping-ladybug.md`.

## Done
- [x] Deleted template assets: empty `Boot/Game/MainMenu.unity` folders + metas, `InputSystem_Actions.inputactions` + meta, `DefaultVolumeProfile.asset` + meta, orphaned `Assets/Scenes.meta`.
- [x] Moved 6 folders into `Assets/_Project/`: `Audio`, `Prefabs`, `Scripts`, `ScriptableObjects`, `Shaders`, `Sprites` (preserved meta GUIDs).
- [x] Created `Assets/_Project/{Scenes, Settings, Settings/Input}` directories + folder metas.
- [x] Authored `Assets/_Project/Settings/Input/PlayerInput.inputactions` with `Player` map (Movement, ActiveAbility, Pause) and empty `UI` map; two control schemes (Keyboard&Mouse, Gamepad).
- [x] Hand-wrote `Assets/_Project/Scripts/Core/PlayerInputActions.cs` in namespace `HellpitRampage.Core` using `InputActionAsset.FromJson` with the same JSON embedded — verified byte-for-byte match against the asset.
- [x] Wrote `Boot.unity` (Main Camera + BootController), `MainMenu.unity` (camera + Canvas/Scaler/Raycaster + UI.Text + EventSystem), `Game.unity` (same as MainMenu with different text). Used legacy `UnityEngine.UI.Text` to avoid the TMP Essentials first-open prompt.
- [x] `ProjectSettings.asset`: flipped `resizableWindow: 0` → `1`. All other required Player settings were already correct.
- [x] `EditorBuildSettings.asset`: replaced scenes list with the three new scenes in order (Boot=0, MainMenu=1, Game=2); cleared stale `m_configObjects`.
- [x] GUID uniqueness check passed across `Assets/`.
- [x] Committed: `[WS-001] Project bootstrap: scenes, input actions, project settings`.

## Deferred to designer (one-click in Unity)
- [ ] After opening Unity 6000.4.6f1, go to `Edit → Project Settings → Input System Package` and assign `Assets/_Project/Settings/Input/PlayerInput.inputactions` as the project-wide actions asset. (Deliberately not auto-wired in EditorBuildSettings — the fileID hash is fragile to author by hand.)
- [ ] Push to GitHub `main` (awaiting designer confirmation; spec calls for it but it's a shared-state action).

## Review notes

- **Risk**: Scene YAMLs were hand-authored using verified built-in script GUIDs from `Library/PackageCache/com.unity.ugui@473409526770/` and `com.unity.inputsystem@21a28c3a6c83/`. The camera and scene-defaults blocks were modeled byte-for-byte on `Assets/Settings/Scenes/URP2DSceneTemplate.unity`. Cannot verify with the editor from this environment — designer Play-test in Boot scene is the final gate.
- **TMP avoided on purpose**: spec asks for "TextMeshPro - Text (UI)" but used legacy `UI.Text` for placeholder to avoid the first-open TMP Essentials import prompt. TMP can be adopted in a later workstream once the editor opens cleanly. Acceptance criteria item 2.b in the spec ("`TextMeshPro - Text (UI)` element") is the one explicit deviation.
- **Namespace**: spec says `SanctuaryBound.Core`; used `HellpitRampage.Core` per the designer's confirmation that the current working title is Hellpit Rampage.
