using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.Editor
{
    /// <summary>Renders the overview and one eye-level shot per room to Captures/*.png from a temporary camera.</summary>
    public static class Capture
    {
        public class Shot
        {
            public string name; public Vector3 pos; public Vector3 euler; public float fov = 72f; public bool ortho; public float size; public int w = 1600, h = 900;
            public Shot(string n, float x, float y, float z, float pitch, float yaw, float fov = 72f) { name = n; pos = new Vector3(x, y, z); euler = new Vector3(pitch, yaw, 0f); this.fov = fov; }
        }

        public static List<Shot> Shots()
        {
            var l = new List<Shot>
            {
                new Shot("overview", 90f, 150f, -90f, 90f, 0f) { ortho = true, size = 92f, w = 2000, h = 2000 },
                new Shot("entry_hall", 112.5f, 1.6f, -177.5f, 2f, 0f),
                new Shot("entry_hall_gate", 112.5f, 1.6f, -151f, 0f, 0f, 66f),
                new Shot("guard_room", 76.5f, 1.6f, -152.5f, 4f, 240f),
                new Shot("prison", 67.5f, 1.6f, -129.5f, 4f, 0f),
                new Shot("prison_warden_corner", 53.5f, 1.6f, -111f, 2f, 268f),
                new Shot("vault", 43.5f, 1.6f, -112.5f, 6f, 262f),
                new Shot("kitchen", 73f, 1.6f, -73.5f, 4f, 325f),
                new Shot("great_hall", 118f, 1.6f, -47.5f, 5f, 195f),
                new Shot("great_hall_from_gallery", 108f, 6.7f, -85.7f, 20f, 10f),
                new Shot("great_hall_landing", 92.5f, 1.6f, -67.5f, 2f, 90f),
                new Shot("armory", 112.5f, 1.6f, -103.5f, 4f, 180f),
                new Shot("throne_room", 122f, 1.6f, -29.5f, 0f, 335f),
                new Shot("throne_room_door", 112.5f, 1.6f, -43.5f, 0f, 0f),
                new Shot("library", 157.5f, 1.6f, -168.5f, 3f, 0f),
                new Shot("study", 152.5f, 1.6f, -124f, 4f, 25f),
                new Shot("forge", 157.5f, 1.6f, -78f, 2f, 0f),
            };
            return l;
        }

        [MenuItem("Tools/Iron Citadel/Capture shots")]
        public static void ShootAll()
        {
            if (EditorSceneManager.GetActiveScene().path != IronCitadelBuilder.ScenePath)
                EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory("Captures");
            var sb = new System.Text.StringBuilder();
            foreach (var s in Shots())
            {
                try { Render(s, "Captures/" + s.name + ".png"); sb.AppendLine("ok " + s.name); }
                catch (System.Exception e) { sb.AppendLine("fail " + s.name + " " + e.Message); }
            }
            File.WriteAllText("Tools/capture_result.txt", sb.ToString());
            Debug.Log("[Capture] " + sb);
        }

        /// <summary>Diagnostic: the overview with every point light off, to see what colours the rock slabs on their own.</summary>
        public static void OverviewDiag()
        {
            if (EditorSceneManager.GetActiveScene().path != IronCitadelBuilder.ScenePath)
                EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath, OpenSceneMode.Single);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var was = new List<bool>();
            foreach (var l in lights) { was.Add(l.enabled); if (l.type == LightType.Point) l.enabled = false; }
            Shot ov = null;
            foreach (var s in Shots()) if (s.name == "overview") ov = s;
            Render(ov, "Tools/diag_overview_nopoint.png");
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = was[i];
            foreach (var l in lights) if (l.type == LightType.Directional) l.enabled = false;
            var p = new Shot("overview_persp", 90f, 120f, -200f, 50f, 0f, 60f);
            Render(p, "Tools/diag_overview_persp.png");
            for (int i = 0; i < lights.Length; i++) lights[i].enabled = was[i];
        }

        /// <summary>Renders one named shot (name must match one in Shots()); used from the live editor.</summary>
        public static void ShootOne(string name)
        {
            foreach (var s in Shots()) if (s.name == name) Render(s, "Captures/" + s.name + ".png");
        }

        public static void Render(Shot s, string path)
        {
            var go = new GameObject("CaptureCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(s.euler));
                cam.fieldOfView = s.fov;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 500f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                if (s.ortho)
                {
                    cam.orthographic = true;
                    cam.orthographicSize = s.size;
                    cam.cullingMask = ~(1 << 6);
                }
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = !s.ortho;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                bool fog = RenderSettings.fog;
                if (s.ortho) RenderSettings.fog = false;
                var ovLight = GameObject.Find("OverviewLight");
                var hud = Object.FindFirstObjectByType<LevelHud>();
                if (hud != null && hud.overviewLight != null) ovLight = hud.overviewLight;
                if (ovLight != null) ovLight.SetActive(s.ortho);
                // fires and candles are particles: give them a couple of seconds so they show in the frame
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                {
                    if ((ps.transform.position - s.pos).sqrMagnitude > 80f * 80f) continue;
                    ps.Simulate(2.5f, true, true);
                }
                var rt = new RenderTexture(s.w, s.h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(s.w, s.h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                RenderSettings.fog = fog;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
