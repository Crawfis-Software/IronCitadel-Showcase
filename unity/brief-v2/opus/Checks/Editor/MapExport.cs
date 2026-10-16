using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace IronCitadel.Checks
{
    /// <summary>
    /// Writes Map/map.json from the open scene: every number is measured from the built geometry or read from
    /// the transforms of the objects in the scene (player, door openings, gallery, stage, throne, markers).
    /// </summary>
    public static class MapExport
    {
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string P3(Vector3 v) => $"[{F(v.x)}, {F(v.y)}, {F(v.z)}]";
        static string P2(float x, float z) => $"[{F(x)}, {F(z)}]";
        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public class Gallery { public float deckY, x0, x1, z0, z1; public Vector3 railA, railB; }
        public class Stage { public float topY, x0, x1, z0, z1; }

        public static Gallery MeasureGallery()
        {
            var g = GameObject.Find("Gallery");
            if (g == null) return null;
            var deck = g.transform.Find("Deck");
            Bounds b = new Bounds(); bool has = false;
            foreach (var r in deck.GetComponentsInChildren<Renderer>()) { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            var hall = Object.FindObjectsByType<Room>().First(r => r.transform == g.transform.parent);
            var rooms = SceneQuery.Rooms(); var hi = rooms.First(r => r.room == hall);
            var res = new Gallery { x0 = Mathf.Max(b.min.x, hi.x0), x1 = Mathf.Min(b.max.x, hi.x1), z0 = Mathf.Max(b.min.z, hi.z0), z1 = Mathf.Min(b.max.z, hi.z1) };
            // deck height: down onto the deck halfway along it
            var top = Physics.RaycastAll(new Vector3((res.x0 + res.x1) * 0.5f + 1.3f, b.max.y + 3f, (res.z0 + res.z1) * 0.5f), Vector3.down, 6f)
                .Where(h => h.collider.transform.IsChildOf(deck)).OrderBy(h => h.distance).FirstOrDefault();
            res.deckY = top.collider != null ? top.point.y : b.max.y;
            var rail = g.transform.Find("Rail");
            Bounds rb = new Bounds(); has = false;
            foreach (var r in rail.GetComponentsInChildren<Renderer>()) { if (!has) { rb = r.bounds; has = true; } else rb.Encapsulate(r.bounds); }
            float rz = rb.center.z;
            res.railA = new Vector3(Mathf.Max(rb.min.x, hi.x0), res.deckY, rz); res.railB = new Vector3(Mathf.Min(rb.max.x, hi.x1), res.deckY, rz);
            return res;
        }

        public static Stage MeasureStage()
        {
            var s = GameObject.Find("Stage");
            if (s == null) return null;
            // the dais blocks (the renderers whose tops are highest, excluding steps and throne)
            var throne = s.transform.Find("Throne");
            Bounds b = new Bounds(); bool has = false;
            foreach (var r in s.GetComponentsInChildren<Renderer>())
            {
                if (throne != null && r.transform.IsChildOf(throne)) continue;
                if (r.bounds.size.y < 3f) continue; // the blocks are 5 m cubes sunk into the floor
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            var c = b.center + new Vector3(-b.extents.x * 0.5f, 0, -b.extents.z * 0.6f);
            var hit = Physics.RaycastAll(new Vector3(c.x, b.max.y + 3f, c.z), Vector3.down, 6f).Where(h => h.collider.transform.IsChildOf(s.transform) && (throne == null || !h.collider.transform.IsChildOf(throne))).OrderBy(h => h.distance).FirstOrDefault();
            var room = SceneQuery.Rooms().First(r => s.transform.IsChildOf(r.room.transform));
            return new Stage { topY = hit.collider != null ? hit.point.y : b.max.y, x0 = Mathf.Max(b.min.x, room.x0), x1 = Mathf.Min(b.max.x, room.x1), z0 = Mathf.Max(b.min.z, room.z0), z1 = Mathf.Min(b.max.z, room.z1) };
        }

        public static string Write(string path = "Map/map.json")
        {
            var rooms = SceneQuery.Rooms();
            var sb = new StringBuilder();
            sb.Append("{\n");
            // start
            var cc = Object.FindAnyObjectByType<CharacterController>();
            var f = cc.transform.forward;
            sb.Append($"  \"start\": {{ \"position\": {P3(cc.transform.position)}, \"facing\": {P2(f.x, f.z)} }},\n");
            // rooms
            sb.Append("  \"rooms\": [\n");
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                string outline = string.Join(", ", r.Outline.Select(p => P2(p.x, p.y)));
                sb.Append($"    {{ \"name\": {Q(r.name)}, \"kind\": {Q(r.kind)}, \"state\": {Q(r.state)}, \"floor_y\": {F(r.floorY)}, \"floor\": [ [{outline}] ] }}{(i < rooms.Count - 1 ? "," : "")}\n");
            }
            sb.Append("  ],\n");
            // doors (leaves set aside while the clear width is measured)
            var doors = Object.FindObjectsByType<DoorLink>().OrderBy(d => d.transform.GetSiblingIndex()).ToList();
            var leaves = doors.Where(d => d.leaf != null).Select(d => d.leaf).ToList();
            foreach (var l in leaves) l.SetActive(false);
            Physics.SyncTransforms();
            sb.Append("  \"doors\": [\n");
            for (int i = 0; i < doors.Count; i++)
            {
                var d = doors[i];
                float w = SceneQuery.ClearWidth(d);
                sb.Append($"    {{ \"rooms\": [{Q(d.roomA)}, {Q(d.roomB)}], \"state\": {Q(d.state)}, \"position\": {P3(d.transform.position)}, \"width\": {F(w)} }}{(i < doors.Count - 1 ? "," : "")}\n");
            }
            sb.Append("  ],\n");
            foreach (var l in leaves) l.SetActive(true);
            Physics.SyncTransforms();
            // gallery
            var g = MeasureGallery();
            if (g != null)
                sb.Append($"  \"gallery\": {{ \"deck_y\": {F(g.deckY)}, \"deck\": [ [{P2(g.x0, g.z0)}, {P2(g.x1, g.z0)}, {P2(g.x1, g.z1)}, {P2(g.x0, g.z1)}] ], \"rail\": [{P2(g.railA.x, g.railA.z)}, {P2(g.railB.x, g.railB.z)}] }},\n");
            var st = MeasureStage();
            if (st != null)
                sb.Append($"  \"stage\": {{ \"top_y\": {F(st.topY)}, \"outline\": [{P2(st.x0, st.z0)}, {P2(st.x1, st.z0)}, {P2(st.x1, st.z1)}, {P2(st.x0, st.z1)}] }},\n");
            var throne = GameObject.Find("Throne");
            sb.Append($"  \"throne\": {P3(throne.transform.position)},\n");
            var markers = Object.FindObjectsByType<Marker>().OrderBy(m => m.transform.GetSiblingIndex()).ToList();
            var enemies = markers.Where(m => m.IsEnemy).ToList();
            sb.Append("  \"enemies\": [\n");
            for (int i = 0; i < enemies.Count; i++)
            {
                var m = enemies[i]; var room = SceneQuery.RoomAt(rooms, m.transform.position + Vector3.up * 0.1f); var fw = m.transform.forward;
                sb.Append($"    {{ \"kind\": {Q(m.kind)}, \"room\": {Q(room != null ? room.name : "?")}, \"position\": {P3(m.transform.position)}, \"facing\": {P2(fw.x, fw.z)} }}{(i < enemies.Count - 1 ? "," : "")}\n");
            }
            sb.Append("  ],\n");
            var pickups = markers.Where(m => !m.IsEnemy).ToList();
            sb.Append("  \"pickups\": [\n");
            for (int i = 0; i < pickups.Count; i++)
            {
                var m = pickups[i]; var room = SceneQuery.RoomAt(rooms, m.transform.position + Vector3.up * 0.1f);
                sb.Append($"    {{ \"kind\": {Q(m.kind)}, \"room\": {Q(room != null ? room.name : "?")}, \"position\": {P3(m.transform.position)} }}{(i < pickups.Count - 1 ? "," : "")}\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
