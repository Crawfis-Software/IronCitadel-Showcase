// IronCitadel conformance kit v2: the run-time NavMesh bake (v1's) and the room graph built from it with the map's
// outlines. Each triangle belongs to the room whose outline and floor band hold its centre, to the gallery, or to
// no room; unassigned NavMesh that touches exactly two rooms (a doorway in a wall's thickness) is an opening.
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
    /// <summary>One NavMeshLink found in the scene, in world space. (v1)</summary>
    public sealed class SceneLink
    {
        public string Name;
        public Vector3 Start, End;
        public float Width;
        public bool Bidirectional;
        public int Area;
    }

    public struct Adjacent
    {
        public int Tri;
        public Vector3 P0, P1;
    }

    /// <summary>One crossing between two nodes (rooms 0..N-1, the gallery N, passages above).</summary>
    public sealed class Gap
    {
        public int A, B;
        public string Via;               // edge (outlines meet) | doorway (unassigned NavMesh between two rooms) | passage | link
        public Vector3 Centre;
        public float Width;              // NavMesh width across it (m): about the clear width less two agent radii
        public float Area;               // m2 of unassigned NavMesh in it (doorway, passage)
        public readonly List<Vector3> Points = new List<Vector3>();

        public bool Touches(int n) => A == n || B == n;
        public int Other(int n) => A == n ? B : A;

        public float DistXZ(Vector3 p)
        {
            float best = new Vector2(Centre.x - p.x, Centre.z - p.z).magnitude;
            foreach (var q in Points) best = Mathf.Min(best, new Vector2(q.x - p.x, q.z - p.z).magnitude);
            return best;
        }

        /// <summary>DistXZ over the centre and points within <paramref name="yBand"/> of p's height; infinity when none is,
        /// so a door one storey above another at the same x, z matches only its own crossing.</summary>
        public float DistNear(Vector3 p, float yBand)
        {
            float best = float.PositiveInfinity;
            if (Mathf.Abs(Centre.y - p.y) <= yBand) best = new Vector2(Centre.x - p.x, Centre.z - p.z).magnitude;
            foreach (var q in Points)
                if (Mathf.Abs(q.y - p.y) <= yBand) best = Mathf.Min(best, new Vector2(q.x - p.x, q.z - p.z).magnitude);
            return best;
        }
    }

    public sealed class Pocket
    {
        public int Node;
        public Vector3 Centre;
        public float Area;
    }

    public sealed class NavModel
    {
        public MapSpec Map;
        public NavMeshQueryFilter Filter;
        public float BakeSeconds;
        public int SourceCount, ModifierCount;
        public string Label;

        public Vector3[] V;
        public int[] I;
        public int T;
        public Vector3[] Cen;
        public float[] Area;
        public int[] Node;               // per triangle: room index, Map.GalleryNode, or -1
        public int[] TriArea;            // per triangle: its NavMesh area index
        public List<Adjacent>[] Adj;
        public int[] Comp;
        public int CompCount;
        public bool[] LevelComp;

        public readonly List<Gap> Gaps = new List<Gap>();
        public readonly List<Pocket> Pockets = new List<Pocket>();
        public readonly List<SceneLink> Links = new List<SceneLink>();
        public int Passages;
        Dictionary<long, List<int>> _triHash;

        public bool IsLevel(int t) => LevelComp[Comp[t]];
        public int NodeOf(Vector3 p) => Map.NodeAt(p);

        public string NodeName(int n)
        {
            if (n < 0) return "no room";
            if (n < Map.Rooms.Count) return Map.Rooms[n].Name;
            if (n == Map.GalleryNode) return "gallery";
            return "passage " + (n - Map.GalleryNode);
        }

        public List<Gap> GapsOf(int n) => Gaps.Where(g => g.Touches(n)).ToList();

        public float AreaOfNode(int n, bool levelOnly)
        {
            float s = 0;
            for (int t = 0; t < T; t++) if (Node[t] == n && (!levelOnly || IsLevel(t))) s += Area[t];
            return s;
        }

        public float AreaOfNavArea(int area, int node)
        {
            float s = 0;
            for (int t = 0; t < T; t++) if (TriArea[t] == area && (node < 0 || Node[t] == node)) s += Area[t];
            return s;
        }

        // ---------- building

        public static NavModel FromTriangulation(MapSpec map, NavMeshQueryFilter filter, List<SceneLink> links)
        {
            var m = new NavModel { Map = map, Filter = filter };
            m.Links.AddRange(links);
            var tri = NavMesh.CalculateTriangulation();
            m.Weld(tri.vertices, tri.indices, tri.areas);
            m.BuildAdjacency();
            m.BuildComponents();
            m.BuildGaps();
            return m;
        }

        void Weld(Vector3[] verts, int[] idx, int[] areas)
        {
            float q = Tune.WeldXZ;
            var hash = new Dictionary<long, List<int>>();
            var outV = new List<Vector3>();
            var remap = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                int kx = Mathf.RoundToInt(p.x / q), kz = Mathf.RoundToInt(p.z / q);
                int found = -1;
                for (int dx = -1; dx <= 1 && found < 0; dx++)
                for (int dz = -1; dz <= 1 && found < 0; dz++)
                {
                    if (!hash.TryGetValue(HKey(kx + dx, kz + dz), out var bucket)) continue;
                    foreach (var w in bucket)
                    {
                        var o = outV[w];
                        if (Mathf.Abs(o.x - p.x) <= q && Mathf.Abs(o.z - p.z) <= q && Mathf.Abs(o.y - p.y) <= Tune.WeldY)
                        { found = w; break; }
                    }
                }
                if (found < 0)
                {
                    found = outV.Count;
                    outV.Add(p);
                    var k = HKey(kx, kz);
                    if (!hash.TryGetValue(k, out var b)) hash[k] = b = new List<int>();
                    b.Add(found);
                }
                remap[i] = found;
            }

            var tris = new List<int>();
            var triAreas = new List<int>();
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                int a = remap[idx[t]], b = remap[idx[t + 1]], c = remap[idx[t + 2]];
                if (a == b || b == c || a == c) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
                triAreas.Add(areas != null && t / 3 < areas.Length ? areas[t / 3] : 0);
            }
            V = outV.ToArray();
            I = tris.ToArray();
            T = I.Length / 3;
            TriArea = triAreas.ToArray();
            Cen = new Vector3[T];
            Area = new float[T];
            Node = new int[T];
            for (int t = 0; t < T; t++)
            {
                var p0 = V[I[3 * t]]; var p1 = V[I[3 * t + 1]]; var p2 = V[I[3 * t + 2]];
                Cen[t] = (p0 + p1 + p2) / 3f;
                Area[t] = Mathf.Abs((p1.x - p0.x) * (p2.z - p0.z) - (p2.x - p0.x) * (p1.z - p0.z)) * 0.5f;
                Node[t] = Map.NodeAt(Cen[t]);
            }
        }

        static long HKey(int a, int b) => ((long)a << 32) ^ (uint)b;

        void BuildAdjacency()
        {
            Adj = new List<Adjacent>[T];
            for (int t = 0; t < T; t++) Adj[t] = new List<Adjacent>(3);
            var edges = new Dictionary<long, int>();
            var edgeCount = new Dictionary<long, int>();
            for (int t = 0; t < T; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = I[3 * t + e], b = I[3 * t + (e + 1) % 3];
                    long k = a < b ? HKey(a, b) : HKey(b, a);
                    if (edges.TryGetValue(k, out var other))
                    {
                        Link(t, other, V[a], V[b]);
                        edgeCount[k] = edgeCount[k] + 1;
                    }
                    else
                    {
                        edges[k] = t;
                        edgeCount[k] = 1;
                    }
                }
            }

            // (v1) NavMesh tiles meet at axis-aligned borders where the two sides need not share vertices: join
            // unmatched, axis-aligned, collinear, overlapping edges on opposite sides whose heights agree
            var groups = new Dictionary<string, List<(int tri, float lo, float hi, float ylo, float yhi, float side)>>();
            foreach (var kv in edgeCount)
            {
                if (kv.Value != 1) continue;
                int a = (int)(kv.Key >> 32), b = (int)(uint)(kv.Key & 0xffffffff);
                int t = edges[kv.Key];
                var pa = V[a]; var pb = V[b];
                bool alongX = Mathf.Abs(pa.z - pb.z) < 0.01f && Mathf.Abs(pa.x - pb.x) > 0.01f;
                bool alongZ = Mathf.Abs(pa.x - pb.x) < 0.01f && Mathf.Abs(pa.z - pb.z) > 0.01f;
                if (!alongX && !alongZ) continue;
                float line = alongX ? pa.z : pa.x;
                string gk = (alongX ? "z" : "x") + Mathf.RoundToInt(line / Tune.WeldXZ).ToString(CultureInfo.InvariantCulture);
                float u0 = alongX ? pa.x : pa.z, u1 = alongX ? pb.x : pb.z;
                float y0 = pa.y, y1 = pb.y;
                if (u0 > u1) { (u0, u1) = (u1, u0); (y0, y1) = (y1, y0); }
                float side = (alongX ? Cen[t].z : Cen[t].x) - line;
                if (!groups.TryGetValue(gk, out var g)) groups[gk] = g = new List<(int, float, float, float, float, float)>();
                g.Add((t, u0, u1, y0, y1, side));
            }
            foreach (var kv in groups)
            {
                var g = kv.Value;
                bool alongX = kv.Key[0] == 'z';
                float line = int.Parse(kv.Key.Substring(1), CultureInfo.InvariantCulture) * Tune.WeldXZ;
                for (int i = 0; i < g.Count; i++)
                for (int j = i + 1; j < g.Count; j++)
                {
                    var e1 = g[i]; var e2 = g[j];
                    if (e1.tri == e2.tri || Mathf.Sign(e1.side) == Mathf.Sign(e2.side)) continue;
                    float lo = Mathf.Max(e1.lo, e2.lo), hi = Mathf.Min(e1.hi, e2.hi);
                    if (hi - lo < Tune.TJunctionMinOverlap) continue;
                    float mid = (lo + hi) * 0.5f;
                    float y1 = Mathf.Lerp(e1.ylo, e1.yhi, (mid - e1.lo) / Mathf.Max(1e-4f, e1.hi - e1.lo));
                    float y2 = Mathf.Lerp(e2.ylo, e2.yhi, (mid - e2.lo) / Mathf.Max(1e-4f, e2.hi - e2.lo));
                    if (Mathf.Abs(y1 - y2) > Tune.WeldY) continue;
                    Vector3 P(float u, float y) => alongX ? new Vector3(u, y, line) : new Vector3(line, y, u);
                    Link(e1.tri, e2.tri, P(lo, (y1 + y2) * 0.5f), P(hi, (y1 + y2) * 0.5f));
                }
            }
        }

        void Link(int t, int u, Vector3 p0, Vector3 p1)
        {
            Adj[t].Add(new Adjacent { Tri = u, P0 = p0, P1 = p1 });
            Adj[u].Add(new Adjacent { Tri = t, P0 = p0, P1 = p1 });
        }

        void BuildComponents()
        {
            Comp = new int[T];
            for (int t = 0; t < T; t++) Comp[t] = -1;
            var stack = new Stack<int>();
            CompCount = 0;
            for (int s = 0; s < T; s++)
            {
                if (Comp[s] >= 0) continue;
                Comp[s] = CompCount;
                stack.Push(s);
                while (stack.Count > 0)
                {
                    int t = stack.Pop();
                    foreach (var a in Adj[t])
                        if (Comp[a.Tri] < 0) { Comp[a.Tri] = CompCount; stack.Push(a.Tri); }
                }
                CompCount++;
            }
            var floorArea = new float[CompCount];
            for (int t = 0; t < T; t++)
            {
                int n = Node[t];
                if (n < 0 || n >= Map.Rooms.Count) continue;
                float y = Cen[t].y - Map.Rooms[n].FloorY;
                if (y >= Tune.FloorBandLow && y <= Tune.FloorBandHigh) floorArea[Comp[t]] += Area[t];
            }
            LevelComp = new bool[CompCount];
            for (int k = 0; k < CompCount; k++) LevelComp[k] = floorArea[k] >= Tune.LevelComponentMinFloorArea;
        }

        static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        void BuildGaps()
        {
            // 1. outlines that meet: adjacent level triangles in two different nodes
            var contacts = new Dictionary<long, List<(Vector3 a, Vector3 b)>>();
            for (int t = 0; t < T; t++)
            {
                if (!IsLevel(t) || Node[t] < 0) continue;
                foreach (var a in Adj[t])
                {
                    if (a.Tri < t || !IsLevel(a.Tri) || Node[a.Tri] < 0 || Node[a.Tri] == Node[t]) continue;
                    var k = PairKey(Node[t], Node[a.Tri]);
                    if (!contacts.TryGetValue(k, out var l)) contacts[k] = l = new List<(Vector3, Vector3)>();
                    l.Add((a.P0, a.P1));
                }
            }
            foreach (var kv in contacts)
            {
                int na = (int)(kv.Key >> 32), nb = (int)(uint)(kv.Key & 0xffffffff);
                foreach (var cluster in ClusterSegments(kv.Value))
                {
                    var g = new Gap { A = na, B = nb, Via = "edge" };
                    var sum = Vector3.zero; float w = 0;
                    foreach (var s in cluster)
                    {
                        float len = Vector3.Distance(s.a, s.b);
                        sum += (s.a + s.b) * 0.5f * len; w += len;
                        g.Points.Add(s.a); g.Points.Add(s.b); g.Points.Add((s.a + s.b) * 0.5f);
                    }
                    g.Centre = w > 0 ? sum / w : g.Points[0];
                    g.Width = w;
                    Gaps.Add(g);
                }
            }

            // 2. unassigned level NavMesh: a doorway (two rooms), a passage (three or more) or a pocket (one)
            var seen = new bool[T];
            var st = new Stack<int>();
            for (int s = 0; s < T; s++)
            {
                if (seen[s] || !IsLevel(s) || Node[s] >= 0) continue;
                var tris = new List<int>();
                seen[s] = true; st.Push(s);
                var touched = new Dictionary<int, List<(Vector3 a, Vector3 b)>>();
                while (st.Count > 0)
                {
                    int t = st.Pop();
                    tris.Add(t);
                    foreach (var a in Adj[t])
                    {
                        if (!IsLevel(a.Tri)) continue;
                        int n = Node[a.Tri];
                        if (n >= 0)
                        {
                            if (!touched.TryGetValue(n, out var l)) touched[n] = l = new List<(Vector3, Vector3)>();
                            l.Add((a.P0, a.P1));
                        }
                        else if (!seen[a.Tri]) { seen[a.Tri] = true; st.Push(a.Tri); }
                    }
                }
                float area = 0; var c = Vector3.zero;
                foreach (var t in tris) { area += Area[t]; c += Cen[t] * Area[t]; }
                c = area > 0 ? c / area : Cen[tris[0]];
                if (touched.Count == 0) continue;
                if (touched.Count == 1)
                {
                    Pockets.Add(new Pocket { Node = touched.Keys.First(), Centre = c, Area = area });
                    continue;
                }
                if (touched.Count == 2)
                {
                    var keys = touched.Keys.OrderBy(k => k).ToList();
                    var g = new Gap { A = keys[0], B = keys[1], Via = "doorway", Centre = c, Area = area };
                    g.Width = Mathf.Min(touched[keys[0]].Sum(x => Vector3.Distance(x.a, x.b)), touched[keys[1]].Sum(x => Vector3.Distance(x.a, x.b)));
                    foreach (var t in tris.Take(400)) g.Points.Add(Cen[t]);
                    foreach (var l in touched.Values) foreach (var x in l) g.Points.Add((x.a + x.b) * 0.5f);
                    Gaps.Add(g);
                    continue;
                }
                Passages++;
                int pn = Map.GalleryNode + Passages;
                foreach (var kv in touched)
                {
                    var g = new Gap { A = kv.Key, B = pn, Via = "passage", Area = area };
                    var sum = Vector3.zero; float w = 0;
                    foreach (var x in kv.Value)
                    {
                        float len = Vector3.Distance(x.a, x.b);
                        sum += (x.a + x.b) * 0.5f * len; w += len;
                        g.Points.Add((x.a + x.b) * 0.5f);
                    }
                    g.Centre = w > 0 ? sum / w : c;
                    g.Width = w;
                    Gaps.Add(g);
                }
            }

            // 3. the scene's NavMeshLinks
            foreach (var l in Links)
            {
                int na = Map.NodeAt(l.Start), nb = Map.NodeAt(l.End);
                if (na < 0 || nb < 0 || na == nb) continue;
                var g = new Gap { A = Math.Min(na, nb), B = Math.Max(na, nb), Via = "link", Centre = (l.Start + l.End) * 0.5f, Width = l.Width };
                g.Points.Add(l.Start); g.Points.Add(l.End);
                Gaps.Add(g);
            }
        }

        /// <summary>Single-linkage clusters of contact segments: two are one crossing when they come within GapJoin.</summary>
        static List<List<(Vector3 a, Vector3 b)>> ClusterSegments(List<(Vector3 a, Vector3 b)> segs)
        {
            int n = segs.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            var samples = segs.Select(s =>
            {
                float len = Vector3.Distance(s.a, s.b);
                int k = Mathf.Clamp(Mathf.CeilToInt(len / 0.25f), 1, 40);
                var l = new List<Vector3>();
                for (int i = 0; i <= k; i++) l.Add(Vector3.Lerp(s.a, s.b, i / (float)k));
                return l;
            }).ToList();
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (Find(i) == Find(j)) continue;
                bool near = false;
                foreach (var p in samples[i])
                {
                    foreach (var q in samples[j]) if ((p - q).sqrMagnitude <= Tune.GapJoin * Tune.GapJoin) { near = true; break; }
                    if (near) break;
                }
                if (near) parent[Find(i)] = Find(j);
            }
            return Enumerable.Range(0, n).GroupBy(Find).Select(g => g.Select(i => segs[i]).ToList()).ToList();
        }

        // ---------- queries (v1)

        public int FindTri(Vector3 p, float maxDist = 1.5f, bool levelOnly = true)
        {
            if (_triHash == null)
            {
                _triHash = new Dictionary<long, List<int>>();
                for (int t = 0; t < T; t++)
                {
                    var a = V[I[3 * t]]; var b = V[I[3 * t + 1]]; var c = V[I[3 * t + 2]];
                    int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / 4f), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / 4f);
                    int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / 4f), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / 4f);
                    for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        var k = HKey(x, z);
                        if (!_triHash.TryGetValue(k, out var l)) _triHash[k] = l = new List<int>();
                        l.Add(t);
                    }
                }
            }
            int best = -1; float bestD = float.MaxValue, bestRaw = float.MaxValue;
            int kx = Mathf.FloorToInt(p.x / 4f), kz = Mathf.FloorToInt(p.z / 4f);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_triHash.TryGetValue(HKey(kx + dx, kz + dz), out var l)) continue;
                foreach (var t in l)
                {
                    if (levelOnly && !IsLevel(t)) continue;
                    var a = V[I[3 * t]]; var b = V[I[3 * t + 1]]; var c = V[I[3 * t + 2]];
                    float d, raw;
                    if (InTriXZ(p, a, b, c)) { raw = Mathf.Abs(PlaneY(p, a, b, c) - p.y); d = Mathf.Max(0f, raw - Tune.TriHeightSlack); }
                    else raw = d = Vector3.Distance(p, ClosestOnTri(p, a, b, c));
                    if (d < bestD || (d == bestD && raw < bestRaw)) { bestD = d; bestRaw = raw; best = t; }
                }
            }
            return bestD <= maxDist ? best : -1;
        }

        static bool InTriXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Cross(Vector3 p, Vector3 a, Vector3 b) => (a.x - p.x) * (b.z - p.z) - (b.x - p.x) * (a.z - p.z);

        static float PlaneY(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Mathf.Abs(n.y) < 1e-6f) return a.y;
            return a.y - (n.x * (p.x - a.x) + n.z * (p.z - a.z)) / n.y;
        }

        static Vector3 ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var best = ClosestOnSeg(p, a, b);
            var q = ClosestOnSeg(p, b, c);
            if ((q - p).sqrMagnitude < (best - p).sqrMagnitude) best = q;
            q = ClosestOnSeg(p, c, a);
            if ((q - p).sqrMagnitude < (best - p).sqrMagnitude) best = q;
            return best;
        }

        static Vector3 ClosestOnSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return a + ab * t;
        }
    }

    /// <summary>Owns the run-time bake (v1): the scene's own NavMesh data is unloaded first, ours is added, and nothing
    /// is ever written to the scene or the project. Only the latest bake is loaded for path queries.</summary>
    public static class NavBake
    {
        static NavMeshDataInstance s_data;
        static readonly List<NavMeshLinkInstance> s_links = new List<NavMeshLinkInstance>();
        static NavMeshData s_dataObj;

        public static NavMeshBuildSettings Settings()
        {
            var s = NavMesh.GetSettingsByID(0);
            if (s.agentTypeID == -1) s = NavMesh.GetSettingsByIndex(0);
            s.agentRadius = Tune.AgentRadius;
            s.agentHeight = Tune.AgentHeight;
            s.agentClimb = Tune.AgentClimb;
            s.agentSlope = Tune.AgentSlope;
            s.minRegionArea = Tune.MinRegionArea;
            s.overrideVoxelSize = true;
            s.voxelSize = Tune.VoxelSize;
            s.ledgeDropHeight = 0f;
            s.maxJumpAcrossDistance = 0f;
            s.buildHeightMesh = true;
            return s;
        }

        public static Bounds LevelBounds(MapSpec map)
        {
            float m = Tune.BakeMargin;
            float y0 = map.FloorMin + Tune.BakeBottom, y1 = map.FloorMax + Tune.BakeTop;
            var min = new Vector3(map.XMin - m, y0, map.ZMin - m);
            var max = new Vector3(map.XMax + m, y1, map.ZMax + m);
            return new Bounds((min + max) * 0.5f, max - min);
        }

        public static void Clear()
        {
            if (s_data.valid) s_data.Remove();
            s_data = default;
            foreach (var l in s_links) NavMesh.RemoveLink(l);
            s_links.Clear();
            if (s_dataObj != null) UnityEngine.Object.DestroyImmediate(s_dataObj);
            s_dataObj = null;
        }

        public static NavModel Bake(MapSpec map, Scene scene, List<SceneLink> links, string label, List<NavMeshBuildSource> modifiers)
        {
            Clear();
            NavMesh.RemoveAllNavMeshData();
            Physics.SyncTransforms();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var settings = Settings();
            var bounds = LevelBounds(map);
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                false, markups, false, scene, sources);
#pragma warning restore CS0618
            int collected = sources.Count;
            if (modifiers != null) sources.AddRange(modifiers);
            s_dataObj = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (s_dataObj == null) throw new Exception("NavMeshBuilder.BuildNavMeshData returned null");
            s_dataObj.hideFlags = HideFlags.HideAndDontSave;
            s_data = NavMesh.AddNavMeshData(s_dataObj);
            foreach (var l in links)
            {
                var inst = NavMesh.AddLink(new NavMeshLinkData
                {
                    startPosition = l.Start, endPosition = l.End, width = l.Width, costModifier = -1,
                    bidirectional = l.Bidirectional, area = l.Area, agentTypeID = settings.agentTypeID
                });
                if (NavMesh.IsLinkValid(inst)) s_links.Add(inst);
            }
            var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            var model = NavModel.FromTriangulation(map, filter, links);
            model.BakeSeconds = (float)sw.Elapsed.TotalSeconds;
            model.SourceCount = collected;
            model.ModifierCount = modifiers?.Count ?? 0;
            model.Label = label;
            return model;
        }

        /// <summary>A query filter over every area but those listed.</summary>
        public static NavMeshQueryFilter FilterWithout(NavModel m, params int[] areas)
        {
            int mask = NavMesh.AllAreas;
            foreach (var a in areas) mask &= ~(1 << a);
            return new NavMeshQueryFilter { agentTypeID = m.Filter.agentTypeID, areaMask = mask };
        }

        /// <summary>Modifier boxes, 0.5 m strips in x, over the part of [x0,x1] x [z0,z1] where <paramref name="inside"/>
        /// holds, from y0 to y1, that mark the NavMesh there with <paramref name="area"/>.</summary>
        public static int AddStrips(List<NavMeshBuildSource> list, Func<float, float, bool> inside, float x0, float x1,
            float z0, float z1, float y0, float y1, int area)
        {
            const float sx = 0.5f, sz = 0.25f;
            int n = 0;
            for (float x = x0; x < x1; x += sx)
            {
                float cx = x + sx * 0.5f;
                float run = float.NaN;
                for (float z = z0; ; z += sz)
                {
                    bool end = z >= z1;
                    bool ins = !end && inside(cx, z + sz * 0.5f);
                    if (ins && float.IsNaN(run)) run = z;
                    else if (!ins && !float.IsNaN(run))
                    {
                        var centre = new Vector3(cx, (y0 + y1) * 0.5f, (run + z) * 0.5f);
                        list.Add(new NavMeshBuildSource
                        {
                            shape = NavMeshBuildSourceShape.ModifierBox,
                            transform = Matrix4x4.TRS(centre, Quaternion.identity, Vector3.one),
                            size = new Vector3(sx, y1 - y0, z - run),
                            area = area
                        });
                        n++;
                        run = float.NaN;
                    }
                    if (end) break;
                }
            }
            return n;
        }

        /// <summary>Reads every enabled NavMeshLink in the scene into world space. (v1)</summary>
        public static List<SceneLink> ReadLinks(Scene scene)
        {
            var list = new List<SceneLink>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var l in root.GetComponentsInChildren<NavMeshLink>(false))
            {
                if (!l.isActiveAndEnabled || !l.activated) continue;
                var toWorld = Matrix4x4.TRS(l.transform.position, l.transform.rotation, Vector3.one);
                var s = l.startTransform != null ? l.startTransform.position : toWorld.MultiplyPoint3x4(l.startPoint);
                var e = l.endTransform != null ? l.endTransform.position : toWorld.MultiplyPoint3x4(l.endPoint);
                list.Add(new SceneLink
                {
                    Name = l.gameObject.name, Start = s, End = e, Width = l.width,
                    Bidirectional = l.bidirectional, Area = l.area
                });
            }
            return list;
        }
    }
}
