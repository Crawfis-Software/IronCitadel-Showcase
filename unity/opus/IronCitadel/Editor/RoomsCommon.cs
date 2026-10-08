using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static partial class CitadelBuilder
    {
        static readonly Rect Full = Rect.MinMaxRect(2.5f, 2.5f, 42.5f, 42.5f);

        /// <summary>Floor, four walls (doors cut), ceiling; registers the open area for the overview cap.</summary>
        static void Shell(Room rm, Rect r, bool ceiling = true)
        {
            Arch_.Floor(rm, r);
            Arch_.Box4(rm, r, rm.H);
            if (ceiling) Arch_.Ceiling(rm, r, rm.H);
            rm.Open.Add(r);
        }

        static Vector2 V(float u, float v) => new Vector2(u, v);

        /// <summary>True if a wall point (room-local) is within 'clear' metres of any door face of this room.</summary>
        static bool NearDoor(Room rm, Vector2 p, float clear)
        {
            foreach (var d in Arch_.Doors)
            {
                if (d.A == rm && (rm.L(d.FaceA) - p).magnitude < clear + d.Width / 2f) return true;
                if (d.B == rm && (rm.L(d.FaceB) - p).magnitude < clear + d.Width / 2f) return true;
            }
            return false;
        }

        /// <summary>Torches round the walls of a rectangle at the given spacing, skipping door openings and listed spots.</summary>
        static void WallTorches(Room rm, Rect r, float spacing, float y, float intensity, float range, string key = null, IList<Vector2> avoid = null, float offset = 0f)
        {
            key = key ?? (rm.St == Style.Dwarf ? "R:SM_Prop_Dwarf_Torch_01" : "D:SM_Prop_Torch_Ornate_02");
            var walls = new (Vector2 a, Vector2 b, Vector2 n)[]
            {
                (V(r.xMin, r.yMin), V(r.xMax, r.yMin), Vector2.up),
                (V(r.xMin, r.yMax), V(r.xMax, r.yMax), Vector2.down),
                (V(r.xMin, r.yMin), V(r.xMin, r.yMax), Vector2.right),
                (V(r.xMax, r.yMin), V(r.xMax, r.yMax), Vector2.left),
            };
            foreach (var w in walls)
            {
                float len = (w.b - w.a).magnitude;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    var p = Vector2.Lerp(w.a, w.b, t) + (w.b - w.a).normalized * offset;
                    if (NearDoor(rm, p, 1.2f)) continue;
                    bool skip = false;
                    if (avoid != null) foreach (var q in avoid) if ((q - p).magnitude < 2.5f) skip = true;
                    if (skip) continue;
                    Arch_.Torch(rm, rm.W(p.x, p.y), new Vector3(w.n.x, 0, w.n.y), y, intensity, range, key);
                }
            }
        }

        /// <summary>A banner hung on a wall, top at 'top' metres.</summary>
        static GameObject Banner(Room rm, Vector2 wallPt, Vector2 inward, float top, string key, float scale = 1f)
        {
            var b = Kit.LocalBounds(key);
            var rot = Quaternion.LookRotation(new Vector3(inward.x, 0, inward.y), Vector3.up);
            var pos = rm.W(wallPt.x, wallPt.y, top - b.max.y * scale) + new Vector3(inward.x, 0, inward.y) * (0.06f - b.min.z * scale);
            var go = Kit.P(key, rm.Props, pos, rot, Vector3.one * scale);
            Kit.StripColliders(go);
            return go;
        }

        static GameObject Prop(Room rm, string key, float u, float v, float yaw = 0f, float scale = 1f, float y = 0f, bool collide = true)
        {
            var go = Kit.PC(key, rm.Props, rm.W(u, v, y), yaw, scale);
            if (!collide) Kit.StripColliders(go);
            return go;
        }

        /// <summary>Prop with its back against a wall at room-local point p, facing 'inward'.</summary>
        static GameObject WallProp(Room rm, string key, Vector2 p, Vector2 inward, float scale = 1f, float y = 0f, float gap = 0.05f)
        {
            return Kit.PW(key, rm.Props, rm.W(p.x, p.y, y), new Vector3(inward.x, 0, inward.y), scale, gap);
        }

        static void Pillar(Room rm, float u, float v, float h, string key = null, bool collide = true, float width = 1.1f)
        {
            key = key ?? rm.St.Pillar;
            var b = Kit.LocalBounds(key);
            int rows = Mathf.Max(1, Mathf.RoundToInt(h / b.size.y));
            float sy = h / (rows * b.size.y);
            for (int k = 0; k < rows; k++)
            {
                var go = Kit.PC(key, rm.Arch, rm.W(u, v, k * b.size.y * sy), 0f, 1f);
                go.transform.localScale = new Vector3(width, sy, width);
                Kit.StripColliders(go);
            }
            if (collide) Kit.Solid(rm.Cols, rm.W(u, v, h / 2f), new Vector3(b.size.x * width, h, b.size.z * width), "Pillar");
        }

        /// <summary>Soft unseen fill: a grid of wide, weak point lights under the ceiling, setting a room's base light level.</summary>
        static void Fill(Room rm, Rect r, float y, float intensity, float range, float spacing, Color? color = null)
        {
            int nx = Mathf.Max(1, Mathf.RoundToInt(r.width / spacing)), nz = Mathf.Max(1, Mathf.RoundToInt(r.height / spacing));
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    Kit.PointLight(rm.Lights, rm.W(r.xMin + (i + 0.5f) * r.width / nx, r.yMin + (j + 0.5f) * r.height / nz, y), color ?? new Color(1f, 0.8f, 0.6f), intensity, range, "Fill");
        }

        // shells for rooms not yet furnished
        static void PlainRoom(Room rm)
        {
            Shell(rm, Full);
            WallTorches(rm, Full, 8f, Mathf.Min(2.6f, rm.H - 0.8f), 8f, 10f);
            Kit.PointLight(rm.Lights, rm.W(22.5f, 22.5f, rm.H - 1f), Kit.Candle, 20f, 25f);
        }
    }
}
