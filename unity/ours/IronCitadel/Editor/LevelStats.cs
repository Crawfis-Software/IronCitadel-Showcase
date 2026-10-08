// Iron Citadel level stats: one probe, the same measurement for every build of the level.
//
//   unity run <project> --timeout 1800 --no-tail -- -executeMethod LevelStatsProbe.LevelStats.Run
//       -scene Assets/.../X.unity -out C:/path/stats-x.json [-label x] -logFile C:/path/x.log
//   (-scene, -out and -label may each take several ';'-separated values to measure several scenes in one session)
//
// Opens the scene (never saves it) and walks every GameObject and every active Renderer. Writes one JSON file.
// On an error it writes <out>.error.txt and exits 2. Uses only UnityEngine and UnityEditor (URP's Volume and
// post-processing volumes are found by type name), so it compiles in any project.
//
// Definitions (the same for every scene):
//   active renderer   Renderer.enabled and its GameObject activeInHierarchy.
//   prefab chain      for a renderer that is part of a prefab instance: the asset path at each nesting level, from
//                     PrefabUtility.GetCorrespondingObjectFromSource applied repeatedly to the Renderer component
//                     (falls back to its GameObject if the renderer is an added component). chain[0] is the
//                     outermost prefab placed in the scene, chain[last] the original source.
//   leaf source       PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer)'s asset path (a .prefab or a
//                     model file); for a renderer outside any prefab, its mesh's asset path.
//   leaf instance     one placed copy of a leaf source: the nearest prefab instance root above the renderer (or the
//                     renderer itself when it is in no prefab).
//   asset category    from the asset path (see Category below).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LevelStatsProbe
{
    public static class LevelStats
    {
        public static void Run()
        {
            // -scene, -out and -label may each list several values, separated by ';', measured in turn in one session.
            string sceneArg = Arg("-scene"), outArg = Arg("-out"), labelArg = Arg("-label");
            string outPath = null;
            try
            {
                if (sceneArg == null || outArg == null)
                    throw new ArgumentException("usage: -executeMethod LevelStatsProbe.LevelStats.Run -scene <Assets/...unity>[;...] -out <file.json>[;...] [-label x[;...]]");
                string[] scenes = sceneArg.Split(';'), outs = outArg.Split(';'), labels = (labelArg ?? "").Split(';');
                if (outs.Length != scenes.Length) throw new ArgumentException("-out needs one path per -scene");
                for (int i = 0; i < scenes.Length; i++)
                {
                    outPath = outs[i];
                    var scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                    var report = Measure(scene, i < labels.Length ? labels[i] : "");
                    string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(outPath, Json.Write(report));
                    Debug.Log("[LevelStats] " + scene.path + " -> " + Path.GetFullPath(outPath));
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[LevelStats] FAILED: " + e);
                try { if (outPath != null) File.WriteAllText(outPath + ".error.txt", e.ToString()); } catch { }
                EditorApplication.Exit(2);
            }
        }

        [MenuItem("Tools/Iron Citadel/Level Stats (open scene)")]
        static void Menu()
        {
            var scene = SceneManager.GetActiveScene();
            string outPath = EditorUtility.SaveFilePanel("Level stats", "", "stats-" + scene.name + ".json", "json");
            if (string.IsNullOrEmpty(outPath)) return;
            File.WriteAllText(outPath, Json.Write(Measure(scene, scene.name)));
            Debug.Log("[LevelStats] -> " + outPath);
        }

        // ------------------------------------------------------------------ categories

        /// <summary>The source pack or kind of an asset, from its path. "" (no asset) means the object lives in the scene.</summary>
        public static string Category(string path)
        {
            if (string.IsNullOrEmpty(path)) return "scene-embedded (no asset)";
            string p = path.Replace('\\', '/');
            if (p == "Library/unity default resources" || p == "Resources/unity_builtin_extra" || p.StartsWith("Packages/com.unity.", StringComparison.Ordinal))
                return "Unity built-in";
            if (p.StartsWith("Packages/", StringComparison.Ordinal)) return "other package";
            if (p.StartsWith("Assets/CrawfisSoftware/", StringComparison.Ordinal))
            {
                if (p.Contains("/Rooms/")) return "CrawfisSoftware rooms";
                if (p.Contains("/SetPieces/")) return "CrawfisSoftware set pieces";
                if (p.Contains("/Levels/")) return "CrawfisSoftware levels";
                if (p.Contains("/Patterns/")) return "CrawfisSoftware patterns";
                return "CrawfisSoftware other";
            }
            if (p.Contains("/Synty/PolygonDungeon/")) return "Synty PolygonDungeon";
            if (p.Contains("/Synty/PolygonDungeonRealms/")) return "Synty PolygonDungeonRealms";
            if (p.Contains("/Synty/PolygonDungeonMap/")) return "Synty PolygonDungeonMap";
            if (p.Contains("/Synty/")) return "other Synty";
            if (p.IndexOf("KayKit", StringComparison.OrdinalIgnoreCase) >= 0) return "KayKit";
            if (p.Contains("/_THIRD_PARTY/")) return "other third-party pack";
            if (p.StartsWith("Assets/Starter Assets/", StringComparison.Ordinal) || p.StartsWith("Assets/Interact/", StringComparison.Ordinal) ||
                p.StartsWith("Assets/IronCitadel/Walker/", StringComparison.Ordinal))
                return "player kit (Starter Assets / walker)";
            if (p.StartsWith("Assets/MacroTileBuilder/", StringComparison.Ordinal)) return "MacroTileBuilder";
            if (p.StartsWith("Assets/IronCitadel/", StringComparison.Ordinal) || p.StartsWith("Assets/Scenes/", StringComparison.Ordinal))
                return "project's own (generated / authored for this level)";
            return "other project asset";
        }

        static readonly Dictionary<string, bool> modelCache = new Dictionary<string, bool>();

        static bool IsModel(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!modelCache.TryGetValue(path, out bool m)) modelCache[path] = m = AssetImporter.GetAtPath(path) is ModelImporter;
            return m;
        }

        // ------------------------------------------------------------------ measurement

        sealed class Tally
        {
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public void Add(string key, int n = 1) { Counts.TryGetValue(key, out int c); Counts[key] = c + n; }
        }

        sealed class AssetRow
        {
            public string Key, Path, Category; public int Renderers, Instances; public long Tris; public bool Model;
            public HashSet<Object> InstanceSet = new HashSet<Object>();
        }

        public static Dictionary<string, object> Measure(Scene scene, string label)
        {
            var R = new Dictionary<string, object>();
            R["label"] = label;
            R["scene"] = scene.path;
            R["unity"] = Application.unityVersion;
            R["project"] = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            R["measuredUtc"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            string sceneFile = Path.Combine(Path.GetDirectoryName(Application.dataPath), scene.path);
            R["sceneFileBytes"] = File.Exists(sceneFile) ? new FileInfo(sceneFile).Length : -1L;

            // ---- every GameObject
            var all = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) all.Add(t.gameObject);
            var active = all.Where(g => g.activeInHierarchy).ToList();
            int maxDepth = 0;
            foreach (var g in active) { int d = 0; for (var t = g.transform.parent; t != null; t = t.parent) d++; maxDepth = Math.Max(maxDepth, d); }
            int staticBatch = 0, staticGI = 0, inPrefab = 0, missingPrefab = 0;
            foreach (var g in active)
            {
                var f = GameObjectUtility.GetStaticEditorFlags(g);
                if ((f & StaticEditorFlags.BatchingStatic) != 0) staticBatch++;
                if ((f & StaticEditorFlags.ContributeGI) != 0) staticGI++;
                if (PrefabUtility.IsPartOfPrefabInstance(g)) inPrefab++;
                if (PrefabUtility.IsPrefabAssetMissing(g)) missingPrefab++;
            }
            R["gameObjects"] = new Dictionary<string, object>
            {
                ["total"] = all.Count, ["active"] = active.Count, ["roots"] = scene.rootCount, ["maxHierarchyDepth"] = maxDepth,
                ["activeInPrefabInstances"] = inPrefab, ["activeOutsidePrefabs"] = active.Count - inPrefab,
                ["activeMissingPrefabAsset"] = missingPrefab, ["batchingStatic"] = staticBatch, ["contributeGI"] = staticGI,
            };

            // ---- outermost prefab instances placed in the scene (active roots)
            var outermost = new Tally();
            var outermostInactive = 0;
            foreach (var g in all)
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(g)) continue;
                if (!g.activeInHierarchy) { outermostInactive++; continue; }
                outermost.Add(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) ?? "");
            }

            // ---- prefab assets at any nesting depth, from every active GameObject's chain
            var anyDepth = new Tally();      // asset path -> active GameObjects whose chain includes it
            var anyDepthRoots = new Tally(); // asset path -> instance roots (any depth) of that asset
            foreach (var g in active)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(g)) continue;
                foreach (var p in Chain(g).Distinct()) anyDepth.Add(p);
                if (PrefabUtility.IsAnyPrefabInstanceRoot(g))
                {
                    // the asset this root is an instance of: walk the sources until one is the root of its asset
                    var c = Chain(g);
                    string asset = c.Count > 0 ? c[0] : "";
                    UnityEngine.Object o = g;
                    for (int i = 0; i < 32; i++)
                    {
                        var s = PrefabUtility.GetCorrespondingObjectFromSource(o);
                        if (s == null) break;
                        var sg = s as GameObject;
                        if (sg != null && sg.transform.parent == null) { asset = AssetDatabase.GetAssetPath(s); break; }
                        o = s;
                    }
                    anyDepthRoots.Add(asset);
                }
            }

            // ---- renderers
            var renderers = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects()) renderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
            var activeR = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();

            // renderers that LOD groups show only at LOD1+
            var lodHigher = new HashSet<Renderer>();
            int lodGroups = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (!lg.gameObject.activeInHierarchy) continue;
                    lodGroups++;
                    var lods = lg.GetLODs();
                    for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) lodHigher.Add(r);
                }

            var byType = new Tally();
            var leaf = new Dictionary<string, AssetRow>();
            var innermostPrefab = new Dictionary<string, AssetRow>();
            var outermostR = new Dictionary<string, AssetRow>();
            var meshes = new Dictionary<Object, AssetRow>();
            var mats = new Dictionary<Object, AssetRow>();
            var chainDepth = new Tally();
            var perDepthUnique = new Dictionary<int, HashSet<string>>();
            long tris = 0, trisLod0 = 0;
            int inPrefabR = 0, outsideR = 0, noMesh = 0, missingMesh = 0, addedRendererOverride = 0;
            var outsideByCat = new Tally();
            var primitiveR = new Tally();
            int sceneMeshSeq = 0, sceneMatSeq = 0;

            foreach (var r in activeR)
            {
                byType.Add(r.GetType().Name);
                Mesh mesh = null;
                bool meshCapable = false;
                if (r is SkinnedMeshRenderer smr) { mesh = smr.sharedMesh; meshCapable = true; }
                else if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) { mesh = mf.sharedMesh; meshCapable = true; }
                }
                long t = mesh != null ? Triangles(mesh) : 0;
                tris += t;
                if (!lodHigher.Contains(r)) trisLod0 += t;
                if (mesh == null) { if (meshCapable) missingMesh++; else noMesh++; }

                string meshPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : "";
                AssetRow meshRow = null;
                if (mesh != null)
                {
                    if (!meshes.TryGetValue(mesh, out meshRow))
                    {
                        string key = string.IsNullOrEmpty(meshPath) ? "(scene mesh #" + (++sceneMeshSeq) + ") " + mesh.name : meshPath + "::" + mesh.name;
                        meshRow = new AssetRow { Key = key, Path = meshPath, Category = Category(meshPath), Tris = t, Model = IsModel(meshPath) };
                        meshes[mesh] = meshRow;
                    }
                    meshRow.Renderers++;
                    if (meshPath == "Library/unity default resources") primitiveR.Add(mesh.name);
                }

                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (!mats.TryGetValue(m, out var row))
                    {
                        string mp = AssetDatabase.GetAssetPath(m);
                        string key = string.IsNullOrEmpty(mp) ? "(scene material #" + (++sceneMatSeq) + ") " + m.name : mp + "::" + m.name;
                        row = new AssetRow { Key = key, Path = mp, Category = Category(mp) };
                        mats[m] = row;
                    }
                    row.Renderers++;
                }

                bool partOfPrefab = PrefabUtility.IsPartOfPrefabInstance(r.gameObject);
                string leafPath, leafKey;
                Object instanceKey;
                if (partOfPrefab)
                {
                    inPrefabR++;
                    var chain = Chain(r);
                    if (chain.Count == 0) { addedRendererOverride++; chain = Chain(r.gameObject); }
                    chainDepth.Add(chain.Count.ToString(CultureInfo.InvariantCulture));
                    for (int i = 0; i < chain.Count; i++)
                    {
                        if (!perDepthUnique.TryGetValue(i, out var set)) perDepthUnique[i] = set = new HashSet<string>();
                        set.Add(chain[i]);
                    }
                    var orig = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r);
                    leafPath = orig != null ? AssetDatabase.GetAssetPath(orig) : (chain.Count > 0 ? chain[chain.Count - 1] : "");
                    leafKey = leafPath;
                    instanceKey = (Object)PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject) ?? r;

                    string inner = chain.LastOrDefault(p => !IsModel(p) && p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
                    if (inner != null) Bump(innermostPrefab, inner, r.gameObject, t);
                    string outer = chain.Count > 0 ? chain[0] : "";
                    var outerRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                    Bump(outermostR, outer, outerRoot != null ? (Object)outerRoot : r, t);
                }
                else
                {
                    outsideR++;
                    leafPath = meshPath;
                    leafKey = mesh == null ? "(no mesh: " + r.GetType().Name + ")" : (meshRow != null ? meshRow.Key : meshPath);
                    instanceKey = r;
                    outsideByCat.Add(mesh == null ? "no mesh (" + r.GetType().Name + ")" : Category(meshPath));
                }
                if (!leaf.TryGetValue(leafKey, out var lrow))
                    leaf[leafKey] = lrow = new AssetRow { Key = leafKey, Path = leafPath, Category = mesh == null && !partOfPrefab ? "no mesh" : Category(leafPath), Model = IsModel(leafPath) };
                lrow.Renderers++;
                lrow.Tris += t;
                lrow.InstanceSet.Add(instanceKey);
            }
            foreach (var row in leaf.Values) row.Instances = row.InstanceSet.Count;

            // textures, from the shader-declared texture properties of every material on an active renderer
            var texes = new Dictionary<Object, AssetRow>();
            int sceneTexSeq = 0;
            foreach (var kv in mats)
            {
                var m = (Material)kv.Key;
                var sh = m.shader;
                if (sh == null) continue;
                for (int i = 0; i < sh.GetPropertyCount(); i++)
                {
                    if (sh.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                    var tex = m.GetTexture(sh.GetPropertyNameId(i));
                    if (tex == null) continue;
                    if (!texes.TryGetValue(tex, out var row))
                    {
                        string tp = AssetDatabase.GetAssetPath(tex);
                        string key = string.IsNullOrEmpty(tp) ? "(scene texture #" + (++sceneTexSeq) + ") " + tex.name : tp + "::" + tex.name;
                        texes[tex] = row = new AssetRow { Key = key, Path = tp, Category = Category(tp) };
                    }
                    row.Renderers += kv.Value.Renderers;
                }
            }

            R["renderers"] = new Dictionary<string, object>
            {
                ["total"] = renderers.Count, ["active"] = activeR.Count, ["byType"] = Sorted(byType),
                ["inPrefabInstances"] = inPrefabR, ["outsideAnyPrefab"] = outsideR, ["outsideAnyPrefabByMeshCategory"] = Sorted(outsideByCat),
                ["addedRendererOverrides"] = addedRendererOverride,
                ["withoutMesh"] = noMesh, ["meshFilterWithoutMesh"] = missingMesh,
                ["builtinPrimitiveRenderers"] = Sorted(primitiveR),
                ["lodGroups"] = lodGroups, ["renderersAtLod1Plus"] = activeR.Count(lodHigher.Contains),
                ["prefabChainDepth"] = Sorted(chainDepth),
            };
            R["triangles"] = new Dictionary<string, object>
            {
                ["sumOverActiveRenderers"] = tris, ["sumExcludingLod1Plus"] = trisLod0,
                ["sumOverUniqueMeshes"] = meshes.Values.Sum(m => m.Tris),
            };

            R["prefabs"] = new Dictionary<string, object>
            {
                ["outermostInstances"] = outermost.Counts.Values.Sum(),
                ["outermostInactiveInstances"] = outermostInactive,
                ["outermostUnique"] = outermost.Counts.Count,
                ["outermost"] = Sorted(outermost),
                ["outermostWithRenderersUnique"] = outermostR.Count,
                ["outermostWithRenderers"] = Rows(outermostR.Values, withTris: true),
                ["anyDepthUnique"] = anyDepth.Counts.Count,
                ["anyDepthUniquePrefabFiles"] = anyDepth.Counts.Keys.Count(k => !IsModel(k)),
                ["anyDepthGameObjects"] = Sorted(anyDepth),
                ["instanceRootsAnyDepth"] = anyDepthRoots.Counts.Values.Sum(),
                ["instanceRootsAnyDepthByAsset"] = Sorted(anyDepthRoots),
                ["uniqueByChainDepth"] = perDepthUnique.OrderBy(k => k.Key).ToDictionary(k => k.Key.ToString(CultureInfo.InvariantCulture), k => (object)k.Value.Count),
                ["innermostPrefabFileUnique"] = innermostPrefab.Count,
                ["innermostPrefabFile"] = Rows(innermostPrefab.Values, withTris: true),
            };
            R["leafSources"] = new Dictionary<string, object>
            {
                ["unique"] = leaf.Count,
                ["uniqueModels"] = leaf.Values.Count(l => l.Model),
                ["uniquePrefabFiles"] = leaf.Values.Count(l => !l.Model && l.Path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)),
                ["byCategory"] = ByCategory(leaf.Values),
                ["rows"] = leaf.Values.OrderByDescending(l => l.Instances).ThenBy(l => l.Key, StringComparer.Ordinal)
                    .Select(l => (object)new Dictionary<string, object>
                    {
                        ["key"] = l.Key, ["category"] = l.Category, ["instances"] = l.Instances, ["renderers"] = l.Renderers, ["tris"] = l.Tris,
                    }).ToList(),
            };
            R["meshes"] = AssetSection(meshes.Values, withTris: true);
            R["materials"] = AssetSection(mats.Values, withTris: false);
            R["textures"] = AssetSection(texes.Values, withTris: false);

            // ---- lights, colliders, probes, volumes, misc
            var lightType = new Tally(); var lightMode = new Tally(); int shadowLights = 0, lightsInactive = 0;
            var colliders = new Tally(); int triggers = 0;
            int reflProbes = 0, lpGroups = 0, lpPositions = 0, particles = 0, terrains = 0, canvases = 0, audio = 0, cameras = 0;
            var volumes = new Tally();
            var scripts = new Tally();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (!(l.enabled && l.gameObject.activeInHierarchy)) { lightsInactive++; continue; }
                    lightType.Add(l.type.ToString());
                    lightMode.Add(l.lightmapBakeType.ToString());
                    if (l.shadows != LightShadows.None) shadowLights++;
                }
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!(c.enabled && c.gameObject.activeInHierarchy)) continue;
                    colliders.Add(c.GetType().Name);
                    if (c.isTrigger) triggers++;
                }
                foreach (var p in root.GetComponentsInChildren<ReflectionProbe>(true)) if (p.enabled && p.gameObject.activeInHierarchy) reflProbes++;
                foreach (var g in root.GetComponentsInChildren<LightProbeGroup>(true))
                    if (g.enabled && g.gameObject.activeInHierarchy) { lpGroups++; lpPositions += g.probePositions != null ? g.probePositions.Length : 0; }
                foreach (var p in root.GetComponentsInChildren<ParticleSystem>(true)) if (p.gameObject.activeInHierarchy) particles++;
                foreach (var p in root.GetComponentsInChildren<Terrain>(true)) if (p.enabled && p.gameObject.activeInHierarchy) terrains++;
                foreach (var p in root.GetComponentsInChildren<Canvas>(true)) if (p.enabled && p.gameObject.activeInHierarchy) canvases++;
                foreach (var p in root.GetComponentsInChildren<AudioSource>(true)) if (p.enabled && p.gameObject.activeInHierarchy) audio++;
                foreach (var p in root.GetComponentsInChildren<Camera>(true)) if (p.enabled && p.gameObject.activeInHierarchy) cameras++;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) { scripts.Add("(missing script)"); continue; }
                    if (!mb.gameObject.activeInHierarchy) continue;
                    var type = mb.GetType();
                    string fn = type.FullName ?? type.Name;
                    if (fn == "UnityEngine.Rendering.Volume" || fn == "UnityEngine.Rendering.PostProcessing.PostProcessVolume")
                    {
                        bool global = false;
                        var prop = type.GetField("isGlobal") ?? (System.Reflection.MemberInfo)type.GetProperty("isGlobal");
                        if (prop is System.Reflection.FieldInfo fi) global = (bool)fi.GetValue(mb);
                        else if (prop is System.Reflection.PropertyInfo pi) global = (bool)pi.GetValue(mb);
                        volumes.Add(type.Name + (global ? " (global)" : " (local)") + (mb.enabled ? "" : " [disabled]"));
                    }
                    if (mb.enabled) scripts.Add(fn);
                }
            }
            R["lights"] = new Dictionary<string, object>
            {
                ["active"] = lightType.Counts.Values.Sum(), ["inactiveOrDisabled"] = lightsInactive, ["byType"] = Sorted(lightType),
                ["byMode"] = Sorted(lightMode), ["castingShadows"] = shadowLights,
            };
            R["colliders"] = new Dictionary<string, object> { ["active"] = colliders.Counts.Values.Sum(), ["byType"] = Sorted(colliders), ["triggers"] = triggers };
            R["probesAndVolumes"] = new Dictionary<string, object>
            {
                ["reflectionProbes"] = reflProbes, ["lightProbeGroups"] = lpGroups, ["lightProbePositions"] = lpPositions,
                ["postProcessingVolumes"] = Sorted(volumes),
            };
            R["lighting"] = new Dictionary<string, object>
            {
                ["lightmaps"] = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0,
                ["lightingDataAsset"] = Lightmapping.lightingDataAsset != null ? AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset) : "",
                ["ambientMode"] = RenderSettings.ambientMode.ToString(),
                ["skybox"] = RenderSettings.skybox != null ? RenderSettings.skybox.name : "",
                ["fog"] = RenderSettings.fog,
            };
            R["other"] = new Dictionary<string, object>
            {
                ["particleSystems"] = particles, ["terrains"] = terrains, ["canvases"] = canvases, ["audioSources"] = audio, ["cameras"] = cameras,
                ["monoBehavioursEnabled"] = scripts.Counts.Values.Sum(), ["scriptTypes"] = Sorted(scripts),
            };
            return R;
        }

        /// <summary>The asset path at each prefab nesting level, outermost first, by GetCorrespondingObjectFromSource.</summary>
        static List<string> Chain(Object o)
        {
            var list = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(o);
                if (src == null) break;
                list.Add(AssetDatabase.GetAssetPath(src));
                o = src;
            }
            return list;
        }

        static long Triangles(Mesh m)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++)
            {
                var top = m.GetTopology(i);
                long idx = m.GetIndexCount(i);
                if (top == MeshTopology.Triangles) n += idx / 3;
                else if (top == MeshTopology.Quads) n += idx / 4 * 2;
            }
            return n;
        }

        static void Bump(Dictionary<string, AssetRow> d, string key, Object instance, long tris)
        {
            if (!d.TryGetValue(key, out var row)) d[key] = row = new AssetRow { Key = key, Path = key, Category = Category(key), Model = IsModel(key) };
            row.Renderers++;
            row.Tris += tris;
            row.InstanceSet.Add(instance);
            row.Instances = row.InstanceSet.Count;
        }

        static Dictionary<string, object> AssetSection(IEnumerable<AssetRow> rows, bool withTris)
        {
            var list = rows.ToList();
            var files = new HashSet<string>(list.Where(r => !string.IsNullOrEmpty(r.Path)).Select(r => r.Path));
            return new Dictionary<string, object>
            {
                ["unique"] = list.Count,
                ["uniqueAssetFiles"] = files.Count,
                ["sceneEmbedded"] = list.Count(r => string.IsNullOrEmpty(r.Path)),
                ["unityBuiltin"] = list.Count(r => r.Category == "Unity built-in"),
                ["byCategory"] = ByCategory(list),
                ["rows"] = list.OrderByDescending(r => r.Renderers).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r =>
                {
                    var d = new Dictionary<string, object> { ["key"] = r.Key, ["category"] = r.Category, ["renderers"] = r.Renderers };
                    if (withTris) d["tris"] = r.Tris;
                    return (object)d;
                }).ToList(),
            };
        }

        static List<object> Rows(IEnumerable<AssetRow> rows, bool withTris) =>
            rows.OrderByDescending(r => r.Instances).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r =>
            {
                var d = new Dictionary<string, object> { ["key"] = r.Key, ["category"] = r.Category, ["instances"] = r.Instances, ["renderers"] = r.Renderers };
                if (withTris) d["tris"] = r.Tris;
                return (object)d;
            }).ToList();

        /// <summary>Per category: unique assets, and renderer instances using them.</summary>
        static Dictionary<string, object> ByCategory(IEnumerable<AssetRow> rows)
        {
            var d = new Dictionary<string, object>();
            foreach (var g in rows.GroupBy(r => r.Category).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var row = new Dictionary<string, object> { ["unique"] = g.Count(), ["renderers"] = g.Sum(r => r.Renderers) };
                if (g.Any(r => r.Instances > 0)) row["instances"] = g.Sum(r => r.Instances);
                d[g.Key] = row;
            }
            return d;
        }

        static Dictionary<string, object> Sorted(Tally t) =>
            t.Counts.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => (object)k.Value);

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        // ------------------------------------------------------------------ a small JSON writer

        static class Json
        {
            public static string Write(object o) { var sb = new StringBuilder(); W(sb, o, 0); sb.Append('\n'); return sb.ToString(); }

            static void W(StringBuilder sb, object o, int ind)
            {
                switch (o)
                {
                    case null: sb.Append("null"); return;
                    case string s: Str(sb, s); return;
                    case bool b: sb.Append(b ? "true" : "false"); return;
                    case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                    case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                    case float f: sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                    case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                    case IDictionary<string, object> map:
                    {
                        if (map.Count == 0) { sb.Append("{}"); return; }
                        sb.Append("{\n");
                        int n = 0;
                        foreach (var kv in map)
                        {
                            sb.Append(' ', ind + 2); Str(sb, kv.Key); sb.Append(": "); W(sb, kv.Value, ind + 2);
                            sb.Append(++n < map.Count ? ",\n" : "\n");
                        }
                        sb.Append(' ', ind).Append('}');
                        return;
                    }
                    case IEnumerable seq:
                    {
                        var items = seq.Cast<object>().ToList();
                        if (items.Count == 0) { sb.Append("[]"); return; }
                        sb.Append("[\n");
                        for (int k = 0; k < items.Count; k++)
                        {
                            sb.Append(' ', ind + 2); W(sb, items[k], ind + 2);
                            sb.Append(k + 1 < items.Count ? ",\n" : "\n");
                        }
                        sb.Append(' ', ind).Append(']');
                        return;
                    }
                    default: Str(sb, o.ToString()); return;
                }
            }

            static void Str(StringBuilder sb, string s)
            {
                sb.Append('"');
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }
        }
    }
}
