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

## L-003 — Unity 6 prefab fileIDs must be large 64-bit integers, not small sequential ints

**Surfaced during:** WS-004 implementation (designer error: *"Prefab 'Player.prefab' has
unexpected file IDs and is likely to be corrupt"*). Root cause was actually introduced in
WS-003 when `Player.prefab` was hand-authored.

**The mistake:** when hand-authoring `Player.prefab` and `Enemy.prefab`, I used small
sequential anchors:

```yaml
--- !u!1 &1
GameObject: ...
--- !u!4 &2
Transform:
  m_GameObject: {fileID: 1}
```

Modern Unity (2018.3+) prefab assets require fileIDs that are large pseudo-random 64-bit
integers — the same shape as Unity-emitted ones (e.g., `&3211983173688998903`). Small
integers like `&1..&6` are reserved or treated as suspicious by the importer, so the
prefab is flagged: *"...has unexpected file IDs and is likely to be corrupt."* The
designer hits this the moment Unity attempts to import the asset.

**Scene YAML is different.** Scenes (`*.unity`) happily use small fileIDs (`&100`, `&101`,
`&200`, etc.) and that pattern has worked since WS-001. The rule is **prefab-asset only**.

**The fix:** rewrite each prefab so every `--- !u!N &<id>` anchor is a 19-digit integer
(comfortably under `int64` max `9223372036854775807`), and every cross-reference (`m_GameObject`,
`m_Component[].component`, `m_Father`, etc.) points at the new ID. Keep the prefab's `.meta`
GUID stable; PrefabImporter has no `mainObjectFileID` field so the meta needs no edit. Any
external reference to the prefab's root GameObject (e.g., a ScriptableObject's `Prefab` field)
must update its `fileID:` to the new root anchor.

**Pattern to copy** — I namespaced prefab anchors by base so different prefabs don't
collide if they ever get referenced from the same file:

```
Player.prefab : 5100000000000000001..5100000000000000006
Enemy.prefab  : 5200000000000000001..5200000000000000006
```

A real Unity-emitted prefab uses hash-derived numbers; that's fine but not required.
What matters is: large positive int64, unique within the file, stable across edits so
references don't break.

**When this applies:**
- Any time I hand-author a `*.prefab` YAML file (WS-005 weapons, WS-006 projectiles, etc.).
- DOES NOT apply to `*.unity` scene files — those keep using small fileIDs.
- DOES NOT apply to `*.asset` ScriptableObject files (NativeFormatImporter sets
  `mainObjectFileID: 11400000`, which is a known stable value).

**Resist:** "the prefab loaded fine in WS-003, so it must be OK" — WS-003 was never
Play-tested before WS-002b. The error was latent. Validate hand-authored prefabs by
modeling fileIDs against a real Unity prefab from the package cache, not from intuition.

---

## L-004 — `FindObjectsByType<T>(FindObjectsSortMode.None)` is deprecated on this Unity build

**Surfaced during:** WS-005 implementation (designer pasted a compile warning).

**The mistake:** WS-005 Gotcha #10 instructs:

> `FindObjectsOfType` is deprecated in Unity 6. Use `FindObjectsByType<T>(FindObjectsSortMode.None)`. Old API still compiles but produces warnings — which our stricter acceptance criteria will fail on.

I followed it verbatim in `PlayerWeapons.cs`. The current Unity build on this project
(6000.4.6f1, Cinemachine 3.1.6) has *already* deprecated the
`FindObjectsByType<T>(FindObjectsSortMode)` overload:

```
warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been
deprecated. Use the FindObjectsByType overloads that do not take a
FindObjectsSortMode parameter.'
```

The spec was written against an earlier point release. Unity's API churn outran it.

**The fix:** drop the `FindObjectsSortMode.None` argument and use the parameterless
overload:

