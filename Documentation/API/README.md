# Game API reference (generated)

The public types and members of Final Factory that a mod can use, with C# signatures and the first paragraph of
the game's own doc comments. It exists so that you, and an AI coding agent, can write a mod without the game's
source. Start from `INDEX.md`, or grep `all-members.txt`:

```sh
grep -i "ItemConfig ::" Documentation/API/all-members.txt        # every member of ItemConfig
grep "FFCore.Extensions.Ecs ::" Documentation/API/all-members.txt  # the Ecs helpers (GetSingleton, SetComponent, ...)
grep -i ":: .*struct .*Health" Documentation/API/all-members.txt   # find a component by name
```

| File | Contents |
|---|---|
| `INDEX.md` | Every namespace, with its number of types |
| `<Namespace>.md` | Each type: its declaration (attributes such as `[Save]` and `[UpdateInGroup]`, base types), its members, and summaries |
| `all-members.txt` | One line per type, member and enum value: `Namespace.Type :: signature` |
| `doc-comments.json` | The summaries, by member; kept so a regeneration without the game's source keeps them |

## Regenerating

After a game update, copy the game DLLs again (`copy-finalfactory-dlls`) and run:

```sh
Tools/generate-api-reference.sh
```

It rebuilds the signatures from your DLLs, so the reference matches the game you build against, and reuses the
summaries in `doc-comments.json` (a member new in this game version shows without one). It needs the Unity editor
this project uses (for its bundled .NET runtime and C# compiler) and `finalfactory.properties` pointing at the
game. The game team refreshes the summaries at release with
`Tools/generate-api-reference.sh --source <the game's Assets/Scripts> --game-version <version>`.

## What is in it, and what is not

The generator (`Tools/ApiReference/ApiReference.cs`) reads the game's shipped assemblies. What it publishes is set
by `Tools/ApiReference/api-reference.json`:

- **Only public API.** Public types, and their public members (plus protected members of classes you can inherit,
  such as `FinalFactorySystemBase`). Never private or internal members, and never method bodies: no game code.
  Every signature here is already visible to anyone who opens the shipped DLLs in a decompiler, which the game's
  terms allow for modding.
- **Only mod-facing assemblies and namespaces.** `FFCore`, `FFComponents`, `FFSystems` and `FFTechnology`, minus
  networking, platform, telemetry, diagnostics, test and cheat namespaces, which a mod should not call. From
  `FFSpaghetti` (the game's UI and glue assembly) only the four types mods use to send player actions.
- **Summaries, not design notes.** The doc comments are the game's own, cut to their first paragraph, with
  internal references removed (source file paths, task and feature numbers, people's names, dates, TODOs).

This version was generated from Final Factory 0.50.0.78 (Steam development branch, build 25734448).
