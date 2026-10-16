using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IronCitadel.Interact;
using IronCitadel.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Builds Assets/Scenes/IronCitadel.unity from the Synty kit. Run with:
//   unity command run_script -- --file Build/BuildLevel.cs --entry BuildLevel.Run
// Rooms sit on a 5 m grid; walls are the kit's 5 m modules, scaled in Y for low rooms and stacked for tall ones.
public static class BuildLevel
{
    public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
    const string PlayerPath = "Assets/Interact/IronCitadelPlayer.prefab";
    const string DungeonFolder = "Assets/Synty/PolygonDungeon/Prefabs";
    const string RealmsFolder = "Assets/Synty/PolygonDungeonRealms/Prefabs";
    const string Look = "Assets/IronCitadel/Settings/Iron Citadel Look.asset";
    const string Lut = "Assets/Cinema Suite/Cinema Themes/LUTs/BleachBypass3D16.png";
    const string MarkerFolder = "Assets/IronCitadel/Markers";

    const float LightGain = 8f;     // the authored light intensities are scaled to the URP light units
    const float Cell = 5f;          // the kit's module size
    const float WallH = 5.15f;      // the kit's wall module height
    const float TileTop = 0.18f;    // a floor tile's top sits this far above its origin
    const float TileLift = 0.09f;   // the tile's collider top is this far above its origin: lifted so it stands on y = 0
    const float DeckTop = 4.60f;    // the gallery deck, just under the stair's top tread (4.62)
    const float StairScale = 0.92f; // Stairs_Large_01 scaled so each riser is 0.285 m, under the 0.3 m step offset
    const float HoleWidth = 1.66f;  // measured clear width of DoorFrame_01's opening
    const float HoleTop = 2.29f;    // and its height

    const string PlainWall = "SM_Env_Wall_01_DoubleSided";
    const string TrimWall = "SM_Env_Wall_Trim_Top_02";
    const string Frame = "SM_Env_Wall_DoorFrame_01";

    class Room { public string name, kind, state; public float x0, x1, z0, z1, h; public Transform root; }
    class Door { public string a, b, state; public float x, z; public bool alongX, slides; }

    static readonly List<Room> Rooms = new List<Room>();
    static readonly List<Door> Doors = new List<Door>();
    static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static Transform Level;
    static int ceilingLayer;
    static readonly StringBuilder Log = new StringBuilder();

    public static string Run()
    {
        Log.Clear();
        ceilingLayer = EnsureLayer("Ceiling");
        Plan();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
            if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null) UnityEngine.Object.DestroyImmediate(go);

