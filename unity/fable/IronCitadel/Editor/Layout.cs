using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.Editor
{
    public enum Dir { N = 0, E = 1, S = 2, W = 3 }

    public enum OpenKind
    {
        Doorway,    // a double-door-sized hole in the wall, no leaves
        Arch,       // a full-module archway
        Arcade,     // archway with nothing above it (the gallery deck is open above)
        Gate,       // the portcullis: large stone frame, portcullis leaf, blocking collider
        GreatDoors, // large frame with open wooden leaves
        Secret,     // single door hole; the prison side hides it behind a swinging bookcase
        OuterDoor,  // double door hole with closed leaves (the way in; nothing lies beyond)
        Bars,       // prison cell front: fence panels and a barred door
        StairTop,   // bottom module only; the stair arrives on the deck above
        Fireplace,  // a wall with a hearth alcove (no passage)
    }

    public class Zone
    {
        public char ch;
        public float h;
        public string wall;
        public string floor;
        public string floorAlt;
        public string ceiling;
        public Zone(char ch, float h, string wall, string floor, string floorAlt = null, string ceiling = null)
        { this.ch = ch; this.h = h; this.wall = wall; this.floor = floor; this.floorAlt = floorAlt; this.ceiling = ceiling; }
    }

    public class Opening
    {
        public int i, j;
        public Dir d;
        public OpenKind kind;
        public bool owner = true;
        public int variant;
        public Opening(int i, int j, Dir d, OpenKind kind, int variant = 0) { this.i = i; this.j = j; this.d = d; this.kind = kind; this.variant = variant; }
    }

    public class TileDef
    {
        public string roomId;
        public int r, c;
        public string[] rows;              // 9 strings, index 0 is j = 8 (north), index 8 is j = 0 (south)
        public Dictionary<char, Zone> zones = new Dictionary<char, Zone>();
        public List<Opening> openings = new List<Opening>();

        public char At(int i, int j) => rows[8 - j][i];
        public float X0 => 45f * c;
        public float Z0 => -45f * (r + 1);
        public Vector3 World(float lx, float y, float lz) => new Vector3(X0 + lx, y, Z0 + lz);
    }

    /// <summary>The hand-authored layout of each tile on its 9 x 9 grid of 5 m modules.</summary>
    public static class Layout
    {
        public const float Low = 3.75f, Std = 5f, Tall = 10f, Throne = 15f, Passage = 3.75f;

        public static Vector3 Vec(Dir d)
        {
            switch (d)
            {
                case Dir.N: return Vector3.forward;
                case Dir.E: return Vector3.right;
                case Dir.S: return Vector3.back;
                default: return Vector3.left;
            }
        }

        public static Dir Opposite(Dir d) => (Dir)(((int)d + 2) % 4);

        static TileDef T(string id, int r, int c, params string[] rows)
        {
            var t = new TileDef { roomId = id, r = r, c = c, rows = rows };
            if (rows.Length != 9) throw new System.Exception("tile " + id + " needs 9 rows");
            foreach (var row in rows) if (row.Length != 9) throw new System.Exception("tile " + id + " row '" + row + "' needs 9 chars");
            return t;
        }

        public static List<TileDef> Tiles()
        {
            var list = new List<TileDef>();

            // ---- Entry hall [3,2]: lobby with vestibule south, gate approach north, passages west and east
            var e = T("entry_hall", 3, 2,
                "###LLL###",
                "###LLL###",
                "##LLLLL##",
                "##LLLLL##",
                "wwLLLLLee",
                "##LLLLL##",
                "##LLLLL##",
                "####v####",
                "####v####");
            e.zones['L'] = new Zone('L', Std, Kit.Wall01, Kit.FloorPlain, Kit.Floors[0]);
            e.zones['v'] = new Zone('v', Passage, Kit.Wall03, Kit.FloorPlain);
            e.zones['w'] = new Zone('w', Passage, Kit.Wall03, Kit.FloorPlain);
            e.zones['e'] = new Zone('e', Passage, Kit.Wall03, Kit.FloorPlain);
            e.openings.Add(new Opening(4, 0, Dir.S, OpenKind.OuterDoor));
            e.openings.Add(new Opening(4, 1, Dir.N, OpenKind.Doorway));
            e.openings.Add(new Opening(1, 4, Dir.E, OpenKind.Doorway));
            e.openings.Add(new Opening(6, 4, Dir.E, OpenKind.Doorway));
            list.Add(e);

            // ---- Guard room [3,1]: low room, passage from the east door, dog-leg out to the prison north
            var g = T("guard_room", 3, 1,
                "####p####",
                "####ppp##",
                "##RRRRR##",
                "##RRRRRp#",
                "##RRRRRpp",
                "##RRRRR##",
                "##RRRRR##",
                "#########",
                "#########");
            g.zones['R'] = new Zone('R', Low, Kit.Wall04, Kit.Floors[3]);
            g.zones['p'] = new Zone('p', Passage, Kit.Wall03, Kit.FloorPlain);
            g.openings.Add(new Opening(7, 5, Dir.W, OpenKind.Doorway));
            g.openings.Add(new Opening(6, 6, Dir.N, OpenKind.Doorway));
            list.Add(g);

            // ---- Prison [2,1]: aisle with cells either side, warden's corner west, cross passage north
            var p = T("prison", 2, 1,
                "####n####",
                "###nnn###",
                "WWcAAAc##",
                "WWcAAAc##",
                "WWAAAAc##",
                "WWcAAAc##",
                "##cAAAc##",
                "##cAAAc##",
                "####A####");
            p.zones['A'] = new Zone('A', Low, Kit.Wall04, Kit.Floors[5]);
            p.zones['c'] = new Zone('c', 3.5f, Kit.Wall04, Kit.Floors[5]);   // each cell becomes its own zone
            p.zones['W'] = new Zone('W', Low, Kit.Wall01, Kit.Floors[3]);
            p.zones['n'] = new Zone('n', Passage, Kit.Wall03, Kit.FloorPlain);
            p.openings.Add(new Opening(1, 4, Dir.E, OpenKind.Doorway));   // warden -> aisle
            p.openings.Add(new Opening(5, 6, Dir.N, OpenKind.Doorway));   // aisle -> cross passage
            // cell fronts face the aisle: west column i = 2 faces east, east column i = 6 faces west
            int v = 0;
            foreach (int j in new[] { 1, 2, 3, 5, 6 }) p.openings.Add(new Opening(2, j, Dir.E, OpenKind.Bars, v++));
            foreach (int j in new[] { 1, 2, 3, 4, 5, 6 }) p.openings.Add(new Opening(6, j, Dir.W, OpenKind.Bars, v++));
            list.Add(p);

            // ---- Vault [2,0]: a small sealed chamber at the tile's east edge; the rest is rock
            var vt = T("vault", 2, 0,
                "#########",
                "#########",
                "#########",
                "######VVV",
                "######VVV",
                "######VVV",
                "#########",
                "#########",
                "#########");
            vt.zones['V'] = new Zone('V', 3.5f, Kit.Wall04, Kit.Floors[8]);
            list.Add(vt);

            // ---- Kitchen [1,1]: low service room, hearth on the north wall, dog-legs to prison and hall
            var k = T("kitchen", 1, 1,
                "#########",
                "#########",
                "##KKKKK##",
                "##KKKKKe#",
                "##KKKKKee",
                "##KKKKK##",
                "####ss###",
                "####s####",
                "####s####");
            k.zones['K'] = new Zone('K', 4f, Kit.Wall01, Kit.Floors[1]);
            k.zones['s'] = new Zone('s', Passage, Kit.Wall03, Kit.FloorPlain);
            k.zones['e'] = new Zone('e', Passage, Kit.Wall03, Kit.FloorPlain);
            k.openings.Add(new Opening(5, 2, Dir.N, OpenKind.Doorway));
            k.openings.Add(new Opening(6, 5, Dir.E, OpenKind.Doorway));
            k.openings.Add(new Opening(4, 6, Dir.N, OpenKind.Fireplace));
            list.Add(k);

            // ---- Great hall [1,2]: tall hall, gallery deck over the south row, landings and stair shafts at the ends
            var d = T("great_hall", 1, 2,
                "#HHHHHHH#",
                "#HHHHHHH#",
                "#HHHHHHH#",
                "#HHHHHHH#",
                "lHHHHHHHr",
                "lHHHHHHHr",
                "lHHHHHHHr",
                "lHHHHHHHr",
                "#uuuuuuu#");
            d.zones['H'] = new Zone('H', Tall, Kit.Wall01, Kit.FloorOrnate, Kit.Floors[0]);
            d.zones['u'] = new Zone('u', Std, Kit.Wall01, Kit.FloorPlain);
            d.zones['l'] = new Zone('l', Tall, Kit.Wall01, Kit.FloorPlain);
            d.zones['r'] = new Zone('r', Tall, Kit.Wall01, Kit.FloorPlain);
            d.openings.Add(new Opening(0, 4, Dir.E, OpenKind.Arch));
            d.openings.Add(new Opening(8, 4, Dir.W, OpenKind.Arch));
            for (int i = 1; i <= 7; i++) d.openings.Add(new Opening(i, 0, Dir.N, OpenKind.Arcade));
            d.openings.Add(new Opening(0, 1, Dir.S, OpenKind.StairTop));
            d.openings.Add(new Opening(8, 1, Dir.S, OpenKind.StairTop));
            list.Add(d);

            // ---- Armory [2,2]: racks between the north door (from the hall) and the gate bay south
            var a = T("armory", 2, 2,
                "####n####",
                "###nn####",
                "##RRRRR##",
                "##RRRRR##",
                "##RRRRR##",
                "##RRRRR##",
                "##RRRRR##",
                "##RRRRR##",
                "###RRR###");
            a.zones['R'] = new Zone('R', Std, Kit.Wall01, Kit.Floors[6]);
            a.zones['n'] = new Zone('n', Passage, Kit.Wall03, Kit.FloorPlain);
            a.openings.Add(new Opening(3, 7, Dir.S, OpenKind.Doorway));
            list.Add(a);

            // ---- Throne room [0,2]: 15 m hall, antechamber that turns, stage at the north end
            var t = T("throne_room", 0, 2,
                "##TTTTT##",
                "##TTTTT##",
                "##TTTTT##",
                "##TTTTT##",
                "##TTTTT##",
                "##TTTTT##",
                "######a##",
                "####aaa##",
                "####a####");
            t.zones['T'] = new Zone('T', Throne, Kit.Wall01, Kit.FloorOrnate, Kit.Floors[9]);
            t.zones['a'] = new Zone('a', Std, Kit.Wall03, Kit.FloorPlain);
            t.openings.Add(new Opening(6, 2, Dir.N, OpenKind.Arch));
            list.Add(t);

            // ---- Library [3,3]: stacks, west passage from the entry hall, north dog-leg to the study
            var l = T("library", 3, 3,
                "####n####",
                "###nn####",
                "##LLLLL##",
                "#wLLLLL##",
                "wwLLLLL##",
                "##LLLLL##",
                "##LLLLL##",
                "#########",
                "#########");
            l.zones['L'] = new Zone('L', Std, Kit.Wall01, Kit.Floors[2]);
            l.zones['w'] = new Zone('w', Passage, Kit.Wall03, Kit.FloorPlain);
            l.zones['n'] = new Zone('n', Passage, Kit.Wall03, Kit.FloorPlain);
            l.openings.Add(new Opening(1, 5, Dir.E, OpenKind.Doorway));
            l.openings.Add(new Opening(3, 7, Dir.S, OpenKind.Doorway));
            list.Add(l);

            // ---- Study [2,3]: between the library and the forge, both doors through dog-legs
            var s = T("study", 2, 3,
                "####n####",
                "####nn###",
                "##SSSSS##",
                "##SSSSS##",
                "##SSSSS##",
                "##SSSSS##",
                "##SSSSS##",
                "###ss####",
                "####s####");
            s.zones['S'] = new Zone('S', Std, Kit.Wall01, Kit.Floors[4]);
            s.zones['s'] = new Zone('s', Passage, Kit.Wall03, Kit.FloorPlain);
            s.zones['n'] = new Zone('n', Passage, Kit.Wall03, Kit.FloorPlain);
            s.openings.Add(new Opening(3, 1, Dir.N, OpenKind.Doorway));
            s.openings.Add(new Opening(5, 7, Dir.S, OpenKind.Doorway));
            list.Add(s);

            // ---- Forge [1,3]: hearth on the north wall, south door from the study, west door to the hall
            var f = T("forge", 1, 3,
                "#########",
                "#########",
                "##FFFFF##",
                "#wFFFFF##",
                "wwFFFFF##",
                "##FFFFF##",
                "##FFFFF##",
                "###ss####",
                "####s####");
            f.zones['F'] = new Zone('F', Std, Kit.Wall01, Kit.Floors[7]);
            f.zones['s'] = new Zone('s', Passage, Kit.Wall03, Kit.FloorPlain);
            f.zones['w'] = new Zone('w', Passage, Kit.Wall03, Kit.FloorPlain);
            f.openings.Add(new Opening(3, 1, Dir.N, OpenKind.Doorway));
            f.openings.Add(new Opening(1, 5, Dir.E, OpenKind.Doorway));
            list.Add(f);

            return list;
        }
    }
}
