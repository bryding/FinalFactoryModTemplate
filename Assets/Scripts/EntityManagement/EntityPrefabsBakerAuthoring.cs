using System;
using System.Collections.Generic;
using FFComponents.Modding;
using Unity.Entities;
using Unity.Entities.Content;
using UnityEngine;

namespace EntityManagement
{
  public class ModdedEntityPrefabBakerAuthoring : MonoBehaviour
  {
    // Add any assets you want to reference here (or another script) so you can reference them in your item configs
    public List<GameObject> Prefabs;
    public WeakObjectSceneReference Scene;
  }
  
  public struct SceneComponentData : IComponentData
  {
    public WeakObjectSceneReference Scene;
  }

  public class ModdedEntityPrefabBaker : Baker<ModdedEntityPrefabBakerAuthoring>
  {
    public override void Bake(ModdedEntityPrefabBakerAuthoring authoring)
    {
      Debug.Log("Baking modded entities...");
      var loader = (UserModLoader)Activator.CreateInstance(typeof(UserModLoader));
      var me = GetEntity(TransformUsageFlags.Dynamic);
      AddComponent(me, new SceneComponentData { Scene = authoring.Scene });
      var configs = loader.DefineEntityConfigs();

      var entityMetaData = AddBuffer<EntityMetaDataElement>(me);

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