        Environment();
        Level = new GameObject("Level").transform;
        foreach (var r in Rooms) CreateRoom(r);
        Walls();
        Gallery();
        Stage();
        Furnish();
        Lights();
        Markers();
        Player();
        new GameObject("Level HUD").AddComponent<LevelHud>();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Log.AppendLine($"saved {ScenePath}: {Rooms.Count} rooms, {Doors.Count} doors, {Level.childCount} room roots");
        return Log.ToString();
    }

    // ---------- plan (metres; x east, z north; Y up) ----------

    static void AddRoom(string name, string kind, string state, float x0, float x1, float z0, float z1, float h)
    {
        Rooms.Add(new Room { name = name, kind = kind, state = state, x0 = x0, x1 = x1, z0 = z0, z1 = z1, h = h });
    }

    // a door sits at the middle of a 5 m wall segment; alongX means the wall runs east-west
    static void AddDoor(string a, string b, string state, float x, float z, bool alongX, bool slides = false)
    {
        Doors.Add(new Door { a = a, b = b, state = state, x = x, z = z, alongX = alongX, slides = slides });
    }

    static void Plan()
    {
        Rooms.Clear(); Doors.Clear();
        AddRoom("Entry hall", "entry hall", "decayed", 0, 15, 0, 20, 5f);
        AddRoom("Armory", "armory", "stocked", 15, 30, 0, 20, 5f);
        AddRoom("Dining hall", "dining hall", "decayed", 30, 60, 0, 20, 8f);
        AddRoom("Guard room", "guard room", "looted", 0, 15, 20, 30, 3.2f);
        AddRoom("Prison", "prison", "looted", 15, 30, 20, 30, 3f);
        AddRoom("Torture chamber", "torture chamber", "decayed", 30, 45, 20, 30, 3f);
        AddRoom("Crypt", "crypt", "decayed", 45, 60, 20, 30, 3f);
        AddRoom("East passage", "extra", "decayed", 60, 65, 5, 30, 4f);
        AddRoom("Kitchen", "kitchen", "looted", 0, 15, -15, 0, 3.2f);
        AddRoom("Storeroom", "storeroom", "looted", 15, 30, -15, 0, 5f);
        AddRoom("Forge", "forge", "decayed", 15, 30, -30, -15, 5f);
        AddRoom("Treasure vault", "treasure vault", "untouched", 0, 15, -30, -15, 2.8f);
        AddRoom("Library", "library", "decayed", 45, 60, -15, 0, 7f);
        AddRoom("Shrine", "shrine", "decayed", 30, 45, -15, 0, 7f);
        AddRoom("Alchemist's study", "alchemist's study", "looted", 45, 60, -30, -15, 5f);
        AddRoom("Throne room", "throne room", "decayed", 30, 45, -30, -15, 10f);

        AddDoor("Entry hall", "Armory", "open", 15, 7.5f, false);
        AddDoor("Entry hall", "Guard room", "shut", 7.5f, 20, true);
        AddDoor("Guard room", "Prison", "shut", 15, 22.5f, false);
        AddDoor("Prison", "Torture chamber", "shut", 30, 22.5f, false);
        AddDoor("Torture chamber", "Crypt", "shut", 45, 22.5f, false);
        AddDoor("Crypt", "East passage", "open", 60, 22.5f, false);
        AddDoor("East passage", "Dining hall", "shut", 60, 7.5f, false);
        AddDoor("Dining hall", "Armory", "open", 30, 12.5f, false);
        AddDoor("Entry hall", "Kitchen", "shut", 7.5f, 0, true);
        AddDoor("Kitchen", "Storeroom", "open", 15, -7.5f, false);
        AddDoor("Storeroom", "Forge", "shut", 22.5f, -15, true);
        AddDoor("Forge", "Treasure vault", "hidden", 15, -22.5f, false, slides: true);
        AddDoor("Dining hall", "Library", "shut", 52.5f, 0, true);
        AddDoor("Library", "Shrine", "open", 45, -7.5f, false);
        AddDoor("Shrine", "Throne room", "shut", 37.5f, -15, true);
        AddDoor("Library", "Alchemist's study", "shut", 52.5f, -15, true);
    }

    static Room RoomNamed(string name) => Rooms.First(r => r.name == name);

    static Room RoomAt(Vector3 p)
    {
        foreach (var r in Rooms)
            if (p.x > r.x0 && p.x < r.x1 && p.z > r.z0 && p.z < r.z1) return r;
        return null;
    }

    // ---------- scene environment ----------

    static void Environment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.095f, 0.09f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.035f, 0.032f, 0.03f);

        var go = new GameObject("Post-processing");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = MakeLook();
    }

    static VolumeProfile MakeLook()
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(Look) != null) AssetDatabase.DeleteAsset(Look);
        Directory.CreateDirectory(Path.GetDirectoryName(Look));
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, Look);

        var ca = p.Add<ColorAdjustments>(true);
        ca.postExposure.overrideState = true; ca.postExposure.value = 0.25f;
        ca.contrast.overrideState = true; ca.contrast.value = 12f;
        ca.saturation.overrideState = true; ca.saturation.value = -12f;

        var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(Lut);
        if (lut != null)
        {
            var cl = p.Add<ColorLookup>(true);
            cl.texture.overrideState = true; cl.texture.value = lut;
            cl.contribution.overrideState = true; cl.contribution.value = 0.6f;
        }
        var tm = p.Add<Tonemapping>(true);
        tm.mode.overrideState = true; tm.mode.value = TonemappingMode.ACES;
        var bl = p.Add<Bloom>(true);
        bl.threshold.overrideState = true; bl.threshold.value = 0.95f;
        bl.intensity.overrideState = true; bl.intensity.value = 0.2f;
        var vg = p.Add<Vignette>(true);
        vg.intensity.overrideState = true; vg.intensity.value = 0.35f;
        vg.smoothness.overrideState = true; vg.smoothness.value = 0.5f;
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    // ---------- rooms: floor and ceiling tiles ----------

    static void CreateRoom(Room r)
    {
        var go = new GameObject(r.name);
        go.transform.SetParent(Level, false);
        var lr = go.AddComponent<LevelRoom>();
        lr.roomName = r.name; lr.kind = r.kind; lr.state = r.state; lr.ceilingY = r.h;
        r.root = go.transform;
        int k = 0;
        for (float x = r.x0; x < r.x1 - 0.01f; x += Cell)
            for (float z = r.z0; z < r.z1 - 0.01f; z += Cell, k++)
            {
                // a 5 m tile's origin is its +x, -z corner: the tile spans x-5..x and z..z+5
                Put("SM_Env_Tiles_0" + (k % 3 + 1), new Vector3(x + Cell, TileLift - TileTop, z), 0, r.root, "Floor");
                Put("SM_Env_Ceiling_Stone_Flat_0" + (k % 3 + 1), new Vector3(x + Cell, r.h, z), 0, r.root, "Ceiling", ceilingLayer);
            }
    }

    // ---------- walls and doors ----------

    static void Walls()
    {
        var built = new HashSet<string>();
        foreach (var r in Rooms)
        {
            // each edge runs inward from its outside: north, south, east, west
            Edge(r, true, r.z1, r.x0, r.x1, new Vector3(0, 0, -1), built);
            Edge(r, true, r.z0, r.x0, r.x1, new Vector3(0, 0, 1), built);
            Edge(r, false, r.x1, r.z0, r.z1, new Vector3(-1, 0, 0), built);
            Edge(r, false, r.x0, r.z0, r.z1, new Vector3(1, 0, 0), built);
        }
    }

    static void Edge(Room r, bool alongX, float line, float from, float to, Vector3 inward, HashSet<string> built)
    {
        for (float s = from; s < to - 0.01f; s += Cell)
        {
            Vector3 a = alongX ? new Vector3(s, 0, line) : new Vector3(line, 0, s);
            Vector3 b = alongX ? new Vector3(s + Cell, 0, line) : new Vector3(line, 0, s + Cell);
            Vector3 mid = (a + b) * 0.5f;
            if (!built.Add((alongX ? "X" : "Z") + line.ToString("F2") + "@" + s.ToString("F2"))) continue;

            Room other = RoomAt(mid - inward * 0.5f);
            float h = other != null ? Mathf.Max(r.h, other.h) : r.h;
            Door door = DoorAt(mid, alongX);
            if (door == null)
            {
                Wall(r.root, a, b, inward, h);
                continue;
            }
            Opening(r, door, a, b, mid, inward);
        }
    }

    static Door DoorAt(Vector3 mid, bool alongX)
    {
        foreach (var d in Doors)
            if (d.alongX == alongX && Mathf.Abs(d.x - mid.x) < 0.01f && Mathf.Abs(d.z - mid.z) < 0.01f) return d;
        return null;
    }

    // a plain wall; over 5.15 m it is stacked, with a trim course hiding the join
    static void Wall(Transform parent, Vector3 a, Vector3 b, Vector3 front, float h)
    {
        if (h <= WallH + 0.01f)
        {
            Piece(PlainWall, a, b, front, 0f, h / WallH, parent);
            return;
        }
        Piece(PlainWall, a, b, front, 0f, 1f, parent);
        Piece(TrimWall, a, b, front, WallH - 0.2f, 1f, parent);
        Piece(PlainWall, a, b, front, WallH, (h - WallH) / WallH, parent);
    }

    // A doorway in the kit's door frame, with the door, hidden slab or nothing in it.
    static void Opening(Room owner, Door d, Vector3 a, Vector3 b, Vector3 mid, Vector3 inward)
    {
        // the frame is a full 5 m module with a hole in it; one copy faces each room
        Piece(Frame, a, b, inward, 0f, 1f, owner.root, "Door frame");
        Piece(Frame, a, b, -inward, 0f, 1f, owner.root, "Door frame");

        var root = new GameObject($"Door: {d.a} - {d.b}").transform;
        root.SetParent(owner.root, false);
        root.SetPositionAndRotation(new Vector3(mid.x, 0f, mid.z), Quaternion.LookRotation(Flat(inward), Vector3.up));
        var door = root.gameObject.AddComponent<LevelDoor>();
        door.roomA = d.a; door.roomB = d.b; door.state = d.state; door.width = HoleWidth;
        door.slidesDown = d.slides;
        if (d.state == "open") return;

        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(root, false);
        blocker.transform.localPosition = new Vector3(0f, HoleTop / 2f, 0f);
        var box = blocker.AddComponent<BoxCollider>();
        box.size = new Vector3(HoleWidth, HoleTop, 0.4f);

        // the leaf hangs from a pivot on its jamb; the stone slab of a hidden door is as wide as the hole's frame
        string leafPrefab = d.state == "hidden" ? "SM_Env_Door_Large_Stone_01" : "SM_Env_Door_03";
        float half = d.state == "hidden" ? 1.025f : HoleWidth / 2f;
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(-half, 0f, 0f);
        var leaf = Put(leafPrefab, Vector3.zero, 0, hinge, "Leaf");
        var lb = LocalBounds(leafPrefab);
        leaf.transform.localPosition = new Vector3(half - lb.center.x, -lb.min.y, -lb.center.z);
        leaf.transform.localRotation = Quaternion.identity;
        door.leaf = hinge;
    }

    // ---------- gallery and stair in the dining hall ----------

    static void Gallery()
    {
        var hall = RoomNamed("Dining hall").root;
        var gallery = new GameObject("Gallery").transform;
        gallery.SetParent(hall, false);
        gallery.gameObject.AddComponent<LevelFeature>().feature = "gallery";
        // the deck ends where the stair's top begins (x 50), so the stair meets it with no overlap
        for (float x = 30f; x < 50f - 0.01f; x += Cell)
            for (float z = 5f; z < 20f - 0.01f; z += Cell)
                Put("SM_Env_Tiles_01", new Vector3(x + Cell, DeckTop - TileTop + TileLift, z), 0, gallery, "Deck");
        // the rail along the deck's edge over the hall floor: an open balustrade, so its own colliders come off and a
        // 0.35 m kick plate keeps the player on the deck (the step offset is 0.3 m, so the plate stops a walk)
        for (float x = 30f; x < 50f - 0.01f; x += Cell)
        {
            var rail = Piece("SM_Env_Railing_01", new Vector3(x, 0, 5f), new Vector3(x + Cell, 0, 5f), new Vector3(0, 0, -1), DeckTop, 1f, gallery, "Rail");
            foreach (var c in rail.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            var kick = new GameObject("Kick plate");
            kick.transform.SetParent(rail.transform, false);
            kick.transform.localPosition = new Vector3(-Cell / 2f, 0.175f, 0f); // the piece spans local x -5..0
            kick.AddComponent<BoxCollider>().size = new Vector3(Cell, 0.35f, 0.05f);
        }

        // the stair: Stairs_Large_01 turned to run east-west from the east wall (x 60, its foot beside the arrival door)
        // up to the deck at x 50. Its run is stretched from 8.77 to 10 m (treads a little deeper, risers unchanged)
        var stair = Put("SM_Env_Stairs_Large_01", new Vector3(55f, 0f, 13.6f), 90f, hall, "Stair");
        stair.transform.localScale = new Vector3(1f, StairScale, 10f / 8.77f);
        stair.AddComponent<LevelFeature>().feature = "stair";
    }

    // ---------- the throne's stage ----------

    static void Stage()
    {
        var throne = RoomNamed("Throne room").root;
        var stage = new GameObject("Stage").transform;
        stage.SetParent(throne, false);
        stage.gameObject.AddComponent<LevelFeature>().feature = "stage";
        const float top = 1f;
        foreach (float x0 in new[] { 30f, 35f, 40f })
        {
            var block = Put("SM_Env_Steps_01", new Vector3(x0 + Cell, 0, -30f), 0, stage, "Stage");
            // an invisible helper collider so the walkable top of the block is its 1.0 m top
            // it reaches 0.6 m over the front edge, where the stair's top tread meets the block
            var topCollider = block.AddComponent<BoxCollider>();
            topCollider.center = new Vector3(-Cell / 2f, 0.5f, Cell / 2f + 0.3f);
            topCollider.size = new Vector3(Cell, 1f, Cell + 0.6f);
            // Stairs_02 turned to rise from the room floor (north) to the stage (south), scaled to the stage's height
            Prop(stage, "SM_Env_Stairs_02", x0 + 2.5f, -22.5f, 0f, 0f, new Vector3(1f, top / 2.63f, 1f), "Stage steps");
        }
        Prop(throne, "SM_Env_Stone_Throne_01", 37.5f, -28.4f, 0f, top, null, "Throne").AddComponent<LevelFeature>().feature = "throne";
    }

    // ---------- furnishing ----------

    static void Furnish()
    {
        // Entry hall: benches, weapon stands and a table, no one in it
        var eh = RoomNamed("Entry hall").root;
        Prop(eh, "SM_Prop_Bench_01", 5f, 1.1f, 0f);
        Prop(eh, "SM_Prop_Bench_01", 5f, 14.9f, 0f);
        Prop(eh, "SM_Prop_WeaponRack_01", 1.4f, 3.5f, 90f);
        Prop(eh, "SM_Prop_WeaponRack_01", 1.4f, 11.5f, 90f);
        Prop(eh, "SM_Prop_WeaponRack_01", 12.5f, 1.6f, 0f);
        Prop(eh, "SM_Prop_Table_01", 11f, 16.6f, 0f);
        Prop(eh, "SM_Prop_Barrel_02", 13.5f, 9f, 0f);

        // Armory: dense racks in rows along both walls, a clear aisle between the two entrances
        var ar = RoomNamed("Armory").root;
        foreach (float z in new[] { 1.4f, 3.7f, 16.3f, 18.6f })
            foreach (float x in new[] { 17.5f, 19.7f, 21.9f, 24.1f, 26.3f })
                Prop(ar, "SM_Prop_WeaponRack_01", x, z, 0f);
        Prop(ar, "SM_Prop_Barrel_03", 28.6f, 2.2f, 0f);
        Prop(ar, "SM_Prop_Chest_03", 27.5f, 9.8f, 90f, name: "Best gear chest");

        // Dining hall: long tables along the south wall, chandeliers over the floor, banners on the walls
        var dh = RoomNamed("Dining hall").root;
        // the long tables stand under the gallery, along its inner side, so the hall floor in front of the rail stays open
        foreach (float x in new[] { 34f, 40f, 46f })
            Prop(dh, "SM_Prop_Table_01", x, 16.8f, 0f);
        foreach (float x in new[] { 36f, 46f, 56f })
            Prop(dh, "SM_Prop_Candle_Chandelier_02", x, 3.0f, 0f, 8f - 2.75f - 0.2f);
        Prop(dh, "SM_Prop_Wall_Banner_02", 30.6f, 2.5f, 90f, 3.0f);
        Prop(dh, "SM_Prop_Wall_Banner_05", 59.4f, 2.5f, -90f, 3.0f);
        Prop(dh, "SM_Prop_Barrel_01", 31.2f, 18.6f, 0f);
        Prop(dh, "SM_Prop_Barrel_03", 31.4f, 16.6f, 0f);

        // Guard room: low and wrecked
        var gr = RoomNamed("Guard room").root;
        Prop(gr, "SM_Prop_Table_01", 4.5f, 26.5f, 20f);
        Prop(gr, "SM_Prop_Bench_01", 12.5f, 25.5f, 0f);
        Prop(gr, "SM_Prop_WeaponRack_01", 1.4f, 28.8f, 0f);
        Prop(gr, "SM_Prop_Barrel_02", 12.5f, 28f, 0f);

        // Prison: barred cell doors along the north wall, the riot's leavings
        var pr = RoomNamed("Prison").root;
        foreach (float x in new[] { 17.5f, 20.5f, 23.5f, 26.5f })
            Prop(pr, "SM_Env_Door_Bars_01", x, 29.5f, 0f);
        Prop(pr, "SM_Prop_Crate_Wood_01", 18.5f, 26f, 15f);
        Prop(pr, "SM_Prop_Crate_Wood_03", 26.5f, 25.6f, -20f);
        Prop(pr, "SM_Prop_Barrel_01", 16.5f, 26.4f, 0f);

        // Torture chamber: stretch table, cage, cold brazier and chains
        var tc = RoomNamed("Torture chamber").root;
        Prop(tc, "SM_Prop_Toture_StretchTable_01", 38f, 27.6f, 0f);
        Prop(tc, "SM_Prop_Toture_Cage_01", 32.5f, 28.2f, 0f);
        Prop(tc, "SM_Prop_Toture_Cage_01", 43f, 28.2f, 0f);
        Prop(tc, "SM_Prop_Brazier_01", 34.5f, 25.2f, 0f);
        Prop(tc, "SM_Prop_Chain_01", 31.0f, 27.0f, 0f, 3f - 2.58f);

        // Crypt: coffins and bone piles, niches broken open
        var cr = RoomNamed("Crypt").root;
        Prop(cr, "SM_Prop_Coffin_01", 49.5f, 27.8f, 90f);
        Prop(cr, "SM_Prop_Coffin_01", 55.5f, 27.8f, 90f);
        Prop(cr, "SM_Env_BonePile_01", 47.5f, 24.5f, 0f);
        Prop(cr, "SM_Env_BonePile_02", 56.5f, 25.0f, 0f);

        // East passage: a bench and barrels against the walls, the lane kept clear
        var ep = RoomNamed("East passage").root;
        Prop(ep, "SM_Prop_Bench_01", 63.6f, 12f, 90f);
        Prop(ep, "SM_Prop_Barrel_03", 63.7f, 17.5f, 0f);

        // Kitchen: a cold hearth, worktables, barrels and crates
        var kt = RoomNamed("Kitchen").root;
        Prop(kt, "SM_Prop_Forge_FirePit_01", 3.5f, -7.5f, 0f);
        Prop(kt, "SM_Prop_Table_01", 10f, -3.0f, 0f);
        Prop(kt, "SM_Prop_Table_01", 10f, -12.0f, 0f);
        Prop(kt, "SM_Prop_Barrel_01", 1.5f, -2.0f, 0f);
        Prop(kt, "SM_Prop_Barrel_02", 1.5f, -13.0f, 0f);
        Prop(kt, "SM_Prop_Crate_Wood_02", 13.4f, -12.6f, 0f);

        // Storeroom: stacked crates and barrels in the corners, the lane kept clear
        var sr = RoomNamed("Storeroom").root;
        Prop(sr, "SM_Prop_Crate_Wood_01", 17.6f, -2.5f, 0f);
        Prop(sr, "SM_Prop_Crate_Wood_01", 17.6f, -4.0f, 0f, 1.10f);
        Prop(sr, "SM_Prop_Crate_Wood_04", 27.3f, -2.6f, 0f);
        Prop(sr, "SM_Prop_Crate_Wood_05", 27.3f, -4.3f, 0f);
        Prop(sr, "SM_Prop_Barrel_03", 28f, -10.5f, 0f);
        Prop(sr, "SM_Prop_Barrel_02", 17f, -13.2f, 0f);

        // Forge: hearth under the hood, the flue up to the ceiling, anvils and tools
        var fg = RoomNamed("Forge").root;
        Prop(fg, "SM_Prop_Forge_FirePit_01", 20f, -21f, 0f);
        Prop(fg, "SM_Prop_Forge_Ventilation_01", 20f, -21f, 0f, 1.1f);
        // the flue runs from the hood up to the ceiling: the pipe is stretched in Y to fill the gap
        Prop(fg, "SM_Prop_Forge_Ventilation_Pipe_01", 20f, -21f, 0f, 2.6f, new Vector3(1f, (4.85f - 2.6f) / 1.76f, 1f));
        Prop(fg, "SM_Prop_Anvil_01", 25.4f, -17.6f, 0f);
        Prop(fg, "SM_Prop_Barrel_01", 25.6f, -24.5f, 0f);
        Prop(fg, "SM_Prop_Barrel_02", 17.3f, -27.5f, 0f);
        Prop(fg, "SM_Prop_Forge_Hammer_01", 26.0f, -20.5f, 30f);

        // Treasure vault: the hoard, chests and coffers
        var tv = RoomNamed("Treasure vault").root;
        Prop(tv, "SM_Prop_Dwarf_TreasureChest_01", 4.0f, -18.5f, 90f, name: "Hoard chest");
        Prop(tv, "SM_Prop_Chest_01", 9.5f, -18.0f, 0f);
        Prop(tv, "SM_Prop_Chest_02", 11.5f, -23.0f, 90f);
        Prop(tv, "SM_Prop_Chest_04", 3.5f, -25.0f, 0f);
        Prop(tv, "SM_Prop_Chest_03", 8.5f, -27.0f, 0f);

        // Library: bookcases packed against the walls, stacked two high; reading tables and fallen books
        var lb = RoomNamed("Library").root;
        foreach (float z in new[] { -2.5f, -5.0f, -7.5f, -10f, -12.5f })
        {
            Prop(lb, "SM_Prop_Bookcase_03", 59.2f, z, -90f);
            Prop(lb, "SM_Prop_Bookcase_03", 59.2f, z, -90f, 1.42f);
        }
        foreach (float x in new[] { 47f, 50f, 56.5f })
        {
            Prop(lb, "SM_Prop_Bookcase_01", x, -14.2f, 0f);
            Prop(lb, "SM_Prop_Bookcase_01", x, -14.2f, 0f, 1.49f);
        }
        Prop(lb, "SM_Prop_Table_Round_01", 50.5f, -10.5f, 0f);
        Prop(lb, "SM_Prop_Table_Round_01", 55f, -4f, 0f);
        Prop(lb, "SM_Item_BookPile_01", 50.2f, -10.1f, 0f);
        Prop(lb, "SM_Item_BookPile_01", 55.6f, -3.2f, 0f);

        // Shrine: a stone altar with candles, statues at the west and east ends, chains from the ceiling
        var sh = RoomNamed("Shrine").root;
        Prop(sh, "SM_Prop_StoneTable_01", 36.8f, -4.0f, 0f);
        Prop(sh, "SM_Prop_Candles_01", 36.2f, -4.0f, 0f, 0.83f);
        Prop(sh, "SM_Prop_Candles_01", 37.8f, -4.0f, 0f, 0.83f);
        Prop(sh, "SM_Env_Statue_01", 31.4f, -4.5f, 0f);
        Prop(sh, "SM_Env_Statue_02", 31.4f, -11.5f, 0f);
        Prop(sh, "SM_Env_Statue_02", 43.4f, -4.5f, 0f);
        Prop(sh, "SM_Prop_Candle_Stand_01", 33.5f, -2.0f, 0f);
        Prop(sh, "SM_Prop_Chain_01", 35.0f, -10f, 0f, 7f - 2.58f);
        Prop(sh, "SM_Prop_Chain_01", 41.0f, -6f, 0f, 7f - 2.58f);

        // Alchemist's study: benches of glassware and jars, a stool or two
        var al = RoomNamed("Alchemist's study").root;
        foreach (float z in new[] { -18f, -22.5f, -27f })
            Prop(al, "SM_Prop_Table_01", 56f, z, 90f);
        foreach (float z in new[] { -18f, -22.5f, -27f })
            Prop(al, "SM_Item_Jar_01", 55.5f, z + 0.3f, 0f, 0.96f);
        Prop(al, "SM_Item_Jar_01", 56.4f, -21.8f, 0f, 0.96f);
        Prop(al, "SM_Prop_Bookcase_02", 59.2f, -23f, -90f);
        Prop(al, "SM_Prop_Barrel_03", 47f, -28f, 0f);

        // Throne room: sparse; a torn runner from the door to the stage, tall banners, four round pillars
        var tr = RoomNamed("Throne room").root;
        Prop(tr, "SM_Prop_Rug_02", 37.5f, -18.2f, 90f, 0f);
        Prop(tr, "SM_Env_Pillar_Round_01", 31.5f, -17.5f, 0f);
        Prop(tr, "SM_Env_Pillar_Round_01", 43.5f, -17.5f, 0f);
        Prop(tr, "SM_Prop_Wall_Banner_01", 30.6f, -23f, 90f, 4.5f);
        Prop(tr, "SM_Prop_Wall_Banner_03", 44.4f, -23f, -90f, 4.5f);
        Prop(tr, "SM_Prop_Wall_Banner_04", 30.6f, -19f, 90f, 4.5f);
        Prop(tr, "SM_Prop_Wall_Banner_06", 44.4f, -19f, -90f, 4.5f);
    }

    // ---------- lights ----------

    static void Lights()
    {
        AddLight("Entry hall", new Vector3(4f, 4.2f, 4f), new Color(1f, 0.74f, 0.48f), 2.6f, 12f);
        AddLight("Entry hall", new Vector3(11f, 4.2f, 12f), new Color(1f, 0.74f, 0.48f), 2.6f, 12f);
        AddLight("Armory", new Vector3(19f, 4.2f, 7.5f), new Color(1f, 0.7f, 0.42f), 2.6f, 12f);
        AddLight("Armory", new Vector3(26f, 4.2f, 12f), new Color(1f, 0.7f, 0.42f), 2.4f, 10f);
        AddLight("Dining hall", new Vector3(36f, 6.3f, 3f), new Color(1f, 0.8f, 0.55f), 4f, 16f);
        AddLight("Dining hall", new Vector3(46f, 6.3f, 3f), new Color(1f, 0.8f, 0.55f), 4f, 16f);
        AddLight("Dining hall", new Vector3(56f, 6.3f, 3f), new Color(1f, 0.8f, 0.55f), 4f, 16f);
        AddLight("Dining hall", new Vector3(40f, 3.6f, 13f), new Color(1f, 0.7f, 0.45f), 2.5f, 10f);
        AddLight("Dining hall", new Vector3(52f, 3.6f, 12f), new Color(1f, 0.7f, 0.45f), 2.5f, 10f);
        AddLight("Guard room", new Vector3(4f, 2.6f, 24f), new Color(0.95f, 0.62f, 0.36f), 1.6f, 8f);
        AddLight("Guard room", new Vector3(12f, 2.6f, 27f), new Color(0.95f, 0.62f, 0.36f), 1.6f, 8f);
        AddLight("Prison", new Vector3(20f, 2.4f, 24f), new Color(0.9f, 0.6f, 0.35f), 1.5f, 8f);
        AddLight("Prison", new Vector3(27f, 2.4f, 24f), new Color(0.9f, 0.6f, 0.35f), 1.5f, 8f);
        AddLight("Torture chamber", new Vector3(34.5f, 2.2f, 24.5f), new Color(1f, 0.45f, 0.25f), 1.8f, 8f);
        AddLight("Torture chamber", new Vector3(42f, 2.4f, 25f), new Color(0.85f, 0.7f, 0.6f), 1.5f, 8f);
        AddLight("Crypt", new Vector3(50f, 2.4f, 27f), new Color(0.6f, 0.7f, 0.9f), 1.5f, 8f);
        AddLight("Crypt", new Vector3(56f, 2.4f, 23f), new Color(0.6f, 0.7f, 0.9f), 1.5f, 8f);
        AddLight("Crypt", new Vector3(48f, 2.4f, 26f), new Color(0.6f, 0.7f, 0.9f), 1.5f, 8f);
        AddLight("East passage", new Vector3(62.5f, 3.4f, 10f), new Color(1f, 0.7f, 0.45f), 1.8f, 9f);
        AddLight("East passage", new Vector3(62.5f, 3.4f, 22f), new Color(1f, 0.7f, 0.45f), 1.8f, 9f);
        AddLight("Kitchen", new Vector3(4f, 2.6f, -6f), new Color(1f, 0.7f, 0.45f), 1.6f, 9f);
        AddLight("Kitchen", new Vector3(12f, 2.6f, -11f), new Color(1f, 0.7f, 0.45f), 1.6f, 9f);
        AddLight("Storeroom", new Vector3(18f, 4.2f, -4f), new Color(1f, 0.7f, 0.45f), 2.2f, 10f);
        AddLight("Storeroom", new Vector3(25f, 4.2f, -10f), new Color(1f, 0.7f, 0.45f), 2.2f, 10f);
        AddLight("Storeroom", new Vector3(22f, 4.2f, -4f), new Color(1f, 0.7f, 0.45f), 1.8f, 10f);
        AddLight("Forge", new Vector3(20f, 3.6f, -21f), new Color(1f, 0.5f, 0.2f), 4f, 9f);
        AddLight("Forge", new Vector3(26f, 4.2f, -26f), new Color(1f, 0.65f, 0.4f), 2f, 9f);
        AddLight("Forge", new Vector3(17f, 4.2f, -17f), new Color(1f, 0.65f, 0.4f), 1.8f, 9f);
        AddLight("Treasure vault", new Vector3(5f, 2.2f, -22.5f), new Color(1f, 0.8f, 0.5f), 1.4f, 8f);
        AddLight("Treasure vault", new Vector3(11f, 2.2f, -26f), new Color(1f, 0.8f, 0.5f), 1.4f, 8f);
        AddLight("Treasure vault", new Vector3(12f, 2.2f, -18f), new Color(1f, 0.8f, 0.5f), 1.4f, 8f);
        AddLight("Library", new Vector3(48f, 5.8f, -4f), new Color(1f, 0.78f, 0.5f), 2.4f, 12f);
        AddLight("Library", new Vector3(56f, 5.8f, -4f), new Color(1f, 0.78f, 0.5f), 2.4f, 12f);
        AddLight("Library", new Vector3(48f, 5.8f, -11f), new Color(1f, 0.78f, 0.5f), 2.4f, 12f);
        AddLight("Library", new Vector3(56f, 5.8f, -11f), new Color(1f, 0.78f, 0.5f), 2.4f, 12f);
        AddLight("Shrine", new Vector3(37.5f, 6f, -6f), new Color(0.55f, 0.9f, 0.75f), 2.2f, 12f);
        AddLight("Shrine", new Vector3(37.5f, 6f, -12f), new Color(0.55f, 0.9f, 0.75f), 2.2f, 12f);
        AddLight("Shrine", new Vector3(37.5f, 1.4f, -4f), new Color(1f, 0.7f, 0.4f), 1.2f, 4f);
        AddLight("Alchemist's study", new Vector3(50f, 4.2f, -18f), new Color(0.7f, 0.95f, 0.75f), 2f, 10f);
        AddLight("Alchemist's study", new Vector3(54f, 4.2f, -23f), new Color(0.7f, 0.95f, 0.75f), 2f, 10f);
        AddLight("Alchemist's study", new Vector3(50f, 4.2f, -28f), new Color(0.7f, 0.95f, 0.75f), 2f, 10f);
        AddLight("Throne room", new Vector3(37.5f, 9f, -25f), new Color(1f, 0.82f, 0.45f), 6f, 22f);
        AddLight("Throne room", new Vector3(33f, 6.5f, -18f), new Color(1f, 0.8f, 0.5f), 3f, 14f);
        AddLight("Throne room", new Vector3(42f, 6.5f, -18f), new Color(1f, 0.8f, 0.5f), 3f, 14f);
    }

    static void AddLight(string room, Vector3 at, Color c, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(RoomNamed(room).root, false);
        go.transform.position = at;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity * LightGain;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    // ---------- markers: defenders and pickups ----------

    static void Markers()
    {
        // Armory: ranged behind the guarded entrance, facing the entry hall (west); out of sight from the hall except at the doorway
        Enemy("Armory", "ranged", 16.8f, 4.0f, -90f);
        Enemy("Armory", "ranged", 16.8f, 11.0f, -90f);
        Enemy("Armory", "ranged", 16.8f, 13.5f, -90f);
        Enemy("Armory", "melee", 21.0f, 9.0f, 0f);
        Enemy("Armory", "melee", 23.5f, 7.0f, 0f);
        Enemy("Armory", "melee", 23.0f, 12.0f, 0f);
        Enemy("Armory", "melee", 25.0f, 9.5f, 0f);
        Pickup("Armory", "best gear", 27.5f, 9.8f);

        // Dining hall: ranged on the gallery facing the rail, melee on the floor and under the gallery
        Enemy("Dining hall", "ranged", 36f, 17f, 180f, DeckTop);
        Enemy("Dining hall", "ranged", 42f, 17f, 180f, DeckTop);
        Enemy("Dining hall", "ranged", 48f, 17f, 180f, DeckTop);
        Enemy("Dining hall", "melee", 33f, 3.0f, 0f);
        Enemy("Dining hall", "melee", 38f, 3.5f, 0f);
        Enemy("Dining hall", "melee", 44f, 4.2f, 0f);
        Enemy("Dining hall", "melee", 50f, 3.0f, 0f);
        Enemy("Dining hall", "melee", 56f, 3.8f, 0f);
        Enemy("Dining hall", "melee", 40f, 11.0f, 0f);
        Enemy("Dining hall", "melee", 35f, 15.0f, 0f);

        // Throne room: the warlord on the stage, guards on the floor either side of the approach
        Enemy("Throne room", "warlord", 33.8f, -26.6f, 90f, 1f);
        Enemy("Throne room", "melee", 33.5f, -20.5f, 0f);
        Enemy("Throne room", "melee", 41.5f, -20.5f, 0f);
        Enemy("Throne room", "melee", 33.5f, -17.5f, 0f);
        Enemy("Throne room", "melee", 41.5f, -17.5f, 0f);

        // the rest: whoever belongs in them
        Enemy("Guard room", "melee", 4.5f, 27.5f, 0f);
        Enemy("Prison", "melee", 21.5f, 25.0f, 0f);
        Enemy("Prison", "melee", 25.5f, 25.0f, 0f);
        Enemy("Torture chamber", "melee", 36.5f, 23.5f, 0f);
        Enemy("Torture chamber", "melee", 41.5f, 25.5f, 0f);
        Enemy("Crypt", "melee", 50.5f, 25.5f, 0f);
        Enemy("Crypt", "melee", 55.5f, 25.0f, 0f);
        Enemy("Kitchen", "melee", 12.5f, -8.5f, 0f);
        Enemy("Forge", "melee", 25.5f, -26f, 0f);

        Pickup("Treasure vault", "hoard", 7.5f, -22.5f);
    }

    static void Enemy(string room, string kind, float x, float z, float yaw, float y = 0f)
    {
        var r = RoomNamed(room);
        var go = new GameObject("Enemy " + kind);
        go.transform.SetParent(r.root, false);
        go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0));
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0, 0.9f, 0);
        body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = MarkerMat(kind == "melee" ? "melee" : kind == "ranged" ? "ranged" : "warlord", kind == "melee" ? new Color(0.8f, 0.15f, 0.12f) : kind == "ranged" ? new Color(0.2f, 0.45f, 1f) : new Color(0.7f, 0.2f, 1f));
        var m = go.AddComponent<LevelMarker>();
        m.markerKind = "enemy"; m.enemyKind = kind; m.room = room;
    }

    static void Pickup(string room, string kind, float x, float z)
    {
        var r = RoomNamed(room);
        var go = new GameObject("Pickup " + kind);
        go.transform.SetParent(r.root, false);
        go.transform.position = new Vector3(x, 0, z);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Body";
        cube.transform.SetParent(go.transform, false);
        cube.transform.localPosition = new Vector3(0, 1.1f, 0);
        cube.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = MarkerMat("pickup", new Color(1f, 0.85f, 0.2f));
        var m = go.AddComponent<LevelMarker>();
        m.markerKind = "pickup"; m.pickupKind = kind; m.room = room;
    }

    // ---------- the player and the HUD ----------

    static void Player()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        if (prefab == null) throw new Exception("no " + PlayerPath);
        var p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        p.transform.SetPositionAndRotation(new Vector3(4f, 0f, 7.5f), Quaternion.Euler(0f, 90f, 0f));
        var cam = p.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data != null) data.renderPostProcessing = true;
        }
    }

    // ---------- helpers ----------

    static Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }

    static GameObject Find(string name)
    {
        if (Prefabs.TryGetValue(name, out var cached)) return cached;
        GameObject found = null;
        foreach (var folder in new[] { DungeonFolder, RealmsFolder })
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    break;
                }
            }
            if (found != null) break;
        }
        if (found == null) throw new Exception("no prefab named " + name);
        Prefabs[name] = found;
        return found;
    }

    static GameObject Put(string prefab, Vector3 pos, float yaw, Transform parent, string name, int layer = -1)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Find(prefab));
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        if (parent != null) go.transform.SetParent(parent, true);
        if (name != null) go.name = name;
        if (layer >= 0) SetLayer(go.transform, layer);
        return go;
    }

    // a wall-like piece: its local +x runs from a towards b, its front faces the given way
    static GameObject Piece(string prefab, Vector3 a, Vector3 b, Vector3 front, float y, float sy, Transform parent, string name = null)
    {
        Vector3 f = Flat(front);
        Vector3 u = Vector3.Cross(Vector3.up, f);
        Vector3 d = Flat(b - a);
        Vector3 origin = Vector3.Dot(u, d) > 0 ? b : a;
        origin.y = y;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Find(prefab));
        go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(f, Vector3.up));
        go.transform.localScale = new Vector3(1f, sy, 1f);
        if (parent != null) go.transform.SetParent(parent, true);
        if (name != null) go.name = name;
        return go;
    }

    // a piece placed by its bounds: its footprint centred on (x, z), its base on the bottom height
    static GameObject Prop(Transform parent, string prefab, float x, float z, float yaw, float bottom = 0f, Vector3? scale = null, string name = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Find(prefab));
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0));
        if (scale.HasValue) go.transform.localScale = scale.Value;
        Bounds b = WorldBounds(go);
        go.transform.position = new Vector3(x - b.center.x, bottom - b.min.y, z - b.center.z);
        if (parent != null) go.transform.SetParent(parent, true);
        if (name != null) go.name = name;
        return go;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static Bounds LocalBounds(string prefab)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Find(prefab));
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var b = WorldBounds(go);
        UnityEngine.Object.DestroyImmediate(go);
        return b;
    }

    static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform c in t) SetLayer(c, layer);
    }

    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tags = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
        var so = new SerializedObject(tags);
        var layers = so.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(p.stringValue)) continue;
            p.stringValue = name;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return i;
        }
        throw new Exception("no free layer for " + name);
    }

    static Material MarkerMat(string key, Color c)
    {
        if (Mats.TryGetValue(key, out var m)) return m;
        Directory.CreateDirectory(MarkerFolder);
        string path = MarkerFolder + "/" + key + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
            m.SetColor("_BaseColor", c);
            AssetDatabase.CreateAsset(m, path);
        }
        Mats[key] = m;
        return m;
    }
}
