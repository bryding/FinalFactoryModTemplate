using FFCore.Serialization;
using Unity.Entities;
using Unity.Mathematics.FixedPoint;

namespace Examples.RepairBeacon
{
  /// <summary>
  ///   Example of simulation state a mod owns: a building that slowly repairs the player's damaged
  ///   buildings around it while it is switched on and fully powered.
  ///   <para>
  ///     [Save] makes the game save it with the building, and a player who joins a multiplayer game gets
  ///     the host's save, so they get it too. Once a mod has shipped, never change the fields of a [Save]
  ///     struct: the game refuses a save whose saved layout differs.
  ///   </para>
  /// </summary>
  [Save]
  public struct RepairBeacon : IComponentData
  {
    /// <summary>Switched by players through <see cref="RepairBeaconActions" />, never written by UI code.</summary>
    public bool Enabled;

    /// <summary>World units (10 per tile) from the beacon's centre tile.</summary>
    public int Range;

    /// <summary>Health restored per second to each damaged building in range. Fixed point, never float.</summary>
    public fp HealPerSecond;
  }
}
