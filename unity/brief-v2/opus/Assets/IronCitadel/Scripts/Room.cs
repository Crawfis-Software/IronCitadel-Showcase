using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// One room of the level. The builder fills in its name, the brief's kind, its state and the floor
    /// rectangles (x, z, width, depth of the walkable interior); the HUD uses them to name the room the
    /// player is in. Map export and checks measure the built geometry instead of trusting these numbers.
    /// </summary>
    public class Room : MonoBehaviour
    {
        public string roomName;
        public string kind;
        public string state;
        public float floorY;
        public float height;
        public Rect[] rects = new Rect[0];
        [Tooltip("Height of a gallery deck in this room above floorY, or 0 for none.")]
        public float galleryHeight;
        [Tooltip("Parent of this room's ceiling tiles; the overview hides their renderers.")]
        public GameObject ceilingGroup;

        public static readonly List<Room> All = new List<Room>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool ContainsXZ(Vector3 p, float margin = 0f)
        {
            foreach (var r in rects)
                if (p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.z >= r.yMin - margin && p.z <= r.yMax + margin) return true;
            return false;
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            if (p.y < floorY - 1f || p.y > floorY + height + 0.5f) return false;
            return ContainsXZ(p, margin);
        }

        public static Room At(Vector3 p)
        {
            Room best = null;
            foreach (var r in All)
                if (r.Contains(p) && (best == null || r.floorY > best.floorY)) best = r;
            if (best != null) return best;
            foreach (var r in All) if (r.Contains(p, 0.6f)) return r; // standing in a doorway
            return null;
        }

        public static Room Named(string n)
        {
            foreach (var r in All) if (r.roomName == n) return r;
            return null;
        }
    }
}
