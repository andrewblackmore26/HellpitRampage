# Hellpit Rampage — Full Game Audit

**Date:** 2026-05-16
**Audited by:** Claude (Opus 4.7) — 6 parallel read-only subagents + main-thread synthesis & spot-verification
**Branch:** `main` — commit `566e463` plus all uncommitted working-tree changes per `git status`
**Method:** Static analysis only. Unity cannot run from this environment (no batch-mode on PATH, project lock). The ~140 EditMode tests were **not** executed; no playtest was performed. Findings that genuinely need runtime confirmation are flagged.
**Scope:** Whole game — Combat, Inventory/Synergy, UI (drag/tooltip/shop/menus/HUD), Core lifecycle, Save/Resume, Settings, and scene/asset wiring.

---

## Executive Summary

The game's **shipped, committed systems are broadly sound** — combat, inventory, synergy, and the WS-012.x UI all hold up under static review. The serious problems cluster in **two areas**:

1. **The Game scene's text UI is silently broken.** The TMP migration was run on scripts and prefabs but never on the scenes. The C# field *types* were changed from `UnityEngine.UI.Text` to `TextMeshProUGUI`, but `Game.unity` still wires those fields to legacy `Text` components. Unity cannot coerce the type, so **~17 label references resolve to `null` at runtime** — the round timer, gold counter, shop item names/prices, reroll cost, sell-modal text, and run-end header all show nothing. The prior WS-012.x audit logged this as a *cosmetic font* issue; it is actually a **functional UI failure**. This is the highest-impact finding affecting the committed game.

2. **The WS-013 Save/Resume system does not work.** It is uncommitted and, per its own task notes, never playtested. Resume is **non-functional**: `RunRestoreController` publishes `ShopPhaseStartedEvent`, but the shop UI listens only for `RoundEndedEvent` — so a resumed run lands the player in an **empty scene with no shop and no way to advance**. Ground items are also silently dropped on resume, and an exception mid-restore leaves an unrecoverable half-restored state.

