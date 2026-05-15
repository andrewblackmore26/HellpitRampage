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

## L-012 — `IDropHandler.OnDrop` fires before the dragged element's `OnEndDrag`

**Surfaced during:** WS-012 implementation (preempted — caught during code review, not via playtest crash).

**The mistake (averted):** when authoring `SellModal.OnDrop`, I initially considered calling
`RemoveItem` directly and letting `DragHandler.OnEndDrag` run its standard "commit drop or
revert" logic afterwards. Unity's UI EventSystem invokes drop callbacks in this order on
the frame the player releases the mouse:

1. `IDropHandler.OnDrop` on the drop target (here: SellModal) — the dragged element is
   still tracked by the EventSystem, but the target gets first crack at consuming the drop.
2. `IEndDragHandler.OnEndDrag` on the dragged element (here: DragHandler).

If the modal removes the item from `InventoryService` in step 1, then in step 2
`DragHandler.TryCommitDrop` calls `InventoryService.MoveItem(Item, _currentSnappedOrigin, _currentRotation)`
on an item that no longer exists in the grid — `Grid.MoveItem` returns false, and the
revert path tries to `_rt.anchoredPosition = _originalAnchoredPos` on a GameObject that
the next `RefreshAll()` is about to destroy. The visual ghost flashes back to origin for
one frame before the grid rebuild deletes it.

The worst case: if any code path between the two events touches `Item.HostBag` or other
fields, you get NREs because removal can leave hostBag references stale.

**The fix:** `DragHandler.OnEndDrag` checks the inventory state defensively before
attempting any commit/revert. If the item is gone, treat it as already committed:

```csharp
public void OnEndDrag(PointerEventData eventData)
{
    if (!_dragging) return;
    _canvasGroup.alpha = 1f;
    _canvasGroup.blocksRaycasts = true;

    bool soldOut = Kind == DraggableKind.Item
                   && Item != null
                   && InventoryService.Instance != null
                   && !InventoryService.Instance.ContainsItem(Item);
    if (soldOut)
    {
        _dropCommitted = true;        // SellModal already handled it; do nothing here.
    }
    else
    {
        bool committed = TryCommitDrop();
        if (committed) _dropCommitted = true;
        if (!committed) ReturnToOriginal();
    }
    // ...
}
```

The `InventoryService.ContainsItem(ItemInstance)` helper exists specifically for this
check — a one-method "is this still tracked?" query so callers don't have to crawl
`Grid.Items` themselves.

**When this applies:**

- Any `IDropHandler` that mutates state the dragged element's `OnEndDrag` will then read.
- Most common with selling, discarding, trashing, banishing — anywhere a drop target
  removes the dragged item.
- DOES NOT apply if the drop target only modifies the target's own state (e.g., a slot
  that accepts the item without removing it from the source).

**Pattern to copy** when authoring a destructive `IDropHandler`:

1. Document the OnDrop → OnEndDrag order in a comment near OnDrop.
2. Make the drag handler's revert path defensive: check inventory containment before
   touching `Item.HostBag` / cell positions.
3. Order operations in `OnDrop` so removal happens **before** any economic side-effect
   (gold grant, etc.) — if removal fails, no compensation is paid out. Mirrored in
   `SellModal.OnDrop`: `RemoveItem` first, then `AddGold`.

**Resist:** "swap the order so OnEndDrag runs first." You can't — the EventSystem
dispatches in target-then-source order and there's no public hook to reverse it.
Defensive checks on the source side are the correct shape.

**Resist:** "set `_dragging = false` in `OnDrop` so `OnEndDrag` early-returns." Tempting
but conflates two concepts (drag-in-progress vs. drag-finalized-by-someone-else) and
breaks the existing cell-highlight reset path in `OnEndDrag`. The containment check is
the cleaner signal.

---

## L-013 — Built-in PNG sub-asset reference uses `fileID: 21300000`, not the .meta's GUID-only field

**Surfaced during:** WS-012 scene wiring (preempted — confirmed by reading an existing
sprite reference before writing the new one).

**The mistake (averted):** when wiring `InventoryGridView._lockIconSprite` to the new
`lock_icon.png`, the temptation was to copy only the GUID from the .meta and write:

```yaml
_lockIconSprite: {guid: e2f30415263748596a7b8c9d0e1f2a99, type: 3}
```

That's how `_cellPrefab`/`_bagPrefab` look in the existing scene wiring — they reference
prefab root anchors (fileID 5400000000000000004 etc.) where the fileID is the prefab's
internal anchor. For a *Sprite* sub-asset of a `TextureImporter`-imported PNG, the
fileID is **always** `21300000` (Unity's canonical fileID for the auto-generated
single Sprite that a TextureImporter emits when `spriteMode: 1`).

If you omit the fileID or use 0, Unity treats the reference as the entire PNG asset
(TextureImporter root, not Sprite sub-asset) and the SerializeField stays null at
runtime. `InventoryGridView.AttachLockIcon` then logs the placeholder warning and
no icon renders. The bug is silent — no exception, just missing UI.

**The fix:** scene reference to a single-sprite PNG asset uses the full triple:

