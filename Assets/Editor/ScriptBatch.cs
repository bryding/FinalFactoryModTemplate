using UnityEditor;
using System.IO;
using UnityEngine;
using Directory = System.IO.Directory;
using File = System.IO.File;

public class ScriptBatch
{
  [MenuItem("Modding/Build X64 Mod (Example)")]
  public static void BuildGame()
  {
    string modName = "FFMod";

    string projectFolder = Path.Combine(Application.dataPath, "..");
    string buildFolder = Path.Combine(projectFolder, "PluginTemp");

    // Get filename.
    string path = EditorUtility.SaveFolderPanel("Choose Final Mod Location", "", "");

    FileUtil.DeleteFileOrDirectory(buildFolder);
    Directory.CreateDirectory(buildFolder);

    // Build player.
    var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/SampleScene.unity" }, Path.Combine(buildFolder, $"{modName}.exe"), BuildTarget.StandaloneWindows64, BuildOptions.Development);

    if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
    {
      // Copy Managed library
      var managedDest = Path.Combine(path, $"{modName}.dll");
      var managedSrc = Path.Combine(buildFolder, $"{modName}_Data/Managed/{modName}.dll");
      FileUtil.DeleteFileOrDirectory(managedDest);
      if (!File.Exists(managedDest))  // Managed side not unloaded
        FileUtil.CopyFileOrDirectory(managedSrc, managedDest);
      else
        Debug.LogWarning($"Couldn't update manged dll, {managedDest} is it currently in use?");

      // Copy Burst library
      var burstedDest = Path.Combine(path, $"{modName}_win_x86_64.dll");
      var burstedSrc = Path.Combine(buildFolder, $"{modName}_Data/Plugins/x86_64/lib_burst_generated.dll");
      FileUtil.DeleteFileOrDirectory(burstedDest);
      if (!File.Exists(burstedDest))
        FileUtil.CopyFileOrDirectory(burstedSrc, burstedDest);
      else
        Debug.LogWarning($"Couldn't update bursted dll, {burstedDest} is it currently in use?");
    }
  }
}