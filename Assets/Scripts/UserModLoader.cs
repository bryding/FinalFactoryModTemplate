using System.Collections.Generic;
using FFComponents.Core;
using FFComponents.Knn;
using FFCore.Abilities;
using FFCore.Config;
using FFCore.Config.Technologies;
using FFCore.Crafting;
using FFCore.Extensions;
using FFCore.Fleet;
using FFCore.GlobalConfig;
using FFCore.Inventory;
using FFCore.Items;
using FFCore.Modding;
using UnityEngine;
using Utils;
public class UserModLoader : IUserModLoader
{
  private const string LothBat = "Loth Bat";
  private const string LothAssembler = "Loth Assembler";
  private const string LothPrinter = "Loth Printer";

  public List<EntityConfig> DefineEntityConfigs()
  {
    var lothBat = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData // Astero was the old name of the game, so you might see it sprinkled about
      {
        Name = LothBat,
        Description = "A bat that is very loth",
        StackSizeLimit = 50,
        ItemCategory = ItemCategory.Ship,
        ItemType = "bat"
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
        VisionRange = 100
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTime = 1,
        CountWhenCrafted = 1,
        SpawnsOutsideOfInventory = true
      },
      IconAssetName = LothBat,
      RenderingData = new RenderingData
      {
        ModelPath = LothBat
      },
      CraftRecipe = new List<RecipeItemDataRaw>
      {
        new()
        {
          ItemName = "Plasma Engine Parts",
          Count = 2
        },
        new()
        {
          ItemName = "AI Controller Circuit",
          Count = 1
        }
      }
    };

