using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        public class Room
        {
            public string id, name, kind, state;
            public float x0, z0, x1, z1, fy, h;
            public Surf wallLo, wallHi, floor, ceil, trim;
            public float dado = 3f;
            public bool fine;          // skirting, cornice and architraves
            public bool cornice = true;
            public int bit;
            public uint RL => 1u << bit;
            public Transform root, structure, props, lights, markers;
            public readonly List<Opening> open = new List<Opening>();
            public float W => x1 - x0;
            public float D => z1 - z0;
            public float cx => (x0 + x1) * 0.5f;
            public float cz => (z0 + z1) * 0.5f;
            public Vector3 P(float x, float y, float z) => new Vector3(x, fy + y, z);
        }

        public class Opening
        {
            public char side;
            public float a, b, y0, y1; // a,b along the wall (world); y relative to the room floor
            public Door door;
        }

        public enum DoorStyle { Doorway, Single, Double, OpenSingle, OpenDouble, Bookcase }

        public class Door
        {
            public Room A, B;
            public float c, w, hgt, bottom;
            public string state;
            public DoorStyle style;
            public int hinge = -1;        // -1: hinge at the low jamb, +1: at the high jamb (single leaves)
            public bool intoB = true;     // leaves swing into B
            public string leaf = "SM_Bld_Door_05";
            public float openAngle = 100f;
            // derived
            public bool alongX;           // wall runs along x
            public Vector3 n;             // unit normal from A to B
            public float g0, g1;          // gap along the normal axis (low..high world coordinate)
            public char sideA, sideB;
            public Transform root;
            public SwingDoor swing;
            public DoorwayInfo info;
            public float a => c - w * 0.5f;
            public float b => c + w * 0.5f;
            public Vector3 Centre => alongX ? new Vector3(c, A.fy + bottom, (g0 + g1) * 0.5f) : new Vector3((g0 + g1) * 0.5f, A.fy + bottom, c);
        }

        static readonly List<Room> s_Rooms = new List<Room>();
        static readonly List<Door> s_Doors = new List<Door>();
        static Transform s_Level;

        static Room R(string id, string name, string kind, string state, float x0, float z0, float x1, float z1, float h,
            Surf wallLo, Surf wallHi, Surf floor, Surf ceil, bool fine, Surf trim = null)
        {
            var r = new Room
            {
                id = id, name = name, kind = kind, state = state, x0 = x0, z0 = z0, x1 = x1, z1 = z1, fy = 0f, h = h,
                wallLo = wallLo, wallHi = wallHi ?? wallLo, floor = floor, ceil = ceil, fine = fine, trim = trim ?? s_Surf["wood_trim"],
                bit = s_Rooms.Count + 1
            };
            s_Rooms.Add(r);
            return r;
        }

        static Room Get(string id) => s_Rooms.First(r => r.id == id);

        static Door Dr(string a, string b, float c, float w, float hgt, string state, DoorStyle style, float bottom = 0f)
        {
            var d = new Door { A = Get(a), B = Get(b), c = c, w = w, hgt = hgt, state = state, style = style, bottom = bottom };
            var A = d.A; var B = d.B;
            const float eps = 0.01f;
            if (A.z1 <= B.z0 + eps) { d.alongX = true; d.n = Vector3.forward; d.g0 = A.z1; d.g1 = B.z0; d.sideA = 'N'; d.sideB = 'S'; }
            else if (B.z1 <= A.z0 + eps) { d.alongX = true; d.n = Vector3.back; d.g0 = B.z1; d.g1 = A.z0; d.sideA = 'S'; d.sideB = 'N'; }
            else if (A.x1 <= B.x0 + eps) { d.alongX = false; d.n = Vector3.right; d.g0 = A.x1; d.g1 = B.x0; d.sideA = 'E'; d.sideB = 'W'; }
            else if (B.x1 <= A.x0 + eps) { d.alongX = false; d.n = Vector3.left; d.g0 = B.x1; d.g1 = A.x0; d.sideA = 'W'; d.sideB = 'E'; }
            else throw new Exception($"rooms {a} and {b} do not share a wall");
            A.open.Add(new Opening { side = d.sideA, a = d.a, b = d.b, y0 = bottom, y1 = bottom + hgt, door = d });
            B.open.Add(new Opening { side = d.sideB, a = d.a, b = d.b, y0 = bottom, y1 = bottom + hgt, door = d });
            s_Doors.Add(d);
            return d;
        }

        // ---------------------------------------------------------------- rooms

        static void BuildRoomShell(Room r)
        {
            var go = new GameObject(r.name);
            go.transform.SetParent(s_Level, false);
            r.root = go.transform;
            var info = go.AddComponent<RoomInfo>();
            info.roomName = r.name; info.kind = r.kind; info.state = r.state;
            info.floorY = r.fy; info.height = r.h; info.renderingLayer = r.bit;
            info.rects = new[] { new Vector4(r.x0, r.z0, r.x1, r.z1) };
            r.structure = Child(r.root, "Structure");
            r.props = Child(r.root, "Props");
            r.lights = Child(r.root, "Lights");
            r.markers = Child(r.root, "Markers");
            var floorRoot = Child(r.root, "Floor");
            var ceilRoot = Child(r.root, "Ceiling");
            ceilRoot.gameObject.layer = LayerCeiling;

            Box(floorRoot, "Floor", new Vector3(r.x0, r.fy - 0.3f, r.z0), new Vector3(r.x1, r.fy, r.z1), r.floor, r.RL);
            var ceil = Box(ceilRoot, "Ceiling", new Vector3(r.x0 - T, r.fy + r.h, r.z0 - T), new Vector3(r.x1 + T, r.fy + r.h + 0.3f, r.z1 + T), r.ceil, r.RL, 0f, true, LayerCeiling);
            ceil.AddComponent<CeilingPart>();

            foreach (var side in new[] { 'S', 'N', 'W', 'E' }) BuildWall(r, side);
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static List<Vector2> Subtract(float lo, float hi, IEnumerable<Vector2> holes)
        {
            var parts = new List<Vector2> { new Vector2(lo, hi) };
            foreach (var hole in holes)
            {
                var next = new List<Vector2>();
                foreach (var p in parts)
                {
                    if (hole.y <= p.x || hole.x >= p.y) { next.Add(p); continue; }
                    if (hole.x > p.x) next.Add(new Vector2(p.x, hole.x));
                    if (hole.y < p.y) next.Add(new Vector2(hole.y, p.y));
                }
                parts = next;
            }
            return parts.Where(p => p.y - p.x > 0.005f).ToList();
        }

        static void BuildWall(Room r, char side)
        {
            bool alongX = side == 'S' || side == 'N';
            float from = alongX ? r.x0 - T : r.z0;
            float to = alongX ? r.x1 + T : r.z1;
            float n0, n1; // normal-axis extent of the wall slab
            switch (side)
            {
                case 'S': n0 = r.z0 - T; n1 = r.z0; break;
                case 'N': n0 = r.z1; n1 = r.z1 + T; break;
                case 'W': n0 = r.x0 - T; n1 = r.x0; break;
                default: n0 = r.x1; n1 = r.x1 + T; break;
            }
            var ops = r.open.Where(o => o.side == side).ToList();
            var cuts = new List<float> { from, to };
            foreach (var o in ops) { cuts.Add(Mathf.Clamp(o.a, from, to)); cuts.Add(Mathf.Clamp(o.b, from, to)); }
            cuts = cuts.Distinct().OrderBy(v => v).ToList();
            var wallRoot = r.structure;
            float bottom = -0.3f;
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float p = cuts[i], q = cuts[i + 1];
                if (q - p < 0.004f) continue;
                var covering = ops.Where(o => o.a <= p + 0.001f && o.b >= q - 0.001f).ToList();
                var solids = Subtract(bottom, r.h, covering.Select(o => new Vector2(o.y0, o.y1)));
                foreach (var s in solids)
                {
                    // split at the dado line when the upper wall has its own finish
                    var bands = new List<(float lo, float hi, Surf surf)>();
                    if (r.wallHi != r.wallLo && s.y > r.dado && s.x < r.dado)
                    {
                        bands.Add((s.x, r.dado, r.wallLo));
                        bands.Add((r.dado, s.y, r.wallHi));
                    }
                    else bands.Add((s.x, s.y, s.x >= r.dado ? r.wallHi : r.wallLo));
                    foreach (var band in bands)
                    {
                        Vector3 mn, mx;
                        if (alongX) { mn = new Vector3(p, r.fy + band.lo, n0); mx = new Vector3(q, r.fy + band.hi, n1); }
                        else { mn = new Vector3(n0, r.fy + band.lo, p); mx = new Vector3(n1, r.fy + band.hi, q); }
                        Box(wallRoot, "Wall_" + side, mn, mx, band.surf, r.RL, r.fy);
                    }
                }
                // trims on the interior face
                float ip = alongX ? Mathf.Max(p, r.x0) : p, iq = alongX ? Mathf.Min(q, r.x1) : q;
                if (iq - ip < 0.02f) continue;
                bool openAtFloor = covering.Any(o => o.y0 < 0.2f);
                if (r.fine && !openAtFloor) Trim(r, side, ip, iq, 0f, 0.17f, 0.025f, r.trim);
                bool openAtTop = covering.Any(o => o.y1 > r.h - 0.25f);
                if (r.fine && r.cornice && !openAtTop) Trim(r, side, ip, iq, r.h - 0.16f, r.h, 0.07f, r.trim);
                bool openAtDado = covering.Any(o => o.y0 < r.dado + 0.1f && o.y1 > r.dado - 0.1f);
                if (r.fine && r.h > 4f && !openAtDado) Trim(r, side, ip, iq, r.dado - 0.05f, r.dado + 0.06f, 0.045f, r.trim);
            }
        }

        static void Trim(Room r, char side, float p, float q, float ylo, float yhi, float depth, Surf s)
        {
            Vector3 mn, mx;
            switch (side)
            {
                case 'S': mn = new Vector3(p, r.fy + ylo, r.z0); mx = new Vector3(q, r.fy + yhi, r.z0 + depth); break;
                case 'N': mn = new Vector3(p, r.fy + ylo, r.z1 - depth); mx = new Vector3(q, r.fy + yhi, r.z1); break;
                case 'W': mn = new Vector3(r.x0, r.fy + ylo, p); mx = new Vector3(r.x0 + depth, r.fy + yhi, q); break;
                default: mn = new Vector3(r.x1 - depth, r.fy + ylo, p); mx = new Vector3(r.x1, r.fy + yhi, q); break;
            }
            Box(r.structure, "Trim", mn, mx, s, r.RL, r.fy, false);
        }

        // ---------------------------------------------------------------- doors

        /// <summary>World box from (along, y, normal) ranges for a door's wall.</summary>
        static void DoorBox(Door d, Transform parent, string name, float a0, float a1, float y0, float y1, float m0, float m1, Surf s, uint rl, bool col = true)
        {
            Vector3 mn, mx;
            if (d.alongX) { mn = new Vector3(a0, y0, m0); mx = new Vector3(a1, y1, m1); }
            else { mn = new Vector3(m0, y0, a0); mx = new Vector3(m1, y1, a1); }
            Box(parent, name, mn, mx, s, rl, d.A.fy, col);
        }

        static void BuildDoor(Door d)
        {
            var A = d.A; var B = d.B;
            uint rl = A.RL | B.RL;
            var go = new GameObject($"Door {A.name} - {B.name}");
            go.transform.SetParent(s_Level.Find("Doors"), false);
            go.transform.position = d.Centre;
            go.transform.rotation = Quaternion.identity;
            d.root = go.transform;
            var info = go.AddComponent<DoorwayInfo>();
            info.roomA = A.name; info.roomB = B.name; info.state = d.state; info.axis = d.n;
            info.designWidth = d.w - 0.08f; info.designHeight = d.hgt;
            d.info = info;

            float fy = A.fy, y0 = fy + d.bottom, y1 = fy + d.bottom + d.hgt;
            var lining = (A.fine || B.fine) ? s_Surf["wood_trim"] : s_Surf["wood_dark"];
            if (d.style == DoorStyle.Bookcase) lining = s_Surf["plaster_dirty"];
            // linings across the wall's depth, and the threshold
            DoorBox(d, go.transform, "JambLow", d.a - 0.02f, d.a + 0.04f, y0, y1, d.g0, d.g1, lining, rl);
            DoorBox(d, go.transform, "JambHigh", d.b - 0.04f, d.b + 0.02f, y0, y1, d.g0, d.g1, lining, rl);
            DoorBox(d, go.transform, "Head", d.a - 0.02f, d.b + 0.02f, y1 - 0.04f, y1 + 0.02f, d.g0, d.g1, lining, rl);
            if (d.bottom < 0.01f)
                DoorBox(d, go.transform, "Threshold", d.a + 0.04f, d.b - 0.04f, fy - 0.3f, fy, d.g0, d.g1, s_Surf["wood_dark"], rl);
            else
                DoorBox(d, go.transform, "Sill", d.a + 0.04f, d.b - 0.04f, y0 - 0.3f, y0, d.g0, d.g1, s_Surf["wood_dark"], rl);
            // architraves on both faces
            bool aLow = d.n.x + d.n.z > 0; // A lies on the low-coordinate side of the wall
            foreach (var (room, lowSide) in new[] { (A, aLow), (B, !aLow) })
            {
                if (!room.fine || d.style == DoorStyle.Bookcase) continue;
                float m0 = lowSide ? d.g0 - 0.05f : d.g1, m1 = lowSide ? d.g0 : d.g1 + 0.05f;
                DoorBox(d, go.transform, "Architrave", d.a - 0.18f, d.a - 0.02f, y0, y1 + 0.16f, m0, m1, room.trim, room.RL, false);
                DoorBox(d, go.transform, "Architrave", d.b + 0.02f, d.b + 0.18f, y0, y1 + 0.16f, m0, m1, room.trim, room.RL, false);
                DoorBox(d, go.transform, "Architrave", d.a - 0.18f, d.b + 0.18f, y1 + 0.02f, y1 + 0.18f, m0, m1, room.trim, room.RL, false);
            }

            switch (d.style)
            {
                case DoorStyle.Single: Leaves(d, false, false); break;
                case DoorStyle.Double: Leaves(d, true, false); break;
                case DoorStyle.OpenSingle: Leaves(d, false, true); break;
                case DoorStyle.OpenDouble: Leaves(d, true, true); break;
            }
        }

        /// <summary>
        /// Hangs Synty door leaves in the opening. A leaf's mesh runs along its local -X from the pivot edge, so the
        /// hinge sits at a jamb and the leaf is turned to span the opening; opening swings the free edge into B.
        /// </summary>
        static void Leaves(Door d, bool pair, bool standingOpen)
        {
            var prefab = Prefab(d.leaf);
            var pb = Bounds(prefab);
            float leafW = pb.size.x, leafH = pb.size.y;
            float clear = d.w - 0.1f;
            float y0 = d.A.fy + d.bottom;
            Vector3 u = d.alongX ? Vector3.right : Vector3.forward;
            Vector3 nInto = d.intoB ? d.n : -d.n;
            // hinge line: just inside the face of the room the leaves swing into
            float faceInto = Vector3.Dot(nInto, Vector3.one) > 0 ? d.g1 : d.g0;
            float hingeN = faceInto - Vector3.Dot(nInto, Vector3.one) * 0.07f;
            var jambs = pair ? new[] { -1, 1 } : new[] { d.hinge };
            var hinges = new List<Transform>(); var angles = new List<float>();
            foreach (var j in jambs)
            {
                float along = j < 0 ? d.a + 0.05f : d.b - 0.05f;
                Vector3 hp = d.alongX ? new Vector3(along, y0, hingeN) : new Vector3(hingeN, y0, along);
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(d.root, false);
                hinge.position = hp; hinge.rotation = Quaternion.identity;
                Vector3 dir = j < 0 ? u : -u; // from hinge toward the free edge
                // want local -X = dir  => local X = -dir; LookRotation gives right = Cross(up, f)
                Vector3 f = Vector3.Cross(-dir, Vector3.up); // Cross(up, f) = -dir  <=>  f = Cross(-dir, up)
                var leaf = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                leaf.transform.SetParent(hinge, false);
                leaf.transform.position = hp;
                leaf.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
                float wEach = pair ? clear * 0.5f : clear;
                leaf.transform.localScale = new Vector3(wEach / leafW, (d.hgt - 0.06f) / leafH, 1f);
                foreach (var rend in leaf.GetComponentsInChildren<Renderer>()) rend.renderingLayerMask = d.A.RL | d.B.RL;
                foreach (var t in leaf.GetComponentsInChildren<Transform>()) t.gameObject.layer = LayerDoorLeaf;
                if (leaf.GetComponentInChildren<Collider>() == null) leaf.AddComponent<BoxCollider>();
                float sign = Mathf.Sign(Vector3.Cross(dir, nInto).y);
                float ang = sign * d.openAngle;
                hinges.Add(hinge); angles.Add(ang);
            }
            if (standingOpen)
            {
                for (int i = 0; i < hinges.Count; i++) hinges[i].localRotation = Quaternion.Euler(0f, angles[i] * 0.92f, 0f);
                return;
            }
            var sw = d.root.gameObject.AddComponent<SwingDoor>();
            sw.hinges = hinges.ToArray(); sw.openAngles = angles.ToArray();
            d.swing = sw; d.info.door = sw;
        }

        // ---------------------------------------------------------------- stairs

        /// <summary>
        /// A straight flight of solid steps rising along <paramref name="dir"/> (unit X or Z) from <paramref name="start"/>
        /// (centre of the bottom edge, at the lower floor) with the given rise. Step boxes carry no colliders;
        /// one invisible ramp through the nosings carries the player.
        /// </summary>
        static void Flight(Room r, Transform parent, Vector3 start, Vector3 dir, float width, float rise, int steps, float going, Surf tread, Surf side, bool ramp = true)
        {
            float riser = rise / steps;
            Vector3 across = Vector3.Cross(Vector3.up, dir).normalized;
            for (int i = 0; i < steps; i++)
            {
                Vector3 a = start + dir * (i * going) - across * (width * 0.5f);
                Vector3 b = start + dir * ((i + 1) * going) + across * (width * 0.5f);
                Vector3 mn = Vector3.Min(a, b), mx = Vector3.Max(a, b);
                mn.y = start.y - 0.3f; mx.y = start.y + (i + 1) * riser;
                Box(parent, "Step", mn, mx, tread, r.RL, r.fy, false);
            }
            if (!ramp) return;
            // ramp top plane passes through the nosings: from the floor one going before the first step
            // to the last nosing, level with the top
            Vector3 lo = start - dir * going;
            Vector3 hi = start + dir * ((steps - 1) * going) + Vector3.up * rise;
            float run = steps * going;
            float len = Mathf.Sqrt(run * run + rise * rise);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            Vector3 mid = (lo + hi) * 0.5f;
            var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-ang, 0f, 0f);
            const float thick = 0.4f;
            var centre = mid - (rot * Vector3.up) * (thick * 0.5f);
            var g = Blocker(parent, "StairRamp", centre, new Vector3(width, thick, len + 0.1f), rot);
            g.isStatic = true;
            // the last tread, level with the top, so the ramp meets the landing without a gap
            Vector3 ta = start + dir * ((steps - 1) * going - 0.05f) - across * (width * 0.5f);
            Vector3 tb = start + dir * (steps * going + 0.05f) + across * (width * 0.5f);
            Vector3 tmn = Vector3.Min(ta, tb), tmx = Vector3.Max(ta, tb);
            tmn.y = start.y + rise - 0.3f; tmx.y = start.y + rise;
            Blocker(parent, "TopTread", (tmn + tmx) * 0.5f, tmx - tmn, Quaternion.identity);
        }
    }
}
