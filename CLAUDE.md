# CLAUDE.md

## Project Overview

This is the **Final Factory Mod Template** — a Unity 6000.3 project used to build mods
for Final Factory. It is not the game itself: the game ships as prebuilt DLLs
(`FFCore`, `FFSystems`, `FFComponents`, `FFTechnology`, `FFNetcode`) that get copied
into `Assets/FinalFactoryDlls` and referenced by the mod assembly.

Read `README.md` first — it covers the DLL copy step, building, installing, and
uploading to the Steam Workshop. Mod-authoring API details are in `DOCUMENTATION.md`.

**The DLL copy must happen before the editor is first opened.** The project references
those assemblies; opening Unity without them produces a cascade of missing-reference
errors that outlive the fix until a restart.

## Unity Editor Interaction

> 🔌 **Interact with the Unity editor through the MCP bridge — ONLY the MCP bridge.**
> All editor interaction — entering/exiting play mode, querying editor/scene state,
> compile verification, capturing screenshots — goes through the Unity MCP bridge
> tools (see `Documentation/Unity-MCP-Setup.md`). **Never fall back to
> file-based channels** (trigger files, editor-log tailing, DLL-mtime watching) on your
> own initiative: they are slow and error-prone in many edge cases (e.g. a compile
> failure never updates the assembly file, so a file-watcher hangs forever). **If the
> MCP bridge is down, STOP and NOTIFY the user** — do not self-recover through side
> channels.

> 🤖 **Editor readiness is Claude's job — never ask the user to babysit imports,
> compiles, or editor restarts.** Verify and monitor readiness yourself:
>
> 0. **FAIL FAST — the bridge is the ONLY channel.** For ANY task that needs the live
>    editor, the *very first* action is: read `mcpforunity://instances`, find the
>    instance whose `path` is under THIS project's working directory, and
>    `set_active_instance` to pin it. **If the resource is empty, the MCP tools aren't
>    connected, or no instance matches this project's path — STOP and NOTIFY the user
>    IMMEDIATELY** and say so in chat, up front, in one line. Do not attempt
>    self-recovery through trigger files, log tails, port-file probes, or process
>    kills/restarts — the user decides how to restore the editor/bridge. A stray
>    running `Unity.exe` is NOT proof this project's editor is live (Unity Hub, another
>    Unity project, a batchmode build, or a stale process all show up too); only the
>    pinned MCP instance's `path` counts.
> 1. **Bridge up?** Read `mcpforunity://instances`. Non-empty → pin the instance and go.
> 2. **Editor busy importing/compiling?** Watch through the bridge, don't ask: poll the
>    `mcpforunity://editor/state` resource (`activity.phase`, `compilation.is_compiling`,
>    `assets.is_updating`) at a modest cadence until idle. The state snapshot can go
>    stale while the main thread is saturated (`staleness.is_stale`) — pair it with a
>    process-CPU check to distinguish "working hard" from "hung" before declaring either.
> 3. **Bridge down or never starts?** Report it and stop. Diagnostic to include: the
>    editor logs a `[UnityMcpStdioAutoStart]` line on every startup path (self-heal,
>    guidance, or explicit-HTTP), so a missing bridge always leaves a one-line
>    explanation for the USER to act on — typically selecting stdio in
>    `Window > MCP for Unity` and restarting the editor. Do not flip transport prefs or
>    kill/restart editors yourself.

> ⚠️ **Pin the instance whenever more than one editor is open.** Every connected Unity
> editor advertises its own MCP instance. Match on the instance `path` under this
> working directory; never hardcode or match a project *name*. An unpinned call can
> execute against — and report results from — the wrong editor.

