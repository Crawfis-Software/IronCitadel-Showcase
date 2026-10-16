using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace IronCitadelV2.Editor
{
    public static partial class IronCitadelV2Builder
    {
        public const float AgentRadius = 0.35f, AgentHeight = 1.8f, AgentClimb = 0.3f, AgentSlope = 45f;
        const float FloorBand = 0.35f;            // a sample within this of y = 0 is "at floor level"
        const float StartStandOff = 8f;           // the start sees the looked-at doorway from this far or farther
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        sealed class Sample
        {
            public int id, i, j;
            public float y, clear;
            public Vector3 nav;
            public string room;
            public bool reach;
        }

        sealed class FloorData
        {
            public int nx, nz;
            public float ox, oz;
            public readonly List<Sample> samples = new List<Sample>();
            public readonly List<(int a, int b)> links = new List<(int, int)>();
            public readonly List<(int i, int j, string room, float top)> blocks = new List<(int, int, string, float)>();
            public readonly List<(string door, List<int> ids)> doors = new List<(string, List<int>)>();
            public List<Sample>[] columns;
            public int Col(int i, int j) => j * nx + i;
        }

        // ------------------------------------------------------------------ NavMesh

        static NavMeshBuildSettings NavSettings()
        {
            var s = NavMesh.GetSettingsByID(0);
            if (s.agentTypeID == -1) s = NavMesh.GetSettingsByIndex(0);
            s.agentRadius = AgentRadius;
            s.agentHeight = AgentHeight;
            s.agentClimb = AgentClimb;
            s.agentSlope = AgentSlope;
            s.minRegionArea = 2f;
            s.overrideVoxelSize = true;
            s.voxelSize = 0.1f;
            s.ledgeDropHeight = 0f;
            s.maxJumpAcrossDistance = 0f;
            s.buildHeightMesh = true;
            return s;
        }

        /// <summary>Bake over the rooms' physics colliders, every door open: the ceilings (each room's Ceiling group)
        /// count as Not Walkable, so no roof grows a NavMesh; triggers, markers and the walker are left out (they do
        /// not exist yet at this step). The data is saved as an asset beside the scene and loaded by the scene's
        /// IronCitadelV2NavMesh, in edit and play mode.</summary>
        static void Bake(Ctx c)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            NavMesh.RemoveAllNavMeshData();
            c.settings = NavSettings();
            c.filter = new NavMeshQueryFilter { agentTypeID = c.settings.agentTypeID, areaMask = NavMesh.AllAreas };
            float W = c.level.tile * c.level.cols, D = c.level.tile * c.level.rows;
            float top = TopOfLevel(c) + 2f;
            var bounds = new Bounds(new Vector3(W / 2f, (top - 3f) / 2f, -D / 2f), new Vector3(W + 4f, top + 3f, D + 4f));
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(bounds, AllLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                false, markups, false, c.scene, sources);
#pragma warning restore CS0618
            int triggers = 0, ceilings = 0, other = 0;
            var kept = new List<NavMeshBuildSource>(sources.Count);
            foreach (var s in sources)
            {
                var comp = s.component;
                if (comp is Collider col && col.isTrigger) { triggers++; continue; }
                if (comp != null && (comp.transform.IsChildOf(c.markT) || (c.playT != null && comp.transform.IsChildOf(c.playT)))) { other++; continue; }
                var src = s;
                if (comp != null && IsCeiling(comp.transform)) { src.area = 1; ceilings++; }
                kept.Add(src);
            }
            // the asset beside the scene: rebaked in place when it exists, so its GUID (and the scene's reference to
            // it) stays the same from build to build; a fresh asset only the first time or when the update fails
            var old = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            string how;
            if (old != null && UnityEngine.AI.NavMeshBuilder.UpdateNavMeshData(old, c.settings, kept, bounds))
            {
                EditorUtility.SetDirty(old);
                AssetDatabase.SaveAssetIfDirty(old);
                how = "rebaked in place";
            }
            else
            {
                var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(c.settings, kept, bounds, Vector3.zero, Quaternion.identity);
                if (data == null) throw new InvalidOperationException("NavMeshBuilder.BuildNavMeshData returned null");
                data.name = "IronCitadelV2.NavMesh";
                if (old != null) AssetDatabase.DeleteAsset(NavMeshPath);
                AssetDatabase.CreateAsset(data, NavMeshPath);
                AssetDatabase.SaveAssets();
                how = old != null ? "the in-place rebake failed, so a fresh asset" : "a fresh asset";
            }
            float bakeS = (float)sw.Elapsed.TotalSeconds;
            c.navData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            var holder = new GameObject("NavMesh");
            holder.transform.SetParent(c.root.transform, false);
            var loader = holder.AddComponent<IronCitadelV2NavMesh>();
            loader.data = c.navData;
            loader.Reload();
            if (!loader.Loaded) throw new InvalidOperationException("the baked NavMesh did not load");

            var tri = NavMesh.CalculateTriangulation();
            float area = 0f;
            for (int k = 0; k + 2 < tri.indices.Length; k += 3) area += TriArea(tri.vertices[tri.indices[k]], tri.vertices[tri.indices[k + 1]], tri.vertices[tri.indices[k + 2]]);
            c.Log($"NavMesh: {kept.Count} sources ({ceilings} ceiling pieces as Not Walkable; {triggers} triggers and {other} markers left out), " +
                  $"agent r {AgentRadius} h {AgentHeight} climb {AgentClimb} slope {AgentSlope}, voxel 0.1, every door open; " +
                  $"{tri.indices.Length / 3} triangles, {area:0} m2, baked in {bakeS:0.0} s; {NavMeshPath} ({how}, guid {AssetDatabase.AssetPathToGUID(NavMeshPath)})");
        }

        static float TopOfLevel(Ctx c)
        {
            float top = 0f;
            foreach (var go in c.rooms.Values) top = Mathf.Max(top, BoundsOf(go.transform).max.y);
            foreach (var p in c.picks.Values) top = Mathf.Max(top, p.lid);
            return Mathf.Max(top, 6f);
        }

        static float TriArea(Vector3 a, Vector3 b, Vector3 d) => Vector3.Cross(b - a, d - a).magnitude * 0.5f;

        // ------------------------------------------------------------------ the start

        /// <summary>placements.json's player_start, else a point in the start room: on the NavMesh at floor level, at
        /// least 0.8 m from its edge, inside the room's cell, walking to every doorway of the room (not a pocket walled
        /// in by furniture), and, where it can, seeing the doorway of start.looks_at from eye height and from at least
        /// <see cref="StartStandOff"/> away (the brief: the player sees the armory's guarded entrance from the safe
        /// entry hall, not from its threshold); of those, the nearest to the cell's centre (1 m steps out to 22 m).
        /// It faces that doorway.</summary>
        static void ChooseStart(Ctx c)
        {
            var room = c.level.Room(c.level.startRoom);
            var centre = V2Model.CellCenter(c.level.tile, room.r, room.c);
            Vector3? look = null, seePt = null;
            var d = c.level.doors.FirstOrDefault(x => x.id == c.level.startLooksAt) ?? (c.level.startLooksAt != null ? c.level.DoorBetween(c.level.startRoom, c.level.startLooksAt) : null);
            if (d != null && c.openings.TryGetValue(d.id, out var o))
            {
                look = o.found ? o.Centre : o.e.centre;
                var intoStart = d.a == room.id ? -o.e.n : o.e.n;      // e.n points from a to b
                seePt = look.Value + intoStart * 0.4f + Vector3.up * 1.4f;
            }
            if (c.placements != null && c.placements.hasStart)
            {
                c.start = c.placements.startPos;
                c.startYaw = c.placements.startYaw;
                c.startHow = $"placements.json player_start {V(c.start)} yaw {c.startYaw:0}";
                c.Log("start: " + c.startHow);
                return;
            }
            // the room's doorways, on the NavMesh: the start must reach every one of them (a cluttered room has
            // pockets walled in by its furniture, and the floor nearest the centre can be one)
            var ways = new List<Vector3>();
            foreach (var rd in c.level.doors.Where(x => x.a == room.id || x.b == room.id))
            {
                var wp = c.openings.TryGetValue(rd.id, out var ro) && ro.found ? ro.Centre : V2Model.EdgeOfDoor(c.level, rd).centre;
                if (NavMesh.SamplePosition(wp + Vector3.up * 0.3f, out var wh, 1.5f, c.filter)) ways.Add(wh.position);
            }
            var path = new NavMeshPath();
            bool ReachesWays(Vector3 p) => ways.All(w => NavMesh.CalculatePath(p, w, c.filter, path) && path.status == NavMeshPathStatus.PathComplete);
            Vector3? found = null;
            bool foundSees = false;
            float bestScore = float.MaxValue, foundDist = 0f;
            int pockets = 0;
            for (int ring = 0; ring <= 22; ring++)
                for (int dz = -ring; dz <= ring; dz++)
                    for (int dx = -ring; dx <= ring; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        var p = centre + new Vector3(dx, 0f, dz);
                        if (!NavMesh.SamplePosition(p + Vector3.up * 0.2f, out var h, 0.5f, c.filter)) continue;
                        if (Mathf.Abs(h.position.y) > FloorBand || c.RoomAt(h.position) != room) continue;
                        bool sees = seePt.HasValue && !Physics.Linecast(h.position + Vector3.up * 1.7f, seePt.Value, AllLayers, QueryTriggerInteraction.Ignore);
                        float dist = look.HasValue ? V2Model.Flat(h.position - look.Value).magnitude : 0f;
                        // tiers: sees it from the stand-off or farther, sees it from nearer, does not see it
                        int tier = !seePt.HasValue ? 0 : sees ? (dist >= StartStandOff ? 0 : 1) : 2;
                        float score = tier * 1000f + V2Model.Flat(h.position - centre).magnitude;
                        if (score >= bestScore) continue;
                        if (!NavMesh.FindClosestEdge(h.position, out var edge, c.filter) || edge.distance < 0.8f) continue;
                        if (!ReachesWays(h.position)) { pockets++; continue; }
                        found = h.position; foundSees = sees; foundDist = dist; bestScore = score;
                    }
            if (found == null) throw new InvalidOperationException($"no NavMesh at floor level within 22 m of {room.id}'s centre that reaches its {ways.Count} doorway(s), for the start");
            c.start = found.Value;
            c.startYaw = look.HasValue ? V2Model.Yaw(V2Model.Flat(look.Value - c.start)) : 90f;
            c.startHow = $"no placements.json: the open floor nearest {room.id}'s centre that walks to all {ways.Count} of its doorways" +
                         (d == null ? ", facing east" : foundSees ? $" and sees the doorway of {d.id} from {foundDist:0.0} m, facing it" : $", facing {d.id} (no such floor sees its doorway)");
            if (d != null && !foundSees) c.Problem($"start: no open floor in {room.id} that walks to its doorways sees the doorway of {d.id}");
            else if (d != null && foundDist < StartStandOff) c.Log($"  note: no floor in {room.id} sees the doorway of {d.id} from {StartStandOff:0} m or more; the start is {foundDist:0.0} m from it");
            c.Log($"start: {V(c.start)} yaw {c.startYaw:0} ({c.startHow}; {pockets} better-placed point(s) skipped, walled in by furniture)");
        }

        // ------------------------------------------------------------------ floor.json

        /// <summary>CONTRACT.md floor.json: a column every metre; in each, a RaycastAll down from above the highest
        /// lid; a hit is kept when the NavMesh lies within 0.2 m of it and it has 1.8 m of headroom (one sample per
        /// surface: hits within 0.3 m are one). Links join samples in 4-neighbouring columns when NavMesh.Raycast
        /// between them is clear and they differ by 0.6 m or less (0.75 m in the mansion: LinkStep). reach: joined to the start over the links, or, for
        /// a piece the links do not join, a complete NavMesh path from the start to one of its samples. blocks: the
        /// columns of a room's tile with no sample at floor level; top is the height of what stands there (a 0.9 m
        /// box climbed in 0.1 m slabs until 1.2 m of it is clear), the room's lid when that reaches the lid or
        /// when the column has no floor at all. doors: the samples within 1.5 m of each design door's crossing.</summary>
        static void SampleFloor(Ctx c)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var F = new FloorData
            {
                nx = Mathf.RoundToInt(c.level.tile * c.level.cols),
                nz = Mathf.RoundToInt(c.level.tile * c.level.rows),
                ox = 0f,
                oz = -c.level.tile * c.level.rows
            };
            F.columns = new List<Sample>[F.nx * F.nz];
            float topY = TopOfLevel(c) + 5f;
            var hits = new RaycastHit[96];
            var ys = new List<float>();
            for (int j = 0; j < F.nz; j++)
                for (int i = 0; i < F.nx; i++)
                {
                    float x = F.ox + i + 0.5f, z = F.oz + j + 0.5f;
                    var room = c.RoomAt(new Vector3(x, 0f, z));
                    int n = Physics.RaycastNonAlloc(new Vector3(x, topY, z), Vector3.down, hits, topY + 6f, AllLayers, QueryTriggerInteraction.Ignore);
                    ys.Clear();
                    for (int k = 0; k < n; k++) ys.Add(hits[k].point.y);
                    ys.Sort();
                    var col = new List<Sample>();
                    foreach (var y in ys)
                    {
                        if (col.Count > 0 && y - col[col.Count - 1].y < 0.3f) continue;
                        var p = new Vector3(x, y, z);
                        if (!NavMesh.SamplePosition(p, out var nh, 0.2f, c.filter)) continue;
                        if (col.Any(q => Mathf.Abs(q.nav.y - nh.position.y) < 0.3f)) continue;
                        float clear = Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, out var up, 60f, AllLayers, QueryTriggerInteraction.Ignore)
                            ? up.distance + 0.05f
                            : Mathf.Max(0f, (room != null ? c.Lid(room.id) : 10f) - y);
                        if (clear < AgentHeight) continue;
                        var s = new Sample { i = i, j = j, y = y, clear = clear, nav = nh.position, room = room?.id };
                        col.Add(s);
                    }
                    F.columns[F.Col(i, j)] = col;
                    foreach (var s in col) { s.id = F.samples.Count; F.samples.Add(s); }

                    // blocks: a room's column with no sample at floor level
                    if (room == null || col.Any(q => Mathf.Abs(q.y) <= FloorBand)) continue;
                    bool floor = ys.Any(q => Mathf.Abs(q) <= FloorBand);
                    float lid = c.Lid(room.id);
                    float top = !floor && !Physics.CheckBox(new Vector3(x, 0.15f, z), new Vector3(0.45f, 0.1f, 0.45f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore)
                        ? lid
                        : ObstacleTop(x, z, lid);
                    F.blocks.Add((i, j, room.id, top));
                }
            float sampleS = (float)sw.Elapsed.TotalSeconds;

            // links
            foreach (var s in F.samples)
            {
                foreach (var (di, dj) in new[] { (1, 0), (0, 1) })
                {
                    int i2 = s.i + di, j2 = s.j + dj;
                    if (i2 >= F.nx || j2 >= F.nz) continue;
                    foreach (var t in F.columns[F.Col(i2, j2)])
                    {
                        if (Mathf.Abs(t.y - s.y) > LinkStep) continue;
                        if (NavMesh.Raycast(s.nav, t.nav, out _, c.filter)) continue;
                        F.links.Add((Mathf.Min(s.id, t.id), Mathf.Max(s.id, t.id)));
                    }
                }
            }
            F.links.Sort();

            // reach: the start's piece over the links, then whole link-pieces joined by a NavMesh path
            var adj = new List<int>[F.samples.Count];
            for (int k = 0; k < adj.Length; k++) adj[k] = new List<int>();
            foreach (var (a, b) in F.links) { adj[a].Add(b); adj[b].Add(a); }
            var comp = new int[F.samples.Count];
            for (int k = 0; k < comp.Length; k++) comp[k] = -1;
            int pieces = 0;
            var queue = new Queue<int>();
            for (int k = 0; k < comp.Length; k++)
            {
                if (comp[k] >= 0) continue;
                comp[k] = pieces; queue.Enqueue(k);
                while (queue.Count > 0) { int u = queue.Dequeue(); foreach (var v in adj[u]) if (comp[v] < 0) { comp[v] = pieces; queue.Enqueue(v); } }
                pieces++;
            }
            if (!NavMesh.SamplePosition(c.start + Vector3.up * 0.2f, out var sh, 1.5f, c.filter))
                throw new InvalidOperationException("the start " + V(c.start) + " is not on the NavMesh");
            var startNav = sh.position;
            var startSample = F.samples.Where(s => Mathf.Abs(s.nav.y - startNav.y) < 0.6f)
                .OrderBy(s => V2Model.Flat(s.nav - startNav).sqrMagnitude).FirstOrDefault();
            var reachPiece = new bool[pieces];
            if (startSample != null) reachPiece[comp[startSample.id]] = true;
            var path = new NavMeshPath();
            int tested = 0, joined = 0;
            var reps = new Dictionary<int, Sample>();
            foreach (var s in F.samples) if (!reps.ContainsKey(comp[s.id])) reps[comp[s.id]] = s;
            foreach (var kv in reps.OrderBy(k => k.Key))
            {
                if (reachPiece[kv.Key]) continue;
                tested++;
                if (NavMesh.CalculatePath(startNav, kv.Value.nav, c.filter, path) && path.status == NavMeshPathStatus.PathComplete)
                { reachPiece[kv.Key] = true; joined++; }
            }
            foreach (var s in F.samples) s.reach = reachPiece[comp[s.id]];

            // doors: samples within 1.5 m of each design door's crossing (the measured doorway's middle at its floor;
            // the edge's middle when no doorway was measured)
            foreach (var d in c.level.doors)
            {
                var cr = c.openings.TryGetValue(d.id, out var op) && op.found ? op.Centre : V2Model.EdgeOfDoor(c.level, d).centre;
                var ids = new List<int>();
                int i0 = Mathf.FloorToInt(cr.x - F.ox) - 3, j0 = Mathf.FloorToInt(cr.z - F.oz) - 3;
                for (int j = Mathf.Max(0, j0); j <= Mathf.Min(F.nz - 1, j0 + 6); j++)
                    for (int i = Mathf.Max(0, i0); i <= Mathf.Min(F.nx - 1, i0 + 6); i++)
                        foreach (var s in F.columns[F.Col(i, j)])
                            if (Vector3.Distance(new Vector3(F.ox + i + 0.5f, s.y, F.oz + j + 0.5f), cr) <= 1.5f) ids.Add(s.id);
                ids.Sort();
                F.doors.Add((d.id, ids));
                if (ids.Count == 0) c.Problem($"floor.json: no sample within 1.5 m of door {d.id}'s crossing {V(cr)}");
            }
            c.floor = F;
            int reach = F.samples.Count(s => s.reach);
            c.Log($"floor: {F.nx} x {F.nz} columns, {F.samples.Count} samples ({reach} reached from the start, {F.samples.Count - reach} not), {F.links.Count} links, " +
                  $"{F.blocks.Count} blocks; {pieces} link-pieces, {tested} tested with a NavMesh path, {joined} joined that way; " +
                  $"sampled in {sampleS:0.0} s, all in {sw.Elapsed.TotalSeconds:0.0} s");
            foreach (var g in F.samples.GroupBy(s => s.room ?? "(none)"))
                c.Log($"  floor {g.Key}: {g.Count()} samples, {g.Count(s => s.reach)} reached, {g.Count(s => s.y > 1f)} above 1 m");
        }

        /// <summary>How high what stands in a column rises above the floor: a 0.9 m square box climbed in slabs (0.1 m
        /// up to 3 m, 0.25 m above), until 1.2 m above the last solid slab is clear. The lid when it is still solid
        /// within 0.3 m of the lid.</summary>
        static float ObstacleTop(float x, float z, float lid)
        {
            float y = 0.05f, lastTop = 0f;
            while (y < lid - 0.3f)
            {
                float h = y < 3f ? 0.1f : 0.25f;
                bool solid = Physics.CheckBox(new Vector3(x, y + h / 2f, z), new Vector3(0.45f, h / 2f - 0.005f, 0.45f), Quaternion.identity, AllLayers, QueryTriggerInteraction.Ignore);
                if (solid) lastTop = y + h;
                else if (y - lastTop >= 1.2f) return Round2(lastTop);
                y += h;
            }
            return y - lastTop < 1.2f && lastTop > 0f ? lid : Round2(lastTop);
        }

        static float Round2(float v) => Mathf.Round(v * 100f) / 100f;

        static void WriteFloor(Ctx c)
        {
            var F = c.floor;
            var sb = new StringBuilder(F.samples.Count * 90 + F.links.Count * 20);
            sb.Append("{\n");
            sb.Append("  \"version\": 1,\n");
            sb.Append("  \"level\": ").Append(JsonConvert.ToString(c.level.name)).Append(",\n");
            sb.Append("  \"cell_m\": 1.0,\n");
            sb.Append("  \"origin\": [").Append(N(F.ox, "0.0")).Append(", ").Append(N(F.oz, "0.0")).Append("],\n");
            sb.Append("  \"nx\": ").Append(F.nx).Append(", \"nz\": ").Append(F.nz).Append(",\n");
            sb.Append("  \"agent\": { \"radius\": 0.35, \"height\": 1.8, \"climb\": 0.3, \"slope\": 45 },\n");
            sb.Append("  \"start\": [").Append(N(c.start.x)).Append(", ").Append(N(c.start.y)).Append(", ").Append(N(c.start.z)).Append("],\n");
            sb.Append("  \"samples\": [\n");
            for (int k = 0; k < F.samples.Count; k++)
            {
                var s = F.samples[k];
                sb.Append("    { \"id\": ").Append(s.id).Append(", \"i\": ").Append(s.i).Append(", \"j\": ").Append(s.j)
                  .Append(", \"y\": ").Append(N(s.y)).Append(", \"room\": ").Append(s.room == null ? "null" : JsonConvert.ToString(s.room))
                  .Append(", \"reach\": ").Append(s.reach ? "true" : "false").Append(", \"clear\": ").Append(N(Mathf.Min(s.clear, 60f))).Append(" }")
                  .Append(k + 1 < F.samples.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n");
            sb.Append("  \"links\": [\n");
            for (int k = 0; k < F.links.Count; k++)
                sb.Append("    [").Append(F.links[k].a).Append(", ").Append(F.links[k].b).Append("]").Append(k + 1 < F.links.Count ? ",\n" : "\n");
            sb.Append("  ],\n");
            sb.Append("  \"blocks\": [\n");
            for (int k = 0; k < F.blocks.Count; k++)
            {
                var b = F.blocks[k];
                sb.Append("    { \"i\": ").Append(b.i).Append(", \"j\": ").Append(b.j).Append(", \"room\": ").Append(JsonConvert.ToString(b.room))
                  .Append(", \"top\": ").Append(N(b.top)).Append(" }").Append(k + 1 < F.blocks.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n");
            sb.Append("  \"doors\": [\n");
            for (int k = 0; k < F.doors.Count; k++)
            {
                var d = F.doors[k];
                sb.Append("    { \"door\": ").Append(JsonConvert.ToString(d.door)).Append(", \"samples\": [").Append(string.Join(", ", d.ids)).Append("] }")
                  .Append(k + 1 < F.doors.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n");
            sb.Append("}\n");
            string path = c.runDir + "/floor.json";
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            c.Log($"wrote {path} ({new FileInfo(path).Length / 1024} KB)");
        }

        static string N(float v, string fmt = "0.###") => v.ToString(fmt, Inv);

        // ------------------------------------------------------------------ the room graph as built

        sealed class EdgeResult
        {
            public string a, b, side, kind, how, designDoor;
            public bool walkable, design;
            public float pathM = -1f;
            public List<Vector2> runs = new List<Vector2>();
            public V2Edge e;
        }

        /// <summary>For every pair of neighbouring rooms, with every door open: along the shared edge every 0.25 m
        /// (within 22 m of its middle), is there NavMesh 0.75 m either side (snapped within 0.35 m) with a clear
        /// NavMesh.Raycast between the two? Runs of such crossings are openings. The run nearest the middle is the
        /// door; it is walkable when a NavMesh path between points 1.5 m either side of the run's middle is complete
        /// and at most 4.5 m long. Openings on an edge the design has no door for are extra openings.</summary>
        static void CheckRoomGraph(Ctx c)
        {
            var results = new List<EdgeResult>();
            foreach (var A in c.level.rooms)
                foreach (char side in new[] { 'E', 'S' })
                {
                    var e = V2Model.EdgeOf(c.level.tile, A.r, A.c, side);
                    var B = c.level.At(e.r2, e.c2);
                    if (B == null) continue;
                    var d = c.level.DoorBetween(A.id, B.id);
                    var res = new EdgeResult { e = e, design = d != null, designDoor = d?.id };
                    if (d != null && d.a != A.id) { res.a = d.a; res.b = d.b; res.side = d.side; res.e = V2Model.EdgeOfDoor(c.level, d); }
                    else { res.a = A.id; res.b = B.id; res.side = side.ToString(); }
                    ScanEdge(c, res);
                    var bd = c.doors.FirstOrDefault(x => (x.door.roomA == res.a && x.door.roomB == res.b) || (x.door.roomA == res.b && x.door.roomB == res.a));
                    res.kind = bd != null ? bd.door.kind : "open";
                    results.Add(res);
                }

            var notes = new List<string>();
            var doorsJ = new JArray();
            var extras = new JArray();
            foreach (var r in results)
            {
                if (!r.design && r.runs.Count == 0 && !r.walkable) continue;
                var mid = r.runs.Count > 0 ? r.runs.OrderBy(x => Mathf.Abs((x.x + x.y) / 2f)).First() : Vector2.zero;
                if (r.design || r.walkable)
                    doorsJ.Add(new JObject
                    {
                        ["a"] = r.a, ["b"] = r.b, ["side_of_a"] = r.side, ["kind"] = r.kind, ["walkable"] = r.walkable, ["how"] = r.how,
                        ["design_door"] = r.designDoor, ["opening_m"] = r.runs.Count > 0 ? Math.Round(mid.y - mid.x + 0.25f, 2) : 0.0,
                        ["path_m"] = r.pathM >= 0f ? Math.Round(r.pathM, 2) : (double?)null
                    });
                if (!r.design)
                    foreach (var run in r.runs)
                    {
                        var p = r.e.centre + r.e.along * ((run.x + run.y) / 2f);
                        extras.Add(new JObject { ["a"] = r.a, ["b"] = r.b, ["side_of_a"] = r.side, ["at"] = new JArray(Math.Round(p.x, 2), Math.Round(p.y, 2), Math.Round(p.z, 2)), ["width_m"] = Math.Round(run.y - run.x + 0.25f, 2) });
                    }
                else if (r.runs.Count > 1)
                    notes.Add($"{r.designDoor}: {r.runs.Count} openings on its edge (the door's and {r.runs.Count - 1} more at t {string.Join(", ", r.runs.Where(x => x != mid).Select(x => F((x.x + x.y) / 2f, "0.0")))})");
            }

            // checks
            var designDoors = c.level.doors.Select(d => results.FirstOrDefault(r => r.designDoor == d.id)).ToList();
            var missing = c.level.doors.Where(d => !results.Any(r => r.designDoor == d.id && r.walkable)).Select(d => d.id).ToList();
            var wrongKind = c.level.doors.Where(d => results.Any(r => r.designDoor == d.id && r.kind != d.kind)).Select(d => d.id).ToList();
            bool doorsMatch = missing.Count == 0 && wrongKind.Count == 0 && extras.Count == 0;
            if (missing.Count > 0) notes.Add("design doors not walkable as built: " + string.Join(", ", missing));
            if (wrongKind.Count > 0) notes.Add("design doors built as another kind: " + string.Join(", ", wrongKind));
            if (extras.Count > 0) notes.Add($"{extras.Count} opening(s) on edges the design has no door for");

            bool Walk(string a, string b) => results.Any(r => r.walkable && ((r.a == a && r.b == b) || (r.a == b && r.b == a)));
            bool loopOk = true;
            var loop = c.level.bigLoop;
            for (int k = 0; k < loop.Count && loop.Count > 1; k++)
            {
                string a = loop[k], b = loop[(k + 1) % loop.Count];
                if (!Walk(a, b)) { loopOk = false; notes.Add($"big loop broken between {a} and {b}"); }
            }
            if (loop.Count < 2) { loopOk = false; notes.Add("level.json has no big_loop"); }

            var vault = c.level.RoomOfKind("V");
            bool vaultOk = false;
            if (vault == null) notes.Add("no vault (kind V) in level.json");
            else
            {
                var ways = results.Where(r => r.walkable && (r.a == vault.id || r.b == vault.id)).ToList();
                vaultOk = ways.Count == 1;
                notes.Add($"vault {vault.id}: {ways.Count} walkable crossing(s) ({string.Join(", ", ways.Select(w => (w.a == vault.id ? w.b : w.a) + " " + w.kind))})");
            }
            notes.Add("method: " + MethodNote);

            var roomsJ = new JArray();
            var tri = NavMesh.CalculateTriangulation();
            var area = new Dictionary<string, double>();
            for (int k = 0; k + 2 < tri.indices.Length; k += 3)
            {
                Vector3 a = tri.vertices[tri.indices[k]], b = tri.vertices[tri.indices[k + 1]], d = tri.vertices[tri.indices[k + 2]];
                var room = c.RoomAt((a + b + d) / 3f);
                if (room == null) continue;
                area[room.id] = (area.TryGetValue(room.id, out var x) ? x : 0.0) + TriArea(a, b, d);
            }
            foreach (var room in c.level.rooms)
            {
                var p = c.picks[room.id];
                roomsJ.Add(new JObject
                {
                    ["id"] = room.id, ["cell"] = new JArray(room.r, room.c), ["prefab"] = p.prefab, ["rotation"] = p.rotation,
                    ["navmesh_m2"] = Math.Round(area.TryGetValue(room.id, out var m2) ? m2 : 0.0, 1),
                    ["reach_m2"] = c.floor.samples.Count(s => s.room == room.id && s.reach)
                });
            }

            c.built = new JObject
            {
                ["level"] = c.level.name,
                ["scene"] = ScenePath,
                ["built_at"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", Inv),
                ["rooms"] = roomsJ,
                ["doors"] = doorsJ,
                ["extra_openings"] = extras,
                ["enemies"] = new JArray(),
                ["checks"] = new JObject
                {
                    ["doors_match_design"] = doorsMatch,
                    ["big_loop_intact"] = loopOk,
                    ["vault_dead_end"] = vaultOk,
                    ["notes"] = new JArray(notes)
                },
                ["navmesh"] = new JObject
                {
                    ["asset"] = NavMeshPath, ["agent"] = new JObject { ["radius"] = AgentRadius, ["height"] = AgentHeight, ["climb"] = AgentClimb, ["slope"] = AgentSlope },
                    ["doors_open_when_baked"] = true, ["carving"] = false,
                    ["triangles"] = tri.indices.Length / 3, ["area_m2"] = Math.Round(area.Values.Sum(), 1)
                },
                ["start"] = new JObject { ["pos"] = new JArray(Math.Round(c.start.x, 2), Math.Round(c.start.y, 2), Math.Round(c.start.z, 2)), ["yaw"] = Math.Round(c.startYaw, 1), ["how"] = c.startHow },
            };
            c.Log($"room graph: {results.Count} neighbouring pairs; {results.Count(r => r.walkable)} walkable; doors match design {doorsMatch}, big loop intact {loopOk}, vault dead end {vaultOk}");
            foreach (var r in results)
                c.Log($"  edge {r.a}-{r.b} ({r.side}{(r.design ? ", design " + r.designDoor : ", no design door")}): {(r.walkable ? "WALKABLE" : "closed")}, kind {r.kind}, " +
                      $"openings {(r.runs.Count == 0 ? "none" : string.Join(" ", r.runs.Select(x => $"[{F(x.x, "0.00")}..{F(x.y, "0.00")}]")))}{(r.pathM >= 0f ? $", path {F(r.pathM)} m" : "")}");
            foreach (var n in notes) c.Log("  note: " + n);
        }

        const string MethodNote = "with every door open, each shared tile edge was scanned every 0.25 m: a crossing is NavMesh within 0.35 m of points " +
                                  "0.75 m either side of the edge with a clear NavMesh.Raycast between them; an edge is walkable when the run of crossings " +
                                  "nearest its middle has a complete NavMesh path between points 1.5 m either side of it no longer than 4.5 m";

        static void ScanEdge(Ctx c, EdgeResult r)
        {
            var e = r.e;
            float? s = null; float last = 0f;
            for (int k = -88; k <= 88; k++)
            {
                float t = k * 0.25f;
                var p = e.centre + e.along * t;
                bool cross = false;
                if (NavMesh.SamplePosition(p - e.n * 0.75f + Vector3.up * 0.3f, out var ha, 0.8f, c.filter) &&
                    NavMesh.SamplePosition(p + e.n * 0.75f + Vector3.up * 0.3f, out var hb, 0.8f, c.filter) &&
                    V2Model.Flat(ha.position - (p - e.n * 0.75f)).magnitude <= 0.35f &&
                    V2Model.Flat(hb.position - (p + e.n * 0.75f)).magnitude <= 0.35f &&
                    !NavMesh.Raycast(ha.position, hb.position, out _, c.filter))
                    cross = true;
                if (cross && s == null) s = t;
                if (!cross && s != null) { r.runs.Add(new Vector2(s.Value, last)); s = null; }
                last = t;
            }
            if (s != null) r.runs.Add(new Vector2(s.Value, last));
            if (r.runs.Count == 0)
            {
                r.walkable = false;
                r.how = "no NavMesh crossing found along the edge, doors open";
                return;
            }
            var run = r.runs.OrderBy(x => Mathf.Abs((x.x + x.y) / 2f)).First();
            var mid = e.centre + e.along * ((run.x + run.y) / 2f);
            var path = new NavMeshPath();
            bool ok = NavMesh.SamplePosition(mid - e.n * 1.5f + Vector3.up * 0.3f, out var pa, 1.0f, c.filter)
                   && NavMesh.SamplePosition(mid + e.n * 1.5f + Vector3.up * 0.3f, out var pb, 1.0f, c.filter)
                   && NavMesh.CalculatePath(pa.position, pb.position, c.filter, path)
                   && path.status == NavMeshPathStatus.PathComplete;
            if (ok)
            {
                float len = 0f;
                for (int k = 1; k < path.corners.Length; k++) len += Vector3.Distance(path.corners[k - 1], path.corners[k]);
                r.pathM = len;
                ok = len <= 4.5f;
            }
            r.walkable = ok;
            r.how = ok ? $"NavMesh path across the edge, doors open: {F(r.pathM)} m between points 1.5 m either side; crossing {F(run.y - run.x + 0.25f)} m wide"
                       : $"a crossing {F(run.y - run.x + 0.25f)} m wide, but no short NavMesh path across it ({(r.pathM >= 0f ? F(r.pathM) + " m" : "none")})";
        }

        // ------------------------------------------------------------------ enemies on the NavMesh, built.json

        static void CheckEnemies(Ctx c)
        {
            var arr = (JArray)c.built["enemies"];
            var defT = c.markT.Find("Defenders");
            if (defT == null) return;
            int off = 0;
            foreach (Transform m in defT)
            {
                var p = m.position;
                bool on = NavMesh.SamplePosition(p + Vector3.up * 0.1f, out var h, 0.6f, c.filter)
                          && V2Model.Flat(h.position - p).magnitude <= 0.3f && Mathf.Abs(h.position.y - p.y) <= 0.4f;
                if (!on) { off++; c.Problem($"enemy {m.name} at {V(p)} is not on the NavMesh"); }
                arr.Add(new JObject { ["id"] = m.name, ["on_navmesh"] = on, ["pos"] = new JArray(Math.Round(p.x, 2), Math.Round(p.y, 2), Math.Round(p.z, 2)) });
            }
            c.Log($"enemies on the NavMesh: {arr.Count - off}/{arr.Count}");
        }

        static void WriteBuilt(Ctx c)
        {
            var notes = (JArray)c.built["checks"]["notes"];
            foreach (var p in c.problems) notes.Add("problem: " + p);
            string path = c.runDir + "/built.json";
            var text = JsonConvert.SerializeObject(c.built, Formatting.Indented).Replace("\r\n", "\n") + "\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            c.Log("wrote " + path);
        }
    }
}
