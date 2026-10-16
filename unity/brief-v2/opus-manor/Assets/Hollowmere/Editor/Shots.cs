using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hollowmere.EditorTools
{
    /// <summary>Renders stills of the open scene through a temporary camera that matches the player's (60° vertical FOV, post-processing on).</summary>
    public static class Shots
    {
        public static string Render(string outPath, Vector3 pos, Vector3 euler, float fov = 60f, int w = 1600, int h = 900, bool ortho = false, float orthoSize = 10f, int mask = -1, bool noFog = false)
        {
            var go = new GameObject("ShotCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var cam = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
                cam.fieldOfView = fov;
                cam.nearClipPlane = ortho ? 0.5f : 0.05f;
                cam.farClipPlane = 600f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.orthographic = ortho;
                cam.orthographicSize = orthoSize;
                cam.cullingMask = mask;
                var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                bool fog = RenderSettings.fog;
                if (noFog) RenderSettings.fog = false;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else cam.Render();
                RenderSettings.fog = fog;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return outPath;
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Hides or shows the player's own body for editor captures (not saved). Call it, let the editor tick once
        /// (the GPU Resident Drawer applies it on the next frame), then render.
        /// </summary>
        public static string HidePlayer(bool hide)
        {
            var cc = Object.FindAnyObjectByType<CharacterController>();
            if (cc == null) return "no player";
            foreach (var r in cc.transform.root.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = hide;
            UnityEditor.SceneView.RepaintAll();
            return "player body " + (hide ? "hidden" : "shown");
        }

        /// <summary>Eye-level shot: the player's eye 1.6 m above the floor at (x, z), looking at a target point.</summary>
        public static string Eye(string outPath, float x, float floorY, float z, Vector3 lookAt, int w = 1600, int h = 900)
        {
            var pos = new Vector3(x, floorY + 1.6f, z);
            var rot = Quaternion.LookRotation(lookAt - pos, Vector3.up).eulerAngles;
            return Render(outPath, pos, rot, 60f, w, h);
        }

        /// <summary>The overview as the M key shows it: orthographic, straight down, ceilings left out, fog lifted, ambient raised.</summary>
        public static string Overview(string outPath, int w = 1400, int h = 2000)
        {
            var ov = Object.FindAnyObjectByType<OverviewMode>();
            var min = ov.levelMin; var max = ov.levelMax;
            var c = (min + max) * 0.5f;
            float aspect = (float)w / h;
            float size = Mathf.Max((max.z - min.z) * 0.5f + 2f, ((max.x - min.x) * 0.5f + 2f) / aspect);
            var amb = RenderSettings.ambientLight;
            RenderSettings.ambientLight = ov.overviewAmbient;
            DynamicGI.UpdateEnvironment();
            try
            {
                return Render(outPath, new Vector3(c.x, max.y + 30f, c.z), new Vector3(90f, 0f, 0f), 60f, w, h, true, size, ov.overviewCamera.cullingMask, true);
            }
            finally { RenderSettings.ambientLight = amb; DynamicGI.UpdateEnvironment(); }
        }
    }
}
