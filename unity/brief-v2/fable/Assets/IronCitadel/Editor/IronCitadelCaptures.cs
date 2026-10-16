using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.EditorTools
{
    /// <summary>Renders the deliverable captures (and ad-hoc shots) from the open scene with a temporary URP camera, writing PNGs under Captures/ at the project root.</summary>
    public static class IronCitadelCaptures
    {
        public class Shot { public string name; public Vector3 pos; public float yaw, pitch; public bool ortho; public float orthoSize; public float fov = 70f; }

        public static readonly List<Shot> Deliverables = new List<Shot>
        {
            new Shot { name = "overview_top", pos = new Vector3(10f, 90f, 22.5f), yaw = 0f, pitch = 90f, ortho = true, orthoSize = 36f },
            new Shot { name = "room_entry_hall", pos = new Vector3(-1.8f, 1.6f, -1.6f), yaw = -130f, pitch = 3f },
            new Shot { name = "armory_guarded_entrance_from_entry_hall", pos = new Vector3(-7.5f, 1.6f, -6.5f), yaw = 0f, pitch = 0f },
            new Shot { name = "room_armory", pos = new Vector3(-7.5f, 1.6f, 1.2f), yaw = 0f, pitch = 2f },
            new Shot { name = "armory_way_in_beneath_gallery", pos = new Vector3(7.5f, 1.6f, 7.5f), yaw = -90f, pitch = 3f },
            new Shot { name = "room_forge", pos = new Vector3(-13.5f, 1.6f, 16.2f), yaw = 35f, pitch = 3f },
            new Shot { name = "room_guard_room", pos = new Vector3(-16.0f, 1.6f, -8.4f), yaw = -55f, pitch = 4f },
            new Shot { name = "room_prison", pos = new Vector3(-20.2f, 1.6f, 0.8f), yaw = 5f, pitch = 4f },
            new Shot { name = "room_torture_chamber", pos = new Vector3(-17.2f, 1.6f, 15.8f), yaw = -40f, pitch = 4f },
            new Shot { name = "room_crypt", pos = new Vector3(-18.0f, 1.6f, 27.0f), yaw = -115f, pitch = 4f },
            new Shot { name = "vault_hidden_way_in", pos = new Vector3(-26.3f, 1.6f, 27.5f), yaw = -90f, pitch = 0f },
            new Shot { name = "room_treasure_vault", pos = new Vector3(-30.6f, 1.6f, 25.8f), yaw = -115f, pitch = 8f },
            new Shot { name = "room_stair_hall", pos = new Vector3(-2.2f, 1.6f, 26.0f), yaw = -68f, pitch = 8f },
            new Shot { name = "room_dining_hall", pos = new Vector3(7.2f, 1.6f, 2.0f), yaw = 15f, pitch = 0f },
            new Shot { name = "hall_floor_from_gallery_rail", pos = new Vector3(4.2f, 6.7f, 16f), yaw = 105f, pitch = 22f },
            new Shot { name = "room_gallery", pos = new Vector3(2.5f, 6.7f, 33.5f), yaw = 180f, pitch = 4f },
            new Shot { name = "room_kitchen", pos = new Vector3(21.3f, 1.6f, 6.6f), yaw = 125f, pitch = 4f },
            new Shot { name = "room_storeroom", pos = new Vector3(30.9f, 1.6f, 2.6f), yaw = 55f, pitch = 3f },
            new Shot { name = "room_library", pos = new Vector3(21.2f, 1.6f, 17.5f), yaw = 90f, pitch = 2f },
            new Shot { name = "room_alchemists_study", pos = new Vector3(36.2f, 1.6f, 17.2f), yaw = 130f, pitch = 5f },
            new Shot { name = "room_shrine", pos = new Vector3(37.2f, 1.6f, 21.0f), yaw = 30f, pitch = 0f },
            new Shot { name = "room_antechamber", pos = new Vector3(34.2f, 1.6f, 27.6f), yaw = -60f, pitch = 0f },
            new Shot { name = "room_throne_room", pos = new Vector3(30f, 1.6f, 31.8f), yaw = 0f, pitch = 2f },
            new Shot { name = "throne_on_its_stage", pos = new Vector3(30f, 1.6f, 45.5f), yaw = 0f, pitch = -4f },
        };

        [MenuItem("Tools/Iron Citadel/Render captures")]
        public static void RenderAll() { Render(Deliverables, "Captures"); }

        public static string Render(List<Shot> shots, string folder, int w = 1600, int h = 900)
        {
            if (SceneManager_IsNotIronCitadel()) EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath);
            Directory.CreateDirectory(folder);
            var go = new GameObject("CaptureCamera");
            var cam = go.AddComponent<Camera>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.renderShadows = true;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.cullingMask = ~(1 << IronCitadelBuilder.MapOnlyLayer);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var written = new List<string>();
            // a soft top light for the plan view only (the game's M map does the same)
            var sunGo = new GameObject("CaptureSun"); var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(0.9f, 0.92f, 1f); sun.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(80f, 20f, 0f); sun.enabled = false;
            // the player's own capsule mesh is hidden while capturing (the player sees it from inside anyway)
            var playerRoot = GameObject.Find("IronCitadelPlayer");
            var hidden = new List<Renderer>();
            if (playerRoot != null) foreach (var r in playerRoot.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            try
            {
                foreach (var s in shots)
                {
                    sun.enabled = s.ortho;
                    cam.transform.position = s.pos; cam.transform.rotation = Quaternion.Euler(s.pitch, s.yaw, 0f);
                    cam.orthographic = s.ortho; cam.orthographicSize = s.orthoSize; cam.fieldOfView = s.fov;
                    cam.cullingMask = s.ortho ? ~((1 << IronCitadelBuilder.MapOnlyLayer) | (1 << IronCitadelBuilder.CeilingLayer)) : ~(1 << IronCitadelBuilder.MapOnlyLayer);
                    data.renderPostProcessing = !s.ortho;
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = null;
                    string path = Path.Combine(folder, s.name + ".png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    written.Add(path);
                }
            }
            finally
            {
                cam.targetTexture = null;
                foreach (var r in hidden) r.enabled = true;
                Object.DestroyImmediate(go); Object.DestroyImmediate(sunGo); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
            return string.Join("\n", written);
        }

        static bool SceneManager_IsNotIronCitadel()
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return s.path != IronCitadelBuilder.ScenePath;
        }

        /// <summary>Ad-hoc: "name,x,y,z,yaw,pitch" entries separated by ';' rendered into Captures/scratch.</summary>
        public static string Adhoc(string spec)
        {
            var list = new List<Shot>();
            foreach (var e in spec.Split(';'))
            {
                var a = e.Split(',');
                if (a.Length < 6) continue;
                list.Add(new Shot { name = a[0], pos = new Vector3(float.Parse(a[1]), float.Parse(a[2]), float.Parse(a[3])), yaw = float.Parse(a[4]), pitch = float.Parse(a[5]) });
            }
            return Render(list, "Captures/scratch");
        }
    }
}