```yaml
_lockIconSprite: {fileID: 21300000, guid: <png-meta-guid>, type: 3}
```

Where:
- `fileID: 21300000` — Unity's fixed ID for the implicit Sprite sub-asset of a
  TextureImporter with `spriteMode: 1` (Single).
- `guid:` — the PNG's `.meta` guid.
- `type: 3` — script/asset type 3 means "asset reference."

Verify by `grep`ing an existing sprite reference in the project (e.g.,
`Enemy.prefab`'s `m_Sprite` line) and copying the shape.

**When this applies:**

- Any new SerializeField of type `Sprite` wired in a `.unity` or `.prefab` YAML by hand.
- DOES NOT apply to multi-sprite atlases — those have multiple sub-anchors per the
  sprite sheet, each with its own `internalID` listed in the `.meta`'s `sprites:` array.
- DOES NOT apply to references to the entire `Texture2D` (e.g., a material's
  `_MainTex`) — that uses fileID `2800000`.

**Pattern to copy** when adding a sprite asset reference in scene YAML:

```yaml
# Single-sprite PNG (Single Sprite Mode):
_someSprite: {fileID: 21300000, guid: <png-meta-guid>, type: 3}

# Multi-sprite atlas — use the internalID from the .meta's spriteSheet.sprites list:
_atlasSprite: {fileID: <internalID-of-named-sprite>, guid: <png-meta-guid>, type: 3}
```

**Resist:** "copy the prefab's fileID shape (5XX...004)." Prefab anchors are
hand-authored or hash-derived (see L-003). Sprite sub-asset anchors are Unity-fixed
constants. Don't conflate them.

---

## L-014 — Code-build complex UI in `Awake` only; `OnEnable` rebuild duplicates children

**Surfaced during:** WS-012.1 design review (preempted — caught before commit).

**The mistake (averted):** the first draft of `DetailTooltipController` called `EnsureBuilt()` from
BOTH `Awake` and `OnEnable`, mirroring the L-007 hot-reload-safety pattern. That pattern is right
for *cached component references* (`_rb = GetComponent<Rigidbody2D>()`) which are idempotent —
calling `GetComponent` twice returns the same instance. It is **wrong** for *Instantiate-driven
child UI construction*: `new GameObject(...)` and `transform.SetParent` happily create a second
copy each time they're called. After a domain reload, the surviving GameObject still has the
prior session's backdrop/panel/modal as children, `_built` is reset to false, and `EnsureBuilt`
runs again → two backdrops, two panels, two modals stacked on top of each other. Visual chaos +
duplicate event subscriptions.

**Why L-007 doesn't fit:** L-007's `EnsureInitialized` calls `if (_field == null)` guards. A
`GetComponent` call doesn't create — it queries. Building a UI tree, by contrast, is a *creation*
side effect. The "is it already built?" check would have to walk the children list and reattach
references to existing UI elements by name (fragile, complex, easy to break).

**The fix:** build UI in `Awake` only. In `OnEnable`, restrict yourself to idempotent operations
(reassign the static `Current`, re-subscribe events). Accept that after a hot-reload during Play,
the controller is broken until Play is restarted — same as L-007's documented stance for
singletons with cross-dependencies.

```csharp
private void Awake()
{
    Current = this;
    EnsureBuilt();       // builds backdrop + panel + modal children — runs ONCE per scene load
    HidePanel();
}

private void OnEnable()
{
    // L-014: domain reload skips Awake, so child refs are null and _built is false. We do NOT
    // call EnsureBuilt here — re-running it would duplicate children. The documented project
    // stance for hot-reload-during-Play is "restart Play".
    Current = this;
    SubscribeEvents();   // idempotent: handler list re-built fresh on subscribe
}
```

**When this applies:**

- Any MonoBehaviour that constructs its own child GameObjects (`new GameObject(...)`,
  `Instantiate`) in an init helper.
- Especially common for code-built tooltip/modal/HUD controllers, dynamic dialogue trees,
  procedural-UI authoring.
- DOES NOT apply to controllers whose UI is fully authored in the scene/prefab — their
  SerializeFields persist across hot-reload because Unity re-deserializes them.

**Resist:** "make EnsureBuilt smart enough to detect existing children and rewire." The rewire
path needs to find every Text/Image/Button by name, handle the case where a child is missing,
and survive future hierarchy changes. The maintenance cost dwarfs the benefit, and the failure
mode (silently wrong references) is worse than the alternative (obvious "Play is broken,
restart it" feedback).

**Resist:** "DestroyImmediate all children first, then rebuild." `DestroyImmediate` from `OnEnable`
during a domain reload can race against Unity's own GO reconstruction; even when it doesn't, you
get a one-frame flash where children disappear. Don't.

**Resist:** "make `_built` a `[SerializeField]` so it persists across reload." Serialized booleans
DO survive hot-reload, but the child *references* (`_backdropGO`, `_panelGO`, etc.) are
non-serialized — they'd still be null even with `_built = true`. You'd skip the rebuild and then
NRE on the first `_panelGO.SetActive(true)`. Either serialize ALL the refs (which means
hand-wiring in the Inspector, defeating the whole "code-built" approach) or leave it as-is.

---

## L-015 — Newer specs may reference dead patterns from earlier specs that were since revised

**Surfaced during:** WS-012.5 implementation (designer feedback — *"Grid items already work the way I want (click → detail tooltip with lock icon) because of WS-012.1. Ground items in the WS-012.5 spec were drafted before WS-012.1 existed, so they still reference the old right-click-to-lock pattern that no longer exists."*).

**The mistake (partial, caught mid-implementation):** WS-012.5 §4.7 / §4.14 told me to "reuse `LockToggleHandler` from WS-012" and to attach `TooltipTarget` (hover) to the ground item prefab. Both are pre-WS-012.1 patterns:
- `LockToggleHandler.cs` was DELETED in WS-012.1; locking moved to `DetailTooltipController` via `GridClickTooltipHandler` (left-click opens the popup with the lock action icon).
- `TooltipTarget` (hover-on-grid-items) was REPLACED in WS-012.1 by `GridClickTooltipHandler` (left-click); the hover behavior was intentionally retired so grid items have a single click affordance.

I caught most of it pre-emptively (asked the designer the ground-item lock UX question before writing code, picked "match grid pattern") — but I still added `TooltipTarget` to the ground item prefab out of habit/spec-fidelity. Designer caught the leftover and pointed it out.

**Why this trap is different from L-004 / L-008 / L-009 (snapshot-vs-reality):** those were about Unity API drift or single-spec mistakes (e.g., spec said `SetAsFirstSibling`, project's render order needed `SetAsLastSibling`). L-015 is about **cross-spec drift** — Spec N+5 references machinery from Spec N that Spec N+3 deleted. The newer spec author wrote against a mental model that hadn't been updated.

**The fix:** before implementing a UX/component layer the spec references, GREP THE CURRENT REPO for the class/component the spec names. If it doesn't exist, find what replaced it and confirm the replacement with the user before coding. The replacement IS the right pattern — not the spec's words.

```bash
# Mid-flight before authoring §4.x:
grep -r "class LockToggleHandler" Assets/_Project/Scripts/ # → 0 results
# That zero result is the signal: the spec is referencing dead code. Stop and ask.
```

**When this applies:**
- Any time a spec says "reuse component X from WS-NNN" — verify X still exists.
- Any time a spec references a class/file by name in §4 — verify it exists before scene-wiring against it.
- Especially when the spec is large (L-sized) and spans multiple subsystems that have evolved separately.

**Pattern to copy** when starting any UI-touching spec:

1. Search for every named component the spec references (`grep -r "class <name>" Assets/_Project/Scripts/`).
2. If any return 0 results, flag with the user: "spec says use X, that doesn't exist — what replaced it?"
3. For the replacement, find an existing usage site in the project (e.g., `InventoryGridView.RenderItem`) and use ITS pattern, not the spec's pattern.

**Resist:** "the spec is detailed and lists specific component names — just attach them and see if they compile." The compile is the wrong gate — `TooltipTarget` compiles just fine, but it's the WRONG component on a 2026-05 ground item. The interaction model dictates which component to use, not the type system.

**Resist:** "the spec's specific component reference IS the latest design intent — newer than the WS-012.1 changes." False: specs date themselves. The repo's current state IS the latest design intent. When they conflict, the repo wins.

**Forward-looking memory:** [feedback_spec_references_pre_revision_components.md](feedback_spec_references_pre_revision_components.md) — grep before wiring, ask the user when a referenced class is missing.

---

## L-016 — Volume sliders that "work" without an AudioMixer are silently no-ops

**Surfaced during:** WS-012.X audit (post WS-012.6 ship). The `SettingsManager` code path
was complete — sliders persisted to `settings.json`, `ApplyVolume` called
`_mainMixer.SetFloat("Volume_Master", db)`, settings round-tripped on relaunch — but
no audio in the game responded. The audit found three independent gaps:

1. No `MainMixer.mixer` asset existed anywhere in `Assets/`.
2. `SettingsManager._mainMixer` SerializeField was `{fileID: 0}` (null) in `Boot.unity`.
3. No `AudioSource` in the project routed its `Output` to a mixer group.

**The mistake:** assuming the audio chain works end-to-end because the C# is correct.
`ApplyVolume` early-returns silently when `_mainMixer == null`. `SetFloat` on an
unexposed parameter name returns false but doesn't throw. AudioSources with
`Output = None` bypass the mixer entirely regardless of the mixer state. Every
failure mode is silent.

**The fix:** three coordinated requirements, none of which can be skipped:

1. **Asset must exist:** create `Assets/_Project/Audio/MainMixer.mixer` via
   `Assets → Create → Audio Mixer` (NOT by hand-editing YAML; the mixer's GUIDs for
   groups, snapshots, and exposed params interlock and are not safely authorable in
   a text editor).
2. **Parameters must be exposed with exact names:** `Volume_Master`, `Volume_Music`,
   `Volume_SFX`, `Volume_Voice`. Case-sensitive, no spaces. Right-click each group's
   Volume slider in the Audio Mixer window → "Expose 'Volume (of <Group>)' to script"
   → rename in the Exposed Parameters dropdown.
3. **Every AudioSource must route to a mixer group:** `Output` field → pick a group
   from `MainMixer` (Music / SFX / Voice). `Output = None` bypasses the mixer
   regardless of slider position; the slider will appear to do nothing on that source.

**When this applies:**
- Any task that adds a new `AudioSource` component, anywhere.
- Any verification step that claims "audio works" — check at least one AudioSource is
  routed and verify slider movement at runtime, don't trust the code path alone.

**Pattern to verify:**

```yaml
# In Boot.unity SettingsManager block — _mainMixer must reference the .mixer asset:
_mainMixer: {fileID: <mixer fileID>, guid: <mixer guid>, type: 2}

# On every AudioSource component — m_OutputAudioMixerGroup must reference a group:
m_OutputAudioMixerGroup: {fileID: <group fileID>, guid: <mixer guid>, type: 2}
```

If either is `{fileID: 0}`, the slider is a placebo for that source.

**Resist:** "the settings round-trip test passes, so audio works." The
`SettingsSaveRoundTripTests` only verify the persistence path. They don't touch the
mixer or any AudioSource. A green test suite tells you nothing about whether sound
actually changes volume in the game.

---

## L-017 — Multi-cell drag ghost: rebuild children on drag start AND on rotation

**Surfaced during:** WS-012.X audit, tracking down the "items render as 1×1"
complaint. WS-012.2 implemented multi-cell rendering correctly for placed grid items
(`InventoryGridView.RenderItem` + `BuildItemCellChildren`) and for grid-internal
drags (`DragHandler.RebuildDraggedVisualForRotation`). But `ShopSlotDragHandler`
instantiated a single-cell `_ghostPrefab` Image at `OnBeginDrag` and never rebuilt
it — so dragging a 2×2 Hollow Crown or 1×2 Bone Knife from a shop slot showed a
fixed 1×1 yellow tile at the cursor, identical to dragging a 1×1 Whetstone. The
grid highlight underneath painted the correct multi-cell footprint, but players
fixate on the cursor.

**The mistake:** treating the cursor ghost as a "drag handle" (single visual cue
that the cursor is holding something) rather than a preview of the item's actual
on-grid appearance. When new drag sources enter the codebase (shop, future ground
re-drag, future inventory containers), the temptation is to copy whichever ghost
prefab the project already has — but the existing single-cell prefab predates
WS-012.2's multi-cell support.

**The fix:** any drag source that picks up an item from outside the grid must call
`InventoryGridView.BuildItemCellChildren(ghostRT, rotatedCells, itemData)` at:
1. **OnBeginDrag** — replaces the prefab's single-cell appearance with the real
   shape from frame 0.
2. **OnRotate** (R-key or right-click) — rebuilds children for the new rotation,
   matching the grid drag path's `RebuildDraggedVisualForRotation`.

Additionally, suppress the prefab's root `Image.color` to alpha 0 so the original
single-cell tint doesn't show through under the rebuilt children (which only cover
populated cells, not the full bbox for L-shapes).

**When this applies:**
- Any new component that drags items from a source other than the inventory grid.
- Future containers (shop, ground re-drag, crafting bench, vendor).
- NOT bags — bags drag as a unit at the grid level, not as a multi-cell ghost.

**Pattern to copy:**

```csharp
// In OnBeginDrag, after the ghost is instantiated:
RebuildGhostVisual();

// In Rotate:
private void Rotate()
{
    _currentRotation = ShapeMath.Next(_currentRotation);
    RebuildGhostVisual();
    UpdateValidationOverlay();
}

private void RebuildGhostVisual()
{
    if (_draggedOffer is not ItemData itemData) return;
    if (itemData.Shape == null) return;
    if (_gridView == null || _ghostRT == null) return;

    var rotated = ShapeMath.Rotate(itemData.Shape.Cells, _currentRotation);
    if (rotated.Count == 0) return;

    int maxX = 0, maxY = 0;
    foreach (var c in rotated) { if (c.x > maxX) maxX = c.x; if (c.y > maxY) maxY = c.y; }
    _ghostRT.sizeDelta = new Vector2((maxX + 1) * CELL_SIZE_PX, (maxY + 1) * CELL_SIZE_PX);

    var rootImg = _ghostInstance != null ? _ghostInstance.GetComponent<Image>() : null;
    if (rootImg != null) rootImg.color = new Color(0f, 0f, 0f, 0f);

    _gridView.BuildItemCellChildren(_ghostRT, rotated, itemData);
}
```

**Resist:** "extract a DragGhostBuilder helper class so all drag sources share the
code." `InventoryGridView.BuildItemCellChildren` IS the shared primitive — both
grid drag and shop drag call it directly. A wrapper class around a 5-line method
adds a layer without removing duplication. Refactor only if a third drag source
appears and the bbox+sizeDelta math becomes painful to repeat.

**Resist:** "the ghost prefab is centered (pivot 0.5, 0.5) so a multi-cell layout
will look offset." The `BuildItemCellChildren` helper uses child pivot (0,0) and
positions children at `(off.x*56, off.y*56)` from the parent's bottom-left, so the
full bbox is centered on the cursor naturally. No pivot surgery needed.

---

## L-019 — Stacked code-built text fields need VerticalLayoutGroup + ContentSizeFitter, not hardcoded sizeDelta + Overflow

**Surfaced during:** post-WS-013 playtest (designer feedback — *"The tooltip text is
overlapping each other"*).

**The mistake:** `TooltipController.BuildPanelContent` built a vertically stacked
column of five TMP text fields by parenting each one directly to the panel with
**hardcoded `anchoredPosition.y` + `sizeDelta.y`**, plus `text.overflowMode =
TextOverflowModes.Overflow`. Two failure modes compound:

1. Adjacent rects fight for the same screen space. Stats was top-anchored at
   `y=-244` with `sizeDelta.y=90` → bottom edge at `-334`. Effects was bottom-anchored
   at `y=60` with `sizeDelta.y=88` → top edge at `-212` from panel top (360 − 60 − 88).
   The two rects literally **overlapped by 56 px in layout space**, before any text
   was even populated. As soon as both fields had content, the renderer drew them on
   top of each other.
2. `TextOverflowModes.Overflow` means whenever any field's content exceeds its
   hardcoded height, the surplus **renders past the rect** onto whatever sits below.
   Bone Knife's 3-line description (slot height 50 px) bled into the Stats slot;
   Mystic Sword's 4-line stats (slot height 90 px) bled into Effects.

The bug was invisible during initial authoring — short placeholder content fit the
fixed heights — and only manifested once real items shipped with multi-line
descriptions and conditional-effect lists.

**The fix:** wrap the five text fields in a `Content` RectTransform that stretches
between the icon (top) and the ActionRow (bottom) of the panel. Give the container
a `VerticalLayoutGroup`. Give each text field a `ContentSizeFitter`
(`verticalFit = PreferredSize`, `horizontalFit = Unconstrained`). Switch the TMP
overflow mode from `Overflow` to `Truncate` as a defensive safety net. Empty content
collapses to height 0 naturally — the layout group respects that — so the visual
contract is "five rows stacked, each as tall as it needs to be, never overlapping."

The minimal Y math (top inset 120 px below panel top to clear the icon, bottom inset
64 px above panel bottom to clear the 56 px ActionRow + 8 px buffer) replaces the
five magic Y constants. Each text's font size, style, alignment, and color is still
configured per call site; only the geometry is now layout-driven.

**When this applies:**

- Any code-built UI that stacks multiple text fields, images, or icon-rows in a
  panel. If you're typing two `anchoredPosition.y = X` lines in a row, you're
  probably about to ship this bug.
- Anywhere `TextOverflowModes.Overflow` is used. Default to `Truncate` (or
  `Ellipsis`); reserve `Overflow` only for transient overlays where bleed is
  visually fine (drop indicators, ghost previews).
- DOES NOT apply to absolute-positioned UI like floating icons, drag ghosts,
  badge overlays, or modal action rows — those don't stack with siblings.

**Pattern to copy** when authoring a code-built stack-of-text panel:

```csharp
// Container that stretches inside the panel, leaving room for icon at top + action row at bottom.
var contentGO = new GameObject("Content", typeof(RectTransform));
contentGO.transform.SetParent(panel, false);
var contentRT = (RectTransform)contentGO.transform;
contentRT.anchorMin = new Vector2(0f, 0f);
contentRT.anchorMax = new Vector2(1f, 1f);
contentRT.pivot = new Vector2(0.5f, 1f);
contentRT.offsetMin = new Vector2(0f, BOTTOM_RESERVED_PX);   // ActionRow + buffer
contentRT.offsetMax = new Vector2(0f, -TOP_RESERVED_PX);     // Icon height + padding (negative)

var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
vlg.padding = new RectOffset(16, 16, 4, 4);
vlg.spacing = 6f;
vlg.childAlignment = TextAnchor.UpperCenter;
vlg.childControlWidth = true;
vlg.childControlHeight = true;
vlg.childForceExpandWidth = true;
vlg.childForceExpandHeight = false;

// Each row is a TMP + ContentSizeFitter; height tracks the rendered preferred height
// of the wrapped content. Empty text → height 0 → row collapses, no whitespace gap.
var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                        typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
go.transform.SetParent(contentRT, false);

var text = go.GetComponent<TextMeshProUGUI>();
text.textWrappingMode = TextWrappingModes.Normal;
text.overflowMode = TextOverflowModes.Truncate;  // defensive — VLG normally handles fit

var fitter = go.GetComponent<ContentSizeFitter>();
fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
```

**Resist:** "just bump `sizeDelta.y` from 50 to 80 — that fixes Bone Knife." It does
fix Bone Knife and breaks the next item that exceeds 80 px. The bug is the
fixed-height architecture, not the magic number you picked. Patch the architecture
once; never tune magic heights again.

**Resist:** "ContentSizeFitter is overkill for static content." It isn't static —
descriptions and conditional-effect lists vary by item. Even if today's content
happens to fit, tomorrow's content (new items, localization to a longer language,
font swap) will overflow the moment someone forgets to revisit the heights.

**Resist:** "switch `TextOverflowModes.Overflow` → `Truncate` and keep the hardcoded
heights — that's a smaller change." Truncate without ContentSizeFitter means the
overflow disappears silently. Player sees "DMG: 5\nCooldown: 0.5s\nRa..." with no
hint that two stat lines are hidden. Worse than the original bug. Truncate is only
acceptable as a safety net when the layout already gives each row its full needed
height.

---

## L-018 — Save files reference content by stable string IDs, never by Unity object reference

**Surfaced during:** WS-013 implementation (preempted — caught during planning, not via
playtest failure).

**The mistake (averted):** the most natural-looking save schema would hold direct
`ItemData` / `BagData` / `HeroData` references — let Newtonsoft serialize whatever it
can. That looks compact in code:

```csharp
[Serializable] public class ItemSaveEntry { public ItemData Item; public Vector2Int Origin; ... }
```

But Unity ScriptableObject references serialize to JSON as either empty objects (for
generic Newtonsoft) or InstanceID ints (Unity's JsonUtility) — and InstanceIDs are
**ephemeral**, regenerated every domain reload. Save → quit → relaunch → InstanceIDs
have shifted → every save reference resolves to null. Even if the serializer captures
the GUID (it doesn't, by default), GUIDs change when assets get reimported with
different settings or moved between projects.

**The fix:** every save schema field that points at a SO holds a plain `string`
identifier. A `DataRegistry` (Managers-parented singleton initialized at Boot) maps the
string back to the live asset reference on load. The string identifier lives on the SO
as a serialized field; the asset stem matches the string by convention (e.g.,
`BoneKnife_Item.asset` has `_id = "bone_knife"`).

The integer rotation enum value gets the same treatment — store the raw int (0/90/180/270)
rather than the enum name, so renaming `Rotation.Deg90 → Rotation.Right` later doesn't
break old saves. Cast at the boundary: `data.Rotation = (int)item.Rotation;` on save,
`(Rotation)entry.Rotation` on load.

**When this applies:**
- Any new save schema, any new persistence layer, any new RemoteConfig / Steam Cloud
  / leaderboard payload.
- Any time a spec says "save the ItemData" or "save the HeroData" — read it as
  "save the stable Id and rehydrate via DataRegistry."
- DOES NOT apply to scene/prefab YAML references — those use Unity's GUID system
  which is meta-file-scoped and stable for in-project references.

**Pattern to copy:**

```csharp
// Data SO:
[SerializeField] private string _id;
public string Id => _id;

// Schema DTO:
[Serializable] public class ItemSaveEntry
{
    public string ItemId;          // resolved via DataRegistry on load
    public int OriginX;
    public int OriginY;
    public int Rotation;           // raw enum int — survives enum renames
    public bool IsLocked;
}

// Capture:
data.Items.Add(new ItemSaveEntry
{
    ItemId = item.Data.Id,
    OriginX = item.Origin.x,
    OriginY = item.Origin.y,
    Rotation = (int)item.Rotation,
    IsLocked = item.IsLocked,
});

// Restore:
var itemData = DataRegistry.Instance.GetItem(entry.ItemId);
if (itemData == null) continue; // GetItem already logged; degrade gracefully
inv.PlaceItem(itemData, new Vector2Int(entry.OriginX, entry.OriginY), (Rotation)entry.Rotation);
```

**Resist:** "Newtonsoft is smart enough to serialize the asset reference, just try it."
It isn't — it serializes the SO's serializable members (which usually omit Unity object
identity entirely) and produces an unrestorable payload. Even if you add a
[JsonConverter] for ScriptableObject, you've coupled persistence to runtime asset
identity. The string ID layer is the indirection that lets content reshuffle without
breaking saves.

**Resist:** "we can store the asset GUID instead — those are stable." Asset GUIDs are
project-scoped and survive renames within the project, but they don't survive
project-to-project moves, code-generation, or asset re-imports that emit new GUIDs.
String IDs are content-owned and travel with the data.

**Resist:** "we have one hero, just hardcode `heroId = "default"`." Tempting on day
one, broken on day two — when the second hero ships, every existing save resolves to
"default" instead of the player's actual choice. The string ID + DataRegistry layer
costs ~30 lines to set up and pays for itself the first time multi-hero or multi-pet
content lands.

---

## L-020 — Guard EVERY scene-startup call site for resume; per-consumer despawn guards can't cover out-of-band re-activation

**Surfaced during:** post-WS-013 playtest (designer feedback — *"resume run starts me off at
round 1 but the timer goes on for infinity"* and an `InvalidOperationException` double-release
from the projectile pool).

### Part A — a resume must suppress *all* fresh-run startup, not just the one you remembered

**The mistake:** WS-013 added a `PendingResume` guard so `HeroStartingLoadout.Start()` skips
seeding the starting bag/item when resuming. But the Game scene has a **second** unconditional
startup component — `GameSceneBootstrap.Start()` calls `RunManager.StartNewRun()` every time
the scene loads. WS-013 never touched it. On resume:

1. Frame 0: `GameSceneBootstrap.Start()` → `StartNewRun()` → `CurrentPhase = Combat`, publishes
   `RoundStartedEvent` → `CombatRoundController` starts the round timer + spawns enemies.
2. Frame 1: `RunRestoreController` (deferred one frame) → `RestoreFromSave()` → `CurrentPhase = Shop`.
3. The timer's `Update()` loop runs off its own `_timerRunning` flag, not the phase. When it
   hits 0 it calls `EndCurrentRound()`, which early-returns because `CurrentPhase != Combat`.
   The round never ends → timer counts forever. Combat + shop are both "live" at once.

**The fix:** mirror the existing guard in *every* startup path:

```csharp
// GameSceneBootstrap.Start(), before StartNewRun():
if (GameManager.Instance != null && GameManager.Instance.PendingResume != null) return;
```

Ordering is safe because `RunRestoreController` clears `PendingResume` one frame later — every
`Start()` in frame 0 still sees it set.

**When this applies:**
- Any feature that "resumes / restores / loads into" a scene instead of starting it fresh.
  Before shipping, enumerate **every** MonoBehaviour with an `Awake`/`Start` that mutates run
  state or spawns content, and confirm each one is resume-aware. Grep for the thing that starts
  the mode (`StartNewRun`, `BeginLevel`, seeding loadouts) — there is usually more than one.
- The trap is partial coverage: you guard the call site you were thinking about and miss the
  sibling. The symptom (fresh content layered on restored content) is confusing because the
  restore *did* run — it just lost a race with an unguarded starter.

### Part B — a per-consumer despawn guard cannot make a pool double-release-safe

**The mistake (latent, pre-existing, amplified by Part A):** `Projectile._isDespawned` (L-005)
is correct and airtight for the same-physics-step double-trigger it was built for. But it is a
**per-activation** flag — reset every `OnEnable`. It cannot catch a double-release where the
instance is re-activated *out of band* between the two releases (scene reload / domain reload:
pooled instances live under the `DontDestroyOnLoad` PoolManager and survive scene loads, while
`PoolManager._pools` resets). `PoolManager.Release` called `pool.Release(instance)` blind, so a
redundant release tripped `ObjectPool`'s `collectionCheck` and threw.

**The fix:** add pool-level idempotency on the `PooledObject` marker — the per-instance source
of truth for "am I currently in my pool," owned by `PoolManager`, independent of any consumer's
flag:

```csharp
// PooledObject.cs
[HideInInspector] public bool IsPooled;

// PoolManager.Get(): after pool.Get()
marker.IsPooled = false;        // checked out

// PoolManager.Release(): before pool.Release(instance)
if (marker.IsPooled)
{
    Debug.LogWarning($"PoolManager.Release: '{instance.name}' already in pool; ignoring.");
    return;
}
marker.IsPooled = true;
pool.Release(instance);
```

`collectionCheck` stays **ON** — this does not suppress it (L-005's rule). It prevents the
redundant call from ever *reaching* `pool.Release`, so the guardrail still fires on a genuinely
new bug. The two guards are complementary: `_isDespawned` enforces the consumer's one-shot
semantics (one hit per projectile); `IsPooled` enforces pool integrity for *every* pooled type.

**When this applies:**
- Any object pool whose instances can outlive a scene (pool owner is `DontDestroyOnLoad`).
- Any pooled type whose self-release guard resets in `OnEnable` — i.e. all of them.
- DOES NOT replace the consumer's own guard; both are needed for different reasons.

**Resist:** "the consumer already has `_isDespawned`, the pool guard is redundant." It isn't —
`_isDespawned` resets on `OnEnable`; `IsPooled` does not reset until the next real `Get`. They
catch different windows.

**Resist:** "just disable `collectionCheck`." L-005 already forbids this. The idempotency flag
fixes the *caller* (`PoolManager`), which is exactly what L-005 asks for.

**Resist:** "Part A and Part B are unrelated, file them separately." They were one playtest:
the unguarded `GameSceneBootstrap` (Part A) ran infinite combat under a restored shop, which
fired projectiles forever and finally surfaced the latent pool defect (Part B). The lesson is
the pair — *partial* resume-awareness produces chaotic states that expose every other latent
race in the codebase.

---

## L-021 — A deferred "wire this in the Inspector" step is a bug with a delay timer; prefer code-resolved input

**Surfaced during:** WS-014.A audit. The user reported *"the pause menu doesn't exist."* The
audit traced it to one line: `PauseMenuController._inputActions: {fileID: 0}` in `Game.unity`.
WS-012.7 had **deliberately deferred** wiring that `InputActionAsset` SerializeField to a
"3-second manual drag-drop in the Unity Inspector" — the audit even logged it as a Blocking
item "queued for trivial Unity Editor wiring." It was never done. The pause menu — Resume,
Settings, Quit-to-Menu, the entire Escape/Start path — was dead code for an entire spec cycle,
and the next spec (WS-014.B) was about to be built on top of it.

**The mistake:** treating "assign this reference in the Inspector" as a free, riskless step
that can be split off from the code change and handed to a human. It is not free. A deferred
manual step has no compiler check, no test, no green/red — it is invisible until someone
plays the exact path it gates. The implementation summary said "pause menu landed"; the code
*was* complete; only the un-greppable, un-testable scene assignment was missing. Every audit
since has had to re-discover the same `{fileID: 0}`.

**The fix — two parts:**

1. **Prefer code-resolved input over a scene SerializeField for critical-path wiring.** This
   project already has a code-defined input wrapper, `PlayerInputActions`
   (`InputActionAsset.FromJson`, `Assets/_Project/Scripts/Core/PlayerInputActions.cs`).
   `PlayerController` news it up directly and never touches a scene reference. Any new
   consumer of an input action should do the same — there is then nothing to wire, nothing
   to forget, and it works on the next Play.

2. **If a manual Editor step genuinely cannot be avoided** (e.g. an `AudioMixer` asset, which
   per L-016 must not be hand-authored), it is NOT done until it has been *verified done* —
   re-grep the scene/asset for `{fileID: 0}` on the field. "I noted it for the user" is not
   completion. An audit checkbox that can only ever be ❓ is a checkbox that will fail.

**Pattern to copy** (resolve an action without a SerializeField):

```csharp
private PlayerInputActions _input;
private InputAction _myAction;

private void EnsureAction()      // call from BOTH Awake and OnEnable (L-007)
{
    if (_myAction != null) return;
    if (_input == null) _input = new PlayerInputActions();
    _myAction = _input.Player.Pause;   // or .Movement, etc.
}

private void OnDestroy() => _input?.Dispose();   // FromJson asset must be destroyed
```

**When this applies:**
- Any MonoBehaviour that needs an `InputAction` — resolve via `new PlayerInputActions()`,
  do not add an `InputActionAsset` SerializeField.
- Any spec that ends with a "manual Unity steps" list. Each item on that list is an
  un-verified claim. Either eliminate it in code, or gate the spec on re-verifying it.
- Any audit reviewing a prior spec — if a previous audit said "deferred to a trivial manual
  step," assume it was never done and check.

**Resist:** "it's just a drag-drop, the user will do it." The WS-012.7 audit said exactly
that, twice, in writing. It was not done. The cost of the deferral was a whole spec cycle of
a headline feature being silently broken.

**Resist:** "a SerializeField is more flexible — a designer can swap the input asset." No
designer on this project swaps input assets; there is one, it is code-generated from
`PlayerInput.inputactions`, and the flexibility was never used. It bought nothing and cost a
dead pause menu.

---

## L-022 — A migration not run on scenes is a *functional* break, not cosmetic, when the field's TYPE changed

**Surfaced during:** WS-014.A audit, reconciling with `tasks/full_audit_2026-05-16.md`. WS-012.4
migrated UI text from `UnityEngine.UI.Text` to `TextMeshProUGUI`. The `MigrateTextToTMPro`
editor tool was run on scripts and prefabs but **never on the scenes**. The earlier
`ws_012_x_audit.md` saw "19 legacy `Text` components in `Game.unity`" and filed it as a
*cosmetic* "Arial vs TMP font" issue, deferred. It is not cosmetic: the C# `SerializeField`
*types* were changed to `TextMeshProUGUI`, so `Game.unity`'s serialized references — still
pointing at legacy `Text` components — fail Unity's type check on deserialization and become
`null`. ~17 in-game labels (round timer, gold, shop names/prices, reroll, sell modal, run-end
header) render **blank**. It is invisible because every consumer null-guards (`if (_label ==
null) return;`) — no exception, no log, just no text.

**The mistake:** assuming "the components still exist, they'll just look slightly wrong."
That holds only if the *consuming field's type* is unchanged (then the old component is still
assignable and you get a font mismatch — genuinely cosmetic). The moment a migration changes
the field **type**, every scene and prefab that wired the old type is silently severed. A
component swap and a type change look identical in a `git diff` of the script; their blast
radius is not.

**The fix:** when a migration changes a `SerializeField`'s declared type, it is not done
until it has been run on **every** asset that wires that field — scripts, prefabs, AND every
scene. A per-scene editor tool (`Tools → WS-012.4 → Migrate Text → TMP (Active Scene)`) must
be run once per scene, with the L-009 close+reopen dance, and the references re-verified
non-null afterward.

**When this applies:**
- Any field-type migration: `Text → TextMeshProUGUI`, `Image → RawImage`, swapping a base
  class for a derived one, replacing a component with a non-assignable cousin.
- Auditing: never classify "migration tool not run on scenes/prefabs" as cosmetic without
  first checking whether the *consuming field's type* changed. If it did, the old wiring is
  `null` and the feature is broken, not ugly.

**How to detect:** grep the scene for the **old** component's script GUID (here
`5f7201a12d95ffc409449d95f23cf332` = `UnityEngine.UI.Text`). Any hit is a field that a
retyped `SerializeField` can no longer bind to.

**Resist:** "the prior audit already looked at this and called it cosmetic." The prior audit
counted the components but did not cross-check the field types. Counting legacy components
tells you migration is incomplete; only the type check tells you whether incomplete means
*ugly* or *broken*.

---

## Meta — when capturing a new lesson

1. Number it (`L-NNN`) so future references stay stable.
2. State the mistake, not the fix, first. Then the fix. Then "when this applies."
3. Include a paste-able pattern block (code or YAML) so the next agent can copy
   without thinking.
4. Add a "Resist" line for the tempting wrong fix.
5. Read this file at the start of every Unity session before touching scene YAML
   or singleton patterns.
