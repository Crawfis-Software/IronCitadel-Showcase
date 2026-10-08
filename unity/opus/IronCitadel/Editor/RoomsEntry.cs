using UnityEngine;

namespace IronCitadel.EditorTools
{
    public static partial class CitadelBuilder
    {
        // ------------------------------------------------------------------ entry hall [3,2]: the guard lobby
        static void BuildEntry(Room rm)
        {
            Shell(rm, Full);
            float H = rm.H;
            // two rows of pillars make a nave from the outer door to the gate
            foreach (float u in new[] { 13f, 32f })
                foreach (float v in new[] { 9f, 16f, 29f, 36f })
                {
                    Pillar(rm, u, v, H, "R:SM_Env_Dwarf_Pillar_01");
                    Kit.StripColliders(Prop(rm, "R:SM_Env_Dwarf_Pillar_Base_02", u, v, 0f, 0.8f));
                }
            // beams along the pillar lines
            foreach (float u in new[] { 13f, 32f })
                for (int i = 0; i < 8; i++)
                {
                    var go = Kit.Fit("R:SM_Env_Dwarf_Beam_02", rm.Ceil, rm.W(u - 0.4f, 2.5f + i * 5f, H - 0.9f), new Vector3(0.8f, 0.9f, 5f), 90f);
                    Kit.StripColliders(go); Kit.SetLayer(go, Kit.CeilingLayer);
                }

            // guard furniture: benches along the side walls, weapon stands by the doors, a duty table
            foreach (float v in new[] { 7f, 11f, 34f, 38f })
            {
                WallProp(rm, "D:SM_Prop_Bench_01", V(2.5f, v), Vector2.right);
                WallProp(rm, "D:SM_Prop_Bench_01", V(42.5f, v), Vector2.left);
            }
            foreach (float u in new[] { 8f, 11.5f, 33.5f, 37f })
            {
                var r = WallProp(rm, "R:SM_Prop_Dwarf_Weapon_Rack_04", V(u, 2.5f), Vector2.up);
            }
            foreach (float u in new[] { 6f, 39f })
            {
                WallProp(rm, "D:SM_Prop_WeaponRack_01", V(u, 42.5f), Vector2.down);
                WeaponsOnRack(rm, rm.W(u, 42.5f - 0.35f), Vector3.back);
            }
            // the guard sergeant's table
            var table = Prop(rm, "R:SM_Prop_Dwarf_Table_03", 26f, 21f, 90f);
            Furnish.TableTop(rm, table, new[] { "R:SM_Prop_Mug_01", "R:SM_Prop_Tankard_01", "M:SM_Prop_Papers_03", "R:SM_Prop_Goblet_01", "D:SM_Item_Coins_01" }, 7, 90f);
            Furnish.Candles(rm, table.transform.position + Vector3.up * 0.91f + Vector3.forward * 0.6f, 0.8f, 4f);
            foreach (var p in new[] { V(25f, 19.5f), V(27f, 19.5f), V(25f, 23f), V(27.2f, 22.6f) })
                Prop(rm, "R:SM_Prop_Dwarf_Chair_Stool_01", p.x, p.y, Kit.R(0, 360));
            var t2 = Prop(rm, "R:SM_Prop_Dwarf_Table_01", 18.5f, 24.5f, 0f);
            Furnish.TableTop(rm, t2, new[] { "R:SM_Prop_Mug_01", "G:SM_Gen_Prop_Food_Bread_01", "R:SM_Prop_Tankard_01" }, 3, 0f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", 18.5f, 23.3f, 0f);
            Prop(rm, "R:SM_Prop_Dwarf_Chair_Bench_01", 18.5f, 25.8f, 180f);
            // stores in the corners
            Prop(rm, "R:SM_Prop_Barrel_Stack_01", 6f, 41.5f, 180f);
            Prop(rm, "R:SM_Prop_Crate_Stack_01", 40.5f, 40.5f, 0f);
            Prop(rm, "R:SM_Prop_Barrel_01", 4f, 4f); Prop(rm, "R:SM_Prop_Barrel_02", 5.1f, 4.3f, 40f); Prop(rm, "R:SM_Prop_Crate_01", 41f, 4f, 15f);
            Prop(rm, "D:SM_Prop_Chest_03", 4f, 15f, 90f); Prop(rm, "D:SM_Prop_Chest_02", 41f, 30f, -90f);
            // braziers flank the nave
            Furnish.Brazier(rm, rm.W(18f, 31.5f), 10f, 11f, "R:SM_Prop_Dwarf_Brazier_01", 0.7f);
            Furnish.Brazier(rm, rm.W(27f, 31.5f), 10f, 11f, "R:SM_Prop_Dwarf_Brazier_01", 0.7f);
            // banners
            foreach (float u in new[] { 15f, 30f }) Banner(rm, V(u, 42.5f), Vector2.down, H - 0.3f, "R:SM_Prop_Banner_02", 0.95f);
            foreach (float v in new[] { 16f, 29f }) { Banner(rm, V(2.5f, v), Vector2.right, H - 0.3f, "R:SM_Prop_Banner_04", 0.85f); Banner(rm, V(42.5f, v), Vector2.left, H - 0.3f, "R:SM_Prop_Banner_04", 0.85f); }
            foreach (float u in new[] { 15f, 30f }) Banner(rm, V(u, 2.5f), Vector2.up, H - 0.3f, "R:SM_Prop_Banner_06", 1f);
            // light: torches round the walls, two chandeliers over the nave
            WallTorches(rm, Full, 7f, 3.0f, 9f, 11f, null, new[] { V(15f, 42.5f), V(30f, 42.5f), V(15f, 2.5f), V(30f, 2.5f), V(2.5f, 16f), V(2.5f, 29f), V(42.5f, 16f), V(42.5f, 29f) });
            Furnish.Chandelier(rm, rm.W(22.5f, 12f, H), 16f, 16f);
            Furnish.Chandelier(rm, rm.W(22.5f, 24f, H), 16f, 16f);
            Kit.PointLight(rm.Lights, rm.W(22.5f, 38.5f, 4.2f), Kit.Fire, 9f, 10f, "GateLight");
            Fill(rm, Full, 5.2f, 2.5f, 13f, 11f);
        }

        static void WeaponsOnRack(Room rm, Vector3 at, Vector3 facing)
        {
            var keys = new[] { "R:SM_Wep_Spear_01", "R:SM_Wep_Spear_03", "D:SM_Wep_Halberd_06", "R:SM_Wep_Spear_05", "D:SM_Wep_Ornate_Spear_01" };
            var side = Vector3.Cross(Vector3.up, facing).normalized;
            for (int i = 0; i < 5; i++)
            {
                var p = at + side * (-0.8f + i * 0.4f);
                var go = Kit.PCR(keys[i % keys.Length], rm.Props, p + Vector3.up * 1.0f,
                    Quaternion.LookRotation(facing) * Quaternion.Euler(-8f, 0, 0), 1f);
                Kit.StripColliders(go);
            }
        }

        // ------------------------------------------------------------------ doors that need more than a frame
        static void DoorDressing()
        {
            foreach (var d in Arch_.Doors)
            {
                if (d.Id == "armory_gate")
                {
                    // the portcullis, down, in the entry hall's face of the gate; spikes before it on the entry side
                    var face = d.FaceA;
                    var pc = Kit.P("D:SM_Env_Portcullis_01", d.Root, face + d.Axis * 0.25f + Vector3.up * 2.83f, 0f, 1f);
                    pc.name = "Portcullis (down)";
                    Kit.StripColliders(pc);
                    var block = Kit.Solid(d.Root, face + d.Axis * 0.25f + Vector3.up * 2.4f, new Vector3(4.6f, 4.8f, 0.5f), "PortcullisBlocker");
                    for (int i = 0; i < 4; i++)
                    {
                        var sp = Kit.PC("D:SM_Env_Trap_Spikes_01", d.Root, face - d.Axis * 1.7f + Vector3.right * (-2.25f + i * 1.5f), 0f, 1f);
                        Kit.StripColliders(sp);
                    }
                    Kit.Solid(d.Root, face - d.Axis * 1.7f + Vector3.up * 0.7f, new Vector3(6.2f, 1.4f, 1.6f), "SpikeRow");
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var log = Kit.PCR("D:SM_Prop_Log_Spike_02", d.Root, face - d.Axis * 2.9f + Vector3.right * s * 3.6f + Vector3.up * 0.75f,
                            Quaternion.Euler(-35f, s * 10f, 0), 1f);
                        Kit.StripColliders(log);
                    }
                }
                else if (d.Id == "outer_door")
                {
                    // the way in: shut behind the player
                    var c = d.Centre - d.Axis * 0.45f;
                    var l = Kit.P("D:SM_Env_Door_Large_Wood_01", d.Root, c + Vector3.right * 1.62f, 180f, 1f);
                    var r = Kit.P("D:SM_Env_Door_Large_Wood_02", d.Root, c - Vector3.right * 1.62f, 180f, 1f);
                    Kit.StripColliders(l); Kit.StripColliders(r);
                    var shut = Kit.Solid(d.Root, c + Vector3.up * 2.3f, new Vector3(3.6f, 4.6f, 0.6f), "OuterDoor (shut)");
                    shut.AddComponent<OuterDoor>();
                }
                else if (d.Id == "throne_doors")
                {
                    // great doors standing open against the tunnel walls
                    var c = d.FaceA + d.Axis * 0.6f;
                    var side = Vector3.Cross(Vector3.up, d.Axis).normalized;
                    var l = Kit.P("D:SM_Env_Door_Large_Wood_01", d.Root, c - side * (d.Width / 2f - 0.15f), Quaternion.Euler(0, 90f, 0), Vector3.one * 1.22f);
                    var r = Kit.P("D:SM_Env_Door_Large_Wood_02", d.Root, c + side * (d.Width / 2f - 0.15f), Quaternion.Euler(0, -90f, 0), Vector3.one * 1.22f);
                    Kit.StripColliders(l); Kit.StripColliders(r);
                }
            }
        }

    }
}
