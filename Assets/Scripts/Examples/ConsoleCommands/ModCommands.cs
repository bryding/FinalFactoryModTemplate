using FFCore.Extensions;
using QFSW.QC;
using UnityEngine;

namespace Examples.ConsoleCommands
{
  /// <summary>
  ///   Example: a command for the game's developer console (Quantum Console, QFSW.QC).
  ///   <para>
  ///     A mod can add commands. The game's console finds every <c>[Command]</c> in the assemblies loaded when it starts,
  ///     and the mod loader loads yours first. Measured (w1227): a mod with
  ///     <c>[Command("w1227-hello")]</c> loaded in a 0.50.0.103 Windows player, and the console ran it and printed its
  ///     return value. The method must be <c>static</c>; its parameters are parsed from what you type
  ///     (<c>modexample-say "good morning"</c> gives <c>text = good morning</c>).
  ///   </para>
  ///   <para>
  ///     Three things to know. (1) Running ANY command in the console, a mod's included, switches off achievements for
  ///     that save for good: a console is a cheat. (2) A command runs on this peer only. It may read the world, but
  ///     changing simulation state from a command makes this peer's game different from the others' (a desync): to change
  ///     the game, send a network operation (CLAUDE.md, "Player actions"). (3) Prefix your commands with your mod's ID so
  ///     they do not collide with the game's or another mod's.
  ///   </para>
  /// </summary>
  public static class ModCommands
  {
    [Command("modexample-count", "How many entities exist in the world (example command from the mod template)")]
    public static string Count()
    {
      var count = Ecs.EntityManager.UniversalQuery.CalculateEntityCount();
      return $"{count} entities";
    }

    [Command("modexample-say", "Echoes its argument (example command from the mod template)")]
    public static string Say(string text = "hello")
    {
      Debug.Log($"modexample-say: {text}");
      return text;
    }
  }
}