Beyond those: a likely **run-breaking gameplay gap** (the player's HP never heals or resets between 30 rounds, with no heal path anywhere in the code), two **known-but-unresolved blockers** carried over from the prior audit (no `AudioMixer` asset; pause input unwired), and a long tail of medium/low correctness and robustness issues.

### Severity counts

| Severity | Count | Meaning |
|---|---|---|
| 🔴 Critical | 2 | Breaks a core experience; guaranteed to occur |
| 🟠 High | 7 | A feature is broken or a run can become unwinnable |
| 🟡 Medium | 22 | Wrong behavior in a real scenario, or a notable edge case |
| 🔵 Low | 28 | Minor, rare, latent, or hygiene |
| ⚪ Cosmetic | 3 | No behavioral impact |
| **Total** | **62** | |

Plus **6 items reviewed and confirmed *not* bugs** (Appendix A) and **a playtest/Unity-Editor checklist** (§7).

### How to read this

Each finding has an ID (`C-`/`H-`/`M-`/`L-`/`COS-`), a `file:line` anchor, the observable symptom, the precise root cause, a concrete minimal fix, and a confidence rating (**Confirmed** = provable from code, **Likely** = strong but ordering/config-dependent, **Needs-playtest** = requires runtime). No code was changed — this is a report. Fixing is a separate decision.

---

## §1 — 🔴 Critical

### C-1 — Game.unity text labels resolve to `null`; most in-game UI text is blank

- **File:** `Assets/_Project/Scenes/Game.unity` — 17 affected label wirings (round timer ~`:2043`, run-end header ~`:1909`, shop header ~`:2532`, gold display ~`:3513`, reroll label ~`:5035`, sell modal ~`:5219`, mode-toggle label ~`:5718`, and 5 shop slots' name/price labels). Cross-ref e.g. [RoundTimerUI.cs:10](Assets/_Project/Scripts/UI/RoundTimerUI.cs#L10) (`_label : TextMeshProUGUI`).
- **Symptom:** Round timer, gold counter, shop item names, shop item prices, reroll cost, sell-modal "Drag here to sell…" text, drag-mode label, and run-end header all display **nothing**. No crash — every consumer null-guards (`if (_label == null) return;`) — so the failure is completely silent.
- **Root cause:** The TMP migration (`Tools/WS-012.4/Migrate Text → TMP`) was run on scripts and prefabs but **never on the scenes**. `Game.unity` still contains **19 legacy `UnityEngine.UI.Text` components** (verified: 19 occurrences of script GUID `5f7201a12d95ffc409449d95f23cf332`). The C# fields were changed to `TextMeshProUGUI`. A serialized reference of type `TextMeshProUGUI` pointing at a `Text` component fails Unity's type check on deserialization and becomes `null`. The prior WS-012.7 audit treated this as a cosmetic Arial-vs-TMP font issue and deferred it; the field-type change makes it a **null-reference wiring failure**.
- **Fix:** In Unity, close+reopen the `Game.unity` tab (per memory `project_unity_open_scene_reload`), run `Tools → WS-012.4 → Migrate Text → TMP (Active Scene)` — the tool swaps each component **and** re-points the `SerializedProperty` reference — then save. Re-verify all 17 fields are non-`{fileID: 0}` and TMP-typed. Do the same for `MainMenu.unity` (see L-24).
- **Confidence:** Confirmed (field types and the 19 legacy components verified directly; per-label fileID mapping per the SA6 scene read).

### C-2 — Resume drops the player into an empty scene with no shop and no way to advance

- **File:** [RunRestoreController.cs:42-48](Assets/_Project/Scripts/Save/RunRestoreController.cs#L42-L48), [ShopOverlayController.cs:14-26](Assets/_Project/Scripts/UI/ShopOverlayController.cs#L14-L26), [ShopController.cs:64-67](Assets/_Project/Scripts/UI/ShopController.cs#L64-L67)
- **Symptom:** "Resume Run" → Game scene loads → gold/HP/inventory restore correctly, but **the shop overlay panel never appears and the 5 shop slots are never populated**. No "Start Next Round" affordance. The run is soft-locked. The same root cause means `CombatRoundController` is never put into a defined "in shop" state on resume.
- **Root cause:** `RunRestoreController.RestoreNextFrame()` publishes only `RunResumedEvent` and `ShopPhaseStartedEvent`. But `ShopOverlayController` (panel `SetActive(true)`) and `ShopController` (`PopulateAllSlots()`) both subscribe to **`RoundEndedEvent`**, not `ShopPhaseStartedEvent` — verified at [ShopOverlayController.cs:18](Assets/_Project/Scripts/UI/ShopOverlayController.cs#L18). Only `GroundManager` and `DragModeService` listen for `ShopPhaseStartedEvent`. The restore flow never publishes `RoundEndedEvent`, so the shop UI is never told to open. `ShopOverlayController._panel` is `SetActive(false)` in `OnEnable` and stays that way.
- **Fix:** In `RunRestoreController.RestoreNextFrame()`, after restore, also publish `new RoundEndedEvent { RoundNumber = data.CurrentRound }`. This single line drives `ShopOverlayController`, `ShopController`, **and** `CombatRoundController.HandleRoundEnded` (stops timer, clears combat) — fixing the shop UI and the combat-state coherence together. Verify it does not re-award round-end gold (it does not — only `RunManager.EndCurrentRound` awards gold, and that is not invoked) and that `GoldFieldSweeper`'s sweep is a harmless no-op (no gold exists on resume). Alternatively, make `ShopOverlayController`/`ShopController` also subscribe to `ShopPhaseStartedEvent`.
- **Confidence:** Confirmed (event wiring traced and verified end-to-end). Note: the WS-013 system is uncommitted and has never been playtested — this is the first time the flow has been examined.

---

## §2 — 🟠 High

### H-1 — Player HP never resets or heals between rounds; no heal path exists anywhere

- **File:** [Health.cs](Assets/_Project/Scripts/Combat/Health.cs) (reset only in `OnEnable`/`Initialize`/`RestoreFromSave`), [CombatRoundController.cs:47-61](Assets/_Project/Scripts/Combat/CombatRoundController.cs#L47-L61) (`HandleRoundStarted` never touches player Health)
- **Symptom:** Contact damage taken in round 1 carries into rounds 2…30. There is no round-start heal, no shop heal, and the player GameObject is persistent (never pool-toggled), so `Health.OnEnable` fires only once. Damage compounds monotonically — the run becomes unwinnable well before round 30.
- **Root cause:** No subscriber to `RoundStartedEvent`/`ShopPhaseStartedEvent` heals the player (grep confirms `GroundManager`, `RoundTimerUI`, `ShopController`, `ShopOverlayController` are the only other `RoundStartedEvent` subscribers; none reference `Health`). The `HealingBurst` behavior is a `Debug.Log` stub ([PlayerWeapons.cs](Assets/_Project/Scripts/Combat/PlayerWeapons.cs)). As of this code state, **nothing in the game can heal the player.**
- **Fix:** This is a **design decision** — confirm intent. If rounds should heal: add a player-Health reset/heal in `CombatRoundController.HandleRoundStarted` (or on `ShopPhaseStartedEvent`). If HP is meant to be a run-long resource, that needs an explicit heal item/economy. The *absence of any heal path* is confirmed; whether it is a bug depends on the intended design.
- **Confidence:** Confirmed (no heal path exists); Likely a bug (a 30-round roguelite with contact damage and no healing is not winnable).

### H-2 — No `AudioMixer` asset exists; volume sliders are silent placebos

- **File:** [Boot.unity:583](Assets/_Project/Scenes/Boot.unity#L583) (`SettingsManager._mainMixer: {fileID: 0}`), [SettingsManager.cs:86](Assets/_Project/Scripts/Core/SettingsManager.cs#L86) (and `:95`, `:104`, `:113`)
- **Symptom:** Master/Music/SFX/Voice sliders persist to `settings.json` but produce no audible effect. `ApplyVolume` early-returns when `_mainMixer == null`.
- **Root cause:** No `*.mixer` asset exists anywhere (`Assets/_Project/Audio/` has only `Music/` and `SFX/` folders). `_mainMixer` is `{fileID: 0}`. Carried over unresolved from the prior audit / WS-012.7 (deferred to the user).
- **Fix:** Create `Assets/_Project/Audio/MainMixer.mixer` via `Assets → Create → Audio Mixer`; groups `Master → Music/SFX/Voice`; expose params `Volume_Master`/`Volume_Music`/`Volume_SFX`/`Volume_Voice` (exact case); assign to `SettingsManager._mainMixer` in `Boot.unity`. Route at least one `AudioSource` to a mixer group, or the chain still cannot be verified (memory `L-016`).
- **Confidence:** Confirmed.

### H-3 — Pause menu is unreachable in-game; `_inputActions` is unwired

- **File:** [Game.unity:5848](Assets/_Project/Scenes/Game.unity#L5848) (`PauseMenuController._inputActions: {fileID: 0}`), [PauseMenuController.cs:78-83](Assets/_Project/Scripts/UI/PauseMenuController.cs#L78-L83)
- **Symptom:** Escape and gamepad Start do nothing. The pause menu — and the in-game route to the Settings menu and Quit-to-Menu — is unreachable for the entire run.
- **Root cause:** `_inputActions` is null in the scene, so `EnsurePauseAction()` early-returns, `_pauseAction` is never resolved, and `OnEnable` never binds `performed`. The failure is **completely silent** — there is no `Debug.LogWarning` for the null field. Carried over unresolved from the prior audit / WS-012.7.
- **Fix:** In Unity, drag the `PlayerInput.inputactions` `InputActionAsset` onto `PauseMenuController._inputActions` at `Game.unity:5848`. Code-side defense: add an `else Debug.LogWarning(...)` in `EnsurePauseAction` so the misconfiguration surfaces.
- **Confidence:** Confirmed.

### H-4 — A mid-restore exception leaves an unrecoverable half-restored run

- **File:** [RunRestoreController.cs:29-41](Assets/_Project/Scripts/Save/RunRestoreController.cs#L29-L41)
- **Symptom:** If `ApplySave` throws partway (DataRegistry NRE, malformed entry, content drift), the player is left in the Game scene with a partially-rebuilt inventory, **no shop signal**, and `PendingResume` already cleared — so even a scene reload would silently start a *fresh* run instead of retrying. The run and the save are effectively lost.
- **Root cause:** `ApplySave` is wrapped in `try { … } finally { EndRestore(); }` with **no `catch`**. `GameManager.PendingResume` is nulled at line 29 *before* the try. On a throw, the post-try `RunResumedEvent`/`ShopPhaseStartedEvent` publishes never run, and the exception escapes the coroutine. There is no graceful-degradation path.
- **Fix:** Add a `catch` that logs the exception and chooses a deliberate fallback — either abort cleanly to MainMenu, or proceed with whatever restored plus the shop signal so the run is at least continuable. Do not leave the post-try publishes unreachable on failure.
- **Confidence:** Confirmed (code structure); trigger requires a malformed save or content drift.

### H-5 — Ground items are silently lost on resume; `GroundManager.Current` is null during `ApplySave`

- **File:** [RunRestoreController.cs:99-128](Assets/_Project/Scripts/Save/RunRestoreController.cs#L99-L128); GroundArea in `Game.unity` ships `m_IsActive: 0`
- **Symptom:** A resumed run loses every item that was on the ground when saved; and any saved item whose shape no longer fits its origin is *also* lost instead of spilling to the ground.
- **Root cause:** `ApplySave` reads `GroundManager.Current` at lines 99-107 (spillover) and 113-128 (ground-item restore). `GroundManager.Current` is set in `Awake`/`OnEnable`, which only run when the GroundArea GameObject is **active** — but GroundArea ships inactive and is only activated by `ShopPhaseStartedEvent`, which `RunRestoreController` publishes **after** `ApplySave` finishes (line 48). So throughout `ApplySave`, `GroundManager.Current` is `null` and both branches silently take the "no GroundManager available, item lost" path (line 106).
- **Fix:** Activate the GroundArea GameObject (or publish `ShopPhaseStartedEvent`) *before* `ApplySave` runs the ground/spillover sections — or restructure so ground restore happens after the shop-phase signal. Note this interacts with C-2's fix; sequence the resume flow as: activate shop UI/GroundArea → restore → confirm.
- **Confidence:** Likely (depends on GroundArea's initial active state — confirmed inactive per the WS-012.5 wiring; verify in-Editor).

### H-6 — All ground items share `InstanceID = 0`; `ItemInstance` equality collapses them into one identity

- **File:** [GroundManager.cs:159](Assets/_Project/Scripts/UI/GroundManager.cs#L159) (`new ItemInstance(0, …)`), [ItemInstance.cs:67-68](Assets/_Project/Scripts/Inventory/ItemInstance.cs#L67-L68) (`Equals`/`GetHashCode` keyed on `InstanceID`)
- **Symptom:** With ≥2 items on the ground, any `HashSet<ItemInstance>` / `Dictionary<ItemInstance,…>` / `.Equals()` comparison treats them all as the same item. Star-active lookups (`SynergyService.IsStarActive(focusItem.InstanceID, …)`), future save dedup, and any hash-keyed logic target or drop the wrong ground item.
- **Root cause:** `GroundManager.AddItem` always constructs `new ItemInstance(0, data, …)`. `ItemInstance.GetHashCode() => InstanceID` and `Equals(i) => i.InstanceID == InstanceID` make every ground instance hash- and value-equal. `GroundManager.FindByInstance`/`ContainsItem` happen to use reference `==` (the `==` operator is not overloaded) so those paths survive — but the latent collision is real and worsens with WS-013 save and synergy work.
- **Fix:** Give ground items unique IDs — e.g. a `static int _nextGroundId = -1000;` decremented per `AddItem` (negative, distinct from `StarIndicatorOverlay`'s `int.MinValue` shop-preview sentinel). Better: centralize sentinel-ID constants and document that grid IDs are always `>= 1` (see L-26).
- **Confidence:** Confirmed (the equality collapse); player-visible impact is Likely/latent.

### H-7 — `HostBag` is a fragile second source of truth that can desync from spatial state

- **File:** [InventoryGrid.cs:159-165](Assets/_Project/Scripts/Inventory/InventoryGrid.cs#L159-L165) (`MoveItem` derives `HostBag` from `effective[0]`), [InventoryGrid.cs:72-81](Assets/_Project/Scripts/Inventory/InventoryGrid.cs#L72-L81) (`public MoveBag` moves the bag but not its items)
- **Symptom:** No live bug today, but two latent paths to corruption: (a) `MoveItem`/`PlaceItem` set `HostBag` from the *first* rotated cell rather than the host that `CanPlaceItem` validated — correct only because `CanPlaceItem` always runs first; (b) `Grid.MoveBag` is `public` and moves only `bag.Origin`, leaving contained items' `Origin` stale — `InventoryService.MoveBagAndItems` compensates externally, but any direct `Grid.MoveBag` caller desyncs items from their bag.
- **Root cause:** `HostBag` duplicates information that is otherwise derivable from cell overlap; the grid primitives don't enforce the invariant.
- **Fix:** Have `CanPlaceItem` return the resolved host via an `out` param and reuse it in `PlaceItem`/`MoveItem` (validated host == stored host by construction). Make `Grid.MoveBag` private, or fold the contained-item shift into it so a bag and its items can never move independently.
- **Confidence:** Likely (latent; no current caller triggers it, but the public surface invites the bug).

---

## §3 — 🟡 Medium

### M-1 — `EnemySpawner` spawns enemies into a *resumed* shop phase
[EnemySpawner.cs:26,37](Assets/_Project/Scripts/Combat/EnemySpawner.cs#L26) — `_spawnOnStart = true` calls `StartSpawning()` in `Start()` unconditionally. On a normal new run, `CombatRoundController` also starts it (harmless). On **resume**, `GameSceneBootstrap` correctly skips `StartNewRun()`, so no `RoundStartedEvent` fires — but `EnemySpawner.Start()` still starts spawning. Enemies pour into the restored shop. **Fix:** set `_spawnOnStart = false` and rely solely on `CombatRoundController.HandleRoundStarted`, or gate it on `GameManager.Instance.PendingResume == null`. *Confirmed.*

### M-2 — Enemies cleared by the round timer drop no gold
[CombatRoundController.cs:80-90](Assets/_Project/Scripts/Combat/CombatRoundController.cs#L80-L90) — `ClearActiveEnemies` calls `PoolManager.Release` directly, bypassing `Health.HandleDeath`, so no `EnemyDiedEvent` and no `GoldDropController` drop. A player who lets the timer expire instead of killing everything **forfeits all that gold**. **Fix:** if surviving enemies should still reward gold, route the clear through a kill path (`Health.Kill()` / lethal `TakeDamage`) so `EnemyDiedEvent` fires before the field is swept. *Confirmed.*

### M-3 — Round-end gold sweep races same-frame enemy deaths
[GoldFieldSweeper.cs:26-37](Assets/_Project/Scripts/Combat/GoldFieldSweeper.cs#L26-L37) — `GoldFieldSweeper` handles `RoundEndedEvent` and enumerates existing `GoldPickup`s. Gold dropped by an enemy dying on the exact final frame may or may not be swept depending on EventBus handler order (determined by non-deterministic scene `OnEnable` order). **Fix:** defer the sweep one frame (`yield return null`) so all same-frame drops have spawned. *Likely.*

### M-4 — `PlayerWeapons` per-instance caches are not flushed on resume; `InstanceID` reuse inherits stale state
[InventoryGrid.cs:224-227](Assets/_Project/Scripts/Inventory/InventoryGrid.cs#L224-L227) — `Grid.Clear()` resets `_nextInstanceID = 1`. `InventoryService.ClearAll()` (used by WS-013 restore) deliberately suppresses per-item `ItemRemovedEvent`, but `PlayerWeapons` evicts its `_cooldownByInstance`/`_attackCountByInstance` dictionaries *only* on `ItemRemovedEvent`. After a resume, restored items reuse IDs `1,2,3…` and inherit the previous run's cooldown/attack-count state — a restored weapon can start mid-cooldown or fire a behavior on the wrong attack number. **Fix:** flush `PlayerWeapons` caches on a clear/restore signal, or stop resetting `_nextInstanceID` (make it process-monotonic). *Likely (manifests on resume into the same session).*

### M-5 — Weapon attack-counter resets when an item is picked up and re-placed
[PlayerWeapons.cs:66-80](Assets/_Project/Scripts/Combat/PlayerWeapons.cs#L66-L80) — `HandleItemRemoved` does `_attackCountByInstance.Remove(id)`; `HandleItemPlaced` re-adds it as `0`. Any in-grid drag fires remove+place and wipes the accumulated count, so a `TriggerCount = 3` behavior needs 3 fresh shots again. **Fix:** if counters should persist across re-placement (likely for a roguelite), only initialize to 0 for a genuinely new `InstanceID`, and don't `Remove` on `ItemRemovedEvent`. Confirm intent with the designer. *Likely.*

### M-6 — `ItemPool.DrawWeighted` silently returns an empty/short shop on a degenerate pool
[ItemPool.cs:22-38](Assets/_Project/Scripts/Inventory/ItemPool.cs#L22-L38) — on an empty pool or non-positive total weight, the method returns fewer items than requested (possibly zero) with no diagnostic — a misconfigured `ItemPool` asset produces a silently empty shop. **Fix:** `Debug.LogWarning` on a degenerate pool. *Likely.*

### M-7 — `RemoveBagOnly` orphans items whose `HostBag` still points at the destroyed bag
[InventoryGrid.cs:53-57](Assets/_Project/Scripts/Inventory/InventoryGrid.cs#L53-L57) — removes the bag from `_bags` but trusts an undocumented invariant that the caller already drained items. If the bag-sale flow ever leaves an item behind (locked item, aborted spill), that item keeps a dangling `HostBag` and its cells become permanently unplaceable. **Fix:** guard — if any `_items` entry still has `HostBag == bag`, log an error / reject. *Likely.*

### M-8 — Shop-to-ground deposit charges gold but loses the item if `AddItem` returns null
[ShopSlotDragHandler.cs:152-191](Assets/_Project/Scripts/UI/ShopSlotDragHandler.cs#L152-L191) — the grid-placement path refunds gold on failure (`AddGold(price)`), but the ground-deposit path calls `GroundManager.Current.AddItem(...)`, ignores its return value, and proceeds to `SpendGold` + `TakeOfferFromSlot`. `AddItem` returns `null` if ground wiring is broken — the player is charged, the slot consumed, no item appears. **Fix:** capture the return; if `null`, refund and return before consuming the slot. *Likely.*

### M-9 — Bag-sell grants gold then skips the spill if `GroundAreaRT` is null — items lost
[SellModal.cs:138-158](Assets/_Project/Scripts/UI/SellModal.cs#L138-L158) — gold is granted first, then the spill loop is guarded by `if (groundRT != null)`. If `GroundManager.Current` is non-null but its `GroundAreaRT` is null, the whole spill is skipped yet the bag is still removed via `RemoveBagWithoutCascade`. Player keeps the gold; bag contents vanish. **Fix:** move the `groundRT` null check up beside the `GroundManager.Current` check and abort the whole sale (before `AddGold`) if it's null. *Likely.*

### M-10 — Grid→ground deposit triggers a redundant full grid refresh and contradicts `RemoveItemSilent`
[DragHandler.cs:190-198](Assets/_Project/Scripts/UI/DragHandler.cs#L190-L198) — `TryDepositOnGround` calls `RemoveItemSilent` (whose entire purpose is to avoid a grid refresh) and then *manually publishes* `ItemRemovedEvent`, which `InventoryGridView` subscribes to and answers with a full `RefreshAll()`. The "silent" removal is not silent; the documented rationale is defeated. **Fix:** either use the normal `InventoryService.RemoveItem` (one refresh, no manual publish), or keep `RemoveItemSilent` and have the dragged GameObject destroy only itself. *Confirmed.*

### M-11 — `GroundManager` Z-sort overrides the dragged item's sibling order every physics frame
[GroundManager.cs:358-363](Assets/_Project/Scripts/UI/GroundManager.cs#L358-L363) — `FixedUpdate` re-sorts all ground items by Y and calls `SetSiblingIndex` on each, overriding `GroundDragHandler.OnBeginDrag`'s `SetAsLastSibling()`. The held item flickers behind other ground items mid-drag. The collision pass correctly skips held items; the sibling-reorder loop does not. **Fix:** skip `IsHeld` items in the sibling loop (or re-apply `SetAsLastSibling` to the held one after). *Likely.*

### M-12 — Ground items snap to the cursor centre when grabbed (no grab offset)
[GroundDragHandler.cs:55-63](Assets/_Project/Scripts/UI/GroundDragHandler.cs#L55-L63) — `OnDrag` sets `_rt.anchoredPosition = local` (raw pointer position) with no grab offset captured, unlike grid `DragHandler` which preserves `_grabOffset`. Grabbing a ground item by its edge makes it jump so its centre is under the cursor. **Fix:** capture `_grabOffset` in `OnBeginDrag`, subtract it in `OnDrag`. *Confirmed.*

### M-13 — `RoundTimerUI` shows "Round 0" and is never seeded on resume
[RoundTimerUI.cs:13,27-39](Assets/_Project/Scripts/UI/RoundTimerUI.cs#L13-L39) — `_currentRound` defaults to 0 and is only set by `RoundStartedEvent`. Before the first round, and through the *entire shop phase of a resumed run* (which gets `ShopPhaseStartedEvent`, not `RoundStartedEvent`), the HUD reads "Round 0". **Fix:** seed `_currentRound` from `RunManager.Instance.CurrentRound` in `OnEnable` (as `GoldDisplayController` already does for gold). *Confirmed.* (Note: masked by C-1 until the TMP wiring is fixed.)

### M-14 — `ShopController` marks every slot literally "SOLD" at round start
[ShopController.cs:64-67](Assets/_Project/Scripts/UI/ShopController.cs#L64-L67) — `HandleRoundStarted` calls `MarkSold()` on all 5 slots to clear them; `ShopSlot` renders `IsSold` as the text "SOLD". If the shop panel is ever visible at round start, all slots misrepresent "not yet stocked" as "you sold this". **Fix:** add a `ShopSlot.Clear()` (empty state) and call it from `HandleRoundStarted`; reserve `MarkSold()` for real purchases. *Likely (depends on whether the panel is visible during combat).*

### M-15 — Resolution-confirm coroutine invokes callbacks on destroyed menu UI
[SettingsManager.cs:201-271](Assets/_Project/Scripts/Core/SettingsManager.cs#L201-L271) — `SetResolutionWithConfirm` runs a 10-second revert countdown on the persistent `SettingsManager` singleton. If the player closes the Settings menu (or changes scene) before confirming, the coroutine keeps invoking `onTick`/`onRevert` closures that capture the now-destroyed `SettingsMenuController` UI → `MissingReferenceException`. **Fix:** add a `CancelPendingResolutionConfirm()` the menu calls in `OnDisable`, or null-check via an "is menu alive" token. Keep the resolution revert itself; only the UI callbacks need guarding. *Likely.*

### M-16 — `DataRegistry.GetHero` logs a warning on every resume when `HeroId` is null
[DataRegistry.cs:86](Assets/_Project/Scripts/Core/DataRegistry.cs#L86), [RunRestoreController.cs:59](Assets/_Project/Scripts/Save/RunRestoreController.cs#L59) — if `RunManager._defaultHero` is unwired, `CurrentHero` is null, `HeroId` saves as `null`, and `GetHero(null)` unconditionally logs `[DataRegistry] Unknown HeroData id ''`. Misleading console noise every resume. **Fix:** in `RunRestoreController`, only call `GetHero` for a non-empty id; make `GetHero` not warn on a null/empty id (only on a non-empty *unknown* id). *Confirmed (given `_defaultHero` unwired).*

### M-17 — A bag that fails to place on restore cascades — its items spill to ground or are lost
[RunRestoreController.cs:80-109](Assets/_Project/Scripts/Save/RunRestoreController.cs#L80-L109) — items are saved with absolute `Origin` only; `HostBag` is reconstructed from spatial overlap. If a bag's `PlaceBag` fails (content drift, overlap), every item that depended on it fails `PlaceItem` and spills to ground (or is lost — see H-5). A partial restore can scatter the player's whole loadout. **Fix:** acceptable as graceful degradation, but log a bag-placement failure at error level and document the cascade; consider whether items should retain bag-relative positions. *Confirmed (behavior); severity depends on drift frequency.*

### M-18 — `RunSaveFailedEvent` has no subscriber — save failures are silent player-facing data loss
[SaveManager.cs:269-274](Assets/_Project/Scripts/Core/SaveManager.cs#L269-L274), [SaveEvents.cs](Assets/_Project/Scripts/Core/Events/SaveEvents.cs) — if `SaveRun` fails to write (disk full, permission, AV lock), `RunSaveFailedEvent` is published with a reason, but **nothing subscribes**. Auto-save is fire-and-forget with only a `Debug.LogError`. The player gets no indication; they quit and find no Resume option, or resume stale state. **Fix:** add a HUD toast / visible indicator subscriber for `RunSaveFailedEvent`. *Confirmed.*

### M-19 — `DragModeService` can wedge — the mode toggle stops working
[DragModeService.cs:56-66](Assets/_Project/Scripts/Core/DragModeService.cs#L56-L66) — `_dragInProgress` is set on `*DragBeganEvent`, cleared only on the matching `*DragEndedEvent`. If a dragged visual is destroyed mid-drag (e.g. `InventoryGridView.RefreshAll` runs on a synergy recompute and destroys the in-flight item), `OnEndDrag` never fires, `*DragEndedEvent` is never published, and `_dragInProgress` stays `true` — `Toggle()` then permanently refuses (Tab key and Mode button silently dead). **Fix:** also reset `_dragInProgress = false` in `HandleShopStarted` / on any phase change. *Likely.*

### M-20 — `EnemySpawner` has no per-round difficulty scaling
[EnemySpawner.cs:26-41](Assets/_Project/Scripts/Combat/EnemySpawner.cs#L26-L41) — `_spawnRate`, `_enemyData`, `_spawnDistance` are fixed for all 30 rounds; round 30 spawns identically to round 1. The round loop never communicates round number to the spawner. **Fix:** if scaling is intended (it usually is for this genre), feed `RoundStartedEvent.RoundNumber` into the spawner. If flat difficulty is a deliberate placeholder, no action — flagged to confirm intent. *Needs-design-confirmation.*

### M-21 — `TooltipController` event subscription is unbalanced if `EventBus` is destroyed first
[TooltipController.cs:381-413](Assets/_Project/Scripts/UI/TooltipController.cs#L381-L413) — `SubscribeEvents`/`UnsubscribeEvents` pair in `OnEnable`/`OnDisable`. If `EventBus.Instance` is already null at `OnDisable` (scene-unload destruction order), `UnsubscribeEvents` skips the actual `Unsubscribe` calls but still flips `_eventsSubscribed`, leaving stale delegates registered against a destroyed handler. Narrow leak (bounded by `EventBus.OnDestroy` clearing handlers). This is a project-wide pattern, not unique to this file. **Fix:** acceptable to leave; if hardening, make `EventBus.Subscribe` idempotent (remove-then-add). *Likely (narrow).*

### M-22 — Resume relies on undefined `Start()` ordering between two MonoBehaviours
[HeroStartingLoadout.cs:18-33](Assets/_Project/Scripts/Inventory/HeroStartingLoadout.cs#L18-L33), [RunRestoreController.cs:17-22](Assets/_Project/Scripts/Save/RunRestoreController.cs#L17-L22) — both run on resume. It works today only because `RunRestoreController` defers its body one frame (`yield return null`) so `HeroStartingLoadout.Start()` always sees `PendingResume != null` and bails. Any future inventory-seeding code in `Awake`/`Start` that forgets the guard silently corrupts the restore. **Fix:** document the dependency; prefer a single guarded entry point. *Likely (works today by timing; fragile).*

---

## §4 — 🔵 Low

| ID | Title | File | Note |
|---|---|---|---|
| L-1 | `RunManager.RestoreFromSave` clamps `CurrentRound` silently → possible soft-lock at final round | [RunManager.cs:77-87](Assets/_Project/Scripts/Core/RunManager.cs#L77-L87) | Log a warning when out of range |
| L-2 | `Enemy` keeps stale `_data`/`_playerTransform` across pool reuse | [Enemy.cs:30-40](Assets/_Project/Scripts/Combat/Enemy.cs#L30-L40) | Benign given spawner call order; null them in `OnEnable` for safety |
| L-3 | `PlayerWeapons` fallback keys cooldowns by `ItemData` — duplicate items collapse | [PlayerWeapons.cs:122-136](Assets/_Project/Scripts/Combat/PlayerWeapons.cs#L122-L136) | Test/standalone-only path; low impact |
| L-4 | `SynergyResolver.ActiveStars` positional keying — fragile for hypothetical multi-star items | [SynergyResolver.cs:56](Assets/_Project/Scripts/Inventory/SynergyResolver.cs#L56) | Key by `(id, starIndex)` if multi-star items appear |
| L-5 | `ConditionalEffect` with `ActivatorTag == None` is silently skipped | [SynergyResolver.cs:51](Assets/_Project/Scripts/Inventory/SynergyResolver.cs#L51) | Content-authoring trap; add an `OnValidate` warning |
| L-6 | `HeroStartingLoadout` calls `Grid.Clear()` directly, bypassing `ClearAll()` event publishing | [HeroStartingLoadout.cs:30](Assets/_Project/Scripts/Inventory/HeroStartingLoadout.cs#L30) | Harmless on an empty grid; use `ClearAll()` |
| L-7 | `RemoveItemSilent` / `RemoveItemDirect` are near-duplicate APIs | [InventoryService.cs:211](Assets/_Project/Scripts/Inventory/InventoryService.cs#L211) | Consolidate or rename for clarity |
| L-8 | Lock icon stays full-opacity while item cells dim in Bags mode | [InventoryGridView.cs:299-318](Assets/_Project/Scripts/UI/InventoryGridView.cs#L299-L318) | Multiply lock-icon alpha by `_itemAlpha` |
| L-9 | `GroundManager.ApplyVisualMode` doesn't dim the ground item's lock icon | [GroundManager.cs:108-119](Assets/_Project/Scripts/UI/GroundManager.cs#L108-L119) | Same fix as L-8 for ground items |
| L-10 | Ground-item fling velocity uses screen-space delta — wrong under canvas scaling | [GroundDragHandler.cs:141-147](Assets/_Project/Scripts/UI/GroundDragHandler.cs#L141-L147) | Divide by `canvas.scaleFactor` or use local-space delta |
| L-11 | `RebuildDraggedVisualForRotation` computes bbox from `max` only (assumes min `(0,0)`) | [DragHandler.cs:126-133](Assets/_Project/Scripts/UI/DragHandler.cs#L126-L133) | OK if `ShapeMath.Rotate` normalizes; compute full min/max defensively |
| L-12 | `InventoryGridView` builds the cell grid in `OnEnable` → hot-reload duplicates cells | [InventoryGridView.cs:43-45](Assets/_Project/Scripts/UI/InventoryGridView.cs#L43-L45) | L-014 class; clear `Cell_*` children before rebuild or build in `Awake` |
| L-13 | `ModeToggleButton` adds a button listener with no `RemoveListener` | [ModeToggleButton.cs:19-22](Assets/_Project/Scripts/UI/ModeToggleButton.cs#L19-L22) | Negligible for a scene-lifetime object; add `OnDestroy` cleanup |
| L-14 | `RecipesComingSoonModal` uses `TextOverflowModes.Overflow` against L-019 | [RecipesComingSoonModal.cs:127](Assets/_Project/Scripts/UI/RecipesComingSoonModal.cs#L127) | Switch to `Ellipsis`/`Truncate` |
| L-15 | `SettingsMenuController.OnApplyClicked` orphans the confirm overlay if `SettingsManager.Instance` is null | [SettingsMenuController.cs:303-329](Assets/_Project/Scripts/UI/SettingsMenuController.cs#L303-L329) | Guard `if (SettingsManager.Instance == null) return;` before showing the overlay |
| L-16 | `SaveManager.WriteAtomic` does delete-then-move — not crash-atomic; leftover `.tmp` never recovered | [SaveManager.cs:400-406](Assets/_Project/Scripts/Core/SaveManager.cs#L400-L406) | Use `File.Move(temp, path, overwrite: true)` (atomic rename) |
| L-17 | `EventBus` never prunes empty handler buckets | [EventBus.cs:51-60](Assets/_Project/Scripts/Core/EventBus.cs#L51-L60) | Negligible memory; remove key when list empties |
| L-18 | `SaveManager.MigrateIfNeeded` warns on schema mismatch but uses the unmigrated data | [SaveManager.cs:198-205](Assets/_Project/Scripts/Core/SaveManager.cs#L198-L205) | Harmless today (empty profile classes); align with the stricter run-save path |
| L-19 | `DataIdValidator` doesn't verify every content asset is present in `DataManifest` | [DataIdValidator.cs](Assets/_Project/Scripts/Editor/DataIdValidator.cs) | "Forgot to add the item to the manifest → resume loses it" gap |
| L-20 | `SettingsManager` re-applies volumes on every scene load (redundant) | [SettingsManager.cs:42-44](Assets/_Project/Scripts/Core/SettingsManager.cs#L42-L44) | Mixer retains values across scenes; remove the `sceneLoaded` re-apply |
| L-21 | `GameStateChangedEvent` is published but has zero subscribers (dead event) | [GameManager.cs:81](Assets/_Project/Scripts/Core/GameManager.cs#L81) | Remove, or comment as an intentional future hook |
| L-22 | `GroundItem.prefab` reuses `GoldPickup.prefab`'s anchor namespace base `5700…` | `Assets/_Project/Prefabs/UI/GroundItem.prefab` | L-003 hygiene; re-anchor to a unique base |
| L-23 | Stale serialized field `_itemTintColor` on `InventoryGridView` in `Game.unity` | [Game.unity:3357](Assets/_Project/Scenes/Game.unity#L3357) | Script no longer declares it; harmless, re-serialize to clean up |
| L-24 | `MainMenu.unity` still has 4 legacy `Text` components | `Assets/_Project/Scenes/MainMenu.unity` | Cosmetic font inconsistency; run TMP migration (no null-wiring here) |
| L-25 | Pressing Escape on the run-end screen does nothing, with no affordance | [PauseMenuController.cs:85-95](Assets/_Project/Scripts/UI/PauseMenuController.cs#L85-L95) | Minor UX; optionally route Escape to return-to-menu |
| L-26 | Ad-hoc sentinel IDs (`0` ground, `int.MinValue` shop preview) with no shared registry | [StarIndicatorOverlay.cs:188-201](Assets/_Project/Scripts/UI/StarIndicatorOverlay.cs#L188-L201) | Centralize as named constants; pairs with H-6 |
| L-27 | `EventBus.Publish` snapshot semantics (mid-dispatch sub/unsub) are undocumented | [EventBus.cs:75-87](Assets/_Project/Scripts/Core/EventBus.cs#L75-L87) | Behavior is correct; add a clarifying comment |
| L-28 | `RunResumedEvent` has zero subscribers | [SaveEvents.cs](Assets/_Project/Scripts/Core/Events/SaveEvents.cs) | Intentional future hook per its own comment — informational only |

---

## §5 — ⚪ Cosmetic

- **COS-1** — `GroundItemSnapshot.ItemId` is typed `ItemData` (a Unity object reference), not a string id; the name contradicts its type and L-018. Works only because the snapshot is converted to `GroundItemSaveEntry` before JSON serialization. Rename the field `Data`, or make it a real `string` DTO. [GroundItem.cs:33](Assets/_Project/Scripts/UI/GroundItem.cs#L33)
- **COS-2** — `GroundManager.OnDrop` is an intentionally empty `IDropHandler` (claims the drop event so it doesn't bubble). Documented; no action — listed so a future audit doesn't flag it as dead code. [GroundManager.cs:293-298](Assets/_Project/Scripts/UI/GroundManager.cs#L293-L298)
- **COS-3** — `TooltipController._panelSize` default duplicated as a magic value; used consistently, no behavioral issue.

---

## §6 — Focused assessment: the WS-013 Save / Resume system

This system is **uncommitted and, per `tasks/todo.md`, never playtested or test-run**. It is the single highest-risk area in the working tree. The audit found it has **never functioned end-to-end**:

| Finding | Effect on resume |
|---|---|
| **C-2** | Shop UI never opens — resumed run is soft-locked |
| **H-4** | An exception mid-restore → unrecoverable half-restored state, save effectively lost |
| **H-5** | Ground items + shape-conflict spillover silently lost (`GroundManager.Current` null during `ApplySave`) |
| **M-1** | Enemy spawner runs into the resumed shop phase |
| **M-4** | Restored weapons inherit the previous run's cooldown/attack-count state |
| **M-13** | Round timer reads "Round 0" through the resumed shop phase |
| **M-16** | `[DataRegistry] Unknown HeroData id` warning every resume (if `_defaultHero` unwired) |
| **M-17** | A bag that fails to place scatters its whole contents |
| **M-22** | Correctness depends on undefined `Start()` ordering |

**Recommendation:** do **not** commit WS-013 as-is. C-2 and H-4/H-5 must be fixed and the flow playtested before this system can be considered working. The save *write* path (`SaveManager.SaveRun`/`CaptureRunState`) is in better shape than the *restore* path — the bugs are concentrated in `RunRestoreController` and the event wiring it depends on.

---

## §7 — Requires playtest / Unity Editor to confirm

Static analysis cannot settle these — they need a running editor:

1. **Run the ~140 EditMode tests** (`Window → General → Test Runner`). Confirm pass count and that no test references deleted classes.
2. **TMP migration on `Game.unity` + `MainMenu.unity`** (C-1, L-24) — and re-verify the 17 label fields are non-null afterward.
3. **`GroundItemPhysics` stability** — if the authored GroundArea RectTransform is ever smaller than a 56px item (or zero-sized for one layout frame), the left/right wall clamps both fire and the item jitters and never sleeps. Verify the authored GroundArea size in `Game.unity` and consider a `width >= itemSize` guard. (Subagent SA3 finding, omitted from §1-3 as Needs-playtest.)
4. **`ShapeMath.Rotate` normalization** — several "this is fine" conclusions (and L-11) assume `Rotate` always returns cells anchored at `(0,0)`. Confirm in `ShapeMath.cs`.
5. **Whether the shop panel is visible during combat** — decides if M-14 ("SOLD" labels) is Medium or cosmetic.
6. **EventBus handler ordering** — M-3's gold-sweep race depends on non-deterministic `OnEnable` order; the deferred-sweep fix removes the dependency regardless.
7. **3-round playthrough** for console errors/warnings — the prior audit's open "no errors/warnings during playthrough" item.

---

## Appendix A — Reviewed and confirmed *not* bugs

These were examined (some explicitly requested) and are sound — recorded so they aren't re-flagged:

- **`PlayerWeapons` cooldown / first-shot timing** — `StepCooldown` pauses at 0 and fires on the first frame a target is available; the pre-check and `StepCooldown` agree. No off-by-one.
- **`ShapeMath.RotateDirection`** — the Up→Right→Down→Left direction rotation matches the `(x,y)→(y,-x)` cell rotation; star-edge cell + direction rotate consistently across `SynergyResolver` and `StarIndicatorOverlay`.
- **`MoveBagAndItems` `ItemMovedEvent` payload** — `OldOrigin` captured before mutation, rotation unchanged (correct — moving a bag doesn't rotate items).
- **Tooltip drag-suppression during a shop drag** — `ShopSlotDragHandler` doesn't publish `ItemDragBeganEvent`, but every hover path is covered via the direct `Unpin()` call and `InspectableItem`'s `eventData.dragging` guard. Fragile but not currently a bug.
- **`RunEndOverlayController` setting `Time.timeScale = 0`** — intentional, documented `TODO(WS-pause-or-slowmo)`.
- **`ShopController` reroll cost progression** (1g/2g/3g tiers) — internally consistent, covered by `ShopRerollCostTests`.

## Appendix B — Notable correct wiring (verified clean)

`RunManager._defaultHero` and `DataRegistry._manifest` are correctly wired in `Boot.unity`; `DataManifest.asset` lists all 12 content assets and every GUID resolves; all 22 ScriptableObjects have populated unique `_id`s and valid `Shape` references; no missing scripts (`m_Script: {fileID: 0}`) or dangling references to deleted classes anywhere; all 9 prefabs use L-003-compliant 19-digit fileIDs; HP-bar fill Image is L-006-compliant; lock-icon sprites use the L-013 `fileID: 21300000`; both EventSystems use `InputSystemUIInputModule` (L-002); camera orthographic size is 8 (the prior audit's off-by-one was resolved).

---

## Recommended fix order

**Tier 1 — before the next playtest of the committed game:**
1. **C-1** — run TMP migration on `Game.unity` (and `MainMenu.unity`). Without this, half the in-game UI is invisible.
2. **H-3** — wire `PauseMenuController._inputActions` (Unity drag-drop).
3. **H-2** — create `MainMixer.mixer` and wire it (Unity).
4. **H-1** — decide and implement the between-rounds heal policy. *Confirm intended design first.*

**Tier 2 — before committing WS-013 Save/Resume:**
5. **C-2** — publish `RoundEndedEvent` from `RunRestoreController` (one line; fixes shop UI + combat state).
6. **H-4** — add a `catch` around `ApplySave`.
7. **H-5** — activate the GroundArea / publish the shop signal *before* `ApplySave`'s ground sections.
8. **M-1, M-4, M-13, M-16, M-17, M-22** — the rest of the resume cluster.
9. Then **playtest the full resume flow** before commit.

**Tier 3 — gameplay & economy correctness:**
10. **M-2, M-3** (gold on timer-clear + sweep race), **M-5** (attack-counter reset), **M-20** (round scaling — confirm intent).

**Tier 4 — robustness & polish:** H-6, H-7, the remaining Mediums, and the Low table as cycles allow.

**Process note:** the prior `ws_012_x_audit.md` classified the TMP scene gap as a cosmetic font issue and deferred it. The field-type change from `Text` to `TextMeshProUGUI` turned it into a null-reference failure — a reminder that "migration not run on scenes" is functional, not cosmetic, whenever the migrated field's *type* changed.
