# Final Factory Mod Template

A working, buildable example mod for [Final Factory](https://store.steampowered.com/app/1383150/Final_Factory/), meant to be used as the starting point for your own mods. It adds three items (a combat ship, an assembler, and a printer), a technology branch to unlock them, a couple of config tweaks, and one custom ECS system — so every kind of mod you might write has an example to crib from.

Questions? Ask in the [Final Factory Discord](https://discord.gg/finalfactory) `#modding` channel — we're happy to help, and we often share game source snippets to unblock modders.

## Requirements

* Unity **6000.3.19f1** (the exact version is recorded in `ProjectSettings/ProjectVersion.txt`; Unity Hub will offer to install it when you open the project)
* A Final Factory install (Steam, or a local build)

## Getting started

1. **Clone the repo** (use `git clone` — downloading the zip does not work because of Git LFS).
2. **Copy the game DLLs into the project — before opening it in Unity.** The project references the game's assemblies, so they must exist the first time the editor opens:
   * Rename `finalfactory.properties.template` to `finalfactory.properties`, then edit it and set `FinalFactoryDir` to your Final Factory install folder (the one containing `finalfactory_Data` on Windows, or `finalfactory.app` on macOS). Use forward slashes; they work on Windows too.
     * Steam example (Windows): `FinalFactoryDir=C:/Program Files (x86)/Steam/steamapps/common/FinalFactory`
     * Steam example (macOS): `FinalFactoryDir=/Users/<you>/Library/Application Support/Steam/steamapps/common/FinalFactory`
     * If you build the game locally, point it at your build output instead.
   * Run the copy script from the project root:
     * Windows: `copy-finalfactory-dlls.cmd` (double-click it, or run it from a terminal)
     * Mac / Linux: `./copy-finalfactory-dlls.sh`
   * This copies `FFCore.dll`, `FFSystems.dll`, `FFComponents.dll`, `FFTechnology.dll`, `FFNetcode.dll` and `FFSpaghetti.dll` into `Assets/FinalFactoryDlls/`. Your `finalfactory.properties` is gitignored, so your local path stays out of git. **When the game updates, re-run the script** and rebuild your mod.
3. **Open the project** with Unity Hub (it will prompt to install the exact editor version if you don't have it).
4. **Add a Steam Workshop preview image**: put a `Preview.png` or `Preview.gif` (under 1 MB — Steam's limit) in the project root. The build fails with a clear error if it's missing or too big.

> NOTE
> Once the project is open, the DLL steps are also available as menu items: `Modding > Set Final Factory Path...` and `Modding > Copy Final Factory DLLs`. They're a convenience for later — the *first* copy must happen before the editor ever opens.

> WARNING
> Known issue: on first open you may see "assembly failed to load" errors (e.g. `Unity.Netcode.Runtime`). Clear the errors and restart Unity once; they don't come back.

## Build, install, test

1. `Modding > Build X64 Mod` builds into `<project root>/build/<YourModID>/` — the managed DLL, the Burst native DLL, an AssetBundle with your prefabs/icons, `manifest.properties`, and your preview image.
2. `Modding > Build and Install` additionally copies that folder into the game's mod directory:
   * Windows: `%USERPROFILE%\AppData\LocalLow\Never Games\finalfactory\mods\<YourModID>`
   * macOS: `~/Library/Application Support/Never Games/finalfactory/mods/<YourModID>`
3. Start Final Factory. Your mod loads at startup — check the in-game **Mod Menu** to confirm it's listed and enabled.

**Important: mod code only runs inside the real game.** Pressing Play in *this* Unity project does not load your mod — the template project is a build environment, not a game host. The loop is always: build → install → launch Final Factory.

## Uploading to the Steam Workshop

1. Start Final Factory with your mod installed.
2. Open the Mod Menu — your mod has a blue `^` icon next to it.
3. Click it to upload. The workshop listing can take a few minutes to appear; once it's fully published, refreshing the screen removes the `^` icon.
4. To upload an update: increment your mod version in `UserMod.cs`, rebuild, reinstall, and the upload icon reappears.

## The mod API

Every mod implements exactly one `IUserMod` — the mod's identity (see `Assets/Scripts/UserMod.cs`):

```csharp
string ID { get; }              // folder + workshop identity: letters/digits/underscore only, NO spaces
string FullName { get; }
string Description { get; }
string Author { get; }
string EmailContact { get; }
string Website { get; }
string[] Dependencies { get; }  // IDs of mods yours requires (empty array if none)
FFVersion ModVersion { get; }   // your mod's version, e.g. new(1, 0, 20, 0)
```

Optionally, implement `IUserModLoader` to actually change the game (see `Assets/Scripts/UserModLoader.cs`):

```csharp
List<EntityConfig> DefineEntityConfigs(); // add new items/buildings/ships (or return an empty list)
List<TechnologyConfig> AddTechnologies(); // add research that unlocks your items
void PostInitializationHook();            // after all config + systems load: tweak ANY config in the game
void OnGameStart(Canvas inGameUiCanvas);  // when a new/loaded game starts: hook UI, late setup
```

### What the examples show

| File | Demonstrates |
|---|---|
| `Assets/Scripts/UserMod.cs` | The identity boilerplate |
| `Assets/Scripts/UserModLoader.cs` | New ship/assembler/printer configs with recipes; cloning an existing entity (the Gherik Connector); editing existing item, terrain, and global config; adding technologies |
| `Assets/Scripts/Systems/FleetRandomMovementSystem.cs` | A Burst-compiled ECS system with a job that changes the simulation safely for multiplayer: on the heartbeat, ordered after the game's own system, with randomness every peer agrees on |
| `Assets/Scripts/Examples/RepairBeacon/` | A complete multiplayer-safe feature: a saved component on the Loth Printer, a heartbeat system that repairs nearby buildings, a player action (network operation) that switches it on and off, and a UI panel built in code |
| `Assets/Scripts/Utils/ConfigUtils.cs` | A small helper for editing accepted-ship lists |
| `Assets/Resources/` | How icons (`Icons/`) and entity prefabs (`ItemEntities/`) get into your mod's AssetBundle |

New systems you write are auto-detected when the game loads your mod — no registration call needed. For how Final Factory's systems, groups, and update timing work (and the multiplayer determinism rules your systems must follow), read **`DOCUMENTATION.md`**.

## Multiplayer & determinism (read before writing a system)

Final Factory multiplayer runs the simulation in deterministic lockstep on every peer. A mod system that touches factory/simulation state must follow the same rules the game's own systems do, or it will desync multiplayer sessions:

* Put simulation logic in **Fixed** groups (`FFFixedPreTransformGroup` is the usual home), with the heartbeat's time (`FFTimeData.deltaTime`); Controller groups are for presentation only.
* Make decisions in `fp` fixed-point math (`Unity.Mathematics.FixedPoint`), and never let the order entities come out of a query decide anything.
* Never let input, the local player, wall-clock time, frame rate or `UnityEngine.Random` reach simulation state. Seed randomness from stable keys (see `FleetRandomMovementSystem.cs`).
* Send player actions as network operations; UI only reads (see `Assets/Scripts/Examples/RepairBeacon/`).
* Delete entities by adding `DeletionMarker`, never `DestroyEntity`.

`CLAUDE.md` has the full rules with the reasons, and `Tools/check-mp-safety.sh` scans your code for the common breaks. The game currently turns multiplayer off while any mod is enabled; following these rules is what lets your mod work there once it doesn't, and keeps single player correct meanwhile.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Mod doesn't appear in the Mod Menu | Folder name must equal your mod `ID` exactly, and the folder needs `<ID>.dll` + `manifest.properties`. `Build and Install` gets this right — manual copies often don't. |
| "Invalid mod ID" | `ID` may only contain letters, digits, and underscores — no spaces. |
| Mod rejected: incompatible game version | Rebuild against the current game's DLLs: re-run the copy script, then rebuild. |
| Mod rejected: no (or multiple) `IUserMod` | Your assembly must contain exactly one `IUserMod` implementation. |
| Burst DLL ignored in-game | The native Burst DLL is exact-game-version locked; the game silently falls back to managed code after an update until you rebuild. |
| Hundreds of `CS0576`/`Debug` errors in a Unity package | The game DLLs got re-imported with **Auto Reference** enabled. The tracked `.meta` files under `Assets/FinalFactoryDlls/` pin it off — don't delete or regenerate them. |
| Game update broke your mod | Re-run the DLL copy script, fix compile errors, rebuild, reinstall. |

## Modding with AI tools

You don't have the game's source code, and an AI coding agent (Claude Code, Codex, Cursor, ...) doesn't either. This template gives it the subset it needs:

* **`CLAUDE.md`** (also read as `AGENTS.md`): how to build, check and test a mod, the mod API, and the multiplayer rules, written for an agent to follow.
* **`Documentation/API/`**: a reference of the game's public, mod-facing API (types, members, signatures and the game's own doc comments), generated from the game's DLLs. `all-members.txt` is one line per member, made for grep. Regenerate it after a game update with `Tools/generate-api-reference.sh`.
* **Checks without opening Unity**: `Tools/compile-check.sh` compiles your mod in seconds with Unity's own compiler, and `Tools/check-mp-safety.sh` flags code that would break multiplayer. An agent can run both after every edit (on Windows, from Git Bash).
* **Worked examples** that compile: new items, a building and a ship (`UserModLoader.cs`), a multiplayer-safe simulation system, and a complete feature with a player action and a UI panel (`Examples/RepairBeacon/`).
* **Skills** in `.claude/skills/`: `build-mod`, `add-entity`, `add-player-action`, `check-mp-safety`, `api-lookup`.
* The [MCP for Unity](https://github.com/CoplayDev/unity-mcp) bridge, so an agent can drive the editor (compile, run the build menu, read the console).

A good first prompt: *"Read CLAUDE.md. Add a new building that ... ; run Tools/compile-check.sh and Tools/check-mp-safety.sh until both pass, then build and install the mod."* What an agent cannot check for you is how the mod plays: start Final Factory and try it.

## Learning DOTS

Writing behavioral mods needs Unity DOTS (ECS) knowledge. Good starting points:

* [Entities Manual](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/index.html)
* [Turbo Makes Games](https://www.youtube.com/c/TurboMakesGames)
* [WAYNGames](https://www.youtube.com/@WAYNGames)
* [Unity Discord](https://discord.gg/unity) — `#dots-forum`
* [Final Factory Discord](https://discord.gg/finalfactory) — `#modding`

## Reverse engineering

Final Factory deliberately ships without a restrictive EULA clause against decompiling for mod development — we encourage it. Please don't use our sources, assets, or IP for competing or commercial products; otherwise, dig in, and reach out in Discord if you get stuck.
