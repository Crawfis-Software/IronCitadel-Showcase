using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using IronCitadel;
// The overview capture in three editor ticks (a renderer switched off only drops out of a render on a later
// editor update): Hide, then Render, then Show.
public static class OverviewSteps
{
    public static string Hide(string a) { Set(false); return "hidden"; }
    public static string Show(string a) { Set(true); return "shown"; }
    static void Set(bool on)
    {
        foreach (var room in Object.FindObjectsByType<Room>())
            if (room.ceilingGroup != null)
                foreach (var r in room.ceilingGroup.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        var hud = Object.FindAnyObjectByType<LevelHUD>();
        hud.overviewCamera.enabled = !on;
    }
    public static string Render(string a)
    {
        var hud = Object.FindAnyObjectByType<LevelHUD>();
        var cam = hud.overviewCamera;
        var amb = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode; bool fog = RenderSettings.fog;
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.75f, 0.72f, 0.68f); RenderSettings.fog = false;
        try
        {
            int w = 1800, h = 2400;   // portrait: the level is taller than it is wide
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            float aspect0 = cam.aspect; cam.aspect = w / (float)h;
            cam.targetTexture = rt; cam.Render(); cam.aspect = aspect0; cam.ResetAspect();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes("Captures/overview.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }
        finally { RenderSettings.ambientMode = mode; RenderSettings.ambientLight = amb; RenderSettings.fog = fog; }
        return "Captures/overview.png";
    }
}
