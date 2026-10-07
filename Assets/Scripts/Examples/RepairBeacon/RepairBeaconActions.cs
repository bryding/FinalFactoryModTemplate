using System;
using FFCore.Extensions;
using NetworkOperations.Settings;
using Unity.Entities;
using UnityEngine;

namespace Examples.RepairBeacon
{
  /// <summary>
  ///   Example of a player action: switching a beacon on or off. In multiplayer every peer runs the whole
  ///   simulation, so a click must not change the world directly; it is sent as a network operation that
  ///   the host checks and every peer applies on the same heartbeat. Single player goes through the same
  ///   path, so there is one code path to test.
  ///   <para>
  ///     The game has no dedicated mod API for this yet. The route that works today is the game's
  ///     SetStructureSetting operation with a setting kind of your own: register an applier on every peer
  ///     (<see cref="Register" />, called from PostInitializationHook), then send with
  ///     <see cref="StructureSettingsDispatch.DispatchSetting(StructureSettingKind, Entity, object)" />.
  ///     These types live in FFSpaghetti.dll, outside the documented mod API, so a game update can change
  ///     them. Pick your own kind number: see CLAUDE.md, "Player actions".
  ///   </para>
  /// </summary>
  public static class RepairBeaconActions
  {
    /// <summary>This example's kind. The game's own kinds are small numbers; use a block no other mod uses.</summary>
    public const StructureSettingKind SetEnabledKind = (StructureSettingKind)47001;

    [Serializable]
    public class SetEnabledPayload
    {
      public bool Enabled;
    }

    public static void Register()
    {
      StructureSettingAppliers.Register(SetEnabledKind, new StructureSettingApplier
      {
        // Validate and Apply run on every peer, on the heartbeat. They must give the same answer everywhere
        // (read only the target's components and the payload; no local player, camera, time or randomness)
        // and must never throw.
        Validate = (target, innerJson) => Ecs.HasComponent<RepairBeacon>(target) && TryRead(innerJson, out _),
        Apply = (target, innerJson) =>
        {
          if (!TryRead(innerJson, out var payload) || !Ecs.TryGetComponent(target, out RepairBeacon beacon))
          {
            return;
          }

          beacon.Enabled = payload.Enabled;
          Ecs.SetComponent(target, beacon);
        }
      });
    }

    /// <summary>
    ///   Asks for <paramref name="beacon" /> to be switched on or off. The change shows about one heartbeat
    ///   later, on every peer. The target must be a building: the action names it by its grid tile.
    /// </summary>
    public static void RequestEnabled(Entity beacon, bool enabled)
    {
      StructureSettingsDispatch.DispatchSetting(SetEnabledKind, beacon, new SetEnabledPayload { Enabled = enabled });
    }

    private static bool TryRead(string innerJson, out SetEnabledPayload payload)
    {
      payload = null;
      try
      {
        payload = JsonUtility.FromJson<SetEnabledPayload>(innerJson);
      }
      catch (Exception)
      {
        return false;
      }

      return payload != null;
    }
  }
}
