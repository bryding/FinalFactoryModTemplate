using System.IO;
using FFCore.Modding;
using FFCore.Version;
using Microsoft.CSharp.RuntimeBinder;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Editor
{
  public static class ScriptBatch
  {
    [MenuItem("Modding/Build X64 Mod")]
    public static void BuildGameWithBurst()
    {
      BuildGameImpl(false, false);
    }

    [MenuItem("Modding/Build and Install")]
    public static void BuildGameAndInstallLocal()
    {
      BuildGameImpl(false, true);
    }

    public static void BuildGameImpl(bool isBurstCompilationEnabled, bool installLocally)
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

        CreateConfigBundle(Path.Combine(modFolder, "AssetBundle"));

        // Create Manifest File
        Debug.Log("Creating manifest file...");
        var manifestFile = Path.Combine(modFolder, "manifest.properties");
        WriteToManifestFile(manifestFile, buildModInfo);
        Debug.Log($"Compile complete for mod: {modInfo.FullName}");
        
        // Setup Preview.png
        Debug.Log("Finding and Validating Preview file...");
        string previewFile = Path.Combine(projectFolder, "Preview.gif");
        if (!File.Exists(previewFile))
        {
          previewFile = Path.Combine(projectFolder, "Preview.png");
          if (!File.Exists(previewFile))
          {
            throw new BuildFailedException(
              $"Could not build mod due to error: No Preview file available.  Please define a Preview.png or Preview.gif in the root directory. (e.g. {previewFile})");
          }
        }
        
        var size = new FileInfo(previewFile).Length;
        Debug.Log($"Found Preview file of size:{size}");
        //Steam has a file size limitation for the preview file of 1MB.  If too large, fail with a descriptive error
        if (size > 1000000)
        {
          throw new BuildFailedException($"Could not build mod due to error: Preview file ({previewFile}) is larger than 1MB.");
        }
        
        //Copy the Preview.png and ensure the new case sensitivity is correct (Capital P, lowercase rest)
        //Windows is case insensitive, so the actual file might not match the exact case, so we fix it with the copy.
        if (previewFile.EndsWith(".png"))
        {
          Debug.Log("Copying png preview file...");
          File.Copy(previewFile, Path.Combine(modFolder, "Preview.png"));
        }
        else
        {
          Debug.Log("Copying gif preview file...");
          File.Copy(previewFile, Path.Combine(modFolder, "Preview.gif"));
        }

        if (installLocally)
        {
          Debug.Log("Installing mod locally...");
          var dataPath = Application.persistentDataPath;
          var destination = dataPath.Replace("DefaultCompany/FFModTemplate", Path.Combine("Never Games/finalfactory/mods", modInfo.ID));
          var source = modFolder;
          CopyDirectory(source, destination);
        }
        
        Debug.Log("Build Successful.");
      }
      else
      {
        Debug.Log("Build failed.");
        throw new BuildFailedException($"Could not build mod due to error:{report.summary}");
      }
    }

    /// <summary>
    /// Updates prefab names to their guid, then creates an asset bundle with the prefabs.
    /// </summary>
    /// <param name="path"></param>
    private static void CreateConfigBundle(string path)
    {
      if (!Directory.Exists(path))
      {
        Directory.CreateDirectory(path);
      }

      Debug.Log($"Creating asset bundle in path {path}...");
      BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }

    //TODO: This should get moved into FFCore, probably in IUserMod (along with a parsing routine)
    private static void WriteToManifestFile(string manifestFile, IUserMod modInfo)
    {
      using var file = new StreamWriter(manifestFile);
      file.WriteLine($"ID={modInfo.ID}");
      file.WriteLine($"FullName={modInfo.FullName}");
      file.WriteLine($"Description={modInfo.Description}");
      file.WriteLine($"Author={modInfo.Author}");
      file.WriteLine($"EmailContact={modInfo.EmailContact}");
      file.WriteLine($"Website={modInfo.Website}");
      for (int x = 0; x < modInfo.Dependencies.Length; x++)
      {
        file.WriteLine($"Dependency{x}={modInfo.Dependencies[x]}");
      }
      file.WriteLine($"ModVersion={modInfo.ModVersion}");
      //TODO: Need to get the version from somewhere in the main FinalFactory game
      file.WriteLine($"GameVersion={FFVersion.FinalFactoryVersion.ToString()}");
    }
    public static void CopyDirectory(string sourceDir, string destDir)
    {
      // If the destination directory doesn't exist, create it
      if (!Directory.Exists(destDir))
        Directory.CreateDirectory(destDir);

      // Get all files in the source directory and copy them to the destination directory
      foreach (string file in Directory.GetFiles(sourceDir))
      {
        string destFile = Path.Combine(destDir, Path.GetFileName(file));
        File.Copy(file, destFile, true);
      }

      // Recursively copy subdirectories
      foreach (string directory in Directory.GetDirectories(sourceDir))
      {
        string destDirectory = Path.Combine(destDir, Path.GetFileName(directory));
        CopyDirectory(directory, destDirectory);
      }
    }
    
  }
}