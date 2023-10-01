using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFCore.Modding;
using FFCore.Version;
using Unity.Entities.Build;
using Unity.Entities.Content;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ScriptBatch
{
  [MenuItem("Modding/Build X64 Mod")]
  public static void BuildGameWithBurst()
  {
    BuildGameImpl(false);
  }

  public static void BuildGameImpl(bool isBurstCompilationEnabled)
  {
    var modNamePlaceholder = "FFMod";

    var projectFolder = Path.Combine(Application.dataPath, "..");
    var pluginTempFolder = Path.Combine(projectFolder, "PluginTemp");
    var buildFolder = Path.Combine(projectFolder, "build");

    Directory.Delete(pluginTempFolder, true);
    if (Directory.Exists(pluginTempFolder) || File.Exists(pluginTempFolder))
    {
      throw new BuildFailedException("PluginTemp folder is in use.");
    }

    Directory.CreateDirectory(pluginTempFolder);

    // Build player.
    Debug.Log("Building mod...");

    var report = BuildPipeline.BuildPlayer(new[]
      {
        "Assets/Scenes/ModScene.unity"
      }, Path.Combine(pluginTempFolder, $"{modNamePlaceholder}.exe"), BuildTarget.StandaloneWindows64,
      BuildOptions.Development);

    if (report.summary.result == BuildResult.Succeeded)
    {
      Debug.Log("Mod compile successful.");
      //Do basic validation on to ensure foldername is correct, then load the modname from the mod
      var managedSrc = Path.Combine(pluginTempFolder, $"{modNamePlaceholder}_Data/Managed/{modNamePlaceholder}.dll");
      IUserMod modInfo = ModLoader.LoadIUserModForDll(managedSrc);
      IUserMod.ValidateModID(modInfo.ID);
      Debug.Log($"Successfully Loaded and validated mod: {modInfo.ID} by {modInfo.Author}");
      var modFolder = Path.Combine(buildFolder, $"{modInfo.ID}");
      if (Directory.Exists(modFolder) || File.Exists(modFolder))
      {
        Debug.Log("Deleting existing mod folder...");
        Directory.Delete(modFolder, true);
        if (Directory.Exists(modFolder) || File.Exists(modFolder))
        {
          throw new BuildFailedException($"Couldn't update build output.  Is it currently in use?");
        }
      }

      Debug.Log("Creating empty mod folder...");
      Directory.CreateDirectory(modFolder);

      // Copy Managed library
      Debug.Log("Copying Managed Library...");
      var managedDest = Path.Combine(modFolder, $"{modInfo.ID}.dll");
      FileUtil.CopyFileOrDirectory(managedSrc, managedDest);

      string srcBurstDll = $"{modNamePlaceholder}_Data/Plugins/x86_64/lib_burst_generated.dll";
      // Copy Burst library
      if (File.Exists(srcBurstDll))
      {
        Debug.Log("Copying Burst Library...");

        var burstedSrc = Path.Combine(pluginTempFolder, srcBurstDll);
        var burstedDest = Path.Combine(modFolder, $"{modInfo.ID}_win_x86_64.dll");
        FileUtil.CopyFileOrDirectory(burstedSrc, burstedDest);
      }

      // Validate the mod after installation
      Debug.Log("Validating mod...");
      IUserMod buildModInfo = ModLoader.LoadIUserModForDll(managedSrc);
      Debug.Log($"Successfully Loaded and validated mod: {buildModInfo.FullName} by {buildModInfo.Author}");

      var sceneGuid = ExecuteContentUpdate(Path.Combine(modFolder, "Content"));

      // Create Manifest File
      Debug.Log("Creating manifest file...");
      var manifestFile = Path.Combine(modFolder, "manifest.properties");
      WriteToManifestFile(manifestFile, buildModInfo, sceneGuid);
      Debug.Log($"Build complete for mod: {modInfo.FullName}");

      //TODO: Need to copy in Preview.png file
      //TODO: And validate that it's < 1MB in size
    }
    else
    {
      Debug.Log("Build failed.");
      throw new BuildFailedException($"Could not build mod due to error:{report.summary}");
    }
  }

  //TODO: This should get moved into FFCore, probably in IUserMod (along with a parsing routine)
  private static void WriteToManifestFile(string manifestFile, IUserMod modInfo, string subsceneGuid)
  {
    using (StreamWriter file = new StreamWriter(manifestFile))
    {
      file.WriteLine($"ID={modInfo.ID}");
      file.WriteLine($"FullName={modInfo.FullName}");
      file.WriteLine($"Description={modInfo.Description}");
      file.WriteLine($"Author={modInfo.Author}");
      file.WriteLine($"EmailContact={modInfo.EmailContact}");
      file.WriteLine($"Website={modInfo.Website}");
      file.WriteLine($"SubsceneGuid={subsceneGuid}");
      for (int x = 0; x < modInfo.Dependencies.Length; x++)
      {
        file.WriteLine($"Dependency{x}={modInfo.Dependencies[x]}");
      }
      file.WriteLine($"ModVersion={modInfo.ModVersion}");
      //TODO: Need to get the version from somewhere in the main FinalFactory game
      file.WriteLine($"GameVersion={FFVersion.FinalFactoryVersion.ToString()}");
    }
  }
  public static void PublishExistingBuild(string buildFolder, string targetFolder)
  {
    Debug.Log("Publishing build...");
    var streamingAssetsPath = $"{buildFolder}/FFMod_Data/StreamingAssets";
    //the content sets are defined by the functor passed in here.  
    RemoteContentCatalogBuildUtility.PublishContent(streamingAssetsPath, targetFolder, f => new string[]
    {
      "all"
    }, true);
    Debug.Log($"Publish complete to path {targetFolder}");
  }

  [MenuItem("Modding/Content Update (fixed)")]
  public static string ExecuteContentUpdate(string publishPath)
  {
    Debug.Log("Creating content update...");
    string buildFolder = Path.Combine(Application.streamingAssetsPath, "ContentBuild");
    if (!string.IsNullOrEmpty(buildFolder))
    {
      var buildTarget = EditorUserBuildSettings.activeBuildTarget;

      if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),
            $"Library/ContentUpdateBuildDir")))
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath),
          $"Library/ContentUpdateBuildDir"));
      if (!Directory.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),
            $"Library/ContentUpdateBuildDir/{PlayerSettings.productName}")))
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath),
          $"Library/ContentUpdateBuildDir/{PlayerSettings.productName}"));

      var tmpBuildFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath),
        $"Library/ContentUpdateBuildDir/{PlayerSettings.productName}");

      var instance = DotsGlobalSettings.Instance;
      var playerGuid = instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client
        ? instance.GetClientGUID()
        : instance.GetServerGUID();
      if (!playerGuid.IsValid) throw new Exception("Invalid Player GUID");

      var subSceneGuids = new HashSet<Unity.Entities.Hash128>();
      for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
      {
        var ssGuids = EditorEntityScenes.GetSubScenes(EditorBuildSettings.scenes[i].guid);
        foreach (var ss in ssGuids)
        {
          if (!subSceneGuids.Contains(ss))
          {
            Debug.Log("GUID -> " + ss);
            subSceneGuids.Add(ss);
          }
        }
      }
      if (subSceneGuids.Count == 0)
      {
        throw new Exception("No subscenes found");
      }
      var subscene = subSceneGuids.First();
      RemoteContentCatalogBuildUtility.BuildContent(subSceneGuids, playerGuid, buildTarget, tmpBuildFolder);
        
      RemoteContentCatalogBuildUtility.PublishContent(tmpBuildFolder, publishPath, f => new string[]
      {
        "all"
      });
      Debug.Log($"Publish Content Update complete to path {publishPath}");
      return subscene.ToString();
    }
    throw new Exception("No build folder found");
  }
}