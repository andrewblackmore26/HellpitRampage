# CLAUDE.md — Project Orientation for Hellpit Rampage

> If you are an AI coder, a fresh Claude conversation, or a new contributor: read this
> document first. It tells you what the project is, how the team works, and where to
> find more detail.

> If you have already read this in a prior session: skim §7 (current status) and §11
> (anti-patterns), then continue.

> **Maintenance note:** this is orientation, not source of truth. Design lives in the
> GDD (not yet written), systems detail in `tasks/lessons.md`, history in `tasks/todo.md`.
> When this doc and a source doc disagree, the source doc wins — and fix this doc.

---

## §1 What this project is

Hellpit Rampage is a 2D top-down roguelite built in **Unity 6 (6000.4.6f1)** with **C#**
and the **Universal Render Pipeline**. It fuses two genres: a horde-survivor combat loop
(the player fights waves of enemies in timed rounds) and a **spatial-inventory** layer
(items occupy multi-cell shapes on a backpack grid, and adjacency drives synergies). A run
is structured as a sequence of rounds — combat phase, then a shop phase between rounds —
with the target run length around 30 rounds. The narrative frame is occult horror with a
companion-as-villain arc: the companion who guides the player is the antagonist. The
project is in active prototype phase; content is placeholder-grade and systems are still
settling. Tone for all work: direct, no flourish — this is a working game, not a demo.

---

## §2 Project structure

```
Hellpit Rampage/
├── Assets/
│   ├── _Project/                  All first-party game content
│   │   ├── Audio/                 Music/ + SFX/ (no MainMixer.mixer yet — see §7/§12)
│   │   ├── Prefabs/               Player, Enemies, Projectiles, Effects, UI
│   │   ├── Scenes/                Boot, MainMenu, Game (the only three scenes)
│   │   ├── ScriptableObjects/     Hard-referenced data SOs: Items, Bags, Enemies,
│   │   │                          Heroes, + DataManifest.asset. Biomes/Dialogue/
│   │   │                          Recipes folders exist but are empty scaffolds.
│   │   ├── Scripts/               All gameplay code (see subfolders below)
│   │   ├── Settings/              URP config + Input/ (PlayerInput.inputactions)
│   │   ├── Sprites/               Placeholder art
│   │   └── Tests/EditMode/        30 EditMode test files (no PlayMode tests)
│   ├── Plugins/Demigiant/DOTween/ Tweening library — do not delete
│   ├── Settings/                  URP pipeline assets
│   └── TextMesh Pro/              TMP essentials — do not delete
├── tasks/
│   ├── lessons.md                 Hard-won gotchas, L-001 … L-020. READ FIRST.
│   ├── todo.md                    Per-spec plans, deviations, and review notes
│   └── ws_012_x_audit.md          Most recent state-of-the-project audit
├── ProjectSettings/
├── Packages/
└── CLAUDE.md                      You are here
```

**Script subfolders** (`Assets/_Project/Scripts/`): `Core/` (singletons, EventBus,
save, settings, data registry) and `Core/Events/` (event structs); `Combat/`;
`Inventory/`; `UI/`; `Save/`; `Editor/` (Editor-only tools). `Companion/`, `Narrative/`,
`Heroes/`, `Shop/`, `Utility/` exist but are empty scaffolds — no code there yet.

---

## §3 How specs work

Work is organized into **WS-NNN specs** (Work Spec, numbered). The designer drafts each
spec in a Claude conversation and provides it inline; an AI coder implements it. Spec
drafts are not committed to the repo — `tasks/todo.md` is the durable record of what each
spec did.

**Standard spec structure** (read every section — they are dense for a reason):

1. **Pre-flight check** — mandatory. Verifies preconditions and current state. If
   anything fails, surface it and stop.
2. **Goal** — one paragraph: what is true after this spec lands.
3. **Scope** — explicit IN / OUT lists. The OUT list matters as much as the IN list.
4. **Known gotchas** — numbered. The hardest-won knowledge in the spec.
5. **Implementation** — file-by-file, with code/YAML snippets where structure isn't obvious.
6. **Acceptance criteria** — explicit checklist for "done."
7. **Troubleshooting** — likely failure modes and fixes.
8. **Notes / open questions** — context that fits nowhere else.

