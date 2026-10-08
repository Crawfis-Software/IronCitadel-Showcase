using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronCitadel.Rooms.Editor
{
    /// <summary>Batchmode dump of the candidate K9 room prefabs: frame (pivot, footprint, floor y),
    /// ceiling marking and height, lights, and where each edge is open at walking height.
    /// <c>-executeMethod IronCitadel.Rooms.Editor.IronCitadelInspect.Run</c>; writes
    /// C:/Repos/IronCitadel/logs/rooms-inspect.txt and one top-down PNG per prefab under
    /// C:/Repos/IronCitadel/captures/inspect/.</summary>
    public static class IronCitadelInspect
    {
        const string RoomsRoot = "Assets/CrawfisSoftware/AllSyntyDungeons/Rooms";
        public const string LogPath = "C:/Repos/IronCitadel/logs/rooms-inspect.txt";
        const string CapDir = "C:/Repos/IronCitadel/captures/inspect";

        static readonly string[] Candidates =
        {
            "HallGalleryBackFeastK9NESW",
            "GuardRoomLobbyMannedK9NESW", "GuardRoomWarrenMannedK9NESW", "BarracksDormOccupiedK9NESW",
            "CommonRoomGalleryBusyK9NESW",
            "GuardRoomPostNightK9NE",
            "PrisonBlockRiotK9NES",
            "TreasuryBoxHoardK9N", "TreasuryRingHoardK9N", "TreasuryLobbySealedK9N",
            "KitchenNotchCookingK9NE", "KitchenSmallCookingK9NE",
            "ArmoryRacksStockedK9NS", "ArmoryVaultStockedK9NS",
            "ThroneRoomAisledCourtK9N", "ThroneRoomNaveCourtK9N",
            "LibraryBaysOpenK9NE", "LibraryBoxOpenK9NE",
            "AlchemyLabRingBrewingK9NS", "AlchemyLabWarrenBrewingK9NS",
            "ForgeCellarWorkingK9NE", "ForgeHypostyleWorkingK9NE",
        };

        [MenuItem("Iron Citadel/Rooms/Inspect candidate prefabs")]
        public static void Run()
        {
            int code = 0;
            try { Inspect(); }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        public static string FindPrefab(string stem)
        {
            foreach (var guid in AssetDatabase.FindAssets(stem + "_ t:Prefab", new[] { RoomsRoot }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p).StartsWith(stem + "_")) return p;
            }
            return null;
        }

        static void Inspect()
        {
            var sb = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var stem in Candidates)
            {
                var path = FindPrefab(stem);
                if (path == null) { sb.AppendLine($"## {stem}: NOT FOUND"); continue; }
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                try { Dump(sb, stem, path, go); }
                catch (Exception e) { sb.AppendLine($"  ERROR {e}"); }
                try
                {
                    var hidden = IronCitadelCapture.HideCeilings(go);
                    IronCitadelCapture.TopDown($"{CapDir}/{stem}.png", Vector2.zero, 24f, 960, true,
                        200f, 5f, new Vector2(-22.5f, -22.5f));
                    IronCitadelCapture.Restore(hidden);
                }
                catch (Exception e) { sb.AppendLine($"  CAPTURE ERROR {e.Message}"); }
                UnityEngine.Object.DestroyImmediate(go);
                File.WriteAllText(LogPath, sb.ToString());
            }
            sb.AppendLine("# PROPS (instantiated at the origin, rotation 0)");
            foreach (var p in Props)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (asset == null) { sb.AppendLine($"  {p}: NOT FOUND"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                var bb = Union(rs);
                var cols = go.GetComponentsInChildren<Collider>(true);
                sb.AppendLine($"  {Path.GetFileNameWithoutExtension(p)}: bounds {V(bb.min)}..{V(bb.max)} size {V(bb.size)}; colliders {string.Join(",", cols.Select(c => c.GetType().Name + (c is MeshCollider mc ? (mc.convex ? "(cvx)" : "") : "")))}; animator {go.GetComponentInChildren<Animator>() != null}");
                UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(LogPath, sb.ToString());
            Debug.Log("[IronCitadelInspect] wrote " + LogPath);
        }

        static readonly string[] Props =
        {
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Portcullis_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Goblin_Spikes_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Goblin_Spikes_02.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Goblin_Spikes_03.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Goblin_Spikes_04.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Fence_Metal_Spikes_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Fence_Metal_Spikes_02.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Trap_Spikes_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Bookcase_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Bookcase_02.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Bookcase_03.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_02.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_Grand_01.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_Grand_02.prefab",
            "Assets/Synty/PolygonDungeonMap/Prefabs/SM_Prop_Bookcase_Small_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Props/SM_Prop_Dwarf_Shelf_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_DoorFrame_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Wall_DoorFrame_Round_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Walls/SM_Env_Door_Frame_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Hero_Knight_Male_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Skeleton_Soldier_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Characters/SM_Chr_Goblin_WarChief_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Dwarf_Soldier_Male_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Dwarf_King_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Dwarf_Worker_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Nomad_Male_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Hero_Male_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Props/SM_Prop_Chest_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Items/SM_Item_Coins_01.prefab",
            "Assets/_THIRD_PARTY/Synty/PolygonDungeon/Prefabs/Environments/Misc/SM_Env_Stone_Throne_01.prefab",
        };

        static string V(Vector3 v) => $"({v.x:0.###}, {v.y:0.###}, {v.z:0.###})";

        static void Dump(StringBuilder sb, string stem, string path, GameObject go)
        {
            var root = go.transform;
            sb.AppendLine($"## {stem}");
            sb.AppendLine($"  path {path}");
            sb.AppendLine($"  root localPos {V(root.localPosition)} euler {V(root.localEulerAngles)} scale {V(root.localScale)} layer {go.layer}");
            sb.AppendLine("  top-level: " + string.Join(", ", root.Cast<Transform>().Select(c => $"{c.name}[{c.childCount}]")));

            var all = go.GetComponentsInChildren<Renderer>(true);
            var b = Union(all);
            sb.AppendLine($"  renderer bounds min {V(b.min)} max {V(b.max)}");
            var nonCeil = all.Where(r => !UnderCeiling(r.transform, root)).ToArray();
            var bn = Union(nonCeil);
            sb.AppendLine($"  non-ceiling bounds min {V(bn.min)} max {V(bn.max)}");

            // floor: the renderers under groups named Floor
            var floors = all.Where(r => HasAncestorNamed(r.transform, root, "Floor")).ToArray();
            if (floors.Length > 0)
            {
                var bf = Union(floors);
                sb.AppendLine($"  floor renderers {floors.Length}: y {bf.min.y:0.###}..{bf.max.y:0.###}; xz {V(bf.min)}..{V(bf.max)}");
            }
            // ceilings
            var ceilT = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ceiling:")).ToArray();
            var roofT = go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("roof:")).ToArray();
            var ceilGroups = go.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ceiling").ToArray();
            var ceilR = all.Where(r => UnderCeiling(r.transform, root)).ToArray();
            sb.AppendLine($"  ceiling: groups 'Ceiling' x{ceilGroups.Length} (parents: {string.Join(",", ceilGroups.Select(g => g.parent ? g.parent.name : "-"))}), 'ceiling:' pieces {ceilT.Length}, 'roof:' {roofT.Length}");
            if (ceilR.Length > 0)
            {
                var bc = Union(ceilR);
                var ys = ceilT.Select(t => t.position.y).Distinct().OrderBy(y => y).Take(8);
                sb.AppendLine($"  ceiling renderers {ceilR.Length}: bounds y {bc.min.y:0.###}..{bc.max.y:0.###}; piece pivot y {string.Join(" ", ys.Select(y => y.ToString("0.##")))}; layers {string.Join(",", ceilR.Select(r => LayerMask.LayerToName(r.gameObject.layer)).Distinct())}; colliders {ceilT.Sum(t => t.GetComponentsInChildren<Collider>(true).Length)}");
            }
            var otherCeil = go.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) >= 0 && !t.name.StartsWith("ceiling:") && t.name != "Ceiling")
                .Select(t => t.name).Distinct().Take(10).ToArray();
            if (otherCeil.Length > 0) sb.AppendLine("  other *ceiling* names: " + string.Join(", ", otherCeil));
            // lights
            var lights = go.GetComponentsInChildren<Light>(true);
            sb.AppendLine($"  lights {lights.Length}: " + string.Join(", ", lights.GroupBy(l => l.type).Select(g => $"{g.Key} x{g.Count()} (I {g.Min(l => l.intensity):0.##}..{g.Max(l => l.intensity):0.##}, R {g.Min(l => l.range):0.#}..{g.Max(l => l.range):0.#})")));
            sb.AppendLine($"  colliders {go.GetComponentsInChildren<Collider>(true).Length}, layers used: {string.Join(",", go.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer).Distinct().Select(LayerMask.LayerToName))}");
            // room groups
            foreach (Transform c in root)
            {
                var rr = c.GetComponentsInChildren<Renderer>(true).Where(r => !UnderCeiling(r.transform, root)).ToArray();
                if (rr.Length == 0) continue;
                var bb = Union(rr);
                sb.AppendLine($"    group {c.name}: x {bb.min.x:0.#}..{bb.max.x:0.#} z {bb.min.z:0.#}..{bb.max.z:0.#} y {bb.min.y:0.#}..{bb.max.y:0.#}");
            }
            // doorways
            Physics.SyncTransforms();
            float half = 22.5f;
            foreach (var edge in new[] { 'N', 'E', 'S', 'W' })
            {
                var runs = OpenRuns(edge, half);
                sb.AppendLine($"  edge {edge}: open " + (runs.Count == 0 ? "none" : string.Join("; ", runs.Select(r => $"t {r.x:0.##}..{r.y:0.##} (w {r.y - r.x:0.##}, mid {(r.x + r.y) / 2:0.##}) depth {Depth(edge, half, (r.x + r.y) / 2):0.#}"))));
                sb.AppendLine($"          wall inner face inset at t=-11: {Inset(edge, half, -11f):0.##}  t=+11: {Inset(edge, half, 11f):0.##}");
            }
            sb.AppendLine();
        }

        /// <summary>Edge frame: t runs along the edge (x for N/S, z for E/W), n is the outward normal.</summary>
        public static void EdgeFrame(char edge, float half, float t, out Vector3 onEdge, out Vector3 n)
        {
            switch (edge)
            {
                case 'N': onEdge = new Vector3(t, 0, half); n = Vector3.forward; break;
                case 'S': onEdge = new Vector3(t, 0, -half); n = Vector3.back; break;
                case 'E': onEdge = new Vector3(half, 0, t); n = Vector3.right; break;
                default: onEdge = new Vector3(-half, 0, t); n = Vector3.left; break;
            }
        }

        static readonly float[] Heights = { 0.5f, 1.2f, 1.8f };

        public static bool Blocked(Vector3 onEdge, Vector3 n, float outside = 1.5f, float inside = 2.0f)
        {
            foreach (var h in Heights)
            {
                var a = onEdge + n * outside + Vector3.up * h;
                var bpt = onEdge - n * inside + Vector3.up * h;
                float d = Vector3.Distance(a, bpt);
                if (Physics.Raycast(a, (bpt - a).normalized, d, ~0, QueryTriggerInteraction.Ignore)) return true;
                if (Physics.Raycast(bpt, (a - bpt).normalized, d, ~0, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        static List<Vector2> OpenRuns(char edge, float half)
        {
            var runs = new List<Vector2>();
            float step = 0.25f;
            float? start = null;
            for (float t = -half; t <= half + 1e-3f; t += step)
            {
                EdgeFrame(edge, half, t, out var p, out var n);
                bool open = !Blocked(p, n);
                if (open && start == null) start = t;
                if (!open && start != null) { runs.Add(new Vector2(start.Value, t - step)); start = null; }
            }
            if (start != null) runs.Add(new Vector2(start.Value, half));
            return runs;
        }

        static float Depth(char edge, float half, float t)
        {
            EdgeFrame(edge, half, t, out var p, out var n);
            var a = p + Vector3.up * 1.2f - n * 0.3f;
            return Physics.Raycast(a, -n, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance + 0.3f : 99f;
        }

        static float Inset(char edge, float half, float t)
        {
            EdgeFrame(edge, half, t, out var p, out var n);
            var a = p + Vector3.up * 1.2f - n * 4f;
            return Physics.Raycast(a, n, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore) ? 4f - hit.distance : 99f;
        }

        static bool UnderCeiling(Transform t, Transform root)
        {
            for (var x = t; x != null && x != root; x = x.parent)
                if (IronCitadelCapture.IsCeiling(x)) return true;
            return false;
        }

        static bool HasAncestorNamed(Transform t, Transform root, string name)
        {
            for (var x = t; x != null && x != root; x = x.parent) if (x.name == name) return true;
            return false;
        }

        static Bounds Union(IList<Renderer> rs)
        {
            if (rs.Count == 0) return new Bounds();
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Count; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
