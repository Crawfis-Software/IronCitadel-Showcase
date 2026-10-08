using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static partial class CitadelBuilder
    {
        static readonly string[] TallBookcases = { "M:SM_Prop_Bookcase_Grand_01", "M:SM_Prop_Bookcase_Grand_02", "M:SM_Prop_Bookcase_Grand_03", "M:SM_Prop_Bookcase_01", "M:SM_Prop_Bookcase_02", "M:SM_Prop_Bookcase_03" };

        /// <summary>A stack: two rows of tall bookcases back to back along u at 'v', from u0 to u1.</summary>
        static void Stack(Room rm, float v, float u0, float u1, bool cap = true)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt((u1 - u0) / 2.5f));
            for (int i = 0; i < n; i++)
            {
                float u = u0 + 1.25f + i * 2.5f;
                WallProp(rm, TallBookcases[Kit.RI(0, TallBookcases.Length)], V(u, v + 0.02f), Vector2.up, 1f, 0f, 0f);
                WallProp(rm, TallBookcases[Kit.RI(0, TallBookcases.Length)], V(u, v - 0.02f), Vector2.down, 1f, 0f, 0f);
            }
            Kit.SolidMinMax(rm.Cols, rm.W(u0, v - 1.1f, 0f), rm.W(u0 + n * 2.5f, v + 1.1f, 5f), "Stack");
            if (Kit.Chance(0.5f))
            {
                float u = u0 + 1.25f + Kit.RI(0, n) * 2.5f;
                int s = Kit.Chance(0.5f) ? 1 : -1;
                Kit.StripColliders(Kit.PCR("M:SM_Prop_Ladder_01", rm.Props, rm.W(u, v + s * 1.55f, 2.45f), Quaternion.Euler(s * 12f, s > 0 ? 180f : 0f, 0), 1f));
            }
        }

        // ------------------------------------------------------------------ library [3,3]: tall bookcases packed close, open, in use
        static void BuildLibrary(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            // the west door meets a screen of bookcases: turn left or right
            for (int k = 0; k < 2; k++)
            {
                float v = 20f + k * 2.5f + 1.25f;
                WallProp(rm, TallBookcases[Kit.RI(0, 3)], V(7.0f, v), Vector2.left, 1f, 0f, 0f);
                WallProp(rm, TallBookcases[Kit.RI(0, 3)], V(7.0f, v), Vector2.right, 1f, 0f, 0f);
            }
            Kit.SolidMinMax(rm.Cols, rm.W(5.9f, 20f, 0f), rm.W(8.1f, 25f, 5f), "Screen");
            // south stacks and north stacks, close-packed; a reading room between them
            foreach (float v in new[] { 6.3f, 10.9f, 15.5f }) { Stack(rm, v, 11.5f, 24f); Stack(rm, v, 28f, 40.5f); }
            foreach (float v in new[] { 29.5f, 34.1f }) { Stack(rm, v, 11.5f, 19f); Stack(rm, v, 26f, 40.5f); }
            Stack(rm, 38.7f, 26f, 40.5f);
            Stack(rm, 38.7f, 11.5f, 19f);
            // wall cases on the east wall
            for (float v = 4f; v < 41.5f; v += 2.5f) WallProp(rm, TallBookcases[Kit.RI(0, TallBookcases.Length)], V(42.5f, v + 1.25f), Vector2.left, 1f, 0f, 0.02f);
            // the reading room: tables in use, books open, candles lit
            var tableItems = new[] { "M:SM_Prop_Book_Open_01", "M:SM_Prop_Book_Open_02", "M:SM_Prop_Book_Pile_01", "M:SM_Prop_Papers_02", "M:SM_Prop_Papers_07", "D:SM_Item_Inkwell_01", "M:SM_Prop_Book_01", "M:SM_Prop_Book_03" };
            foreach (var (u, v) in new[] { (14f, 21f), (14f, 24.5f), (21f, 21f), (21f, 24.5f), (31f, 21f), (31f, 24.5f), (37.5f, 22.75f) })
            {
                var t = Prop(rm, "R:SM_Prop_Dwarf_Table_02", u, v, 0f);
                Furnish.TableTop(rm, t, tableItems, 5, 0f);
                Furnish.Candles(rm, t.transform.position + Vector3.up * 0.91f + Vector3.right * 0.5f, 1.6f, 4.5f);
                Prop(rm, "R:SM_Prop_Chair_01", u - 0.5f, v - 1.0f, Kit.R(-20, 20));
                Prop(rm, "R:SM_Prop_Chair_01", u + 0.5f, v + 1.0f, 180f + Kit.R(-20, 20));
            }
            Prop(rm, "M:SM_Prop_Globe_01", 26f, 19.2f, 30f);
            Prop(rm, "M:SM_Prop_Book_Stand_02", 26f, 26.5f, 200f);
            Prop(rm, "M:SM_Prop_Book_Stand_01", 10f, 27f, 120f);
            foreach (var (u, v) in new[] { (11f, 19f), (24.5f, 27.2f), (40f, 19.3f), (33.8f, 27f) })
                Kit.StripColliders(Prop(rm, Kit.Pick("M:SM_Prop_Book_Pile_02", "M:SM_Prop_Book_Pile_03"), u, v, Kit.R(0, 360)));
            Prop(rm, "M:SM_Prop_Ladder_02", 41.6f, 33f, -90f);
            // a librarian's desk by the north door
            var desk = Prop(rm, "R:SM_Prop_Dwarf_Table_03", 22.5f, 31.5f, 0f);
            Furnish.TableTop(rm, desk, tableItems, 7, 0f);
            Furnish.Candles(rm, desk.transform.position + Vector3.up * 0.91f, 1.8f, 5f);
            Prop(rm, "R:SM_Prop_Chair_01", 22.5f, 32.8f, 180f);
            // moderate light: chandeliers over the reading room, candles, a few sconces
            foreach (float u in new[] { 15f, 25f, 34f }) Furnish.Chandelier(rm, rm.W(u, 22.75f, H), 12f, 13f);
            Fill(rm, Full, 5.4f, 3f, 11f, 10f);
            Arch_.Torch(rm, rm.W(2.5f, 10f), Vector3.right, 3.2f, 6f, 9f, "R:SM_Prop_Dwarf_Torch_01");
            Arch_.Torch(rm, rm.W(2.5f, 35f), Vector3.right, 3.2f, 6f, 9f, "R:SM_Prop_Dwarf_Torch_01");
            foreach (float u in new[] { 9.5f, 35.5f }) Arch_.Torch(rm, rm.W(u, 42.5f), Vector3.back, 3.2f, 6f, 9f, "R:SM_Prop_Dwarf_Torch_01");
            foreach (float u in new[] { 9.5f, 26f }) Arch_.Torch(rm, rm.W(u, 2.5f), Vector3.forward, 3.2f, 6f, 9f, "R:SM_Prop_Dwarf_Torch_01");
            foreach (var (u, v) in new[] { (17.75f, 8.6f), (34.25f, 8.6f), (17.75f, 13.2f), (34.25f, 13.2f), (15f, 31.8f), (33f, 31.8f), (15f, 36.4f), (33f, 36.4f) })
                Kit.PointLight(rm.Lights, rm.W(u, v, 3.6f), Kit.Candle, 3.5f, 7f, "AisleLamp");
            foreach (var (u, v) in new[] { (26f, 8.6f), (26f, 13.2f), (22.5f, 36f) })
                Prop(rm, "D:SM_Prop_Candle_Stand_01_Preset", u, v, 0f);
        }

        // ------------------------------------------------------------------ alchemist's study [2,3]: glassware, jars, a brew going
        static void BuildStudy(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            Arch_.InnerWall(rm, V(16.5f, 7.5f), V(28.5f, 7.5f), 0.6f, 0f, 3.8f, null, null, true);
            Arch_.InnerWall(rm, V(16.5f, 37.5f), V(28.5f, 37.5f), 0.6f, 0f, 3.8f, null, null, true);
            // shelves of jars against the screens and the walls
            var jars = new[] { "D:SM_Item_Jar_01", "D:SM_Item_Jar_02", "D:SM_Item_Potion_01", "D:SM_Item_Potion_03", "D:SM_Item_Potion_05", "D:SM_Item_Potion_07", "D:SM_Item_Vial_01", "G:SM_Gen_Prop_Bottle_04" };
            foreach (float u in new[] { 18.6f, 22.5f, 26.4f })
            {
                JarShelves(rm, V(u, 7.8f), Vector2.up, jars);
                JarShelves(rm, V(u, 37.2f), Vector2.down, jars);
            }
            foreach (float v in new[] { 5f, 9f, 13f, 17f, 28f, 32f, 36f, 40f })
            {
                JarShelves(rm, V(2.5f, v), Vector2.right, jars);
                JarShelves(rm, V(42.5f, v), Vector2.left, jars);
            }
            foreach (float v in new[] { 20.5f, 24.5f })
            {
                WallProp(rm, "M:SM_Prop_Bookcase_Small_02", V(2.5f, v), Vector2.right);
                WallProp(rm, "M:SM_Prop_Bookcase_Small_01", V(42.5f, v), Vector2.left);
            }
            // benches of glassware
            var glass = new[] { "D:SM_Item_Potion_01", "D:SM_Item_Potion_02", "D:SM_Item_Potion_04", "D:SM_Item_Potion_06", "D:SM_Item_Potion_08", "D:SM_Item_Potion_09",
                "D:SM_Item_Vial_01", "D:SM_Item_Vial_03", "D:SM_Item_Jar_01", "D:SM_Item_Jar_02", "D:SM_Item_Scale_01", "M:SM_Prop_Book_Open_02", "D:SM_Item_Shrooms", "D:SM_Item_Bones" };
            foreach (var (u, v) in new[] { (16.3f, 15.5f), (16.3f, 29.5f), (28.7f, 15.5f), (28.7f, 29.5f) })
            {
                for (int k = -1; k <= 1; k++)
                {
                    var t = Prop(rm, "R:SM_Prop_Dwarf_Table_03", u, v + k * 3.32f, 90f);
                    Furnish.TableTop(rm, t, glass, 12, 90f);
                }
                Furnish.Candles(rm, rm.W(u, v + 1.6f, 0.91f), 1.6f, 4.5f);
                Kit.PointLight(rm.Lights, rm.W(u, v - 1.5f, 1.4f), Kit.Pick(new Color(0.4f, 1f, 0.5f), new Color(0.6f, 0.4f, 1f), new Color(0.3f, 0.7f, 1f)), 2.2f, 4.5f, "PotionGlow");
                Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", u + 1.3f, v + 0.8f, Kit.R(0, 360));
            }
            Prop(rm, "D:SM_Item_Potion_Pole_01", 15f, 22.5f, 90f);
            foreach (var (u, v) in new[] { (7.5f, 22.5f), (37.5f, 22.5f) })
            {
                var rt = Prop(rm, "D:SM_Prop_Table_Round_01", u, v, Kit.R(0, 90));
                Furnish.TableTop(rm, rt, new[] { "M:SM_Prop_Book_Open_01", "D:SM_Item_Bones", "D:SM_Item_Shrunken_Head", "D:SM_Item_Potion_08", "D:SM_Item_Vial_02", "R:SM_Prop_Scroll_Open_01" }, 5, 0f);
                Furnish.Candles(rm, rt.transform.position + Vector3.up * 0.8f, 1.4f, 4f);
            }
            Prop(rm, "D:SM_Prop_Skeleton_Table_01", 8f, 8.5f, 90f);
            Prop(rm, "D:SM_Prop_Skeleton_Table_02", 37f, 36.5f, -90f);
            foreach (var (u, v) in new[] { (11f, 22.5f), (34f, 22.5f) }) Prop(rm, "D:SM_Prop_Cauldron_01", u, v, Kit.R(0, 360), 0.9f);
            Prop(rm, "R:SM_Prop_Tech_Furnace_02", 30.5f, 12f, 0f);
            Prop(rm, "R:SM_Prop_Tech_Container_03", 31.8f, 12.3f, 30f);
            Prop(rm, "R:SM_Prop_Tech_Container_01", 29.4f, 12.6f, 0f);
            Prop(rm, "M:SM_Prop_Book_Stand_01", 28.5f, 27f, 220f);
            foreach (var (u, v) in new[] { (5f, 22.5f), (40.5f, 22.5f), (16f, 33.5f) })
                Kit.StripColliders(Prop(rm, Kit.Pick("M:SM_Prop_Book_Pile_01", "M:SM_Prop_Book_Pile_03", "R:SM_Prop_Tech_Container_Pile_01"), u, v, Kit.R(0, 360)));
            // the brew: a great cauldron on a fire over a drawn circle, green and steaming
            Kit.StripColliders(Prop(rm, "D:SM_Env_Tiles_Pentagram_01", 22.5f, 22.5f, 0f, 1f, 0.01f));
            Prop(rm, "D:SM_Prop_Cauldron_01", 22.5f, 22.5f, 0f, 1.9f);
            Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(22.5f, 22.5f, 0.05f), 0f, 1.2f);
            Kit.P("R:FX_Steam_01", rm.Props, rm.W(22.5f, 22.5f, 1.5f), 0f, 1f);
            Kit.P("D:FX_Magic_Swirl_01", rm.Props, rm.W(22.5f, 22.5f, 1.4f), 0f, 0.6f);
            var brew = Kit.Prim(PrimitiveType.Cylinder, rm.Props, rm.W(22.5f, 22.5f, 1.38f), new Vector3(1.25f, 0.02f, 1.25f), Kit.Mat("Brew", new Color(0.3f, 1f, 0.35f), true), 0f, false, "Brew");
            Kit.PointLight(rm.Lights, rm.W(22.5f, 22.5f, 2.4f), new Color(0.45f, 1f, 0.45f), 14f, 10f, "BrewGlow");
            Kit.PointLight(rm.Lights, rm.W(22.5f, 22.5f, 0.4f), Kit.Fire, 6f, 5f, "BrewFire");
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                Kit.StripColliders(Prop(rm, Kit.Pick("D:SM_Prop_Candles_01_Preset", "D:SM_Prop_Candles_03_Preset"), 22.5f + Mathf.Cos(a) * 3.3f, 22.5f + Mathf.Sin(a) * 3.3f, 0f));
            }
            Furnish.Defender(rm, "D:SM_Chr_Goblin_Shaman_01", rm.W(24.9f, 21.6f), -70f, "Alchemist");
            // moderate light
            foreach (float v in new[] { 13f, 32f }) Furnish.Chandelier(rm, rm.W(22.5f, v, H), 12f, 12f);
            Fill(rm, Full, 5.4f, 3f, 11f, 10f);
            WallTorches(rm, Full, 9f, 3.4f, 5f, 9f, null, new[] { V(2.5f, 22.5f), V(42.5f, 22.5f) });
        }

        /// <summary>Three plank shelves on a wall, each with a row of jars and bottles.</summary>
        static void JarShelves(Room rm, Vector2 p, Vector2 inward, string[] jars)
        {
            var n = new Vector3(inward.x, 0, inward.y);
            var along = new Vector3(-inward.y, 0, inward.x);
            foreach (float y in new[] { 1.3f, 2.0f, 2.7f })
            {
                var shelf = Kit.PW("G:SM_Gen_Prop_Shelf_01", rm.Props, rm.W(p.x, p.y, y), n, 1.15f, 0.02f);
                Kit.StripColliders(shelf);
                for (int i = 0; i < 4; i++)
                {
                    var at = rm.W(p.x, p.y, y + 0.41f) + n * 0.26f + along * (-0.66f + i * 0.44f + Kit.R(-0.06f, 0.06f));
                    Kit.StripColliders(Kit.PC(jars[Kit.RI(0, jars.Length)], rm.Props, at, Kit.R(0, 360), 1f));
                }
            }
        }

        // ------------------------------------------------------------------ forge [1,3]: working, lit by its fire
        static void BuildForge(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            Arch_.InnerWall(rm, V(7f, 18f), V(7f, 27f), 0.6f, 0f, 3.8f, null, null, true);
            Arch_.InnerWall(rm, V(18f, 7f), V(27f, 7f), 0.6f, 0f, 3.8f, null, null, true);
            // the hearth under its hood; the flue climbs to the ceiling
            var hc = rm.W(24f, 25f);
            Prop(rm, "D:SM_Prop_Forge_FirePit_01", 24f, 25f, 0f, 1.15f);
            Kit.P("R:FX_Fire_Large_01", rm.Props, hc + Vector3.up * 0.9f, 0f, 0.8f);
            Kit.P("R:FX_Dust_Embers_Large_01", rm.Props, hc + Vector3.up * 1.4f, 0f, 1f);
            var hood = Kit.PC("D:SM_Prop_Forge_Ventilation_01", rm.Props, hc + Vector3.up * 2.9f, 0f, 1.3f);
            Kit.StripColliders(hood);
            float hoodTop = 2.9f + Kit.LocalBounds("D:SM_Prop_Forge_Ventilation_01").size.y * 1.3f;
            var pipeB = Kit.LocalBounds("D:SM_Prop_Forge_Ventilation_Pipe_01");
            float y = hoodTop - 0.05f;
            while (y < H - 0.05f)
            {
                float seg = Mathf.Min(pipeB.size.y * 1.2f, H - y);
                var p = Kit.Fit("D:SM_Prop_Forge_Ventilation_Pipe_01", rm.Props, new Vector3(hc.x - 0.8f, y, hc.z - 0.8f), new Vector3(1.6f, seg, 1.6f), 0f);
                Kit.StripColliders(p);
                y += seg;
            }
            Kit.PointLight(rm.Lights, hc + Vector3.up * 1.8f, Kit.Fire, 48f, 24f, "ForgeFire");
            Fill(rm, Full, 5.3f, 2.2f, 11f, 10f, new Color(1f, 0.62f, 0.35f));
            Kit.PointLight(rm.Lights, hc + Vector3.up * 0.8f + Vector3.back * 2f, new Color(1f, 0.45f, 0.15f), 10f, 8f, "ForgeGlow");
            // anvils, quench barrels and tools round it
            var anvils = new[] { (19.5f, 21.5f, 60f), (28.5f, 21.5f, -60f), (24f, 30.5f, 180f) };
            var waterMat = Kit.Mat("QuenchWater", new Color(0.08f, 0.14f, 0.16f));
            foreach (var (u, v, yaw) in anvils)
            {
                Prop(rm, "R:SM_Prop_Anvil_01", u, v, yaw);
                var hammer = Kit.PCR("R:SM_Prop_Forge_Hammer_01", rm.Props, rm.W(u + 0.2f, v, 1.08f), Quaternion.Euler(90f, yaw, 0), 1f);
                Kit.StripColliders(hammer);
                Vector3 off = Quaternion.Euler(0, yaw, 0) * new Vector3(1.4f, 0, 0.3f);
                var bp = rm.W(u, v) + off;
                Kit.PC("G:SM_Gen_Prop_Barrel_Wood_02", rm.Props, bp, 0f, 1f).name = "QuenchBarrel";
                Kit.Prim(PrimitiveType.Cylinder, rm.Props, bp + Vector3.up * 0.9f, new Vector3(0.62f, 0.01f, 0.62f), waterMat, 0f, false, "Water");
                Kit.P("R:FX_Sparks_01", rm.Props, rm.W(u, v, 1.1f), 0f, 0.6f);
            }
            Kit.P("R:FX_Steam_01", rm.Props, rm.W(19.5f, 21.5f) + Quaternion.Euler(0, 60f, 0) * new Vector3(1.4f, 1.0f, 0.3f), 0f, 0.4f);
            foreach (var (u, v, yaw) in new[] { (16f, 28f, 90f), (32f, 28f, -90f) })
            {
                var t = Prop(rm, "R:SM_Prop_Dwarf_Table_01", u, v, yaw);
                Furnish.TableTop(rm, t, new[] { "R:SM_Prop_Forge_Tongs_01", "R:SM_Prop_Forge_Hammer_02", "R:SM_Prop_Forge_Hammer_01", "R:SM_Prop_Toolstrap_01", "R:SM_Prop_Caliper_01", "R:SM_Wep_Sword_Small_02" }, 6, yaw);
            }
            // finished work and raw stock round the walls
            foreach (float v in new[] { 31f, 35f, 39f })
            {
                WallProp(rm, "D:SM_Prop_WeaponRack_01", V(2.5f, v), Vector2.right);
                WeaponsOnRack(rm, rm.W(2.85f, v), Vector3.right);
            }
            foreach (float u in new[] { 21f, 25f })
            {
                Prop(rm, "R:SM_Prop_Tech_Furnace_01", u, 41f, 180f);
                Kit.PointLight(rm.Lights, rm.W(u, 39.8f, 1.2f), new Color(1f, 0.45f, 0.15f), 8f, 7f, "FurnaceGlow");
                Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(u, 40.1f, 0.6f), 0f, 0.7f);
            }
            // the crucible of molten metal and its moulds, glowing
            Prop(rm, "R:SM_Env_Dwarf_Forge_Smelter_02", 36.5f, 33.5f, -90f, 0.85f);
            Kit.PointLight(rm.Lights, rm.W(36.5f, 33.5f, 2.6f), new Color(1f, 0.55f, 0.2f), 18f, 12f, "Crucible");
            Prop(rm, "R:SM_Prop_Dwarf_Smelter_Mould_01", 30f, 37.5f, 90f, 0.8f);
            Kit.PointLight(rm.Lights, rm.W(30f, 37.5f, 1.0f), new Color(1f, 0.6f, 0.2f), 8f, 7f, "Mould");
            Prop(rm, "R:SM_Prop_Anvil_01", 14.5f, 33f, 20f);
            Prop(rm, "G:SM_Gen_Prop_Barrel_Wood_01", 13f, 34f, 0f);
            Prop(rm, "R:SM_Env_Ore_Pile_01", 40f, 24f, 0f, 0.8f);
            Prop(rm, "R:SM_Env_Ore_Pile_02", 40.5f, 19.5f, 90f, 0.8f);
            Prop(rm, "D:SM_Prop_Minecart_01", 36.5f, 14f, 90f);
            Prop(rm, "D:SM_Prop_Crate_Rocks_01", 40.5f, 14f, 0f, 1.2f); Prop(rm, "D:SM_Prop_Crate_Rocks_01", 40.5f, 12.6f, 20f, 1.2f);
            foreach (float u in new[] { 6f, 9f, 12f })
            {
                var hw = Kit.PW(Kit.Pick("R:SM_Prop_Hanging_Weapon_01", "R:SM_Prop_Hanging_Weapon_02"), rm.Props, rm.W(u, 42.5f, 2.4f), Vector3.back, 1.3f, 0.02f);
                Kit.StripColliders(hw);
            }
            Prop(rm, "R:SM_Prop_Barrel_Stack_01", 15f, 41.6f, 180f);
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_02", 5f, 5f, 0f);
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_01", 6.3f, 4.6f, 0f);
            // the smiths at their anvils
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Worker_01", rm.W(18.3f, 20.6f), 50f, "Smith");
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Miner_01", rm.W(24f, 31.9f), 180f, "Smith");
            // firelit: the hearth and furnaces, a few low torches
            WallTorches(rm, Full, 12f, 2.8f, 3.5f, 8f);
        }
    }
}
