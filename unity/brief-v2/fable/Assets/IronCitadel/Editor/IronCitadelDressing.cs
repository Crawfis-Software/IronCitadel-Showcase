using UnityEngine;

namespace IronCitadel.EditorTools
{
    /// <summary>Room dressing, lights and markers for the Iron Citadel. All coordinates are world metres.</summary>
    public static partial class IronCitadelBuilder
    {
        static Room cur;
        // bookcase meshes (Dungeon Map pack): local x -2.5..0, z 0..1.05; whether the shelves open toward local -z (front at z=0)
        const bool BookFrontAtZ0 = false; // measured: the book faces point to +z, so the front is the z=1.05 side

        // ---- helpers -------------------------------------------------------------
        static GameObject P(string path, float x, float z, float yaw, float s = 1f, float y = float.NaN, bool noCol = false)
        {
            var go = Place(path, new Vector3(x, float.IsNaN(y) ? cur.floorY : y, z), yaw, cur.props, s == 1f ? (Vector3?)null : Vector3.one * s);
            if (noCol) StripColliders(go);
            return go;
        }
        static GameObject PE(string path, float x, float y, float z, float ex, float ey, float ez, float s = 1f, bool noCol = false)
        {
            var go = PlaceR(path, new Vector3(x, y, z), new Vector3(ex, ey, ez), cur.props, s == 1f ? (Vector3?)null : Vector3.one * s);
            if (noCol) StripColliders(go);
            return go;
        }
        static Light L(float x, float y, float z, Color c, float range, float intensity, bool shadows = false, string name = "Light")
        {
            var go = new GameObject(name); go.transform.SetParent(cur.lights, false); go.transform.position = new Vector3(x, y, z);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None; l.shadowStrength = 0.8f;
            return l;
        }
        static readonly Color Warm = new Color(1f, 0.70f, 0.40f);
        static readonly Color Ember = new Color(1f, 0.35f, 0.12f);
        static readonly Color Cold = new Color(0.45f, 0.58f, 0.85f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        static readonly Color Candle = new Color(1f, 0.78f, 0.5f);

        /// <summary>A wall torch: (x,z) on the wall line, yaw = the wall's facing (into the room).</summary>
        static void Torch(float x, float z, float yaw, float y = 2.3f, float intensity = 3.6f, float range = 11f)
        {
            var f = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad));
            var p = new Vector3(x, cur.floorY - FLOOR + y, z) + f * 0.22f;
            var t = Place(Pr("SM_Prop_Torch_Ornate_01"), p, yaw, cur.props, null, "Torch");
            if (t != null) StripColliders(t);
            Place(Fx("FX_CandleFlame_01"), p + Vector3.up * 0.42f + f * 0.02f, 0f, cur.props, Vector3.one * 2.2f, "TorchFlame");
            L(p.x + f.x * 0.3f, p.y + 0.5f, p.z + f.z * 0.3f, Warm, range, intensity, false, "TorchLight");
        }
        static void Brazier(float x, float z, bool lit, float y = float.NaN, float range = 11f, float intensity = 3f, bool shadows = false)
        {
            float fy = float.IsNaN(y) ? cur.floorY : y;
            P(Pr("SM_Prop_Brazier_01"), x, z, Jit(180f), 1f, fy);
            if (!lit) return;
            Place(Fx("FX_Fire_01"), new Vector3(x, fy + 0.55f, z), 0f, cur.props, Vector3.one * 0.55f, "BrazierFire");
            L(x, fy + 1.3f, z, Warm, range, intensity, shadows, "BrazierLight");
        }
        static void CandleStand(float x, float z, float range = 6f, float intensity = 1.4f)
        {
            P(Pr("SM_Prop_Candle_Stand_01"), x, z, Jit(180f));
            L(x, cur.floorY + 1.85f, z, Candle, range, intensity, false, "CandleLight");
        }
        static void Candles(float x, float y, float z, int variant = 1, bool light = true)
        {
            P(Pr("SM_Prop_Candles_0" + variant), x, z, Jit(180f), 1f, y, true);
            if (light) L(x, y + 0.35f, z, Candle, 4f, 0.9f, false, "CandleLight");
        }
        static void Chandelier(float x, float y, float z, float range = 16f, float intensity = 5f, bool shadows = false, bool large = true)
        {
            var c = Place(Pr(large ? "SM_Prop_Candle_Chandelier_02" : "SM_Prop_Candle_Chandelier_01"), new Vector3(x, y, z), Jit(180f), cur.props, null, "Chandelier");
            float top = cur.floorY - FLOOR + cur.h;
            // chain from the ceiling down to the chandelier's hook
            float len = top - y;
            if (len > 0.2f) Place(Pr("SM_Prop_Chain_01"), new Vector3(x, top, z), 0f, cur.props, new Vector3(1f, len / 2.53f, 1f), "ChandelierChain");
            L(x, y - (large ? 1.9f : 1.5f), z, Warm, range, intensity, shadows, "ChandelierLight");
        }
        /// <summary>A corner cobweb (a 5 m corner piece at scale 1). corner: SE, SW, NW or NE of the room; (cx, cz) is the corner point.</summary>
        static void Web(float cx, float cz, string corner, float s = 1f, float y = float.NaN)
        {
            float yaw = corner == "SE" ? 0f : corner == "SW" ? 90f : corner == "NW" ? 180f : -90f;
            P(Re("SM_Env_SpiderWeb_0" + (1 + rng.Next(4))), cx, cz, yaw, s, y, true);
        }
        static void Rubble(float x, float z) { P(Ro("SM_Env_Brick_Rubble_0" + (1 + rng.Next(6))), x, z, Jit(180f), 1f, float.NaN, true); }
        static void Bones(float x, float z) { P(Bo(rng.Next(2) == 0 ? "SM_Env_BonePile_Small_01" : "SM_Env_BonePile_Small_02"), x, z, Jit(180f), 1f, float.NaN, true); }
        static void Banner(string prefab, float x, float y, float z, float yaw) { var b = Place(prefab, new Vector3(x, y, z), yaw, cur.props, null, "Banner"); if (b != null) StripColliders(b); }
        static void Column(float x, float z, int tiers) { for (int i = 0; i < tiers; i++) Place(Pi("SM_Env_Pillar_Square_06"), new Vector3(x, cur.floorY + i * 5f, z), 0f, cur.props, new Vector3(1f, 0.998f, 1f), "Column"); }
        static void Dust(float x, float y, float z, float s = 1f) { Place(Fx("FX_Dust_Large_01_Soft"), new Vector3(x, y, z), 0f, cur.props, Vector3.one * s, "Dust"); }

        static void DressAll()
        {
            foreach (var r in rooms.Values)
            {
                cur = r;
                switch (r.name)
                {
                    case "Entry hall": DressEntry(); break;
                    case "Armory": DressArmory(); break;
                    case "Forge": DressForge(); break;
                    case "Guard room": DressGuard(); break;
                    case "Prison": DressPrison(); break;
                    case "Torture chamber": DressTorture(); break;
                    case "Crypt": DressCrypt(); break;
                    case "Treasure vault": DressVault(); break;
                    case "Stair hall": DressStairHall(); break;
                    case "Dining hall": DressDining(); break;
                    case "Gallery": DressGallery(); break;
                    case "Kitchen": DressKitchen(); break;
                    case "Storeroom": DressStore(); break;
                    case "Library": DressLibrary(); break;
                    case "Alchemist's study": DressStudy(); break;
                    case "Shrine": DressShrine(); break;
                    case "Antechamber": DressAntechamber(); break;
                    case "Throne room": DressThrone(); break;
                }
            }
        }

