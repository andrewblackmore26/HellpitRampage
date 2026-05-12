# Lessons learned

Patterns and corrections captured from designer feedback during this project.
Read this at session start before touching anything Unity-specific.

---

## L-001 — `DontDestroyOnLoad` only works on root GameObjects

**Surfaced during:** WS-002 playtest (designer feedback after commit `23a047b`).

**The mistake:** I parented `GameManager`, `EventBus`, `SaveManager` under a `Managers`
GameObject in `Boot.unity` for hierarchy organization, then each singleton's `Awake`
called `DontDestroyOnLoad(gameObject)`. Unity warns at runtime:

> DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.

The warning is non-fatal in EditMode tests (no scene loads happen), so the test suite
still passed. But the moment Boot → MainMenu transitions, the Managers hierarchy
(including all three singletons) is destroyed with the Boot scene. The singletons
become null references in MainMenu and Game.

**The fix:** every singleton that lives under a parent GameObject must call
`DontDestroyOnLoad(transform.root.gameObject)` instead of
`DontDestroyOnLoad(gameObject)`. This walks up to the topmost ancestor and persists
the whole hierarchy. Multiple calls on the same root are idempotent — fine to have
all three singletons doing it.

**When this applies:**
- Any new singleton in `Assets/_Project/Scripts/Core/` (e.g., the WS-004 PoolManager,
  AudioManager, RunManager).
- Any MonoBehaviour that needs to outlive a scene transition.
- DOES NOT apply if the GameObject is already a scene root.

**Pattern to copy:**

```csharp
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
    Instance = this;
    // DontDestroyOnLoad only works on root GameObjects. The singleton sits under a
    // `Managers` parent in Boot.unity for hierarchy organization, so persist the root.
    DontDestroyOnLoad(transform.root.gameObject);
}
```

**Resist:** "just put the singleton at scene root" undoes the hierarchy organization
the designer wants. The `transform.root.gameObject` form keeps the Inspector tidy
AND persists correctly.

---

## L-002 — New Input System requires `InputSystemUIInputModule`, not `StandaloneInputModule`

**Surfaced during:** WS-002 playtest (designer feedback — Start Run button on
MainMenu wasn't clickable).

**The mistake:** when authoring `MainMenu.unity`, I created the EventSystem
GameObject with only the `EventSystem` script (UnityEngine.EventSystems.EventSystem,
guid `76c392e42b5098c458856cdf6ecaaaa1`). No input module at all. UI clicks went
nowhere because the EventSystem had no module routing pointer/keyboard input.

Even worse: Unity's default editor template attaches `StandaloneInputModule`, which
*would* receive clicks via legacy `UnityEngine.Input`. But this project uses the new
**Input System Package** — `Active Input Handling` is set to "Input System Package"
(not "Both"), so legacy `Input.*` calls always return nothing. The Standalone module
is therefore broken in this project anyway. The editor surfaces a yellow warning
banner and a "Replace with InputSystem UI Input Module" button.

**The fix:** add `InputSystemUIInputModule` (script guid
`01614664b831546d2ae94a42149d80ac`, from `com.unity.inputsystem`) to the EventSystem
GameObject. A minimal YAML entry with no serialized fields works — its `OnEnable`
calls `AssignDefaultActions()` if no actions are set, auto-binding click/point/
navigate/submit/cancel to the package's defaults.

**When this applies:**
- Any scene that contains a `Canvas` with interactive UI (`Button`, `Toggle`, `Slider`,
  `InputField`, drag handlers, etc.).
- Any scene I hand-author with an EventSystem GameObject.

**Pattern to copy** (paste after the `EventSystem` MonoBehaviour block, increment
fileID accordingly, and add it to the GameObject's `m_Component` list):

```yaml
--- !u!114 &403
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 400}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 01614664b831546d2ae94a42149d80ac, type: 3}
  m_Name:
  m_EditorClassIdentifier:
```

**Resist:** "use StandaloneInputModule, it's simpler" — in this project it does
nothing. Don't add it. Don't list both; pick `InputSystemUIInputModule`.

---

## Meta — when capturing a new lesson

1. Number it (`L-NNN`) so future references stay stable.
2. State the mistake, not the fix, first. Then the fix. Then "when this applies."
3. Include a paste-able pattern block (code or YAML) so the next agent can copy
   without thinking.
4. Add a "Resist" line for the tempting wrong fix.
5. Read this file at the start of every Unity session before touching scene YAML
   or singleton patterns.
