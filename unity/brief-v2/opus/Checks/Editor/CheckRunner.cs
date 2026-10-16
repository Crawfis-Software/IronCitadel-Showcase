using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronCitadel.Checks
{
    /// <summary>
    /// The one entry point: RunAll() (menu Tools/Iron Citadel/Run all checks) re-opens the saved scene from disk,
    /// writes Map/map.json from it, runs the static checks, then enters play mode and runs every walk with the
    /// shipped player, leaves play mode and writes Checks/results.txt. It returns at once; the run takes a few
    /// minutes and finishes when results.txt carries the line "END OF RUN <id>" (also logged to the console).
    /// </summary>
    [InitializeOnLoad]
    public static class CheckRunner
    {
        public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
        public const string ResultsPath = "Checks/results.txt";
        const string PhaseKey = "IronCitadel.Checks.Phase", RunKey = "IronCitadel.Checks.Run";
        static readonly string StaticPath = Path.Combine("Library", "IronCitadelChecks_static.txt");
        static readonly string WalksPath = Path.Combine("Library", "IronCitadelChecks_walks.txt");

        static CheckRunner()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Poll;
        }

        static string Phase { get => SessionState.GetString(PhaseKey, ""); set => SessionState.SetString(PhaseKey, value); }

        [MenuItem("Tools/Iron Citadel/Run all checks")]
        public static void RunAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[IronCitadel.Checks] leave play mode first"); return; }
            string run = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            SessionState.SetString(RunKey, run);
            var lines = new List<string>();
            lines.Add($"Iron Citadel checks, run {run}");
            lines.Add($"scene: {ScenePath} ({new FileInfo(ScenePath).Length} bytes, saved {File.GetLastWriteTime(ScenePath):yyyy-MM-dd HH:mm:ss}, sha1 {Sha1(ScenePath)})");
            lines.Add($"unity {Application.unityVersion}");
            lines.Add("");
            // the saved scene, fresh from disk
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            try
            {
                var map = MapExport.Write("Map/map.json");
                lines.Add($"MAP written from the saved scene: {map} ({new FileInfo(map).Length} bytes, sha1 {Sha1(map)})");
                lines.Add("");
                lines.Add("STATIC CHECKS (measured on the saved scene)");
                var st = StaticChecks.Run(out int pass, out int fail);
                lines.AddRange(st);
                lines.Add($"static checks: {pass} passed, {fail} failed");
            }
            catch (Exception e) { lines.Add("STATIC CHECKS CRASHED: " + e); }
            File.WriteAllLines(StaticPath, lines);
            // back to the saved scene before playing, so the walks run on exactly what is on disk
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Phase = "walks";
            EditorApplication.EnterPlaymode();
        }

        /// The static checks alone, written to Checks/results_static.txt (for quick iteration; not the deliverable).
        [MenuItem("Tools/Iron Citadel/Run static checks only")]
        public static void RunStaticOnly()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var st = StaticChecks.Run(out int pass, out int fail);
            st.Add($"static checks: {pass} passed, {fail} failed");
            File.WriteAllLines("Library/IronCitadelChecks_static_only.txt", st);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && Phase == "walks")
            {
                var go = new GameObject("WalkRunner (checks)");
                go.AddComponent<WalkRunner>();
            }
            if (s == PlayModeStateChange.EnteredEditMode && Phase == "exit")
            {
                Phase = "";
                Finish();
            }
        }

        static void Poll()
        {
            if (Phase != "walks" || !EditorApplication.isPlaying) return;
            var w = WalkRunner.Instance;
            if (w == null || !w.Done) return;
            var lines = new List<string> { "", "WALKS (play mode, one continuous session with the shipped player prefab from the start position)" };
            lines.AddRange(w.Lines);
            File.WriteAllLines(WalksPath, lines);
            WalkRunner.Instance = null;
            Phase = "exit";
            EditorApplication.ExitPlaymode();
        }

        static void Finish()
        {
            var lines = new List<string>();
            if (File.Exists(StaticPath)) lines.AddRange(File.ReadAllLines(StaticPath));
            if (File.Exists(WalksPath)) lines.AddRange(File.ReadAllLines(WalksPath));
            int fails = lines.Count(l => l.StartsWith("FAIL"));
            int passes = lines.Count(l => l.StartsWith("PASS"));
            lines.Add("");
            lines.Add($"SUMMARY: {passes} passed, {fails} failed" + (fails == 0 ? " — all checks and walks passed" : ""));
            lines.Add($"END OF RUN {SessionState.GetString(RunKey, "?")}");
            Directory.CreateDirectory("Checks");
            File.WriteAllLines(ResultsPath, lines);
            Debug.Log($"[IronCitadel.Checks] wrote {ResultsPath}: {passes} passed, {fails} failed");
        }

        static string Sha1(string path)
        {
            using (var s = SHA1.Create()) return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }
    }
}
