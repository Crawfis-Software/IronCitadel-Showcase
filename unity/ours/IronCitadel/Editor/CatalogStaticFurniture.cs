using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>The static pass for the room catalogue (Roger: walls, floors, ceilings and furniture static; clutter
    /// not). The synthesized room prefabs already save Floor / Walls / Ceiling / Wall props static; this makes every
    /// GameObject named exactly "Furniture" static too, it and every descendant, inactive ones included
    /// (<c>isStatic = true</c>, every StaticEditorFlag, as PrefabSynthesis does: on a nested prefab instance's child it
    /// is an override on the instance). Nothing under a "Clutter" is ever touched. A prefab is saved only when a flag
    /// changed, in place, so its GUID and file IDs stay.
    ///
    /// <para>Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Rooms.Editor.CatalogStaticFurniture.Run
    /// [-root Assets/CrawfisSoftware/AllSyntyDungeons/Rooms] [-dry]</c>. Writes per-theme counts (the first folder under
    /// the root) to <see cref="ResultPath"/>. Exits 0, 2 when some prefab failed, 1 on an exception.
    /// Menu: Iron Citadel/Static Furniture.</para></summary>
    public static class CatalogStaticFurniture
    {
        public const string DefaultRoot = "Assets/CrawfisSoftware/AllSyntyDungeons/Rooms";
        public const string ResultPath = "C:/Repos/IronCitadel/logs/static-furniture-result.txt";
        public const string FurnitureName = "Furniture";
        public const string ClutterName = "Clutter";
        const int Chunk = 100;

        sealed class Tally { public int prefabs, changed, groups, objects, noFurniture, failed, saveNotOk; }

        [MenuItem("Iron Citadel/Static Furniture/Make furniture static (all rooms)")]
        public static void RunMenu()
        {
            if (!EditorUtility.DisplayDialog("Static furniture",
                    $"Make every Furniture group static in every prefab under\n{DefaultRoot}?\n\nThousands of prefabs; this takes a while.", "Run", "Cancel"))
                return;
            int code = RunCore(DefaultRoot, false, true);
            Debug.Log($"[CatalogStaticFurniture] done, code {code}; {ResultPath}");
        }

        [MenuItem("Iron Citadel/Static Furniture/Dry run (count only)")]
        public static void DryMenu()
        {
            int code = RunCore(DefaultRoot, true, true);
            Debug.Log($"[CatalogStaticFurniture] dry run done, code {code}; {ResultPath}");
        }

        /// <summary>-executeMethod entry: [-root folder] [-dry].</summary>
        public static void Run()
        {
            int code;
            try
            {
                var args = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(args, "-root");
                string root = i >= 0 && i + 1 < args.Length ? args[i + 1].Replace('\\', '/').TrimEnd('/') : DefaultRoot;
                bool dry = Array.IndexOf(args, "-dry") >= 0;
                code = RunCore(root, dry, !Application.isBatchMode);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log($"[CatalogStaticFurniture] exit {code}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static int RunCore(string root, bool dry, bool progress)
        {
            var started = DateTime.Now;
            var log = new StringBuilder();
            log.AppendLine($"CatalogStaticFurniture {started:yyyy-MM-dd HH:mm:ss}");
            log.AppendLine($"root: {root}{(dry ? "   (DRY RUN: nothing saved; 'changed' = would change)" : "")}");
            var byTheme = new SortedDictionary<string, Tally>(StringComparer.Ordinal);
            var failures = new List<string>();
            var notOk = new List<string>();
            int code = 0, total = 0;
            try
            {
                if (!AssetDatabase.IsValidFolder(root)) throw new DirectoryNotFoundException("not a project folder: " + root);
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { root })
                    .Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(p => p, StringComparer.Ordinal)
                    .ToList();
                total = paths.Count;
                log.AppendLine($"{total} prefab(s), chunks of {Chunk}");
                for (int start = 0; start < paths.Count; start += Chunk)
                {
                    int end = Math.Min(start + Chunk, paths.Count);
                    if (!dry) AssetDatabase.StartAssetEditing();
                    try
                    {
                        for (int k = start; k < end; k++)
                        {
                            string path = paths[k];
                            if (progress && EditorUtility.DisplayCancelableProgressBar("Static furniture",
                                    $"{k + 1} / {paths.Count}  {Path.GetFileName(path)}", (k + 1f) / paths.Count))
                                throw new OperationCanceledException($"cancelled at {k} / {paths.Count}");
                            var t = TallyFor(byTheme, root, path);
                            t.prefabs++;
                            try
                            {
                                if (!ProcessOne(path, dry, t)) notOk.Add(path);
                            }
                            catch (Exception e)
                            {
                                t.failed++;
                                failures.Add($"{path}: {e.GetType().Name}: {e.Message}");
                            }
                        }
                    }
                    finally
                    {
                        if (!dry) AssetDatabase.StopAssetEditing();
                    }
                    EditorUtility.UnloadUnusedAssetsImmediate();
                    GC.Collect();
                    Debug.Log($"[CatalogStaticFurniture] {end} / {paths.Count}");
                }
                if (!dry) AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                code = 1;
                log.AppendLine("ERROR " + e);
            }
            finally
            {
                if (progress) EditorUtility.ClearProgressBar();
            }
            if (code == 0 && failures.Count > 0) code = 2;

            var sum = new Tally();
            log.AppendLine();
            log.AppendLine($"{"theme",-18} {"prefabs",8} {(dry ? "would" : "saved"),8} {"furn.grp",9} {"objects",9} {"no-furn",8} {"failed",7}");
            foreach (var kv in byTheme)
            {
                var t = kv.Value;
                log.AppendLine($"{kv.Key,-18} {t.prefabs,8} {t.changed,8} {t.groups,9} {t.objects,9} {t.noFurniture,8} {t.failed,7}");
                sum.prefabs += t.prefabs; sum.changed += t.changed; sum.groups += t.groups; sum.objects += t.objects;
                sum.noFurniture += t.noFurniture; sum.failed += t.failed; sum.saveNotOk += t.saveNotOk;
            }
            log.AppendLine($"{"TOTAL",-18} {sum.prefabs,8} {sum.changed,8} {sum.groups,9} {sum.objects,9} {sum.noFurniture,8} {sum.failed,7}");
            log.AppendLine($"(objects = GameObjects whose static flags changed; of {total} found)");
            if (notOk.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"SaveAsPrefabAsset reported success = false for {notOk.Count} prefab(s) (the file may still have been written; check them):");
                foreach (var p in notOk) log.AppendLine("  " + p);
            }
            if (failures.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"FAILED {failures.Count}:");
                foreach (var f in failures) log.AppendLine("  " + f);
            }
            log.AppendLine();
            log.AppendLine($"took {(DateTime.Now - started).TotalMinutes:0.0} min; RESULT {(code == 0 ? "ok" : code == 2 ? "some prefabs failed" : "error")}");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, log.ToString());
            Debug.Log(log.ToString());
            return code;
        }

        static Tally TallyFor(SortedDictionary<string, Tally> byTheme, string root, string path)
        {
            string rel = path.StartsWith(root + "/", StringComparison.Ordinal) ? path.Substring(root.Length + 1) : path;
            int slash = rel.IndexOf('/');
            string theme = slash > 0 ? rel.Substring(0, slash) : "(root)";
            if (!byTheme.TryGetValue(theme, out var t)) byTheme[theme] = t = new Tally();
            return t;
        }

        /// <summary>One prefab: flag every Furniture group and its descendants static; save when something changed.
        /// False only when SaveAsPrefabAsset reported failure.</summary>
        static bool ProcessOne(string path, bool dry, Tally t)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var seen = new HashSet<GameObject>();
                int groups = 0, flipped = 0;
                foreach (var f in contents.GetComponentsInChildren<Transform>(true))
                {
                    if (f.name != FurnitureName || UnderClutter(f, contents.transform)) continue;
                    groups++;
                    foreach (var d in f.GetComponentsInChildren<Transform>(true))
                    {
                        var go = d.gameObject;
                        if (!seen.Add(go) || UnderClutter(d, f)) continue;
                        var before = GameObjectUtility.GetStaticEditorFlags(go);
                        go.isStatic = true;
                        if (GameObjectUtility.GetStaticEditorFlags(go) != before) flipped++;
                    }
                }
                t.groups += groups;
                t.objects += flipped;
                if (groups == 0) t.noFurniture++;
                if (flipped == 0) return true;
                t.changed++;
                if (dry) return true;
                // inside StartAssetEditing the import is deferred to StopAssetEditing, so the returned asset can be
                // null; the out flag is what says whether the file was written
                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool ok);
                if (!ok) t.saveNotOk++;
                return ok;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>True when <paramref name="t"/> or an ancestor up to <paramref name="stop"/> is named Clutter.</summary>
        static bool UnderClutter(Transform t, Transform stop)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == ClutterName) return true;
                if (p == stop) break;
            }
            return false;
        }
    }
}
