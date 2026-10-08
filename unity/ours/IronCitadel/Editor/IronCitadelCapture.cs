using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Offscreen PNG captures for the Iron Citadel rooms level: a top-down ortho map with the
    /// ceilings hidden, and an eye-height perspective shot. Everything it changes (ceilings, lights,
    /// ambient) it puts back, so a capture never dirties the saved scene.</summary>
    public static class IronCitadelCapture
    {
        /// <summary>A room prefab marks its lid with a "Ceiling" group whose pieces are named
        /// "ceiling:&lt;mesh&gt;"; the Builder's capture code also hides "roof:" children in map views.</summary>
        public static bool IsCeiling(Transform t)
        {
            string n = t.name;
            return n == "Ceiling" || n.StartsWith("ceiling:") || n.StartsWith("roof:");
        }

        public static List<GameObject> HideCeilings(GameObject root)
        {
            var hidden = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(false))
            {
                if (t == null || !t.gameObject.activeSelf || !IsCeiling(t)) continue;
                t.gameObject.SetActive(false);
                hidden.Add(t.gameObject);
            }
            return hidden;
        }

        public static void Restore(List<GameObject> hidden)
        {
            foreach (var go in hidden) if (go != null) go.SetActive(true);
        }

        static int warmUps;

        /// <summary>Top-down orthographic map centred on <paramref name="centerXZ"/> (world x, z) and
        /// <paramref name="halfSize"/> metres each way. <paramref name="rig"/> true lights it with a flat
        /// grey ambient and two soft directional lights (scene lights off); false keeps the scene's own
        /// lights and ambient (the night as walked).</summary>
        public static void TopDown(string pngPath, Vector2 centerXZ, float halfSize, int px, bool rig,
            float camY = 300f, float gridM = 0f, Vector2 gridOrigin = default)
        {
            var cam = NewCam("~ICTopDown");
            try
            {
                cam.orthographic = true;
                cam.orthographicSize = halfSize;
                cam.aspect = 1f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = camY + 50f;
                cam.transform.position = new Vector3(centerXZ.x, camY, centerXZ.y);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var tex = Render(cam, px, px, rig);
                if (gridM > 0f) Grid(tex, centerXZ, halfSize, px, gridM, gridOrigin);
                Save(tex, pngPath);
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        public static void Perspective(string pngPath, Vector3 eye, float yawDeg, float pitchDeg, float fov,
            int w, int h, bool rig)
        {
            var cam = NewCam("~ICEye");
            try
            {
                cam.orthographic = false;
                cam.fieldOfView = fov;
                cam.aspect = w / (float)h;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 400f;
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
                var tex = Render(cam, w, h, rig);
                Save(tex, pngPath);
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        static Camera NewCam(string name)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);
            return cam;
        }

        static Texture2D Render(Camera cam, int w, int h, bool rig)
        {
            var prevMode = RenderSettings.ambientMode;
            var prevAmb = RenderSettings.ambientLight;
            var prevActive = RenderTexture.active;
            var suppressed = new List<Light>();
            var rigLights = new List<GameObject>();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { useMipMap = false, antiAliasing = 1 };
            rt.Create();
            try
            {
                if (rig)
                {
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f, 1f);
                    foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                    {
                        if (!l.enabled) continue;
                        l.enabled = false;
                        suppressed.Add(l);
                    }
                    rigLights.Add(RigLight(0.55f, new Vector3(50f, 50f, 0f)));
                    rigLights.Add(RigLight(0.45f, new Vector3(-30f, -60f, 0f)));
                }
                bool prevAsync = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                try
                {
                    if (warmUps == 0) { RenderOnce(cam, rt); warmUps++; }
                    RenderOnce(cam, rt);
                }
                finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderSettings.ambientMode = prevMode;
                RenderSettings.ambientLight = prevAmb;
                foreach (var l in suppressed) if (l != null) l.enabled = true;
                foreach (var g in rigLights) if (g != null) Object.DestroyImmediate(g);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        static GameObject RigLight(float intensity, Vector3 euler)
        {
            var go = new GameObject("~ICRigLight");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        static void RenderOnce(Camera cam, RenderTexture rt)
        {
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
            }
        }

        static void Grid(Texture2D tex, Vector2 c, float half, int px, float step, Vector2 origin)
        {
            var pix = tex.GetPixels32();
            float m2p = px / (2f * half);
            float x0 = c.x - half, z0 = c.y - half;
            for (float x = origin.x + Mathf.Ceil((x0 - origin.x) / step) * step; x <= c.x + half; x += step)
            {
                int ix = Mathf.RoundToInt((x - x0) * m2p);
                if (ix <= 0 || ix >= px) continue;
                for (int y = 0; y < px; y++) pix[y * px + ix] = new Color32(255, 220, 0, 255);
            }
            for (float z = origin.y + Mathf.Ceil((z0 - origin.y) / step) * step; z <= c.y + half; z += step)
            {
                int iy = Mathf.RoundToInt((z - z0) * m2p);
                if (iy <= 0 || iy >= px) continue;
                for (int x = 0; x < px; x++) pix[iy * px + x] = new Color32(255, 220, 0, 255);
            }
            tex.SetPixels32(pix);
            tex.Apply();
        }

        static void Save(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
