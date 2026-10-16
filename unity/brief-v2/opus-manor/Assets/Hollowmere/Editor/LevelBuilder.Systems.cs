using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        public const int AreaGunRoom = 3, AreaHallFloor = 4, AreaHidden = 5;

        [MenuItem("Hollowmere/Rebuild Level")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SetupLayers();
            SetupNavSettings();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            s_Rooms.Clear(); s_Doors.Clear(); s_Clear.Clear(); s_Rng = new System.Random(7); s_PropCount = 0; s_Prefabs = null;
            MakeSurfaces();
            s_Level = new GameObject("Level").transform;
            Child(s_Level, "Doors");
            DefineLayout();
            foreach (var r in s_Rooms) BuildRoomShell(r);
            foreach (var d in s_Doors) BuildDoor(d);
            BuildGallery();
            BuildBackStair();
            BuildStage();
            BuildCells();
            BuildHiddenBookcase();
            DressFoyer(); DressGunRoom(); DressWatchRoom(); DressCells(); DressButcher(); DressBoiler(); DressKitchen(); DressStore(); DressBackStair();
            DressHall(); DressLibrary(); DressStrongRoom(); DressLab(); DressBallroom(); DressChapel(); DressCrypt();
            PlaceMarkers();
            var crowded = DoorwayClearance();
            BuildSystems();
            BakeNavMesh();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuild();
            return $"built {s_Rooms.Count} rooms, {s_Doors.Count} doors, {s_PropCount} props in {sw.Elapsed.TotalSeconds:F1}s" + (crowded.Length > 0 ? "; CROWDED DOORWAYS: " + crowded : "");
        }

        /// <summary>Lists prop colliders standing within 1.1 m of either side of a doorway, in front of its opening.</summary>
        static string DoorwayClearance()
        {
            Physics.SyncTransforms();
            var sb = new System.Text.StringBuilder();
            foreach (var d in s_Doors)
            {
                if (d.bottom > 0.01f) continue;
                Vector3 c = d.Centre; Vector3 u = d.alongX ? Vector3.right : Vector3.forward;
                foreach (var side in new[] { -1f, 1f })
                {
                    float gap = (d.g1 - d.g0) * 0.5f;
                    var centre = c + d.n * side * (gap + 0.6f) + Vector3.up * 1.0f;
                    var half = (d.alongX ? new Vector3((d.w - 0.1f) * 0.5f, 0.8f, 0.55f) : new Vector3(0.55f, 0.8f, (d.w - 0.1f) * 0.5f));
                    foreach (var col in Physics.OverlapBox(centre, half, Quaternion.identity, ~((1 << LayerDoorLeaf) | (1 << LayerHelper) | (1 << LayerCeiling)), QueryTriggerInteraction.Ignore))
                    {
                        var t = col.transform;
                        bool prop = false; for (var p = t; p != null; p = p.parent) if (p.name == "Props" || p.name == "Markers") { prop = true; break; }
                        if (prop) sb.Append($"[{d.A.name}|{d.B.name}: {col.name}] ");
                    }
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- markers

        static readonly string[] RangedBodies = { "SM_Chr_Jock_01", "SM_Chr_Gimp_01", "SM_Chr_Ghoul_01", "SM_Chr_Witch_01" };
        static readonly string[] MeleeBodies = { "SM_Chr_Butcher_01", "SM_Chr_Werewolf_01", "SM_Chr_Demon_01", "SM_Chr_Yokai_01" };
        static int s_Body;

        static void PlaceMarkers()
        {
            var gun = Get("gun"); var hall = Get("hall"); var ball = Get("ballroom");
            Vector3 gunDoor = new Vector3(14f, 0f, 7.0f); // a point in the foyer, seen through the entrance
            // the gun room: ranged behind the entrance, facing the foyer; melee through the rest
            foreach (var p in new[] { new Vector3(10.0f, 0f, 10.0f), new Vector3(18.0f, 0f, 10.0f), new Vector3(8.4f, 0f, 10.5f), new Vector3(19.6f, 0f, 10.5f) })
                Enemy(gun, "ranged", p, gunDoor - p);
            foreach (var (p, f) in new[] { (new Vector3(7.9f, 0f, 15.4f), Vector3.right), (new Vector3(11.6f, 0f, 13.6f), new Vector3(1f, 0f, -1f)), (new Vector3(20.1f, 0f, 16.4f), Vector3.left),
                                          (new Vector3(16.4f, 0f, 13.4f), new Vector3(-1f, 0f, -1f)), (new Vector3(12.0f, 0f, 18.6f), Vector3.back) })
                Enemy(gun, "melee", p, f);
            // the dining hall: ranged hold the gallery, melee on the floor
            foreach (var x in new[] { 6f, 11f, 16f, 21f, 26f })
                Enemy(hall, "ranged", new Vector3(x, DeckTop, 22.6f), Vector3.forward);
            foreach (var (p, f) in new[] { (new Vector3(9.0f, 0f, 25.3f), new Vector3(0.3f, 0f, -1f)), (new Vector3(23.0f, 0f, 25.3f), new Vector3(-0.3f, 0f, -1f)),
                                          (new Vector3(12.6f, 0f, 29.6f), Vector3.right), (new Vector3(19.6f, 0f, 29.6f), Vector3.left),
                                          (new Vector3(7.5f, 0f, 34.0f), new Vector3(1f, 0f, -1f)), (new Vector3(24.5f, 0f, 34.0f), new Vector3(-1f, 0f, -1f)),
                                          (new Vector3(27.6f, 0f, 29.0f), Vector3.left) })
                Enemy(hall, "melee", p, f);
            // the ballroom: the master and his bodyguards
            Enemy(ball, "master", new Vector3(14.5f, StageTop, 73.8f), new Vector3(0.25f, 0f, -1f));
            foreach (var p in new[] { new Vector3(13.0f, StageTop, 72.6f), new Vector3(19.0f, StageTop, 72.6f), new Vector3(12.2f, 0f, 68.4f), new Vector3(19.8f, 0f, 68.4f) })
                Enemy(ball, "melee", p, Vector3.back);
            // the master's assistant in the laboratory
            Enemy(Get("lab"), "melee", new Vector3(21.4f, 0f, 50.2f), Vector3.left);
            // pickups
            Pickup(gun, "best gear", BestGearPos);
            Pickup(Get("strong"), "hoard", HoardPos + new Vector3(0.1f, 0.92f, 0f));
        }

        static void Enemy(Room r, string kind, Vector3 pos, Vector3 face)
        {
            face.y = 0f;
            var root = new GameObject($"Enemy {kind}");
            root.transform.SetParent(r.markers, false);
            root.transform.SetPositionAndRotation(new Vector3(pos.x, r.fy + pos.y, pos.z), Quaternion.LookRotation(face.normalized, Vector3.up));
            root.layer = LayerMarker;
            var m = root.AddComponent<EnemyMarker>();
            m.kind = kind; m.room = r.name;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.9f, 0f); cap.radius = 0.3f; cap.height = 1.8f;
            string body = kind == "master" ? "SM_Chr_Count_01" : kind == "ranged" ? RangedBodies[s_Body++ % 4] : MeleeBodies[s_Body++ % 4];
            var ch = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(body));
            ch.transform.SetParent(root.transform, false);
            ch.transform.localPosition = Vector3.zero; ch.transform.localRotation = Quaternion.identity;
            foreach (var a in ch.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var c in ch.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in ch.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMarker;
            foreach (var rend in ch.GetComponentsInChildren<Renderer>(true)) rend.renderingLayerMask = r.RL;
            Pose(ch.transform, kind);
            // a coloured ring at the feet names the kind
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "MarkerRing";
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            ring.transform.localScale = new Vector3(0.95f, 0.01f, 0.95f);
            ring.layer = LayerMarker;
            var mr = ring.GetComponent<MeshRenderer>();
            mr.sharedMaterial = s_Mat[kind == "master" ? "marker_master" : kind == "ranged" ? "marker_ranged" : "marker_melee"];
            mr.renderingLayerMask = r.RL;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            // facing tick
            var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(tick.GetComponent<Collider>());
            tick.name = "MarkerFacing";
            tick.transform.SetParent(root.transform, false);
            tick.transform.localPosition = new Vector3(0f, 0.016f, 0.62f);
            tick.transform.localScale = new Vector3(0.12f, 0.01f, 0.32f);
            tick.layer = LayerMarker;
            tick.GetComponent<MeshRenderer>().sharedMaterial = mr.sharedMaterial;
            tick.GetComponent<MeshRenderer>().renderingLayerMask = r.RL;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static void Aim(Transform from, Transform to, Vector3 dir)
        {
            if (from == null || to == null) return;
            var cur = to.position - from.position;
            if (cur.sqrMagnitude < 1e-6f) return;
            from.rotation = Quaternion.FromToRotation(cur, dir.normalized) * from.rotation;
        }

        /// <summary>Brings the T-posed rig's arms down; ranged markers aim a revolver, melee hold a blade.</summary>
        static void Pose(Transform ch, string kind)
        {
            Vector3 fwd = ch.forward, right = ch.right, down = Vector3.down;
            foreach (var s in new[] { "L", "R" })
            {
                var sh = FindDeep(ch, "Shoulder_" + s); var el = FindDeep(ch, "Elbow_" + s); var hd = FindDeep(ch, "Hand_" + s);
                Vector3 outw = s == "R" ? right : -right;
                if (kind == "ranged" && s == "R")
                {
                    Aim(sh, el, fwd * 0.95f + down * 0.12f + outw * 0.12f);
                    Aim(el, hd, fwd + down * 0.04f - outw * 0.05f);
                }
                else if (kind == "melee" && s == "R")
                {
                    Aim(sh, el, down * 0.85f + outw * 0.22f + fwd * 0.2f);
                    Aim(el, hd, fwd * 0.75f + down * 0.45f);
                }
                else
                {
                    Aim(sh, el, down + outw * 0.16f + fwd * 0.04f);
                    Aim(el, hd, down + fwd * 0.22f + outw * 0.04f);
                }
            }
            var hand = FindDeep(ch, "Hand_R");
            if (hand == null || kind == "master") return;
            var w = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(kind == "ranged" ? "SM_Wep_Revolver_01" : "SM_Wep_Hatchet_01"));
            foreach (var c in w.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            w.transform.SetParent(hand, true);
            w.transform.position = hand.position + fwd * 0.06f;
            w.transform.rotation = kind == "ranged" ? Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(0f, 90f, 0f)
                                                    : Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(-70f, 0f, 0f);
            foreach (var t in w.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMarker;
        }

        static void Pickup(Room r, string kind, Vector3 pos)
        {
            var root = new GameObject($"Pickup {kind}");
            root.transform.SetParent(r.markers, false);
            root.transform.position = new Vector3(pos.x, r.fy + pos.y, pos.z);
            var m = root.AddComponent<PickupMarker>();
            m.kind = kind; m.room = r.name;
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.name = "MarkerRing";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ring.transform.localScale = new Vector3(0.7f, 0.006f, 0.7f);
            var mr = ring.GetComponent<MeshRenderer>();
            mr.sharedMaterial = s_Mat["marker_pickup"]; mr.renderingLayerMask = r.RL; mr.shadowCastingMode = ShadowCastingMode.Off;
            var l = Lamp(r, "PickupGlow", root.transform.position + Vector3.up * 0.5f, new Color(1f, 0.85f, 0.45f), 0.8f, 2.2f);
            l.transform.SetParent(root.transform, true);
        }

        // ---------------------------------------------------------------- systems

        static Vector3 s_Min, s_Max;

        static void BuildSystems()
        {
            // level bounds from the rooms
            s_Min = new Vector3(s_Rooms.Min(r => r.x0) - T, 0f, s_Rooms.Min(r => r.z0) - T);
            s_Max = new Vector3(s_Rooms.Max(r => r.x1) + T, s_Rooms.Max(r => r.fy + r.h), s_Rooms.Max(r => r.z1) + T);

            // the player at the front door, facing the hall towards the gun room entrance
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interact/IronCitadelPlayer.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.SetPositionAndRotation(StartPos, Quaternion.identity);

            var sys = new GameObject("Systems");
            sys.AddComponent<LevelHud>();
            var ov = sys.AddComponent<OverviewMode>();
            var camGo = new GameObject("OverviewCamera");
            camGo.transform.SetParent(sys.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            cam.cullingMask = ~((1 << LayerCeiling) | (1 << LayerHelper));
            cam.depth = 10;
            cam.enabled = false;
            var ucd = camGo.AddComponent<UniversalAdditionalCameraData>();
            ucd.renderPostProcessing = true;
            ov.overviewCamera = cam; ov.levelMin = s_Min; ov.levelMax = s_Max;
            ov.Fit();

            // look: ambient, fog, reflections, post-processing
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.125f, 0.135f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.022f;
            RenderSettings.fogColor = new Color(0.035f, 0.033f, 0.04f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.15f;
            DynamicGI.UpdateEnvironment();
            var ls = new LightingSettings { name = "IronCitadelLighting", bakedGI = false, realtimeGI = false };
            AssetDatabase.CreateAsset(ls, "Assets/Hollowmere/Settings/IronCitadelLighting.lighting");
            Lightmapping.lightingSettings = ls;

            var volGo = new GameObject("PostProcess");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = MakeProfile();
        }

        static VolumeProfile MakeProfile()
        {
            const string path = "Assets/Hollowmere/Settings/IronCitadelVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            T Add<T>() where T : VolumeComponent { var c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); return c; }
            var tm = Add<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
            var ca = Add<ColorAdjustments>(); ca.postExposure.Override(1.05f); ca.contrast.Override(12f); ca.saturation.Override(-12f); ca.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            var bl = Add<Bloom>(); bl.intensity.Override(0.55f); bl.threshold.Override(0.95f); bl.scatter.Override(0.65f);
            var vg = Add<Vignette>(); vg.intensity.Override(0.32f); vg.smoothness.Override(0.45f);
            var fg = Add<FilmGrain>(); fg.type.Override(FilmGrainLookup.Thin1); fg.intensity.Override(0.18f);
            var sm = Add<ShadowsMidtonesHighlights>(); sm.shadows.Override(new Vector4(0.95f, 0.98f, 1.06f, 0f)); sm.highlights.Override(new Vector4(1.04f, 1.0f, 0.94f, 0f));
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // ---------------------------------------------------------------- navigation

        static void BakeNavMesh()
        {
            var navGo = new GameObject("Navigation");
            var surf = navGo.AddComponent<NavMeshSurface>();
            surf.agentTypeID = 0;
            surf.collectObjects = CollectObjects.All;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = ~(1 << LayerCeiling);
            surf.overrideVoxelSize = true; surf.voxelSize = 0.1f;
            void Area(string name, Vector3 min, Vector3 max, int area)
            {
                var g = new GameObject(name);
                g.transform.SetParent(navGo.transform, false);
                g.layer = LayerHelper;
                var v = g.AddComponent<NavMeshModifierVolume>();
                v.center = (min + max) * 0.5f; v.size = max - min; v.area = area;
            }
            var gun = Get("gun"); var hall = Get("hall");
            Area("GunRoomArea", new Vector3(gun.x0 - 0.1f, -0.5f, gun.z0 - 0.3f), new Vector3(gun.x1 + 0.1f, 3f, gun.z1 + 0.25f), AreaGunRoom);
            Area("HallFloorArea", new Vector3(hall.x0 + 0.75f, -0.5f, hall.z0), new Vector3(hall.x1, 2.5f, hall.z1), AreaHallFloor);
            Area("HiddenThresholdArea", new Vector3(3.45f, -0.5f, 40.0f), new Vector3(4.15f, 2.0f, 41.45f), AreaHidden);
            // bake with every door standing open
            foreach (var sw in Object.FindObjectsByType<SwingDoor>(FindObjectsInactive.Include)) sw.SetOpenImmediate(true);
            surf.BuildNavMesh();
            foreach (var sw in Object.FindObjectsByType<SwingDoor>(FindObjectsInactive.Include)) sw.SetOpenImmediate(false);
            // keep the data with the scene
            var data = surf.navMeshData;
            string path = "Assets/Scenes/IronCitadel_NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
            surf.navMeshData = data;
        }

        static void SetupNavSettings()
        {
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var areas = so.FindProperty("areas");
            void Name(int i, string n, float cost) { var e = areas.GetArrayElementAtIndex(i); e.FindPropertyRelative("name").stringValue = n; e.FindPropertyRelative("cost").floatValue = cost; }
            Name(AreaGunRoom, "GunRoom", 1f); Name(AreaHallFloor, "HallFloor", 1f); Name(AreaHidden, "HiddenThreshold", 1f);
            var settings = so.FindProperty("m_Settings");
            var s0 = settings.GetArrayElementAtIndex(0);
            s0.FindPropertyRelative("agentRadius").floatValue = 0.4f;
            s0.FindPropertyRelative("agentHeight").floatValue = 1.8f;
            s0.FindPropertyRelative("agentClimb").floatValue = 0.3f;
            s0.FindPropertyRelative("agentSlope").floatValue = 45f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetupLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void L(int i, string n) { layers.GetArrayElementAtIndex(i).stringValue = n; }
            L(LayerCeiling, "Ceiling"); L(LayerDoorLeaf, "DoorLeaf"); L(LayerMarker, "Marker"); L(LayerHelper, "SeeThrough");
            // one rendering layer per room keeps each room's lights inside it
            var rl = tm.FindProperty("m_RenderingLayers");
            while (rl.arraySize < 24) rl.InsertArrayElementAtIndex(rl.arraySize);
            for (int i = 1; i < 24; i++) rl.GetArrayElementAtIndex(i).stringValue = "Room " + i;
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddToBuild()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
