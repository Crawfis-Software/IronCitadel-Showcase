using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel
{
    /// <summary>A room of the level. The floor tiles are children of the "Floor" child; the outline is computed from them.</summary>
    public class IronRoom : MonoBehaviour
    {
        public string roomName;
        public string kind;      // the brief's kind, or "extra"
        public string state;     // decayed, looted, stocked, untouched
        public float floorY;     // nominal floor surface height
        public float ceilingY;   // nominal ceiling height (underside)
        public bool isGallery;   // a raised deck inside another room (the HUD shows it as part of its parent)
        public string parentRoom; // for the gallery: the room it belongs to

        readonly List<Rect> _rects = new List<Rect>();
        float _yMin, _yMax;

        /// <summary>Axis-aligned x/z rectangles of the floor tiles under the Floor child (world space).</summary>
        public List<Rect> FloorRects()
        {
            var list = new List<Rect>();
            var floor = transform.Find("Floor");
            if (floor == null) return list;
            foreach (var r in floor.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                list.Add(Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z));
            }
            return list;
        }

        public Bounds FloorBounds()
        {
            var rects = FloorRects();
            if (rects.Count == 0) return new Bounds(transform.position, Vector3.zero);
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (var r in rects) { x0 = Mathf.Min(x0, r.xMin); x1 = Mathf.Max(x1, r.xMax); z0 = Mathf.Min(z0, r.yMin); z1 = Mathf.Max(z1, r.yMax); }
            var b = new Bounds(); b.SetMinMax(new Vector3(x0, floorY, z0), new Vector3(x1, ceilingY, z1));
            return b;
        }

        void Awake() { Refresh(); }

        public void Refresh()
        {
            _rects.Clear(); _rects.AddRange(FloorRects());
            _yMin = floorY - 0.6f; _yMax = ceilingY + 0.5f;
        }

        /// <summary>True when the point is over this room's floor and between its floor and ceiling.</summary>
        public bool Contains(Vector3 p)
        {
            if (_rects.Count == 0) Refresh();
            if (p.y < _yMin || p.y > _yMax) return false;
            foreach (var r in _rects) if (p.x >= r.xMin - 0.05f && p.x <= r.xMax + 0.05f && p.z >= r.yMin - 0.05f && p.z <= r.yMax + 0.05f) return true;
            return false;
        }

        public static IronRoom RoomAt(Vector3 p)
        {
            IronRoom best = null;
            foreach (var r in FindObjectsByType<IronRoom>(FindObjectsSortMode.None))
            {
                if (!r.Contains(p)) continue;
                if (best == null || (r.isGallery && !best.isGallery)) best = r; // the gallery wins over the hall beneath it
            }
            return best;
        }
    }
}
