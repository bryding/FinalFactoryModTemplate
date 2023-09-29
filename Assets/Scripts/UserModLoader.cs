﻿using System;
using System.Collections.Generic;
using EntityManagement;
using FFCore.Abilities;
using FFCore.Config;
using FFCore.Extensions;
using FFCore.Fleet;
using FFCore.GlobalConfig;
using FFCore.Items;
using FFCore.Modding;
using UnityEngine;

public class UserModLoader : UserModLoaderMono, IUserModLoader
{
  // Add any assets you want to reference here (or another script) so you can reference them in your item configs
  public GameObject LothBatPrefab;
  
  public override List<EntityConfig> DefineEntityConfigs()
  {
    var config = new EntityConfig
    {
      Prefab = LothBatPrefab,
      EntityReferenceGuid = Guid.NewGuid(),
      ItemConfig = new AsteroItemConfigData // Astero was the old name of the game, so you might see it sprinkled about
      {
        Name = "Loth Bat",
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

  public override void PostInitializationHook()
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