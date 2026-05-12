# Active task

Nothing in progress. WS-002 complete; next workstream TBD.

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
