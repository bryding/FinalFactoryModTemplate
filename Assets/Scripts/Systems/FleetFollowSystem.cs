using FFComponents.Combat;
using FFComponents.Knn;
using FFComponents.UnitStates.Combat;
using FFCore.Extensions;
using FFCore.Systems;
using FFSystems.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Systems
{
  [UpdateInGroup(typeof(AsteroPreTransformSimulationGroup))]
  public partial class FleetFollowSystem : FinalFactorySystemBase
  {
    protected override void OnCreate()
    {
      base.OnCreate();
      SetSystemQueryForInPlayEntities(new EntityQueryBuilder(Allocator.Temp)
        .WithAll<FleetIdleMarker, FleetShip>()
        .WithNone<DisableKnnMarker, AbilityMarker>());
    }

    protected override void PerformSystemUpdate()
    {
      Debug.Log("Scheduling FleetFollow job...");
      Dependency = new FleetFollowJob
      {
        AllCommanders = SystemAPI.GetComponentLookup<FleetCommander>(true),
        Elapsed = ElapsedGameTime,
        Seed = MasterSeed
      }.Schedule(CachedEntityQuery, Dependency);
    }

    [BurstCompile]
    private partial struct FleetFollowJob : IJobEntity
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