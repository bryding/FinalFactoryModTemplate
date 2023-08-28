using FFComponents.Combat;
using FFComponents.Knn;
using FFComponents.SystemMarkers;
using FFComponents.UnitStates.Combat;
using FFCore.Extensions;
using FFCore.Random;
using FFCore.Systems;
using FFSystems.Core;
using FFSystems.UnitStateMachine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
  [RequireMatchingQueriesForUpdate]
  [UpdateInGroup(typeof(AsteroPreTransformSimulationGroup))]
  public partial struct FleetRandomMovementISystem : ISystem
  {
    private EntityQuery _query;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
      state.RequireForUpdate<MasterRandom>();
      state.RequireForUpdate<TimeSystem.TimeSystemData>();
      _query = new EntityQueryBuilder(Allocator.Temp)
        .WithAll<FleetIdleMarker, FleetShip>()
        .WithNone<DisableKnnMarker, AbilityMarker>().WithNone<DeletionMarker, OutOfPlay>().Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
      //Calling a debug statement is very slow and should NEVER be done in an update call.  However, this makes it
      //really easy to see that your mod is loaded and running when debugging.  Be sure to remove it before releasing
      //your mod!
      Debug.Log("Scheduling Fleet Random Movement job...");
      state.Dependency = new FleetRandomMovementJob
      {
        AllCommanders = SystemAPI.GetComponentLookup<FleetCommander>(true),
        Elapsed = SystemAPI.GetSingleton<TimeSystem.TimeSystemData>().realElapsedTime,
        Seed = SystemAPI.GetSingleton<MasterRandom>().TheMasterSeed
      }.Schedule(_query, 
        //Make sure this system runs after FleetIdleSystem has finished running.  If not, FleetIdleSystem will override
        //each ship's movement and make the FleetIdleSystem's movement changes have no effect.
        JobHandle.CombineDependencies(state.Dependency, state.WorldUnmanaged.GetExistingSystemState<FleetIdleSystem>().Dependency));
    }

    [BurstCompile]
    private partial struct FleetRandomMovementJob : IJobEntity
    {
      [ReadOnly] public ComponentLookup<FleetCommander> AllCommanders;

      public double Elapsed;
      public uint Seed;

      public void Execute(Entity entity, ref LocalTransform localTransform, in FleetShip fleetShip)
      {
        if (!AllCommanders.TryGetComponent(fleetShip.OwnerEntity, out var commander))
        {
          return;
        }

        var nextRandom = RandomSystem.GetRandomForEntity(Seed, Elapsed, entity);
        if (nextRandom.NextFloat() < 0.95f) return;

        var commanderPosition = commander.FleetPosition;
        var random = nextRandom.UnitCircle() * 50;

        localTransform.Position = commanderPosition + new float3(random.x, 0, random.y);
      }
    }
  }
}