using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Per-room ambience: one local post-processing Volume per room, its look picked from a mood table,
    /// layered over the project's default volume profile (Assets/Settings/Vivid Volume Profile.asset, which
    /// is neutral apart from its LUT). Lights are not touched.
    ///
    /// <para>Rooms scene (a root named <see cref="IronCitadelRoomsAssembler.RootName"/> with a "Rooms" child): one
    /// Volume named <see cref="ChildName"/> under each room instance, a trigger box over the room's 45 m cell (inset by
    /// the blend distance, so a zone ends at its own cell's edge and neighbours never overlap) from 0.5 m under the floor to 0.5 m over the
    /// room's highest lid (rooms-index.json lids_m.max beside the theme folders; renderer bounds when it is missing).
    /// The mood comes from the theme folder the room prefab sits in.</para>
    ///
    /// <para>Any other scene (the pattern-tile level): every room group (a transform with a direct "Floor" child whose
    /// name is a room-kind area, e.g. <c>throne_room-6</c>, <c>keep (stronghold)</c>) gets its floor cells (5 m) from
    /// its Floor renderers, merged greedily into rectangles, one Volume box per rectangle (at most
    /// <see cref="MaxBoxesPerRoom"/> per room, <see cref="MaxPatternVolumes"/> in all), from 0.5 m under the floor to
    /// 0.5 m over the room's Walls. Corridors ("Ways through ...") and the tiles' unlabelled Floor get none. They live
    /// under one scene root, <see cref="PatternsRootName"/>.</para>
    ///
    /// <para>Idempotent: <see cref="Apply"/> first removes every Volume it made before (the root, any "Ambience"
    /// Volume, any Volume using a profile under <see cref="ProfilesDir"/>), then refreshes the mood profiles in place
    /// (their GUIDs are kept) and builds again.</para>
    ///
    /// <para>Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelAmbience.ApplyBatch
    /// [-scene Assets/IronCitadel/Scenes/IronCitadel_Rooms.unity]</c> opens the scene (both Iron Citadel scenes when
    /// -scene is absent), applies, saves and writes <see cref="ResultPath"/>. Menu: Iron Citadel/Ambience.</para>
    ///
    /// <para>The grade: one global Volume per scene, <see cref="GradeName"/> at <see cref="GradePriority"/>, using
    /// <see cref="GradePath"/> (ACES and +2.4 EV exposure, the old film grade's bloom, contrast and vignette). The room
    /// lights were authored under Gamma and read several stops dim in the project's Linear space, so without it play
    /// mode is close to black. The profile is created once and never rebuilt: tune it in the Inspector.</para>
    ///
    /// <para>The LUTs: each mood names a Cinema Suite LUT, read from its vertically flipped copy under
    /// <see cref="LutDir"/> (&lt;name&gt;_URP.png). Cinema Suite strips keep green 0 on the top row and URP reads the
    /// bottom row as green 0, so the originals grade upside down. A volume's LUT texture cannot blend: entering a room's
    /// blend zone swaps the texture at once and only the contribution eases, so two rooms whose zones overlap cut from
    /// one LUT to the other. Hence no zone crosses its room's edge: at a doorway the look eases from one room's to the
    /// scene grade alone and on to the next room's. Pattern rectangles are inset only on faces that leave the room.</para>
    ///
    /// <para>The film (IronCitadelFlythrough) must evaluate local volumes at the camera and keep its own global volume
    /// below <see cref="Priority"/>, or no room's mood reaches a frame.</para></summary>
    public static class IronCitadelAmbience
    {
        public const string ProfilesDir = "Assets/IronCitadel/Ambience/Profiles";
        public const string LutDir = "Assets/IronCitadel/Ambience/LUTs";
        public const string ChildName = "Ambience";
        public const string PatternsRootName = "IronCitadelAmbience";
        public const string PatternsScenePath = "Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity";
        public const string ResultPath = "C:/Repos/IronCitadel/logs/ambience-result.txt";

        /// <summary>Above the scene grade (<see cref="GradePriority"/>); the default profile is not a Volume and every
        /// volume beats it. The flythrough's own global volume, when it uses one, sits at Priority - 1.</summary>
        public const float Priority = 10f;
        /// <summary>The scene grade: above the default profile (which still gives the LUT), under the moods.</summary>
        public const float GradePriority = 1f;
        public const string GradeName = "IronCitadel Grade";
        public const string GradePath = ProfilesDir + "/grade.asset";
        public const float RoomBlend = 3f;
        public const float PatternBlend = 2f;
        public const float CellM = 5f;
        public const int MaxBoxesPerRoom = 8;
        public const int MaxPatternVolumes = 400;
        const float DefaultLid = 6f, MinLid = 3f, MaxLid = 25f, Below = 0.5f, Above = 0.5f;

        // ------------------------------------------------------------------ moods

        /// <summary>One look. A null field is not overridden, so the default profile's value shows through.
        /// Colours are sRGB (URP linearizes colorFilter and bloom tint; bloom tint is luminance-normalized, so only
        /// its hue counts). Darkness rides in colorFilter, not postExposure, so it is relative to whatever exposure
        /// the base grade has (the scene grade's, in play and in the film).</summary>
        public sealed class Mood
        {
            public string Name, Look;
            public float? Temperature, Tint;                       // WhiteBalance, -100..100 (tint: - green, + magenta)
            public Color? Filter;                                  // ColorAdjustments.colorFilter
            public float? Saturation, Contrast, PostExposure;      // ColorAdjustments; postExposure clamped to +-0.3
            public float? Vignette; public Color? VignetteColor;   // Vignette (smoothness 0.45 when intensity is set)
            public float? Bloom; public Color? BloomTint;          // Bloom
            public string Lut; public float LutAmount = 1f;        // ColorLookup: <Lut>_URP.png under LutDir, contribution
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static readonly Mood[] Moods =
        {
            new Mood { Name = "crypt",     Look = "cold blue, desaturated, heavier vignette", Lut = "Winterfell3D16",   LutAmount = 0.85f, Saturation = -28f, Contrast = 8f,  Vignette = 0.38f, VignetteColor = C(0.02f, 0.03f, 0.07f) },
            new Mood { Name = "prison",    Look = "dank green-grey, dark",                    Lut = "Matrix3D16",       LutAmount = 0.85f, Saturation = -22f, Contrast = 6f,  Vignette = 0.34f },
            new Mood { Name = "forge",     Look = "hot orange, bloom up",                     Lut = "Fire3D16",         LutAmount = 0.6f,  Saturation = 8f,                   Bloom = 0.8f,  BloomTint = C(1.00f, 0.68f, 0.40f) },
            new Mood { Name = "library",   Look = "warm amber",                               Lut = "DineInHell3D16",   LutAmount = 0.75f, Saturation = -4f,                  Vignette = 0.20f },
            new Mood { Name = "regal",     Look = "regal gold",                               Lut = "TexMex3D16",       LutAmount = 0.7f,  Saturation = 10f,  Contrast = 6f,  Bloom = 0.45f, BloomTint = C(1.00f, 0.85f, 0.55f) },
            new Mood { Name = "hall",      Look = "warm lights, blue shadows",                Lut = "Hollywood3D16",    LutAmount = 0.6f,  Saturation = 4f },
            new Mood { Name = "guard",     Look = "cool, bleached",                           Lut = "BleachBypass3D16", LutAmount = 0.8f,  Saturation = -6f,  Contrast = 4f },
            new Mood { Name = "hearth",    Look = "warm (kitchen)",                           Lut = "HarshSun3D16",     LutAmount = 0.75f, Saturation = 5f,                   Bloom = 0.35f, BloomTint = C(1.00f, 0.78f, 0.55f) },
            new Mood { Name = "alchemy",   Look = "sickly green",                             Lut = "Hackers3D16",      LutAmount = 0.6f,  Saturation = -10f, Contrast = 5f,  Vignette = 0.24f, Bloom = 0.3f, BloomTint = C(0.70f, 1.00f, 0.55f) },
            new Mood { Name = "shrine",    Look = "violet",                                   Lut = "Endgame3D16",      LutAmount = 0.8f,  Saturation = -8f,                  Vignette = 0.22f, Bloom = 0.3f, BloomTint = C(0.80f, 0.70f, 1.00f) },
            new Mood { Name = "torture",   Look = "dark red",                                 Lut = "RedRoom3D16",      LutAmount = 0.8f,  Saturation = -12f, Contrast = 12f, Vignette = 0.42f, VignetteColor = C(0.10f, 0.00f, 0.00f) },
            new Mood { Name = "grotto",    Look = "teal",                                     Lut = "Underwater3D16",   LutAmount = 0.9f,  Saturation = 4f,                   Vignette = 0.28f, Bloom = 0.25f, BloomTint = C(0.55f, 1.00f, 0.90f) },
            new Mood { Name = "storeroom", Look = "dusty sepia",                              Lut = "Sepia3D16",        LutAmount = 0.8f,  Saturation = -24f, Contrast = -4f, Vignette = 0.22f },
        };

        /// <summary>Theme folder or room kind (normalized: lower case, letters and digits only) to mood; a null mood
        /// is "none". Covers the 21 theme folders of AllSyntyDungeons/Rooms and the 24 kinds of PrefabSynthesis
        /// data/room-kinds/dungeon.room-kinds.json (GuardRoom and guard_room both normalize to "guardroom").</summary>
        static readonly Dictionary<string, string> MoodByName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alchemylab"] = "alchemy", ["armory"] = "guard", ["barracks"] = "guard", ["bedroom"] = "hearth",
            ["commonroom"] = "hall", ["crypt"] = "crypt", ["forge"] = "forge", ["grotto"] = "grotto",
            ["guardroom"] = "guard", ["hall"] = "hall", ["hallgallery"] = "hall", ["kitchen"] = "hearth",
            ["library"] = "library", ["prison"] = "prison", ["shrine"] = "shrine", ["storeroom"] = "storeroom",
            ["study"] = "library", ["throneroom"] = "regal", ["torturechamber"] = "torture", ["treasury"] = "regal",
            ["workshop"] = "storeroom",
            // kinds that are not theme folders
            ["closet"] = "storeroom", ["dininghall"] = "hall", ["bare"] = null, ["none"] = null,
        };

        /// <summary>Area names to room kinds, from dungeon.room-kinds.json "areas" (the names that are not kinds
        /// themselves). door-N and switch-N match "door" and "switch" once the trailing number is stripped.</summary>
        static readonly Dictionary<string, string> AreaKinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["after"] = "common_room", ["before"] = "common_room", ["chamber"] = "storeroom", ["door"] = "none",
            ["entry"] = "bare", ["flank"] = "bare", ["floor"] = "hall", ["great-hall"] = "hall", ["hold"] = "armory",
            ["keep"] = "guard_room", ["lower"] = "common_room", ["neck"] = "bare", ["next"] = "bedroom",
            ["other-room"] = "study", ["perch"] = "bare", ["pocket"] = "closet", ["post"] = "bare", ["rejoin"] = "bare",
            ["reward"] = "storeroom", ["room"] = "bedroom", ["spot"] = "storeroom", ["stairs"] = "bare",
            ["switch"] = "none", ["trunk"] = "bare", ["upper"] = "bare", ["walkway"] = "bare", ["watched"] = "hall",
            ["watched-ground"] = "hall", ["way-back"] = "bare", ["way-round"] = "bare", ["way-up"] = "bare",
        };

        static readonly HashSet<string> Kinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "hall", "guard_room", "armory", "bedroom", "common_room", "study", "storeroom", "closet", "dining_hall",
            "crypt", "shrine", "torture_chamber", "prison", "forge", "kitchen", "treasury", "throne_room", "library",
            "barracks", "alchemy_lab", "workshop", "grotto", "bare", "none",
        };

        static string Norm(string s) => new string(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        /// <summary>The mood for a theme folder (GuardRoom) or a room kind (guard_room); null for none.</summary>
        public static Mood MoodFor(string themeOrKind)
        {
            if (string.IsNullOrEmpty(themeOrKind)) return null;
            if (!MoodByName.TryGetValue(Norm(themeOrKind), out var name) || name == null) return null;
            return Moods.FirstOrDefault(m => m.Name == name);
        }

        /// <summary>The room kind an area (room group) name stands for, by the room-kinds matching rule: drop a
        /// leading "row,col:" prefix and a trailing " (pattern)" role note, match whole, then with -N/_N stripped.
        /// Null when nothing matches (corridors like "Ways through ...", "Floor", "Uprights").</summary>
        public static string KindOfArea(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = name.Trim().ToLowerInvariant();
            var grid = Regex.Match(n, @"^\d+,\d+:");
            if (grid.Success) n = n.Substring(grid.Length);
            int paren = n.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) n = n.Substring(0, paren).Trim();
            if (TryKind(n, out var kind)) return kind;
            string bare = Regex.Replace(n, @"[-_]\d+$", "");
            return bare != n && TryKind(bare, out kind) ? kind : null;
        }

        static bool TryKind(string n, out string kind)
        {
            if (AreaKinds.TryGetValue(n, out kind)) return true;
            if (Kinds.Contains(n)) { kind = n; return true; }
            kind = null;
            return false;
        }

        // ------------------------------------------------------------------ entry points

        [MenuItem("Iron Citadel/Ambience/Apply to open scene")]
        public static void ApplyMenu()
        {
            var log = new StringBuilder();
            Apply(SceneManager.GetActiveScene(), log);
            Debug.Log(log.ToString());
        }

        [MenuItem("Iron Citadel/Ambience/Remove from open scene")]
        public static void RemoveMenu()
        {
            var scene = SceneManager.GetActiveScene();
            int n = Remove(scene);
            if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[IronCitadelAmbience] removed {n} ambience volume(s) from {scene.path}");
        }

        /// <summary>-executeMethod entry: [-scene path] (default: the Rooms and the Patterns scene) opened, applied,
        /// saved; writes <see cref="ResultPath"/>. Exits 0, or 1 on an exception.</summary>
        public static void ApplyBatch()
        {
            var log = new StringBuilder();
            log.AppendLine($"IronCitadelAmbience {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            int code = 0;
            try
            {
                var args = Environment.GetCommandLineArgs();
                int a = Array.IndexOf(args, "-scene");
                var scenes = a >= 0 && a + 1 < args.Length
                    ? new[] { args[a + 1].Replace('\\', '/') }
                    : new[] { IronCitadelRoomsAssembler.ScenePath, PatternsScenePath };
                foreach (var path in scenes)
                {
                    if (!File.Exists(path)) throw new FileNotFoundException("no scene at " + path);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Apply(scene, log);
                    if (!EditorSceneManager.SaveScene(scene)) throw new Exception("could not save " + path);
                    log.AppendLine("saved " + path);
                }
                AssetDatabase.SaveAssets();
                log.AppendLine("RESULT ok");
            }
            catch (Exception e)
            {
                log.AppendLine("RESULT failed: " + e);
                code = 1;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, log.ToString());
            Debug.Log(log.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// <summary>Replace this tool's volumes in <paramref name="scene"/> (it does not save the scene; the
        /// scene builders save after calling it).</summary>
        public static void Apply(Scene scene, StringBuilder log)
        {
            if (log == null) log = new StringBuilder();
            log.AppendLine($"ambience: {scene.path}");
            int removed = Remove(scene);
            var profiles = EnsureProfiles(log);
            var roomsT = FindRooms(scene);
            int made = roomsT != null ? ApplyRooms(scene, roomsT, profiles, log) : ApplyPatterns(scene, profiles, log);
            log.AppendLine($"ambience: removed {removed}, made {made} local volume(s) ({(roomsT != null ? "rooms" : "pattern rooms")}, priority {Priority})");
            var grade = EnsureGrade(log);
            var gv = new GameObject(GradeName).AddComponent<Volume>();
            if (gv.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(gv.gameObject, scene);
            gv.gameObject.layer = 0;
            gv.isGlobal = true;
            gv.priority = GradePriority;
            gv.sharedProfile = grade;
            made++;
            log.AppendLine($"ambience: global {GradeName} (priority {GradePriority}) from {GradePath}");
            if (removed + made > 0) EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Delete every volume this tool made: the patterns root, and any Volume named
        /// <see cref="ChildName"/> or using a profile under <see cref="ProfilesDir"/>.</summary>
        public static int Remove(Scene scene)
        {
            var doomed = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == PatternsRootName) { doomed.Add(root); continue; }
                foreach (var v in root.GetComponentsInChildren<Volume>(true))
                {
                    string profilePath = v.sharedProfile != null ? AssetDatabase.GetAssetPath(v.sharedProfile) : "";
                    if (v.gameObject.name != ChildName && !profilePath.StartsWith(ProfilesDir + "/", StringComparison.Ordinal)) continue;
                    var go = v.gameObject;
                    // a GameObject the prefab asset itself owns cannot be destroyed in an instance; never ours anyway
                    if (PrefabUtility.IsPartOfPrefabInstance(go) && !PrefabUtility.IsAddedGameObjectOverride(go)) continue;
                    doomed.Add(go);
                }
            }
            int n = 0;
            foreach (var go in doomed)
                if (go != null) { Object.DestroyImmediate(go); n++; }
            return n;
        }

        // ------------------------------------------------------------------ profiles

        /// <summary>Create or refresh one VolumeProfile per mood under <see cref="ProfilesDir"/>. An existing
        /// asset keeps its GUID: its components are deleted and rebuilt from the table.</summary>
        public static Dictionary<string, VolumeProfile> EnsureProfiles(StringBuilder log)
        {
            EnsureFolder(ProfilesDir);
            var map = new Dictionary<string, VolumeProfile>(StringComparer.Ordinal);
            foreach (var m in Moods)
            {
                string path = $"{ProfilesDir}/{m.Name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(sub is VolumeComponent old)) continue;
                    AssetDatabase.RemoveObjectFromAsset(old);
                    Object.DestroyImmediate(old, true);
                }
                profile.components.Clear();
                Fill(profile, m);
                EditorUtility.SetDirty(profile);
                map[m.Name] = profile;
            }
            AssetDatabase.SaveAssets();
            log?.AppendLine($"ambience: {map.Count} mood profile(s) refreshed under {ProfilesDir}");
            return map;
        }

        /// <summary>The scene grade's profile: made with the old film grade's values when missing, otherwise left
        /// exactly as it is (hand edits survive every rebuild).</summary>
        public static VolumeProfile EnsureGrade(StringBuilder log)
        {
            EnsureFolder(ProfilesDir);
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, GradePath);
            AddTo<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
            var ca = AddTo<ColorAdjustments>(p);
            ca.postExposure.Override(2.4f);
            ca.contrast.Override(8f);
            ca.saturation.Override(8f);
            var b = AddTo<Bloom>(p);
            b.intensity.Override(0.6f);
            b.threshold.Override(0.95f);
            var v = AddTo<Vignette>(p);
            v.intensity.Override(0.28f);
            v.smoothness.Override(0.45f);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            log?.AppendLine("ambience: made the grade profile " + GradePath);
            return p;
        }

        static T AddTo<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(false);
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        static void Fill(VolumeProfile p, Mood m)
        {
            if (m.Lut != null)
            {
                string path = $"{LutDir}/{m.Lut}_URP.png";
                var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (lut == null) throw new FileNotFoundException($"mood {m.Name}: no LUT at {path}");
                var cl = AddTo<ColorLookup>(p);
                cl.texture.Override(lut);
                cl.contribution.Override(Mathf.Clamp01(m.LutAmount));
            }
            if (m.Temperature.HasValue || m.Tint.HasValue)
            {
                var wb = AddTo<WhiteBalance>(p);
                if (m.Temperature.HasValue) wb.temperature.Override(m.Temperature.Value);
                if (m.Tint.HasValue) wb.tint.Override(m.Tint.Value);
            }
            if (m.Filter.HasValue || m.Saturation.HasValue || m.Contrast.HasValue || m.PostExposure.HasValue)
            {
                var ca = AddTo<ColorAdjustments>(p);
                if (m.Filter.HasValue) ca.colorFilter.Override(m.Filter.Value);
                if (m.Saturation.HasValue) ca.saturation.Override(m.Saturation.Value);
                if (m.Contrast.HasValue) ca.contrast.Override(m.Contrast.Value);
                if (m.PostExposure.HasValue) ca.postExposure.Override(Mathf.Clamp(m.PostExposure.Value, -0.3f, 0.3f));
            }
            if (m.Vignette.HasValue)
            {
                var v = AddTo<Vignette>(p);
                v.intensity.Override(m.Vignette.Value);
                v.smoothness.Override(0.45f);
                if (m.VignetteColor.HasValue) v.color.Override(m.VignetteColor.Value);
            }
            if (m.Bloom.HasValue || m.BloomTint.HasValue)
            {
                var b = AddTo<Bloom>(p);
                if (m.Bloom.HasValue) b.intensity.Override(m.Bloom.Value);
                if (m.BloomTint.HasValue) b.tint.Override(m.BloomTint.Value);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // ------------------------------------------------------------------ the volume

        static GameObject MakeVolume(string name, Transform parent, Scene scene, Vector3 localCentre, Vector3 size, float blend, VolumeProfile profile)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = 0; // cameras' default volume mask is the Default layer
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var box = go.AddComponent<BoxCollider>(); // before isGlobal = false, which collects the colliders
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = false;
            vol.priority = Priority;
            vol.blendDistance = blend;
            vol.weight = 1f;
            vol.sharedProfile = profile;
            return go;
        }

        // ------------------------------------------------------------------ rooms scene

        static Transform FindRooms(Scene scene)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == IronCitadelRoomsAssembler.RootName);
            return root != null ? root.transform.Find("Rooms") : null;
        }

        static int ApplyRooms(Scene scene, Transform roomsT, Dictionary<string, VolumeProfile> profiles, StringBuilder log)
        {
            var rooms = roomsT.Cast<Transform>().ToList();
            var paths = new Dictionary<Transform, string>();
            foreach (var r in rooms) paths[r] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject) ?? "";
            var lids = ReadIndexLids(paths.Values.Where(p => p.Length > 0));
            float side = IronCitadelRoomsAssembler.Tile - 2f * RoomBlend;
            int made = 0;
            foreach (var room in rooms)
            {
                string path = paths[room];
                string theme = ThemeOf(path) ?? room.name;
                var mood = MoodFor(theme);
                if (mood == null) { log.AppendLine($"  {room.name}: theme '{theme}' has no mood; no volume"); continue; }
                bool indexed = lids.TryGetValue(path, out float lid);
                if (!indexed) lid = MeasureLid(room);
                lid = Mathf.Clamp(lid, MinLid, MaxLid);
                float h = Below + lid + Above;
                MakeVolume(ChildName, room, scene, new Vector3(0f, -Below + h / 2f, 0f), new Vector3(side, h, side), RoomBlend, profiles[mood.Name]);
                made++;
                log.AppendLine($"  {room.name}: {theme} -> {mood.Name} ({mood.Look}); lid {lid:0.0} m ({(indexed ? "rooms-index" : "renderer bounds")}); box {side:0} x {h:0.0} x {side:0} m, blend {RoomBlend} m");
            }
            return made;
        }

        /// <summary>"Assets/.../Rooms/GuardRoom/x.prefab" -> "GuardRoom".</summary>
        static string ThemeOf(string prefabPath)
        {
            int a = prefabPath.LastIndexOf('/');
            if (a <= 0) return null;
            int b = prefabPath.LastIndexOf('/', a - 1);
            return b < 0 ? null : prefabPath.Substring(b + 1, a - b - 1);
        }

        /// <summary>The rooms-index.json beside the theme folders: "Assets/.../Rooms/rooms-index.json".</summary>
        static string RoomsIndexFor(string prefabPath)
        {
            int a = prefabPath.LastIndexOf('/');
            if (a <= 0) return null;
            int b = prefabPath.LastIndexOf('/', a - 1);
            return b <= 0 ? null : prefabPath.Substring(0, b) + "/rooms-index.json";
        }

        /// <summary>lids_m.max per prefab path, streamed out of each rooms-index.json (18 MB for AllSyntyDungeons).</summary>
        static Dictionary<string, float> ReadIndexLids(IEnumerable<string> prefabPaths)
        {
            var lids = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var group in prefabPaths.Distinct().GroupBy(p => RoomsIndexFor(p)))
            {
                if (group.Key == null || !File.Exists(group.Key)) continue;
                var want = new HashSet<string>(group, StringComparer.Ordinal);
                try
                {
                    using (var sr = new StreamReader(group.Key))
                    using (var jr = new JsonTextReader(sr))
                    {
                        while (jr.Read())
                        {
                            if (jr.TokenType != JsonToken.PropertyName || jr.Depth != 1 || (string)jr.Value != "rooms") continue;
                            if (!jr.Read() || jr.TokenType != JsonToken.StartArray) break;
                            while (jr.Read() && jr.TokenType == JsonToken.StartObject)
                            {
                                var o = JObject.Load(jr);
                                var p = (string)o["prefab"];
                                var max = (o["lids_m"] as JObject)?["max"];
                                if (p != null && want.Contains(p) && max != null && (max.Type == JTokenType.Integer || max.Type == JTokenType.Float))
                                    lids[p] = (float)max;
                            }
                            break;
                        }
                    }
                }
                catch (Exception e) { Debug.LogWarning($"[IronCitadelAmbience] could not read {group.Key}: {e.Message}"); }
            }
            return lids;
        }

        static float MeasureLid(Transform room)
        {
            float top = float.NegativeInfinity;
            foreach (var r in room.GetComponentsInChildren<MeshRenderer>(false)) top = Mathf.Max(top, r.bounds.max.y);
            return float.IsNegativeInfinity(top) ? DefaultLid : top - room.position.y;
        }

        // ------------------------------------------------------------------ pattern-tile rooms

        struct CellRect { public int i, j, w, h; public int Area => w * h; }

        static int ApplyPatterns(Scene scene, Dictionary<string, VolumeProfile> profiles, StringBuilder log)
        {
            var groups = new List<(Transform group, string kind, Mood mood)>();
            var noMood = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == PatternsRootName || root.name == IronCitadelRoomsAssembler.RootName) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.childCount == 0 || !t.gameObject.activeInHierarchy || t.Find("Floor") == null) continue;
                    string kind = KindOfArea(t.name);
                    if (kind == null) continue; // a corridor, the tile's own unlabelled Floor, anything unnamed
                    var mood = MoodFor(kind);
                    if (mood == null) { noMood.TryGetValue(kind, out int c); noMood[kind] = c + 1; continue; }
                    groups.Add((t, kind, mood));
                }
            }
            if (noMood.Count > 0) log.AppendLine("  room groups with no mood: " + string.Join(", ", noMood.Select(kv => $"{kv.Key} x{kv.Value}")));
            if (groups.Count == 0) { log.AppendLine("  no room groups found; nothing to do"); return 0; }

            var rootGo = new GameObject(PatternsRootName);
            if (rootGo.scene != scene) SceneManager.MoveGameObjectToScene(rootGo, scene);
            int made = 0, capped = 0;
            foreach (var (g, kind, mood) in groups)
            {
                var cells = FloorCells(g.Find("Floor"), out float floorY);
                if (cells.Count == 0) { log.AppendLine($"  {g.parent?.name} / {g.name}: no floor renderers; no volume"); continue; }
                float top = WallTop(g, floorY);
                var rects = MergeRects(cells).OrderByDescending(r => r.Area).ToList();
                int keep = Math.Min(rects.Count, Math.Min(MaxBoxesPerRoom, MaxPatternVolumes - made));
                if (keep < rects.Count) capped += rects.Count - keep;
                float bottom = floorY - Below, height = top + Above - bottom;
                int covered = 0;
                for (int k = 0; k < keep; k++)
                {
                    var r = rects[k];
                    // a face whose far side is not all this room's floor is pulled in by the blend distance
                    float x0 = r.i * CellM + (Leaves(cells, r.i - 1, r.j, 0, 1, r.h) ? PatternBlend : 0f);
                    float x1 = (r.i + r.w) * CellM - (Leaves(cells, r.i + r.w, r.j, 0, 1, r.h) ? PatternBlend : 0f);
                    float z0 = r.j * CellM + (Leaves(cells, r.i, r.j - 1, 1, 0, r.w) ? PatternBlend : 0f);
                    float z1 = (r.j + r.h) * CellM - (Leaves(cells, r.i, r.j + r.h, 1, 0, r.w) ? PatternBlend : 0f);
                    float w = Mathf.Max(0.5f, x1 - x0), d = Mathf.Max(0.5f, z1 - z0);
                    var centre = new Vector3((x0 + x1) / 2f, bottom + height / 2f, (z0 + z1) / 2f);
                    MakeVolume($"{g.parent?.name} : {g.name} : {k + 1}", rootGo.transform, scene, centre, new Vector3(w, height, d), PatternBlend, profiles[mood.Name]);
                    covered += r.Area;
                    made++;
                }
                log.AppendLine($"  {g.parent?.name} / {g.name}: {kind} -> {mood.Name}; {cells.Count} cell(s) in {rects.Count} rect(s), {keep} kept ({covered * 100 / cells.Count}% of the floor); y {bottom:0.0}..{bottom + height:0.0}");
                if (made >= MaxPatternVolumes) { log.AppendLine($"  cap of {MaxPatternVolumes} volumes reached"); break; }
            }
            if (capped > 0) log.AppendLine($"  {capped} small rectangle(s) dropped by the caps");
            return made;
        }

        /// <summary>True when any of the n cells from (i, j), stepping (di, dj), is not in <paramref name="cells"/>.</summary>
        static bool Leaves(HashSet<Vector2Int> cells, int i, int j, int di, int dj, int n)
        {
            for (int k = 0; k < n; k++)
                if (!cells.Contains(new Vector2Int(i + k * di, j + k * dj))) return true;
            return false;
        }

        /// <summary>The 5 m cells (world grid from the origin) the Floor renderers cover. Each renderer's AABB
        /// (tiles are turned in 90-degree steps, so it is tight) spans round(size / 5) cells, at least one, centred
        /// on its own centre, so a piece's pivot (corner or centre) does not matter. floorY: the median floor top.</summary>
        static HashSet<Vector2Int> FloorCells(Transform floor, out float floorY)
        {
            var cells = new HashSet<Vector2Int>();
            var tops = new List<float>();
            foreach (var r in floor.GetComponentsInChildren<MeshRenderer>(false))
            {
                var b = r.bounds;
                tops.Add(b.max.y);
                int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / CellM)), nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / CellM));
                int i0 = Mathf.FloorToInt((b.center.x - (nx - 1) * CellM / 2f) / CellM);
                int j0 = Mathf.FloorToInt((b.center.z - (nz - 1) * CellM / 2f) / CellM);
                for (int i = i0; i < i0 + nx; i++)
                    for (int j = j0; j < j0 + nz; j++)
                        cells.Add(new Vector2Int(i, j));
            }
            tops.Sort();
            floorY = tops.Count > 0 ? tops[tops.Count / 2] : 0f;
            return cells;
        }

        static float WallTop(Transform group, float floorY)
        {
            float top = float.NegativeInfinity;
            var walls = group.Find("Walls");
            if (walls != null)
                foreach (var r in walls.GetComponentsInChildren<MeshRenderer>(false)) top = Mathf.Max(top, r.bounds.max.y);
            float h = float.IsNegativeInfinity(top) ? DefaultLid : top - floorY;
            return floorY + Mathf.Clamp(h, MinLid, MaxLid);
        }

        /// <summary>Greedy: from each uncovered cell (row by row), run east as far as the floor goes, then grow north
        /// while the whole run is floor.</summary>
        static List<CellRect> MergeRects(HashSet<Vector2Int> cells)
        {
            var left = new HashSet<Vector2Int>(cells);
            var rects = new List<CellRect>();
            foreach (var c in cells.OrderBy(v => v.y).ThenBy(v => v.x))
            {
                if (!left.Contains(c)) continue;
                int w = 1;
                while (left.Contains(new Vector2Int(c.x + w, c.y))) w++;
                int h = 1;
                while (true)
                {
                    bool full = true;
                    for (int k = 0; k < w && full; k++) full = left.Contains(new Vector2Int(c.x + k, c.y + h));
                    if (!full) break;
                    h++;
                }
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        left.Remove(new Vector2Int(c.x + dx, c.y + dy));
                rects.Add(new CellRect { i = c.x, j = c.y, w = w, h = h });
            }
            return rects;
        }
    }
}
