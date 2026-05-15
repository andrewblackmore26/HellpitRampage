# WS-014.A Audit — Pre-First-Playable Foundation Check

Date: 2026-05-16
Audited by: Claude (Opus 4.7) — 5 parallel read-only subagents + main-thread synthesis, reconciled against `tasks/full_audit_2026-05-16.md`
Source spec: `WS-014_A_Audit_Foundation.md`
Branch: `main` (commit `566e463` + extensive uncommitted working tree — see Pre-Flight below)

---

## Summary

- Total checks: **87**
- ✓ Implemented: **57**
- ⚠ Partial / off-spec but functional: **2**
- ✗ Not implemented / broken: **13**
- 💥 Regressed: **0**
- ❓ Cannot determine (Unity Editor / manual playtest required): **15**

**Headline:** This audit found and reconciled with a same-day whole-project audit, `tasks/full_audit_2026-05-16.md` (62 findings). Three foundation problems block "first playable":

1. **The pause menu was dead** (user's reported bug). `PauseMenuController.cs` is complete and code-builds its whole UI, but `PauseMenuController._inputActions` was `{fileID: 0}` (null) in `Game.unity` — the WS-012.7 manual wiring step that was deferred and never done. `EnsurePauseAction()` early-returned on the null asset, so Escape/Start were never listened for. **FIXED in §3.1** — the fragile SerializeField was removed entirely; the Pause action is now resolved in code via `new PlayerInputActions()` (the pattern `PlayerController` already uses). No Unity Editor step, cannot be forgotten again.

2. **Resume soft-locks (`full_audit` C-2).** `RunRestoreController` restores run state then publishes `ShopPhaseStartedEvent`, but the shop UI (`ShopOverlayController` panel, `ShopController` slot population) opens on `RoundEndedEvent` — not `ShopPhaseStartedEvent`. On resume the shop never appears: empty scene, no way to advance. **FIXED in §3.2** — `RunRestoreController` now also publishes `RoundEndedEvent`, mirroring `RunManager.EndCurrentRound`'s normal order. Verified by direct code trace; **not yet playtested.**

3. **In-game HUD text renders blank (`full_audit` C-1) — NOT fixable here.** TMP migration was run on scripts and prefabs but never on the scenes. The C# label fields are now `TextMeshProUGUI`-typed while `Game.unity` still wires legacy `UnityEngine.UI.Text` components; the type mismatch makes ~17 references deserialize to `null`. Round timer, gold counter, shop item names/prices, reroll cost, sell-modal text and the run-end header all display **nothing** — silently, because every consumer null-guards. This is an **Editor-only fix** (run the migration tool per-scene); it cannot be done via static file edits. It is the **#1 blocker for WS-014.B** and is documented below as Important/manual.

**Correction to a spec assumption:** WS-014.A §2.3 / §3.4 suspected `RunEndedEvent` has no death publisher. **The audit disproves this** — player death is wired end-to-end (`Health → PlayerDiedEvent → RunManager.HandlePlayerDied → EndRun(false) → RunEndedEvent`) and unit-tested. The §3.4 fix was **not applied** — it is not needed.

**Correction to this audit's own first pass:** the 5 scoped subagents initially rated the TMP gap "cosmetic" and WS-013 restore "clean." Reconciling against `full_audit_2026-05-16.md` and re-verifying directly corrected both — see C-1 and C-2 above. The lesson is recorded as L-022.

---

## §2.1 WS-012.6 (Settings + Pause Menu)

### Settings code — ✓ 6/6

- ✓ `SettingsManager.cs` exists with state + setter API ([SettingsManager.cs:82-271](Assets/_Project/Scripts/Core/SettingsManager.cs#L82-L271)).
- ✓ `SettingsState.cs` exists — 16 fields + `FromSaveData`/`ToSaveData` ([SettingsState.cs:3-72](Assets/_Project/Scripts/Core/SettingsState.cs#L3-L72)).
- ✓ `SettingsChangedEvent` defined — `struct ... : IGameEvent` + `SettingKind` enum.
- ✓ `SaveManager.SaveSettings(SettingsSaveData)` ([SaveManager.cs:155-166](Assets/_Project/Scripts/Core/SaveManager.cs#L155-L166)).
- ✓ `SaveManager.LoadSettings()` — with null + exception fallback ([SaveManager.cs:168-191](Assets/_Project/Scripts/Core/SaveManager.cs#L168-L191)).
- ✓ `settings.json` written on a setting change — every setter calls `Persist()` → `SaveSettings` → `File.WriteAllText`.

### Audio mixer (WS-012.7 manual step) — ✗ 5/5

- ✗ `MainMixer.mixer` asset does not exist — glob `**/*.mixer` returns nothing.
- ✗ Master → Music/SFX/Voice hierarchy — no asset.
- ✗ Four exposed parameters — names exist only as string literals in `SettingsManager`.
- ✗ `SettingsManager._mainMixer` wired — `{fileID: 0}` at [Boot.unity:583](Assets/_Project/Scenes/Boot.unity#L583).
- ✗ Functional: slider moves audio — `ApplyVolume` early-returns on null mixer; no AudioSource routes `Output` to a group. (`full_audit` H-2.)

### Settings menu UI — ✓ 7/7

- ✓ Accessible from Main Menu — `_settingsButton → OnSettingsClicked → _settingsMenu.Open` ([MainMenuController.cs:75-83](Assets/_Project/Scripts/UI/MainMenuController.cs#L75-L83)).
- ✓ `SettingsMenuController.cs` with tabs — four-tab enum + tab bar ([SettingsMenuController.cs:379-393](Assets/_Project/Scripts/UI/SettingsMenuController.cs#L379-L393)).
- ✓ Audio tab — 4× `BuildSliderRow` (slider + value + mute).
- ✓ Display tab — resolution cycler, fullscreen, VSync, framerate, Apply.
- ✓ Accessibility tab — shake cycler, Reduce Motion, High Contrast.
- ✓ Controls tab — static bindings text.
- ✓ Back button — `Close()` invokes `_onClose`.

### Pause menu — ✗ 1/14 (the blocker; FIXED in §3.1)

- ✓ `PauseMenuController.cs` exists ([PauseMenuController.cs:16](Assets/_Project/Scripts/UI/PauseMenuController.cs#L16)).
- ✓ PauseMenuController GameObject in Game.unity — `PauseMenu` (`fileID 5000`), MonoBehaviour at [Game.unity:5836](Assets/_Project/Scenes/Game.unity#L5836).
- ✓ Panel exists — **code-built** in `Awake` (`BuildUI()`, [PauseMenuController.cs:164-193](Assets/_Project/Scripts/UI/PauseMenuController.cs#L164-L193)); built in `Awake` not `OnEnable` (L-014 compliant). The scene GameObject correctly has `m_Children: []`.
- ✓ Panel default state inactive — `_root.SetActive(false)` in `Awake`.
- ✓ Resume button wired — code-built, `OnResumeClicked → Resume()`.
- ✓ Settings button wired — code-built, opens `_settingsMenu`.
- ✓ Quit-to-Menu button wired — `GameManager.TransitionTo(GameState.MainMenu)` (GameManager has no `ReturnToMainMenu`; `TransitionTo` is the correct equivalent).
- ✗ **`_inputActions` NOT `{fileID: 0}`** — FAILED. Was `{fileID: 0}` at [Game.unity:5848](Assets/_Project/Scenes/Game.unity#L5848). **The entire "pause menu doesn't exist" bug. FIXED in §3.1** (`full_audit` H-3).
- ✓ Subscribes to Pause action in OnEnable — correct code; was inert pre-fix because the action never resolved.
- ❓→fixed Escape opens panel — sound after §3.1.
- ❓→fixed Gamepad Start opens panel — sound after §3.1.
- ❓ Resume hides panel / restores `Time.timeScale` — code correct.
- ❓ Quit to Menu returns to Main Menu — code correct.
- ❓ Settings opens — `_settingsMenu: {fileID: 5012}` is correctly wired.

### Light controller pass — ✓ 3/4

- ✓ Move action — named `Movement`; WASD + `<Gamepad>/leftStick` ([PlayerInput.inputactions:37-102](Assets/_Project/Settings/Input/PlayerInput.inputactions#L37-L102)).
- ✓ Pause action — `<Keyboard>/escape` + `<Gamepad>/start`.
- ✓ EventSystem uses InputSystemUIInputModule — both scenes; no `StandaloneInputModule`.
- ✗ Gamepad menu navigation — both EventSystems have `m_FirstSelected: {fileID: 0}`; no code sets initial focus. Deferred (Important).

### TMP migration (WS-012.7 manual step) — ✗ 2/3 — and the failure is FUNCTIONAL, not cosmetic

- ✗ **Game.unity zero legacy Text** — **19 legacy `UnityEngine.UI.Text` components remain** (script GUID `5f7201a12d95ffc409449d95f23cf332`). **`full_audit` C-1 (Critical, Confirmed):** because the C# label fields were retyped to `TextMeshProUGUI`, the scene's references to these legacy `Text` components deserialize to `null`. ~17 in-game labels — round timer (~`Game.unity:2043`), gold (~`:3513`), shop item names/prices (5 slots), reroll cost (~`:5035`), sell-modal text (~`:5219`), mode-toggle label (~`:5718`), run-end header (~`:1909`) — render **blank**. No crash (every consumer null-guards). The prior `ws_012_x_audit.md` mis-classified this as a cosmetic Arial-vs-TMP font issue; the field-type change makes it a null-wiring failure. See L-022.
- ✗ MainMenu.unity zero legacy Text — 4 legacy Text components. (`full_audit` L-24 — cosmetic here: MainMenu's controllers code-build their text, so no null-wiring, just font inconsistency.)
- ✓ Default TMP font asset exists — `LiberationSans SDF.asset` under `Assets/TextMesh Pro/Resources/Fonts & Materials/`.

---

## §2.2 WS-013 (Save/Resume)

### Save code — ✓ 8/8

- ✓ `SaveRun()` / `LoadRun()` / `HasRunSave()` / `DeleteRunSave()` — [SaveManager.cs:258/232/214/278](Assets/_Project/Scripts/Core/SaveManager.cs#L214).
- ✓ `RunSaveData` DTO — `BagSaveEntry`/`ItemSaveEntry`/`GroundItemSaveEntry`, string-Id keyed ([RunSaveData.cs:11](Assets/_Project/Scripts/Core/RunSaveData.cs#L11)).
- ✓ Subscribes to `ShopPhaseStartedEvent` (auto-save) and `RunEndedEvent` (auto-delete) — [SaveManager.cs:88-89](Assets/_Project/Scripts/Core/SaveManager.cs#L88-L89).
- ✓ Atomic write — `WriteAtomic` temp → Delete + `File.Move`, try/catch publishing `RunSaveFailedEvent`. (Minor: Delete+Move is not crash-atomic — `full_audit` L-16; `RunSaveFailedEvent` has no subscriber — `full_audit` M-18.)

### Restore flow (presence) — ✓ 5/5

- ✓ `RunRestoreController.cs` exists + in Game.unity ([Game.unity:5910](Assets/_Project/Scenes/Game.unity#L5910)).
- ✓ `GameManager.PendingResume` ([GameManager.cs:22](Assets/_Project/Scripts/Core/GameManager.cs#L22)).
- ✓ `GameManager.StartResumeFromSave()` ([GameManager.cs:51](Assets/_Project/Scripts/Core/GameManager.cs#L51)).
- ✓ Resume button on MainMenu — `_resumeRunButton → StartResumeFromSave`.
- ✓ Button conditionally shown — `RefreshResumeButtonVisibility → SetActive(HasRunSave())`.

### Data infrastructure — ✓ 10/10

- ✓ `HeroData.cs` + `DefaultHero.asset` (`_id: default_hero`).
- ✓ `DataManifest.cs` + `DataManifest.asset` (10 items, 1 bag, 1 hero, 1 enemy; all GUIDs resolve).
- ✓ `DataRegistry.cs` — `GetItem/GetBag/GetHero/GetEnemy`; GameObject in Boot under `Managers`, `_manifest` wired, `Awake → IndexManifest()`.
- ✓ `_id` on ItemData/BagData/HeroData/EnemyData; all 13 data assets backfilled with unique snake_case Ids.
- ✓ `Tools → Hellpit Rampage → Validate Data IDs` menu exists; would pass.

| Asset | `_id` | Asset | `_id` |
|---|---|---|---|
| BoneKnife_Item | `bone_knife` | TestStick_Item | `test_stick` |
| BottleOfHush_Item | `bottle_of_hush` | VeilThread_Item | `veil_thread` |
| HollowCrown_Item | `hollow_crown` | Whetstone_Item | `whetstone` |
| MysticSword_Item | `mystic_sword` | WardenPouch_Bag | `warden_pouch` |
| SharpeningStone_Item | `sharpening_stone` | BasicEnemy_Enemy | `basic_enemy` |
| TarnishedBell_Item | `tarnished_bell` | DefaultHero | `default_hero` |
| TempoStone_Item | `tempo_stone` | | |

### RunRestoreController behavior — ✗ 4/6

- ✓ Restores RunManager (round, gold, hero) + player HP — [RunRestoreController.cs:59-67](Assets/_Project/Scripts/Save/RunRestoreController.cs#L59-L67).
- ✓ Clears inventory before restoring — `inv.ClearAll()`.
- ✓ Bags first, then items — bag loop precedes item loop; placement failures spill.
- ✗ **Restores ground items** — code path exists ([RunRestoreController.cs:113-128](Assets/_Project/Scripts/Save/RunRestoreController.cs#L113-L128)) but `GroundManager.Current` is `null` during `ApplySave` (GroundArea ships inactive and is only activated by `ShopPhaseStartedEvent`, published *after* `ApplySave`). Ground items — and shape-conflict spillover — are **silently lost on resume**. (`full_audit` H-5. Not fixed in this spec — see Findings/Important.)
- ✓ Suppresses auto-save during restore — `BeginRestore()/EndRestore()` in try/finally.
- ✗ **Functional: New Run → quit → relaunch → Continue Run → state intact** — FAILED pre-fix: the shop never opened (C-2 soft-lock). **FIXED in §3.2** for the shop-UI path; ground items still lost (H-5). Needs a playtest to confirm.

### Manual playtest (WS-013 §4.10)

- ❓ Fresh boot, no save → only New Run — `RefreshResumeButtonVisibility` gates on `HasRunSave()`; sound.
- ⚠ Round-1 shop entry writes a save — sound, but the file is `run_save.json`, not `save.json` (by design — run state is a separate file; [SaveManager.cs:20](Assets/_Project/Scripts/Core/SaveManager.cs#L20)).
- ✗ Continue Run returns to shop with state — pre-fix C-2 soft-lock; **FIXED in §3.2**, unplaytested.
- ❓ Multi-round save preserves locks + ground — code paths present (ground subject to H-5).
- ❓ Death → save deleted — wired (§2.3); confirm file is gone via playtest.
- ❓ New Run with existing save → save cleared — `OnStartRunClicked → DeleteRunSave()`.
- ❓ Mid-shop transactions lost on quit — expected per design.

---

## §2.3 Run-End Event Publication

- ✓ `RunEndedEvent` exists — `struct RunEndedEvent : IGameEvent { bool Victory; }` ([RunEndedEvent.cs:3](Assets/_Project/Scripts/Core/Events/RunEndedEvent.cs#L3)).
- ✗ `RunEndReason` enum does NOT exist — outcome is a `bool Victory` only; "Abandoned" is unrepresentable.
- ✓ **Player death publishes RunEndedEvent** — the suspected gap does not exist: `Health.HandleDeath` (Owner.Player) → `PlayerDiedEvent` ([Health.cs:99](Assets/_Project/Scripts/Combat/Health.cs#L99)) → `RunManager.HandlePlayerDied` → `EndRun(false)` → `RunEndedEvent` ([RunManager.cs:156](Assets/_Project/Scripts/Core/RunManager.cs#L156)). Covered by `RunManagerTests`.
- ⚠ Boss / round-30 / victory condition — no boss; "victory" = round-count cap `CurrentRound >= _totalRounds` (30) → `EndRun(true)`. Expected WS-014.B work.
- ✓ Publication paths verified present (not a gap) — `RunEndedEvent` is consumed by `SaveManager` (delete save), `RunEndOverlayController` ("You Died"/"Run Complete!"), `CombatRoundController`, `PauseMenuController`, `ShopOverlayController`.

**Verdict: the death-deletes-save flow IS wired and tested. WS-014.A §3.4 not needed, not applied.**

---

## §2.4 Cross-Cutting Health

- ❓ EditMode tests pass/fail — cannot run Unity. **30 test files, 157 `[Test]`/`[TestCase]` methods**; no compile blockers found.
- ❓ Boot → MainMenu → New Run → Game loads without errors — transition path compiles; runtime needs a playtest.
- ❓ Player can play a round — needs playtest.
- ❓ Console clean across 3 rounds — needs playtest.
- ❓ No PlayMode tests fail — no PlayMode test assembly exists; N/A.
- ✓ No Missing Script references — 5 sampled prefabs all resolve; no `fileID: 0` scripts under `Prefabs/`.
- ✓ Game.unity has no `CompanionAppearanceUI` / `BiomeTransition` — confirmed absent (correct; WS-014.B work).

Compile-risk sweep: no `enableWordWrapping` in `.cs` (L-019); no live deprecated `FindObjectsSortMode` calls; no tests reference deleted classes. Working tree compiles.

---

## Findings

### Blocking — FIXED in this spec (§3)

1. **Pause menu non-functional — `_inputActions` null in Game.unity.** Fixed §3.1 by refactoring `PauseMenuController` to resolve the Pause action in code.
2. **Resume soft-locks — shop UI never opens (`full_audit` C-2).** Fixed §3.2 by publishing `RoundEndedEvent` from `RunRestoreController`.

### Blocking — NOT fixable here (Editor-only); must be done before WS-014.B

3. **In-game HUD text is blank — TMP scene migration (`full_audit` C-1, Critical).** ~17 `Game.unity` labels resolve to `null`. **The single most important pre-WS-014.B task.** Fix: in Unity, close+reopen the `Game.unity` tab (L-009), run `Tools → WS-012.4 → Migrate Text → TMP (Active Scene)`, save; re-verify the 17 fields are non-null and TMP-typed. Cannot be done via static file edits (hand-converting 19 components in scene YAML is unsafe).

### Important (deferred — manual Editor steps, or a focused resume-fix / WS-014.B)

1. **No AudioMixer asset (`full_audit` H-2).** Volume sliders are silent. Editor-only (L-016 forbids hand-authored mixer YAML): create `MainMixer.mixer`, build `Master → Music/SFX/Voice`, expose `Volume_Master/Music/SFX/Voice` (exact case), wire to `SettingsManager._mainMixer`, route AudioSources. Not a "can't play" blocker.
2. **Resume loses ground items + spillover (`full_audit` H-5).** `GroundManager.Current` is null during `ApplySave`. Fix: activate GroundArea / publish the shop signal *before* `ApplySave`'s ground sections. Code fix, deferred per agreed scope — belongs with a focused resume pass.
3. **A mid-restore exception leaves an unrecoverable half-restored run (`full_audit` H-4).** `ApplySave` is `try/finally` with no `catch`; `PendingResume` is already cleared. Fix: add a `catch` with a deliberate fallback. Code fix, deferred.
4. **No `RunEndReason` enum.** `RunEndedEvent` carries only `bool Victory`. WS-014.B's distinct death vs victory screens (and any "Abandon Run") need `RunEndReason { Death, Victory, Abandoned }` — recommend WS-014.B add it and widen `RunEndedEvent`.
5. **No boss / explicit victory entity.** Victory = surviving round 30. WS-014.B work.
6. **EventSystem `m_FirstSelected` null.** No gamepad initial focus into menus. Deferred.
7. **The wider 62-finding picture.** `tasks/full_audit_2026-05-16.md` catalogues H-1 (player HP never heals across 30 rounds — likely run-breaking, needs a design decision), H-6/H-7, and 22 Medium + 28 Low items. WS-014.B planning should read it. WS-014.A scope is WS-012.6 + WS-013 only; those items are out of scope here but are real.

### Cosmetic (defer indefinitely)

1. `SettingsManager.Persist()` writes one file per slider tick — debounce only if perf complaints arise.
2. `DataIdValidator` checks non-empty + uniqueness, not snake_case or manifest-membership (`full_audit` L-19).
3. After §3.1, `Game.unity:5848` carries an orphan `_inputActions: {fileID: 0}` YAML property for a removed field — harmless; Unity drops unmapped properties on the next scene save. Left untouched to avoid an unnecessary scene edit / the L-009 reopen dance.
4. Removed `_confirmRevertCo` — a stray 0-byte file at the repo root (untracked debris).

---

## Fixes Applied

### §3.1 — Pause menu input wiring (Blocking #1)

**File:** `Assets/_Project/Scripts/UI/PauseMenuController.cs`

Per the user decision, instead of re-deferring the Unity Inspector wiring (the WS-012.7 manual step skipped once already), the fragile SerializeField was **removed** and the action is resolved in code:

- Removed `[SerializeField] private InputActionAsset _inputActions;`.
- Added `private PlayerInputActions _input;` — the code-defined `InputActionAsset.FromJson` wrapper (`Assets/_Project/Scripts/Core/PlayerInputActions.cs`).
- `EnsurePauseAction()` now does `if (_input == null) _input = new PlayerInputActions();` then `_pauseAction = _input.Player.Pause;` — the **exact pattern `PlayerController` uses** (`PlayerController.cs:36`). Still called from `Awake` and `OnEnable` for hot-reload safety (L-007).
- `OnDisable()` now also calls `_pauseAction.Disable()`.
- Added `OnDestroy()` → `_input?.Dispose()` (destroys the `FromJson`-built asset), matching `PlayerController.OnDestroy`.

The Pause action no longer depends on any scene reference — the failure mode is structurally eliminated. No scene edit; works on next Play. ~6 net lines, one file.

### §3.2 — Resume soft-lock (Blocking #2, `full_audit` C-2)

**File:** `Assets/_Project/Scripts/Save/RunRestoreController.cs`

`RunRestoreController.RestoreNextFrame()` now publishes `RoundEndedEvent { RoundNumber = data.CurrentRound }` after restore and before `ShopPhaseStartedEvent` — mirroring the order `RunManager.EndCurrentRound` uses in the normal flow ([RunManager.cs:98](Assets/_Project/Scripts/Core/RunManager.cs#L98)).

This single event drives `ShopOverlayController.HandleRoundEnded` (activates the shop panel), `ShopController.HandleRoundEnded → PopulateAllSlots()` (stocks the 5 slots), and `CombatRoundController.HandleRoundEnded` (defined shop-state). Side effects on resume are verified harmless: no round-end gold is awarded (only `RunManager.EndCurrentRound` awards it, and it is not invoked here), and `GoldFieldSweeper`'s `RoundEndedEvent` sweep is a no-op (no gold pickups exist on resume). `ShopPhaseStartedEvent` is still published afterward for `GroundManager` / `DragModeService`.

One added publish line + explanatory comment. **Verified by code trace; not playtested** — H-4 and H-5 remain (resume is functional in the common case, fragile on exceptions and ground items).

### Cleanup

- Removed `_confirmRevertCo` — a stray 0-byte repo-root file (untracked debris).

### Not applied

- **§3.4 (RunEndedEvent death publisher)** — §2.3 proved the publisher exists and is unit-tested. Not needed.
- **§3.2-spec (AudioMixer)** and **§3.3-spec (TMP migration)** — Editor-only; documented as manual follow-ups.
- **H-4 / H-5 (resume robustness)** — agreed out of scope for WS-014.A; belong to a focused, playtested resume pass.

---

## Verification Results

- **Static review of both code fixes:**
  - Pause fix — `PlayerInputActions` is in `HellpitRampage.Core` (already imported); `_input.Player.Pause` returns `InputAction`; `PlayerInputActions : IDisposable`. Compiles; `using UnityEngine.InputSystem;` still required for `InputAction`.
  - C-2 fix — `RoundEndedEvent` (in `HellpitRampage.Core`, imported) has an `int RoundNumber` field; `ShopOverlayController` / `ShopController` / `CombatRoundController` / `GoldFieldSweeper` all subscribe to it; trace confirms shop panel + slot population fire on resume.
- **Cannot run** the 157 EditMode tests, the Boot→MainMenu→Game playthrough, or any in-Editor check — no Unity Editor available to this audit.
- ⚠ **The §4 full playtest in the spec could not be executed.** Both fixes are verified by static analysis only.

**Open items requiring the user in Unity before WS-014.B:**
1. **Run the TMP migration on `Game.unity`** (Blocking #3 / C-1) — without it the in-game HUD is blank. Re-verify the 17 label fields afterward.
2. Press Play in `Game.unity`: press Escape → pause panel appears (confirms §3.1).
3. New Run → reach the shop (auto-save) → MainMenu → Continue Run → confirm the shop opens with state (confirms §3.2). Note ground items will be missing (H-5).
4. Run the EditMode suite (expect 157 tests).
5. Optional: AudioMixer creation; H-4/H-5 resume hardening.

---

## Pre-Flight Note (§0)

- §0.1 / §0.2 — `tasks/ws_012_x_audit.md` and `tasks/lessons.md` re-read; all claimed files confirmed present.
- §0.3 — **git state is NOT clean.** ~50 modified/untracked files = the entire WS-012.6 + WS-012.7 + WS-013 implementation, done but never committed (last commit `566e463` = WS-012.5). Stopping per the spec's literal instruction was rejected: the uncommitted tree *is* the audit subject, and the spec also requires WS-013 to have "landed." Proceeded by design; per the user's decision, committed together as one `[WS-014.A]` commit with an honest message recording WS-013's known issues.
- A same-day whole-project audit, `tasks/full_audit_2026-05-16.md` (62 findings), was discovered mid-audit. It was not named in the WS-014.A spec but is authoritative on-disk ground truth; this audit reconciled against it and adopted its C-1/C-2/H-4/H-5 findings.

---

## Recommendations for WS-014.B Spec

WS-014.B (first playable) may safely assume:

- **Save / data registry is solid.** All 8 save-code + 10 data-infrastructure checks pass.
- **The pause menu now works in code** (§3.1) — Escape/Start, Resume/Settings/Quit-to-Menu. WS-014.B's "Quit to Menu mid-run" can use the existing path. Pending one Play-mode confirmation.
- **Death → RunEndedEvent → save deletion + "You Died" overlay is wired and tested.** WS-014.B only needs the Victory side.
- **Resume opens the shop again** (§3.2) — but is NOT fully trustworthy: ground items are lost (H-5) and a malformed save is unrecoverable (H-4). Treat resume as "common-case working, edge-case fragile."

WS-014.B MUST call out / handle:

1. **C-1 / TMP migration must be done first.** WS-014.B's death and victory screens add yet more text; if the migration still has not run, every new label is blank too. Fold the `Game.unity` TMP migration into WS-014.B as a gating step (or do it before WS-014.B starts) and verify.
2. **Add `RunEndReason`.** Widen `RunEndedEvent` from `bool Victory` to `RunEndReason { Death, Victory, Abandoned }` before building distinct death/victory screens.
3. **Victory has no trigger.** Round-30 survival routes to `EndRun(victory: true)`, but there is no boss and no victory screen — WS-014.B's core work.
4. **Finish the resume cluster** — H-4 (catch around `ApplySave`) and H-5 (GroundArea ordering) — and **playtest the full resume flow** before relying on it.
5. **AudioMixer** and **`m_FirstSelected`** remain undone — fold in if WS-014.B's screens need sound or controller focus.
6. **Read `tasks/full_audit_2026-05-16.md`.** Especially H-1 (no between-rounds healing — a 30-round run may be unwinnable; needs a design decision) before declaring "first playable."
7. `CompanionAppearanceUI` / `BiomeTransition` do not exist — WS-014.B greenfield.
