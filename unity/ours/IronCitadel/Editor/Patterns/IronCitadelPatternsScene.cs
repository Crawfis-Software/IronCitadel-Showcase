using System;
using System.IO;
using System.Linq;
using System.Text;
using CrawfisSoftware.MacroTileBuilder.ChunkFill.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronCitadel.Patterns.Editor
{
    /// <summary>
    /// The Iron Citadel's PATTERN-TILE level as a scene: Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity holds the
    /// level prefab the solver's tiling assembled (synth assemble, Levels_PolygonDungeon_IronCitadel) and a "PlayerStart"
    /// at its way in. The level is 3 x 4 slots of 100 m (d-20 pattern tiles), centred on the origin with +z north, so it
    /// does not sit on level.json's 45 m grid.
    ///
    /// <para>Batch: <c>unity run &lt;Builder&gt; -- -executeMethod IronCitadel.Patterns.Editor.IronCitadelPatternsScene.BuildAndCapture
    /// -logFile &lt;log&gt;</c> builds the scene, then shoots a top-down overview with ceilings and roofs hidden to
    /// <see cref="CapturePath"/> (the scene is saved first, so the hidden state is never saved). It writes
    /// <see cref="ResultPath"/> either way. Menu: Iron Citadel/Patterns/Build scene (no capture). A rebuild replaces only
    /// the roots named <c>iron-citadel</c> and <c>PlayerStart</c>; everything else in the saved scene stays.</para>
    /// </summary>
    public static class IronCitadelPatternsScene
    {
        public const string LevelFolder = "Assets/CrawfisSoftware/PolygonDungeon/Levels/IronCitadel";
        public const string LevelName = "iron-citadel";
        public const string ScenePath = "Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity";
        public const string CapturePath = "C:/Repos/IronCitadel/captures/patterns-overview.png";
        public const string ResultPath = "C:/Repos/IronCitadel/logs/patterns-scene-result.txt";

        // The grid the solver used (C:/Repos/IronCitadel/patterns/iron-citadel-request.json): 3 columns x 4 rows of
        // 100 m slots, the assembled prefab centred on its own origin, row 0 the south row.
        private const int Columns = 3, Rows = 4;
        private const float SlotM = 100f;

        // The way in: slot (1,0)'s south edge, the level's south rim at "1/2" (stitched cells 29-30 of 60, 5 m each),
        // so x = 0 and the rim is z = -200. The start stands half a cell inside, facing north (+z).
        private static readonly Vector3 EntryOnRim = new Vector3(0f, 0f, -Rows * SlotM / 2f + 2.5f);
        private const float EntryYawDeg = 0f;

        private const string StartName = "PlayerStart";
        private static readonly string[] HiddenPrefixes = { "ceiling:", "roof:" };

        [MenuItem("Iron Citadel/Patterns/Build scene")]
        public static void BuildMenu()
        {
            var log = new StringBuilder();
            Build(log);
            Debug.Log(log.ToString());
        }

        /// <summary>Batch entry: build and save the scene, then capture the overview.</summary>
        public static void BuildAndCapture()
        {
            var log = new StringBuilder();
            log.AppendLine($"IronCitadelPatternsScene {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            try
            {
                GameObject level = Build(log);
                Capture(level, log);
                log.AppendLine("RESULT ok");
            }
            catch (Exception e)
            {
                log.AppendLine("RESULT failed: " + e);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, log.ToString());
            Debug.Log(log.ToString());
        }

        private static GameObject Build(StringBuilder log)
        {
            string guid = AssetDatabase.FindAssets("t:Prefab", new[] { LevelFolder })
                .FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == LevelName);
            if (guid == null) throw new InvalidOperationException($"no prefab '{LevelName}' under {LevelFolder}");
            string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            log.AppendLine("level prefab: " + prefabPath);

            // A rebuild opens the saved scene and replaces only the two roots this script owns (the level and
            // PlayerStart), so whatever else is at the root (a walker, a HUD, lighting) survives.
            bool fresh = !File.Exists(ScenePath);
            Scene scene = fresh
                ? EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == LevelName || root.name == StartName)
                {
                    log.AppendLine("replacing root: " + root.name);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            var level = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            level.name = LevelName;
            level.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (TileThumbnailCapture.TryComputeBounds(level, out Bounds b))
                log.AppendLine($"level bounds: min {b.min} max {b.max} (nominal x {-Columns * SlotM / 2} .. {Columns * SlotM / 2}, z {-Rows * SlotM / 2} .. {Rows * SlotM / 2})");

            float floorY = FloorUnder(level, EntryOnRim, log);
            var start = new GameObject(StartName);
            SceneManager.MoveGameObjectToScene(start, scene);
            start.transform.SetPositionAndRotation(new Vector3(EntryOnRim.x, floorY, EntryOnRim.z),
                Quaternion.Euler(0f, EntryYawDeg, 0f));
            log.AppendLine($"PlayerStart: {start.transform.position} yaw {EntryYawDeg}");

            // A new scene's default camera stands at the start, at eye height, looking in: Play shows the way in
            // until a first-person controller is dropped on PlayerStart. A rebuild leaves the cameras alone.
            Camera cam = fresh ? UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault() : null;
            if (cam != null)
                cam.transform.SetPositionAndRotation(start.transform.position + Vector3.up * 1.7f,
                    Quaternion.Euler(5f, EntryYawDeg, 0f));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("SaveScene failed: " + ScenePath);
            AssetDatabase.SaveAssets();
            log.AppendLine("scene saved: " + ScenePath);
            return level;
        }

        /// <summary>The floor's top under a point: a physics ray when the level has colliders, else the highest
        /// renderer top below 2 m whose footprint holds the point, else 0 (band 0 is the ground).</summary>
        private static float FloorUnder(GameObject level, Vector3 p, StringBuilder log)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out RaycastHit hit, 10f))
            {
                log.AppendLine($"floor under the start (ray): {hit.point.y:0.###} on {hit.collider.name}");
                return hit.point.y;
            }
            float best = float.NegativeInfinity; string on = null;
            foreach (Renderer r in level.GetComponentsInChildren<Renderer>())
            {
                Bounds rb = r.bounds;
                if (p.x < rb.min.x || p.x > rb.max.x || p.z < rb.min.z || p.z > rb.max.z) continue;
                if (rb.max.y > 2f || rb.max.y <= best) continue;
                best = rb.max.y; on = r.name;
            }
            if (on != null) { log.AppendLine($"floor under the start (bounds): {best:0.###} on {on}"); return best; }
            log.AppendLine("floor under the start: nothing found, using 0");
            return 0f;
        }

        private static void Capture(GameObject level, StringBuilder log)
        {
            int hidden = 0;
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true))
            {
                if (t == level.transform || !t.gameObject.activeSelf) continue;
                if (!HiddenPrefixes.Any(prefix => t.name.StartsWith(prefix, StringComparison.Ordinal))) continue;
                t.gameObject.SetActive(false);
                hidden++;
            }
            log.AppendLine($"capture: hid {hidden} ceiling:/roof: child(ren)");

            // A red pad on the start, for the overview only (the scene was saved without it).
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "~start-marker";
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.SetParent(level.transform, false);
            GameObject start = GameObject.Find(StartName);
            pad.transform.position = (start != null ? start.transform.position : EntryOnRim) + Vector3.up * 3f;
            pad.transform.localScale = new Vector3(6f, 0.5f, 6f);
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                mat.SetColor("_BaseColor", Color.red);
                pad.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var opts = new ThumbnailOptions { ImageWidth = 1536, ImageHeight = 2048, OrthoMargin = 0.02f };
            var frame = new Bounds(Vector3.zero, new Vector3(Columns * SlotM, 40f, Rows * SlotM));
            byte[] png = TileThumbnailCapture.CapturePngTopDown(level, opts, SlotM,
                new Vector2(-Columns * SlotM / 2f, -Rows * SlotM / 2f), frame);
            UnityEngine.Object.DestroyImmediate(pad);
            if (png == null) throw new InvalidOperationException("capture returned nothing (no graphics device?)");
            Directory.CreateDirectory(Path.GetDirectoryName(CapturePath));
            File.WriteAllBytes(CapturePath, png);
            log.AppendLine($"capture: {CapturePath} ({png.Length} bytes, 1536 x 2048, frame 300 x 400 m, 100 m grid)");
        }
    }
}
