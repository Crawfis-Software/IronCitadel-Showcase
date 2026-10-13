using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Geometric audit of the placed room prefabs: floating, sunk, wall-clipping and overlapping props,
    /// unattached wall mounts and hangings, duplicates, props through the lid, and per room the lid and floor
    /// holes, lines of sight to the void and the dark floor. Writes rooms-audit.md / .json and contact sheets of
    /// the worst findings. It adds mesh colliders in memory and never saves the scene.</summary>
    public static class IronCitadelAudit
    {
        const int AllLayers = ~0;
        const QueryTriggerInteraction NoTrig = QueryTriggerInteraction.Ignore;
        static readonly HashSet<string> GroupNames = new HashSet<string> { "Floor", "Walls", "Ceiling", "Furniture", "Clutter", "Wall props", "Lights" };

        enum Kind { Structural, Prop, WallMount, Hanging }

        sealed class Piece
        {
            public int id;
            public GameObject go;
            public string room, space, group, setPiece, source, path;
            public GameObject setPieceGo;
            public Bounds b;
            public bool hasBounds;
            public bool primitive;   // generic mode: a built-in Cube/Plane/... whose GameObject name says what it is (Floor, Wall, Rock)
            public Kind kind;
            public readonly List<Renderer> rs = new List<Renderer>();
            public readonly HashSet<Collider> own = new HashSet<Collider>();
            public float Volume => Mathf.Max(b.size.x, 0.01f) * Mathf.Max(b.size.y, 0.01f) * Mathf.Max(b.size.z, 0.01f);
            public string Label => $"{room}/{space}/{group} {source}" + (setPiece != null ? $" [{setPiece}]" : "") +
                $" at ({b.center.x:0.0}, {b.min.y:0.00}, {b.center.z:0.0}) size {b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00}";
        }

        sealed class Finding
        {
            public string cat; public float mag; public Piece p, other; public string detail; public Vector3 at; public float ext; public string shot;
        }

        sealed class RoomStat
        {
            public string room; public int props, lights, floorPts, freePts, lidHoles, floorHoles, escapes, dark; public float lightSum;
            public readonly Dictionary<string, int> scaled = new Dictionary<string, int>();
            public readonly List<string> lidHoleAt = new List<string>(), floorHoleAt = new List<string>(), escapeAt = new List<string>();
        }

        static readonly Regex RxStructural = new Regex(@"^sm_env_(wall|tiles|tile_|floor|ceiling|pillar|column|stairs|stair_|arch|door|beam|roof|cave|battlement|railing|fence|balustrade)", RegexOptions.IgnoreCase);
        static readonly Regex RxOpening = new Regex(@"door|arch|frame|window|gate|portcullis", RegexOptions.IgnoreCase);
        static readonly Regex RxHanging = new Regex(@"chandelier|hanging|_hang|cage|rope|chain|meat|herb|lantern_ceiling", RegexOptions.IgnoreCase);
        static readonly Regex RxWallMount = new Regex(@"torch(?!_stand)|sconce|banner|flag(?!_stand|pole)|shield|sign|painting|portrait|tapestry|antler|trophy|wall|plaque|mirror|manacle|shackle|cobweb|web|hook|curtain", RegexOptions.IgnoreCase);
        static readonly Regex RxRug = new Regex(@"rug|carpet|runner|mat_", RegexOptions.IgnoreCase);
        static readonly Regex RxContainer = new Regex(@"bookcase|bookshelf|shelf|shelves|rack|cabinet|cupboard|crate|barrel|cart|wagon|bed|chest|pot_|pot$|cauldron|basket|bucket|display|stand|bin|trough|wheelbarrow|coffin|sarcophag|cage|brazier|fireplace|hearth|forge|oven|sink|tub|bath|box|sack|pile", RegexOptions.IgnoreCase);
        static readonly Regex RxSeat = new Regex(@"chair|stool|bench(?!_work)|seat|throne", RegexOptions.IgnoreCase);
        static readonly Regex RxTable = new Regex(@"table|desk|workbench|bench_work|counter", RegexOptions.IgnoreCase);
        static readonly Regex RxBook = new Regex(@"book|paper|scroll|tome", RegexOptions.IgnoreCase);

        // ---- generic mode (-generic): no room prefab hierarchy; rooms from level.json cells, structure by name, no shots
        static readonly Regex RxCharacter = new Regex(@"^(sm_)?chr_|character", RegexOptions.IgnoreCase);
        static readonly Regex RxGenCeiling = new Regex(@"ceiling", RegexOptions.IgnoreCase);
        static readonly Regex RxGenFloor = new Regex(@"^sm_env_(dwarf_|basement_)?(tiles|tile_|floor|stairs|stair_|steps)", RegexOptions.IgnoreCase);
        static readonly Regex RxGenWall = new Regex(@"^sm_env_(dwarf_|basement_)?(wall|door|arch|pillar|column|beam|support|railing|fence|balustrade|battlement|roof|cave|trim|window)", RegexOptions.IgnoreCase);
        // built-in primitives a cold build uses as structure, by GameObject name (Opus: Floor, Wall, Lintel, UnderStair, TunnelRoof, Rock; Fable: Rock)
        static readonly Regex RxPrimFloor = new Regex(@"^(floor|platform|deck|ledge|step|stair|landing|ground)", RegexOptions.IgnoreCase);
        static readonly Regex RxPrimCeiling = new Regex(@"^(ceiling|roof|tunnelroof|lid|cap)", RegexOptions.IgnoreCase);
        static readonly Regex RxPrimRock = new Regex(@"^(rock|cave|fill|surround|bedrock)", RegexOptions.IgnoreCase);
        static readonly Regex RxPrimWall = new Regex(@"^(wall|lintel|understair|pillar|column|beam|post|jamb|doorframe)", RegexOptions.IgnoreCase);

        sealed class GenericSpec
        {
            public string levelPath;
            public float tileM = 45f;
            public readonly List<(string id, int r, int c)> rooms = new List<(string, int, int)>();
            public int primitivesSkipped, primitivesStructural, charactersSkipped, outsideRooms;
            public string RoomAt(Vector3 w)
            {
                int c = Mathf.FloorToInt(w.x / tileM), r = Mathf.FloorToInt(-w.z / tileM);
                foreach (var x in rooms) if (x.r == r && x.c == c) return x.id;
                return "outside";
            }
            public Vector3 Centre(int r, int c) => new Vector3((c + 0.5f) * tileM, 0f, -(r + 0.5f) * tileM);
        }

#pragma warning disable 649
        [Serializable] sealed class LevelJ { public WorldJ world; public RoomJ[] rooms; }
        [Serializable] sealed class WorldJ { public float tile_m; }
        [Serializable] sealed class RoomJ { public string id; public int[] cell; }
#pragma warning restore 649

        static GenericSpec LoadLevel(string path)
        {
            var lj = JsonUtility.FromJson<LevelJ>(File.ReadAllText(path));
            if (lj == null || lj.rooms == null || lj.rooms.Length == 0) throw new Exception("no rooms in " + path);
            var spec = new GenericSpec { levelPath = path };
            if (lj.world != null && lj.world.tile_m > 0f) spec.tileM = lj.world.tile_m;
            foreach (var r in lj.rooms) if (r.cell != null && r.cell.Length == 2) spec.rooms.Add((r.id, r.cell[0], r.cell[1]));
            return spec;
        }

        static string Arg(string name, string dflt)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return dflt;
        }

        /// <summary>-executeMethod entry. -scene, -out, -shots override the defaults; -generic (with -level) audits any scene by
        /// level.json cells and piece names. Exits in batchmode (0 ok, 1 exception).</summary>
        public static void Run()
        {
            int code = 0;
            try
            {
                if (Environment.GetCommandLineArgs().Contains("-generic"))
                    RunCore(Arg("-scene", IronCitadelRoomsAssembler.ScenePath), Arg("-out", IronCitadelRoomsAssembler.OutRoot + "/logs/audit/generic"), 0,
                        LoadLevel(Arg("-level", "Assets/IronCitadel/level.json")));
                else
                    RunCore(Arg("-scene", IronCitadelRoomsAssembler.ScenePath), Arg("-out", IronCitadelRoomsAssembler.OutRoot + "/logs/audit"),
                        int.Parse(Arg("-shots", "72"), CultureInfo.InvariantCulture));
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            Debug.Log($"[IronCitadelAudit] done, exit {code}");
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("Iron Citadel/Rooms/Audit props")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunCore(SceneManager.GetActiveScene().path, IronCitadelRoomsAssembler.OutRoot + "/logs/audit", 72);
        }

        static void RunCore(string scenePath, string outDir, int maxShots, GenericSpec spec = null)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (SceneManager.GetActiveScene().path != scenePath) EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var root = spec == null ? scene.GetRootGameObjects().FirstOrDefault(g => g.name == "IronCitadel") : null;
            if (spec == null && root == null) throw new Exception("no IronCitadel root in " + scenePath);
            var scan = spec == null ? new[] { root } : scene.GetRootGameObjects();
            Directory.CreateDirectory(outDir);
            var log = new StringBuilder();
            var play = root != null ? root.transform.Find("Play") : null;
            bool playWas = play != null && play.gameObject.activeSelf;
            if (playWas) play.gameObject.SetActive(false);
            var markers = root != null ? root.transform.Find("Markers") : null;
            bool markWas = markers != null && markers.gameObject.activeSelf;
            if (markWas) markers.gameObject.SetActive(false);
            var added = new List<Collider>();
            bool backfacesWas = Physics.queriesHitBackfaces;
            if (spec != null) Physics.queriesHitBackfaces = true;   // a ray started inside a one-sided shelf board sees the board, not the floor below it
            s_verts.Clear();
            try
            {
                // 1. a mesh collider on everything drawn, so the rays see the real surfaces (shelves, not bounding boxes)
                foreach (var g in scan)
                    foreach (var mf in g.GetComponentsInChildren<MeshFilter>(false))
                    {
                        if (mf.sharedMesh == null || mf.GetComponent<MeshCollider>() != null) continue;
                        var r = mf.GetComponent<MeshRenderer>();
                        if (r == null || !r.enabled) continue;
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        added.Add(mc);
                    }
                Physics.SyncTransforms();
                log.AppendLine($"mesh colliders added: {added.Count}");

                // 2. the pieces: one per Synty prefab instance (or per loose renderer)
                var pieces = new List<Piece>();
                var byGo = new Dictionary<GameObject, Piece>();
                var roomsT = root != null ? root.transform.Find("Rooms") : null;
                var featT = root != null ? root.transform.Find("Features") : null;
                var tops = new List<Transform>();
                if (roomsT != null) tops.AddRange(roomsT.Cast<Transform>());
                if (featT != null) tops.AddRange(featT.Cast<Transform>());
                var colliderPiece = new Dictionary<Collider, Piece>();
                if (spec == null)
                {
                    foreach (var top in tops) Collect(top, pieces, byGo);
                    foreach (var top in tops)
                        foreach (var c in top.GetComponentsInChildren<Collider>(false))
                        {
                            if (c.isTrigger) continue;
                            var p = PieceAbove(c.transform, top, byGo);
                            if (p != null) { colliderPiece[c] = p; p.own.Add(c); }
                        }
                }
                else
                {
                    foreach (var g in scan) CollectGeneric(g.transform, pieces, byGo, spec);
                    foreach (var p in pieces)
                    {
                        p.room = spec.RoomAt(p.b.center);
                        if (p.room == "outside") spec.outsideRooms++;
                        p.group = GenericGroup(p);
                        p.kind = Classify(p);
                    }
                    foreach (var g in scan)
                        foreach (var c in g.GetComponentsInChildren<Collider>(false))
                        {
                            if (c.isTrigger) continue;
                            var p = PieceAbove(c.transform, null, byGo);
                            if (p != null) { colliderPiece[c] = p; p.own.Add(c); }
                        }
                    log.AppendLine($"generic: level {spec.levelPath}, {spec.rooms.Count} rooms on {spec.tileM} m cells; structure by name: {pieces.Count(p => p.group == "Floor")} floor, {pieces.Count(p => p.group == "Walls")} wall, {pieces.Count(p => p.group == "Ceiling")} ceiling, {pieces.Count(p => p.group == "Rock")} rock pieces; {spec.primitivesStructural} built-in primitives kept as structure by name, {spec.primitivesSkipped} built-in primitive renderers and {spec.charactersSkipped} character renderers skipped; {spec.outsideRooms} pieces outside the room cells (not tested); backface hits on");
                }
                log.AppendLine($"pieces: {pieces.Count} ({pieces.Count(p => p.kind == Kind.Structural)} structural, {pieces.Count(p => p.kind == Kind.Prop)} props, {pieces.Count(p => p.kind == Kind.WallMount)} wall mounts, {pieces.Count(p => p.kind == Kind.Hanging)} hangings) in {sw.Elapsed.TotalSeconds:0}s");

                // 3. per-piece tests
                var findings = new List<Finding>();
                var walls = pieces.Where(p => p.kind == Kind.Structural && p.group == "Walls" && !RxOpening.IsMatch(p.source) && p.hasBounds).ToList();
                var props = pieces.Where(p => p.kind != Kind.Structural && p.hasBounds && (spec == null || p.room != "outside")).ToList();
                foreach (var p in props)
                {
                    float gap = MinGap(p, out int hits, out int n);
                    if (p.kind == Kind.Prop)
                    {
                        if (hits == 0) Add(findings, "void-below", 9f, p, "nothing under it within 6 m");
                        else if (spec == null ? gap > 0.04f : gap > 0.05f)
                        {
                            // generic: a piece within 0.05 m of anything (its shelf, its table, a pile) is seated, not floating
                            var touch = Touching(p, 0.05f, colliderPiece, spec == null);
                            if (touch != null) Add(findings, "floating-touching", gap, p, $"{gap:0.00} m above the surface below, touching {touch.source}");
                            else Add(findings, "floating", gap, p, $"{gap:0.00} m above the surface below ({hits}/{n} rays hit)");
                        }
                    }
                    else
                    {
                        bool supported = hits > 0 && gap <= 0.04f;
                        if (!supported && Touching(p, 0.12f, colliderPiece, false) == null)
                            Add(findings, "unattached", hits > 0 ? Mathf.Min(gap, 5f) : 6f, p,
                                (p.kind == Kind.Hanging ? "hangs from nothing" : "mounted on nothing") + (hits > 0 ? $"; {gap:0.00} m above the surface below" : "; nothing below within 6 m"));
                    }
                    float floorTop = FloorTopUnder(p, colliderPiece);
                    if (p.kind == Kind.Prop && !float.IsNegativeInfinity(floorTop) && floorTop - p.b.min.y > 0.05f)
                        Add(findings, "sunk", floorTop - p.b.min.y, p, $"{floorTop - p.b.min.y:0.00} m below the floor top");
                    // the lid or a deck above
                    var up = Physics.RaycastAll(new Vector3(p.b.center.x, p.b.min.y + 0.01f, p.b.center.z), Vector3.up, 60f, AllLayers, NoTrig);
                    float lid = float.PositiveInfinity;
                    foreach (var h in up)
                        if (colliderPiece.TryGetValue(h.collider, out var q) && q != p && q.kind == Kind.Structural && (q.group == "Ceiling" || q.group == "Floor") && h.point.y > p.b.min.y + 0.3f)
                            lid = Mathf.Min(lid, h.point.y);
                    if (!float.IsPositiveInfinity(lid) && p.b.max.y - lid > 0.05f)
                        Add(findings, "through-lid", p.b.max.y - lid, p, $"top {p.b.max.y - lid:0.00} m above the lid or deck at y {lid:0.00}");
                    // walls
                    var shrunk = Shrink(p.b, 0.03f);
                    Piece worst = null; int worstIn = 0, total = 0; float worstDepth = 0f;
                    foreach (var w in walls)
                    {
                        if (!w.b.Intersects(shrunk)) continue;
                        var wb = Shrink(w.b, 0.04f);
                        if (wb.size.x <= 0f || wb.size.y <= 0f || wb.size.z <= 0f) continue;
                        int cin = CountInside(p, wb, out int tot, out float depth);
                        total = tot;
                        if (cin > worstIn) { worstIn = cin; worst = w; worstDepth = depth + 0.04f; }
                    }
                    if (worst != null && total > 0)
                    {
                        float f = worstIn / (float)total;
                        bool flag = p.kind == Kind.Prop ? (f >= 0.03f || worstIn >= 8) && worstDepth > 0.08f : f >= 0.6f;
                        if (flag) Add2(findings, "wall-clip", worstDepth, p, worst, $"{f:P0} of its vertices inside {worst.source}, up to {worstDepth:0.00} m deep");
                    }
                }
                log.AppendLine($"per-piece tests done in {sw.Elapsed.TotalSeconds:0}s");

                // 4. prop against prop
                var sorted = props.OrderBy(p => p.b.min.x).ToList();
                var dupSeen = new HashSet<string>();
                for (int i = 0; i < sorted.Count; i++)
                {
                    var a = sorted[i];
                    for (int j = i + 1; j < sorted.Count; j++)
                    {
                        var c = sorted[j];
                        if (c.b.min.x > a.b.max.x) break;
                        if (!a.b.Intersects(c.b)) continue;
                        var omin = Vector3.Max(a.b.min, c.b.min);
                        var omax = Vector3.Min(a.b.max, c.b.max);
                        var osz = omax - omin;
                        if (osz.x <= 0f || osz.y <= 0f || osz.z <= 0f) continue;
                        var small = a.Volume <= c.Volume ? a : c;
                        var big = small == a ? c : a;
                        if (RxRug.IsMatch(a.source) && RxRug.IsMatch(c.source))
                        {
                            float area = osz.x * osz.z, sa = Mathf.Max(small.b.size.x * small.b.size.z, 0.01f);
                            if (area > 0.1f * sa) Add2(findings, "rug-overlap", area / sa, small, big, $"{area / sa:P0} of the smaller rug under {big.source}");
                            continue;
                        }
                        float ratio = osz.x * osz.y * osz.z / small.Volume;
                        if (ratio < 0.2f) continue;
                        if (a.source == c.source && (a.b.center - c.b.center).magnitude < 0.03f)
                        {
                            if (dupSeen.Add(a.source + a.b.center.ToString("F1"))) Add2(findings, "duplicate", 1f, small, big, "the same prefab twice at the same place");
                            continue;
                        }
                        if (RxContainer.IsMatch(big.source)) continue;
                        if (RxSeat.IsMatch(small.source) && RxTable.IsMatch(big.source)) continue;
                        if (RxSeat.IsMatch(big.source) && RxTable.IsMatch(small.source)) continue;
                        int cin = CountInside(small, Shrink(big.b, 0.02f), out int tot, out float depth);
                        if (tot == 0) continue;
                        float g = cin / (float)tot;
                        if (g >= 0.3f && depth + 0.02f > 0.05f)
                            Add2(findings, "overlap", g, small, big, $"{g:P0} of {small.source}'s vertices inside {big.source}'s box, {depth + 0.02f:0.00} m deep" + (a.setPieceGo != null && a.setPieceGo == c.setPieceGo ? " (same set piece)" : ""));
                    }
                }
                log.AppendLine($"pair tests done in {sw.Elapsed.TotalSeconds:0}s");

                // 5. per room: scale oddities, the lid, the floor, the void, the dark
                var roomStats = new List<RoomStat>();
                var roomCentres = new List<(string room, Vector3 c0, List<Light> lights)>();
                if (spec == null)
                {
                    if (roomsT != null)
                        foreach (Transform top in roomsT)
                            roomCentres.Add((top.name, top.position, top.GetComponentsInChildren<Light>(false).Where(l => l.enabled && l.type != LightType.Directional).ToList()));
                }
                else
                {
                    var all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.gameObject.activeInHierarchy && l.type != LightType.Directional).ToList();
                    foreach (var rm in spec.rooms)
                        roomCentres.Add((rm.id, spec.Centre(rm.r, rm.c), all.Where(l => spec.RoomAt(l.transform.position) == rm.id).ToList()));
                }
                    foreach (var rc in roomCentres)
                    {
                        var st = new RoomStat { room = rc.room };
                        var rp = props.Where(p => p.room == rc.room).ToList();
                        st.props = rp.Count;
                        foreach (var p in rp)
                        {
                            var s = p.go.transform.lossyScale;
                            float mx = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))), mn = Mathf.Min(Mathf.Abs(s.x), Mathf.Min(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                            if (mx > 1.02f || mn < 0.98f)
                            {
                                string key = $"{p.source} x({s.x:0.00}, {s.y:0.00}, {s.z:0.00})";
                                st.scaled[key] = st.scaled.TryGetValue(key, out var k) ? k + 1 : 1;
                            }
                        }
                        var lights = rc.lights;
                        st.lights = lights.Count;
                        st.lightSum = lights.Sum(l => l.intensity);
                        var c0 = rc.c0;
                        float half = spec == null ? 22.5f : spec.tileM / 2f;
                        // generic: sample off the half metre, or a build whose 5 m tiles seam at x.5 (Opus) has every fifth ray fall between two tiles
                        float off = spec == null ? 0.5f : 0.37f;
                        for (float x = c0.x - half + off; x < c0.x + half; x += 1f)
                            for (float z = c0.z - half + off; z < c0.z + half; z += 1f)
                            {
                                var hs = Physics.RaycastAll(new Vector3(x, 40f, z), Vector3.down, 45f, AllLayers, NoTrig);
                                float floorY = float.NegativeInfinity, ceilY = float.NegativeInfinity;
                                bool anyCeil = false;
                                foreach (var h in hs)
                                {
                                    if (!colliderPiece.TryGetValue(h.collider, out var q)) continue;
                                    if (q.group == "Floor") floorY = Mathf.Max(floorY, h.point.y);
                                    else if (q.group == "Ceiling") { anyCeil = true; ceilY = Mathf.Max(ceilY, h.point.y); }
                                }
                                if (float.IsNegativeInfinity(floorY))
                                {
                                    if (anyCeil)
                                    {
                                        st.floorHoles++;
                                        if (st.floorHoleAt.Count < 6) st.floorHoleAt.Add($"({x:0}, {z:0})");
                                        if (spec != null && st.floorHoleAt.Count <= 3)
                                            log.AppendLine($"floor hole {st.room} ({x:0.0}, {z:0.0}): " + string.Join("; ", hs.OrderByDescending(h => h.point.y).Select(h => colliderPiece.TryGetValue(h.collider, out var q) ? $"{q.source}/{q.group}@{h.point.y:0.00}" : $"{h.collider.name}(unmapped)@{h.point.y:0.00}")));
                                    }
                                    continue;
                                }
                                st.floorPts++;
                                if (!anyCeil || ceilY < floorY) { st.lidHoles++; if (st.lidHoleAt.Count < 6) st.lidHoleAt.Add($"({x:0}, {floorY:0.0}, {z:0})"); }
                                var o = new Vector3(x, floorY + 1.2f, z);
                                if (Physics.CheckSphere(o, 0.2f, AllLayers, NoTrig)) continue;
                                st.freePts++;
                                for (int k = 0; k < 16; k++)
                                {
                                    float ang = k * 22.5f * Mathf.Deg2Rad;
                                    var dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                                    if (!Physics.Raycast(o, dir, 150f, AllLayers, NoTrig))
                                    {
                                        st.escapes++;
                                        if (st.escapeAt.Count < 6) st.escapeAt.Add($"({x:0}, {floorY:0.0}, {z:0}) toward {k * 22.5f:0} deg");
                                        break;
                                    }
                                }
                                bool lit = false;
                                foreach (var l in lights)
                                {
                                    var lp = l.transform.position;
                                    var d = lp - o;
                                    float dist = d.magnitude;
                                    if (dist > Mathf.Min(l.range, 9f)) continue;
                                    if (dist < 0.3f || !Physics.Raycast(o, d / dist, dist - 0.25f, AllLayers, NoTrig)) { lit = true; break; }
                                }
                                if (!lit) st.dark++;
                            }
                        roomStats.Add(st);
                    }
                log.AppendLine($"room grids done in {sw.Elapsed.TotalSeconds:0}s");

                // 6. shots of the worst, four to a sheet
                var shots = PickShots(findings, maxShots);
                string tmp = Path.Combine(outDir, "_tiles");
                Directory.CreateDirectory(tmp);
                var sheetLines = new StringBuilder();
                for (int s = 0; s < shots.Count; s += 4)
                {
                    var files = new List<string>();
                    for (int t = 0; t < 4 && s + t < shots.Count; t++)
                    {
                        var f = shots[s + t];
                        string tile = Path.Combine(tmp, $"f{s + t:000}.png");
                        string how = ShotOf(f, tile, colliderPiece);
                        files.Add(tile);
                        f.shot = $"sheet {s / 4 + 1:00} tile {t + 1}";
                        string corner = t == 0 ? "top left" : t == 1 ? "top right" : t == 2 ? "bottom left" : "bottom right";
                        sheetLines.AppendLine($"findings-{s / 4 + 1:00}.png tile {t + 1} ({corner}): [{f.cat}] {f.p.Label}: {f.detail} | {f.p.path} | camera {how}");
                    }
                    Sheet(files, Path.Combine(outDir, $"findings-{s / 4 + 1:00}.png"), 2);
                }
                // room panoramas: the eye point of captures.txt and the room's centre, four yaws each, night and lit
                var eyes = spec == null ? ReadEyes(IronCitadelRoomsAssembler.OutRoot + "/captures/rooms/captures.txt") : null;
                if (roomsT != null)
                    foreach (Transform top in roomsT)
                    {
                        var spots = new List<Vector3>();
                        if (eyes.TryGetValue(top.name, out var e)) spots.Add(e);
                        var rp = props.Where(p => p.room == top.name && p.hasBounds).ToList();
                        if (rp.Count > 0)
                        {
                            var bb = rp[0].b;
                            foreach (var p in rp) bb.Encapsulate(p.b);
                            var ctr = new Vector3(bb.center.x, 0f, bb.center.z);
                            var cand = Free(ctr, 6f);
                            if (cand.HasValue) spots.Add(cand.Value);
                        }
                        foreach (bool rig in new[] { false, true })
                        {
                            var files = new List<string>();
                            for (int si = 0; si < spots.Count; si++)
                                for (int k = 0; k < 4; k++)
                                {
                                    string tile = Path.Combine(tmp, $"r-{top.name}-{si}-{k}-{(rig ? "lit" : "night")}.png");
                                    IronCitadelCapture.Perspective(tile, spots[si], k * 90f, 6f, 70f, 640, 360, rig);
                                    files.Add(tile);
                                }
                            if (files.Count > 0) Sheet(files, Path.Combine(outDir, $"room-{top.name}-{(rig ? "lit" : "night")}.png"), 4);
                        }
                        sheetLines.AppendLine($"room-{top.name}-*.png: rows are viewpoints {string.Join("; ", spots.Select(v => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})"))}, columns yaw 0 (north), 90 (east), 180 (south), 270 (west)");
                    }
                File.WriteAllText(Path.Combine(outDir, "sheets.txt"), sheetLines.ToString());
                try { Directory.Delete(tmp, true); } catch (Exception) { }
                log.AppendLine($"shots done in {sw.Elapsed.TotalSeconds:0}s");

                WriteReport(outDir, scenePath, pieces, props, findings, roomStats, log.ToString(), sw.Elapsed, spec);
                Debug.Log("[IronCitadelAudit] " + log);
            }
            finally
            {
                foreach (var c in added) if (c != null) Object.DestroyImmediate(c);
                if (playWas) play.gameObject.SetActive(true);
                if (markWas) markers.gameObject.SetActive(true);
                Physics.queriesHitBackfaces = backfacesWas;
                Physics.SyncTransforms();
            }
        }

        // ------------------------------------------------------------------ generic pieces

        /// <summary>One piece per outermost Synty prefab instance (else per loose renderer); built-in primitives (markers, rock caps) skipped.</summary>
        static void CollectGeneric(Transform top, List<Piece> pieces, Dictionary<GameObject, Piece> byGo, GenericSpec spec)
        {
            foreach (var r in top.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh) ?? "";
                bool primitive = meshPath.StartsWith("Library/unity default resources", StringComparison.OrdinalIgnoreCase);
                if (primitive && !(RxPrimFloor.IsMatch(r.name) || RxPrimCeiling.IsMatch(r.name) || RxPrimWall.IsMatch(r.name) || RxPrimRock.IsMatch(r.name))) { spec.primitivesSkipped++; continue; }
                GameObject go = null; string src = null;
                if (primitive) { go = r.gameObject; src = r.name; spec.primitivesStructural++; }
                else
                    for (var t = r.transform; t != null; t = t.parent)
                    {
                        if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                        string ap = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) ?? "";
                        if (ap.Contains("/Synty/")) { go = t.gameObject; src = Path.GetFileNameWithoutExtension(ap); }
                        else if (go != null) break;
                    }
                if (go == null) { go = r.gameObject; src = mf.sharedMesh.name; }
                // a character (a defender stand-in) is a marker like the capsules: its body is skinned, so its hair or weapon would read as floating
                if (RxCharacter.IsMatch(src) || RxCharacter.IsMatch(go.name)) { spec.charactersSkipped++; continue; }
                if (!byGo.TryGetValue(go, out var p))
                {
                    p = new Piece { id = pieces.Count, go = go, source = src, space = "-", group = "-", room = "-", primitive = primitive };
                    var names = new List<string>();
                    for (var t = go.transform; t != null; t = t.parent) names.Add(t.name);
                    names.Reverse();
                    p.path = string.Join("/", names);
                    byGo[go] = p;
                    pieces.Add(p);
                }
                p.rs.Add(r);
                if (!p.hasBounds) { p.b = r.bounds; p.hasBounds = true; } else p.b.Encapsulate(r.bounds);
            }
        }

        /// <summary>Floor, Walls or Ceiling by the Synty name; a floor tile turned over, or 7 m up, is a ceiling.</summary>
        static string GenericGroup(Piece p)
        {
            string s = p.source;
            if (p.primitive) return RxPrimRock.IsMatch(s) ? "Rock" : RxPrimCeiling.IsMatch(s) ? "Ceiling" : RxPrimFloor.IsMatch(s) ? "Floor" : "Walls";
            if (RxGenCeiling.IsMatch(s)) return "Ceiling";
            if (RxGenFloor.IsMatch(s)) return p.go.transform.up.y < -0.5f || p.b.min.y >= 7f ? "Ceiling" : "Floor";
            if (RxGenWall.IsMatch(s)) return "Walls";
            return "-";
        }

        // ------------------------------------------------------------------ pieces

        static void Collect(Transform top, List<Piece> pieces, Dictionary<GameObject, Piece> byGo)
        {
            foreach (var r in top.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var go = PrefabUtility.GetNearestPrefabInstanceRoot(r.gameObject);
                if (go == null || go == top.gameObject || !go.transform.IsChildOf(top)) go = r.gameObject;
                else
                {
                    string ap = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) ?? "";
                    if (ap.Contains("/SetPieces/") || ap.Contains("/Rooms/")) go = r.gameObject;
                }
                if (!byGo.TryGetValue(go, out var p))
                {
                    p = new Piece { id = pieces.Count, go = go, room = top.name };
                    string src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                    p.source = string.IsNullOrEmpty(src) || src.Contains("/SetPieces/") || src.Contains("/Rooms/") ? mf.sharedMesh.name : Path.GetFileNameWithoutExtension(src);
                    var names = new List<string>();
                    for (var t = go.transform; t != null && t != top; t = t.parent)
                    {
                        names.Add(t.name);
                        if (t != go.transform)
                        {
                            if (p.group == null && GroupNames.Contains(t.name)) p.group = t.name;
                            if (p.setPiece == null && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                            {
                                string ap = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) ?? "";
                                if (ap.Contains("/SetPieces/")) { p.setPiece = Path.GetFileNameWithoutExtension(ap); p.setPieceGo = t.gameObject; }
                            }
                        }
                        if (t.parent == top) p.space = t.name;
                    }
                    names.Reverse();
                    p.path = string.Join("/", names);
                    if (p.group == null) p.group = "-";
                    if (p.space == null) p.space = "-";
                    p.kind = Classify(p);
                    byGo[go] = p;
                    pieces.Add(p);
                }
                p.rs.Add(r);
                if (!p.hasBounds) { p.b = r.bounds; p.hasBounds = true; } else p.b.Encapsulate(r.bounds);
            }
        }

        static Piece PieceAbove(Transform t, Transform top, Dictionary<GameObject, Piece> byGo)
        {
            for (var x = t; x != null && x != top; x = x.parent) if (byGo.TryGetValue(x.gameObject, out var p)) return p;
            return null;
        }

        static Kind Classify(Piece p)
        {
            if (p.group == "Floor" || p.group == "Walls" || p.group == "Ceiling" || p.group == "Rock") return Kind.Structural;
            string s = p.source;
            if ((p.group == "-" || p.group == "Lights") && RxStructural.IsMatch(s)) return Kind.Structural;
            if (RxHanging.IsMatch(s)) return Kind.Hanging;
            if (p.group == "Wall props" || RxWallMount.IsMatch(s)) return Kind.WallMount;
            return Kind.Prop;
        }

        // ------------------------------------------------------------------ tests

        /// <summary>The smallest drop from the piece's bottom face to the first other surface below, sampled on a grid.</summary>
        static float MinGap(Piece p, out int hits, out int samples)
        {
            var b = p.b;
            float y0 = b.min.y + 0.02f, best = float.PositiveInfinity;
            hits = 0; samples = 0;
            int nx = Mathf.Clamp(Mathf.CeilToInt(b.size.x / 0.3f) + 1, 2, 6), nz = Mathf.Clamp(Mathf.CeilToInt(b.size.z / 0.3f) + 1, 2, 6);
            float ix = Mathf.Min(0.15f * b.size.x, 0.05f), iz = Mathf.Min(0.15f * b.size.z, 0.05f);
            var pts = new List<Vector3>();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    pts.Add(new Vector3(Mathf.Lerp(b.min.x + ix, b.max.x - ix, i / (float)(nx - 1)), y0, Mathf.Lerp(b.min.z + iz, b.max.z - iz, j / (float)(nz - 1))));
            pts.Add(new Vector3(b.center.x, y0, b.center.z));
            foreach (var o in pts)
            {
                samples++;
                float d = float.PositiveInfinity;
                foreach (var h in Physics.RaycastAll(o, Vector3.down, 6f, AllLayers, NoTrig))
                    if (!p.own.Contains(h.collider)) d = Mathf.Min(d, h.distance);
                if (float.IsPositiveInfinity(d)) continue;
                hits++;
                best = Mathf.Min(best, d - 0.02f);
            }
            return Mathf.Max(best, 0f);
        }

        /// <summary>The highest Floor-group surface under the piece's centre, from just above its top.</summary>
        static float FloorTopUnder(Piece p, Dictionary<Collider, Piece> map)
        {
            float top = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(p.b.center.x, p.b.max.y + 0.05f, p.b.center.z), Vector3.down, p.b.size.y + 30f, AllLayers, NoTrig))
                if (map.TryGetValue(h.collider, out var q) && q != p && q.group == "Floor") top = Mathf.Max(top, h.point.y);
            return top;
        }

        static Piece Touching(Piece p, float margin, Dictionary<Collider, Piece> map, bool structuralOnly)
        {
            Piece best = null;
            foreach (var c in Physics.OverlapBox(p.b.center, p.b.extents + Vector3.one * margin, Quaternion.identity, AllLayers, NoTrig))
            {
                if (!map.TryGetValue(c, out var q) || q == p) continue;
                if (q.kind == Kind.Structural) return q;
                if (!structuralOnly && best == null) best = q;
            }
            return best;
        }

        static Bounds Shrink(Bounds b, float m) => new Bounds(b.center, b.size - Vector3.one * (2f * m));

        static readonly Dictionary<Mesh, Vector3[]> s_verts = new Dictionary<Mesh, Vector3[]>();

        static Vector3[] Verts(Mesh m)
        {
            if (s_verts.TryGetValue(m, out var v)) return v;
            try
            {
                var all = m.vertices;
                if (all.Length > 1200)
                {
                    int step = Mathf.CeilToInt(all.Length / 1200f);
                    v = new Vector3[(all.Length + step - 1) / step];
                    for (int i = 0, j = 0; i < all.Length && j < v.Length; i += step, j++) v[j] = all[i];
                }
                else v = all;
            }
            catch (Exception)
            {
                var b = m.bounds;
                v = new[]
                {
                    b.min, b.max, b.center,
                    new Vector3(b.min.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.min.z),
                    new Vector3(b.max.x, b.max.y, b.min.z), new Vector3(b.max.x, b.min.y, b.max.z), new Vector3(b.min.x, b.max.y, b.max.z),
                };
            }
            s_verts[m] = v;
            return v;
        }

        /// <summary>How many of the piece's (sampled) mesh vertices lie inside the box, and the deepest one's distance from the box's faces.</summary>
        static int CountInside(Piece p, Bounds box, out int total, out float maxDepth)
        {
            total = 0; maxDepth = 0f;
            int inside = 0;
            foreach (var r in p.rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var verts = Verts(mf.sharedMesh);
                var m = r.transform.localToWorldMatrix;
                foreach (var v in verts)
                {
                    total++;
                    var w = m.MultiplyPoint3x4(v);
                    if (!box.Contains(w)) continue;
                    inside++;
                    float d = Mathf.Min(Mathf.Min(w.x - box.min.x, box.max.x - w.x), Mathf.Min(w.z - box.min.z, box.max.z - w.z));
                    d = Mathf.Min(d, Mathf.Min(w.y - box.min.y, box.max.y - w.y));
                    if (d > maxDepth) maxDepth = d;
                }
            }
            return inside;
        }

        static void Add(List<Finding> fs, string cat, float mag, Piece p, string detail)
            => fs.Add(new Finding { cat = cat, mag = mag, p = p, detail = detail, at = p.b.center, ext = Mathf.Max(p.b.extents.x, Mathf.Max(p.b.extents.y, p.b.extents.z)) });

        static void Add2(List<Finding> fs, string cat, float mag, Piece p, Piece q, string detail)
        {
            var bb = p.b;
            bb.Encapsulate(q.b);
            fs.Add(new Finding { cat = cat, mag = mag, p = p, other = q, detail = detail, at = p.b.center, ext = Mathf.Max(bb.extents.x, Mathf.Max(bb.extents.y, bb.extents.z)) });
        }

        // ------------------------------------------------------------------ shots

        static readonly Dictionary<string, int> ShotQuota = new Dictionary<string, int>
        {
            { "floating", 28 }, { "void-below", 6 }, { "floating-touching", 6 }, { "sunk", 8 }, { "wall-clip", 10 }, { "overlap", 10 },
            { "unattached", 8 }, { "through-lid", 4 }, { "duplicate", 2 }, { "rug-overlap", 4 },
        };

        static List<Finding> PickShots(List<Finding> findings, int max)
        {
            var picked = new List<Finding>();
            var perKey = new Dictionary<string, int>();
            foreach (var cat in ShotQuota.Keys)
            {
                int n = 0;
                foreach (var f in findings.Where(f => f.cat == cat).OrderByDescending(f => f.mag))
                {
                    if (n >= ShotQuota[cat]) break;
                    string key = f.cat + "|" + f.p.room + "|" + f.p.source + "|" + (f.p.setPiece ?? "");
                    perKey.TryGetValue(key, out int k);
                    if (k >= 2) continue;
                    perKey[key] = k + 1;
                    picked.Add(f);
                    n++;
                }
            }
            if (picked.Count > max) picked = picked.Take(max).ToList();
            return picked;
        }

        /// <summary>A lit close-up of the finding with a magenta box round the piece and a dot under its bottom centre.</summary>
        static string ShotOf(Finding f, string path, Dictionary<Collider, Piece> map)
        {
            var b = f.p.b;
            var c = f.other != null ? f.at : b.center;
            float ext = Mathf.Max(f.ext, 0.15f);
            var marker = Marker(b, f.cat == "floating" || f.cat == "floating-touching" || f.cat == "void-below");
            try
            {
                string how = null;
                Vector3 eye = Vector3.zero;
                foreach (float d in new[] { Mathf.Clamp(ext * 2.4f + 0.9f, 1.3f, 9f), Mathf.Clamp(ext * 1.4f + 0.6f, 0.9f, 6f) })
                {
                    for (int k = 0; k < 8 && how == null; k++)
                    {
                        float yaw = k * 45f * Mathf.Deg2Rad;
                        var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                        float rise = Mathf.Clamp(ext * 0.6f + 0.3f, 0.3f, 1.6f);
                        var e = c + dir * d + Vector3.up * rise;
                        if (Physics.CheckSphere(e, 0.2f, AllLayers, NoTrig)) continue;
                        var to = c - e;
                        float len = to.magnitude;
                        bool clear = true;
                        foreach (var h in Physics.RaycastAll(e, to / len, Mathf.Max(len - ext * 0.95f, 0.05f), AllLayers, NoTrig))
                            if (!map.TryGetValue(h.collider, out var q) || (q != f.p && q != f.other)) { clear = false; break; }
                        if (!clear) continue;
                        eye = e;
                        how = $"{d:0.0} m away at yaw {k * 45}";
                    }
                    if (how != null) break;
                }
                if (how == null)
                {
                    eye = c + new Vector3(0.6f, ext + 1.2f, -0.6f);
                    how = "fallback, from above";
                }
                var v = c - eye;
                float yawDeg = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                float pitchDeg = -Mathf.Asin(Mathf.Clamp(v.y / v.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
                IronCitadelCapture.Perspective(path, eye, yawDeg, pitchDeg, 50f, 640, 360, true);
                return how;
            }
            finally { if (marker != null) Object.DestroyImmediate(marker); }
        }

        static GameObject Marker(Bounds b, bool dot)
        {
            var go = new GameObject("~ICAuditMarker");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.012f;
            lr.loop = false;
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(sh) { color = new Color(1f, 0f, 1f, 1f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0f, 1f, 1f));
            lr.sharedMaterial = mat;
            var mn = b.min; var mx = b.max;
            Vector3 P(int x, int y, int z) => new Vector3(x == 0 ? mn.x : mx.x, y == 0 ? mn.y : mx.y, z == 0 ? mn.z : mx.z);
            var pts = new[]
            {
                P(0,0,0), P(1,0,0), P(1,0,1), P(0,0,1), P(0,0,0), P(0,1,0), P(1,1,0), P(1,0,0), P(1,1,0), P(1,1,1), P(1,0,1), P(1,1,1), P(0,1,1), P(0,0,1), P(0,1,1), P(0,1,0),
            };
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            if (dot)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(go.transform, false);
                s.transform.position = new Vector3(b.center.x, b.min.y - 0.02f, b.center.z);
                s.transform.localScale = Vector3.one * Mathf.Clamp(b.size.magnitude * 0.06f, 0.03f, 0.12f);
                s.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return go;
        }

        /// <summary>An eye point (1.7 m up) with a person's clearance, at or spiralling out from the wanted x, z.</summary>
        static Vector3? Free(Vector3 want, float search)
        {
            for (float r = 0f; r <= search; r += 0.75f)
                for (int k = 0; k < (r == 0f ? 1 : 12); k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    var p = want + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    if (!IronCitadelRoomsAssembler.FloorAt(new Vector3(p.x, 0f, p.z), out var fy)) continue;
                    var e = new Vector3(p.x, fy + 1.7f, p.z);
                    if (!Physics.CheckCapsule(e - Vector3.up * 1.2f, e + Vector3.up * 0.2f, 0.4f, AllLayers, NoTrig)) return e;
                }
            return null;
        }

        static Dictionary<string, Vector3> ReadEyes(string capturesTxt)
        {
            var d = new Dictionary<string, Vector3>();
            if (!File.Exists(capturesTxt)) return d;
            var rx = new Regex(@"^eye-(\w+)\.png: eye \(([-\d.]+), ([-\d.]+), ([-\d.]+)\)");
            foreach (var line in File.ReadAllLines(capturesTxt))
            {
                var m = rx.Match(line);
                if (!m.Success) continue;
                d[m.Groups[1].Value] = new Vector3(F(m.Groups[2].Value), F(m.Groups[3].Value), F(m.Groups[4].Value));
            }
            return d;
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static void Sheet(List<string> files, string path, int cols)
        {
            if (files.Count == 0) return;
            var tiles = files.Select(fp => { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); t.LoadImage(File.ReadAllBytes(fp)); return t; }).ToList();
            int w = tiles[0].width, h = tiles[0].height;
            int rows = (tiles.Count + cols - 1) / cols;
            var sheet = new Texture2D(w * cols, h * rows, TextureFormat.RGBA32, false);
            var fill = new Color32[w * cols * h * rows];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 20, 24, 255);
            sheet.SetPixels32(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int col = i % cols, row = rows - 1 - i / cols;
                if (tiles[i].width == w && tiles[i].height == h) sheet.SetPixels32(col * w, row * h, w, h, tiles[i].GetPixels32());
                Object.DestroyImmediate(tiles[i]);
            }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        // ------------------------------------------------------------------ report

        static void WriteReport(string outDir, string scenePath, List<Piece> pieces, List<Piece> props, List<Finding> findings, List<RoomStat> stats, string log, TimeSpan took, GenericSpec spec = null)
        {
            var md = new StringBuilder();
            md.AppendLine(spec == null ? "# Iron Citadel rooms: prop audit" : "# Iron Citadel: generic prop audit");
            md.AppendLine();
            md.AppendLine($"- Scene: `{scenePath}`, audited {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} in {took.TotalSeconds:0} s.");
            md.AppendLine($"- Pieces: {pieces.Count} ({props.Count} non-structural). Findings: {findings.Count}.");
            if (spec == null)
                md.AppendLine("- Thresholds: floating > 0.04 m drop to the surface below; sunk > 0.05 m under the Floor group's top; wall-clip > 0.08 m deep and 3% of the vertices (60% for a wall mount); overlap 30% of the smaller prop's vertices inside the larger's box, 0.05 m deep (containers, seats under tables excluded); unattached = a wall mount or hanging touching nothing within 0.12 m.");
            else
            {
                md.AppendLine($"- Generic mode: rooms are the cells of `{spec.levelPath}`; a piece is the outermost Synty prefab instance over each renderer; Floor, Walls and Ceiling are read from the piece's name (a floor tile turned over or 7 m up is a ceiling); built-in primitives named Floor/Wall/Lintel/Rock/... count as structure (Rock is neither lid nor wall) ({spec.primitivesStructural}), other primitives (markers, {spec.primitivesSkipped}) and characters (defender stand-ins, {spec.charactersSkipped}) are skipped; rays hit back faces.");
                md.AppendLine("- Thresholds: floating > 0.05 m drop to the surface below AND touching nothing within 0.05 m (else floating-touching); sunk > 0.05 m under a Floor piece's top; wall-clip > 0.08 m deep and 3% of the vertices (60% for a wall mount); overlap 30% of the smaller prop's vertices inside the larger's box, 0.05 m deep (containers, seats under tables excluded); unattached = a wall mount or hanging touching nothing within 0.12 m.");
            }
            md.AppendLine();
            md.AppendLine("## Per room");
            md.AppendLine();
            md.AppendLine("| room | props | floating | void | touching | sunk | wall-clip | overlap | unattached | through-lid | dup | rugs | lights (sum) | floor pts | lid holes | floor holes | void sight | dark floor |");
            md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            int C(string room, string cat) => findings.Count(f => f.p.room == room && f.cat == cat);
            foreach (var s in stats)
                md.AppendLine($"| {s.room} | {s.props} | {C(s.room, "floating")} | {C(s.room, "void-below")} | {C(s.room, "floating-touching")} | {C(s.room, "sunk")} | {C(s.room, "wall-clip")} | {C(s.room, "overlap")} | {C(s.room, "unattached")} | {C(s.room, "through-lid")} | {C(s.room, "duplicate")} | {C(s.room, "rug-overlap")} | {s.lights} ({s.lightSum:0}) | {s.floorPts} | {s.lidHoles} | {s.floorHoles} | {s.escapes}/{s.freePts} | {(s.freePts > 0 ? s.dark * 100f / s.freePts : 0f):0}% |");
            foreach (var feat in findings.Where(f => stats.All(s => s.room != f.p.room)).GroupBy(f => f.p.room))
                md.AppendLine($"| {feat.Key} (feature) | - | {feat.Count(f => f.cat == "floating")} | {feat.Count(f => f.cat == "void-below")} | {feat.Count(f => f.cat == "floating-touching")} | {feat.Count(f => f.cat == "sunk")} | {feat.Count(f => f.cat == "wall-clip")} | {feat.Count(f => f.cat == "overlap")} | {feat.Count(f => f.cat == "unattached")} | {feat.Count(f => f.cat == "through-lid")} | {feat.Count(f => f.cat == "duplicate")} | {feat.Count(f => f.cat == "rug-overlap")} | | | | | | |");
            md.AppendLine();
            foreach (var s in stats)
            {
                if (s.lidHoleAt.Count + s.floorHoleAt.Count + s.escapeAt.Count == 0) continue;
                md.AppendLine($"- {s.room}: " + string.Join("; ", new[]
                {
                    s.lidHoleAt.Count > 0 ? "lid holes at " + string.Join(" ", s.lidHoleAt) : null,
                    s.floorHoleAt.Count > 0 ? "floor holes at " + string.Join(" ", s.floorHoleAt) : null,
                    s.escapeAt.Count > 0 ? "void seen from " + string.Join(" ", s.escapeAt) : null,
                }.Where(x => x != null)));
            }
            md.AppendLine();
            md.AppendLine("## Where the findings come from");
            md.AppendLine();
            foreach (var cat in new[] { "floating", "void-below", "floating-touching", "sunk", "wall-clip", "overlap", "unattached", "through-lid", "duplicate", "rug-overlap" })
            {
                var fs = findings.Where(f => f.cat == cat).ToList();
                if (fs.Count == 0) continue;
                md.AppendLine($"- {cat}: {fs.Count} ({fs.Count(f => f.p.setPiece != null)} inside set pieces, {fs.Count(f => f.p.setPiece == null)} room-placed; books/papers {fs.Count(f => RxBook.IsMatch(f.p.source))})");
            }
            md.AppendLine();
            md.AppendLine("### Floating, by set piece");
            md.AppendLine();
            foreach (var g in findings.Where(f => f.cat == "floating" || f.cat == "void-below").GroupBy(f => f.p.setPiece ?? "(room-placed)").OrderByDescending(g => g.Count()).Take(25))
                md.AppendLine($"- {g.Count()}: {g.Key} (rooms: {string.Join(", ", g.Select(f => f.p.room).Distinct())}; worst {g.Max(f => f.mag):0.00} m; props: {string.Join(", ", g.GroupBy(f => f.p.source).OrderByDescending(x => x.Count()).Take(4).Select(x => $"{x.Key} x{x.Count()}"))})");
            md.AppendLine();
            md.AppendLine("### Floating, by prop");
            md.AppendLine();
            foreach (var g in findings.Where(f => f.cat == "floating" || f.cat == "void-below").GroupBy(f => f.p.source).OrderByDescending(g => g.Count()).Take(25))
                md.AppendLine($"- {g.Count()}: {g.Key} (median {Median(g.Select(f => f.mag)):0.00} m, max {g.Max(f => f.mag):0.00} m; rooms: {string.Join(", ", g.Select(f => f.p.room).Distinct())})");
            md.AppendLine();
            md.AppendLine("### Overlaps, by pair");
            md.AppendLine();
            foreach (var g in findings.Where(f => f.cat == "overlap").GroupBy(f => f.p.source + " in " + f.other.source).OrderByDescending(g => g.Count()).Take(25))
                md.AppendLine($"- {g.Count()}: {g.Key} (rooms: {string.Join(", ", g.Select(f => f.p.room).Distinct())}; set pieces: {string.Join(", ", g.Select(f => f.p.setPiece ?? "(room)").Distinct().Take(4))})");
            md.AppendLine();
            md.AppendLine("### Wall clips, by prop");
            md.AppendLine();
            foreach (var g in findings.Where(f => f.cat == "wall-clip").GroupBy(f => f.p.source).OrderByDescending(g => g.Count()).Take(25))
                md.AppendLine($"- {g.Count()}: {g.Key} (deepest {g.Max(f => f.mag):0.00} m; rooms: {string.Join(", ", g.Select(f => f.p.room).Distinct())}; set pieces: {string.Join(", ", g.Select(f => f.p.setPiece ?? "(room)").Distinct().Take(4))})");
            md.AppendLine();
            md.AppendLine("### Scaled props (not 1,1,1), by room");
            md.AppendLine();
            foreach (var s in stats.Where(s => s.scaled.Count > 0))
                md.AppendLine($"- {s.room}: {string.Join("; ", s.scaled.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key} x{kv.Value}"))}");
            md.AppendLine();
            md.AppendLine("## Findings (worst first, up to 60 per category)");
            md.AppendLine();
            foreach (var cat in new[] { "void-below", "floating", "floating-touching", "sunk", "through-lid", "wall-clip", "overlap", "unattached", "duplicate", "rug-overlap" })
            {
                var fs = findings.Where(f => f.cat == cat).OrderByDescending(f => f.mag).ToList();
                if (fs.Count == 0) continue;
                md.AppendLine($"### {cat} ({fs.Count})");
                md.AppendLine();
                foreach (var f in fs.Take(60))
                    md.AppendLine($"- {f.p.Label}: {f.detail}" + (f.shot != null ? $" [{f.shot}]" : "") + $" -- `{f.p.path}`");
                md.AppendLine();
            }
            md.AppendLine("## Log");
            md.AppendLine();
            md.AppendLine("```");
            md.Append(log);
            md.AppendLine("```");
            File.WriteAllText(Path.Combine(outDir, "rooms-audit.md"), md.ToString());

            var js = new StringBuilder();
            js.AppendLine("[");
            for (int i = 0; i < findings.Count; i++)
            {
                var f = findings[i];
                js.Append("  {\"cat\": ").Append(Q(f.cat)).Append(", \"mag\": ").Append(f.mag.ToString("0.000", CultureInfo.InvariantCulture))
                  .Append(", \"room\": ").Append(Q(f.p.room)).Append(", \"space\": ").Append(Q(f.p.space)).Append(", \"group\": ").Append(Q(f.p.group))
                  .Append(", \"source\": ").Append(Q(f.p.source)).Append(", \"setPiece\": ").Append(f.p.setPiece == null ? "null" : Q(f.p.setPiece))
                  .Append(", \"other\": ").Append(f.other == null ? "null" : Q(f.other.source))
                  .Append(", \"pos\": [").Append(V(f.p.b.center.x)).Append(", ").Append(V(f.p.b.min.y)).Append(", ").Append(V(f.p.b.center.z)).Append("]")
                  .Append(", \"size\": [").Append(V(f.p.b.size.x)).Append(", ").Append(V(f.p.b.size.y)).Append(", ").Append(V(f.p.b.size.z)).Append("]")
                  .Append(", \"detail\": ").Append(Q(f.detail)).Append(", \"path\": ").Append(Q(f.p.path)).Append(", \"shot\": ").Append(f.shot == null ? "null" : Q(f.shot))
                  .Append("}").AppendLine(i + 1 < findings.Count ? "," : "");
            }
            js.AppendLine("]");
            if (spec == null) { File.WriteAllText(Path.Combine(outDir, "rooms-audit.json"), js.ToString()); return; }

            // generic: an object with the summary, the rooms and the findings list
            var cats = new[] { "floating", "floating-touching", "void-below", "sunk", "wall-clip", "overlap", "unattached", "through-lid", "duplicate", "rug-overlap" };
            var lightsAll = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.gameObject.activeInHierarchy && l.type != LightType.Directional).ToList();
            var gj = new StringBuilder();
            gj.AppendLine("{");
            gj.AppendLine($"  \"mode\": \"generic\", \"scene\": {Q(scenePath)}, \"level\": {Q(spec.levelPath)}, \"auditedUtc\": {Q(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))}, \"seconds\": {took.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)},");
            gj.AppendLine($"  \"pieces\": {pieces.Count}, \"structural\": {pieces.Count(p => p.kind == Kind.Structural)}, \"props\": {props.Count(p => p.kind == Kind.Prop)}, \"wallMounts\": {props.Count(p => p.kind == Kind.WallMount)}, \"hangings\": {props.Count(p => p.kind == Kind.Hanging)}, \"primitivesSkipped\": {spec.primitivesSkipped}, \"primitivesStructural\": {spec.primitivesStructural}, \"charactersSkipped\": {spec.charactersSkipped}, \"outsideRooms\": {spec.outsideRooms},");
            gj.AppendLine($"  \"structure\": {{\"floor\": {pieces.Count(p => p.group == "Floor")}, \"walls\": {pieces.Count(p => p.group == "Walls")}, \"ceiling\": {pieces.Count(p => p.group == "Ceiling")}, \"rock\": {pieces.Count(p => p.group == "Rock")}}},");
            gj.AppendLine($"  \"lights\": {{\"total\": {lightsAll.Count}, \"realtime\": {lightsAll.Count(l => l.lightmapBakeType == LightmapBakeType.Realtime)}, \"mixed\": {lightsAll.Count(l => l.lightmapBakeType == LightmapBakeType.Mixed)}, \"baked\": {lightsAll.Count(l => l.lightmapBakeType == LightmapBakeType.Baked)}, \"inRooms\": {stats.Sum(s => s.lights)}}},");
            gj.AppendLine("  \"summary\": {" + string.Join(", ", cats.Select(c => $"{Q(c)}: {findings.Count(f => f.cat == c)}")) + "},");
            int totFloor = stats.Sum(s => s.floorPts), totFree = stats.Sum(s => s.freePts), totDark = stats.Sum(s => s.dark);
            gj.AppendLine($"  \"floor\": {{\"floorPts\": {totFloor}, \"freePts\": {totFree}, \"dark\": {totDark}, \"darkShare\": {(totFree > 0 ? totDark / (float)totFree : 0f).ToString("0.000", CultureInfo.InvariantCulture)}, \"lidHoles\": {stats.Sum(s => s.lidHoles)}, \"floorHoles\": {stats.Sum(s => s.floorHoles)}, \"escapes\": {stats.Sum(s => s.escapes)}}},");
            gj.AppendLine("  \"rooms\": [");
            for (int i = 0; i < stats.Count; i++)
            {
                var s = stats[i];
                var rm = spec.rooms.First(x => x.id == s.room);
                int Cn(string cat) => findings.Count(f => f.p.room == s.room && f.cat == cat);
                gj.Append($"    {{\"room\": {Q(s.room)}, \"cell\": [{rm.r}, {rm.c}], \"props\": {s.props}, \"walls\": {pieces.Count(p => p.room == s.room && p.group == "Walls")}, \"floors\": {pieces.Count(p => p.room == s.room && p.group == "Floor")}, \"ceilings\": {pieces.Count(p => p.room == s.room && p.group == "Ceiling")}, ")
                 .Append($"\"lights\": {s.lights}, \"lightSum\": {s.lightSum.ToString("0.0", CultureInfo.InvariantCulture)}, \"floorPts\": {s.floorPts}, \"freePts\": {s.freePts}, \"dark\": {s.dark}, \"darkShare\": {(s.freePts > 0 ? s.dark / (float)s.freePts : 0f).ToString("0.000", CultureInfo.InvariantCulture)}, \"lidHoles\": {s.lidHoles}, \"floorHoles\": {s.floorHoles}, \"escapes\": {s.escapes}, ")
                 .Append("\"findings\": {" + string.Join(", ", cats.Select(c => $"{Q(c)}: {Cn(c)}")) + "}}")
                 .AppendLine(i + 1 < stats.Count ? "," : "");
            }
            gj.AppendLine("  ],");
            gj.Append("  \"findings\": ").Append(js.ToString().TrimEnd());
            gj.AppendLine();
            gj.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "rooms-audit.json"), gj.ToString());
        }

        static float Median(IEnumerable<float> xs)
        {
            var a = xs.OrderBy(x => x).ToList();
            return a.Count == 0 ? 0f : a[a.Count / 2];
        }

        static string V(float x) => x.ToString("0.00", CultureInfo.InvariantCulture);

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";
    }
}
