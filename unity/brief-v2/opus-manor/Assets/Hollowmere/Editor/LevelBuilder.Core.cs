using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hollowmere.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/IronCitadel.unity from code: the shell (floors, walls with openings, ceilings, trims),
    /// doors, stairs, gallery and stage, the dressing, lights, markers, the player and the level's systems.
    /// Re-running it rebuilds the scene from scratch.
    /// </summary>
    public static partial class LevelBuilder
    {
        public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
        public const string MatDir = "Assets/Hollowmere/Materials";
        public const int LayerCeiling = 8;
        public const int LayerDoorLeaf = 9;
        public const int LayerMarker = 10;
        public const int LayerHelper = 11;
        const float T = 0.2f; // wall thickness owned by each room

        // ---------------------------------------------------------------- surfaces

        public class Surf
        {
            public Material mat;
            public float tu = 3f, tv = 3f;
        }

        static readonly Dictionary<string, Surf> s_Surf = new Dictionary<string, Surf>();
        static readonly Dictionary<string, Material> s_Mat = new Dictionary<string, Material>();

        static Texture2D Tex(string file)
        {
            string[] roots =
            {
                "Assets/Synty/PolygonHorrorMansion/Textures/Building/",
                "Assets/Synty/PolygonHorrorMansion/Textures/Building/Walls/",
                "Assets/Synty/PolygonHorrorMansion/Textures/",
            };
            foreach (var r in roots)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(r + file);
                if (t != null) return t;
            }
            throw new Exception("texture not found: " + file);
        }

        static Material MakeMat(string name, Texture2D tex, Color tint, float smooth = 0.12f, float metal = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            s_Mat[name] = m;
            return m;
        }

        static Surf S(string name, string tex, float tile, Color tint, float smooth = 0.12f, float tv = -1f)
        {
            var s = new Surf { mat = MakeMat(name, tex == null ? null : Tex(tex), tint, smooth), tu = tile, tv = tv > 0 ? tv : tile };
            s_Surf[name] = s;
            return s;
        }

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
        static Color Grey(float v) => new Color(v, v, v, 1f);

        static void MakeSurfaces()
        {
            Directory.CreateDirectory(MatDir);
            s_Surf.Clear(); s_Mat.Clear();
            // Wallpapers: the dado textures map 3 m of wall to one repeat, wainscot at the bottom.
            S("wp_damask", "WallPaper_Tex_04.png", 3f, Grey(0.86f));
            S("wp_damask_plain", "WallPaper_Tex_04_Trimless.png", 3f, Grey(0.82f));
            S("wp_damask_torn", "WallPaper_Tex_04_Grunge.png", 3f, Grey(0.85f));
            S("wp_damask_stain", "WallPaper_Tex_04_Grunge_02.png", 3f, Grey(0.85f));
            S("wp_panel", "WallPaper_Tex_06.png", 3f, Grey(0.85f));
            S("wp_red", "WallPaper_Tex_05.png", 3f, Grey(0.9f));
            S("wp_rose", "WallPaper_Tex_02.png", 3f, Grey(0.85f));
            S("wp_beige", "WallPaper_Tex_03.png", 3f, Grey(0.9f));
            S("wp_ring", "WallPaper_Tex_01.png", 3f, Grey(0.88f));
            S("brick_brown", "Brick_Brown_Tex.png", 2.4f, Grey(0.8f));
            S("brick_grey", "Brick_Grey_Tex.png", 2.4f, Grey(0.62f));
            S("brick_dark", "Brick_Grey_Tex.png", 2.4f, C(0.42f, 0.4f, 0.38f));
            S("concrete", "Concrete_Tex.png", 3f, Grey(0.72f));
            S("concrete_dark", "Concrete_Tex.png", 3f, Grey(0.45f));
            S("plaster", "Wood_Glasshouse.png", 3f, C(0.86f, 0.84f, 0.78f));
            S("plaster_dirty", "Wood_Glasshouse.png", 3f, C(0.62f, 0.6f, 0.55f));
            S("boards", "Wood_Drywall_01.png", 3f, Grey(0.85f));
            S("boards_dark", "Wood_Drywall_02.png", 3f, Grey(0.62f));
            S("siding", "WallPanelB_baseTexBaked.png", 3f, C(0.75f, 0.74f, 0.7f));
            S("planks", "Wood_01.png", 2.4f, Grey(0.85f));
            S("planks_dark", "Wood_02.png", 2.4f, Grey(0.6f));
            S("parquet", "Wood_Fancy_01.png", 2.0f, Grey(0.8f), 0.3f);
            S("floorboards", "Wood_Fancy_02.png", 2.6f, Grey(0.8f), 0.22f);
            S("tile_green", "Tile_Bathroom_01.png", 1.6f, Grey(0.8f), 0.3f);
            S("tile_stone", "Tile_Kitchen_01.png", 1.8f, Grey(0.78f), 0.2f);
            S("tile_blue", "Tile_Kitchen_02.png", 1.6f, Grey(0.85f), 0.25f);
            S("carpet_red", "Carpet_01.png", 3f, Grey(0.9f));
            S("carpet_blue", "Carpet_02.png", 3f, Grey(0.9f));
            S("carpet_green", "Carpet_03.png", 3f, Grey(0.9f));
            S("carpet_pattern", "Carpet_04.png", 2.5f, Grey(0.9f));
            S("carpet_hex", "Carpet_05.png", 1.6f, Grey(0.85f));
            S("roof_tar", "RoofTar_01.png", 3f, Grey(0.6f));
            // Plain trims and joinery.
            S("wood_trim", "Wood_Fancy_02.png", 2.6f, C(0.55f, 0.42f, 0.34f), 0.25f);
            S("wood_dark", "Wood_Fancy_02.png", 2.6f, C(0.32f, 0.24f, 0.2f), 0.25f);
            S("stone_trim", "Concrete_Tex.png", 3f, C(0.66f, 0.64f, 0.6f));
            S("iron", "Concrete_Tex.png", 3f, C(0.16f, 0.16f, 0.17f), 0.4f);
            S("cloth_red", "Curtains_01.png", 2f, C(0.55f, 0.08f, 0.1f));
            // Marker rings.
            MakeMat("marker_ranged", null, C(0.9f, 0.1f, 0.08f), 0.3f, 0f, C(1.6f, 0.12f, 0.08f));
            MakeMat("marker_melee", null, C(0.95f, 0.5f, 0.08f), 0.3f, 0f, C(1.5f, 0.6f, 0.05f));
            MakeMat("marker_master", null, C(0.6f, 0.1f, 0.9f), 0.3f, 0f, C(1.2f, 0.15f, 2.0f));
            MakeMat("marker_pickup", null, C(0.95f, 0.85f, 0.2f), 0.3f, 0f, C(2.0f, 1.6f, 0.3f));
            MakeMat("glow_warm", null, C(1f, 0.8f, 0.5f), 0.3f, 0f, C(3.0f, 1.9f, 0.9f));
            MakeMat("glow_moon", null, C(0.7f, 0.8f, 1f), 0.3f, 0f, C(0.9f, 1.1f, 1.6f));
            MakeMat("void_black", null, C(0.01f, 0.01f, 0.01f), 0f);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- meshes

        /// <summary>
        /// An axis-aligned box whose UVs follow world coordinates, so a texture keeps its real size on boxes
        /// of any size and lines up across neighbouring boxes. Wall faces measure v from <paramref name="vOrigin"/>.
        /// </summary>
        static Mesh BoxMesh(Vector3 min, Vector3 max, Surf s, float vOrigin)
        {
            var c = (min + max) * 0.5f;
            var v = new List<Vector3>(24); var n = new List<Vector3>(24); var uv = new List<Vector2>(24); var tri = new List<int>(36);
            void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Vector3 normal, Func<Vector3, Vector2> map)
            {
                int i = v.Count;
                foreach (var p in new[] { a, b, cc, d }) { v.Add(p - c); n.Add(normal); uv.Add(map(p)); }
                bool flip = Vector3.Dot(Vector3.Cross(b - a, cc - a), normal) < 0f;
                if (!flip) tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            float tu = s.tu, tv = s.tv;
            Func<Vector3, Vector2> mx = p => new Vector2(p.z / tu, (p.y - vOrigin) / tv);
            Func<Vector3, Vector2> my = p => new Vector2(p.x / tu, p.z / tv);
            Func<Vector3, Vector2> mz = p => new Vector2(p.x / tu, (p.y - vOrigin) / tv);
            float x0 = min.x, y0 = min.y, z0 = min.z, x1 = max.x, y1 = max.y, z1 = max.z;
            Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, mx);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, mx);
            Quad(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up, my);
            Quad(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), Vector3.down, my);
            Quad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward, mz);
            Quad(new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), Vector3.back, mz);
            var m = new Mesh { name = "box" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 min, Vector3 max, Surf s, uint rl, float vOrigin = 0f,
            bool collider = true, int layer = 0, bool isStatic = true)
        {
            for (int i = 0; i < 3; i++) if (max[i] < min[i]) { float t = min[i]; min[i] = max[i]; max[i] = t; }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            go.layer = layer;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BoxMesh(min, max, s, vOrigin);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = s.mat;
            mr.renderingLayerMask = rl;
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = max - min;
            }
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        /// <summary>An invisible box collider (ramps, rails' collision, blockers).</summary>
        static GameObject Blocker(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rot, int layer = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot);
            go.layer = layer;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ---------------------------------------------------------------- prefabs

        static Dictionary<string, string> s_Prefabs;

        static GameObject Prefab(string name)
        {
            if (s_Prefabs == null)
            {
                s_Prefabs = new Dictionary<string, string>();
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var key = Path.GetFileNameWithoutExtension(p);
                    if (!s_Prefabs.ContainsKey(key) || p.Contains("PolygonHorrorMansion")) s_Prefabs[key] = p;
                }
            }
            if (!s_Prefabs.TryGetValue(name, out var path)) throw new Exception("prefab not found: " + name);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static int s_PropCount;

        /// <summary>
        /// Places a Synty prefab. Position is world (y added to the room floor). Small clutter loses its colliders
        /// so it does not snag the player.
        /// </summary>
        static GameObject Put(Room r, string prefab, float x, float z, float yaw = 0f, float y = 0f, float s = 1f,
            Vector3? scale = null, Vector3? euler = null, Transform parent = null, bool colliders = true, bool isStatic = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(prefab));
            go.transform.SetParent(parent != null ? parent : r.props, false);
            go.transform.position = new Vector3(x, r.fy + y, z);
            go.transform.rotation = euler.HasValue ? Quaternion.Euler(euler.Value) : Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale ?? Vector3.one * s;
            Finish(go, r.RL, colliders, isStatic);
            s_PropCount++;
            return go;
        }

        static void Finish(GameObject go, uint rl, bool colliders, bool isStatic)
        {
            foreach (var rend in go.GetComponentsInChildren<Renderer>(true)) rend.renderingLayerMask = rl;
            var b = Bounds(go);
            bool small = b.size.y < 0.45f && b.size.x * b.size.z < 0.7f;
            if (!colliders || small)
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            if (isStatic)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Yaw that turns a prefab's +Z face toward the room from the given wall side.</summary>
        static float FaceIn(char side) => side switch { 'S' => 0f, 'N' => 180f, 'W' => 90f, 'E' => -90f, _ => 0f };

        /// <summary>A point on a wall's interior face. <paramref name="along"/> is world x for S/N walls, world z for W/E walls.</summary>
        static Vector3 WallPoint(Room r, char side, float along, float y, float off = 0f)
        {
            switch (side)
            {
                case 'S': return new Vector3(along, r.fy + y, r.z0 + off);
                case 'N': return new Vector3(along, r.fy + y, r.z1 - off);
                case 'W': return new Vector3(r.x0 + off, r.fy + y, along);
                default: return new Vector3(r.x1 - off, r.fy + y, along);
            }
        }

        /// <summary>Places a wall-mounted prefab (front face +Z) against a wall, facing into the room.</summary>
        static GameObject OnWall(Room r, string prefab, char side, float along, float y, float off = 0f, float s = 1f,
            Vector3? scale = null, bool colliders = false, float roll = 0f)
        {
            var p = WallPoint(r, side, along, y, off);
            var go = Put(r, prefab, p.x, p.z, FaceIn(side), p.y - r.fy, s, scale, null, null, colliders);
            if (roll != 0f) go.transform.rotation = go.transform.rotation * Quaternion.Euler(0f, 0f, roll);
            return go;
        }

        // ---------------------------------------------------------------- lights

        static Light Lamp(Room r, string name, Vector3 pos, Color c, float intensity, float range, bool shadows = false,
            LightType type = LightType.Point, Vector3? euler = null, float spot = 60f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(r.lights, false);
            go.transform.position = pos;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = c;
            l.intensity = intensity * (s_Gain.TryGetValue(r.id, out var g) ? g : 1.7f);
            l.range = range;
            if (type == LightType.Spot) { l.spotAngle = spot; l.innerSpotAngle = spot * 0.55f; }
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.85f;
            l.shadowNearPlane = 0.15f;
            l.renderingLayerMask = (int)r.RL;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            data.renderingLayers = r.RL;
            return l;
        }

        /// <summary>Per-room light gain: the tall bright rooms get the most, the dark rooms the least.</summary>
        static readonly Dictionary<string, float> s_Gain = new Dictionary<string, float>
        {
            { "hall", 5.0f }, { "ballroom", 7.0f }, { "foyer", 2.0f }, { "gun", 5.0f }, { "library", 4.0f }, { "lab", 3.0f },
            { "chapel", 4.5f }, { "stair", 8.0f }, { "watch", 5.0f }, { "cells", 4.2f }, { "butcher", 5.5f }, { "crypt", 7.0f },
            { "kitchen", 3.2f }, { "store", 3.8f }, { "boiler", 7.5f }, { "strong", 5.0f },
        };

        static readonly Color Candle = new Color(1f, 0.66f, 0.38f);
        static readonly Color Warm = new Color(1f, 0.78f, 0.55f);
        static readonly Color Moon = new Color(0.55f, 0.66f, 0.92f);
        static readonly Color Cold = new Color(0.78f, 0.86f, 1f);
        static readonly Color Sick = new Color(0.72f, 0.95f, 0.72f);
    }
}
