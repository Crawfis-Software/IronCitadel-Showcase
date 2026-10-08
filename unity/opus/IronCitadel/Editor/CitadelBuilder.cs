using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronCitadel.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/IronCitadel.unity from level.json's layout: 11 rooms on 45 m tiles, 13 doors, the gallery,
    /// the stage, the portcullis and the secret bookcase. Run: IronCitadel.EditorTools.CitadelBuilder.Build().
    /// </summary>
    public static partial class CitadelBuilder
    {
        public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
        public const string PlayerPrefab = "Assets/Interact/IronCitadelPlayer.prefab";
        public static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        public static Transform Level;
        public static readonly List<string> Log = new List<string>();

        static Room MakeRoom(string id, string name, int row, int col, float h, Style st, int bit)
        {
            var rm = new Room { Id = id, Name = name, Row = row, Col = col, H = h, St = st, Mask = 1u << bit };
            rm.Root = Kit.Group(name, Level);
            rm.Arch = Kit.Group("Architecture", rm.Root);
            rm.Props = Kit.Group("Furnishing", rm.Root);
            rm.Lights = Kit.Group("Lights", rm.Root);
            rm.Ceil = Kit.Group("Ceiling", rm.Root);
            rm.Cols = Kit.Group("Colliders", rm.Root);
            rm.People = Kit.Group("Markers", rm.Root);
            Rooms[id] = rm;
            return rm;
        }

        static Door AddDoor(string id, Vector3 centre, Vector3 axis, Room a, Room b)
        {
            var d = new Door { Id = id, Centre = centre, Axis = axis, A = a, B = b };
            Arch_.Doors.Add(d);
            return d;
        }

        public static string Build()
        {
            Kit.Rng = new System.Random(20261007);
            Rooms.Clear();
            Arch_.Doors.Clear();
            Log.Clear();
            Kit.Missing.Clear();
            Kit.EnsureLayers();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Level = new GameObject("IronCitadel").transform;

            // light-layer bits: rooms that touch (even diagonally) never share one, so no light bleeds through
            // the rock between rooms; URP lights only 8 rendering layers, so bits are reused far apart
            var E = MakeRoom("entry_hall", "Entry Hall", 3, 2, 6f, Style.Dwarf, 1);
            var G = MakeRoom("guard_room", "Guard Room", 3, 1, 3.6f, Style.Dungeon, 2);
            var P = MakeRoom("prison", "Prison", 2, 1, 3.6f, Style.Dungeon, 4);
            var V = MakeRoom("vault", "Treasure Vault", 2, 0, 3.2f, Style.Dungeon, 7);
            var K = MakeRoom("kitchen", "Kitchen", 1, 1, 4.0f, Style.Dungeon, 1);
            var D = MakeRoom("great_hall", "Great Hall", 1, 2, 12.5f, Style.Dwarf, 2);
            var A = MakeRoom("armory", "Armory", 2, 2, 6f, Style.Dwarf, 5);
            var T = MakeRoom("throne_room", "Throne Room", 0, 2, 15f, Style.Dwarf, 4);
            var L = MakeRoom("library", "Library", 3, 3, 6f, Style.Dwarf, 3);
            var S = MakeRoom("study", "Alchemist's Study", 2, 3, 6f, Style.Dwarf, 6);
            var F = MakeRoom("forge", "Forge", 1, 3, 6f, Style.Dungeon, 3);

            var north = Vector3.forward; var east = Vector3.right;
            var outer = AddDoor("outer_door", new Vector3(112.5f, 0, -180f), -north, E, null);
            outer.Width = 3.3f; outer.Height = 4.6f; outer.Frame = "D:SM_Env_Door_Large_Frame_02"; outer.FrameScale = 1f;
            AddDoor("west_door", new Vector3(90f, 0, -157.5f), -east, E, G);
            AddDoor("east_door", new Vector3(135f, 0, -157.5f), east, E, L);
            var gate = AddDoor("armory_gate", new Vector3(112.5f, 0, -135f), north, E, A);
            gate.Width = 4.0f; gate.Height = 4.6f; gate.Frame = "D:SM_Env_Door_Large_Frame_01"; gate.FrameScale = 1f;
            AddDoor("cell_block", new Vector3(67.5f, 0, -135f), north, G, P);
            var vd = AddDoor("vault_door", new Vector3(45f, 0, -112.5f), -east, P, V);
            vd.Width = 2.13f; vd.Height = 3.2f; vd.Frame = null; vd.Secret = true;
            AddDoor("prison_kitchen", new Vector3(67.5f, 0, -90f), north, P, K);
            AddDoor("kitchen_hall", new Vector3(90f, 0, -67.5f), east, K, D);
            AddDoor("library_study", new Vector3(157.5f, 0, -135f), north, L, S);
            AddDoor("study_forge", new Vector3(157.5f, 0, -90f), north, S, F);
            AddDoor("forge_hall", new Vector3(135f, 0, -67.5f), -east, F, D);
            AddDoor("under_gallery", new Vector3(112.5f, 0, -90f), -north, D, A);
            var td = AddDoor("throne_doors", new Vector3(112.5f, 0, -45f), north, D, T);
            td.Width = 5f; td.Height = 5.7f; td.Frame = "D:SM_Env_Door_Large_Frame_01"; td.FrameScale = 1.25f;

            BuildEntry(E);
            BuildGuard(G);
            BuildPrison(P);
            BuildVault(V);
            BuildKitchen(K);
            BuildHall(D);
            BuildArmory(A);
            BuildThrone(T);
            BuildLibrary(L);
            BuildStudy(S);
            BuildForge(F);

            var passages = Kit.Group("Passages", Level);
            foreach (var d in Arch_.Doors)
            {
                Arch_.Passage(d, passages);
                Arch_.SetMasks(d.Root, d.A.Mask | (d.B != null ? d.B.Mask : 0u));
            }
            DoorDressing();

            foreach (var rm in Rooms.Values) Arch_.SetMasks(rm.Root, rm.Mask);
            foreach (var rm in Rooms.Values) RoomLabel(rm);
            RockCap();
            Atmosphere();
            var player = PlacePlayer();
            Overview(player);
            MarkStatic();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            int objects = Level.GetComponentsInChildren<Transform>(true).Length;
            int lights = Level.GetComponentsInChildren<Light>(true).Length;
            return $"built {ScenePath}: {objects} objects, {lights} lights; missing prefabs: {string.Join(",", Kit.Missing)}; {string.Join("; ", Log)}";
        }

        static void AddToBuildSettings()
        {
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void MarkStatic()
        {
            foreach (var rm in Rooms.Values)
                foreach (var t in new[] { rm.Arch, rm.Ceil, rm.Cols })
                    foreach (var tr in t.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(tr.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
        }

        // ---------------------------------------------------------------- atmosphere, post, overview, player

        static void Atmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.1f, 0.11f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.035f, 0.03f, 0.028f);

            var profilePath = "Assets/IronCitadel/Generated/CitadelVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            foreach (var c in profile.components.ToList()) { profile.Remove(c.GetType()); }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(profilePath)) if (o != profile) AssetDatabase.RemoveObjectFromAsset(o);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(0.95f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.65f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(1.0f); ca.contrast.Override(12f); ca.saturation.Override(8f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.28f); vig.smoothness.Override(0.45f);
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var vol = new GameObject("PostVolume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            vol.transform.SetParent(Level, false);
        }

        public static Vector3 StartPos = new Vector3(112.5f, 0.05f, -176.6f);

        static GameObject PlacePlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "IronCitadelPlayer";
            player.transform.SetPositionAndRotation(StartPos, Quaternion.identity); // facing north (+z)
            var cam = player.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            if (cam != null)
            {
                cam.cullingMask = ~(1 << Kit.OverviewLayer);
                cam.farClipPlane = 400f;
                cam.nearClipPlane = 0.05f;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            return player;
        }

        static void Overview(GameObject player)
        {
            var root = Kit.Group("Overview", Level);
            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(root, false);
            camGo.transform.SetPositionAndRotation(new Vector3(90f, 150f, -90f), Quaternion.Euler(90f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 93f;
            cam.nearClipPlane = 1f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.045f, 0.04f);
            cam.cullingMask = ~(1 << Kit.CeilingLayer);
            cam.depth = 10;
            cam.enabled = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var lightGo = new GameObject("OverviewLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.rotation = Quaternion.Euler(72f, 30f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = 0.9f; l.color = new Color(1f, 0.95f, 0.88f);
            l.shadows = LightShadows.None;
            l.renderingLayerMask = -1;
            var ad = l.GetUniversalAdditionalLightData(); ad.renderingLayers = 0xFFFFFFFF;
            l.enabled = false;

            var hudGo = new GameObject("CitadelHUD");
            hudGo.transform.SetParent(root, false);
            var hud = hudGo.AddComponent<CitadelHUD>();
            hud.overviewCamera = cam;
            hud.overviewLight = l;
            hud.player = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PlayerCapsule");
            hud.throneCentre = ThroneGoalCentre;
            hud.throneHalfExtents = ThroneGoalHalf;
        }

        public static Vector3 ThroneGoalCentre = new Vector3(112.5f, 2.2f, -6.5f);
        public static Vector3 ThroneGoalHalf = new Vector3(3.5f, 1.6f, 3.5f);

        static void RoomLabel(Room rm)
        {
            var go = new GameObject("Label_" + rm.Id);
            go.transform.SetParent(rm.Root, false);
            Vector3 c = rm.W(22.5f, 22.5f, 40f);
            if (rm.Id == "vault") c = rm.W(34.5f, 33.5f, 40f);
            if (rm.Id == "kitchen") c = rm.W(29.5f, 15.5f, 40f);
            if (rm.Id == "great_hall") c = rm.W(22.5f, 27f, 40f);
            if (rm.Id == "throne_room") c = rm.W(22.5f, 20f, 40f);
            go.transform.SetPositionAndRotation(c, Quaternion.Euler(90, 0, 0));
            var tm = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.text = rm.Name.ToUpperInvariant(); tm.fontSize = 64; tm.characterSize = 0.32f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(1f, 0.93f, 0.7f);
            tm.fontStyle = FontStyle.Bold;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            Kit.SetLayer(go, Kit.OverviewLayer);
        }

        /// <summary>
        /// A flat cap over everything that is not a room or a passage, above the tallest ceiling. Only the overview
        /// camera draws it, so from above the rock reads as rock and the rooms as holes cut into it.
        /// </summary>
        static void RockCap()
        {
            const float cell = 0.5f;
            int n = Mathf.RoundToInt(180f / cell);
            var open = new bool[n, n];
            void Mark(float x0, float z0, float x1, float z1)
            {
                int i0 = Mathf.Clamp(Mathf.RoundToInt(x0 / cell), 0, n), i1 = Mathf.Clamp(Mathf.RoundToInt(x1 / cell), 0, n);
                int j0 = Mathf.Clamp(Mathf.RoundToInt(-z1 / cell), 0, n), j1 = Mathf.Clamp(Mathf.RoundToInt(-z0 / cell), 0, n);
                for (int i = i0; i < i1; i++) for (int j = j0; j < j1; j++) open[i, j] = true;
            }
            foreach (var rm in Rooms.Values)
                foreach (var r in rm.Open)
                    Mark(rm.X0 + r.xMin, rm.Z0 + r.yMin, rm.X0 + r.xMax, rm.Z0 + r.yMax);
            foreach (var d in Arch_.Doors)
            {
                Vector3 a = d.FaceA, b = d.B != null ? d.FaceB : d.Centre;
                Vector3 side = Vector3.Cross(Vector3.up, d.Axis).normalized * (d.Width / 2f);
                var min = Vector3.Min(a - side, b + side); var max = Vector3.Max(a - side, b + side);
                Mark(min.x, min.z, max.x, max.z);
            }
            // greedy rectangles over the closed cells
            var used = new bool[n, n];
            var root = Kit.Group("RockCap", Level);
            var mat = Kit.Mat("RockCap", new Color(0.16f, 0.14f, 0.13f));
            int boxes = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    if (open[i, j] || used[i, j]) continue;
                    int w = 1;
                    while (i + w < n && !open[i + w, j] && !used[i + w, j]) w++;
                    int h = 1;
                    while (j + h < n)
                    {
                        bool ok = true;
                        for (int k = 0; k < w; k++) if (open[i + k, j + h] || used[i + k, j + h]) { ok = false; break; }
                        if (!ok) break;
                        h++;
                    }
                    for (int a = 0; a < w; a++) for (int b = 0; b < h; b++) used[i + a, j + b] = true;
                    float x = (i + w / 2f) * cell, z = -(j + h / 2f) * cell;
                    var go = Kit.Prim(PrimitiveType.Cube, root, new Vector3(x, 16.5f, z), new Vector3(w * cell, 1f, h * cell), mat, 0f, false, "Rock");
                    go.layer = Kit.OverviewLayer;
                    go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    boxes++;
                }
            Log.Add($"rock cap {boxes} boxes");
        }
    }
}
