# Final Factory Mod Template

This repo demonstrates how to create basic mods in Final Factory and can be used as a template for Final Factory mods generally.

## Requirements

* Unity 2022.3.17f1

## Getting Started

1. Clone the repo (the zip doesn't work)
1. Download the required Unity version
1. Navigate to your Final Factory installation folder and then `finalfactory_data/Managed/`
  * For example: "C:\Program Files\Steam\steamapps\common\FinalFactory\finalfactory_Data\Managed"
  * If you compiled Final Factory locally, it would be in the builds folder.
1. Grab the following DLL's from that folder:
  * FFCore.dll
  * FFSystems.dll
  * FFComponents.dll
  * FFTechnology.dll
  * FFNetcode.dll (If present)
1. Copy these DLL's to the `Assets/FinalFactoryDlls` folder in this project
1. Open the root folder in Unity
1. Place a preview image file for Steam (Must be \<1MB) in the template project root folder.  It must be called Preview.png or Preview.jpg
  * This is the image that will show for your mod on the steam workshop when you upload the mod
  
> WARNING  
> Known Issue: When you first open the project, you may get error that assemblies failed to load (e.g. Unity.Netcode.Runtime).  
> Simply clear these errors and restart the Unity project, and the errors will go away.

## Building your mod

This repo comes with an editor script that will put the managed and burst DLL's into a folder of your choosing. 

1. Navigate to the top menu in Unity, click Modding \-\> Build X64 Mod to build your mod.  Your mod will build in the \<project root\>/build folder

## Installing your mod
There are two ways you can install your mod:

1. Manually copy the folder in \<project root\>/build to the `C:\Users\<user>\AppData\LocalLow\Never Games\finalfactory\mods` folder OR
1. Navigate to the top menu in Unity, click Modding \-\> Build and Install  (this will build your mod and install it into the folder listed above)

Then start Final Factory and your mod should load on startup

## Uploading your mod to the workshop

1. Start up Final Factory with your mod installed
1. Go to the Mod Menu
1. Next to your mod, there should be a blue ^ icon
1. Click the icon and it will upload to the workshop.  Note: It may take a few minutes for the workshop to show your mod.  Once it's fully published, refreshing the screen will remove the ^ icon
1. If you want to upload your mod again, increment your mod version, rebuild, reinstall, and the upload icon will reappear

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
* [Unity Discord](https://discord.gg/unity) (see #dots-forum)* 
* [Final Factory Discord](https://discord.gg/finalfactory) (see #modding)

