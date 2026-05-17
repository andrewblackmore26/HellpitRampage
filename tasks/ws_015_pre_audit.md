# WS-015 Pre-Refactor Audit — Shop-as-Overlay Current State

> Spec §0.2 deliverable. Read-only snapshot of `Game.unity` and the singleton
> architecture **before** the shop-as-scene refactor. This is the input document the
> migration works from. Captured 2026-05-18 against HEAD `7e4d7d0` (WS-014.B).

---

## 1. Scene & build state

- Gameplay scene: `Assets/_Project/Scenes/Game.unity` (GUID `0c988a6916ff423ab8153e13a01fcdb9`).
- Build Settings order: `Boot` (0), `MainMenu` (1), `Game` (2).
- Only code reference to a gameplay scene name: `GameManager.SceneNameFor()` →
  `case GameState.InRun: return "Game";` (`GameManager.cs:98`). All scene loads route
  through `GameManager.TransitionTo()` → `SceneManager.LoadScene(name)` (`GameManager.cs:89`).
- No EditMode test loads a scene by name — the rename is code/asset-only, no test churn
  from the rename itself.

---

## 2. Singleton inventory (§0.3)

All managers live under `Managers` in `Boot.unity` and persist via
`DontDestroyOnLoad(transform.root.gameObject)` (L-001). All have the duplicate-instance
guard `if (Instance != null && Instance != this) { Destroy(gameObject); return; }`.

| Singleton | Host scene | Persistent? | Notes |
|-----------|-----------|-------------|-------|
| `GameManager` | Boot / Managers | ✅ | Owns `CurrentState`, `PendingResume`; only `SceneManager.LoadScene` call site |
| `EventBus` | Boot / Managers | ✅ | Type-keyed pub/sub |
| `SaveManager` | Boot / Managers | ✅ | Auto-saves on `ShopPhaseStartedEvent` (unless `_restoringFromSave`) |
| `PoolManager` | Boot / Managers | ✅ | Object pools |
| `RunManager` | Boot / Managers | ✅ | Round/phase/gold/hero. **No HP fields.** |
| `SettingsManager` | Boot / Managers | ✅ | Settings state |
| `DataRegistry` | Boot / Managers | ✅ | Id→asset lookup from `DataManifest` |
| `InventoryService` | **Game.unity** | ❌ **scene-scoped** | Holds `Grid` (bags+items). Must be promoted. |
| `SynergyService` | **Game.unity** | ❌ **scene-scoped** | Caches `SynergyResolver.Resolution`; combat queries it. Must be promoted (D1). |
| `GroundManager` | **Game.unity** (on `GroundArea`) | ❌ **scene-scoped** | Static `Current`; ground items list. Stays Shop-scoped; state moves to `InventoryService`. |

`HeroStartingLoadout` (Game.unity root) is not a singleton but is scene-scoped and
seeds inventory in `Start()` — must move to Boot and become `RunStartedEvent`-driven (D2).

---

## 3. Game.unity hierarchy

### Root objects

| Root | Category | Notes |
|------|----------|-------|
| Main Camera | combat | Orthographic size 8; Cinemachine Brain |
| CM Player Follow | combat | Cinemachine virtual camera |
| Player | combat | `PlayerController`, `PlayerWeapons`, `Health`, `Animator` |
| ReferenceMarkers | combat | Debug markers |
| EnemySpawner | combat | `EnemySpawner` |
| Canvas | shared | Screen-Space-Overlay; `CanvasScaler` 1920×1080, match 0.5 |
| EventSystem | shared | `InputSystemUIInputModule` |
| DeathOverlayController | combat | `RunEndOverlayController` (death/victory) |
| GameSceneBootstrap | combat | Composition root → rename to `CombatSceneBootstrap` |
| CombatRoundController | combat | Round timer + state |
| RoundTimerUI | combat | Reads `CombatRoundController` |
| ShopOverlayController | **shop** | Show/hide shop panel + Next Round button |
| InventoryService | **promote→Boot** | Inventory data |
| HeroStartingLoadout | **promote→Boot** | Run-start loadout seeding |
| ShopController | **shop** | Shop slot population, reroll |
| GoldDropController | combat | Gold pickup |
| SynergyService | **promote→Boot** | Synergy resolution cache |
| GoldFieldSweeper | combat | Gold cleanup |
| DragModeService | **shop** | Item/sell drag mode |
| RunRestoreController | **move→Shop** | Resume restore (resume lands in shop phase) |

