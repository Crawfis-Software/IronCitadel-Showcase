// IronCitadel conformance kit v2: scores a scene against brief v2's "What must be true", reading the build's map.json.
// Rows 1-10 are scored (pass / fail / manual); the info rows are measured and reported, never scored.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronCitadel.ConformanceV2
{
    public sealed class CheckRow
    {
        public string Id, MustBeTrue, Status, Short, Reason;
        public JObj Measured = new JObj();

        public JObj ToJson() => new JObj()
            .Add("id", Id).Add("must_be_true", MustBeTrue).Add("status", Status)
            .Add("summary", Short).Add("reason", Reason).Add("measured", Measured);
    }

    /// <summary>How a shut or hidden door was opened for the bakes, and what the E ray found from each side.</summary>
    public sealed class DoorOpen
    {
        public MapDoor Door;
        public readonly List<Collider> Colliders = new List<Collider>();
        public readonly List<string> How = new List<string>();
        public readonly JObj Sides = new JObj();
        public int SidesFound, SidesOk;
    }

    public sealed partial class Scorer
    {
        readonly MapSpec Map;
        readonly Scene scene;
        readonly bool doWalk;
        NavModel M0, M1, M2;
        public NavModel Model => M2 ?? M1 ?? M0;
        public MapSpec MapSpec => Map;
        readonly List<CheckRow> rows = new List<CheckRow>();
        public readonly List<CheckRow> Info = new List<CheckRow>();
        public readonly List<WalkResult> Walks = new List<WalkResult>();
        public readonly List<string> Notes = new List<string>();
        public List<CheckRow> Rows => rows;

        MapRoom entry, armory, hall, throne, vault, vaultNb;
        MapDoor hidden;
        public Vector3 StartPosition;
        string startSource;
        Vector3? sceneStart;
        string sceneStartSource;
        readonly List<NavMeshBuildSource> modifiers = new List<NavMeshBuildSource>();
        int armoryBoxes, hallFloorBoxes;
        readonly Dictionary<MapDoor, DoorOpen> shutOpen = new Dictionary<MapDoor, DoorOpen>();
        DoorOpen hiddenOpen;
        List<Collider> allColliders;

        // shared between rows, and drawn on the map
        Gap choke, flank;
        Vector3? chokeTarget, hallPoint, wayInPoint, deckPoint, thronePoint, vaultPoint, vaultNbPoint;
        List<Vector3> wayRound;
        public readonly List<Vector3> DeckEyes = new List<Vector3>();
        public readonly List<Vector3> HallFloorSamples = new List<Vector3>();
        public readonly List<Vector3> HallFloorHidden = new List<Vector3>();
        public readonly List<Vector3> StageSamples = new List<Vector3>();
        public readonly List<(Vector3 p, bool tall)> Covers = new List<(Vector3, bool)>();
        public readonly List<Vector3> ChokeViolations = new List<Vector3>();
        string vaultClosedStatus;
        List<object> vaultClosedGaps;
        readonly Dictionary<MapRoom, JObj> ceilings = new Dictionary<MapRoom, JObj>();
        readonly Dictionary<MapRoom, (int pts, int hits, float low, float tall, float p90, float med)> ceil =
            new Dictionary<MapRoom, (int, int, float, float, float, float)>();

        public Scorer(MapSpec map, Scene scene, bool walk)
        {
            Map = map; this.scene = scene; doWalk = walk;
        }

        static string Pc(float f) => (f * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string N1(float f) => f.ToString("0.0", CultureInfo.InvariantCulture);
        static string N2(float f) => f.ToString("0.00", CultureInfo.InvariantCulture);
        static float HDist(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        static string Fmt(Vector3 p) => "(" + N1(p.x) + ", " + N1(p.y) + ", " + N1(p.z) + ")";

        // ---------------------------------------------------------------------------------------------
        public JObj Score()
        {
            entry = Map.RoomOfKind("entry hall"); armory = Map.RoomOfKind("armory"); hall = Map.RoomOfKind("dining hall");
            throne = Map.RoomOfKind("throne room"); vault = Map.RoomOfKind("treasure vault");
            hidden = Map.Doors.FirstOrDefault(d => d.State == "hidden" && vault != null && d.Joins(vault)) ??
                     Map.Doors.FirstOrDefault(d => d.State == "hidden");
            if (hidden != null && vault != null && hidden.Joins(vault)) vaultNb = hidden.Other(vault);

            var guard = new SceneGuard(scene, Map);
            var opened = new List<Collider>();
            bool oldBack = Physics.queriesHitBackfaces;
            try
            {
                allColliders = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Collider>(false)).Where(Doors.Solid).ToList();
                BuildModifiers();

                // 1. as built: shut and hidden doors closed
                M0 = NavBake.Bake(Map, scene, guard.Links, "as built", modifiers);
                FindStart(guard);
                OpenShutDoors(opened);

                // 2. shut doors opened, the hidden door closed: the vault must be sealed
                M1 = NavBake.Bake(Map, scene, guard.Links, "shut doors opened", modifiers);
                VaultClosed();
                OpenHiddenDoor(opened);
                OpenOtherDoors(opened);

                // 3. every door opened: the room graph, the paths and the walks
                M2 = NavBake.Bake(Map, scene, guard.Links, "all doors opened", modifiers);
                foreach (var c in opened) if (c != null) c.enabled = true;
                Physics.SyncTransforms();
                if (Map.StartPos != null) StartPosition = Snap(M2, Map.StartPos.Value + Vector3.up * 0.5f, Tune.StartSnapRadius, M2.Filter) ?? StartPosition;
                hallPoint = hall != null ? CentralPoint(M2, hall) : null;

                Physics.queriesHitBackfaces = true;
                Run("1", "The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery.", Row1_Armory);
                Run("1b", "Act one: the armory's guards come into view only at its doorway.", Row1b_Choke);
                Run("2", "A way leads from the entry hall to the dining hall without passing through the armory.", Row2_WayRound);
                Run("3", "A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail.", Row3_Gallery);
                Run("4", "The throne stands on a stage with steps.", Row4_Stage);
                Run("5", "The vault has one way in, and the player may have to search for it.", Row5_Vault);
                MeasureCeilings();
                Run("6", "The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room.", Row6_Heights);
                Run("7", "Everything is indoors, and every room has a ceiling.", Row7_Ceilings);
                Run("8", "Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through.", Row8_Doors);
                Run("9", "The level has tall and low occluders the player can hide behind, standing or crouching.", Row9_Occluders);
                RunWalks();
                Run("10", "WALK: a CharacterController walks five ways through the level (not a brief line).", Row10_Walks);

                RunInfo("I1", "Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m).", Info1_GallerySight);
                RunInfo("I2", "Information: the map's honesty against the scene.", Info2_Honesty);
                RunInfo("I3", "Information: defenders and pickups against the brief's paragraph.", Info3_Defenders);
            }
            finally
            {
                Physics.queriesHitBackfaces = oldBack;
                foreach (var c in opened) if (c != null) c.enabled = true;
                guard.Restore();
                NavBake.Clear();
            }
            return BuildReport(guard);
        }

        void Run(string id, string line, Func<CheckRow, bool?> check)
        {
            var row = new CheckRow { Id = id, MustBeTrue = line };
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

        void RunInfo(string id, string line, Action<CheckRow> info)
        {
            var row = new CheckRow { Id = id, MustBeTrue = line, Status = "info" };
            try { info(row); }
            catch (Exception e)
            {
                row.Reason = "error while measuring: " + e.GetType().Name + ": " + e.Message;
                row.Short = "error";
                Debug.LogException(e);
            }
            Info.Add(row);
        }

        JObj BuildReport(SceneGuard guard)
        {
            int pass = rows.Count(r => r.Status == "pass"), fail = rows.Count(r => r.Status == "fail"), manual = rows.Count(r => r.Status == "manual");
            var bakes = new List<object>();
            foreach (var m in new[] { M0, M1, M2 })
                if (m != null)
                    bakes.Add(new JObj().Add("label", m.Label).Add("sources", m.SourceCount).Add("modifier_boxes", m.ModifierCount)
                        .Add("triangles", m.T).Add("components", m.CompCount).Add("level_components", m.LevelComp.Count(b => b))
                        .Add("gaps", m.Gaps.Count).Add("seconds", m.BakeSeconds));
            return new JObj()
                .Add("tool", "IronCitadel.ConformanceV2 1.1")
                .Add("scene", scene.path)
                .Add("map", Map.Path)
                .Add("run_utc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))
                .Add("unity", Application.unityVersion)
                .Add("summary", new JObj().Add("pass", pass).Add("fail", fail).Add("manual", manual).Add("automated_all_pass", fail == 0))
                .Add("agent", new JObj().Add("radius", Tune.AgentRadius).Add("height", Tune.AgentHeight).Add("step", Tune.AgentClimb)
                    .Add("slope", Tune.AgentSlope).Add("walk_skin", Tune.WalkSkin))
                .Add("bakes", bakes)
                .Add("armory_marked_boxes", armoryBoxes).Add("hall_floor_marked_boxes", hallFloorBoxes)
                .Add("other_interact_receivers_opened_for_the_last_bake", otherDoors.Cast<object>().ToList())
                .Add("scene_navmesh_links", guard.Links.Count)
                .Add("enemy_bodies_switched_off", guard.MarkerBodies.Count)
                .Add("navmesh_obstacles_switched_off", guard.ObstaclesOff)
                .Add("start", new JObj().Add("source", startSource).Add("position", StartPosition)
                    .Add("room", M2 != null ? M2.NodeName(M2.NodeOf(StartPosition)) : null)
                    .Add("scene_start", sceneStart.HasValue ? (object)sceneStart.Value : null).Add("scene_start_source", sceneStartSource))
                .Add("checks", rows.Select(r => (object)r.ToJson()).ToList())
                .Add("info", Info.Select(r => (object)r.ToJson()).ToList())
                .Add("room_graph", M2 != null ? GraphJson(M2) : new List<object>())
                .Add("walks", Walks.Select(w => (object)w.ToJson()).ToList())
                .Add("map_rooms_folded_into_gallery", Map.FoldedIntoGallery.Cast<object>().ToList())
                .Add("map_problems", Map.Problems.Cast<object>().ToList())
                .Add("notes", Notes.Cast<object>().ToList());
        }

        // ---------------------------------------------------------------------------------------------
        // helpers

        readonly Dictionary<(NavModel, int), float> floorCache = new Dictionary<(NavModel, int), float>();

        /// <summary>(v1) A room's floor height: the commonest SamplePosition height (0.1 m bins) in its floor band. The
        /// model's NavMesh must be the one loaded.</summary>
        public float FloorOf(NavModel m, MapRoom r)
        {
            if (floorCache.TryGetValue((m, r.Index), out var cached)) return cached;
            var ys = new List<float>();
            var stageYs = new List<float>();
            var onStage = Map.HasStage && r.Kind == "throne room" ? Map.Stage : null;
            float step = Tune.FloorProbeStep;
            for (float x = r.XMin + step * 0.5f; x < r.XMax; x += step)
            for (float z = r.ZMin + step * 0.5f; z < r.ZMax; z += step)
            {
                if (!r.ContainsXZ(x, z)) continue;
                var q = new Vector3(x, r.FloorY, z);
                if (!NavMesh.SamplePosition(q, out var hit, Tune.FloorBandHigh + 0.05f, m.Filter)) continue;
                var p = hit.position;
                float dy = p.y - r.FloorY;
                if (HDist(p, q) > step * 0.5f || dy < Tune.FloorBandLow || dy > Tune.FloorBandHigh || m.NodeOf(p) != r.Index) continue;
                int t = m.FindTri(p, 0.6f);
                if (t < 0 || !m.IsLevel(t)) continue;
                ys.Add(p.y);
                if (onStage != null && onStage.Contains(p.x, p.z)) stageYs.Add(p.y);
            }
            // 1.1: the throne room's floor is measured off the stage, which can be wider than the floor below it
            if (onStage != null && ys.Count > stageYs.Count) foreach (var y in stageYs) ys.Remove(y);
            float floor = r.FloorY;
            if (ys.Count > 0)
            {
                var mode = ys.GroupBy(y => Mathf.RoundToInt(y / 0.1f)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
                floor = mode.Average();
            }
            floorCache[(m, r.Index)] = floor;
            return floor;
        }

        static Vector3? Snap(NavModel m, Vector3 p, float r, NavMeshQueryFilter f)
        {
            return NavMesh.SamplePosition(p, out var hit, r, f) ? hit.position : (Vector3?)null;
        }

        /// <summary>(v1) A floor-level point of the room's level NavMesh, the closest found to <paramref name="near"/>.</summary>
        Vector3? FloorPointNear(NavModel m, MapRoom r, Vector3 near, float maxR, Func<Vector3, bool> extra = null, bool levelOnly = true)
        {
            float floor = FloorOf(m, r);
            float step = maxR <= 6f ? 0.5f : 1.5f;
            for (float rad = 0; rad <= maxR; rad += step)
            {
                int steps = rad == 0 ? 1 : Mathf.Max(8, (int)(rad * 4));
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2 / steps;
                    var p = new Vector3(near.x + Mathf.Cos(a) * rad, floor + 0.5f, near.z + Mathf.Sin(a) * rad);
                    if (!NavMesh.SamplePosition(p, out var hit, 1.0f, m.Filter)) continue;
                    if (m.NodeOf(hit.position) != r.Index || Mathf.Abs(hit.position.y - floor) > 0.6f) continue;
                    int t = m.FindTri(hit.position, 0.6f, levelOnly);
                    if (t < 0 || (levelOnly && !m.IsLevel(t))) continue;
                    if (extra != null && !extra(hit.position)) continue;
                    return hit.position;
                }
            }
            return null;
        }

        /// <summary>A floor point near the room's middle (the outline's centroid), preferring the hall floor's area.</summary>
        Vector3? CentralPoint(NavModel m, MapRoom r)
        {
            var c = r.Centroid;
            var near = new Vector3(c.x, r.FloorY, c.y);
            float span = Mathf.Max(r.XMax - r.XMin, r.ZMax - r.ZMin);
            return FloorPointNear(m, r, near, span, p => r != hall || IsHallFloorXZ(p.x, p.z)) ?? FloorPointNear(m, r, near, span);
        }

        /// <summary>Floor-level samples of a room on a grid: NavMesh points in its outline and floor band.</summary>
        List<Vector3> FloorSamples(NavModel m, MapRoom r, float step)
        {
            var l = new List<Vector3>();
            float floor = FloorOf(m, r);
            for (float x = r.XMin + step * 0.5f; x < r.XMax; x += step)
            for (float z = r.ZMin + step * 0.5f; z < r.ZMax; z += step)
            {
                if (!r.ContainsXZ(x, z)) continue;
                var probe = new Vector3(x, floor + 0.5f, z);
                if (!NavMesh.SamplePosition(probe, out var hit, 1f, m.Filter)) continue;
                var p = hit.position;
                if (HDist(p, probe) > step * 0.5f || Mathf.Abs(p.y - floor) > 0.6f || m.NodeOf(p) != r.Index) continue;
                int t = m.FindTri(p, 0.6f);
                if (t < 0 || !m.IsLevel(t)) continue;
                l.Add(p);
            }
            return l;
        }

        static List<Vector3> PathCorners(Vector3 a, Vector3 b, NavMeshQueryFilter f, out NavMeshPathStatus status)
        {
            var path = new NavMeshPath();
            var sa = NavMesh.SamplePosition(a, out var ha, 2f, f) ? ha.position : a;
            var sb = NavMesh.SamplePosition(b, out var hb, 2f, f) ? hb.position : b;
            if (!NavMesh.CalculatePath(sa, sb, f, path)) { status = NavMeshPathStatus.PathInvalid; return new List<Vector3>(); }
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

        List<object> RoomsVisited(NavModel m, List<Vector3> dense)
        {
            var l = new List<object>();
            string last = null;
            foreach (var p in dense)
            {
                var n = m.NodeName(m.NodeOf(p));
                if (n == "no room" || n == last) continue;
                l.Add(n); last = n;
            }
            return l;
        }

        static bool Clear(Vector3 eye, Vector3 target) => !Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore);

        bool IsHallFloorXZ(float x, float z)
        {
            if (hall == null || !hall.ContainsXZ(x, z)) return false;
            if (Map.HasGallery && Map.DistToDeck(x, z) <= Tune.HallFloorDeckClear) return false;
            return hall.InsetInside(x, z, Tune.HallFloorInset);
        }

        MapRoom RoomNamed(string name) => Map.Room(name);

        string DoorText(MapDoor d) => d == null ? "(none)" : d.Label + " at " + Fmt(d.Position);

        /// <summary>The listed door a crossing matches: same two rooms (the gallery counts as the dining hall), nearest
        /// position within width / 2 + DoorMatchSlack, at the door's own height (DoorMatchYBand).</summary>
        MapDoor MatchDoor(NavModel m, Gap g)
        {
            var a = RoomOfNode(g.A); var b = RoomOfNode(g.B);
            if (a == null || b == null || a == b) return null;
            MapDoor best = null; float bd = float.MaxValue;
            foreach (var d in Map.Doors)
            {
                if (!((d.RA == a && d.RB == b) || (d.RA == b && d.RB == a))) continue;
                float dist = g.DistNear(d.Position, Tune.DoorMatchYBand);
                if (dist > d.Width * 0.5f + Tune.DoorMatchSlack || dist >= bd) continue;
                bd = dist; best = d;
            }
            return best;
        }

        /// <summary>The map room a node stands for: rooms are themselves, the gallery is the dining hall's, passages none.</summary>
        MapRoom RoomOfNode(int n)
        {
            if (n >= 0 && n < Map.Rooms.Count) return Map.Rooms[n];
            if (n == Map.GalleryNode) return hall;
            return null;
        }

        List<object> GraphJson(NavModel m)
        {
            var l = new List<object>();
            foreach (var g in m.Gaps.OrderBy(g => m.NodeName(g.A)).ThenBy(g => m.NodeName(g.B)))
            {
                var d = MatchDoor(m, g);
                var ra = RoomOfNode(g.A); var rb = RoomOfNode(g.B);
                string role = ra != null && ra == rb ? "inside " + ra.Name : d != null ? "listed door" : "NOT LISTED";
                l.Add(new JObj().Add("between", new List<object> { m.NodeName(g.A), m.NodeName(g.B) }).Add("via", g.Via)
                    .Add("centre", g.Centre).Add("navmesh_width_m", g.Width).Add("area_m2", g.Area)
                    .Add("door", d != null ? (object)d.Index : null).Add("door_state", d?.State).Add("role", role));
            }
            return l;
        }

        // ---------------------------------------------------------------------------------------------
        // modifiers: the armory (row 2's way round) and the hall floor (row 3's climb) as their own NavMesh areas

        void BuildModifiers()
        {
            if (armory != null)
                armoryBoxes = NavBake.AddStrips(modifiers, armory.ContainsXZ, armory.XMin, armory.XMax, armory.ZMin, armory.ZMax,
                    armory.FloorY + Tune.RoomBandLow, armory.FloorY + Tune.RoomBandHigh, Tune.ArmoryArea);
            if (hall != null)
                hallFloorBoxes = NavBake.AddStrips(modifiers, IsHallFloorXZ, hall.XMin, hall.XMax, hall.ZMin, hall.ZMax,
                    hall.FloorY + Tune.FloorBandLow, hall.FloorY + Tune.HallFloorBandHigh, Tune.HallFloorArea);
        }

        // ---------------------------------------------------------------------------------------------
        // start (v1's search, kept as information beside the map's start)

        void FindStart(SceneGuard guard)
        {
            Transform found = null;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
            {
                var n = t.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (n.Contains("playerstart") || n.Contains("spawnpoint") || n == "start")
                { found = t; sceneStartSource = "object named " + t.name; break; }
            }
            if (found == null)
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(false))
                {
                    bool tagged = false;
                    try { tagged = t.CompareTag("Player"); } catch { }
                    if (tagged) { found = t; sceneStartSource = "object tagged Player: " + t.name; break; }
                }
            if (found == null && guard.Controllers.Count > 0)
            {
                found = guard.Controllers[0].transform;
                sceneStartSource = "CharacterController on " + found.name;
            }
            if (found != null) sceneStart = found.position;

            Vector3 p;
            if (Map.StartPos != null) { p = Map.StartPos.Value; startSource = "map.json start"; }
            else if (sceneStart != null) { p = sceneStart.Value; startSource = "scene: " + sceneStartSource + " (the map has no start)"; }
            else
            {
                var c = entry != null ? entry.Centroid : Vector2.zero;
                p = new Vector3(c.x, entry?.FloorY ?? 0f, c.y);
                startSource = "fallback: the entry hall's centroid";
            }
            var snapped = Snap(M0, p + Vector3.up * 0.5f, Tune.StartSnapRadius, M0.Filter);
            if (snapped == null) Notes.Add("the start (" + startSource + ") is not within " + Tune.StartSnapRadius + " m of the NavMesh");
            StartPosition = snapped ?? p;
            if (sceneStart != null && Map.StartPos != null && HDist(sceneStart.Value, Map.StartPos.Value) > 2f)
                Notes.Add("the map's start is " + N1(HDist(sceneStart.Value, Map.StartPos.Value)) + " m from the scene's (" + sceneStartSource + ")");
        }

        // ---------------------------------------------------------------------------------------------
        // doors: shut doors through the E contract, the hidden door as v1 check 5

        void OpenShutDoors(List<Collider> opened)
        {
            foreach (var d in Map.Doors.Where(x => x.State == "shut"))
            {
                var o = new DoorOpen { Door = d };
                shutOpen[d] = o;
                ESides(M0, d, o, d.RA, d.RB);
                foreach (var c in Physics.OverlapSphere(d.Position + Vector3.up * Tune.DoorTargetLift,
                             Mathf.Max(Tune.ShutDoorInteractRadius, d.Width * 0.5f + 0.5f), ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!Doors.Solid(c) || o.Colliders.Contains(c)) continue;
                    var rec = Doors.Receiver(c);
                    if (rec == null) continue;
                    var cols = Doors.CollidersOf(rec, c).Where(x => !o.Colliders.Contains(x)).ToList();
                    if (cols.Count == 0) continue;
                    o.Colliders.AddRange(cols);
                    o.How.Add("Interact() within " + Tune.ShutDoorInteractRadius + " m: " + Doors.PathOf(rec.transform));
                }
                foreach (var c in o.Colliders) if (c.enabled) { c.enabled = false; opened.Add(c); }
            }
            Physics.SyncTransforms();
        }

        /// <summary>Casts the E ray from a floor point on each side of the door, toward it, then aimed EAimSpread either
        /// side along it (a ray at a pair's centre can thread the seam between the leaves). Records the receivers.</summary>
        void ESides(NavModel m, MapDoor d, DoorOpen o, params MapRoom[] sides)
        {
            foreach (var r in sides)
            {
                if (r == null) continue;
                var p = DoorSidePoint(m, d, r);
                if (p == null) { o.Sides.Add(r.Name, "no floor of " + r.Name + " within " + Tune.DoorSideSearch + " m of the door"); continue; }
                o.SidesFound++;
                var eye = p.Value + Vector3.up * Tune.EyeHeight;
                var dir = d.Position - p.Value;
                if (new Vector2(dir.x, dir.z).sqrMagnitude < 0.01f)
                {
                    var oc = d.Other(r)?.Centroid ?? new Vector2(d.Position.x, d.Position.z);
                    dir = new Vector3(oc.x - p.Value.x, 0, oc.y - p.Value.z);
                }
                var across = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0f, dir.z)).normalized;
                MonoBehaviour rec = null; RaycastHit hit = default; string what = null;
                foreach (float off in new[] { 0f, Tune.EAimSpread, -Tune.EAimSpread })
                {
                    rec = Doors.Cast(eye, dir + across * off, out var h, out var w);
                    if (off == 0f || rec != null) { hit = h; what = w + (off != 0f ? " (aimed " + off.ToString("0.00", CultureInfo.InvariantCulture) + " m along the door)" : ""); }
                    if (rec != null) break;
                }
                o.Sides.Add(r.Name, new JObj().Add("from", p.Value).Add("result", what).Add("ok", rec != null));
                if (rec == null) continue;
                o.SidesOk++;
                foreach (var c in Doors.CollidersOf(rec, hit.collider)) if (!o.Colliders.Contains(c)) o.Colliders.Add(c);
                o.How.Add("E from " + r.Name + ": " + Doors.PathOf(rec.transform));
            }
        }

        /// <summary>Where E is cast from on room r's side of door d: r's floor near the door or, for a door well above
        /// that floor (onto a gallery), NavMesh at the door's own height on r's side (the deck counts as the hall's).</summary>
        Vector3? DoorSidePoint(NavModel m, MapDoor d, MapRoom r)
        {
            if (Mathf.Abs(d.Position.y - FloorOf(m, r)) <= Tune.DoorMatchYBand) return FloorPointNear(m, r, d.Position, Tune.DoorSideSearch);
            for (float rad = 0; rad <= Tune.DoorSideSearch; rad += 0.5f)
            {
                int steps = rad == 0 ? 1 : Mathf.Max(8, (int)(rad * 4));
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2 / steps;
                    var q = new Vector3(d.Position.x + Mathf.Cos(a) * rad, d.Position.y + 0.5f, d.Position.z + Mathf.Sin(a) * rad);
                    if (!NavMesh.SamplePosition(q, out var hit, 1.0f, m.Filter) || Mathf.Abs(hit.position.y - d.Position.y) > 0.6f) continue;
                    int n = m.NodeOf(hit.position);
                    if (n != r.Index && !(r == hall && n == Map.GalleryNode)) continue;
                    if (m.FindTri(hit.position, 0.6f, false) < 0) continue;
                    return hit.position;
                }
            }
            return null;
        }

        void VaultClosed()
        {
            if (vault == null || vaultNb == null) { vaultClosedStatus = "no vault or no hidden door to it"; return; }
            vaultClosedGaps = M1.GapsOf(vault.Index).Select(g => (object)M1.NodeName(g.Other(vault.Index))).ToList();
            var np = FloorPointNear(M1, vaultNb, hidden.Position, 8f);
            // sealed, the vault is an island, often smaller than the level-space rule: then any floor NavMesh in it will do
            var vc = new Vector3(vault.Centroid.x, vault.FloorY, vault.Centroid.y);
            var vp = FloorPointNear(M1, vault, vc, 30f) ?? FloorPointNear(M1, vault, vc, 30f, null, false);
            if (vp == null) { vaultClosedStatus = "no vault floor on the NavMesh"; return; }
            if (np == null) { vaultClosedStatus = "no floor of " + vaultNb.Name + " near the hidden door"; return; }
            PathCorners(np.Value, vp.Value, M1.Filter, out var st);
            vaultClosedStatus = st.ToString();
        }

        readonly List<string> otherDoors = new List<string>();
        public List<string> OtherDoors => otherDoors;

        /// <summary>"Every door opened": after the listed shut doors and the hidden door, every other Interact()
        /// receiver small enough to be a door (Doors.CollidersOf) is opened too, so a door the map does not list
        /// (inside a room, or an unlisted way between rooms) neither blocks the paths nor hides an opening from row 8.</summary>
        void OpenOtherDoors(List<Collider> opened)
        {
            var done = new HashSet<MonoBehaviour>();
            foreach (var c in allColliders)
            {
                if (!Doors.Solid(c)) continue;
                var rec = Doors.Receiver(c);
                if (rec == null || !done.Add(rec)) continue;
                var cols = Doors.CollidersOf(rec, null).Where(x => x.enabled).ToList();
                if (cols.Count == 0) continue;
                foreach (var x in cols) { x.enabled = false; opened.Add(x); }
                otherDoors.Add(Doors.PathOf(rec.transform) + " at " + Fmt(rec.transform.position));
            }
            Physics.SyncTransforms();
        }

        void OpenHiddenDoor(List<Collider> opened)
        {
            if (hidden == null) return;
            var o = hiddenOpen = new DoorOpen { Door = hidden };
            foreach (var c in allColliders)
            {
                if (!Doors.Solid(c) || o.Colliders.Contains(c)) continue;
                var cc = c.bounds.center;
                if (HDist(cc, hidden.Position) > Tune.VaultInteractReach) continue;
                var rec = Doors.Receiver(c);
                if (rec == null) continue;
                o.Colliders.Add(c);
                var path = Doors.PathOf(rec.transform);
                if (!o.How.Contains(path)) o.How.Add(path);
            }
            if (vaultNb != null) ESides(M1, hidden, o, vaultNb);
            if (o.Colliders.Count == 0)
            {
                // a way behind obstacles: v1's span, every collider this close to the door above its floor
                float floor = hidden.Position.y, R = Tune.VaultDisableRadius;
                float yLo = floor + Tune.VaultFloorClearance, yHi = floor + Tune.VaultDoorHeight;
                var centre = new Vector3(hidden.Position.x, (yLo + yHi) * 0.5f, hidden.Position.z);
                var half = new Vector3(hidden.Width * 0.5f + R, (yHi - yLo) * 0.5f, hidden.Width * 0.5f + R);
                foreach (var c in Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!Doors.Solid(c)) continue;
                    var b = c.bounds;
                    if (b.max.y <= yLo || b.size.x > Tune.VaultSkipLargerThan || b.size.z > Tune.VaultSkipLargerThan) continue;
                    o.Colliders.Add(c);
                    o.How.Add("span: " + Doors.PathOf(c.transform));
                }
            }
            foreach (var c in o.Colliders) if (c.enabled) { c.enabled = false; opened.Add(c); }
            Physics.SyncTransforms();
        }
    }

    /// <summary>(v1) Switches off the scene's own NavMesh components, player controllers and the colliders of the player
    /// rig under each controller; v2 also switches off the enemies' bodies (markers, not level geometry) and every
    /// NavMeshObstacle, whose carving would cut the kit's own bake where a door leaf has been switched off. All are put
    /// back afterwards. The scene is never saved.</summary>
    public sealed class SceneGuard
    {
        readonly List<Behaviour> off = new List<Behaviour>();
        readonly List<Collider> offColliders = new List<Collider>();
        public readonly List<CharacterController> Controllers = new List<CharacterController>();
        public readonly List<SceneLink> Links;
        public readonly List<string> MarkerBodies = new List<string>();
        public int ObstaclesOff;

        public SceneGuard(Scene scene, MapSpec map)
        {
            Links = NavBake.ReadLinks(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var s in root.GetComponentsInChildren<NavMeshSurface>(false)) if (s.enabled) { s.enabled = false; off.Add(s); }
                foreach (var l in root.GetComponentsInChildren<NavMeshLink>(false)) if (l.enabled) { l.enabled = false; off.Add(l); }
                foreach (var o in root.GetComponentsInChildren<NavMeshObstacle>(false)) if (o.enabled) { o.enabled = false; off.Add(o); ObstaclesOff++; }
                foreach (var c in root.GetComponentsInChildren<CharacterController>(false))
                {
                    if (c.enabled) { c.enabled = false; Controllers.Add(c); }
                    foreach (var col in c.GetComponentsInChildren<Collider>(false))
                        if (!(col is CharacterController) && col.enabled) { col.enabled = false; offColliders.Add(col); }
                }
            }
            Physics.SyncTransforms();
            foreach (var e in map.Enemies)
            {
                var p = e.Position;
                foreach (var c in Physics.OverlapCapsule(p + Vector3.up * 0.3f, p + Vector3.up * 1.7f, 0.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!Doors.Solid(c) || offColliders.Contains(c)) continue;
                    var b = c.bounds;
                    if (new Vector2(b.center.x - p.x, b.center.z - p.z).magnitude > 0.45f) continue;
                    if (Mathf.Max(b.size.x, b.size.z) > 2f || b.size.y > 4.5f) continue;
                    c.enabled = false; offColliders.Add(c); MarkerBodies.Add(Doors.PathOf(c.transform));
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
