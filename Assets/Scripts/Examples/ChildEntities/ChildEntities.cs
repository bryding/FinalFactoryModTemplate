using System.Collections.Generic;
using FFCore.Extensions;
using Unity.Entities;
using UnityEngine;

namespace Examples.ChildEntities
{
  /// <summary>
  ///   Example (template issue #8): finding the children of your custom entities in <c>PostInitializationHook</c>.
  ///   <para>
  ///     A modded item's prefab is an entity, and its model parts are child entities. Their names are not kept in a built
  ///     player, so you cannot look a child up by the name it has in Unity. What does survive is the entity's
  ///     <see cref="LinkedEntityGroup" /> buffer (the root first, then every descendant) and the components each child
  ///     carries. Find a child by a component only it has, and change it on the PREFAB so every instance the game makes later
  ///     has the change, on every peer.
  ///   </para>
  /// </summary>
  public static class ChildEntities
  {
    /// <summary>
    ///   Every descendant of the prefab (the prefab itself left out), in the order of its <c>LinkedEntityGroup</c>.
    ///   Empty when the prefab has no children.
    /// </summary>
    public static List<Entity> Children(Entity prefab)
    {
      var result = new List<Entity>();
      var entityManager = Ecs.EntityManager;
      if (!entityManager.HasBuffer<LinkedEntityGroup>(prefab))
      {
        return result;
      }

      foreach (var linked in entityManager.GetBuffer<LinkedEntityGroup>(prefab))
      {
        if (linked.Value != prefab)
        {
          result.Add(linked.Value);
        }
      }

      return result;
    }

    /// <summary>The children of the prefab that carry a component of type <typeparamref name="T" />.</summary>
    public static List<Entity> ChildrenWith<T>(Entity prefab) where T : unmanaged, IComponentData
    {
      var entityManager = Ecs.EntityManager;
      var result = new List<Entity>();
      foreach (var child in Children(prefab))
      {
        if (entityManager.HasComponent<T>(child))
        {
          result.Add(child);
        }
      }

      return result;
    }

    /// <summary>
    ///   Writes one line per child to the log: its position in the group and the component types it carries. Run it once
    ///   on your prefab to see what to look for, then use <see cref="ChildrenWith{T}" /> with a component only the child
    ///   you want has.
    /// </summary>
    public static void LogChildren(string label, Entity prefab)
    {
      var entityManager = Ecs.EntityManager;
      var children = Children(prefab);
      Debug.Log($"ChildEntities: '{label}' has {children.Count} child entities.");
      for (var i = 0; i < children.Count; i++)
      {
        using var types = entityManager.GetComponentTypes(children[i]);
        var names = new List<string>();
        foreach (var type in types)
        {
          names.Add(type.GetManagedType()?.Name ?? "?");
        }

        Debug.Log($"ChildEntities: '{label}' child {i}: {string.Join(", ", names)}");
      }
    }
  }
}
