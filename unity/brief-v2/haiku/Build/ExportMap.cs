using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IronCitadel.Interact;
using IronCitadel.Level;
using UnityEditor.SceneManagement;
using UnityEngine;

// Writes Map/map.json from the saved scene: floors, doors, gallery, stage, throne and markers are read from the
// scene's objects and their bounds, not from the build plan. Run after the scene is saved:
//   unity command run_script -- --file Build/ExportMap.cs --entry ExportMap.Run
public static class ExportMap
{
    const string ScenePath = "Assets/Scenes/IronCitadel.unity";
    const float Cell = 5f;

    struct Box { public float x0, z0, x1, z1; }

    public static string Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        var log = new StringBuilder();

        var rooms = Object.FindObjectsByType<LevelRoom>(FindObjectsSortMode.None).OrderBy(r => r.transform.GetSiblingIndex()).ToList();
        var roomFloors = new Dictionary<LevelRoom, List<Box>>();
        var roomY = new Dictionary<LevelRoom, float>();
        foreach (var r in rooms)
        {
            var tiles = new List<Bounds>();
            var tops = new List<float>();
            foreach (Transform c in r.transform)
                if (c.name == "Floor") { tiles.Add(WorldBounds(c)); tops.Add(WalkTop(c)); }
            roomFloors[r] = Merge(tiles);
            roomY[r] = tops.Count > 0 ? tops.Max() : 0f;
        }

        var sb = new StringBuilder();
        sb.AppendLine("{");

        // start
        var interact = Object.FindFirstObjectByType<InteractOnE>();
        var player = interact.transform.root;
        sb.AppendLine($"  \"start\": {{ \"position\": {V3(player.position)}, \"facing\": {Facing(player.forward)} }},");

        // rooms
        sb.AppendLine("  \"rooms\": [");
        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            var outlines = roomFloors[r].Select(Outline);
            sb.Append($"    {{ \"name\": \"{r.roomName}\", \"kind\": \"{r.kind}\", \"state\": \"{r.state}\", \"floor_y\": {F(roomY[r])}, \"floor\": [ {string.Join(", ", outlines)} ] }}");
            sb.AppendLine(i < rooms.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ],");

        // doors
        var doors = Object.FindObjectsByType<LevelDoor>(FindObjectsSortMode.None).OrderBy(d => d.transform.GetSiblingIndex()).ToList();
        sb.AppendLine("  \"doors\": [");
        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            sb.Append($"    {{ \"rooms\": [\"{d.roomA}\", \"{d.roomB}\"], \"state\": \"{d.state}\", \"position\": {V3(d.transform.position)}, \"width\": {F(d.width)} }}");
            sb.AppendLine(i < doors.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ],");

        // gallery, stage, throne
        var features = Object.FindObjectsByType<LevelFeature>(FindObjectsSortMode.None);
        var gallery = features.FirstOrDefault(f => f.feature == "gallery");
        var stage = features.FirstOrDefault(f => f.feature == "stage");
        var throne = features.FirstOrDefault(f => f.feature == "throne");

        if (gallery != null)
        {
            var deck = gallery.transform.Cast<Transform>().Where(t => t.name == "Deck").Select(WorldBounds).ToList();
            var rails = gallery.transform.Cast<Transform>().Where(t => t.name == "Rail").Select(WorldBounds).OrderBy(b => b.min.x).ToList();
            float deckY = gallery.transform.Cast<Transform>().Where(t => t.name == "Deck").Max(WalkTop);
            string railLine = rails.Count == 0 ? "[]" : $"[ [{F(rails.Min(b => b.min.x))}, {F(rails.Average(b => b.center.z))}], [{F(rails.Max(b => b.max.x))}, {F(rails.Average(b => b.center.z))}] ]";
            sb.AppendLine($"  \"gallery\": {{ \"deck_y\": {F(deckY)}, \"deck\": [ {string.Join(", ", Merge(deck).Select(Outline))} ], \"rail\": {railLine} }},");
        }
        else log.AppendLine("ERROR no gallery feature");

        if (stage != null)
        {
            var blockTs = stage.transform.Cast<Transform>().Where(t => t.name == "Stage").ToList();
            var blocks = blockTs.Select(WorldBounds).ToList();
            float topY = blockTs.Max(WalkTop);
            var box = Bounding(blocks);
            sb.AppendLine($"  \"stage\": {{ \"top_y\": {F(topY)}, \"outline\": {Outline(box)} }},");
        }
        else log.AppendLine("ERROR no stage feature");

        if (throne != null)
        {
            var b = WorldBounds(throne.transform);
            sb.AppendLine($"  \"throne\": {V3(new Vector3(b.center.x, b.min.y, b.center.z))},");
        }
        else log.AppendLine("ERROR no throne feature");

