using System.Collections.Generic;
using UnityEngine;

namespace Hollowmere
{
    /// <summary>
    /// Names a room of the level. The builder copies the room's plan rectangles here so the HUD can tell
    /// which room the player stands in; the map exporter and the checks measure the room's floor from the
    /// floor colliders under the "Floor" child instead.
    /// </summary>
    public class RoomInfo : MonoBehaviour
    {
        public string roomName;
        public string kind;
        public string state;
        public float floorY;
        public float height;
        [Tooltip("Plan rectangles (xMin, zMin, xMax, zMax) in world metres.")]
        public Vector4[] rects;
        public int renderingLayer;

        static readonly List<RoomInfo> s_All = new List<RoomInfo>();
        public static IReadOnlyList<RoomInfo> All => s_All;

        void OnEnable() { if (!s_All.Contains(this)) s_All.Add(this); }
        void OnDisable() { s_All.Remove(this); }

        public bool ContainsXZ(Vector3 p, float margin = 0f)
        {
            if (rects == null) return false;
            foreach (var r in rects)
                if (p.x >= r.x - margin && p.x <= r.z + margin && p.z >= r.y - margin && p.z <= r.w + margin) return true;
            return false;
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            return ContainsXZ(p, margin) && p.y >= floorY - 0.6f && p.y <= floorY + height + 0.2f;
        }

        public Vector3 Centre
        {
            get
            {
                if (rects == null || rects.Length == 0) return transform.position;
                float area = 0; Vector2 c = Vector2.zero;
                foreach (var r in rects)
                {
                    float a = (r.z - r.x) * (r.w - r.y);
                    c += a * new Vector2((r.x + r.z) * 0.5f, (r.y + r.w) * 0.5f);
                    area += a;
                }
                c /= Mathf.Max(area, 0.0001f);
                return new Vector3(c.x, floorY, c.y);
            }
        }

        /// <summary>The room containing a point, preferring the smallest match.</summary>
        public static RoomInfo At(Vector3 p, float margin = 0f)
        {
            RoomInfo best = null; float bestArea = float.MaxValue;
            foreach (var r in s_All)
            {
                if (!r.Contains(p, margin)) continue;
                float a = 0; foreach (var q in r.rects) a += (q.z - q.x) * (q.w - q.y);
                if (a < bestArea) { best = r; bestArea = a; }
            }
            return best;
        }

        public static RoomInfo Find(string name)
        {
            foreach (var r in s_All) if (r.roomName == name) return r;
            foreach (var r in Object.FindObjectsByType<RoomInfo>(FindObjectsInactive.Include)) if (r.roomName == name) return r;
            return null;
        }
    }
}
