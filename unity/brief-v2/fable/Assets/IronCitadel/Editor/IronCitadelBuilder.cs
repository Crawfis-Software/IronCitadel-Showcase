using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronCitadel.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/IronCitadel.unity from scratch out of the Synty POLYGON dungeon packs' prefabs.
    /// Deterministic: run it again after changing this file and the scene is rebuilt.
    ///   Menu: Tools/Iron Citadel/Build level     or     IronCitadel.EditorTools.IronCitadelBuilder.Build()
    /// </summary>
    public static partial class IronCitadelBuilder
    {
        public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
        public const float FLOOR = 0.1f;     // top of the 0.10 m thick floor tiles
        public const int CeilingLayer = 6;   // "Ceilings": hidden by the M overview camera
        public const int MapOnlyLayer = 7;   // "MapOnly": labels and the player marker, seen only by the overview camera
        const float LOW = 3f, STD = 5f, TALL = 10f, THRONE = 15f;

        // prefab roots
        const string PD = "Assets/Synty/PolygonDungeon/Prefabs/";
        const string PR = "Assets/Synty/PolygonDungeonRealms/Prefabs/";
        const string PM = "Assets/Synty/PolygonDungeonMap/Prefabs/";
        const string PG = "Assets/Synty/PolygonGeneric/Prefabs/";
        static string W(string n) => PD + "Environments/Walls/" + n + ".prefab";
        static string Fl(string n) => PD + "Environments/Floors/" + n + ".prefab";
        static string Mi(string n) => PD + "Environments/Misc/" + n + ".prefab";
        static string Pi(string n) => PD + "Environments/Pillars/" + n + ".prefab";
        static string Bo(string n) => PD + "Environments/Bones/" + n + ".prefab";
        static string Ro(string n) => PD + "Environments/Rocks/" + n + ".prefab";
        static string Wo(string n) => PD + "Environments/Wood/" + n + ".prefab";
        static string Pr(string n) => PD + "Props/" + n + ".prefab";
        static string It(string n) => PD + "Items/" + n + ".prefab";
        static string Wp(string n) => PD + "Weapons/" + n + ".prefab";
        static string Fx(string n) => PD + "FX/" + n + ".prefab";
        static string Rp(string n) => PR + "Props/" + n + ".prefab";
        static string Re(string n) => PR + "Environments/" + n + ".prefab";
        static string Rfx(string n) => PR + "FX/" + n + ".prefab";
        static string Mp(string n) => PM + n + ".prefab";
        static string Gp(string n) => PG + "Props/" + n + ".prefab";

        public class Room
        {
            public string name, kind, state;
            public float x0, x1, z0, z1, h;     // floor extents; h = ceiling underside above FLOOR (nominal)
            public float floorY = FLOOR;
            public bool isGallery; public string parent;
            public bool noWalls;                 // the gallery: a deck inside the hall
            public string[] tiles = DefaultTiles;
            public Transform root, floor, walls, ceil, props, lights, markers;
            public Dictionary<string, string> spec = new Dictionary<string, string>(); // "S:2:0" -> piece spec
            public float Cx => (x0 + x1) / 2f; public float Cz => (z0 + z1) / 2f;
            public IronRoom comp;
        }
        static readonly string[] DefaultTiles = { "SM_Env_Tiles_03", "SM_Env_Tiles_04", "SM_Env_Tiles_06", "SM_Env_Tiles_08", "SM_Env_Tiles_010" };

        static readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        static readonly List<string> missing = new List<string>();
        static System.Random rng;
        static Transform levelRoot, doorsRoot, markersRoot;
        static Material matRanged, matMelee, matWarlord, matGear, matHoard, matFacing;

        [MenuItem("Tools/Iron Citadel/Build level")]
        public static void Build()
        {
            var t0 = DateTime.Now;
            rooms.Clear(); missing.Clear(); rng = new System.Random(7);
            EnsureLayers();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            levelRoot = new GameObject("Level").transform;
            doorsRoot = new GameObject("Doors").transform; doorsRoot.SetParent(levelRoot);
            markersRoot = new GameObject("Markers").transform; markersRoot.SetParent(levelRoot);
            MakeMaterials();

            DefineRooms();
            DefineDoors();
            foreach (var r in rooms.Values) BuildRoomShell(r);
            BuildDoorPieces();
            BuildGalleryAndStairs();
            BuildStage();
            DressAll();
            PlaceMarkers();
            PlacePlayerAndSystems();
            SetupLightingAndPost();

            foreach (var r in rooms.Values) r.comp.Refresh();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            string msg = "[IronCitadelBuilder] built " + ScenePath + " in " + (DateTime.Now - t0).TotalSeconds.ToString("F1") + " s; rooms " + rooms.Count + "; missing prefabs: " + (missing.Count == 0 ? "none" : string.Join(", ", missing));
            Debug.Log(msg);
            File.WriteAllText("Tools/build_result.txt", msg + "\n");
        }

        // ---------------------------------------------------------------- layout
        static Room R(string name, string kind, string state, float x0, float x1, float z0, float z1, float h)
        {
            var r = new Room { name = name, kind = kind, state = state, x0 = x0, x1 = x1, z0 = z0, z1 = z1, h = h };
            rooms[name] = r; return r;
        }

        static void DefineRooms()
        {
            R("Entry hall", "entry hall", "decayed", -15, 0, -10, 0, STD);
            R("Armory", "armory", "stocked", -15, 0, 0, 15, STD);
            R("Forge", "forge", "decayed", -15, 0, 15, 25, STD);
            R("Guard room", "guard room", "looted", -25, -15, -10, 0, LOW);
            R("Prison", "prison", "looted", -25, -15, 0, 15, LOW);
            R("Torture chamber", "torture chamber", "decayed", -25, -15, 15, 25, LOW);
            R("Crypt", "crypt", "decayed", -30, -15, 25, 35, LOW);
            R("Treasure vault", "treasure vault", "untouched", -35, -30, 25, 30, LOW);
            R("Stair hall", "extra", "decayed", -15, 0, 25, 35, TALL);
            R("Dining hall", "dining hall", "decayed", 0, 20, 0, 35, TALL);
            var g = R("Gallery", "extra", "decayed", 0, 5, 0, 35, TALL);
            g.isGallery = true; g.parent = "Dining hall"; g.noWalls = true; g.floorY = 5f + FLOOR;
            R("Kitchen", "kitchen", "looted", 20, 30, 0, 10, LOW);
            R("Storeroom", "storeroom", "looted", 30, 40, 0, 10, STD);
            R("Library", "library", "decayed", 20, 35, 10, 25, STD);
            R("Alchemist's study", "alchemist's study", "looted", 35, 45, 10, 20, STD);
            R("Shrine", "shrine", "decayed", 35, 45, 20, 30, TALL);
            R("Antechamber", "extra", "decayed", 25, 35, 25, 30, STD);
            R("Throne room", "throne room", "decayed", 20, 40, 30, 55, THRONE);
            rooms["Dining hall"].tiles = new[] { "SM_Env_Tiles_03", "SM_Env_Tiles_04", "SM_Env_Tiles_08" };
            rooms["Throne room"].tiles = new[] { "SM_Env_Tiles_04", "SM_Env_Tiles_08", "SM_Env_Tiles_Ornate_01" };
            rooms["Shrine"].tiles = new[] { "SM_Env_Tiles_Ornate_01", "SM_Env_Tiles_04" };
            rooms["Treasure vault"].tiles = new[] { "SM_Env_Tiles_Ornate_01" };
        }

        public class DoorDef { public string a, b, type, state, leaf; public Vector3 pos; public float yaw; public int tier; public string name; public bool leafOpen; public float leafAngle; }
        static readonly List<DoorDef> doors = new List<DoorDef>();

        /// <summary>type: door (frame + optional leaf), double (open double doorway), round (open single doorway), large (grand frame), secret (bricked-up doorway that sinks on E).</summary>
        static void Door(string a, string b, float x, float z, string type, string state, string leaf = null, int tier = 0, bool leafOpen = false, float leafAngle = 0f)
        {
            doors.Add(new DoorDef { a = a, b = b, pos = new Vector3(x, FLOOR + tier * 5f, z), type = type, state = state, leaf = leaf, tier = tier, leafOpen = leafOpen, leafAngle = leafAngle, name = "Door " + a + " - " + b });
        }

        static void DefineDoors()
        {
            doors.Clear();
            Door("Entry hall", "Armory", -7.5f, 0f, "large", "open");           // the guarded entrance
            Door("Entry hall", "Guard room", -15f, -7.5f, "door", "shut", "SM_Env_Door_03");
            Door("Guard room", "Prison", -22.5f, 0f, "door", "open", "SM_Env_Door_Bars_01", 0, true, 125f);
            Door("Prison", "Torture chamber", -17.5f, 15f, "door", "shut", "SM_Env_Door_04");
            Door("Torture chamber", "Crypt", -22.5f, 25f, "round", "open");
            Door("Crypt", "Treasure vault", -30f, 27.5f, "secret", "hidden");
            Door("Crypt", "Stair hall", -15f, 27.5f, "door", "open");
            Door("Stair hall", "Dining hall", 0f, 27.5f, "double", "open");       // the way round comes into the hall, beneath the gallery
            Door("Stair hall", "Gallery", 0f, 32.5f, "door", "open", null, 1);     // upper doorway onto the gallery deck
            Door("Forge", "Stair hall", -7.5f, 25f, "door", "shut", "SM_Env_Door_05");   // the forge opens off the stair hall, so the armory keeps exactly two ways in
            Door("Armory", "Dining hall", 0f, 7.5f, "door", "shut", "SM_Env_Door_04"); // the armory's back door, beneath the gallery
            Door("Dining hall", "Kitchen", 20f, 7.5f, "door", "shut", "SM_Env_Door_03");
            Door("Kitchen", "Storeroom", 30f, 2.5f, "door", "open");
            Door("Dining hall", "Library", 20f, 17.5f, "double", "open");
            Door("Library", "Alchemist's study", 35f, 17.5f, "door", "shut", "SM_Env_Door_03");
            Door("Alchemist's study", "Shrine", 37.5f, 20f, "round", "open");
            Door("Shrine", "Antechamber", 35f, 27.5f, "door", "shut", "SM_Env_Door_05");
            Door("Antechamber", "Throne room", 30f, 30f, "large", "open");

            // burial niches in the crypt's north wall (the void lies behind it)
            rooms["Crypt"].spec["N:0:0"] = "alcove"; rooms["Crypt"].spec["N:1:0"] = "alcove"; rooms["Crypt"].spec["N:2:0"] = "alcove";

            // mark the wall segments each door touches
            foreach (var d in doors)
            {
                foreach (var rn in new[] { d.a, d.b })
                {
                    var r = rooms[rn];
                    if (r.noWalls) r = rooms[r.parent]; // the gallery's walls belong to the hall it sits in
                    if (!Locate(r, d.pos, out string side, out int seg, out float along))
                        throw new Exception("door " + d.name + " is not on a wall of " + rn);
                    bool mid = Mathf.Abs(along - 2.5f) < 0.01f;
                    if (d.type == "large" && !mid)
                    {
                        // the frame straddles the boundary between segment seg-1 and seg: half walls at the outer ends of both
                        if (along > 0.01f) throw new Exception("door " + d.name + " must sit at a segment centre or boundary");
                        r.spec[Key(side, seg - 1, d.tier)] = "half_lo";
                        r.spec[Key(side, seg, d.tier)] = "half_hi";
                    }
                    else if (d.type == "large") r.spec[Key(side, seg, d.tier)] = "none";
                    else if (!mid) throw new Exception("door " + d.name + " is not at a segment centre");
                    else r.spec[Key(side, seg, d.tier)] = d.type == "secret" ? "door" : d.type;
                }
            }
        }

        static string Key(string side, int seg, int tier) => side + ":" + seg + ":" + tier;

        /// <summary>Which side of the room a point on its boundary lies on, the 5 m segment index, and the distance along that segment.</summary>
        static bool Locate(Room r, Vector3 p, out string side, out int seg, out float along)
        {
            side = null; seg = -1; along = 0;
            const float e = 0.01f;
            if (Mathf.Abs(p.z - r.z0) < e && p.x >= r.x0 - e && p.x <= r.x1 + e) { side = "S"; seg = Mathf.FloorToInt((p.x - r.x0 + e) / 5f); along = p.x - r.x0 - seg * 5f; return true; }
            if (Mathf.Abs(p.z - r.z1) < e && p.x >= r.x0 - e && p.x <= r.x1 + e) { side = "N"; seg = Mathf.FloorToInt((p.x - r.x0 + e) / 5f); along = p.x - r.x0 - seg * 5f; return true; }
            if (Mathf.Abs(p.x - r.x0) < e && p.z >= r.z0 - e && p.z <= r.z1 + e) { side = "W"; seg = Mathf.FloorToInt((p.z - r.z0 + e) / 5f); along = p.z - r.z0 - seg * 5f; return true; }
            if (Mathf.Abs(p.x - r.x1) < e && p.z >= r.z0 - e && p.z <= r.z1 + e) { side = "E"; seg = Mathf.FloorToInt((p.z - r.z0 + e) / 5f); along = p.z - r.z0 - seg * 5f; return true; }
            return false;
        }

        // ---------------------------------------------------------------- shells
        static void BuildRoomShell(Room r)
        {
            r.root = new GameObject("Room " + r.name).transform; r.root.SetParent(levelRoot);
            r.floor = Child(r.root, "Floor"); r.walls = Child(r.root, "Walls"); r.ceil = Child(r.root, "Ceiling"); r.props = Child(r.root, "Props"); r.lights = Child(r.root, "Lights"); r.markers = Child(r.root, "Markers");
            r.comp = r.root.gameObject.AddComponent<IronRoom>();
            r.comp.roomName = r.name; r.comp.kind = r.kind; r.comp.state = r.state; r.comp.floorY = r.floorY; r.comp.ceilingY = r.floorY - FLOOR + r.h; r.comp.isGallery = r.isGallery; r.comp.parentRoom = r.parent;

            int nx = Mathf.RoundToInt((r.x1 - r.x0) / 5f), nz = Mathf.RoundToInt((r.z1 - r.z0) / 5f);
            float baseY = r.floorY - FLOOR;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float cx = r.x0 + i * 5f + 2.5f, cz = r.z0 + j * 5f + 2.5f;
                    string tile = r.tiles[(i * 7 + j * 3 + r.name.Length) % r.tiles.Length];
                    if (r.name == "Throne room") tile = Mathf.Abs(cx - 30f) < 0.1f ? "SM_Env_Tiles_Ornate_01" : r.tiles[(i + j) % 2];
                    if (r.name == "Dining hall" && cx > 5f && cx < 15f && cz > 5f && cz < 30f) tile = "SM_Env_Tiles_04";
                    // floor tiles: origin at the +x,-z corner of the 5x5 (spans x-5..0, z 0..5)
                    Place(Fl(tile), new Vector3(cx + 2.5f, baseY, cz - 2.5f), 0f, r.floor, name: "Tile");
                    if (!r.isGallery)
                    {
                        string cp = new[] { "SM_Env_Ceiling_Stone_Flat_01", "SM_Env_Ceiling_Stone_Flat_02", "SM_Env_Ceiling_Stone_Flat_03" }[(i * 5 + j * 11) % 3];
                        var c = Place(W(cp), new Vector3(cx + 2.5f, baseY + r.h + 0.15f, cz - 2.5f), 0f, r.ceil, name: "Ceiling");
                        SetLayerRecursive(c, CeilingLayer);
                    }
                }
            if (r.noWalls) return;
            int tiers = Mathf.CeilToInt(r.h / 5f - 0.01f);
            for (int tier = 0; tier < tiers; tier++)
            {
                float y = baseY + tier * 5f;
                BuildSide(r, "S", nx, tier, y); BuildSide(r, "N", nx, tier, y); BuildSide(r, "W", nz, tier, y); BuildSide(r, "E", nz, tier, y);
            }
        }

        /// <summary>Geometry of a side: low corner p_lo, unit v from low to high, facing yaw (front toward the room), and whether the piece's local +x (u) runs with v.</summary>
        static void Side(Room r, string side, out Vector3 pLo, out Vector3 v, out float yaw)
        {
            switch (side)
            {
                case "S": pLo = new Vector3(r.x0, 0, r.z0); v = Vector3.right; yaw = 0f; break;        // faces +z
                case "N": pLo = new Vector3(r.x0, 0, r.z1); v = Vector3.right; yaw = 180f; break;      // faces -z
                case "W": pLo = new Vector3(r.x0, 0, r.z0); v = Vector3.forward; yaw = 90f; break;     // faces +x
                default: pLo = new Vector3(r.x1, 0, r.z0); v = Vector3.forward; yaw = -90f; break;     // faces -x
            }
        }
        static Vector3 U(float yaw) => new Vector3(Mathf.Cos(yaw * Mathf.Deg2Rad), 0, -Mathf.Sin(yaw * Mathf.Deg2Rad));

        /// <summary>Origin for a piece of width w whose local x spans [-w,0], covering [t0, t0+w] along the segment from pLo.</summary>
        static Vector3 PieceOrigin(Vector3 pLo, Vector3 v, float yaw, float t0, float w)
        {
            bool same = Vector3.Dot(U(yaw), v) > 0f;
            return same ? pLo + v * (t0 + w) : pLo + v * t0;
        }

        static void BuildSide(Room r, string side, int n, int tier, float y)
        {
            Side(r, side, out var pLo, out var v, out var yaw);
            for (int s = 0; s < n; s++)
            {
                string spec = r.spec.TryGetValue(Key(side, s, tier), out var sp) ? sp : "plain";
                if (tier > 0 && spec != "door" && !r.spec.ContainsKey(Key(side, s, tier))) spec = "plain";
                var segLo = pLo + v * (s * 5f);
                string piece = null; float w = 5f, t0 = 0f;
                switch (spec)
                {
                    case "plain": piece = PlainWall(r, side, s, tier); break;
                    case "door": piece = "SM_Env_Wall_DoorFrame_01"; break;
                    case "round": piece = "SM_Env_Wall_DoorFrame_Round_01"; break;
                    case "double": piece = "SM_Env_Wall_DoorFrame_Double_01"; break;
                    case "alcove": piece = "SM_Env_Wall_Alcove_Small_01"; break;
                    case "window": piece = "SM_Env_Wall_Window_01"; break;
                    case "none": continue;
                    case "half_lo": piece = "SM_Env_Wall_Half_01"; w = 2.55f; t0 = 0f; break;
                    case "half_hi": piece = "SM_Env_Wall_Half_01"; w = 2.55f; t0 = 2.45f; break;
                }
                var o = PieceOrigin(segLo, v, yaw, t0, w);
                Place(W(piece), new Vector3(o.x, y, o.z), yaw, r.walls, name: side + s + "t" + tier + " " + spec);
            }
        }

        static string PlainWall(Room r, string side, int s, int tier)
        {
            int h = (r.name.GetHashCode() & 0x7fff) + s * 31 + tier * 7 + side[0];
            if (r.kind == "dining hall" || r.kind == "throne room" || r.kind == "shrine" || r.name == "Antechamber")
                return (h % 5 == 0) ? "SM_Env_Wall_03" : (h % 7 == 0 ? "SM_Env_Wall_04" : "SM_Env_Wall_01");
            if (r.kind == "crypt" || r.kind == "prison" || r.kind == "torture chamber")
                return (h % 3 == 0) ? "SM_Env_Wall_05" : (h % 4 == 0 ? "SM_Env_Wall_06" : "SM_Env_Wall_01_Alt");
            return (h % 4 == 0) ? "SM_Env_Wall_03" : (h % 6 == 0 ? "SM_Env_Wall_01_Alt" : "SM_Env_Wall_01");
        }

        // ---------------------------------------------------------------- doors
        static void BuildDoorPieces()
        {
            foreach (var d in doors)
            {
                var ra = rooms[d.a]; var rb = rooms[d.b];
                // direction across the wall from A to B
                Vector3 n = new Vector3(rb.Cx - ra.Cx, 0, rb.Cz - ra.Cz);
                if (d.a == "Stair hall" && d.b == "Gallery") n = Vector3.right;
                n = Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector3(Mathf.Sign(n.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(n.z));
                var go = new GameObject(d.name); go.transform.SetParent(doorsRoot);
                go.transform.position = d.pos; go.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
                var comp = go.AddComponent<IronDoor>();
                comp.roomA = d.a; comp.roomB = d.b; comp.state = d.state;
                Vector3 u = Vector3.Cross(Vector3.up, n).normalized; // along the wall
                float wallYaw = Mathf.Atan2(u.x, u.z) * Mathf.Rad2Deg; // yaw whose local +x maps to u: local +x -> (cos, -sin); solve
                wallYaw = Mathf.Atan2(-u.z, u.x) * Mathf.Rad2Deg;
                switch (d.type)
                {
                    case "door":
                        comp.nominalWidth = 1.54f; comp.piece = "SM_Env_Wall_DoorFrame_01";
                        if (d.leaf != null)
                        {
                            // leaf: hinge at local x=0, panel toward -x (1.33 wide). Hinge at +0.69u so the panel sits centred in the 1.54 opening.
                            var hinge = new GameObject("Leaf " + d.leaf); hinge.transform.SetParent(go.transform);
                            hinge.transform.position = d.pos + u * 0.69f; hinge.transform.rotation = Quaternion.Euler(0, wallYaw, 0);
                            var leaf = Place(W(d.leaf), hinge.transform.position, wallYaw, hinge.transform, name: "LeafMesh");
                            var di = hinge.AddComponent<DoorInteract>();
                            di.open = d.leafOpen; di.openAngle = d.leafOpen ? d.leafAngle : 100f;
                            if (d.leafOpen) hinge.transform.rotation = Quaternion.Euler(0, wallYaw + d.leafAngle, 0);
                        }
                        break;
                    case "round": comp.nominalWidth = 1.54f; comp.piece = "SM_Env_Wall_DoorFrame_Round_01"; break;
                    case "double": comp.nominalWidth = 2.18f; comp.piece = "SM_Env_Wall_DoorFrame_Double_01"; break;
                    case "large":
                        {
                            comp.nominalWidth = 4.0f; comp.piece = "SM_Env_Door_Large_Frame_02 x0.9";
                            var f = Place(W("SM_Env_Door_Large_Frame_02"), d.pos - new Vector3(0, FLOOR, 0), wallYaw, go.transform, new Vector3(0.9f, 0.9f, 0.9f), "LargeFrame");
                            if (d.a == "Entry hall")
                            {
                                // the great doors stand swung open into the armory
                                Place(W("SM_Env_Door_Large_Wood_01"), d.pos + u * 2.05f, wallYaw + 115f, go.transform, new Vector3(0.9f, 0.9f, 0.9f), "GreatDoorL");
                                Place(W("SM_Env_Door_Large_Wood_02"), d.pos - u * 2.05f, wallYaw - 115f, go.transform, new Vector3(0.9f, 0.9f, 0.9f), "GreatDoorR");
                            }
                        }
                        break;
                    case "secret":
                        {
                            comp.nominalWidth = 1.54f; comp.piece = "SM_Env_Wall_DoorFrame_01 + SM_Env_Wall_Quarter_01 plug";
                            // a bricked-up doorway: a quarter wall scaled to fill the 1.54 x 2.19 opening; sinks into the floor on E
                            var plug = new GameObject("SecretPlug"); plug.transform.SetParent(go.transform);
                            plug.transform.position = d.pos + u * 0.80f - new Vector3(0, FLOOR, 0); plug.transform.rotation = Quaternion.Euler(0, wallYaw, 0);
                            // the quarter wall is single-sided: one facing each room, back to back, so the bricked-up doorway reads from both sides.
                            // Scaled a little wider than the 1.54 m opening so it overlaps the jambs and no light leaks round it.
                            // full height (2.7 m): the part above the 2.19 m lintel sits inside the solid wall, so the arched top of the opening is filled too
                            Place(W("SM_Env_Wall_Quarter_01"), plug.transform.position, wallYaw, plug.transform, new Vector3(0.64f, 1.0f, 1.0f), "PlugMeshA");
                            Place(W("SM_Env_Wall_Quarter_01"), plug.transform.position - u * 1.56f, wallYaw + 180f, plug.transform, new Vector3(0.64f, 1.0f, 1.0f), "PlugMeshB");
                            var sd = plug.AddComponent<SecretDoor>(); sd.slideDistance = 2.95f; sd.duration = 2.2f;
                        }
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- gallery, stairs, stage
        static void BuildGalleryAndStairs()
        {
            var g = rooms["Gallery"]; var d = rooms["Dining hall"]; var s = rooms["Stair hall"];
            var deckRoot = Child(g.root, "Deck");
            // the deck's underside: ceiling slabs hanging below the deck tiles (seen from the hall floor beneath)
            for (int j = 0; j < 7; j++)
            {
                var c = Place(W("SM_Env_Ceiling_Stone_Flat_01"), new Vector3(5f, 5f, j * 5f), 0f, deckRoot, name: "DeckUnderside");
            }
            // railing along the deck's edge over the hall floor (x = 5), 7 pieces of 5 m
            var railRoot = Child(g.root, "Rail");
            for (int j = 0; j < 7; j++)
                Place(Mi(j == 3 ? "SM_Env_Railing_Broken_01" : "SM_Env_Railing_01"), new Vector3(5f, 5f + FLOOR, j * 5f), 90f, railRoot, name: "Rail");
            // pillars under the deck edge
            var pil = Child(g.root, "Pillars");
            for (int j = 1; j < 7; j++)
                Place(Pi("SM_Env_Pillar_Square_06"), new Vector3(4.6f, FLOOR, j * 5f), 0f, pil, new Vector3(1f, 0.985f, 1f), "DeckPillar");
            // stairs in the stair hall, along its north side (z 30..35): flight 1 (x -14..-9) rises 2.5 m toward +x, a 4 m landing (x -9..-5),
            // flight 2 (x -5..0) rises to 5 m at the upper doorway. SM_Env_Stairs_02 has a ramp collider (2.5 m over 5 m). The foot of flight 1
            // is reached from the flat strip x -15..-14 beside it, which opens onto the lower passage.
            var st = Child(s.root, "Stairs");
            Place(Fl("SM_Env_Stairs_02"), new Vector3(-14f, FLOOR, 30f), -90f, st, name: "Flight1");
            Place(Fl("SM_Env_Tiles_04"), new Vector3(-5f, FLOOR + 2.4f, 30f), 0f, st, new Vector3(0.81f, 1f, 1f), "Landing");
            Place(Fl("SM_Env_Stairs_02"), new Vector3(-5f, FLOOR + 2.5f, 30f), -90f, st, name: "Flight2");
            // skirt walls under the landing and the upper flight along the passage side (the stair blocks are hollow beneath)
            for (int i = 0; i < 5; i++)
                Place(W("SM_Env_Wall_Quarter_01"), new Vector3(-9.05f + i * 1.632f, FLOOR - 0.1f, 29.9f), 180f, st, new Vector3(0.6f, 0.9f, 1f), "StairSkirt");
            Place(W("SM_Env_Wall_Quarter_01"), new Vector3(-0.89f, FLOOR - 0.1f, 29.9f), 180f, st, new Vector3(0.33f, 0.9f, 1f), "StairSkirt");
            // the sloped railing rises toward its local -x; at yaw 180 it spans O.x..O.x+5 rising toward +x like the flights
            Place(Mi("SM_Env_Railing_Angle_01"), new Vector3(-13.5f, FLOOR, 29.85f), 180f, st, name: "StairRail1"); // starts 0.5 m in so the foot is reached from the strip by the wall
            Place(Mi("SM_Env_Railing_01"), new Vector3(-5f, FLOOR + 2.5f, 29.85f), 0f, st, new Vector3(0.81f, 1f, 1f), "LandingRail");
            Place(Mi("SM_Env_Railing_Angle_01"), new Vector3(-5f, FLOOR + 2.5f, 29.85f), 180f, st, name: "StairRail2");
        }

        static void BuildStage()
        {
            var t = rooms["Throne room"];
            var stage = Child(t.root, "Stage");
            // three stepped blocks (1 m rise): flat top against the north wall, steps descending toward the hall
            for (int i = 0; i < 3; i++)
                Place(Fl("SM_Env_Steps_01"), new Vector3(22.5f + i * 5f, FLOOR, 55f), 180f, stage, name: "StageSteps");
            var plinth = Place(Pi("SM_Env_Pillar_Square_Base_01"), new Vector3(30f, FLOOR + 1f, 54.1f), 0f, stage, new Vector3(0.6f, 0.5f, 0.5f), "ThronePlinth");
            var throne = Place(Mi("SM_Env_Stone_Throne_01"), new Vector3(30f, FLOOR + 1f + 0.285f, 54.3f), 180f, stage, new Vector3(1.3f, 1.3f, 1.3f), "Throne");
            var trig = new GameObject("ThroneTrigger"); trig.transform.SetParent(stage); trig.transform.position = new Vector3(30f, FLOOR + 1f + 1.2f, 53.2f);
            var bc = trig.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(7f, 2.4f, 3.4f);
            trig.AddComponent<ThroneTrigger>();
        }

        // ---------------------------------------------------------------- player, systems, lighting
        static void PlacePlayerAndSystems()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Interact/IronCitadelPlayer.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "IronCitadelPlayer";
            player.transform.position = new Vector3(-7.5f, FLOOR + 0.05f, -8f);
            player.transform.rotation = Quaternion.identity; // facing +z: the guarded entrance
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.farClipPlane = 300f; cam.nearClipPlane = 0.05f;
                cam.cullingMask = ~(1 << MapOnlyLayer);
            }
            var sys = new GameObject("Systems");
            sys.AddComponent<LevelHud>();
            var map = sys.AddComponent<OverviewMap>(); map.ceilingLayer = CeilingLayer; map.mapOnlyLayer = MapOnlyLayer;
        }

        static void SetupLightingAndPost()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.11f, 0.14f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.012f, 0.012f, 0.018f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = null;
            Lightmapping.bakedGI = false; Lightmapping.realtimeGI = false;

            Directory.CreateDirectory("Assets/IronCitadel/Settings");
            string path = "Assets/IronCitadel/Settings/IronCitadelPost.asset";
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES); AssetDatabase.AddObjectToAsset(tone, profile);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(0.9f); bloom.intensity.Override(0.55f); bloom.scatter.Override(0.72f); AssetDatabase.AddObjectToAsset(bloom, profile);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.24f); vig.smoothness.Override(0.5f); AssetDatabase.AddObjectToAsset(vig, profile);
            var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.9f); ca.contrast.Override(10f); ca.saturation.Override(-6f); ca.colorFilter.Override(new Color(1f, 0.97f, 0.93f)); AssetDatabase.AddObjectToAsset(ca, profile);
            // The Cinema Themes LUT strips (256x16) tinted the image green through URP's Color Lookup, so no LUT is used; the grade is ACES + the adjustments above.
            var film = profile.Add<FilmGrain>(true); film.type.Override(FilmGrainLookup.Thin1); film.intensity.Override(0.15f); AssetDatabase.AddObjectToAsset(film, profile);
            AssetDatabase.SaveAssets();
            var vol = new GameObject("PostVolume").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = profile; vol.priority = 1f;

            // LUT size 16 so the Cinema Themes 256x16 strips are valid for URP's colour lookup
            var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (rp != null)
            {
                var so = new SerializedObject(rp);
                var p = so.FindProperty("m_ColorGradingLutSize"); if (p != null) p.intValue = 16;
                var sh = so.FindProperty("m_AdditionalLightsShadowmapResolution"); if (sh != null) sh.intValue = 2048;
                var sd = so.FindProperty("m_ShadowDistance"); if (sd != null) sd.floatValue = 60f;
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rp);
            }
        }

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(CeilingLayer).stringValue = "Ceilings";
            layers.GetArrayElementAtIndex(MapOnlyLayer).stringValue = "MapOnly";
            tagManager.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- helpers
        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
        }

        public static GameObject Place(string path, Vector3 pos, float yaw, Transform parent, Vector3? scale = null, string name = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { if (!missing.Contains(path)) missing.Add(path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (name != null) go.name = name;
            return go;
        }

        static GameObject PlaceR(string path, Vector3 pos, Vector3 euler, Transform parent, Vector3? scale = null, string name = null)
        {
            var go = Place(path, pos, 0, parent, scale, name);
            if (go != null) go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer; foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        static void StripColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        }

        static float Jit(float a) => (float)(rng.NextDouble() * 2 - 1) * a;

        static void MakeMaterials()
        {
            Directory.CreateDirectory("Assets/IronCitadel/Materials");
            matRanged = Mat("Marker_Ranged", new Color(0.15f, 0.55f, 1f), new Color(0.05f, 0.3f, 0.9f));
            matMelee = Mat("Marker_Melee", new Color(1f, 0.2f, 0.15f), new Color(0.8f, 0.1f, 0.05f));
            matWarlord = Mat("Marker_Warlord", new Color(0.8f, 0.2f, 1f), new Color(0.6f, 0.05f, 0.9f));
            matGear = Mat("Marker_Gear", new Color(1f, 0.85f, 0.2f), new Color(1.2f, 0.9f, 0.2f));
            matHoard = Mat("Marker_Hoard", new Color(1f, 0.7f, 0.1f), new Color(1.4f, 0.8f, 0.1f));
            matFacing = Mat("Marker_Facing", new Color(1f, 1f, 1f), new Color(0.9f, 0.9f, 0.9f));
        }

        static Material Mat(string name, Color c, Color emission)
        {
            string path = "Assets/IronCitadel/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.color = c; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A marker: a capsule (enemy) or cube/sphere (pickup) with a white nose showing the facing.</summary>
        static void Marker(string kind, string roomName, Vector3 pos, float facingYaw)
        {
            var r = rooms[roomName];
            GameObject go;
            Material m;
            switch (kind)
            {
                case "ranged": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f); m = matRanged; break;
                case "melee": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.transform.localScale = new Vector3(0.7f, 0.95f, 0.7f); m = matMelee; break;
                case "warlord": go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.transform.localScale = new Vector3(1.0f, 1.2f, 1.0f); m = matWarlord; break;
                case "gear": go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f); m = matGear; break;
                default: go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f); m = matHoard; break;
            }
            go.name = "Marker " + kind + " (" + roomName + ")";
            go.transform.SetParent(markersRoot);
            float lift = kind == "ranged" ? 0.9f : kind == "melee" ? 0.95f : kind == "warlord" ? 1.2f : kind == "gear" ? 1.1f : 0.9f;
            go.transform.position = pos + Vector3.up * lift;
            go.transform.rotation = Quaternion.Euler(0, facingYaw, 0);
            go.GetComponent<Renderer>().sharedMaterial = m;
            var mk = go.AddComponent<IronMarker>(); mk.kind = kind; mk.room = roomName;
            if (kind == "ranged" || kind == "melee" || kind == "warlord")
            {
                var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); nose.name = "Facing";
                nose.transform.SetParent(go.transform, false);
                nose.transform.localPosition = new Vector3(0, 0.45f, 0.7f); nose.transform.localScale = new Vector3(0.25f, 0.12f, 0.9f);
                nose.GetComponent<Renderer>().sharedMaterial = matFacing; UnityEngine.Object.DestroyImmediate(nose.GetComponent<Collider>());
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                var col = go.AddComponent<CapsuleCollider>(); col.radius = 0.5f; col.height = 2f;
            }
            else
            {
                var l = new GameObject("Glow").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.type = LightType.Point; l.range = 5f; l.intensity = 2.5f; l.color = kind == "gear" ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.75f, 0.2f);
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
        }
    }
}
