using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Renders the rooms scene through every LUT in a folder, one global ColorLookup override at a time, to
    /// choose the ambience moods' LUTs from real URP frames. The scene grade stays on; the room mood volumes are
    /// switched off for the probe, and the scene is never saved.
    ///
    /// <para>Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelLutProbe.Run
    /// [-luts &lt;folder&gt;] [-out &lt;dir&gt;] [-asis]</c>. Writes &lt;out&gt;/&lt;view&gt;/&lt;lut&gt;.jpg, _none.jpg (no LUT) and
    /// probe.txt. Each LUT is flipped vertically first: Cinema Suite strips keep green 0 on the top row (the legacy
    /// image-effect layout) and URP reads the bottom row as green 0. -asis renders them unflipped.</para></summary>
    public static class IronCitadelLutProbe
    {
        const string DefaultLuts = "Assets/Cinema Suite/Cinema Themes/LUTs";
        const string DefaultOut = "C:/Repos/IronCitadel/luts";
        const int W = 480, H = 270;

        // x, y, z, yaw, pitch: mid-shot poses from the flythrough
        static readonly (string name, float[] p)[] Views =
        {
            ("kitchen", new[] { 67.73f, 1.72f, -72.02f, 20.46f, 3.02f }),
            ("prison", new[] { 67.38f, 1.82f, -115.84f, -26.16f, 4.61f }),
            ("throne", new[] { 114.63f, 1.84f, -19.39f, -31.39f, 0.87f }),
            ("library", new[] { 155.38f, 1.82f, -148.43f, 40.15f, 3.01f }),
        };

        public static void Run()
        {
            var log = new StringBuilder($"IronCitadelLutProbe {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
            string outDir = Arg("-out", DefaultOut);
            int code = 0;
            try
            {
                string lutDir = Arg("-luts", DefaultLuts);
                EditorSceneManager.OpenScene(IronCitadelRoomsAssembler.ScenePath, OpenSceneMode.Single);
                int off = 0;
                foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
                    if (!v.isGlobal) { v.enabled = false; off++; }
                log.AppendLine($"scene {IronCitadelRoomsAssembler.ScenePath}; {off} local volume(s) off");

                var luts = AssetDatabase.FindAssets("t:Texture2D", new[] { lutDir })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).ToList();
                log.AppendLine($"{luts.Count} LUT(s) under {lutDir}");

                var camGo = new GameObject("~LutProbeCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
                cam.allowHDR = true;
                cam.fieldOfView = 55f;
                cam.aspect = W / (float)H;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                var volGo = new GameObject("~LutProbeVolume");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true; vol.priority = 50f;
                var prof = ScriptableObject.CreateInstance<VolumeProfile>();
                var cl = prof.Add<ColorLookup>(true);
                vol.sharedProfile = prof;

                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                rt.Create();
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);

                void Shoot(string view, string file, int renders)
                {
                    for (int i = 0; i < renders; i++)
                    {
                        VolumeManager.instance.Update(cam.transform, data.volumeLayerMask);
                        var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                        if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                        else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                    }
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(outDir, view, file + ".jpg"), tex.EncodeToJPG(90));
                }

                bool asis = Environment.GetCommandLineArgs().Contains("-asis");
                var used = luts.Select(t => (t.name, asis ? t : FlipY(t))).ToList();
                log.AppendLine(asis ? "LUTs as imported" : "LUTs flipped vertically");

                foreach (var (view, p) in Views)
                {
                    Directory.CreateDirectory(Path.Combine(outDir, view));
                    cam.transform.SetPositionAndRotation(new Vector3(p[0], p[1], p[2]), Quaternion.Euler(p[4], p[3], 0f));
                    cl.texture.Override(null); cl.contribution.Override(0f);
                    Shoot(view, "_none", 3);
                    foreach (var (name, t) in used)
                    {
                        cl.texture.Override(t); cl.contribution.Override(1f);
                        Shoot(view, name, 1);
                    }
                    log.AppendLine($"{view}: {used.Count + 1} frame(s)");
                }
                log.AppendLine($"ColorLookup valid for {used[0].name}: {Valid(cl, used[0].Item2)}");
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(volGo);
                log.AppendLine("RESULT ok");
            }
            catch (Exception e)
            {
                log.AppendLine("RESULT failed: " + e);
                code = 1;
            }
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "probe.txt"), log.ToString());
            Debug.Log(log.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static bool Valid(ColorLookup cl, Texture2D t)
        {
            cl.texture.Override(t);
            return cl.ValidateLUT();
        }

        /// <summary>A copy of <paramref name="src"/> with its rows reversed (linear, no mips, clamped), through the GPU so
        /// the source need not be readable.</summary>
        static Texture2D FlipY(Texture2D src)
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt, new Vector2(1f, -1f), new Vector2(0f, 1f));
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true)
                { name = src.name + "_flipY", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        static string Arg(string name, string fallback)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1].Replace('\\', '/') : fallback;
        }
    }
}
