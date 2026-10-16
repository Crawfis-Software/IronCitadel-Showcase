using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Cap = IronCitadel.Rooms.Editor.IronCitadelCapture;
using Object = UnityEngine.Object;

namespace IronCitadelV2.Editor
{
    public static partial class IronCitadelV2Builder
    {
        static bool s_warm;
        static int s_blank;

        /// <summary>PNGs of the saved scene into &lt;run&gt;/captures: the map with the ceilings hidden (flat light, and
        /// the night as walked with the grade), one eye-level view per room from its first door, and the places the
        /// design leans on: the start, the flank door from the hall, the gallery and the hall floor, the hidden door,
        /// the throne. The walker and its HUD are hidden while they render; everything changed is put back.</summary>
        static void Captures(Ctx c)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string dir = c.runDir + "/captures";
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png")) File.Delete(old);
            s_blank = 0;
            bool playWas = c.playT != null && c.playT.gameObject.activeSelf;
            if (c.playT != null) c.playT.gameObject.SetActive(false);
            var shots = new List<string>();
            try
            {
                float W = c.level.tile * c.level.cols, D = c.level.tile * c.level.rows;
                var hidden = Cap.HideCeilings(c.root);
                try
                {
                    int pw = 2400, ph = Mathf.RoundToInt(pw * (D + 8f) / (W + 8f));
                    Ortho(dir + "/overview-rig.png", new Vector2(W / 2f, -D / 2f), W + 8f, D + 8f, pw, ph, true, false); shots.Add("overview-rig");
                    Ortho(dir + "/overview-night.png", new Vector2(W / 2f, -D / 2f), W + 8f, D + 8f, pw, ph, false, true); shots.Add("overview-night");
                }
                finally { Cap.Restore(hidden); }

                // one view per room: from the floor point and heading that see the most of the room's floor
                foreach (var room in c.level.rooms)
                {
                    var eyes = c.floor.samples.Where(s => s.room == room.id && s.reach && Mathf.Abs(s.y) <= FloorBand && s.clear >= 2.2f && s.i % 3 == 0 && s.j % 3 == 0).ToList();
                    var targets = c.floor.samples.Where(s => s.room == room.id && s.reach && (s.i & 1) == 0 && (s.j & 1) == 0).ToList();
                    if (BestView(c, eyes, targets, out var eye, out var yaw, out var pitch))
                    {
                        Persp(dir + $"/room-{room.id}.png", eye, yaw, Mathf.Clamp(pitch, 2f, 20f), 75f);
                        shots.Add("room-" + room.id);
                    }
                    else c.Log($"captures: no view found in {room.id}");
                }

                // the start, as the player first sees it
                Persp(dir + "/start.png", c.start + Vector3.up * 1.7f, c.startYaw, 4f, 75f); shots.Add("start");

                // each door with a role the tactics lean on (the flank), seen from b's side; the hidden door from a's
                foreach (var d in c.level.doors)
                {
                    bool flank = d.roles.Contains("flank");
                    if (!flank && d.kind != "hidden") continue;
                    if (!c.openings.TryGetValue(d.id, out var o) || !o.found) continue;
                    var from = d.kind == "hidden" ? -o.e.n : o.e.n;      // e.n points from a to b
                    var stand = Snap(c, o.Centre + from * 5f, 2f);
                    var look = o.Centre + Vector3.up * 1.4f - (stand + Vector3.up * 1.7f);
                    string name = d.kind == "hidden" ? "hidden-door" : "flank-door";
                    Persp(dir + $"/{name}-{d.id}.png", stand + Vector3.up * 1.7f, V2Model.Yaw(look), Pitch(look), 70f);
                    shots.Add($"{name}-{d.id}");
                }

                // the gallery: down onto the hall floor, and from the floor up at it
                var gallery = c.level.galleryRoom != null ? c.level.Room(c.level.galleryRoom) : null;
                if (gallery != null)
                {
                    var high = c.floor.samples.Where(s => s.room == gallery.id && s.reach && s.y > 2.5f).ToList();
                    var low = c.floor.samples.Where(s => s.room == gallery.id && s.reach && Mathf.Abs(s.y) <= FloorBand).ToList();
                    if (high.Count > 0 && low.Count > 0)
                    {
                        // down: the gallery point that sees the most hall floor; up: the floor point that sees the most gallery
                        if (BestView(c, high.Where(s => (s.i & 1) == 0 && (s.j & 1) == 0).ToList(), low.Where(s => (s.i & 1) == 0 && (s.j & 1) == 0).ToList(), out var ge, out var gy, out var gpitch))
                        { Persp(dir + "/gallery-down.png", ge, gy, Mathf.Clamp(gpitch, 8f, 45f), 75f); shots.Add("gallery-down"); }
                        if (BestView(c, low.Where(s => s.clear >= 2.2f && s.i % 3 == 0 && s.j % 3 == 0).ToList(), high, out var fe, out var fy, out var fpitch))
                        { Persp(dir + "/gallery-up.png", fe, fy, Mathf.Clamp(fpitch, -40f, 0f), 75f); shots.Add("gallery-up"); }
                    }
                    else c.Log($"captures: no reachable floor over 2.5 m in {gallery.id}, so no gallery shots");
                }

                // the throne, from 9 m out toward the middle of its room
                if (c.goal != null)
                {
                    var room = c.level.Room(c.level.goalRoom);
                    var gpos = c.goal.position;
                    var outward = V2Model.Flat(FloorCentroid(c, room) - gpos);
                    if (outward.sqrMagnitude < 1f) outward = Vector3.forward;
                    var stand = Snap(c, gpos + outward.normalized * 9f, 3f);
                    var look = gpos + Vector3.up * 1.6f - (stand + Vector3.up * 1.7f);
                    Persp(dir + "/throne.png", stand + Vector3.up * 1.7f, V2Model.Yaw(look), Pitch(look), 70f); shots.Add("throne");
                }
            }
            finally
            {
                if (c.playT != null) c.playT.gameObject.SetActive(playWas);
            }
            c.Log($"captures: {shots.Count} PNG(s) in {dir} ({sw.Elapsed.TotalSeconds:0.0} s): {string.Join(", ", shots)}");
            if (s_blank > 0) c.Problem($"captures: {s_blank} of {shots.Count} PNG(s) came out blank (the render failed; the console says why)");
        }

