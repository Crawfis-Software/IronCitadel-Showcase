using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IronCitadel.Checks
{
    /// <summary>
    /// Measurements taken from the open scene's geometry (colliders and renderers), not from the builder's plan.
    /// </summary>
    public static class SceneQuery
    {
        public class RoomInfo
        {
            public Room room;
            public string name, kind, state;
            public float floorY;            // measured top of the floor tiles
            public float x0, x1, z0, z1;    // measured inner faces of its walls
            public float ceilMin, ceilMax;  // measured ceiling heights above the floor
            public float ceilCoverage;      // fraction of floor samples with this room's ceiling overhead
            public Vector3 Center => new Vector3((x0 + x1) * 0.5f, floorY, (z0 + z1) * 0.5f);
            public float Area => (x1 - x0) * (z1 - z0);
            public bool ContainsXZ(Vector3 p, float m = 0f) => p.x >= x0 - m && p.x <= x1 + m && p.z >= z0 - m && p.z <= z1 + m;
            public List<Vector2> Outline => new List<Vector2> { new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) };
        }

        public class Opening
        {
            public RoomInfo from; public string toRoom; public char side;
            public Vector3 center; public float width, yMin, yMax;
        }

        // ------------------------------------------------------------ classification
        static bool Under(Transform t, string name)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == name) return true;
            return false;
        }

        public static bool IsLeaf(Collider c) => c.GetComponentInParent<SwingDoor>() != null || c.GetComponentInParent<SecretBookcase>() != null;

        public static bool IsPlayer(Collider c) => c.GetComponentInParent<CharacterController>() != null || c.transform.root.name.StartsWith("IronCitadelPlayer");

        /// Walls, floors, ceilings, door frames, the gallery and stair structure: the building, not its furniture.
        public static bool IsStructure(Collider c)
        {
            if (IsLeaf(c) || IsPlayer(c)) return false;
            var t = c.transform;
            return Under(t, "Walls") || Under(t, "Floor") || Under(t, "Ceiling") || Under(t, "Doors") || Under(t, "Gallery") || Under(t, "Stair") || Under(t, "Stage");
        }

        public static bool InGroup(Collider c, Room r, string group)
        {
            var g = r.transform.Find(group);
            return g != null && c.transform.IsChildOf(g);
        }

        // ------------------------------------------------------------ rooms
        public static List<RoomInfo> Rooms()
        {
            Physics.SyncTransforms();
            var list = new List<RoomInfo>();
            foreach (var r in Object.FindObjectsByType<Room>().OrderBy(r => r.transform.GetSiblingIndex()))
                list.Add(MeasureRoom(r));
            return list;
        }

        static RoomInfo MeasureRoom(Room r)
        {
            var info = new RoomInfo { room = r, name = r.roomName, kind = r.kind, state = r.state };
            // outer extent from the floor tiles
            var floor = r.transform.Find("Floor");
            Bounds fb = new Bounds(); bool has = false;
            foreach (var rend in floor.GetComponentsInChildren<Renderer>()) { if (!has) { fb = rend.bounds; has = true; } else fb.Encapsulate(rend.bounds); }
            // floor height: straight down at a few points, the first hit on this room's floor
            var ys = new List<float>();
            foreach (var p in new[] { fb.center, fb.center + new Vector3(fb.extents.x * 0.5f, 0, 0), fb.center - new Vector3(0, 0, fb.extents.z * 0.5f) })
                foreach (var h in Physics.RaycastAll(new Vector3(p.x, fb.max.y + 1f, p.z), Vector3.down, 4f).OrderBy(h => h.distance))
                    if (InGroup(h.collider, r, "Floor")) { ys.Add(h.point.y); break; }
            info.floorY = ys.Count > 0 ? ys.Max() : fb.max.y;
            // inner wall faces: from inside, towards each side, against this room's walls only
            float sampleY = info.floorY + Mathf.Min(2.7f, r.height - 0.3f);
            info.x0 = Face(r, fb, sampleY, Vector3.left);
            info.x1 = Face(r, fb, sampleY, Vector3.right);
            info.z0 = Face(r, fb, sampleY, Vector3.back);
            info.z1 = Face(r, fb, sampleY, Vector3.forward);
            // ceilings overhead
            int n = 0, covered = 0; float cmin = float.MaxValue, cmax = 0;
            for (float x = info.x0 + 0.5f; x < info.x1 - 0.2f; x += 1f)
                for (float z = info.z0 + 0.5f; z < info.z1 - 0.2f; z += 1f)
                {
                    n++;
                    var hits = Physics.RaycastAll(new Vector3(x, info.floorY + 0.3f, z), Vector3.up, 60f);
                    var ceil = hits.Where(h => InGroup(h.collider, r, "Ceiling")).OrderBy(h => h.distance).ToList();
                    if (ceil.Count > 0) { covered++; float hy = ceil[0].point.y - info.floorY; cmin = Mathf.Min(cmin, hy); cmax = Mathf.Max(cmax, hy); }
                }
            info.ceilCoverage = n > 0 ? covered / (float)n : 0;
            info.ceilMin = cmin == float.MaxValue ? 0 : cmin; info.ceilMax = cmax;
            return info;
        }

        static float Face(Room r, Bounds fb, float y, Vector3 dir)
        {
            var c = fb.center; var hitsAt = new List<float>();
            var lateral = Mathf.Abs(dir.x) > 0 ? Vector3.forward : Vector3.right;
            float ext = Mathf.Abs(dir.x) > 0 ? fb.extents.z : fb.extents.x;
            foreach (float f in new[] { -0.6f, -0.3f, 0f, 0.3f, 0.6f })
            {
                var o = new Vector3(c.x, y, c.z) + lateral * ext * f;
                // start 2 m inside the floor's edge to avoid starting inside furniture against the wall
                float half = Mathf.Abs(dir.x) > 0 ? fb.extents.x : fb.extents.z;
                o += dir * (half - 2.0f);
                foreach (var h in Physics.RaycastAll(o - dir * 0.01f, dir, 3.0f).OrderBy(h => h.distance))
                    if (InGroup(h.collider, r, "Walls")) { hitsAt.Add(Vector3.Dot(h.point, dir)); break; }
            }
            if (hitsAt.Count == 0) return dir.x < 0 ? fb.min.x + 0.43f : dir.x > 0 ? fb.max.x - 0.43f : dir.z < 0 ? fb.min.z + 0.43f : fb.max.z - 0.43f;
            hitsAt.Sort();
            float v = hitsAt[hitsAt.Count / 2];
            return Mathf.Abs(dir.x) > 0 ? v * dir.x : v * dir.z;
        }

        public static RoomInfo RoomAt(List<RoomInfo> rooms, Vector3 p, float margin = 0f)
        {
            RoomInfo best = null;
            foreach (var r in rooms)
                if (r.ContainsXZ(p, margin) && p.y >= r.floorY - 0.6f && p.y <= r.floorY + r.ceilMax + 0.2f)
                    if (best == null || r.floorY > best.floorY) best = r;
            return best;
        }

        // ------------------------------------------------------------ openings
        /// Scans every room's walls for gaps (rays across the wall that meet no structure), with door leaves
        /// and the secret bookcase ignored. Each run of gaps is one opening, labelled with the room beyond it.
        public static List<Opening> Openings(List<RoomInfo> rooms)
        {
            Physics.SyncTransforms();
            var result = new List<Opening>();
            foreach (var r in rooms)
            {
                foreach (char side in "SNWE")
                {
                    Vector3 n = side == 'S' ? Vector3.back : side == 'N' ? Vector3.forward : side == 'W' ? Vector3.left : Vector3.right;
                    bool alongX = side == 'S' || side == 'N';
                    float a0 = alongX ? r.x0 : r.z0, a1 = alongX ? r.x1 : r.z1;
                    float face = side == 'S' ? r.z0 : side == 'N' ? r.z1 : side == 'W' ? r.x0 : r.x1;
                    float top = r.ceilMin > 0 ? r.ceilMin : r.room.height;
                    var open = new List<(float a, float y)>();
                    for (float a = a0 + 0.05f; a <= a1 - 0.05f; a += 0.1f)
                        for (float y = r.floorY + 0.25f; y < r.floorY + top - 0.2f; y += 0.25f)
                        {
                            Vector3 o = alongX ? new Vector3(a, y, face) : new Vector3(face, y, a);
                            o -= n * 0.15f;
                            bool blocked = Physics.RaycastAll(o, n, 2.2f, ~0, QueryTriggerInteraction.Ignore).Any(h => IsStructure(h.collider));
                            if (!blocked) open.Add((a, y));
                        }
                    // cluster by position along the wall
                    // runs along the wall, then each run split where its open heights have a gap (one opening above another)
                    var groups = new List<List<(float a, float y)>>();
                    foreach (var run in Cluster(open.Select(o => o.a).Distinct().OrderBy(v => v).ToList(), 0.15f))
                    {
                        var inRun = open.Where(o => o.a >= run.min - 0.001f && o.a <= run.max + 0.001f).ToList();
                        foreach (var band in Cluster(inRun.Select(o => o.y).Distinct().OrderBy(v => v).ToList(), 0.3f))
                            groups.Add(inRun.Where(o => o.y >= band.min - 0.001f && o.y <= band.max + 0.001f).ToList());
                    }
                    foreach (var pts in groups)
                    {
                        var grp = (min: pts.Min(o => o.a), max: pts.Max(o => o.a));
                        float mid = (grp.min + grp.max) * 0.5f, ymin = pts.Min(p => p.y), ymax = pts.Max(p => p.y);
                        Vector3 c = alongX ? new Vector3(mid, r.floorY, face) : new Vector3(face, r.floorY, mid);
                        // the room beyond: a little past the boundary, at the opening's height
                        Vector3 beyond = c + n * 1.4f; beyond.y = ymin + 0.1f;
                        var other = RoomAt(rooms, beyond, 0.1f);
                        if (other == r) other = null;
                        result.Add(new Opening
                        {
                            from = r, side = side, toRoom = other != null ? other.name : "(outside)",
                            center = new Vector3(c.x, ymin - 0.25f < r.floorY + 0.1f ? r.floorY : ymin - 0.25f, c.z),
                            width = grp.max - grp.min + 0.1f, yMin = ymin, yMax = ymax
                        });
                    }
                }
            }
            return result;
        }

        static List<(float min, float max)> Cluster(List<float> vals, float gap)
        {
            var res = new List<(float, float)>();
            if (vals.Count == 0) return res;
            float s = vals[0], p = vals[0];
            for (int i = 1; i < vals.Count; i++)
            {
                if (vals[i] - p > gap) { res.Add((s, p)); s = vals[i]; }
                p = vals[i];
            }
            res.Add((s, p));
            return res;
        }

        /// Clear width of an opening, measured at 1 m above its sill along the wall, structure only.
        public static float ClearWidth(DoorLink d)
        {
            var c = d.transform.position + Vector3.up * 1.0f; var along = d.transform.right;
            float w = 0;
            foreach (var dir in new[] { along, -along })
            {
                float best = 3f;
                foreach (float off in new[] { -0.3f, 0f, 0.3f })
                {
                    var o = c + d.transform.forward * off;
                    foreach (var h in Physics.RaycastAll(o, dir, 3f).OrderBy(h => h.distance))
                        if (IsStructure(h.collider)) { best = Mathf.Min(best, h.distance); break; }
                }
                w += best;
            }
            return w;
        }

        // ------------------------------------------------------------ sight
        /// True if nothing solid (walls, props, shut doors) stands between a and b. The player capsule is ignored.
        public static bool Clear(Vector3 a, Vector3 b)
        {
            var d = b - a; float len = d.magnitude;
            foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
                if (!IsPlayer(h.collider)) return false;
            return true;
        }
    }
}
