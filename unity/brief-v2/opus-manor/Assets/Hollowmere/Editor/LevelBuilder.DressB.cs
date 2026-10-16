using UnityEngine;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        // ================================================================ DINING HALL
        static void DressHall()
        {
            var r = Get("hall");
            KeepClear("hall", 2f, 20.2f, 30f, 23.2f);        // beneath the gallery
            KeepClear("hall", 14.6f, 20f, 17.6f, 36f);       // across the hall
            KeepClear("hall", 26f, 28.6f, 30f, 31.4f);       // to the chapel door
            // two long tables, east-west, with a crossing in the middle
            float[] rows = { 27.6f, 31.6f };
            for (int ri = 0; ri < rows.Length; ri++)
            {
                float z = rows[ri];
                for (int k = 0; k < 6; k++)
                {
                    float x = k < 3 ? 6.0f + k * 3.44f : 19.3f + (k - 3) * 3.44f;
                    bool laid = (ri == 0 && k < 3) || (ri == 1 && k >= 4);
                    bool overturned = (ri == 0 && k == 4) || (ri == 1 && k == 1);
                    var t = Put(r, "SM_Prop_Table_01", x, z, 90f + (overturned ? 0f : Rf(-1.5f, 1.5f)));
                    if (overturned) { Topple(t, 0f, 90f); t.transform.position += new Vector3(0f, 0f, ri == 0 ? -0.5f : 0.5f); continue; }
                    if (laid)
                    {
                        Put(r, "SM_Prop_Table_01_Cloth_01", x, z, 90f, 0f, 1f, null, null, null, false);
                        for (int s = -1; s <= 1; s += 2)
                            for (int j = 0; j < 3; j++)
                            {
                                float px = x - 1.1f + j * 1.1f;
                                PutOn(r, "SM_Prop_Plate_Set_01", px, z + s * 0.62f, 0.92f, s > 0 ? 180f : 0f);
                                if (j == 1) PutOn(r, "SM_Prop_Goblet_01", px + 0.3f, z + s * 0.4f, 0.92f);
                            }
                        PutOn(r, "SM_Prop_Candle_Stand_01", x, z, 0.92f, 90f);
                        PutOn(r, Pick("SM_Prop_Bowl_01", "SM_Gen_Prop_Food_Bread_02", "SM_Gen_Prop_Pot_05"), x + 1.2f, z + 0.1f, 0.92f);
                    }
                    else
                    {
                        PutOn(r, Pick("SM_Prop_Plate_02", "SM_Prop_Bowl_02", "SM_Prop_Goblet_01"), x - 0.8f, z + 0.3f, 0.91f, Rf(0, 360));
                        PutOn(r, "SM_Prop_Candle_Stand_02", x + 0.6f, z - 0.2f, 0.91f, Rf(0, 360)).transform.rotation = Quaternion.Euler(0f, Rf(0, 360), 90f);
                    }
                    // chairs on both sides
                    for (int s = -1; s <= 1; s += 2)
                        for (int j = 0; j < 3; j++)
                        {
                            float cx = x - 1.1f + j * 1.1f, cz = z + s * 1.25f;
                            int roll = Ri(0, 10);
                            if (roll < 2) continue;
                            var ch = Put(r, "SM_Prop_Chair_03", cx + Rf(-0.1f, 0.1f), cz + s * Rf(0f, 0.25f), (s > 0 ? 180f : 0f) + Rf(-12f, 12f));
                            if (roll < 4) { Topple(ch, Pick(90f, -90f), 0f); ch.transform.position += new Vector3(0f, 0f, s * 0.3f); }
                        }
                }
            }
            // sideboards and the fireplace
            foreach (var x in new[] { 6.5f, 11.0f, 21.0f, 25.5f })
            {
                Put(r, "SM_Prop_Cabinet_01", x, r.z1 - 0.48f, 180f);
                PutOn(r, "SM_Prop_Display_Plate_02", x - 0.5f, r.z1 - 0.45f, 2.1f, 180f);
                PutOn(r, "SM_Prop_Candle_Stand_01", x + 0.5f, r.z1 - 0.45f, 2.1f, 180f);
            }
            OnWall(r, "SM_Bld_Fireplace_01", 'W', 28.75f, 0f, 0f, 1f, null, true);
            OnWall(r, "SM_Prop_Mirror_01", 'W', 30.0f, 2.1f, 0.02f, 1.1f);
            Put(r, "SM_Prop_ArmChair_01", 3.8f, 32.4f, 120f);
            Put(r, "SM_Prop_ArmChair_03", 3.8f, 27.8f, 60f);
            // portraits, high on the walls and along the gallery
            foreach (var x in new[] { 5.5f, 10.0f, 22.0f, 26.5f }) Portrait(r, 'N', x, 4.3f, 1.5f, 5, 1 + (int)x % 5);
            foreach (var x in new[] { 6f, 12f, 20f, 26f }) Portrait(r, 'S', x, DeckTop + 2.2f, 1.1f, 4, 1 + (int)x % 5);
            Portrait(r, 'E', 25.6f, 3.2f, 1.2f, 6, 2); Portrait(r, 'E', 34.0f, 3.2f, 1.2f, 6, 4);
            Portrait(r, 'W', 34.2f, 3.2f, 1.2f, 6, 3); Portrait(r, 'E', 21.8f, DeckTop + 2.2f, 1.0f, 1, 5);
            foreach (var x in new[] { 3.6f, 28.4f }) OnWall(r, "SM_Prop_Curtain_01_Closed_01", 'N', x + 1.22f, 0f, 0f, 1f, new Vector3(1f, 2.6f, 1f));
            // chandeliers
            foreach (var x in new[] { 9f, 16f, 23f })
            {
                Hang(r, "SM_Prop_Light_Chandelier_01", x, 29.6f, 1.15f, 0f);
                Lamp(r, "Chandelier", r.P(x, 5.4f, 29.6f), Warm, 7.0f, 16f, x == 16f);
            }
            // sconces along the walls, the gallery and the arcade beneath it
            foreach (var (side, along, y) in new[] { ('N', 8f, 3.1f), ('N', 24f, 3.1f), ('E', 26.8f, 3.1f), ('E', 33.2f, 3.1f), ('W', 26.0f, 3.1f), ('W', 33.6f, 3.1f),
                                                    ('S', 4.5f, DeckTop + 2.0f), ('S', 15.5f, DeckTop + 2.0f), ('S', 27.5f, DeckTop + 2.0f),
                                                    ('S', 8f, 2.3f), ('S', 22f, 2.3f), ('S', 27.5f, 2.3f) })
            {
                OnWall(r, "SM_Prop_Light_Wall_01", side, along, y, 0f);
                Lamp(r, "Sconce", WallPoint(r, side, along, y + 0.2f, 0.35f), Candle, y > 4f ? 2.0f : 1.6f, 6.5f);
            }
            Lamp(r, "HallFill", r.P(16f, 7.5f, 29f), Warm, 2.0f, 20f);
            Lamp(r, "PassageLamp", r.P(17f, 2.6f, 20.6f), Candle, 1.0f, 5f);
            Lamp(r, "PassageLamp", r.P(4.0f, 2.6f, 21.0f), Candle, 0.8f, 5f);
            // on the gallery: music stands and chairs
            Put(r, "SM_Prop_Tripod_01", 8.5f, 21.2f, 0f, DeckTop);
            Put(r, "SM_Prop_Chair_01", 13.2f, 20.9f, 10f, DeckTop);
            Put(r, "SM_Prop_Chair_01", 18.8f, 21.0f, -15f, DeckTop);
            Topple(Put(r, "SM_Prop_Chair_01", 23.5f, 21.1f, 80f, DeckTop), 90f).transform.position += Vector3.up * DeckTop;
            Put(r, "SM_Prop_Gramophone_01", 28.6f, 20.6f, 200f, DeckTop);
            // decay
            Scatter(r, new[] { "SM_Prop_Plate_01", "SM_Prop_Plate_03", "SM_Prop_Fork_02", "SM_Prop_Spoon_02", "SM_Prop_Goblet_01" }, 4f, 24f, 28f, 35.5f, 16, 80f);
            Scatter(r, Papers, 3f, 24f, 29f, 35.5f, 6);
            Neglect(r, 14, 4);
        }

        // ================================================================ LIBRARY
        static void DressLibrary()
        {
            var r = Get("library");
            KeepClear("library", 14.2f, 36.4f, 17.8f, 46f);
            KeepClear("library", 4.6f, 38.6f, 7.6f, 43.4f);
            // the west wall: bookcases, one of them the hidden door
            int ins = 1;
            foreach (var z in new[] { 38f, 42f, 44f }) LibraryBookcase(r, r.props, z, 1 + (ins++ % 3));
            Box(r.props, "WallBookcaseCollider", new Vector3(r.x0, 0f, 38f), new Vector3(r.x0 + 0.6f, 3.6f, 40f), s_Surf["wood_dark"], r.RL, 0f, true).GetComponent<MeshRenderer>().enabled = false;
            Box(r.props, "WallBookcaseCollider", new Vector3(r.x0, 0f, 42f), new Vector3(r.x0 + 0.6f, 3.6f, 46f), s_Surf["wood_dark"], r.RL, 0f, true).GetComponent<MeshRenderer>().enabled = false;
            Put(r, "SM_Prop_Clock_01", r.x0 + 0.35f, 37.1f, 90f);
            // the other walls
            WallBookcases(r, 'N', new[] { 4.6f, 6.6f, 8.6f, 10.6f, 12.6f, 18.4f, 20.4f, 22.4f, 24.4f });
            WallBookcases(r, 'S', new[] { 6.6f, 8.6f, 10.6f, 12.6f, 21.4f, 23.4f, 25.4f, 27.4f });
            WallBookcases(r, 'E', new[] { 38.4f, 40.4f, 42.4f, 44.4f, 46.0f });
            // freestanding stacks, packed close
            int n = 0;
            foreach (var x in new[] { 8.6f, 11.1f, 13.6f, 18.4f, 20.9f, 23.4f })
            {
                for (int k = 0; k < 2; k++)
                {
                    float z = 39.6f + k * 2f;
                    StackHalf(r, x, z, 90f, 1 + (n++ % 3));
                    StackHalf(r, x, z, -90f, 1 + (n++ % 3));
                }
                var c = new GameObject("StackCollider");
                c.transform.SetParent(r.props, false);
                c.transform.position = new Vector3(x, 1.8f, 41.6f);
                c.AddComponent<BoxCollider>().size = new Vector3(1.24f, 3.6f, 4.0f);
            }
            // reading tables, lamps, armchairs
            foreach (var x in new[] { 10.0f, 22.0f })
            {
                Put(r, "SM_Prop_Table_02", x, 38.0f, 0f);
                PutOn(r, "SM_Prop_Lamp_01", x + 0.4f, 37.8f, 0.9f);
                Lamp(r, "ReadingLamp", r.P(x + 0.4f, 1.6f, 37.8f), Warm, 2.0f, 7f, x < 15f);
                Put(r, "SM_Prop_Chair_02", x - 1.2f, 38.2f, 90f);
                Topple(Put(r, "SM_Prop_Chair_02", x + 1.3f, 38.6f, -80f), 90f);
                PutOn(r, "SM_Prop_Book_Open_01", x - 0.3f, 38.2f, 0.9f, 20f);
                PutOn(r, "SM_Prop_Book_Pile_02", x + 0.1f, 38.5f, 0.9f, 70f);
            }
            Put(r, "SM_Prop_Desk_02", 25.9f, 41.6f, -90f);
            PutOn(r, "SM_Prop_Lamp_01", 26.0f, 42.2f, 0.84f);
            Lamp(r, "DeskLamp", r.P(26.0f, 1.5f, 42.2f), Warm, 1.6f, 6f);
            Put(r, "SM_Prop_ArmChair_03", 25.0f, 44.6f, 200f);
            Put(r, "SM_Prop_ArmChair_01", 25.4f, 38.8f, -30f);
            var ladder = Put(r, "SM_Prop_Ladder_01", 19.4f, 39.3f, 0f);
            ladder.transform.rotation = Quaternion.Euler(-14f, 0f, 0f);
            // books pulled down and scattered
            Scatter(r, Books, 5f, 37f, 27f, 45.6f, 40, 60f);
            Scatter(r, new[] { "SM_Prop_Book_Pile_01", "SM_Prop_Book_Pile_03", "SM_Prop_Book_Pile_04" }, 5f, 37f, 27f, 45.6f, 8);
            Scatter(r, Papers, 5f, 37f, 27f, 45.6f, 8);
            Put(r, "SM_Prop_Rug_Tileable_01", 16f + 1.25f, 37.2f, 0f, 0f, 1f, new Vector3(1.4f, 1f, 1f), null, null, false);
            Put(r, "SM_Prop_Rug_Tileable_01", 16f + 1.25f, 39.7f, 0f, 0f, 1f, new Vector3(1.4f, 1f, 1f), null, null, false);
            Put(r, "SM_Prop_Rug_Tileable_01", 16f + 1.25f, 42.2f, 0f, 0f, 1f, new Vector3(1.4f, 1f, 1f), null, null, false);
            foreach (var z in new[] { 39.2f, 43.6f })
            {
                Hang(r, "SM_Prop_Light_Chandelier_03", 16f, z, 1.1f);
                Lamp(r, "Chandelier", r.P(16f, 3.9f, z), Warm, 2.6f, 11f, z > 40f);
            }
            Lamp(r, "AisleFill", r.P(6.2f, 3.2f, 41f), Warm, 1.2f, 7f);
            Neglect(r, 8, 4);
        }

        static void WallBookcases(Room r, char side, float[] starts)
        {
            int i = 0;
            foreach (var a in starts)
            {
                // place so the bookcase spans [a, a + 2] along the wall
                float pivot = side switch { 'S' => a + 2f, 'N' => a, 'W' => a, _ => a + 2f };
                var p = WallPoint(r, side, pivot, 0f, -0.11f);
                var bc = Put(r, "SM_Bld_Bookshelf_01", p.x, p.z, FaceIn(side), 0f, 1f, new Vector3(0.8f, 1.2f, 1f), null, null, true);
                var ins = Put(r, "SM_Bld_Bookshelf_01_Insert_0" + (1 + (i++ % 3)), p.x, p.z, FaceIn(side), 0f, 1f, null, null, bc.transform, false);
                ins.transform.localPosition = Vector3.zero; ins.transform.localRotation = Quaternion.identity; ins.transform.localScale = Vector3.one;
            }
        }

        static void StackHalf(Room r, float x, float z, float yaw, int insert)
        {
            float px = yaw > 0 ? x - 0.11f : x + 0.11f;
            float pz = yaw > 0 ? z : z + 2f;
            var bc = Put(r, "SM_Bld_Bookshelf_01", px, pz, yaw, 0f, 1f, new Vector3(0.8f, 1.2f, 1f), null, null, false);
            var ins = Put(r, "SM_Bld_Bookshelf_01_Insert_0" + insert, px, pz, yaw, 0f, 1f, null, null, bc.transform, false);
            ins.transform.localPosition = Vector3.zero; ins.transform.localRotation = Quaternion.identity; ins.transform.localScale = Vector3.one;
        }

        // ================================================================ STRONG ROOM
        static readonly Vector3 HoardPos = new Vector3(0.4f, 0.0f, 41.0f);

        static void DressStrongRoom()
        {
            var r = Get("strong");
            Put(r, "SM_Prop_Safe_01", r.x0 + 0.45f, 39.6f, 90f);
            Put(r, "SM_Prop_Safe_01", r.x0 + 0.45f, 42.4f, 90f);
            Put(r, "SM_Prop_Table_04", 0.5f, 41.0f, 90f);
            PutOn(r, "SM_Gen_Prop_Chest_01", 0.5f, 41.4f, 0.92f, 90f, 0.8f);
            PutOn(r, "SM_Prop_Lockbox_01", 0.5f, 40.5f, 0.92f, 80f);
            Put(r, "SM_Prop_Lockbox_02", 1.6f, r.z1 - 0.35f, 180f);
            PutOn(r, "SM_Prop_Lockbox_01", 1.6f, r.z1 - 0.35f, 0.47f, 175f);
            Put(r, "SM_Gen_Prop_Chest_02", 2.4f, r.z0 + 0.45f, 0f);
            Put(r, "SM_Prop_Chest_01", 1.0f, r.z0 + 0.45f, 0f);
            // the family silver on shelves
            OnWall(r, "SM_Prop_Shelf_02", 'N', 2.6f, 1.25f, 0f, 1f, null, true);
            OnWall(r, "SM_Prop_Shelf_02", 'S', 2.8f, 1.25f, 0f, 1f, null, true);
            foreach (var (side, along) in new[] { ('N', 2.3f), ('N', 2.9f), ('S', 2.5f), ('S', 3.1f) })
            {
                var p = WallPoint(r, side, along, 0f, 0.15f);
                PutOn(r, "SM_Prop_Goblet_01", p.x, p.z, 1.38f);
                PutOn(r, Pick("SM_Prop_Candle_Stand_01", "SM_Prop_Display_Plate_01"), p.x, p.z, 0.75f, FaceIn(side));
            }
            PutOn(r, "SM_Gen_Prop_Coin_Pile_02", 0.4f, 40.9f, 0.92f);
            PutOn(r, "SM_Gen_Prop_Coin_Stack_01", 0.7f, 40.3f, 0.92f);
            PutOn(r, "SM_Gen_Prop_Coin_Stack_02", 0.3f, 41.8f, 0.92f);
            PutOn(r, "SM_Prop_Candle_Stand_02", 0.6f, 41.9f, 0.92f, 90f);
            Hang(r, "SM_Prop_Light_Bulb_01", 1.6f, 41.0f, 0.5f);
            Lamp(r, "Bulb", r.P(1.6f, 1.75f, 41.0f), Warm, 0.9f, 5f, true);
            Scatter(r, Dust, r.x0 + 0.3f, r.z0 + 0.3f, r.x1 - 0.3f, r.z1 - 0.3f, 4, 0f, 0.6f, 1f);
            Neglect(r, 0, 4);
        }

        // ================================================================ LABORATORY
        static void DressLab()
        {
            var r = Get("lab");
            KeepClear("lab", 14.6f, 46.4f, 17.4f, 54f);
            string[] glass = { "SM_Prop_Science_Bottle_01", "SM_Prop_Science_Bottle_02", "SM_Prop_Science_Burner_01", "SM_Prop_Exorcist_Bottle_01", "SM_Prop_Exorcist_Bottle_02",
                               "SM_Prop_Exorcist_Bottle_03", "SM_Prop_Exorcist_Bottle_04", "SM_Prop_Exorcist_Bottle_05", "SM_Prop_Specimen_Jar_01", "SM_Prop_Specimen_Jar_02", "SM_Gen_Prop_Potion_01" };
            foreach (var z in new[] { 48.6f, 51.8f })
                foreach (var x in new[] { 10.0f, 12.4f, 19.6f, 22.0f })
                {
                    Put(r, "SM_Prop_Kitchen_Bench_01", x, z, 0f);
                    for (int k = 0; k < 5; k++)
                    {
                        var g = PutOn(r, Pick(glass), x - 0.9f + k * 0.45f + Rf(-0.08f, 0.08f), z + Rf(-0.3f, 0.3f), 0.96f, Rf(0, 360));
                        if (Ri(0, 3) == 0) { g.transform.rotation = Quaternion.Euler(90f, Rf(0, 360), 0f); g.transform.position += Vector3.up * 0.05f; }
                    }
                }
            // smashed glassware and stains on the floor
            Scatter(r, glass, 8.6f, 47f, 23.6f, 53.6f, 14, 90f);
            Scatter(r, new[] { "SM_Prop_Decal_Floor_Large" }, 9f, 47f, 23f, 53.5f, 6, 0f, 0.7f, 1.5f);
            Scatter(r, Papers, 9f, 47f, 23f, 53.5f, 6);
            // shelves of specimen jars
            foreach (var (side, along) in new[] { ('W', 48.4f), ('W', 51.6f), ('E', 48.4f), ('E', 51.6f), ('N', 10.5f), ('N', 21.5f) })
            {
                OnWall(r, "SM_Prop_Shelf_03", side, along, 1.3f, 0f, 1f, null, true);
                for (int k = 0; k < 3; k++)
                {
                    var p = WallPoint(r, side, along - 0.4f + k * 0.4f, 0f, 0.14f);
                    PutOn(r, Pick("SM_Prop_Specimen_Jar_01", "SM_Prop_Specimen_Jar_02", "SM_Prop_Exorcist_Bottle_02"), p.x, p.z, 1.1f, 0f, 0.8f);
                    PutOn(r, Pick("SM_Prop_Specimen_Jar_02", "SM_Gen_Prop_Bottle_01", "SM_Prop_Science_Bottle_02"), p.x, p.z, 1.66f, 0f, 0.7f);
                }
            }
            // the operating table and the master's oddities
            Put(r, "SM_Prop_Table_04", 22.6f, 47.3f, 0f);
            Put(r, "SM_Prop_Cabinet_02", 9.2f, r.z1 - 0.55f, 180f);
            PutOn(r, "SM_Prop_Crystal_Ball_01", 12.4f, 51.6f, 0.96f);
            PutOn(r, "SM_Prop_Cat_Stuffed_Badly_01", 19.6f, 48.5f, 0.96f, 200f);
            Put(r, "SM_Prop_Cage_01", 22.4f, 53.0f, 0f, 0f, 0.8f);
            Topple(Put(r, "SM_Prop_Stool_01", 13.2f, 50.2f, 0f), 90f);
            Put(r, "SM_Prop_Stool_01", 20.8f, 50.2f, 0f);
            foreach (var x in new[] { 11.2f, 20.8f })
            {
                Hang(r, "SM_Prop_Light_Bulb_01", x, 50.2f, 0.6f);
                Lamp(r, "Bulb", r.P(x, 2.9f, 50.2f), Sick, 2.0f, 8f, x < 15f);
            }
            Lamp(r, "AisleFill", r.P(16f, 2.8f, 50.2f), Cold, 0.9f, 7f);
            Neglect(r, 6, 4);
        }

        // ================================================================ BALLROOM
        static void DressBallroom()
        {
            var r = Get("ballroom");
            KeepClear("ballroom", 14.2f, 54.4f, 17.8f, 70.4f);
            // pilasters along the side walls
            foreach (var z in new[] { 57.4f, 62.0f, 66.6f, 71.2f })
            {
                Put(r, "SM_Bld_Base_Pillar_02", r.x0 + 0.3f, z, 0f, 0f, 1f, new Vector3(1.6f, r.h / 3.01f, 1.6f));
                Put(r, "SM_Bld_Base_Pillar_02", r.x1 - 0.3f, z, 0f, 0f, 1f, new Vector3(1.6f, r.h / 3.01f, 1.6f));
            }
            // the torn runner from the door to the stage
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) continue;
                Put(r, "SM_Prop_Rug_Tileable_01", 16f + 1.25f, 55.6f + i * 2.5f, 0f, 0f, 1f, null, null, null, false);
            }
            var torn = Put(r, "SM_Prop_Rug_Tileable_01", 16f + 1.25f + 0.5f, 60.9f, 14f, 0.01f, 1f, new Vector3(1f, 1f, 0.6f), null, null, false);
            Put(r, "SM_Prop_Rug_Tileable_End_01", 16f + 1.25f, 55.6f, 180f, 0f, 1f, null, null, null, false).transform.position = new Vector3(16f - 1.25f + 0.02f, 0f, 55.62f);
            Put(r, "SM_Prop_Rug_Tileable_End_01", 16f + 1.25f, 68.1f, 0f, 0f, 1f, null, null, null, false);
            // the stage: the master's chair, candelabra, the curtain behind
            var chair = Put(r, "SM_Prop_ArmChair_05", ChairPos.x, ChairPos.z, 180f, StageTop, 1.15f);
            chair.name = "MastersChair";
            chair.AddComponent<ChairGoal>();
            Put(r, "SM_Prop_Candle_Stand_03", 13.6f, 74.9f, 180f, StageTop);
            Put(r, "SM_Prop_Candle_Stand_03", 18.4f, 74.9f, 180f, StageTop);
            foreach (var a in new[] { 10.0f, 12.5f, 15.0f, 17.5f, 20.0f })
                OnWall(r, "SM_Prop_Curtain_01_Closed_01", 'N', a, StageTop, 0.02f, 1f, new Vector3(1.04f, 2.6f, 1f));
            // sparse furniture: a piano, a few gilt chairs, a gramophone
            Put(r, "SM_Prop_Piano_01", 26.6f, 59.4f, 200f);
            Put(r, "SM_Prop_Chair_04", 4.0f, 59.6f, 90f);
            Put(r, "SM_Prop_Chair_04", 4.0f, 64.4f, 90f);
            Topple(Put(r, "SM_Prop_Chair_04", 27.6f, 68.6f, -100f), 90f);
            Put(r, "SM_Prop_Chair_04", 28.0f, 64.2f, -90f);
            // the fallen chandelier and the plaster it brought down
            var fallen = Put(r, "SM_Prop_Light_Chandelier_01", 8.6f, 65.0f, 0f, 0f, 1.25f);
            fallen.transform.rotation = Quaternion.Euler(78f, 25f, 0f);
            fallen.transform.position = new Vector3(8.6f, 1.3f, 65.0f);
            Scatter(r, Rocks, 6.5f, 63f, 11f, 67.5f, 9, 0f, 0.1f, 0.25f);
            Box(r.props, "BrokenChain", new Vector3(8.58f, r.h - 1.6f, 64.98f), new Vector3(8.62f, r.h, 65.02f), s_Surf["iron"], r.RL, 0f, false);
            // chandeliers
            foreach (var (x, z, s) in new[] { (16f, 59.5f, 1.55f), (16f, 66.0f, 1.55f), (23.5f, 62.6f, 1.2f), (8.5f, 58.2f, 1.2f), (23.5f, 69.2f, 1.2f) })
            {
                Hang(r, "SM_Prop_Light_Chandelier_01", x, z, s);
                Lamp(r, "Chandelier", r.P(x, r.h - 3.6f * s + 0.8f, z), Warm, 9.0f, 20f, z < 60f);
            }
            // sconces between the pilasters
            foreach (var z in new[] { 59.7f, 64.3f, 68.9f })
                foreach (var side in new[] { 'W', 'E' })
                {
                    OnWall(r, "SM_Prop_Light_Wall_01", side, z, 3.2f, 0f, 1.2f);
                    Lamp(r, "Sconce", WallPoint(r, side, z, 3.4f, 0.4f), Candle, 2.4f, 8f);
                }
            Lamp(r, "StageSpot", r.P(16f, 9.5f, 70.5f), Warm, 30f, 16f, true, LightType.Spot, new Vector3(62f, 0f, 0f), 36f);
            Lamp(r, "Fill", r.P(16f, 9.0f, 63f), Warm, 3.0f, 26f);
            Portrait(r, 'S', 8.0f, 4.4f, 1.6f, 5, 2);
            Portrait(r, 'S', 24.0f, 4.4f, 1.6f, 5, 3);
            Neglect(r, 10, 4);
        }

        // ================================================================ CHAPEL
        static void DressChapel()
        {
            var r = Get("chapel");
            KeepClear("chapel", 30.4f, 29.0f, 37.0f, 31.0f);
            KeepClear("chapel", 33.8f, 24f, 36.6f, 29.4f);
            // the altar on a low dais at the east end
            Box(r.props, "Dais", new Vector3(37.0f, -0.3f, 26.5f), new Vector3(r.x1, 0.26f, 33.5f), s_Surf["tile_stone"], r.RL);
            Put(r, "SM_Prop_Table_01", 38.7f, 30f, 0f, 0.26f);
            Put(r, "SM_Prop_Table_01_Cloth_01", 38.7f, 30f, 0f, 0.26f, 1f, null, null, null, false);
            PutOn(r, "SM_Prop_Candle_Stand_01", 38.7f, 29.0f, 1.18f, 90f);
            var tipped = PutOn(r, "SM_Prop_Candle_Stand_02", 38.6f, 31.0f, 1.18f, 90f);
            tipped.transform.rotation = Quaternion.Euler(0f, 60f, 90f);
            PutOn(r, "SM_Prop_Exorcist_Bible_01", 38.9f, 30.2f, 1.18f, 80f);
            var cross = OnWall(r, "SM_Prop_Exorcist_Cross_03", 'E', 30f, 3.6f, 0.02f, 5f);
            cross.transform.rotation = cross.transform.rotation * Quaternion.Euler(0f, 0f, 180f); // hung upside down
            OnWall(r, "SM_Prop_Exorcist_Cross_01", 'N', 33.0f, 2.6f, 0.02f, 3f);
            var fallenCross = Put(r, "SM_Prop_Exorcist_Cross_02", 35.6f, 34.6f, 0f, 0.05f, 3f, null, null, null, false);
            fallenCross.transform.rotation = Quaternion.Euler(90f, 20f, 0f);
            foreach (var (x, z) in new[] { (37.4f, 26.9f), (37.4f, 33.1f), (39.6f, 27.0f), (39.6f, 33.0f) })
                Put(r, "SM_Prop_Candle_Stand_03", x, z, 270f, 0.26f);
            // pews facing the altar, several overturned or broken
            int k = 0;
            foreach (var x in new[] { 32.0f, 33.3f, 34.6f, 35.9f })
                foreach (var z in new[] { 32.2f, 34.2f, 26.4f, 28.2f })
                {
                    if (z < 29f && x > 33.5f) continue;
                    k++;
                    var pew = Put(r, "SM_Prop_Bench_Seat_01", x, z, 90f);
                    if (k % 4 == 2) Topple(pew, 0f, -90f).transform.position += new Vector3(-0.3f, 0f, 0f);
                    else if (k % 5 == 3) pew.transform.rotation = Quaternion.Euler(0f, 90f + Rf(15f, 30f), 0f);
                }
            Topple(Put(r, "SM_Gen_Prop_Statue_04", 31.2f, 34.8f, 30f), 0f, 90f);
            Put(r, "SM_Gen_Prop_Statue_02", 39.3f, 25.0f, -60f);
            // moonlit lancet windows on the north and east walls
            FakeWindow(r, 'N', 32.4f, 2.4f, 1.3f, 4.2f);
            FakeWindow(r, 'N', 37.6f, 2.4f, 1.3f, 4.2f);
            FakeWindow(r, 'E', 26.0f, 2.8f, 1.1f, 3.8f);
            FakeWindow(r, 'E', 34.0f, 2.8f, 1.1f, 3.8f);
            Lamp(r, "MoonShaft", r.P(32.4f, 7.2f, 35.6f), Moon, 6.0f, 14f, true, LightType.Spot, new Vector3(58f, 180f, 0f), 50f);
            Lamp(r, "MoonShaft", r.P(37.6f, 7.2f, 35.6f), Moon, 6.0f, 14f, false, LightType.Spot, new Vector3(58f, 180f, 0f), 50f);
            Lamp(r, "AltarCandles", r.P(38.4f, 1.6f, 30f), Candle, 2.2f, 7f);
            Lamp(r, "Fill", r.P(34f, 5.5f, 30f), Moon, 1.2f, 14f);
            Scatter(r, Papers, 31f, 24.6f, 39.5f, 35.5f, 5);
            Scatter(r, Rocks, 31f, 24.6f, 39.5f, 35.5f, 6, 0f, 0.1f, 0.2f);
            Put(r, "SM_Prop_Decal_Floor_Large", 35.2f, 30.0f, 0f, 0f, 2.6f, null, null, null, false);
            Neglect(r, 8, 4);
        }

        /// <summary>A tall window: a glowing moonlit pane behind iron mullions, set on the wall face.</summary>
        static void FakeWindow(Room r, char side, float along, float y0, float w, float h)
        {
            Vector3 a = WallPoint(r, side, along - w * 0.5f, y0, 0f), b = WallPoint(r, side, along + w * 0.5f, y0 + h, 0.025f);
            var pane = Box(r.props, "WindowPane", a, b, s_Surf["concrete"], r.RL, 0f, false);
            pane.GetComponent<MeshRenderer>().sharedMaterial = s_Mat["glow_moon"];
            // mullions and frame
            var S = s_Surf["iron"];
            for (int i = 0; i <= 2; i++)
            {
                float t = along - w * 0.5f + i * w * 0.5f;
                Box(r.props, "Mullion", WallPoint(r, side, t - 0.04f, y0, 0f), WallPoint(r, side, t + 0.04f, y0 + h, 0.07f), S, r.RL, 0f, false);
            }
            for (int i = 0; i <= 4; i++)
            {
                float yy = y0 + i * h / 4f;
                Box(r.props, "Transom", WallPoint(r, side, along - w * 0.5f, yy - 0.035f, 0f), WallPoint(r, side, along + w * 0.5f, yy + 0.035f, 0.07f), S, r.RL, 0f, false);
            }
            Box(r.props, "Sill", WallPoint(r, side, along - w * 0.5f - 0.1f, y0 - 0.12f, 0f), WallPoint(r, side, along + w * 0.5f + 0.1f, y0, 0.18f), s_Surf["stone_trim"], r.RL, 0f, false);
        }

        // ================================================================ CRYPT
        static void DressCrypt()
        {
            var r = Get("crypt");
            KeepClear("crypt", 34f, 12f, 36f, 23.6f);
            // burial niches in tiers along the east and west walls
            foreach (var side in new[] { 'W', 'E' })
            {
                for (int i = 0; i < 5; i++)
                {
                    float z0 = 12.2f + i * 2.25f;
                    float zc = z0 + 1.1f;
                    // piers
                    Niche(r, side, z0, zc, i);
                }
            }
            // coffins on the floor, some broken open
            foreach (var (x, z, yaw, open) in new[] { (32.9f, 15.0f, 4f, false), (37.5f, 15.2f, -3f, true), (32.9f, 19.0f, 0f, true), (37.5f, 19.4f, 9f, false) })
            {
                Box(r.props, "Plinth", new Vector3(x - 0.6f, 0f, z - 1.15f), new Vector3(x + 0.6f, 0.4f, z + 1.15f), s_Surf["stone_trim"], r.RL);
                var cof = Put(r, open ? "SM_Prop_Coffin_01" : "SM_Prop_Coffin_02", x, z, yaw, 0.4f);
                if (open)
                {
                    var lid = Put(r, "SM_Prop_Coffin_01", x + 0.9f, z + 0.3f, yaw + 25f, 0f, 1f, new Vector3(1f, 0.25f, 1f));
                    lid.transform.rotation = Quaternion.Euler(0f, yaw + 25f, 18f);
                    PutOn(r, "SM_Gen_Prop_Skull_01", x + 0.2f, z - 0.6f, 0.0f, Rf(0, 360), 0.5f);
                }
            }
            foreach (var (x, z) in new[] { (34.0f, 13.0f), (36.0f, 17.2f), (34.2f, 21.2f) })
            {
                Put(r, "SM_Prop_Candle_02", x, z, 0f, 0f, 1.2f, null, null, null, false);
                Lamp(r, "Candle", r.P(x, 0.6f, z), Candle, 0.9f, 4.5f, false);
            }
            Put(r, "SM_Prop_Urn_01", 31.0f, 22.8f, 0f);
            Put(r, "SM_Gen_Prop_Statue_03", 39.2f, 22.8f, 225f, 0f, 0.9f);
            Scatter(r, Rocks, 31f, 12.4f, 39.4f, 23f, 10, 0f, 0.1f, 0.3f);
            Lamp(r, "Fill", r.P(35f, 2.0f, 18f), Cold, 0.7f, 9f);
            Lamp(r, "Fill", r.P(35f, 2.0f, 13.5f), Cold, 0.5f, 6f);
            Neglect(r, 6, 4);
        }

        static void Niche(Room r, char side, float z0, float zc, int i)
        {
            var S = s_Surf["brick_dark"];
            float d = 0.9f;
            float xw = side == 'W' ? r.x0 : r.x1;
            float xi = side == 'W' ? r.x0 + d : r.x1 - d;
            float xa = Mathf.Min(xw, xi), xb = Mathf.Max(xw, xi);
            Box(r.props, "Pier", new Vector3(xa, 0f, z0), new Vector3(xb, r.h, z0 + 0.2f), S, r.RL);
            if (i == 4) Box(r.props, "Pier", new Vector3(xa, 0f, z0 + 2.25f), new Vector3(xb, r.h, z0 + 2.45f), S, r.RL);
            bool collapsed = (i == 1 && side == 'E') || (i == 3 && side == 'W');
            foreach (var y in new[] { 0.0f, 0.8f, 1.6f })
            {
                Box(r.props, "Shelf", new Vector3(xa, y, z0 + 0.2f), new Vector3(xb, y + 0.12f, z0 + 2.25f), s_Surf["stone_trim"], r.RL);
                if (collapsed && y > 0.5f) continue;
                float xc = (xa + xb) * 0.5f;
                int roll = (i * 3 + (int)(y * 10) + (side == 'W' ? 1 : 0)) % 4;
                if (roll == 0) Put(r, "SM_Prop_Coffin_02", xc, zc, 0f, y + 0.12f, 0.82f, null, null, null, false);
                else if (roll == 1)
                {
                    var c = Put(r, "SM_Prop_Coffin_01", xc + (side == 'W' ? 0.25f : -0.25f), zc + 0.2f, side == 'W' ? 18f : -18f, y + 0.12f, 0.82f, null, null, null, false);
                    PutOn(r, "SM_Gen_Prop_Skull_01", xc, zc - 0.5f, y + 0.12f, Rf(0, 360), 0.45f);
                }
                else if (roll == 2) { PutOn(r, "SM_Gen_Prop_Skull_01", xc, zc + 0.3f, y + 0.12f, Rf(0, 360), 0.45f); PutOn(r, "SM_Gen_Prop_Skull_01", xc + 0.1f, zc - 0.4f, y + 0.12f, Rf(0, 360), 0.42f); }
                else PutOn(r, "SM_Prop_Urn_01", xc, zc, y + 0.12f, 0f, 0.6f);
            }
            if (collapsed)
            {
                float xc = (xa + xb) * 0.5f;
                for (int k = 0; k < 6; k++) Put(r, Pick(Rocks), xc + Rf(-0.4f, 0.4f), zc + Rf(-0.9f, 0.9f), Rf(0, 360), 0.1f + Rf(0f, 0.6f), Rf(0.15f, 0.3f), null, null, null, false);
                for (int k = 0; k < 4; k++) Put(r, Pick(Rocks), xc + (side == 'W' ? 1f : -1f) * Rf(0.6f, 1.4f), zc + Rf(-1f, 1f), Rf(0, 360), 0f, Rf(0.12f, 0.25f), null, null, null, false);
            }
        }
    }
}