        static Vector3 SamplePos(Ctx c, Sample s) => new Vector3(c.floor.ox + s.i + 0.5f, s.y, c.floor.oz + s.j + 0.5f);

        /// <summary>The eye (1.7 m over one of <paramref name="eyes"/>) and heading (every 15 degrees) that see the
        /// most of <paramref name="targets"/>: a target counts when the line from the eye to 0.3 m over it is clear and
        /// it lies within 52.5 degrees of the heading (a 16:9 frame at 75 degrees shows about that), weighted by its
        /// distance up to 15 m so a view across the room beats a corner. The pitch looks at the seen targets' mean.
        /// A heading with anything solid within 2 m of the eye inside 30 degrees of it (a pillar filling half the
        /// frame) is passed over; only when that leaves no view at all is the test dropped.</summary>
        static bool BestView(Ctx c, List<Sample> eyes, List<Sample> targets, out Vector3 eye, out float yaw, out float pitch) =>
            BestView(c, eyes, targets, true, out eye, out yaw, out pitch) || BestView(c, eyes, targets, false, out eye, out yaw, out pitch);

        static bool BestView(Ctx c, List<Sample> eyes, List<Sample> targets, bool clearForeground, out Vector3 eye, out float yaw, out float pitch)
        {
            eye = default; yaw = 0f; pitch = 0f;
            float best = 0f;
            var w = new float[24]; var dy = new float[24]; var dd = new float[24]; var n = new int[24];
            var near = new float[48];
            foreach (var e in eyes)
            {
                var ep = SamplePos(c, e) + Vector3.up * 1.7f;
                Array.Clear(w, 0, 24); Array.Clear(dy, 0, 24); Array.Clear(dd, 0, 24); Array.Clear(n, 0, 24);
                for (int q = 0; q < 48 && clearForeground; q++)
                {
                    var dir = Quaternion.Euler(0f, q * 7.5f, 0f) * Vector3.forward;
                    near[q] = Physics.Raycast(ep, dir, out var nh, 2f, AllLayers, QueryTriggerInteraction.Ignore) ? nh.distance : 2f;
                }
                foreach (var t in targets)
                {
                    var tp = SamplePos(c, t) + Vector3.up * 0.3f;
                    var fl = V2Model.Flat(tp - ep);
                    float dist = fl.magnitude;
                    if (dist < 1.5f || dist > 45f) continue;
                    if (Physics.Linecast(ep, tp, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                    int b = Mathf.FloorToInt(Mathf.Repeat(V2Model.Yaw(fl), 360f) / 15f) % 24;
                    w[b] += Mathf.Min(dist, 15f); dy[b] += tp.y - ep.y; dd[b] += dist; n[b]++;
                }
                for (int k = 0; k < 24; k++)
                {
                    if (clearForeground)
                    {
                        bool blocked = false;     // heading k looks along 7.5 + 15k degrees, step 2k+1 of the 7.5-degree fan
                        for (int m = -4; m <= 4 && !blocked; m++) blocked = near[(2 * k + 1 + m + 48) % 48] < 2f;
                        if (blocked) continue;
                    }
                    float s = 0f, sy = 0f, sd = 0f; int sn = 0;
                    for (int m = -3; m <= 3; m++) { int q = (k + m + 24) % 24; s += w[q]; sy += dy[q]; sd += dd[q]; sn += n[q]; }
                    if (s <= best || sn == 0) continue;
                    best = s; eye = ep; yaw = k * 15f + 7.5f;
                    pitch = -Mathf.Atan2(sy / sn, sd / sn) * Mathf.Rad2Deg;
                }
            }
            return best > 0f;
        }

        static Vector3 FloorCentroid(Ctx c, V2Room room)
        {
            var pts = c.floor.samples.Where(s => s.room == room.id && s.reach && Mathf.Abs(s.y) <= FloorBand).ToList();
            if (pts.Count == 0) return V2Model.CellCenter(c.level.tile, room.r, room.c);
            var sum = Vector3.zero;
            foreach (var s in pts) sum += SamplePos(c, s);
            var mean = sum / pts.Count;
            // the reachable point nearest the mean, so an L-shaped room's middle is still floor
            return SamplePos(c, pts.OrderBy(s => (SamplePos(c, s) - mean).sqrMagnitude).ThenBy(s => s.id).First());
        }

        static Vector3 Snap(Ctx c, Vector3 p, float radius) =>
            NavMesh.SamplePosition(p + Vector3.up * 0.3f, out var h, radius, c.filter) ? h.position : p;

        /// <summary>Euler pitch (down is positive) that looks along <paramref name="d"/>.</summary>
        static float Pitch(Vector3 d) => -Mathf.Atan2(d.y, V2Model.Flat(d).magnitude) * Mathf.Rad2Deg;

        static void Ortho(string png, Vector2 centreXZ, float w, float d, int pw, int ph, bool rig, bool post)
        {
            var cam = NewCam("~V2Map");
            try
            {
                cam.orthographic = true;
                cam.orthographicSize = d / 2f;
                cam.aspect = pw / (float)ph;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 450f;
                cam.transform.SetPositionAndRotation(new Vector3(centreXZ.x, 400f, centreXZ.y), Quaternion.Euler(90f, 0f, 0f));
                SavePng(RenderCam(cam, pw, ph, rig, post), png);
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        static void Persp(string png, Vector3 eye, float yaw, float pitch, float fov, int w = 1280, int h = 720)
        {
            var cam = NewCam("~V2Eye");
            try
            {
                cam.orthographic = false;
                cam.fieldOfView = fov;
                cam.aspect = w / (float)h;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 400f;
                cam.transform.SetPositionAndRotation(eye, Quaternion.Euler(pitch, yaw, 0f));
                SavePng(RenderCam(cam, w, h, false, true), png);
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        static Camera NewCam(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f, 1f);

            return cam;
        }

        /// <summary>v1's offscreen render (IronCitadelCapture), with the camera's post-processing switchable (the grade
        /// and the room looks are post-processing) and any aspect. rig: flat grey ambient and two soft directional
        /// lights with the scene's lights off.</summary>
        static Texture2D RenderCam(Camera cam, int w, int h, bool rig, bool post)
        {
            var acd = cam.GetUniversalAdditionalCameraData();
            acd.renderPostProcessing = post;
            acd.volumeLayerMask = 1;              // the Default layer, where the grade and the room volumes live
            acd.volumeTrigger = cam.transform;
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
                    foreach (var root in cam.gameObject.scene.GetRootGameObjects())
                        foreach (var l in root.GetComponentsInChildren<Light>(false))
                        {
                            if (!l.enabled) continue;
                            l.enabled = false;
                            suppressed.Add(l);
                        }
                    rigLights.Add(RigLight(0.55f, new Vector3(50f, 50f, 0f)));
                    rigLights.Add(RigLight(0.45f, new Vector3(-30f, -60f, 0f)));
                }
                // The build's NewScene (a single-scene switch) unloads the main volume stack's components, and URP's
                // render-request path does not reload them: the uber pass then asserts on a null ColorAdjustments
                // and every later render in the same call fails. Reload them first.
                VolumeManager.instance.CheckDefaultVolumeState();
                VolumeManager.instance.CheckStack(VolumeManager.instance.stack);
                bool prevAsync = ShaderUtil.allowAsyncCompilation;
                ShaderUtil.allowAsyncCompilation = false;
                try
                {
                    if (!s_warm) { RenderOnce(cam, rt); s_warm = true; }
                    RenderOnce(cam, rt);
                }
                finally { ShaderUtil.allowAsyncCompilation = prevAsync; }
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                // a failed render leaves the target cleared to transparent black
                bool blank = true;
                for (int y = 0; y < 3 && blank; y++)
                    for (int x = 0; x < 3 && blank; x++)
                        blank = tex.GetPixel((2 * x + 1) * w / 6, (2 * y + 1) * h / 6).a == 0f;
                if (blank) s_blank++;
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
            var go = new GameObject("~V2RigLight") { hideFlags = HideFlags.DontSave };
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

        static void SavePng(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
