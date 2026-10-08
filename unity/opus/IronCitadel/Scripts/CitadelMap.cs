using UnityEngine;

namespace IronCitadel
{
    /// <summary>
    /// Where things are in the Iron Citadel, in world metres. Cell [r, c] spans x 45c..45(c+1), z -45(r+1)..-45r.
    /// Names the room (and the part of it) for any position, for the HUD.
    /// </summary>
    public static class CitadelMap
    {
        public const float Tile = 45f;

        public static readonly string[,] Rooms =
        {
            { null, null, "Throne Room", null },
            { null, "Kitchen", "Great Hall", "Forge" },
            { "Treasure Vault", "Prison", "Armory", "Alchemist's Study" },
            { null, "Guard Room", "Entry Hall", "Library" },
        };

        public struct Zone
        {
            public string name;
            public float x0, x1, z0, z1, y0, y1;
            public Zone(string n, float x0, float x1, float z0, float z1, float y0 = -10f, float y1 = 100f)
            { name = n; this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1; this.y0 = y0; this.y1 = y1; }
            public bool Contains(Vector3 p) => p.x >= x0 && p.x <= x1 && p.z >= z0 && p.z <= z1 && p.y >= y0 && p.y <= y1;
        }

        // Sub-areas, checked before the plain room name. Great hall cell [1,2]: x 90..135, z -90..-45.
        public static readonly Zone[] Zones =
        {
            new Zone("Great Hall — minstrels' gallery", 92.5f, 132.5f, -87.5f, -81.1f, 3.5f),
            new Zone("Great Hall — walkway under the gallery", 97.5f, 127.5f, -87.5f, -81.5f, -10f, 3.5f),
            new Zone("Great Hall — west stair", 92.5f, 97.5f, -81.5f, -71.5f),
            new Zone("Great Hall — west landing", 92.5f, 97.5f, -71.5f, -61.5f),
            new Zone("Great Hall — east stair", 127.5f, 132.5f, -81.5f, -71.5f),
            new Zone("Great Hall — east landing", 127.5f, 132.5f, -71.5f, -61.5f),
            new Zone("Prison — warden's corner", 47.5f, 57.5f, -132.5f, -92.5f),
            new Zone("Throne Room — the stage", 92.5f, 132.5f, -10.5f, -2.5f, 0.8f),
            new Zone("Throne Room — antechamber", 107.5f, 117.5f, -42.5f, -36.5f),
        };

        public static Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(-p.z / Tile), Mathf.FloorToInt(p.x / Tile));

        public static string RoomAt(Vector3 p)
        {
            var c = Cell(p);
            if (c.x < 0 || c.x > 3 || c.y < 0 || c.y > 3) return "Outside";
            return Rooms[c.x, c.y];
        }

        public static string Describe(Vector3 p)
        {
            foreach (var z in Zones) if (z.Contains(p)) return z.name;
            var c = Cell(p);
            if (c.x < 0 || c.x > 3 || c.y < 0 || c.y > 3) return "Outside the walls";
            string room = Rooms[c.x, c.y] ?? "Solid rock";
            float u = p.x - c.y * Tile, v = p.z + (c.x + 1) * Tile;
            if (u < 2.4f || u > 42.6f || v < 2.4f || v > 42.6f) return "Passage — " + room;
            return room;
        }
    }
}
