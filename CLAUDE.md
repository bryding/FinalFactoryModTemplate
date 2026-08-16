# CLAUDE.md

This file guides AI coding agents (Claude Code loads `CLAUDE.md`; other tools read the
identical `AGENTS.md`). Keep exactly one canonical document — `AGENTS.md` is a symlink.

## Project Overview

This is the **Final Factory Mod Template** — a Unity 6000.3 project used to build mods
for Final Factory. It is not the game: the game ships as prebuilt DLLs (`FFCore`,
`FFSystems`, `FFComponents`, `FFTechnology`, `FFNetcode`) that get copied into
`Assets/FinalFactoryDlls/` and referenced by the mod assemblies.

Read `README.md` first (setup, build, install, workshop). `DOCUMENTATION.md` is the
modding reference: system groups and timing, determinism rules, entity lifecycle, the
mod load pipeline, and testing.

Three facts that shape every task here:

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
   (e.g. a sixth assembly) must be added there, with a matching Auto-Reference-off meta.

## The mod API (what a mod is)

- Exactly one `IUserMod` per mod (`Assets/Scripts/UserMod.cs`): `ID` (letters, digits,
  underscores — no spaces; it's the folder + workshop identity), `FullName`,
  `Description`, `Author`, `EmailContact`, `Website`, `Dependencies` (other mod IDs),
  `ModVersion` (`FFVersion`).
- Optionally one `IUserModLoader` (`Assets/Scripts/UserModLoader.cs`):
  `DefineEntityConfigs()` (new items/ships/buildings), `AddTechnologies()`,
  `PostInitializationHook()` (edit any existing config), `OnGameStart(Canvas)`.
- ECS systems in the mod assembly are auto-discovered at load — no registration.
- Icons live in `Assets/Resources/Icons/`, entity prefabs in
  `Assets/Resources/ItemEntities/`; the build packs them into the mod's AssetBundle.
  Referencing an existing game model/icon by name (e.g. `ModelPath = "Assembler"`)
  reuses the game's asset instead.

## Constraints mod code must respect

- **Determinism** (multiplayer is deterministic lockstep; a mod that breaks it desyncs
  every session it's in — full rules in `DOCUMENTATION.md`):
  - `fp` (`Unity.Mathematics.FixedPoint`) for simulation state, never `float`.
  - Simulation state changes only in **Fixed** groups (`FFFixedPreTransformGroup` is
    the default home). Controller groups are render-rate → presentation only.
  - No wall-clock, frame-rate, or `UnityEngine.Random` inputs to simulation. Use
    `RandomSystem.GetRandomForEntity(...)` as `FleetRandomMovementSystem.cs` does.
- **Deletion**: add `DeletionMarker`, never `DestroyEntity`; queries generally exclude
  marked entities with `.WithNone<DeletionMarker>()`.
- **Ordering**: `UpdateBefore`/`UpdateAfter` are fine; `OrderFirst`/`OrderLast` are
  forbidden for mod systems.
- **Burst**: keep systems `[BurstCompile]`-compatible (unmanaged types in jobs).

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

**Headless alternative** (no editor open, e.g. CI or a fresh clone): a batchmode
import compiles everything —
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

`.claude/skills/` contains ready-made workflows: `build-mod` (build + install + verify
through the bridge) and `add-entity` (add a new item/building/ship end-to-end).

## Architecture

| Path | Purpose |
|------|---------|
| `Assets/Scripts/` | Mod code — the `FFMod` assembly (`FFMod.asmdef`) |
| `Assets/Scripts/UserMod.cs` | `IUserMod` — mod identity |
| `Assets/Scripts/UserModLoader.cs` | `IUserModLoader` — entity configs, tech, config edits |
| `Assets/Scripts/Systems/` | DOTS systems added by the mod |
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
