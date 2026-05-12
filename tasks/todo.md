# Active task

Nothing in progress. WS-002b fixes applied; next workstream TBD.

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
