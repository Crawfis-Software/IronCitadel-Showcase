using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        // Key positions shared by the dressing, the markers and the systems.
        public static readonly Vector3 StartPos = new Vector3(14f, 0.02f, 1.2f);
        public const float DeckTop = 4.2f;
        const float DeckZ0 = 20f, DeckZ1 = 23.6f;
        const float StageTop = 1.2f;
        static readonly Vector3 ChairPos = new Vector3(16f, StageTop, 74.6f);

        static void DefineLayout()
        {
            var S = s_Surf;
            // id, name, kind, state, x0, z0, x1, z1, height, wall low, wall high, floor, ceiling, fine trims
            R("foyer", "Foyer", "foyer", "decayed", 9f, 0f, 19f, 8f, 3.4f, S["wp_damask_torn"], null, S["tile_stone"], S["plaster_dirty"], true);
            R("gun", "Gun Room", "gun room", "stocked", 7f, 9.2f, 21f, 19.6f, 3.4f, S["wp_panel"], null, S["floorboards"], S["plaster"], true, S["wood_dark"]);
            R("watch", "Watch Room", "watch room", "decayed", 4.2f, 0.5f, 8.6f, 6.5f, 2.5f, S["boards_dark"], null, S["planks_dark"], S["planks_dark"], false);
            R("cells", "Cellar Cells", "cellar cells", "decayed", -13f, -2f, 3.8f, 9f, 2.6f, S["brick_grey"], null, S["concrete_dark"], S["concrete_dark"], false);
            R("butcher", "Butcher's Room", "butcher's room", "decayed", -13f, 9.4f, -5f, 17.4f, 2.5f, S["tile_green"], S["plaster_dirty"], S["concrete"], S["plaster_dirty"], false).dado = 1.5f;
            R("boiler", "Boiler Room", "boiler room", "decayed", -4.6f, 9.4f, 6.6f, 17.4f, 3.8f, S["brick_brown"], null, S["concrete_dark"], S["concrete_dark"], false);
            R("kitchen", "Kitchen", "kitchen", "looted", -13f, 17.8f, -3.4f, 27f, 2.7f, S["tile_blue"], S["plaster_dirty"], S["tile_stone"], S["plaster_dirty"], false).dado = 1.4f;
            R("store", "Storeroom", "storeroom", "looted", -13f, 27.4f, -3.4f, 34f, 3.2f, S["planks"], null, S["planks_dark"], S["planks"], false);
            R("stair", "Back Stair", "extra", "decayed", -3f, 19.4f, 1.6f, 32.4f, 7.0f, S["boards"], S["plaster_dirty"], S["floorboards"], S["plaster_dirty"], false).dado = 1.2f;
            R("hall", "Dining Hall", "dining hall", "decayed", 2f, 20f, 30f, 36f, 9.0f, S["wp_damask"], S["wp_damask_plain"], S["parquet"], S["plaster"], true);
            R("library", "Library", "library", "looted", 4f, 36.4f, 28f, 46f, 5.0f, S["wp_rose"], null, S["floorboards"], S["planks_dark"], true, S["wood_dark"]);
            R("strong", "Strong Room", "strong room", "untouched", -0.6f, 39f, 3.6f, 43f, 2.3f, S["brick_dark"], null, S["concrete"], S["concrete_dark"], false);
            R("lab", "Laboratory", "laboratory", "decayed", 8f, 46.4f, 24f, 54f, 3.6f, S["tile_green"], S["plaster_dirty"], S["tile_stone"], S["plaster_dirty"], false).dado = 1.5f;
            R("ballroom", "Ballroom", "ballroom", "decayed", 2f, 54.4f, 30f, 76f, 12f, S["wp_panel"], S["wp_beige"], S["parquet"], S["plaster"], true);
            R("chapel", "Chapel", "chapel", "decayed", 30.4f, 24f, 40f, 36f, 8f, S["plaster_dirty"], S["plaster"], S["tile_stone"], S["planks_dark"], false).dado = 1.2f;
            R("crypt", "Crypt", "crypt", "decayed", 30.4f, 12f, 40f, 23.6f, 2.5f, S["brick_dark"], null, S["concrete_dark"], S["brick_dark"], false);

            // a, b, centre along the wall, width (clear + linings), height, state, style, sill
            var d = Dr("foyer", "gun", 14f, 2.08f, 2.8f, "open", DoorStyle.OpenDouble); d.intoB = false; d.leaf = "SM_Bld_Door_02"; d.openAngle = 96f;
            d = Dr("foyer", "watch", 4.5f, 1.28f, 2.2f, "shut", DoorStyle.Single); d.hinge = 1; d.leaf = "SM_Bld_Door_05";
            Dr("watch", "cells", 3.5f, 1.48f, 2.2f, "open", DoorStyle.Doorway);
            d = Dr("cells", "butcher", -11.6f, 1.28f, 2.2f, "open", DoorStyle.OpenSingle); d.hinge = -1; d.leaf = "SM_Bld_Door_03"; d.openAngle = 104f;
            Dr("cells", "boiler", 2.4f, 1.48f, 2.2f, "open", DoorStyle.Doorway);
            d = Dr("butcher", "kitchen", -9f, 1.28f, 2.2f, "shut", DoorStyle.Single); d.hinge = -1; d.leaf = "SM_Bld_Door_03";
            d = Dr("kitchen", "store", -8f, 1.28f, 2.2f, "shut", DoorStyle.Single); d.hinge = -1; d.leaf = "SM_Bld_Door_06";
            Dr("kitchen", "stair", 25.8f, 1.48f, 2.3f, "open", DoorStyle.Doorway);
            Dr("stair", "hall", 21.8f, 1.88f, 2.6f, "open", DoorStyle.Doorway);
            d = Dr("stair", "hall", 21.8f, 1.28f, 2.3f, "shut", DoorStyle.Single, DeckTop); d.hinge = -1; d.leaf = "SM_Bld_Door_05";
            d = Dr("gun", "hall", 17f, 1.48f, 2.4f, "shut", DoorStyle.Single); d.intoB = false; d.hinge = 1; d.leaf = "SM_Bld_Door_05";
            d = Dr("hall", "library", 16f, 2.48f, 3.0f, "open", DoorStyle.OpenDouble); d.leaf = "SM_Bld_Door_01"; d.openAngle = 92f;
            d = Dr("hall", "chapel", 30f, 2.08f, 3.0f, "shut", DoorStyle.Double); d.leaf = "SM_Bld_Door_06";
            Dr("chapel", "crypt", 35f, 1.28f, 2.2f, "open", DoorStyle.Doorway);
            Dr("library", "strong", 40.725f, 1.33f, 2.15f, "hidden", DoorStyle.Bookcase);
            d = Dr("library", "lab", 16f, 1.48f, 2.4f, "shut", DoorStyle.Single); d.hinge = -1; d.leaf = "SM_Bld_Door_04";
            d = Dr("lab", "ballroom", 16f, 2.48f, 3.0f, "open", DoorStyle.OpenDouble); d.leaf = "SM_Bld_Door_01"; d.openAngle = 92f;
        }

        // ---------------------------------------------------------------- the gallery

        static void BuildGallery()
        {
            var hall = Get("hall");
            var g = Child(hall.root, "Gallery");
            var S = s_Surf;
            // deck
            var deck = Box(g, "GalleryDeck", new Vector3(hall.x0, DeckTop - 0.35f, DeckZ0), new Vector3(hall.x1, DeckTop, DeckZ1), S["floorboards"], hall.RL);
            deck.name = "GalleryDeck";
            // carpet runner on the deck
            Box(g, "GalleryRunner", new Vector3(hall.x0 + 0.6f, DeckTop, DeckZ0 + 1.1f), new Vector3(hall.x1 - 0.4f, DeckTop + 0.012f, DeckZ0 + 2.6f), S["carpet_red"], hall.RL, 0f, false);
            // fascia and beam along the open edge
            Box(g, "Fascia", new Vector3(hall.x0, DeckTop - 0.6f, DeckZ1 - 0.05f), new Vector3(hall.x1, DeckTop + 0.02f, DeckZ1 + 0.12f), S["wood_dark"], hall.RL, 0f, false);
            // columns under the edge
            foreach (float x in new[] { 5.5f, 9.5f, 13.5f, 20.5f, 24.5f, 28.2f })
            {
                Put(hall, "SM_Bld_Base_Pillar_02", x, DeckZ1 - 0.15f, 0f, 0f, 1f, new Vector3(1.1f, (DeckTop - 0.6f) / 3.01f, 1.1f), null, g);
            }
            // cross beams under the deck
            for (float x = hall.x0 + 2f; x < hall.x1 - 0.5f; x += 4f)
                Box(g, "Joist", new Vector3(x - 0.12f, DeckTop - 0.62f, DeckZ0), new Vector3(x + 0.12f, DeckTop - 0.35f, DeckZ1), S["wood_dark"], hall.RL, 0f, false);
            // balustrade: see-through balusters, solid only to the player
            BalustradeX(hall, g, hall.x0, hall.x1, DeckZ1 - 0.13f, DeckTop);
            // the screens passage: timber screens between the columns beneath the gallery, with two openings
            float sz0 = DeckZ1 - 0.25f, sz1 = DeckZ1 - 0.05f;
            foreach (var (a, b) in new[] { (hall.x0, 5.26f), (5.74f, 9.26f), (9.74f, 13.26f), (15.6f, 20.26f), (20.74f, 24.26f), (26.6f, 27.96f), (28.44f, hall.x1) })
            {
                Box(g, "Screen", new Vector3(a, 0f, sz0 + 0.04f), new Vector3(b, 2.3f, sz1 - 0.04f), S["wood_dark"], hall.RL);
                Box(g, "ScreenRail", new Vector3(a, 2.3f, sz0), new Vector3(b, 2.42f, sz1), S["wood_trim"], hall.RL, 0f, false);
                Box(g, "ScreenRail", new Vector3(a, 0.9f, sz0), new Vector3(b, 0.98f, sz1), S["wood_trim"], hall.RL, 0f, false);
            }
        }

        /// <summary>A balustrade running along x. Its meshes carry no colliders; an invisible blocker on the
        /// SeeThrough layer stops the player but not sight lines.</summary>
        static void BalustradeX(Room r, Transform parent, float xa, float xb, float z, float y)
        {
            float len = xb - xa;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 2.5f));
            float s = len / (n * 2.5f);
            for (int i = 0; i < n; i++)
            {
                float x = xa + (i + 1) * 2.5f * s;
                Put(r, "SM_Bld_Base_Stairs_Ornate_Rail_01", x, z, 0f, y - r.fy, 1f, new Vector3(s, 1f, 1f), null, parent, false);
            }
            Blocker(parent, "RailBlocker", new Vector3((xa + xb) * 0.5f, y + 0.6f, z), new Vector3(len, 1.2f, 0.25f), Quaternion.identity, LayerHelper);
        }

        // ---------------------------------------------------------------- the back stair

        static void BuildBackStair()
        {
            var r = Get("stair");
            var g = Child(r.root, "Stair");
            var S = s_Surf;
            // partition between the floor passage and the flight
            Box(g, "Partition", new Vector3(-0.4f, -0.3f, 24.6f), new Vector3(0f, r.h, 31.0f), S["boards"], r.RL, r.fy);
            // the flight rises south from z = 31 to the landing at z = 24.6
            Flight(r, g, new Vector3(0.8f, 0f, 31.0f), Vector3.back, 1.6f, DeckTop, 24, 6.4f / 24f, S["floorboards"], S["wood_dark"]);
            // landing over the south end
            Box(g, "Landing", new Vector3(r.x0, DeckTop - 0.3f, r.z0), new Vector3(r.x1, DeckTop, 24.6f), S["floorboards"], r.RL);
            Box(g, "LandingEdge", new Vector3(r.x0, DeckTop - 0.45f, 24.5f), new Vector3(-0.4f, DeckTop + 0.01f, 24.68f), S["wood_dark"], r.RL, 0f, false);
            BalustradeX(r, g, r.x0, -0.4f, 24.5f, DeckTop);
            // handrail along the flight
            Box(g, "Handrail", new Vector3(1.48f, 0.9f, 24.6f), new Vector3(1.56f, 0.98f, 31f), S["wood_dark"], r.RL, 0f, false)
                .transform.rotation = Quaternion.identity;
        }

        // ---------------------------------------------------------------- the stage

        static Transform s_Stage;

        static void BuildStage()
        {
            var r = Get("ballroom");
            var g = Child(r.root, "Stage");
            s_Stage = g;
            var S = s_Surf;
            Box(g, "StageTop", new Vector3(10f, -0.3f, 70.4f), new Vector3(22f, StageTop, r.z1), S["floorboards"], r.RL);
            Box(g, "StageApron", new Vector3(10f, 0f, 70.32f), new Vector3(22f, StageTop - 0.06f, 70.42f), S["wood_dark"], r.RL, 0f, false);
            Box(g, "StageLip", new Vector3(9.95f, StageTop - 0.06f, 70.3f), new Vector3(22.05f, StageTop + 0.04f, 70.5f), S["wood_trim"], r.RL, 0f, false);
            Flight(r, g, new Vector3(16f, 0f, 70.4f - 6 * 0.3f), Vector3.forward, 6f, StageTop, 6, 0.3f, S["floorboards"], S["wood_dark"]);
            Box(g, "StageCarpet", new Vector3(13.5f, StageTop, 72.2f), new Vector3(18.5f, StageTop + 0.015f, 76f), S["carpet_red"], r.RL, 0f, false);
        }

        // ---------------------------------------------------------------- the cells

        static void BuildCells()
        {
            var r = Get("cells");
            var g = Child(r.root, "Cells");
            var S = s_Surf;
            float pitch = 2.8f;
            // partitions, south row: cells z [-2, 1]; north row: z [6, 9]
            for (int i = 0; i <= 6; i++)
            {
                float x = r.x0 + i * pitch;
                if (i > 0 && i < 6) Box(g, "CellWall", new Vector3(x - 0.1f, -0.3f, r.z0), new Vector3(x + 0.1f, r.h, 1f), S["brick_grey"], r.RL, r.fy);
            }
            foreach (float x in new[] { -10.2f, -7.4f, -4.6f, -1.8f, 1.0f })
                Box(g, "CellWall", new Vector3(x - 0.1f, -0.3f, 6f), new Vector3(x + 0.1f, r.h, r.z1), S["brick_grey"], r.RL, r.fy);
            // barred fronts: some standing, some forced off their hinges
            float sy = (r.h - 0.02f) / 3.0f;
            float sx = (pitch - 0.2f) / 2.5f;
            int k = 0;
            for (int i = 0; i < 6; i++)
            {
                float xl = r.x0 + i * pitch + 0.1f, xr = xl + pitch - 0.2f;
                CellFront(r, g, xl, xr, 1f, 0f, sx, sy, k++);
            }
            float[] north = { -10.2f, -7.4f, -4.6f, -1.8f };
            foreach (var xl0 in north)
            {
                float xl = xl0 + 0.1f, xr = xl0 + pitch - 0.1f;
                CellFront(r, g, xl, xr, 6f, 180f, sx, sy, k++);
            }
        }

        static void CellFront(Room r, Transform g, float xl, float xr, float z, float yaw, float sx, float sy, int k)
        {
            // states: 0 standing, 1 forced and leaning, 2 torn down on the floor
            int state = new[] { 0, 1, 0, 2, 1, 0, 2, 0, 1, 0 }[k % 10];
            float pivotX = yaw == 0f ? xr : xl;
            if (state == 0)
            {
                Put(r, "SM_Bld_Cell_Door_01", pivotX, z, yaw, 0f, 1f, new Vector3(sx, sy, 1f), null, g);
            }
            else if (state == 1)
            {
                var go = Put(r, "SM_Bld_Cell_Door_01", pivotX, z + (yaw == 0f ? 0.25f : -0.25f), yaw + (yaw == 0f ? -14f : 14f), 0f, 1f, new Vector3(sx, sy, 1f), null, g);
                go.transform.rotation = go.transform.rotation * Quaternion.Euler(yaw == 0f ? -9f : 9f, 0f, 3f);
            }
            else
            {
                float zf = yaw == 0f ? z + 1.9f : z - 1.9f;
                var go = Put(r, "SM_Bld_Cell_Door_01", pivotX + (yaw == 0f ? -0.15f : 0.15f), zf, 0f, 0.06f, 1f, new Vector3(sx, sy, 1f), null, g, false);
                go.transform.rotation = Quaternion.Euler(yaw == 0f ? 90f : -90f, yaw + 8f, 0f);
                go.transform.position = new Vector3(pivotX + (yaw == 0f ? -0.1f : 0.1f), r.fy + 0.1f, yaw == 0f ? z + 0.05f : z - 0.05f);
            }
        }

        // ---------------------------------------------------------------- the hidden bookcase

        static void BuildHiddenBookcase()
        {
            var lib = Get("library");
            var d = s_Doors.Find(x => x.style == DoorStyle.Bookcase);
            float p = 40f; // south end of the bookcase leaf
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(d.root, false);
            hinge.position = new Vector3(lib.x0 + 0.61f, lib.fy, p + 2f);
            hinge.rotation = Quaternion.identity;
            var bc = LibraryBookcase(lib, hinge, p, 2);
            foreach (var t in bc.GetComponentsInChildren<Transform>()) t.gameObject.layer = LayerDoorLeaf;
            foreach (var rend in bc.GetComponentsInChildren<Renderer>()) rend.renderingLayerMask = lib.RL | Get("strong").RL;
            var col = bc.AddComponent<BoxCollider>();
            // collider in the bookcase's local frame (x -2.5..0 before scale, z 0.11..0.72)
            col.center = new Vector3(-1.25f, 1.5f, 0.42f);
            col.size = new Vector3(2.5f, 3.0f, 0.6f);
            var sw = d.root.gameObject.AddComponent<SwingDoor>();
            sw.hinges = new[] { hinge }; sw.openAngles = new[] { -95f }; sw.hidden = true; sw.speed = 70f;
            d.swing = sw; d.info.door = sw;
        }

        /// <summary>A 2 m wide, 3.6 m tall bookcase standing against the library's west wall, its south end at z.</summary>
        static GameObject LibraryBookcase(Room lib, Transform parent, float z, int insert)
        {
            var bc = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("SM_Bld_Bookshelf_01"));
            bc.transform.SetParent(parent, true);
            bc.transform.position = new Vector3(lib.x0 - 0.11f, lib.fy, z);
            bc.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            bc.transform.localScale = new Vector3(0.8f, 1.2f, 1f);
            foreach (var c in bc.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var ins = (GameObject)PrefabUtility.InstantiatePrefab(Prefab("SM_Bld_Bookshelf_01_Insert_0" + insert));
            ins.transform.SetParent(bc.transform, false);
            foreach (var c in ins.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var rend in bc.GetComponentsInChildren<Renderer>()) rend.renderingLayerMask = lib.RL;
            s_PropCount++;
            return bc;
        }
    }
}
