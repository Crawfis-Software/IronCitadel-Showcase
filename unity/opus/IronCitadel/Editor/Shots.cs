using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IronCitadel.EditorTools
{
    /// <summary>Renders eye-level shots and the overview from the open IronCitadel scene to PNG files.</summary>
    public static class Shots
    {
        public const string Dir = "C:/Repos/ColdRuns/opus/Captures/";

        public static void Open()
        {
            if (EditorSceneManager.GetActiveScene().path != CitadelBuilder.ScenePath)
                EditorSceneManager.OpenScene(CitadelBuilder.ScenePath);
        }

        static void Prepare()
        {
            var root = GameObject.Find("IronCitadel");
            if (root != null) Kit.SimulateParticles(root.transform, 2f);
        }

        public static string Shot(string file, Vector3 pos, float yaw, float pitch = 0f, float fov = 65f, int w = 1920, int h = 1080, bool hideCeiling = false)
        {
            Prepare();
            var go = new GameObject("ShotCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                go.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0));
                cam.fieldOfView = fov;
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
                cam.cullingMask = ~(1 << Kit.OverviewLayer) & (hideCeiling ? ~(1 << Kit.CeilingLayer) : ~0);
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                Capture.RenderToPng(cam, w, h, Dir + file);
            }
            finally { Object.DestroyImmediate(go); }
            return Dir + file;
        }

        /// <summary>Eye level for a player standing at room-local (u, v) on floor height y.</summary>
        public static string Eye(string file, string roomId, float u, float v, float yaw, float pitch = 0f, float floorY = 0f, float fov = 65f, int w = 1920, int h = 1080)
        {
            var rm = Cell(roomId);
            return Shot(file, new Vector3(rm.x + u, floorY + 1.6f, rm.y + v), yaw, pitch, fov, w, h);
        }

        static Vector2 Cell(string id)
        {
            (int r, int c) = id switch
            {
                "entry_hall" => (3, 2), "guard_room" => (3, 1), "prison" => (2, 1), "vault" => (2, 0), "kitchen" => (1, 1),
                "great_hall" => (1, 2), "armory" => (2, 2), "throne_room" => (0, 2), "library" => (3, 3), "study" => (2, 3), "forge" => (1, 3),
                _ => (0, 0)
            };
            return new Vector2(c * 45f, -(r + 1) * 45f);
        }

        /// <summary>The deliverable: one eye-level shot per room (u, v, yaw, pitch, floor height).</summary>
        public static readonly (string id, float u, float v, float yaw, float pitch, float y)[] RoomShots =
        {
            ("entry_hall", 19f, 4.2f, 8f, -1f, 0f),
            ("guard_room", 32.8f, 27.8f, 228f, 7f, 0f),
            ("prison", 13.2f, 23.5f, 92f, 3f, 0f),
            ("vault", 41.6f, 22.5f, 262f, 8f, 0f),
            ("kitchen", 37.5f, 10f, 312f, 5f, 0f),
            ("great_hall", 22.5f, 10.6f, 0f, -5f, 0f),
            ("armory", 22.5f, 27f, 180f, 3f, 0f),
            ("throne_room", 22.5f, 16f, 0f, -7f, 0f),
            ("library", 40.5f, 24.5f, 262f, 2f, 0f),
            ("study", 22.5f, 13f, 0f, 9f, 0f),
            ("forge", 11f, 19.5f, 68f, 3f, 0f),
        };

        public static string All(string prefix = "", int w = 1920, int h = 1080, string only = null)
        {
            Open();
            var done = new System.Collections.Generic.List<string>();
            int i = 1;
            foreach (var s in RoomShots)
            {
                if (only == null || only.Contains(s.id))
                    done.Add(Eye($"{prefix}{i:00}_{s.id}.png", s.id, s.u, s.v, s.yaw, s.pitch, s.y, 68f, w, h));
                i++;
            }
            return string.Join(" | ", done);
        }

        /// <summary>Extra detail shots of the level's key features, in Captures/extras.</summary>
        public static string Extras(int w = 1600, int h = 900)
        {
            Open();
            System.IO.Directory.CreateDirectory(Dir + "extras");
            Eye("extras/gallery_view_from_rail.png", "great_hall", 18f, 7.9f, 10f, 22f, 5f, 68f, w, h);
            Eye("extras/gallery_and_arcade_from_hall.png", "great_hall", 30f, 22f, 200f, -6f, 0f, 68f, w, h);
            Eye("extras/walkway_under_gallery.png", "great_hall", 9.5f, 5.5f, 90f, 0f, 0f, 68f, w, h);
            Eye("extras/west_stair_from_landing.png", "great_hall", 4.2f, 24f, 175f, 8f, 0f, 70f, w, h);
            Eye("extras/armory_gate_from_entry.png", "entry_hall", 22.5f, 31f, 0f, 3f, 0f, 65f, w, h);
            Eye("extras/archers_from_behind.png", "armory", 23.5f, 13f, 185f, 6f, 0f, 65f, w, h);
            Eye("extras/warden_corner_bookcases_closed.png", "prison", 9.5f, 22.5f, 270f, 4f, 0f, 70f, w, h);
            var hinge = GameObject.Find("SecretBookcase (vault door)");
            var rot = hinge.transform.rotation;
            hinge.transform.rotation = Quaternion.Euler(0, -90f, 0) * rot;
            Eye("extras/warden_corner_bookcase_open.png", "prison", 9.5f, 22.5f, 270f, 4f, 0f, 70f, w, h);
            hinge.transform.rotation = rot;
            Eye("extras/throne_stage_close.png", "throne_room", 16f, 27f, 25f, -12f, 0f, 70f, w, h);
            return "extras done";
        }

        public static string Overview(string file, int size = 2048)
        {
            Prepare();
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "OverviewCamera");
            var light = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(l => l.name == "OverviewLight");
            bool fog = RenderSettings.fog; var amb = RenderSettings.ambientLight;
            bool lightOn = light.enabled;
            float ortho = cam.orthographicSize;
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
                light.enabled = true;
                cam.orthographicSize = 91f;
                Capture.RenderToPng(cam, size, size, Dir + file);
            }
            finally
            {
                RenderSettings.fog = fog; RenderSettings.ambientLight = amb; light.enabled = lightOn; cam.orthographicSize = ortho;
            }
            return Dir + file;
        }
    }
}
