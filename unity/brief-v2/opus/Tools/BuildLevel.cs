// Builds Assets/Scenes/IronCitadel.unity from scratch out of the Synty POLYGON dungeon kits.
// Run on an open editor:
//   unity command --timeout 600 run_script -- --file Tools/BuildLevel.cs --entry BuildLevel.Run --timeout_ms 590000
// Layout is on a 5 m grid, 1 unit = 1 m, y up, floors at y = 0. Every room has its own inward-facing walls
// (their backs on the room's outline), its own floor tiles and its own ceiling at the room's height.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using IronCitadel;
using Object = UnityEngine.Object;
using Random = System.Random;

public static class BuildLevel
{
    public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
    const float WT = 0.43f;   // wall thickness from the outline to the brick face
    const float WB = 0.16f;   // wall pivot offset from the back face

    // ---------------------------------------------------------------- data
    public class RoomDef
    {
        public string id, name, kind, state, tile;
        public float x0, x1, z0, z1, y, h;
        public Transform tr, floor, walls, ceil, props, lights, hang;
        public Room comp;
        public float ix0 => x0 + WT; public float ix1 => x1 - WT; public float iz0 => z0 + WT; public float iz1 => z1 - WT;
        public float cx => (x0 + x1) * 0.5f; public float cz => (z0 + z1) * 0.5f;
        public int Layers => Mathf.CeilToInt(h / 5f - 0.01f);
    }

    public class DoorDef
    {
        public string a, b, style, state; public float x, z, y; public int layer;
        public bool OnZEdge; // the wall runs along x (a z = const boundary)
        public GameObject leaf;
    }

    static readonly List<RoomDef> rooms = new List<RoomDef>();
    static readonly List<DoorDef> doors = new List<DoorDef>();
    static Dictionary<string, string> index;
    static readonly Dictionary<string, Bounds> boundsCache = new Dictionary<string, Bounds>();
    static Random rng;
    static Transform root, markersRoot, doorsRoot;
    static readonly List<string> log = new List<string>();
    static Material mRanged, mMelee, mWarlord, mGear, mHoard, mHelper;

    static RoomDef Rm(string id) => rooms.First(r => r.id == id);

    static void DefineLayout()
    {
        rooms.Clear(); doors.Clear();
        AddRoom("E", "Entry Hall", "entry hall", "decayed", 0, 15, 0, 10, 5f, "D/SM_Env_Tiles_03");
        AddRoom("A", "Armory", "armory", "stocked", 0, 15, 10, 20, 5f, "D/SM_Env_Tiles_05");
        AddRoom("S", "Storeroom", "storeroom", "looted", -10, 0, 0, 10, 5f, "D/SM_Env_Tiles_04");
        AddRoom("K", "Kitchen", "kitchen", "decayed", -10, 0, 10, 20, 3f, "D/SM_Env_Tiles_08");
        AddRoom("X", "Servery Stair", "extra", "decayed", -10, 0, 20, 35, 10f, "D/SM_Env_Tiles_04");
        AddRoom("F", "Forge", "forge", "decayed", -20, -10, 0, 10, 5f, "D/SM_Env_Tiles_09");
        AddRoom("G", "Guard Room", "guard room", "looted", 15, 25, 0, 10, 3f, "D/SM_Env_Tiles_04");
        AddRoom("P", "Prison", "prison", "looted", 15, 30, 10, 20, 3f, "D/SM_Env_Tiles_09");
        AddRoom("TC", "Torture Chamber", "torture chamber", "decayed", 30, 40, 10, 20, 3f, "D/SM_Env_Tiles_09");
        AddRoom("D", "Great Dining Hall", "dining hall", "looted", 0, 30, 20, 40, 10f, "D/SM_Env_Tiles_06");
        AddRoom("L", "Library", "library", "looted", 15, 30, 40, 55, 7.5f, "D/SM_Env_Tiles_03");
        AddRoom("AS", "Alchemist's Study", "alchemist's study", "looted", 30, 40, 40, 50, 5f, "D/SM_Env_Tiles_08");
        AddRoom("V", "Treasure Vault", "treasure vault", "untouched", 30, 35, 50, 55, 3f, "D/SM_Env_Tiles_Ornate_01");
        AddRoom("H", "Shrine", "shrine", "looted", 0, 15, 40, 55, 10f, "D/SM_Env_Tiles_Rune_01");
        AddRoom("C", "Crypt", "crypt", "decayed", -10, 0, 40, 50, 3f, "D/SM_Env_Tiles_09");
        AddRoom("T", "Throne Room", "throne room", "decayed", -5, 20, 55, 85, 15f, "D/SM_Env_Tiles_Ornate_01");

        AddDoor("E", "A", 7.5f, 10f, "framed", "open");
        AddDoor("A", "D", 7.5f, 20f, "single", "shut");
        AddDoor("E", "S", 0f, 2.5f, "single", "shut");
        AddDoor("S", "K", -7.5f, 10f, "single", "open");
        AddDoor("S", "F", -10f, 7.5f, "round", "open");
        AddDoor("K", "X", -7.5f, 20f, "single", "shut");
        AddDoor("X", "D", 0f, 22.5f, "single", "open");
        AddDoor("X", "D", 0f, 22.5f, "single", "open", 1);   // up on the gallery
        AddDoor("D", "L", 22.5f, 40f, "large", "open");
        AddDoor("L", "H", 15f, 47.5f, "double", "open");
        AddDoor("L", "AS", 30f, 42.5f, "single", "shut");
        AddDoor("L", "V", 30f, 52.5f, "hidden", "hidden");
        AddDoor("H", "C", 0f, 42.5f, "round", "open");
        AddDoor("H", "T", 7.5f, 55f, "large", "open");
        AddDoor("E", "G", 15f, 7.5f, "single", "open");
        AddDoor("G", "P", 17.5f, 10f, "single", "open");
        AddDoor("P", "TC", 30f, 12.5f, "single", "shut");
    }

    static void AddRoom(string id, string name, string kind, string state, float x0, float x1, float z0, float z1, float h, string tile)
        => rooms.Add(new RoomDef { id = id, name = name, kind = kind, state = state, x0 = x0, x1 = x1, z0 = z0, z1 = z1, y = 0f, h = h, tile = tile });

    static void AddDoor(string a, string b, float x, float z, string style, string state, int layer = 0)
    {
        var d = new DoorDef { a = a, b = b, x = x, z = z, style = style, state = state, layer = layer, y = layer * 5f };
        var ra = Rm(a);
        d.OnZEdge = Mathf.Abs(z - ra.z0) < 0.01f || Mathf.Abs(z - ra.z1) < 0.01f;
        doors.Add(d);
    }

    // ---------------------------------------------------------------- entry
    public static string Run(string args)
    {
        rng = new Random(20261013);
        log.Clear(); boundsCache.Clear();
        BuildIndex();
        ConfigureNavAgent();
        DefineLayout();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeMaterials();
        root = new GameObject("IronCitadel").transform;
        var roomsRoot = new GameObject("Rooms").transform; roomsRoot.SetParent(root);
        doorsRoot = new GameObject("Doors").transform; doorsRoot.SetParent(root);
        markersRoot = new GameObject("Markers").transform; markersRoot.SetParent(root);

        foreach (var r in rooms) BuildShell(r, roomsRoot);
        foreach (var d in doors) BuildDoor(d);
        BuildGallery();
        BuildServeryStair();
        BuildStage();
        DressAll();
        PlaceMarkers();
        SetupLightingAndPost();
        var hud = SetupPlayerAndHud();
        BakeNavMesh();
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(SceneManager_GetActive(), ScenePath);
        AddToBuild();
        foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            foreach (var m in rend.sharedMaterials)
                if (m == null && rend is ParticleSystemRenderer) continue;
                else if (m == null || m.shader == null || m.shader.name.Contains("Error") || !m.shader.isSupported)
                    log.Add("bad material on " + rend.name + ": " + (m == null ? "null" : m.name + " / " + m.shader.name));
        File.WriteAllLines("Notes/build_log.txt", log);
        return "built " + rooms.Count + " rooms, " + doors.Count + " doors, objects=" + root.GetComponentsInChildren<Transform>(true).Length + " warnings=" + log.Count;
    }

    static UnityEngine.SceneManagement.Scene SceneManager_GetActive() => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

    static void AddToBuild()
    {
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void ConfigureNavAgent()
    {
        var o = Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings");
        var so = new SerializedObject(o);
        var s = so.FindProperty("m_Settings").GetArrayElementAtIndex(0);
        s.FindPropertyRelative("agentRadius").floatValue = 0.45f;
        s.FindPropertyRelative("agentHeight").floatValue = 1.85f;
        s.FindPropertyRelative("agentSlope").floatValue = 40f;
        s.FindPropertyRelative("agentClimb").floatValue = 0.28f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- prefab helpers
    static void BuildIndex()
    {
        if (index != null) return;
        index = new Dictionary<string, string>();
        var packs = new (string key, string folder)[] {
            ("D", "Assets/Synty/PolygonDungeon/Prefabs"), ("R", "Assets/Synty/PolygonDungeonRealms/Prefabs"),
            ("M", "Assets/Synty/PolygonDungeonMap/Prefabs"), ("G", "Assets/Synty/PolygonGeneric/Prefabs") };
        foreach (var p in packs)
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { p.folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var k = p.key + "/" + Path.GetFileNameWithoutExtension(path);
                if (!index.ContainsKey(k)) index[k] = path;
            }
    }

    static GameObject Prefab(string key)
    {
        if (!index.TryGetValue(key, out var path)) { log.Add("missing prefab " + key); return null; }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static Bounds LocalBounds(string key)
    {
        if (boundsCache.TryGetValue(key, out var b)) return b;
        var go = Prefab(key);
        bool has = false; b = new Bounds();
        if (go != null)
        {
            var inv = go.transform.worldToLocalMatrix;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds; var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var w = m.MultiplyPoint3x4(c);
                    if (!has) { b = new Bounds(w, Vector3.zero); has = true; } else b.Encapsulate(w);
                }
            }
        }
        boundsCache[key] = b;
        return b;
    }

    static Vector3 Rot(float deg, Vector3 v) => Quaternion.Euler(0, deg, 0) * v;

    /// Pivot placement.
    static GameObject Put(string key, Vector3 pos, float rotY, Transform parent, Vector3? scale = null, Vector3? euler = null)
    {
        var p = Prefab(key); if (p == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        go.transform.position = pos;
        go.transform.rotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.Euler(0, rotY, 0);
        if (scale.HasValue) go.transform.localScale = scale.Value;
        return go;
    }

    /// Places the prefab so its bounds' centre lands on (c.x, c.z) and its bottom on y = bottom.
    static GameObject PutC(string key, float cx, float cz, float bottom, float rotY, Transform parent, Vector3? scale = null)
    {
        var s = scale ?? Vector3.one;
        var lb = LocalBounds(key);
        var cl = Vector3.Scale(lb.center, s);
        var off = Rot(rotY, new Vector3(cl.x, 0, cl.z));
        var pos = new Vector3(cx - off.x, bottom - lb.min.y * s.y, cz - off.z);
        return Put(key, pos, rotY, parent, s);
    }

    static float SideRot(char side) => side == 'S' ? 0f : side == 'N' ? 180f : side == 'W' ? 90f : 270f;

    static Vector3 Inward(char side) => side == 'S' ? Vector3.forward : side == 'N' ? Vector3.back : side == 'W' ? Vector3.right : Vector3.left;

    static Vector3 FacePoint(RoomDef r, char side, float along)
        => side == 'S' ? new Vector3(along, 0, r.iz0) : side == 'N' ? new Vector3(along, 0, r.iz1) : side == 'W' ? new Vector3(r.ix0, 0, along) : new Vector3(r.ix1, 0, along);

    /// Against a wall's brick face, facing into the room (prefab front = local +z). y is the pivot height,
    /// or, when bottom is true, the height of the prefab's bottom.
    static GameObject Against(string key, RoomDef r, char side, float along, float y, Transform parent, float gap = 0.02f, Vector3? scale = null, bool bottom = true, float extraRot = 0f)
    {
        var s = scale ?? Vector3.one;
        var lb = LocalBounds(key);
        float rot = SideRot(side) + extraRot;
        var back = new Vector3(lb.center.x * s.x, 0, lb.min.z * s.z);
        if (Mathf.Abs(extraRot - 180f) < 1f) back = new Vector3(lb.center.x * s.x, 0, lb.max.z * s.z);
        var pos = FacePoint(r, side, along) + Inward(side) * gap - Rot(rot, back);
        pos.y = bottom ? y - lb.min.y * s.y : y;
        return Put(key, pos, rot, parent, s);
    }

    /// Particle effects scale with their transform (Synty's default is local scaling, which ignores it).
    static GameObject Fx(string key, Vector3 pos, Transform parent, float scale)
    {
        var go = Put(key, pos, 0, parent, Vector3.one * scale);
        if (go == null) return null;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; }
        return go;
    }

    static void NoCol(GameObject go) { if (go == null) return; foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); }

