using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace IronCitadel.EditorTools
{
    /// <summary>Lays prefabs out in a labelled grid in a scratch scene and renders a PNG, for choosing art.</summary>
    public static class Lineup
    {
        static Dictionary<string, string> _index;
        static readonly Dictionary<string, string> Packs = new Dictionary<string, string>
        { { "D", "PolygonDungeon" }, { "R", "PolygonDungeonRealms" }, { "M", "PolygonDungeonMap" }, { "G", "PolygonGeneric" } };

        /// <summary>Resolves "Name" or "D:Name" / "R:Name" / "M:Name" / "G:Name" (pack-qualified) to a prefab path.</summary>
        public static string FindPrefab(string key)
        {
            if (_index == null)
            {
                _index = new Dictionary<string, string>();
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var n = Path.GetFileNameWithoutExtension(p);
                    var pack = p.Split('/')[2];
                    _index[pack + "/" + n] = p;
                    if (!_index.ContainsKey(n)) _index[n] = p;
                }
            }
            int c = key.IndexOf(':');
            if (c > 0 && Packs.TryGetValue(key.Substring(0, c), out var dir)) key = dir + "/" + key.Substring(c + 1);
            return _index.TryGetValue(key, out var path) ? path : null;
        }

        public static string Run(string namesCsv, string outPng, int cols = 6, float yaw = 0f, float pitch = 25f, int w = 1920, int h = 1080, float spacing = 1.5f, float zoom = 0.85f, float sun = 1.6f, float amb = 0.45f)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var names = namesCsv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var lightGo = new GameObject("Sun");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional; l.intensity = sun; l.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(45, yaw + 35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(amb, amb, amb);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            float x = 0, z = 0, rowDepth = 0;
            int col = 0;
            var all = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            var missing = new List<string>();
            foreach (var n in names)
            {
                var p = FindPrefab(n);
                if (p == null) { missing.Add(n); continue; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.rotation = Quaternion.identity;
                var b = RendererBounds(go);
                // place so bounds min.x at x, bounds max.z at z (rows go toward -z)
                var offset = new Vector3(x - b.min.x, 0, z - b.max.z);
                go.transform.position = offset;
                var nb = RendererBounds(go);
                var label = new GameObject("label_" + n);
                var tm = label.AddComponent<TextMesh>();
                tm.text = n.Replace("SM_", "") + $"\n{b.size.x:F1}x{b.size.y:F1}x{b.size.z:F1}";
                tm.font = font; tm.fontSize = 48; tm.characterSize = 0.04f; tm.anchor = TextAnchor.UpperLeft; tm.color = Color.yellow;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                label.transform.position = new Vector3(nb.min.x, 0.02f, nb.min.z - 0.1f);
                label.transform.rotation = Quaternion.Euler(90, 0, 0);
                if (first) { all = nb; first = false; } else all.Encapsulate(nb);
                all.Encapsulate(label.transform.position + new Vector3(0, 0, -1.5f));
                x += b.size.x + spacing;
                rowDepth = Mathf.Max(rowDepth, b.size.z + 2.0f);
                if (++col >= cols) { col = 0; x = 0; z -= rowDepth + spacing; rowDepth = 0; }
            }
            // ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(all.center.x, -0.01f, all.center.z);
            ground.transform.localScale = new Vector3(all.size.x / 10f + 2, 1, all.size.z / 10f + 2);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.color = new Color(0.2f, 0.25f, 0.2f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gm;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
            cam.fieldOfView = 40; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1000;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            float radius = all.extents.magnitude;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * zoom;
            camGo.transform.rotation = rot;
            camGo.transform.position = all.center - rot * Vector3.forward * dist;
            Capture.RenderToPng(cam, w, h, outPng);
            return $"ok {names.Count - missing.Count} placed; missing: {string.Join(",", missing)}";
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.5f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }

    public static class Capture
    {
        public static void RenderToPng(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            var req = new RenderPipeline.StandardRequest();
            if (RenderPipeline.SupportsRenderRequest(cam, req))
            {
                req.destination = rt;
                RenderPipeline.SubmitRenderRequest(cam, req);
            }
            else cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
