using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Catalog
{
    // args: "path;path;...#tag[#back]". Renders each prefab framed in its own tile; composes a contact sheet Notes/cat_<tag>.png (6 columns, row-major).
    public static string Run(string args)
    {
        var parts = args.Split('#');
        var list = parts[0].Split(';');
        string tag = parts.Length > 1 ? parts[1] : "a";
        bool back = parts.Length > 2 && parts[2] == "back";
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
        int cols = 6, tile = 300;
        int rows = (list.Length + cols - 1) / cols;
        var sheet = new Texture2D(cols * tile, rows * tile, TextureFormat.RGB24, false);
        var camGo = new GameObject("CatCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.25f, 0.3f);
        cam.fieldOfView = 35;
        for (int i = 0; i < list.Length; i++)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(list[i].Trim());
            if (p == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
            go.transform.position = new Vector3(i * 100, 0, 0);
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float rad = b.extents.magnitude;
            float dist = rad / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            var dir = new Vector3(0.6f, 0.55f, back ? 1f : -1f).normalized;
            cam.transform.position = b.center + dir * dist;
            cam.transform.LookAt(b.center);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = dist * 4;
            var rt = RenderTexture.GetTemporary(tile, tile, 24);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, tile, tile), (i % cols) * tile, (rows - 1 - i / cols) * tile);
            RenderTexture.active = null; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
        }
        sheet.Apply();
        File.WriteAllBytes("Notes/cat_" + tag + ".png", sheet.EncodeToPNG());
        Object.DestroyImmediate(camGo);
        return "ok " + list.Length;
    }

    public static void Shoot(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
