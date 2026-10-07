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
using Examples.RepairBeacon;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using Utils;
public class UserModLoader : IUserModLoader
{
  private const string LothBat = "Loth Bat";
  private const string LothAssembler = "Loth Assembler";
  private const string LothPrinter = "Loth Printer";
  private const string GherikConnector = "GherikConnector";

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
        BaseCraftTimeFP = 1,
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
        CraftSpeedModifierFP = 3,
        ProductionOutputType = ProductionOutputType.Items
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTimeFP = 1,
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
        MaxTempFP = 102,
        HeatRateFP = fp.one / 50
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
          "Assembler" // The name of an existing game item: the mod loader makes this item a copy of the game's Assembler entity (its components too), then applies the configs above.
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
        CraftSpeedModifierFP = 3,
        ProductionOutputType = ProductionOutputType.Items
      },
      CraftConfig = new CraftConfigData
      {
        BaseCraftTimeFP = 1,
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
        MaxTempFP = 102,
        HeatRateFP = fp.one / 50
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
          LothPrinter // Not a game item, so the mod loader builds the entity from this mod's own prefab of that name (Assets/Resources/ItemEntities/Loth Printer.prefab).
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

    // A minimal placeable that starts life as a clone of the game's Connector. The interesting
    // part happens in PostInitializationHook below, where its prefab and configs are copied from
    // the real Connector — a useful pattern when your modded item is a variant of an existing one.
    var gherikConnector = new EntityConfig
    {
      ItemConfig = new AsteroItemConfigData
      {
        Name = GherikConnector,
        Description = "A Connector variant cloned from the real thing in PostInitializationHook.",
        StackSizeLimit = 50,
        ItemCategory = ItemCategory.Stations
      },
      RenderingData = new RenderingData
      {
        // The name of an existing game item: the mod loader makes this item a copy of that item's whole
        // entity (its behaviour components too, not only the model).
        ModelPath = "Connector"
      },
      PlaceableConfig = new PlaceableConfig
      {
        Length = 1,
        Width = 1,
        Height = 1
      },
      // Icons come only from this mod's own icon bundle (Assets/Resources/Icons): the game looks
      // IconAssetName up there and nowhere else, so a game icon's name ("Connector") does not work.
      // This reuses the Loth Assembler's icon; drop your own PNG in Assets/Resources/Icons for a new look.
      IconAssetName = LothAssembler
    };

    return new List<EntityConfig>
    {
      lothBat,
      lothAssembler,
      lothPrinter,
      gherikConnector
    };
  }

  public void PostInitializationHook()
  {
    // Multiplayer-safe example (Assets/Scripts/Examples/RepairBeacon): the Loth Printer also works as a
    // repair beacon. The component goes on the PREFAB here, so every printer built has it, on every peer.
    // The player action that switches it is registered here too: PostInitializationHook runs once at startup
    // on every peer, before any game starts, which is what a network action's applier needs.
    Ecs.GetSingleton<ItemConfig>().GetPrefabForName(LothPrinter).AddAndSetComponent(new RepairBeacon
    {
      Enabled = true,
      Range = 300,
      HealPerSecond = 5
    });
    RepairBeaconActions.Register();

    // Update a single item's config example
    var itemConfig = Ecs.GetSingleton<ItemConfig>();
    var terrainConfigs = itemConfig.TerrainConfigs;
    var bauxiteId = itemConfig.GetIdForName("Bauxite Asteroid");
    var bauxiteIdAsteroidConfig = terrainConfigs[bauxiteId];
    bauxiteIdAsteroidConfig.OreSpawnMultiplierFP = 100;
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

  private static RepairBeaconPanel _repairBeaconPanel;

  public void OnGameStart(Canvas inGameUiCanvas)
  {
    // Called for every new or loaded game. Build the example's UI panel once per canvas.
    if (_repairBeaconPanel == null || _repairBeaconPanel.transform.parent != inGameUiCanvas.transform)
    {
      _repairBeaconPanel = RepairBeaconPanel.Create(inGameUiCanvas);
      var driver = new GameObject("RepairBeaconPanelDriver").AddComponent<RepairBeaconPanelDriver>();
      driver.Panel = _repairBeaconPanel;
    }
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
          ValueFp = (fp)10
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
          ValueFp = (fp)200
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