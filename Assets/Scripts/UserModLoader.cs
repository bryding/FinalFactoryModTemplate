using System.Collections.Generic;
using FFCore.Config;
using FFCore.Extensions;
using FFCore.Modding;

public class UserModLoader : IUserModLoader
{
  public List<EntityConfig> DefineEntityConfigs()
  {
    return new List<EntityConfig>();
  }

  public void PostInitializationHook()
  {
    var itemConfig = Ecs.GetSingleton<ItemConfig>();
    var terrainConfigs = itemConfig.TerrainConfigs;

    var bauxiteId = itemConfig.GetIdForName("Bauxite Asteroid");
    var bauxiteIdAsteroidConfig = terrainConfigs[bauxiteId];
    bauxiteIdAsteroidConfig.OreSpawnMultiplier = 100;
    terrainConfigs[bauxiteId] = bauxiteIdAsteroidConfig;
  }
}