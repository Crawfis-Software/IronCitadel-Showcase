// IronCitadel conformance kit: conformance.md and a top-down NavMesh map (conformance-map.png).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace IronCitadel.Conformance
{
    public static class ReportWriter
    {
        // ---------------------------------------------------------------------------------------------
        // markdown

        public static string Markdown(JObj report, Scorer scorer, LevelSpec L)
        {
            var sb = new StringBuilder();
            var sum = (JObj)report["summary"];
            sb.Append("# Conformance: ").Append(Path.GetFileNameWithoutExtension((string)report["scene"])).Append("\n\n");
            sb.Append("- Scene: `").Append(report["scene"]).Append("`\n");
            sb.Append("- Level: ").Append(L.Name).Append(" (`").Append(L.Path).Append("`)\n");
            sb.Append("- Run: ").Append(report["run_utc"]).Append(", Unity ").Append(report["unity"]).Append("\n");
            var bake = (JObj)report["bake"];
            sb.Append("- Bake: ").Append(bake["triangles"]).Append(" NavMesh triangles from ").Append(bake["sources"])
              .Append(" colliders in ").Append(MiniJson.Num(Convert.ToDouble(bake["seconds"]))).Append(" s; agent r ")
              .Append(Tune.AgentRadius.ToString(CultureInfo.InvariantCulture)).Append(", h ").Append(Tune.AgentHeight.ToString(CultureInfo.InvariantCulture))
              .Append(", step ").Append(Tune.AgentClimb.ToString(CultureInfo.InvariantCulture)).Append(", slope ").Append(Tune.AgentSlope.ToString(CultureInfo.InvariantCulture))
              .Append(", skin ").Append(Tune.WalkSkin.ToString(CultureInfo.InvariantCulture)).Append(" (bake and walk)\n");
            var st = (JObj)report["start"];
            sb.Append("- Start: ").Append(st["source"]).Append(", at ").Append(Vec(st["position"])).Append(" in ").Append(st["node"]).Append("\n\n");
            sb.Append("**Result: ").Append(sum["pass"]).Append(" pass, ").Append(sum["fail"]).Append(" fail, ")
              .Append(sum["manual"]).Append(" manual.**\n\n");

            sb.Append("| # | must_be_true | result | measured | reason |\n|---|---|---|---|---|\n");
            foreach (var r in scorer.Rows)
            {
                sb.Append("| ").Append(r.Id).Append(" | ").Append(Cell(r.MustBeTrue)).Append(" | **")
                  .Append(r.Status.ToUpperInvariant()).Append("** | ").Append(Cell(r.Short)).Append(" | ")
                  .Append(Cell(r.Reason)).Append(" |\n");
            }

            sb.Append("\n## Opening graph\n\n| between | cells | kind | via | shared edge (m) | gaps | crossing | off edge centre (m) |\n|---|---|---|---|---|---|---|---|\n");
            foreach (JObj o in (IList)report["openings"])
            {
                var b = (IList)o["between"]; var c = (IList)o["cells"];
                sb.Append("| ").Append(b[0]).Append(" - ").Append(b[1]).Append(" | ").Append(c[0]).Append(" ").Append(c[1])
                  .Append(" | ").Append(o["kind"]).Append(" | ").Append(o["via"]).Append(" | ")
                  .Append(MiniJson.Num(Convert.ToDouble(o["shared_edge_m"]))).Append(" | ").Append(o["gaps"]).Append(" | ")
                  .Append(Vec(o["crossing"])).Append(" | ")
                  .Append(o["offset_from_edge_centre_m"] != null ? MiniJson.Num(Convert.ToDouble(o["offset_from_edge_centre_m"])) : "")
                  .Append(" |\n");
            }

            if (scorer.Walks.Count > 0)
            {
                sb.Append("\n## Walks\n\n| route | result | walked / planned (m) | note |\n|---|---|---|---|\n");
                foreach (var w in scorer.Walks)
                    sb.Append("| ").Append(w.Route).Append(" | **").Append(w.Pass ? "PASS" : "FAIL").Append("** | ")
                      .Append(MiniJson.Num(w.Walked)).Append(" / ").Append(MiniJson.Num(w.Planned)).Append(" | ")
                      .Append(Cell(w.Note)).Append(" |\n");
            }

            if (scorer.Notes.Count > 0)
            {
                sb.Append("\n## Notes\n\n");
                foreach (var n in scorer.Notes) sb.Append("- ").Append(n).Append("\n");
            }

            sb.Append("\n## Map\n\n`conformance-map.png`: north is up, 4 px per metre. Green is floor-level NavMesh, olive raised NavMesh; ")
              .Append("orange, cyan and yellow are the samples checks 8 and 7 found as the stage, the gallery deck and the climbs; ")
              .Append("grey-blue is NavMesh that is not level space ")
              .Append("(roofs, wall tops, table tops). White dots are listed doors, red dots openings that should not ")
              .Append("exist, the blue dot is the start, pink lines are the walks and red crosses their snags.\n");

            sb.Append("\n## Thresholds (Tune, top of ConformanceChecks.cs)\n\n");
            foreach (var f in typeof(Tune).GetFields(BindingFlags.Public | BindingFlags.Static))
                sb.Append("- `").Append(f.Name).Append("` = ").Append(Convert.ToString(f.GetValue(null), CultureInfo.InvariantCulture)).Append("\n");
            sb.Append("\nFull measurements are in `conformance.json`.\n");
            return sb.ToString();
        }

        static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");

        static string Vec(object o)
        {
            if (o is Vector3 v) return "(" + MiniJson.Num(v.x) + ", " + MiniJson.Num(v.y) + ", " + MiniJson.Num(v.z) + ")";
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------------------------------------
        // map

        const float Px = 4f;      // pixels per metre
        const float Margin = 12f; // metres around the grid

        public static void Map(string path, LevelSpec L, NavModel m, Scorer scorer)
        {
            if (m == null) return;
            float x0 = -Margin, z0 = -(L.Rows * L.Tile + Margin);
            int W = Mathf.CeilToInt((L.Cols * L.Tile + 2 * Margin) * Px), H = Mathf.CeilToInt((L.Rows * L.Tile + 2 * Margin) * Px);
            var buf = new Color32[W * H];
            for (int py = 0; py < H; py++)
            for (int px = 0; px < W; px++)
            {
                float x = x0 + (px + 0.5f) / Px, z = z0 + (py + 0.5f) / Px;
                var c = L.CellOf(new Vector3(x, 0, z));
                Color32 col = !L.InGrid(c) ? new Color32(12, 12, 14, 255) : L.IsRock(c) ? new Color32(48, 44, 40, 255) : new Color32(26, 26, 32, 255);
                buf[py * W + px] = col;
            }

            // triangles: polygon-level, so their heights are only a guide; the stage, deck and climbs are
            // painted on top from the scorer's height-accurate samples
            var order = Enumerable.Range(0, m.T).OrderBy(t => (m.IsLevel(t) ? 1000f : 0f) + m.Cen[t].y).ToList();
            foreach (var t in order)
            {
                float y = m.Cen[t].y - L.FloorY;
                Color32 col;
                if (!m.IsLevel(t)) col = new Color32(70, 72, 100, 255);
                else if (y < Tune.FloorBandHigh) col = new Color32(58, 140, 78, 255);
                else col = new Color32(96, 112, 92, 255);
                FillTri(buf, W, H, x0, z0, m.V[m.I[3 * t]], m.V[m.I[3 * t + 1]], m.V[m.I[3 * t + 2]], col);
            }
            int half = Mathf.Max(1, Mathf.RoundToInt(Tune.SampleStep * Px * 0.5f));
            foreach (var p in scorer.ClimbSamples) Square(buf, W, H, x0, z0, p, half, new Color32(225, 205, 60, 255));
            foreach (var p in scorer.DeckSamples) Square(buf, W, H, x0, z0, p, half, new Color32(60, 200, 225, 255));
            foreach (var p in scorer.StageSamples) Square(buf, W, H, x0, z0, p, half, new Color32(225, 140, 40, 255));

            var grid = new Color32(130, 130, 140, 255);
            for (int k = 0; k <= L.Cols; k++) Line(buf, W, H, x0, z0, new Vector3(k * L.Tile, 0, 0), new Vector3(k * L.Tile, 0, -L.Rows * L.Tile), grid);
            for (int k = 0; k <= L.Rows; k++) Line(buf, W, H, x0, z0, new Vector3(0, 0, -k * L.Tile), new Vector3(L.Cols * L.Tile, 0, -k * L.Tile), grid);

            for (int r = 0; r < L.Rows; r++)
            for (int c = 0; c < L.Cols; c++)
            {
                char ch = L.Grid[r][c];
                Text(buf, W, H, x0, z0, new Vector3(c * L.Tile + 2f, 0, -r * L.Tile - 2f), ch.ToString(), 3, new Color32(235, 235, 235, 255));
            }

            foreach (var w in scorer.Walks)
            {
                for (int i = 1; i < w.Trace.Count; i++) Line(buf, W, H, x0, z0, w.Trace[i - 1], w.Trace[i], new Color32(255, 150, 200, 255));
                foreach (var s in w.Snags) Cross(buf, W, H, x0, z0, s.At, 7, new Color32(255, 40, 40, 255));
            }

            foreach (var o in m.Openings.Values)
            {
                bool listed = false;
                foreach (var d in L.Doors)
                {
                    bool same = d.IsOutside
                        ? (o.A == d.A && o.B == NavModel.Out) || (o.B == d.A && o.A == NavModel.Out)
                        : NavModel.Key(d.A, d.B.Value) == NavModel.Key(o.A, o.B);
                    if (same && d.Passable && !d.IsSecret) listed = true;
                }
                Disc(buf, W, H, x0, z0, o.Crossing, listed ? 4 : 6, listed ? new Color32(250, 250, 250, 255) : new Color32(255, 40, 40, 255));
            }
            var start = scorer.StartPosition;
            Disc(buf, W, H, x0, z0, start, 6, new Color32(60, 120, 255, 255));

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(buf);
                tex.Apply(false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

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
            ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
            ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
            ['G'] = new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " },
            ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
            ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
            ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
            ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
            ['#'] = new[] { " # # ", " # # ", "#####", " # # ", "#####", " # # ", " # # " },
        };

        static void Text(Color32[] buf, int W, int H, float x0, float z0, Vector3 topLeft, string s, int scale, Color32 col)
        {
            var o = ToPx(x0, z0, topLeft);
            int cx = (int)o.x;
            foreach (var ch in s)
            {
                if (Font.TryGetValue(char.ToUpperInvariant(ch), out var g))
                    for (int row = 0; row < 7; row++)
                    for (int colI = 0; colI < 5; colI++)
                    {
                        if (g[row][colI] != '#') continue;
                        for (int sy = 0; sy < scale; sy++)
                        for (int sx = 0; sx < scale; sx++)
                            Put(buf, W, H, cx + colI * scale + sx, (int)o.y - row * scale - sy, col);
                    }
                cx += 6 * scale;
            }
        }
    }
}