        // enemies and pickups
        var markers = Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).OrderBy(m => m.transform.GetSiblingIndex()).ToList();
        var enemies = markers.Where(m => m.markerKind == "enemy").ToList();
        var pickups = markers.Where(m => m.markerKind == "pickup").ToList();
        sb.AppendLine("  \"enemies\": [");
        for (int i = 0; i < enemies.Count; i++)
        {
            var m = enemies[i];
            string at = RoomAt(m.transform.position, rooms, roomFloors);
            if (at != m.room) log.AppendLine($"ERROR enemy {m.enemyKind} at {m.transform.position} stands in {at}, built for {m.room}");
            sb.Append($"    {{ \"kind\": \"{m.enemyKind}\", \"room\": \"{at}\", \"position\": {V3(m.transform.position)}, \"facing\": {Facing(m.transform.forward)} }}");
            sb.AppendLine(i < enemies.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ],");
        sb.AppendLine("  \"pickups\": [");
        for (int i = 0; i < pickups.Count; i++)
        {
            var m = pickups[i];
            string at = RoomAt(m.transform.position, rooms, roomFloors);
            if (at != m.room) log.AppendLine($"ERROR pickup {m.pickupKind} at {m.transform.position} stands in {at}, built for {m.room}");
            sb.Append($"    {{ \"kind\": \"{m.pickupKind}\", \"room\": \"{at}\", \"position\": {V3(m.transform.position)} }}");
            sb.AppendLine(i < pickups.Count - 1 ? "," : "");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        Directory.CreateDirectory("Map");
        File.WriteAllText("Map/map.json", sb.ToString());
        log.AppendLine($"wrote Map/map.json: {rooms.Count} rooms, {doors.Count} doors, {enemies.Count} enemies, {pickups.Count} pickups; scene {scene.path}");
        return log.ToString();
    }

    static string RoomAt(Vector3 p, List<LevelRoom> rooms, Dictionary<LevelRoom, List<Box>> floors)
    {
        foreach (var r in rooms)
            foreach (var b in floors[r])
                if (p.x >= b.x0 - 0.01f && p.x <= b.x1 + 0.01f && p.z >= b.z0 - 0.01f && p.z <= b.z1 + 0.01f) return r.roomName;
        return "";
    }

    // the height a player stands at on a piece: the top of its colliders (the walkable surface), or its renderers
    static float WalkTop(Transform t)
    {
        var cols = t.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) return WorldBounds(t).max.y;
        return cols.Max(c => c.bounds.max.y);
    }

    static Bounds WorldBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    // maximal rectangles covering the 5 m cells of the given tile bounds
    static List<Box> Merge(List<Bounds> tiles)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var b in tiles) cells.Add((Mathf.RoundToInt(b.min.x / Cell), Mathf.RoundToInt(b.min.z / Cell)));
        var used = new HashSet<(int, int)>();
        var boxes = new List<Box>();
        foreach (var c in cells.OrderBy(c => c.Item2).ThenBy(c => c.Item1))
        {
            if (used.Contains(c)) continue;
            int x1 = c.Item1;
            while (cells.Contains((x1 + 1, c.Item2)) && !used.Contains((x1 + 1, c.Item2))) x1++;
            int z1 = c.Item2;
            while (true)
            {
                bool row = true;
                for (int x = c.Item1; x <= x1 && row; x++)
                    row = cells.Contains((x, z1 + 1)) && !used.Contains((x, z1 + 1));
                if (!row) break;
                z1++;
            }
            for (int x = c.Item1; x <= x1; x++)
                for (int z = c.Item2; z <= z1; z++) used.Add((x, z));
            boxes.Add(new Box { x0 = c.Item1 * Cell, z0 = c.Item2 * Cell, x1 = (x1 + 1) * Cell, z1 = (z1 + 1) * Cell });
        }
        return boxes;
    }

    static Box Bounding(List<Bounds> bs)
    {
        float x0 = bs.Min(b => b.min.x), z0 = bs.Min(b => b.min.z), x1 = bs.Max(b => b.max.x), z1 = bs.Max(b => b.max.z);
        return new Box { x0 = x0, z0 = z0, x1 = x1, z1 = z1 };
    }

    static string Outline(Box b) =>
        $"[ [{F(b.x0)}, {F(b.z0)}], [{F(b.x1)}, {F(b.z0)}], [{F(b.x1)}, {F(b.z1)}], [{F(b.x0)}, {F(b.z1)}] ]";

    static string Facing(Vector3 f)
    {
        Vector3 d = new Vector3(f.x, 0, f.z).normalized;
        return $"[{F(d.x)}, {F(d.z)}]";
    }

    static string V3(Vector3 v) => $"[{F(v.x)}, {F(v.y)}, {F(v.z)}]";

    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
