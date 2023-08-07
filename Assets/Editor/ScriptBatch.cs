using System.IO;
using Codice.CM.Common.Merge;
using FFCore.Modding;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ScriptBatch
{
  [MenuItem("Modding/Build X64 Mod (Example)")]
  public static void BuildGame()
  {
    var modName = "FFMod";

    var projectFolder = Path.Combine(Application.dataPath, "..");
    var buildFolder = Path.Combine(projectFolder, "PluginTemp");

    // Get filename.
    var path = EditorUtility.SaveFolderPanel("Choose Final Mod Location", "build", "");

    FileUtil.DeleteFileOrDirectory(buildFolder);
    Directory.CreateDirectory(buildFolder);

    // Build player.
    var report = BuildPipeline.BuildPlayer(new[] {"Assets/Scenes/SampleScene.unity"},
      Path.Combine(buildFolder, $"{modName}.exe"), BuildTarget.StandaloneWindows64, BuildOptions.Development);

    if (report.summary.result == BuildResult.Succeeded)
    {
      // Copy Managed library
      var managedDest = Path.Combine(path, $"{modName}.dll");
      var managedSrc = Path.Combine(buildFolder, $"{modName}_Data/Managed/{modName}.dll");
      FileUtil.DeleteFileOrDirectory(managedDest);
      if (!File.Exists(managedDest)) // Managed side not unloaded
      {
        FileUtil.CopyFileOrDirectory(managedSrc, managedDest);
      }
      else
      {
        Debug.LogWarning($"Couldn't update manged dll, {managedDest} is it currently in use?");
      }

      // Copy Burst library
      var burstedDest = Path.Combine(path, $"{modName}_win_x86_64.dll");
      var burstedSrc = Path.Combine(buildFolder, $"{modName}_Data/Plugins/x86_64/lib_burst_generated.dll");
      FileUtil.DeleteFileOrDirectory(burstedDest);
      if (!File.Exists(burstedDest))
      {
        FileUtil.CopyFileOrDirectory(burstedSrc, burstedDest);
      }
      else
      {
        Debug.LogWarning($"Couldn't update bursted dll, {burstedDest} is it currently in use?");
      }
      
      // Create Manifest File
      IUserMod modInfo = ModLoader.LoadIUserModForDll(managedSrc);
      Debug.Log($"Mod Manifest loaded successfully for mod: {modInfo.Name} by {modInfo.Author}");
      var manifestFile = Path.Combine(buildFolder, "manifest.properties");
      WriteToManifestFile(manifestFile, modInfo);
    }
  }

  private static void WriteToManifestFile(string manifestFile, IUserMod modInfo)
  {
    using (StreamWriter file = new StreamWriter(manifestFile))
    {
      file.WriteLine($"Name={modInfo.Name}");
      file.WriteLine($"Description={modInfo.Description}");
      file.WriteLine($"Author={modInfo.Author}");
      file.WriteLine($"EmailContact={modInfo.EmailContact}");
      file.WriteLine($"Website={modInfo.Website}");
      for(int x=0;x<modInfo.Dependencies.Length;x++)
      {
        file.WriteLine($"Dependency{x}={modInfo.Dependencies[x]}");
      }
      file.WriteLine($"ModVersion={modInfo.ModVersion}");
      file.WriteLine($"GameVersion={Application.version}");
    }
  }
}