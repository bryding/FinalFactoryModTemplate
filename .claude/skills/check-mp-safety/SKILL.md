---
name: check-mp-safety
description: Review the mod's code for Final Factory multiplayer determinism (lockstep) before calling a change done — run the scanner, then walk the rules it cannot check. Use after writing or changing any ECS system, UI that touches the world, saved components, or randomness; and when asked whether a mod "works in multiplayer".
---

# Check multiplayer safety

## 1. Run the scanner

```sh
Tools/check-mp-safety.sh
```

- **ERROR**: almost always a real desync. Fix it.
- **WARN**: read the line. "Controller group" warnings are expected for presentation systems: confirm
  the system only touches presentation (UI, rings, colours). "Query order" warnings are fine when the
  order cannot change the result (sums, independent per-entity writes); otherwise sort by
  `Placeable.CenterTile`.
- A clean run does not prove safety. Continue with step 2.

## 2. Walk the rules the scanner cannot see

For each system or UI file you changed, answer with a file:line:

1. **Where does it run?** Changes to the world (positions, health, inventories, research, components
   the game reads, spawning, deleting) → a Fixed group. Anything in a Controller group or a
   MonoBehaviour may only read the world, draw, or send a player action.
2. **What does it read?** Simulation must read only simulation: no input, camera, hover/selection,
   `MePlayer`, frame or wall-clock time, `LocalToWorld`, presentation-only components
   (`RangeIndicatorDisplay`, anything in `FFComponents.Presentation`).
3. **Is every decision fixed point and order-free?** Distances, ranges, picks and timers in `fp`;
   ties broken by a stable key; no "last writer wins" across entities in query order.
4. **Does a join see the same world?** List the state your simulation depends on. Each item must be
   `[Save]` on a saved entity (building, ship, player), or recomputed every heartbeat from such state
   before anything reads it. Prefab defaults are what a joining player gets for everything else.
5. **Did a `[Save]` struct change?** Its fields must not change once shipped (the game refuses the
   save). New `[Save]` types are fine.
6. **Structural changes**: game components (`FF*` namespaces) added or removed only in Fixed groups,
   and never on child mesh entities.
7. **Player actions**: every UI change goes through a network operation whose `Validate`/`Apply` read
   only the target and payload, and never throw (see the `add-player-action` skill).

## 3. Report

Say which rules you checked and how (file:line), what you fixed, and what still needs a run in the
game. You cannot test multiplayer itself from here: the game currently turns multiplayer off while
mods are enabled.
