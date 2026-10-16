using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using IronCitadel.Interact;
using IronCitadel.Level;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The checks and walks for Assets/Scenes/IronCitadel.unity. Entry point: RunChecks.Run (see Checks/README.md).
// Static checks read the saved scene. Physics checks and walks run in play mode; the player is driven only through
// its own controls (StarterAssetsInputs.MoveInput / LookInput) and InteractOnE.Interact(). Writes Checks/results.txt.
public static class RunChecks
{
    const string ScenePath = "Assets/Scenes/IronCitadel.unity";
    const string ResultsPath = "Checks/results.txt";
    const string MapPath = "Map/map.json";
    const float Cell = 5f;

    static readonly List<string> Lines = new List<string>();
    static readonly Stack<IEnumerator> Flow = new Stack<IEnumerator>();
    static int passes, fails;

    static readonly string[] RequiredKinds =
    {
        "entry hall", "armory", "guard room", "prison", "kitchen", "library", "alchemist's study", "forge",
        "treasure vault", "dining hall", "throne room", "crypt", "storeroom", "shrine", "torture chamber",
    };
    static readonly string[] LowKinds = { "guard room", "prison", "kitchen", "torture chamber", "treasure vault", "crypt" };
    static readonly string[] StandardKinds = { "entry hall", "armory", "storeroom", "forge", "alchemist's study" };
    static readonly string[] TallKinds = { "dining hall", "throne room", "library", "shrine" };

    class RoomData
    {
        public LevelRoom room;
        public string name, kind, state;
        public List<UnityEngine.Bounds> floors = new List<UnityEngine.Bounds>();
        public List<UnityEngine.Bounds> ceilings = new List<UnityEngine.Bounds>();
        public float floorY, ceilingBottom, height;
        public List<Light> lights = new List<Light>();
        public bool Contains(Vector3 p)
        {
            foreach (var b in floors)
                if (p.x >= b.min.x - 0.01f && p.x <= b.max.x + 0.01f && p.z >= b.min.z - 0.01f && p.z <= b.max.z + 0.01f) return true;
            return false;
        }
    }

    static List<RoomData> Rooms = new List<RoomData>();
    static LevelDoor[] Doors = new LevelDoor[0];
    static LevelFeature Gallery, Stage, Throne;
    static LevelMarker[] Markers = new LevelMarker[0];

    // ---------- entry point ----------

    public static string Run()
    {
        Lines.Clear(); passes = 0; fails = 0;
        Directory.CreateDirectory("Checks");
        if (!File.Exists(MapPath)) Lines.Add("FAIL map.file: Map/map.json is missing; run Build/ExportMap.cs first");
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        LoadScene();
        // keep this script alive across play mode (no domain reload), but reload the scene for every play session so
        // each walk starts from the saved state; the project's own setting is put back in Finish
        savedPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedPlayOptions = EditorSettings.enterPlayModeOptions;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Flow.Clear();
        Flow.Push(Main());
        EditorApplication.update += Drive;
        return "checks started; results will be in " + ResultsPath;
    }

    // Runs the flow one step at a time from the editor's update. A nested IEnumerator is run to its end before
    // the flow goes on; a plain yield waits a frame.
    static void Drive()
    {
        try
        {
            while (Flow.Count > 0)
            {
                var top = Flow.Peek();
                if (!top.MoveNext()) { Flow.Pop(); continue; }
                if (top.Current is IEnumerator nested) { Flow.Push(nested); continue; }
                return;
            }
            EditorApplication.update -= Drive;
            Finish();
        }
        catch (Exception e)
        {
            Check("runner.exception", false, e.GetType().Name + ": " + e.Message + " | " + (e.StackTrace ?? "").Split('\n').FirstOrDefault());
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.update -= Drive;
            Finish();
        }
    }

    static void Finish()
    {
        EditorSettings.enterPlayModeOptionsEnabled = savedPlayOptionsEnabled;
        EditorSettings.enterPlayModeOptions = savedPlayOptions;
        Lines.Add("");
        Lines.Add($"SUMMARY {passes} passed, {fails} failed");
        Lines.Add("DONE");
        File.WriteAllLines(ResultsPath, Lines.ToArray());
        Debug.Log($"[Checks] {passes} passed, {fails} failed; written to {ResultsPath}");
    }

    static bool savedPlayOptionsEnabled;
    static EnterPlayModeOptions savedPlayOptions;

    static void Check(string id, bool ok, string detail)
    {
        if (ok) passes++; else fails++;
        Lines.Add((ok ? "PASS " : "FAIL ") + id + ": " + detail);
    }

    static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

    static IEnumerator Main()
    {
        StaticChecks();
        yield return null;
        yield return PlaySession(PhysicsChecks);
        yield return Walk("A", "way round to the gallery", WalkA());
        yield return Walk("B", "into the armory beneath the gallery, to its best gear", WalkB());
        yield return Walk("C", "hidden way into the vault and back", WalkC());
        yield return Walk("D", "dining hall on to the throne", WalkD());
        yield return null;
        LightingChecks();
    }

