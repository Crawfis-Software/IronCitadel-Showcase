// IronCitadel conformance kit v2: conformance.md and a top-down map (conformance-map.png) drawn from the map's
// outlines and the all-doors-opened NavMesh.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IronCitadel.ConformanceV2
{
    public static class ReportWriter
    {
        // ---------------------------------------------------------------------------------------------
        // markdown

        public static string Markdown(JObj report, Scorer scorer)
        {
            var map = scorer.MapSpec;
            var sb = new StringBuilder();
            var sum = (JObj)report["summary"];
            sb.Append("# Conformance v2: ").Append(Path.GetFileNameWithoutExtension((string)report["scene"])).Append("\n\n");
            sb.Append("- Scene: `").Append(report["scene"]).Append("`\n");
            sb.Append("- Map: `").Append(report["map"]).Append("`\n");
            sb.Append("- Run: ").Append(report["run_utc"]).Append(", Unity ").Append(report["unity"]).Append(", ").Append(report["tool"]).Append("\n");
            sb.Append("- Agent (bake and walk): r ").Append(F(Tune.AgentRadius)).Append(", h ").Append(F(Tune.AgentHeight))
              .Append(", step ").Append(F(Tune.AgentClimb)).Append(", slope ").Append(F(Tune.AgentSlope)).Append(", skin ").Append(F(Tune.WalkSkin)).Append("\n");
            foreach (JObj b in (IList)report["bakes"])
                sb.Append("- Bake \"").Append(b["label"]).Append("\": ").Append(b["triangles"]).Append(" triangles from ").Append(b["sources"])
                  .Append(" colliders and ").Append(b["modifier_boxes"]).Append(" modifier boxes, ").Append(b["gaps"]).Append(" crossings, ")
                  .Append(MiniJson.Num(Convert.ToDouble(b["seconds"]))).Append(" s\n");
            var st = (JObj)report["start"];
            sb.Append("- Start: ").Append(st["source"]).Append(", at ").Append(Vec(st["position"])).Append(" in ").Append(st["room"]);
            if (st["scene_start"] != null) sb.Append("; the scene's own start: ").Append(st["scene_start_source"]).Append(" at ").Append(Vec(st["scene_start"]));
            sb.Append("\n- Enemy bodies switched off for sight lines and the bake: ").Append(report["enemy_bodies_switched_off"]).Append("\n");
            sb.Append("- NavMesh obstacles switched off (their carving would cut the kit's bake): ").Append(report["navmesh_obstacles_switched_off"]).Append("\n");
            sb.Append("- Other Interact() receivers opened for the all-doors bake: ").Append(scorer.OtherDoors.Count);
            if (scorer.OtherDoors.Count > 0) sb.Append(" (").Append(Cell(string.Join("; ", scorer.OtherDoors.Take(12)))).Append(scorer.OtherDoors.Count > 12 ? "; ..." : "").Append(")");
            if (map.FoldedIntoGallery.Count > 0)
                sb.Append("\n- Map rooms that are the gallery deck, counted as the gallery (the dining hall's): ").Append(Cell(string.Join(", ", map.FoldedIntoGallery)));
            sb.Append("\n\n");
            sb.Append("**Result: ").Append(sum["pass"]).Append(" pass, ").Append(sum["fail"]).Append(" fail, ")
              .Append(sum["manual"]).Append(" manual.**\n\n");

            sb.Append("| # | must be true | result | measured | reason |\n|---|---|---|---|---|\n");
            foreach (var r in scorer.Rows)
                sb.Append("| ").Append(r.Id).Append(" | ").Append(Cell(r.MustBeTrue)).Append(" | **").Append(r.Status.ToUpperInvariant())
                  .Append("** | ").Append(Cell(r.Short)).Append(" | ").Append(Cell(r.Reason)).Append(" |\n");

            sb.Append("\n## Information (not scored)\n\n| # | what | measured | how |\n|---|---|---|---|\n");
            foreach (var r in scorer.Info)
                sb.Append("| ").Append(r.Id).Append(" | ").Append(Cell(r.MustBeTrue)).Append(" | ").Append(Cell(r.Short)).Append(" | ").Append(Cell(r.Reason)).Append(" |\n");

            sb.Append("\n## Room graph (every door opened)\n\n| between | via | centre | NavMesh width (m) | listed door | role |\n|---|---|---|---|---|---|\n");
            foreach (JObj o in (IList)report["room_graph"])
            {
                var b = (IList)o["between"];
                object door = o["door"];
                sb.Append("| ").Append(b[0]).Append(" - ").Append(b[1]).Append(" | ").Append(o["via"]).Append(" | ").Append(Vec(o["centre"]))
                  .Append(" | ").Append(MiniJson.Num(Convert.ToDouble(o["navmesh_width_m"]))).Append(" | ")
                  .Append(door != null ? "#" + door + " (" + o["door_state"] + ")" : "").Append(" | ").Append(o["role"]).Append(" |\n");
            }

            if (scorer.Walks.Count > 0)
            {
                sb.Append("\n## Walks\n\n| route | result | walked / planned (m) | rooms planned | note |\n|---|---|---|---|---|\n");
                foreach (var w in scorer.Walks)
                    sb.Append("| ").Append(w.Route).Append(" | **").Append(w.Pass ? "PASS" : "FAIL").Append("** | ")
                      .Append(MiniJson.Num(w.Walked)).Append(" / ").Append(MiniJson.Num(w.Planned)).Append(" | ")
                      .Append(Cell(string.Join(" > ", w.Rooms))).Append(" | ").Append(Cell(w.Note)).Append(" |\n");
            }

            if (scorer.Notes.Count > 0)
            {
                sb.Append("\n## Notes\n\n");
                foreach (var n in scorer.Notes) sb.Append("- ").Append(n).Append("\n");
            }
            if (map.Problems.Count > 0)
            {
                sb.Append("\n## Problems reading the map\n\n");
                foreach (var n in map.Problems) sb.Append("- ").Append(n).Append("\n");
            }

            sb.Append("\n## Rooms (numbers on the map)\n\n| # | name | kind | state | floor_y | outline m2 |\n|---|---|---|---|---|---|\n");
            foreach (var r in map.Rooms)
                sb.Append("| ").Append(r.Index + 1).Append(" | ").Append(Cell(r.Name)).Append(" | ").Append(Cell(r.Kind)).Append(" | ").Append(Cell(r.State))
                  .Append(" | ").Append(MiniJson.Num(r.FloorY)).Append(" | ").Append(MiniJson.Num(r.Area)).Append(" |\n");

            sb.Append("\n## Map\n\n`conformance-map.png`: north (+z) is up, ").Append(F(Px)).Append(" px per metre. The NavMesh is the all-doors-opened bake: ")
              .Append("green is floor-level NavMesh in a room, olive raised NavMesh (the gallery, the stage, furniture tops in a room's band), ")
              .Append("teal level NavMesh outside every outline (doorways in the walls' thickness), grey-blue NavMesh that is not level space. ")
              .Append("White lines are the map's room outlines, numbered as above; cyan the gallery deck and its rail, with the rail eyes as cyan dots; ")
              .Append("orange the stage outline and the throne. Dark red squares are hall-floor samples no rail eye sees. ")
              .Append("Doors: white dots are listed doors that are openings, yellow listed doors that are not, red openings no door lists. ")
              .Append("The blue dot is the start, red dots enemies, gold dots pickups; violet squares are tall occluders, light blue low ones. ")
              .Append("Pink lines are the walks, red crosses their snags; small red crosses are entry-hall samples that see an armory enemy from more than ")
              .Append(F(Tune.ChokeSightBack)).Append(" m.\n");

            sb.Append("\n## Thresholds (Tune.cs)\n\n");
            foreach (var f in typeof(Tune).GetFields(BindingFlags.Public | BindingFlags.Static))
                sb.Append("- `").Append(f.Name).Append("` = ").Append(Convert.ToString(f.GetValue(null), CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("\nFull measurements are in `conformance.json`.\n");
            return sb.ToString();
        }

        static string F(float f) => f.ToString(CultureInfo.InvariantCulture);
        static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");

        static string Vec(object o)
        {
            if (o is Vector3 v) return "(" + MiniJson.Num(v.x) + ", " + MiniJson.Num(v.y) + ", " + MiniJson.Num(v.z) + ")";
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------------------------------------
        // map

        const float Px = 4f;      // pixels per metre
        const float Margin = 6f;  // metres round the map's outlines

        public static void Map(string path, Scorer scorer)
        {
            var map = scorer.MapSpec;
            var m = scorer.Model;
            float x0 = map.XMin - Margin, z0 = map.ZMin - Margin;
            int W = Mathf.CeilToInt((map.XMax - map.XMin + 2 * Margin) * Px), H = Mathf.CeilToInt((map.ZMax - map.ZMin + 2 * Margin) * Px);
            var buf = new Color32[W * H];
            var bg = new Color32(16, 16, 20, 255);
            for (int i = 0; i < buf.Length; i++) buf[i] = bg;

            if (m != null)
            {
                var order = Enumerable.Range(0, m.T).OrderBy(t => (m.IsLevel(t) ? 1000f : 0f) + m.Cen[t].y).ToList();
                foreach (var t in order)
                {
                    int n = m.Node[t];
                    Color32 col;
                    if (!m.IsLevel(t)) col = new Color32(70, 72, 100, 255);
                    else if (n >= 0 && n < map.Rooms.Count)
                        col = m.Cen[t].y - map.Rooms[n].FloorY <= Tune.FloorBandHigh ? new Color32(58, 140, 78, 255) : new Color32(110, 118, 70, 255);
                    else if (n == map.GalleryNode) col = new Color32(110, 118, 70, 255);
                    else col = new Color32(40, 120, 120, 255);
                    FillTri(buf, W, H, x0, z0, m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]], col);
                }
            }

            foreach (var p in scorer.HallFloorHidden) Square(buf, W, H, x0, z0, p, 2, new Color32(150, 30, 30, 255));

            var white = new Color32(235, 235, 235, 255);
            foreach (var r in map.Rooms)
                foreach (var poly in r.Floor) Outline(buf, W, H, x0, z0, poly, white);
            var cyan = new Color32(60, 210, 235, 255);
            foreach (var d in map.Deck) Outline(buf, W, H, x0, z0, d, cyan);
            for (int i = 1; i < map.Rail.Count; i++)
            {
                var a = new Vector3(map.Rail[i - 1].x, 0, map.Rail[i - 1].y); var b = new Vector3(map.Rail[i].x, 0, map.Rail[i].y);
                Line(buf, W, H, x0, z0, a, b, cyan);
                Line(buf, W, H, x0, z0, a + new Vector3(0.25f, 0, 0.25f), b + new Vector3(0.25f, 0, 0.25f), cyan);
            }
            foreach (var e in scorer.DeckEyes) Disc(buf, W, H, x0, z0, e, 2, cyan);
            var orange = new Color32(240, 150, 40, 255);
            if (map.Stage != null) Outline(buf, W, H, x0, z0, map.Stage, orange);
            if (map.Throne != null) Disc(buf, W, H, x0, z0, map.Throne.Value, 5, orange);

            foreach (var c in scorer.Covers)
                Square(buf, W, H, x0, z0, c.p, 3, c.tall ? new Color32(170, 90, 255, 255) : new Color32(140, 210, 255, 255));

            foreach (var w in scorer.Walks)
            {
                for (int i = 1; i < w.Trace.Count; i++) Line(buf, W, H, x0, z0, w.Trace[i - 1], w.Trace[i], new Color32(255, 150, 200, 255));
                foreach (var s in w.Snags) Cross(buf, W, H, x0, z0, s.At, 7, new Color32(255, 40, 40, 255));
            }
            foreach (var p in scorer.ChokeViolations) Cross(buf, W, H, x0, z0, p, 3, new Color32(255, 60, 60, 255));

            foreach (var d in scorer.DoorDots)
                Disc(buf, W, H, x0, z0, d.p, d.kind == 0 ? 4 : 6,
                    d.kind == 0 ? new Color32(250, 250, 250, 255) : d.kind == 1 ? new Color32(250, 220, 40, 255) : new Color32(255, 40, 40, 255));
            foreach (var e in map.Enemies) Disc(buf, W, H, x0, z0, e.Position, 4, new Color32(230, 50, 50, 255));
            foreach (var p in map.Pickups) Disc(buf, W, H, x0, z0, p.Position, 5, new Color32(250, 200, 40, 255));
            Disc(buf, W, H, x0, z0, scorer.StartPosition, 6, new Color32(60, 120, 255, 255));

            foreach (var r in map.Rooms)
            {
                if (r.Floor.Count == 0) continue;
                var c = r.Centroid;
                var label = (r.Index + 1).ToString(CultureInfo.InvariantCulture);
                int scale = 3;
                float wM = label.Length * 6 * scale / Px, hM = 7 * scale / Px;
                Text(buf, W, H, x0, z0, new Vector3(c.x - wM * 0.5f, 0, c.y + hM * 0.5f), label, scale, white);
            }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(buf);
                tex.Apply(false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static void Outline(Color32[] buf, int W, int H, float x0, float z0, Poly p, Color32 col)
        {
            for (int i = 0, j = p.P.Length - 1; i < p.P.Length; j = i++)
                Line(buf, W, H, x0, z0, new Vector3(p.P[j].x, 0, p.P[j].y), new Vector3(p.P[i].x, 0, p.P[i].y), col);
        }

        // (v1) the drawing helpers

        static void Square(Color32[] buf, int W, int H, float x0, float z0, Vector3 p, int half, Color32 c)
        {
            var q = ToPx(x0, z0, p);
            int cx = Mathf.FloorToInt(q.x), cy = Mathf.FloorToInt(q.y);
            for (int dy = -half; dy < half; dy++)
            for (int dx = -half; dx < half; dx++) Put(buf, W, H, cx + dx, cy + dy, c);
        }

        static void Put(Color32[] buf, int W, int H, int px, int py, Color32 c)
        {
            if (px < 0 || py < 0 || px >= W || py >= H) return;
            buf[py * W + px] = c;
        }

        static Vector2 ToPx(float x0, float z0, Vector3 p) => new Vector2((p.x - x0) * Px, (p.z - z0) * Px);

        static void FillTri(Color32[] buf, int W, int H, float x0, float z0, Vector3 a, Vector3 b, Vector3 c, Color32 col)
        {
            var A = ToPx(x0, z0, a); var B = ToPx(x0, z0, b); var C = ToPx(x0, z0, c);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))));
            int maxX = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))));
            int maxY = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));
            float area = Edge(A, B, C);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var P = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = Edge(B, C, P), w1 = Edge(C, A, P), w2 = Edge(A, B, P);
                if (area > 0 ? (w0 >= 0 && w1 >= 0 && w2 >= 0) : (w0 <= 0 && w1 <= 0 && w2 <= 0)) buf[y * W + x] = col;
            }
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        static void Line(Color32[] buf, int W, int H, float x0, float z0, Vector3 a, Vector3 b, Color32 col)
        {
            var A = ToPx(x0, z0, a); var B = ToPx(x0, z0, b);
            int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(B.x - A.x), Mathf.Abs(B.y - A.y))) + 1;
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(A, B, i / (float)n);
                Put(buf, W, H, (int)p.x, (int)p.y, col);
            }
        }

        static void Disc(Color32[] buf, int W, int H, float x0, float z0, Vector3 at, int r, Color32 col)
        {
            var c = ToPx(x0, z0, at);
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r) continue;
                bool rim = x * x + y * y > (r - 1.5f) * (r - 1.5f);
                Put(buf, W, H, (int)c.x + x, (int)c.y + y, rim ? new Color32(0, 0, 0, 255) : col);
            }
        }

        static void Cross(Color32[] buf, int W, int H, float x0, float z0, Vector3 at, int r, Color32 col)
        {
            var c = ToPx(x0, z0, at);
            for (int k = -r; k <= r; k++)
            for (int t = -1; t <= 1; t++)
            {
                Put(buf, W, H, (int)c.x + k + t, (int)c.y + k, col);
                Put(buf, W, H, (int)c.x + k + t, (int)c.y - k, col);
            }
        }

        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
            ['3'] = new[] { "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### " },
            ['4'] = new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
            ['5'] = new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            ['6'] = new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
            ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
            ['8'] = new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            ['9'] = new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
        };

        static void Text(Color32[] buf, int W, int H, float x0, float z0, Vector3 topLeft, string s, int scale, Color32 col)
        {
            var o = ToPx(x0, z0, topLeft);
            int cx = (int)o.x;
            var shadow = new Color32(0, 0, 0, 255);
            foreach (var ch in s)
            {
                if (Font.TryGetValue(ch, out var g))
                    for (int pass = 0; pass < 2; pass++)
                    for (int row = 0; row < 7; row++)
                    for (int colI = 0; colI < 5; colI++)
                    {
                        if (g[row][colI] != '#') continue;
                        for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                            Put(buf, W, H, cx + colI * scale + sx + (pass == 0 ? 1 : 0), (int)o.y - row * scale - sy - (pass == 0 ? 1 : 0), pass == 0 ? shadow : col);
                    }
                cx += 6 * scale;
            }
        }
    }
}
