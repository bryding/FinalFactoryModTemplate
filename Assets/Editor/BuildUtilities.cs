using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities.Build;
using Unity.Entities.Content;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEngine;

namespace Editor
{
  public static class BuildUtilities
  {
    [MenuItem("Modding/Publish Existing Build")]
    // prepares the content files for publish.  The original files can be deleted or retained during this process by changing the last parameter of the PublishContent call.
    public static void PublishExistingBuild()
    {
      var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish",
        Path.GetDirectoryName(Application.dataPath), "Builds");
      if (!string.IsNullOrEmpty(buildFolder))
      {
        var streamingAssetsPath = $"{buildFolder}/{PlayerSettings.productName}_Data/StreamingAssets";
        //the content sets are defined by the functor passed in here.  
        RemoteContentCatalogBuildUtility.PublishContent(streamingAssetsPath, $"{buildFolder}-RemoteContent", f =>
          new string[]
          {
            "all"
          }, true);
        Debug.Log($"Publish complete to path {streamingAssetsPath}");
      }
    }

    [MenuItem("Modding/Create Content Update")]
    // This method is somewhat complicated because it will build the scenes from a player build but without fully building the player.
    public static void CreateContentUpdate()
    {
      var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish",
        Path.GetDirectoryName(Application.dataPath), "Builds");
      if (!string.IsNullOrEmpty(buildFolder))
      {
        var buildTarget = EditorUserBuildSettings.activeBuildTarget;
        var tmpBuildFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath),
          $"/Library/ContentUpdateBuildDir/{PlayerSettings.productName}");

        var instance = DotsGlobalSettings.Instance;
        var playerGuid = instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client
          ? instance.GetClientGUID()
          : instance.GetServerGUID();
        if (!playerGuid.IsValid) throw new Exception("Invalid Player GUID");

        var subSceneGuids = new HashSet<Unity.Entities.Hash128>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
          var ssGuids = EditorEntityScenes.GetSubScenes(EditorBuildSettings.scenes[i].guid);
          foreach (var ss in ssGuids) subSceneGuids.Add(ss);
        }
        RemoteContentCatalogBuildUtility.BuildContent(subSceneGuids, playerGuid, buildTarget, tmpBuildFolder);

        var publishFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds",
          $"{buildFolder}-RemoteContent");
        RemoteContentCatalogBuildUtility.PublishContent(tmpBuildFolder, publishFolder, f => new string[]
        {
          "all"
        });
        
        Debug.Log($"Publish Content Update complete to path {publishFolder}");
      }
    }
    
  }
}