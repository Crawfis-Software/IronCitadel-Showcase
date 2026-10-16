// IronCitadel conformance kit v2: the batch entry point and the menu item.
//
//   unity run <project> --timeout 900 --no-tail -- -executeMethod IronCitadel.ConformanceV2.ConformanceCli.Run
//       -scene Assets/Scenes/IronCitadel.unity -map C:/path/map.json -out C:/path/scores/x [-noWalk]
//       -logFile C:/path/scores/x/unity.log
//
// Writes <out>/conformance.json, conformance.md and conformance-map.png. The scene is opened, scored and
// never saved. On an error it writes <out>/conformance-error.txt and exits 2.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronCitadel.ConformanceV2
{
    public static class ConformanceCli
    {
        public static void Run()
        {
            string scenePath = Arg("-scene"), mapPath = Arg("-map"), outDir = Arg("-out");
            try
            {
                if (scenePath == null || mapPath == null || outDir == null)
                    throw new ArgumentException("usage: -executeMethod IronCitadel.ConformanceV2.ConformanceCli.Run -scene <Assets/...unity> -map <map.json> -out <dir> [-noWalk]");
                var scene = EditorSceneManager.OpenScene(ToProjectPath(scenePath), OpenSceneMode.Single);
                ScoreScene(scene, mapPath, outDir, !HasArg("-noWalk"));
            }
            catch (Exception e)
            {
                Fail(outDir, e);
            }
        }

        public static void Fail(string outDir, Exception e)
        {
            Debug.LogError("[ConformanceV2] FAILED: " + e);
            try
            {
                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);
                    File.WriteAllText(Path.Combine(outDir, "conformance-error.txt"), e.ToString());
                }
            }
            catch { }
            EditorApplication.Exit(2);
        }

        /// <summary>Scores an open scene and writes the three report files. Does not save or reload the scene.</summary>
        public static JObj ScoreScene(Scene scene, string mapPath, string outDir, bool walk)
        {
            var map = MapSpec.Load(mapPath);
            var scorer = new Scorer(map, scene, walk);
            var report = scorer.Score();
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "conformance.json"), MiniJson.Write(report));
            File.WriteAllText(Path.Combine(outDir, "conformance.md"), ReportWriter.Markdown(report, scorer));
            ReportWriter.Map(Path.Combine(outDir, "conformance-map.png"), scorer);
            var sum = (JObj)report["summary"];
            Debug.Log("[ConformanceV2] " + scene.path + ": " + sum["pass"] + " pass, " + sum["fail"] + " fail, " + sum["manual"] +
                      " manual -> " + Path.GetFullPath(outDir));
            return report;
        }

        [MenuItem("Tools/Iron Citadel/Score Open Scene (brief v2)...")]
        static void Menu()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Iron Citadel conformance v2", "Save the scene first: it is reloaded from disk after scoring.", "OK");
                return;
            }
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if (scene.isDirty)
            {
                EditorUtility.DisplayDialog("Iron Citadel conformance v2",
                    "The scene has unsaved changes. Save or revert them first: the scorer reloads the scene from disk afterwards.", "OK");
                return;
            }
            var lastMap = EditorPrefs.GetString("IronCitadel.ConformanceV2.Map", "");
            var mapPath = EditorUtility.OpenFilePanel("map.json", string.IsNullOrEmpty(lastMap) ? "" : Path.GetDirectoryName(lastMap), "json");
            if (string.IsNullOrEmpty(mapPath)) return;
            EditorPrefs.SetString("IronCitadel.ConformanceV2.Map", mapPath);
            var defOut = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ConformanceReports", scene.name + "-v2"));
            var outDir = EditorUtility.SaveFolderPanel("Folder for conformance.json / .md", Path.GetDirectoryName(defOut), scene.name + "-v2");
            if (string.IsNullOrEmpty(outDir)) return;
            var path = scene.path;
            try
            {
                EditorUtility.DisplayProgressBar("Iron Citadel conformance v2", "Baking and scoring " + scene.name + "...", 0.5f);
                ScoreScene(scene, mapPath, outDir, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Iron Citadel conformance v2", "Scoring failed: " + e.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // reload from disk: nothing the run switched off or added survives
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            EditorUtility.RevealInFinder(Path.Combine(outDir, "conformance.md"));
        }

        // ---------------------------------------------------------------------------------------------

        public static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        public static bool HasArg(string name)
        {
            foreach (var s in Environment.GetCommandLineArgs())
                if (string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Accepts "Assets/..." or an absolute path inside this project.</summary>
        public static string ToProjectPath(string p)
        {
            p = p.Replace('\\', '/');
            if (p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)) return p;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/') + "/";
            var full = Path.GetFullPath(p).Replace('\\', '/');
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return full.Substring(root.Length);
            throw new ArgumentException("the scene must be inside this project: " + p);
        }
    }
}
