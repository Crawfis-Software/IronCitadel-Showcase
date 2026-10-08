using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static partial class CitadelBuilder
    {
        public const float Deck = 5f;           // gallery floor height
        static readonly string[] Feast = { "G:SM_Gen_Prop_Plate_01", "G:SM_Gen_Prop_Plate_01", "R:SM_Prop_Goblet_01", "R:SM_Prop_Goblet_02", "R:SM_Prop_Tankard_01",
            "R:SM_Prop_Mug_01", "G:SM_Gen_Prop_Food_Bread_01", "G:SM_Gen_Prop_Food_Meat_01", "G:SM_Gen_Prop_Food_Meat_05", "G:SM_Gen_Prop_Food_Vegetable_02",
            "G:SM_Gen_Prop_Food_Vegetable_05", "G:SM_Gen_Prop_Bottle_01", "G:SM_Gen_Prop_Bottle_03", "R:SM_Prop_Camp_Pot_04" };

        static readonly string[] Soldiers = { "R:Chr_BR_Dwarf_Soldier_Male_01", "R:Chr_BR_Dwarf_Soldier_Female_01" };

        // ------------------------------------------------------------------ great hall [1,2]
        static void BuildHall(Room rm)
        {
            float H = rm.H;
            Arch_.Floor(rm, Full);
            Arch_.Box4(rm, Full, H);
            Arch_.Ceiling(rm, Full, H);
            rm.Open.Add(Rect.MinMaxRect(7.5f, 2.5f, 37.5f, 42.5f));
            rm.Open.Add(Rect.MinMaxRect(2.5f, 2.5f, 7.5f, 28.5f));
            rm.Open.Add(Rect.MinMaxRect(37.5f, 2.5f, 42.5f, 28.5f));

            foreach (int side in new[] { -1, 1 })   // -1 west, +1 east
            {
                float Mx(float u) => side < 0 ? u : 45f - u;   // mirror
                Vector2 into = side < 0 ? Vector2.right : Vector2.left;   // from the strip toward the hall
                // the stair tower wall between strip and hall: shaft + landing (with the arch), full height
                var arch = new Opening(26f - 8.5f, 3f, 3.6f);
                Arch_.InnerWall(rm, V(Mx(7.5f), 8.5f), V(Mx(7.5f), 28.5f), 0.6f, 0f, H, null, new[] { arch });
                // walkway's end wall under the platform
                Arch_.InnerWall(rm, V(Mx(7.5f), 2.5f), V(Mx(7.5f), 8.5f), 0.6f, 0f, Deck - 0.6f);
                // closed strip north of the landing: hall-facing wall, landing's north wall, solid fill
                Arch_.WallRun(rm, V(Mx(7.8f), 28.5f), V(Mx(7.8f), 42.5f), into, 0f, H);
                Arch_.WallRun(rm, V(Mx(2.5f), 28.5f), V(Mx(7.2f), 28.5f), Vector2.down, 0f, H);
                Kit.SolidMinMax(rm.Cols, rm.W(Mx(2.5f), 28.5f, 0f), rm.W(Mx(7.5f), 42.5f, H), "ClosedStrip");
                // under-stair void: solid
                Kit.SolidMinMax(rm.Cols, rm.W(Mx(2.5f), 2.5f, 0f), rm.W(Mx(7.2f), 8.5f, Deck - 0.6f), "UnderStair");
                // the arch's frame on the hall side and the landing side
                Kit.StripColliders(Kit.P("D:SM_Env_Door_Frame_Round_02", rm.Arch, rm.W(Mx(7.85f), 26f), Quaternion.LookRotation(new Vector3(into.x, 0, 0)), Vector3.one * 1.15f));
                Kit.StripColliders(Kit.P("D:SM_Env_Door_Frame_Round_02", rm.Arch, rm.W(Mx(7.15f), 26f), Quaternion.LookRotation(new Vector3(-into.x, 0, 0)), Vector3.one * 1.15f));

                // the stair: two flights rising south from the landing (v 18.5, y 0) to the gallery platform (v 8.5, y 5)
                float u0 = side < 0 ? 2.5f : 37.8f, u1 = side < 0 ? 7.2f : 42.5f;
                var f1 = Kit.Fit("R:SM_Env_Dwarf_Stairs_01", rm.Arch, rm.W(u0, 13.5f, -0.23f), new Vector3(u1 - u0, 2.73f, 5f), 0f);
                var f2 = Kit.Fit("R:SM_Env_Dwarf_Stairs_01", rm.Arch, rm.W(u0, 8.5f, 2.27f), new Vector3(u1 - u0, 2.73f, 5f), 0f);
                Kit.StripColliders(f1); Kit.StripColliders(f2);
                float uc = (u0 + u1) / 2f;
                Kit.Ramp(rm.Cols, rm.W(uc, 18.6f, 0f), rm.W(uc, 8.5f, Deck), u1 - u0, "StairRamp");
                // landing lights and a torch in the shaft
                Arch_.Torch(rm, rm.W(Mx(2.5f), 25.8f), new Vector3(side < 0 ? 1 : -1, 0, 0), 2.8f, 7f, 9f, "R:SM_Prop_Dwarf_Torch_01");
                Arch_.Torch(rm, rm.W(Mx(2.5f), 12f), new Vector3(side < 0 ? 1 : -1, 0, 0), 4.6f, 6f, 8f, "R:SM_Prop_Dwarf_Torch_01");
                Arch_.Torch(rm, rm.W(Mx(2.5f), 5.5f), new Vector3(side < 0 ? 1 : -1, 0, 0), Deck + 2.6f, 7f, 9f, "R:SM_Prop_Dwarf_Torch_01");
                Banner(rm, V(Mx(5f), 28.5f), Vector2.down, 6f, "R:SM_Prop_Banner_06", 1f);
                // fireplace in the hall's side wall, north part
                var fp = Kit.PW("R:SM_Env_Dwarf_Wall_Fireplace_01", rm.Props, rm.W(Mx(7.8f), 35.5f), new Vector3(into.x, 0, 0), 0.85f, 0f);
                Kit.StripColliders(fp);
                var fireAt = rm.W(Mx(8.6f), 35.5f, 0.35f);
                Kit.P("R:FX_Fire_Large_01", rm.Props, fireAt, 0f, 0.8f);
                Kit.PointLight(rm.Lights, fireAt + new Vector3(into.x, 0, 0) * 1.2f + Vector3.up * 1.2f, Kit.Fire, 16f, 13f, "Hearth");
                Kit.Solid(rm.Cols, rm.W(Mx(8.6f), 35.5f, 1.5f), new Vector3(2.0f, 3f, 4.6f), "Fireplace");
            }

            // gallery deck over the walkway, full width at 5 m; its front edge is the arcade's top (v 8.9)
            const float edge = 8.9f;
            // over the stair strips the deck stops where the stairs arrive (v 8.5), so the top step is flush
            Arch_.Floor(rm, Rect.MinMaxRect(7.5f, 2.5f, 37.5f, edge), Deck);
            Arch_.Floor(rm, Rect.MinMaxRect(2.5f, 2.5f, 7.5f, 8.5f), Deck);
            Arch_.Floor(rm, Rect.MinMaxRect(37.5f, 2.5f, 42.5f, 8.5f), Deck);
            Arch_.Ceiling(rm, Rect.MinMaxRect(7.5f, 2.5f, 37.5f, 8.1f), Deck - 0.6f);
            // the arcade under the gallery's front edge: one row of arches centred on v 8.5
            Arch_.Pieces(rm, rm.Arch, V(7.5f, 8.5f + Style.Dwarf.Depth), V(37.5f, 8.5f + Style.Dwarf.Depth), Vector2.up, 0f, Deck, Style.Dwarf, "R:SM_Env_Dwarf_Wall_Archway_01");
            for (int i = 0; i <= 6; i++)
            {
                float u = 7.5f + i * 5f;
                float w = (i == 0 || i == 6) ? 1.0f : 1.6f;
                Kit.Solid(rm.Cols, rm.W(u, 8.5f, Deck / 2f), new Vector3(w, Deck, 0.78f), "ArcadePier");
            }
            Kit.SolidMinMax(rm.Cols, rm.W(7.5f, 8.11f, 3.7f), rm.W(37.5f, 8.89f, Deck), "ArcadeLintel");
            // the arcade stands on a plinth: a landing at 0.25 m, then two steps down to the hall floor at v 11
            Arch_.Floor(rm, Rect.MinMaxRect(7.5f, 8.5f, 37.5f, 10f), 0.25f, null, null, null, false, false);
            for (int i = 0; i < 6; i++)
                Kit.StripColliders(Kit.Fit("R:SM_Env_Dwarf_Stairs_Small_01", rm.Arch, rm.W(7.5f + i * 5f, 10f, 0f), new Vector3(5f, 0.25f, 1f), 0f));
            Kit.SolidMinMax(rm.Cols, rm.W(7.5f, 8.5f, -0.5f), rm.W(37.5f, 10f, 0.25f), "Plinth");
            Kit.Ramp(rm.Cols, rm.W(22.5f, 11.05f, 0f), rm.W(22.5f, 9.95f, 0.25f), 30f, "PlinthSteps");
            Kit.Ramp(rm.Cols, rm.W(22.5f, 7.6f, 0f), rm.W(22.5f, 8.55f, 0.25f), 30f, "ArcadeThreshold");
            // balustrade at the very front of the gallery, an invisible guard above it
            for (int i = 0; i < 6; i++)
            {
                var b = Kit.P("D:SM_Env_Railing_04", rm.Arch, rm.W(12.5f + i * 5f, edge - 0.15f, Deck), 0f, 1f);
                b.name = "GalleryRailing";
                Kit.StripColliders(b);
            }
            Kit.SolidMinMax(rm.Cols, rm.W(7.5f, edge - 0.3f, Deck), rm.W(37.5f, edge + 0.1f, Deck + 2.4f), "GalleryRail").layer = 2; // invisible guard: Ignore Raycast
            foreach (float u in new[] { 7.5f, 37.5f })
                Kit.StripColliders(Prop(rm, "R:SM_Env_Dwarf_Stairs_Pillar_01", u, edge - 0.15f, 0f, 1f, Deck));
            // minstrels on the gallery: drums, stools, a music stand
            Prop(rm, "D:SM_Prop_Goblin_Drum_01", 14f, 5f, 20f, 1f, Deck);
            Prop(rm, "D:SM_Prop_Goblin_Drum_01", 31f, 4.6f, -30f, 0.8f, Deck);
            Prop(rm, "M:SM_Prop_Book_Stand_01", 22.5f, 6.5f, 180f, 1f, Deck);
            foreach (float u in new[] { 12f, 17f, 20f, 25f, 28f, 33f }) Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", u, 4.5f + Kit.R(0, 1.5f), Kit.R(0, 360), 1f, Deck);
            for (int i = 0; i < 5; i++) Arch_.Torch(rm, rm.W(7.5f + 3.75f + i * 7.5f, 2.5f), Vector3.forward, Deck + 2.6f, 7f, 9f, "R:SM_Prop_Dwarf_Torch_01");
            // the walkway beneath: service stores and torches either side of the armory door
            foreach (float u in new[] { 13f, 32f }) Arch_.Torch(rm, rm.W(u, 2.5f), Vector3.forward, 2.6f, 6f, 8f, "R:SM_Prop_Dwarf_Torch_01");
            Prop(rm, "R:SM_Prop_Barrel_Stack_01", 10.5f, 3.6f, 0f);
            Prop(rm, "R:SM_Prop_Barrel_01", 35.5f, 3.4f); Prop(rm, "R:SM_Prop_Barrel_02", 34.6f, 3.5f, 30f); Prop(rm, "R:SM_Prop_Crate_01", 36.5f, 4.4f, 10f);

            // the feast: four long tables with benches, a high table at the north end
            foreach (float u in new[] { 12.5f, 17f, 28f, 32.5f })
            {
                for (int k = 0; k < 7; k++)
                {
                    float v = 13.2f + k * 3.32f;
                    var t = Prop(rm, "R:SM_Prop_Dwarf_Table_03", u, v, 90f);
                    Furnish.TableTop(rm, t, Feast, 9, 90f);
                    if (k % 2 == 1) Furnish.Candles(rm, rm.W(u + Kit.R(-0.2f, 0.2f), v, 0.91f), 1.2f, 4f);
                    foreach (float s in new[] { -1.05f, 1.05f })
                        for (int b = 0; b < 2; b++)
                            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", u + s, v - 0.82f + b * 1.64f, 90f);
                }
            }
            foreach (float u0 in new[] { 9.5f, 26f })
                for (int k = 0; k < 3; k++)
                {
                    var t = Prop(rm, "R:SM_Prop_Dwarf_Table_03", u0 + 1.66f + k * 3.32f, 39.2f, 0f);
                    Furnish.TableTop(rm, t, Feast, 9, 0f);
                    Prop(rm, "R:SM_Prop_Chair_01", u0 + 0.8f + k * 3.32f, 40.4f, 180f);
                    Prop(rm, "R:SM_Prop_Chair_01", u0 + 2.5f + k * 3.32f, 40.4f, 180f);
                }
            // runner down the aisle from the arcade to the throne doors
            for (int k = 0; k < 3; k++)
                Kit.StripColliders(Kit.Fit("D:SM_Prop_Rug_01", rm.Props, rm.W(21.1f, 9.5f + k * 10.6f, 0.0f), new Vector3(2.8f, 0.06f, 10.4f), 90f));
            // chandeliers high over the tables (clear of the gallery's sightlines)
            foreach (float u in new[] { 14.75f, 30.25f })
                foreach (float v in new[] { 15f, 24.5f, 34f })
                    Furnish.Chandelier(rm, rm.W(u, v, H), 30f, 20f, "D:SM_Prop_Candle_Chandelier_02_Preset", 1.5f);
            Fill(rm, Rect.MinMaxRect(7.5f, 8.5f, 37.5f, 42.5f), 10.5f, 9f, 17f, 10f);
            Fill(rm, Rect.MinMaxRect(7.5f, 2.5f, 37.5f, 8.5f), 3.6f, 2.5f, 8f, 10f);
            // banners on the walls
            foreach (float v in new[] { 14f, 21f, 31f, 39f })
            {
                Banner(rm, V(7.8f, v), Vector2.right, H - 0.5f, Kit.Pick("R:SM_Prop_Banner_01", "R:SM_Prop_Banner_03"), 1f);
                Banner(rm, V(37.2f, v), Vector2.left, H - 0.5f, Kit.Pick("R:SM_Prop_Banner_01", "R:SM_Prop_Banner_03"), 1f);
            }
            foreach (float u in new[] { 13f, 32f }) Banner(rm, V(u, 42.5f), Vector2.down, H - 0.5f, "R:SM_Prop_Banner_01", 1f);
            // torches on the hall walls, low and high
            foreach (float v in new[] { 11f, 17.5f, 30f })
            {
                Arch_.Torch(rm, rm.W(7.8f, v), Vector3.right, 3.2f, 7f, 10f, "R:SM_Prop_Dwarf_Torch_01");
                Arch_.Torch(rm, rm.W(37.2f, v), Vector3.left, 3.2f, 7f, 10f, "R:SM_Prop_Dwarf_Torch_01");
            }
            foreach (float u in new[] { 9.5f, 18.5f, 26.5f, 35.5f }) Arch_.Torch(rm, rm.W(u, 42.5f), Vector3.back, 3.2f, 7f, 10f, "R:SM_Prop_Dwarf_Torch_01");
            foreach (float v in new[] { 15f, 25f, 36f })
            {
                Kit.PointLight(rm.Lights, rm.W(9f, v, 9f), Kit.Candle, 10f, 14f, "HighLight");
                Kit.PointLight(rm.Lights, rm.W(36f, v, 9f), Kit.Candle, 10f, 14f, "HighLight");
            }

            // the feasters: the level's largest group, at the tables
            var feasters = new[] { "R:Chr_BR_Dwarf_Soldier_Male_01", "R:Chr_BR_Dwarf_Casual_Male_01", "R:Chr_BR_Dwarf_Soldier_Female_01", "D:SM_Chr_Goblin_Warrior_Male_01",
                "R:Chr_BR_Dwarf_Casual_Female_01", "R:Chr_BR_Dwarf_Miner_01", "R:Chr_BR_Dwarf_Soldier_Male_01", "D:SM_Chr_Goblin_Male_01" };
            var seats = new[] { (12.5f, 15f, -1), (12.5f, 25f, 1), (17f, 18.2f, -1), (17f, 31.5f, 1), (28f, 21.6f, -1), (28f, 28.2f, 1), (32.5f, 16.6f, 1), (32.5f, 33f, -1) };
            for (int i = 0; i < seats.Length; i++)
            {
                var (u, v, s) = seats[i];
                float bu = u + s * 1.05f;
                Furnish.Defender(rm, feasters[i], rm.W(bu + s * 0.12f, v, 0f), s < 0 ? 90f : -90f, "Feaster", true);
            }
        }

        // ------------------------------------------------------------------ armory [2,2]
        static void BuildArmory(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            // the way in from the walkway turns at a screen hung with shields
            Arch_.InnerWall(rm, V(16.5f, 37.5f), V(28.5f, 37.5f), 0.8f, 0f, 4.2f, null, null, true);
            for (int i = 0; i < 4; i++)
            {
                var sh = Kit.PW(Kit.Pick("R:SM_Wep_Shield_01", "R:SM_Wep_Shield_03", "R:SM_Wep_Shield_05", "R:SM_Wep_Shield_07"), rm.Props, rm.W(18.5f + i * 2.7f, 37.1f, 2.0f), Vector3.back, 1f, 0.05f);
                Kit.StripColliders(sh);
            }
            // dense rows of racks and armour stands, aisles between, a central aisle to the gate
            var rackKeys = new[] { "R:SM_Prop_Dwarf_Weapon_Rack_04", "R:SM_Prop_Dwarf_Weapon_Rack_03" };
            foreach (float v in new[] { 11f, 16.5f, 22f, 27.5f, 33f })
                foreach (var (ua, ub) in new[] { (4.5f, 19f), (26f, 40.5f) })
                {
                    float u = ua;
                    int k = 0;
                    while (u < ub - 0.8f)
                    {
                        if (k % 3 == 2)
                        {
                            Prop(rm, "M:SM_Prop_KnightStand_01", u + 0.4f, v - 0.45f, 180f);
                            Prop(rm, "M:SM_Prop_KnightStand_01", u + 0.4f, v + 0.45f, 0f);
                            u += 1.0f;
                        }
                        else
                        {
                            Prop(rm, rackKeys[k % 2], u + 1.0f, v, 90f);
                            u += 2.05f;
                        }
                        k++;
                    }
                    Prop(rm, Kit.Pick("R:SM_Prop_Dwarf_Weapon_Barrel_01", "R:SM_Prop_Dwarf_Weapon_Barrel_02"), ub + 0.3f, v, Kit.R(0, 360));
                }
            // armour stands line the central aisle, facing it
            foreach (float v in new[] { 13.75f, 19.25f, 24.75f, 30.25f })
            {
                Prop(rm, "M:SM_Prop_KnightStand_01", 20.3f, v, 90f);
                Prop(rm, "M:SM_Prop_KnightStand_01", 24.7f, v, -90f);
            }
            // racks of polearms along the side walls
            foreach (float v in new[] { 8f, 14f, 20f, 26f, 32f, 38f })
            {
                WallProp(rm, "D:SM_Prop_WeaponRack_01", V(2.5f, v), Vector2.right);
                WeaponsOnRack(rm, rm.W(2.85f, v), Vector3.right);
                WallProp(rm, "D:SM_Prop_WeaponRack_01", V(42.5f, v), Vector2.left);
                WeaponsOnRack(rm, rm.W(42.15f, v), Vector3.left);
            }
            foreach (float u in new[] { 6f, 10f, 14f, 31f, 35f, 39f })
            {
                var hw = Kit.PW(Kit.Pick("R:SM_Prop_Hanging_Weapon_01", "R:SM_Prop_Hanging_Weapon_02", "R:SM_Prop_Hanging_Weapon_03"), rm.Props, rm.W(u, 42.5f, 2.6f), Vector3.back, 1.4f, 0.02f);
                Kit.StripColliders(hw);
            }
            // by the gate: the archers' station
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_02", 17f, 5f, 0f);
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_01", 28f, 5.2f, 0f);
            Furnish.Brazier(rm, rm.W(14.5f, 6.5f), 8f, 10f, "D:SM_Prop_Brazier_01");
            Furnish.Brazier(rm, rm.W(30.5f, 6.5f), 8f, 10f, "D:SM_Prop_Brazier_01");
            foreach (var (u, v) in new[] { (19.5f, 7.5f), (22.5f, 7.0f), (25.5f, 7.5f) })
                Furnish.Defender(rm, Soldiers[(int)u % 2], rm.W(u, v), 180f, "Archer");
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Soldier_Male_01", rm.W(10.5f, 19.25f), 80f, "RackGuard");
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Soldier_Female_01", rm.W(34f, 30.25f), -100f, "RackGuard");
            // the best gear: a royal harness on a plinth in the north-east corner, away from the archers
            Kit.StripColliders(Prop(rm, "R:SM_Env_Dwarf_Pillar_Base_02", 38.5f, 39f, 0f, 1f));
            var stand = Prop(rm, "M:SM_Prop_KnightStand_Royal_01", 38.5f, 39f, 225f, 1.05f, 0.5f);
            var sword = Kit.PCR("D:SM_Wep_Crystal_Ornate_Straightsword_01", rm.Props, rm.W(37.2f, 40.2f, 1.6f), Quaternion.Euler(0, 225f, 12f), 1f);
            Kit.StripColliders(sword);
            var shield = Kit.PCR("D:SM_Wep_Shield_Ornate_01", rm.Props, rm.W(39.8f, 37.9f, 1.3f), Quaternion.Euler(-10f, 225f, 0), 1f);
            Kit.StripColliders(shield);
            Furnish.Pickup(rm, rm.W(38.5f, 39f, 0.5f), "the armory's best gear", 1.4f);
            // light
            WallTorches(rm, Full, 7.5f, 3.2f, 7f, 10f);
            foreach (float v in new[] { 13.75f, 24.75f, 35f })
                Furnish.Chandelier(rm, rm.W(22.5f, v, H), 12f, 13f);
            Fill(rm, Full, 5.2f, 4f, 13f, 10f);
        }

        // ------------------------------------------------------------------ throne room [0,2]
        static void BuildThrone(Room rm)
        {
            float H = rm.H;
            Shell(rm, Full);
            // the antechamber screen: from the doors the way turns left or right, and the throne is hidden
            Arch_.InnerWall(rm, V(13.5f, 9.5f), V(31.5f, 9.5f), 1.0f, 0f, 8f, null, null, true);
            Kit.StripColliders(Kit.Fit("R:SM_Env_Dwarf_Wall_Trim_01", rm.Arch, rm.W(13.5f, 9.0f, 7.6f), new Vector3(18f, 0.8f, 1.0f), 0f));
            foreach (float u in new[] { 16.5f, 22.5f, 28.5f }) Banner(rm, V(u, 10.0f), Vector2.up, 7.6f, "R:SM_Prop_Banner_02", 1f);
            foreach (float u in new[] { 18f, 27f }) Banner(rm, V(u, 9.0f), Vector2.down, 7.4f, "R:SM_Prop_Banner_04", 1f);
            Arch_.Torch(rm, rm.W(15f, 9f), Vector3.back, 3f, 8f, 10f, "R:SM_Prop_Dwarf_Torch_01");
            Arch_.Torch(rm, rm.W(30f, 9f), Vector3.back, 3f, 8f, 10f, "R:SM_Prop_Dwarf_Torch_01");

            // runner from the screen to the steps
            for (int k = 0; k < 2; k++)
                Kit.StripColliders(Kit.Fit("D:SM_Prop_Rug_01", rm.Props, rm.W(21.05f, 10.6f + k * 10.3f, 0f), new Vector3(2.9f, 0.06f, 10.3f), 90f));

            // the stage: 1.25 m up, steps across its whole front, no rail
            const float rise = 1.25f, v0 = 31.25f, v1 = 35f;
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 8; i++)
                {
                    var st = Kit.Fit("R:SM_Env_Dwarf_Stairs_Small_01", rm.Arch, rm.W(2.5f + i * 5f, v0 + row * 1.25f, row * rise / 3f), new Vector3(5f, rise / 3f, 1.25f), 180f);
                    Kit.StripColliders(st);
                }
            Arch_.Floor(rm, Rect.MinMaxRect(2.5f, v1, 42.5f, 42.5f), rise);
            Kit.SolidMinMax(rm.Cols, rm.W(2.5f, v1, 0f), rm.W(42.5f, 42.5f, rise), "Stage");
            Kit.Ramp(rm.Cols, rm.W(22.5f, v0, 0f), rm.W(22.5f, v1, rise), 40f, "StageSteps");

            // the throne, facing the doors down the runner
            var throne = Prop(rm, "R:SM_Prop_Dwarf_Throne_01", 22.5f, 40.6f, 180f, 1f, rise);
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_King_01", rm.W(22.5f, 37.9f, rise), 180f, "Warlord");
            foreach (var u in new[] { 14.5f, 18.75f, 26.25f, 30.5f })
                Furnish.Defender(rm, Soldiers[(int)u % 2], rm.W(u, 29.2f), 180f, "ThroneGuard");
            // braziers and statues on the stage, banners behind
            Furnish.Brazier(rm, rm.W(15.5f, 37.5f, rise), 14f, 14f, "R:SM_Prop_Dwarf_Brazier_01", 0.8f);
            Furnish.Brazier(rm, rm.W(29.5f, 37.5f, rise), 14f, 14f, "R:SM_Prop_Dwarf_Brazier_01", 0.8f);
            WallProp(rm, "R:SM_Env_Statue_03", V(8f, 42.5f), Vector2.down, 1.3f, rise);
            WallProp(rm, "R:SM_Env_Statue_03", V(37f, 42.5f), Vector2.down, 1.3f, rise);
            foreach (float u in new[] { 13f, 18f, 27f, 32f }) Banner(rm, V(u, 42.5f), Vector2.down, H - 0.6f, "R:SM_Prop_Banner_01", 1.1f);
            // tall pillars down both sides, banners between them
            foreach (float u in new[] { 8f, 37f })
                foreach (float v in new[] { 13f, 20.5f, 28f })
                {
                    Pillar(rm, u, v, H, "R:SM_Env_Dwarf_Pillar_05", true, 1.0f);
                    Kit.StripColliders(Prop(rm, "R:SM_Env_Dwarf_Pillar_Base_01", u, v, 0f, 0.8f));
                    Arch_.Torch(rm, rm.W(u, v + (u < 20 ? 0f : 0f)) + new Vector3(u < 20 ? 0.75f : -0.75f, 0, 0), new Vector3(u < 20 ? 1 : -1, 0, 0), 3.4f, 9f, 11f, "R:SM_Prop_Dwarf_Torch_01");
                }
            foreach (float v in new[] { 16.75f, 24.25f })
            {
                Banner(rm, V(2.5f, v), Vector2.right, H - 1f, "R:SM_Prop_Banner_03", 1.1f);
                Banner(rm, V(42.5f, v), Vector2.left, H - 1f, "R:SM_Prop_Banner_03", 1.1f);
            }
            // bright: high lights flood the room, warm light on the throne
            foreach (float u in new[] { 12f, 33f })
                foreach (float v in new[] { 14f, 28f })
                    Kit.PointLight(rm.Lights, rm.W(u, v, 11f), Kit.Candle, 30f, 24f, "HighLight");
            Fill(rm, Full, 12.5f, 16f, 19f, 10f, new Color(1f, 0.85f, 0.65f));
            Kit.PointLight(rm.Lights, rm.W(22.5f, 34f, 6f), new Color(1f, 0.8f, 0.55f), 26f, 16f, "ThroneLight");
            Kit.PointLight(rm.Lights, rm.W(22.5f, 37f, 2.6f), new Color(1f, 0.75f, 0.5f), 3f, 6f, "ThroneFace");
            Kit.PointLight(rm.Lights, rm.W(22.5f, 6f, 6f), Kit.Candle, 12f, 12f, "AnteLight");
            WallTorches(rm, Full, 9f, 3.4f, 8f, 10f, null, new[] { V(2.5f, 16.75f), V(2.5f, 24.25f), V(42.5f, 16.75f), V(42.5f, 24.25f) });
        }
    }
}
