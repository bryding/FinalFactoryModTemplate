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
    public UserModLoaderMono UserModLoader;
  }

  public abstract class UserModLoaderMono : MonoBehaviour, IUserModLoader
  {
    public abstract List<EntityConfig> DefineEntityConfigs();
    public abstract void PostInitializationHook();
  }

  public class ModdedEntityPrefabBaker : Baker<ModdedEntityPrefabBakerAuthoring>
  {
    public override void Bake(ModdedEntityPrefabBakerAuthoring authoring)
    {
      Debug.Log("Baking modded entities...");
      var configs = authoring.UserModLoader.DefineEntityConfigs();
  
      var entityMetaData = AddBuffer<EntityMetaDataElement>(GetEntity(TransformUsageFlags.Dynamic));
  
      foreach (var config in configs)
      {
        var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
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