```csharp
// WRONG (now-deprecated, even though it was the spec's recommendation):
Enemy[] all = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

// RIGHT for this Unity build:
Enemy[] all = Object.FindObjectsByType<Enemy>();
```

The no-arg overload defaults to `FindObjectsInactive.Exclude` (matches the old
`FindObjectsOfType` behavior), so pooled-inactive instances are already filtered.
The defensive `activeInHierarchy` filter in the loop body is still correct, just
redundant — fine to keep.

**When this applies:**
- Any new Combat / Inventory / UI code that needs to enumerate components.
- Any time a spec quotes `FindObjectsByType<T>(FindObjectsSortMode.None)` — treat the
  recommendation as out of date and silently use the no-arg overload.

**Pattern to copy:**

```csharp
T[] all = Object.FindObjectsByType<T>();
foreach (var x in all)
{
    if (x == null) continue;
    if (!x.gameObject.activeInHierarchy) continue; // defensive against future API changes
    // ...
}
```

**Resist:** "the spec said `FindObjectsSortMode.None`, so use it" — the spec is a
snapshot, the Unity build is canonical. When they disagree, the running editor wins.
If `FindObjectsByType<T>()` itself gets deprecated later, the warning text will be
explicit; follow the new direction, capture it as L-NNN.

---

## L-005 — Pooled objects with trigger callbacks must guard against double-release

**Surfaced during:** WS-006 playtest (designer hit
*"InvalidOperationException: Trying to release an object that has already been released to the pool"*
on the first projectile contact, throwing from `PoolManager.Release` → `pool.Release(instance)`).

