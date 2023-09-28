using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using FFComponents.Initialization;
using FFComponents.Map;
using FFCore.Config;
using FFCore.Extensions;
using FFCore.Serialization;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Behaviours
{
  public class StartController : MonoBehaviour
  {
    
    public static List<ComponentType> GetAllComponentTypes(bool includeUnsavedTypes)
    {
      var types = new List<ComponentType>();
      types.Add(typeof(Parent));
      types.Add(typeof(LocalTransform));

      foreach (var type in TypeManager.AllTypes)
      {
        if (type.Category is not (TypeManager.TypeCategory.ComponentData or TypeManager.TypeCategory.BufferData))
        {
          continue;
        }

        if (!includeUnsavedTypes && type.Type?.GetCustomAttribute(typeof(Save)) == null) continue;

        var compType = ComponentType.FromTypeIndex(type.TypeIndex);
        types.Add(compType);
      }

      return types;
    }
    
    private void Update()
    {
      if (!Ecs.TryGetSingletonEntity<EntityPrefabContainer>(out var entityContainer) ||
          Ecs.TryGetSingletonEntity<FFGrid>(out _))
      {
        return;
      }

      try
      {
        Debug.Log("Performing Config Initialization");

        var managedItemConfigs = EntityPrefabContainerBaker.LoadManagedItemConfigs();
        
        for (int index = 0; index < managedItemConfigs.Count; ++index)
        {
          FFItemConfig ffItemConfig = managedItemConfigs[index];
          var temp = ffItemConfig.EntityPrefab;
        }

        var itemPrefabs = entityContainer.GetBuffer<FfItemEntityPrefab>();
        var connectorItemPrefabs = entityContainer.GetBuffer<ConnectorItemEntityPrefab>();

        var itemPrefabArray = itemPrefabs.ToReinterpretedArray<FfItemEntityPrefab, Entity>();
        
        foreach (Entity entity in itemPrefabArray)
        {
          //This is a MarshalledEntity from the save
          //public int Identifier;
          //public AsteroEntityType EntityType;
          //public Dictionary<Type, byte[]> Components = new();
          
          Debug.Log($"Loaded Entity:{entity.ToString()}");
          //byte[] byteArray = ObjectToByteArray(entity);
        }

        Debug.Log("System Start Controller finished loading...");
      }
      catch (Exception e)
      {
        Debug.LogError("Something went wrong with game initialization in the Start Controller. Contact dev.");
        Debug.LogException(e);
      }
      finally
      {
        enabled = false;
      }
    }
    
    // Convert an object to a byte array
    public static byte[] ObjectToByteArray(Entity obj)
    {
      BinaryFormatter bf = new BinaryFormatter();
      using (var ms = new MemoryStream())
      {
        bf.Serialize(ms, obj);
        return ms.ToArray();
      }
    }
    
    // Convert a byte array to an Object
    public static Entity ByteArrayToObject(byte[] arrBytes)
    {
      using (var memStream = new MemoryStream())
      {
        var binForm = new BinaryFormatter();
        memStream.Write(arrBytes, 0, arrBytes.Length);
        memStream.Seek(0, SeekOrigin.Begin);
        var obj = binForm.Deserialize(memStream);
        return (Entity)obj;
      }
    }
  }
}