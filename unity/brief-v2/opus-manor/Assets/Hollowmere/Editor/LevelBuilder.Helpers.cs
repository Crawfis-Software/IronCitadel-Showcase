using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        static System.Random s_Rng = new System.Random(7);
        static float Rf(float a, float b) => a + (float)s_Rng.NextDouble() * (b - a);
        static int Ri(int a, int b) => s_Rng.Next(a, b);
        static T Pick<T>(params T[] items) => items[s_Rng.Next(items.Length)];

        /// <summary>Areas each room keeps clear for the ways through (x0, z0, x1, z1).</summary>
        static readonly Dictionary<string, List<Rect>> s_Clear = new Dictionary<string, List<Rect>>();

        static void KeepClear(string room, float x0, float z0, float x1, float z1)
        {
            if (!s_Clear.TryGetValue(room, out var l)) s_Clear[room] = l = new List<Rect>();
            l.Add(Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1)));
        }

        static bool IsClear(Room r, float x, float z, float pad = 0.3f)
        {
            if (!s_Clear.TryGetValue(r.id, out var l)) return true;
            foreach (var q in l)
                if (x > q.xMin - pad && x < q.xMax + pad && z > q.yMin - pad && z < q.yMax + pad) return false;
            return true;
        }

        /// <summary>Scatters small clutter (no colliders) over a rectangle, skipping the clear areas.</summary>
        static void Scatter(Room r, string[] prefabs, float x0, float z0, float x1, float z1, int count, float tilt = 0f, float s0 = 1f, float s1 = 1f, float y = 0f)
        {
            int placed = 0, tries = 0;
            while (placed < count && tries++ < count * 20)
            {
                float x = Rf(x0, x1), z = Rf(z0, z1);
                if (!IsClear(r, x, z, 0.15f)) continue;
                var go = Put(r, Pick(prefabs), x, z, Rf(0, 360), y, Rf(s0, s1), null, null, null, false);
                if (tilt > 0f) go.transform.rotation = Quaternion.Euler(Rf(-tilt, tilt), Rf(0, 360), Rf(-tilt, tilt));
                placed++;
            }
        }

        static readonly string[] Dust = { "SM_Env_Dust_01", "SM_Env_Dust_02", "SM_Env_Dust_03", "SM_Env_Dust_04", "SM_Env_Dust_05", "SM_Env_Dust_06" };
        static readonly string[] Webs = { "SM_Env_SpiderWeb_01", "SM_Env_SpiderWeb_02", "SM_Env_SpiderWeb_03", "SM_Env_SpiderWeb_04", "SM_Env_SpiderWeb_05", "SM_Env_SpiderWeb_06" };
        static readonly string[] Papers = { "SM_Prop_Papers_01", "SM_Prop_Papers_02", "SM_Prop_Papers_03", "SM_Prop_Page_01", "SM_Prop_Page_03", "SM_Prop_Page_05" };
        static readonly string[] Books = { "SM_Prop_Book_01", "SM_Prop_Book_02", "SM_Prop_Book_03", "SM_Prop_Book_04", "SM_Prop_Book_Open_01", "SM_Prop_Book_Open_02", "SM_Prop_Book_Pile_04", "SM_Prop_Book_Pile_05" };
        static readonly string[] Rocks = { "SM_Gen_Env_Rock_01", "SM_Gen_Env_Rock_02", "SM_Gen_Env_Rock_03", "SM_Gen_Env_Rock_06", "SM_Gen_Env_Rock_07" };

        /// <summary>Dust on the floor and webs in the upper corners.</summary>
        static void Neglect(Room r, int dust, int webs)
        {
            Scatter(r, Dust, r.x0 + 0.4f, r.z0 + 0.4f, r.x1 - 0.4f, r.z1 - 0.4f, dust, 0f, 0.8f, 1.6f);
            var corners = new[] { (r.x0, r.z0, 45f), (r.x1, r.z0, -45f), (r.x0, r.z1, 135f), (r.x1, r.z1, -135f) };
            for (int i = 0; i < webs; i++)
            {
                var c = corners[i % 4];
                float inset = 0.05f;
                float x = c.Item1 + (c.Item1 < r.cx ? inset : -inset), z = c.Item2 + (c.Item2 < r.cz ? inset : -inset);
                var go = Put(r, Pick(Webs), x, z, c.Item3, r.h - 0.05f - Rf(0f, 0.3f), Rf(0.8f, 1.3f), null, null, null, false);
                go.transform.rotation = Quaternion.Euler(0f, c.Item3 + 180f, 0f);
            }
        }

        /// <summary>Lays an object on its side (overturned) about its local Z or X axis.</summary>
        static GameObject Topple(GameObject go, float aroundZ, float aroundX = 0f, float lift = 0f)
        {
            var t = go.transform;
            t.rotation = t.rotation * Quaternion.Euler(aroundX, 0f, aroundZ);
            // rest on the floor
            var b = Bounds(go);
            float floor = t.position.y - (t.position.y - b.min.y);
            t.position += Vector3.up * (FloorUnder(go) - b.min.y + lift);
            return go;
        }

        static float FloorUnder(GameObject go)
        {
            // props are placed relative to room floors at y = 0 in this level
            return 0f;
        }

        /// <summary>Places a prefab so its lowest point rests at the given height (for things on tables or shelves).</summary>
        static GameObject PutOn(Room r, string prefab, float x, float z, float top, float yaw = 0f, float s = 1f)
        {
            var go = Put(r, prefab, x, z, yaw, top, s, null, null, null, false);
            var b = Bounds(go);
            go.transform.position += Vector3.up * (r.fy + top - b.min.y);
            return go;
        }

        /// <summary>Hangs a prefab whose pivot is at its top (chandeliers, hooks) from the ceiling.</summary>
        static GameObject Hang(Room r, string prefab, float x, float z, float s = 1f, float drop = 0f, bool asCeiling = true)
        {
            var go = Put(r, prefab, x, z, Rf(0, 360), r.h - drop, s, null, null, null, false);
            if (asCeiling) foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerCeiling;
            return go;
        }

        /// <summary>A framed painting on a wall.</summary>
        static void Portrait(Room r, char side, float along, float y, float s = 1f, int frame = 5, int painting = 1)
        {
            var f = OnWall(r, "SM_Prop_PaintingFrame_0" + frame, side, along, y, 0.01f, s);
            var p = OnWall(r, "SM_Prop_Painting_0" + painting, side, along, y + (frame == 5 ? 0.15f * s : 0f), 0.035f, s * (frame == 5 ? 1.55f : frame == 4 ? 1.45f : 1.25f));
        }
    }
}
