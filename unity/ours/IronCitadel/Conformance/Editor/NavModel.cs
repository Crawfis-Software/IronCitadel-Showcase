// IronCitadel conformance kit: the run-time NavMesh bake and the opening graph built from it.
using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace IronCitadel.Conformance
{
    /// <summary>One NavMeshLink found in the scene, in world space.</summary>
    public sealed class SceneLink
    {
        public string Name;
        public Vector3 Start, End;
        public float Width;
        public bool Bidirectional;
        public int Area;
    }

    /// <summary>A walkable connection between two graph nodes (cells, or "outside").</summary>
    public sealed class Opening
    {
        public Cell A, B;
        public string Kind;              // edge | diagonal | far
        public bool ViaLink;
        public float Length;             // total shared NavMesh edge length across the boundary (m)
        public Vector3 Crossing;         // length-weighted midpoint of the shared edges
        public float MinY = float.MaxValue, MaxY = float.MinValue;
        public int Gaps;                 // separate crossings, clustered by distance
        public readonly List<Vector3> Mids = new List<Vector3>();
        public readonly List<float> Lens = new List<float>();
        public readonly List<string> Links = new List<string>();

        public bool Touches(Cell c) => A == c || B == c;
        public Cell Other(Cell c) => A == c ? B : A;
    }

    public struct Adjacent
    {
        public int Tri;
        public Vector3 P0, P1;   // the shared edge (or the overlapping part of two collinear edges)
    }

    public sealed class NavModel
    {
        public static readonly Cell Out = new Cell(-99, -99);

        public LevelSpec Level;
        public NavMeshQueryFilter Filter;
        public float BakeSeconds;
        public int SourceCount;
        public string Label;

        public Vector3[] V;          // welded vertices
        public int[] I;              // 3 per triangle, into V
        public int T;                // triangle count
        public Vector3[] Cen;
        public float[] Area;         // projected (xz) area, m2
        public Cell[] CellOfTri;
        public List<Adjacent>[] Adj;
        public int[] Comp;
        public int CompCount;
        public bool[] LevelComp;     // per component: does it carry real floor-level area in a room cell?
        public float[] CompArea;

        public readonly Dictionary<string, Opening> Openings = new Dictionary<string, Opening>();
        public readonly List<SceneLink> Links = new List<SceneLink>();

        Dictionary<long, List<int>> _triHash;

        public bool IsLevel(int t) => LevelComp[Comp[t]];

        public Cell NodeOf(Vector3 p)
        {
            var c = Level.CellOf(p);
            return Level.InGrid(c) ? c : Out;
        }

        public string NodeName(Cell c) => c == Out ? "outside" : Level.NodeName(c);

        public static string Key(Cell a, Cell b)
        {
            bool swap = a.R > b.R || (a.R == b.R && a.C > b.C);
            return swap ? b + "|" + a : a + "|" + b;
        }

        public Opening Find(Cell a, Cell b) => Openings.TryGetValue(Key(a, b), out var o) ? o : null;

        public List<Opening> OpeningsOf(Cell c)
        {
            var l = new List<Opening>();
            foreach (var o in Openings.Values) if (o.Touches(c)) l.Add(o);
            return l;
        }

        public float TriY(int t) => Cen[t].y;

        // ---------- building ----------

        public static NavModel FromTriangulation(LevelSpec level, NavMeshQueryFilter filter, List<SceneLink> links)
        {
            var m = new NavModel { Level = level, Filter = filter };
            m.Links.AddRange(links);
            var tri = NavMesh.CalculateTriangulation();
            m.Weld(tri.vertices, tri.indices);
            m.BuildAdjacency();
            m.BuildComponents();
            m.BuildOpenings();
            return m;
        }

        void Weld(Vector3[] verts, int[] idx)
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
            for (int t = 0; t + 2 < idx.Length; t += 3)
            {
                int a = remap[idx[t]], b = remap[idx[t + 1]], c = remap[idx[t + 2]];
                if (a == b || b == c || a == c) continue;
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            V = outV.ToArray();
            I = tris.ToArray();
            T = I.Length / 3;
            Cen = new Vector3[T];
            Area = new float[T];
            CellOfTri = new Cell[T];
            for (int t = 0; t < T; t++)
            {
                var p0 = V[I[3 * t]]; var p1 = V[I[3 * t + 1]]; var p2 = V[I[3 * t + 2]];
                Cen[t] = (p0 + p1 + p2) / 3f;
                Area[t] = Mathf.Abs((p1.x - p0.x) * (p2.z - p0.z) - (p2.x - p0.x) * (p1.z - p0.z)) * 0.5f;
                CellOfTri[t] = NodeOf(Cen[t]);
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

            // NavMesh tiles meet at axis-aligned borders where the two sides need not share vertices
            // (a T-junction). Join unmatched, axis-aligned, collinear, overlapping edges whose triangles
            // lie on opposite sides of the line and whose heights agree.
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
            CompArea = new float[CompCount];
            for (int t = 0; t < T; t++)
            {
                CompArea[Comp[t]] += Area[t];
                var c = CellOfTri[t];
                if (c == Out || Level.IsRock(c)) continue;
                float y = Cen[t].y - Level.FloorY;
                if (y >= Tune.FloorBandLow && y <= Tune.FloorBandHigh) floorArea[Comp[t]] += Area[t];
            }
            LevelComp = new bool[CompCount];
            for (int k = 0; k < CompCount; k++) LevelComp[k] = floorArea[k] >= Tune.LevelComponentMinFloorArea;
        }

        void BuildOpenings()
        {
            for (int t = 0; t < T; t++)
            {
                if (!IsLevel(t)) continue;
                foreach (var a in Adj[t])
                {
                    if (a.Tri < t || !IsLevel(a.Tri)) continue;
                    var ca = CellOfTri[t]; var cb = CellOfTri[a.Tri];
                    if (ca == cb) continue;
                    var o = GetOrAdd(ca, cb);
                    float len = Vector3.Distance(a.P0, a.P1);
                    var mid = (a.P0 + a.P1) * 0.5f;
                    o.Length += len;
                    o.Mids.Add(mid);
                    o.Lens.Add(len);
                    o.MinY = Mathf.Min(o.MinY, mid.y);
                    o.MaxY = Mathf.Max(o.MaxY, mid.y);
                }
            }
            foreach (var l in Links)
            {
                var ca = NodeOf(l.Start); var cb = NodeOf(l.End);
                if (ca == cb) continue;
                var o = GetOrAdd(ca, cb);
                o.ViaLink = true;
                o.Links.Add(l.Name);
                var mid = (l.Start + l.End) * 0.5f;
                o.Mids.Add(mid);
                o.Lens.Add(Mathf.Max(0.01f, l.Width));
                o.Length += Mathf.Max(0.01f, l.Width);
                o.MinY = Mathf.Min(o.MinY, mid.y);
                o.MaxY = Mathf.Max(o.MaxY, mid.y);
            }
            foreach (var o in Openings.Values)
            {
                var sum = Vector3.zero; float w = 0;
                for (int i = 0; i < o.Mids.Count; i++) { sum += o.Mids[i] * o.Lens[i]; w += o.Lens[i]; }
                o.Crossing = w > 0 ? sum / w : o.Mids[0];
                // cluster crossings: two mids further apart than GapClusterDist are separate gaps
                var reps = new List<Vector3>();
                foreach (var p in o.Mids)
                {
                    bool near = false;
                    foreach (var r in reps) if ((r - p).magnitude < Tune.GapClusterDist) { near = true; break; }
                    if (!near) reps.Add(p);
                }
                o.Gaps = reps.Count;
                int dr = Math.Abs(o.A.R - o.B.R), dc = Math.Abs(o.A.C - o.B.C);
                if (o.A == Out || o.B == Out)
                {
                    var inside = o.A == Out ? o.B : o.A;
                    bool border = inside.R == 0 || inside.C == 0 || inside.R == Level.Rows - 1 || inside.C == Level.Cols - 1;
                    o.Kind = border ? "edge" : "far";
                }
                else o.Kind = dr + dc == 1 ? "edge" : (dr == 1 && dc == 1 ? "diagonal" : "far");
            }
        }

        Opening GetOrAdd(Cell a, Cell b)
        {
            var k = Key(a, b);
            if (!Openings.TryGetValue(k, out var o))
            {
                bool swap = a.R > b.R || (a.R == b.R && a.C > b.C);
                o = new Opening { A = swap ? b : a, B = swap ? a : b };
                Openings[k] = o;
            }
            return o;
        }

        // ---------- queries ----------

        /// <summary>The level triangle under (or nearest) a world point; -1 when none is close.</summary>
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
            // A triangle under the point in xz may sit up to TriHeightSlack off the real surface (polygon-level
            // heights), so that much vertical error is forgiven; ties go to the triangle nearest in height.
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

        /// <summary>Flood from a triangle, entering only triangles whose cell passes <paramref name="allowed"/>.
        /// Scene NavMeshLinks are followed too, between allowed cells.</summary>
        public bool[] Flood(int start, Func<Cell, bool> allowed)
        {
            var seen = new bool[T];
            if (start < 0) return seen;
            var st = new Stack<int>();
            seen[start] = true;
            st.Push(start);
            List<(int, int)> linkTris = null;
            while (st.Count > 0)
            {
                int t = st.Pop();
                foreach (var a in Adj[t])
                {
                    if (seen[a.Tri] || !allowed(CellOfTri[a.Tri])) continue;
                    seen[a.Tri] = true;
                    st.Push(a.Tri);
                }
                if (Links.Count > 0)
                {
                    if (linkTris == null)
                    {
                        linkTris = new List<(int, int)>();
                        foreach (var l in Links) linkTris.Add((FindTri(l.Start, 2f, false), FindTri(l.End, 2f, false)));
                    }
                    for (int k = 0; k < linkTris.Count; k++)
                    {
                        var (s, e) = linkTris[k];
                        int next = s == t ? e : (e == t && Links[k].Bidirectional ? s : -1);
                        if (next < 0 || seen[next] || !allowed(CellOfTri[next])) continue;
                        seen[next] = true;
                        st.Push(next);
                    }
                }
            }
            return seen;
        }

        public float AreaInCell(Cell c, bool levelOnly, float yLo = float.MinValue, float yHi = float.MaxValue)
        {
            float s = 0;
            for (int t = 0; t < T; t++)
            {
                if (CellOfTri[t] != c) continue;
                if (levelOnly && !IsLevel(t)) continue;
                float y = Cen[t].y - Level.FloorY;
                if (y < yLo || y > yHi) continue;
                s += Area[t];
            }
            return s;
        }
    }

    /// <summary>Owns the run-time bake: the scene's own NavMesh data is unloaded first, ours is added,
    /// and nothing is ever written to the scene or the project.</summary>
    public static class NavBake
    {
        static NavMeshDataInstance s_data;
        static readonly List<NavMeshLinkInstance> s_links = new List<NavMeshLinkInstance>();
        static NavMeshData s_dataObj;

        public static NavMeshBuildSettings Settings()
        {
            var s = NavMesh.GetSettingsByID(0);
            if (s.agentTypeID == -1) s = NavMesh.GetSettingsByIndex(0);
            s.agentRadius = Tune.AgentRadius;          // the walker's own size: see Tune.AgentRadius
            s.agentHeight = Tune.AgentHeight;
            s.agentClimb = Tune.AgentClimb;
            s.agentSlope = Tune.AgentSlope;
            s.minRegionArea = Tune.MinRegionArea;
            s.overrideVoxelSize = true;
            s.voxelSize = Tune.VoxelSize;
            s.ledgeDropHeight = 0f;
            s.maxJumpAcrossDistance = 0f;
            s.buildHeightMesh = true;            // exact heights for SamplePosition (stairs, stage)
            return s;
        }

        public static Bounds LevelBounds(LevelSpec L)
        {
            float w = L.Cols * L.Tile, d = L.Rows * L.Tile, m = Tune.BakeMargin;
            var centre = new Vector3(w * 0.5f, L.FloorY + (Tune.BakeTop + Tune.BakeBottom) * 0.5f, -d * 0.5f);
            return new Bounds(centre, new Vector3(w + 2 * m, Tune.BakeTop - Tune.BakeBottom, d + 2 * m));
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

        public static NavModel Bake(LevelSpec L, Scene scene, List<SceneLink> links, string label)
        {
            Clear();
            NavMesh.RemoveAllNavMeshData();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var settings = Settings();
            var bounds = LevelBounds(L);
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
#pragma warning disable CS0618
            UnityEditor.AI.NavMeshBuilder.CollectSourcesInStage(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                false, markups, false, scene, sources);
#pragma warning restore CS0618
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
            var model = NavModel.FromTriangulation(L, filter, links);
            model.BakeSeconds = (float)sw.Elapsed.TotalSeconds;
            model.SourceCount = sources.Count;
            model.Label = label;
            return model;
        }

        /// <summary>Reads every enabled NavMeshLink in the scene into world space.</summary>
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