**Naming conventions for specs:**
- `WS-NNN` — sequential specs (WS-001, WS-002, …).
- `WS-NNN.M` — sub-specs depending on a parent (WS-012.3 sits under WS-012).
- `WS-NNN.X` — audit specs that verify a sequence (WS-012.X audited the 12.x specs).
- `WS-META-NNN` — meta-level specs about the project itself (this document, for example).

**A spec is "done" when:**
- All acceptance-criteria checkboxes are checked.
- The spec's manual playtest scenarios actually pass (the designer runs these in Unity).
- All EditMode tests pass — existing plus any the spec added.
- Console is clean: **zero errors, zero warnings**.
- An implementation summary is recorded in `tasks/todo.md` (see §4).
- Committed to `main`.

**The audit pattern:** before a new spec sequence, or after a large one lands, run an
audit (precedent: `tasks/ws_012_x_audit.md`). An audit is read-only verification — walk
every claim in prior specs, mark each Implemented / Partial / Not / Regressed, and write
`tasks/ws_<scope>_x_audit.md`. Audits catch the gaps a linear spec→implement→next-spec
flow assumes don't exist.

---

## §4 Working style

This project uses a specific collaboration pattern. Follow it.

1. **Designer drafts a spec** and provides it inline.
2. **AI coder reads the spec AND the actual codebase** — comparing what the spec assumes
   against what is actually there.
3. **AI coder surfaces deviations during planning.** "Spec assumes X exists; the project
   has Y instead. Options: A, B, C." Then waits for the designer to choose.
4. **Designer approves an approach** — and may adjust scope based on what surfaced.
5. **AI coder implements**, committing in logical chunks.
6. **AI coder records an implementation summary** in `tasks/todo.md`.
7. **Designer performs any manual Unity Editor steps** the AI coder cannot (menu actions,
   drag-and-drop wiring, mixer creation).
8. **Designer verifies via manual playtest.**
9. **Implementation accepted.**

**An implementation summary (in `tasks/todo.md`) records:**
- New files created (paths).
- Existing files edited (paths + a summary of changes).
- Scene / asset YAML edited.
- New tests added, and the resulting test count.
- Every plan deviation from the spec, each with a rationale.
- Required manual Unity Editor steps for the designer.
- New lessons captured (additions to `tasks/lessons.md`).

This pattern exists to prevent: silent spec deviations, "implementation complete" claims
that don't survive a playtest, and discovering a wrong precondition hours into a spec.

---

## §5 Hard rules (NEVER)

Non-negotiable. A violation requires a deliberate designer override.

1. **NEVER use `Rigidbody2D.velocity`.** Use `linearVelocity` (Unity 6 convention; the
   whole codebase already does).
2. **NEVER use legacy `Input.*`** (`Input.GetKey`, `Input.mousePosition`, …). `Active
   Input Handling` is "Input System Package" — legacy `Input.*` silently returns zero/
   false. Use `Keyboard.current` / `Mouse.current` / `Gamepad.current` (L-008).
3. **NEVER use legacy `UnityEngine.UI.Text`.** Use `TextMeshProUGUI`. New text uses TMP,
   and write `textWrappingMode`, not the obsolete `enableWordWrapping`.
4. **NEVER use `FindObjectsOfType<T>()`, and NEVER pass `FindObjectsSortMode`.** Both are
   deprecated on this Unity build. Use the parameterless `FindObjectsByType<T>()` (L-004).
5. **NEVER ship code with errors OR warnings.** Treat warnings as errors — the spec
   pre-flight requires a clean console.
6. **NEVER reproduce copyrighted material in placeholder assets.** No song lyrics, no
   character names from other IP, no recognizable logos.
7. **NEVER `DontDestroyOnLoad(gameObject)` for a singleton parented under `Managers`.**
   It only works on roots — use `DontDestroyOnLoad(transform.root.gameObject)` (L-001).
8. **NEVER hand-author prefab (`.prefab`) fileIDs as small ints** (`&1`, `&2`). Prefab
   anchors must be large 19-digit int64 values. Scenes and `.asset` files are exempt (L-003).
