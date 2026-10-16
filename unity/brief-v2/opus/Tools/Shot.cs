// Renders views of the open scene to PNG files outside Assets.
//   args: one or more ';'-separated specs:
//     overview|<out.png>                     top-down of the whole level with ceilings hidden (the HUD's overview camera)
//     eye|x|y|z|yaw|pitch|fov|<out.png>      a first-person style view (post-processing on)
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using IronCitadel;

public static class Shot
{
    public static string Run(string args)
    {
        var outs = new List<string>();
        foreach (var spec in args.Split(';'))
        {
            var p = spec.Trim().Split('|');
            if (p.Length < 2) continue;
            if (p[0] == "overview") { Overview(p[1], p.Length > 2 ? int.Parse(p[2]) : 2400); outs.Add(p[1]); }
            else if (p[0] == "eye")
            {
                float x = float.Parse(p[1]), y = float.Parse(p[2]), z = float.Parse(p[3]), yaw = float.Parse(p[4]), pitch = float.Parse(p[5]), fov = float.Parse(p[6]);
                Eye(new Vector3(x, y, z), yaw, pitch, fov, p[7]); outs.Add(p[7]);
            }
        }
        return string.Join(", ", outs);
    }

    public static void Eye(Vector3 pos, float yaw, float pitch, float fov, string path, int w = 1600, int h = 900)
    {
        var go = new GameObject("ShotCam");
        var hidden = new List<Renderer>();
        var cc = Object.FindAnyObjectByType<CharacterController>();
        if (cc != null) foreach (var r in cc.transform.root.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
        try
        {
            var cam = go.AddComponent<Camera>();
            cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Render(cam, path, w, h);
        }
        finally { Object.DestroyImmediate(go); foreach (var r in hidden) r.enabled = true; }
    }

    public static void Overview(string path, int w)
    {
        var hud = Object.FindAnyObjectByType<LevelHUD>();
        var cam = hud.overviewCamera;
        var hidden = new List<Renderer>();
        foreach (var room in Object.FindObjectsByType<Room>(FindObjectsSortMode.None))
            if (room.ceilingGroup != null)
                foreach (var r in room.ceilingGroup.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
        var amb = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode; bool fog = RenderSettings.fog;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.75f, 0.72f, 0.68f); RenderSettings.fog = false;
        try
        {
            float aspect = 16f / 9f;
            int h = Mathf.RoundToInt(w / aspect);
            cam.enabled = true;
            Render(cam, path, w, h);
        }
        finally
        {
            cam.enabled = false;
            foreach (var r in hidden) r.enabled = true;
            RenderSettings.ambientMode = mode; RenderSettings.ambientLight = amb; RenderSettings.fog = fog;
        }
    }

    public static void Render(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevT = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = prevT;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
