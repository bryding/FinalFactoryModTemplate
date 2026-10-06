# CLAUDE.md

This file guides AI coding agents (Claude Code loads `CLAUDE.md`; other tools read the
identical `AGENTS.md`). Keep exactly one canonical document — `AGENTS.md` is a symlink.

## Project Overview

This is the **Final Factory Mod Template** — a Unity 6000.3 project used to build mods
for Final Factory. It is not the game: the game ships as prebuilt DLLs (`FFCore`,
`FFSystems`, `FFComponents`, `FFTechnology`, `FFNetcode`, `FFSpaghetti`) that get copied into
`Assets/FinalFactoryDlls/` and referenced by the mod assemblies. You do not have the game's
source, and you do not need it: `Documentation/API/` is the reference for everything a mod can
use (see "Finding your way in the game's API").

Read `README.md` first (setup, build, install, workshop). `DOCUMENTATION.md` is the
modding reference: system groups and timing, determinism rules, entity lifecycle, the
mod load pipeline, and testing.

Four facts that shape every task here:

1. **The DLL copy must happen before the editor is first opened.** The project
   references those assemblies; without them the first open produces a cascade of
   missing-reference errors. `./copy-finalfactory-dlls.sh` / `.cmd` (driven by the
   gitignored `finalfactory.properties`) does the copy; after a game update, re-run it.
