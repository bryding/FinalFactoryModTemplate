using System.Collections.Generic;
using FFComponents.Knn;
using FFCore.Abilities;
using FFCore.Config;
using FFCore.Config.Technologies;
using FFCore.Extensions;
using FFCore.Fleet;
using FFCore.GlobalConfig;
using FFCore.Items;
using FFCore.Modding;
using Utils;

public class UserModLoader : IUserModLoader
{
  const string LothBat = "Loth Bat";
  
  public List<EntityConfig> DefineEntityConfigs()
  {
    var config = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData // Astero was the old name of the game, so you might see it sprinkled about
      {
        Name = LothBat,
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
      IconAssetName = LothBat,
      RenderingData = new RenderingData
      {
        ModelPath = LothBat,
      },
      CraftRecipe = new List<RecipeItemDataRaw>()
      {
        new()
        {
          ItemName = "Plasma Engine Parts",
          Count = 2,
        },
        new()
        {
          ItemName = "AI Controller Circuit",
          Count = 1,
        }
      }
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

    // Update the accepted ships for various structures so the Loth Bat can be part of the their fleet
    var playerId = itemConfig.GetIdForName("Player");
    ConfigUtils.AddShipToAcceptedShips(itemConfig, playerId, LothBat);
    
    var shipYardId = itemConfig.GetIdForName("Ship Yard");
    ConfigUtils.AddShipToAcceptedShips(itemConfig, shipYardId, LothBat);
    
    var defensePlatformId = itemConfig.GetIdForName("Defense Platform");
    ConfigUtils.AddShipToAcceptedShips(itemConfig, defensePlatformId, LothBat);
    
    // Currently the mod loader doesn't support adjusting the friendly vision. I'll fix this at some point, but this
    // is a good example of being able to change any components you want on entity prefabs in this hook.
    Ecs.SetComponent(itemConfig.GetPrefabForName(LothBat),new KnnFleetVision
    {
      Range = 50,
    });

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
      Requirements = new List<string> {"Start Tech", "Mining Logistics"},
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