        // ---- rooms ---------------------------------------------------------------
        static void DressEntry()
        {
            // a guard lobby, long deserted: benches, weapon stands, a table
            P(Pr("SM_Prop_Bench_01"), -13.6f, -3.0f, 90f);
            P(Pr("SM_Prop_Bench_01"), -1.4f, -4.5f, -90f);
            PE(Pr("SM_Prop_Bench_01"), -4.5f, FLOOR + 1.03f, -7.2f, 0f, 25f, 180f);
            var table = P(Pr("SM_Prop_Table_01"), -8.6f, -6.2f, 8f);
            P(Pr("SM_Prop_Candles_02"), -8.2f, -6.0f, 0f, 1f, FLOOR + 0.96f, true);
            L(-8.2f, FLOOR + 1.3f, -6.0f, Candle, 5f, 1.1f);
            P(Rp("SM_Prop_Tankard_01"), -9.3f, -6.6f, 40f, 1f, FLOOR + 0.96f, true);
            P(Rp("SM_Prop_Mug_01"), -7.6f, -5.9f, 10f, 1f, FLOOR + 0.96f, true);
            P(It("SM_Item_Book_01"), -9.6f, -5.6f, 70f, 1f, FLOOR + 0.96f, true);
            P(Pr("SM_Prop_Stool_01"), -10.4f, -6.9f, 20f);
            P(Pr("SM_Prop_Stool_02"), -7.0f, -7.3f, -30f);
            PE(Pr("SM_Prop_Stool_03"), -10.1f, FLOOR + 0.3f, -4.9f, 90f, 30f, 0f);
            P(Pr("SM_Prop_WeaponRack_01"), -14.6f, -2.0f, 90f);
            P(Pr("SM_Prop_WeaponRack_01"), -13.0f, -9.6f, 0f);
            P(Mp("SM_Prop_KnightStand_01"), -12.2f, -0.9f, 180f);
            P(Mp("SM_Prop_KnightStand_Broken_01"), -2.8f, -0.9f, 180f);
            P(Mp("SM_Prop_KnightStand_01"), -13.6f, -5.6f, 120f);   // half screens the side door
            P(Pr("SM_Prop_Rug_02"), -7.5f, -2.6f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Vase_03"), -0.6f, -0.7f, 0f);
            P(Pr("SM_Prop_Vase_Broken_01"), -1.1f, -9.3f, 30f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Chest_Wood_01"), -14.3f, -9.3f, 60f);
            P(Pr("SM_Prop_Barrel_02"), -0.6f, -8.0f, 0f);
            Rubble(-13.9f, -0.6f); Bones(-0.8f, -5.9f);
            Web(-15f, 0f, "NW", 0.9f); Web(0f, -10f, "SE", 0.9f);
            Dust(-7.5f, 2.5f, -5f, 1.5f);
            Torch(-11f, -10f, 0f); Torch(-4f, -10f, 0f);
            Torch(-12.4f, 0f, 180f); Torch(-2.6f, 0f, 180f);
            Torch(-15f, -3f, 90f); Torch(0f, -7f, -90f);
        }

        static void DressArmory()
        {
            // dense rows of racks and armour stands, still stocked. The centre aisle (x -8.5..-6.5) and the strip z 5.6..8.4 stay clear.
            float[] zs = { 2.0f, 4.3f, 9.7f, 12.0f, 14.1f };
            for (int i = 0; i < zs.Length; i++)
            {
                bool rack = i % 2 == 0;
                P(rack ? Pr("SM_Prop_WeaponRack_01") : Mp("SM_Prop_KnightStand_01"), -13.9f, zs[i], 90f);
                P(rack ? Mp("SM_Prop_KnightStand_01") : Pr("SM_Prop_WeaponRack_01"), -11.0f, zs[i], -90f);
                P(rack ? Mp("SM_Prop_KnightStand_01") : Pr("SM_Prop_WeaponRack_01"), -5.0f, zs[i], 90f);
                P(rack ? Pr("SM_Prop_WeaponRack_01") : Mp("SM_Prop_KnightStand_01"), -2.0f, zs[i], -90f);
            }
            // the best gear: a royal stand at the head of the centre aisle
            P(Mp("SM_Prop_KnightStand_Royal_01"), -7.5f, 12.2f, 180f);
            P(Pr("SM_Prop_WeaponRack_01"), -9.6f, 13.3f, 150f); P(Pr("SM_Prop_WeaponRack_01"), -5.4f, 13.3f, 210f);
            P(Rp("SM_Prop_Dwarf_Weapon_Barrel_01"), -14.4f, 0.8f, 0f); P(Rp("SM_Prop_Dwarf_Weapon_Barrel_02"), -0.7f, 0.8f, 0f);
            P(Rp("SM_Prop_Dwarf_Weapon_Barrel_01"), -14.4f, 14.3f, 0f); P(Rp("SM_Prop_Dwarf_Weapon_Barrel_02"), -0.7f, 14.3f, 0f);
            P(Pr("SM_Prop_Crate_Metal_01"), -14.2f, 7.0f, 15f); P(Pr("SM_Prop_Crate_Metal_02"), -14.2f, 8.2f, -10f, 1f, FLOOR + 1.1f);
            P(Pr("SM_Prop_Crate_Metal_03"), -0.8f, 11.0f, 0f);
            // shields and weapons on the walls
            for (int i = 0; i < 3; i++)
            {
                var s = Place(Wp(i == 1 ? "SM_Wep_Shield_Round_01" : "SM_Wep_Shield_Heater_01"), new Vector3(-14.75f, 2.4f, 3f + i * 4f), 90f, cur.props, null, "WallShield"); if (s != null) StripColliders(s);
                var h = Place(Rp("SM_Prop_Hanging_Weapon_0" + (1 + i)), new Vector3(-0.25f, 2.3f, 3.5f + i * 4f), -90f, cur.props, null, "WallWeapon"); if (h != null) StripColliders(h);
            }
            Chandelier(-7.5f, 4.9f, 7.5f, 14f, 3f, true, false);
            Torch(-15f, 4f, 90f); Torch(-15f, 11f, 90f); Torch(0f, 4f, -90f); Torch(0f, 11f, -90f);
            Torch(-12.4f, 15f, 180f); Torch(-2.6f, 15f, 180f);
        }

        static void DressForge()
        {
            P(Pr("SM_Prop_Forge_FirePit_01"), -7.5f, 22.4f, 0f);
            Place(Pr("SM_Prop_Forge_Ventilation_01"), new Vector3(-7.5f, FLOOR + 2.75f, 22.4f), 0f, cur.props, null, "ForgeHood");
            Place(Pr("SM_Prop_Forge_Ventilation_Pipe_01"), new Vector3(-7.5f, FLOOR + 3.55f, 22.4f), 0f, cur.props, null, "ForgeFlue");
            L(-7.5f, FLOOR + 0.9f, 22.4f, Ember, 7f, 1.3f, false, "EmberGlow");
            P(Rp("SM_Prop_Anvil_01"), -10.3f, 20.6f, 20f); P(Rp("SM_Prop_Anvil_01"), -4.6f, 20.8f, -15f);
            P(Pr("SM_Prop_Barrel_01"), -3.0f, 23.6f, 0f); P(Pr("SM_Prop_Barrel_03"), -2.1f, 22.5f, 30f); P(Pr("SM_Prop_Barrel_01"), -12.6f, 23.6f, 0f);
            P(Pr("SM_Prop_Barrel_Broken_01"), -12.0f, 17.0f, 40f);
            P(Pr("SM_Prop_Table_01"), -13.4f, 19.6f, 90f);
            P(Rp("SM_Prop_Forge_Hammer_01"), -13.4f, 19.0f, 30f, 1f, FLOOR + 0.96f, true);
            P(Rp("SM_Prop_Forge_Tongs_01"), -13.2f, 20.3f, -60f, 1f, FLOOR + 0.96f, true);
            P(Rp("SM_Prop_Caliper_01"), -13.6f, 19.8f, 10f, 1f, FLOOR + 0.96f, true);
            PE(Rp("SM_Prop_Forge_Hammer_02"), -9.6f, FLOOR + 0.05f, 19.4f, 90f, 40f, 0f, 1f, true);
            PE(Rp("SM_Prop_Forge_Pickaxe_01"), -14.6f, FLOOR + 0.6f, 22.8f, -70f, 0f, 20f, 1f, true);
            PE(Rp("SM_Prop_Forge_Spade_01"), -1.2f, FLOOR + 0.05f, 18.2f, 90f, -20f, 0f, 1f, true);
            P(Pr("SM_Prop_WeaponRack_01"), -0.4f, 19.5f, -90f);
            P(Pr("SM_Prop_Crate_Wood_02"), -1.0f, 16.0f, 10f); P(Pr("SM_Prop_Weight_01"), -11.4f, 23.0f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Cauldron_01"), -4.0f, 16.4f, 0f);
            P(Gp("SM_Gen_Prop_Sack_Stack_01"), -14.2f, 16.0f, 0f);
            Place(Pr("SM_Prop_Chain_03"), new Vector3(-5f, FLOOR + 4.9f, 18.5f), 0f, cur.props, null, "HangingChain");
            Rubble(-13.8f, 24.3f); Rubble(-1.0f, 24.4f);
            Web(-15f, 25f, "NW", 0.9f);
            Torch(-15f, 20f, 90f, 2.3f, 2f); Torch(0f, 21.5f, -90f, 2.3f, 2f); Torch(-12.5f, 15f, 180f, 2.3f, 2f);
            Marker("melee", "Forge", new Vector3(-11.5f, FLOOR, 18.0f), 60f);
        }

        static void DressGuard()
        {
            // a guard post wrecked in a brawl, low and dim. The lane from the entry door (east, z -7.5) west to x -21 and north to the prison door stays clear.
            P(Pr("SM_Prop_Table_Round_Broken_01"), -18.5f, -3.4f, 15f);
            PE(Pr("SM_Prop_Stool_01"), -17.4f, FLOOR + 0.3f, -4.3f, 90f, 30f, 0f);
            PE(Pr("SM_Prop_Stool_02"), -17.8f, FLOOR + 0.3f, -5.6f, -90f, 60f, 0f);
            P(Pr("SM_Prop_Stool_01"), -19.6f, -2.6f, 40f);
            P(Pr("SM_Prop_Bench_01"), -24.4f, -1.2f, 90f);
            P(Pr("SM_Prop_Bed_01"), -18.3f, -1.0f, 90f);
            PE(Pr("SM_Prop_Bed_02"), -24.0f, FLOOR + 1.2f, -6.0f, 0f, 100f, 180f);
            P(Pr("SM_Prop_WeaponRack_01"), -15.4f, -2.6f, -90f);
            P(Pr("SM_Prop_Chest_Wood_01"), -24.3f, -3.6f, 90f);
            P(Pr("SM_Prop_Barrel_Broken_02"), -16.2f, -9.2f, 20f);
            P(Pr("SM_Prop_Barrel_01"), -24.2f, -9.3f, 0f);
            PE(Pr("SM_Prop_Skeleton_01"), -18.2f, FLOOR, -8.3f, 0f, 40f, 0f, 1f, true);
            PE(Wp("SM_Wep_BrokenSword_01"), -19.4f, FLOOR + 0.05f, -8.9f, 90f, 10f, 0f, 1f, true);
            P(Pr("SM_Prop_Vase_Shard_02"), -21.5f, -8.0f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Vase_Broken_02"), -22.6f, -8.6f, 0f, 1f, float.NaN, true);
            P(Mp("SM_Prop_Papers_06"), -20.4f, -7.2f, 20f, 1f, FLOOR + 0.01f, true);
            P(Pr("SM_Prop_Lantern_01"), -18.9f, -3.9f, 0f, 1f, FLOOR + 0.55f, true);
            L(-18.9f, FLOOR + 1.0f, -3.9f, Candle, 7f, 1.5f);
            Rubble(-24.3f, -0.6f);
            Web(-25f, -10f, "SW", 0.55f);
            Torch(-20f, -10f, 0f, 2.0f, 1.6f, 7f);
            CandleStand(-16.3f, -5.0f, 6f, 1.2f);
        }

        static void DressPrison()
        {
            // cells on both long sides: east x -18..-15 (z 0..12), west x -25..-22 (z 3..12). Corridor x -22..-18. Dark.
            float[] cellZ = { 0f, 3f, 6f, 9f };
            for (int i = 0; i < 4; i++)
            {
                float z0 = cellZ[i];
                // east cells
                FenceX(-18f, -15f, z0 + 3f);                 // partition at the cell's north edge
                if (i == 0) FenceX(-18f, -15f, 0.05f);        // (south edge is the room wall; a short fence stub for the first)
                CellFront(-18f, z0, 30f + i * 35f, i == 2);
                // west cells (none at z 0..3: the door from the guard room is there)
                if (i > 0)
                {
                    FenceX(-25f, -22f, z0 + 3f);
                    if (i == 1) FenceX(-25f, -22f, 3f);
                    CellFront(-22f, z0, i == 3 ? 35f : 25f, false, true);
                }
            }
            // cell contents
            PE(Pr("SM_Prop_Bed_01"), -16.3f, FLOOR + 1.2f, 1.4f, 0f, 90f, 180f);
            P(Pr("SM_Prop_Bed_01"), -16.3f, 4.6f, 90f);
            P(Rp("SM_Prop_Camp_Roll_01"), -16.4f, 7.6f, 90f, 1f, float.NaN, true);
            P(Rp("SM_Prop_Camp_Roll_02"), -16.2f, 10.6f, 80f, 1f, float.NaN, true);
            PE(Pr("SM_Prop_Skeleton_01"), -16.0f, FLOOR, 10.2f, 0f, 120f, 0f, 1f, true);
            var sh = Place(Pr("SM_Prop_Skeleton_Slave_Shackles_01"), new Vector3(-15.2f, FLOOR, 7.3f), -90f, cur.props, null, "Shackled"); if (sh != null) StripColliders(sh);
            var sh2 = Place(Pr("SM_Prop_Skeleton_Slave_Wall_Sitting_01"), new Vector3(-24.4f, FLOOR, 10.4f), 90f, cur.props, null, "Sitting"); if (sh2 != null) StripColliders(sh2);
            P(Rp("SM_Prop_Camp_Roll_03"), -23.6f, 4.5f, 10f, 1f, float.NaN, true);
            PE(Pr("SM_Prop_Stool_01"), -23.5f, FLOOR + 0.3f, 7.5f, 90f, 0f, 0f);
            Bones(-23.8f, 8.4f); Bones(-16.5f, 2.6f); Bones(-15.6f, 11.4f);
            P(Pr("SM_Prop_Vase_Broken_03"), -23.4f, 11.2f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Chain_05"), -24.8f, 9.0f, 90f, 1f, FLOOR + 1.6f, true);
            // corridor and the jailer's corner (north-west, beside the passage to the torture chamber)
            P(Pr("SM_Prop_Table_01"), -24.0f, 13.2f, 90f);
            P(Mp("SM_Prop_Papers_03"), -24.0f, 13.0f, 20f, 1f, FLOOR + 0.96f, true);
            P(Pr("SM_Prop_Lantern_01"), -24.1f, 14.0f, 0f, 1f, FLOOR + 0.96f, true);
            L(-23.6f, FLOOR + 1.5f, 14.0f, Candle, 7f, 1.4f);
            P(Pr("SM_Prop_Toture_Cage_01"), -24.3f, 1.2f, 0f);
            PE(Pr("SM_Prop_Stool_02"), -19.2f, FLOOR + 0.3f, 2.0f, -90f, 20f, 0f);
            P(Pr("SM_Prop_Barrel_Broken_01"), -21.6f, 14.5f, 0f);
            Rubble(-19.2f, 14.4f);
            L(-20f, FLOOR + 2.4f, 7f, Cold, 13f, 0.55f, false, "ColdFill");
            P(Pr("SM_Prop_Lantern_02"), -18.2f, 6.0f, 0f, 1f, FLOOR + 2.2f, true);
            L(-18.6f, FLOOR + 2.2f, 6.0f, Candle, 6f, 1.0f);
            Web(-25f, 15f, "NW", 0.55f); Web(-15f, 15f, "NE", 0.55f);
            Marker("melee", "Prison", new Vector3(-22.3f, FLOOR, 13.8f), -90f);
        }

        /// <summary>A 3 m run of iron fence along x from xa to xb at z.</summary>
        static void FenceX(float xa, float xb, float z)
        {
            float len = Mathf.Abs(xb - xa); float cx = (xa + xb) / 2f;
            Place(Pr("SM_Prop_Metal_Fence_01"), new Vector3(cx, cur.floorY, z), 0f, cur.props, new Vector3(len / 2.5f, 1f, 1f), "CellFence");
            Place(Pr("SM_Prop_Metal_Fence_Post_01"), new Vector3(xa, cur.floorY, z), 0f, cur.props, null, "FencePost");
            Place(Pr("SM_Prop_Metal_Fence_Post_01"), new Vector3(xb, cur.floorY, z), 0f, cur.props, null, "FencePost");
        }
        /// <summary>A cell front at x spanning z0..z0+3: a barred door (1.35) hanging open plus a fence panel. west: the cell lies on the -x side.</summary>
        static void CellFront(float x, float z0, float doorAngle, bool doorOnFloor, bool west = false)
        {
            float sign = west ? -1f : 1f;
            // fence panel 1.65 long from z0+1.35 to z0+3
            Place(Pr("SM_Prop_Metal_Fence_01"), new Vector3(x, cur.floorY, z0 + 2.175f), 90f, cur.props, new Vector3(1.65f / 2.5f, 1f, 1f), "CellFence");
            Place(Pr("SM_Prop_Metal_Fence_Post_01"), new Vector3(x, cur.floorY, z0 + 1.35f), 0f, cur.props, null, "FencePost");
            if (doorOnFloor)
            {
                PlaceR(W("SM_Env_Door_Bars_01"), new Vector3(x + sign * 0.6f, cur.floorY + 0.12f, z0 + 0.2f), new Vector3(90f, 90f + 25f, 0f), cur.props, null, "CellDoorFallen");
                return;
            }
            // hinge at the cell's south end. The leaf's panel runs toward its local -x; yaw 90 lays it along +z (closed, on the front line).
            // Opening swings it into the corridor: toward -x for east cells (yaw 90 - angle), toward +x for west cells (yaw 90 + angle).
            var hinge = new GameObject("CellDoor"); hinge.transform.SetParent(cur.props); hinge.transform.position = new Vector3(x, cur.floorY, z0 + 0.0f);
            hinge.transform.rotation = Quaternion.Euler(0f, 90f - sign * doorAngle, 0f);
            Place(W("SM_Env_Door_Bars_01"), hinge.transform.position, 0f, hinge.transform, null, "CellDoorMesh").transform.localRotation = Quaternion.identity;
        }

        static void DressTorture()
        {
            // low and dark; the way runs diagonally from the prison door (-17.5, 15) to the crypt door (-22.5, 25)
            P(Pr("SM_Prop_Toture_StretchTable_01"), -23.0f, 18.6f, 0f);
            P(Pr("SM_Prop_Toture_Cage_01"), -16.2f, 21.3f, 0f); P(Pr("SM_Prop_Toture_Cage_01"), -16.2f, 23.6f, 20f);
            P(Pr("SM_Prop_Toture_IronMaiden_01"), -15.9f, 17.6f, -90f);
            P(Pr("SM_Prop_Toture_Stocks_01"), -18.6f, 23.6f, 30f);
            Brazier(-20.6f, 16.8f, false);
            P(Pr("SM_Prop_Weight_01"), -24.3f, 21.0f, 0f, 1f, float.NaN, true);
            Place(Pr("SM_Prop_Chain_02"), new Vector3(-19.5f, FLOOR + 2.85f, 20.0f), 0f, cur.props, null, "HangingChain");
            Place(Pr("SM_Prop_Chain_04"), new Vector3(-21.5f, FLOOR + 2.85f, 22.5f), 40f, cur.props, null, "HangingChain");
            var sh = Place(Pr("SM_Prop_Skeleton_Slave_Shackles_02"), new Vector3(-15.2f, FLOOR, 19.8f), -90f, cur.props, null, "Shackled"); if (sh != null) StripColliders(sh);
            PE(Pr("SM_Prop_Skeleton_Slave_Lying_01"), -23.6f, FLOOR, 23.2f, 0f, 70f, 0f, 1f, true);
            P(Bo("SM_Env_BonePile_03"), -24.0f, 21.8f, 0f, 0.8f, float.NaN, true);
            P(Gp("SM_Gen_Prop_Skull_01"), -17.0f, 16.2f, 30f, 1f, float.NaN, true);
            PE(Wp("SM_Wep_Goblin_Shiv_Bone_01"), -22.4f, FLOOR + 1.0f, 17.0f, 0f, 30f, 90f, 1f, true);
            P(Pr("SM_Prop_Barrel_03"), -24.2f, 15.8f, 0f);
            Rubble(-15.8f, 24.4f);
            Web(-25f, 25f, "NW", 0.55f); Web(-15f, 15f, "SE", 0.55f);
            CandleStand(-21.6f, 16.0f, 6f, 1.1f);
            L(-16.5f, FLOOR + 2.4f, 22.5f, Cold, 9f, 0.6f, false, "ColdFill");
            L(-20f, FLOOR + 2.5f, 21f, new Color(0.7f, 0.45f, 0.3f), 10f, 0.5f, false, "DimFill");
            Marker("melee", "Torture chamber", new Vector3(-24.2f, FLOOR, 23.6f), 120f);
        }

        static void DressCrypt()
        {
            // tombs and niches broken open; the way runs along z 26..28.5 from the torture door to the stair hall door, past the hidden door at (-25, 27.5)
            float[] xs = { -26f, -22f, -18f };
            for (int i = 0; i < 3; i++)
            {
                P(Mp(i == 1 ? "SM_Prop_Tomb_Royal_01" : "SM_Prop_Tomb_01"), xs[i], 31.2f, 0f);
                if (i == 0) P(Mp("SM_Prop_Tomb_Skeleton_01"), xs[i], 31.2f, 0f, 1f, float.NaN, true);
                if (i == 2) PE(Mp("SM_Prop_Tomb_Lid_01"), xs[i] + 0.9f, FLOOR + 0.3f, 30.6f, 0f, 20f, 70f);
                else PE(Mp(i == 1 ? "SM_Prop_Tomb_Royal_Lid_01" : "SM_Prop_Tomb_Lid_01"), xs[i] + 0.3f, FLOOR + 0.63f, 31.6f, 8f, 18f, 0f);
            }
            P(Rp("SM_Prop_Sarcophagus_01"), -28.6f, 33.1f, 90f);
            P(Pr("SM_Prop_Coffin_01"), -16.4f, 33.4f, 25f);
            PE(Pr("SM_Prop_Coffin_01"), -28.4f, FLOOR + 0.4f, 29.9f, 0f, 60f, 25f);
            // niches in the north wall
            for (int i = 0; i < 3; i++)
            {
                float x = -27.5f + i * 5f;
                if (i != 1) PE(Pr("SM_Prop_Skeleton_01"), x, FLOOR, 35.7f, 0f, 90f, 0f, 1f, true);
                else P(Pr("SM_Prop_Coffin_01"), x, 35.8f, 90f);
                Candles(x - 1.4f, FLOOR, 35.9f, 3, i == 0); Candles(x + 1.3f, FLOOR, 35.9f, 1, i == 2);
            }
            P(Bo("SM_Env_BonePile_Small_01"), -16.6f, 26.0f, 0f, 1f, float.NaN, true);
            P(Bo("SM_Env_BonePile_03"), -29.1f, 34.3f, 0f, 0.8f, float.NaN, true);
            P(It("SM_Item_Bones"), -23.5f, 29.6f, 40f, 1f, float.NaN, true);
            P(Bo("SM_Env_BonePile_Small_02"), -19.8f, 33.6f, 110f, 1f, float.NaN, true);
            P(Gp("SM_Gen_Prop_Skull_01"), -24.2f, 29.9f, 70f, 1f, float.NaN, true);
            Rubble(-19.2f, 25.8f); P(Pi("SM_Env_Pillar_Broken_Pile_01"), -28.8f, 25.9f, 0f, 1f, float.NaN, true);
            P(Pi("SM_Env_Pillar_Broken_01"), -15.9f, 30.4f, 0f);
            P(Mi("SM_Env_Stone_Lantern_01"), -25.0f, 25.6f, 0f); L(-25.0f, FLOOR + 1.7f, 25.9f, Candle, 7f, 1.3f);
            P(Mi("SM_Env_Stone_Lantern_01"), -17.4f, 29.6f, 0f); L(-17.4f, FLOOR + 1.7f, 29.6f, Candle, 7f, 1.3f);
            L(-22.5f, FLOOR + 2.5f, 30.5f, Cold, 15f, 0.55f, false, "ColdFill");
            L(-27.5f, FLOOR + 2.4f, 29.5f, Cold, 12f, 1.2f, false, "ColdFillWest");
            Candles(-28.6f, FLOOR + 1.46f, 33.1f, 2, true);
            Web(-30f, 35f, "NW", 0.55f); Web(-15f, 35f, "NE", 0.55f); Web(-15f, 25f, "SE", 0.5f);
            Dust(-22.5f, 1.5f, 30f, 1.2f);
        }

        static void DressVault()
        {
            // small, low, sealed; the hoard untouched. Keep x -31.5..-30 near z 27.5 clear for the hidden door.
            P(Rp("SM_Prop_Dwarf_TreasureChest_01"), -33.0f, 29.1f, 180f, 0.6f);
            P(Pr("SM_Prop_Chest_01"), -34.4f, 26.0f, 90f); P(Pr("SM_Prop_Chest_03"), -34.4f, 27.4f, 90f);
            P(Pr("SM_Prop_Chest_02"), -31.0f, 29.4f, 180f); P(Pr("SM_Prop_Chest_04"), -30.9f, 25.7f, -90f);
            P(Pr("SM_Prop_Crate_Ornate_01"), -34.3f, 29.3f, 20f);
            P(Rp("SM_Prop_Gold_Pile_01"), -32.6f, 27.3f, 30f, 0.5f, float.NaN, true);
            P(Rp("SM_Prop_Gold_Coins_03"), -31.8f, 26.4f, 0f, 1f, float.NaN, true); P(Rp("SM_Prop_Gold_Coins_05"), -33.6f, 28.2f, 40f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Jewel_02"), -31.6f, 28.6f, 0f, 1f, float.NaN, true); P(Pr("SM_Prop_Jewel_05"), -33.8f, 25.9f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Vase_09"), -34.4f, 25.5f, 0f); P(Pr("SM_Prop_Vase_08"), -30.7f, 29.4f, 0f);
            P(Rp("SM_Prop_Goblet_01"), -32.0f, 25.8f, 0f, 1f, float.NaN, true); P(Rp("SM_Prop_Goblet_02"), -33.3f, 29.0f, 0f, 1f, FLOOR + 0.8f, true);
            P(Pr("SM_Prop_Gem_02"), -32.4f, 28.9f, 0f, 1f, FLOOR + 0.45f, true);
            var sword = Place(Wp("SM_Wep_Ornate_Sword_01"), new Vector3(-32.2f, FLOOR + 0.35f, 27.1f), 60f, cur.props, null, "HoardSword"); if (sword != null) StripColliders(sword);
            L(-32.5f, FLOOR + 2.2f, 27.5f, Gold, 8f, 1.6f, false, "VaultGlow");
            Web(-35f, 30f, "NW", 0.5f);
        }

        static void DressStairHall()
        {
            Torch(-12.5f, 25f, 0f); Torch(-4.5f, 25f, 0f); Torch(-10.5f, 25f, 0f, 2.3f, 2.4f);
            Torch(-15f, 33f, 90f, 4.0f, 2.6f, 10f);
            Torch(0f, 30.2f, -90f, 7.2f, 2.4f, 10f);
            Banner(Rp("SM_Prop_Banner_Damaged_02"), -5f, 9.7f, 25.25f, 0f); Banner(Rp("SM_Prop_Banner_Damaged_04"), -11f, 9.7f, 25.25f, 0f);
            P(Mi("SM_Env_Statue_04"), -1.6f, 25.9f, -60f);
            Rubble(-8.9f, 25.7f); P(Pr("SM_Prop_Vase_04"), -14.6f, 34.4f, 0f);
            P(Pr("SM_Prop_Barrel_02"), -13.6f, 25.9f, 0f);
            Web(-15f, 25f, "SW", 1.0f); Web(0f, 35f, "NE", 0.9f, FLOOR + 5f); Web(-15f, 35f, "NW", 0.5f);
            Dust(-7f, 4f, 30f, 1.8f);
        }

        static void DressDining()
        {
            // two long rows of tables with a cross-aisle at z 14.5..18.5, a head table at the north end
            float[] rowX = { 9.0f, 15.0f };
            float[] zs = { 7.0f, 10.0f, 13.0f, 20.0f, 23.0f, 26.0f, 29.0f };
            int k = 0;
            foreach (float rx in rowX)
                foreach (float z in zs)
                {
                    k++;
                    if (k == 3) PE(Pr("SM_Prop_Table_01"), rx + 0.4f, FLOOR + 0.96f, z, 0f, 105f, 180f);
                    else if (k == 12) PE(Pr("SM_Prop_Table_01"), rx - 0.6f, FLOOR + 0.70f, z, 0f, 90f, 90f);
                    else
                    {
                        P(Pr("SM_Prop_Table_01"), rx, z, 90f + Jit(3f));
                        if (k % 3 == 0) P(Pr("SM_Prop_Candles_01"), rx + Jit(0.3f), z + Jit(0.8f), 0f, 1f, FLOOR + 0.96f, true);
                        if (k % 2 == 0) P(Rp("SM_Prop_Tankard_01"), rx + Jit(0.5f), z + Jit(1.0f), Jit(180f), 1f, FLOOR + 0.96f, true);
                        if (k % 4 == 1) P(Rp("SM_Prop_Goblet_02"), rx + Jit(0.5f), z + Jit(1.0f), 0f, 1f, FLOOR + 0.96f, true);
                        if (k == 5) P(Gp("SM_Gen_Prop_Plate_01"), rx, z + 0.4f, 0f, 1f, FLOOR + 0.96f, true);
                        if (k == 9) P(Gp("SM_Gen_Prop_Food_Bread_01"), rx - 0.3f, z - 0.5f, 30f, 1f, FLOOR + 0.96f, true);
                    }
                    // benches on both sides, a few knocked over
                    if (k % 5 == 2) PE(Pr("SM_Prop_Bench_01"), rx - 1.5f, FLOOR + 0.5f, z + 0.3f, 0f, 90f, 90f);
                    else P(Pr("SM_Prop_Bench_01"), rx - 1.25f, z, 90f + Jit(4f));
                    if (k % 7 == 4) PE(Pr("SM_Prop_Bench_01"), rx + 1.6f, FLOOR + 1.03f, z - 0.4f, 0f, 100f, 180f);
                    else P(Pr("SM_Prop_Bench_01"), rx + 1.25f, z, -90f + Jit(4f));
                }
            // head table across the north end
            for (int i = 0; i < 3; i++) P(Pr("SM_Prop_Table_01"), 9.0f + i * 3f, 32.6f, 0f);
            P(Pr("SM_Prop_StoneChair_01"), 12.0f, 33.6f, 180f); P(Rp("SM_Prop_Chair_01"), 9.5f, 33.6f, 180f); PE(Rp("SM_Prop_Chair_Broken_01"), 14.8f, FLOOR, 33.7f, 0f, 160f, 0f);
            P(Pr("SM_Prop_Candles_02"), 12.0f, 32.6f, 0f, 1f, FLOOR + 0.96f, true); L(12.0f, FLOOR + 1.3f, 32.6f, Candle, 5f, 1.2f);
            P(Rp("SM_Prop_Goblet_01"), 10.2f, 32.4f, 0f, 1f, FLOOR + 0.96f, true); P(Pr("SM_Prop_Vase_05"), 14.0f, 32.8f, 0f, 1f, FLOOR + 0.96f);
            // debris
            PE(Rp("SM_Prop_Chair_Broken_01"), 6.5f, FLOOR, 12.0f, 0f, 40f, 0f); PE(Rp("SM_Prop_Chair_Broken_01"), 17.8f, FLOOR, 24.5f, 0f, -70f, 0f);
            P(Pr("SM_Prop_Barrel_01"), 18.9f, 1.0f, 0f); P(Pr("SM_Prop_Barrel_Broken_01"), 17.8f, 1.2f, 30f);
            P(Pr("SM_Prop_Crate_Wood_02"), 19.0f, 4.2f, 15f);
            P(Pr("SM_Prop_Vase_Broken_01"), 7.4f, 31.0f, 0f, 1f, float.NaN, true);
            PE(Pr("SM_Prop_Skeleton_01"), 12.2f, FLOOR, 16.0f, 0f, 75f, 0f, 1f, true);
            P(Ro("SM_Env_Rubble_01"), 18.6f, 33.5f, 0f, 1f, float.NaN, true); Rubble(1.0f, 34.3f); Rubble(18.8f, 12.0f);
            P(Mp("SM_Prop_Papers_07"), 12.0f, 17.2f, 30f, 1f, FLOOR + 0.01f, true);
            // columns along the east wall and the arcade pillars already under the deck
            Column(18.9f, 10f, 2); Column(18.9f, 20f, 2); Column(18.9f, 30f, 2);
            // chandeliers and banners
            Chandelier(12f, 8.4f, 10f, 20f, 6f, true); Chandelier(12f, 8.4f, 22f, 20f, 6f, false); Chandelier(12f, 8.4f, 31f, 18f, 5f, false);
            for (int i = 0; i < 4; i++) Banner(Rp("SM_Prop_Banner_Damaged_0" + (1 + i)), 19.75f, 9.6f, 5f + i * 7.5f, -90f);
            Banner(Rp("SM_Prop_Banner_Damaged_05"), 6f, 9.6f, 34.75f, 180f); Banner(Rp("SM_Prop_Banner_Damaged_01"), 15f, 9.6f, 34.75f, 180f);
            Banner(Pr("SM_Prop_Wall_Banner_01"), 10.5f, 8.8f, 34.75f, 180f);
            // torches under the gallery and along the east wall
            Torch(0f, 3f, 90f); Torch(0f, 12.5f, 90f); Torch(0f, 20f, 90f); Torch(0f, 25f, 90f);
            Torch(20f, 3f, -90f); Torch(20f, 12.5f, -90f); Torch(20f, 22.5f, -90f); Torch(20f, 31f, -90f);
            Torch(10f, 0f, 0f); Torch(15f, 0f, 0f);
            Web(20f, 35f, "NE", 1.3f); Web(0f, 0f, "SW", 0.9f); Web(20f, 0f, "SE", 1.0f);
            Dust(10f, 4f, 10f, 2.5f); Dust(10f, 4f, 25f, 2.5f);
            // the largest group of defenders
            Marker("melee", "Dining hall", new Vector3(8.0f, FLOOR, 4.5f), 0f);
            Marker("melee", "Dining hall", new Vector3(12.0f, FLOOR, 11.5f), -90f);
            Marker("melee", "Dining hall", new Vector3(17.0f, FLOOR, 8.5f), 180f);
            Marker("melee", "Dining hall", new Vector3(12.0f, FLOOR, 21.0f), 90f);
            Marker("melee", "Dining hall", new Vector3(7.5f, FLOOR, 30.4f), 0f);
            Marker("melee", "Dining hall", new Vector3(17.0f, FLOOR, 27.5f), 180f);
            Marker("melee", "Dining hall", new Vector3(12.0f, FLOOR, 30.8f), 180f);
        }

        static void DressGallery()
        {
            // ranged enemies hold the gallery, facing out over the rail
            Marker("ranged", "Gallery", new Vector3(2.6f, cur.floorY, 8f), 90f);
            Marker("ranged", "Gallery", new Vector3(2.6f, cur.floorY, 18f), 90f);
            Marker("ranged", "Gallery", new Vector3(2.6f, cur.floorY, 26f), 90f);
            P(Pr("SM_Prop_Crate_Wood_01"), 1.0f, 1.0f, 20f); P(Pr("SM_Prop_Barrel_01"), 1.2f, 2.3f, 0f);
            P(Pr("SM_Prop_Crate_Wood_03"), 1.0f, 13.0f, 0f);
            P(Pr("SM_Prop_Vase_02"), 0.8f, 34.2f, 0f);
            Torch(0f, 5f, 90f, 7.3f, 2.2f, 10f); Torch(0f, 15f, 90f, 7.3f, 2.2f, 10f); Torch(0f, 24f, 90f, 7.3f, 2.2f, 10f);
        }

        static void DressKitchen()
        {
            // a cold hearth, worktables, barrels and sacks; low
            P(W("SM_Env_Wall_Fireplace_Entrance_01"), 25.0f, 9.25f, 180f, 1f, FLOOR - 0.1f);
            P(Pr("SM_Prop_Cauldron_01"), 25.0f, 9.0f, 0f);
            L(25.0f, FLOOR + 0.7f, 8.9f, Ember, 5f, 0.6f, false, "ColdHearth");
            P(Pr("SM_Prop_Table_01"), 22.5f, 3.5f, 0f);
            P(Pr("SM_Prop_Table_01"), 27.0f, 7.4f, 0f);
            P(Gp("SM_Gen_Prop_Food_Meat_02"), 22.0f, 3.5f, 20f, 1f, FLOOR + 0.96f, true); P(Gp("SM_Gen_Prop_Food_Vegetable_03"), 23.2f, 3.3f, 0f, 1f, FLOOR + 0.96f, true);
            P(Gp("SM_Gen_Prop_Plate_01"), 27.5f, 7.6f, 0f, 1f, FLOOR + 0.96f, true); P(Gp("SM_Gen_Prop_Pot_02"), 26.3f, 7.2f, 0f, 1f, FLOOR + 0.96f, true);
            PE(Wp("SM_Wep_Goblin_Machete_01"), 27.0f, FLOOR + 1.0f, 7.9f, 0f, 60f, 90f, 1f, true);
            P(Rp("SM_Prop_Camp_Pot_03"), 24.0f, 1.2f, 0f, 1f, float.NaN, true); P(Rp("SM_Prop_Camp_Pot_01"), 26.0f, 9.4f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Barrel_01"), 29.3f, 9.2f, 0f); P(Pr("SM_Prop_Barrel_02"), 28.3f, 9.3f, 0f); P(Pr("SM_Prop_Barrel_Broken_01"), 21.0f, 9.2f, 40f);
            P(Gp("SM_Gen_Prop_Sack_Stack_01"), 29.3f, 6.4f, 0f); P(Gp("SM_Gen_Prop_Sack_01"), 21.6f, 1.0f, 30f); P(Gp("SM_Gen_Prop_Sack_02"), 24.3f, 0.8f, -20f); P(Gp("SM_Gen_Prop_Sack_04"), 22.2f, 1.6f, 70f);
            P(Pr("SM_Prop_Crate_Wood_03"), 20.9f, 1.1f, 0f);
            P(Rp("SM_Prop_Barrel_Shelf_Small_01"), 27.5f, 0.55f, 0f);
            var shelf = Place(Rp("SM_Prop_Dwarf_Shelf_01"), new Vector3(23.0f, FLOOR + 1.6f, 9.75f), 180f, cur.props, null, "Shelf"); if (shelf != null) StripColliders(shelf);
            P(Rp("SM_Prop_Camp_Pot_05"), 22.6f, 9.6f, 0f, 1f, FLOOR + 1.68f, true); P(Rp("SM_Prop_Mug_01"), 23.4f, 9.6f, 0f, 1f, FLOOR + 1.68f, true);
            P(Gp("SM_Gen_Prop_Food_Bread_02"), 25.9f, 5.2f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Stool_01"), 23.8f, 4.9f, 15f); PE(Pr("SM_Prop_Stool_02"), 21.2f, FLOOR + 0.3f, 5.3f, 90f, 0f, 0f);
            P(Pr("SM_Prop_Vase_Shard_03"), 25.0f, 6.0f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Lantern_01"), 24.0f, 9.4f, 0f, 1f, FLOOR + 1.0f, true); L(24.0f, FLOOR + 1.3f, 9.0f, Candle, 7f, 1.4f);
            CandleStand(21.3f, 4.6f, 6f, 1.2f);
            Torch(26.5f, 0f, 0f, 2.1f, 1.8f, 8f);
            Web(30f, 10f, "NE", 0.55f);
            Marker("melee", "Kitchen", new Vector3(23.4f, FLOOR, 8.2f), 90f);
        }

        static void DressStore()
        {
            P(Rp("SM_Prop_Crate_Stack_01"), 37.6f, 7.6f, 0f);
            P(Pr("SM_Prop_Crate_Wood_01"), 36.3f, 9.3f, 0f); P(Pr("SM_Prop_Crate_Wood_04"), 31.6f, 9.1f, 20f); P(Pr("SM_Prop_Crate_Wood_05"), 39.2f, 4.4f, 0f);
            P(Pr("SM_Prop_Crate_Wood_01"), 32.7f, 9.2f, 0f, 1f, FLOOR + 1.1f);
            P(Pr("SM_Prop_Crate_Metal_Open_01"), 33.2f, 7.3f, 30f);
            P(Rp("SM_Prop_Crate_Broken_01"), 34.6f, 3.0f, 60f);
            P(Rp("SM_Prop_Barrel_Stack_01"), 39.5f, 1.8f, 90f);
            P(Pr("SM_Prop_Barrel_01"), 31.0f, 6.6f, 0f); P(Pr("SM_Prop_Barrel_02"), 32.0f, 7.4f, 0f); P(Pr("SM_Prop_Barrel_03"), 31.4f, 0.9f, 0f);
            P(Pr("SM_Prop_Barrel_Broken_02"), 35.2f, 6.0f, 0f);
            P(Gp("SM_Gen_Prop_Sack_Stack_01"), 33.6f, 0.8f, 0f); P(Gp("SM_Gen_Prop_Sack_Stack_02"), 36.2f, 0.8f, 30f);
            P(Gp("SM_Gen_Prop_Sack_03"), 35.0f, 2.0f, 0f, 1f, float.NaN, true); P(Gp("SM_Gen_Prop_Sack_05"), 37.4f, 2.4f, 0f, 1f, float.NaN, true);
            P(Rp("SM_Prop_Barrel_Shelf_Large_01"), 35.2f, 9.5f, 180f);
            P(Pr("SM_Prop_Vase_Group_01"), 38.6f, 9.0f, 0f);
            PE(Pr("SM_Prop_Plank_01"), 34.0f, FLOOR + 0.05f, 5.0f, 0f, 30f, 0f, 1f, true); PE(Pr("SM_Prop_Plank_02"), 36.6f, FLOOR + 0.05f, 4.6f, 0f, -20f, 0f, 1f, true);
            P(Wo("SM_Env_Rubble_Plank_01"), 38.0f, 5.5f, 0f, 1f, float.NaN, true);
            PE(Pr("SM_Prop_Ladder_01"), 30.6f, FLOOR, 8.2f, -12f, 90f, 0f, 0.85f);
            P(Pr("SM_Prop_Lantern_01"), 33.2f, 7.3f, 0f, 1f, FLOOR + 0.7f, true); L(33.2f, FLOOR + 1.2f, 7.3f, Candle, 7f, 1.5f);
            Torch(30f, 7f, 90f, 2.3f, 2f);
            Web(40f, 10f, "NE", 0.9f); Web(40f, 0f, "SE", 0.8f);
            Dust(35f, 2.5f, 5f, 1.4f);
        }

        static void DressLibrary()
        {
            // bookcases round the walls (backs to the wall) and two islands; the central aisle z 14.1..20.9 stays clear
            string[] kinds = { "SM_Prop_Bookcase_01", "SM_Prop_Bookcase_02", "SM_Prop_Bookcase_03", "SM_Prop_Bookcase_Grand_01", "SM_Prop_Bookcase_Grand_02", "SM_Prop_Bookcase_Broken_01", "SM_Prop_Bookcase_Empty_01", "SM_Prop_Bookcase_Grand_03" };
            int n = 0;
            string K() => kinds[(n++ * 5) % kinds.Length];
            for (int i = 0; i < 6; i++)
            {
                Bookcase(K(), 21.25f + i * 2.5f, 24.95f, 180f); // north wall, facing -z
                Bookcase(K(), 21.25f + i * 2.5f, 10.05f, 0f);   // south wall, facing +z
            }
            foreach (float z in new[] { 11.25f, 13.75f, 21.25f, 23.75f })
            {
                Bookcase(K(), 20.05f, z, 90f);   // west wall, facing +x
                Bookcase(K(), 34.95f, z, -90f);  // east wall, facing -x
            }
            for (int i = 0; i < 4; i++)
            {
                float x = 24.25f + i * 2.5f;
                Bookcase(K(), x, 13.0f, 0f); Bookcase(K(), x, 13.0f, 180f);     // island A: backs to back on z = 13, fronts to +z and -z
                Bookcase(K(), x, 22.0f, 180f); Bookcase(K(), x, 22.0f, 0f);     // island B on z = 22
            }
            P(Pr("SM_Prop_StoneTable_01"), 25.0f, 15.4f, 0f); P(Pr("SM_Prop_StoneTable_01"), 30.0f, 19.6f, 0f);
            P(Mp("SM_Prop_Book_Open_01"), 24.6f, 15.4f, 20f, 1f, FLOOR + 0.83f, true); P(Mp("SM_Prop_Book_Pile_02"), 25.7f, 15.2f, 0f, 1f, FLOOR + 0.83f, true);
            P(Mp("SM_Prop_Papers_02"), 29.5f, 19.5f, 40f, 1f, FLOOR + 0.84f, true); P(Mp("SM_Prop_Book_Pile_01"), 30.6f, 19.8f, 0f, 1f, FLOOR + 0.83f, true);
            P(It("SM_Item_Inkwell_01"), 29.8f, 20.0f, 0f, 1f, FLOOR + 0.83f, true);
            P(Rp("SM_Prop_Chair_01"), 25.0f, 14.5f, 0f); PE(Rp("SM_Prop_Chair_Broken_01"), 31.2f, FLOOR, 20.8f, 0f, 200f, 0f);
            P(Pr("SM_Prop_Stool_03"), 28.9f, 20.4f, 10f);
            P(Mp("SM_Prop_Book_Stand_01"), 22.0f, 15.0f, 45f); P(Mp("SM_Prop_Book_Open_02"), 22.0f, 15.0f, 45f, 1f, FLOOR + 1.1f, true);
            P(Mp("SM_Prop_Globe_01"), 33.2f, 20.6f, 0f);
            // books pulled down and scattered
            for (int i = 0; i < 10; i++)
            {
                float x = 21.5f + (float)rng.NextDouble() * 12.5f, z = (rng.Next(2) == 0) ? 14.2f + (float)rng.NextDouble() * 1.8f : 19.0f + (float)rng.NextDouble() * 1.8f;
                if (i % 3 == 0) P(Mp("SM_Prop_Book_Pile_0" + (1 + i % 3)), x, z, Jit(180f), 1f, float.NaN, true);
                else if (i % 3 == 1) PE(Mp("SM_Prop_Book_0" + (1 + i % 5)), x, FLOOR + 0.04f, z, 90f, Jit(180f), 0f, 1f, true);
                else P(Mp("SM_Prop_Papers_0" + (1 + i % 8)), x, z, Jit(180f), 1f, FLOOR + 0.01f, true);
            }
            P(Mp("SM_Prop_Papers_08"), 27f, 17.0f, 20f, 1f, FLOOR + 0.01f, true);
            P(It("SM_Item_BookPile_01"), 33.5f, 14.5f, 0f, 1f, float.NaN, true);
            Chandelier(27.5f, 4.9f, 17.5f, 13f, 2.6f, false, false);
            CandleStand(22.6f, 20.2f); CandleStand(32.4f, 14.6f); CandleStand(33.0f, 23.5f, 5f, 1.1f);
            Dust(27.5f, 2.5f, 17.5f, 1.8f);
            Marker("melee", "Library", new Vector3(31.8f, FLOOR, 15.3f), -90f);
        }

        /// <summary>A 2.5 m bookcase whose back is on a wall or island line (at z = backZOrX for north/south facing, x = backZOrX for east/west facing), centred at cx along it, front toward yaw.</summary>
        static void Bookcase(string name, float cx, float backZOrX, float yaw)
        {
            // the mesh spans local x -2.5..0 and z 0..1.05. BookFrontAtZ0: the shelves open toward local -z (front at z=0, back at z=1.05).
            var f = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0, Mathf.Cos(yaw * Mathf.Deg2Rad)); // wanted front direction
            Vector3 back = (Mathf.Abs(f.z) > 0.5f) ? new Vector3(cx, cur.floorY, backZOrX) : new Vector3(backZOrX, cur.floorY, cx);
            float rot = BookFrontAtZ0 ? yaw + 180f : yaw;          // local +z must point away from the front when the front is at z=0
            var u = new Vector3(Mathf.Cos(rot * Mathf.Deg2Rad), 0, -Mathf.Sin(rot * Mathf.Deg2Rad)); // local +x after rotation
            var fz = new Vector3(Mathf.Sin(rot * Mathf.Deg2Rad), 0, Mathf.Cos(rot * Mathf.Deg2Rad)); // local +z after rotation
            // the mesh occupies origin + x*u + z*fz, x in [-2.5,0], z in [0,1.05]; centre the width on the back point and put the back face on the back line
            Vector3 origin = back + u * 1.25f - fz * (BookFrontAtZ0 ? 1.05f : 0f);
            Place(Mp(name), origin, rot, cur.props, null, "Bookcase");
        }

        static void DressStudy()
        {
            // benches of glassware, much smashed; shelves of jars spilled. Keep x 35..38.5, z 16.5..20 clear (library door to shrine door).
            P(Pr("SM_Prop_Table_01"), 43.5f, 12.6f, 90f); P(Pr("SM_Prop_Table_01"), 43.5f, 15.6f, 90f);
            string[] potions = { "SM_Item_Potion_01", "SM_Item_Potion_03", "SM_Item_Potion_05", "SM_Item_Vial_01", "SM_Item_Jar_01", "SM_Item_Potion_07", "SM_Item_Vial_03", "SM_Item_Jar_02", "SM_Item_Potion_09", "SM_Item_Scale_01", "SM_Item_Potion_Pole_01", "SM_Item_Vial_02" };
            for (int i = 0; i < potions.Length; i++)
            {
                float z = 11.4f + i * 0.5f; float x = 43.1f + (i % 3) * 0.4f;
                if (i % 4 == 3) PE(It(potions[i]), x, FLOOR + 0.98f, z, 90f, Jit(180f), 0f, 1f, true);
                else P(It(potions[i]), x, z, Jit(180f), 1f, FLOOR + 0.96f, true);
            }
            for (int i = 0; i < 4; i++) P(Pr("SM_Prop_Vase_Shard_0" + (1 + i)), 42.2f + Jit(0.6f), 12f + i * 1.3f, Jit(180f), 1f, float.NaN, true);
            P(Pr("SM_Prop_Vase_Broken_02"), 41.6f, 14.0f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Cauldron_01"), 40.0f, 11.3f, 0f); L(40.0f, FLOOR + 0.9f, 11.3f, new Color(0.45f, 1f, 0.55f), 5f, 1.6f, false, "CauldronGlow");
            P(Mp("SM_Prop_Bookcase_Small_01"), 37.2f, 10.1f, 0f); P(Mp("SM_Prop_Bookcase_Small_Empty_01"), 39.6f, 10.1f, 0f);
            var shelf = Place(Rp("SM_Prop_Dwarf_Shelf_01"), new Vector3(35.25f, FLOOR + 1.5f, 12.6f), 90f, cur.props, null, "Shelf"); if (shelf != null) StripColliders(shelf);
            P(It("SM_Item_Jar_01"), 35.5f, 12.2f, 0f, 1f, FLOOR + 1.58f, true); P(It("SM_Item_Potion_02"), 35.5f, 12.9f, 0f, 1f, FLOOR + 1.58f, true);
            var shelf2 = Place(Rp("SM_Prop_Dwarf_Shelf_02"), new Vector3(35.25f, FLOOR + 2.3f, 12.6f), 90f, cur.props, null, "Shelf"); if (shelf2 != null) StripColliders(shelf2);
            P(It("SM_Item_Potion_04"), 35.5f, 12.4f, 0f, 1f, FLOOR + 2.38f, true); P(It("SM_Item_Jar_02"), 35.5f, 13.1f, 0f, 1f, FLOOR + 2.38f, true);
            P(Pr("SM_Prop_Table_Round_01"), 38.8f, 14.3f, 0f);
            P(Mp("SM_Prop_Book_Open_01"), 38.8f, 14.3f, 30f, 1f, FLOOR + 0.91f, true); P(Pr("SM_Prop_Candle_01"), 39.2f, 14.6f, 0f, 1f, FLOOR + 0.91f, true);
            L(39.2f, FLOOR + 1.3f, 14.6f, Candle, 5f, 1.0f);
            PE(Pr("SM_Prop_Stool_01"), 37.9f, FLOOR + 0.3f, 15.3f, 90f, 20f, 0f);
            P(Pr("SM_Prop_Skeleton_Table_01"), 41.0f, 18.2f, 200f);
            P(Mp("SM_Prop_Papers_04"), 38.5f, 12.5f, 0f, 1f, FLOOR + 0.01f, true); P(Mp("SM_Prop_Papers_01"), 40.5f, 16.5f, 60f, 1f, FLOOR + 0.01f, true);
            P(It("SM_Item_Shrooms"), 36.4f, 10.9f, 0f, 1f, float.NaN, true);
            P(Rp("SM_Prop_Scroll_Open_01"), 43.5f, 17.8f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Crate_Wood_02"), 44.3f, 19.2f, 20f);
            Chandelier(40f, 4.9f, 15f, 12f, 2.4f, false, false);
            CandleStand(36.3f, 11.0f, 6f, 1.3f);
            Web(45f, 20f, "NE", 0.9f);
            Marker("melee", "Alchemist's study", new Vector3(41.2f, FLOOR, 16.0f), -90f);
        }

        static void DressShrine()
        {
            // tall and still: an altar, candles and statues, desecrated. The way runs along x 35..38.5 from (37.5,20) to (35,27.5).
            P(Mi("SM_Env_Alter_01"), 41.0f, 28.2f, 180f);
            P(Mi("SM_Env_Statue_04"), 37.6f, 29.1f, 180f); P(Mi("SM_Env_Statue_03"), 43.8f, 29.0f, 180f);
            P(Re("SM_Env_Statue_Broken_01"), 43.6f, 21.4f, -90f); P(Re("SM_Env_Statue_Broken_01_Head"), 41.8f, 23.2f, 40f, 1f, float.NaN, true);
            Column(39.8f, 22.6f, 2); P(Pi("SM_Env_Pillar_Broken_02"), 44.2f, 26.2f, 0f);
            P(Pi("SM_Env_Pillar_Broken_Pile_02"), 43.0f, 25.0f, 20f, 1f, float.NaN, true);
            CandleStand(39.4f, 27.2f, 7f, 1.4f); CandleStand(42.6f, 27.2f, 7f, 1.4f);
            Candles(40.0f, FLOOR + 1.2f, 28.0f, 2); Candles(42.0f, FLOOR + 1.2f, 28.2f, 4, false);
            Candles(44.3f, FLOOR, 20.6f, 1); Candles(40.6f, FLOOR, 24.9f, 3, false);
            PE(Pr("SM_Prop_Skeleton_01"), 40.6f, FLOOR, 26.3f, 0f, 100f, 0f, 1f, true);
            P(Bo("SM_Env_BonePile_Small_01"), 44.0f, 28.6f, 0f, 1f, float.NaN, true);
            P(Pr("SM_Prop_Vase_Broken_03"), 36.4f, 28.9f, 0f, 1f, float.NaN, true); P(Pr("SM_Prop_Vase_07"), 44.4f, 23.5f, 0f);
            Rubble(44.2f, 20.6f); P(Ro("SM_Env_Rubble_02"), 36.5f, 24.0f, 0f, 1f, float.NaN, true);
            Banner(Pr("SM_Prop_Wall_Banner_03"), 38.5f, 8.8f, 29.75f, 180f); Banner(Pr("SM_Prop_Wall_Banner_03"), 43.5f, 8.8f, 29.75f, 180f);
            Banner(Rp("SM_Prop_Banner_Damaged_04"), 44.75f, 9.6f, 24f, -90f);
            L(40f, FLOOR + 8f, 25f, Cold, 16f, 1.3f, false, "ShaftLight");
            L(41f, FLOOR + 3f, 27.5f, Candle, 9f, 1.5f, false, "AltarGlow");
            Torch(45f, 22f, -90f, 2.4f, 1.8f); Torch(40f, 20f, 0f, 2.4f, 1.6f);
            Web(45f, 30f, "NE", 1.2f); Web(35f, 20f, "SW", 1.0f);
            Dust(40f, 4f, 25f, 2f);
            Marker("melee", "Shrine", new Vector3(42.2f, FLOOR, 24.0f), 180f);
        }

        static void DressAntechamber()
        {
            Brazier(26.4f, 26.0f, true, float.NaN, 9f, 2.4f); Brazier(33.6f, 26.0f, true, float.NaN, 9f, 2.4f);
            P(Pr("SM_Prop_Rug_03"), 30.0f, 27.5f, 90f, 1f, float.NaN, true);
            P(Mi("SM_Env_Statue_02"), 26.9f, 29.0f, 180f); P(Mi("SM_Env_Statue_02"), 33.1f, 29.0f, 180f);
            Banner(Pr("SM_Prop_Wall_Banner_02"), 26.2f, 4.8f, 29.75f, 180f); Banner(Pr("SM_Prop_Wall_Banner_02"), 33.8f, 4.8f, 29.75f, 180f);
            P(Pi("SM_Env_Pillar_Square_02"), 25.6f, 25.6f, 0f); P(Pi("SM_Env_Pillar_Square_02"), 34.4f, 25.6f, 0f);
            Rubble(25.6f, 29.3f);
            Torch(30f, 25f, 0f, 2.4f, 2.0f);
            Marker("melee", "Antechamber", new Vector3(28.2f, FLOOR, 26.3f), 90f);
            Marker("melee", "Antechamber", new Vector3(31.8f, FLOOR, 26.3f), -90f);
        }

        static void DressThrone()
        {
            // tall, bright, sparse and grand, though fallen: columns, braziers, banners, a torn runner to the stage
            foreach (float z in new[] { 35f, 41f, 47f }) { Column(23f, z, 3); Column(37f, z, 3); }
            var rug = Place(Pr("SM_Prop_Rug_01"), new Vector3(30f, FLOOR, 40.6f), 90f, cur.props, new Vector3(1.45f, 1f, 1f), "Runner"); if (rug != null) StripColliders(rug);
            Brazier(27f, 36f, true, float.NaN, 13f, 3.2f); Brazier(33f, 36f, true, float.NaN, 13f, 3.2f);
            Brazier(27f, 44f, true, float.NaN, 13f, 3.2f, true); Brazier(33f, 44f, true, float.NaN, 13f, 3.2f);
            Brazier(26.5f, 54.2f, true, FLOOR + 1f, 12f, 3f); Brazier(33.5f, 54.2f, true, FLOOR + 1f, 12f, 3f);
            Banner(Rp("SM_Prop_Banner_01"), 20.3f, 14.3f, 36f, 90f); Banner(Rp("SM_Prop_Banner_03"), 20.3f, 14.3f, 44f, 90f);
            Banner(Rp("SM_Prop_Banner_01"), 39.7f, 14.3f, 36f, -90f); Banner(Rp("SM_Prop_Banner_03"), 39.7f, 14.3f, 44f, -90f);
            Banner(Rp("SM_Prop_Banner_Damaged_03"), 30f, 13.6f, 54.75f, 180f);
            Banner(Pr("SM_Prop_Wall_Banner_01"), 25.5f, 9.3f, 54.75f, 180f); Banner(Pr("SM_Prop_Wall_Banner_01"), 34.5f, 9.3f, 54.75f, 180f);
            P(Mi("SM_Env_Statue_01"), 21.4f, 52.0f, 90f); P(Mi("SM_Env_Statue_01"), 38.6f, 52.0f, -90f);
            P(Pi("SM_Env_Pillar_Broken_Pile_02"), 36.2f, 38.5f, 0f, 1f, float.NaN, true);
            P(Ro("SM_Env_Brick_Rubble_04"), 22.0f, 44.5f, 0f, 1f, float.NaN, true); P(Ro("SM_Env_Rubble_02"), 38.4f, 32.8f, 0f, 1f, float.NaN, true);
            PE(Pr("SM_Prop_Skeleton_01"), 25.6f, FLOOR, 48.4f, 0f, 30f, 0f, 1f, true);
            PE(Wp("SM_Wep_Shield_Heater_02"), 34.6f, FLOOR + 0.05f, 46.0f, 90f, 20f, 0f, 1f, true);
            PE(Wp("SM_Wep_Spear_01"), 24.0f, FLOOR + 0.05f, 38.0f, 90f, -30f, 0f, 1f, true);
            PE(Pr("SM_Prop_Candle_Chandelier_02_Broken"), 35.0f, FLOOR + 1.1f, 40.5f, 75f, 20f, 0f);
            Chandelier(30f, 11.6f, 38f, 22f, 5.5f, false); Chandelier(30f, 11.6f, 47f, 22f, 5.5f, false);
            L(30f, FLOOR + 13f, 42f, Cold, 30f, 1.6f, false, "HighFill");
            L(30f, FLOOR + 4.5f, 52.5f, Gold, 14f, 3.5f, false, "ThroneGlow");
            Torch(20f, 33f, 90f, 2.5f, 2.2f); Torch(40f, 33f, -90f, 2.5f, 2.2f); Torch(20f, 52f, 90f, 2.5f, 2.2f); Torch(40f, 52f, -90f, 2.5f, 2.2f);
            Dust(30f, 6f, 42f, 3f);
            Web(40f, 55f, "NE", 1.5f); Web(20f, 30f, "SW", 1.4f);
            Marker("warlord", "Throne room", new Vector3(28.3f, FLOOR + 1f, 53.9f), 180f);
            Marker("melee", "Throne room", new Vector3(24.2f, FLOOR + 1f, 53.8f), 180f);
            Marker("melee", "Throne room", new Vector3(35.8f, FLOOR + 1f, 53.8f), 180f);
            Marker("melee", "Throne room", new Vector3(27.0f, FLOOR, 48.3f), 180f);
            Marker("melee", "Throne room", new Vector3(33.0f, FLOOR, 48.3f), 180f);
        }

        static void PlaceMarkers()
        {
            // the armory's defenders and the two pickups (room dressing placed the rest)
            Marker("ranged", "Armory", new Vector3(-10.6f, FLOOR, 2.6f), 180f);
            Marker("ranged", "Armory", new Vector3(-4.4f, FLOOR, 2.6f), 180f);
            Marker("ranged", "Armory", new Vector3(-7.5f, FLOOR, 4.6f), 180f);
            Marker("melee", "Armory", new Vector3(-12.4f, FLOOR, 11.0f), 180f);
            Marker("melee", "Armory", new Vector3(-3.5f, FLOOR, 11.0f), 180f);
            Marker("melee", "Armory", new Vector3(-12.4f, FLOOR, 4.0f), 90f);
            Marker("melee", "Armory", new Vector3(-3.5f, FLOOR, 4.0f), -90f);
            Marker("gear", "Armory", new Vector3(-7.5f, FLOOR, 10.4f), 180f);
            Marker("hoard", "Treasure vault", new Vector3(-32.6f, FLOOR + 0.35f, 27.3f), 0f);
        }
    }
}
