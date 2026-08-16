---
name: add-entity
description: Add a new item, building, or ship to the mod end-to-end — EntityConfig, craft recipe, icon, model, technology unlock — then compile-verify and build. Use when asked to add a new entity, item, ship, building, assembler, or research/tech to the mod.
---

# Add a new entity to the mod

Everything happens in `Assets/Scripts/UserModLoader.cs`; the existing examples are the
templates. Pick the closest one and adapt:

| You're adding | Start from | Key configs |
|---|---|---|
| A ship / combat unit | `lothBat` | `FleetConfig` (speed, health, weapon, vision), `ItemCategory.Ship` |
| A production building | `lothAssembler` / `lothPrinter` | `AssemblerConfig`, `PlaceableConfig`, `PowerConfig`, `InventoryMetaDataConfig` |
| A variant of an existing entity | `gherikConnector` | Minimal config + clone the real entity's prefab/configs in `PostInitializationHook` |

## Steps

1. **Name**: add a `private const string` for it. The name is the player-facing name
   AND the config lookup key — pick it once, reuse the constant everywhere.
2. **EntityConfig** in `DefineEntityConfigs()`, added to the returned list. Craft
   recipe `ItemName`s and tech `Requirements` must be EXACT existing game item/tech
   names — a typo surfaces as a load error in the game, not a compile error here.
3. **Icon**: either drop a PNG in `Assets/Resources/Icons/` named exactly like the
   entity and set `IconAssetName` to it, or reuse a game icon by name.
4. **Model**: either a prefab in `Assets/Resources/ItemEntities/` (see `Loth Bat`) with
   `RenderingData.ModelPath` naming it, or reuse a game model by name
   (`ModelPath = "Assembler"`).
5. **Unlock**: add a `TechnologyConfig` in `AddTechnologies()` with the new entity in
   `ItemsUnlocked`, requirements on existing techs, and a research cost — or, for a
   from-the-start item, add it to an existing tech / leave it always unlocked.
6. **Config-only tweaks** to existing game entities belong in `PostInitializationHook()`
   instead (see the Bauxite and GlobalConfig examples there).

## Determinism check (before finishing)

Any numeric gameplay field that the simulation consumes should be integer or `fp` —
follow the existing configs' field types exactly (`BaseCraftTimeFP`, `HeatRateFP`,
`ValueFp` are `fp` for a reason). Never introduce `float` gameplay state.

## Verify

1. Compile-verify through the MCP bridge (CLAUDE.md ritual): `refresh_unity` → fresh
   domain reload → `read_console` zero `error CS`.
2. Run the `build-mod` skill (build + install).
3. Remind the user: runtime proof requires launching Final Factory itself — new item in
   the Mod Menu's mod, tech visible in research, item craftable and placeable.
