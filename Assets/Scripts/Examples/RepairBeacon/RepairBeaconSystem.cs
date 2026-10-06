using FFComponents.Combat;
using FFComponents.Core;
using FFComponents.Power;
using FFComponents.SystemMarkers;
using FFCore.Systems;
using FFCore.Time;
using FFCore.Utils;
using FFSystems.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Mathematics.FixedPoint;

namespace Examples.RepairBeacon
{
  /// <summary>
  ///   Example of a multiplayer-safe simulation system. Each heartbeat, every switched-on, fully powered
  ///   beacon repairs the player's damaged buildings within its range.
  ///   <para>
  ///     What makes it safe in a lockstep game, where every peer runs it and must get the same result:
  ///     it is in a Fixed group (runs on the heartbeat, not every rendered frame); time is the heartbeat's
  ///     fixed-point step (FFTimeData), never Unity's frame time; distances are fixed point between grid tiles,
  ///     never float transforms; it reads no input, camera or local player; and its result does not depend on
  ///     the order entities are visited in (each building's repair is a sum, and fixed-point addition gives
  ///     the same sum in any order). When "first" or "nearest" matters, sort by a key every peer agrees on,
  ///     such as Placeable.CenterTile.
  ///   </para>
  /// </summary>
  [UpdateInGroup(typeof(FFFixedPreTransformGroup))]
  public partial class RepairBeaconSystem : FinalFactorySystemBase
  {
    private EntityQuery beaconQuery;

    protected override void OnCreate()
    {
      base.OnCreate();
      // The buildings that can be repaired (in play, not being deleted).
      SetSystemQueryForInPlayEntities(new EntityQueryBuilder(Allocator.Temp)
        .WithAllRW<Health>()
        .WithAll<Placeable>());
      beaconQuery = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<RepairBeacon, Placeable, StationGridPowerConsumer>()
        .WithNone<OutOfPlay, DeletionMarker>()
        .Build(this);
      RequireForUpdate(beaconQuery);
      RequireForUpdate<FFTimeData>();
    }

    protected override void PerformSystemUpdate()
    {
      var dt = SystemAPI.GetSingleton<FFTimeData>().deltaTime;

      var beaconEntities = beaconQuery.ToEntityArray(Allocator.Temp);
      var beacons = new NativeList<ActiveBeacon>(beaconEntities.Length, Allocator.TempJob);
      foreach (var entity in beaconEntities)
      {
        var beacon = EntityManager.GetComponentData<RepairBeacon>(entity);
        var power = EntityManager.GetComponentData<StationGridPowerConsumer>(entity);
        if (!beacon.Enabled || power.SatisfactionRatio < fp.one)
        {
          continue;
        }

        beacons.Add(new ActiveBeacon
        {
          Tile = EntityManager.GetComponentData<Placeable>(entity).CenterTile.xz,
          Range = beacon.Range,
          HealPerHeartbeat = beacon.HealPerSecond * dt
        });
      }

      if (beacons.Length == 0)
      {
        beacons.Dispose();
        return;
      }

      Dependency = new RepairJob { Beacons = beacons.AsArray() }.Schedule(CachedEntityQuery, Dependency);
      Dependency = beacons.Dispose(Dependency);
    }

    private struct ActiveBeacon
    {
      public int2 Tile;
      public fp Range;
      public fp HealPerHeartbeat;
    }

    [BurstCompile]
    private partial struct RepairJob : IJobEntity
    {
      [ReadOnly] public NativeArray<ActiveBeacon> Beacons;

      private void Execute(ref Health health, in Placeable placeable)
      {
        if (!placeable.PlayerPlaced || health.Invulnerable || health.CurrentHealth >= health.MaxHealth)
        {
          return;
        }

        var tile = placeable.CenterTile.xz;
        var heal = fp.zero;
        for (var i = 0; i < Beacons.Length; i++)
        {
          // MathHelper.worldDistance: fixed-point distance in world units between two grid tiles.
          if (MathHelper.worldDistance(tile, Beacons[i].Tile) <= Beacons[i].Range)
          {
            heal += Beacons[i].HealPerHeartbeat;
          }
        }

        if (heal > fp.zero)
        {
          health.CurrentHealth = fpmath.min(health.CurrentHealth + heal, health.MaxHealth);
        }
      }
    }
  }
}
