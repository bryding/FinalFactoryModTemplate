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
using Unity.Mathematics.FixedPoint;
using Unity.Transforms;

namespace Systems
{
  [UpdateInGroup(typeof(AsteroPreTransformSimulationGroup))]
  public partial class FleetRandomMovementSystem : FinalFactorySystemBase
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
      //Calling a debug statement is very slow and should NEVER be done in an update call.  However, this makes it
      //really easy to see that your mod is loaded and running when debugging.  Be sure to remove it before releasing
      //your mod!
      var job = new FleetRandomMovementJob
      {
        AllCommanders = SystemAPI.GetComponentLookup<FleetCommander>(true),
        Elapsed = ElapsedGameTime,
        Seed = MasterSeed
      };
      // Dependency = job.Schedule(CachedEntityQuery, 
      // //Make sure this system runs after FleetIdleSystem has finished running.  If not, FleetIdleSystem will override
      // //each ship's movement and make the FleetIdleSystem's movement changes have no effect.
      // JobHandle.CombineDependencies(Dependency, World.Unmanaged.GetExistingSystemState<FleetIdleSystem>().Dependency));
    }

    [BurstCompile]
    private partial struct FleetRandomMovementJob : IJobEntity
    {
      [ReadOnly] public ComponentLookup<FleetCommander> AllCommanders;

      public fp Elapsed;
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