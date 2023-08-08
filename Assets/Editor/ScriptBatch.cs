using System.IO;
using FFCore.Modding;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ScriptBatch
{
  [MenuItem("Modding/Build X64 Mod (Example)")]
  public static void BuildGame()
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
    var report = BuildPipeline.BuildPlayer(new[] {"Assets/Scenes/SampleScene.unity"},
      Path.Combine(pluginTempFolder, $"{modNamePlaceholder}.exe"), BuildTarget.StandaloneWindows64, BuildOptions.Development);

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

      // Copy Burst library
      Debug.Log("Copying Burst Library...");
      var burstedSrc = Path.Combine(pluginTempFolder, $"{modNamePlaceholder}_Data/Plugins/x86_64/lib_burst_generated.dll");
      var burstedDest = Path.Combine(modFolder, $"{modInfo.ID}_win_x86_64.dll");
      FileUtil.CopyFileOrDirectory(burstedSrc, burstedDest);
      
      // Validate the mod after installation
      Debug.Log("Validating mod...");
      IUserMod buildModInfo = ModLoader.LoadIUserModForDll(managedSrc);
      Debug.Log($"Successfully Loaded and validated mod: {buildModInfo.FullName} by {buildModInfo.Author}");
      
      // Create Manifest File
      Debug.Log("Creating manifest file...");
      var manifestFile = Path.Combine(modFolder, "manifest.properties");
      WriteToManifestFile(manifestFile, buildModInfo);
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
  private static void WriteToManifestFile(string manifestFile, IUserMod modInfo)
  {
    using (StreamWriter file = new StreamWriter(manifestFile))
    {
      file.WriteLine($"ID={modInfo.ID}");
      file.WriteLine($"FullName={modInfo.FullName}");
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