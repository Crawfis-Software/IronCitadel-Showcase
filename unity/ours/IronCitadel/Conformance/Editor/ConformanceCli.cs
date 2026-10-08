// IronCitadel conformance kit: the batch entry point and the menu item.
//
//   unity run <project> -- -executeMethod IronCitadel.Conformance.ConformanceCli.Run
//       -scene Assets/Scenes/IronCitadel.unity -level C:/path/level.json -out C:/path/scores/x [-noWalk]
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

namespace IronCitadel.Conformance
{
    public static class ConformanceCli
    {
        public static void Run()
        {
            string scenePath = Arg("-scene"), levelPath = Arg("-level"), outDir = Arg("-out");
            try
            {
                if (scenePath == null || levelPath == null || outDir == null)
                    throw new ArgumentException("usage: -executeMethod IronCitadel.Conformance.ConformanceCli.Run -scene <Assets/...unity> -level <level.json> -out <dir> [-noWalk]");
                var scene = EditorSceneManager.OpenScene(ToProjectPath(scenePath), OpenSceneMode.Single);
                ScoreScene(scene, levelPath, outDir, !HasArg("-noWalk"));
            }
            catch (Exception e)
            {
                Debug.LogError("[Conformance] FAILED: " + e);
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
        }

        /// <summary>Scores an open scene and writes the three report files. Does not save or reload the scene.</summary>
        public static JObj ScoreScene(Scene scene, string levelPath, string outDir, bool walk)
        {
            var level = LevelSpec.Load(levelPath);
            var scorer = new Scorer(level, scene, walk);
            var report = scorer.Score(out var model);
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "conformance.json"), MiniJson.Write(report));
            File.WriteAllText(Path.Combine(outDir, "conformance.md"), ReportWriter.Markdown(report, scorer, level));
            ReportWriter.Map(Path.Combine(outDir, "conformance-map.png"), level, model, scorer);
            var sum = (JObj)report["summary"];
            Debug.Log("[Conformance] " + scene.path + ": " + sum["pass"] + " pass, " + sum["fail"] + " fail, " + sum["manual"] +
                      " manual -> " + Path.GetFullPath(outDir));
            return report;
        }

        [MenuItem("Tools/Iron Citadel/Score Open Scene...")]
        static void Menu()
        {
            var scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Iron Citadel conformance", "Save the scene first: it is reloaded from disk after scoring.", "OK");
                return;
            }
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            if (scene.isDirty)
            {
                EditorUtility.DisplayDialog("Iron Citadel conformance",
                    "The scene has unsaved changes. Save or revert them first: the scorer reloads the scene from disk afterwards.", "OK");
                return;
            }
            var lastLevel = EditorPrefs.GetString("IronCitadel.Conformance.Level", "");
            var level = EditorUtility.OpenFilePanel("level.json", string.IsNullOrEmpty(lastLevel) ? "" : Path.GetDirectoryName(lastLevel), "json");
            if (string.IsNullOrEmpty(level)) return;
            EditorPrefs.SetString("IronCitadel.Conformance.Level", level);
            var defOut = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ConformanceReports", scene.name));
            var outDir = EditorUtility.SaveFolderPanel("Folder for conformance.json / .md", Path.GetDirectoryName(defOut), scene.name);
            if (string.IsNullOrEmpty(outDir)) return;
            var path = scene.path;
            try
            {
                EditorUtility.DisplayProgressBar("Iron Citadel conformance", "Baking and scoring " + scene.name + "...", 0.5f);
                ScoreScene(scene, level, outDir, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Iron Citadel conformance", "Scoring failed: " + e.Message, "OK");
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
