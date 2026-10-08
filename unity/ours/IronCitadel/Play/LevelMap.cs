using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace IronCitadel.Play
{
    /// <summary>level.json read at run time: which room a world point stands in. Cell [r, c] spans
    /// x 45c..45(c+1) and z -45(r+1)..-45r (level.json's origin rule), so the lookup is two divisions.</summary>
    public sealed class LevelMap : IRoomNames
    {
        [Serializable] class WorldJson { public float tile_m = 45f; public int rows; public int cols; }
        [Serializable] class RoomJson { public string id; public string kind; public int[] cell; }
        [Serializable] class LevelJson { public string name; public WorldJson world; public string[] grid; public RoomJson[] rooms; }

        public string Name { get; private set; }
        public float Tile { get; private set; }
        public int Rows { get; private set; }
        public int Cols { get; private set; }

        string[] grid;
        readonly Dictionary<(int, int), string> roomName = new Dictionary<(int, int), string>();
        readonly Dictionary<(int, int), string> roomId = new Dictionary<(int, int), string>();

        public static LevelMap Parse(string json)
        {
            var j = JsonUtility.FromJson<LevelJson>(json);
            var m = new LevelMap
            {
                Name = j.name,
                Tile = j.world != null && j.world.tile_m > 0f ? j.world.tile_m : 45f,
                grid = j.grid ?? new string[0]
            };
            m.Rows = j.world != null && j.world.rows > 0 ? j.world.rows : m.grid.Length;
            m.Cols = j.world != null && j.world.cols > 0 ? j.world.cols : (m.grid.Length > 0 ? m.grid[0].Length : 0);

            // "kinds": { "E": "entry hall: a guard lobby", ... } (JsonUtility cannot read a map)
            var kinds = new Dictionary<string, string>();
            var block = Regex.Match(json, "\"kinds\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
            if (block.Success)
                foreach (Match kv in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
                {
                    var text = kv.Groups[2].Value;
                    int colon = text.IndexOf(':');
                    kinds[kv.Groups[1].Value] = Capitalise((colon > 0 ? text.Substring(0, colon) : text).Trim());
                }

            if (j.rooms != null)
                foreach (var r in j.rooms)
                {
                    if (r.cell == null || r.cell.Length < 2) continue;
                    var key = (r.cell[0], r.cell[1]);
                    m.roomId[key] = r.id;
                    m.roomName[key] = r.kind != null && kinds.TryGetValue(r.kind, out var k) ? k : Pretty(r.id);
                }
            return m;
        }

        /// <summary>The cell under a world point, or false when it is off the grid.</summary>
        public bool CellAt(Vector3 p, out int r, out int c)
        {
            c = Mathf.FloorToInt(p.x / Tile);
            r = Mathf.FloorToInt(-p.z / Tile);
            return r >= 0 && r < Rows && c >= 0 && c < Cols;
        }

        /// <summary>"Great hall", "Throne room", ... for the cell under <paramref name="p"/>; "Solid rock" for a # cell
        /// and "Outside" off the grid.</summary>
        public string RoomNameAt(Vector3 p, out string id)
        {
            id = null;
            if (!CellAt(p, out var r, out var c)) return "Outside";
            if (roomName.TryGetValue((r, c), out var n)) { id = roomId[(r, c)]; return n; }
            return r < grid.Length && c < grid[r].Length && grid[r][c] == '#' ? "Solid rock" : "Unmapped";
        }

        public Vector3 Centre => new Vector3(Tile * Cols / 2f, 0f, -Tile * Rows / 2f);
        public Vector2 Size => new Vector2(Tile * Cols, Tile * Rows);

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s.Substring(1);
        static string Pretty(string id) => Capitalise((id ?? "?").Replace('_', ' '));
    }
}
