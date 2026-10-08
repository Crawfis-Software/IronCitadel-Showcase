using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronCitadel.EditorTools
{
    /// <summary>Placement helpers over the Synty prefabs. Keys are "D:Name" (Dungeon), "R:" (Realms), "M:" (Map), "G:" (Generic).</summary>
    public static class Kit
    {
        public static int CeilingLayer = 8, OverviewLayer = 9;
        public static System.Random Rng = new System.Random(7);

        static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Bounds> _bounds = new Dictionary<string, Bounds>();
        public static readonly List<string> Missing = new List<string>();

        public static float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
        public static int RI(int a, int bExclusive) => Rng.Next(a, bExclusive);
        public static T Pick<T>(params T[] items) => items[Rng.Next(items.Length)];
        public static bool Chance(float p) => Rng.NextDouble() < p;

        public static GameObject Prefab(string key)
        {
            if (_prefabs.TryGetValue(key, out var go)) return go;
            var path = Lineup.FindPrefab(key);
            go = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            if (go == null && !Missing.Contains(key)) Missing.Add(key);
            _prefabs[key] = go;
            return go;
        }

        /// <summary>Mesh bounds of a prefab in its own root space (particles ignored).</summary>
        public static Bounds LocalBounds(string key)
        {
            if (_bounds.TryGetValue(key, out var b)) return b;
            var go = Prefab(key);
            b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            if (go != null)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    var m = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    var mb = Xform(m, mesh.bounds);
                    if (first) { b = mb; first = false; } else b.Encapsulate(mb);
                }
            }
            if (first) b = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            _bounds[key] = b;
            return b;
        }

        public static Bounds Xform(Matrix4x4 m, Bounds b)
        {
            var c = b.center; var e = b.extents;
            var r = new Bounds(m.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z)), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                r.Encapsulate(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z))));
            return r;
        }

        public static GameObject Inst(string key, Transform parent)
        {
            var p = Prefab(key);
            if (p == null)
            {
                var ph = new GameObject("MISSING " + key);
                ph.transform.SetParent(parent, false);
                return ph;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            return go;
        }

        /// <summary>Places the prefab pivot at pos, turned yaw degrees about +y (yaw 0 keeps +z north).</summary>
        public static GameObject P(string key, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f)
        {
            var go = Inst(key, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        public static GameObject P(string key, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = Inst(key, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>Places the prefab so its bounds' bottom centre lands on 'at', turned 'yaw', uniformly scaled.</summary>
        public static GameObject PC(string key, Transform parent, Vector3 at, float yaw = 0f, float scale = 1f)
        {
            var b = LocalBounds(key);
            var rot = Quaternion.Euler(0, yaw, 0);
            var bottomCentre = new Vector3(b.center.x, b.min.y, b.center.z) * scale;
            var go = Inst(key, parent);
            go.transform.SetPositionAndRotation(at - rot * bottomCentre, rot);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Like PC but with an arbitrary rotation (for tipped-over furniture).</summary>
        public static GameObject PCR(string key, Transform parent, Vector3 at, Quaternion rot, float scale = 1f)
        {
            var b = LocalBounds(key);
            var go = Inst(key, parent);
            go.transform.SetPositionAndRotation(at - rot * (b.center * scale), rot);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>
        /// Places a prefab whose front is local +z with its back against a wall: 'wallPoint' is on the wall face at floor
        /// height (y used as base), 'inward' the wall's normal into the room. Centred along the wall.
        /// </summary>
        public static GameObject PW(string key, Transform parent, Vector3 wallPoint, Vector3 inward, float scale = 1f, float gap = 0.02f)
        {
            var b = LocalBounds(key);
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var backCentre = new Vector3(b.center.x, b.min.y, b.min.z) * scale;
            var go = Inst(key, parent);
            go.transform.SetPositionAndRotation(wallPoint + inward * gap - rot * backCentre, rot);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Scales and places a prefab so that its rotated bounds fill the world box [min, min+size]. yaw in multiples of 90.</summary>
        public static GameObject Fit(string key, Transform parent, Vector3 min, Vector3 size, float yaw = 0f)
        {
            var b = LocalBounds(key);
            int q = Mathf.RoundToInt(yaw / 90f) & 3;
            bool swap = q == 1 || q == 3;
            var ls = new Vector3(
                (swap ? size.z : size.x) / Mathf.Max(0.001f, b.size.x),
                size.y / Mathf.Max(0.001f, b.size.y),
                (swap ? size.x : size.z) / Mathf.Max(0.001f, b.size.z));
            var rot = Quaternion.Euler(0, q * 90f, 0);
            var go = Inst(key, parent);
            go.transform.localScale = ls;
            go.transform.rotation = rot;
            go.transform.position = Vector3.zero;
            var m = Matrix4x4.TRS(Vector3.zero, rot, ls);
            var wb = Xform(m, b);
            go.transform.position = min - wb.min;
            return go;
        }

        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>An invisible solid box (BoxCollider only).</summary>
        public static GameObject Solid(Transform parent, Vector3 centre, Vector3 size, string name = "Solid", float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0, yaw, 0));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return go;
        }

        /// <summary>An invisible solid box spanning two corners.</summary>
        public static GameObject SolidMinMax(Transform parent, Vector3 a, Vector3 b, string name = "Solid")
        {
            var min = Vector3.Min(a, b); var max = Vector3.Max(a, b);
            return Solid(parent, (min + max) / 2f, max - min, name);
        }

        /// <summary>An invisible ramp collider from bottom-edge centre 'a' to top-edge centre 'b', width w.</summary>
        public static GameObject Ramp(Transform parent, Vector3 a, Vector3 b, float width, string name = "StairRamp")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var d = b - a;
            var flat = new Vector3(d.x, 0, d.z).normalized;
            // look along the slope, with 'up' perpendicular to it in the vertical plane
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            Vector3 up = Vector3.Cross(d.normalized, right).normalized;
            if (up.y < 0) up = -up;
            var rot = Quaternion.LookRotation(d.normalized, up);
            const float thick = 0.4f;
            go.transform.SetPositionAndRotation((a + b) / 2f - up * (thick / 2f), rot);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, thick, d.magnitude + 0.4f);
            return go;
        }

        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        public static Material Mat(string name, Color color, bool unlit = false, float emission = 0f)
        {
            string key = name;
            if (_mats.TryGetValue(key, out var m) && m != null) return m;
            string path = "Assets/IronCitadel/Generated/" + name + ".mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                System.IO.Directory.CreateDirectory("Assets/IronCitadel/Generated");
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            if (!unlit) m.SetFloat("_Smoothness", 0.1f);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(m);
            _mats[key] = m;
            return m;
        }

        /// <summary>A visible primitive (no collider unless asked).</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, float yaw = 0f, bool collider = false, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color color, float intensity, float range, string name = "Light")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        public static readonly Color Fire = new Color(1f, 0.58f, 0.25f);
        public static readonly Color Candle = new Color(1f, 0.72f, 0.42f);
        public static readonly Color Cold = new Color(0.55f, 0.65f, 1f);

        /// <summary>Adds named layers "Ceiling" and "Overview" to the project if missing.</summary>
        public static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void Ensure(int index, string name)
            {
                var p = layers.GetArrayElementAtIndex(index);
                if (p.stringValue != name) p.stringValue = name;
            }
            Ensure(CeilingLayer, "Ceiling");
            Ensure(OverviewLayer, "Overview");
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SimulateParticles(Transform root, float t = 1.5f)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.transform.parent == null || ps.transform.parent.GetComponent<ParticleSystem>() == null)
                    ps.Simulate(t, true, true);
        }
    }
}
