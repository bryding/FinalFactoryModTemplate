# Final Factory Mod Documentation

This is the official documentation on how to mod Final Factory and how its internal systems work.

## Configuration vs Behavioral Mods

There are two types ways to mod Final Factory:

1. Configuration-driven mods (Simple)  
These mods are able to update the configuration for existing entities and add new models, ships, and objects to the world.  For instance, adjusting the amount of resources contained with an asteroid is something that can be handled with pure configuration overrides.

2. Behavioral mods (Advanced)  
These mods implement their own ECS Systems which override or replace existing system behavior.  These are much more advanced, and can modify nearly any behavior in the game, but are more likely to break from game upgrades and introduce bugs.

Some mods have both updated configuration and behavior.

**If you're new, it's strongly recommended to do a configuration-based mod before moving to altering behavior.**

## Unity ECS Overview

To deeply mod Final Factory and modify behavior, you'll need to have a strong understanding of Unity ECS.  This is
outside the scope of this document, but some good resources to get started are here:
https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html

Final Factory heavily uses Unity ECS to drive nearly all built-in behavior.  While a few things use Unity monobehaviors,
the vast majority of behavior is driven by Unity Systems.

## Final Factory System Organization and Naming

Final Factory's system groups are defined in SystemGroups.cs.

Final Factory uses the following system groups that run in the following order:  
Early -> PreTransform -> Transform (Unity) -> PostTransform -> ItemFinalizer -> Late

(There are a few other system groups, but these are not typically used and described here)

The purpose of each System group is as follows:

### Early
Early system groups do setup and other initialization operations at the beginning of every frame.  For example, setting
up the current time delta, heartbeat, and other initial frame data all happens in early.

### PreTransform
Pretransform contains a large number of systems that need to setup parameters before objects are moved in the scene.
All systems that want to do things like update the position or velocity of objects should go into this group.

### Transform
This is actually not a FF System Group--it's built-in to Unity.  While you should not add systems here, it's important
to understand that this is the point at which Unity entities are manipulated and moved in the world.  Therefore, if you
do something like update the position of an object after transform, it won't update until the following frame.

### PostTransform
PostTransform is where most of the systems go.  At this point, everything is located in the world exactly where it
should be, so this is an excellent time to update crafting progress, heat, damage, and most other attributes about the
world.  In short, this is the point in the calculation where the world is "correct", and any system that cares about
that should be placed here.

### ItemFinalizer
This system group is typically used to place objects in the world that result from pretransform and post transform.  By
placing objects here, we make sure the world isn't changing in the middle of PostTransform, causing race conditions and
other hard to diagnose bugs.

### Late
Late system groups typically do cleanup, such as deleting destroyed entities and objects from the game.  Deleting
entities only at the end of frames greatly simplifies the architecture and avoids errors operating on destroyed entities.

### Fixed vs Controller
For each of the above phases, there is both a "Fixed" and a "Controller" variant of the group.  

System Groups are predictably named: FF<Fixed/Controller><Phase>Group
 
Examples:  
FFControllerPostTransformGroup  
FFFixedPostTransformGroup

Each group has a different purpose, described below.

**Controller Groups:**  
These run on every frame that is rendered, making them ideal for animations, ship movement, and other updates where 
perfectly smooth movement is desired.  DeltaTime for these systems is based on the amount of time passed since the last
render pass.

**Fixed groups:**  
These run on every factory simulation and should be used for any action that updates factory state, such as crafting,
inventory updates, etc.  

When in doubt, use the Fixed group.  Creating Controller groups that affect factory state can result in multiplayer 
desyncs.

The separation between render and simulation ensures that highly fluid movement, animations, and behavior exist on high end systems while factory
simulation is predictable.  

### Rate Limiting

In future versions, rate limiting will be turned on (likely 60UPS--Updates per Second).  This is required for the 
multiplayer rollout. When this happens, Delta time for Fixed groups will be 1000ms/UPS, which means that if the game 
cannot keep up, the user will experience time dilation in order to keep simulation consistent.  This is similar to how 
Factorio works.  You can see this behavior by running "setRateLimit <UPS>" in the console.

If FPS > UPS, you will see multiple calls to Update() methods in Controller groups for each
Fixed group's Update() call.  If FPS goes below target UPS, Fixed system groups will run on every Controller Update() 
call.



