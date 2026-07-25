# Unity MCP Setup

This note records how Unity MCP is configured for this repo so Claude Code can
drive the Unity editor.

## Baseline

- Unity `6000.3` (source of truth: `ProjectSettings/ProjectVersion.txt`)
- Unity MCP package: `com.coplaydev.unity-mcp` v10.0.0
  (`https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0`,
  registered in `Packages/manifest.json` — that file is the source of truth for the
  version, this note may lag it)
- Prerequisites on the dev machine: `uv`/`uvx` and the `claude` CLI

## Transport = stdio

Claude Code spawns the MCP server as its own child process; the server connects
into the Unity editor's stdio bridge, which listens on a **project-scoped**
loopback port (default `6400`). stdio is chosen over HTTP because:

- it supports running multiple Claude workspaces + Unity editors at once without
  port cross-talk (HTTP's URL lives in global per-user EditorPrefs → shared 8080)
- the server is Claude's child, so it dies with the session (no shutdown hook)
- no shared listening port → no unauthenticated-port exposure

The editor advertises its bridge in
`~/.unity-mcp/unity-mcp-status-<shortHash>.json`, reporting `unity_port`, `reason`,
`last_heartbeat`, and `project_path`. The `<shortHash>` is derived from the project
path, so it differs per working copy — do NOT hardcode it. Find your copy's file by
matching its `project_path` to this project's `Assets` folder. A fresh
`last_heartbeat` means the editor-side bridge is up.

## One-time registration (Claude Code, local/project scope)

```
claude mcp add --transport stdio UnityMCP -- \
  "<path>/uvx.exe" --from "mcpforunityserver==10.0.0" mcp-for-unity
```

(Pin the `mcpforunityserver` version to match the editor package in
`Packages/manifest.json`. Verify the live registration with `claude mcp get UnityMCP`.)

- `claude mcp get UnityMCP` reporting "Connected" only confirms the server binary
  speaks MCP — it does **not** confirm the editor is attached.
- Tools load only at Claude startup; restart `claude` in the workspace after
  registering.

## Per-editor step (supervised, automatic)

`Assets/Editor/UnityMcpStdioAutoStart.cs` (`[InitializeOnLoad]`) explicitly calls
`StdioBridgeHost.StartAutoConnect()` once the editor is idle, so the bridge comes
up on every editor launch without touching `Window > MCP for Unity`.

It **supervises** the bridge rather than starting it once: it stays subscribed to
`EditorApplication.update` and polls `StdioBridgeHost.IsRunning` (throttled — most
frames are just a timestamp compare; real checks run ~every 5s when healthy, ~every
3s while reconnecting), so if the session drops mid-run — without a domain reload to
re-run `[InitializeOnLoad]` — it restarts it, within ~5s. Attempts are bounded per
outage (so a port permanently held by another editor can't spam the console), the
budget resets whenever the bridge is seen running, and a manual **Start Session** is
picked up automatically. If you see the editor sitting at stdio "no session" and it
does not recover, check the console for `[UnityMcpStdioAutoStart]` give-up warnings
(usually the port is held by another editor copy).

It only runs **when stdio is already the selected transport**
(`MCPForUnity.UseHttpTransport=false`); it does not change the transport choice,
so anyone preferring HTTP is left untouched.

The explicit start is needed because the package's own auto-start
(`StdioBridgeHost` static ctor → `ShouldAutoStartBridge()`) reads a *cached* copy
of the pref whose value depends on undefined `[InitializeOnLoad]` ordering, so it
is unreliable on a cold open. This script reads the pref directly and kicks the
start after init settles, covering every ordering.

To use stdio for the first time (or to start manually), set the Transport
dropdown to **Stdio** in `Window > MCP for Unity` and start the session; the
pref then persists and the script auto-starts it on subsequent launches.

The script also self-heals a **wiped** transport pref. `MCPForUnity.UseHttpTransport`
is a machine-global EditorPref that any Unity-family process can flush over on exit;
once unset, the package treats it as HTTP and starts the wrong bridge. Whenever stdio
is observed selected, the script records that in this project's own
`EditorUserSettings` (`UserSettings/`, not committed, not machine-global), and heals a
later unset pref back to stdio only when that record exists. A first boot with no
record is left alone — you get a console line pointing at the one-time setup above,
not a silent default.

## Verifying the editor is actually attached

Don't trust "Connected" alone. Confirm with a live round-trip:

- read the `mcpforunity://instances` resource — the project should appear as
  `<ProjectName>@<hash>` with `status: running`
- call a read-only tool (e.g. `manage_editor telemetry_status`) and confirm
  `{"success": true}`

## Multiple instances

To run a second workspace, register its Claude the same way. If two stdio
editors cross-talk, pin each call with `unity_instance="<ProjectName>@<hash>"`,
or set a session default with `set_active_instance`.

Match on the instance `path` under this working directory, never on a project name —
the matching instance differs per working copy.

## Compile-check fallback

When Unity is in Safe Mode or console errors block normal MCP work, the mod
assembly can be checked directly without the editor:

```
dotnet build FFMod.csproj --no-restore
```

This does not replace Unity Test Runner results but isolates C# compile blockers.
Note it needs the Final Factory DLLs already copied into `Assets/FinalFactoryDlls`
(see `README.md`) — without them the reference resolution fails before any of your
own code is compiled.
