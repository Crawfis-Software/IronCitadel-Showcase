using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronCitadel.Editor
{
    /// <summary>Adds a scripted walk-through to the open level and enters play mode; results land in Tools/playtest.txt.</summary>
    public static class Playtest
    {
        static readonly List<PlaytestDriver.Step> Route = new List<PlaytestDriver.Step>();

        static void S(string name, float x, float z, string action = "", bool sprint = false, float timeout = 30f)
        {
            Route.Add(new PlaytestDriver.Step { name = name, target = new Vector3(x, 0f, z), action = action, sprint = sprint, timeout = timeout });
        }

        static void BuildRoute()
        {
            Route.Clear();
            // act one: the lobby and the closed gate
            S("lobby", 112.5f, -165f, "shot");
            S("gate_blocked", 112.5f, -136.5f, "shot", false, 10f);
            S("lobby2", 112.5f, -158f);
            // the west wing: guard room, prison, the secret vault, kitchen
            S("west_passage", 96f, -157.5f);
            S("guard_passage", 82.5f, -157.5f);
            S("guard_turn", 82.5f, -152.5f);
            S("guard_room", 74f, -151f, "shot");
            S("guard_n1", 77.5f, -147.5f);
            S("guard_n2", 77.5f, -142.5f);
            S("guard_n3", 67.5f, -142.5f);
            S("guard_door", 67.5f, -137.5f);
            S("prison_south", 67.5f, -131f, "shot");
            S("aisle_mid", 67.5f, -112.5f);
            S("warden_in", 57.5f, -112.5f);
            S("warden", 49f, -112.5f);
            S("bookcase", 47.1f, -112.5f, "interact");
            S("vault_door", 44.3f, -112.5f, "", false, 15f);
            S("vault", 38f, -112.5f, "shot");
            S("back_warden", 49f, -112.5f);
            S("aisle_back", 67.5f, -112.5f);
            S("aisle_n", 67.5f, -102.5f);
            S("cross1", 72.5f, -102.5f);
            S("cross2", 72.5f, -97.5f);
            S("cross3", 67.5f, -97.5f);
            S("prison_north", 67.5f, -92.5f);
            S("kitchen_s", 67.5f, -87.5f);
            S("k2", 67.5f, -77.5f);
            S("k3", 72.5f, -77.5f);
            S("kitchen", 72.5f, -70f, "shot");
            S("k_e1", 77.5f, -62.5f);
            S("k_e2", 82.5f, -62.5f);
            S("k_e3", 82.5f, -67.5f);
            S("kitchen_door", 87.5f, -67.5f);
            // the great hall: landing, up the walled stair, along the gallery, down the other side
            S("landing_w", 92.5f, -67.5f, "shot");
            S("shaft_bottom", 92.5f, -73f);
            S("stair_top", 92.5f, -85.5f, "", false, 25f);
            S("deck_w", 92.5f, -87.5f);
            S("deck_mid", 112.5f, -87.8f);
            S("gallery_rail", 112.5f, -86.3f, "shot");
            S("overview", 112.5f, -86.5f, "overview");
            S("deck_e", 132.5f, -87.5f);
            S("stair_e_top", 132.5f, -85.5f);
            S("shaft_e_bottom", 132.5f, -73f, "", false, 25f);
            S("landing_e", 132.5f, -67.5f);
            S("hall_in", 126f, -67.5f);
            S("hall_ne", 126f, -55f);
            S("hall_mid", 112.5f, -55f, "shot");
            S("hall_s", 112.5f, -82.5f, "", true);
            S("walkway", 112.5f, -87.5f);
            // the armory from behind, the gear, the archers at the gate
            S("armory_door", 112.5f, -92.5f);
            S("a1", 112.5f, -97.5f);
            S("a2", 107.5f, -97.5f);
            S("a3", 107.5f, -102.5f);
            S("armory", 112.5f, -110f, "shot");
            S("pickup_side", 116.5f, -112f);
            S("pickup_s", 116.5f, -117.5f);
            S("pickup", 112.8f, -116.8f);
            S("gate_bay", 112.5f, -129f, "shot", false, 20f);
            S("a_back1", 107.5f, -102.5f);
            S("a_back2", 107.5f, -97.5f);
            S("a_back3", 112.5f, -97.5f);
            S("a_back4", 112.5f, -92.5f);
            // north across the hall to the throne
            S("hall_n", 112.5f, -50f, "", true);
            S("great_doors", 112.5f, -44.5f);
            S("t1", 112.5f, -37.5f);
            S("t2", 122.5f, -37.5f);
            S("t3", 122.5f, -32.5f);
            S("t4", 122.5f, -27.5f, "shot");
            S("runner", 112.5f, -20f);
            S("steps", 112.5f, -8.5f, "", false, 20f);
            S("throne", 112.5f, -4.6f, "shot");
        }

        [MenuItem("Tools/Iron Citadel/Playtest route")]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != IronCitadelBuilder.ScenePath)
                EditorSceneManager.OpenScene(IronCitadelBuilder.ScenePath, OpenSceneMode.Single);
            foreach (var old in Object.FindObjectsByType<PlaytestDriver>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            BuildRoute();
            var hud = Object.FindFirstObjectByType<LevelHud>();
            var go = new GameObject("PlaytestDriver");
            var d = go.AddComponent<PlaytestDriver>();
            d.steps = new List<PlaytestDriver.Step>(Route);
            d.hud = hud;
            d.player = hud != null ? hud.player : null;
            if (d.player == null) { Debug.LogError("[Playtest] no player"); return; }
            System.IO.File.Delete("Tools/playtest.txt");
            EditorApplication.EnterPlaymode();
        }
    }
}
