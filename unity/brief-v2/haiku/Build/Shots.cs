using System.IO;
using System.Text;
using IronCitadel.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Renders review and deliverable shots of the open scene to Captures/ (outside Assets, so Unity does not import them).
// Run: unity command run_script -- --file Build/Shots.cs --entry Shots.Run
public static class Shots
{
    const int W = 1600, H = 900;
    static int ceilingLayer;

    public static string Run()
    {
        var log = new StringBuilder();
        ceilingLayer = LayerMask.NameToLayer("Ceiling");
        Directory.CreateDirectory("Captures");

        // the top-down overview: ceilings culled, the whole level in frame
        Overview("overview", new Vector3(32.5f, 120f, 0f), 40f);

        // eye-level shots of each room: from, to (both at standing eye height unless given)
        Eye("room entry-hall", new Vector3(3f, 1.6f, 3f), new Vector3(14f, 1.6f, 12f));
        Eye("room armory", new Vector3(29f, 1.6f, 12.5f), new Vector3(16f, 1.6f, 6f));
        Eye("room dining-hall", new Vector3(58f, 1.6f, 2.5f), new Vector3(33f, 2.5f, 12f));
        Eye("room guard-room", new Vector3(2f, 1.6f, 22f), new Vector3(13f, 1.6f, 28f));
        Eye("room prison", new Vector3(16f, 1.6f, 21.5f), new Vector3(28f, 1.6f, 28f));
        Eye("room torture-chamber", new Vector3(31f, 1.6f, 22f), new Vector3(44f, 1.6f, 28f));
        Eye("room crypt", new Vector3(46f, 1.6f, 22f), new Vector3(59f, 1.6f, 27f));
        Eye("room east-passage", new Vector3(61f, 1.6f, 8f), new Vector3(64f, 1.6f, 20f));
        Eye("room kitchen", new Vector3(2f, 1.6f, -2f), new Vector3(13f, 1.6f, -12f));
        Eye("room storeroom", new Vector3(16f, 1.6f, -2f), new Vector3(29f, 1.6f, -12f));
        Eye("room forge", new Vector3(16f, 1.6f, -18f), new Vector3(29f, 1.6f, -26f));
        Eye("room treasure-vault", new Vector3(1f, 1.6f, -18f), new Vector3(14f, 1.6f, -26f));
        Eye("room library", new Vector3(46f, 1.6f, -2f), new Vector3(59f, 1.6f, -12f));
        Eye("room shrine", new Vector3(31f, 1.6f, -2f), new Vector3(44f, 1.6f, -12f));
        Eye("room alchemists-study", new Vector3(46f, 1.6f, -18f), new Vector3(59f, 1.6f, -27f));
        Eye("room throne-room", new Vector3(37.5f, 1.6f, -15.5f), new Vector3(37.5f, 2.5f, -29f));

        // the named views
        // from beside the start, clear of the player's own body (the start is at 4, 7.5)
        Eye("armory-guarded-entrance-from-entry-hall", new Vector3(8.5f, 1.6f, 3f), new Vector3(15f, 1.6f, 7.5f));
        Eye("armory-way-in-beneath-gallery", new Vector3(40f, 1.6f, 4f), new Vector3(30f, 1.6f, 12.5f));
        Eye("dining-hall-floor-from-gallery-rail", new Vector3(42f, 6.2f, 8.5f), new Vector3(40f, 0.5f, -2f));
        Eye("throne-on-its-stage", new Vector3(37.5f, 1.6f, -18f), new Vector3(37.5f, 2.5f, -28.4f));
        Eye("vault-hidden-way-in", new Vector3(17.5f, 1.6f, -22.5f), new Vector3(14.5f, 1.6f, -22.5f));

        log.AppendLine("rendered shots to Captures/");
        return log.ToString();
    }

    static void Overview(string name, Vector3 pos, float halfHeight)
    {
        // as the in-game M overview: no fog, no post-processing, ceilings culled
        var cam = MakeCam(pos, Quaternion.Euler(90f, 0f, 0f));
        cam.orthographic = true;
        cam.orthographicSize = halfHeight;
        cam.cullingMask = ~(1 << ceilingLayer);
        cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        Save(cam, name);
        RenderSettings.fog = fog;
    }

    static void Eye(string name, Vector3 from, Vector3 to)
    {
        var cam = MakeCam(from, Quaternion.identity);
        cam.transform.LookAt(to);
        cam.fieldOfView = 70f;
        Save(cam, name);
    }

    static Camera MakeCam(Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Shot camera");
        go.transform.SetPositionAndRotation(pos, rot);
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 300f;
        cam.cullingMask = ~0;
        go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        return cam;
    }

    static void Save(Camera cam, string name)
    {
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        File.WriteAllBytes("Captures/" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(cam.gameObject);
    }
}
