using System.Collections.Generic;
using FFCore.Abilities;
using FFCore.Config;
using FFCore.Config.Technologies;
using FFCore.Extensions;
using FFCore.Fleet;
using FFCore.GlobalConfig;
using FFCore.Items;
using FFCore.Modding;

public class UserModLoader : IUserModLoader
{
  const string LothBat = "Loth Bat";
  
  public List<EntityConfig> DefineEntityConfigs()
  {
    var config = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData // Astero was the old name of the game, so you might see it sprinkled about
      {
        Name = LothBat, // This must match your prefab name exactly
        Description = "A bat that is very loth",
        StackSizeLimit = 50,
        ItemCategory = ItemCategory.Ship,
        ItemType = "bat",
      },
      FleetConfig = new FleetConfig
      {
        WeaponConfig = ProjectileType.BatBolt,
        ShipType = ShipType.CombatBot,
        BaseFleetUnitCapacityCost = 1,
        KnnSize = .1f,
        MaxHealth = 25,
        Speed = 150,
        KeepDistance = 55,
        VisionRange = 100,
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTime = 1,
        CountWhenCrafted = 1,
        SpawnsOutsideOfInventory = true,
      },
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

    var playerId = itemConfig.GetIdForName("Player");
    var playerDynamicConfig = itemConfig.DynamicConfig[playerId];
    
    var acceptedShips = playerDynamicConfig.AcceptedShips;
    acceptedShips.Add(itemConfig.GetIdForName(LothBat));
    playerDynamicConfig.AcceptedShips = acceptedShips;
    itemConfig.DynamicConfig[playerId] = playerDynamicConfig;
    
    // Update global config example
    var globalConfig = Ecs.GetSingleton<GlobalConfig>();
    var terrainConfig = globalConfig.Terrain;
    terrainConfig.MinOre = 999999999;
    globalConfig.Terrain = terrainConfig;
    Ecs.SetSingleton(globalConfig);
  }
  
  public List<TechnologyConfig> AddTechnologies()
  {
    var config = new TechnologyConfig
    {
      Name = LothBat,
      Description = "Unlocks bigger, badder, Loth Bats.",
      ItemsUnlocked = new List<string> {LothBat},
      IconNameFromModdedBundle = LothBat,
      Cost = 50,
      Disabled = false,
      Requirements = new List<string> {"Start Tech"},
      ResearchRequirements = new List<TechnologyConfig.SpecificResearchRequirement>()
      {
        new()
        {
          Name = "Asteroid Research",
          Value = 10,
        }
      },
      Rewards = new List<TechnologyConfig.TechnologyRewardFunction>(),
    };

    return new List<TechnologyConfig> {config};
  }
}