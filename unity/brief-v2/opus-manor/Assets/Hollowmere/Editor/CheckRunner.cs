using System;
using System.IO;
using Hollowmere.Checks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hollowmere.EditorTools
{
    /// <summary>
    /// The one entry point for the checks: opens the saved scene from disk, writes Map/map.json from it, then
    /// enters play mode, where Hollowmere.Checks.CheckSuite runs every check and walk and writes Checks/results.txt
    /// before leaving play mode.
    /// </summary>
    public static class CheckRunner
    {
        [MenuItem("Hollowmere/Run All Checks")]
        public static void RunAllMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "busy: already in play mode";
            EditorSceneManager.OpenScene(LevelBuilder.ScenePath, OpenSceneMode.Single);
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ExportMap();
            Directory.CreateDirectory(Path.Combine(root, "Checks"));
            File.WriteAllText(CheckSuite.ResultFile, $"RUNNING since {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            Directory.CreateDirectory(Path.Combine(root, "Temp"));
            File.WriteAllText(CheckSuite.RequestFile, DateTime.Now.ToString("o"));
            EditorApplication.isPlaying = true;
            return "started: the scene is in play mode; Checks/results.txt is written when the run ends";
        }

        /// <summary>Writes Map/map.json from the open scene (which RunAll has just loaded from disk).</summary>
        [MenuItem("Hollowmere/Export Map")]
        public static string ExportMap()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Directory.CreateDirectory(Path.Combine(root, "Map"));
            string path = Path.Combine(root, "Map", "map.json");
            File.WriteAllText(path, LevelSurvey.MapJson());
            return path;
        }
    }
}