9. **NEVER hand-edit scene/prefab YAML for ScriptedImporter asset references** (e.g.
   `.inputactions`). Their main-asset fileIDs are content-derived — guessing produces a
   silent null. Use Unity Editor drag-and-drop (WS-012.7 deviation 1).
10. **NEVER save Unity object references in a save file.** Persist a stable string Id and
    resolve via `DataRegistry`. InstanceIDs and GUIDs do not survive (L-018).
11. **NEVER skip a spec's pre-flight check** — even when it looks redundant. It catches drift.
12. **NEVER claim implementation complete without a manual playtest.** A clean compile is
    not a working feature.

---

## §6 Standing rules (ALWAYS)

Project conventions. Follow them by default; an exception must be intentional and surfaced.

1. **ALWAYS use the EventBus** for cross-system communication. Direct singleton-to-
   singleton references couple systems — avoid them.
2. **ALWAYS use `[SerializeField] private` for Inspector fields**, never `public`. Expose
   via a property getter where outside access is needed.
3. **ALWAYS use 4-space indentation, PascalCase classes, `_camelCase` private fields.**
4. **ALWAYS namespace new code `HellpitRampage.<Subsystem>`** (`Core`, `Combat`,
   `Inventory`, `UI`, `Save`, `EditorTools`).
5. **ALWAYS read `tasks/lessons.md` at session start** before touching anything Unity-
   specific. L-001 … L-020 capture failure modes you may be about to repeat.
6. **ALWAYS use `InputSystemUIInputModule` on EventSystems**, never the legacy
   `StandaloneInputModule` — it does nothing in this project (L-002).
7. **ALWAYS persist via the SaveManager + string-Id pattern** — every data SO carries an
   `_id`; saves resolve through `DataRegistry`. No `PlayerPrefs` (L-018).
8. **ALWAYS write tests in EditMode.** There are no PlayMode tests; keep it that way
   unless integration genuinely requires one.
