using System;
using System.Collections.Generic;
using FFComponents.Modding;
using FFCore.Config;
using FFCore.Modding;
using Unity.Entities;
using UnityEngine;

namespace EntityManagement
{
  public class ModdedEntityPrefabBakerAuthoring : MonoBehaviour
  {
    // Add any assets you want to reference here (or another script) so you can reference them in your item configs
    public List<GameObject> Prefabs;
  }

  public class ModdedEntityPrefabBaker : Baker<ModdedEntityPrefabBakerAuthoring>
  {
    public override void Bake(ModdedEntityPrefabBakerAuthoring authoring)
    {
      Debug.Log("Baking modded entities...");
      var loader = (UserModLoader)Activator.CreateInstance(typeof(UserModLoader));

      var configs = loader.DefineEntityConfigs();
  
      var entityMetaData = AddBuffer<EntityMetaDataElement>(GetEntity(TransformUsageFlags.Dynamic));

      for (var index = 0; index < configs.Count; index++)
      {
        var config = configs[index];
        var entity = GetEntity(authoring.Prefabs[index], TransformUsageFlags.Dynamic);
        var entityMetaDataElement = new EntityMetaDataElement
        {
          Entity = entity,
          Guid = config.EntityReferenceGuid.ToString()
        };

        entityMetaData.Add(entityMetaDataElement);
      }
    }
  }
}