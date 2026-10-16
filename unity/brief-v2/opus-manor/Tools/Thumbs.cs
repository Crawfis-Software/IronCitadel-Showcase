using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class Thumbs
{
    // args: comma separated list of prefab name substrings, output folder
    public static string Run(string names, string outDir)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lightGo = new GameObject("Sun");
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.5f;
        lightGo.transform.rotation = Quaternion.Euler(40, 140, 0);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
        cam.fieldOfView = 30;
        var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        Directory.CreateDirectory(outDir);
        var all = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
        int count = 0;
        foreach (var key in names.Split(','))
        {
            foreach (var path in all.Where(p => Path.GetFileNameWithoutExtension(p).Contains(key.Trim())))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { Object.DestroyImmediate(go); continue; }
                var b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                float rad = Mathf.Max(b.extents.magnitude, 0.1f);
                var dir = new Vector3(0.6f, 0.45f, -1f).normalized;
                cam.transform.position = b.center + dir * rad / Mathf.Sin(15 * Mathf.Deg2Rad) * 1.05f;
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = 0.01f; cam.farClipPlane = rad * 20 + 10;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, Path.GetFileNameWithoutExtension(path) + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);
                count++;
            }
        }
        return "thumbs " + count;
    }
}
