using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace IronCitadelV2.Editor
{
    /// <summary>The run directory's inputs (CONTRACT.md): level.json from the design program, picks.json from the
    /// picker, placements.json from the tactics program (optional). Parsed strictly: a missing file or a field the
    /// builder needs throws, naming it, and the build stops.</summary>
    public sealed class V2Room
    {
        public string id, kind, name, height, wing, role;
        public int r, c;
        public List<string> themes = new List<string>();
    }

    public sealed class V2Door
    {
        public string id, a, b, side, kind;
        public List<string> roles = new List<string>();
    }

    public sealed class V2Level
    {
        public string name;
        public float tile = 45f;
        public int rows, cols;
        public readonly List<V2Room> rooms = new List<V2Room>();
        public readonly List<V2Door> doors = new List<V2Door>();
        public readonly List<string> bigLoop = new List<string>();
        public string startRoom, startLooksAt, goalRoom, galleryRoom, galleryToward, galleryWayIn;
        public JObject json;

        public V2Room Room(string id) => rooms.FirstOrDefault(x => x.id == id);
        public V2Room At(int r, int c) => rooms.FirstOrDefault(x => x.r == r && x.c == c);
        public V2Room RoomOfKind(string kind) => rooms.FirstOrDefault(x => x.kind == kind);
        public V2Door DoorBetween(string a, string b) => doors.FirstOrDefault(d => (d.a == a && d.b == b) || (d.a == b && d.b == a));
    }

    public sealed class V2Pick
    {
        public string room, kind, theme, family, state, stateKind, ceiling, doorSet, prefab, guid;
        public int r, c, rotation;
        public float lid;
        public bool walkOk;
    }

    public sealed class V2PlacedEnemy
    {
        public string id, room, kind, post, watches;
        public Vector3 pos;
        public float yaw;
    }

    public sealed class V2PlacedPickup
    {
        public string id, room;
        public Vector3 pos;
    }

    public sealed class V2Placements
    {
        public readonly List<V2PlacedEnemy> enemies = new List<V2PlacedEnemy>();
        public readonly List<V2PlacedPickup> pickups = new List<V2PlacedPickup>();
        public bool hasStart;
        public Vector3 startPos;
        public float startYaw;
    }

    /// <summary>A tile edge seen from cell (r, c) on side N/E/S/W: its middle at floor height, the unit normal from
    /// (r, c) out across it, and the unit direction along it (+x for N/S edges, +z for E/W).</summary>
    public struct V2Edge
    {
        public int r, c, r2, c2;
        public char side;
        public Vector3 centre, n, along;
        public override string ToString() => $"[{r},{c}]{side}";
    }

    public static class V2Model
    {
        public static V2Level LoadLevel(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("level.json is missing: " + path, path);
            var j = JObject.Parse(File.ReadAllText(path));
            var L = new V2Level { json = j };
            L.name = (string)j["name"] ?? throw Missing(path, "name");
            L.tile = j["tile_m"] != null ? (float)j["tile_m"] : 45f;
            var grid = j["grid"] as JObject ?? throw Missing(path, "grid");
            L.rows = (int)grid["rows"];
            L.cols = (int)grid["cols"];
            foreach (var r in j["rooms"] as JArray ?? throw Missing(path, "rooms"))
            {
                var cell = r["cell"] as JArray ?? throw Missing(path, "rooms[].cell");
                var room = new V2Room
                {
                    id = (string)r["id"], kind = (string)r["kind"], name = (string)r["name"], height = (string)r["height"],
                    wing = (string)r["wing"], role = (string)r["role"], r = (int)cell[0], c = (int)cell[1]
                };
                if (string.IsNullOrEmpty(room.id)) throw Missing(path, "rooms[].id");
                if (string.IsNullOrEmpty(room.name)) room.name = Pretty(room.id);
                if (r["themes"] is JArray th) room.themes.AddRange(th.Select(t => (string)t));
                if (room.r < 0 || room.r >= L.rows || room.c < 0 || room.c >= L.cols)
                    throw new InvalidDataException($"level.json: room {room.id} cell [{room.r},{room.c}] is off the {L.rows}x{L.cols} grid");
                if (L.rooms.Any(x => x.id == room.id)) throw new InvalidDataException("level.json: room id twice: " + room.id);
                if (L.At(room.r, room.c) != null) throw new InvalidDataException($"level.json: two rooms in cell [{room.r},{room.c}]");
                L.rooms.Add(room);
            }
            foreach (var d in j["doors"] as JArray ?? throw Missing(path, "doors"))
            {
                var door = new V2Door { id = (string)d["id"], a = (string)d["a"], b = (string)d["b"], side = (string)d["side_of_a"], kind = (string)d["kind"] };
                if (d["roles"] is JArray roles) door.roles.AddRange(roles.Select(t => (string)t));
                var ra = L.Room(door.a) ?? throw new InvalidDataException($"level.json: door {door.id}: no room {door.a}");
                var rb = L.Room(door.b) ?? throw new InvalidDataException($"level.json: door {door.id}: no room {door.b}");
                if (door.kind != "open" && door.kind != "shut" && door.kind != "hidden")
                    throw new InvalidDataException($"level.json: door {door.id}: kind '{door.kind}' is not open, shut or hidden");
                var e = EdgeOf(L.tile, ra.r, ra.c, door.side != null && door.side.Length > 0 ? door.side[0] : '?');
                if (door.side == null || door.side.Length != 1 || "NESW".IndexOf(door.side[0]) < 0 || e.r2 != rb.r || e.c2 != rb.c)
                    throw new InvalidDataException($"level.json: door {door.id}: {door.b} [{rb.r},{rb.c}] is not on side {door.side} of {door.a} [{ra.r},{ra.c}]");
                L.doors.Add(door);
            }
            if (j["big_loop"] is JArray loop) L.bigLoop.AddRange(loop.Select(t => (string)t));
            L.startRoom = (string)j["start"]?["room"] ?? throw Missing(path, "start.room");
            L.startLooksAt = (string)j["start"]?["looks_at"];
            L.goalRoom = (string)j["goal"]?["room"] ?? throw Missing(path, "goal.room");
            L.galleryRoom = (string)j["gallery"]?["room"];
            L.galleryToward = (string)j["gallery"]?["toward"];
            L.galleryWayIn = (string)j["gallery"]?["way_in_from"];
            if (L.Room(L.startRoom) == null) throw new InvalidDataException("level.json: start.room is not a room: " + L.startRoom);
            if (L.Room(L.goalRoom) == null) throw new InvalidDataException("level.json: goal.room is not a room: " + L.goalRoom);
            return L;
        }

        public static Dictionary<string, V2Pick> LoadPicks(string path, V2Level L)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("picks.json is missing: " + path, path);
            var j = JObject.Parse(File.ReadAllText(path));
            var map = new Dictionary<string, V2Pick>(StringComparer.Ordinal);
            foreach (var p in j["picks"] as JArray ?? throw Missing(path, "picks"))
            {
                var cell = p["cell"] as JArray;
                var pick = new V2Pick
                {
                    room = (string)p["room"], kind = (string)p["kind"], theme = (string)p["theme"], family = (string)p["family"],
                    state = (string)p["state"], stateKind = (string)p["state_kind"], ceiling = (string)p["ceiling"],
                    doorSet = (string)p["door_set"], prefab = (string)p["prefab"], guid = (string)p["guid"],
                    rotation = p["rotation"] != null ? (int)p["rotation"] : 0,
                    lid = p["lid_m"] != null ? (float)p["lid_m"] : 0f,
                    walkOk = p["walk_ok"] == null || (bool)p["walk_ok"],
                    r = cell != null ? (int)cell[0] : -1, c = cell != null ? (int)cell[1] : -1
                };
                if (string.IsNullOrEmpty(pick.room) || string.IsNullOrEmpty(pick.prefab)) throw Missing(path, "picks[].room / prefab");
                map[pick.room] = pick;
            }
            foreach (var room in L.rooms)
            {
                if (!map.TryGetValue(room.id, out var pick)) throw new InvalidDataException($"picks.json has no pick for room {room.id}");
                if (pick.r >= 0 && (pick.r != room.r || pick.c != room.c))
                    throw new InvalidDataException($"picks.json: {room.id} is picked for cell [{pick.r},{pick.c}] but level.json puts it in [{room.r},{room.c}]");
                if (pick.rotation % 90 != 0) throw new InvalidDataException($"picks.json: {room.id} rotation {pick.rotation} is not a multiple of 90");
            }
            return map;
        }

        /// <summary>placements.json, or null when the file is absent (no enemies, no pickups, the default start).</summary>
        public static V2Placements LoadPlacements(string path)
        {
            if (!File.Exists(path)) return null;
            var j = JObject.Parse(File.ReadAllText(path));
            var P = new V2Placements();
            if (j["enemies"] is JArray es)
                foreach (var e in es)
                    P.enemies.Add(new V2PlacedEnemy
                    {
                        id = (string)e["id"], room = (string)e["room"], kind = (string)e["kind"], post = (string)e["post"],
                        watches = (string)e["watches"], pos = Vec(e["pos"]) ?? throw Missing(path, "enemies[].pos"),
                        yaw = e["yaw"] != null ? (float)e["yaw"] : 0f
                    });
            if (j["pickups"] is JArray ps)
                foreach (var p in ps)
                    P.pickups.Add(new V2PlacedPickup { id = (string)p["id"], room = (string)p["room"], pos = Vec(p["pos"]) ?? throw Missing(path, "pickups[].pos") });
            if (j["player_start"] is JObject st && Vec(st["pos"]) is Vector3 sp)
            {
                P.hasStart = true;
                P.startPos = sp;
                P.startYaw = st["yaw"] != null ? (float)st["yaw"] : 0f;
            }
            return P;
        }

        static Vector3? Vec(JToken t)
        {
            if (!(t is JArray a) || a.Count < 3) return null;
            return new Vector3((float)a[0], (float)a[1], (float)a[2]);
        }

        static Exception Missing(string path, string what) => new InvalidDataException($"{Path.GetFileName(path)}: '{what}' is missing");

        public static string Pretty(string id) => string.IsNullOrEmpty(id) ? "?" : char.ToUpperInvariant(id[0]) + id.Substring(1).Replace('_', ' ');

        // ------------------------------------------------------------------ geometry (CONTRACT.md conventions)

        public static Vector3 CellCenter(float tile, int r, int c) => new Vector3(tile * c + tile / 2f, 0f, -tile * r - tile / 2f);

        public static V2Edge EdgeOf(float tile, int r, int c, char side)
        {
            var e = new V2Edge { r = r, c = c, side = side };
            switch (side)
            {
                case 'N': e.r2 = r - 1; e.c2 = c; e.centre = new Vector3(tile * c + tile / 2, 0, -tile * r); e.n = Vector3.forward; break;
                case 'S': e.r2 = r + 1; e.c2 = c; e.centre = new Vector3(tile * c + tile / 2, 0, -tile * (r + 1)); e.n = Vector3.back; break;
                case 'E': e.r2 = r; e.c2 = c + 1; e.centre = new Vector3(tile * (c + 1), 0, -tile * r - tile / 2); e.n = Vector3.right; break;
                default: e.r2 = r; e.c2 = c - 1; e.centre = new Vector3(tile * c, 0, -tile * r - tile / 2); e.n = Vector3.left; break;
            }
            e.along = Mathf.Abs(e.n.z) > 0.5f ? Vector3.right : Vector3.forward;
            return e;
        }

        public static V2Edge EdgeOfDoor(V2Level L, V2Door d)
        {
            var a = L.Room(d.a);
            return EdgeOf(L.tile, a.r, a.c, d.side[0]);
        }

        public static char SideBetween(int r1, int c1, int r2, int c2) =>
            r2 == r1 - 1 ? 'N' : r2 == r1 + 1 ? 'S' : c2 == c1 + 1 ? 'E' : 'W';

        public static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