> ⚠️ **A successful-looking result does NOT prove your code change compiled.** The
> editor will NOT recompile while it is in **play mode**, and if compilation **fails**
> it keeps the **last good assembly**. In either case anything you run executes against
> **stale code**. After EVERY code change, positively confirm the change recompiled and
> is live — **through the MCP bridge, not by watching files**:
> 1. **Ensure the editor is idle in Edit mode first.** Check `mcpforunity://editor/state`:
>    `play_mode.is_playing` false, `activity.phase` idle.
> 2. **Trigger and await the compile**: call `refresh_unity`, then poll `editor/state`
>    until `compilation.is_compiling` is false and `last_domain_reload_after_unix_ms` is
>    NEWER than your edit. (A "Connection closed" error from `refresh_unity` usually IS
>    the domain reload — poll state, don't retry blindly.)
> 3. **Check for compile errors**: `read_console` filtered for `error CS`. Zero entries
>    after a fresh domain reload = compiled. On failure the console entry contains the
>    exact file/line.
> 4. Where a **behavioral signal** is available, prefer confirming it too.
>
> **Bridge console caveat**: `read_console` reliably returns warnings/errors/exceptions
> but generally NOT plain `Debug.Log` entries — never treat "0 log entries" as proof a
> log-line marker didn't fire. For Log-level markers, use a state probe via
> `execute_code` instead.

## Build & Install

All flows are Unity editor menu items, implemented in `Assets/Editor/ScriptBatch.cs`:

- `Modding > Set Final Factory Path...` — picks the install folder, writes `finalfactory.properties`
- `Modding > Copy Final Factory DLLs` — re-copies the game DLLs (run after a game update)
- `Modding > Build X64 Mod` — builds into `<project root>/build`
- `Modding > Build and Install` — builds, then installs into
  `%USERPROFILE%/AppData/LocalLow/Never Games/finalfactory/mods`

Outside the editor, the same DLL copy is available as `copy-finalfactory-dlls.cmd`
(Windows) / `copy-finalfactory-dlls.sh` (Mac/Linux), driven by `finalfactory.properties`.
That file is gitignored — only `finalfactory.properties.template` is tracked, so local
install paths stay out of git.

Workshop upload happens in-game (Mod Menu → the blue `^` icon), not from Unity.

## Architecture

| Path | Purpose |
|------|---------|
| `Assets/Scripts/` | Mod code — the `FFMod` assembly (`FFMod.asmdef`) |
| `Assets/Scripts/UserMod.cs` | `IUserMod` implementation — name, description, author, version |
| `Assets/Scripts/UserModLoader.cs` | Optional `IUserModLoader` — entity configs + post-init hook |
| `Assets/Scripts/Systems/` | DOTS systems added by the mod |
| `Assets/Editor/` | Editor tooling — no asmdef, so it compiles into `Assembly-CSharp-Editor` |
| `Assets/FinalFactoryDlls/` | Game DLLs copied from the install (gitignored content) |

Constraints that mod code must respect:

- **A mod implements exactly one `IUserMod`.**
- **Deterministic**: use `fp` (fixed-point) from `Unity.Mathematics.FixedPoint` for
  simulation state — never `float`. Mod systems run inside the game's simulation, which
  must stay deterministic across peers; a `float` there is a desync.
- **ECS-first**: systems you add are auto-detected and registered when mods load.
- **Burst**: systems should be `[BurstCompile]`-compatible (unmanaged types only in jobs).

`DOCUMENTATION.md` is the reference for how to write these — it covers the ECS model,
the system groups mod systems slot into, fixed-vs-controller update timing, and entity
lifecycle/deletion rules.

## Documentation

- `README.md` — setup, build, install, workshop upload
- `DOCUMENTATION.md` — modding reference: ECS overview, system groups and update
  timing, entity lifecycle, configuration-vs-behavioral mods
- `Documentation/Unity-MCP-Setup.md` — MCP transport, registration, and the
  `UnityMcpStdioAutoStart` supervisor

## External Dependencies

Notable packages beyond standard Unity (see `Packages/manifest.json`):

- `com.unity.entities` — Unity ECS
- `com.unity.physics` — Unity Physics for ECS
- `com.unity.netcode.gameobjects` — multiplayer networking
- `com.nevergames.mathematics.fixedpoint` — fixed-point math for determinism
- `com.nevergames.steamworks.facepunch` — Steam integration
- `com.coplaydev.unity-mcp` — the MCP bridge

Keep these versions in sync with the game build you are targeting — a mod compiled
against a different Entities/Collections/Physics version than the game ships will fail
to load.
