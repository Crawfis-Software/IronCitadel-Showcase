using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.EditorTools
{
    /// <summary>A wall/floor style: 5 m Synty pieces whose front is local +z and which run toward local -x from the pivot.</summary>
    public class Style
    {
        public string[] Walls;
        public float[] WallWeights;
        public float Depth;              // pivot sits this far behind the wall face
        public string[] Floors;
        public float FloorThick;
        public string Ceiling = "D:SM_Env_Ceiling_Stone_Flat_01";
        public string Frame = "D:SM_Env_Door_Frame_Round_02";
        public string Pillar;            // free-standing pillar for wall ends and corners
        public float PillarHeight = 5f;

        public string PickWall()
        {
            if (WallWeights == null) return Walls[Kit.RI(0, Walls.Length)];
            float t = Kit.R(0, WallWeights.Sum());
            for (int i = 0; i < Walls.Length; i++) { t -= WallWeights[i]; if (t <= 0) return Walls[i]; }
            return Walls[0];
        }

        public static readonly Style Dungeon = new Style
        {
            Walls = new[] { "D:SM_Env_Wall_01", "D:SM_Env_Wall_03", "D:SM_Env_Wall_04", "D:SM_Env_Wall_05" },
            WallWeights = new[] { 6f, 1f, 1f, 1f },
            Depth = 0.22f,
            Floors = new[] { "D:SM_Env_Tiles_01", "D:SM_Env_Tiles_02", "D:SM_Env_Tiles_03", "D:SM_Env_Tiles_04" },
            FloorThick = 0.12f,
            Pillar = "D:SM_Env_Pillar_Square_06",
        };

        public static readonly Style Dwarf = new Style
        {
            Walls = new[] { "R:SM_Env_Dwarf_Wall_01", "R:SM_Env_Dwarf_Wall_02", "R:SM_Env_Dwarf_Wall_05", "R:SM_Env_Dwarf_Wall_06" },
            WallWeights = new[] { 4f, 2f, 1f, 1f },
            Depth = 0.3f,
            Floors = new[] { "R:SM_Env_Dwarf_Floor_01", "R:SM_Env_Dwarf_Floor_02", "R:SM_Env_Dwarf_Floor_05" },
            FloorThick = 0.25f,
            Pillar = "R:SM_Env_Dwarf_Pillar_01",
        };
    }

    public struct Opening
    {
        public float At, Width, Height;
        public Opening(float at, float width, float height) { At = at; Width = width; Height = height; }
    }

    public class Room
    {
        public string Id, Name;
        public int Row, Col;
        public float H;
        public Style St;
        public uint Mask;
        public Transform Root, Arch, Props, Lights, Ceil, Cols, People;
        public readonly List<Rect> Open = new List<Rect>();

        public float X0 => Col * Arch_.Tile;
        public float Z0 => -(Row + 1) * Arch_.Tile;
        public Vector3 W(float u, float v, float y = 0f) => new Vector3(X0 + u, y, Z0 + v);
        public Vector2 L(Vector3 w) => new Vector2(w.x - X0, w.z - Z0);
    }

    public class Door
    {
        public string Id;
        public Vector3 Centre;      // on the tile edge, floor level
        public Vector3 Axis;        // unit, from room A toward room B
        public float Width = 3f, Height = 3f, HalfLength = 2.5f;
        public Room A, B;           // B may be null (outside)
        public string Frame = "D:SM_Env_Door_Frame_Round_02";
        public float FrameScale = 1.15f;
        public bool FrameA = true, FrameB = true;
        public bool Secret;
        public Transform Root;
        public Vector3 FaceA => Centre - Axis * HalfLength;
        public Vector3 FaceB => Centre + Axis * HalfLength;
    }

    /// <summary>Architecture builders. Distances are metres; room-local u runs east, v north, from the tile's SW corner.</summary>
    public static class Arch_
    {
        public const float Tile = 45f, Inset = 2.5f;
        public static readonly List<Door> Doors = new List<Door>();

        static Vector3 V3(Vector2 v) => new Vector3(v.x, 0, v.y);

        /// <summary>Openings for doors whose face lies on the wall line a→b of this room.</summary>
        public static List<Opening> DoorOpenings(Room rm, Vector2 a, Vector2 b)
        {
            var list = new List<Opening>();
            Vector3 wa = rm.W(a.x, a.y), wb = rm.W(b.x, b.y);
            Vector3 dir = (wb - wa).normalized;
            float len = (wb - wa).magnitude;
            foreach (var d in Doors)
            {
                foreach (var face in new[] { (d.FaceA, d.A), (d.FaceB, d.B) })
                {
                    if (face.Item2 != rm) continue;
                    Vector3 rel = face.Item1 - wa; rel.y = 0;
                    float t = Vector3.Dot(rel, dir);
                    float off = (rel - dir * t).magnitude;
                    if (off < 0.2f && t > 0 && t < len) list.Add(new Opening(t, d.Width, d.Height));
                }
            }
            return list;
        }

        /// <summary>
        /// A wall from a to b (room-local), its face on the line, facing 'inward'. Door openings on the line are cut
        /// automatically; extra openings may be passed. Visual pieces have no colliders; a clean box collider sits behind.
        /// </summary>
        public static void WallRun(Room rm, Vector2 a, Vector2 b, Vector2 inward, float y0, float h, Style st = null,
            IEnumerable<Opening> extra = null, bool collide = true, Transform parent = null, bool autoDoors = true)
        {
            st = st ?? rm.St;
            parent = parent ?? rm.Arch;
            var ops = new List<Opening>();
            if (autoDoors) ops.AddRange(DoorOpenings(rm, a, b));
            if (extra != null) ops.AddRange(extra);
            ops = ops.OrderBy(o => o.At).ToList();
            Vector2 dir = (b - a).normalized;
            float L = (b - a).magnitude;
            float cur = 0f;
            foreach (var o in ops)
            {
                float s0 = Mathf.Max(cur, o.At - o.Width / 2f), s1 = Mathf.Min(L, o.At + o.Width / 2f);
                Segment(rm, parent, a + dir * cur, a + dir * s0, inward, y0, h, st, collide);
                if (o.Height < h - 0.05f)
                    Segment(rm, parent, a + dir * s0, a + dir * s1, inward, y0 + o.Height, h - o.Height, st, collide, lintel: true);
                cur = s1;
            }
            Segment(rm, parent, a + dir * cur, b, inward, y0, h, st, collide);
        }

        static void Segment(Room rm, Transform parent, Vector2 p0, Vector2 p1, Vector2 inward, float y0, float h, Style st, bool collide, bool lintel = false)
        {
            float len = (p1 - p0).magnitude;
            if (len < 0.05f || h < 0.05f) return;
            Pieces(rm, parent, p0, p1, inward, y0, h, st);
            if (collide)
            {
                Vector3 n = V3(inward).normalized;
                Vector3 mid = (rm.W(p0.x, p0.y) + rm.W(p1.x, p1.y)) / 2f;
                Vector3 along = (rm.W(p1.x, p1.y) - rm.W(p0.x, p0.y)).normalized;
                var go = Kit.Solid(rm.Cols, mid - n * 0.5f + Vector3.up * (y0 + h / 2f), Vector3.one, lintel ? "Lintel" : "Wall");
                go.transform.rotation = Quaternion.LookRotation(n, Vector3.up);
                go.GetComponent<BoxCollider>().size = new Vector3(len, h, 1f);
            }
        }

        /// <summary>Visual wall pieces only, filling the segment exactly (pieces stretched to fit).</summary>
        public static void Pieces(Room rm, Transform parent, Vector2 p0, Vector2 p1, Vector2 inward, float y0, float h, Style st, string forceKey = null)
        {
            float len = (p1 - p0).magnitude;
            if (len < 0.05f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 5f));
            float pw = len / n;
            int rows = Mathf.Max(1, Mathf.RoundToInt(h / 5f));
            float ph = h / rows;
            Vector3 nrm = V3(inward).normalized;
            var rot = Quaternion.LookRotation(nrm, Vector3.up);
            Vector3 right = rot * Vector3.right;
            Vector3 w0 = rm.W(p0.x, p0.y), w1 = rm.W(p1.x, p1.y);
            Vector3 start = Vector3.Dot(w0, right) > Vector3.Dot(w1, right) ? w0 : w1;
            for (int i = 0; i < n; i++)
                for (int k = 0; k < rows; k++)
                {
                    var pos = start - right * (pw * i) - nrm * st.Depth;
                    pos.y = y0 + k * ph;
                    var go = Kit.P(forceKey ?? st.PickWall(), parent, pos, rot, new Vector3(pw / 5f, ph / 5f, 1f));
                    Kit.StripColliders(go);
                }
        }

        /// <summary>A free-standing wall of given thickness centred on a→b, faced on both sides, with optional openings.</summary>
        public static void InnerWall(Room rm, Vector2 a, Vector2 b, float thick, float y0, float h, Style st = null,
            IEnumerable<Opening> openings = null, bool endPillars = false, Transform parent = null)
        {
            st = st ?? rm.St;
            parent = parent ?? rm.Arch;
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x);
            var ops = openings?.ToList() ?? new List<Opening>();
            WallRun(rm, a + n * thick / 2f, b + n * thick / 2f, n, y0, h, st, ops, false, parent, false);
            WallRun(rm, a - n * thick / 2f, b - n * thick / 2f, -n, y0, h, st, ops, false, parent, false);
            // colliders: solid between openings, lintels above them
            float L = (b - a).magnitude, cur = 0f;
            void Box(float s0, float s1, float yb, float hh)
            {
                if (s1 - s0 < 0.05f || hh < 0.05f) return;
                Vector2 m = a + dir * ((s0 + s1) / 2f);
                var go = Kit.Solid(rm.Cols, rm.W(m.x, m.y, yb + hh / 2f), Vector3.one, "InnerWall");
                go.transform.rotation = Quaternion.LookRotation(V3(n), Vector3.up);
                go.GetComponent<BoxCollider>().size = new Vector3(s1 - s0, hh, thick + 0.1f);
            }
            foreach (var o in ops.OrderBy(o => o.At))
            {
                Box(cur, o.At - o.Width / 2f, y0, h);
                Box(o.At - o.Width / 2f, o.At + o.Width / 2f, y0 + o.Height, h - o.Height);
                cur = o.At + o.Width / 2f;
            }
            Box(cur, L, y0, h);
            if (endPillars && st.Pillar != null)
            {
                foreach (var e in new[] { a, b })
                {
                    float sc = h / st.PillarHeight;
                    var p = Kit.PC(st.Pillar, parent, rm.W(e.x, e.y, y0), 0f, 1f);
                    p.transform.localScale = new Vector3(1.1f, sc, 1.1f);
                    Kit.StripColliders(p);
                }
            }
        }

        /// <summary>The four outer walls of a rectangular room, door openings cut automatically.</summary>
        public static void Box4(Room rm, Rect r, float h, Style st = null, float y0 = 0f)
        {
            st = st ?? rm.St;
            Vector2 sw = new Vector2(r.xMin, r.yMin), se = new Vector2(r.xMax, r.yMin), nw = new Vector2(r.xMin, r.yMax), ne = new Vector2(r.xMax, r.yMax);
            WallRun(rm, sw, se, Vector2.up, y0, h, st);
            WallRun(rm, nw, ne, Vector2.down, y0, h, st);
            WallRun(rm, sw, nw, Vector2.right, y0, h, st);
            WallRun(rm, se, ne, Vector2.left, y0, h, st);
        }

        public static void Floor(Room rm, Rect r, float y = 0f, Style st = null, string[] keys = null, Transform parent = null, bool collide = true, bool randomTurn = true)
        {
            st = st ?? rm.St;
            keys = keys ?? st.Floors;
            parent = parent ?? rm.Arch;
            int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / 5f)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / 5f));
            float sx = r.width / nx, sz = r.height / nz;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    string key = keys[Kit.RI(0, keys.Length)];
                    float yaw = randomTurn ? 90f * Kit.RI(0, 4) : 0f;
                    var go = Kit.Fit(key, parent, rm.W(r.xMin + i * sx, r.yMin + j * sz, y - st.FloorThick), new Vector3(sx, st.FloorThick, sz), yaw);
                    Kit.StripColliders(go);
                }
            if (collide)
                Kit.SolidMinMax(rm.Cols, rm.W(r.xMin, r.yMin, y - 0.6f), rm.W(r.xMax, r.yMax, y), "Floor");
        }

        public static void Ceiling(Room rm, Rect r, float y, string key = null, Transform parent = null)
        {
            key = key ?? rm.St.Ceiling;
            parent = parent ?? rm.Ceil;
            int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / 5f)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / 5f));
            float sx = r.width / nx, sz = r.height / nz;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var go = Kit.Fit(key, parent, rm.W(r.xMin + i * sx, r.yMin + j * sz, y), new Vector3(sx, 0.15f, sz), 90f * Kit.RI(0, 4));
                    Kit.StripColliders(go);
                    Kit.SetLayer(go, Kit.CeilingLayer);
                }
        }

        /// <summary>The tunnel of a door through the 5 m of rock between two rooms, with frames on both faces.</summary>
        public static void Passage(Door d, Transform parent)
        {
            var root = Kit.Group("Door_" + d.Id, parent);
            d.Root = root;
            var host = d.A;
            var st = host.St;
            Vector3 ax = d.Axis;
            Vector3 side = Vector3.Cross(Vector3.up, ax).normalized;
            float hw = d.Width / 2f;
            Vector3 a = d.FaceA, b = d.FaceB;
            if (d.B == null) b = d.Centre; // outer door: tunnel ends at the tile edge
            float len = (b - a).magnitude;
            Vector3 mid = (a + b) / 2f;

            // floor
            var cols = Kit.Group("Colliders", root);
            {
                var min = Vector3.Min(a - side * hw, b + side * hw); var max = Vector3.Max(a - side * hw, b + side * hw);
                var go = Kit.Fit(st.Floors[0], root, new Vector3(min.x, -st.FloorThick, min.z), new Vector3(max.x - min.x, st.FloorThick, max.z - min.z), 0f);
                Kit.StripColliders(go);
                Kit.SolidMinMax(cols, new Vector3(min.x, -0.6f, min.z), new Vector3(max.x, 0f, max.z), "Floor");
            }
            // side walls (visual pieces facing into the tunnel) and their colliders
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 n = -side * s;                       // inward normal for this side wall
                Vector3 p0 = a + side * s * hw, p1 = b + side * s * hw;
                var rot = Quaternion.LookRotation(n, Vector3.up);
                Vector3 right = rot * Vector3.right;
                Vector3 start = Vector3.Dot(p0, right) > Vector3.Dot(p1, right) ? p0 : p1;
                int rows = Mathf.Max(1, Mathf.RoundToInt(d.Height / 5f));
                float ph = d.Height / rows;
                for (int k = 0; k < rows; k++)
                {
                    var pos = start - n * st.Depth; pos.y = k * ph;
                    var go = Kit.P(st.PickWall(), root, pos, rot, new Vector3(len / 5f, ph / 5f, 1f));
                    Kit.StripColliders(go);
                }
                var c = Kit.Solid(cols, (p0 + p1) / 2f - n * 0.5f + Vector3.up * d.Height / 2f, Vector3.one, "TunnelWall");
                c.transform.rotation = rot;
                c.GetComponent<BoxCollider>().size = new Vector3(len + 1f, d.Height + 0.5f, 1f);
            }
            // ceiling
            {
                var min = Vector3.Min(a - side * hw, b + side * hw); var max = Vector3.Max(a - side * hw, b + side * hw);
                var go = Kit.Fit(st.Ceiling, root, new Vector3(min.x, d.Height, min.z), new Vector3(max.x - min.x, 0.15f, max.z - min.z), 0f);
                Kit.StripColliders(go);
                Kit.SetLayer(go, Kit.CeilingLayer);
                Kit.SolidMinMax(cols, new Vector3(min.x, d.Height, min.z), new Vector3(max.x, d.Height + 0.5f, max.z), "TunnelRoof");
            }
            // frames on the room faces
            if (d.Frame != null)
            {
                if (d.FrameA) Kit.StripColliders(Kit.P(d.Frame, root, a, Quaternion.LookRotation(-ax, Vector3.up), Vector3.one * d.FrameScale));
                if (d.FrameB && d.B != null) Kit.StripColliders(Kit.P(d.Frame, root, b, Quaternion.LookRotation(ax, Vector3.up), Vector3.one * d.FrameScale));
            }
        }

        /// <summary>Wall-mounted torch with flame and light, on the wall face at 'wallPoint', facing 'inward'.</summary>
        public static void Torch(Room rm, Vector3 wallPoint, Vector3 inward, float y = 2.6f, float intensity = 8f, float range = 10f, string key = "D:SM_Prop_Torch_Ornate_01")
        {
            var p = wallPoint; p.y = y;
            var go = Kit.PW(key, rm.Props, p, inward, 1f, 0.0f);
            go.name = "Torch";
            Kit.StripColliders(go);
            var b = Kit.LocalBounds(key);
            Vector3 top = go.transform.TransformPoint(new Vector3(b.center.x, b.max.y, b.max.z * 0.6f + b.center.z * 0.4f));
            var fx = Kit.P("D:FX_Fire_01", go.transform, top, 0f, 0.2f);
            fx.name = "Flame";
            Kit.PointLight(rm.Lights, top + inward * 0.35f + Vector3.up * 0.1f, Kit.Fire, intensity, range, "TorchLight");
        }

        public static void SetMasks(Transform root, uint mask)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = mask;
            foreach (var l in root.GetComponentsInChildren<Light>(true))
            {
                l.renderingLayerMask = (int)mask;
                var ad = l.GetUniversalAdditionalLightData();
                if (ad != null) ad.renderingLayers = mask;
            }
        }
    }
}
