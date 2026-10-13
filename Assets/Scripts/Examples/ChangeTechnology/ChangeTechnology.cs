using System.Collections.Generic;
using System.Linq;
using Behaviours.Research;
using FFCore.Config.Technologies;
using UnityEngine;

namespace Examples.ChangeTechnology
{
  /// <summary>
  ///   Example (template issue #6): changing a technology the game already has, from <c>PostInitializationHook</c>.
  ///   <para>
  ///     <c>IUserModLoader.AddTechnologies</c> only ADDS technologies. A config returned there with the name of an existing
  ///     technology is added next to it, it does not replace it. To change an existing one, edit the game's own
  ///     <see cref="TechnologyConfig" /> after the game has loaded them all: <see cref="TechnologyLoader.TechnologyConfigs" />
  ///     holds the list the research tree and the Technology window read, and each entry is a class, so a change to its
  ///     fields is seen by both (<c>PostInitializationHook</c> runs after the tree is built, at startup, on every peer).
  ///   </para>
  ///   <para>
  ///     Multiplayer: research costs and what a technology unlocks are part of the simulation, so every peer must make
  ///     the same change; a mod that every player has installed does. <c>TechnologyLoader</c> lives in FFSpaghetti.dll,
  ///     outside the documented API (the reference lists only <c>TechnologyConfig</c> and the other FFCore types), so a game
  ///     update can change it.
  ///   </para>
  /// </summary>
  public static class ChangeTechnology
  {
    /// <summary>
    ///   Finds a technology by its name (the name in the Technology window, also <c>TechnologyConfig.Name</c>), or null.
    /// </summary>
    public static TechnologyConfig Find(string name)
    {
      return TechnologyLoader.Instance.TechnologyConfigs.FirstOrDefault(tech => tech.Name == name);
    }

    /// <summary>
    ///   Makes the named technology cheaper by the given percentage, and logs what it was and is. Returns false (and
    ///   logs why) when the game has no technology of that name, so a game update that renames it shows in the log
    ///   instead of throwing in the middle of startup.
    /// </summary>
    public static bool ReduceCost(string name, int percent)
    {
      var tech = Find(name);
      if (tech == null)
      {
        Debug.LogWarning($"ChangeTechnology: the game has no technology named '{name}'; left alone.");
        return false;
      }

      var before = tech.Cost;
      tech.Cost = Mathf.Max(1, before * (100 - percent) / 100);
      Debug.Log($"ChangeTechnology: '{name}' now costs {tech.Cost} (was {before}).");
      return true;
    }

    /// <summary>
    ///   Adds an item (by its name, as in <c>ItemsUnlocked</c>) to what the named technology unlocks, once.
    /// </summary>
    public static bool AlsoUnlock(string techName, string itemName)
    {
      var tech = Find(techName);
      if (tech == null)
      {
        Debug.LogWarning($"ChangeTechnology: the game has no technology named '{techName}'; left alone.");
        return false;
      }

      tech.ItemsUnlocked ??= new List<string>();
      if (!tech.ItemsUnlocked.Contains(itemName))
      {
        tech.ItemsUnlocked.Add(itemName);
      }

      Debug.Log($"ChangeTechnology: '{techName}' now also unlocks '{itemName}'.");
      return true;
    }
  }
}
