# Content Audit — post WS-014.B (2026-05-16)

Snapshot of authored game content after WS-014.B. Counts are of `.asset` files under
`Assets/_Project/ScriptableObjects/`, registered in `DataManifest.asset`.

| Category | Count | Detail |
|----------|-------|--------|
| Heroes (`HeroData`) | 1 | `default_hero` (DefaultHero.asset) |
| Items (`ItemData`) | 10 | bone_knife, bottle_of_hush, hollow_crown, mystic_sword, sharpening_stone, tarnished_bell, tempo_stone, test_stick, veil_thread, whetstone |
| Bags (`BagData`) | 1 | `warden_pouch` (WardenPouch_Bag.asset) |
| Enemies (`EnemyData`) | 2 | `basic_enemy`; `boss_companion_devil_placeholder` (NEW — WS-014.B) |
| Bosses | 1 | `boss_companion_devil_placeholder` — an `EnemyData`, not a separate type. 800 HP, reuses `Enemy.prefab` scaled ×3 + tinted dark red at spawn. |
| Companion | 0 real | Placeholder system only — `CompanionAppearanceScheduler` + `CompanionPlaceholderUI`. Six emotional states (Composed/Concerned/Pleading/Glitched/Devil/Final) shown as a tinted rectangle + subtitle text. No real dialogue, art, or voice. |
| Biomes | 0 real | Placeholder only — `BiomeTransitionController` tints the camera background per biome band (Outskirts 1-10, Inner Town 11-20, Depths 21-30). No backdrop art. |

## WS-014.B additions

- 1 EnemyData (placeholder boss).
- Placeholder companion + biome systems (code, no content assets).
- Asset-folder infrastructure: 17 folders under `Art/`, `Audio/`, `Text/`, each with a
  `README.md` documenting what real launch content is needed (file counts, dimensions,
  formats, naming, priority). These READMEs are the actionable spec for real-content
  production — see `Assets/_Project/Art/Characters/Heroes/README.md` (canonical example).

## Notable gaps for real content (next phases)

- Heroes: 1 of a designed ~5 (per `Art/Characters/Heroes/README.md`).
- Enemies: 1 real of a designed 15-18.
- Bosses: 0 real (1 placeholder) of a designed ~6.
- All companion dialogue, biome art, hero/enemy/item/UI sprites, music, SFX, voice — not
  started; documented in the per-folder READMEs.
- No GDD exists; the READMEs cite GDD sections as forward references.