**The mistake:** `Projectile.OnTriggerEnter2D` called `Despawn` → `PoolManager.Release`
unconditionally on every Enemy contact. When a projectile flies into a cluster where
two enemy colliders overlap (very common with `EnemySpawner` placing enemies near each
other), Unity 2D physics queues **both** `OnTriggerEnter2D` callbacks inside a single
physics step and dispatches them sequentially. `SetActive(false)` (called by the
pool's `actionOnRelease`) does not retroactively cancel callbacks already queued for
this step. The second callback re-enters `Despawn`, which calls `pool.Release`
again on the same instance. `UnityEngine.Pool.ObjectPool` has `collectionCheck`
enabled in editor builds and throws on the second release.

The same risk exists if a projectile's lifetime expiry in `Update` races with an
`OnTriggerEnter2D` from the same frame — both call paths reach `Despawn`.

**The fix:** add a `private bool _isDespawned` field to any pooled MonoBehaviour
that can self-release from multiple call sites, reset it in `OnEnable` (NOT
`Initialize` — see "Resist" below), and short-circuit subsequent despawns:

```csharp
private bool _isDespawned;

private void OnEnable()
{
    _isDespawned = false;
}

private void OnTriggerEnter2D(Collider2D other)
{
    if (_isDespawned) return;            // skip second-and-later hits this step
    // ... filter, apply effect ...
    Despawn();
}

private void Despawn()
{
    if (_isDespawned) return;            // belt and suspenders
    _isDespawned = true;
    PoolManager.Instance.Release(gameObject);
}
```

The flag also encodes the design intent (one-hit-per-projectile) — without it, a
projectile would damage every overlapping enemy in the same frame before despawning.

**When this applies:**
- Any MonoBehaviour that lives in a pool AND can call its own
  `PoolManager.Release` from more than one callback (trigger, collision, lifetime,
  external script, animation event).
- Particularly common with `OnTriggerEnter2D` / `OnTriggerStay2D` on pooled
  projectiles, hitboxes, pickups.
- DOES NOT apply to `Enemy.cs` despawn-on-death — enemies release themselves only via
  `Health.HandleDeath`, which already has an `_isDead` guard preventing
  re-publish + re-release.

**Resist:** "reset the flag in `Initialize` instead of `OnEnable`." `ObjectPool`'s
`actionOnGet` fires `SetActive(true)` **before** the caller's `Initialize` runs.
If the previous spawn left `_isDespawned = true` and `Initialize` is forgotten or
deferred even by one frame, the first physics step's `OnTriggerEnter2D` would
early-out and the projectile would never damage anything. `OnEnable` is invariant
across pool reuse; `Initialize` is opt-in.

**Resist:** "disable `collectionCheck` in `PoolManager` so the exception goes
away." `collectionCheck` is a guardrail that surfaces real bugs (this one). Keep
it on in editor; fix the caller.

**Resist:** "check `gameObject.activeSelf` instead of an explicit flag." Works in
this case but conflates "I called Release" with "I'm inactive for any reason"
(scene unload, manual SetActive, parent deactivation). Explicit flag is clearer
and survives refactors.

---

## L-006 — `UnityEngine.UI.Image` with `Type=Filled` requires a sprite, even for flat-color bars

**Surfaced during:** WS-007 playtest (designer feedback — "the red bar at the top still doesn't
decrease in size when I make contact with an enemy — instead I just die abruptly after a certain
number of enemy contacts").

**The mistake:** when hand-authoring `Game.unity`'s HP bar in WS-006, I set the `HPBarFill` Image
to `m_Type: 3` (Filled) with `m_Sprite: {fileID: 0}` (no sprite). The intent was a flat-color
bar driven by `fillAmount`. The WS-006 risks section even *flagged* this:

> HPBarFill Image Type=Filled with m_Sprite: {fileID: 0} is the one item I'm least confident about.
> If Unity renders a blank fill (no mesh) at runtime, the workaround is to drop in
> {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0} (default UISprite).

I shipped the risky configuration anyway. Result: damage applied correctly to `Health._currentHP`,
`OnHealthChanged` fired, `HPBarController.HandleHealthChanged` set `_fillImage.fillAmount = 0.92`,
but the rendered quad stayed visually full. The player died after enough contacts — bar UI
*never updated*. Telltale signature: damage works (player dies), bar doesn't move.

**Why it fails:** `Image.Type = Filled` clips the rendered quad using the sprite's mesh as a mask
and the `m_FillMethod`/`m_FillAmount` parameters as the clip ratio. With `m_Sprite: {fileID: 0}`,
there is no mesh to clip, so the fill math has nothing to operate on — Unity falls back to
rendering the full quad with the Image's color. `Simple` mode (`m_Type: 0`) renders fine without
a sprite because it doesn't try to mask. `Filled` does not.

**The fix:** assign Unity's built-in default UISprite (the same one every UI Button uses) to
the Image's `m_Sprite` field:

```yaml
m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}
m_Type: 3                    # Filled
m_FillMethod: 0              # Horizontal
m_FillOrigin: 0              # Left
m_FillAmount: 1              # driven by HPBarController at runtime
```

The default UISprite has a 9-slice border but renders cleanly even when tinted to a solid color
via `m_Color`. No texture import needed; it's a built-in asset (the `0000…f000…` guid is Unity's
fixed-asset bucket for default UI resources).

**When this applies:**
- Any hand-authored UI Image whose `m_Type` is `1` (Sliced), `2` (Tiled), or `3` (Filled).
- DOES NOT apply to `m_Type: 0` (Simple) — those render fine with `m_Sprite: 0`.
- Watch out for the symmetric inverse on `HPBarBackground` (1003): it uses `m_Type: 0` and
  `m_Sprite: 0`, which is correct and renders as a flat dark quad. Don't "fix" what isn't broken.

**Pattern to copy** — when adding a fillAmount-driven bar (HP, MP, XP, ammo, progress):

```yaml
--- !u!114 &<image-fileid>
MonoBehaviour:
  m_GameObject: {fileID: <go>}
  m_Script: {fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}   # UI.Image
  m_Color: {r: <fill-color>}
  m_Sprite: {fileID: 10907, guid: 0000000000000000f000000000000000, type: 0}      # REQUIRED
  m_Type: 3                                                                       # Filled
  m_FillMethod: 0                                                                 # Horizontal (0), Vertical (1), Radial90/180/360 (2-4)
  m_FillOrigin: 0
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillCenter: 1
  m_PreserveAspect: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1
```

**Resist:** "the WS-006 risk note said it might be fine — let's wait for playtest to find out."
That's exactly how this latent bug shipped. If a risk note documents a known-suspicious fallback,
apply the safe configuration up front. The cost of one extra YAML field is zero; the cost of
debugging a "damage works but bar doesn't" mystery cost a whole user-turn.

**Resist:** "just set Type=Simple and shrink the RectTransform width to simulate a fill."
That works but couples your UI logic to RectTransform math, requires recomputing pivot/anchor
math when the bar resizes, and breaks if the container ever uses a layout group. `Image.Filled`
with a sprite is the canonical UI bar pattern.

---

## L-007 — Hot-reload during Play mode skips `Awake`; `OnEnable` must be defensive

**Surfaced during:** WS-007 mid-session diagnostic (designer feedback — *"getting a bunch of these
errors: NullReferenceException at PlayerController.OnEnable line 23"* after I edited `Enemy.cs`
to add Debug.Log statements while the user was in Play mode).

**The mistake:** `PlayerController` initialized two non-serialized private fields in `Awake`:

```csharp
private void Awake()
{
    _rb = GetComponent<Rigidbody2D>();
    _input = new PlayerInputActions();
}

private void OnEnable()
{
    _input.Player.Enable();   // NRE if _input is null
}
```

When the user is in Play mode and I edit any script in the project, Unity does a **domain
reload**: it tears down the C# runtime, recompiles the project, and reconstructs all
MonoBehaviour instances. On reconstruction, Unity calls `OnDisable → OnEnable` on the surviving
GameObjects — but **does not call `Awake`**, because the GameObjects already exist (they're not
freshly instantiated). All non-serialized fields reset to their default values (null for
references). `OnEnable` then dereferences a null `_input` and throws.

The same crash happens if you check "Enter Play Mode Settings → Reload Domain → Disabled" and
then enter Play mode for a second time — Unity also skips `Awake` in that path.

**The fix:** make `OnEnable` idempotent and tolerant of missing initialization. Promote the
`Awake` init body to a private helper and call it from BOTH `Awake` AND `OnEnable`:

```csharp
private void Awake() => EnsureInitialized();

private void OnEnable()
{
    EnsureInitialized();         // domain-reload safety
    _input.Player.Enable();
}

private void OnDisable()
{
    _input?.Player.Disable();    // also defensive — _input may be null mid-reload
}

private void EnsureInitialized()
{
    if (_rb == null) _rb = GetComponent<Rigidbody2D>();
    if (_input == null) _input = new PlayerInputActions();
}
```

`EnsureInitialized` is idempotent (null-guards on every field), so calling it twice on a fresh
scene-load (once from `Awake`, once from `OnEnable`) is a no-op the second time. On a hot-reload
where `Awake` is skipped, the `OnEnable` call backfills the missing init.

**When this applies:**
- Any MonoBehaviour that creates non-serialized objects in `Awake` (input actions, event
  subscribers, lazily-built dictionaries, cached component references on the same GO).
- Any code path that reads such a field in `OnEnable` / `Update` / event handlers.
- DOES NOT apply to `[SerializeField]` fields — those persist across domain reload because
  they're re-deserialized from the GameObject's serialized state.

**Pattern to copy:**

```csharp
private MyRuntimeObject _runtimeObj;

private void Awake() => EnsureInitialized();
private void OnEnable()
{
    EnsureInitialized();
    // ... use _runtimeObj
}
private void EnsureInitialized()
{
    if (_runtimeObj == null) _runtimeObj = new MyRuntimeObject();
}
```

**Resist:** "just check `if (_input != null) _input.Player.Enable()` in OnEnable and skip if
null." That hides the problem — input actions wouldn't be enabled after the reload, so the
player can't move until they trigger something else that recreates `_input`. Re-initialize, don't
silently skip.

**Resist:** "always disable Enter Play Mode → Reload Domain → so Awake always runs." That's an
editor preference; you can't enforce it for collaborators, and you can't enforce it for in-Play
hot reloads from external file edits. Make the code robust instead.

**Resist:** "move all init into OnEnable, drop Awake entirely." OnEnable fires once per
enable/disable cycle. If a designer toggles the GameObject off and on during play, you'd
recreate `_input` each time and leak the old one's allocations. Keep `Awake` as the primary
init path; `OnEnable` is a recovery path.

**Resist:** "apply the same pattern to singletons (`Instance = this` in `EnsureInitialized`)."
Tempting but dangerous. Unity guarantees all `Awake` calls complete before any `OnEnable`
fires on a fresh scene-load, which is what lets `RunManager.OnEnable` reach `EventBus.Instance`
safely. After a hot-reload, `Awake` doesn't run and the order of `OnEnable` calls across
components is **undefined**. If `RunManager.OnEnable` runs before `EventBus.OnEnable`,
`EventBus.Instance` is still null and `RunManager.OnEnable → Subscribe<PlayerDiedEvent>()`
silently drops the subscription (the `if (EventBus.Instance != null)` guard skips it).
Result: player dies post-hot-reload but RunEndOverlay never appears — much worse than an
obvious NRE that surfaces the problem.

The current trade-off for singletons: leave `Instance = this` in `Awake` only, accept that
hot-reload-during-Play leaves all singletons null + NREing, and treat that as the documented
workflow "stop Play, restart Play" recovery. A proper fix needs a deferred-subscription
two-phase pattern (singletons publish a `ManagersReadyEvent` after all have initialized;
subscribers wait for it) and that design lands when WS-008+ has the headroom for it. Until
then: apply the L-007 pattern ONLY to MonoBehaviours that don't participate in cross-singleton
dependencies (e.g., `Enemy`/`Projectile` `_rb` fetches, `PlayerController._input`).

---

## L-008 — `Input.mousePosition` is dead in this project; use `Mouse.current.position.ReadValue()`

**Surfaced during:** WS-010 implementation (Tooltip authoring). Spec §4.12.2 wrote `Vector2 mousePos = Input.mousePosition;` and §7 even reassured "Tooltip uses legacy `Input.mousePosition` — supported in Unity 6 alongside the new Input System." That reassurance is *wrong* for this project.

**The mistake (preempted, not committed):** following the spec verbatim would have shipped a Tooltip that always reads `(0, 0, 0)` for the cursor and renders at the canvas origin every frame. The bug would look like "tooltip appears but pinned in the wrong place" — not an obvious "tooltip never shows" failure, so it could easily slip past a quick playtest.

**Why it fails:** `ProjectSettings → Active Input Handling` is set to **"Input System Package"** (not "Both") in this project (verified by L-002, and by `ProjectSettings/ProjectSettings.asset:m_ActiveInputHandler: 1`). With that setting, the legacy `UnityEngine.Input.*` static surface still *compiles* but every call returns its default (zero / false / empty). Mouse position reads silently return `(0, 0, 0)`. There is no warning, no exception — just wrong data.

**The fix:** read the mouse position via the new Input System's `Mouse.current`:

```csharp
using UnityEngine.InputSystem;

Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
```

The null guard matters: `Mouse.current` is null on a machine with no mouse attached (e.g., a touch-only build), or briefly during domain reload. The legacy `Input.mousePosition` returned `Vector3.zero` in those cases silently — the new API returns null and you'd NRE without the guard.

The returned `Vector2` is in the same screen-space coordinate system as the old `Input.mousePosition.xy`, so any downstream `RectTransformUtility.ScreenPointToLocalPointInRectangle` call works unchanged.

**When this applies:**
- Anywhere we need cursor screen position: tooltips, drag ghosts, drop indicators, debug overlays, custom right-click menus.
- Anywhere we need keyboard state: use `Keyboard.current.<key>.wasPressedThisFrame` etc. (Already established in `DragHandler` and `ShopSlotDragHandler` — keep doing it.)
- Anywhere we need gamepad state: `Gamepad.current.<button/stick>.ReadValue()`.
- DOES NOT apply to UI clicks routed through the EventSystem — those go through `InputSystemUIInputModule` (L-002) and arrive as `PointerEventData` regardless of which input backend is active.

**Pattern to copy:**

```csharp
using UnityEngine.InputSystem;

private void Update()
{
    Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    bool clicked        = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    bool rPressed       = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    // ...
}
```

**Resist:** "the spec says `Input.mousePosition`, ship it as written." Spec is downstream of project settings, which change on a per-repo basis. When the spec contradicts the active project configuration, the project wins. The same trap fired in L-004 (deprecated `FindObjectsSortMode.None`) — pattern is identical: spec snapshot vs. live project state, project wins.

**Resist:** "switch `Active Input Handling` to `Both` so legacy `Input.*` works again." It's tempting and would unblock the spec, but it doubles the input attack surface (now both backends route events) and slows down editor startup. The new Input System is canon for this project per L-002; don't undo it for a one-line cursor read.

---

## L-009 — `SetAsFirstSibling` on a uGUI overlay hides it beneath opaque siblings

**Surfaced during:** WS-011 playtest (designer feedback — *"The bonus appears but I do not see any red line"*).

**The mistake:** `SynergyHoverDisplay.SpawnLine` followed the WS-011 spec's gotcha #19 verbatim:

```csharp
go.transform.SetAsFirstSibling(); // render under items
```

The intent was "line above the grid cells, beneath the item icons." But every visual layer in
`InventoryGridView` — cells, bags, items — is instantiated as a **direct child of the same
`_gridParent` RectTransform**:

```csharp
// InventoryGridView.BuildCellGrid
Image cell = Instantiate(_cellPrefab, _gridParent);
// InventoryGridView.RenderBag / RenderItem
Image itemImg = Instantiate(_itemPrefab, _gridParent);
```

uGUI renders children in `m_Children` order — **later siblings draw on top of earlier ones**.
`SetAsFirstSibling()` pushes the line to index 0, so it draws BEFORE the 54 opaque cells
(`_emptyCellColor.a = 1`). The cells overpaint the line completely. The damage bonus + tooltip
worked because they don't depend on rendering — but the line was invisible.

**The fix:** let the line render last (or use `SetAsLastSibling` explicitly). With
`raycastTarget = false` on the line's `Image`, it still doesn't intercept drag input meant for
items underneath, so "on top of items" causes no behavioral problems — only visual benefit:

```csharp
img.raycastTarget = false;
// ... position math ...
go.transform.SetAsLastSibling();
```

The spec's "below items, above cells" intent would require either a dedicated middle-layer
RectTransform (a `GridLines` child sibling between cell and item containers) OR
`SetSiblingIndex(cellCount)`. Neither is worth the complexity when "above everything with
alpha 0.7" is a cleaner UI affordance anyway (Slay the Spire's artifact links work this way).

**When this applies:**

- Any uGUI overlay (synergy lines, drop indicators, connection arrows, target highlights,
  damage popups) spawned into a parent that *also* holds opaque background siblings.
- DOES NOT apply when overlays go into their own dedicated parent RectTransform (e.g., a
  `OverlayLayer` child whose siblings are guaranteed to be only overlays).

**Pattern to copy** for any uGUI overlay element:

```csharp
GameObject go = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
go.transform.SetParent(overlayParent, false);   // worldPositionStays=false for UI

var img = go.GetComponent<Image>();
img.color = new Color(tint.r, tint.g, tint.b, 0.7f);
img.raycastTarget = false;                       // overlay never blocks input

// ... layout / size / rotation ...

go.transform.SetAsLastSibling();                 // draw on top; raycastTarget=false keeps it click-through
```

**Resist:** "the spec says SetAsFirstSibling, ship it as written." Same pattern as L-004 and
L-008 — when the spec contradicts the live project's actual rendering hierarchy, the project
wins. Verify visually before declaring an overlay implementation done; "the tooltip appears"
is NOT proof that the line appears.

**Resist:** "drop the opacity to 1.0 so the line is obvious." That over-corrects toward
ugliness. The fix is sibling order, not alpha — though slightly higher alpha (0.55 → 0.7)
and thickness (2px → 3px) help readability without making the line dominate the canvas.

**Resist:** "make cells transparent so the line shows through them." That would break the
existing grid visual (empty cells are clearly demarcated dark squares — important for drag
targeting). Touch the new code, not the old layer.

---

## L-010 — Be willing to pivot architecture once a working implementation reveals the right shape

**Surfaced during:** WS-011.5 spec authoring (designer feedback) — WS-011 shipped a working global-rule synergy system (`ItemSynergyRule` + `SynergyRegistry` ScriptableObjects), but designing the next 20 synergies revealed the rules were spreading across two locations (a registry asset + the item assets they applied to). The user locked in an "item-owned conditional effects" model in GDD §11 instead, and WS-011.5 was specced to replace WS-011's architecture mid-stream.

**The mistake (averted):** the temptation when a recently-shipped system *works* is to extend it rather than rebuild it. "We have rules, we have tooltips, we have hover lines — let's just add Behavior as a third rule type and call it done." That preserves sunk cost but compounds the wrong abstraction.

**The fix:** treat the working WS-011 implementation as **paid-for research**. The shape of the problem was unknowable before WS-011 was built; once built, the right model became obvious (rules belong on the item that performs them, not in a parallel registry). Then delete the registry layer cleanly and rebuild from the new center.

**When this applies:**

- Any system that's been built once and "works" but the designer surfaces a re-shaped requirement that conflicts with the current architecture's primary axis (data location, ownership, identity, lifetime).
- Especially in mid-prototype phase, where the cost of pivoting is bounded (~10h of code rewriting) and the cost of carrying the wrong abstraction forward is unbounded (every future content item authored against the old shape multiplies the migration cost later).

**Pattern to copy:**

1. **Diagnose what was right about the first build** — extracting `ItemStatModifiers` and `Resolution` as pure data types survived the rewrite unchanged; the work spent on `SynergyResolver`'s adjacency math was reused. The wasted code was the surface layer (rule assets, registry assets, hover-line component). Identify the load-bearing pieces before destroying the surface.
2. **Delete the old surface in one atomic commit** — don't leave `// TODO remove later` stubs. Keep main branch always buildable.
3. **Migrate the on-disk data in the same change** — WS-011.5 moved the two rules' content into item-owned ConditionalEffects lists in the same pass that deleted the rule assets. The repo never has a "half-migrated" intermediate state.
4. **Update tests in lock-step** — the 8 SynergyResolverTests built on the global-rule API would only have caused false-confidence if kept around. Deleted with the architecture they tested; replaced with 12 new tests against the new model. Test count went 75 → 79.

**Resist:** "we can keep the registry around as a Phase-1-compatible adapter so old assets still load." Adapter layers proliferate. The right move is a single clean break with an explicit cutover, not a long migration window.

**Resist:** "if we rewrite, we lose the work." The deleted code is in git. The lesson stays. The next refactor will go faster because the team has now done one.

---

## L-011 — Third-revision-in-a-day: stop authoring around pre-written specs as gospel

**Surfaced during:** WS-011 → WS-011.5 v2 → WS-011.5 v3, all on 2026-05-14. Three architectures in one day:
1. WS-011: global rule registry (`ItemSynergyRule` + `SynergyRegistry` ScriptableObjects).
2. WS-011.5 v2: item-owned recipient-only (effects live on the receiving weapon's ItemData; "Bone Knife declares 'when Sharpening is adjacent to my star, +1 dmg to me'").
3. WS-011.5 v3: item-owned unified-target (effects live on the active item with a `Target: Self | Neighbor` field; "Whetstone declares 'when Weapon is adjacent to my star, +1 dmg to neighbor'" — Brotato-style).

Each revision was a strict improvement; the issue isn't that earlier versions were wrong, but that **the first written spec read very confidently in either direction** and I implemented each fully before discovering the next reframing. L-010 already captured the lesson "pivot when working code reveals the right shape" — L-011 sharpens it.

**The mistake (averted by the user, not by me):** if the user had only fed me v1 of the v3 spec, I would have shipped it without ever asking "is this the BB pattern or the magical-sword pattern?" — both are valid; both are different. A spec that conflates them produces working code that pleases the spec author for a day, then breaks the moment the next 5 items are designed.

**The right move next time:** when a spec describes a model that has multiple plausible directions for "who is the active party and who is the recipient" — pause BEFORE implementing and ask the user a clarifying question about an extreme example. For synergies the question is:

> "If a Whetstone sits next to a Bone Knife and the Whetstone has the activation logic, which item is the 'owner' of the effect for purposes of UI, tooltips, save data, and the resolver's primary keying?"

The user's answer collapses the search space. The two follow-up specs were collapsed by exactly this realization. If I'd surfaced the question after WS-011, the v2 implementation would have been skipped entirely.

**Pattern to copy when a synergy/effect/ability spec arrives:**

1. **Identify the directional ambiguity.** Synergies always have an "active party" and a "passive recipient"; tools always have an "owner" and a "user"; effects always have a "source" and a "target." Whichever direction the spec writes from, check whether the OPPOSITE direction is also plausible for any example item.
2. **Probe with one extreme example each direction.** "Whetstone buffs neighbors" vs "Sword consumes adjacent whetstones for +damage" — both are real patterns from real games. If both feel like things the designer wants, the architecture must support both — meaning a `Target` field or equivalent on the data, not separate code paths.
3. **Implement the unified version first.** Don't ship the partial version and "extend later" — the data shape decides everything downstream (UI, tooltips, save format, test fixtures). v3's `Target: Self | Neighbor` is the simplest possible unifier; everything else falls out cleanly.

**When this applies:**

- Any spec describing a system that fires from one item TO another item (synergies, status effects, abilities, area buffs, recipes, bonds).
- Especially when the spec uses asymmetric language ("the starred item", "the recipient", "the source", "the contributor") — that's the tell that one direction may be hardcoded in the head of the spec writer.

**Resist:** "the spec is detailed and self-consistent, just build it." Detailed-and-self-consistent specs can still be wrong about the direction of fundamental data ownership. The cost of one clarifying question (one user turn) is much less than the cost of a full re-implementation (a half-day of work, twice, in this case).

**Resist:** "we've already implemented v2, sunk cost says we should patch it." See L-010 — paid-for research. v2's body of work (resolver/service/star overlay/behavior pipeline/test infrastructure) carried forward almost entirely. The data shape and authoring direction reversed; everything else stayed.

---

## Meta — when capturing a new lesson

1. Number it (`L-NNN`) so future references stay stable.
2. State the mistake, not the fix, first. Then the fix. Then "when this applies."
3. Include a paste-able pattern block (code or YAML) so the next agent can copy
   without thinking.
4. Add a "Resist" line for the tempting wrong fix.
5. Read this file at the start of every Unity session before touching scene YAML
   or singleton patterns.
