using System.Collections.Generic;
using FFCore.Config;
using FFCore.Extensions;
using FFCore.GlobalConfig;
using FFCore.Modding;

public class UserModLoader : IUserModLoader
{
  public List<EntityConfig> DefineEntityConfigs()
  {
    var config = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData // Astero was the old name of the game, so you might see it sprinkled about
      {
        Name = "Mod Item",
        Description = "This is a modded item",
        StackSizeLimit = 50
      }
      // The rest of the configs can be set in post init, or anytime after this method is called
      // The mod loader in Final Factory will automatically add blank entity prefabs if you dont specify one here,
      // but you'll want to update the rest of its data in post init
    };

    return new List<EntityConfig> {config};
  }

  public void PostInitializationHook()
  {
    // Update a single item's config example
    var itemConfig = Ecs.GetSingleton<ItemConfig>();
    var terrainConfigs = itemConfig.TerrainConfigs;
    var bauxiteId = itemConfig.GetIdForName("Bauxite Asteroid");
    var bauxiteIdAsteroidConfig = terrainConfigs[bauxiteId];
    bauxiteIdAsteroidConfig.OreSpawnMultiplier = 100;
    terrainConfigs[bauxiteId] = bauxiteIdAsteroidConfig;

    // Update global config example
    var globalConfig = Ecs.GetSingleton<GlobalConfig>();
    var terrainConfig = globalConfig.Terrain;
    terrainConfig.MinOre = 999999999;
    globalConfig.Terrain = terrainConfig;
    Ecs.SetSingleton(globalConfig);
  }
}