9. **ALWAYS close + reopen scene tabs in Unity** before/after an Editor script or a hand
   edit modifies a `.unity` file — Unity does not auto-reload an open scene from disk and
   will overwrite your edit on save. (Tracked in the team's running gotcha list.)
10. **ALWAYS guard despawn / pool-release paths with idempotency flags** (L-005, L-020).
11. **ALWAYS guard EVERY scene-startup call site when a feature can resume into a scene** —
    partial resume-awareness produces chaotic mixed states (L-020).
12. **ALWAYS commit one WS spec per commit**, message prefixed `[WS-NNN]` / `[WS-NNN.M]`,
    and push to `main`. Single-branch workflow — no feature branches.

---

## §7 Current implementation status (point-in-time snapshot)

**Last updated:** 2026-05-16

**Working tree:** `HEAD` is commit `566e463` (WS-012.5). About 110 files of WS-012.6 /
12.7 / WS-013 work — plus this document — are on disk but **not yet committed**. If this
date is more than ~4 weeks stale, re-read `tasks/todo.md` and the latest audit instead of
trusting this table.

| Spec | Status | Summary |
|------|--------|---------|
| WS-001 | ✅ | Project bootstrap — scenes (Boot/MainMenu/Game), input actions, project settings |
| WS-002 / 002b | ✅ | Core singletons (GameManager, EventBus, SaveManager skeleton); `DontDestroyOnLoad` root fix |
| WS-003 … 011.5 | ✅ | Combat loop, object pooling, weapons & projectiles, HP bar, spatial inventory grid, shop, tooltip, and the WS-011 → WS-011.5 pivot to item-owned conditional-effect synergies (one squash commit) |
| WS-012 | ✅ | Item & bag locking, sell modal, gold auto-vacuum |
| WS-012.1 | ✅ | Tooltip UX refinement; locking consolidated into the unified tooltip |
| WS-012.2 | ✅ | Multi-cell item shapes + placeholder colors |
| WS-012.3 | 📦 | Unified tooltip (hover + click-to-pin); old DetailTooltipController/GridClickTooltipHandler removed |
| WS-012.4 | ⚠️ | Visual foundation (TMP, camera, URP audit) — TMP migration of the **scenes** is still a pending manual Editor step |
| WS-012.5 | ✅ | Ground/spillover system, bag selling, drag-mode toggle (HEAD commit) |
| WS-012.6 | ⚠️ | Settings menu, pause menu, accessibility, controller foundation — code complete, but no `MainMixer.mixer` asset exists and pause `_inputActions` is unwired |
| WS-012.7 | 📦 | Audit cleanup — shop drag-ghost fix, camera size, lessons L-016/L-017 |
| WS-012.X | ✅ | Verification audit → `tasks/ws_012_x_audit.md` |
| WS-013 | 📦 | Save / resume system (run + meta + settings; string-Id indirection) — on disk, **never playtested** |
| Full Game Audit | 🚧 | Whole-game audit started 2026-05-16; deliverable `tasks/full_audit_2026-05-16.md` not yet written |
| WS-META-001 | 🚧 | This document |

Legend: ✅ Implemented & committed · 📦 Implemented, on disk, uncommitted · ⚠️ Partial /
blocked · 🚧 In progress

**Test suite:** 30 EditMode test files under `Assets/_Project/Tests/EditMode/`. The
EditMode suite is the regression net — keep it green.

**Most recent audit:** `tasks/ws_012_x_audit.md` (2026-05-15).

---

## §8 Where to look for more

| Question | Where |
|----------|-------|
| What lessons / gotchas has the project hit? | `tasks/lessons.md` (L-001 … L-020) |
| What did each spec actually do? Plan deviations? | `tasks/todo.md` |
| What did the last audit find? | `tasks/ws_012_x_audit.md` |
| What's the working style? Hard/standing rules? | This doc — §3, §4, §5, §6 |
| What's left to do / what's blocked? | `tasks/todo.md` + §7 above |
| What is the game's design? | **No GDD exists yet.** §1 here is the only summary. A design question with no answer in `lessons.md`/`todo.md` is an open question — surface it, don't invent design. |
| What's the systems architecture? | **No Tech Architecture doc exists yet.** Read the code under `Assets/_Project/Scripts/` and the relevant lessons. |
| Where are the spec drafts? | Not in the repo — the designer provides each WS spec inline per conversation. `tasks/todo.md` is the durable record. |

A Game Design Doc and a Tech Architecture spec would each be worth creating. Until they
exist, `tasks/lessons.md` + `tasks/todo.md` + the code are canonical.

---

## §9 Naming conventions

| Thing | Convention | Example |
|-------|------------|---------|
| Asset Id (`_id` on a data SO) | snake_case | `bone_knife`, `default_hero` |
| Data SO asset file | PascalName + `_Type` suffix | `BoneKnife_Item.asset`, `WardenPouch_Bag.asset` |
| C# class | PascalCase | `InventoryService` |
| File name | Matches the class name | `InventoryService.cs` |
| Public method / property | PascalCase | `ToggleItemLock`, `CurrentGold` |
| Private field | `_camelCase` | `_isLocked` |
| SerializeField | `[SerializeField] private`, `_camelCase` | `[SerializeField] private GameObject _panel;` |
| Event struct | PascalCase + `Event`, implements `IGameEvent` | `ItemLockChangedEvent` |
| Enum | PascalCase | `Rotation`, `DragMode` |
| Namespace | `HellpitRampage.<Subsystem>` | `HellpitRampage.Inventory` |
| Scene / folder | PascalCase | `Game.unity`, `Scripts/Inventory/` |

---

## §10 "What should I do next?" — decision tree

**You were given a new WS spec to implement.**
1. Read the entire spec. Do not skim.
2. Run the pre-flight check. Verify every precondition.
3. Read the codebase the spec assumes exists. Grep for every named class/component — if
   it returns zero hits, the spec references dead code (L-015). Compare against reality.
4. Surface deviations to the designer as a planning step. Get approval before coding.
5. Implement, committing in logical chunks.
6. Record an implementation summary in `tasks/todo.md` (§4).
7. List the manual Unity Editor steps the designer must perform.

**You were asked to fix a bug.**
1. Reproduce it first. Don't fix what you can't trigger.
2. Trace the root cause. Don't grep-and-replace blindly.
3. Apply the smallest fix that addresses the cause.
4. Add a test if the behavior is now nailed down.
5. If the failure mode generalizes, capture it as a new lesson in `tasks/lessons.md`.

**You were asked a design question.**
1. Check this doc §1, then `tasks/lessons.md`, then `tasks/todo.md`.
2. If still unanswered: there is no GDD — surface it as an open question. Don't invent design.

**You were asked "what's next?"**
1. Check §7 for current status and `tasks/todo.md` for blockers.
2. Propose options from what's drafted-but-not-started. Let the designer decide.

**A test is failing.**
1. Read the test; understand what it asserts.
2. Decide whether the test or the code is wrong.
3. Test wrong (assertion outdated) → update the test, note it in the commit.
4. Code wrong → fix the code. Never edit a test to match buggy behavior.

**The console shows a warning during playtest.**
1. Investigate — warnings hide regressions. Fix the cause, not the warning.
2. If genuinely benign, document it in `tasks/lessons.md` and decide suppress vs accept.

---

## §11 Anti-patterns to avoid

Each of these has caused a real problem on this project.

1. **Implementing without reading the full spec.** Skipping pre-flight or the OUT-of-scope
   list leads straight to scope drift.
2. **Claiming "complete" when only the code is done.** Compiling is not working; wiring is
   not playtesting. WS-012.6 shipped with "pause menu present" — the pause menu does not
   actually pause, because a scene reference was never wired.
3. **Silent deviations from spec.** If you can't do what the spec says, surface it. The
   three explicit deviations during WS-013 planning were healthy; a silent one is the bug.
4. **Trusting a spec's component names.** Newer specs reference classes that older specs
   deleted (L-015). Grep the repo before wiring against any named class.
5. **Implementing without verifying current state.** The "items render 1×1" report was
   chased through fully-correct code before the audit found the real cause was the shop
   drag ghost. Verify on-disk reality first.
6. **Reusing code by copying instead of extracting.** If two sites need the same logic,
   call the shared primitive — don't proliferate near-duplicates.
7. **Casual singleton creation.** Each singleton adds lifecycle, scene-transition, and
   init-order cost. Add one only when the alternative is genuinely worse.
8. **Hardcoding paths and magic numbers.** Move them to SerializeFields or constants.
   Stacked UI with hardcoded `sizeDelta.y` overlaps the moment content grows (L-019).
9. **Ignoring `tasks/lessons.md`.** Every lesson exists because someone paid for it once.
10. **Refactoring out of scope.** A bug-fix spec is not a license to refactor a nearby
    system. Surface the refactor idea; let the designer prioritize it.
11. **Editing an open scene's YAML without telling the designer to reload it.** Unity
    keeps a stale in-memory copy and will overwrite your disk edit on save.
12. **Forgetting hot-reload semantics.** Domain reload skips `Awake`. Build code UI in
    `Awake` only (L-014); make `OnEnable` defensive for cached refs (L-007).

---

## §12 Project-specific quirks

- **Single-branch git workflow** — all commits land on `main`, no feature branches. One
  commit per WS spec, message prefixed `[WS-NNN]`.
- **`DataRegistry` uses a hand-authored `DataManifest.asset`**, not `Resources.LoadAll`.
  Deliberate for the current ~12-asset scale; reasoning in `todo.md` (WS-013 decisions).
- **Settings persist separately** as `settings.json`, distinct from the run save
  (`run_save.json`) and the meta save (`save.json`). No `PlayerPrefs` anywhere.
- **UI is mostly code-built**, constructed in `Awake`, not authored as large prefab YAML.
- **Unity cannot be driven from the dev shell** — no batch-mode Unity on PATH, and the
  editor holds a project lock. AI coders cannot run the EditMode tests or playtest; those
  steps are always handed to the designer. Static cross-reference review is the substitute.
- **Two manual Editor blockers are outstanding** (per `ws_012_x_audit.md`): no
  `MainMixer.mixer` audio asset exists, and the pause menu's `_inputActions` reference is
  unwired in `Game.unity`. Until both are fixed, volume sliders and the pause menu do not
  function in play.
- **The lesson numbering can drift** between `tasks/todo.md` and `tasks/lessons.md`. When
  citing a lesson, trust the `L-NNN` heading in `lessons.md` itself.

---

## End of CLAUDE.md

If you've read this far, you have enough context to work productively on Hellpit Rampage.
Open `tasks/todo.md` for the current focus, or wait for the designer to direct you.
