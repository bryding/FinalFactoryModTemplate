---
name: api-lookup
description: Find the right Final Factory type, component, system, config field or helper method for a mod — from the generated reference in Documentation/API instead of guessing. Use whenever code needs a game API you have not already seen used in this repo, when a compile error says a member does not exist, or after a game update.
---

# Look up the game's API

You do not have the game's source. `Documentation/API/` is generated from the game's DLLs and is the
complete list of what a mod can call. Never invent a name.

## Search

```sh
# One line per member, "Namespace.Type :: signature":
grep -i "health" Documentation/API/all-members.txt | head -40
grep "FFComponents.Combat.Health ::" Documentation/API/all-members.txt
grep "FFCore.Extensions.Ecs ::" Documentation/API/all-members.txt          # Ecs.GetSingleton, SetComponent, ...
grep -E "FFCore.Systems.FF(Fixed|Controller)[A-Za-z]+Group ::" Documentation/API/all-members.txt   # system groups
grep -i "\[Save\]" Documentation/API/all-members.txt | grep -i inventory   # saved components
```

Then read the type's section in `Documentation/API/<Namespace>.md` for its attributes (`[Save]`,
`[UpdateInGroup]`), base types and the summaries.

## Where things usually are

| Looking for | Namespace |
|---|---|
| Components (`IComponentData`) of buildings, ships, players | `FFComponents.*` (`Core`, `Combat`, `Inventory`, `Power`, `Player`, ...) |
| Item and building configs (`EntityConfig`, `ItemConfig`, `FleetConfig`, ...) | `FFCore.Config`, `FFCore.Config.Technologies` |
| Entity helpers (`Ecs.*`, extension methods on `Entity`) | `FFCore.Extensions` |
| System groups | `FFCore.Systems` |
| The system base class (`FinalFactorySystemBase`) | `FFSystems.Core` |
| Game systems to order against (`UpdateBefore`/`UpdateAfter`) | `FFSystems.*` |
| Time (`FFTimeData`), randomness (`RandomSystem`) | `FFCore.Time`, `FFSystems.Core` |
| Grid and maths helpers (`GridHelper`, `MathHelper`) | `FFCore.Utils` |
| Player actions | `NetworkOperations.Settings` (see the `add-player-action` skill) |
| The mod interfaces | `FFCore.Modding` |

## Rules

- `[Obsolete]` in a signature means do not use it; its message says what to use instead.
- A member missing from the reference is not public: a mod cannot call it.
- After a game update, run `Tools/generate-api-reference.sh` so the reference matches the DLLs you
  build against, then `Tools/compile-check.sh`.
- The summaries describe the game's behaviour; the signatures are the truth for what compiles.
