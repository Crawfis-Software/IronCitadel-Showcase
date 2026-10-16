using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hollowmere.EditorTools
{
    /// <summary>The captures delivered with the level: one eye-level shot per room and the five set pieces.</summary>
    public static class ShotList
    {
        public struct Shot
        {
            public string file; public Vector3 eye; public Vector3 look;
            public Shot(string f, float x, float fy, float z, float lx, float ly, float lz) { file = f; eye = new Vector3(x, fy, z); look = new Vector3(lx, ly, lz); }
        }

        public static readonly List<Shot> Rooms = new List<Shot>
        {
            new Shot("room_01_foyer", 18.2f, 0f, 0.9f, 9.6f, 1.2f, 7.0f),
            new Shot("room_02_gun_room", 20.2f, 0f, 19.0f, 11.0f, 1.0f, 12.8f),
            new Shot("room_03_watch_room", 8.25f, 0f, 6.2f, 4.8f, 0.7f, 1.2f),
            new Shot("room_04_cellar_cells", 3.3f, 0f, 3.6f, -13f, 0.9f, 3.4f),
            new Shot("room_05_butchers_room", -11.8f, 0f, 9.9f, -6.5f, 0.9f, 15.5f),
            new Shot("room_06_boiler_room", 2.4f, 0f, 9.9f, 0.6f, 1.6f, 17.4f),
            new Shot("room_07_kitchen", -3.9f, 0f, 26.4f, -12f, 0.8f, 19.5f),
            new Shot("room_08_storeroom", -8f, 0f, 27.9f, -8.5f, 0.9f, 34f),
            new Shot("room_09_back_stair_extra", 0.7f, 0f, 32.2f, 0.8f, 3.6f, 24.6f),
            new Shot("room_10_dining_hall", 3.4f, 0f, 35.4f, 18f, 3.2f, 24f),
            new Shot("room_11_library", 16.6f, 0f, 37.0f, 15.6f, 2.0f, 46f),
            new Shot("room_12_strong_room", 3.3f, 0f, 41.0f, -0.6f, 0.7f, 40.8f),
            new Shot("room_13_laboratory", 15.0f, 0f, 47.0f, 22.5f, 0.9f, 52.6f),
            new Shot("room_14_ballroom", 16f, 0f, 55.0f, 16f, 2.4f, 76f),
            new Shot("room_15_chapel", 30.9f, 0f, 30f, 40f, 2.0f, 30f),
            new Shot("room_16_crypt", 35f, 0f, 23.1f, 35f, 0.6f, 12f),
        };

        public static readonly List<Shot> SetPieces = new List<Shot>
        {
            new Shot("set_1_guarded_entrance_from_foyer", 14f, 0f, 1.6f, 14f, 1.3f, 9f),
            new Shot("set_2_way_into_gun_room_beneath_gallery", 9.5f, 0f, 22.9f, 17f, 1.1f, 20f),
            new Shot("set_3_hall_floor_from_gallery_rail", 13.6f, 4.2f, 23.15f, 16f, 0f, 30.5f),
            new Shot("set_4_masters_chair_on_stage", 16.6f, 0f, 63.5f, 16f, 1.9f, 74.6f),
            new Shot("set_5_strong_room_hidden_way_in", 7.4f, 0f, 39.2f, 2.0f, 1.0f, 40.8f),
        };

        public static string RenderAll(string dir, bool rooms = true, bool sets = true, int w = 1600, int h = 900)
        {
            Directory.CreateDirectory(dir);
            int n = 0;
            if (rooms) foreach (var s in Rooms) { Shots.Eye(Path.Combine(dir, s.file + ".png"), s.eye.x, s.eye.y, s.eye.z, s.look, w, h); n++; }
            if (sets)
                foreach (var s in SetPieces)
                {
                    if (s.file.Contains("hidden")) continue; // rendered by RenderHidden, with the bookcase swung open
                    Shots.Eye(Path.Combine(dir, s.file + ".png"), s.eye.x, s.eye.y, s.eye.z, s.look, w, h); n++;
                }
            return "rendered " + n;
        }

        /// <summary>
        /// Swings the hidden bookcase open or shut in the open scene (not saved). Call, let the editor tick once (the
        /// GPU Resident Drawer picks up moved renderers on the next frame), then call RenderHidden, then shut it again.
        /// </summary>
        public static string SetHidden(bool open)
        {
            foreach (var d in Object.FindObjectsByType<SwingDoor>(FindObjectsInactive.Exclude)) if (d.hidden) d.SetOpenImmediate(open);
            UnityEditor.SceneView.RepaintAll();
            return "hidden door " + (open ? "open" : "shut");
        }

        public static string RenderHidden(string dir, int w = 1600, int h = 900)
        {
            foreach (var s in SetPieces)
                if (s.file.Contains("hidden")) Shots.Eye(Path.Combine(dir, s.file + ".png"), s.eye.x, s.eye.y, s.eye.z, s.look, w, h);
            return "rendered hidden";
        }
    }
}