    static void LoadScene()
    {
        Rooms.Clear();
        var roomComps = UnityEngine.Object.FindObjectsByType<LevelRoom>(FindObjectsSortMode.None).OrderBy(r => r.transform.GetSiblingIndex()).ToList();
        foreach (var lr in roomComps)
        {
            var rd = new RoomData { room = lr, name = lr.roomName, kind = lr.kind, state = lr.state };
            foreach (Transform c in lr.transform)
            {
                if (c.name == "Floor") rd.floors.Add(BoundsOf(c));
                else if (c.name == "Ceiling") rd.ceilings.Add(BoundsOf(c));
            }
            // the walkable top: the colliders' top, which is where the player's capsule stands
            rd.floorY = lr.transform.Cast<Transform>().Where(c => c.name == "Floor").Select(WalkTop).DefaultIfEmpty(0f).Max();
            rd.ceilingBottom = rd.ceilings.Count > 0 ? rd.ceilings.Min(b => b.min.y) : float.NaN;
            rd.height = rd.ceilingBottom + 0.15f - rd.floorY;
            Rooms.Add(rd);
        }
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            var rd = Rooms.FirstOrDefault(r => r.Contains(l.transform.position));
            if (rd != null) rd.lights.Add(l);
        }
        Doors = UnityEngine.Object.FindObjectsByType<LevelDoor>(FindObjectsSortMode.None).OrderBy(d => d.transform.GetSiblingIndex()).ToArray();
        var features = UnityEngine.Object.FindObjectsByType<LevelFeature>(FindObjectsSortMode.None);
        Gallery = features.FirstOrDefault(f => f.feature == "gallery");
        Stage = features.FirstOrDefault(f => f.feature == "stage");
        Throne = features.FirstOrDefault(f => f.feature == "throne");
        Markers = UnityEngine.Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).OrderBy(m => m.transform.GetSiblingIndex()).ToArray();
    }

    static float WalkTop(Transform t)
    {
        var cols = t.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return BoundsOf(t).max.y;
        return cols.Max(c => c.bounds.max.y);
    }

    static UnityEngine.Bounds BoundsOf(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new UnityEngine.Bounds(t.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static RoomData RoomNamed(string name) => Rooms.First(r => r.name == name);

    // ---------- static checks: the saved scene and the map ----------

    static void StaticChecks()
    {
        Lines.Add("STATIC CHECKS (edit mode, saved scene " + ScenePath + ")");

        Check("rooms.count", Rooms.Count == 16, Rooms.Count + " rooms; names unique: " + (Rooms.Select(r => r.name).Distinct().Count() == Rooms.Count));
        var kinds = Rooms.Select(r => r.kind).ToList();
        var missing = RequiredKinds.Where(k => !kinds.Contains(k)).ToList();
        Check("rooms.kinds", missing.Count == 0, "the 15 kinds the brief names are present" + (missing.Count > 0 ? "; missing " + string.Join(", ", missing) : ""));
        var allowed = new HashSet<string>(RequiredKinds.Concat(new[] { "extra" }));
        var bad = kinds.Where(k => !allowed.Contains(k)).ToList();
        Check("rooms.kind-names", bad.Count == 0, "every kind is one the brief names, or 'extra'" + (bad.Count > 0 ? "; not allowed: " + string.Join(", ", bad) : ""));

        foreach (var r in Rooms)
            Check("ceiling." + r.name, r.ceilings.Count > 0 && r.ceilings.Count == r.floors.Count,
                r.ceilings.Count + " ceiling tiles over " + r.floors.Count + " floor tiles");

        foreach (var r in Rooms)
        {
            float h = r.height;
            string band;
            bool ok;
            if (LowKinds.Contains(r.kind)) { band = "low, 2.5-3.5 m"; ok = h >= 2.5f && h <= 3.5f; }
            else if (StandardKinds.Contains(r.kind)) { band = "standard, 4.5-5.5 m"; ok = h >= 4.5f && h <= 5.5f; }
            else if (TallKinds.Contains(r.kind)) { band = "tall, 6.5 m and up"; ok = h >= 6.5f; }
            else { band = "extra, any height"; ok = true; }
            Check("height." + r.name, ok, F(h) + " m from the ceiling tiles (" + band + ")");
        }
        var throneRoom = RoomNamed("Throne room");
        var tallest = Rooms.Where(r => r != throneRoom).Max(r => r.height);
        Check("height.throne-tallest", throneRoom.height > tallest, "throne room " + F(throneRoom.height) + " m, next tallest " + F(tallest) + " m");

        Check("doors.count", Doors.Length == 16, Doors.Length + " doors: " + Doors.Count(d => d.state == "open") + " open, " +
            Doors.Count(d => d.state == "shut") + " shut, " + Doors.Count(d => d.state == "hidden") + " hidden");
        foreach (var d in Doors)
        {
            var a = Rooms.FirstOrDefault(r => r.name == d.roomA);
            var b = Rooms.FirstOrDefault(r => r.name == d.roomB);
            bool onWall = a != null && b != null && OnSharedWall(d.transform.position, a, b);
            Check("door.on-shared-wall " + d.roomA + " / " + d.roomB, onWall, "at " + V(d.transform.position) + ", width " + F(d.width));
        }

        var armoryNeighbours = Doors.Where(d => d.roomA == "Armory" || d.roomB == "Armory")
            .Select(d => d.roomA == "Armory" ? d.roomB : d.roomA).OrderBy(s => s).ToList();
        Check("armory.two-ways-in", armoryNeighbours.SequenceEqual(new[] { "Dining hall", "Entry hall" }), "the armory opens to: " + string.Join(", ", armoryNeighbours));

        Check("route.way-round-avoids-armory", Reachable("Entry hall", "Dining hall", new[] { "Armory", "Treasure vault" }, false),
            "the entry hall reaches the dining hall without the armory or the vault");
        Check("route.dining-to-throne", Reachable("Dining hall", "Throne room", new[] { "Treasure vault" }, false), "the dining hall reaches the throne room");
        var vaultDoors = Doors.Where(d => d.roomA == "Treasure vault" || d.roomB == "Treasure vault").ToList();
        Check("vault.one-way-in", vaultDoors.Count == 1 && vaultDoors[0].state == "hidden", vaultDoors.Count + " door to the vault, state " + (vaultDoors.Count > 0 ? vaultDoors[0].state : "-"));
        Check("vault.on-no-way", Reachable("Entry hall", "Throne room", new[] { "Treasure vault" }, false),
            "the entry hall reaches the throne without the vault, so the vault lies on no way through");

        if (Gallery != null && Stage != null && Throne != null)
        {
            var deck = Gallery.transform.Cast<Transform>().Where(t => t.name == "Deck").Select(BoundsOf).ToList();
            var rails = Gallery.transform.Cast<Transform>().Where(t => t.name == "Rail").Select(BoundsOf).ToList();
            var stair = Gallery.transform.parent.Cast<Transform>().FirstOrDefault(t => t.name == "Stair");
            float deckY = Gallery.transform.Cast<Transform>().Where(t => t.name == "Deck").Max(WalkTop);
            Check("gallery.height", deckY >= 4f && deckY <= 6f, "walkable deck top " + F(deckY) + " m above the hall floor");
            Check("gallery.rail", rails.Count >= 4, rails.Count + " rail pieces along the deck edge");
            if (stair != null)
            {
                var sb = BoundsOf(stair);
                float stairTop = WalkTop(stair);
                Check("gallery.stair-reaches-deck", Mathf.Abs(stairTop - deckY) < 0.05f, "stair top " + F(stairTop) + " m, deck " + F(deckY) + " m");
                var arrival = Doors.First(d => (d.roomA == "Dining hall" || d.roomB == "Dining hall") && (d.roomA == "East passage" || d.roomB == "East passage"));
                Vector3 ap = arrival.transform.position;
                bool atFoot = ap.x > sb.min.x - 0.2f && ap.x < sb.max.x + 0.2f && ap.z > sb.min.z && ap.z < sb.max.z;
                Check("gallery.stair-foot-at-arrival", atFoot, "the arrival door " + V(ap) + " opens onto the stair foot");
            }
            var back = Doors.First(d => (d.roomA == "Armory" || d.roomB == "Armory") && (d.roomA == "Dining hall" || d.roomB == "Dining hall"));
            Vector3 bp = back.transform.position;
            bool beneath = deck.Any(b => bp.x > b.min.x - 0.2f && bp.x < b.max.x + 0.2f && bp.z > b.min.z && bp.z < b.max.z);
            Check("gallery.armory-way-beneath", beneath, "the armory's back door " + V(bp) + " lies beneath the deck");
            float underside = deck.Min(b => b.min.y);
            Check("gallery.headroom", underside >= 2.4f, "the deck's underside is " + F(underside) + " m above the hall floor");
        }
        else Check("gallery.present", false, "gallery, stage or throne feature missing");

        float stageTop = Stage != null ? BoundsOf(Stage.transform).max.y : float.NaN;
        Check("stage.top", Math.Abs(stageTop - 1f) < 0.05f, "stage top " + F(stageTop) + " m");
        if (Throne != null)
        {
            var tb = BoundsOf(Throne.transform);
            Check("throne.on-stage", Math.Abs(tb.min.y - stageTop) < 0.05f && tb.center.z < -25f,
                "throne base " + F(tb.min.y) + " m, centre " + V(tb.center));
        }

        var enemies = Markers.Where(m => m.markerKind == "enemy").ToList();
        var pickups = Markers.Where(m => m.markerKind == "pickup").ToList();
        var perRoom = enemies.GroupBy(m => m.room).ToDictionary(g => g.Key, g => g.Count());
        int diningCount = perRoom.TryGetValue("Dining hall", out var dn) ? dn : 0;
        int maxOther = perRoom.Where(p => p.Key != "Dining hall").Select(p => p.Value).DefaultIfEmpty(0).Max();
        Check("enemies.dining-largest", diningCount > maxOther, "dining hall " + diningCount + " defenders, the largest other group " + maxOther);
        Check("enemies.entry-hall-empty", !perRoom.ContainsKey("Entry hall"), "the entry hall holds " + (perRoom.TryGetValue("Entry hall", out var eh) ? eh : 0));
        Check("enemies.armory-ranged", enemies.Count(m => m.room == "Armory" && m.enemyKind == "ranged") >= 2,
            "ranged defenders in the armory: " + enemies.Count(m => m.room == "Armory" && m.enemyKind == "ranged"));
        Check("enemies.warlord", enemies.Count(m => m.enemyKind == "warlord" && m.room == "Throne room") == 1, "one warlord, in the throne room");
        Check("pickups.two", pickups.Count == 2 && pickups.Any(p => p.pickupKind == "best gear" && p.room == "Armory") &&
            pickups.Any(p => p.pickupKind == "hoard" && p.room == "Treasure vault"), "best gear in the armory, hoard in the vault");

        if (File.Exists(MapPath))
        {
            var text = File.ReadAllText(MapPath);
            int rooms = CountOf(text, "\"floor\":"), doors = CountOf(text, "\"rooms\": [\"");
            Check("map.matches-scene", rooms == Rooms.Count && doors == Doors.Length,
                "map.json: " + rooms + " rooms, " + doors + " doors; scene: " + Rooms.Count + " rooms, " + Doors.Length + " doors");
        }
        Lines.Add("");
    }

    static int CountOf(string text, string token)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { n++; i += token.Length; }
        return n;
    }

    // the cells a room covers, as integer (x, z) indices on the 5 m grid
    static HashSet<(int, int)> CellsOf(RoomData r) =>
        new HashSet<(int, int)>(r.floors.Select(f => (Mathf.RoundToInt(f.min.x / Cell), Mathf.RoundToInt(f.min.z / Cell))));

    static bool OnSharedWall(Vector3 p, RoomData a, RoomData b)
    {
        var ca = CellsOf(a);
        var cb = CellsOf(b);
        foreach (var (i, j) in ca)
        {
            Vector3 mid;
            if (cb.Contains((i + 1, j))) { mid = new Vector3((i + 1) * Cell, 0, j * Cell + 2.5f); if (Near(p, mid)) return true; }
            if (cb.Contains((i - 1, j))) { mid = new Vector3(i * Cell, 0, j * Cell + 2.5f); if (Near(p, mid)) return true; }
            if (cb.Contains((i, j + 1))) { mid = new Vector3(i * Cell + 2.5f, 0, (j + 1) * Cell); if (Near(p, mid)) return true; }
            if (cb.Contains((i, j - 1))) { mid = new Vector3(i * Cell + 2.5f, 0, j * Cell); if (Near(p, mid)) return true; }
        }
        return false;
    }

    static bool Near(Vector3 p, Vector3 mid) => Mathf.Abs(p.x - mid.x) < 0.1f && Mathf.Abs(p.z - mid.z) < 0.1f;

    // can one room reach another through the door graph, avoiding some rooms; hidden doors count only if allowed
    static bool Reachable(string from, string to, string[] avoid, bool allowHidden)
    {
        var seen = new HashSet<string> { from };
        var queue = new Queue<string>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var r = queue.Dequeue();
            if (r == to) return true;
            foreach (var d in Doors)
            {
                if (d.state == "hidden" && !allowHidden) continue;
                string other = d.roomA == r ? d.roomB : d.roomB == r ? d.roomA : null;
                if (other == null || seen.Contains(other) || avoid.Contains(other)) continue;
                seen.Add(other);
                queue.Enqueue(other);
            }
        }
        return false;
    }

    // ---------- lighting: render each room from standing eye height and measure how lit it is ----------

    static void LightingChecks()
    {
        Lines.Add("");
        Lines.Add("LIGHTING (rendered from standing eye height; mean luminance 0 to 1)");
        var eyes = new (string room, Vector3 from, Vector3 to)[]
        {
            ("Entry hall", new Vector3(3f, 1.6f, 3f), new Vector3(14f, 1.6f, 12f)),
            ("Armory", new Vector3(29f, 1.6f, 12.5f), new Vector3(16f, 1.6f, 6f)),
            ("Dining hall", new Vector3(58f, 1.6f, 2.5f), new Vector3(33f, 2.5f, 12f)),
            ("Guard room", new Vector3(2f, 1.6f, 22f), new Vector3(13f, 1.6f, 28f)),
            ("Prison", new Vector3(16f, 1.6f, 21.5f), new Vector3(28f, 1.6f, 28f)),
            ("Torture chamber", new Vector3(31f, 1.6f, 22f), new Vector3(44f, 1.6f, 28f)),
            ("Crypt", new Vector3(46f, 1.6f, 22f), new Vector3(59f, 1.6f, 27f)),
            ("East passage", new Vector3(61f, 1.6f, 8f), new Vector3(64f, 1.6f, 20f)),
            ("Kitchen", new Vector3(2f, 1.6f, -2f), new Vector3(13f, 1.6f, -12f)),
            ("Storeroom", new Vector3(16f, 1.6f, -2f), new Vector3(29f, 1.6f, -12f)),
            ("Forge", new Vector3(16f, 1.6f, -18f), new Vector3(29f, 1.6f, -26f)),
            ("Treasure vault", new Vector3(1f, 1.6f, -18f), new Vector3(14f, 1.6f, -26f)),
            ("Library", new Vector3(46f, 1.6f, -2f), new Vector3(59f, 1.6f, -12f)),
            ("Shrine", new Vector3(31f, 1.6f, -2f), new Vector3(44f, 1.6f, -12f)),
            ("Alchemist's study", new Vector3(46f, 1.6f, -18f), new Vector3(59f, 1.6f, -27f)),
            ("Throne room", new Vector3(37.5f, 1.6f, -15.5f), new Vector3(37.5f, 2.5f, -29f)),
        };
        foreach (var e in eyes)
        {
            float lum = MeanLuminance(e.from, e.to);
            // 0.06 of 1 is about 15 of 255 on average: dim, but the walls, floor and furniture can be read
            Check("light." + e.room, lum >= 0.06f, "mean luminance " + lum.ToString("0.000", CultureInfo.InvariantCulture) + " (0.06 wanted)");
        }
    }

    static float MeanLuminance(Vector3 from, Vector3 to)
    {
        const int W = 640, H = 360;
        var go = new GameObject("Check camera");
        go.transform.position = from;
        go.transform.LookAt(to);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        var px = tex.GetPixels32();
        double sum = 0;
        foreach (var p in px) sum += 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b;
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(go);
        return (float)(sum / px.Length / 255.0);
    }

    // ---------- play sessions ----------

    static IEnumerator PlaySession(Func<IEnumerator> body)
    {
        yield return EnterPlay();
        yield return body();
        yield return ExitPlay();
    }

    static IEnumerator EnterPlay()
    {
        EditorApplication.isPlaying = true;
        float t0 = Time.realtimeSinceStartup;
        while (!(Application.isPlaying && FindPlayer() != null) && Time.realtimeSinceStartup - t0 < 120f) yield return null;
        // the scene is reloaded for each session, so the objects checked in play mode are the session's own
        LoadScene();
        // let the first frames settle, so the player stands on the floor
        for (int i = 0; i < 60; i++) yield return null;
    }

    static IEnumerator ExitPlay()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        float t0 = Time.realtimeSinceStartup;
        while (EditorApplication.isPlaying && Time.realtimeSinceStartup - t0 < 60f) yield return null;
        yield return null;
    }

    static InteractOnE FindPlayer() => UnityEngine.Object.FindAnyObjectByType<InteractOnE>();

    // ---------- physics checks (play mode) ----------

    static IEnumerator PhysicsChecks()
    {
        Lines.Add("");
        Lines.Add("PHYSICS (play mode; rays against the built colliders)");
        var player = FindPlayer();
        Check("start.position", (player.transform.position - new Vector3(4f, 0f, 7.5f)).magnitude < 0.05f,
            "the player starts at " + V(player.transform.position) + ", in the entry hall");

        // the player's own controls: walk, then sprint, forward (the start faces the armory, 11 m off)
        var inputs = UnityEngine.Object.FindAnyObjectByType<StarterAssetsInputs>();
        float walked = 0f, sprinted = 0f;
        foreach (bool sprint in new[] { false, true })
        {
            Vector3 from = player.transform.position;
            float t0 = Time.realtimeSinceStartup;
            inputs.SprintInput(sprint);
            while (Time.realtimeSinceStartup - t0 < 0.8f)
            {
                inputs.MoveInput(new Vector2(0f, 1f));
                yield return null;
            }
            inputs.MoveInput(Vector2.zero);
            float d = Vector3.ProjectOnPlane(player.transform.position - from, Vector3.up).magnitude;
            if (sprint) sprinted = d; else walked = d;
            for (int i = 0; i < 10; i++) yield return null;
        }
        inputs.SprintInput(false);
        Check("controls.walk-and-sprint", walked > 1.5f && sprinted > walked * 1.25f,
            "walked " + F(walked) + " m in 0.8 s, sprinted " + F(sprinted) + " m in 0.8 s");
        yield return null;

        foreach (var r in Rooms)
        {
            int samples = 0, hits = 0;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var f in r.floors)
                for (float x = f.min.x + 0.5f; x <= f.max.x - 0.5f; x += 1f)
                    for (float z = f.min.z + 0.5f; z <= f.max.z - 0.5f; z += 1f)
                    {
                        if (SkipFloor(r, x, z)) continue;
                        samples++;
                        // any hit at floor level counts: furniture can be the first thing the ray meets
                        bool onFloor = false;
                        foreach (var h in Physics.RaycastAll(new Vector3(x, 2f, z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (!IsFloorPiece(h.collider.transform)) continue;
                            lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y);
                            if (Mathf.Abs(h.point.y - r.floorY) < 0.05f) onFloor = true;
                        }
                        if (onFloor) hits++;
                    }
            Check("floor." + r.name, samples > 0 && hits == samples,
                hits + " of " + samples + " sample points stand on the floor at " + F(r.floorY) + " m; the floor colliders top out at " + F(lo) + " to " + F(hi) + " m");
        }
        yield return null;

        foreach (var r in Rooms)
        {
            int samples = 0, ok = 0;
            foreach (var f in r.floors)
                for (float x = f.min.x + 0.5f; x <= f.max.x - 0.5f; x += 1f)
                    for (float z = f.min.z + 0.5f; z <= f.max.z - 0.5f; z += 1f)
                    {
                        if (SkipFloor(r, x, z) || (r.name == "Dining hall" && z > 5f && x < 50f)) continue;
                        samples++;
                        // any surface the ray meets near the ceiling counts: furniture and chandeliers are in the way of the first hit
                        foreach (var h in Physics.RaycastAll(new Vector3(x, 0.3f, z), Vector3.up, 20f, ~0, QueryTriggerInteraction.Ignore))
                            if (Mathf.Abs(h.point.y - (r.floorY + r.height)) < 0.25f) { ok++; break; }
                    }
            Check("ceiling.hit." + r.name, samples > 0 && ok == samples, ok + " of " + samples + " up-rays meet the ceiling near " + F(r.height) + " m");
        }
        yield return null;

        // walls and openings: a horizontal ray across every room boundary, at three heights, in 0.5 m steps.
        // A ray that gets through must pass a listed open doorway, or the scene has an unlisted opening.
        int sightLines = 0;
        var unlisted = new List<string>();
        foreach (var r in Rooms)
        {
            foreach (var edge in BoundaryEdges(r))
            {
                for (float t = 0.25f; t < edge.length; t += 0.5f)
                {
                    Vector3 p = edge.start + edge.dir * t;
                    foreach (float hgt in new[] { 0.5f, 1.5f, 2.2f })
                    {
                        Vector3 a = new Vector3(p.x, hgt, p.z) - edge.outward * 0.8f;
                        Vector3 b = new Vector3(p.x, hgt, p.z) + edge.outward * 0.8f;
                        if (Physics.Linecast(a, b, out _, ~0, QueryTriggerInteraction.Ignore)) continue;
                        sightLines++;
                        if (!ListedOpening(p, edge.alongX)) unlisted.Add(r.name + " at " + V(p) + " height " + F(hgt));
                    }
                }
            }
        }
        Check("walls.no-unlisted-openings", unlisted.Count == 0,
            sightLines + " open sight lines, all through listed open doorways" + (unlisted.Count > 0 ? "; UNLISTED: " + string.Join("; ", unlisted.Take(8)) : ""));
        yield return null;

        // each door: an open doorway is clear across its width; a shut or hidden door blocks its opening
        foreach (var d in Doors)
        {
            Vector3 c = d.transform.position + Vector3.up * 1.0f;
            Vector3 across = d.transform.forward;
            float clear = 0f;
            for (float s = -1.2f; s <= 1.2f; s += 0.02f)
            {
                Vector3 p = c + d.transform.right * s;
                if (!Physics.Linecast(p - across * 0.8f, p + across * 0.8f, out _, ~0, QueryTriggerInteraction.Ignore)) clear += 0.02f;
            }
            if (d.state == "open")
                Check("door.clear " + d.roomA + " / " + d.roomB, clear >= 1.2f && Mathf.Abs(clear - d.width) < 0.25f,
                    "clear width " + F(clear) + " m, declared " + F(d.width) + " m");
            else
                Check("door.blocks " + d.roomA + " / " + d.roomB, clear < 0.2f, d.state + " door, clear width " + F(clear) + " m before it is opened");
        }
        yield return null;

        // the run starts 0.4 m in from the east wall, so the wall's own face is not read as the first step
        var stairFrom = new Vector3(59.6f, 0f, 13.6f);
        var stairTo = new Vector3(50.4f, 0f, 13.6f);
        float stairRise = MaxRise(stairFrom, stairTo);
        Check("stair.risers", stairRise <= 0.30f, "stair to the gallery: highest rise " + F(stairRise) + " m within 5 cm of run; surface every 0.5 m from the foot: " +
            Profile(stairFrom, stairTo));
        float stageRise = MaxRise(new Vector3(37.5f, 0f, -19.6f), new Vector3(37.5f, 0f, -25.4f), out var stageWhere);
        Check("stage.steps", stageRise <= 0.30f, "throne steps: highest rise " + F(stageRise) + " m within 5 cm of run, " + stageWhere + "; surface every 0.5 m from the floor: " +
            Profile(new Vector3(37.5f, 0f, -19.6f), new Vector3(37.5f, 0f, -25.4f)));
        var stageTops = new List<string>();
        bool stageFlat = true;
        foreach (var x in new[] { 31.5f, 37.5f, 43.5f })
            foreach (var z in new[] { -26f, -27f })
            {
                float y = Physics.Raycast(new Vector3(x, 5f, z), Vector3.down, out var sh, 6f, ~0, QueryTriggerInteraction.Ignore) ? sh.point.y : float.NaN;
                stageTops.Add(F(y));
                if (Mathf.Abs(y - 1f) > 0.05f) stageFlat = false;
            }
        Check("stage.walkable-top", stageFlat, "the stage surface reads " + string.Join(", ", stageTops) + " m at six points (1.00 wanted)");
        yield return null;

        // from the gallery's rail, how much of the hall floor is in view
        // the eye of a player standing at the rail (the rail is at z 5; the capsule keeps 0.35 m clear of it)
        // the floor points under the furniture standing on it (tables, benches) are left out: they are not floor to see
        var eye = new Vector3(42f, DeckTopOr(4.6f) + 1.6f, 5.5f);
        var floorFurniture = Rooms.First(r => r.name == "Dining hall").room.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("SM_Prop_")).Select(BoundsOf).Where(b => b.max.y < 2f).ToList();
        int visible = 0, total = 0, covered = 0;
        var rows = new List<string>();
        var blockers = new Dictionary<string, int>();
        for (float z = 0.5f; z <= 4.5f; z += 0.5f)
        {
            int rowSeen = 0, rowTotal = 0;
            for (float x = 31f; x <= 59f; x += 1f)
            {
                if (floorFurniture.Any(b => x > b.min.x && x < b.max.x && z > b.min.z && z < b.max.z)) { covered++; continue; }
                total++; rowTotal++;
                if (!Physics.Linecast(eye, new Vector3(x, 0.05f, z), out var hit, ~0, QueryTriggerInteraction.Ignore)) { visible++; rowSeen++; }
                else
                {
                    string who = hit.collider.transform.name + (hit.collider.transform.parent != null ? " in " + hit.collider.transform.parent.name : "");
                    blockers[who] = blockers.TryGetValue(who, out int n) ? n + 1 : 1;
                }
            }
            rows.Add("z " + F(z) + ": " + rowSeen + "/" + rowTotal);
        }
        string blocked = string.Join("; ", blockers.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + " x" + kv.Value));
        Check("gallery.view-of-floor", total > 0 && visible >= total * 0.5f,
            visible + " of " + total + " free hall-floor points in view from the rail (" + covered + " under furniture left out); by row " + string.Join(", ", rows) +
            "; blocked by " + blocked);

        // the armory's guards: out of sight from the middle of the entry hall, in sight from the doorway
        var hall = new Vector3(7.5f, 1.6f, 7.5f);
        var doorway = new Vector3(15.6f, 1.6f, 7.5f);
        foreach (var g in Markers.Where(m => m.room == "Armory" && m.enemyKind == "ranged"))
        {
            Vector3 head = g.transform.position + Vector3.up * 1.6f;
            bool hidden = Physics.Linecast(hall, head, out _, ~0, QueryTriggerInteraction.Ignore);
            bool seen = !Physics.Linecast(doorway, head, out _, ~0, QueryTriggerInteraction.Ignore);
            Check("guard.sight " + V(g.transform.position), hidden && seen, "out of sight from the hall: " + hidden + "; in sight from the doorway: " + seen);
        }
        yield return null;

        // cover: tall and low furniture to stand or crouch behind
        int tallProps = 0, lowProps = 0;
        foreach (var r in Rooms)
            foreach (Transform t in r.room.transform)
            {
                if (!t.name.StartsWith("SM_Prop_") && !t.name.StartsWith("SM_Env_Statue") && !t.name.StartsWith("SM_Env_Pillar")) continue;
                float h = BoundsOf(t).size.y;
                if (h >= 1.9f) tallProps++;
                else if (h <= 1.2f) lowProps++;
            }
        Check("cover.tall", tallProps >= 5, tallProps + " props 1.9 m and taller, to stand behind");
        Check("cover.low", lowProps >= 5, lowProps + " props 1.2 m and under, to crouch behind");
    }

    static float DeckTopOr(float fallback) => Gallery != null
        ? Gallery.transform.Cast<Transform>().Where(t => t.name == "Deck").Select(WalkTop).DefaultIfEmpty(fallback).Max()
        : fallback;

    // the colliders around a point (a stall's cause), by object name and its parent
    static string Touching(Vector3 p)
    {
        var names = new List<string>();
        foreach (var c in Physics.OverlapSphere(p + Vector3.up * 0.5f, 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.GetComponentInParent<InteractOnE>() != null) continue;
            string name = c.transform.name + (c.transform.parent != null ? " in " + c.transform.parent.name : "");
            if (!names.Contains(name)) names.Add(name);
        }
        return names.Count == 0 ? "nothing" : string.Join(", ", names.Take(6));
    }

    static bool IsFloorPiece(Transform t)
    {
        for (; t != null; t = t.parent) if (t.name == "Floor") return true;
        return false;
    }

    static bool SkipFloor(RoomData r, float x, float z)
    {
        // the stair, the gallery deck and the stage are not the floor under them
        if (r.name == "Dining hall" && x > 50f && z > 7f) return true;
        if (r.name == "Throne room" && z < -20f && z > -30f && x > 30f && x < 45f) return true;
        return false;
    }

    struct Edge { public Vector3 start, dir, outward; public float length; public bool alongX; }

    // the sides of a room's cells that face outside the room, as 5 m wall lines
    static List<Edge> BoundaryEdges(RoomData r)
    {
        var cells = CellsOf(r);
        var list = new List<Edge>();
        foreach (var (i, j) in cells)
        {
            float x0 = i * Cell, z0 = j * Cell, x1 = x0 + Cell, z1 = z0 + Cell;
            if (!cells.Contains((i, j + 1))) list.Add(new Edge { start = new Vector3(x0, 0, z1), dir = Vector3.right, outward = Vector3.forward, length = Cell, alongX = true });
            if (!cells.Contains((i, j - 1))) list.Add(new Edge { start = new Vector3(x0, 0, z0), dir = Vector3.right, outward = Vector3.back, length = Cell, alongX = true });
            if (!cells.Contains((i + 1, j))) list.Add(new Edge { start = new Vector3(x1, 0, z0), dir = Vector3.forward, outward = Vector3.right, length = Cell, alongX = false });
            if (!cells.Contains((i - 1, j))) list.Add(new Edge { start = new Vector3(x0, 0, z0), dir = Vector3.forward, outward = Vector3.left, length = Cell, alongX = false });
        }
        return list;
    }

    static bool ListedOpening(Vector3 p, bool alongX)
    {
        foreach (var d in Doors)
        {
            if (d.state != "open") continue;
            float across = alongX ? Mathf.Abs(d.transform.position.z - p.z) : Mathf.Abs(d.transform.position.x - p.x);
            float along = alongX ? Mathf.Abs(d.transform.position.x - p.x) : Mathf.Abs(d.transform.position.z - p.z);
            if (across < 0.6f && along <= d.width / 2f + 0.3f) return true;
        }
        return false;
    }

    static string Profile(Vector3 from, Vector3 to)
    {
        var parts = new List<string>();
        float length = (to - from).magnitude;
        Vector3 dir = (to - from).normalized;
        for (float s = 0f; s <= length + 0.001f; s += 0.5f)
        {
            var p = from + dir * s;
            parts.Add(Physics.Raycast(new Vector3(p.x, 5.5f, p.z), Vector3.down, out var h, 5.5f, ~0, QueryTriggerInteraction.Ignore) ? F(h.point.y) : "-");
        }
        return string.Join(" ", parts);
    }

    static float MaxRise(Vector3 from, Vector3 to) => MaxRise(from, to, out _);

    static float MaxRise(Vector3 from, Vector3 to, out string where)
    {
        // the surface heights along a line, in 1 cm steps, found from above; the largest rise within 5 cm
        var heights = new List<float>();
        var points = new List<Vector3>();
        float length = (to - from).magnitude;
        Vector3 dir = (to - from).normalized;
        for (float s = 0f; s <= length; s += 0.01f)
        {
            var p = from + dir * s;
            // the ray starts under the ceiling (5.5 m) and above the stair and deck (4.6 m)
            heights.Add(Physics.Raycast(new Vector3(p.x, 5.5f, p.z), Vector3.down, out var h, 5.5f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : float.NaN);
            points.Add(p);
        }
        float best = 0f;
        int at = -1, atEnd = -1;
        for (int i = 0; i < heights.Count; i++)
            for (int j = i + 1; j < heights.Count && j <= i + 5; j++)
                if (!float.IsNaN(heights[i]) && !float.IsNaN(heights[j]) && heights[j] - heights[i] > best)
                {
                    best = heights[j] - heights[i]; at = i; atEnd = j;
                }
        where = at < 0 ? "none" : "at (" + F(points[at].x) + ", " + F(points[at].z) + ") from " + F(heights[at]) + " to " + F(heights[atEnd]) + " m";
        return best;
    }

    // ---------- walks (play mode; each is one play session from the start) ----------

    class WalkStep { public bool isDoor; public float x, z; public string a, b; }
    static WalkStep M(float x, float z) => new WalkStep { x = x, z = z };
    static WalkStep D(string a, string b) => new WalkStep { isDoor = true, a = a, b = b };

    static List<WalkStep> WayRound() => new List<WalkStep>
    {
        M(8.5f, 7.5f), M(8.5f, 18.5f), M(7.5f, 18.9f), D("Entry hall", "Guard room"),
        M(7.5f, 21.3f), M(13.8f, 22.5f), D("Guard room", "Prison"),
        M(16.6f, 22.5f), M(28.8f, 22.5f), D("Prison", "Torture chamber"),
        M(31.6f, 22.5f), M(43.8f, 22.5f), D("Torture chamber", "Crypt"),
        M(46.6f, 22.5f), M(61.2f, 22.5f), M(61.2f, 12f), M(61.2f, 7.5f), D("East passage", "Dining hall"),
        M(58.8f, 7.5f),
    };

    static List<WalkStep> WalkA()
    {
        var s = WayRound();
        s.AddRange(new[] { M(57f, 12f), M(51f, 12f), M(46f, 12f) });
        return s;
    }

    static List<WalkStep> WalkB()
    {
        var s = WayRound();
        s.AddRange(new[]
        {
            M(58f, 4f), M(45f, 3.5f), M(34f, 5.5f), M(32.5f, 9f), M(31.2f, 12.5f),
            M(28.8f, 12.5f), M(28.4f, 10.6f),
        });
        return s;
    }

    static List<WalkStep> WalkC() => new List<WalkStep>
    {
        M(7.5f, 4f), M(7.5f, 1.2f), D("Entry hall", "Kitchen"),
        M(7.5f, -1.5f), M(7.5f, -6f), M(13.5f, -7.5f), M(16.5f, -7.5f),
        M(19.5f, -12f), M(22.5f, -13.8f), D("Storeroom", "Forge"),
        M(22.5f, -16.4f), M(17.5f, -18.5f), M(16.3f, -22.5f), D("Forge", "Treasure vault"),
        M(13.5f, -22.5f), M(12.5f, -21f), M(8f, -21f), M(12.5f, -21f), M(13.5f, -22.5f), M(16.3f, -22.5f), M(17.5f, -18.5f), M(21.5f, -16.5f),
    };

    static List<WalkStep> WalkD()
    {
        var s = WayRound();
        s.AddRange(new[]
        {
            M(58f, 4f), M(53f, 1.2f), D("Dining hall", "Library"),
            M(52.5f, -2f), M(47.5f, -7.5f), M(46.6f, -7.5f), M(43.5f, -7.5f),
            M(39.5f, -11.5f), M(37.5f, -13.8f), D("Shrine", "Throne room"),
            M(37.5f, -16.4f), M(37.5f, -20f), M(37.5f, -22f), M(37.5f, -25.5f), M(37.5f, -27f),
        });
        return s;
    }

    static IEnumerator Walk(string id, string title, List<WalkStep> steps)
    {
        Lines.Add("");
        Lines.Add("WALK " + id + ": " + title);
        yield return EnterPlay();

        var player = FindPlayer();
        var inputs = UnityEngine.Object.FindAnyObjectByType<StarterAssetsInputs>();
        var fpc = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
        var cam = Camera.main;
        var visited = new List<string>();
        string room = "";
        float progressTime = Time.realtimeSinceStartup;
        Vector3 progressPos = player.transform.position;
        bool stalled = false;
        string stallDetail = "";
        int hallFloorFrames = 0;
        var doorLog = new List<string>();

        foreach (var step in steps)
        {
            if (!step.isDoor)
            {
                while (true)
                {
                    Vector3 p = player.transform.position;
                    Vector3 flat = new Vector3(step.x - p.x, 0f, step.z - p.z);
                    if (flat.magnitude < 0.45f) break;
                    room = Track(player.transform, visited, room);
                    if (id == "A") hallFloorFrames += HallFloorFrame(player.transform);
                    Steer(player, fpc, inputs, flat);
                    if ((p - progressPos).magnitude > 0.2f) { progressPos = p; progressTime = Time.realtimeSinceStartup; }
                    if (Time.realtimeSinceStartup - progressTime > 4f)
                    {
                        stalled = true;
                        stallDetail = "stalled at " + V(p) + " heading for (" + F(step.x) + ", " + F(step.z) + "); touching " + Touching(p);
                        break;
                    }
                    yield return null;
                }
                if (stalled) break;
            }
            else
            {
                var door = Doors.First(d => (d.roomA == step.a && d.roomB == step.b) || (d.roomA == step.b && d.roomB == step.a));
                Vector3 centre = door.transform.position + Vector3.up * 1.1f;
                bool aimed = false;
                float aimStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - aimStart < 8f)
                {
                    room = Track(player.transform, visited, room);
                    Vector3 p = player.transform.position;
                    Vector3 flat = new Vector3(centre.x - p.x, 0f, centre.z - p.z);
                    if (flat.magnitude > 1.6f) { Steer(player, fpc, inputs, flat); yield return null; continue; }
                    if (AimAt(player, fpc, inputs, cam, centre)) { aimed = true; break; }
                    yield return null;
                }
                inputs.MoveInput(Vector2.zero);
                inputs.LookInput(Vector2.zero);
                if (aimed)
                {
                    player.Interact();
                    for (int i = 0; i < 10; i++) yield return null;
                }
                bool opened = door.opened;
                doorLog.Add(step.a + " / " + step.b + ": " + (opened ? "opened with E" : "NOT opened") + (aimed ? "" : " (never aimed at the door: " + aimLog + ")"));
                if (!opened)
                {
                    stalled = true;
                    stallDetail = "door " + step.a + " / " + step.b + " did not open";
                    break;
                }
                progressTime = Time.realtimeSinceStartup;
                progressPos = player.transform.position;
            }
        }
        for (int i = 0; i < 20; i++) { room = Track(player.transform, visited, room); yield return null; }
        inputs.MoveInput(Vector2.zero);
        inputs.LookInput(Vector2.zero);

        Vector3 end = player.transform.position;
        Lines.Add("  rooms in order: " + string.Join(" > ", visited));
        foreach (var dl in doorLog) Lines.Add("  door " + dl);
        Lines.Add("  ends at " + V(end) + " in " + (room == "" ? "-" : room));
        Check("walk." + id + ".no-stall", !stalled, stalled ? stallDetail : "walked the whole route without stalling");

        switch (id)
        {
            case "A":
                Check("walk.A.way-round-avoids-armory", !visited.Contains("Armory"), "rooms: " + string.Join(", ", visited));
                Check("walk.A.up-to-gallery", !stalled && end.y > 4.4f && room == "Dining hall", "ends on the gallery at y " + F(end.y) + " in " + room);
                Check("walk.A.no-hall-floor", hallFloorFrames == 0, hallFloorFrames + " frames on the hall floor after the arrival door");
                break;
            case "B":
                bool throughDining = visited.Contains("Dining hall") && visited.Contains("Armory") && visited.IndexOf("Armory") > visited.IndexOf("Dining hall");
                var gear = Markers.First(m => m.pickupKind == "best gear");
                float dGear = Vector2.Distance(new Vector2(end.x, end.z), new Vector2(gear.transform.position.x, gear.transform.position.z));
                Check("walk.B.beneath-gallery-into-armory", !stalled && throughDining && room == "Armory", "dining hall then armory; ends in " + room);
                Check("walk.B.best-gear", !stalled && dGear < 1.5f, "ends " + F(dGear) + " m from the best gear");
                break;
            case "C":
                bool vault = visited.Contains("Treasure vault");
                Check("walk.C.vault-and-back", !stalled && vault && room == "Forge", "vault visited: " + vault + "; ends in " + room);
                break;
            case "D":
                var tb = Throne.transform.position;
                float dThrone = Vector2.Distance(new Vector2(end.x, end.z), new Vector2(tb.x, tb.z));
                Check("walk.D.throne", !stalled && dThrone < 4f && end.y > 0.9f, "ends " + F(dThrone) + " m from the throne, at y " + F(end.y));
                HudChecks();
                break;
        }
        yield return ExitPlay();
    }

    // the HUD: the room it names, the throne announcement, and the M overview (its camera culls the ceilings).
    // M itself is a key press, so the overview is switched through the HUD's own SetMap, the code M calls.
    static void HudChecks()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<LevelHud>();
        if (hud == null) { Check("hud.present", false, "no LevelHud in the scene"); return; }
        const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = typeof(LevelHud);
        string room = (string)type.GetField("_room", any).GetValue(hud);
        float throneTime = (float)type.GetField("_throneTime", any).GetValue(hud);
        Check("hud.room-and-throne", room == "Throne room" && throneTime >= 0f,
            "the HUD names the room '" + room + "'; throne announced at " + F(throneTime) + " s");

        type.GetMethod("SetMap", any).Invoke(hud, new object[] { true });
        var cam = (Camera)type.GetField("_overview", any).GetValue(hud);
        int ceiling = LayerMask.NameToLayer("Ceiling");
        bool overviewOn = cam != null && cam.enabled && cam.orthographic;
        bool culls = cam != null && ceiling >= 0 && (cam.cullingMask & (1 << ceiling)) == 0;
        Check("hud.overview", overviewOn && culls, "M overview: camera on " + overviewOn + ", orthographic, ceiling layer hidden " + culls);
        type.GetMethod("SetMap", any).Invoke(hud, new object[] { false });
    }

    // the room under the player's feet, recorded when it changes
    static string Track(Transform player, List<string> visited, string current)
    {
        string now = RoomUnder(player);
        if (now != "" && now != current && (visited.Count == 0 || visited[visited.Count - 1] != now)) visited.Add(now);
        return now == "" ? current : now;
    }

    static readonly RaycastHit[] FeetHits = new RaycastHit[16];

    static string RoomUnder(Transform player)
    {
        var feet = player.position + Vector3.up * 0.3f;
        int n = Physics.RaycastNonAlloc(feet, Vector3.down, FeetHits, 1.5f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        string name = "";
        for (int i = 0; i < n; i++)
        {
            var h = FeetHits[i];
            if (h.transform.IsChildOf(player)) continue;
            var room = h.collider.GetComponentInParent<LevelRoom>();
            if (room != null && h.distance < best) { best = h.distance; name = room.roomName; }
        }
        return name;
    }

    // a frame of the walk on the dining hall floor: a floor tile, not the stair, after the arrival door
    static int HallFloorFrame(Transform player)
    {
        var p = player.position;
        if (p.x > 59.4f) return 0;
        if (RoomUnder(player) != "Dining hall") return 0;
        if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var h, 1.5f, ~0, QueryTriggerInteraction.Ignore)) return 0;
        if (h.collider.transform.name != "Floor") return 0;
        return p.x > 50f && p.z > 7.2f ? 0 : 1;
    }

    // turn towards a flat direction and walk forward, with the player's own look and move inputs
    static void Steer(InteractOnE player, FirstPersonController fpc, StarterAssetsInputs inputs, Vector3 flatTo)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        float dyaw = Vector3.SignedAngle(fwd, flatTo.normalized, Vector3.up);
        float rate = Mathf.Clamp(dyaw * 4f, -150f, 150f);
        inputs.LookInput(new Vector2(TurnInput(fpc, rate), 0f));
        inputs.MoveInput(Mathf.Abs(dyaw) < 25f ? new Vector2(0f, 1f) : Vector2.zero);
    }

    static string aimLog = "";

    // aim the camera (the E ray) at a point; true once it is on target
    static bool AimAt(InteractOnE player, FirstPersonController fpc, StarterAssetsInputs inputs, Camera cam, Vector3 target)
    {
        Vector3 to = target - cam.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        float dyaw = Vector3.SignedAngle(fwd, Vector3.ProjectOnPlane(to, Vector3.up).normalized, Vector3.up);
        float pitchNow = Mathf.Asin(Mathf.Clamp(cam.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float pitchWant = Mathf.Asin(Mathf.Clamp(to.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        float dpitch = pitchWant - pitchNow;
        float yawRate = Mathf.Clamp(dyaw * 5f, -150f, 150f);
        float pitchRate = Mathf.Clamp(dpitch * 5f, -120f, 120f);
        // the controller tilts the camera down for a positive look.y, so looking up needs a negative input
        inputs.LookInput(new Vector2(TurnInput(fpc, yawRate), -TurnInput(fpc, pitchRate)));
        inputs.MoveInput(Vector2.zero);
        aimLog = "dyaw " + F(dyaw) + " dpitch " + F(dpitch) + " look (" + F(TurnInput(fpc, yawRate)) + ", " + F(-TurnInput(fpc, pitchRate)) +
            ") camera forward " + V(cam.transform.forward) + " player forward " + V(player.transform.forward) + " at " + V(player.transform.position);
        return Mathf.Abs(dyaw) < 1.5f && Mathf.Abs(dpitch) < 1.5f;
    }

    // degrees per second -> the look value the controller reads (it scales by Time.deltaTime unless the device is a mouse)
    static float TurnInput(FirstPersonController fpc, float degreesPerSecond)
    {
        bool mouse = false;
        var prop = typeof(FirstPersonController).GetProperty("IsCurrentDeviceMouse", BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null) mouse = (bool)prop.GetValue(fpc);
        float speed = Mathf.Max(fpc.RotationSpeed, 0.0001f);
        if (Mathf.Abs(degreesPerSecond) < 0.01f) return 0f;
        // the controller ignores look below 0.1 in length, so a turn is never sent smaller than that
        float v = mouse ? degreesPerSecond * Time.deltaTime / speed : degreesPerSecond / speed;
        if (Mathf.Abs(v) < 0.12f) v = Mathf.Sign(v) * 0.12f;
        return mouse ? Mathf.Clamp(v, -2f, 2f) : v;
    }
}
