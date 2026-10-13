using System;
using FFCore.Extensions;
using NetworkOperations.Settings;
using Unity.Entities;

namespace Examples.RepairBeacon
{
  /// <summary>
  ///   Example of a player action: switching a beacon on or off. In multiplayer every peer runs the whole
  ///   simulation, so a click must not change the world directly; it is sent as a network operation that
  ///   the host checks and every peer applies on the same heartbeat. Single player goes through the same
  ///   path, so there is one code path to test.
  ///   <para>
  ///     The supported route is <see cref="ModActions" /> (game 0.50.0.103 and later): register an action on every peer
  ///     (<see cref="Register" />, called from PostInitializationHook, which runs once at startup before any game
  ///     starts), then send it with <see cref="ModActions.Send{TPayload}" />. See CLAUDE.md, "Player actions".
  ///   </para>
  /// </summary>
  public static class RepairBeaconActions
  {
    /// <summary>Your mod's ID (<c>IUserMod.ID</c>): the action's wire kind is derived from it.</summary>
    public const string ModId = "RandomMovementExample";

    /// <summary>This example's action number, 0 to 255, one per kind of action your mod sends.</summary>
    public const int SetEnabledAction = 1;

    [Serializable]
    public class SetEnabledPayload
    {
      public bool Enabled;
    }

    public static void Register()
    {
      // Validate and Apply run on every peer, on the heartbeat. They must give the same answer everywhere
      // (read only the target's components and the payload; no local player, camera, time or randomness)
      // and must not throw: an exception is caught and logged, and the action is skipped on that peer.
      ModActions.Register<SetEnabledPayload>(ModId, SetEnabledAction,
        validate: (target, payload) => Ecs.HasComponent<RepairBeacon>(target),
        apply: (target, payload) =>
        {
          if (!Ecs.TryGetComponent(target, out RepairBeacon beacon))
          {
            return;
          }

          beacon.Enabled = payload.Enabled;
          Ecs.SetComponent(target, beacon);
        });
    }

    /// <summary>
    ///   Asks for <paramref name="beacon" /> to be switched on or off. The change shows about one heartbeat
    ///   later, on every peer. The target must be a building: the action names it by its grid tile.
    /// </summary>
    public static void RequestEnabled(Entity beacon, bool enabled)
    {
      ModActions.Send(ModId, SetEnabledAction, beacon, new SetEnabledPayload { Enabled = enabled });
    }
  }
}