    static T Pick<T>(params T[] items) => items[rng.Next(items.Length)];
    static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static GameObject Group(string name, Transform parent) { var g = new GameObject(name); g.transform.SetParent(parent, false); return g; }

    static void SetStatic(GameObject go) { if (go != null) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic); }

    // ---------------------------------------------------------------- shell
    static void BuildShell(RoomDef r, Transform parent)
    {
        r.tr = Group(r.name, parent).transform;
        r.floor = Group("Floor", r.tr).transform;
        r.walls = Group("Walls", r.tr).transform;
        r.ceil = Group("Ceiling", r.tr).transform;
        r.hang = Group("Hanging", r.ceil).transform;   // chandeliers, chains, cobwebs: hidden with the ceiling in the overview
        r.props = Group("Props", r.tr).transform;
        r.lights = Group("Lights", r.tr).transform;
        r.comp = r.tr.gameObject.AddComponent<Room>();
        r.comp.roomName = r.name; r.comp.kind = r.kind; r.comp.state = r.state; r.comp.floorY = r.y; r.comp.height = r.h;
        r.comp.rects = new[] { new Rect(r.ix0, r.iz0, r.ix1 - r.ix0, r.iz1 - r.iz0) };
        r.comp.ceilingGroup = r.ceil.gameObject;

        var tb = LocalBounds(r.tile);
        for (float x = r.x0; x < r.x1 - 0.01f; x += 5f)
            for (float z = r.z0; z < r.z1 - 0.01f; z += 5f)
                SetStatic(PutC(r.tile, x + 2.5f, z + 2.5f, r.y - tb.size.y, 90f * rng.Next(4), r.floor));
        string ceilKey = (r.id == "K" || r.id == "S" || r.id == "G") ? "D/SM_Env_Ceiling_Stone_Flat_02" : "D/SM_Env_Ceiling_Stone_Flat_01";
        for (float x = r.x0; x < r.x1 - 0.01f; x += 5f)
            for (float z = r.z0; z < r.z1 - 0.01f; z += 5f)
                SetStatic(PutC(ceilKey, x + 2.5f, z + 2.5f, r.y + r.h, 0f, r.ceil));

        foreach (char side in new[] { 'S', 'N', 'W', 'E' })
        {
            bool alongX = side == 'S' || side == 'N';
            float a0 = alongX ? r.x0 : r.z0, a1 = alongX ? r.x1 : r.z1;
            for (float a = a0; a < a1 - 0.01f; a += 5f)
                for (int layer = 0; layer < r.Layers; layer++)
                {
                    var door = DoorOn(r, side, a, layer);
                    string key = WallKey(r, side, a, layer, door);
                    if (key == null) continue;
                    float y = r.y + layer * 5f;
                    Vector3 pos; float rot = SideRot(side);
                    if (side == 'S') pos = new Vector3(a + 5f, y, r.z0 + WB);
                    else if (side == 'N') pos = new Vector3(a, y, r.z1 - WB);
                    else if (side == 'W') pos = new Vector3(r.x0 + WB, y, a);
                    else pos = new Vector3(r.x1 - WB, y, a + 5f);
                    SetStatic(Put(key, pos, rot, r.walls));
                }
        }
    }

    static DoorDef DoorOn(RoomDef r, char side, float a, int layer)
    {
        foreach (var d in doors)
        {
            if (d.a != r.id && d.b != r.id) continue;
            if (d.layer != layer) continue;
            if (side == 'S' && d.OnZEdge && Mathf.Abs(d.z - r.z0) < 0.01f && d.x > a && d.x < a + 5f) return d;
            if (side == 'N' && d.OnZEdge && Mathf.Abs(d.z - r.z1) < 0.01f && d.x > a && d.x < a + 5f) return d;
            if (side == 'W' && !d.OnZEdge && Mathf.Abs(d.x - r.x0) < 0.01f && d.z > a && d.z < a + 5f) return d;
            if (side == 'E' && !d.OnZEdge && Mathf.Abs(d.x - r.x1) < 0.01f && d.z > a && d.z < a + 5f) return d;
        }
        return null;
    }

    static string WallKey(RoomDef r, char side, float a, int layer, DoorDef door)
    {
        if (door != null)
        {
            switch (door.style)
            {
                case "large": return null;
                case "double": case "framed": return "D/SM_Env_Wall_DoorFrame_Double_01";
                case "round": return "D/SM_Env_Wall_DoorFrame_Round_01";
                default: return "D/SM_Env_Wall_DoorFrame_01";
            }
        }
        // Burial niches in the crypt, plain courses elsewhere.
        if (r.id == "C" && layer == 0 && side != 'E' && rng.NextDouble() < 0.75) return "D/SM_Env_Wall_Alcove_Small_01";
        if (layer >= 1 && (r.id == "T" || r.id == "D" || r.id == "H") && rng.NextDouble() < 0.25) return "D/SM_Env_Wall_04";
        return Pick("D/SM_Env_Wall_01", "D/SM_Env_Wall_01", "D/SM_Env_Wall_03", "D/SM_Env_Wall_04", "D/SM_Env_Wall_01_Alt");
    }

    // ---------------------------------------------------------------- doors
    static void BuildDoor(DoorDef d)
    {
        var ra = Rm(d.a); var rb = Rm(d.b);
        var go = new GameObject($"Door {ra.name} - {rb.name}" + (d.layer > 0 ? " (gallery)" : ""));
        go.transform.SetParent(doorsRoot, false);
        go.transform.position = new Vector3(d.x, d.y, d.z);
        go.transform.rotation = Quaternion.Euler(0, d.OnZEdge ? 0f : 90f, 0);
        var link = go.AddComponent<DoorLink>();
        link.roomA = ra.name; link.roomB = rb.name; link.state = d.state;
        float rot = d.OnZEdge ? 0f : 90f;

        if (d.style == "large" || d.style == "framed")
            SetStatic(PutC("D/SM_Env_Door_Large_Frame_01", d.x, d.z, d.y, rot, go.transform));

        if (d.state == "shut")
        {
            // Hinge at one jamb; the leaf (pivot on its right edge, extending along its -x) is turned so it
            // extends along the hinge's +x across the 1.6 m opening.
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(go.transform, false);
            hinge.transform.localPosition = new Vector3(-0.8f, 0, 0);
            hinge.transform.localRotation = Quaternion.identity;
            var sw = hinge.AddComponent<SwingDoor>(); sw.leafSide = 1f;
            string leafKey = d.a == "P" || d.b == "TC" ? "D/SM_Env_Door_05" : d.a == "A" ? "D/SM_Env_Door_04" : "D/SM_Env_Door_03";
            var leaf = Put(leafKey, Vector3.zero, 0, hinge.transform);
            leaf.transform.localPosition = new Vector3(0.0f, 0.0f, 0f);
            leaf.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            leaf.transform.localScale = new Vector3(1.2f, 0.93f, 1f);
            leaf.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            Carve(leaf);
            link.leaf = hinge;
            d.leaf = hinge;
        }
        if (d.state == "hidden") BuildSecretBookcase(d, link);
        if (d.a == "G" && d.b == "P")
        {
            // The prison door was forced in the riot: its barred leaf lies torn off beside the doorway.
            var p = Rm("P");
            var bars = Put("D/SM_Env_Door_Bars_01", new Vector3(19.4f, 0.06f, 11.6f), 0, p.props, null, new Vector3(-88f, 35f, 0f));
            NoCol(bars);
        }
    }

    static void BuildSecretBookcase(DoorDef d, DoorLink link)
    {
        var lib = Rm("L");
        var holder = new GameObject("Secret Bookcase");
        holder.transform.SetParent(lib.props, false);
        var bc = Against("M/SM_Prop_Bookcase_01", lib, 'E', d.z, 0f, holder.transform, 0.01f);
        bc.name = "Bookcase (secret)";
        var sb = bc.AddComponent<SecretBookcase>();
        sb.slide = new Vector3(0, 0, -2.65f);
        bc.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        Carve(bc);
        link.leaf = bc;
        d.leaf = bc;
    }

    /// A carving NavMesh obstacle the size of the object's colliders, so routes planned on the NavMesh go round
    /// a shut door or the bookcase wherever it stands, and through the opening once it has moved.
    static void Carve(GameObject go)
    {
        Physics.SyncTransforms();
        var cols = go.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return;
        Bounds b = cols[0].bounds; foreach (var c in cols) b.Encapsulate(c.bounds);
        var ob = go.AddComponent<NavMeshObstacle>();
        ob.shape = NavMeshObstacleShape.Box;
        ob.center = go.transform.InverseTransformPoint(b.center);
        var size = go.transform.InverseTransformVector(b.size);
        ob.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        ob.carving = true; ob.carveOnlyStationary = true;
    }

    // ---------------------------------------------------------------- gallery, stair, stage
    const float DeckEdge = 23.75f;   // the gallery deck runs from the south wall to z = 23.75

    static void BuildGallery()
    {
        var D = Rm("D");
        var g = Group("Gallery", D.tr).transform;
        var deck = Group("Deck", g).transform;
        var tb = LocalBounds("D/SM_Env_Tiles_03");
        float depth = DeckEdge - 20f, sz = depth / 5f;
        for (float x = 0; x < 30f; x += 5f)
        {
            SetStatic(PutC("D/SM_Env_Tiles_03", x + 2.5f, 20f + depth * 0.5f, 5f - tb.size.y, 0, deck, new Vector3(1f, 1f, sz)));
            SetStatic(PutC("D/SM_Env_Ceiling_Stone_Flat_01", x + 2.5f, 20f + depth * 0.5f, 5f - tb.size.y - 0.15f, 0, deck, new Vector3(1f, 1f, sz)));
        }
        // the deck's edge beam and the posts carrying it
        for (float x = 0; x < 30f; x += 5f)
            SetStatic(PutC("R/SM_Env_Dwarf_Beam_04", x + 2.5f, DeckEdge - 0.2f, 4.35f, 0, g));
        foreach (float x in new[] { 5f, 10f, 15f, 20f, 25f })
            SetStatic(PutC("D/SM_Env_Pillar_Square_06", x, DeckEdge - 0.32f, 0f, 0, g, new Vector3(1f, 0.95f, 1f)));
        // An archers' firing step along the rail: a 0.28 m stone ledge, 0.9 m deep, that a person steps up onto
        // to see down over the rail.
        var stepT = Group("Firing Step", deck).transform;
        // it starts clear of the doorway from the servery stair at the west end
        float sx0 = 1.6f, sx1 = 29.57f, seg = (sx1 - sx0) / 6f;
        for (int i = 0; i < 6; i++)
            SetStatic(PutC("R/SM_Env_Dwarf_Wall_Block_Cube_01", sx0 + seg * (i + 0.5f), DeckEdge - 0.2f - 0.45f - 0.125f, 5f + 0.28f - 5f * 0.06f, 0, stepT, new Vector3(seg / 5f, 0.06f, 0.9f / 5f)));
        // A waist-high open rail along the edge: stone posts carrying a slim stone top rail at 0.85 m, open
        // beneath (too low a gap for anyone to pass), so the floor below shows under and over it.
        var rail = Group("Rail", g).transform;
        float rz = DeckEdge - 0.2f;
        for (int k = 0; k <= 12; k++)
        {
            float x = 0.62f + k * 2.405f;
            SetStatic(PutC("D/SM_Env_Railing_Pillar_05", x, rz, 5f, 0, rail, new Vector3(1f, 0.68f, 1f)));
            if (k == 12) break;
            var beam = PutC("R/SM_Env_Dwarf_Beam_04", x + 1.2025f, rz, 5.65f, 0, rail, new Vector3(2.405f / 5f, 0.25f, 0.5f));
            SetStatic(beam);
        }
        D.comp.galleryHeight = 5f;
    }

    static void BuildServeryStair()
    {
        var X = Rm("X");
        var s = Group("Stair", X.tr).transform;
        // Two straight runs of steps up the east wall, rising south to a landing in front of the gallery
        // door. Stairs_01's collider is a ramp: y = 0.24 + 0.5136 d (local), 4.79 m per piece. Scaled so the
        // two pieces climb exactly 5 m over 8.81 m (about 30 degrees), from z = 33.0 down to z = 24.19.
        float sy = 5.0f / (0.24f + 0.5136f * 9.58f), sz = 0.92f;
        float pz1 = 33.0f - 0.03f * sz;
        Put("D/SM_Env_Stairs_01", new Vector3(-3.38f, 0f, pz1), 0, s, new Vector3(0.6f, sy, sz));
        Put("D/SM_Env_Stairs_01", new Vector3(-3.38f, 2.46f * sy, pz1 - 4.79f * sz), 0, s, new Vector3(0.6f, sy, sz));
        // the landing (deck) z 20..24.2 and its soffit
        var tb = LocalBounds("D/SM_Env_Tiles_03");
        SetStatic(PutC("D/SM_Env_Tiles_03", -2.5f, 22.1f, 5f - tb.size.y, 0, s, new Vector3(1f, 1f, 0.84f)));
        SetStatic(PutC("D/SM_Env_Ceiling_Stone_Flat_01", -2.5f, 22.1f, 5f - tb.size.y - 0.15f, 0, s, new Vector3(1f, 1f, 0.84f)));
        SetStatic(PutC("D/SM_Env_Pillar_Square_06", -4.72f, 23.95f, 0f, 0, s, new Vector3(0.8f, 0.97f, 0.8f)));
        // a wall between the flight and the rest of the room, full height, from the landing to near the foot
        foreach (float z0 in new[] { 24.1f, 26.1f })
            for (int layer = 0; layer < 2; layer++)
                SetStatic(Put("D/SM_Env_Wall_01_DoubleSided", new Vector3(-3.67f, layer * 5.05f, z0), 90f, s));
        // railings on the landing's open edges: west edge (runs along z) and north edge west of the wall
        Put("D/SM_Env_Railing_01", new Vector3(-5f, 5f, 24.2f), 270f, s, new Vector3(0.76f, 1, 1));
        Put("D/SM_Env_Railing_01", new Vector3(-5.0f, 5f, 24.2f), 180f, s, new Vector3(0.212f, 1, 1));
    }

    static void BuildStage()
    {
        var T = Rm("T");
        var st = Group("Stage", T.tr).transform;
        // the dais: two dwarven stone blocks sunk so their tops stand 1.5 m proud of the floor
        foreach (float x in new[] { 5f, 10f })
            SetStatic(PutC("R/SM_Env_Dwarf_Wall_Block_Cube_01", x, 82.0f, 1.5f - 5f, 0, st));
        // six 0.25 m steps up the front
        var sk = "R/SM_Env_Dwarf_Wall_Block_Cube_01";
        var lb = LocalBounds(sk);
        for (int i = 0; i < 6; i++)
        {
            float front = 76.5f + 0.5f * i, depth = 79.7f - front, top = 0.25f * (i + 1);
            var sc = new Vector3(7f / lb.size.x, top / lb.size.y, depth / lb.size.z);
            SetStatic(PutC(sk, 7.5f, front + depth * 0.5f, 0f, 0, st, sc));
        }
        // an invisible ramp over the step noses so the walk up is smooth
        var ramp = new GameObject("StepRamp (invisible helper)");
        ramp.transform.SetParent(st, false);
        float ang = Mathf.Atan2(1.5f, 3.0f);
        ramp.transform.position = new Vector3(7.5f, 0.75f - 0.05f * Mathf.Cos(ang), 77.5f + 0.05f * Mathf.Sin(ang));
        ramp.transform.rotation = Quaternion.Euler(-ang * Mathf.Rad2Deg, 0, 0);
        var bc = ramp.AddComponent<BoxCollider>(); bc.size = new Vector3(7f, 0.1f, Mathf.Sqrt(1.5f * 1.5f + 3f * 3f));
        // the throne, faces down the hall
        var throne = PutC("D/SM_Env_Stone_Throne_01", 7.5f, 83.2f, 1.5f, 180f, st, new Vector3(1.7f, 1.7f, 1.7f));
        throne.name = "Throne";
    }

    // ---------------------------------------------------------------- lights
    static Light PointL(RoomDef r, Vector3 pos, Color c, float intensity, float range, bool shadows = false, string name = "Light")
    {
        var go = new GameObject(name);
        go.transform.SetParent(r.lights, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity * Gain(r); l.range = range * RangeGain(r);
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        if (shadows) { l.shadowStrength = 0.85f; l.shadowBias = 0.05f; l.shadowNormalBias = 0.4f; }
        var ad = go.AddComponent<UniversalAdditionalLightData>();
        if (shadows)
        {
            var so = new SerializedObject(ad);
            var tier = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tier != null) { tier.intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        return l;
    }

    static Light SpotL(RoomDef r, Vector3 pos, Vector3 euler, Color c, float intensity, float range, float angle, bool shadows = true)
    {
        var go = new GameObject("Spot");
        go.transform.SetParent(r.lights, false);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(euler);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.color = c; l.intensity = intensity * Gain(r); l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * 0.5f;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        go.AddComponent<UniversalAdditionalLightData>();
        return l;
    }

    // Light levels by room: bright halls, standard rooms, low rooms and the dark ones.
    static float Gain(RoomDef r)
    {
        switch (r.id)
        {
            case "D": return 3.2f;
            case "T": return 4.6f;
            case "P": case "TC": return 4.6f;
            case "C": return 2.2f;
            case "G": return 3.8f;
            case "V": return 2.0f;
            default: return 2.4f;
        }
    }
    static float RangeGain(RoomDef r) => r.id == "D" || r.id == "T" || r.id == "H" ? 1.25f : 1.1f;

    static readonly Color Fire = new Color(1f, 0.7f, 0.46f), Candle = new Color(1f, 0.8f, 0.6f), Cold = new Color(0.55f, 0.65f, 0.9f), Moss = new Color(0.55f, 0.9f, 0.6f);

    /// A lit wall torch on a bracket, with flame and light.
    static void Torch(RoomDef r, char side, float along, float y = 2.4f, float intensity = 3.5f, float range = 9f, bool shadows = true)
    {
        var t = Against("D/SM_Prop_Torch_Ornate_02", r, side, along, y, r.props, 0.0f, null, false);
        NoCol(t);
        var head = FacePoint(r, side, along) + Inward(side) * 0.42f + Vector3.up * (y + 0.5f);
        Fx("D/FX_Fire_01", head, r.props, 0.32f);
        PointL(r, head + Inward(side) * 0.3f, Fire, intensity, range, shadows, "Torch Light");
    }

    static void CandleAt(RoomDef r, Vector3 p, float intensity = 1.2f, float range = 4f, string key = "D/SM_Prop_Candles_02_Preset")
    {
        var c = Put(key, p, Rf(0, 360), r.props); NoCol(c);
        PointL(r, p + Vector3.up * 0.5f, Candle, intensity, range, false, "Candle Light");
    }

    // ---------------------------------------------------------------- dressing
    static void DressAll()
    {
        DressEntry(); DressArmory(); DressStore(); DressKitchen(); DressServery(); DressForge();
        DressGuard(); DressPrison(); DressTorture(); DressHall(); DressLibrary(); DressAlchemist();
        DressVault(); DressShrine(); DressCrypt(); DressThrone();
    }

    static GameObject Prop(RoomDef r, string key, float x, float z, float rot, Vector3? scale = null, bool col = true, float bottom = 0f)
    {
        var go = PutC(key, x, z, r.y + bottom, rot, r.props, scale);
        if (!col) NoCol(go);
        return go;
    }

    static GameObject Tilted(RoomDef r, string key, Vector3 pos, Vector3 euler, bool col = false, Vector3? scale = null)
    {
        var go = Put(key, pos, 0, r.props, scale, euler);
        if (!col) NoCol(go);
        return go;
    }

    static void Cobweb(RoomDef r, float x, float z, float y, float rot, float s = 0.6f)
    {
        var w = Put(Pick("R/SM_Env_SpiderWeb_01", "R/SM_Env_SpiderWeb_02", "R/SM_Env_SpiderWeb_03"), new Vector3(x, y, z), rot, r.hang, Vector3.one * s);
        NoCol(w);
    }

    static void Litter(RoomDef r, int n, float x0, float x1, float z0, float z1, params string[] keys)
    {
        for (int i = 0; i < n; i++)
        {
            var go = Put(keys[rng.Next(keys.Length)], new Vector3(Rf(x0, x1), r.y + 0.01f, Rf(z0, z1)), Rf(0, 360), r.props);
            NoCol(go);
        }
    }

    static void DressEntry()
    {
        var r = Rm("E");
        // benches along the south and east walls, a guard table and stools, weapon stands by the west wall
        Against("D/SM_Prop_Bench_01", r, 'S', 4.0f, 0f, r.props, 0.15f);
        Against("D/SM_Prop_Bench_01", r, 'S', 11.0f, 0f, r.props, 0.15f);
        Against("D/SM_Prop_Bench_01", r, 'E', 3.0f, 0f, r.props, 0.15f);
        Prop(r, "D/SM_Prop_Table_01", 10.6f, 4.6f, 90f);
        Prop(r, "D/SM_Prop_Stool_01", 9.6f, 3.8f, 20f);
        Prop(r, "D/SM_Prop_Stool_02", 11.6f, 5.6f, 75f);
        Tilted(r, "D/SM_Prop_Stool_01", new Vector3(11.8f, 0.3f, 3.5f), new Vector3(0, 30, 88));
        Prop(r, "R/SM_Prop_Mug_01", 10.5f, 4.2f, 0f, null, false, 0.96f);
        Prop(r, "R/SM_Prop_Tankard_01", 10.8f, 5.1f, 0f, null, false, 0.96f);
        Prop(r, "M/SM_Prop_Papers_06", 10.4f, 4.8f, 20f, null, false, 0.965f);
        Against("D/SM_Prop_WeaponRack_01", r, 'W', 6.0f, 0f, r.props, 0.05f);
        Against("M/SM_Prop_KnightStand_Broken_01", r, 'W', 8.6f, 0f, r.props, 0.25f);
        Against("D/SM_Prop_WeaponRack_01", r, 'N', 2.6f, 0f, r.props, 0.05f);
        Against("M/SM_Prop_KnightStand_01", r, 'N', 12.6f, 0f, r.props, 0.3f);
        // a few weapons left on the racks
        Tilted(r, "D/SM_Wep_Spear_01", new Vector3(0.75f, 0.1f, 5.6f), new Vector3(-12, 90, 0));
        Tilted(r, "D/SM_Wep_Shield_Round_01", new Vector3(0.65f, 0.55f, 6.6f), new Vector3(-10, 90, 0));
        Litter(r, 4, 1.5f, 13.5f, 1.5f, 8.5f, "D/SM_Prop_Vase_Shard_01", "D/SM_Prop_Vase_Shard_04", "R/SM_Env_Tiles_Loose_06", "D/SM_Env_Rubble_Pebbles_01");
        Cobweb(r, 0.6f, 9.4f, 3.6f, 45f);
        Cobweb(r, 14.4f, 0.6f, 3.6f, 225f);
        // banners either side of the armory entrance; torches flank it so the eye goes there first
        Against("D/SM_Prop_Wall_Banner_03", r, 'N', 2.4f, 4.6f, r.props, 0.03f, null, false);
        Against("D/SM_Prop_Wall_Banner_03", r, 'N', 12.6f, 4.6f, r.props, 0.03f, null, false);
        Torch(r, 'N', 4.1f, 2.3f, 4.5f, 9f, true);
        Torch(r, 'N', 10.9f, 2.3f, 4.5f, 9f, true);
        Torch(r, 'S', 7.5f, 2.4f, 2.2f, 8f, false);
        PointL(r, new Vector3(7.5f, 4.2f, 5f), Candle, 1.4f, 11f, false, "Fill");
    }

    static void DressArmory()
    {
        var r = Rm("A");
        // a screen wall just inside the entrance: weapons hung on both faces; it hides the hall beyond
        var screen = Put("D/SM_Env_Wall_01_DoubleSided", new Vector3(10.03f, 0f, 12.6f), 0, r.props);
        SetStatic(screen);
        foreach (float x in new[] { 6.0f, 7.5f, 9.0f })
        {
            Put(Pick("R/SM_Prop_Hanging_Weapon_01", "R/SM_Prop_Hanging_Weapon_02", "R/SM_Prop_Hanging_Weapon_03"), new Vector3(x, 2.1f, 12.3f), 180f, r.props);
            Put(Pick("D/SM_Wep_Shield_Heater_01", "D/SM_Wep_Shield_Heater_02", "D/SM_Wep_Shield_Ornate_01"), new Vector3(x, 3.1f, 12.28f), 180f, r.props);
        }
        Tilted(r, "D/SM_Wep_Shield_Ornate_02", new Vector3(7.5f, 3.0f, 12.92f), new Vector3(0, 0, 0));
        // dense rows of weapon racks and armour stands either side of a central aisle
        foreach (float z in new[] { 14.6f, 17.0f })
        {
            foreach (float x in new[] { 1.9f, 3.9f, 11.1f, 13.1f })
                Prop(r, "R/SM_Prop_Dwarf_Weapon_Rack_04", x, z, 90f);
            Prop(r, "M/SM_Prop_KnightStand_01", 5.3f, z + 0.05f, 180f);
            Prop(r, "M/SM_Prop_KnightStand_01", 9.7f, z + 0.05f, 180f);
        }
        // armour stands and spear barrels along the walls
        foreach (float z in new[] { 13.6f, 15.8f, 18.2f })
        {
            Against("M/SM_Prop_KnightStand_01", r, 'W', z, 0f, r.props, 0.15f);
            Against("M/SM_Prop_KnightStand_01", r, 'E', z, 0f, r.props, 0.15f);
        }
        foreach (float x in new[] { 1.5f, 3.6f, 11.4f, 13.5f })
            Against("D/SM_Prop_WeaponRack_01", r, 'N', x, 0f, r.props, 0.05f);
        Against("R/SM_Prop_Dwarf_Weapon_Barrel_02", r, 'N', 5.4f, 0f, r.props, 0.1f);
        Against("R/SM_Prop_Dwarf_Weapon_Barrel_02", r, 'N', 9.6f, 0f, r.props, 0.1f);
        Prop(r, "R/SM_Prop_Dwarf_Weapon_Barrel_01", 0.95f, 10.95f, 0f);
        Prop(r, "R/SM_Prop_Dwarf_Weapon_Barrel_02", 14.05f, 10.95f, 40f);
        // weapons racked on the north wall
        foreach (float x in new[] { 1.1f, 1.6f, 2.1f, 11.0f, 11.5f, 12.0f })
            Tilted(r, Pick("D/SM_Wep_Spear_01", "D/SM_Wep_Halberd_06", "D/SM_Wep_Ornate_Spear_01"), new Vector3(x, 0.15f, 19.25f), new Vector3(-10, 0, 0));
        foreach (float x in new[] { 3.2f, 3.7f, 4.2f, 13.1f, 13.6f, 14.1f })
            Tilted(r, Pick("D/SM_Wep_GreatSword_01", "D/SM_Wep_Straightsword_01", "D/SM_Wep_Ornate_Sword_01"), new Vector3(x, 0.2f, 19.25f), new Vector3(-12, 0, 0));
        // the best gear: royal armour on a stand and an ornate blade, behind the screen
        Prop(r, "M/SM_Prop_KnightStand_Royal_01", 7.5f, 13.55f, 0f);
        Prop(r, "R/SM_Prop_Dwarf_Table_Small_02", 6.2f, 13.5f, 0f);
        Tilted(r, "D/SM_Wep_Crystal_Ornate_Straightsword_01", new Vector3(6.2f, 0.93f, 13.5f), new Vector3(90, 90, 0));
        Prop(r, "D/SM_Wep_Shield_Ornate_01", 8.9f, 13.4f, 0f, null, false);
        // light: torches and a hanging lamp, the warlord's men keep it lit
        Torch(r, 'W', 11.0f, 2.4f, 3.0f, 8f, true);
        Torch(r, 'E', 11.0f, 2.4f, 3.0f, 8f, true);
        Torch(r, 'N', 7.5f, 2.6f, 3.2f, 9f, true);
        Torch(r, 'W', 17.0f, 2.4f, 2.2f, 7f, false);
        Torch(r, 'E', 17.0f, 2.4f, 2.2f, 7f, false);
        // a lamp hung just inside the entrance lights the weapons on the screen, seen from the entry hall
        var lamp = Put("D/SM_Prop_Lantern_02", new Vector3(7.5f, 3.4f, 11.6f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(7.5f, 3.2f, 11.3f), Fire, 2.2f, 6f, false, "Entrance Lamp");
        var ch = Put("D/SM_Prop_Candle_Chandelier_01_Preset", new Vector3(7.5f, 5.0f, 16.5f), 0, r.hang); NoCol(ch);
        PointL(r, new Vector3(7.5f, 3.0f, 16.5f), Candle, 3f, 10f, true, "Chandelier");
    }

    static void DressStore()
    {
        var r = Rm("S");
        // crates and barrels, plundered: lids off, broken staves, sacks slit open
        Against("R/SM_Prop_Barrel_Shelf_Large_01", r, 'S', -7.0f, 0f, r.props, 0.1f);
        Against("R/SM_Prop_Barrel_Shelf_Large_Broken_02", r, 'S', -4.2f, 0f, r.props, 0.1f);
        Against("R/SM_Prop_Crate_Stack_01", r, 'N', -2.4f, 0f, r.props, 0.15f);
        Against("R/SM_Prop_Barrel_Stack_01", r, 'E', 7.6f, 0f, r.props, 0.1f);
        Prop(r, "D/SM_Prop_Crate_Wood_01", -8.6f, 3.6f, 10f);
        Prop(r, "D/SM_Prop_Crate_Wood_03", -8.4f, 4.75f, 35f, null, true, 0f);
        Prop(r, "R/SM_Prop_Crate_Broken_01", -5.6f, 2.0f, 60f);
        Prop(r, "D/SM_Prop_Crate_Metal_Open_01", -3.4f, 7.7f, 15f);
        Prop(r, "D/SM_Prop_Barrel_01", -1.3f, 5.2f, 0f);
        Prop(r, "D/SM_Prop_Barrel_Broken_01", -1.2f, 6.4f, 0f);
        Tilted(r, "D/SM_Prop_Barrel_03", new Vector3(-8.2f, 0.38f, 4.6f), new Vector3(0, 20, 90), true);
        Prop(r, "D/SM_Prop_Barrel_Broken_02", -6.8f, 7.6f, 0f, null, false);
        Prop(r, "G/SM_Gen_Prop_Sack_Stack_01", -8.7f, 8.6f, 0f);
        Prop(r, "G/SM_Gen_Prop_Sack_03", -5.5f, 8.8f, 40f);
        Prop(r, "G/SM_Gen_Prop_Sack_05", -4.6f, 2.6f, 140f, null, false);
        Prop(r, "R/SM_Prop_Crate_01", -1.3f, 8.5f, 5f);
        Prop(r, "R/SM_Prop_Crate_01", -1.3f, 8.5f, 25f, null, true, 1.1f);
        Litter(r, 6, -9f, -1f, 1f, 9f, "D/SM_Env_Rubble_Plank_02", "R/SM_Prop_Barrel_Plug_01", "G/SM_Gen_Prop_Food_Vegetable_02", "D/SM_Prop_Plank_01");
        Cobweb(r, -9.4f, 0.6f, 3.8f, 135f);
        PointL(r, new Vector3(-5f, 3.2f, 5f), Candle, 2.2f, 11f, false, "Fill");
        var lantern = Put("D/SM_Prop_Lantern_02", new Vector3(-1.3f, 1.03f, 5.2f), 0, r.props); NoCol(lantern);
        PointL(r, new Vector3(-1.3f, 1.6f, 5.2f), Fire, 1.8f, 7f, true, "Lantern");
    }

    static void DressKitchen()
    {
        var r = Rm("K");
        // a cold hearth on the west wall with a spit across it
        var hearth = Against("D/SM_Env_Wall_Fireplace_Entrance_01", r, 'W', 15f, 0f, r.props, 0.0f);
        Prop(r, "D/SM_Prop_Goblin_Rotisserie_01", -9.0f, 15f, 90f);
        Prop(r, "D/SM_Prop_Cauldron_01", -8.9f, 12.0f, 30f);
        // worktables, one tipped over, food left to spoil
        Prop(r, "D/SM_Prop_Table_01", -4.3f, 14.2f, 0f);
        Prop(r, "R/SM_Prop_Dwarf_Table_02", -4.6f, 17.6f, 90f);
        Tilted(r, "D/SM_Prop_Table_01", new Vector3(-1.6f, 0.7f, 12.6f), new Vector3(0, 80, 88), true);
        Prop(r, "G/SM_Gen_Prop_Food_Meat_02", -4.6f, 14.1f, 20f, null, false, 0.96f);
        Prop(r, "G/SM_Gen_Prop_Food_Bread_01", -3.6f, 14.3f, 70f, null, false, 0.96f);
        Prop(r, "G/SM_Gen_Prop_Pot_02", -5.1f, 14.4f, 0f, null, false, 0.96f);
        Prop(r, "G/SM_Gen_Prop_Plate_01", -4.2f, 17.4f, 0f, null, false, 0.91f);
        Prop(r, "G/SM_Gen_Prop_Food_Vegetable_04", -4.9f, 17.9f, 0f, null, false, 0.91f);
        Prop(r, "R/SM_Prop_Camp_Pot_03", -4.0f, 17.9f, 0f, null, false, 0.91f);
        // barrels and sacks
        Against("D/SM_Prop_Barrel_01", r, 'E', 18.6f, 0f, r.props, 0.1f);
        Against("D/SM_Prop_Barrel_02", r, 'E', 17.5f, 0f, r.props, 0.1f);
        Against("G/SM_Gen_Prop_Sack_Stack_02", r, 'N', -1.5f, 0f, r.props, 0.1f);
        Prop(r, "G/SM_Gen_Prop_Sack_01", -9.1f, 18.4f, 20f);
        Prop(r, "G/SM_Gen_Prop_Sack_04", -9.2f, 17.2f, 80f, null, false);
        Prop(r, "D/SM_Prop_Barrel_Broken_01", -8.8f, 11.2f, 0f);
        Litter(r, 7, -9f, -1f, 11f, 19f, "G/SM_Gen_Prop_Food_Vegetable_01", "G/SM_Gen_Prop_Food_Meat_05", "G/SM_Gen_Prop_Plate_01", "R/SM_Prop_Mug_01", "D/SM_Prop_Vase_Shard_02");
        // shelves of crocks on the south wall
        Against("G/SM_Gen_Prop_Shelf_02", r, 'S', -3.0f, 0f, r.props, 0.05f);
        Cobweb(r, -9.3f, 19.3f, 2.4f, 135f, 0.45f);
        Cobweb(r, -0.7f, 10.7f, 2.4f, 315f, 0.45f);
        var beamY = 2.62f;
        foreach (float z in new[] { 12.5f, 17.5f })
            SetStatic(Put("D/SM_Env_Basement_Support_Beam_01", new Vector3(-5f, beamY, z), 90f, r.props, new Vector3(1f, 1f, 2f)));
        var lamp = Put("D/SM_Prop_Lantern_01", new Vector3(-4.0f, 0.96f, 13.8f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(-4.0f, 1.6f, 13.8f), Fire, 1.6f, 7f, true, "Lantern");
        PointL(r, new Vector3(-5f, 2.4f, 18f), Candle, 0.7f, 7f, false, "Fill");
    }

    static void DressServery()
    {
        var r = Rm("X");
        Prop(r, "R/SM_Prop_Dwarf_Table_03", -8.7f, 24.0f, 90f);
        Prop(r, "G/SM_Gen_Prop_Plate_01", -8.6f, 23.5f, 0, null, false, 0.91f);
        Prop(r, "R/SM_Prop_Goblet_02", -8.7f, 24.4f, 0, null, false, 0.91f);
        Against("G/SM_Gen_Prop_Shelf_01", r, 'W', 29.5f, 0f, r.props, 0.05f);
        Against("D/SM_Prop_Barrel_01", r, 'N', -8.8f, 0f, r.props, 0.1f);
        Against("D/SM_Prop_Barrel_02", r, 'N', -7.6f, 0f, r.props, 0.1f);
        Prop(r, "D/SM_Prop_Crate_Wood_02", -6.4f, 31.6f, 15f);
        Litter(r, 4, -9f, -4.5f, 21f, 33f, "G/SM_Gen_Prop_Plate_01", "R/SM_Prop_Mug_01", "D/SM_Prop_Vase_Shard_01");
        Against("D/SM_Prop_Wall_Banner_03", r, 'W', 32f, 7.5f, r.props, 0.03f, null, false);
        Torch(r, 'W', 26.5f, 2.4f, 2.6f, 8f, true);
        Torch(r, 'N', -5.0f, 7.2f, 2.4f, 9f, false);
        Torch(r, 'S', -2.5f, 7.0f, 2.4f, 8f, true);
        PointL(r, new Vector3(-6.5f, 4f, 30f), Candle, 1.2f, 10f, false, "Fill");
        PointL(r, new Vector3(-1.9f, 3.5f, 30.5f), Candle, 1.5f, 6f, false, "Stair Fill");
    }

    static void DressForge()
    {
        var r = Rm("F");
        // hearth under a hood whose flue climbs to the ceiling, all gone cold
        float hx = -15f, hz = 8.2f;
        Prop(r, "D/SM_Prop_Forge_FirePit_01", hx, hz, 0f);
        Prop(r, "D/SM_Prop_Forge_Ventilation_01", hx, hz, 0f, null, true, 2.25f);
        Prop(r, "D/SM_Prop_Forge_Ventilation_Pipe_01", hx, hz, 0f, new Vector3(1f, (5f - 3.95f) / 1.76f, 1f), true, 3.95f);
        // anvils, quench barrels and the tools left where they fell
        Prop(r, "R/SM_Prop_Anvil_01", -15.2f, 4.6f, 0f);
        Prop(r, "R/SM_Prop_Anvil_01", -11.9f, 6.2f, 75f);
        Prop(r, "D/SM_Prop_Barrel_02", -17.9f, 4.4f, 0f);
        Prop(r, "D/SM_Prop_Barrel_01", -12.0f, 8.4f, 0f);
        Tilted(r, "R/SM_Prop_Forge_Hammer_01", new Vector3(-14.2f, 0.06f, 4.0f), new Vector3(0, 30, 90));
        Tilted(r, "R/SM_Prop_Forge_Tongs_01", new Vector3(-16.1f, 0.05f, 3.6f), new Vector3(90, 70, 0));
        Tilted(r, "R/SM_Prop_Forge_Hammer_02", new Vector3(-12.5f, 0.06f, 5.0f), new Vector3(0, 110, 90));
        Tilted(r, "R/SM_Prop_Forge_Pickaxe_02", new Vector3(-18.6f, 0.05f, 2.0f), new Vector3(0, 50, 90));
        Prop(r, "R/SM_Prop_Toolstrap_01", -15.2f, 4.6f, 20f, null, false, 1.02f);
        Against("R/SM_Prop_Dwarf_Weapon_Rack_04", r, 'W', 2.6f, 0f, r.props, 0.1f, null, true, 0f);
        Against("R/SM_Prop_Dwarf_Smelting_Pot_01", r, 'S', -12.4f, 0f, r.props, 0.1f, new Vector3(0.6f, 0.6f, 0.6f));
        Prop(r, "R/SM_Env_Ore_Pile_01", -18.6f, 8.6f, 0f, new Vector3(0.35f, 0.5f, 0.35f), false);
        Litter(r, 5, -19f, -11f, 1f, 7f, "D/SM_Prop_Bolt_01", "R/SM_Prop_Rock_Small_02", "D/SM_Prop_Plank_02", "R/SM_Prop_Caliper_01");
        Cobweb(r, -19.4f, 0.6f, 4f, 45f);
        PointL(r, new Vector3(-15f, 3.4f, 3.5f), new Color(0.8f, 0.72f, 0.62f), 1.8f, 11f, false, "Fill");
        PointL(r, new Vector3(-12.5f, 3.6f, 8.2f), new Color(0.8f, 0.72f, 0.62f), 0.9f, 8f, false, "Fill");
        var lamp = Put("D/SM_Prop_Lantern_02", new Vector3(-11.9f, 1.02f, 6.2f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(-11.9f, 1.6f, 6.2f), Fire, 1.8f, 7f, true, "Lantern");
    }

    static void DressGuard()
    {
        var r = Rm("G");
        // a guard post wrecked in a brawl
        Tilted(r, "D/SM_Prop_Table_Round_Broken_01", new Vector3(20.0f, 0f, 5.0f), new Vector3(0, 30, 0), true);
        Tilted(r, "R/SM_Prop_Chair_Broken_01", new Vector3(18.5f, 0f, 6.2f), new Vector3(0, 140, 0), true);
        Tilted(r, "D/SM_Prop_Stool_02", new Vector3(21.6f, 0.33f, 6.0f), new Vector3(0, 10, 90), true);
        Prop(r, "D/SM_Prop_Stool_01", 21.8f, 3.6f, 40f);
        Against("D/SM_Prop_Bed_01", r, 'E', 3.0f, 0f, r.props, 0.05f, null, true, 0f);
        Against("R/SM_Prop_Dwarf_Bed_01", r, 'E', 7.6f, 0f, r.props, 0.05f, null, true, 0f);
        Against("D/SM_Prop_WeaponRack_01", r, 'S', 18.5f, 0f, r.props, 0.05f);
        Tilted(r, "D/SM_Wep_Axe_01", new Vector3(19.4f, 0.05f, 3.2f), new Vector3(0, 60, 90));
        Tilted(r, "D/SM_Wep_BrokenSword_01", new Vector3(17.2f, 0.05f, 4.8f), new Vector3(0, 10, 90));
        Prop(r, "D/SM_Prop_Skeleton_Slave_Lying_01", 21.0f, 7.9f, 120f, null, false);
        Prop(r, "D/SM_Prop_Chest_Wood_01", 16.2f, 8.9f, 90f);
        Litter(r, 8, 16f, 24f, 1f, 9f, "R/SM_Prop_Mug_01", "G/SM_Gen_Prop_Bottle_02", "G/SM_Gen_Prop_Bottle_04", "D/SM_Prop_Vase_Shard_01", "R/SM_Prop_Tankard_01", "G/SM_Gen_Prop_Papers_02");
        Cobweb(r, 24.4f, 9.4f, 2.4f, 315f, 0.45f);
        var lamp = Put("D/SM_Prop_Lantern_01", new Vector3(16.3f, 0.7f, 8.9f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(16.4f, 1.4f, 8.6f), Fire, 1.1f, 6f, true, "Lantern");
        PointL(r, new Vector3(20f, 2.3f, 4f), Cold, 0.55f, 8f, false, "Dim Fill");
    }

    static void DressPrison()
    {
        var r = Rm("P");
        // a row of cells along the north wall and a shorter row along the south; bars between them,
        // barred doors forced in the riot
        float zN = 16.0f, zS = 13.0f;
        float[] northX = { 15.43f, 18.26f, 21.09f, 23.92f, 26.75f, 29.57f };
        for (int i = 0; i < northX.Length - 1; i++)
        {
            float a = northX[i], b = northX[i + 1], mid = (a + b) * 0.5f;
            // front: bars either side of a barred door
            Put("D/SM_Prop_Metal_Fence_01", new Vector3(a + 0.45f, 0, zN), 0, r.props, new Vector3(0.36f, 1.15f, 1f));
            Put("D/SM_Prop_Metal_Fence_01", new Vector3(b - 0.45f, 0, zN), 0, r.props, new Vector3(0.36f, 1.15f, 1f));
            // forced door, swung or hanging at odd angles
            var dr = Put("D/SM_Env_Door_Bars_01", new Vector3(mid - 0.95f, 0, zN), 0, r.props, new Vector3(1.0f, 1.18f, 1f), new Vector3(0, -55f - rng.Next(60), i == 2 ? 6f : 0f));
            NoCol(dr);
            if (i > 0) Put("D/SM_Prop_Metal_Fence_01", new Vector3(a, 0, (zN + r.iz1) * 0.5f), 90f, r.props, new Vector3(1.45f, 1.15f, 1f));
            // overturned bedding inside
            Tilted(r, "R/SM_Prop_Dwarf_Bed_01", new Vector3(mid + 0.4f, 0.0f, 18.2f), new Vector3(0, 90 + Rf(-20, 20), i % 2 == 0 ? 0 : 85), i % 2 == 1);
            if (i % 2 == 0) Prop(r, "D/SM_Prop_Skeleton_Slave_Wall_Sitting_01", mid - 0.5f, 19.0f, 180f, null, false);
            Prop(r, Pick("R/SM_Prop_Camp_Roll_01", "R/SM_Prop_Camp_Roll_02", "G/SM_Gen_Prop_Skull_01"), mid, 17.0f, Rf(0, 360), null, false);
        }
        float[] southX = { 19.6f, 22.2f, 24.8f, 27.4f };
        for (int i = 0; i < southX.Length - 1; i++)
        {
            float a = southX[i], b = southX[i + 1], mid = (a + b) * 0.5f;
            Put("D/SM_Prop_Metal_Fence_01", new Vector3(a + 0.35f, 0, zS), 0, r.props, new Vector3(0.28f, 1.15f, 1f));
            Put("D/SM_Prop_Metal_Fence_01", new Vector3(b - 0.35f, 0, zS), 0, r.props, new Vector3(0.28f, 1.15f, 1f));
            var dr = Put("D/SM_Env_Door_Bars_01", new Vector3(mid + 0.95f, 0, zS), 180f, r.props, new Vector3(1.0f, 1.18f, 1f), new Vector3(0, 180f + 60f + rng.Next(40), 0));
            NoCol(dr);
            Put("D/SM_Prop_Metal_Fence_01", new Vector3(a, 0, (zS + r.iz0) * 0.5f), 90f, r.props, new Vector3(1.03f, 1.15f, 1f));
            if (i == southX.Length - 2) Put("D/SM_Prop_Metal_Fence_01", new Vector3(b, 0, (zS + r.iz0) * 0.5f), 90f, r.props, new Vector3(1.03f, 1.15f, 1f));
            Tilted(r, "R/SM_Prop_Camp_Roll_03", new Vector3(mid, 0.0f, 11.3f), new Vector3(0, Rf(0, 180), 0));
        }
        // the corridor: an overturned bench and the warders' table
        Tilted(r, "D/SM_Prop_Bench_01", new Vector3(23.5f, 0.4f, 14.6f), new Vector3(0, 10, 85), true);
        Prop(r, "D/SM_Prop_Skeleton_01", 21.0f, 14.0f, 40f, null, false);
        Litter(r, 6, 16f, 29f, 13.4f, 15.6f, "R/SM_Prop_Camp_Plank_01", "D/SM_Prop_Vase_Shard_03", "G/SM_Gen_Prop_Skull_01", "R/SM_Prop_Mug_01");
        Put("D/SM_Prop_Chain_01", new Vector3(16.4f, 0.4f, 19.3f), 0, r.props);
        Cobweb(r, 15.7f, 10.7f, 2.4f, 45f, 0.45f);
        Cobweb(r, 29.3f, 19.3f, 2.4f, 225f, 0.45f);
        // dark: one guttering lantern and a faint cold light
        var lamp = Put("D/SM_Prop_Lantern_02", new Vector3(17.0f, 2.1f, 10.6f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(17.0f, 2.2f, 11.0f), Fire, 0.9f, 7f, true, "Lantern");
        PointL(r, new Vector3(23f, 2.5f, 14.5f), Cold, 0.55f, 9f, false, "Dim Fill");
        PointL(r, new Vector3(18f, 2.5f, 17.8f), Cold, 0.25f, 6f, false, "Dim Fill");
        PointL(r, new Vector3(28.5f, 2.2f, 12.2f), Fire, 0.45f, 4.5f, false, "Ember");
    }

    static void DressTorture()
    {
        var r = Rm("TC");
        Prop(r, "D/SM_Prop_Toture_StretchTable_01", 35f, 15.5f, 0f);
        Against("D/SM_Prop_Toture_IronMaiden_01", r, 'N', 38.0f, 0f, r.props, 0.1f);
        Against("D/SM_Prop_Toture_Stocks_01", r, 'S', 37.3f, 0f, r.props, 0.4f);
        Prop(r, "D/SM_Prop_Toture_Cage_01", 32.0f, 18.4f, 20f);
        Put("D/SM_Prop_Toture_Cage_01", new Vector3(38.3f, 0.95f, 11.6f), 0, r.props, new Vector3(0.9f, 0.9f, 0.9f));
        Put("D/SM_Prop_Chain_01", new Vector3(38.3f, 0.95f + 1.8f, 11.6f), 0, r.props, new Vector3(1, 0.1f, 1));
        // chains hanging from the ceiling and shackles on the walls
        foreach (var p in new[] { new Vector3(33.2f, 0.4f, 13.0f), new Vector3(36.8f, 0.4f, 13.2f), new Vector3(34.4f, 0.4f, 18.5f) })
        { var c = Put("D/SM_Prop_Chain_01", p, Rf(0, 90), r.props); NoCol(c); }   // chains stay with the room's furnishings (they are part of the room's kind)
        Against("D/SM_Prop_Skeleton_Slave_Shackles_02", r, 'W', 16.5f, 0.2f, r.props, 0.05f);
        Against("D/SM_Prop_Skeleton_Slave_Shackles_01", r, 'E', 15.2f, 0f, r.props, 0.05f);
        // a brazier long cold, and its pokers
        Prop(r, "D/SM_Prop_Brazier_01", 32.6f, 13.4f, 0f);
        Tilted(r, "R/SM_Prop_Forge_Tongs_01", new Vector3(33.6f, 0.05f, 12.6f), new Vector3(90, 30, 0));
        Prop(r, "R/SM_Prop_Dwarf_Table_Small_01", 31.2f, 15.4f, 0f);
        Prop(r, "R/SM_Prop_Caliper_01", 31.2f, 15.4f, 0f, null, false, 0.91f);
        Prop(r, "D/SM_Prop_Skeleton_01", 35.5f, 12.2f, 0f, null, false);
        Litter(r, 4, 31f, 39f, 11f, 19f, "G/SM_Gen_Prop_Skull_01", "G/SM_Gen_Prop_Skull_01", "G/SM_Gen_Prop_Skull_01");
        Cobweb(r, 39.4f, 19.4f, 2.4f, 225f, 0.45f);
        var lamp = Put("D/SM_Prop_Lantern_01", new Vector3(31.2f, 0.91f, 15.0f), 0, r.props); NoCol(lamp);
        PointL(r, new Vector3(31.3f, 1.5f, 15.0f), Fire, 0.8f, 6.5f, true, "Lantern");
        PointL(r, new Vector3(36f, 2.5f, 16f), new Color(0.75f, 0.45f, 0.4f), 0.5f, 8f, false, "Dim Fill");
    }

    static void DressHall()
    {
        var r = Rm("D");
        // long tables in four rows running the length of the hall from the gallery towards the high table,
        // benches either side; one row overturned in the looting, the rest still laid under dust
        float[] rowX = { 4.6f, 10.6f, 19.4f, 25.4f };
        for (int row = 0; row < 4; row++)
        {
            float x = rowX[row];
            int count = row == 1 || row == 2 ? 2 : 3;
            for (int i = 0; i < count; i++)
            {
                float z = 24.9f + 1.47f + i * 2.95f;
                bool over = row == 2 && i < 2;
                if (over)
                {
                    Tilted(r, "D/SM_Prop_Table_01", new Vector3(x - 0.2f, 0.7f, z), new Vector3(0, 90, 88), true);
                    Tilted(r, "D/SM_Prop_Bench_01", new Vector3(x + 1.4f, 0.4f, z + 0.3f), new Vector3(0, 98, 86), true);
                    continue;
                }
                Prop(r, "D/SM_Prop_Table_01", x, z, 90f);
                if (rng.NextDouble() < 0.85) Prop(r, "D/SM_Prop_Bench_01", x - 1.15f, z + Rf(-0.2f, 0.2f), 90f + Rf(-6, 6));
                else Tilted(r, "D/SM_Prop_Bench_01", new Vector3(x - 1.45f, 0.4f, z), new Vector3(0, 90 + Rf(-20, 20), 88), true);
                Prop(r, "D/SM_Prop_Bench_01", x + 1.15f, z + Rf(-0.2f, 0.2f), 270f + Rf(-6, 6));
                for (int k = 0; k < 3; k++)
                    Prop(r, Pick("G/SM_Gen_Prop_Plate_01", "R/SM_Prop_Goblet_01", "R/SM_Prop_Mug_01", "R/SM_Prop_Tankard_01", "G/SM_Gen_Prop_Food_Bread_02"), x + Rf(-0.4f, 0.4f), z + Rf(-1.2f, 1.2f), Rf(0, 360), null, false, 0.96f);
                if (i == 1) Prop(r, "D/SM_Prop_Candles_03_Preset", x, z, 0, null, false, 0.96f);
            }
        }
        // the high table across the north end
        for (int i = 0; i < 4; i++) Prop(r, "R/SM_Prop_Dwarf_Table_03", 9.0f + i * 3.32f + 1.66f, 38.2f, 0f);
        foreach (float x in new[] { 14f, 17f }) Prop(r, "R/SM_Prop_Chair_01", x, 37.1f, 180f);
        Tilted(r, "R/SM_Prop_Chair_Broken_01", new Vector3(22.5f, 0f, 36.6f), new Vector3(0, 200, 0), true);
        // a great hearth on the east wall
        Against("D/SM_Env_Wall_Fireplace_Entrance_01", r, 'E', 31.5f, 0f, r.props, 0.0f, new Vector3(1.3f, 1.3f, 1.3f));
        // banners in tatters
        foreach (float x in new[] { 3f, 9f, 21f, 27f })
            Against(Pick("R/SM_Prop_Banner_Damaged_01", "R/SM_Prop_Banner_Damaged_02"), r, 'N', x, 9.6f, r.props, 0.05f, null, false);
        foreach (float z in new[] { 28f, 36f })
        {
            Against(Pick("R/SM_Prop_Banner_Damaged_02", "R/SM_Prop_Banner_Damaged_03"), r, 'W', z, 9.4f, r.props, 0.05f, null, false);
            Against(Pick("R/SM_Prop_Banner_Damaged_01", "R/SM_Prop_Banner_Damaged_03"), r, 'E', z, 9.4f, r.props, 0.05f, null, false);
        }
        // chandeliers hanging from the ceiling on chains
        foreach (float x in new[] { 7.5f, 15f, 22.5f })
        {
            var ch = PutC("R/SM_Prop_Dwarf_Chandelier_01", x, 32.8f, 6.2f, 0, r.hang); NoCol(ch);
            for (int c = 0; c < 4; c++)
            {
                var off = Rot(45 + 90 * c, new Vector3(0, 0, 2.2f));
                var chain = Put("R/SM_Prop_Dwarf_Chain_01", new Vector3(x + off.x, 6.2f + 3.2f, 32.8f + off.z), 0, r.hang, new Vector3(1, 0.2f, 1)); NoCol(chain);
            }
            var top = Put("R/SM_Prop_Dwarf_Chain_01", new Vector3(x, 6.2f + 3.6f, 32.8f), 0, r.hang, new Vector3(1, 0.12f, 1)); NoCol(top);
            Fx("R/FX_Fire_Chandelier_01", new Vector3(x, 6.9f, 32.8f), r.hang, 0.8f);
            PointL(r, new Vector3(x, 6.0f, 32.8f), Candle, 14f, 22f, x == 15f, "Chandelier");
        }
        // torches along the walls, and the hearth glow (cold) replaced by a lamp on the high table
        Torch(r, 'W', 31f, 2.6f, 3.0f, 9f, false);
        Torch(r, 'E', 25.0f, 2.6f, 3.0f, 9f, false);
        Torch(r, 'N', 5.5f, 2.6f, 3.2f, 9f, true);
        Torch(r, 'N', 24.5f, 2.6f, 3.2f, 9f, true);
        Torch(r, 'S', 15f, 2.5f, 2.0f, 7f, false);       // beneath the gallery
        Torch(r, 'S', 2.5f, 7.6f, 2.4f, 8f, false);      // up on the gallery
        Torch(r, 'S', 27.5f, 7.6f, 2.4f, 8f, false);
        PointL(r, new Vector3(15f, 8.5f, 22.5f), Candle, 1.6f, 14f, false, "Gallery Fill");
        CandleAt(r, new Vector3(15f, 0.91f, 38.2f), 2.0f, 6f, "D/SM_Prop_Candles_04_Preset");
        Litter(r, 10, 1f, 29f, 26f, 39f, "G/SM_Gen_Prop_Plate_01", "R/SM_Prop_Goblet_01", "R/SM_Prop_Mug_01", "G/SM_Gen_Prop_Food_Bread_01", "D/SM_Prop_Vase_Shard_01");
        Litter(r, 3, 1f, 29f, 20.8f, 24f, "D/SM_Prop_Barrel_Broken_02", "G/SM_Gen_Prop_Plate_01");
        Cobweb(r, 28.7f, 38.7f, 8.5f, 225f, 0.7f);
        Cobweb(r, 1.3f, 38.7f, 8.5f, 135f, 0.7f);
        // gallery: a few crates and a dropped bow for the archers' post
        Prop(r, "D/SM_Prop_Crate_Wood_02", 6.5f, 21.0f, 10f, null, true, 5f);
        Prop(r, "D/SM_Prop_Barrel_01", 18.0f, 21.1f, 0f, null, true, 5f);
        Prop(r, "D/SM_Prop_Crate_Wood_04", 24.2f, 21.2f, 30f, null, true, 5f);
    }

    static void DressLibrary()
    {
        var r = Rm("L");
        string[] cases = { "M/SM_Prop_Bookcase_01", "M/SM_Prop_Bookcase_01", "M/SM_Prop_Bookcase_01", "M/SM_Prop_Bookcase_02", "M/SM_Prop_Bookcase_03", "M/SM_Prop_Bookcase_Grand_01" };
        // tall bookcases packed close: along the walls and in four freestanding rows
        Against("M/SM_Prop_Bookcase_Broken_01", r, 'N', 17.0f, 0f, r.props, 0.01f);
        Against(Pick(cases), r, 'N', 26.4f, 0f, r.props, 0.01f);
        Against("M/SM_Prop_Bookcase_Grand_03", r, 'N', 23.7f, 0f, r.props, 0.01f);
        Against("M/SM_Prop_Bookcase_01", r, 'E', 45.6f, 0f, r.props, 0.01f);   // hidden bookcase is at z 52.5 (BuildSecretBookcase)
        Against(Pick(cases), r, 'W', 51.9f, 0f, r.props, 0.01f);
        Against("M/SM_Prop_Bookcase_Empty_01", r, 'W', 43.0f, 0f, r.props, 0.01f);
        Against(Pick(cases), r, 'S', 17.4f, 0f, r.props, 0.01f);
        Against(Pick(cases), r, 'S', 27.6f, 0f, r.props, 0.01f);
        // freestanding rows (back to back pairs), running east-west
        foreach (float z in new[] { 47.6f, 51.3f })
            foreach (float x in new[] { 19.2f, 21.7f, 24.6f })
            {
                if (z > 50f && x > 24f) continue;
                PutC(Pick(cases), x, z - 0.53f, 0f, 180f, r.props);
                PutC(Pick(cases), x, z + 0.53f, 0f, 0f, r.props);
            }
        // a second reading table by the door from the hall; a smashed case on the north wall
        Prop(r, "D/SM_Prop_Table_01", 25.8f, 43.6f, 90f);
        Prop(r, "R/SM_Prop_Chair_01", 24.7f, 43.2f, 90f);
        Prop(r, "M/SM_Prop_Book_Open_02", 25.8f, 43.9f, 70f, null, false, 0.96f);
        Tilted(r, "M/SM_Prop_Book_Pile_01", new Vector3(26.1f, 0.96f, 42.8f), new Vector3(0, 20, 0));
        // reading tables
        Prop(r, "D/SM_Prop_Table_01", 21.0f, 44.6f, 90f);
        Prop(r, "R/SM_Prop_Chair_01", 19.9f, 44.0f, 80f);
        Tilted(r, "R/SM_Prop_Chair_Broken_01", new Vector3(22.3f, 0f, 45.4f), new Vector3(0, 250, 0), true);
        Prop(r, "M/SM_Prop_Book_Open_01", 21.0f, 44.4f, 15f, null, false, 0.96f);
        Prop(r, "M/SM_Prop_Book_Pile_02", 21.2f, 45.4f, 70f, null, false, 0.96f);
        Prop(r, "D/SM_Item_Inkwell_01", 20.8f, 43.8f, 0f, null, false, 0.96f);
        Prop(r, "M/SM_Prop_Book_Stand_01", 18.0f, 42.0f, 30f);
        Prop(r, "M/SM_Prop_Globe_01", 28.6f, 41.4f, 0f);
        Prop(r, "M/SM_Prop_Book_Pile_03", 26.2f, 52.9f, 80f, null, false);
        // books pulled down and scattered
        Litter(r, 22, 16f, 29f, 41f, 54f, "M/SM_Prop_Book_01", "M/SM_Prop_Book_02", "M/SM_Prop_Book_03", "M/SM_Prop_Book_04", "M/SM_Prop_Book_Open_01", "M/SM_Prop_Papers_01", "M/SM_Prop_Papers_03", "M/SM_Prop_Papers_07");
        foreach (var p in new[] { new Vector2(20.4f, 47.2f), new Vector2(23.4f, 50.0f), new Vector2(17.4f, 46.9f), new Vector2(25.6f, 43.0f) })
            Prop(r, Pick("M/SM_Prop_Book_Pile_01", "M/SM_Prop_Book_Pile_03", "M/SM_Prop_Book_Block_02"), p.x, p.y, Rf(0, 360), null, false);
        // ladders against the tall cases
        Cobweb(r, 29.3f, 54.3f, 6.5f, 225f, 0.8f);
        Cobweb(r, 15.7f, 54.3f, 6.5f, 135f, 0.8f);
        // light: candles on the reading tables, two hanging chandeliers, a cool high fill
        CandleAt(r, new Vector3(21.2f, 0.96f, 45.0f), 1.8f, 6f, "D/SM_Prop_Candles_01_Preset");
        CandleAt(r, new Vector3(18.0f, 1.48f, 42.0f), 1.2f, 5f, "D/SM_Prop_Candles_02_Preset");
        foreach (var p in new[] { new Vector3(21f, 7.5f, 44.5f), new Vector3(22.5f, 7.5f, 53f) })
        {
            var ch = Put("D/SM_Prop_Candle_Chandelier_02_Preset", p, 0, r.hang); NoCol(ch);
            PointL(r, p + Vector3.down * 2.3f, Candle, 4.0f, 12f, p.z < 50f, "Chandelier");
        }
        PointL(r, new Vector3(22.5f, 6.5f, 48f), Cold, 0.5f, 14f, false, "High Fill");
    }

    static void DressAlchemist()
    {
        var r = Rm("AS");
        // benches of glassware, much of it smashed; shelves of jars, spilled and stained
        Prop(r, "R/SM_Prop_Dwarf_Table_03", 35.0f, 45.2f, 0f);
        Prop(r, "R/SM_Prop_Dwarf_Table_02", 37.6f, 42.4f, 90f);
        Against("R/SM_Prop_Dwarf_Table_03", r, 'N', 35.2f, 0f, r.props, 0.1f);
        string[] glass = { "D/SM_Item_Potion_01", "D/SM_Item_Potion_02", "D/SM_Item_Potion_03", "D/SM_Item_Potion_05", "D/SM_Item_Potion_07", "D/SM_Item_Vial_01", "D/SM_Item_Vial_02", "D/SM_Item_Jar_01" };
        for (int i = 0; i < 9; i++) Prop(r, Pick(glass), 35.0f + Rf(-1.4f, 1.4f), 45.2f + Rf(-0.45f, 0.45f), Rf(0, 360), null, false, 0.91f);
        for (int i = 0; i < 6; i++) Prop(r, Pick(glass), 35.2f + Rf(-1.4f, 1.4f), 49.0f + Rf(-0.3f, 0.3f), Rf(0, 360), null, false, 0.91f);
        Prop(r, "D/SM_Item_Scale_01", 37.6f, 42.0f, 90f, null, false, 0.91f);
        Prop(r, "G/SM_Gen_Prop_Skull_01", 37.5f, 43.1f, 200f, null, false, 0.91f);
        Prop(r, "D/SM_Item_Potion_Pole_01", 31.4f, 48.6f, 0f);
        Prop(r, "D/SM_Prop_Cauldron_01", 32.0f, 44.0f, 0f);
        // shelves of jars on the walls
        foreach (float z in new[] { 42.5f, 45.0f, 47.5f })
            foreach (float y in new[] { 1.3f, 2.1f })
            {
                var sh = Against("R/SM_Prop_Dwarf_Shelf_01", r, 'E', z, y, r.props, 0.0f);
                for (int k = 0; k < 3; k++) Put(Pick(glass), FacePoint(r, 'E', z - 0.5f + 0.5f * k) + new Vector3(-0.16f, y + 0.16f, 0), Rf(0, 360), r.props);
            }
        Against("G/SM_Gen_Prop_Shelf_03", r, 'S', 33.5f, 0f, r.props, 0.05f);
        // smashed glass and stains
        Litter(r, 10, 31f, 39f, 41f, 49f, "D/SM_Prop_Vase_Shard_02", "D/SM_Prop_Vase_Shard_03", "D/SM_Prop_Vase_Shard_04", "D/SM_Prop_Vase_Broken_03");
        Prop(r, "D/SM_Prop_Vase_Broken_03", 34.2f, 44.0f, 0f, null, false);
        Prop(r, "D/SM_Prop_Vase_Broken_01", 37.0f, 46.6f, 40f, null, false);
        Cobweb(r, 39.3f, 49.3f, 3.8f, 225f, 0.6f);
        CandleAt(r, new Vector3(35.8f, 0.91f, 45.0f), 1.4f, 6f, "D/SM_Prop_Candles_03_Preset");
        PointL(r, new Vector3(32.0f, 1.4f, 44.0f), Moss, 0.9f, 5f, false, "Cauldron Glow");
        PointL(r, new Vector3(35f, 3.6f, 45f), Candle, 1.0f, 9f, false, "Fill");
        Torch(r, 'W', 47.5f, 2.4f, 2.0f, 7f, true);
    }

    static void DressVault()
    {
        var r = Rm("V");
        // small, low and sealed: a hoard nobody found
        Against("R/SM_Prop_Dwarf_TreasureChest_01", r, 'N', 32.9f, 0f, r.props, 0.02f, new Vector3(0.8f, 0.8f, 0.8f));
        Against("D/SM_Prop_Chest_03", r, 'E', 51.3f, 0f, r.props, 0.05f, null, true, 0f);
        Prop(r, "D/SM_Prop_Chest_04", 32.6f, 51.0f, 160f);
        Prop(r, "D/SM_Prop_Chest_01", 31.0f, 54.1f, 10f);
        Prop(r, "R/SM_Prop_Gold_Pile_02", 32.6f, 52.6f, 20f, new Vector3(0.75f, 0.9f, 0.75f), false);
        Prop(r, "R/SM_Prop_Gold_Pile_03", 31.2f, 51.3f, 70f, null, false);
        for (int i = 0; i < 6; i++) Prop(r, Pick("R/SM_Prop_Gold_Coins_06", "R/SM_Prop_Gold_Coins_07", "R/SM_Prop_Gem_02", "R/SM_Prop_Goblet_02", "D/SM_Item_Coins_03"), Rf(30.9f, 34.1f), Rf(50.9f, 54.1f), Rf(0, 360), null, false);
        Put("R/FX_Gold_Sparkle_01", new Vector3(32.6f, 0.4f, 52.6f), 0, r.props);
        PointL(r, new Vector3(32.5f, 2.3f, 52.5f), new Color(1f, 0.82f, 0.5f), 1.0f, 6f, false, "Glint");
    }

    static void DressShrine()
    {
        var r = Rm("H");
        // tall and still: an altar on the south side, candles, statues — desecrated
        Against("D/SM_Env_Alter_01", r, 'S', 7.5f, 0f, r.props, 0.6f);
        Prop(r, "D/SM_Env_Tiles_Pentagram_01", 7.5f, 46.5f, 0f, null, false, 0.0f);
        foreach (var p in new[] { new Vector2(5.4f, 41.6f), new Vector2(9.6f, 41.6f), new Vector2(4.2f, 46.0f), new Vector2(10.8f, 46.0f) })
            CandleAt(r, new Vector3(p.x, 0f, p.y), 1.5f, 6f, "D/SM_Prop_Candle_Stand_01_Preset");
        CandleAt(r, new Vector3(7.0f, 2.75f, 41.9f), 1.6f, 5f, "D/SM_Prop_Candles_04_Preset");
        // statues along the walls, some thrown down
        Against("D/SM_Env_Statue_02", r, 'W', 47.5f, 0f, r.props, 0.4f);
        Against("D/SM_Env_Statue_03", r, 'W', 52.5f, 0f, r.props, 0.4f);
        Against("D/SM_Env_Statue_04", r, 'E', 52.5f, 0f, r.props, 0.4f);
        Tilted(r, "R/SM_Env_Statue_Broken_02", new Vector3(12.6f, 0.4f, 43.8f), new Vector3(0, 30, 80), true);
        Prop(r, "R/SM_Env_Statue_Broken_01_Head", 11.5f, 44.8f, 120f, null, false);
        Prop(r, "R/SM_Env_Statue_Broken_05", 2.0f, 42.0f, 60f);
        Prop(r, "D/SM_Env_Pillar_Broken_02", 2.2f, 53.0f, 0f);
        Prop(r, "D/SM_Env_Pillar_Broken_Pile_02", 4.0f, 52.0f, 200f, null, false);
        foreach (var p in new[] { new Vector2(3.0f, 50.0f), new Vector2(12.0f, 50.0f) })
        {
            PutC("D/SM_Env_Wall_Pillar_Large_Round_01_Base", p.x, p.y, 0f, 0, r.props);
            for (int k = 0; k < 2; k++) SetStatic(PutC("D/SM_Env_Wall_Pillar_Large_Round_01", p.x, p.y, 0.9f + k * 4.95f, 0, r.props));
        }
        Litter(r, 6, 1f, 14f, 43f, 54f, "R/SM_Env_Rubble_Slab_04", "R/SM_Env_Rubble_Tile_02", "D/SM_Env_Brick_Rubble_01", "D/SM_Prop_Vase_Shard_01");
        Prop(r, "R/SM_Env_Rubble_Slab_02", 9.8f, 52.6f, 30f, null, false);
        Cobweb(r, 13.7f, 53.7f, 8.5f, 225f, 0.7f);
        Put("D/FX_Ghost_Dust_01", new Vector3(7.5f, 3f, 47.5f), 0, r.props);
        // a cold shaft of light from high above onto the altar; candle warmth below
        SpotL(r, new Vector3(7.5f, 9.6f, 44.5f), new Vector3(75, 180, 0), new Color(0.75f, 0.82f, 1f), 12f, 16f, 40f, true);
        PointL(r, new Vector3(7.5f, 7f, 48f), Cold, 0.7f, 16f, false, "High Fill");
    }

    static void DressCrypt()
    {
        var r = Rm("C");
        // tombs in two rows, broken open; burial niches in the walls (the alcove wall pieces)
        Prop(r, "M/SM_Prop_Tomb_Royal_01", -6.5f, 44.6f, 90f);
        Tilted(r, "M/SM_Prop_Tomb_Royal_Lid_01", new Vector3(-6.6f, 0.25f, 43.4f), new Vector3(0, 84, 14), true);
        Prop(r, "M/SM_Prop_Tomb_01", -3.0f, 44.6f, 90f);
        Prop(r, "M/SM_Prop_Tomb_Skeleton_01", -3.0f, 44.6f, 90f, null, false, 0.2f);
        Prop(r, "R/SM_Prop_Sarcophagus_01", -6.5f, 47.6f, 90f);
        Prop(r, "D/SM_Prop_Coffin_01", -2.6f, 47.7f, 90f);
        Tilted(r, "M/SM_Prop_Tomb_Lid_01", new Vector3(-1.2f, 0.3f, 46.2f), new Vector3(0, 10, 75), true);
        Prop(r, "M/SM_Prop_Tomb_01", -8.6f, 41.4f, 90f);
        // collapse in the north-west corner
        Prop(r, "D/SM_Env_RockPile_Square_02", -8.4f, 49.0f, 30f, new Vector3(0.6f, 0.5f, 0.6f));
        Prop(r, "D/SM_Env_Brick_Rubble_03", -6.6f, 49.1f, 0f, null, false);
        Prop(r, "D/SM_Prop_Bricks_04", -4.0f, 49.0f, 15f, null, false);
        Litter(r, 8, -9.5f, -0.5f, 40.5f, 49.5f, "G/SM_Gen_Prop_Skull_01", "G/SM_Gen_Prop_Skull_01", "D/SM_Prop_Skeleton_01", "G/SM_Gen_Prop_Skull_01", "D/SM_Prop_Brick_01");
        Cobweb(r, -9.4f, 40.6f, 2.4f, 45f, 0.45f);
        Cobweb(r, -0.6f, 49.4f, 2.4f, 225f, 0.45f);
        CandleAt(r, new Vector3(-5.0f, 0.63f, 44.4f), 0.9f, 5f, "D/SM_Prop_Candles_01_Preset");
        PointL(r, new Vector3(-5f, 2.4f, 45f), Cold, 0.32f, 9f, false, "Dim Fill");
        PointL(r, new Vector3(-1.4f, 1.0f, 41.5f), Moss, 0.35f, 4f, false, "Glow");
    }

    static void DressThrone()
    {
        var r = Rm("T");
        // sparse and grand: two rows of tall columns, the torn runner, braziers at the stage, banners
        foreach (float x in new[] { 0.5f, 14.5f })
            foreach (float z in new[] { 61f, 68f, 75f })
            {
                PutC("D/SM_Env_Wall_Pillar_Large_Round_01_Base", x, z, 0f, 0, r.props);
                for (int k = 0; k < 3; k++) SetStatic(PutC("D/SM_Env_Wall_Pillar_Large_Round_01", x, z, 0.9f + k * 4.75f, 0, r.props));
            }
        // the runner, torn in two and scuffed aside
        Prop(r, "D/SM_Prop_Rug_01", 7.5f, 63.0f, 90f, null, false, 0.0f);
        Tilted(r, "D/SM_Prop_Rug_01", new Vector3(7.8f, 0.0f, 71.2f), new Vector3(0, 93f, 0), false, new Vector3(0.42f, 1f, 1f));
        Tilted(r, "D/SM_Prop_Rug_02", new Vector3(6.0f, 0.0f, 74.6f), new Vector3(0, 61f, 0), false, new Vector3(0.5f, 1f, 0.6f));
        // braziers burning either side of the steps
        foreach (float x in new[] { 3.0f, 12.0f })
        {
            Prop(r, "R/SM_Prop_Dwarf_Brazier_01", x, 77.2f, 0f);
            Fx("R/FX_Fire_Large_01", new Vector3(x, 1.3f, 77.2f), r.props, 0.45f);
            PointL(r, new Vector3(x, 2.3f, 77.2f), Fire, 7f, 14f, true, "Brazier");
        }
        // banners behind the throne and on the long walls
        Against("D/SM_Prop_Wall_Banner_05", r, 'N', 4.0f, 12.5f, r.props, 0.05f, null, false);
        Against("D/SM_Prop_Wall_Banner_05", r, 'N', 11.0f, 12.5f, r.props, 0.05f, null, false);
        foreach (float z in new[] { 64.5f, 71.5f })
        {
            Against("R/SM_Prop_Banner_Damaged_01", r, 'W', z, 12.5f, r.props, 0.05f, null, false);
            Against("R/SM_Prop_Banner_Damaged_02", r, 'E', z, 12.5f, r.props, 0.05f, null, false);
        }
        // a fallen statue and rubble: grandeur, fallen
        Tilted(r, "R/SM_Env_Statue_Broken_03", new Vector3(-2.6f, 0.4f, 80.5f), new Vector3(0, 40, 75), true);
        Prop(r, "R/SM_Env_Statue_06", 17.6f, 82.4f, 200f);
        Prop(r, "R/SM_Env_Rubble_Slab_01", 16.8f, 60.0f, 20f, new Vector3(0.6f, 0.6f, 0.6f), false);
        Prop(r, "D/SM_Env_Pillar_Broken_Pile_02", -2.4f, 66f, 90f, null, false);
        Cobweb(r, 18.6f, 83.6f, 12.5f, 225f, 0.8f);
        Cobweb(r, -3.6f, 83.6f, 12.5f, 135f, 0.8f);
        // bright: wall torches down both sides, two great chandeliers, light on the throne
        foreach (float z in new[] { 58f, 64.5f, 71.5f, 78.5f })
        {
            Torch(r, 'W', z, 3.0f, 3.5f, 11f, z == 64.5f);
            Torch(r, 'E', z, 3.0f, 3.5f, 11f, z == 71.5f);
        }
        foreach (float z in new[] { 64f, 74f })
        {
            var ch = PutC("R/SM_Prop_Dwarf_Chandelier_01", 7.5f, z, 8.5f, 0, r.hang); NoCol(ch);
            var top = Put("R/SM_Prop_Dwarf_Chain_01", new Vector3(7.5f, 8.5f + 3.6f, z), 0, r.hang, new Vector3(1, 0.8f, 1)); NoCol(top);
            Fx("R/FX_Fire_Chandelier_01", new Vector3(7.5f, 9.2f, z), r.hang, 0.8f);
            PointL(r, new Vector3(7.5f, 8.2f, z), Candle, 16f, 24f, z > 70f, "Chandelier");
        }
        SpotL(r, new Vector3(7.5f, 12f, 76f), new Vector3(55, 0, 0), new Color(1f, 0.85f, 0.65f), 30f, 20f, 38f, true);
        PointL(r, new Vector3(7.5f, 11f, 66f), Candle, 3f, 30f, false, "High Fill");
        // warm light washing the long walls and the columns, high up, so the whole hall reads bright
        foreach (float z in new[] { 61f, 68f, 75f })
            foreach (float x in new[] { 1.8f, 13.2f })
                PointL(r, new Vector3(x, 6.5f, z), Candle, 5.5f, 16f, false, "Wall Wash");
        PointL(r, new Vector3(7.5f, 5.5f, 80f), Candle, 6f, 12f, false, "Stage Fill");
    }

    // ---------------------------------------------------------------- markers
    static void MakeMaterials()
    {
        Directory.CreateDirectory("Assets/IronCitadel/Materials");
        mRanged = Mat("M_Marker_Ranged", new Color(0.2f, 0.45f, 1f), 0.6f);
        mMelee = Mat("M_Marker_Melee", new Color(0.9f, 0.18f, 0.12f), 0.6f);
        mWarlord = Mat("M_Marker_Warlord", new Color(0.65f, 0.2f, 0.95f), 0.9f);
        mGear = Mat("M_Marker_BestGear", new Color(0.2f, 1f, 0.75f), 1.6f);
        mHoard = Mat("M_Marker_Hoard", new Color(1f, 0.78f, 0.2f), 1.6f);
    }

    static Material Mat(string name, Color c, float emit)
    {
        string path = "Assets/IronCitadel/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * emit);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Enemy(string kind, string roomId, float x, float y, float z, Vector2 facing)
    {
        var r = Rm(roomId);
        var go = new GameObject($"{kind} ({r.name})");
        go.transform.SetParent(markersRoot, false);
        go.transform.position = new Vector3(x, y, z);
        go.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0, facing.y));
        var mk = go.AddComponent<Marker>(); mk.kind = kind; mk.roomName = r.name;
        var mat = kind == "ranged" ? mRanged : kind == "melee" ? mMelee : mWarlord;
        float s = kind == "warlord" ? 1.25f : 1f;
        Prim(PrimitiveType.Capsule, go.transform, new Vector3(0, 0.9f * s, 0), new Vector3(0.7f, 0.9f, 0.7f) * s, mat);
        Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 1.45f * s, 0.32f * s), new Vector3(0.36f, 0.14f, 0.3f) * s, mat);   // visor shows facing
        if (kind == "ranged") Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0.3f, 1.1f, 0.25f), new Vector3(0.05f, 0.7f, 0.05f), mat);
        if (kind == "warlord") Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 2.35f, 0), new Vector3(0.5f, 0.12f, 0.5f), mHoard);
    }

    static void Pickup(string kind, string roomId, float x, float y, float z)
    {
        var r = Rm(roomId);
        var go = new GameObject($"{kind} ({r.name})");
        go.transform.SetParent(markersRoot, false);
        go.transform.position = new Vector3(x, y, z);
        var mk = go.AddComponent<Marker>(); mk.kind = kind; mk.roomName = r.name;
        var mat = kind == "best gear" ? mGear : mHoard;
        var gem = Prim(PrimitiveType.Cube, go.transform, new Vector3(0, 1.3f, 0), new Vector3(0.32f, 0.32f, 0.32f), mat);
        gem.transform.localRotation = Quaternion.Euler(45, 0, 45);
        var l = new GameObject("Glow"); l.transform.SetParent(go.transform, false); l.transform.localPosition = new Vector3(0, 1.3f, 0);
        var lt = l.AddComponent<Light>(); lt.type = LightType.Point; lt.color = mat.GetColor("_BaseColor"); lt.intensity = 0.8f; lt.range = 2.5f;
        l.AddComponent<UniversalAdditionalLightData>();
    }

    static GameObject Prim(PrimitiveType t, Transform parent, Vector3 lp, Vector3 ls, Material m)
    {
        var p = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false); p.transform.localPosition = lp; p.transform.localScale = ls;
        var mr = p.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = ShadowCastingMode.On;
        return p;
    }

    static void PlaceMarkers()
    {
        var toHall = new Vector2(0, -1);
        // armory: archers behind the guarded entrance, facing the entry hall through it; men-at-arms in the rows
        Vector2 entry = new Vector2(7.5f, 6.0f);
        foreach (var p in new[] { new Vector2(1.0f, 11.5f), new Vector2(2.4f, 11.05f), new Vector2(12.6f, 11.05f), new Vector2(14.0f, 11.5f) })
            Enemy("ranged", "A", p.x, 0, p.y, (entry - p).normalized);
        foreach (var p in new[] { new Vector2(2.9f, 15.8f), new Vector2(12.1f, 15.8f), new Vector2(2.9f, 18.2f), new Vector2(12.1f, 18.2f), new Vector2(7.5f, 16.4f) })
            Enemy("melee", "A", p.x, 0, p.y, new Vector2(0, -1));
        // dining hall: archers on the gallery looking out over the floor; the largest group on the floor
        foreach (float x in new[] { 3.5f, 9.5f, 15.5f, 21.5f, 27.0f })
            Enemy("ranged", "D", x, 5.28f, 22.95f, new Vector2(0, 1));
        foreach (var p in new[] { new Vector2(7.6f, 29.0f), new Vector2(15.0f, 31.5f), new Vector2(22.4f, 33.5f), new Vector2(13.5f, 36.6f), new Vector2(7.6f, 34.5f), new Vector2(27.8f, 29.5f) })
            Enemy("melee", "D", p.x, 0, p.y, new Vector2(Rf(-0.5f, 0.5f), Rf(-1f, -0.3f)).normalized);
        // throne room: the warlord before the throne, guards at the foot of the stage and along the aisle
        Enemy("warlord", "T", 7.5f, 1.5f, 81.0f, toHall);
        foreach (var p in new[] { new Vector2(4.2f, 75.4f), new Vector2(10.8f, 75.4f), new Vector2(3.2f, 69.5f), new Vector2(11.8f, 69.5f) })
            Enemy("melee", "T", p.x, 0, p.y, toHall);
        Pickup("best gear", "A", 7.5f, 0f, 14.3f);
        Pickup("hoard", "V", 32.6f, 0f, 53.0f);
    }

    // ---------------------------------------------------------------- lighting, post, player, HUD
    static void SetupLightingAndPost()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.27f, 0.26f, 0.27f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.205f, 0.19f);
        RenderSettings.ambientGroundColor = new Color(0.13f, 0.12f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.06f, 0.05f, 0.045f);
        RenderSettings.fogDensity = 0.022f;
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0.3f;
        var ls = new LightingSettings { bakedGI = false, realtimeGI = false };
        Directory.CreateDirectory("Assets/IronCitadel");
        AssetDatabase.CreateAsset(ls, "Assets/IronCitadel/IronCitadelLighting.lighting");
        Lightmapping.lightingSettings = ls;

        var volGo = new GameObject("Post Volume");
        volGo.transform.SetParent(root, false);
        var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1;
        string profPath = "Assets/IronCitadel/IronCitadelPost.asset";
        AssetDatabase.DeleteAsset(profPath);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(prof, profPath);
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bl = prof.Add<Bloom>(true); bl.threshold.Override(1.0f); bl.intensity.Override(0.7f); bl.scatter.Override(0.65f); bl.tint.Override(new Color(1f, 0.85f, 0.7f));
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0.75f); ca.contrast.Override(14f); ca.saturation.Override(-8f); ca.colorFilter.Override(new Color(1f, 0.98f, 0.95f));
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.3f); vg.smoothness.Override(0.45f);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(-4f);
        var lut = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Cinema Suite/Cinema Themes/LUTs/Weathered3D16.png");
        if (lut != null && lut.width == 256 && lut.height == 16) { var cl = prof.Add<ColorLookup>(true); cl.texture.Override(lut); cl.contribution.Override(0.2f); }
        foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();
        vol.sharedProfile = prof;
    }

    static LevelHUD SetupPlayerAndHud()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interact/IronCitadelPlayer.prefab");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.position = new Vector3(7.5f, 0.02f, 2.2f);
        player.transform.rotation = Quaternion.identity;
        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam != null)
        {
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.farClipPlane = 200f;
        }

        var level = new GameObject("Level");
        level.transform.SetParent(root, false);
        var hud = level.AddComponent<LevelHUD>();
        hud.throne = GameObject.Find("Throne").transform;
        hud.stageTopY = 1.5f;
        var oc = new GameObject("Overview Camera");
        oc.transform.SetParent(level.transform, false);
        var ocam = oc.AddComponent<Camera>();
        ocam.orthographic = true;
        float minX = rooms.Min(r => r.x0), maxX = rooms.Max(r => r.x1), minZ = rooms.Min(r => r.z0), maxZ = rooms.Max(r => r.z1);
        oc.transform.position = new Vector3((minX + maxX) * 0.5f, 90f, (minZ + maxZ) * 0.5f);
        oc.transform.rotation = Quaternion.Euler(90, 0, 0);
        ocam.orthographicSize = (maxZ - minZ) * 0.5f + 3f;
        ocam.nearClipPlane = 1f; ocam.farClipPlane = 120f;
        ocam.clearFlags = CameraClearFlags.SolidColor; ocam.backgroundColor = new Color(0.03f, 0.03f, 0.035f);
        ocam.enabled = false;
        var ocd = ocam.GetUniversalAdditionalCameraData(); ocd.renderPostProcessing = true;
        hud.overviewCamera = ocam;
        return hud;
    }

    static void BakeNavMesh()
    {
        var surf = root.gameObject.AddComponent<NavMeshSurface>();
        surf.collectObjects = CollectObjects.Children;
        surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surf.layerMask = ~0;
        Physics.SyncTransforms();
        surf.BuildNavMesh();
        string dir = "Assets/Scenes/IronCitadel";
        Directory.CreateDirectory(dir);
        string p = dir + "/NavMesh.asset";
        AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(surf.navMeshData, p);
        surf.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(p);
        surf.AddData();
    }
}
