# Final Factory Mod Template

This repo demonstrates how to create basic mods in Final Factory and can be used as a template for Final Factory mods generally.

## Requirements

* Unity 2022.2.9f1
* Git & Git LFS

## Getting Started

1. Clone the repo (the zip doesn't work)
1. Download the required Unity version
1. Open the root folder in Unity

## Building and using your mod

This repo comes with an editor script that will put the managed and burst DLL's into a folder of your choosing. 

1. Navigate to the top menu in Unity, click Modding -> Build X64 Mod
1. Select the folder you want your DLL's to live in
1. Move this folder to the `Mods` folder in the Final Factory application root
1. Boot up Final Factory and your mod should load on startup

_Steam Workshop support under construction_
<br>
<br>

## The Basics

All mods must implement exactly one `IUserMod` interface. It has the following API:

```C#
string Name { get; }
string Description { get; }
string Author { get; }
ModVersion Version { get; }
```

See `Assets/Scripts/UserMod.cs`for an example.

Additionally, mods can optionally implement the `IUserModLoader` interface. It has the following API:

```C#
/// <summary>
///   If you want to add any entities to the game, define them here. Each entity should be associated with an entity
///   prefab and all the various item configs. If you don't want to add any entities, return an empty list.
/// </summary>
/// <returns>A list of configs associated with each new entity you want to add to the game</returns>
List<EntityConfig> DefineEntityConfigs();
/// <summary>
///   Use this hook to define any logic that happens after all the configuration and systems have been loaded.
///   The sky is the limit here. You can access all the config in the game at this point and change anything you want
///   about the items in the game or change what systems are running.
/// </summary>
void PostInitializationHook();
```

See `Assets/Scripts/UserModLoader.cs` for an example.

## Systems

You have full access to all the components and systems in Final Factory via the FFCore, FFSystems, and FFComponents DLL's. You can write new systems and burst compiled  jobs in the exact same way I would as the developer.  Any systems you add to the project will get detected in Final Factory when mods load and automatically get added to the running systems list.

See `Assets/Scripts/Systems/FleetFollowSystem.cs` for an example of a system that adds some silly behavior to ships following the player.
<br>
<br>

### Resources

If you plan on writing new systems for Final Factory, you'll need some Unity DOTS knowledge. I recommend these resources:

* [Entities Manual](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html)
* [Turbo Makes Games](https://www.youtube.com/c/TurboMakesGames)
* [WAYNGames](https://www.youtube.com/@WAYNGames)
* [Unity Discord](https://discord.gg/unity) (see #dots-forum)
