using FFComponents.Combat;
using FFComponents.Knn;
using FFComponents.UnitStates.Combat;
using FFCore.Systems;
using FFSystems.Core;
using FFSystems.UnitStateMachine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Mathematics.FixedPoint;
using Unity.Transforms;

namespace Systems
{
  /// <summary>
  ///   Example of a simulation tweak: now and then, an idle ship of a player's fleet jumps to a random spot
  ///   near the player.
  ///   <para>
  ///     It moves ships, which is simulation, so it is written to stay identical on every peer of a
  ///     multiplayer game:
  ///     - it runs on the heartbeat (a Fixed group), after the game's FleetIdleSystem so the game does not
  ///       overwrite the move;
  ///     - its time is the simulation clock (SimulationElapsedTime), never wall-clock or frame time;
  ///     - its randomness is seeded from the world seed, the simulation time and the ship's
  ///       DeterministicCombatObjectId, which is the same on every peer. Never seed from an Entity: entity
  ///       handles differ between peers. (RandomSystem.GetRandomForEntity does exactly that, so do not use
  ///       it for simulation.)
  ///   </para>
  /// </summary>
  [UpdateInGroup(typeof(FFFixedPreTransformGroup))]
  [UpdateAfter(typeof(FleetIdleSystem))]
  public partial class FleetRandomMovementSystem : FinalFactorySystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      SetSystemQueryForInPlayEntities(new EntityQueryBuilder(Allocator.Temp)
        .WithAllRW<LocalTransform>()
        .WithAll<FleetIdleMarker, FleetShip, DeterministicCombatObjectId>()
        .WithNone<DisableKnnMarker, AbilityMarker>());
    }

    protected override void PerformSystemUpdate()
    {
      // Tip: a Debug.Log here proves the system runs, but logging every heartbeat is slow; remove it before
      // you release.
      Dependency = new FleetRandomMovementJob
      {
        AllCommanders = SystemAPI.GetComponentLookup<FleetCommander>(true),
        SimulationTime = SimulationElapsedTime,
        Seed = MasterSeed
      }.Schedule(CachedEntityQuery, Dependency);
    }

    [BurstCompile]
    private partial struct FleetRandomMovementJob : IJobEntity
    {
      [ReadOnly] public ComponentLookup<FleetCommander> AllCommanders;

      public fp SimulationTime;
      public uint Seed;

      private void Execute(ref LocalTransform localTransform, in FleetShip fleetShip, in DeterministicCombatObjectId id)
      {
        if (id.Value == 0 || !AllCommanders.TryGetComponent(fleetShip.OwnerEntity, out var commander))
        {
          return;
        }

        var identity = math.hash(new uint2((uint)id.Value, (uint)(id.Value >> 32)));
        var random = RandomSystem.GetRandomForStableHashAndSimulationTime(Seed, identity, SimulationTime);
        // About once every 20 heartbeats per ship (16 heartbeats a second).
        if (random.NextInt(20) != 0)
        {
          return;
        }

        // Whole-unit offsets from integer draws: no sin/cos, whose float results can differ between CPUs.
        var offset = new float3(random.NextInt(-50, 51), 0f, random.NextInt(-50, 51));
        localTransform.Position = commander.FleetPosition + offset;
      }
    }
  }
}