    var lothAssembler = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData
      {
        Name = LothAssembler,
        Description = "A turbo charged assembler.",
        StackSizeLimit = 10,
        ItemCategory = ItemCategory.Stations,
        ItemType = "assembler"
      },
      AssemblerConfig = new AssemblerConfig
      {
        CraftSpeedModifier = 3,
        ProductionOutputType = ProductionOutputType.Items
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTime = 1,
        CountWhenCrafted = 1,
        SpawnsOutsideOfInventory = false
      },
      PlaceableConfig = new PlaceableConfig
      {
        Length = 2,
        Width = 2,
        Height = 2,
        PlaceableType = PlaceableType.AssemblyModule
      },
      PowerConfig = new PowerConfig
      {
        BaseMaxPower = 40,
        BaseIdlePower = 5,
        MaxTemp = 102,
        HeatRate = .02f
      },
      FleetConfig = new FleetConfig
      {
        MaxHealth = 300
      },
      InventoryMetaDataConfig = new InventoryMetaDataConfig
      {
        // The secondary index is the index of the inventory that is used for the output of the assembler.
        SecondaryIndexStart = 6,
        SecondaryIndexEnd = 7,
        ConnectorIndexStart = 7,
        ConnectorIndexEnd = 11,
        PrimaryIndexEnd = 6,
        LimitBasedOnFilter = true,
        LimitTypeToOneSlot = true,
        UseConnectorInventory = true,
        InserterDropoffInventory = InventoryType.Connector,
        InserterPickupInventory = InventoryType.Secondary,
        OperationType = InventoryOperationType.Standard
      },
      IconAssetName = LothAssembler,
      RenderingData = new RenderingData
      {
        ModelPath =
          "Assembler" // Since this is the name of an existing model from the game, the mod loader will assign that model to this new entity.
      },
      CraftRecipe = new List<RecipeItemDataRaw>
      {
        new()
        {
          ItemName = "Medium Density Structure",
          Count = 20
        },
        new()
        {
          ItemName = "AI Controller Circuit",
          Count = 5
        },
        new()
        {
          ItemName = "Fabricator",
          Count = 5
        }
      }

    };

    var lothPrinter = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData
      {
        Name = LothPrinter,
        Description = "A turbo charged printer.",
        StackSizeLimit = 10,
        ItemCategory = ItemCategory.Stations,
        ItemType = "assembler"
      },
      AssemblerConfig = new AssemblerConfig
      {
        CraftSpeedModifier = 3,
        ProductionOutputType = ProductionOutputType.Items
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTime = 1,
        CountWhenCrafted = 1,
        SpawnsOutsideOfInventory = false
      },
      PlaceableConfig = new PlaceableConfig
      {
        Length = 2,
        Width = 2,
        Height = 2,
        PlaceableType = PlaceableType.AssemblyModule
      },
      PowerConfig = new PowerConfig
      {
        BaseMaxPower = 40,
        BaseIdlePower = 5,
        MaxTemp = 102,
        HeatRate = .02f
      },
      FleetConfig = new FleetConfig
      {
        MaxHealth = 300
      },
      InventoryMetaDataConfig = new InventoryMetaDataConfig
      {
        // The secondary index is the index of the inventory that is used for the output of the assembler.
        SecondaryIndexStart = 6,
        SecondaryIndexEnd = 7,
        ConnectorIndexStart = 7,
        ConnectorIndexEnd = 11,
        PrimaryIndexEnd = 6,
        LimitBasedOnFilter = true,
        LimitTypeToOneSlot = true,
        UseConnectorInventory = true,
        InserterDropoffInventory = InventoryType.Connector,
        InserterPickupInventory = InventoryType.Secondary,
        OperationType = InventoryOperationType.Standard
      },
      IconAssetName = LothPrinter,
      RenderingData = new RenderingData
      {
        ModelPath =
          LothPrinter // Since this is the name of an existing model from the game, the mod loader will assign that model to this new entity.
      },
      CraftRecipe = new List<RecipeItemDataRaw>
      {
        new()
        {
          ItemName = "Medium Density Structure",
          Count = 20
        },
        new()
        {
          ItemName = "Fabricator",
          Count = 15
        }
      }

    };

    var gherikConnector = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData
      {
        Name = "Connector"
      },
      RenderingData = new RenderingData
      {
        ModelPath = "Connector"
      },
      PlaceableConfig = new PlaceableConfig
      {
        Length = 1,
        Width = 1,
        Height = 1
      },
      IconAssetName = "GConnector"
    };

    return new List<EntityConfig>
    {
      lothBat,
      lothAssembler,
      lothPrinter
    };
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
    itemConfig.GetPrefabForName(LothBat).SetComponent(new KnnFleetVision
    {
      Range = 50
    });

    // Update global config example
    var globalConfig = Ecs.GetSingleton<GlobalConfig>();
    var terrainConfig = globalConfig.Terrain;
    terrainConfig.MinOre = 999999999;
    globalConfig.Terrain = terrainConfig;
    Ecs.SetSingleton(globalConfig);


    // Get existing data and stuff
    var realConnector = itemConfig.GetPrefabForName("Connector");
    var gherikConnector = itemConfig.GetPrefabForName("GherikConnector");
    var gherikId = gherikConnector.GetComponent<AsteroItem>().ConfigIndex;
    var realId = realConnector.GetComponent<AsteroItem>().ConfigIndex;

    var prefabs = itemConfig.ItemPrefabs;

    // Update the gherik prefab to the connector's prefab
    prefabs[gherikId] = realConnector;
    itemConfig.PlaceableConfigLookup[gherikId] = itemConfig.PlaceableConfigLookup[realId];

    // Update the placeable component data from the real connector
    var realPlaceable = realConnector.GetComponent<Placeable>();
    // YOU MUST UPDATE THE ITEM IDENTIFIER HERE OR THE PLACEALBE WILL HAVE THE OLD ITEM ID ON IT 
    realPlaceable.ItemIdentifier = gherikId;
    gherikConnector.SetComponent(realPlaceable);

    // Update power config example
    itemConfig.PowerConfigLookup[gherikId] = itemConfig.PowerConfigLookup[realId];
    var gherikPower = itemConfig.PowerConfigLookup[gherikId];
    gherikPower.BaseIdlePower = 50;
    itemConfig.PowerConfigLookup[gherikId] = gherikPower;
  }

  public void OnGameStart(Canvas inGameUiCanvas)
  {

  }

  public List<TechnologyConfig> AddTechnologies()
  {
    var lothBatTech = new TechnologyConfig
    {
      Name = LothBat,
      Description = "Unlocks bigger, badder, Loth Bats.",
      ItemsUnlocked = new List<string>
      {
        LothBat
      },
      IconNameFromModdedBundle = LothBat,
      Cost = 50,
      Disabled = false,
      Requirements = new List<string>
      {
        "Start Tech",
        "Mining Logistics"
      },
      ResearchRequirements = new List<TechnologyConfig.SpecificResearchRequirement>
      {
        new()
        {
          Name = "Asteroid Research",
          Value = 10
        }
      },
      Rewards = new List<TechnologyConfig.TechnologyRewardFunction>()
    };
    var lothAssemblerTech = new TechnologyConfig
    {
      Name = LothAssembler,
      Description = "Unlocks a more powerful assembler",
      ItemsUnlocked = new List<string>
      {
        LothAssembler,
        LothPrinter
      },
      IconNameFromModdedBundle = LothAssembler,
      Cost = 200,
      Disabled = false,
      Requirements = new List<string>
      {
        "Start Tech",
        "Automation"
      },
      ResearchRequirements = new List<TechnologyConfig.SpecificResearchRequirement>
      {
        new()
        {
          Name = "Planetary Research",
          Value = 200
        }
      },
      Rewards = new List<TechnologyConfig.TechnologyRewardFunction>()
    };

    return new List<TechnologyConfig>
    {
      lothBatTech,
      lothAssemblerTech
    };
  }
}