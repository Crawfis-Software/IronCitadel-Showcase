// Renders the deliverable captures of the open scene into Captures/ (outside Assets):
//   unity command --timeout 300 run_script -- --file Tools/Captures.cs --entry Captures.Run --args '[""]' --timeout_ms 290000
// Eye-level shots are 1.6 m above the floor with the player's 60 degree vertical field of view; the player
// capsule is hidden. The overview (the HUD's M camera with the ceilings hidden) and the open-bookcase shot
// need later editor ticks, so they are separate commands: see Tools/OverviewSteps.cs and OpenVaultShot below.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using IronCitadel;

public static class Captures
{
    public static string Run(string args)
    {
        Directory.CreateDirectory("Captures");
        foreach (var f in Directory.GetFiles("Captures", "*.png")) if (!f.EndsWith("vault_hidden_way_in.png") && !f.EndsWith("overview.png")) File.Delete(f);
        var made = new List<string>();
        // the overview is made by Tools/OverviewSteps.cs (Hide, Render, Show as three commands)

        // one eye-level view per room: x, z, yaw, pitch
        var rooms = new (string name, float x, float z, float yaw, float pitch)[]
        {
            ("Entry Hall", 13.9f, 1.0f, -40f, 2f),
            ("Armory", 14.0f, 19.2f, 218f, 6f),
            ("Storeroom", -0.9f, 0.9f, -45f, 5f),
            ("Kitchen", -0.9f, 10.9f, -40f, 5f),
            ("Servery Stair", -6.0f, 34.0f, 130f, -10f),
            ("Forge", -17.8f, 1.1f, 28f, 2f),
            ("Guard Room", 15.9f, 0.9f, 45f, 5f),
            ("Prison", 16.0f, 14.5f, 90f, 5f),
            ("Torture Chamber", 30.9f, 10.9f, 45f, 5f),
            ("Great Dining Hall", 0.9f, 39.0f, 125f, -5f),
            ("Library", 22.5f, 40.9f, 0f, 0f),
            ("Alchemist's Study", 30.9f, 40.9f, 45f, 5f),
            ("Treasure Vault", 31.0f, 52.5f, 90f, 10f),
            ("Shrine", 14.2f, 47.5f, -90f, -5f),
            ("Crypt", -0.8f, 42.5f, -90f, 5f),
            ("Throne Room", 7.5f, 56.2f, 0f, -5f),
        };
        foreach (var r in rooms)
        {
            var room = Object.FindObjectsByType<Room>().First(x => x.roomName == r.name);
            string file = "Captures/room_" + Slug(r.name) + ".png";
            Shot.Eye(new Vector3(r.x, room.floorY + 1.6f, r.z), r.yaw, r.pitch, 60f, file);
            made.Add(file);
        }

        // the armory's guarded entrance, seen from the entry hall (the guards are behind it, out of sight)
        Shot.Eye(new Vector3(9.5f, 1.6f, 3.5f), -12f, 0f, 60f, "Captures/armory_entrance_from_entry_hall.png");
        // the way into the armory beneath the gallery, from the hall floor
        Shot.Eye(new Vector3(3.2f, 1.6f, 28.0f), 152f, -8f, 60f, "Captures/armory_way_in_beneath_gallery.png");
        // the hall floor from the gallery's rail (standing on the archers' step)
        Shot.Eye(new Vector3(12f, 6.88f, 23.05f), 10f, 32f, 60f, "Captures/hall_floor_from_gallery_rail.png");
        // the throne on its stage
        Shot.Eye(new Vector3(7.5f, 1.6f, 70.5f), 0f, -6f, 60f, "Captures/throne_on_stage.png");
        // the vault's hidden way in: the bookcase as it stands, then slid aside on E
        var sb = Object.FindAnyObjectByType<SecretBookcase>();
        Shot.Eye(new Vector3(24.6f, 1.6f, 50.2f), 58f, 2f, 60f, "Captures/vault_hidden_way_in_closed.png");
        // The open state is rendered by OpenVaultShot below, in three separate commands, because a renderer
        // only picks up the moved bookcase on a later editor update.
        made.Add("set pieces");
        return string.Join(", ", made);
    }

    // unity command eval "var sb=UnityEngine.Object.FindAnyObjectByType<IronCitadel.SecretBookcase>(); sb.transform.localPosition += sb.slide; return 1;"
    // unity command run_script -- --file Tools/Captures.cs --entry Captures.OpenVaultShot --args '[""]'
    // unity command eval "var sb=UnityEngine.Object.FindAnyObjectByType<IronCitadel.SecretBookcase>(); sb.transform.localPosition -= sb.slide; return 1;"
    public static string OpenVaultShot(string args)
    {
        Shot.Eye(new Vector3(26.4f, 1.6f, 52.1f), 88f, 2f, 60f, "Captures/vault_hidden_way_in.png");
        return "Captures/vault_hidden_way_in.png";
    }

    static string Slug(string s) => new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Replace("__", "_").Trim('_');
}

// ---- rendering helpers (same as Tools/Shot.cs, kept here so this file runs on its own)
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
