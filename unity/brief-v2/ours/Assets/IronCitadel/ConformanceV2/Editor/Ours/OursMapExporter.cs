// IronCitadel conformance kit v2: writes THE MAP (map.json) for our own v2 build from its design files and the built
// scene. Only our build needs this: a cold run writes its own map.
//
//   unity run <project> --timeout 900 --no-tail -- -executeMethod IronCitadel.ConformanceV2.Ours.ExportCli.Run
//       -run C:/Repos/IronCitadel/v2 -scene Assets/.../IronCitadelV2.unity -out C:/Repos/IronCitadel/v2/map.json
//       [-score C:/path/scores/x [-noWalk]] -logFile <log>
//
// From the run folder: level.json (rooms, names, doors and their kinds), picks.json (each room's state),
// floor.json (the 1 m floor samples the outlines, floor heights, gallery deck and stage are traced from; the door
// crossings) and built.json (each door's NavMesh opening). From the scene: the IronCitadelMarker components (start,
// defenders, pickups, goal), read by reflection so the kit compiles without them, and the throne prop.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronCitadel.ConformanceV2.Ours
{
    public static class ExportCli
    {
        public static void Run()
        {
            string runDir = ConformanceCli.Arg("-run"), scenePath = ConformanceCli.Arg("-scene"), outPath = ConformanceCli.Arg("-out");
            string scoreDir = ConformanceCli.Arg("-score");
            try
            {
                if (runDir == null || scenePath == null || outPath == null)
                    throw new ArgumentException("usage: -executeMethod IronCitadel.ConformanceV2.Ours.ExportCli.Run -run <v2 run dir> -scene <Assets/...unity> -out <map.json> [-score <dir> [-noWalk]]");
                var scene = EditorSceneManager.OpenScene(ConformanceCli.ToProjectPath(scenePath), OpenSceneMode.Single);
                Export(runDir, scene, outPath);
                if (scoreDir != null) ConformanceCli.ScoreScene(scene, outPath, scoreDir, !ConformanceCli.HasArg("-noWalk"));
            }
            catch (Exception e)
            {
                ConformanceCli.Fail(scoreDir ?? Path.GetDirectoryName(Path.GetFullPath(outPath ?? ".")), e);
            }
        }

        // ---------------------------------------------------------------------------------------------

        sealed class Room
        {
            public string Id, Name, Letter, State;
            public float FloorY;
            public readonly List<Sample> Samples = new List<Sample>();
            public float XMin, XMax, ZMin, ZMax;
        }

        sealed class Sample
        {
            public int Id, I, J;
            public float Y;
            public string Room;
            public bool Reach;
        }

        static Dictionary<string, object> Obj(object o) => o as Dictionary<string, object>;
        static List<object> Arr(object o) => o as List<object> ?? new List<object>();
        static string S(object o) => o == null ? null : Convert.ToString(o, CultureInfo.InvariantCulture);
        static float Fl(object o) => o == null ? 0f : (float)Convert.ToDouble(o, CultureInfo.InvariantCulture);
        static object Get(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;

        static Dictionary<string, object> Load(string dir, string file)
        {
            var p = Path.Combine(dir, file);
            if (!File.Exists(p)) throw new FileNotFoundException("the run has no " + file, p);
            return Obj(MiniJson.Parse(File.ReadAllText(p))) ?? throw new FormatException(file + ": the top level is not an object");
        }

        /// <summary>Writes map.json for our v2 build and returns it.</summary>
        public static JObj Export(string runDir, Scene scene, string outPath)
        {
            var level = Load(runDir, "level.json");
            var picks = Load(runDir, "picks.json");
            var floor = Load(runDir, "floor.json");
            var built = Load(runDir, "built.json");
            var notes = new List<object>();

            // rooms, names and states
            var rooms = new List<Room>();
            var byId = new Dictionary<string, Room>();
            foreach (var o in Arr(Get(level, "rooms")))
            {
                var d = Obj(o);
                var r = new Room { Id = S(Get(d, "id")), Name = S(Get(d, "name")), Letter = S(Get(d, "kind")) };
                rooms.Add(r); byId[r.Id] = r;
            }
            foreach (var o in Arr(Get(picks, "picks")))
            {
                var d = Obj(o);
                if (!byId.TryGetValue(S(Get(d, "room")) ?? "", out var r)) continue;
                var sk = (S(Get(d, "state_kind")) ?? "").ToLowerInvariant();
                r.State = sk == "looted" ? "looted" : sk == "abandoned" ? "decayed" : sk == "use" ? (r.Letter == "V" ? "untouched" : "stocked")
                        : MapSpec.States.Contains(sk) ? sk : "decayed";
            }

            // the floor samples
            float cell = Fl(Get(floor, "cell_m"));
            var origin = Arr(Get(floor, "origin"));
            float ox = Fl(origin[0]), oz = Fl(origin[1]);
            var samples = new Dictionary<int, Sample>();
            foreach (var o in Arr(Get(floor, "samples")))
            {
                var d = Obj(o);
                var s = new Sample
                {
                    Id = (int)Fl(Get(d, "id")), I = (int)Fl(Get(d, "i")), J = (int)Fl(Get(d, "j")), Y = Fl(Get(d, "y")),
                    Room = S(Get(d, "room")), Reach = Get(d, "reach") is bool b && b
                };
                samples[s.Id] = s;
                if (s.Room != null && byId.TryGetValue(s.Room, out var r)) r.Samples.Add(s);
            }
            float X(float i) => ox + i * cell;
            float Z(float j) => oz + j * cell;

            var sampled = new HashSet<(int, int)>(samples.Values.Select(s => (s.I, s.J)));
            var byDoor = new HashSet<(int, int)>();   // within 2 cells of a door's crossing samples
            foreach (var fd in Arr(Get(floor, "doors")))
                foreach (var sid in Arr(Get(Obj(fd), "samples")))
                    if (samples.TryGetValue((int)Fl(sid), out var cs))
                        for (int di = -2; di <= 2; di++) for (int dj = -2; dj <= 2; dj++) byDoor.Add((cs.I + di, cs.J + dj));
            var roomsJ = new List<object>();
            foreach (var r in rooms)
            {
                var use = r.Samples.Where(s => s.Reach).ToList();
                if (use.Count == 0) { use = r.Samples; notes.Add(r.Name + ": no reachable floor samples; the outline uses all " + use.Count); }
                r.FloorY = Mode(use.Select(s => s.Y));
                var cells = new HashSet<(int, int)>(use.Select(s => (s.I, s.J)));
                FillBays(cells, sampled, byDoor);
                var polys = Outlines(cells, 4, X, Z);
                if (polys.Count == 0) { notes.Add(r.Name + ": no floor samples, no outline"); continue; }
                r.XMin = polys.SelectMany(p => p).Min(v => v.x); r.XMax = polys.SelectMany(p => p).Max(v => v.x);
                r.ZMin = polys.SelectMany(p => p).Min(v => v.y); r.ZMax = polys.SelectMany(p => p).Max(v => v.y);
                roomsJ.Add(new JObj().Add("name", r.Name).Add("kind", MapSpec.NormKind(r.Name)).Add("state", r.State ?? "decayed")
                    .Add("floor_y", r.FloorY).Add("floor", polys.Select(p => (object)PolyJ(p)).ToList()));
            }

            // doors: the mean of each door's crossing samples; the clear width is the NavMesh opening plus two agent radii
            var opening = new Dictionary<string, float>();
            foreach (var o in Arr(Get(built, "doors")))
            {
                var d = Obj(o);
                var id = S(Get(d, "design_door"));
                if (id != null && Get(d, "opening_m") != null) opening[id] = Fl(Get(d, "opening_m"));
            }
            var crossing = new Dictionary<string, Vector3>();
            foreach (var o in Arr(Get(floor, "doors")))
            {
                var d = Obj(o);
                var pts = Arr(Get(d, "samples")).Select(x => samples.TryGetValue((int)Fl(x), out var s) ? s : null).Where(s => s != null).ToList();
                if (pts.Count == 0) continue;
                crossing[S(Get(d, "door"))] = new Vector3(pts.Average(s => X(s.I + 0.5f)), pts.Average(s => s.Y), pts.Average(s => Z(s.J + 0.5f)));
            }
            var doorsJ = new List<object>();
            foreach (var o in Arr(Get(level, "doors")))
            {
                var d = Obj(o);
                var id = S(Get(d, "id"));
                var a = byId.TryGetValue(S(Get(d, "a")) ?? "", out var ra) ? ra.Name : S(Get(d, "a"));
                var b = byId.TryGetValue(S(Get(d, "b")) ?? "", out var rb) ? rb.Name : S(Get(d, "b"));
                if (!crossing.TryGetValue(id, out var pos)) { notes.Add("door " + id + ": no crossing samples in floor.json; placed between the rooms' centres"); pos = Mid(ra, rb); }
                float w = opening.TryGetValue(id, out var ow) ? ow + 2 * Tune.AgentRadius : 1.5f;
                doorsJ.Add(new JObj().Add("rooms", new List<object> { a, b }).Add("state", S(Get(d, "kind"))).Add("position", pos).Add("width", w).Add("id", id));
            }

            // the gallery: the dining hall's samples 3 m and more above its floor, at the commonest such height
            var hall = rooms.FirstOrDefault(r => r.Letter == "D");
            JObj galleryJ = null;
            if (hall != null)
            {
                var high = hall.Samples.Where(s => s.Reach && s.Y >= hall.FloorY + Tune.GalleryMinRise - Tune.GalleryRiseSlack).ToList();
                if (high.Count > 0 && hall.Samples.Any(s => s.Reach && Mathf.Abs(s.Y - hall.FloorY) <= 0.5f))
                {
                    float deckY = Mode(high.Select(s => s.Y));
                    var deck = high.Where(s => Mathf.Abs(s.Y - deckY) <= 0.5f).ToList();
                    var deckCells = new HashSet<(int, int)>(deck.Select(s => (s.I, s.J)));
                    var polys = Outlines(deckCells, 4, X, Z);
                    var all = polys.SelectMany(p => p).ToList();
                    float dx0 = all.Min(v => v.x), dx1 = all.Max(v => v.x), dz0 = all.Min(v => v.y), dz1 = all.Max(v => v.y);
                    var floorPts = hall.Samples.Where(s => s.Reach && Mathf.Abs(s.Y - hall.FloorY) <= 0.5f && !deckCells.Contains((s.I, s.J))).ToList();
                    var fc = new Vector2(floorPts.Average(s => X(s.I + 0.5f)), floorPts.Average(s => Z(s.J + 0.5f)));
                    var dc = new Vector2((dx0 + dx1) * 0.5f, (dz0 + dz1) * 0.5f);
                    var to = fc - dc;
                    List<object> rail;
                    if (Mathf.Abs(to.x) >= Mathf.Abs(to.y))
                    {
                        float x = to.x > 0 ? dx1 : dx0;
                        rail = new List<object> { V2(x, dz0), V2(x, dz1) };
                    }
                    else
                    {
                        float z = to.y > 0 ? dz1 : dz0;
                        rail = new List<object> { V2(dx0, z), V2(dx1, z) };
                    }
                    galleryJ = new JObj().Add("deck_y", deckY).Add("deck", polys.Select(p => (object)PolyJ(p)).ToList()).Add("rail", rail);
                    notes.Add("gallery: " + deck.Count + " deck samples at " + deckY.ToString("0.00", CultureInfo.InvariantCulture) + " m; the rail is the deck's edge facing the hall floor");
                }
                else notes.Add("gallery: no dining-hall samples 3 m above its floor");
            }

            // the throne: a prop named throne in the throne room, nearest the Goal marker
            var markers = Markers(scene);
            var throneRoom = rooms.FirstOrDefault(r => r.Letter == "T");
            var goal = markers.FirstOrDefault(m => m.Kind == "Goal");
            Vector3? thronePos = null;
            if (throneRoom != null)
            {
                var props = new List<(Transform t, Bounds b)>();
                foreach (var root in scene.GetRootGameObjects())
                foreach (var rd in root.GetComponentsInChildren<Renderer>(false))
                {
                    // the mansion's throne is the master's armchair (the builder's mansion profile)
                    if (rd.name.IndexOf("throne", StringComparison.OrdinalIgnoreCase) < 0 && rd.name.IndexOf("ArmChair_05", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var b = rd.bounds;
                    if (Mathf.Max(b.size.x, b.size.z) > 4f || b.size.y > 5f) continue;
                    if (b.center.x < throneRoom.XMin - 2 || b.center.x > throneRoom.XMax + 2 || b.center.z < throneRoom.ZMin - 2 || b.center.z > throneRoom.ZMax + 2) continue;
                    props.Add((rd.transform, b));
                }
                var near = goal != null ? goal.Position : new Vector3((throneRoom.XMin + throneRoom.XMax) * 0.5f, throneRoom.FloorY, (throneRoom.ZMin + throneRoom.ZMax) * 0.5f);
                if (props.Count > 0)
                {
                    var best = props.OrderBy(p => Vector3.Distance(p.b.center, near)).First();
                    thronePos = new Vector3(best.b.center.x, best.b.min.y, best.b.center.z);
                    notes.Add("throne: the prop " + Doors.PathOf(best.t) + " (" + props.Count + " renderers named throne in the room)");
                }
                else if (goal != null) { thronePos = goal.Position; notes.Add("throne: no prop named throne; the Goal marker is used"); }
            }

            // the stage: the throne room's reachable samples 1 to 2 m above its floor, the largest patch, as a rectangle
            JObj stageJ = null;
            if (throneRoom != null)
            {
                var band = throneRoom.Samples.Where(s => s.Reach && s.Y >= throneRoom.FloorY + 1f && s.Y <= throneRoom.FloorY + 2f).ToList();
                if (band.Count == 0) band = throneRoom.Samples.Where(s => s.Y >= throneRoom.FloorY + 1f && s.Y <= throneRoom.FloorY + 2f).ToList();
                if (band.Count > 0)
                {
                    var comp = Components(new HashSet<(int, int)>(band.Select(s => (s.I, s.J)))).OrderByDescending(c => c.Count).First();
                    var top = band.Where(s => comp.Contains((s.I, s.J))).ToList();
                    float topY = Mode(top.Select(s => s.Y));
                    float x0 = X(comp.Min(c => c.Item1)), x1 = X(comp.Max(c => c.Item1) + 1), z0 = Z(comp.Min(c => c.Item2)), z1 = Z(comp.Max(c => c.Item2) + 1);
                    if (thronePos != null)
                    {
                        var t = thronePos.Value;
                        bool inside = t.x >= x0 && t.x <= x1 && t.z >= z0 && t.z <= z1;
                        float outBy = Mathf.Max(Mathf.Max(x0 - t.x, t.x - x1), Mathf.Max(z0 - t.z, t.z - z1));
                        if (!inside && outBy <= 3f && Mathf.Abs(t.y - topY) <= 0.4f)
                        {
                            x0 = Mathf.Min(x0, t.x - 0.5f); x1 = Mathf.Max(x1, t.x + 0.5f); z0 = Mathf.Min(z0, t.z - 0.5f); z1 = Mathf.Max(z1, t.z + 0.5f);
                            notes.Add("stage: grown " + outBy.ToString("0.0", CultureInfo.InvariantCulture) + " m to take in the throne, which stands on it (its base at the stage top) where the throne itself keeps the NavMesh off");
                        }
                    }
                    stageJ = new JObj().Add("top_y", topY).Add("outline", new List<object> { V2(x0, z0), V2(x1, z0), V2(x1, z1), V2(x0, z1) });
                    notes.Add("stage: " + top.Count + " samples, top " + topY.ToString("0.00", CultureInfo.InvariantCulture) + " m");
                }
                else notes.Add("stage: no throne-room samples 1 to 2 m above its floor");
            }

            // start, enemies and pickups from the scene's markers (placements.json when the scene has none)
            var startM = markers.FirstOrDefault(m => m.Kind == "PlayerStart");
            JObj startJ = startM != null ? new JObj().Add("position", startM.Position).Add("facing", V2(startM.Forward.x, startM.Forward.z)) : null;
            var enemiesJ = new List<object>();
            foreach (var m in markers.Where(m => m.Kind == "Defender"))
            {
                var k = (m.Note ?? "").Split(',')[0].Trim().ToLowerInvariant();
                var kind = k == "boss" || k == "warlord" ? "warlord" : k == "ranged" ? "ranged" : "melee";
                enemiesJ.Add(new JObj().Add("kind", kind).Add("room", NameOf(byId, m.Room)).Add("position", m.Position)
                    .Add("facing", V2(m.Forward.x, m.Forward.z)).Add("source", m.Path + ": " + m.Note));
            }
            var pickupsJ = new List<object>();
            foreach (var m in markers.Where(m => m.Kind == "Pickup"))
            {
                var n = (m.Note ?? "").Trim().ToLowerInvariant();
                var kind = n.Contains("hoard") ? "hoard" : n.Contains("gear") ? "best gear" : n.Replace('_', ' ');
                pickupsJ.Add(new JObj().Add("kind", kind).Add("room", NameOf(byId, m.Room)).Add("position", m.Position).Add("source", m.Path));
            }
            if (startJ == null || enemiesJ.Count == 0 || pickupsJ.Count == 0) FromPlacements(runDir, byId, ref startJ, enemiesJ, pickupsJ, notes);

            var map = new JObj()
                .Add("level", S(Get(level, "name")))
                .Add("start", startJ)
                .Add("rooms", roomsJ)
                .Add("doors", doorsJ)
                .Add("gallery", galleryJ)
                .Add("stage", stageJ)
                .Add("throne", thronePos.HasValue ? (object)thronePos.Value : null)
                .Add("enemies", enemiesJ)
                .Add("pickups", pickupsJ)
                .Add("exporter", new JObj().Add("tool", "IronCitadel.ConformanceV2.Ours.ExportCli").Add("run", Path.GetFullPath(runDir).Replace('\\', '/'))
                    .Add("scene", scene.path).Add("written_utc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))
                    .Add("method", "outlines: each room's reachable 1 m floor.json cells, holes and 1-cell bays beside doors filled, traced; floor_y: the commonest sample height; doors: the mean of floor.json's crossing samples, width built.json opening_m + 2 x 0.35; deck: the hall's samples >= 2.95 m up; stage: the throne room's samples 1-2 m up; start, enemies, pickups: the scene's IronCitadelMarker components")
                    .Add("notes", notes));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            File.WriteAllText(outPath, MiniJson.Write(map));
            Debug.Log("[ConformanceV2] map written: " + Path.GetFullPath(outPath) + " (" + roomsJ.Count + " rooms, " + doorsJ.Count + " doors, " +
                      enemiesJ.Count + " enemies, " + pickupsJ.Count + " pickups)");
            return map;
        }

        static string NameOf(Dictionary<string, Room> byId, string id) => id != null && byId.TryGetValue(id, out var r) ? r.Name : id;

        static Vector3 Mid(Room a, Room b)
        {
            if (a == null || b == null) return Vector3.zero;
            return new Vector3((a.XMin + a.XMax + b.XMin + b.XMax) * 0.25f, (a.FloorY + b.FloorY) * 0.5f, (a.ZMin + a.ZMax + b.ZMin + b.ZMax) * 0.25f);
        }

        static List<object> V2(float x, float z) => new List<object> { (double)x, (double)z };

        static List<object> PolyJ(List<Vector2> p) => p.Select(v => (object)V2(v.x, v.y)).ToList();

        /// <summary>The commonest value in 0.1 bins, averaged within its bin.</summary>
        static float Mode(IEnumerable<float> ys)
        {
            var l = ys.ToList();
            if (l.Count == 0) return 0f;
            return l.GroupBy(y => Mathf.RoundToInt(y / 0.1f)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Average();
        }

        // ---------------------------------------------------------------------------------------------
        // outlines: cells -> holes filled -> 4-connected patches (small ones dropped) -> boundary loops

        static List<HashSet<(int, int)>> Components(HashSet<(int, int)> cells)
        {
            var res = new List<HashSet<(int, int)>>();
            var seen = new HashSet<(int, int)>();
            foreach (var c in cells)
            {
                if (seen.Contains(c)) continue;
                var comp = new HashSet<(int, int)>();
                var st = new Stack<(int, int)>();
                st.Push(c); seen.Add(c);
                while (st.Count > 0)
                {
                    var (i, j) = st.Pop();
                    comp.Add((i, j));
                    foreach (var n in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                        if (cells.Contains(n) && seen.Add(n)) st.Push(n);
                }
                res.Add(comp);
            }
            return res;
        }

        /// <summary>Unsampled cells (no floor.json sample of any room) beside a door's crossing with three or four of their
        /// four neighbours in the room, until none is left: an opened door leaf's footprint (the Hollowmere Manor armory's
        /// dining-hall door left a 3 x 1 m notch, and the kit counted its NavMesh as a second doorway). Only by a door: a
        /// 1-cell slit elsewhere is a wall inside the room.</summary>
        static void FillBays(HashSet<(int, int)> cells, HashSet<(int, int)> sampled, HashSet<(int, int)> byDoor)
        {
            for (bool grew = true; grew;)
            {
                var add = new List<(int, int)>();
                foreach (var (i, j) in cells)
                    foreach (var n in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                    {
                        if (cells.Contains(n) || sampled.Contains(n) || !byDoor.Contains(n)) continue;
                        var (a, b) = n;
                        int k = (cells.Contains((a + 1, b)) ? 1 : 0) + (cells.Contains((a - 1, b)) ? 1 : 0)
                              + (cells.Contains((a, b + 1)) ? 1 : 0) + (cells.Contains((a, b - 1)) ? 1 : 0);
                        if (k >= 3) add.Add(n);
                    }
                grew = false;
                foreach (var n in add) grew |= cells.Add(n);
            }
        }

        static void FillHoles(HashSet<(int, int)> cells)
        {
            if (cells.Count == 0) return;
            int i0 = cells.Min(c => c.Item1) - 1, i1 = cells.Max(c => c.Item1) + 1, j0 = cells.Min(c => c.Item2) - 1, j1 = cells.Max(c => c.Item2) + 1;
            var outside = new HashSet<(int, int)>();
            var st = new Stack<(int, int)>();
            st.Push((i0, j0)); outside.Add((i0, j0));
            while (st.Count > 0)
            {
                var (i, j) = st.Pop();
                foreach (var n in new[] { (i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1) })
                {
                    if (n.Item1 < i0 || n.Item1 > i1 || n.Item2 < j0 || n.Item2 > j1 || cells.Contains(n) || !outside.Add(n)) continue;
                    st.Push(n);
                }
            }
            for (int i = i0; i <= i1; i++)
            for (int j = j0; j <= j1; j++)
                if (!outside.Contains((i, j))) cells.Add((i, j));
        }

        static List<List<Vector2>> Outlines(HashSet<(int, int)> cellsIn, int minCells, Func<float, float> X, Func<float, float> Z)
        {
            var comps = Components(cellsIn);
            int biggest = comps.Count > 0 ? comps.Max(c => c.Count) : 0;
            var keep = new HashSet<(int, int)>(comps.Where(c => c.Count >= minCells || c.Count == biggest).SelectMany(c => c));
            FillHoles(keep);
            var res = new List<List<Vector2>>();
            foreach (var loop in Trace(keep))
                res.Add(loop.Select(v => new Vector2(X(v.Item1), Z(v.Item2))).ToList());
            return res;
        }

        /// <summary>Boundary loops of a cell set, counter-clockwise with the cells on the left; at a corner where two
        /// loops touch the left turn is taken, so they stay apart. Collinear corners are dropped.</summary>
        static List<List<(int, int)>> Trace(HashSet<(int, int)> cells)
        {
            var edges = new Dictionary<(int, int), List<(int, int)>>();
            void E((int, int) a, (int, int) b)
            {
                if (!edges.TryGetValue(a, out var l)) edges[a] = l = new List<(int, int)>();
                l.Add(b);
            }
            foreach (var (i, j) in cells)
            {
                if (!cells.Contains((i, j - 1))) E((i, j), (i + 1, j));
                if (!cells.Contains((i + 1, j))) E((i + 1, j), (i + 1, j + 1));
                if (!cells.Contains((i, j + 1))) E((i + 1, j + 1), (i, j + 1));
                if (!cells.Contains((i - 1, j))) E((i, j + 1), (i, j));
            }
            var loops = new List<List<(int, int)>>();
            while (edges.Count > 0)
            {
                var start = edges.Keys.OrderBy(k => k.Item2).ThenBy(k => k.Item1).First();
                var loop = new List<(int, int)> { start };
                var cur = start;
                (int, int) dir = (0, 0);
                for (int guard = 0; guard < 1000000; guard++)
                {
                    if (!edges.TryGetValue(cur, out var outs) || outs.Count == 0) break;
                    (int, int) next = outs[0];
                    if (outs.Count > 1 && dir != (0, 0))
                    {
                        var left = (cur.Item1 - dir.Item2, cur.Item2 + dir.Item1);
                        var straight = (cur.Item1 + dir.Item1, cur.Item2 + dir.Item2);
                        if (outs.Contains(left)) next = left;
                        else if (outs.Contains(straight)) next = straight;
                    }
                    outs.Remove(next);
                    if (outs.Count == 0) edges.Remove(cur);
                    dir = (next.Item1 - cur.Item1, next.Item2 - cur.Item2);
                    cur = next;
                    if (cur == start) break;
                    loop.Add(cur);
                }
                // drop collinear corners
                var simple = new List<(int, int)>();
                for (int k = 0; k < loop.Count; k++)
                {
                    var a = loop[(k + loop.Count - 1) % loop.Count]; var b = loop[k]; var c = loop[(k + 1) % loop.Count];
                    long cross = (long)(b.Item1 - a.Item1) * (c.Item2 - b.Item2) - (long)(b.Item2 - a.Item2) * (c.Item1 - b.Item1);
                    if (cross != 0) simple.Add(b);
                }
                if (simple.Count >= 3) loops.Add(simple);
            }
            return loops;
        }

        // ---------------------------------------------------------------------------------------------
        // the scene's markers, by reflection (IronCitadel.Rooms.IronCitadelMarker)

        sealed class Marker
        {
            public string Kind, Room, Note, Path;
            public Vector3 Position, Forward;
        }

        static List<Marker> Markers(Scene scene)
        {
            var res = new List<Marker>();
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType("IronCitadel.Rooms.IronCitadelMarker", false);
                if (type != null) break;
            }
            if (type == null) return res;
            var fKind = type.GetField("kind"); var fRoom = type.GetField("room"); var fNote = type.GetField("note");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren(type, false))
            {
                res.Add(new Marker
                {
                    Kind = Convert.ToString(fKind?.GetValue(c), CultureInfo.InvariantCulture), Room = fRoom?.GetValue(c) as string,
                    Note = fNote?.GetValue(c) as string, Path = Doors.PathOf(c.transform), Position = c.transform.position, Forward = c.transform.forward
                });
            }
            return res;
        }

        static void FromPlacements(string runDir, Dictionary<string, Room> byId, ref JObj startJ, List<object> enemiesJ, List<object> pickupsJ, List<object> notes)
        {
            var p = Path.Combine(runDir, "placements.json");
            if (!File.Exists(p)) { notes.Add("the scene has no markers and the run no placements.json"); return; }
            var pl = Obj(MiniJson.Parse(File.ReadAllText(p)));
            Vector3 P(object o) { var l = Arr(o); return new Vector3(Fl(l[0]), Fl(l[1]), Fl(l[2])); }
            List<object> Yaw(object o) { float a = Fl(o) * Mathf.Deg2Rad; return V2(Mathf.Sin(a), Mathf.Cos(a)); }
            if (startJ == null && Obj(Get(pl, "player_start")) is Dictionary<string, object> st)
            {
                startJ = new JObj().Add("position", P(Get(st, "pos"))).Add("facing", Yaw(Get(st, "yaw")));
                notes.Add("start: from placements.json (no PlayerStart marker in the scene)");
            }
            if (enemiesJ.Count == 0)
            {
                foreach (var o in Arr(Get(pl, "enemies")))
                {
                    var d = Obj(o);
                    var k = (S(Get(d, "kind")) ?? "").ToLowerInvariant();
                    enemiesJ.Add(new JObj().Add("kind", k == "boss" || k == "warlord" ? "warlord" : k == "ranged" ? "ranged" : "melee")
                        .Add("room", NameOf(byId, S(Get(d, "room")))).Add("position", P(Get(d, "pos"))).Add("facing", Yaw(Get(d, "yaw"))).Add("source", "placements.json " + S(Get(d, "id"))));
                }
                notes.Add("enemies: from placements.json (no defender markers in the scene)");
            }
            if (pickupsJ.Count == 0)
            {
                foreach (var o in Arr(Get(pl, "pickups")))
                {
                    var d = Obj(o);
                    var id = (S(Get(d, "id")) ?? "").ToLowerInvariant();
                    pickupsJ.Add(new JObj().Add("kind", id.Contains("hoard") ? "hoard" : id.Contains("gear") ? "best gear" : id.Replace('_', ' '))
                        .Add("room", NameOf(byId, S(Get(d, "room")))).Add("position", P(Get(d, "pos"))).Add("source", "placements.json " + id));
                }
                notes.Add("pickups: from placements.json (no pickup markers in the scene)");
            }
        }
    }
}
