// IronCitadel conformance kit: scores the open scene against level.json's must_be_true list.
// Framework-free: it reads only physics colliders, NavMeshLinks and (for ceilings, as a fallback) renderers.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronCitadel.Conformance
{
    // =====================================================================================================
    // THRESHOLDS. Every number the checks use lives here, so they can be tuned in one place.
    // Heights are metres above the room's measured floor (the ground floor is y = 0 in level.json).
    // =====================================================================================================
    public static class Tune
    {
        // --- the bake: equivalent to a NavMeshSurface (collect All, use Physics Colliders) with its own agent ---
        // The shared walker is Starter Assets' first-person rig sized to a person (kit/walker, IronCitadelPlayer):
        // CharacterController radius 0.35, height 1.8, step offset 0.3, slope 45, skin 0.02. The NavMesh is baked
        // with exactly these numbers and check 10 walks a CharacterController with them, so a pass means that
        // player can walk it.
        public const float AgentRadius = 0.35f, AgentHeight = 1.8f, AgentClimb = 0.3f, AgentSlope = 45f;
        // Kept so callers that print the bake size still compile; the bake has used the agent size since round 2.
        public const float BakeRadius = AgentRadius, BakeHeight = AgentHeight;
        public const float VoxelSize = 0.1f;              // finer than Unity's radius / 3
        public const float MinRegionArea = 2f;            // m2; islands smaller than this are dropped by the baker
        public const float BakeMargin = 45f;              // m of world baked around the grid (the outside)
        public const float BakeBottom = -10f, BakeTop = 60f;

        // --- the opening graph ---
        public const float WeldXZ = 0.05f, WeldY = 0.35f; // vertices this close are one vertex
        public const float TJunctionMinOverlap = 0.05f;   // tile-border edges must overlap this much to join
        public const float GapClusterDist = 6f;           // crossings further apart than this are separate gaps
        public const float FloorBandLow = -1f, FloorBandHigh = 1f;   // "floor level"
        public const float FloorProbeStep = 1.5f;         // m between the samples that find a room's floor height
        public const float TriHeightSlack = 1.5f;         // m a NavMesh polygon triangle may sit off the real surface
                                                          // (CalculateTriangulation blends a stage or stair into the floor)
        public const float LevelComponentMinFloorArea = 25f;         // a NavMesh island counts as level space only
                                                                      // with this much floor-level area in room cells
                                                                      // (drops roofs, wall tops and table tops)
        // --- 1 rooms on cells ---
        public const float RoomMinNavArea = 40f;          // m2 of level NavMesh a room cell must hold
        public const float RockMaxNavArea = 1f;           // m2 of level NavMesh a # cell may hold

        // --- 4 routes ---
        public const float StartSnapRadius = 4f;

        // --- 5 vault ---
        public const float VaultSpanHalf = 2f;            // the doorway span: edge centre +- this, along the edge
        public const float VaultDoorHeight = 3.5f;        // ... from the floor up to this
        public const float VaultDisableRadius = 1.5f;     // colliders within this of the span are switched off
        public const float VaultFloorClearance = 0.3f;    // colliders whose top is below floor + this are floors: kept
        public const float VaultSkipLargerThan = 50f;     // colliders wider than this (m, in x or z) are kept
        public const float VaultInteractReach = 8f;       // ... and every collider with an Interact() method within this
                                                          // of the edge centre (a door at the end of a passage is off the edge)

        // --- 7, 8 sampling: the stage, the deck and the climbs are read from NavMesh.SamplePosition on this grid ---
        public const float SampleStep = 0.5f;             // m between samples, along and across the side
        public const float ClimbSampleRise = 0.5f;        // m between the heights a climb is sampled at

        // --- 7 gallery ---
        public const float GalleryHeightTolerance = 1f;   // deck_m +- this (5 m -> 4..6 m)
        public const float GallerySideBand = 15f;         // "the southern part": within this of the hall's south edge
        public const float GalleryMinArea = 20f;          // m2
        public const float ClimbBandLow = 1f;             // a climb is NavMesh between floor + 1 m ...
        public const float ClimbBandTopMargin = 0.75f;    // ... and deck - 0.75 m, joining the floor to the deck
        public const float ClimbEndZone = 12f;            // a climb's centre within this of the hall's west / east edge
        // The hall's CENTRAL FLOOR: the hall cell inset by CentralInsetEnds from its west and east edges, by
        // CentralInsetGallerySide from the gallery (south) edge and by CentralInsetFar from the far (north) edge,
        // below floor + FloorLevelMaxRise. A path from a side doorway to the gallery must not touch it.
        public const float CentralInsetEnds = 7f, CentralInsetGallerySide = 7f, CentralInsetFar = 3f;
        public const float FloorLevelMaxRise = 1f;
        public const float PathSampleStep = 0.5f;         // paths are checked every 0.5 m, not only at corners
        // Visibility: eyes on the gallery's hall-side NavMesh edge, leaning RailLean over it, EyeHeight up;
        // targets on the hall floor north of the gallery footprint, minus VisibilityInsetEnds at each end
        // (where the stair shafts stand), lifted FloorTargetLift. The NavMesh edge stands AgentRadius back from
        // the rail, so RailLean puts the eye 0.2 m past the rail's inner face (as first tuned with a 0.3 agent).
        // Only floor under a ceiling higher than the deck + HallOpenAbove counts as the hall floor: floor in a
        // side room or passage that shares the hall's cell, under its own lower lid, is not the hall.
        public const float EyeHeight = 1.6f, RailLean = AgentRadius + 0.2f, RailSampleStep = 2f;
        public const float HallOpenAbove = 1f;
        public const float FloorSampleStep = 2f, FloorTargetLift = 0.2f, VisibilityInsetEnds = 6f;
        public const float MinVisibleFraction = 0.9f;
        public const float UnderGalleryMaxDist = 3f;      // the under_gallery crossing lies this close below the deck

        // --- 8 stage ---
        // "A full-width row of steps": the steps are judged against the FREE width at the stage's front line --
        // the NavMesh along a line StageFrontLineOffset in front of the stage, from wall to wall at that line --
        // not the room cell's width. An aisled room whose stage spans its nave wall to wall passes.
        public const float StageTolerance = 0.25f;        // rise_m +- this
        public const float StageMinArea = 20f;
        public const float StageFrontLineOffset = 1f;     // m in front of the stage's front edge (median of its columns)
        public const float StageFrontProbe = 4f;          // m out from the stage edge that must step down to the floor
        public const float StageMinStepFraction = 0.8f;   // share of the free width at the front line that steps down

        // --- 9 heights: scores exactly "the prison, guard room, kitchen and vault are low; the great hall and
        // throne room are tall". Standard rooms are reported, not scored. Fractions are of a room's floor samples.
        public const float CeilingSampleStep = 3f, CeilingMaxRay = 60f;
        public const float LowCeilingMax = 6.5f;          // LOW: at least LowMinFraction of the samples under <= 6.5 m
        public const float LowMinFraction = 0.75f;
        public const float TallCeilingMin = 8f;           // TALL: at least TallMinFraction of the samples under >= 8 m
        public const float TallMinFraction = 0.25f;
        public const float HeightTargetPercentile = 0.9f; // an "about N m" room reports this percentile (information)
        public const float HeightTargetTolerance = 3f;    // "about 15 m" -> 12..18 (information, not scored)

        // --- 10 walk ---
        public const float WalkSpeed = 3f, WalkDt = 0.05f, WalkReach = 0.3f;
        public const float WalkStuckSeconds = 2f, WalkStuckProgress = 0.05f;
        public const float WalkFallDrop = 2.5f, WalkEndTolerance = 1f, WalkSkin = 0.02f;   // the PlayerCapsule's skin width
    }

    public sealed class CheckRow
    {
        public string Id, MustBeTrue, Status, Short, Reason;
        public JObj Measured = new JObj();

        public JObj ToJson() => new JObj()
            .Add("id", Id).Add("must_be_true", MustBeTrue).Add("status", Status)
            .Add("summary", Short).Add("reason", Reason).Add("measured", Measured);
    }

    /// <summary>A hall-relative frame on one side of a cell: U runs along that side, V inward from it.</summary>
    public sealed class SideFrame
    {
        public string Side, LowEnd, HighEnd;
        public Cell Cell, LowNeighbour, HighNeighbour, SideNeighbour;
        public float XMin, XMax, ZMin, ZMax, Width;

        public SideFrame(LevelSpec L, Cell c, string side)
        {
            Cell = c; Side = side ?? "south";
            XMin = L.XMin(c); XMax = L.XMax(c); ZMin = L.ZMin(c); ZMax = L.ZMax(c); Width = L.Tile;
            bool ns = Side == "south" || Side == "north";
            LowEnd = ns ? "west" : "north";
            HighEnd = ns ? "east" : "south";
            LowNeighbour = ns ? new Cell(c.R, c.C - 1) : new Cell(c.R - 1, c.C);
            HighNeighbour = ns ? new Cell(c.R, c.C + 1) : new Cell(c.R + 1, c.C);
            switch (Side)
            {
                case "north": SideNeighbour = new Cell(c.R - 1, c.C); break;
                case "west": SideNeighbour = new Cell(c.R, c.C - 1); break;
                case "east": SideNeighbour = new Cell(c.R, c.C + 1); break;
                default: SideNeighbour = new Cell(c.R + 1, c.C); break;
            }
        }

        public float U(Vector3 p)
        {
            return Side == "south" || Side == "north" ? p.x - XMin : ZMax - p.z;
        }

        public float V(Vector3 p)
        {
            switch (Side)
            {
                case "north": return ZMax - p.z;
                case "west": return p.x - XMin;
                case "east": return XMax - p.x;
                default: return p.z - ZMin;
            }
        }

        public Vector3 World(float u, float v, float y)
        {
            switch (Side)
            {
                case "north": return new Vector3(XMin + u, y, ZMax - v);
                case "west": return new Vector3(XMin + v, y, ZMax - u);
                case "east": return new Vector3(XMax - v, y, ZMax - u);
                default: return new Vector3(XMin + u, y, ZMin + v);
            }
        }

        public Vector3 Inward
        {
            get
            {
                switch (Side)
                {
                    case "north": return Vector3.back;
                    case "west": return Vector3.right;
                    case "east": return Vector3.left;
                    default: return Vector3.forward;
                }
            }
        }
    }

    public sealed class Scorer
    {
        readonly LevelSpec L;
        readonly Scene scene;
        readonly bool doWalk;
        NavModel M, MOpen;
        readonly List<CheckRow> rows = new List<CheckRow>();
        public readonly List<WalkResult> Walks = new List<WalkResult>();
        public readonly List<string> Notes = new List<string>();

        RoomSpec entry, hall, armory, throne, prison, vault;
        DoorSpec gate, secret, outer;
        Vector3 start;
        string startSource;
        Vector3? hallPoint, galleryPoint, galleryLow, galleryHigh, lowDoorStart, highDoorStart, stagePoint;
        SideFrame hallFrame;

        public Scorer(LevelSpec level, Scene scene, bool walk)
        {
            L = level; this.scene = scene; doWalk = walk;
        }

        // ---------------------------------------------------------------------------------------------
        public JObj Score(out NavModel closedModel)
        {
            entry = L.RoomOfKind('E'); hall = L.RoomOfKind('D'); armory = L.RoomOfKind('A');
            throne = L.RoomOfKind('T'); prison = L.RoomOfKind('P'); vault = L.RoomOfKind('V');
            gate = L.Doors.Find(d => d.IsPortcullis);
            secret = L.Doors.Find(d => d.IsSecret);
            outer = L.Doors.Find(d => d.IsOutside);

            var guard = new SceneGuard(scene);
            var links = guard.Links;
            try
            {
                M = NavBake.Bake(L, scene, links, "closed");
                FindStart(guard);

                Run("1", Check1_Rooms);
                Run("2", Check2_Doors);
                Run("3", Check3_Portcullis);
                Run("4", Check4_Routes);
                Run("6", Check6_Throne);
                Run("7", Check7_Gallery);
                Run("8", Check8_Stage);
                Run("9", Check9_Heights);
                WalkRoutesClosed();
                Run("5", Check5_Vault);              // re-bakes with the secret door opened, walks prison -> vault
                rows.Add(new CheckRow
                {
                    Id = "5b", MustBeTrue = Mbt(5) + " [the look]", Status = "manual",
                    Short = "judge in the editor",
                    Reason = "Whether the closed bookcase door looks like the bookcases beside it is a visual call. " +
                             "Look at the prison's west end."
                });
                Run("10", Check10_Walk);
            }
            finally
            {
                guard.Restore();
                NavBake.Clear();
            }

            rows.Sort((a, b) => Order(a.Id).CompareTo(Order(b.Id)));
            closedModel = M;
            return BuildReport();
        }

        static float Order(string id)
        {
            if (id == "5b") return 5.5f;
            return float.TryParse(id, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 99f;
        }

        string Mbt(int n) => n >= 1 && n <= L.MustBeTrue.Count ? L.MustBeTrue[n - 1] : "(must_be_true line " + n + ")";

        void Run(string id, Func<CheckRow, bool?> check)
        {
            var row = new CheckRow { Id = id };
            int n;
            row.MustBeTrue = int.TryParse(id, out n) && n <= 9 ? Mbt(n) :
                "WALK: a first-person CharacterController walks the key routes (not a must_be_true line)";
            try
            {
                var ok = check(row);
                row.Status = ok == null ? "manual" : ok.Value ? "pass" : "fail";
            }
            catch (Exception e)
            {
                row.Status = "fail";
                row.Reason = "error while checking: " + e.GetType().Name + ": " + e.Message;
                row.Short = "error";
                Debug.LogException(e);
            }
            rows.Add(row);
        }

        JObj BuildReport()
        {
            int pass = rows.Count(r => r.Status == "pass"), fail = rows.Count(r => r.Status == "fail"),
                manual = rows.Count(r => r.Status == "manual");
            var openings = new List<object>();
            foreach (var o in M.Openings.Values.OrderBy(o => M.NodeName(o.A)).ThenBy(o => M.NodeName(o.B)))
                openings.Add(OpeningJson(M, o));
            var walks = new List<object>();
            foreach (var w in Walks) walks.Add(w.ToJson());
            var report = new JObj()
                .Add("tool", "IronCitadel.Conformance 1.0")
                .Add("scene", scene.path)
                .Add("level", L.Path)
                .Add("level_name", L.Name)
                .Add("run_utc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))
                .Add("unity", Application.unityVersion)
                .Add("summary", new JObj().Add("pass", pass).Add("fail", fail).Add("manual", manual)
                    .Add("automated_all_pass", fail == 0))
                .Add("bake", new JObj()
                    .Add("agent", new JObj().Add("radius", Tune.AgentRadius).Add("height", Tune.AgentHeight)
                        .Add("step", Tune.AgentClimb).Add("slope", Tune.AgentSlope).Add("walk_skin", Tune.WalkSkin))
                    .Add("sources", M.SourceCount).Add("triangles", M.T).Add("components", M.CompCount)
                    .Add("level_components", M.LevelComp.Count(b => b))
                    .Add("seconds", M.BakeSeconds).Add("scene_navmesh_links", M.Links.Count))
                .Add("start", new JObj().Add("source", startSource).Add("position", start)
                    .Add("node", M.NodeName(M.NodeOf(start))))
                .Add("checks", rows.Select(r => (object)r.ToJson()).ToList())
                .Add("openings", openings)
                .Add("walks", walks)
                .Add("notes", Notes.Cast<object>().ToList());
            return report;
        }

        public List<CheckRow> Rows => rows;
        public Vector3 StartPosition => start;

        JObj OpeningJson(NavModel m, Opening o)
        {
            var j = new JObj().Add("between", new List<object> { m.NodeName(o.A), m.NodeName(o.B) })
                .Add("cells", new List<object> { CellText(o.A), CellText(o.B) })
                .Add("kind", o.Kind).Add("via", o.ViaLink ? "navmesh link" : "navmesh")
                .Add("shared_edge_m", o.Length).Add("gaps", o.Gaps).Add("crossing", o.Crossing)
                .Add("y_range", new List<object> { o.MinY, o.MaxY });
            if (o.Kind == "edge" && o.A != NavModel.Out && o.B != NavModel.Out)
            {
                var c = L.SharedEdgeCentre(o.A, o.B);
                j.Add("offset_from_edge_centre_m", Horizontal(o.Crossing - c));
            }
            if (o.Links.Count > 0) j.Add("links", o.Links.Cast<object>().ToList());
            return j;
        }

        static string CellText(Cell c) => c == NavModel.Out ? "outside" : c.ToString();
        static float Horizontal(Vector3 d) => new Vector2(d.x, d.z).magnitude;

        // ---------------------------------------------------------------------------------------------
        // helpers

        readonly Dictionary<(NavModel, Cell), float> floorCache = new Dictionary<(NavModel, Cell), float>();

        /// <summary>The room's floor height: the commonest NavMesh height (0.1 m bins) in the floor band, read with
        /// SamplePosition on a grid. Triangle centroids are not used: a polygon can span a stage and the floor.</summary>
        float FloorOf(NavModel m, Cell c)
        {
            if (floorCache.TryGetValue((m, c), out var cached)) return cached;
            var ys = new List<float>();
            float step = Tune.FloorProbeStep;
            for (float x = L.XMin(c) + step * 0.5f; x < L.XMax(c); x += step)
            for (float z = L.ZMin(c) + step * 0.5f; z < L.ZMax(c); z += step)
            {
                var q = new Vector3(x, L.FloorY, z);
                if (!NavMesh.SamplePosition(q, out var hit, Tune.FloorBandHigh + 0.05f, m.Filter)) continue;
                var p = hit.position;
                float dy = p.y - L.FloorY;
                if (Horizontal(p - q) > step * 0.5f || dy < Tune.FloorBandLow || dy > Tune.FloorBandHigh || m.NodeOf(p) != c) continue;
                int t = m.FindTri(p, 0.6f);
                if (t < 0 || !m.IsLevel(t)) continue;
                ys.Add(p.y);
            }
            float floor = L.FloorY;
            if (ys.Count > 0)
            {
                var mode = ys.GroupBy(y => Mathf.RoundToInt(y / 0.1f)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
                floor = mode.Average();
            }
            floorCache[(m, c)] = floor;
            return floor;
        }

        Vector3? Snap(NavModel m, Vector3 p, float r)
        {
            return NavMesh.SamplePosition(p, out var hit, r, m.Filter) ? hit.position : (Vector3?)null;
        }

        /// <summary>A floor-level point of the room's level NavMesh, the closest found to <paramref name="near"/>.</summary>
        Vector3? FloorPoint(NavModel m, Cell c, Vector3 near)
        {
            float floor = FloorOf(m, c);
            for (float rad = 0; rad <= 24f; rad += 1.5f)
            {
                int steps = rad == 0 ? 1 : Mathf.Max(8, (int)(rad * 2));
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2 / steps;
                    var p = new Vector3(near.x + Mathf.Cos(a) * rad, floor + 0.5f, near.z + Mathf.Sin(a) * rad);
                    if (!NavMesh.SamplePosition(p, out var hit, 1.0f, m.Filter)) continue;
                    if (m.NodeOf(hit.position) != c || Mathf.Abs(hit.position.y - floor) > 0.6f) continue;
                    int t = m.FindTri(hit.position, 0.6f);
                    if (t < 0 || !m.IsLevel(t)) continue;
                    return hit.position;
                }
            }
            return null;
        }

        List<Vector3> PathCorners(NavModel m, Vector3 a, Vector3 b, out NavMeshPathStatus status)
        {
            var path = new NavMeshPath();
            var sa = Snap(m, a, 2f) ?? a;
            var sb = Snap(m, b, 2f) ?? b;
            if (!NavMesh.CalculatePath(sa, sb, m.Filter, path)) { status = NavMeshPathStatus.PathInvalid; return new List<Vector3>(); }
            status = path.status;
            return path.corners.ToList();
        }

        static float PathLength(List<Vector3> pts)
        {
            float s = 0;
            for (int i = 1; i < pts.Count; i++) s += Vector3.Distance(pts[i - 1], pts[i]);
            return s;
        }

        static List<Vector3> Densify(List<Vector3> pts, float step)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0) { o.Add(pts[0]); continue; }
                var a = pts[i - 1]; var b = pts[i];
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / step));
                for (int k = 1; k <= n; k++) o.Add(Vector3.Lerp(a, b, k / (float)n));
            }
            return o;
        }

        List<object> NodesVisited(NavModel m, List<Vector3> dense)
        {
            var l = new List<object>();
            string last = null;
            foreach (var p in dense)
            {
                var n = m.NodeName(m.NodeOf(p));
                if (n != last) { l.Add(n); last = n; }
            }
            return l;
        }

        // ---------------------------------------------------------------------------------------------
        // start

        void FindStart(SceneGuard guard)
        {
            Transform found = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
            {
                var n = t.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (n.Contains("playerstart") || n.Contains("spawnpoint") || n == "start")
                { found = t; startSource = "object named " + t.name; break; }
            }
            if (found == null)
            {
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                {
                    bool tagged = false;
                    try { tagged = t.CompareTag("Player"); } catch { }
                    if (tagged) { found = t; startSource = "object tagged Player: " + t.name; break; }
                }
            }
            if (found == null && guard.Controllers.Count > 0)
            {
                found = guard.Controllers[0].transform;
                startSource = "CharacterController on " + found.name;
            }
            Vector3 p;
            if (found != null) p = found.position;
            else
            {
                var side = LevelSpec.SideWord(outer?.Outside) ?? "south";
                var cell = outer?.A ?? L.StartCell;
                var edge = L.SideCentre(cell, side);
                p = edge + (L.Centre(cell) - edge).normalized * 1.5f;
                startSource = "fallback: 1.5 m inside the outer door";
            }
            var snapped = Snap(M, p + Vector3.up * 0.5f, Tune.StartSnapRadius);
            if (snapped == null) Notes.Add("the start (" + startSource + ") is not within " + Tune.StartSnapRadius + " m of the NavMesh");
            start = snapped ?? p;
        }

        // ---------------------------------------------------------------------------------------------
        // 1. rooms on cells; # cells are rock

        bool? Check1_Rooms(CheckRow row)
        {
            var failures = new List<string>();
            var rooms = new JObj();
            foreach (var r in L.Rooms)
            {
                float a = M.AreaInCell(r.Cell, true);
                rooms.Add(r.Id, new JObj().Add("cell", r.Cell.ToString()).Add("navmesh_m2", a)
                    .Add("tile_fraction", a / (L.Tile * L.Tile)));
                if (a < Tune.RoomMinNavArea) failures.Add(r.Id + " has " + a.ToString("0.#") + " m2 of NavMesh");
            }
            var rock = new JObj();
            for (int rr = 0; rr < L.Rows; rr++)
            for (int cc = 0; cc < L.Cols; cc++)
            {
                var c = new Cell(rr, cc);
                if (!L.IsRock(c)) continue;
                float a = M.AreaInCell(c, true);
                float isolated = M.AreaInCell(c, false, Tune.FloorBandLow, Tune.FloorBandHigh) - M.AreaInCell(c, true, Tune.FloorBandLow, Tune.FloorBandHigh);
                rock.Add(c.ToString(), new JObj().Add("level_navmesh_m2", a).Add("unreachable_floor_m2", isolated));
                if (a > Tune.RockMaxNavArea) failures.Add("rock " + c + " has " + a.ToString("0.#") + " m2 of walkable level NavMesh");
                if (isolated > 5f) Notes.Add("rock cell " + c + " has " + isolated.ToString("0") + " m2 of floor-level NavMesh that no room reaches (ignored)");
            }
            row.Measured.Add("rooms", rooms).Add("rock_cells", rock)
                .Add("outside_level_navmesh_m2", M.AreaInCell(NavModel.Out, true));
            row.Short = failures.Count == 0 ? L.Rooms.Count + " rooms hold NavMesh; rock cells are empty" : string.Join("; ", failures);
            row.Reason = failures.Count == 0
                ? "every room cell holds at least " + Tune.RoomMinNavArea + " m2 of level NavMesh and no # cell holds more than " + Tune.RockMaxNavArea + " m2"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }

        // ---------------------------------------------------------------------------------------------
        // 2. doors: the opening graph equals level.json's doors

        bool? Check2_Doors(CheckRow row)
        {
            var expected = new Dictionary<string, DoorSpec>();
            var handledElsewhere = new HashSet<string>();
            foreach (var d in L.Doors)
            {
                if (d.IsOutside) continue;
                var k = NavModel.Key(d.A, d.B.Value);
                if (d.IsPortcullis || d.IsSecret) { handledElsewhere.Add(k); continue; }
                expected[k] = d;
            }
            string outerKey = outer != null ? NavModel.Key(outer.A, NavModel.Out) : null;

            var missing = new List<string>();
            var present = new List<object>();
            foreach (var kv in expected)
            {
                var o = M.Find(kv.Value.A, kv.Value.B.Value);
                if (o == null) { missing.Add(kv.Value.Id + " " + L.NodeName(kv.Value.A) + "-" + L.NodeName(kv.Value.B.Value)); continue; }
                present.Add(new JObj().Add("door", kv.Value.Id).Add("shared_edge_m", o.Length).Add("gaps", o.Gaps)
                    .Add("offset_from_edge_centre_m", Horizontal(o.Crossing - L.SharedEdgeCentre(kv.Value.A, kv.Value.B.Value))));
            }
            var extra = new List<string>();
            foreach (var kv in M.Openings)
            {
                if (expected.ContainsKey(kv.Key) || handledElsewhere.Contains(kv.Key) || kv.Key == outerKey) continue;
                var o = kv.Value;
                extra.Add(M.NodeName(o.A) + "-" + M.NodeName(o.B) + " (" + o.Kind + (o.ViaLink ? ", link" : "") +
                          ", " + o.Length.ToString("0.#") + " m at " + Fmt(o.Crossing) + ")");
            }
            bool outerFound = outerKey != null && M.Openings.ContainsKey(outerKey);
            row.Measured.Add("expected", expected.Count).Add("found", present)
                .Add("missing", missing.Cast<object>().ToList()).Add("extra", extra.Cast<object>().ToList())
                .Add("left_to_checks_3_and_5", handledElsewhere.Count)
                .Add("outer_door_opening_found", outerFound);
            row.Short = (expected.Count - missing.Count) + "/" + expected.Count + " doors; " + missing.Count + " missing; " + extra.Count + " extra";
            if (missing.Count == 0 && extra.Count == 0)
                row.Reason = "the NavMesh opens exactly the " + expected.Count + " listed doors (outer_door left out; the portcullis and the secret door are checks 3 and 5)";
            else
                row.Reason = (missing.Count > 0 ? "missing: " + string.Join(", ", missing) + ". " : "") +
                             (extra.Count > 0 ? "extra: " + string.Join(", ", extra) + "." : "");
            return missing.Count == 0 && extra.Count == 0;
        }

        static string Fmt(Vector3 p) => "(" + p.x.ToString("0.#", CultureInfo.InvariantCulture) + ", " +
                                         p.y.ToString("0.#", CultureInfo.InvariantCulture) + ", " +
                                         p.z.ToString("0.#", CultureInfo.InvariantCulture) + ")";

        // ---------------------------------------------------------------------------------------------
        // 3. portcullis

        bool? Check3_Portcullis(CheckRow row)
        {
            if (gate == null || armory == null || hall == null) { row.Reason = "level.json names no portcullis door, armory or great hall"; return null; }
            var gateOpen = M.Find(gate.A, gate.B.Value);
            var others = new List<string>();
            bool fromHall = false;
            foreach (var o in M.OpeningsOf(armory.Cell))
            {
                var other = o.Other(armory.Cell);
                if (other == hall.Cell) fromHall = true;
                else others.Add(M.NodeName(other));
            }
            row.Measured.Add("gate_opening", gateOpen != null)
                .Add("armory_openings_besides_hall", others.Cast<object>().ToList())
                .Add("armory_opens_to_hall", fromHall);

            // a real path from the start to the armory, and the rooms it passes
            var target = FloorPoint(M, armory.Cell, L.Centre(armory.Cell));
            if (target != null)
            {
                var corners = PathCorners(M, start, target.Value, out var st);
                row.Measured.Add("path_start_to_armory", new JObj().Add("status", st.ToString())
                    .Add("visits", st == NavMeshPathStatus.PathComplete ? NodesVisited(M, Densify(corners, 1f)) : new List<object>()));
            }
            bool ok = gateOpen == null && others.Count == 0 && fromHall;
            row.Short = ok ? "gate shut; armory opens only to the hall" :
                (gateOpen != null ? "the gate is passable" : others.Count > 0 ? "armory also opens to " + string.Join(", ", others) : "armory has no opening to the hall");
            row.Reason = ok
                ? "no NavMesh crosses " + gate.Id + ", and the armory's only opening is to " + hall.Id + ". The spikes are judged by eye."
                : (gateOpen != null ? gate.Id + " is crossed by " + gateOpen.Length.ToString("0.#") + " m of NavMesh at " + Fmt(gateOpen.Crossing) + ". " : "") +
                  (others.Count > 0 ? "the armory also opens to " + string.Join(", ", others) + ". " : "") +
                  (!fromHall ? "the armory has no opening to " + hall.Id + "." : "");
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 4. two routes

        bool? Check4_Routes(CheckRow row)
        {
            if (hall == null || entry == null) { row.Reason = "level.json names no entry hall or great hall"; return null; }
            bool ok = true;
            var reasons = new List<string>();
            int startTri = M.FindTri(start, 2f);
            var startNode = M.NodeOf(start);
            foreach (var id in new[] { "west_wing", "east_wing" })
            {
                var route = L.Route(id);
                if (route == null) { reasons.Add("level.json has no route " + id); ok = false; continue; }
                var j = new JObj();
                var missingLinks = new List<object>();
                for (int i = 0; i + 1 < route.Cells.Count; i++)
                    if (M.Find(route.Cells[i], route.Cells[i + 1]) == null)
                        missingLinks.Add(L.NodeName(route.Cells[i]) + "-" + L.NodeName(route.Cells[i + 1]));
                bool armoryOnRoute = armory != null && route.Cells.Contains(armory.Cell);
                var allowed = new HashSet<Cell>(route.Cells);
                if (startNode == NavModel.Out) allowed.Add(NavModel.Out);
                bool navOk = false;
                if (startTri >= 0)
                {
                    var seen = M.Flood(startTri, c => allowed.Contains(c));
                    for (int t = 0; t < M.T && !navOk; t++) if (seen[t] && M.CellOfTri[t] == hall.Cell) navOk = true;
                }
                j.Add("cells", route.Cells.Select(c => (object)L.NodeName(c)).ToList())
                    .Add("graph_links_missing", missingLinks)
                    .Add("navmesh_within_route_cells_reaches_hall", navOk)
                    .Add("passes_armory", armoryOnRoute);
                row.Measured.Add(id, j);
                if (missingLinks.Count > 0) { ok = false; reasons.Add(id + " lacks " + string.Join(", ", missingLinks)); }
                if (!navOk) { ok = false; reasons.Add(id + ": no NavMesh from the start to the hall inside its own cells"); }
                if (armoryOnRoute) { ok = false; reasons.Add(id + " passes the armory"); }
            }
            hallPoint = FloorPoint(M, hall.Cell, L.Centre(hall.Cell));
            if (hallPoint == null) { ok = false; reasons.Add("no floor NavMesh in the great hall"); }
            else
            {
                var corners = PathCorners(M, start, hallPoint.Value, out var st);
                row.Measured.Add("path_start_to_hall", new JObj().Add("status", st.ToString())
                    .Add("length_m", PathLength(corners))
                    .Add("visits", st == NavMeshPathStatus.PathComplete ? NodesVisited(M, Densify(corners, 1f)) : new List<object>()));
                if (st != NavMeshPathStatus.PathComplete) { ok = false; reasons.Add("no complete NavMesh path from the start to the hall (" + st + ")"); }
            }
            if (startTri < 0) { ok = false; reasons.Add("the start is not on the level NavMesh"); }
            row.Short = ok ? "west and east wings both reach the hall" : string.Join("; ", reasons);
            row.Reason = ok
                ? "both wings link up cell by cell, the NavMesh joins the start to the hall inside each wing's own cells (so not through the armory), and a real path start -> hall is complete"
                : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 5. vault: closed, no opening; opened, a dead end off the prison only

        bool? Check5_Vault(CheckRow row)
        {
            if (vault == null || secret == null || prison == null) { row.Reason = "level.json names no vault, prison or secret door"; return null; }
            var reasons = new List<string>();
            var closed = M.OpeningsOf(vault.Cell);
            row.Measured.Add("closed_openings", closed.Select(o => (object)M.NodeName(o.Other(vault.Cell))).ToList());
            var closedElsewhere = closed.Where(o => o.Other(vault.Cell) != prison.Cell).ToList();
            if (closedElsewhere.Count > 0) reasons.Add("closed, the vault opens to " + string.Join(", ", closedElsewhere.Select(o => M.NodeName(o.Other(vault.Cell)))));
            bool passage = closed.Count > closedElsewhere.Count;
            if (passage)
            {
                // NavMesh crosses the prison edge: a passage through the rock that can end at the closed door inside
                // the prison cell. Sealed means the prison room has no path into the vault.
                var cp = FloorPoint(M, prison.Cell, L.Centre(prison.Cell));
                var cv = FloorPoint(M, vault.Cell, L.Centre(vault.Cell));
                if (cp == null || cv == null) reasons.Add("closed, the vault already opens to " + prison.Id);
                else
                {
                    PathCorners(M, cp.Value, cv.Value, out var cst);
                    row.Measured.Add("closed_path_prison_to_vault", cst.ToString());
                    if (cst == NavMeshPathStatus.PathComplete) reasons.Add("closed, the prison already reaches the vault (PathComplete)");
                }
            }
            bool onRoute = L.Routes.Any(r => r.Cells.Contains(vault.Cell));
            if (onRoute) reasons.Add("level.json puts the vault on a route");

            // open it generically: switch off every collider near the shared edge's doorway span, re-bake
            var a = secret.A; var b = secret.B.Value;
            var edge = L.SharedEdgeCentre(a, b);
            float floor = FloorOf(M, prison.Cell);
            bool alongZ = a.C != b.C;  // cells side by side east-west -> the edge runs north-south
            float R = Tune.VaultDisableRadius;
            float yLo = floor + Tune.VaultFloorClearance, yHi = floor + Tune.VaultDoorHeight;
            var centre = new Vector3(edge.x, (yLo + yHi) * 0.5f, edge.z);
            var half = alongZ ? new Vector3(R, (yHi - yLo) * 0.5f + R, Tune.VaultSpanHalf + R)
                              : new Vector3(Tune.VaultSpanHalf + R, (yHi - yLo) * 0.5f + R, R);
            Physics.SyncTransforms();
            var hits = Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            var disabled = new List<Collider>();
            var kept = new List<object>();
            foreach (var c in hits)
            {
                if (c == null || !c.enabled || c is CharacterController) continue;
                var bb = c.bounds;
                if (bb.max.y <= floor + Tune.VaultFloorClearance) { kept.Add(c.name + " (floor)"); continue; }
                if (bb.size.x > Tune.VaultSkipLargerThan || bb.size.z > Tune.VaultSkipLargerThan) { kept.Add(c.name + " (too large)"); continue; }
                disabled.Add(c);
            }
            // and the door itself wherever it stands near the span: the walker's E contract is a solid collider with an
            // Interact() method on its GameObject or a parent (SendMessageUpwards), so such a collider is a door
            var doors = new List<object>();
            foreach (var c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(false)))
            {
                if (c == null || !c.enabled || c.isTrigger || c is CharacterController || disabled.Contains(c)) continue;
                var cc = c.bounds.center;
                if (new Vector2(cc.x - edge.x, cc.z - edge.z).magnitude > Tune.VaultInteractReach || !Interactable(c.transform)) continue;
                disabled.Add(c);
                doors.Add(Path(c.transform));
            }
            row.Measured.Add("opened_by_disabling", disabled.Select(c => (object)Path(c.transform)).ToList())
                .Add("interactable_doors", doors)
                .Add("kept_near_span", kept);
            try
            {
                foreach (var c in disabled) c.enabled = false;
                Physics.SyncTransforms();
                MOpen = NavBake.Bake(L, scene, M.Links, "vault opened");
                var open = MOpen.OpeningsOf(vault.Cell);
                var names = open.Select(o => MOpen.NodeName(o.Other(vault.Cell))).ToList();
                row.Measured.Add("opened_openings", names.Cast<object>().ToList())
                    .Add("opened_vault_navmesh_m2", MOpen.AreaInCell(vault.Cell, true));
                bool onlyPrison = open.Count == 1 && open[0].Other(vault.Cell) == prison.Cell;
                if (!onlyPrison)
                    reasons.Add(open.Count == 0 ? "opened, the vault still has no opening (nothing near the doorway span was in the way, or the vault has no floor)"
                                                : "opened, the vault opens to " + string.Join(", ", names) + " (want: " + prison.Id + " only)");
                // a real path into the opened vault, and the walk
                var pp = FloorPoint(MOpen, prison.Cell, L.Centre(prison.Cell));
                var vp = FloorPoint(MOpen, vault.Cell, L.Centre(vault.Cell));
                if (pp != null && vp != null)
                {
                    PathCorners(MOpen, pp.Value, vp.Value, out var st);
                    row.Measured.Add("opened_path_prison_to_vault", st.ToString());
                    if (st != NavMeshPathStatus.PathComplete) reasons.Add("opened, no complete path prison -> vault (" + st + ")");
                    if (doWalk) Walks.Add(Walker.Walk(MOpen, "prison to vault (opened)", new List<Vector3?> { pp, vp }));
                }
                else if (doWalk) Walks.Add(WalkResult.Missing("prison to vault (opened)", "no floor point in the " + (pp == null ? "prison" : "vault")));
            }
            finally
            {
                foreach (var c in disabled) if (c != null) c.enabled = true;
                Physics.SyncTransforms();
            }
            bool ok = reasons.Count == 0;
            row.Short = ok ? "sealed when closed; opened, a dead end off the prison" : string.Join("; ", reasons);
            row.Reason = ok
                ? (passage ? "closed, a passage crosses the " + prison.Id + " edge but the prison has no path into the vault"
                           : "closed, the vault has no opening") +
                  "; with the " + disabled.Count + " colliders within " + R + " m of the " + secret.Id + " span or with an Interact() method within " +
                  Tune.VaultInteractReach + " m of it switched off and the NavMesh re-baked, its only opening is " + prison.Id + ", so it is a dead end and on no route"
                : string.Join("; ", reasons);
            return ok;
        }

        /// <summary>True when the transform or a parent has a MonoBehaviour with a parameterless Interact() method.</summary>
        static bool Interactable(Transform t)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            for (; t != null; t = t.parent)
                foreach (var mb in t.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().GetMethod("Interact", flags, null, Type.EmptyTypes, null) != null) return true;
            return false;
        }

        static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        // ---------------------------------------------------------------------------------------------
        // 6. throne room: one door, from the hall

        bool? Check6_Throne(CheckRow row)
        {
            if (throne == null || hall == null) { row.Reason = "level.json names no throne room or great hall"; return null; }
            var ops = M.OpeningsOf(throne.Cell);
            var names = ops.Select(o => M.NodeName(o.Other(throne.Cell))).ToList();
            row.Measured.Add("openings", names.Cast<object>().ToList());
            bool ok = ops.Count == 1 && ops[0].Other(throne.Cell) == hall.Cell;
            if (ops.Count == 1) row.Measured.Add("gaps", ops[0].Gaps);
            row.Short = ok ? "one opening, to the hall" : "openings: " + (names.Count == 0 ? "none" : string.Join(", ", names));
            row.Reason = ok ? "the throne room's only opening is to " + hall.Id : "the throne room opens to " + (names.Count == 0 ? "nothing" : string.Join(", ", names)) + " (want: " + hall.Id + " only)";
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // Height-accurate sampling. NavMesh.CalculateTriangulation returns polygon triangles whose corners
        // can sit on different levels (a stage and the floor below its steps can share a polygon), so the
        // stage, the gallery and the climbs are found with NavMesh.SamplePosition, which reads the detail mesh.

        public struct Sample
        {
            public int Iu, Iv;
            public float U, V;
            public Vector3 P;
        }

        /// <summary>NavMesh points within <paramref name="tol"/> of height <paramref name="y"/>, on a
        /// Tune.SampleStep grid over the frame's cell from its side edge out to <paramref name="vMax"/>.</summary>
        List<Sample> SampleAt(SideFrame F, float vMax, float y, float tol)
        {
            float step = Tune.SampleStep;
            var list = new List<Sample>();
            int nu = Mathf.FloorToInt(F.Width / step), nv = Mathf.FloorToInt(Mathf.Min(vMax, F.Width) / step);
            for (int iu = 0; iu < nu; iu++)
            for (int iv = 0; iv < nv; iv++)
            {
                float u = (iu + 0.5f) * step, v = (iv + 0.5f) * step;
                var q = F.World(u, v, y);
                if (!NavMesh.SamplePosition(q, out var hit, tol + 0.05f, M.Filter)) continue;
                var p = hit.position;
                if (Horizontal(p - q) > step * 0.5f || Mathf.Abs(p.y - y) > tol || M.NodeOf(p) != F.Cell) continue;
                list.Add(new Sample { Iu = iu, Iv = iv, U = u, V = v, P = p });
            }
            return list;
        }

        static long GridKey(int a, int b) => ((long)a << 32) | (uint)b;

        static List<List<Sample>> Clusters(List<Sample> s, bool diagonal)
        {
            var at = new Dictionary<long, int>();
            for (int i = 0; i < s.Count; i++) at[GridKey(s[i].Iu, s[i].Iv)] = i;
            var seen = new bool[s.Count];
            var result = new List<List<Sample>>();
            for (int i = 0; i < s.Count; i++)
            {
                if (seen[i]) continue;
                var cl = new List<Sample>();
                var st = new Stack<int>();
                st.Push(i); seen[i] = true;
                while (st.Count > 0)
                {
                    int k = st.Pop();
                    cl.Add(s[k]);
                    for (int du = -1; du <= 1; du++)
                    for (int dv = -1; dv <= 1; dv++)
                    {
                        if ((du == 0 && dv == 0) || (!diagonal && du != 0 && dv != 0)) continue;
                        if (at.TryGetValue(GridKey(s[k].Iu + du, s[k].Iv + dv), out var j) && !seen[j]) { seen[j] = true; st.Push(j); }
                    }
                }
                result.Add(cl);
            }
            return result;
        }

        static Vector3 Mean(List<Sample> s) =>
            new Vector3(s.Average(x => x.P.x), s.Average(x => x.P.y), s.Average(x => x.P.z));

        bool PathOk(Vector3 a, Vector3 b)
        {
            PathCorners(M, a, b, out var st);
            return st == NavMeshPathStatus.PathComplete;
        }

        /// <summary>Sampled points the map paints over the triangles: stage, gallery deck, climbs.</summary>
        public readonly List<Vector3> StageSamples = new List<Vector3>(), DeckSamples = new List<Vector3>(), ClimbSamples = new List<Vector3>();

        // ---------------------------------------------------------------------------------------------
        // 7. gallery

        bool? Check7_Gallery(CheckRow row)
        {
            var gal = L.Feature("gallery");
            if (gal == null || hall == null) { row.Reason = "level.json has no gallery feature or great hall"; return null; }
            var side = LevelSpec.SideWord(gal.Side) ?? "south";
            float deck = gal.DeckM ?? 5f;
            var F = hallFrame = new SideFrame(L, hall.Cell, side);
            float floor = FloorOf(M, hall.Cell);
            float step = Tune.SampleStep;
            var reasons = new List<string>();

            // (a) the deck: NavMesh at deck +- tolerance within GallerySideBand of the side edge, reachable
            var deckS = SampleAt(F, Tune.GallerySideBand, floor + deck, Tune.GalleryHeightTolerance);
            DeckSamples.AddRange(deckS.Select(s => s.P));
            float area = deckS.Count * step * step;
            float deckH = float.NaN;
            bool reachable = false;
            var ja = new JObj().Add("side", side).Add("area_m2", area);
            if (deckS.Count > 0)
            {
                var ys = deckS.Select(s => s.P.y - floor).OrderBy(y => y).ToList();
                deckH = ys[ys.Count / 2];
                ja.Add("height_m", deckH).Add("span_m", deckS.Max(s => s.U) - deckS.Min(s => s.U) + step)
                  .Add("depth_m", deckS.Max(s => s.V) + step * 0.5f);
                var centroid = Mean(deckS);
                galleryPoint = deckS.OrderBy(s => (s.P - centroid).sqrMagnitude).First().P;
                galleryLow = deckS.OrderBy(s => s.U).ThenBy(s => s.V).First().P;
                galleryHigh = deckS.OrderBy(s => -s.U).ThenBy(s => s.V).First().P;
                if (hallPoint != null)
                {
                    PathCorners(M, hallPoint.Value, galleryPoint.Value, out var st);
                    reachable = st == NavMeshPathStatus.PathComplete;
                    ja.Add("path_hall_floor_to_gallery", st.ToString());
                }
            }
            ja.Add("reachable_from_hall_floor", reachable);
            row.Measured.Add("a_deck", ja);
            if (area < Tune.GalleryMinArea)
                reasons.Add("no gallery: " + area.ToString("0.#") + " m2 of NavMesh at " + (deck - Tune.GalleryHeightTolerance) + ".." +
                            (deck + Tune.GalleryHeightTolerance) + " m within " + Tune.GallerySideBand + " m of the " + side + " wall");
            else if (!reachable) reasons.Add("the gallery is not reachable from the hall floor");

            // (b) climbs: NavMesh at every height from floor + ClimbBandLow to deck - ClimbBandTopMargin, in one
            // piece, joined by complete paths to the hall floor below and the deck above
            float bandLo = Tune.ClimbBandLow, bandHi = deck - Tune.ClimbBandTopMargin;
            var climbS = new Dictionary<long, Sample>();
            for (float y = bandLo; y <= bandHi + 1e-3f; y += Tune.ClimbSampleRise)
                foreach (var s in SampleAt(F, F.Width, floor + y, Tune.ClimbSampleRise * 0.5f))
                    climbS[GridKey(s.Iu, s.Iv)] = s;
            ClimbSamples.AddRange(climbS.Values.Select(s => s.P));
            var climbs = new List<object>();
            bool lowClimb = false, highClimb = false;
            foreach (var cl in Clusters(climbS.Values.ToList(), true))
            {
                float minY = cl.Min(s => s.P.y) - floor, maxY = cl.Max(s => s.P.y) - floor;
                if (minY > bandLo + Tune.ClimbSampleRise || maxY < bandHi - Tune.ClimbSampleRise) continue; // does not rise the whole way
                var lo = cl.OrderBy(s => s.P.y).First().P;
                var hi = cl.OrderBy(s => -s.P.y).First().P;
                bool joinsFloor = hallPoint != null && PathOk(lo, hallPoint.Value);
                bool joinsDeck = galleryPoint != null && PathOk(hi, galleryPoint.Value);
                float u = cl.Average(s => s.U);
                string where = u <= Tune.ClimbEndZone ? F.LowEnd : u >= F.Width - Tune.ClimbEndZone ? F.HighEnd : "middle";
                if (joinsFloor && joinsDeck && where == F.LowEnd) lowClimb = true;
                if (joinsFloor && joinsDeck && where == F.HighEnd) highClimb = true;
                climbs.Add(new JObj().Add("at", where).Add("centre", Mean(cl)).Add("u_m", u)
                    .Add("rises_m", new List<object> { minY, maxY }).Add("joins_floor", joinsFloor).Add("joins_deck", joinsDeck)
                    .Add("area_m2", cl.Count * step * step));
            }
            row.Measured.Add("b_climbs", climbs);
            if (!lowClimb) reasons.Add("no climb near the hall's " + F.LowEnd + " end");
            if (!highClimb) reasons.Add("no climb near the hall's " + F.HighEnd + " end");

            // (c) from each side doorway to the gallery without touching the central floor
            var jc = new JObj().Add("central_floor_uv", new List<object>
            {
                Tune.CentralInsetEnds, F.Width - Tune.CentralInsetEnds, Tune.CentralInsetGallerySide, F.Width - Tune.CentralInsetFar
            });
            foreach (var end in new[] { "low", "high" })
            {
                var nb = end == "low" ? F.LowNeighbour : F.HighNeighbour;
                var endName = end == "low" ? F.LowEnd : F.HighEnd;
                var o = L.InGrid(nb) ? M.Find(hall.Cell, nb) : null;
                if (o == null) { reasons.Add("the hall has no " + endName + " doorway"); jc.Add(endName, "no doorway"); continue; }
                var outward = (L.Centre(nb) - L.Centre(hall.Cell)).normalized;
                var s0 = Snap(M, o.Crossing + outward * 1f, 1.5f) ?? Snap(M, o.Crossing, 1.5f);
                if (end == "low") lowDoorStart = s0; else highDoorStart = s0;
                if (s0 == null || galleryPoint == null) { reasons.Add("cannot path from the " + endName + " doorway to the gallery"); jc.Add(endName, "no start or no gallery"); continue; }
                var corners = PathCorners(M, s0.Value, galleryPoint.Value, out var st);
                int bad = 0; Vector3? firstBad = null;
                foreach (var p in Densify(corners, Tune.PathSampleStep))
                {
                    if (M.NodeOf(p) != hall.Cell || p.y - floor >= Tune.FloorLevelMaxRise) continue;
                    float u = F.U(p), v = F.V(p);
                    if (u >= Tune.CentralInsetEnds && u <= F.Width - Tune.CentralInsetEnds &&
                        v >= Tune.CentralInsetGallerySide && v <= F.Width - Tune.CentralInsetFar)
                    { bad++; if (firstBad == null) firstBad = p; }
                }
                jc.Add(endName, new JObj().Add("status", st.ToString()).Add("length_m", PathLength(corners))
                    .Add("points_on_central_floor", bad).Add("first_on_central_floor", firstBad.HasValue ? (object)firstBad.Value : null));
                if (st != NavMeshPathStatus.PathComplete) reasons.Add("no complete path from the " + endName + " doorway to the gallery (" + st + ")");
                else if (bad > 0) reasons.Add("from the " + endName + " doorway the gallery is reached across the hall floor (at " + Fmt(firstBad.Value) + ")");
            }
            row.Measured.Add("c_side_paths", jc);

            // (d) visibility: eyes at the deck's hall-side NavMesh edge, every RailSampleStep along it
            var eyes = new List<Vector3>();
            var bins = new Dictionary<int, Sample>();
            foreach (var s in deckS)
            {
                int b = Mathf.FloorToInt(s.U / Tune.RailSampleStep);
                if (!bins.TryGetValue(b, out var cur) || s.V > cur.V) bins[b] = s;
            }
            foreach (var s in bins.Values)
            {
                var edge = s.P;
                if (NavMesh.Raycast(s.P, s.P + F.Inward * 5f, out var eh, M.Filter)) edge = eh.position;
                eyes.Add(edge + F.Inward * Tune.RailLean + Vector3.up * Tune.EyeHeight);
            }
            int samples = 0, seen = 0, underLowLid = 0;
            var hiddenAt = new List<object>();
            float vStart = (deckS.Count > 0 ? deckS.Max(s => s.V) : 0f) + 0.5f;   // north of the gallery footprint
            float openAbove = (float.IsNaN(deckH) ? deck : deckH) + Tune.HallOpenAbove;
            bool oldBack = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
            for (float u = Tune.VisibilityInsetEnds + Tune.FloorSampleStep * 0.5f; u < F.Width - Tune.VisibilityInsetEnds; u += Tune.FloorSampleStep)
            for (float v = vStart; v < F.Width; v += Tune.FloorSampleStep)
            {
                var probe = F.World(u, v, floor + 0.5f);
                if (!NavMesh.SamplePosition(probe, out var hit, 0.6f, M.Filter)) continue;
                var hp = hit.position;
                if (Horizontal(hp - probe) > 0.3f || Mathf.Abs(hp.y - floor) > 0.5f || M.NodeOf(hp) != hall.Cell) continue;
                int t = M.FindTri(hp, 1f);
                if (t < 0 || !M.IsLevel(t)) continue;
                // the hall floor lies under the hall's own (tall) volume; a lower lid means a side room or passage
                if (Physics.Raycast(hp + Vector3.up * 0.1f, Vector3.up, out var lid, Tune.CeilingMaxRay, ~0, QueryTriggerInteraction.Ignore)
                    && lid.distance + 0.1f < openAbove) { underLowLid++; continue; }
                samples++;
                var target = hp + Vector3.up * Tune.FloorTargetLift;
                bool vis = false;
                foreach (var e in eyes)
                    if (!Physics.Linecast(e, target, ~0, QueryTriggerInteraction.Ignore)) { vis = true; break; }
                if (vis) seen++;
                else if (hiddenAt.Count < 40) hiddenAt.Add(hp);
            }
            }
            finally { Physics.queriesHitBackfaces = oldBack; }
            float frac = samples > 0 ? seen / (float)samples : 0f;
            row.Measured.Add("d_visibility", new JObj().Add("eyes", eyes.Count).Add("floor_samples", samples)
                .Add("skipped_under_lid_below_m", openAbove).Add("skipped_samples", underLowLid)
                .Add("visible_fraction", frac).Add("hidden_samples_first_40", hiddenAt));
            if (eyes.Count == 0) reasons.Add("no rail to look from");
            else if (frac < Tune.MinVisibleFraction) reasons.Add("the rail sees " + (frac * 100).ToString("0") + "% of the hall floor (want " + (Tune.MinVisibleFraction * 100) + "%)");

            // (e) under_gallery: the hall's opening on the gallery side lies below the deck
            var under = L.InGrid(F.SideNeighbour) ? M.Find(hall.Cell, F.SideNeighbour) : null;
            if (under == null) { reasons.Add("no opening on the gallery side (under_gallery)"); row.Measured.Add("e_under_gallery", "no opening"); }
            else
            {
                float best = float.MaxValue;
                foreach (var s in deckS)
                    if (s.P.y >= under.Crossing.y + 2f) best = Mathf.Min(best, Horizontal(s.P - under.Crossing));
                row.Measured.Add("e_under_gallery", new JObj().Add("to", M.NodeName(F.SideNeighbour)).Add("crossing", under.Crossing)
                    .Add("deck_within_m", best < float.MaxValue ? best : float.NaN));
                if (best > Tune.UnderGalleryMaxDist) reasons.Add("the opening to " + M.NodeName(F.SideNeighbour) + " is not under the gallery");
            }

            bool ok = reasons.Count == 0;
            row.Short = ok ? "deck " + deckH.ToString("0.0") + " m, climbs at both ends, side paths clear, " + (frac * 100).ToString("0") + "% of floor seen"
                           : string.Join("; ", reasons);
            row.Reason = ok
                ? "a " + area.ToString("0") + " m2 deck " + deckH.ToString("0.0") + " m up along the " + side + " side, reachable from the floor; a climb at each end; both side doorways reach it without the central floor; the rail sees " + (frac * 100).ToString("0") + "% of the floor; under_gallery opens beneath it"
                : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 8. stage

        bool? Check8_Stage(CheckRow row)
        {
            var stage = L.Feature("stage");
            if (stage == null || throne == null) { row.Reason = "level.json has no stage feature or throne room"; return null; }
            float rise = stage.RiseM ?? 1.25f;
            var side = LevelSpec.SideWord(stage.Side) ?? "north";
            var F = new SideFrame(L, throne.Cell, side);
            float floor = FloorOf(M, throne.Cell);
            float step = Tune.SampleStep;
            var reasons = new List<string>();

            // the largest connected patch of NavMesh at rise +- tolerance is the stage
            var best = Clusters(SampleAt(F, F.Width, floor + rise, Tune.StageTolerance), false)
                .OrderByDescending(c => c.Count).FirstOrDefault() ?? new List<Sample>();
            StageSamples.AddRange(best.Select(s => s.P));
            float area = best.Count * step * step;
            row.Measured.Add("area_m2", area);
            if (area < Tune.StageMinArea)
            {
                row.Short = "no stage at " + rise + " m";
                row.Reason = "no NavMesh at " + rise + " +- " + Tune.StageTolerance + " m in " + throne.Id + " (largest patch " + area.ToString("0.#") + " m2)";
                return false;
            }
            var ys = best.Select(s => s.P.y - floor).OrderBy(y => y).ToList();
            float h = ys[ys.Count / 2];
            float uMin = best.Min(s => s.U) - step * 0.5f, uMax = best.Max(s => s.U) + step * 0.5f;
            var centroid = Mean(best);
            // the room's floor width along the stage side, from floor-level samples (information only)
            var floorS = SampleAt(F, F.Width, floor, 0.3f);
            float fuMin = floorS.Count > 0 ? floorS.Min(s => s.U) - step * 0.5f : 0f;
            float fuMax = floorS.Count > 0 ? floorS.Max(s => s.U) + step * 0.5f : L.Tile;
            float width = uMax - uMin, roomWidth = fuMax - fuMin;
            bool onSide = F.V(centroid) < L.Tile * 0.5f;
            stagePoint = best.OrderBy(s => (s.P - centroid).sqrMagnitude).First().P;
            var throneFloor = FloorPoint(M, throne.Cell, L.Centre(throne.Cell));
            bool reachable = throneFloor != null && PathOk(throneFloor.Value, stagePoint.Value);

            // the stage's front edge: the front-most sample in each column; the front line runs StageFrontLineOffset
            // in front of the median front edge
            var front = new Dictionary<int, Sample>();
            foreach (var s in best) if (!front.TryGetValue(s.Iu, out var cur) || s.V > cur.V) front[s.Iu] = s;
            var frontVs = front.Values.Select(s => s.V).OrderBy(v => v).ToList();
            float vFront = frontVs[frontVs.Count / 2] + step * 0.5f;
            float vLine = vFront + Tune.StageFrontLineOffset;

            // the FREE width at the front line: NavMesh (floor, steps or stage height) on the SampleStep grid along
            // the line, contiguous from the column nearest the stage's centre out to a wall (no sample) each way
            int nu = Mathf.FloorToInt(F.Width / step);
            var line = new Dictionary<int, Vector3>();
            for (int iu = 0; iu < nu; iu++)
            {
                float u = (iu + 0.5f) * step;
                foreach (float dy in new[] { 0f, rise * 0.5f, rise })
                {
                    var q = F.World(u, vLine, floor + dy);
                    if (!NavMesh.SamplePosition(q, out var hit, 0.45f, M.Filter)) continue;
                    var p = hit.position;
                    if (Horizontal(p - q) > step * 0.5f || p.y < floor - 0.3f || p.y > floor + rise + Tune.StageTolerance || M.NodeOf(p) != F.Cell) continue;
                    line[iu] = p;
                    break;
                }
            }
            int cu = Mathf.Clamp(Mathf.FloorToInt(F.U(centroid) / step), 0, nu - 1), seed = -1;
            for (int d = 0; d < nu && seed < 0; d++)
                foreach (int k in new[] { cu - d, cu + d })
                    if (line.ContainsKey(k) && (k + 0.5f) * step >= uMin && (k + 0.5f) * step <= uMax) { seed = k; break; }
            int lo = seed, hi = seed;
            if (seed >= 0)
            {
                while (line.ContainsKey(lo - 1)) lo--;
                while (line.ContainsKey(hi + 1)) hi++;
            }
            float fL = lo * step, fR = (hi + 1) * step;
            float freeWidth = seed >= 0 ? fR - fL : 0f;
            float widthFrac = freeWidth > 0 ? width / freeWidth : 0f;

            // steps across the free width: for each column of the free run (less 0.5 m at each wall), the stage's
            // front-most sample there must step down -- a straight NavMesh ray StageFrontProbe metres further out
            // reaches the floor without meeting a NavMesh edge. A column with no stage counts as no step.
            int cols = 0, stepped = 0;
            var blockedAt = new List<object>();
            if (seed >= 0)
                for (int iu = lo; iu <= hi; iu++)
                {
                    float u = (iu + 0.5f) * step;
                    if (u < fL + 0.5f || u > fR - 0.5f) continue;
                    cols++;
                    if (!front.TryGetValue(iu, out var s)) { if (blockedAt.Count < 20) blockedAt.Add(line[iu]); continue; }
                    var e = Snap(M, F.World(s.U, s.V + Tune.StageFrontProbe, floor + 0.3f), 1.5f);
                    if (e != null && e.Value.y - floor < 0.6f && !NavMesh.Raycast(s.P, e.Value, out _, M.Filter)) stepped++;
                    else if (blockedAt.Count < 20) blockedAt.Add(s.P);
                }
            float stepFrac = cols > 0 ? stepped / (float)cols : 0f;
            row.Measured.Add("height_m", h).Add("width_m", width).Add("free_width_at_front_m", freeWidth)
                .Add("front_line", seed >= 0 ? (object)new List<object> { F.World(fL, vLine, floor), F.World(fR, vLine, floor) } : null)
                .Add("width_fraction_of_free", widthFrac).Add("room_floor_width_m", roomWidth)
                .Add("centre", centroid).Add("on_" + side + "_half", onSide)
                .Add("reachable_from_floor", reachable).Add("front_columns", cols).Add("front_stepped_fraction", stepFrac)
                .Add("front_blocked_at", blockedAt);
            if (seed < 0) reasons.Add("no NavMesh on a line " + Tune.StageFrontLineOffset + " m in front of the stage");
            if (!onSide) reasons.Add("the stage is not in the room's " + side + " half");
            if (!reachable) reasons.Add("the stage is not reachable from the room's floor");
            if (seed >= 0 && stepFrac < Tune.StageMinStepFraction)
                reasons.Add("steps cover " + (stepFrac * 100).ToString("0") + "% of the " + freeWidth.ToString("0.#") + " m free width at the stage's front line (want " +
                            (Tune.StageMinStepFraction * 100) + "%; the stage is " + width.ToString("0.#") + " m wide)");
            bool ok = reasons.Count == 0;
            row.Short = ok ? "stage " + h.ToString("0.00") + " m, steps on " + (stepFrac * 100).ToString("0") + "% of the " + freeWidth.ToString("0.#") + " m free width at its front" : string.Join("; ", reasons);
            row.Reason = ok ? "a " + area.ToString("0") + " m2 stage " + h.ToString("0.00") + " m up on the " + side + " side, " + width.ToString("0.#") + " m wide, reached by steps along " +
                              (stepFrac * 100).ToString("0") + "% of the " + freeWidth.ToString("0.#") + " m free width at its front line (the room cell's floor is " + roomWidth.ToString("0.#") + " m wide)"
                            : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 9. heights: the share of each room's floor under a low or a tall ceiling
        //    LOW:  at least LowMinFraction of the floor samples under a ceiling of at most LowCeilingMax
        //    TALL: at least TallMinFraction of the floor samples under a ceiling of at least TallCeilingMin
        //    Rooms of the "low" and "tall" classes are scored; standard rooms are reported, not scored.
        //    An "about N m" room reports its HeightTargetPercentile ceiling against N, as information.

        bool? Check9_Heights(CheckRow row)
        {
            var j = new JObj();
            var lowFrac = new Dictionary<string, float>();
            var tallFrac = new Dictionary<string, float>();
            var pctl = new Dictionary<string, float>();
            var sampled = new HashSet<string>();
            bool oldBack = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            List<Renderer> renderers = null;
            try
            {
                foreach (var r in L.Rooms)
                {
                    float floor = FloorOf(M, r.Cell);
                    var hits = new List<float>();
                    var pts = new List<Vector3>();
                    var open = new List<Vector3>();
                    for (float x = L.XMin(r.Cell) + Tune.CeilingSampleStep * 0.5f; x < L.XMax(r.Cell); x += Tune.CeilingSampleStep)
                    for (float z = L.ZMin(r.Cell) + Tune.CeilingSampleStep * 0.5f; z < L.ZMax(r.Cell); z += Tune.CeilingSampleStep)
                    {
                        var probe = new Vector3(x, floor + 0.5f, z);
                        if (!NavMesh.SamplePosition(probe, out var hit, 1f, M.Filter)) continue;
                        var p = hit.position;
                        if (M.NodeOf(p) != r.Cell || Mathf.Abs(p.y - floor) > 0.6f) continue;
                        int t = M.FindTri(p, 0.6f);
                        if (t < 0 || !M.IsLevel(t)) continue;
                        pts.Add(p);
                        if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, out var rh, Tune.CeilingMaxRay, ~0, QueryTriggerInteraction.Ignore))
                            hits.Add(rh.distance + 0.1f);
                        else open.Add(p);
                    }
                    string source = "physics";
                    if (open.Count > 0)
                    {
                        // fallback, per sample with no collider above it: a ceiling built without colliders -- the lowest
                        // wide renderer above the point (a room can have colliders overhead in places and none elsewhere)
                        renderers = renderers ?? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).ToList();
                        int before = hits.Count;
                        foreach (var p in open)
                        {
                            float bestH = float.MaxValue;
                            foreach (var rd in renderers)
                            {
                                var b = rd.bounds;
                                if (b.size.x * b.size.z < 4f || b.min.y < p.y + 2f) continue;
                                if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                                bestH = Mathf.Min(bestH, b.min.y - p.y);
                            }
                            if (bestH < float.MaxValue) hits.Add(bestH);
                        }
                        int found = hits.Count - before;
                        if (found > 0) source = before == 0 ? "renderer bounds (no ceiling colliders)" : "physics; renderer bounds for " + found + " samples with no collider above";
                    }
                    int misses = pts.Count - hits.Count;
                    hits.Sort();
                    // fractions are of every floor sample: a sample with no ceiling above it is neither low nor tall
                    float lf = pts.Count > 0 ? hits.Count(x => x <= Tune.LowCeilingMax) / (float)pts.Count : 0f;
                    float tf = pts.Count > 0 ? hits.Count(x => x >= Tune.TallCeilingMin) / (float)pts.Count : 0f;
                    float At(float q) => hits.Count > 0 ? hits[Mathf.Clamp(Mathf.FloorToInt(q * hits.Count), 0, hits.Count - 1)] : float.NaN;
                    float med = At(0.5f), p90 = At(Tune.HeightTargetPercentile);
                    if (hits.Count > 0) sampled.Add(r.Id);
                    lowFrac[r.Id] = lf; tallFrac[r.Id] = tf; pctl[r.Id] = p90;
                    string role = r.HeightClass == "low" ? "scored: low" : r.HeightClass == "tall" ? "scored: tall" : "reported";
                    j.Add(r.Id, new JObj().Add("class", r.HeightClass).Add("role", role).Add("samples", pts.Count).Add("open_above", misses)
                        .Add("low_fraction", lf).Add("tall_fraction", tf)
                        .Add("median_ceiling_m", med).Add("p90_ceiling_m", p90).Add("source", source));
                }
            }
            finally { Physics.queriesHitBackfaces = oldBack; }
            row.Measured.Add("rooms", j);

            string Pc(float f) => (f * 100).ToString("0") + "%";
            var reasons = new List<string>();
            var lowParts = new List<string>();
            var tallParts = new List<string>();
            foreach (var r in L.Rooms)
            {
                if (r.HeightClass != "low" && r.HeightClass != "tall") continue;
                if (!sampled.Contains(r.Id)) { reasons.Add(r.Id + " has no measurable ceiling"); continue; }
                if (r.HeightClass == "low")
                {
                    lowParts.Add(r.Id + " " + Pc(lowFrac[r.Id]));
                    if (lowFrac[r.Id] < Tune.LowMinFraction)
                        reasons.Add(r.Id + " is not low: " + Pc(lowFrac[r.Id]) + " of its floor is under a ceiling of at most " + Tune.LowCeilingMax + " m (want " + Pc(Tune.LowMinFraction) + ")");
                }
                else
                {
                    tallParts.Add(r.Id + " " + Pc(tallFrac[r.Id]));
                    if (tallFrac[r.Id] < Tune.TallMinFraction)
                        reasons.Add(r.Id + " is not tall: " + Pc(tallFrac[r.Id]) + " of its floor is under a ceiling of at least " + Tune.TallCeilingMin + " m (want " + Pc(Tune.TallMinFraction) + ")");
                }
            }
            // "about N m" rooms: information only
            var targets = new JObj();
            var targetText = new List<string>();
            foreach (var r in L.Rooms)
            {
                if (r.HeightTargetM == null || !sampled.Contains(r.Id)) continue;
                float p = pctl[r.Id], n = r.HeightTargetM.Value;
                bool near = Mathf.Abs(p - n) <= Tune.HeightTargetTolerance;
                targets.Add(r.Id, new JObj().Add("about_m", n).Add("p90_ceiling_m", p).Add("within_tolerance", near));
                targetText.Add(r.Id + " p" + Mathf.RoundToInt(Tune.HeightTargetPercentile * 100) + " " + p.ToString("0.0") + " m against about " + n + " m" +
                               (near ? "" : " (off by more than " + Tune.HeightTargetTolerance + " m)"));
            }
            row.Measured.Add("targets_information", targets);
            bool ok = reasons.Count == 0;
            row.Short = "low (<= " + Tune.LowCeilingMax + " m, want " + Pc(Tune.LowMinFraction) + "): " + string.Join(", ", lowParts) +
                        "; tall (>= " + Tune.TallCeilingMin + " m, want " + Pc(Tune.TallMinFraction) + "): " + string.Join(", ", tallParts);
            string info = targetText.Count > 0 ? " Information, not scored: " + string.Join("; ", targetText) + "." : "";
            row.Reason = (ok ? "every low room has at least " + Pc(Tune.LowMinFraction) + " of its floor under a ceiling of at most " + Tune.LowCeilingMax +
                               " m, and every tall room at least " + Pc(Tune.TallMinFraction) + " under one of at least " + Tune.TallCeilingMin + " m; standard rooms are reported, not scored."
                             : string.Join("; ", reasons) + ".") + info;
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 10. walk

        void WalkRoutesClosed()
        {
            if (!doWalk) return;
            foreach (var id in new[] { "west_wing", "east_wing" })
            {
                var route = L.Route(id);
                string name = "start to hall, " + id.Replace('_', ' ');
                if (route == null || hall == null) { Walks.Add(WalkResult.Missing(name, "no route " + id)); continue; }
                var wps = new List<Vector3?> { start };
                string missing = null;
                for (int i = 0; i + 1 < route.Cells.Count; i++)
                {
                    var o = M.Find(route.Cells[i], route.Cells[i + 1]);
                    if (o == null) { missing = "no opening " + L.NodeName(route.Cells[i]) + "-" + L.NodeName(route.Cells[i + 1]); break; }
                    wps.Add(Snap(M, o.Crossing, 1.5f));
                }
                wps.Add(hallPoint);
                Walks.Add(missing != null ? WalkResult.Missing(name, missing) : Walker.Walk(M, name, wps));
            }
            var F = hallFrame;
            string lowName = F != null ? F.LowEnd : "west", highName = F != null ? F.HighEnd : "east";
            Walks.Add(Walker.Walk(M, "hall " + lowName + " door to gallery, " + lowName + " stair", new List<Vector3?> { lowDoorStart, galleryLow }));
            Walks.Add(Walker.Walk(M, "hall " + highName + " door to gallery, " + highName + " stair", new List<Vector3?> { highDoorStart, galleryHigh }));
            Walks.Add(Walker.Walk(M, "hall to the throne stage", new List<Vector3?> { hallPoint, stagePoint }));
        }

        bool? Check10_Walk(CheckRow row)
        {
            if (!doWalk) { row.Reason = "walk skipped (-noWalk)"; return null; }
            var bad = Walks.Where(w => !w.Pass).ToList();
            row.Measured.Add("routes", Walks.Select(w => (object)new JObj().Add("route", w.Route).Add("pass", w.Pass)
                .Add("walked_m", w.Walked).Add("planned_m", w.Planned).Add("snags", w.Snags.Count).Add("note", w.Note)).ToList());
            row.Short = (Walks.Count - bad.Count) + "/" + Walks.Count + " routes walked";
            row.Reason = bad.Count == 0 ? "a CharacterController (r " + Tune.AgentRadius + ", h " + Tune.AgentHeight + ", step " + Tune.AgentClimb + ") followed every route to its end"
                : string.Join("; ", bad.Select(w => w.Route + ": " + w.Note));
            return bad.Count == 0;
        }
    }

    /// <summary>Switches off the scene's own NavMesh components, player controllers and the colliders of the
    /// player rig under each controller (Starter Assets' PlayerCapsule carries a visible capsule with its own
    /// collider), and puts them back afterwards. The scene is never saved.</summary>
    public sealed class SceneGuard
    {
        readonly List<Behaviour> off = new List<Behaviour>();
        readonly List<Collider> offColliders = new List<Collider>();
        public readonly List<CharacterController> Controllers = new List<CharacterController>();
        public readonly List<SceneLink> Links;

        public SceneGuard(Scene scene)
        {
            Links = NavBake.ReadLinks(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var s in root.GetComponentsInChildren<NavMeshSurface>(false)) if (s.enabled) { s.enabled = false; off.Add(s); }
                foreach (var l in root.GetComponentsInChildren<NavMeshLink>(false)) if (l.enabled) { l.enabled = false; off.Add(l); }
                foreach (var c in root.GetComponentsInChildren<CharacterController>(false))
                {
                    if (c.enabled) { c.enabled = false; Controllers.Add(c); }
                    // the player's own body would otherwise be baked as an obstacle at the start
                    foreach (var col in c.GetComponentsInChildren<Collider>(false))
                        if (!(col is CharacterController) && col.enabled) { col.enabled = false; offColliders.Add(col); }
                }
            }
            NavMesh.RemoveAllNavMeshData();
            Physics.SyncTransforms();
        }

        public void Restore()
        {
            foreach (var c in Controllers) if (c != null) c.enabled = true;
            foreach (var col in offColliders) if (col != null) col.enabled = true;
            foreach (var b in off) if (b != null) b.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