### Canvas children (render order)

```
Canvas
├── HPBarBackground → HPBarFill            combat
├── DeathOverlay  [m_IsActive: 0]          combat
│   ├── YouDiedText
│   └── ReturnButton → Text
├── RoundTimerLabel                        combat
├── ShopOverlayPanel  [m_IsActive: 0]      SHOP — extract whole subtree
│   ├── TopBar
│   │   ├── GoldDisplay (GoldDisplayController)
│   │   ├── ShopOverlayHeader
│   │   └── RerollButton
│   ├── ShopSlotsArea
│   │   └── ShopSlot_0..4 (ShopSlot, ShopSlotDragHandler each)
│   ├── InventoryGridContainer
│   │   ├── GridAnchor (BackpackBoundsProvider)
│   │   │   ├── InventoryGridView
│   │   │   └── StarIndicatorOverlay (StarredEdgeRenderer)
│   │   └── GroundArea  [m_IsActive: 0]  (GroundManager)
│   └── BottomBar
│       ├── StartNextRoundButton
│       └── ModeToggleButton (ModeToggleButton)
├── TooltipController                      shop (Canvas-level)
├── PauseMenu (PauseMenuController)         shared — duplicate into Shop
└── SettingsMenu (SettingsMenuController)   shared — duplicate into Shop
```

`ShopOverlayPanel` is a full-screen RectTransform (anchors 0,0–1,1), starts inactive.
`GroundArea` and `DeathOverlay` also start inactive.

---

## 4. What moves where

**→ Shop.unity:** `ShopOverlayPanel` + entire subtree, `TooltipController`,
`ShopController`, `ShopOverlayController`, `DragModeService`, `RunRestoreController`.
New per-scene `Camera`/`Canvas`/`EventSystem`/`SceneRoot`; duplicated `PauseMenu` +
`SettingsMenu`; new `ShopSceneBootstrap`.

**→ Boot.unity (Managers):** `InventoryService`, `SynergyService`, `HeroStartingLoadout`,
new `SceneRouter`.

**Stays in Combat.unity:** cameras, Player, enemies, gold systems, HP bar, round timer,
death/victory overlays, `CombatRoundController`, `PauseMenu`/`SettingsMenu` (original
instances). `GameSceneBootstrap` → renamed `CombatSceneBootstrap`.

---

## 5. Current round-transition flow (to be reworked — D4)

- **Combat → Shop:** `CombatRoundController` timer hits 0 → `RunManager.EndCurrentRound()`
  → sets phase `Shop`, publishes `RoundEndedEvent` (combat cleanup + shop UI show) +
  `ShopPhaseStartedEvent` (SaveManager auto-save, GroundManager, DragModeService) +
  round-end bonus gold. Final round: `EndRun(victory)` instead.
- **Shop → Combat:** `StartNextRoundButton` → `ShopOverlayController.HandleStartNextClicked()`
  → `RunManager.AdvanceToNextRound()` → `CurrentRound++`, phase `Combat`, publishes
  `RoundStartedEvent` (combat init + shop UI hide).
- **Resume:** `MainMenuController` → `GameManager.StartResumeFromSave()` → loads `Game`,
  `RunRestoreController` (1 frame later) restores state and publishes `RoundEndedEvent` +
  `ShopPhaseStartedEvent` to wake the shop UI.

Post-refactor these events become **intra-scene post-load signals** published by scene
bootstraps; `RunManager` stays the cross-scene source of truth for round/phase.

---

## 6. HP today (to be reworked — D3)

`Health` (player) initializes `_currentHP = _maxHP = _startingHP` (100) in `OnEnable`.
HP does **not** persist between rounds today only because the player GameObject lives in
the single Game scene. `SaveManager` captures HP from the player `Health` component;
`RunRestoreController.FindPlayerHealth()` restores it. With separate scenes the Shop
scene has no player — HP must move to `RunManager`.

---

## 7. Cross-scene coupling risks

- Shop overlay is **well-isolated** — no shop UI script holds an Inspector reference to a
  combat object. Lowest-risk part of the extraction.
- `SynergyService` / `InventoryService` are queried by combat code → must be persistent.
- `GroundManager.Current` static must null out on scene unload (already nulls in `OnDestroy`).
- `EventBus` subscriptions: every shop component must have matched `Subscribe`/`Unsubscribe`
  in `OnEnable`/`OnDisable` so scene unloads don't leak subscribers.
