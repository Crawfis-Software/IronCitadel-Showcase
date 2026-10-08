// IronCitadel conformance kit: level.json, read into plain types, plus the world <-> cell rule.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace IronCitadel.Conformance
{
    /// <summary>A grid cell, [row, col]. Row 0 is the north edge, col 0 the west edge.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int R, C;
        public Cell(int r, int c) { R = r; C = c; }
        public bool Equals(Cell o) => R == o.R && C == o.C;
        public override bool Equals(object obj) => obj is Cell o && Equals(o);
        public override int GetHashCode() => R * 397 ^ C;
        public override string ToString() => "[" + R + "," + C + "]";
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public int[] ToArray() => new[] { R, C };
    }

    public sealed class RoomSpec
    {
        public string Id, Kind, HeightText, HeightClass;  // HeightClass: low | standard | tall
        public float? HeightTargetM;                     // "tall, about 15 m" -> 15
        public Cell Cell;
    }

    public sealed class DoorSpec
    {
        public string Id, Kind, Note;
        public bool Passable;
        public Cell A;
        public Cell? B;          // null when the door leads outside
        public string Outside;   // e.g. "outside, south"
        public bool IsOutside => B == null;
        public bool IsPortcullis => Kind != null && Kind.IndexOf("portcullis", StringComparison.OrdinalIgnoreCase) >= 0;
        public bool IsSecret => Kind != null && Kind.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public sealed class RouteSpec
    {
        public string Id, Note;
        public bool Blocked;
        public List<Cell> Cells = new List<Cell>();
    }

    public sealed class FeatureSpec
    {
        public string Id, Room, Side, Door, Note;
        public float? DeckM, RiseM;
    }

    public sealed class LevelSpec
    {
        public string Path, Name;
        public float Tile = 45f;
        public float FloorY = 0f;
        public int Rows, Cols;
        public string[] Grid;
        public List<RoomSpec> Rooms = new List<RoomSpec>();
        public List<DoorSpec> Doors = new List<DoorSpec>();
        public List<RouteSpec> Routes = new List<RouteSpec>();
        public List<FeatureSpec> Features = new List<FeatureSpec>();
        public List<string> MustBeTrue = new List<string>();
        public Cell StartCell;
        public string StartAt;

        // ---------- geometry: the origin rule ----------
        // The north-west corner of cell [0,0] is at world (0,0,0); +x is east, +z is north.
        // Cell [r,c] spans x 45c..45(c+1) and z -45(r+1)..-45r.

        public float XMin(Cell c) => Tile * c.C;
        public float XMax(Cell c) => Tile * (c.C + 1);
        public float ZMin(Cell c) => -Tile * (c.R + 1);
        public float ZMax(Cell c) => -Tile * c.R;
        public Vector3 Centre(Cell c) => new Vector3(Tile * (c.C + 0.5f), FloorY, -Tile * (c.R + 0.5f));

        public bool InGrid(Cell c) => c.R >= 0 && c.R < Rows && c.C >= 0 && c.C < Cols;

        /// <summary>The cell under a world point (may be outside the grid).</summary>
        public Cell CellOf(Vector3 p) => new Cell(Mathf.FloorToInt(-p.z / Tile), Mathf.FloorToInt(p.x / Tile));

        public char KindAt(Cell c) => InGrid(c) ? Grid[c.R][c.C] : '\0';
        public bool IsRock(Cell c) => KindAt(c) == '#';
        public RoomSpec RoomAt(Cell c) => Rooms.Find(r => r.Cell == c);
        public RoomSpec Room(string id) => Rooms.Find(r => r.Id == id);
        public RoomSpec RoomOfKind(char kind) => Rooms.Find(r => r.Kind == kind.ToString());

        /// <summary>The midpoint of the edge two 4-adjacent cells share, at floor height.</summary>
        public Vector3 SharedEdgeCentre(Cell a, Cell b)
        {
            var ca = Centre(a); var cb = Centre(b);
            return new Vector3((ca.x + cb.x) * 0.5f, FloorY, (ca.z + cb.z) * 0.5f);
        }

        /// <summary>The midpoint of a cell's outer edge on the named side (north/south/east/west).</summary>
        public Vector3 SideCentre(Cell c, string side)
        {
            var m = Centre(c);
            switch (side)
            {
                case "north": return new Vector3(m.x, FloorY, ZMax(c));
                case "south": return new Vector3(m.x, FloorY, ZMin(c));
                case "east": return new Vector3(XMax(c), FloorY, m.z);
                case "west": return new Vector3(XMin(c), FloorY, m.z);
            }
            return m;
        }

        public static string SideWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var w in new[] { "north", "south", "east", "west" })
                if (text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return w;
            return null;
        }

        public string NodeName(Cell c)
        {
            if (!InGrid(c)) return "outside";
            var room = RoomAt(c);
            return room != null ? room.Id : "rock" + c;
        }

        // ---------- loading ----------

        public static LevelSpec Load(string path)
        {
            var root = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (root == null) throw new FormatException("level.json: the root is not an object");
            var L = new LevelSpec { Path = System.IO.Path.GetFullPath(path), Name = Str(root, "name") };

            var world = Obj(root, "world");
            if (world != null)
            {
                if (world.TryGetValue("tile_m", out var t)) L.Tile = F(t);
                if (world.TryGetValue("rows", out var r)) L.Rows = (int)F(r);
                if (world.TryGetValue("cols", out var c)) L.Cols = (int)F(c);
            }
            var grid = List(root, "grid");
            L.Grid = new string[grid.Count];
            for (int i = 0; i < grid.Count; i++) L.Grid[i] = (string)grid[i];
            if (L.Rows == 0) L.Rows = L.Grid.Length;
            if (L.Cols == 0) L.Cols = L.Grid[0].Length;

            foreach (var o in List(root, "rooms"))
            {
                var d = (Dictionary<string, object>)o;
                var room = new RoomSpec
                {
                    Id = Str(d, "id"), Kind = Str(d, "kind"), Cell = ToCell(d["cell"]),
                    HeightText = Str(d, "height") ?? "standard"
                };
                var h = room.HeightText.ToLowerInvariant();
                room.HeightClass = h.Contains("low") ? "low" : h.Contains("tall") ? "tall" : "standard";
                var m = Regex.Match(h, @"([0-9]+(\.[0-9]+)?)\s*m");
                if (m.Success) room.HeightTargetM = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                L.Rooms.Add(room);
            }

            foreach (var o in List(root, "doors"))
            {
                var d = (Dictionary<string, object>)o;
                var between = (List<object>)d["between"];
                var door = new DoorSpec
                {
                    Id = Str(d, "id"), Kind = Str(d, "kind"), Note = Str(d, "note"),
                    Passable = d.TryGetValue("passable", out var p) && p is bool pb && pb,
                    A = ToCell(between[0])
                };
                if (between[1] is string s) door.Outside = s;
                else door.B = ToCell(between[1]);
                L.Doors.Add(door);
            }

            foreach (var o in List(root, "routes"))
            {
                var d = (Dictionary<string, object>)o;
                var route = new RouteSpec
                {
                    Id = Str(d, "id"), Note = Str(d, "note"),
                    Blocked = d.TryGetValue("blocked", out var b) && b is bool bb && bb
                };
                foreach (var c in (List<object>)d["cells"]) route.Cells.Add(ToCell(c));
                L.Routes.Add(route);
            }

            foreach (var o in List(root, "features"))
            {
                var d = (Dictionary<string, object>)o;
                var f = new FeatureSpec
                {
                    Id = Str(d, "id"), Room = Str(d, "room"), Side = Str(d, "side"),
                    Door = Str(d, "door"), Note = Str(d, "note")
                };
                if (d.TryGetValue("deck_m", out var dm)) f.DeckM = F(dm);
                if (d.TryGetValue("rise_m", out var rm)) f.RiseM = F(rm);
                L.Features.Add(f);
            }

            foreach (var o in List(root, "must_be_true")) L.MustBeTrue.Add((string)o);

            var start = Obj(root, "start");
            if (start != null)
            {
                L.StartCell = ToCell(start["cell"]);
                L.StartAt = Str(start, "at");
            }
            return L;
        }

        public FeatureSpec Feature(string id) => Features.Find(f => f.Id == id);
        public RouteSpec Route(string id) => Routes.Find(r => r.Id == id);

        static Cell ToCell(object o)
        {
            var l = (List<object>)o;
            return new Cell((int)F(l[0]), (int)F(l[1]));
        }

        static float F(object o) => Convert.ToSingle(o, CultureInfo.InvariantCulture);
        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as string : null;
        static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        static List<object> List(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
    }
}
