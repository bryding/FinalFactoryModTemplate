---
name: build-mod
description: Build the Final Factory mod (and optionally install it into the local game) with every step verified through the Unity MCP bridge. Use when asked to build, rebuild, install, or ship the mod, or to prove the mod still builds after a change.
---

# Build (and install) the mod

The whole flow runs through the Unity MCP bridge — never by watching files. If the
bridge is down, stop and tell the user (CLAUDE.md, "Unity Editor Interaction").

## 1. Preflight

1. Read `mcpforunity://instances`, pin the instance whose `path` is THIS project, via
   `set_active_instance`.
2. Editor must be idle in Edit mode (`mcpforunity://editor/state`: `play_mode.is_playing`
   false, `activity.phase` idle). Builds cannot run in play mode.
3. Game DLLs present? `Assets/FinalFactoryDlls/FFCore.dll` must exist. If missing, run
   the copy script (`./copy-finalfactory-dlls.sh` / `.cmd`) — and if
   `finalfactory.properties` is missing too, ask the user for their install path.
4. A `Preview.png` or `Preview.gif` under 1 MB must exist in the project root — the
   build hard-fails without it, so check first and report a friendly error instead.

## 2. Compile-verify (after any code change)

`refresh_unity` → poll `editor/state` until `compilation.is_compiling` is false and the
domain reload is newer than the edit → `read_console` filtered for `error CS`. Zero
errors = compiled. Fix errors before building.

## 3. Build

- Build only: `execute_menu_item` → `Modding/Build X64 Mod`
- Build + install into the local game: `execute_menu_item` → `Modding/Build and Install`

The build is synchronous but can take a minute; poll `editor/state` until idle, then
`read_console` for `BuildFailedException` / errors. The failure messages are
descriptive (missing preview, oversized preview, output folder locked).

## 4. Verify the output

Check `build/<ModID>/` (ModID from `Assets/Scripts/UserMod.cs`) contains:
`<ModID>.dll`, `manifest.properties`, `AssetBundle/`, and `Preview.png`/`.gif`.
A `<ModID>_win_x86_64.dll` appears when Burst code was generated.

If installing: confirm the same files landed in the game's mods folder —
Windows `%USERPROFILE%/AppData/LocalLow/Never Games/finalfactory/mods/<ModID>`,
macOS `~/Library/Application Support/Never Games/finalfactory/mods/<ModID>`.

## 5. Report honestly

A successful build proves compilation and packaging — NOT runtime behavior. Mod code
only runs inside the real game (never in this editor project), so end with: launch
Final Factory, check the Mod Menu lists the mod, then test in a game. Offer the exact
mod folder path so the user can double-check.
