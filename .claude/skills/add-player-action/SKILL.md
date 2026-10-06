---
name: add-player-action
description: Let players change something in the game world from the mod's UI (switch a building on or off, set a mode, pick a colour or a target) in a way that works in multiplayer — a network operation with a host-checked, deterministic applier, plus UI that only reads state. Use when a mod needs a button, toggle, panel or hotkey that changes simulation state.
---

# Add a player action (network operation)

In Final Factory multiplayer every peer runs the simulation, so a click must not write the world.
It is sent as a network operation: the host checks it, every peer applies it on the same
heartbeat, and single player goes through the same path. The worked example is
`Assets/Scripts/Examples/RepairBeacon/` — copy its shape.

## 1. The state

The thing the action changes is a component on a **building** (the action finds its target by
grid tile, so it must be a `Placeable`). Make it `[Save]` so saves and joining players get it, and
add it to the building's prefab in `PostInitializationHook`
(`itemConfig.GetPrefabForName(name).AddAndSetComponent(new MyState { ... })`). Never change the
fields of a `[Save]` struct after the mod has shipped.

## 2. The action

A static class like `RepairBeaconActions`:

- a kind: `public const StructureSettingKind MyKind = (StructureSettingKind)N;` — choose a block no
  other mod uses (CLAUDE.md, "Player actions", lists the known ones);
- a `[Serializable]` payload class (sent as JSON);
- `Register()`: `StructureSettingAppliers.Register(MyKind, new StructureSettingApplier { Validate, Apply })`;
- `Request...(Entity building, ...)`: `StructureSettingsDispatch.DispatchSetting(MyKind, building, payload)`.

`Validate` and `Apply` run on every peer, on the heartbeat:

- read only the target's components and the payload (no `MePlayer`, camera, selection, time,
  randomness), so every peer decides the same;
- parse the JSON inside try/catch and return false / do nothing on bad input: they must never throw;
- `Apply` may change other entities too, deterministically (it is not told which player sent it).

Call `Register()` from `PostInitializationHook` (runs once at startup, on every peer).

## 3. The UI

A MonoBehaviour (build it in `OnGameStart`, as `RepairBeaconPanel` does, or load a prefab from
your asset bundle) that:

- reads the selected entity (`SpaghettiApi.Instance.SelectedEntity`) and its component;
- on click, calls your `Request...` method — and nothing else: no `Ecs.SetComponent`;
- shows the component's current value every frame, so the result (about one heartbeat later) and
  other players' changes appear by themselves. If a widget would flicker back while the action is in
  flight, remember the value you sent until the state matches or a second passes.

A hotkey is the same: read the key in a MonoBehaviour or a Controller-group system, then send the action.

## 4. The behaviour

The system that acts on the state is simulation: a Fixed group, fixed-point maths, the heartbeat's
time. See `RepairBeaconSystem.cs` and CLAUDE.md, "Multiplayer and determinism".

## 5. Check

1. `Tools/compile-check.sh` — must say OK.
2. `Tools/check-mp-safety.sh` — no ERROR; read every WARN that points at your files.
3. Build and install (`build-mod` skill), then in the game: click, and the change shows about a
   heartbeat later; save, load, and it is still there.

Caveat to tell the user: these types live in `FFSpaghetti.dll`, outside the documented mod API, so a
game update can change them; and the game currently turns multiplayer off while mods are enabled.
