using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace IronCitadel.Editor
{
    public class Cell
    {
        public TileDef tile;
        public Zone zone;
        public int i, j;        // module indices inside the tile
        public int gi, gj;      // global module indices (gi east, gj north)
        public string zoneId;
        public Vector3 Centre => new Vector3(tile.X0 + 5f * i + 2.5f, 0f, tile.Z0 + 5f * j + 2.5f);
        public Vector3 SW => new Vector3(tile.X0 + 5f * i, 0f, tile.Z0 + 5f * j);
    }

    /// <summary>Shared state for one build.</summary>
    public class Ctx
    {
        public Transform root, structure, ceilings, rock, props, lights, markers;
        public System.Random rng;
        public Material matGuard, matGold, matRock, matLabel, matOverview, matDark;
        public Font font;
        public LevelHud hud;
        public int ceilingLayer = 6, overviewLayer = 7;
        public Dictionary<string, TileDef> tiles = new Dictionary<string, TileDef>();
        public Dictionary<string, int> defenderCounts = new Dictionary<string, int>();
        public List<string> notes = new List<string>();
        public int lightCount, propCount;
        public float Rand(float a, float b) => (float)(a + rng.NextDouble() * (b - a));
        public int Range(int a, int b) => rng.Next(a, b);
    }

    /// <summary>Builds Assets/Scenes/IronCitadel.unity from level.json and the hand-authored tile layouts.</summary>
    public static class IronCitadelBuilder
    {
        public const string ScenePath = "Assets/Scenes/IronCitadel.unity";
        public const string DataPath = "Assets/IronCitadel/level.json";
        public const string PlayerPrefab = "Assets/Interact/IronCitadelPlayer.prefab";
        public const string MatDir = "Assets/IronCitadel/Materials";
        const int Grid = 36;

        static Cell[,] _cells;
        static Dictionary<(int, int, Dir), Opening> _openings;

        [MenuItem("Tools/Iron Citadel/Build level")]
        public static void Build()
        {
            var log = new StringBuilder();
            try
            {
                BuildInner(log);
            }
            catch (Exception ex)
            {
                log.AppendLine("ERROR " + ex);
                Debug.LogException(ex);
            }
            Directory.CreateDirectory("Tools");
            File.WriteAllText("Tools/build_result.txt", log.ToString());
            Debug.Log("[IronCitadel] " + log);
        }

        static void BuildInner(StringBuilder log)
        {
            EnsureLayers();
            var ctx = new Ctx { rng = new System.Random(7) };

            var json = MiniJson.Obj(MiniJson.Parse(File.ReadAllText(DataPath)));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ctx.root = new GameObject("IronCitadel").transform;
            ctx.structure = Kit.Group("Structure", ctx.root);
            ctx.ceilings = Kit.Group("Ceilings", ctx.root);
            ctx.rock = Kit.Group("Rock", ctx.root);
            ctx.props = Kit.Group("Props", ctx.root);
            ctx.lights = Kit.Group("Lights", ctx.root);
            ctx.markers = Kit.Group("Markers", ctx.root);
            MakeMaterials(ctx);

            // tiles from the layout, checked against the data file
            foreach (var t in Layout.Tiles()) ctx.tiles[t.roomId] = t;
            var roomEntries = new List<LevelHud.RoomEntry>();
            foreach (var ro in MiniJson.Arr(json["rooms"]))
            {
                var r = MiniJson.Obj(ro);
                string id = MiniJson.Str(r["id"]);
                var cell = MiniJson.Arr(r["cell"]);
                int rr = MiniJson.Int(cell[0]), cc = MiniJson.Int(cell[1]);
                if (!ctx.tiles.TryGetValue(id, out var td)) { log.AppendLine("no layout for room " + id); continue; }
                if (td.r != rr || td.c != cc) log.AppendLine("WARNING layout cell for " + id + " differs from data");
                td.r = rr; td.c = cc;
                roomEntries.Add(new LevelHud.RoomEntry { id = id, displayName = DisplayName(id), row = rr, col = cc });
            }
            foreach (var d in MiniJson.Arr(json["defenders"]))
            {
                var o = MiniJson.Obj(d);
                ctx.defenderCounts[MiniJson.Str(o["room"])] = MiniJson.Int(o["count"]);
            }

            BuildGrid(ctx);
            RegisterOpenings(ctx, json, log);
            BuildStructure(ctx, log);

            // dressing
            foreach (var t in ctx.tiles.Values) Dressing.Dress(ctx, t);

            // player at the outer door, facing north
            var start = MiniJson.Obj(json["start"]);
            var sc = MiniJson.Arr(start["cell"]);
            int sr = MiniJson.Int(sc[0]), scol = MiniJson.Int(sc[1]);
            var startPos = new Vector3(45f * scol + 22.5f, 0.05f, -45f * (sr + 1) + 2.2f);
            var player = Kit.Place(PlayerPrefab, null, startPos, 0f, null, "IronCitadelPlayer");
            Transform capsule = player != null ? FindChild(player.transform, "PlayerCapsule") : null;
            Camera mainCam = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (mainCam != null)
            {
                var data = mainCam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                mainCam.farClipPlane = 400f;
            }

            // overview camera and HUD
            var ovGo = new GameObject("OverviewCamera");
            ovGo.transform.SetPositionAndRotation(new Vector3(90f, 150f, -90f), Quaternion.Euler(90f, 0f, 0f));
            var ov = ovGo.AddComponent<Camera>();
            ov.orthographic = true;
            ov.orthographicSize = 93f;
            ov.nearClipPlane = 1f;
            ov.farClipPlane = 400f;
            ov.clearFlags = CameraClearFlags.SolidColor;
            ov.backgroundColor = Color.black;
            ov.cullingMask = ~(1 << ctx.ceilingLayer);
            ov.depth = 10f;
            ov.enabled = false;
            var ovData = ov.GetUniversalAdditionalCameraData();
            ovData.renderPostProcessing = true;
            ovData.renderShadows = false;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "OverviewPlayerMarker";
            marker.transform.localScale = Vector3.one * 5f;
            marker.transform.position = startPos + Vector3.up * 40f;
            marker.GetComponent<Renderer>().sharedMaterial = ctx.matOverview;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.SetActive(false);

            // a flat top light used only while the overview is up, so the map reads
            var ovLightGo = new GameObject("OverviewLight");
            ovLightGo.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
            var ovLight = ovLightGo.AddComponent<Light>();
            ovLight.type = LightType.Directional;
            ovLight.intensity = 1.7f;
            ovLight.color = new Color(0.85f, 0.9f, 1f);
            ovLight.shadows = LightShadows.None;
            ovLightGo.SetActive(false);

            var hudGo = new GameObject("HUD");
            var hud = hudGo.AddComponent<LevelHud>();
            hud.rooms = roomEntries.ToArray();
            hud.player = capsule != null ? capsule : (player != null ? player.transform : null);
            hud.overviewCamera = ov;
            hud.overviewMarker = marker;
            hud.overviewLight = ovLightGo;
            ctx.hud = hud;
            foreach (var goal in UnityEngine.Object.FindObjectsByType<ThroneGoal>(FindObjectsSortMode.None)) goal.hud = hud;

            SetupLighting(ctx);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            log.AppendLine("ok " + ScenePath);
            log.AppendLine("props " + ctx.propCount + " lights " + ctx.lightCount);
            foreach (var m in Kit.Missing) log.AppendLine("MISSING PREFAB " + m);
            foreach (var n in ctx.notes) log.AppendLine("note " + n);
        }

        public static string DisplayName(string id)
        {
            switch (id)
            {
                case "entry_hall": return "Entry hall";
                case "guard_room": return "Guard room";
                case "prison": return "Prison";
                case "vault": return "Treasure vault";
                case "kitchen": return "Kitchen";
                case "great_hall": return "Great hall";
                case "armory": return "Armory";
                case "throne_room": return "Throne room";
                case "library": return "Library";
                case "study": return "Alchemist’s study";
                case "forge": return "Forge";
                default: return id;
            }
        }

        static Transform FindChild(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var f = FindChild(c, name); if (f != null) return f; }
            return null;
        }

        // ------------------------------------------------------------------ grid

        static void BuildGrid(Ctx ctx)
        {
            _cells = new Cell[Grid, Grid];
            foreach (var t in ctx.tiles.Values)
            {
                int cellIndex = 0;
                for (int i = 0; i < 9; i++)
                    for (int j = 0; j < 9; j++)
                    {
                        char ch = t.At(i, j);
                        if (ch == '#') continue;
                        if (!t.zones.TryGetValue(ch, out var z)) throw new Exception("tile " + t.roomId + " has no zone '" + ch + "'");
                        var cell = new Cell { tile = t, zone = z, i = i, j = j, gi = 9 * t.c + i, gj = 9 * (3 - t.r) + j };
                        cell.zoneId = t.roomId + ":" + ch + (ch == 'c' ? (cellIndex++).ToString() : "");
                        _cells[cell.gi, cell.gj] = cell;
                    }
            }
        }

        public static Cell CellAt(int gi, int gj)
        {
            if (gi < 0 || gj < 0 || gi >= Grid || gj >= Grid) return null;
            return _cells[gi, gj];
        }

        public static Cell CellOf(TileDef t, int i, int j) => CellAt(9 * t.c + i, 9 * (3 - t.r) + j);

        static Cell Neighbour(Cell c, Dir d)
        {
            var v = Layout.Vec(d);
            return CellAt(c.gi + (int)v.x, c.gj + (int)v.z);
        }

        static void Register(Cell c, Dir d, Opening op, bool mirror)
        {
            _openings[(c.gi, c.gj, d)] = op;
            if (!mirror) return;
            var n = Neighbour(c, d);
            if (n == null) return;
            var m = new Opening(n.i, n.j, Layout.Opposite(d), op.kind, op.variant) { owner = false };
            _openings[(n.gi, n.gj, Layout.Opposite(d))] = m;
        }

        static void RegisterOpenings(Ctx ctx, Dictionary<string, object> json, StringBuilder log)
        {
            _openings = new Dictionary<(int, int, Dir), Opening>();
            foreach (var t in ctx.tiles.Values)
                foreach (var op in t.openings)
                {
                    var c = CellOf(t, op.i, op.j);
                    if (c == null) { log.AppendLine("opening on solid cell in " + t.roomId); continue; }
                    Register(c, op.d, op, op.kind != OpenKind.Fireplace && op.kind != OpenKind.StairTop);
                }

            // doors between tiles, from the data file: each sits at the middle of the shared edge
            foreach (var dobj in MiniJson.Arr(json["doors"]))
            {
                var d = MiniJson.Obj(dobj);
                var between = MiniJson.Arr(d["between"]);
                var a = MiniJson.Arr(between[0]);
                int ar = MiniJson.Int(a[0]), ac = MiniJson.Int(a[1]);
                string kind = MiniJson.Str(d["kind"]);
                TileDef ta = TileAt(ctx, ar, ac);
                if (ta == null) { log.AppendLine("door " + d["id"] + " from unknown tile"); continue; }
                if (between[1] is string s)
                {
                    // outside: south edge of the entry hall
                    var c = CellOf(ta, 4, 0);
                    Register(c, Dir.S, new Opening(4, 0, Dir.S, OpenKind.OuterDoor), false);
                    continue;
                }
                var b = MiniJson.Arr(between[1]);
                int br = MiniJson.Int(b[0]), bc = MiniJson.Int(b[1]);
                Dir dir;
                if (br == ar - 1) dir = Dir.N; else if (br == ar + 1) dir = Dir.S; else if (bc == ac + 1) dir = Dir.E; else dir = Dir.W;
                int i = dir == Dir.N || dir == Dir.S ? 4 : (dir == Dir.E ? 8 : 0);
                int j = dir == Dir.E || dir == Dir.W ? 4 : (dir == Dir.N ? 8 : 0);
                var cell = CellOf(ta, i, j);
                if (cell == null) { log.AppendLine("door " + d["id"] + " on solid cell"); continue; }
                OpenKind k = OpenKind.Doorway;
                if (kind.Contains("portcullis")) k = OpenKind.Gate;
                else if (kind.Contains("secret")) k = OpenKind.Secret;
                else if (kind.Contains("great")) k = OpenKind.GreatDoors;
                Register(cell, dir, new Opening(i, j, dir, k), true);
                var nb = Neighbour(cell, dir);
                if (nb == null) log.AppendLine("door " + d["id"] + " has no neighbour cell");
            }
        }

        static TileDef TileAt(Ctx ctx, int r, int c)
        {
            foreach (var t in ctx.tiles.Values) if (t.r == r && t.c == c) return t;
            return null;
        }

        // ------------------------------------------------------------------ structure

        static void BuildStructure(Ctx ctx, StringBuilder log)
        {
            var tileGroups = new Dictionary<string, Transform>();
            foreach (var t in ctx.tiles.Values) tileGroups[t.roomId] = Kit.Group(t.roomId, ctx.structure);

            for (int gi = 0; gi < Grid; gi++)
                for (int gj = 0; gj < Grid; gj++)
                {
                    var c = _cells[gi, gj];
                    if (c == null) continue;
                    var parent = tileGroups[c.tile.roomId];
                    // floor
                    string floor = c.zone.floor;
                    if (c.zone.floorAlt != null && ctx.rng.NextDouble() < 0.22) floor = c.zone.floorAlt;
                    Kit.Place(floor, parent, c.SW + new Vector3(5f, 0f, 0f), 0f);
                    // ceiling
                    string ceil = c.zone.ceiling ?? Kit.Ceilings[ctx.rng.Next(Kit.Ceilings.Length)];
                    var cg = Kit.Place(ceil, ctx.ceilings, c.SW + new Vector3(5f, c.zone.h, 0f), 0f);
                    if (cg != null) Kit.SetLayerRecursive(cg, ctx.ceilingLayer);
                    // walls
                    for (int k = 0; k < 4; k++)
                    {
                        var d = (Dir)k;
                        var n = Neighbour(c, d);
                        bool need = n == null || n.zoneId != c.zoneId;
                        if (!need) continue;
                        _openings.TryGetValue((gi, gj, d), out var op);
                        WallStack(ctx, parent, c, d, op);
                    }
                }

            // rock: dark slabs under every solid module inside the 4 x 4 grid, for the overview
            for (int r = 0; r < 4; r++)
                for (int cc = 0; cc < 4; cc++)
                {
                    var t = TileAt(ctx, r, cc);
                    if (t == null)
                    {
                        RockSlab(ctx, new Vector3(45f * cc + 22.5f, -0.6f, -45f * (r + 1) + 22.5f), 45f);
                        continue;
                    }
                    for (int i = 0; i < 9; i++)
                        for (int j = 0; j < 9; j++)
                            if (t.At(i, j) == '#') RockSlab(ctx, t.World(5f * i + 2.5f, -0.6f, 5f * j + 2.5f), 5f);
                }
        }

        static void RockSlab(Ctx ctx, Vector3 centre, float size)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = "Rock";
            g.transform.SetParent(ctx.rock, false);
            g.transform.position = centre;
            g.transform.localScale = new Vector3(size, 0.3f, size);
            g.GetComponent<Renderer>().sharedMaterial = ctx.matRock;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        /// <summary>Wall facing into cell c on side d: stacked 5 m modules up to the zone height, with the opening's piece at the bottom.</summary>
        static void WallStack(Ctx ctx, Transform parent, Cell c, Dir d, Opening op)
        {
            var f = -Layout.Vec(d);
            var right = new Vector3(f.z, 0f, -f.x);
            var mid = c.Centre + Layout.Vec(d) * 2.5f;
            var rot = Quaternion.LookRotation(f, Vector3.up);
            float h = c.zone.h;
            int modules = Mathf.CeilToInt(h / 5f - 0.001f);
            float upperStart = 5f;
            string bottom = c.zone.wall;
            bool nothingAbove = false;
            if (op != null)
            {
                switch (op.kind)
                {
                    case OpenKind.Doorway: bottom = Kit.WallDoorDouble; break;
                    case OpenKind.OuterDoor: bottom = Kit.WallDoorDouble; break;
                    case OpenKind.Secret: bottom = Kit.WallDoorSingle; break;
                    case OpenKind.Arch: bottom = Kit.DwarfArchway; break;
                    case OpenKind.Arcade: bottom = Kit.DwarfArchway; nothingAbove = true; break;
                    case OpenKind.Gate: bottom = null; upperStart = 5.59f; break;
                    case OpenKind.GreatDoors: bottom = null; upperStart = 5.46f; break;
                    case OpenKind.Bars: bottom = null; nothingAbove = true; break;
                    case OpenKind.StairTop: nothingAbove = true; break;
                    case OpenKind.Fireplace: bottom = Kit.WallFireplaceGap; break;
                }
            }
            for (int m = 0; m < modules; m++)
            {
                if (m > 0 && nothingAbove) break;
                string piece = m == 0 ? bottom : c.zone.wall;
                if (piece == null) continue;
                float y = m == 0 ? 0f : (m == 1 ? upperStart : upperStart + 5f * (m - 1));
                var pos = new Vector3(mid.x + right.x * 2.5f, y, mid.z + right.z * 2.5f);
                Kit.PlaceRot(piece, parent, pos, rot);
            }
            if (op != null && op.owner) OpeningExtras(ctx, parent, c, d, op, mid, f, right, rot);
        }

        static void OpeningExtras(Ctx ctx, Transform parent, Cell c, Dir d, Opening op, Vector3 mid, Vector3 f, Vector3 right, Quaternion rot)
        {
            switch (op.kind)
            {
                case OpenKind.Gate:
                {
                    Kit.PlaceRot(Kit.LargeFrame1, parent, mid, rot, null, "GateFrame");
                    Kit.PlaceRot(Kit.Portcullis, parent, mid + Vector3.up * 2.83f, rot, null, "Portcullis");
                    var block = new GameObject("GateBlocker");
                    block.transform.SetParent(parent, false);
                    block.transform.SetPositionAndRotation(mid + Vector3.up * 2.5f, rot);
                    var bc = block.AddComponent<BoxCollider>();
                    bc.size = new Vector3(5.2f, 5.2f, 0.8f);
                    break;
                }
                case OpenKind.GreatDoors:
                {
                    Kit.PlaceRot(Kit.LargeFrame2, parent, mid, rot, null, "GreatDoorFrame");
                    // leaves hinge on the jambs and stand open into the owner's cell
                    var l = Kit.PlaceRot(Kit.LargeDoorL, parent, mid + right * 1.62f, rot * Quaternion.Euler(0f, 105f, 0f), null, "GreatDoorLeaf");
                    var r = Kit.PlaceRot(Kit.LargeDoorR, parent, mid - right * 1.62f, rot * Quaternion.Euler(0f, -105f, 0f), null, "GreatDoorLeaf");
                    break;
                }
                case OpenKind.OuterDoor:
                {
                    Kit.PlaceRot(Kit.DoubleDoorL, parent, mid + f * 0.05f + right * 1.33f, rot, null, "OuterDoorLeaf");
                    Kit.PlaceRot(Kit.DoubleDoorR, parent, mid + f * 0.05f - right * 1.33f, rot, null, "OuterDoorLeaf");
                    break;
                }
                case OpenKind.Secret:
                {
                    // hinge at the bookcase's left edge, the bookcase hanging off it; it swings into the prison
                    var hinge = new GameObject("SecretDoorHinge");
                    hinge.transform.SetParent(parent, false);
                    hinge.transform.SetPositionAndRotation(mid + f * 0.32f - right * 1.1f, rot);
                    var sd = hinge.AddComponent<SecretDoor>();
                    sd.openAngle = -110f;
                    var book = Kit.PlaceRot(Kit.BookcaseSmall, hinge.transform, hinge.transform.position + right * 2.33f, rot, null, "SecretBookcase");
                    ctx.notes.Add("secret door at " + mid);
                    break;
                }
                case OpenKind.Bars:
                {
                    Bars(ctx, parent, op.variant, mid, f, right, rot);
                    break;
                }
                case OpenKind.Fireplace:
                {
                    Kit.PlaceRot(Kit.FireplaceAlcove, parent, mid + f * 0.1f, rot, null, "Hearth");
                    var fire = Kit.PlaceRot(Kit.FxFire, parent, mid - f * 0.7f + Vector3.up * 0.25f, Quaternion.identity, Vector3.one * 1.3f, "HearthFire");
                    Dressing.PointLight(ctx, mid + f * 0.9f + Vector3.up * 1.4f, new Color(1f, 0.6f, 0.3f), 16f, 5f, true);
                    break;
                }
            }
        }

        static void Bars(Ctx ctx, Transform parent, int variant, Vector3 mid, Vector3 f, Vector3 right, Quaternion rot)
        {
            int kind = variant % 3; // 0 shut, 1 open, 2 forced
            string panel = kind == 2 ? Kit.FenceWeak : Kit.FencePanel;
            Kit.PlaceRot(panel, parent, mid - right * 1.25f, rot, null, "CellFence");
            Kit.PlaceRot(Kit.FencePost, parent, mid + right * 0.02f, rot, null, "CellPost");
            Kit.PlaceRot(Kit.FencePost, parent, mid + right * 1.42f, rot, null, "CellPost");
            Kit.PlaceRot(Kit.FencePanel, parent, mid + right * 1.96f, rot, new Vector3(0.44f, 1f, 1f), "CellFence");
            var hinge = mid + right * 1.38f;
            if (kind == 0)
                Kit.PlaceRot(Kit.DoorBars, parent, hinge, rot, new Vector3(1f, 1.12f, 1f), "CellDoorShut");
            else if (kind == 1)
                Kit.PlaceRot(Kit.DoorBars, parent, hinge, rot * Quaternion.Euler(0f, 100f + (variant % 2) * 25f, 0f), new Vector3(1f, 1.12f, 1f), "CellDoorOpen");
            else
            {
                // torn off and lying in the aisle
                var pos = mid - f * (1.5f + (variant % 2) * 0.6f) + right * 0.3f + Vector3.up * 0.08f;
                Kit.PlaceRot(Kit.DoorBars, parent, pos, Quaternion.Euler(90f, ctx.Rand(0f, 360f), 0f), new Vector3(1f, 1.12f, 1f), "CellDoorForced");
            }
        }

        // ------------------------------------------------------------------ materials, layers, lighting

        static void MakeMaterials(Ctx ctx)
        {
            Directory.CreateDirectory(MatDir);
            ctx.matGuard = Mat("Marker_Guard", new Color(0.55f, 0.08f, 0.08f), Color.black);
            ctx.matGold = Mat("Marker_Pickup", new Color(1f, 0.8f, 0.2f), new Color(1.2f, 0.9f, 0.2f));
            ctx.matRock = Mat("Rock", new Color(0.16f, 0.15f, 0.14f), Color.black);
            ctx.matDark = Mat("Dark", new Color(0.05f, 0.05f, 0.05f), Color.black);
            ctx.matOverview = Mat("OverviewMarker", new Color(1f, 0.9f, 0.2f), new Color(2f, 1.6f, 0.3f));
            ctx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string labelPath = MatDir + "/Label.mat";
            ctx.matLabel = AssetDatabase.LoadAssetAtPath<Material>(labelPath);
            if (ctx.matLabel == null)
            {
                ctx.matLabel = new Material(Shader.Find("IronCitadel/TextDepth"));
                AssetDatabase.CreateAsset(ctx.matLabel, labelPath);
            }
            if (ctx.font != null && ctx.font.material != null) ctx.matLabel.mainTexture = ctx.font.material.mainTexture;
            EditorUtility.SetDirty(ctx.matLabel);
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name, Color baseColor, Color emission)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            SetLayer(layers, 6, "Ceiling");
            SetLayer(layers, 7, "Overview");
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var p = layers.GetArrayElementAtIndex(index);
            if (p.stringValue != name) p.stringValue = name;
        }

        static void SetupLighting(Ctx ctx)
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.18f, 0.24f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 16;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.fogColor = new Color(0.012f, 0.012f, 0.02f);

            string lsPath = "Assets/IronCitadel/IronCitadelLighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;

            string profilePath = "Assets/IronCitadel/IronCitadelPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var bloom = profile.Add<Bloom>(true);
                bloom.name = "Bloom";
                bloom.intensity.Override(0.45f);
                bloom.threshold.Override(0.95f);
                bloom.scatter.Override(0.6f);
                AssetDatabase.AddObjectToAsset(bloom, profile);
                var tone = profile.Add<Tonemapping>(true);
                tone.name = "Tonemapping";
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.AddObjectToAsset(tone, profile);
                var vig = profile.Add<Vignette>(true);
                vig.name = "Vignette";
                vig.intensity.Override(0.22f);
                AssetDatabase.AddObjectToAsset(vig, profile);
                var ca = profile.Add<ColorAdjustments>(true);
                ca.name = "ColorAdjustments";
                ca.postExposure.Override(0.55f);
                ca.contrast.Override(8f);
                AssetDatabase.AddObjectToAsset(ca, profile);
                AssetDatabase.SaveAssets();
            }
            var vol = new GameObject("Global Volume");
            var v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
        }
    }
}
