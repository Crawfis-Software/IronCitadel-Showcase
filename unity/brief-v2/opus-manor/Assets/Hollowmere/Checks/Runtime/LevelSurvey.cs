using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Hollowmere.Checks
{
    /// <summary>
    /// Measures the built level from its colliders and components: room floors and ceilings, door openings,
    /// the gallery, the stage, markers. Used by the map export and by the checks, so both read the scene as built.
    /// </summary>
    public static class LevelSurvey
    {
        public const int LayerCeiling = 8, LayerDoorLeaf = 9, LayerMarker = 10, LayerSeeThrough = 11;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<RoomInfo> Rooms() =>
            Object.FindObjectsByType<RoomInfo>(FindObjectsInactive.Exclude).OrderBy(r => r.transform.GetSiblingIndex()).ToList();

        public static List<DoorwayInfo> Doors() =>
            Object.FindObjectsByType<DoorwayInfo>(FindObjectsInactive.Exclude).OrderBy(d => d.transform.GetSiblingIndex()).ToList();

        public static List<Collider> ChildColliders(Transform root, string child)
        {
            var t = root.Find(child);
            return t == null ? new List<Collider>() : t.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToList();
        }

        /// <summary>The room's floor rectangles (x0, z0, x1, z1), from its floor colliders.</summary>
        public static List<Rect> FloorRects(RoomInfo r) =>
            ChildColliders(r.transform, "Floor").Select(c => Rect.MinMaxRect(c.bounds.min.x, c.bounds.min.z, c.bounds.max.x, c.bounds.max.z)).ToList();

        public static float FloorY(RoomInfo r)
        {
            var cs = ChildColliders(r.transform, "Floor");
            return cs.Count == 0 ? float.NaN : cs.Max(c => c.bounds.max.y);
        }

        public static float CeilingY(RoomInfo r)
        {
            var cs = ChildColliders(r.transform, "Ceiling");
            return cs.Count == 0 ? float.NaN : cs.Min(c => c.bounds.min.y);
        }

        public static bool InFloor(RoomInfo r, Vector3 p, float margin = 0f)
        {
            foreach (var q in FloorRects(r))
                if (p.x >= q.xMin - margin && p.x <= q.xMax + margin && p.z >= q.yMin - margin && p.z <= q.yMax + margin) return true;
            return false;
        }

        /// <summary>The room whose floor outline holds the point (and whose height span holds its y).</summary>
        public static RoomInfo RoomAt(Vector3 p, List<RoomInfo> rooms = null)
        {
            rooms ??= Rooms();
            foreach (var r in rooms)
            {
                float fy = FloorY(r), cy = CeilingY(r);
                if (InFloor(r, p) && p.y >= fy - 0.6f && p.y <= cy + 0.1f) return r;
            }
            return null;
        }

        public struct Opening
        {
            public Vector3 centre;   // middle of the opening at its bottom
            public float width, height;
            public Vector3 along;    // unit vector across the opening
            public Vector3 through;  // unit vector from roomA to roomB
            public float depthLo, depthHi; // extent through the wall, as coordinates on the 'through' axis
        }

        /// <summary>The clear opening between a doorway's jamb linings, under its head, above its threshold.</summary>
        public static Opening Measure(DoorwayInfo d)
        {
            Collider lo = null, hi = null, head = null, sill = null;
            foreach (var c in d.GetComponentsInChildren<Collider>())
            {
                if (c.transform.parent != d.transform) continue;
                switch (c.name) { case "JambLow": lo = c; break; case "JambHigh": hi = c; break; case "Head": head = c; break; case "Threshold": case "Sill": sill = c; break; }
            }
            var o = new Opening { through = d.axis.normalized };
            bool alongX = Mathf.Abs(d.axis.z) > 0.5f;
            o.along = alongX ? Vector3.right : Vector3.forward;
            float a0 = alongX ? lo.bounds.max.x : lo.bounds.max.z;
            float a1 = alongX ? hi.bounds.min.x : hi.bounds.min.z;
            float y0 = sill != null ? sill.bounds.max.y : d.transform.position.y;
            float y1 = head != null ? head.bounds.min.y : y0 + d.designHeight;
            float m = alongX ? (lo.bounds.center.z) : (lo.bounds.center.x);
            o.depthLo = alongX ? lo.bounds.min.z : lo.bounds.min.x;
            o.depthHi = alongX ? lo.bounds.max.z : lo.bounds.max.x;
            o.width = a1 - a0;
            o.height = y1 - y0;
            float ac = (a0 + a1) * 0.5f;
            o.centre = alongX ? new Vector3(ac, y0, m) : new Vector3(m, y0, ac);
            return o;
        }

        public static Bounds? NamedBounds(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return null;
            var c = go.GetComponent<Collider>();
            return c != null ? c.bounds : (Bounds?)null;
        }

        public static Collider Find(string name)
        {
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude)) if (c.name == name) return c;
            return null;
        }

        public static string F(float v) => v.ToString("0.00", Inv);
        public static string V3(Vector3 v) => $"[{F(v.x)}, {F(v.y)}, {F(v.z)}]";
        public static string V2(float x, float z) => $"[{F(x)}, {F(z)}]";
        public static string RectOutline(Rect q) => $"[{V2(q.xMin, q.yMin)}, {V2(q.xMax, q.yMin)}, {V2(q.xMax, q.yMax)}, {V2(q.xMin, q.yMax)}]";
        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>Writes the level, as built, in the brief's map form.</summary>
        public static string MapJson()
        {
            var sb = new StringBuilder();
            var rooms = Rooms();
            var fpc = Object.FindAnyObjectByType<CharacterController>();
            var pt = fpc.transform;
            var fwd = pt.forward; fwd.y = 0; fwd.Normalize();
            sb.Append("{\n");
            sb.Append($"  \"start\": {{ \"position\": {V3(pt.position)}, \"facing\": {V2(fwd.x, fwd.z)} }},\n");
            sb.Append("  \"rooms\": [\n");
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                var outs = string.Join(", ", FloorRects(r).Select(RectOutline));
                sb.Append($"    {{ \"name\": {Q(r.roomName)}, \"kind\": {Q(r.kind)}, \"state\": {Q(r.state)}, \"floor_y\": {F(FloorY(r))}, \"ceiling_y\": {F(CeilingY(r))}, \"floor\": [ {outs} ] }}");
                sb.Append(i + 1 < rooms.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n  \"doors\": [\n");
            var doors = Doors();
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i]; var o = Measure(d);
                sb.Append($"    {{ \"rooms\": [{Q(d.roomA)}, {Q(d.roomB)}], \"state\": {Q(d.state)}, \"position\": {V3(o.centre)}, \"width\": {F(o.width)}, \"height\": {F(o.height)} }}");
                sb.Append(i + 1 < doors.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n");
            var deck = Find("GalleryDeck").bounds;
            var rail = Find("RailBlocker");
            var hall = rooms.First(r => r.kind == "dining hall");
            Collider hallRail = hall.GetComponentsInChildren<Collider>().First(c => c.name == "RailBlocker");
            var rb = hallRail.bounds;
            sb.Append($"  \"gallery\": {{ \"deck_y\": {F(deck.max.y)}, \"deck\": [ {RectOutline(Rect.MinMaxRect(deck.min.x, deck.min.z, deck.max.x, deck.max.z))} ], \"rail\": [{V2(rb.min.x, rb.center.z)}, {V2(rb.max.x, rb.center.z)}] }},\n");
            var stage = Find("StageTop").bounds;
            sb.Append($"  \"stage\": {{ \"top_y\": {F(stage.max.y)}, \"outline\": {RectOutline(Rect.MinMaxRect(stage.min.x, stage.min.z, stage.max.x, stage.max.z))} }},\n");
            var chair = Object.FindAnyObjectByType<ChairGoal>();
            sb.Append($"  \"chair\": {V3(chair.transform.position)},\n");
            sb.Append("  \"enemies\": [\n");
            var enemies = Object.FindObjectsByType<EnemyMarker>(FindObjectsInactive.Exclude).OrderBy(e => e.transform.position.z).ThenBy(e => e.transform.position.x).ToList();
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i]; var f = e.transform.forward; f.y = 0; f.Normalize();
                var room = RoomAt(e.transform.position + Vector3.up * 0.1f, rooms);
                sb.Append($"    {{ \"kind\": {Q(e.kind)}, \"room\": {Q(room != null ? room.roomName : "?")}, \"position\": {V3(e.transform.position)}, \"facing\": {V2(f.x, f.z)} }}");
                sb.Append(i + 1 < enemies.Count ? ",\n" : "\n");
            }
            sb.Append("  ],\n  \"pickups\": [\n");
            var pickups = Object.FindObjectsByType<PickupMarker>(FindObjectsInactive.Exclude).OrderBy(p => p.kind).ToList();
            for (int i = 0; i < pickups.Count; i++)
            {
                var p = pickups[i];
                var room = RoomAt(p.transform.position + Vector3.up * 0.05f, rooms);
                sb.Append($"    {{ \"kind\": {Q(p.kind)}, \"room\": {Q(room != null ? room.roomName : "?")}, \"position\": {V3(p.transform.position)} }}");
                sb.Append(i + 1 < pickups.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }
    }
}
