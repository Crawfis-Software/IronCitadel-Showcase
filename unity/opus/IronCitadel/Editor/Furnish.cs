using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.EditorTools
{
    /// <summary>Furnishing helpers shared by the room builders: markers (posed characters), pickups, lights, clutter.</summary>
    public static class Furnish
    {
        public static int DefenderCount, PickupCount;
        public static readonly Dictionary<string, int> PerRoom = new Dictionary<string, int>();

        static Material IconMat(Color c) => Kit.Mat("Icon_" + ColorUtility.ToHtmlStringRGB(c), c, true);

        /// <summary>A defender marker: a Synty character posed standing (or seated), a body collider, and a red overview icon.</summary>
        public static GameObject Defender(Room rm, string key, Vector3 feet, float yaw, string role, bool seated = false, float scale = 1f)
        {
            var go = Kit.P(key, rm.People, feet, yaw, scale);
            go.name = "Defender_" + role;
            Kit.StripColliders(go);
            Poser.Pose(go, seated ? 0.6f : 0.6f, 0f, seated);
            if (!seated)
            {
                var cap = go.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0, 0.9f, 0); cap.height = 1.8f; cap.radius = 0.3f;
            }
            Icon(rm, feet, new Color(0.95f, 0.15f, 0.12f), 2.4f, "DefenderIcon");
            DefenderCount++;
            PerRoom[rm.Id] = PerRoom.TryGetValue(rm.Id, out int n) ? n + 1 : 1;
            return go;
        }

        public static GameObject Icon(Room rm, Vector3 at, Color c, float size, string name)
        {
            var go = Kit.Prim(PrimitiveType.Cylinder, rm.People, new Vector3(at.x, 18f, at.z), new Vector3(size, 0.05f, size), IconMat(c), 0f, false, name);
            go.layer = Kit.OverviewLayer;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A pickup marker: a soft golden glow and sparkle over the item, a gold overview icon, announced when reached.</summary>
        public static GameObject Pickup(Room rm, Vector3 at, string label, float glowHeight = 1.2f)
        {
            var root = new GameObject("Pickup_" + label.Replace(' ', '_'));
            root.transform.SetParent(rm.People, false);
            root.transform.position = at;
            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = Vector3.up * glowHeight;
            var l = Kit.PointLight(glow.transform, glow.transform.position, new Color(1f, 0.8f, 0.35f), 3.5f, 5f, "PickupLight");
            var fx = Kit.P("R:FX_Gold_Sparkle_01", glow.transform, glow.transform.position, 0f, 1f);
            var pm = root.AddComponent<PickupMarker>();
            pm.label = label;
            pm.hideWhenTaken = new[] { glow };
            Icon(rm, at, new Color(1f, 0.8f, 0.2f), 3.2f, "PickupIcon");
            PickupCount++;
            return root;
        }

        /// <summary>A hanging candle chandelier with a light; pivot at the ceiling.</summary>
        public static void Chandelier(Room rm, Vector3 ceilingPoint, float intensity, float range, string key = "D:SM_Prop_Candle_Chandelier_02_Preset", float scale = 1f)
        {
            var go = Kit.P(key, rm.Props, ceilingPoint, Kit.R(0, 90), scale);
            go.name = "Chandelier";
            Kit.StripColliders(go);
            Kit.SetLayer(go, Kit.CeilingLayer);
            var b = Kit.LocalBounds(key);
            var p = ceilingPoint + Vector3.up * (b.min.y * scale * 0.75f);
            Kit.PointLight(rm.Lights, p, Kit.Candle, intensity, range, "ChandelierLight");
        }

        public static void Brazier(Room rm, Vector3 at, float intensity = 3f, float range = 10f, string key = "D:SM_Prop_Brazier_01", float scale = 1f)
        {
            var go = Kit.PC(key, rm.Props, at, Kit.R(0, 360), scale);
            go.name = "Brazier";
            var top = at + Vector3.up * (Kit.LocalBounds(key).size.y * scale * 0.85f);
            Kit.P("R:FX_Fire_Small_01", go.transform, top, 0f, Mathf.Clamp(Kit.LocalBounds(key).size.x * scale * 0.45f, 0.35f, 1f));
            Kit.PointLight(rm.Lights, top + Vector3.up * 0.5f, Kit.Fire, intensity, range, "BrazierLight");
        }

        public static void Candles(Room rm, Vector3 at, float intensity = 0.6f, float range = 3.5f)
        {
            var key = Kit.Pick("D:SM_Prop_Candles_01_Preset", "D:SM_Prop_Candles_02_Preset", "D:SM_Prop_Candles_03_Preset", "D:SM_Prop_Candle_01_Preset");
            var go = Kit.PC(key, rm.Props, at, Kit.R(0, 360));
            Kit.StripColliders(go);
            Kit.PointLight(rm.Lights, at + Vector3.up * 0.45f, Kit.Candle, intensity, range, "CandleLight");
        }

        /// <summary>Scatters small items over a table top (bounds-based), kept inside the top.</summary>
        public static void TableTop(Room rm, GameObject table, string[] items, int count, float yaw)
        {
            var b = Kit.LocalBounds(PrefabKey(table));
            var t = table.transform;
            for (int i = 0; i < count; i++)
            {
                float lx = Kit.R(b.min.x + 0.25f, b.max.x - 0.25f), lz = Kit.R(b.min.z + 0.25f, b.max.z - 0.25f);
                var p = t.TransformPoint(new Vector3(lx, b.max.y, lz));
                var key = items[Kit.RI(0, items.Length)];
                var go = Kit.PC(key, rm.Props, p, Kit.R(0, 360));
                Kit.StripColliders(go);
            }
        }

        public static string PrefabKey(GameObject instance)
        {
            var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (src == null) return null;
            var path = UnityEditor.AssetDatabase.GetAssetPath(src);
            var pack = path.Split('/')[2];
            string p = pack == "PolygonDungeon" ? "D" : pack == "PolygonDungeonRealms" ? "R" : pack == "PolygonDungeonMap" ? "M" : "G";
            return p + ":" + System.IO.Path.GetFileNameWithoutExtension(path);
        }
    }
}
