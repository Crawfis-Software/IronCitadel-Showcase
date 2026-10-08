using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static partial class CitadelBuilder
    {
        static readonly string[] Prisoners = { "R:Chr_Nomad_Male_01", "R:Chr_Nomad_Male_02", "R:Chr_Nomad_Female_01", "R:Chr_Nomad_Male_03", "R:Chr_Nomad_Female_02", "R:Chr_Hero_Male_01" };

        static void BeamCeiling(Room rm, Rect r, float spacing, bool alongU = true)
        {
            float H = rm.H;
            if (alongU)
                for (float v = r.yMin + spacing; v < r.yMax - 0.5f; v += spacing)
                    for (float u = r.xMin; u < r.xMax - 0.01f; u += 5f)
                    {
                        float w = Mathf.Min(5f, r.xMax - u);
                        var b = Kit.Fit("D:SM_Env_Basement_Support_Beam_01", rm.Ceil, rm.W(u, v - 0.17f, H - 0.38f), new Vector3(w, 0.38f, 0.34f), 90f);
                        Kit.StripColliders(b); Kit.SetLayer(b, Kit.CeilingLayer);
                    }
            else
                for (float u = r.xMin + spacing; u < r.xMax - 0.5f; u += spacing)
                    for (float v = r.yMin; v < r.yMax - 0.01f; v += 5f)
                    {
                        float w = Mathf.Min(5f, r.yMax - v);
                        var b = Kit.Fit("D:SM_Env_Basement_Support_Beam_01", rm.Ceil, rm.W(u - 0.17f, v, H - 0.38f), new Vector3(0.34f, 0.38f, w), 0f);
                        Kit.StripColliders(b); Kit.SetLayer(b, Kit.CeilingLayer);
                    }
        }

        static void Post(Room rm, float u, float v)
        {
            var p = Kit.Fit("D:SM_Env_Basement_Support_Pole_01", rm.Arch, rm.W(u - 0.3f, v - 0.3f, 0f), new Vector3(0.6f, rm.H - 0.3f, 0.6f), 0f);
            Kit.StripColliders(p);
            Kit.Solid(rm.Cols, rm.W(u, v, rm.H / 2f), new Vector3(0.6f, rm.H, 0.6f), "Post");
        }

        // ------------------------------------------------------------------ guard room [3,1]: a guard post at night
        static void BuildGuard(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            BeamCeiling(rm, Full, 5f);
            foreach (float u in new[] { 22.5f, 32.5f }) foreach (float v in new[] { 12.5f, 32.5f }) Post(rm, u, v);
            // screens turn both ways in
            Arch_.InnerWall(rm, V(38.5f, 18f), V(38.5f, 27f), 0.6f, 0f, H, null, null, true);
            Arch_.InnerWall(rm, V(18f, 38.5f), V(27f, 38.5f), 0.6f, 0f, H, null, null, true);
            // the barracks wing behind a partition, two doorways into it
            Arch_.InnerWall(rm, V(16f, 2.5f), V(16f, 42.5f), 0.6f, 0f, H, null, new[] { new Opening(9.5f, 2.6f, 2.9f), new Opening(30.5f, 2.6f, 2.9f) });
            foreach (float v in new[] { 6f, 15f, 21f, 27f, 36f, 40.5f })
                foreach (var (u, n) in new[] { (2.5f, Vector2.right), (15.7f, Vector2.left) })
                {
                    if (u > 10f && (Mathf.Abs(v - 12f) < 2.5f || Mathf.Abs(v - 33f) < 2.5f)) continue;
                    WallProp(rm, "D:SM_Prop_Bed_01", V(u, v), n);
                    Prop(rm, Kit.Pick("D:SM_Prop_Chest_01", "D:SM_Prop_Chest_04", "R:SM_Prop_Camp_Chest_01"), u + n.x * 3.35f, v, n.x > 0 ? 90f : -90f);
                }
            foreach (float v in new[] { 9f, 24f, 31f }) Kit.StripColliders(Prop(rm, "R:SM_Prop_Camp_Roll_01", 9.2f, v, Kit.R(0, 360)));
            Kit.PointLight(rm.Lights, rm.W(9f, 12f, 3.0f), Kit.Candle, 1.5f, 9f, "BarracksLamp");
            Kit.PointLight(rm.Lights, rm.W(9f, 33f, 3.0f), Kit.Candle, 1.5f, 9f, "BarracksLamp");
            Prop(rm, "D:SM_Prop_Lantern_01", 8.9f, 24.1f, 0f);

            // the night watch's table, rug and stove
            Kit.StripColliders(Kit.Fit("R:SM_Prop_Rug_06", rm.Props, rm.W(24.5f, 19.5f, 0f), new Vector3(5.6f, 0.05f, 6f), 90f));
            var t = Prop(rm, "R:SM_Prop_Dwarf_Table_02", 27f, 22.5f, 90f);
            Furnish.TableTop(rm, t, new[] { "R:SM_Prop_Mug_01", "M:SM_Prop_Papers_02", "M:SM_Prop_Papers_05", "D:SM_Item_Coins_01", "R:SM_Prop_Tankard_01", "G:SM_Gen_Prop_Food_Bread_02" }, 7, 90f);
            Furnish.Candles(rm, t.transform.position + Vector3.up * 0.91f, 2.5f, 5f);
            Kit.StripColliders(Kit.P("D:SM_Prop_Lantern_02", rm.Props, rm.W(27f, 22.5f, H - 0.75f), 0f, 1.1f));
            Kit.StripColliders(Kit.P("D:SM_Prop_Chain_02", rm.Props, rm.W(27f, 22.5f, H - 0.05f), 0f, 0.4f));
            Kit.PointLight(rm.Lights, rm.W(27f, 22.5f, H - 1.3f), Kit.Candle, 5f, 8f, "TableLantern");
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", 25.6f, 21.5f, 10f); Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", 28.4f, 23.8f, 70f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_Broken_01", 25.4f, 24.2f, 200f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", 28.75f, 21.5f, 0f);
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Soldier_Male_01", rm.W(28.55f, 21.5f), -90f, "NightWatch", true);
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Soldier_Female_01", rm.W(22.5f, 35.5f), 0f, "NightWatch");
            var t2 = Prop(rm, "R:SM_Prop_Dwarf_Table_01", 23f, 28.5f, 15f);
            Furnish.TableTop(rm, t2, new[] { "R:SM_Prop_Mug_01", "D:SM_Item_Coins_02", "M:SM_Prop_Papers_03", "R:SM_Prop_Dwarf_Chair_Stool_01" }, 4, 15f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", 23.2f, 27.2f, 15f);
            var stove = Prop(rm, "D:SM_Prop_Fireplace_01", 31f, 16f, 200f, 1.1f);
            Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(31f, 16f, 0.55f), 0f, 0.35f);
            Kit.PointLight(rm.Lights, rm.W(31f, 16.6f, 0.9f), Kit.Fire, 6f, 9f, "Stove");
            Prop(rm, "R:SM_Prop_Barrel_01", 32.5f, 15f); Prop(rm, "R:SM_Prop_Crate_01", 33.2f, 16.6f, 20f);
            // arms: racks down the middle and along the south wall, armour on stands
            foreach (float u in new[] { 21.5f, 25f })
            {
                var r1 = Prop(rm, "D:SM_Prop_WeaponRack_01", u, 9.3f, 0f);
                WeaponsOnRack(rm, rm.W(u, 9.55f), Vector3.forward);
                var r2 = Prop(rm, "D:SM_Prop_WeaponRack_01", u, 8.7f, 180f);
                WeaponsOnRack(rm, rm.W(u, 8.45f), Vector3.back);
            }
            foreach (float u in new[] { 29f, 34f })
            {
                WallProp(rm, "D:SM_Prop_WeaponRack_01", V(u, 2.5f), Vector2.up);
                WeaponsOnRack(rm, rm.W(u, 2.85f), Vector3.forward);
            }
            foreach (float v in new[] { 6f, 9f, 31f, 34f }) Prop(rm, Kit.Pick("M:SM_Prop_KnightStand_01", "M:SM_Prop_KnightStand_01", "M:SM_Prop_KnightStand_Broken_01"), 41.6f, v, -90f);
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_02", 19f, 4f, 0f);
            Prop(rm, "R:SM_Prop_Barrel_01", 40.5f, 40f); Prop(rm, "R:SM_Prop_Barrel_02", 41.2f, 38.8f, 50f); Prop(rm, "R:SM_Prop_Crate_01", 40.6f, 36.5f, 5f);
            Prop(rm, "R:SM_Prop_Barrel_Stack_01", 30f, 41.5f, 180f);
            Prop(rm, "R:SM_Prop_Crate_Stack_01", 18.2f, 41f, 0f);
            Prop(rm, "D:SM_Prop_Chest_03", 41.4f, 14f, -90f);
            Prop(rm, "D:SM_Prop_Chest_02", 41.4f, 27f, -90f);
            // off-watch corner: benches round a cask, gear dumped by the bunks' doorways, a shield rack
            Prop(rm, "R:SM_Prop_Barrel_Shelf_Small_01", 31.5f, 33.2f, 0f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", 29.5f, 33f, 90f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", 33.6f, 33.4f, 90f);
            foreach (var (u, v) in new[] { (30.6f, 34.3f), (32.4f, 31.6f) }) Kit.StripColliders(Prop(rm, "R:SM_Prop_Tankard_01", u, v, Kit.R(0, 360), 1f, 0.95f));
            foreach (var (u, v) in new[] { (18.5f, 13.5f), (18.2f, 31.5f), (19f, 34.6f) })
                Prop(rm, Kit.Pick("R:SM_Prop_Camp_Chest_02", "D:SM_Prop_Chest_01", "R:SM_Prop_Crate_01"), u, v, Kit.R(0, 360));
            foreach (float u in new[] { 26f, 28.5f, 31f })
            {
                var sh = Kit.PW(Kit.Pick("R:SM_Wep_Shield_02", "R:SM_Wep_Shield_04", "R:SM_Wep_Shield_06", "D:SM_Wep_Shield_Heater_01"), rm.Props, rm.W(u, 42.5f, 1.7f), Vector3.back, 1f, 0.05f);
                Kit.StripColliders(sh);
            }
            Prop(rm, "R:SM_Prop_Dwarf_Weapon_Barrel_01", 36f, 40.6f, 0f);
            Prop(rm, "M:SM_Prop_KnightStand_01", 24f, 33.5f, 150f);
            // dim: the stove, one brazier, a few low torches
            Furnish.Brazier(rm, rm.W(36.5f, 12f), 6f, 9f, "D:SM_Prop_Brazier_01");
            WallTorches(rm, Rect.MinMaxRect(16.3f, 2.5f, 42.5f, 42.5f), 10f, 2.2f, 4.5f, 8f);
            Fill(rm, Rect.MinMaxRect(16f, 2.5f, 42.5f, 42.5f), 3.1f, 2.4f, 10f, 10f);
        }

        // ------------------------------------------------------------------ prison [2,1]: rows of cells in a riot
        static void BuildPrison(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            BeamCeiling(rm, Full, 5f, false);
            var spines = new List<(float v, float ua, float ub)>();
            // rows: (v0 of the row, opening direction +1 north / -1 south, cell origins)
            var rows = new List<(float v0, int s, float[] us)>
            {
                (2.5f, 1, new[] { 14.5f, 26.5f, 30.5f, 34.5f, 38.5f }),
                (12.5f, -1, new[] { 14.5f, 18.5f, 22.5f, 26.5f, 30.5f, 34.5f }),
                (16.5f, 1, new[] { 14.5f, 18.5f, 22.5f, 26.5f, 30.5f, 34.5f }),
                (26.5f, -1, new[] { 14.5f, 18.5f, 22.5f, 26.5f, 30.5f, 34.5f }),
                (30.5f, 1, new[] { 14.5f, 18.5f, 22.5f, 26.5f, 30.5f, 34.5f }),
                (38.5f, -1, new[] { 14.5f, 26.5f, 30.5f, 34.5f, 38.5f }),
            };
            int cellNo = 0;
            var forcedFronts = new List<Vector3>();
            foreach (var (v0, s, us) in rows)
            {
                foreach (float u0 in us)
                {
                    bool forced = Kit.Chance(0.5f) || cellNo % 4 == 1;
                    Cell(rm, u0, v0, s, forced, cellNo, forcedFronts);
                    cellNo++;
                }
                // side walls between and at the ends of the row (each boundary once)
                var bounds = new SortedSet<float>();
                foreach (float u0 in us) { bounds.Add(u0); bounds.Add(u0 + 4f); }
                foreach (float u in bounds)
                {
                    if (u <= 2.6f || u >= 42.4f) continue;
                    Arch_.InnerWall(rm, V(u, v0 + 0.05f), V(u, v0 + 3.95f), 0.3f, 0f, H, null, null, false);
                }
            }
            // spines of the two back-to-back blocks
            foreach (float v in new[] { 16.5f, 30.5f }) Arch_.InnerWall(rm, V(14.5f, v), V(38.5f, v), 0.4f, 0f, H);
            foreach (float v in new[] { 16.5f, 30.5f })
                foreach (float u in new[] { 14.5f, 38.5f })
                    Kit.StripColliders(Kit.PC("D:SM_Env_Wall_End_01", rm.Arch, rm.W(u, v), 0f, 0.75f));

            // the warden's corner at the west end: shelves and bookcases against the wall; one bookcase is a door
            const string bk = "M:SM_Prop_Bookcase_Small_01";
            for (int k = -3; k <= 3; k++)
            {
                float v = 22.5f + k * 2.2f;
                if (k == 0) { SecretBookcase(rm, v, bk); continue; }
                var b = WallProp(rm, k % 2 == 0 ? bk : "M:SM_Prop_Bookcase_Small_02", V(2.5f, v), Vector2.right, 1f, 0f, 0.02f);
            }
            foreach (float v in new[] { 6.5f, 9.5f, 35.5f, 38.5f }) WallProp(rm, "G:SM_Gen_Prop_Shelf_03", V(2.5f, v), Vector2.right);
            var desk = Prop(rm, "R:SM_Prop_Dwarf_Table_01", 8.3f, 19.5f, 90f);
            Furnish.TableTop(rm, desk, new[] { "M:SM_Prop_Papers_01", "M:SM_Prop_Papers_04", "M:SM_Prop_Book_Open_01", "D:SM_Item_Inkwell_01", "M:SM_Prop_Book_Pile_01" }, 5, 90f);
            Furnish.Candles(rm, desk.transform.position + Vector3.up * 0.56f + Vector3.forward * 0.4f, 2.2f, 5f);
            Kit.PC("D:SM_Prop_Lantern_01", rm.Props, rm.W(8.3f, 20.3f, 0.56f), 0f, 1f);
            Kit.PointLight(rm.Lights, rm.W(7.5f, 20.5f, 1.6f), Kit.Candle, 3.5f, 9f, "WardenLamp");
            Kit.PointLight(rm.Lights, rm.W(6f, 26f, 2.8f), Kit.Candle, 1.6f, 8f, "WardenFill");
            Kit.PointLight(rm.Lights, rm.W(6f, 9f, 2.8f), Kit.Candle, 1.2f, 8f, "WardenFill");
            Kit.PCR("R:SM_Prop_Chair_Broken_01", rm.Props, rm.W(7f, 19f, 0.4f), Quaternion.Euler(0, 40f, 90f), 1f);
            Prop(rm, "D:SM_Prop_Toture_Stocks_01", 8f, 33f, 90f);
            Prop(rm, "D:SM_Prop_Toture_Cage_01", 9f, 7f, 20f);
            Prop(rm, "D:SM_Prop_Chest_02", 4.2f, 13f, 90f);
            Kit.PCR("M:SM_Prop_Book_Pile_02", rm.Props, rm.W(5f, 24.5f, 0.2f), Quaternion.Euler(0, 30, 80f), 1f);
            for (int i = 0; i < 6; i++) Kit.StripColliders(Prop(rm, Kit.Pick("M:SM_Prop_Papers_01", "M:SM_Prop_Papers_03", "M:SM_Prop_Papers_06"), Kit.R(4f, 12f), Kit.R(14f, 31f), Kit.R(0, 360)));

            // riot debris in the aisles
            var debris = new[] { "D:SM_Prop_Barrel_Broken_01", "D:SM_Prop_Barrel_Broken_02", "R:SM_Prop_Crate_Broken_01", "D:SM_Env_Brick_Rubble_02", "D:SM_Env_Brick_Rubble_04",
                "R:SM_Prop_Dwarf_Chair_Stool_Broken_01", "D:SM_Prop_Vase_Broken_01", "D:SM_Env_Rubble_02", "D:SM_Prop_Plank_01" };
            var aisle = new[] { (16f, 37f, 7.5f, 11.5f), (16f, 37f, 21.5f, 25.5f), (16f, 37f, 35f, 38f), (39f, 42f, 8f, 37f) };
            for (int i = 0; i < 26; i++)
            {
                var (ua, ub, va, vb) = aisle[i % aisle.Length];
                var key = debris[Kit.RI(0, debris.Length)];
                Kit.StripColliders(Prop(rm, key, Kit.R(ua, ub), Kit.R(va, vb), Kit.R(0, 360)));
            }
            Kit.PCR("R:SM_Prop_Dwarf_Table_Broken_01", rm.Props, rm.W(30f, 23.5f, 0.5f), Quaternion.Euler(0, 20, 95f), 1f);
            Kit.PCR("R:SM_Prop_Dwarf_Bed_01", rm.Props, rm.W(40.5f, 18f, 0.7f), Quaternion.Euler(0, 80f, 88f), 1f);
            Kit.PCR("D:SM_Env_Door_Bars_01", rm.Props, rm.W(24f, 9.3f, 0.12f), Quaternion.Euler(-90f, 25f, 0), 1f);
            Kit.PCR("D:SM_Env_Door_Bars_01", rm.Props, rm.W(19.5f, 36.3f, 0.12f), Quaternion.Euler(-90f, -70f, 0), 1f);
            // a barrel set alight in the middle aisle
            var fireBarrel = Prop(rm, "R:SM_Prop_Barrel_01", 21f, 23.2f, 0f);
            Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(21f, 23.2f, 1.05f), 0f, 1f);
            Kit.PointLight(rm.Lights, rm.W(21f, 23.2f, 1.9f), Kit.Fire, 7f, 9f, "BurningBarrel");
            Kit.P("R:FX_Dust_Embers_Small_01", rm.Props, rm.W(21f, 23.2f, 1.6f), 0f, 1f);
            // the rioters
            var spots = new[] { (19.5f, 9.5f, 30f), (33f, 10.5f, 200f), (25.5f, 24f, -60f), (35.5f, 22.5f, 100f), (40.5f, 30f, 170f), (28f, 36.5f, 140f) };
            for (int i = 0; i < spots.Length; i++)
                Furnish.Defender(rm, Prisoners[i], rm.W(spots[i].Item1, spots[i].Item2), spots[i].Item3, "Rioter");
            // dark: a few torches at the aisle ends, the warden's candle, the burning barrel
            foreach (var (u, v, n) in new[] { (42.5f, 9.5f, Vector3.left), (42.5f, 23.5f, Vector3.left), (42.5f, 36.5f, Vector3.left), (2.5f, 3.6f, Vector3.right), (12.5f, 42.5f, Vector3.back) })
                Arch_.Torch(rm, rm.W(u, v), n, 2.3f, 4.5f, 8f, "D:SM_Prop_Torch_Ornate_02");
            Fill(rm, Full, 3.1f, 0.9f, 10f, 11f, new Color(0.75f, 0.7f, 0.8f));
            Kit.PointLight(rm.Lights, rm.W(22.5f, 4.5f, 2.8f), Kit.Fire, 2.5f, 7f, "DoorLight");
            Kit.PointLight(rm.Lights, rm.W(22.5f, 40.5f, 2.8f), Kit.Fire, 2.5f, 7f, "DoorLight");
        }

        static void Cell(Room rm, float u0, float v0, int s, bool forced, int index, List<Vector3> forcedFronts)
        {
            float H = rm.H;
            float vf = s > 0 ? v0 + 4f : v0;            // the barred front, on the aisle
            Vector3 toAisle = new Vector3(0, 0, s);
            var cell = Kit.Group($"Cell_{index:00}" + (forced ? "_forced" : "_shut"), rm.Props);
            // bars above the front up to the ceiling (a wall piece as lintel), on both faces
            Arch_.Pieces(rm, cell, V(u0, vf), V(u0 + 4f, vf), new Vector2(0, s), 2.45f, H - 2.45f, rm.St);
            Arch_.Pieces(rm, cell, V(u0, vf), V(u0 + 4f, vf), new Vector2(0, -s), 2.45f, H - 2.45f, rm.St);
            Kit.SolidMinMax(rm.Cols, rm.W(u0, vf - 0.2f, 2.45f), rm.W(u0 + 4f, vf + 0.2f, H), "CellLintel");
            // fence and door: north-facing cells have the door at the west end, south-facing at the east end
            float fenceU = s > 0 ? u0 + 2.75f : u0 + 1.25f;
            float hingeU = s > 0 ? u0 + 1.5f : u0 + 2.5f;
            var fence = Kit.P("D:SM_Prop_Metal_Fence_01", cell, rm.W(fenceU, vf), s > 0 ? 0f : 180f, 1f);
            Kit.StripColliders(fence);
            Kit.Solid(rm.Cols, rm.W(fenceU, vf, 1.25f), new Vector3(2.5f, 2.5f, 0.2f), "CellBars");
            var postU = s > 0 ? u0 + 0.08f : u0 + 3.92f;
            Kit.StripColliders(Kit.PC("D:SM_Prop_Metal_Fence_Post_01", cell, rm.W(postU, vf), 0f, 1f));
            Kit.Solid(rm.Cols, rm.W(postU, vf, 1.25f), new Vector3(0.2f, 2.5f, 0.2f), "CellPost");
            float doorYaw = s > 0 ? 0f : 180f;
            if (forced)
            {
                if (index % 3 == 0)
                {
                    // torn off and thrown down in the aisle
                    var d = Kit.PCR("D:SM_Env_Door_Bars_01", cell, rm.W(hingeU + (s > 0 ? -0.7f : 0.7f), vf + s * 1.4f, 0.13f), Quaternion.Euler(-90f, doorYaw + Kit.R(-30, 30), 0), 1f);
                    Kit.StripColliders(d);
                }
                else
                {
                    // wrenched open out into the aisle, hanging
                    var d = Kit.P("D:SM_Env_Door_Bars_01", cell, rm.W(hingeU, vf), Quaternion.Euler(Kit.R(-4, 4), doorYaw + (s > 0 ? 1 : 1) * Kit.R(95, 120), Kit.R(-6, 6)), Vector3.one);
                    Kit.StripColliders(d);
                }
                forcedFronts.Add(rm.W(u0 + 2f, vf));
            }
            else
            {
                var d = Kit.P("D:SM_Env_Door_Bars_01", cell, rm.W(hingeU, vf), doorYaw, 1f);
                Kit.StripColliders(d);
                Kit.Solid(rm.Cols, rm.W(hingeU + (s > 0 ? -0.67f : 0.67f), vf, 1.1f), new Vector3(1.35f, 2.2f, 0.2f), "CellDoor");
            }
            // inside: bedding and a pot; overturned where the cell was forced
            float vin = v0 + 2f, uin = u0 + 2f;
            if (forced)
            {
                Kit.PCR("R:SM_Prop_Dwarf_Bed_01", cell, rm.W(u0 + 1.0f, vin, 0.7f), Quaternion.Euler(Kit.R(-10, 10), Kit.R(0, 30), 88f), 1f);
                Kit.StripColliders(Prop(rm, Kit.Pick("R:SM_Prop_Camp_Roll_02", "R:SM_Prop_Camp_Rug_05"), uin + 0.5f, vin + Kit.R(-0.8f, 0.8f), Kit.R(0, 360)));
                Kit.PCR("D:SM_Prop_Stool_01", cell, rm.W(uin + 0.8f, vin - 0.7f, 0.25f), Quaternion.Euler(90f, Kit.R(0, 360), 0), 1f);
            }
            else
            {
                Prop(rm, "R:SM_Prop_Dwarf_Bed_01", u0 + 0.85f, vin, 0f);
                if (Kit.Chance(0.35f)) WallProp(rm, "D:SM_Prop_Skeleton_Slave_Wall_Sitting_01", V(u0 + 2.8f, s > 0 ? v0 : v0 + 4f), new Vector2(0, s), 1f);
                else Prop(rm, "D:SM_Prop_Stool_02", uin + 0.9f, vin + 0.3f, Kit.R(0, 360));
            }
            var chain = Kit.PW("D:SM_Prop_Chain_03", cell, rm.W(u0 + 3.2f, s > 0 ? v0 : v0 + 4f, 2.6f), new Vector3(0, 0, s), 1f, 0.05f);
            Kit.StripColliders(chain);
            Kit.StripColliders(Prop(rm, "G:SM_Gen_Prop_Pot_04", u0 + 3.4f, vin + (s > 0 ? -1.2f : 1.2f), 0f));
        }

        /// <summary>The vault door: a bookcase on a hinge, flush with its neighbours, swinging into the passage behind.</summary>
        static void SecretBookcase(Room rm, float v, string key)
        {
            var b = Kit.LocalBounds(key);                 // x [-2.30,-0.17] (width 2.13), z [0,0.89], y [0,3.35]
            // hinge at the back-south edge of the bookcase, on the wall face; bookcase front faces east (+x)
            float halfW = b.size.x / 2f;
            Vector3 hingePos = rm.W(2.5f, v - halfW);
            var hinge = new GameObject("SecretBookcase (vault door)");
            hinge.transform.SetParent(rm.Props, false);
            hinge.transform.SetPositionAndRotation(hingePos, Quaternion.identity);
            var rot = Quaternion.LookRotation(Vector3.right, Vector3.up);   // local +z (front) → east
            // with yaw 90, local -x runs north; place so the mesh's south edge (local x max) sits on the hinge
            var go = Kit.Inst(key, hinge.transform);
            go.transform.rotation = rot;
            go.transform.position = hingePos + rot * new Vector3(-b.max.x, 0f, -b.min.z) + Vector3.right * 0.02f;
            Kit.StripColliders(go);
            var box = hinge.AddComponent<BoxCollider>();
            box.center = new Vector3(b.size.z / 2f + 0.02f, b.size.y / 2f, halfW);
            box.size = new Vector3(b.size.z, b.size.y, b.size.x);
            var body = hinge.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            var sb = hinge.AddComponent<SecretBookcase>();
            sb.openYaw = -90f;
            sb.seconds = 1.8f;
        }

        // ------------------------------------------------------------------ treasure vault [2,0]: small, low, sealed
        static void BuildVault(Room rm)
        {
            var r = Rect.MinMaxRect(26.5f, 14.5f, 42.5f, 30.5f);
            Shell(rm, r);
            foreach (float u in new[] { 31.5f, 37.5f }) foreach (float v in new[] { 19.5f, 25.5f }) Pillar(rm, u, v, rm.H, "D:SM_Env_Pillar_Square_02", true, 1f);
            // the hoard
            Kit.StripColliders(Prop(rm, "R:SM_Prop_Gold_Pile_01", 34.5f, 22.5f, 20f, 0.9f));
            Kit.StripColliders(Prop(rm, "R:SM_Prop_Gold_Pile_02", 29.5f, 27.5f, 70f));
            Kit.StripColliders(Prop(rm, "R:SM_Prop_Gold_Pile_03", 39.5f, 17.5f, 10f));
            Kit.StripColliders(Prop(rm, "R:SM_Prop_Gold_Pile_03", 29f, 17f, 140f));
            Prop(rm, "R:SM_Prop_Dwarf_TreasureChest_01", 34.5f, 29.2f, 180f, 0.8f);
            foreach (var (u, v, yaw) in new[] { (28f, 21f, 90f), (28f, 24f, 80f), (41f, 21.2f, -90f), (41f, 27.5f, -100f), (31f, 15.6f, 0f), (38f, 15.6f, 10f) })
                Prop(rm, Kit.Pick("D:SM_Prop_Chest_01", "D:SM_Prop_Chest_02", "D:SM_Prop_Chest_03", "R:SM_Prop_Camp_Chest_02"), u, v, yaw);
            var coins = new[] { "R:SM_Prop_Gold_Coins_01", "R:SM_Prop_Gold_Coins_03", "R:SM_Prop_Gold_Coins_05", "R:SM_Prop_Gold_Coins_06", "R:SM_Prop_Gold_Coins_07",
                "D:SM_Prop_Jewel_02", "D:SM_Prop_Jewel_05", "R:SM_Prop_Goblet_02", "R:SM_Prop_Gem_Cluster_01", "D:SM_Item_Coins_03", "D:SM_Item_Coins_04" };
            for (int i = 0; i < 26; i++)
                Kit.StripColliders(Prop(rm, coins[Kit.RI(0, coins.Length)], Kit.R(27.5f, 41.5f), Kit.R(15.5f, 29.5f), Kit.R(0, 360)));
            Prop(rm, "D:SM_Prop_Crate_Ornate_01", 40.8f, 29f, 15f);
            Kit.StripColliders(Prop(rm, "R:SM_Prop_Hell_Skull_01", 41f, 29f, 200f, 0.5f, 1.02f));
            WallProp(rm, "R:SM_Env_Statue_05", V(34.5f, 30.5f), Vector2.down, 0.9f).name = "Statue";
            Furnish.Pickup(rm, rm.W(34.5f, 22.5f, 0.6f), "the hoard of the treasure vault", 1.2f);
            // dim: two candle stands
            foreach (var (u, v) in new[] { (28f, 29f), (41f, 16f) })
            {
                Prop(rm, "D:SM_Prop_Candle_Stand_01_Preset", u, v, 0f);
                Kit.PointLight(rm.Lights, rm.W(u, v, 1.9f), Kit.Candle, 4f, 8f, "CandleStand");
            }
            Fill(rm, r, 2.8f, 2.2f, 9f, 8f, new Color(1f, 0.78f, 0.45f));
        }

        // ------------------------------------------------------------------ kitchen [1,1]: small service room cooking for the feast
        static void BuildKitchen(Room rm)
        {
            var r = Rect.MinMaxRect(16.5f, 2.5f, 42.5f, 28.5f);
            Shell(rm, r);
            float H = rm.H;
            BeamCeiling(rm, r, 4.5f);
            // screens: neither door looks straight into the kitchen
            Arch_.InnerWall(rm, V(19.5f, 6.5f), V(26.5f, 6.5f), 0.5f, 0f, H, null, null, true);
            Arch_.InnerWall(rm, V(38.5f, 19f), V(38.5f, 26f), 0.5f, 0f, H, null, null, true);
            // the hearth on the north wall: fire, spit roast, cauldron
            var hearth = Kit.PW("R:SM_Env_Dwarf_Wall_Fireplace_01", rm.Props, rm.W(29.5f, 28.5f), Vector3.back, 0.78f, 0f);
            Kit.StripColliders(hearth);
            Kit.Solid(rm.Cols, rm.W(29.5f, 27.6f, 2f), new Vector3(4.5f, 4f, 1.8f), "Hearth");
            Kit.P("R:FX_Fire_Large_01", rm.Props, rm.W(29.5f, 27.6f, 0.3f), 0f, 0.8f);
            Kit.P("R:FX_Dust_Embers_Large_01", rm.Props, rm.W(29.5f, 26.5f, 0.6f), 0f, 0.6f);
            Kit.PointLight(rm.Lights, rm.W(29.5f, 25.6f, 1.6f), Kit.Fire, 32f, 18f, "HearthFire");
            Fill(rm, r, 3.5f, 2.2f, 10f, 9f, new Color(1f, 0.7f, 0.45f));
            Prop(rm, "D:SM_Prop_Goblin_Rotisserie_Meat_01", 29.5f, 25.3f, 0f);
            Prop(rm, "D:SM_Prop_Cauldron_01", 33.6f, 26.2f, 20f, 1.2f);
            Kit.P("R:FX_Steam_01", rm.Props, rm.W(33.6f, 26.2f, 1.0f), 0f, 0.6f);
            // worktables with the feast in preparation
            var food = new[] { "G:SM_Gen_Prop_Food_Meat_01", "G:SM_Gen_Prop_Food_Meat_03", "G:SM_Gen_Prop_Food_Meat_06", "G:SM_Gen_Prop_Food_Bread_01", "G:SM_Gen_Prop_Food_Vegetable_01",
                "G:SM_Gen_Prop_Food_Vegetable_04", "G:SM_Gen_Prop_Food_Vegetable_06", "G:SM_Gen_Prop_Pot_04", "G:SM_Gen_Prop_Plate_01", "R:SM_Wep_Knife_Small_01", "R:SM_Prop_Camp_Pot_04" };
            foreach (var (u, v) in new[] { (27f, 17.5f), (27f, 12.5f), (34.5f, 12.5f), (21.5f, 13f) })
            {
                var t = Prop(rm, "R:SM_Prop_Dwarf_Table_03", u, v, 0f);
                Furnish.TableTop(rm, t, food, 11, 0f);
            }
            var t2 = Prop(rm, "G:SM_Gen_Prop_Table_01", 34.5f, 18f, 90f);
            Furnish.TableTop(rm, t2, food, 6, 90f);
            // stores along the west and east walls
            var stores = new[] { "G:SM_Gen_Prop_Sack_Stack_01", "G:SM_Gen_Prop_Sack_Stack_02", "R:SM_Prop_Barrel_01", "R:SM_Prop_Barrel_02", "R:SM_Prop_Barrel_03", "R:SM_Prop_Crate_01", "G:SM_Gen_Prop_Sack_01", "G:SM_Gen_Prop_Sack_02" };
            for (float v = 9f; v < 27.5f; v += 1.4f) Prop(rm, stores[Kit.RI(0, stores.Length)], 17.5f + Kit.R(0, 0.4f), v, Kit.R(0, 360));
            for (float u = 34f; u < 41.5f; u += 1.3f) Prop(rm, stores[Kit.RI(0, stores.Length)], u, 3.4f + Kit.R(0, 0.3f), Kit.R(0, 360));
            Prop(rm, "R:SM_Prop_Barrel_Stack_01", 22f, 27.6f, 180f);
            foreach (var (u, v) in new[] { (42.5f, 8f), (42.5f, 13f) }) WallProp(rm, "G:SM_Gen_Prop_Shelf_02", V(u, v), Vector2.left);
            foreach (var (u, v) in new[] { (42.5f, 8f), (42.5f, 13f) })
                for (int i = 0; i < 4; i++)
                    Kit.StripColliders(Kit.PC(Kit.Pick("G:SM_Gen_Prop_Pot_01", "G:SM_Gen_Prop_Pot_02", "D:SM_Item_Jar_01", "G:SM_Gen_Prop_Bottle_02"), rm.Props,
                        rm.W(u - 0.45f, v - 0.7f + i * 0.45f, Kit.Pick(0.95f, 1.75f)), Kit.R(0, 360)));
            // a bread oven in the west wall, a grill in the middle, a serving counter of dishes for the hall
            var oven = WallProp(rm, "D:SM_Env_Wall_Fireplace_Alcove_01", V(16.5f, 19.5f), Vector2.right, 0.95f, 0f, 0f);
            Kit.StripColliders(oven);
            Kit.Solid(rm.Cols, rm.W(17.4f, 19.5f, 1.7f), new Vector3(1.9f, 3.4f, 2.4f), "Oven");
            Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(17.3f, 19.5f, 0.35f), 0f, 0.6f);
            Kit.PointLight(rm.Lights, rm.W(18.6f, 19.5f, 1.0f), Kit.Fire, 8f, 8f, "Oven");
            var grill = Prop(rm, "R:SM_Prop_Dwarf_Fire_Pit_01", 34.5f, 22.5f, 90f, 0.9f);
            Prop(rm, "R:SM_Prop_Camp_Pot_05", 34f, 22.5f, 0f, 1f, 0.3f);
            Kit.P("R:FX_Fire_Small_01", rm.Props, rm.W(35.2f, 22.5f, 0.2f), 0f, 0.5f);
            Kit.P("R:FX_Steam_01", rm.Props, rm.W(34f, 22.5f, 1.3f), 0f, 0.4f);
            Kit.PointLight(rm.Lights, rm.W(34.5f, 22.5f, 1.2f), Kit.Fire, 9f, 8f, "Grill");
            for (int i = 0; i < 3; i++)
            {
                var c = WallProp(rm, "D:SM_Prop_Table_01", V(24f + i * 2.6f, 2.5f), Vector2.up);
                Furnish.TableTop(rm, c, new[] { "G:SM_Gen_Prop_Plate_01", "G:SM_Gen_Prop_Food_Meat_05", "G:SM_Gen_Prop_Food_Bread_01", "R:SM_Prop_Goblet_02", "G:SM_Gen_Prop_Pot_05" }, 6, 0f);
            }
            for (float u = 18f; u < 24f; u += 1.2f) Prop(rm, stores[Kit.RI(0, stores.Length)], u, 27.6f, Kit.R(0, 360));
            Prop(rm, "R:SM_Prop_Crate_Stack_01", 40.6f, 26.5f, 0f);
            Prop(rm, "G:SM_Gen_Prop_Sack_Stack_01", 37.5f, 27.5f, 30f); Prop(rm, "G:SM_Gen_Prop_Sack_Stack_02", 38.6f, 26.2f, 80f);
            Prop(rm, "R:SM_Prop_Barrel_01", 40.8f, 18f); Prop(rm, "R:SM_Prop_Barrel_03", 41.2f, 16.8f, 40f);
            // the cooks
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Casual_Female_01", rm.W(28.3f, 23.4f), 15f, "Cook");
            Furnish.Defender(rm, "R:Chr_BR_Dwarf_Casual_Male_01", rm.W(27f, 15.7f), 0f, "Cook");
            // firelit: the hearth does the work, a lantern and a couple of low torches help
            Kit.PointLight(rm.Lights, rm.W(27f, 14.5f, 3.2f), Kit.Candle, 7f, 10f, "Lantern");
            Kit.PointLight(rm.Lights, rm.W(36f, 8f, 3.2f), Kit.Candle, 5f, 9f, "Lantern");
            Kit.PC("D:SM_Prop_Lantern_01", rm.Props, rm.W(34.9f, 12.4f, 0.91f), 0f, 1f);
            Kit.PC("D:SM_Prop_Lantern_01", rm.Props, rm.W(27.6f, 12.5f, 0.91f), 0f, 1f);
            Arch_.Torch(rm, rm.W(16.5f, 15f), Vector3.right, 2.4f, 4f, 7f, "D:SM_Prop_Torch_Ornate_02");
            Arch_.Torch(rm, rm.W(36f, 2.5f), Vector3.forward, 2.4f, 4f, 7f, "D:SM_Prop_Torch_Ornate_02");
        }
    }
}