2. **Mod code never runs in THIS editor.** Mod systems are registered only in real
   player builds of the game (the game's `ModLoader` skips registration in editors).
   Play mode here proves nothing about mod behavior. The only runtime test is:
   `Modding > Build and Install`, then launch Final Factory itself. What CAN be
   verified here: compilation, and the build pipeline producing a valid mod folder.
3. **The DLL `.meta` files under `Assets/FinalFactoryDlls/` are tracked and pin
   Auto Reference OFF. Never delete or regenerate them.** The game ships a
   global-namespace `Debug` type in FFCore; if the game DLLs auto-reference into every
   assembly, Unity packages (netcode, shadergraph, burst) fail with hundreds of
   `CS0576` alias-conflict errors. The mod assemblies reference the DLLs explicitly via
   `precompiledReferences` in `FFMod.asmdef` / `FFMod.Editor.asmdef` — a new game DLL
   must be added there, with a matching Auto-Reference-off meta (and, like
   `FFSpaghetti.dll.meta`, `validateReferences: 0` if its own dependencies are not in this project).
4. **Final Factory multiplayer is deterministic lockstep.** Every peer runs the whole
   simulation, and a mod's systems run on every peer that has the mod. Code that changes the
   game world must give the same result on every peer, and a player's action must reach the
   world as a network operation, never as a direct write. The rules are below; break them and
   the mod desyncs every multiplayer game it is in.

## Fast checks without the editor

Three scripts give an agent a quick loop with no editor open (Unity 6000.3.19f1 must be installed,
the game DLLs copied, and `finalfactory.properties` set; on Windows run them from Git Bash):

| Script | What it does |
|---|---|
| `Tools/compile-check.sh` | Compiles `Assets/` (all but Editor folders) with Unity's own C# compiler and the Entities source generators, against the game DLLs and the package assemblies `FFMod.asmdef` references. "OK" or the compiler errors, in seconds. Run it after every edit. |
| `Tools/check-mp-safety.sh` | Scans `Assets/Scripts` for code that breaks multiplayer determinism (input or frame time in simulation, per-peer randomness, `DestroyEntity`, UI writing the world, ...). ERROR = almost certainly a desync. |
| `Tools/generate-api-reference.sh` | Regenerates `Documentation/API/` from your copied DLLs after a game update. |

They prove compilation and catch common mistakes. They do not prove behaviour, Burst compilation
or the asset bundle: build in Unity and test in the game for those (below).

## The mod API (what a mod is)

- Exactly one `IUserMod` per mod (`Assets/Scripts/UserMod.cs`): `ID` (letters, digits,
  underscores — no spaces; it's the folder + workshop identity), `FullName`,
  `Description`, `Author`, `EmailContact`, `Website`, `Dependencies` (other mod IDs),
  `ModVersion` (`FFVersion`).
- Optionally one `IUserModLoader` (`Assets/Scripts/UserModLoader.cs`). The game calls, in order:
  `DefineEntityConfigs()` (new items/ships/buildings) → `AddTechnologies()` →
  `PostInitializationHook()` (once at startup, on every peer: edit any config or prefab, register
  player actions) → `OnGameStart(Canvas)` (each new or loaded game: build UI).
- ECS systems in the mod assembly are auto-discovered at load — no registration.
- `SpaghettiApi.Instance` gives the UI's `SelectedEntity` and `HoveredEntity`.
- Icons live in `Assets/Resources/Icons/`, entity prefabs in
  `Assets/Resources/ItemEntities/`; the build packs them into the mod's AssetBundle.
  - `IconAssetName` must name an icon in YOUR bundle: the game looks icons up only in the
    mod's own icon bundle.
  - `RenderingData.ModelPath` naming one of your prefabs uses its model. Naming an existing game
    item instead (e.g. `"Connector"`) makes your item a copy of that item's whole entity, its
    behaviour components included, not just its look.

## Finding your way in the game's API

`Documentation/API/` is generated from the game's assemblies: every public type a mod can use, with
C# signatures and the first paragraph of the game's own doc comments.

- `Documentation/API/all-members.txt` has one line per member (`Namespace.Type :: signature`).
  Grep it first: `grep -i "ItemConfig ::" Documentation/API/all-members.txt`,
  `grep "FFCore.Extensions.Ecs ::"`, `grep -i "health"`.
- `Documentation/API/INDEX.md` lists the namespaces; each `<Namespace>.md` has its types with members
  and summaries. Components (`IComponentData`) are mostly in `FFComponents.*`, configs in
  `FFCore.Config*`, systems (for `UpdateBefore`/`UpdateAfter`) in `FFSystems.*`.
- Never guess an API: a wrong name is a compile error at best and a silent mistake at worst. If the
  reference does not have it, it is not public, and a mod cannot call it.
- After a game update, run `Tools/generate-api-reference.sh` so the reference matches your DLLs.

## Multiplayer and determinism

Every peer runs the simulation and must reach bit-identical state on every heartbeat (16 per second,
in single player too). A mod's systems run inside that simulation on every peer. In this list,
"simulation" means anything that changes the game world: positions, health, inventories, research,
components the game reads.

1. **Simulation runs on the heartbeat.** Put it in a Fixed group (`FFFixedPreTransformGroup` is the
   usual home; `FFFixedEarlyGroup`, `FFFixedPostTransformGroup`, ...). Controller groups
   (`FFController*Group`) run every rendered frame, at a different rate on every machine:
   presentation only (UI, range rings, colours, effects).
2. **Time is the heartbeat's.** `SystemAPI.GetSingleton<FFTimeData>().deltaTime` (fixed point) and
   `SimulationElapsedTime` (on `FinalFactorySystemBase`). Never `SystemAPI.Time`, `World.Time`,
   `UnityEngine.Time`, or `ElapsedGameTime` / `WallClockElapsedTime` (wall clock) in simulation.
3. **No local inputs in simulation.** No `Input`, mouse, camera, hover or selection, and no
   `MePlayer` (a different player on every peer). Player actions go through network operations
   (next section).
4. **Decide in fixed point.** Ranges, nearest-target picks, damage, cooldowns, rates: `fp` and
   `fpmath` (`Unity.Mathematics.FixedPoint`). Motion may stay float where the game's own motion is
   float (`LocalTransform`, `LinearMotion`); avoid `sin`/`cos`/`log` on floats, whose results can
   differ between CPUs. `fpmath.log10` is not implemented (it is `[Obsolete]`): use
   `fpmath.log2(x) / fpmath.log2((fp)10)`.
5. **Randomness from stable keys.** `RandomSystem.GetRandomForTileAndSimulationTime(seed, tile, time)`
   for buildings, `GetRandomForStableHashAndSimulationTime(seed, hash, time)` with a ship's or
   player's `DeterministicCombatObjectId`. Never `RandomSystem.GetRandomForEntity` (it seeds from the
   Entity handle, which differs between peers), never `UnityEngine.Random`.
6. **Never let query order decide.** Chunk order differs between peers. When first, nearest or the
   last writer matters, sort by a key every peer agrees on (a building's `Placeable.CenterTile`) and
   break ties with it. Use `Schedule`, not `ScheduleParallel`, for jobs that add into shared totals.
7. **Read simulation positions.** In Fixed systems use `LocalTransform` or the building's tile
   (`Placeable.CenterTile`, `FpWorldPositionBasedOnGridTile`), never `LocalToWorld`.
8. **Delete with `DeletionMarker`**, never `DestroyEntity`.
9. **Know what is saved; a join is a save load.** A player who joins gets the host's save. Only
   buildings, ships, players and a few other root entities are saved, and on them only `[Save]`
   components; everything else comes back from the prefab when the save loads. So:
   - running state the simulation depends on must be `[Save]` on a saved entity;
   - a value the game does not save but derives (KNN vision ranges, for one) must be recomputed every
     heartbeat from saved state, early enough that the first heartbeat after a load or join agrees;
   - never change the fields of a `[Save]` struct once the mod has shipped: the game refuses the
     whole save. Marking a type `[Save]` for the first time is safe (old saves load it with the
     prefab's value).
10. **Don't change which game components an entity carries outside the heartbeat.** The game compares
    the set of game component types (namespaces `FF*`) of every entity across peers. Adding or
    removing a game component (`RotationParameters`, markers, ...) belongs in a Fixed group, and never
    on unsaved child entities (meshes), which a joining client rebuilds from the prefab. Components in
    your mod's own namespace are not compared.
11. **Ordering**: `UpdateBefore`/`UpdateAfter` are fine. Avoid `OrderFirst`/`OrderLast`; the one
    exception is running before a game system that is itself `OrderFirst` (the game's `KnnSystem` in
    `FFFixedEarlyGroup` refreshes vision first thing on the heartbeat), and then say why in a comment.
12. **Burst**: keep systems `[BurstCompile]`-compatible (unmanaged types in jobs).

`Tools/check-mp-safety.sh` catches the common breaks; it is no substitute for reading this list.

## Player actions (network operations)

A click must not change the world directly: send it as a network operation, which the host checks and
every peer applies on the same heartbeat (single player goes through the same path). The game has no
dedicated mod API for this yet; the route that works today is its `SetStructureSetting` operation with a
setting kind of your own. `Assets/Scripts/Examples/RepairBeacon/RepairBeaconActions.cs` is the
worked example:

- In `PostInitializationHook` (runs on every peer), register an applier:
  `StructureSettingAppliers.Register((StructureSettingKind)N, new StructureSettingApplier { Validate, Apply })`.
- From UI, send: `StructureSettingsDispatch.DispatchSetting(kind, buildingEntity, payload)`. The payload
  is any `[Serializable]` class (it travels as JSON).
- `Validate` and `Apply` run on every peer and must be deterministic (read only the target's components
  and the payload) and must never throw. The target is found by grid tile, so it must be a building
  (`Placeable`); `Apply` may also change other entities, deterministically.
- The UI only reads; it shows the new state about one heartbeat after the click.
- Pick a kind block of your own: the game's kinds are small numbers (0-16 today); the example uses
  47001. Known mod blocks: 48200-48499 (Gherik's mods).
- These types live in `FFSpaghetti.dll`, which is outside the documented mod API: a game update can
  change them. `Documentation/API/NetworkOperations.Settings.md` has their signatures.
- The applier is not told which player sent the action, so per-player state is not possible this way.

Note: the game currently turns multiplayer off while any mod is enabled. Following these rules is what
lets a mod work in multiplayer once the game allows mods there; it also keeps single player correct (the
simulation runs at 16 heartbeats a second regardless of frame rate).

## Unity Editor Interaction

> 🔌 **Interact with the Unity editor through the MCP bridge — ONLY the MCP bridge.**
> All editor interaction — entering/exiting play mode, querying editor/scene state,
> compile verification, running menu items, capturing screenshots — goes through the
> Unity MCP bridge tools (see `Documentation/Unity-MCP-Setup.md`). **Never fall back to
> file-based channels** (trigger files, editor-log tailing, DLL-mtime watching) on your
> own initiative: they are slow and error-prone in many edge cases (e.g. a compile
> failure never updates the assembly file, so a file-watcher hangs forever). **If the
> MCP bridge is down, STOP and NOTIFY the user** — do not self-recover through side
> channels.

> 🤖 **Editor readiness is the agent's job — never ask the user to babysit imports,
> compiles, or editor restarts.** Verify and monitor readiness yourself:
>
> 0. **FAIL FAST — the bridge is the ONLY channel.** For ANY task that needs the live
>    editor, the *very first* action is: read `mcpforunity://instances`, find the
>    instance whose `path` is under THIS project's working directory, and
>    `set_active_instance` to pin it. **If the resource is empty, the MCP tools aren't
>    connected, or no instance matches this project's path — STOP and NOTIFY the user
>    IMMEDIATELY** in one line. Do not attempt self-recovery through trigger files, log
>    tails, port-file probes, or process kills/restarts — the user decides how to
>    restore the editor/bridge. A stray running Unity process is NOT proof this
>    project's editor is live; only the pinned MCP instance's `path` counts.
> 1. **Bridge up?** Read `mcpforunity://instances`. Non-empty → pin the instance and go.
> 2. **Editor busy importing/compiling?** Watch through the bridge, don't ask: poll the
>    `mcpforunity://editor/state` resource (`activity.phase`, `compilation.is_compiling`,
>    `assets.is_updating`) at a modest cadence until idle. The state snapshot can go
>    stale while the main thread is saturated (`staleness.is_stale`) — pair it with a
>    process-CPU check to distinguish "working hard" from "hung" before declaring either.
> 3. **Bridge down or never starts?** Report it and stop. The editor logs a
>    `[UnityMcpStdioAutoStart]` line on every startup path, so a missing bridge always
>    leaves a one-line explanation for the USER to act on — typically selecting stdio in
>    `Window > MCP for Unity` and restarting the editor.

> ⚠️ **A successful-looking result does NOT prove your code change compiled.** The
> editor will NOT recompile in **play mode**, and a **failed** compile keeps the last
> good assembly — either way you'd be running stale code. After EVERY code change,
> positively confirm the change compiled and is live, through the bridge:
> 1. Editor idle in Edit mode (`play_mode.is_playing` false, `activity.phase` idle).
> 2. `refresh_unity`, then poll `editor/state` until `compilation.is_compiling` is
>    false and `last_domain_reload_after_unix_ms` is NEWER than your edit. (A
>    "Connection closed" from `refresh_unity` usually IS the domain reload — poll
>    state, don't retry blindly.)
> 3. `read_console` filtered for `error CS`. Zero entries after a fresh reload =
>    compiled; a failure names the exact file/line.
>
> `read_console` reliably returns warnings/errors but generally NOT plain `Debug.Log`
> entries — never treat "0 log entries" as proof a Log marker didn't fire; use an
> `execute_code` state probe instead.

**Headless alternatives** (no editor open, e.g. CI or a fresh clone): `Tools/compile-check.sh`
(seconds; see "Fast checks without the editor"), or a full batchmode import, which also
compiles the Editor assembly —
`<Unity editor binary> -batchmode -quit -projectPath <repo> -logFile <log>`; exit code
0 and zero `error CS` lines in the log = the project compiles.

## Build & Install

All flows are Unity editor menu items, implemented in `Assets/Editor/ScriptBatch.cs`
(agent: run them via the bridge's `execute_menu_item`):

- `Modding > Set Final Factory Path...` — writes `finalfactory.properties`
- `Modding > Copy Final Factory DLLs` — re-copies the game DLLs (after a game update)
- `Modding > Build X64 Mod` — builds `<project root>/build/<ModID>/` (managed DLL,
  Burst DLL, AssetBundle, `manifest.properties`, preview image)
- `Modding > Build and Install` — same, then installs into the game's mods folder
  (Windows `%USERPROFILE%/AppData/LocalLow/Never Games/finalfactory/mods`,
  macOS `~/Library/Application Support/Never Games/finalfactory/mods`)

The build requires a `Preview.png` or `Preview.gif` (< 1 MB) in the project root and
fails with a descriptive error otherwise. Workshop upload happens in-game (Mod Menu →
blue `^` icon), not from Unity.

`.claude/skills/` contains ready-made workflows:

| Skill | Use it to |
|---|---|
| `build-mod` | build + install + verify through the bridge |
| `add-entity` | add a new item, building or ship end-to-end |
| `add-player-action` | let players change something through a network operation, with its UI |
| `check-mp-safety` | review code for multiplayer determinism before you call it done |
| `api-lookup` | find the right game type or method in `Documentation/API` |

## Architecture

| Path | Purpose |
|------|---------|
| `Assets/Scripts/` | Mod code — the `FFMod` assembly (`FFMod.asmdef`) |
| `Assets/Scripts/UserMod.cs` | `IUserMod` — mod identity |
| `Assets/Scripts/UserModLoader.cs` | `IUserModLoader` — entity configs, tech, config edits |
| `Assets/Scripts/Systems/` | DOTS systems added by the mod |
| `Assets/Scripts/Examples/RepairBeacon/` | The multiplayer-safe example: a `[Save]` component, a heartbeat system, a player action, a code-built UI panel |
| `Documentation/API/` | Generated reference of the game's public mod-facing API (`README.md` there explains it) |
| `Tools/` | `compile-check.sh`, `check-mp-safety.sh`, `generate-api-reference.sh` and the generator's source |
| `Assets/Editor/` | Editor tooling — the `FFMod.Editor` assembly (build menu, MCP autostart) |
| `Assets/FinalFactoryDlls/` | Game DLLs (gitignored) + tracked Auto-Reference-off `.meta`s |
| `Assets/Resources/Icons`, `.../ItemEntities` | Mod icons and entity prefabs → AssetBundle |

## External Dependencies

Notable packages (see `Packages/manifest.json`): `com.unity.entities` (+graphics),
`com.unity.physics`, `com.unity.netcode.gameobjects`,
`com.nevergames.mathematics.fixedpoint` (`fp` determinism math),
`com.nevergames.steamworks.facepunch`, `com.coplaydev.unity-mcp` (the MCP bridge).

**Package versions must match the game build being targeted** — a mod compiled against
a different Entities version than the game ships is rejected at load. The template
tracks the game's released versions; do not bump packages independently.

## Pitfalls that have bitten mods

- A system in a Controller group that changes the world (inventories, health, positions, spawning):
  it runs at frame rate on each machine. Move it to a Fixed group.
- UI code (`MonoBehaviour.Update`) calling `Ecs.SetComponent` on a building: the change exists on
  that machine only. Send a player action.
- Rewarding `MePlayer`: a different player on every peer. Research from buildings goes to the
  `HostPlayer` in the game; lumin orbs go to every player.
- Doubling a value once (a vision range) that the game does not save: after a load or join it is
  back to normal. Recompute derived values every heartbeat.
- `entity != null` on an `Entity` (a struct): always true. Compare with `Entity.Null`.
- `GetComponentLookup<T>(true)` (read-only) and then writing through it: throws when Unity's safety
  checks are on.
- `RangeIndicatorData` (range rings) is retired by the game; nothing sets it. Size your ring
  yourself in a presentation system (`HoverSelectionState` says what the player hovers or selects).
- Naming a game icon in `IconAssetName`: icons come only from your own bundle.
