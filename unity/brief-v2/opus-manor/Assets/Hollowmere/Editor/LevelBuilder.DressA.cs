using UnityEngine;

namespace Hollowmere.EditorTools
{
    public static partial class LevelBuilder
    {
        // ================================================================ FOYER
        static void DressFoyer()
        {
            var r = Get("foyer");
            KeepClear("foyer", 12.6f, 0.6f, 15.4f, 8f);   // start to the gun room entrance
            KeepClear("foyer", 9f, 3.6f, 14.5f, 5.4f);     // across to the watch room door
            // front door: a locked pair of leaves on the south wall with a stone surround
            // two leaves side by side in the stone surround: each runs 1.13 m along -X from its pivot
            Put(r, "SM_Bld_Door_01", 14f, r.z0 + 0.16f, 0f, 0f, 1f, new Vector3(1f, 1.12f, 1f));
            Put(r, "SM_Bld_Door_01", 14f + 1.13f, r.z0 + 0.16f, 0f, 0f, 1f, new Vector3(1f, 1.12f, 1f));
            Box(r.props, "FrontDoorFrame", new Vector3(12.65f, 0f, r.z0), new Vector3(12.85f, 2.95f, r.z0 + 0.3f), s_Surf["stone_trim"], r.RL);
            Box(r.props, "FrontDoorFrame", new Vector3(15.15f, 0f, r.z0), new Vector3(15.35f, 2.95f, r.z0 + 0.3f), s_Surf["stone_trim"], r.RL);
            Box(r.props, "FrontDoorFrame", new Vector3(12.65f, 2.78f, r.z0), new Vector3(15.35f, 3.05f, r.z0 + 0.3f), s_Surf["stone_trim"], r.RL);
            Put(r, "SM_Prop_Door_Mat_02", 14f, 1.0f, 0f);
            Put(r, "SM_Prop_Rug_05", 14f, 4.3f, 90f, 0f, 1.35f, null, null, null, false);
            // benches, coat stand, tall clock
            Put(r, "SM_Prop_Bench_Seat_01", 18.55f, 2.6f, -90f);
            Put(r, "SM_Prop_Bench_Seat_01", 18.55f, 5.6f, -90f);
            Put(r, "SM_Bld_Wall_Bench_01", r.x0, 0.3f, 90f).transform.position = new Vector3(r.x0 - 0.11f, 0f, 0.3f);
            Put(r, "SM_Prop_Coat_Stand_01", 9.55f, 6.6f, 120f);
            Put(r, "SM_Prop_Coat_Stand_01_Insert_01", 9.55f, 6.6f, 120f);
            Put(r, "SM_Prop_Clock_01", 17.6f, 7.65f, 180f);
            Put(r, "SM_Prop_Umbrella_Stand_01", 12.2f, 0.45f, 0f);
            Put(r, "SM_Prop_Table_Side_03", 10.1f, 1.1f, 15f);
            PutOn(r, "SM_Prop_Lamp_02", 10.1f, 1.1f, 0.89f);
            Put(r, "SM_Prop_Planter_01", 12.0f, 7.5f, 0f);
            Put(r, "SM_Prop_Planter_01_Plant_01", 12.0f, 7.5f, 0f, 0.35f);
            Put(r, "SM_Prop_Planter_02", 16.0f, 7.5f, 0f);
            Put(r, "SM_Prop_Planter_02_Plant_01", 16.0f, 7.5f, 0f, 0.35f);
            // a toppled side chair and a fallen frame
            Topple(Put(r, "SM_Prop_Chair_02", 17.4f, 1.0f, 200f), 90f);
            var fallen = Put(r, "SM_Prop_PaintingFrame_03", 18.3f, 7.0f, 0f, 0f, 1f, null, null, null, false);
            fallen.transform.rotation = Quaternion.Euler(-78f, -90f, 0f);
            fallen.transform.position = new Vector3(18.75f, 0.02f, 4.1f);
            // walls: mirror, portraits, sconces
            OnWall(r, "SM_Prop_Mirror_01", 'E', 4.1f, 1.0f, 0.02f, 1f);
            Portrait(r, 'W', 1.6f, 1.9f, 0.85f, 4, 2);
            Portrait(r, 'N', 10.6f, 2.0f, 0.8f, 6, 3);
            Portrait(r, 'S', 17.3f, 1.9f, 0.8f, 1, 4);
            foreach (var (side, along) in new[] { ('W', 2.6f), ('W', 6.4f), ('E', 1.8f), ('E', 6.6f), ('N', 12.6f), ('N', 15.4f) })
            {
                OnWall(r, "SM_Prop_Light_Wall_01", side, along, 2.1f, 0.0f);
                var p = WallPoint(r, side, along, 2.3f, 0.3f);
                Lamp(r, "Sconce", p, Candle, 1.1f, 4.5f);
            }
            Hang(r, "SM_Prop_Light_Chandelier_02", 14f, 4.6f, 0.62f, 0f);
            Lamp(r, "Chandelier", r.P(14f, 2.6f, 4.4f), Warm, 3.2f, 9f, true);
            Scatter(r, Papers, 10f, 1f, 18f, 7.5f, 6);
            Scatter(r, new[] { "SM_Gen_Env_Leaves_01", "SM_Gen_Env_Leaves_02" }, 12.8f, 0.4f, 15.2f, 2.0f, 4);
            Neglect(r, 10, 4);
        }

        // ================================================================ GUN ROOM
        static readonly Vector3 BestGearPos = new Vector3(14f, 0.95f, 15.2f);

        static void DressGunRoom()
        {
            var r = Get("gun");
            KeepClear("gun", 16.2f, 15.6f, 17.8f, 19.6f);  // back door
            KeepClear("gun", 12.8f, 15.6f, 17.8f, 16.8f);  // to the best gear
            KeepClear("gun", 13f, 9.2f, 15f, 11.2f);       // entrance
            // the screen of cabinets facing the entrance, and its back
            foreach (float x in new[] { 11.9f, 14.0f, 16.1f })
            {
                GunCabinet(r, x, 11.5f, 180f);
                GunCabinet(r, x, 11.72f, 0f);
            }
            // a timber screen between the back-to-back cabinets, solid to the floor
            Box(r.props, "GunScreen", new Vector3(10.85f, 0f, 11.52f), new Vector3(17.15f, 2.7f, 11.7f), s_Surf["wood_dark"], r.RL);
            Box(r.props, "GunScreenCap", new Vector3(10.8f, 2.7f, 11.45f), new Vector3(17.2f, 2.8f, 11.77f), s_Surf["wood_trim"], r.RL, 0f, false);
            // rows of cabinets, back to back, west and east of the middle
            foreach (float x in new[] { 9.2f, 18.8f })
            {
                for (int i = 0; i < 2; i++)
                {
                    float z = 14.3f + i * 2.1f;
                    GunCabinet(r, x, z, -90f, true);
                    GunCabinet(r, x, z, 90f, true);
                }
            }
            // wall racks of long arms and blades
            GunRack(r, 'W', 12.6f, 18.8f); GunRack(r, 'E', 12.6f, 18.8f);
            GunRack(r, 'N', 7.4f, 11.6f); GunRack(r, 'N', 18.2f, 20.6f);
            GunRack(r, 'S', 7.3f, 9.6f); GunRack(r, 'S', 18.4f, 20.7f);
            // the best gear: a display table in the middle, lit from above
            Put(r, "SM_Prop_Desk_01", 14f, 15.0f, 180f);
            PutOn(r, "SM_Prop_Lockbox_02", 13.4f, 14.95f, 0.84f, 10f);
            PutOn(r, "SM_Wep_Revolver_01", 14.3f, 14.8f, 0.84f, 70f);
            PutOn(r, "SM_Wep_Revolver_01", 14.6f, 15.1f, 0.84f, 95f);
            PutOn(r, "SM_Gen_Wep_Axe_01", 14.2f, 15.25f, 0.84f, 0f).transform.rotation = Quaternion.Euler(0f, 80f, 90f);
            PutOn(r, "SM_Wep_Knife_01", 13.8f, 14.65f, 0.84f, 30f);
            PutOn(r, "SM_Prop_Lamp_03", 15.0f, 14.7f, 0.84f);
            Lamp(r, "GearLamp", r.P(14f, 2.6f, 15.0f), Warm, 4.0f, 6f, true, LightType.Spot, new Vector3(90f, 0f, 0f), 70f);
            // trophies
            OnWall(r, "SM_Prop_Bear_Mount_01", 'W', 14.3f, 2.55f, 0f, 1f);
            OnWall(r, "SM_Prop_Bear_Mount_01", 'E', 14.3f, 2.55f, 0f, 1f);
            OnWall(r, "SM_Prop_Ornamental_Head_01", 'N', 16.2f, 2.65f, 0f, 0.8f);
            OnWall(r, "SM_Prop_Mask_01", 'S', 9.4f, 2.4f, 0f, 1f);
            OnWall(r, "SM_Prop_Mask_02", 'S', 18.6f, 2.4f, 0f, 1f);
            OnWall(r, "SM_Prop_Mask_03", 'N', 18.2f, 2.5f, 0f, 1f);
            OnWall(r, "SM_Prop_Mask_Count_01", 'S', 11.2f, 2.5f, 0f, 0.9f);
            PutOn(r, "SM_Prop_Cat_Stuffed_02", 11.9f, 11.6f, 2.53f, 180f);
            PutOn(r, "SM_Prop_Cat_Stuffed_01", 16.1f, 11.6f, 2.53f, 160f);
            // ammunition crates as low cover
            Put(r, "SM_Gen_Prop_Crate_01", 8.0f, 12.6f, 10f, 0f, 0.9f);
            Put(r, "SM_Gen_Prop_Crate_02", 19.9f, 12.4f, -8f, 0f, 0.9f);
            Put(r, "SM_Gen_Prop_Crate_03", 11.0f, 18.7f, 4f, 0f, 0.9f);
            Put(r, "SM_Gen_Prop_Crate_01", 20.0f, 18.6f, 40f, 0f, 0.85f);
            Put(r, "SM_Prop_Lockbox_01", 8.1f, 12.6f, 0f, 0.82f);
            // lamps
            foreach (var (x, z) in new[] { (10.5f, 10.4f), (17.5f, 10.4f), (9.2f, 17.0f), (18.8f, 17.0f), (14f, 18.4f) })
                Lamp(r, "Lamp", r.P(x, 2.9f, z), Warm, 1.6f, 6f);
            foreach (var (x, z) in new[] { (7.9f, 14.3f), (10.6f, 14.3f), (17.4f, 14.3f), (20.1f, 14.3f), (14f, 12.9f) })
                Lamp(r, "AisleLamp", r.P(x, 2.4f, z), Warm, 0.9f, 3.6f);
            foreach (var x in new[] { 10.4f, 17.6f })
                Hang(r, "SM_Prop_Light_Chandelier_03", x, 15.2f, 0.9f);
            // light the screen of cabinets so the entrance draws the eye from the foyer
            Lamp(r, "EntranceLamp", r.P(14f, 2.7f, 10.2f), Warm, 2.4f, 4.5f);
            Lamp(r, "BackDoorLamp", r.P(17f, 2.7f, 18.6f), Warm, 1.2f, 4.5f);
            Neglect(r, 5, 2);
        }

        /// <summary>A tall glazed cabinet stocked with arms. Yaw 0 puts its front to +z.</summary>
        static void GunCabinet(Room r, float x, float z, float yaw, bool tall = false)
        {
            var go = Put(r, "SM_Prop_Cabinet_02", x, z, yaw);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var fwd = rot * Vector3.forward; var right = rot * Vector3.right;
            Vector3 c = new Vector3(x, 0f, z) + fwd * 0.26f;
            // long arms standing upright behind the glass, a revolver laid in front of them
            string[] arms = { "SM_Wep_Hatchet_01", "SM_Gen_Wep_Spade_01", "SM_Wep_Hatchet_01", "SM_Wep_Hatchet_01", "SM_Gen_Wep_Spade_01" };
            for (int i = 0; i < 5; i++)
            {
                var p = c + right * (-0.62f + i * 0.31f);
                var k = Put(r, arms[(i + (int)(x + z)) % arms.Length], p.x, p.z, yaw, 0.93f, arms[i] == "SM_Wep_Hatchet_01" ? 1.12f : 1.0f, null, null, null, false);
                k.transform.rotation = Quaternion.LookRotation(right, Vector3.up) * Quaternion.Euler(0f, 0f, -5f);
                k.transform.position = new Vector3(p.x, r.fy + 0.93f, p.z);
            }
            for (int i = 0; i < 2; i++)
            {
                var p = c + fwd * 0.12f + right * (-0.35f + i * 0.7f);
                var g = Put(r, "SM_Wep_Revolver_01", p.x, p.z, yaw, 0.93f, 1.1f, null, null, null, false);
                g.transform.rotation = Quaternion.LookRotation(right, Vector3.up) * Quaternion.Euler(0f, 0f, 90f);
                g.transform.position = new Vector3(p.x, r.fy + 0.95f, p.z);
            }
        }

        /// <summary>
        /// A rack of arms along a wall: two rails with long arms, blades and hunting tools hung upright side by side,
        /// and a shelf of revolvers above.
        /// </summary>
        static void GunRack(Room r, char side, float from, float to)
        {
            string[] arms = { "SM_Gen_Wep_Axe_01", "SM_Prop_Scythe_01", "SM_Wep_Hatchet_01", "SM_Prop_Pitchfork_01", "SM_Gen_Wep_Pickaxe_01", "SM_Wep_Hatchet_01", "SM_Gen_Wep_Spade_01", "SM_Gen_Wep_Axe_01" };
            var S = s_Surf["wood_dark"];
            foreach (var y in new[] { 0.55f, 1.55f })
            {
                var a = WallPoint(r, side, from, y, 0f); var b = WallPoint(r, side, to, y + 0.07f, 0.09f);
                Box(r.props, "RackRail", a, b, S, r.RL, 0f, false);
            }
            var c0 = WallPoint(r, side, from, 0f, 0f); var c1 = WallPoint(r, side, to, 1.0f, 0.4f);
            var col = new GameObject("RackCollider"); col.transform.SetParent(r.props, false);
            col.transform.position = (c0 + c1) * 0.5f; col.AddComponent<BoxCollider>().size = new Vector3(Mathf.Abs(c1.x - c0.x), 1.0f, Mathf.Abs(c1.z - c0.z));
            int i = 0;
            for (float t = from + 0.2f; t <= to - 0.15f; t += 0.28f, i++)
            {
                var p = WallPoint(r, side, t, 0f, 0.13f);
                var g = Put(r, arms[i % arms.Length], p.x, p.z, FaceIn(side), 0.02f, i % arms.Length == 2 || i % arms.Length == 5 ? 1.6f : 1f, null, null, null, false);
                g.transform.rotation = Quaternion.Euler(0f, FaceIn(side) + 90f, 0f) * Quaternion.Euler(0f, 0f, 4f);
                if (i % arms.Length == 2 || i % arms.Length == 5) g.transform.position = WallPoint(r, side, t, 0.7f, 0.13f);
            }
            OnWall(r, "SM_Prop_Shelf_01", side, (from + to) * 0.5f, 1.95f, 0f, 1f, new Vector3((to - from) / 1.3f, 1f, 1f), false);
            for (float t = from + 0.25f; t <= to - 0.2f; t += 0.42f)
            {
                var p = WallPoint(r, side, t, 2.1f, 0.15f);
                var g = Put(r, "SM_Wep_Revolver_01", p.x, p.z, FaceIn(side) + 90f, 2.1f, 1.1f, null, null, null, false);
            }
        }

        /// <summary>A wall rack: a shelf of revolvers with long tools and blades hung beneath.</summary>
        static void WeaponRack(Room r, char side, float along)
        {
            OnWall(r, "SM_Prop_Shelf_01", side, along, 1.75f, 0f, 1f, null, true);
            for (int i = 0; i < 3; i++)
            {
                var p = WallPoint(r, side, along - 0.45f + i * 0.45f, 1.9f, 0.16f);
                var g = Put(r, "SM_Wep_Revolver_01", p.x, p.z, FaceIn(side) + 90f, 1.9f, 1f, null, null, null, false);
            }
            string[] longs = { "SM_Gen_Wep_Axe_01", "SM_Prop_Pitchfork_01", "SM_Prop_Scythe_01", "SM_Gen_Wep_Pickaxe_01", "SM_Gen_Wep_Spade_01" };
            for (int i = 0; i < 3; i++)
            {
                var p = WallPoint(r, side, along - 0.5f + i * 0.5f, 0f, 0.12f);
                var g = Put(r, longs[(i + (int)along) % longs.Length], p.x, p.z, FaceIn(side), 0f, 1f, null, null, null, false);
                g.transform.rotation = Quaternion.Euler(0f, FaceIn(side), 0f) * Quaternion.Euler(-8f, 0f, 0f);
            }
        }

        // ================================================================ WATCH ROOM
        static void DressWatchRoom()
        {
            var r = Get("watch");
            KeepClear("watch", 4.2f, 3.0f, 8.6f, 5.0f);
            Put(r, "SM_Prop_Desk_01", 6.5f, 1.05f, 0f);
            PutOn(r, "SM_Prop_Lamp_02", 7.2f, 1.0f, 0.84f);
            Lamp(r, "DeskLamp", r.P(7.2f, 1.3f, 1.0f), Candle, 1.4f, 5f, true);
            PutOn(r, "SM_Prop_Papers_02", 6.2f, 1.1f, 0.84f);
            PutOn(r, "SM_Prop_Key_02", 5.9f, 0.9f, 0.84f, 40f);
            Topple(Put(r, "SM_Prop_Chair_01", 6.0f, 2.1f, 160f), 90f);
            Topple(Put(r, "SM_Prop_Stool_02", 7.7f, 2.5f, 30f), -90f);
            // key board on the north wall, keys hanging
            OnWall(r, "SM_Prop_Shelf_01", 'N', 6.4f, 1.55f, 0f, 1f, null, false);
            for (int i = 0; i < 5; i++)
            {
                var p = WallPoint(r, 'N', 5.9f + i * 0.25f, 1.42f, 0.05f);
                var k = Put(r, "SM_Prop_Key_0" + (1 + i % 4), p.x, p.z, 0f, p.y - r.fy, 1f, null, null, null, false);
                k.transform.rotation = Quaternion.Euler(0f, 180f, 90f);
            }
            Scatter(r, new[] { "SM_Prop_Key_01", "SM_Prop_Key_03" }, 5f, 1.8f, 8f, 3f, 3);
            // the watchman's bunk, a locker knocked over, bottles
            Put(r, "SM_Prop_Bed_Small_01", 5.1f, 5.6f, 90f);
            Topple(Put(r, "SM_Prop_Wardrobe_02", 7.9f, 5.9f, -90f), -90f);
            Scatter(r, new[] { "SM_Gen_Prop_Bottle_01", "SM_Gen_Prop_Bottle_03", "SM_Gen_Prop_Bottle_05" }, 4.8f, 1.8f, 8.2f, 5.6f, 5, 90f);
            Scatter(r, Papers, 4.6f, 1.6f, 8.2f, 6f, 4);
            OnWall(r, "SM_Prop_Light_Wall_02", 'W', 1.6f, 1.6f, 0f);
            Lamp(r, "Lantern", WallPoint(r, 'W', 1.6f, 1.7f, 0.35f), Candle, 0.9f, 4f);
            PutOn(r, "SM_Prop_Lamp_03", 5.0f, 4.2f, 0f, 0f).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Neglect(r, 4, 2);
        }

        // ================================================================ CELLAR CELLS
        static void DressCells()
        {
            var r = Get("cells");
            KeepClear("cells", -13f, 2.6f, 3.8f, 4.6f);    // the aisle's middle
            KeepClear("cells", -13f, 4.6f, -10.2f, 9f);    // passage to the butcher's room
            KeepClear("cells", 1.0f, 4.6f, 3.8f, 9f);      // passage to the boiler room
            // cell contents: cots and buckets, many overturned
            float[] south = { -11.6f, -8.8f, -6.0f, -3.2f, -0.4f, 2.4f };
            for (int i = 0; i < south.Length; i++)
            {
                var bed = Put(r, "SM_Prop_Bed_Small_01", south[i] + 0.4f, -1.3f, 90f);
                if (i % 2 == 1) Topple(bed, 75f);
                Put(r, Pick("SM_Prop_Guts_Bucket_02", "SM_Gen_Prop_Pot_04", "SM_Prop_Stool_01"), south[i] - 0.8f, 0.3f, Rf(0, 360));
                OnWall(r, "SM_Prop_Shackle_01", 'S', south[i] - 0.6f, 1.6f, 0.0f);
            }
            float[] north = { -8.8f, -6.0f, -3.2f, -0.4f };
            for (int i = 0; i < north.Length; i++)
            {
                var bed = Put(r, "SM_Prop_Bed_Small_01", north[i] - 0.4f, 8.2f, -90f);
                if (i % 2 == 0) Topple(bed, -70f);
                OnWall(r, "SM_Prop_Shackle_01", 'N', north[i] + 0.6f, 1.6f, 0.0f);
                Scatter(r, new[] { "SM_Gen_Env_Leaves_Pile_01", "SM_Gen_Prop_Sack_04" }, north[i] - 1.2f, 6.4f, north[i] + 1.2f, 8.8f, 1);
            }
            // the riot: a guard's table and chairs overturned in the aisle
            Topple(Put(r, "SM_Prop_Table_03", -5.4f, 5.4f, 12f), 90f);
            Topple(Put(r, "SM_Prop_Chair_01", -4.0f, 5.3f, 70f), -90f);
            Topple(Put(r, "SM_Prop_Chair_01", -7.4f, 1.7f, 250f), 90f);
            Topple(Put(r, "SM_Gen_Prop_Barrel_Wood_01", 0.4f, 5.4f, 0f), 90f);
            Put(r, "SM_Gen_Prop_Barrel_Wood_02", -12.4f, 1.6f, 0f);
            Scatter(r, Papers, -12.5f, 1.2f, 3.5f, 5.8f, 6);
            Scatter(r, new[] { "SM_Gen_Prop_Plank_01", "SM_Gen_Prop_Plank_02", "SM_Gen_Prop_Bottle_02" }, -12.5f, 1.2f, 3.5f, 5.8f, 6, 10f);
            Scatter(r, Rocks, -12.5f, 1.2f, 3.5f, 5.8f, 5, 0f, 0.12f, 0.25f);
            // caged bulbs along the aisle
            foreach (var x in new[] { -9.8f, -3.6f, 2.2f })
            {
                Hang(r, "SM_Prop_Light_Bulb_01", x, 3.6f, 0.6f);
                Lamp(r, "Bulb", r.P(x, 1.95f, 3.6f), Cold, 1.1f, 6.5f, x > 0f);
            }
            foreach (var x in new[] { -12f, -6.5f, -0.8f }) Lamp(r, "AisleFill", r.P(x, 1.3f, 3.5f), Cold, 0.45f, 5.5f);
            Lamp(r, "CellGlow", r.P(-6.0f, 2.0f, -0.5f), Cold, 0.35f, 4f);
            Lamp(r, "CellGlow", r.P(-3.2f, 2.0f, 7.6f), Cold, 0.35f, 4f);
            Neglect(r, 10, 4);
        }

        // ================================================================ BUTCHER'S ROOM
        static void DressButcher()
        {
            var r = Get("butcher");
            KeepClear("butcher", -12.4f, 9.4f, -10.8f, 11.4f);
            KeepClear("butcher", -11.6f, 10.6f, -8.2f, 17.4f);
            // the block, long dry
            Box(r.props, "ButchersBlock", new Vector3(-7.4f, 0f, 12.7f), new Vector3(-6.4f, 0.86f, 13.7f), s_Surf["planks_dark"], r.RL);
            Box(r.props, "ButchersBlockFoot", new Vector3(-7.5f, 0f, 12.6f), new Vector3(-6.3f, 0.12f, 13.8f), s_Surf["wood_dark"], r.RL, 0f, false);
            Put(r, "SM_Prop_Decal_Floor_Large", -6.9f, 13.2f, 70f, 0.87f, 0.75f, null, null, null, false);
            var cleaver = Put(r, "SM_Wep_Knife_Butcher_01", -6.85f, 13.25f, 0f, 0.95f, 1.3f, null, null, null, false);
            cleaver.transform.rotation = Quaternion.Euler(0f, 30f, 100f);
            cleaver.transform.position = new Vector3(-6.85f, 1.02f, 13.25f);
            Put(r, "SM_Prop_Decal_Floor_Large", -6.8f, 13.3f, 30f, 0f, 2.2f, null, null, null, false);
            Put(r, "SM_Prop_Decal_Floor_Large", -7.6f, 14.6f, 110f, 0f, 1.4f, null, null, null, false);
            // meat hooks on chains along the west half
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 2; j++)
                {
                    float x = -12.3f + j * 0.9f, z = 11.6f + i * 1.3f;
                    var hook = Put(r, j == 0 ? "SM_Prop_Hook_01" : "SM_Prop_Hook_02", x, z, Rf(0, 360), r.h, 1f, new Vector3(1f, 0.55f, 1f), null, null, false);
                    if ((i + j) % 3 == 0)
                    {
                        var meat = Put(r, "SM_Gen_Prop_Food_Meat_0" + (1 + (i + j) % 6), x, z, Rf(0, 360), 1.05f, 1.6f, null, null, null, false);
                        meat.transform.rotation = Quaternion.Euler(90f, Rf(0, 360), 0f);
                    }
                }
            Box(r.props, "HookRail", new Vector3(-12.45f, r.h - 0.12f, 11.2f), new Vector3(-12.15f, r.h - 0.02f, 16.2f), s_Surf["iron"], r.RL, 0f, false);
            Box(r.props, "HookRail", new Vector3(-11.55f, r.h - 0.12f, 11.2f), new Vector3(-11.25f, r.h - 0.02f, 16.2f), s_Surf["iron"], r.RL, 0f, false);
            // chains, cages
            foreach (var (x, z) in new[] { (-8.0f, 11.0f), (-7.2f, 15.8f), (-9.6f, 12.4f) })
                Put(r, "SM_Gen_Prop_Chain_01", x, z, 0f, r.h - 1.6f, 1f, new Vector3(1f, 0.9f, 1f), null, null, false);
            Put(r, "SM_Prop_Cage_01", -5.95f, 16.2f, 90f);
            Put(r, "SM_Prop_Cage_01", -5.95f, 10.5f, 90f);
            Topple(Put(r, "SM_Prop_Cage_01", -7.4f, 16.3f, 10f), 0f, 0f);
            // knives and buckets
            OnWall(r, "SM_Prop_Shelf_01", 'E', 13.0f, 1.5f, 0f, 1f);
            foreach (var dz in new[] { -0.4f, 0f, 0.4f })
            {
                var p = WallPoint(r, 'E', 13.0f + dz, 1.3f, 0.06f);
                var k = Put(r, dz == 0f ? "SM_Wep_Knife_Butcher_01" : "SM_Wep_Knife_01", p.x, p.z, -90f, 1.3f, 1f, null, null, null, false);
                k.transform.rotation = Quaternion.Euler(0f, -90f, -90f);
            }
            Put(r, "SM_Prop_Guts_Bucket_01", -7.8f, 12.0f, 0f);
            Put(r, "SM_Prop_Guts_Bucket_02", -5.6f, 13.6f, 0f);
            Put(r, "SM_Prop_Sink_02", -9.8f, r.z1, 180f).transform.position = new Vector3(-10.4f, 0f, r.z1 - 0.05f);
            Topple(Put(r, "SM_Prop_Table_04", -9.5f, 13.9f, 90f), 90f);
            Scatter(r, new[] { "SM_Prop_Decal_Floor_Large" }, -12.5f, 10f, -5.5f, 17f, 3, 0f, 0.8f, 1.4f);
            Hang(r, "SM_Prop_Light_Bulb_01", -8.5f, 13.4f, 0.6f);
            Lamp(r, "Bulb", r.P(-8.5f, 1.9f, 13.4f), new Color(1f, 0.55f, 0.45f), 1.3f, 7f, true);
            Lamp(r, "Fill", r.P(-11.2f, 1.6f, 10.4f), new Color(1f, 0.6f, 0.5f), 0.5f, 5f);
            Lamp(r, "Fill", r.P(-9.6f, 1.6f, 16.2f), new Color(1f, 0.6f, 0.5f), 0.5f, 5f);
            Neglect(r, 5, 4);
        }

        // ================================================================ BOILER ROOM
        static void DressBoiler()
        {
            var r = Get("boiler");
            KeepClear("boiler", 1.6f, 9.4f, 3.2f, 11.4f);
            // the furnace: a brick housing whose flue climbs to the ceiling, the iron furnace in front
            Box(r.props, "FurnaceHousing", new Vector3(-1.2f, 0f, 15.2f), new Vector3(1.8f, 2.2f, r.z1), s_Surf["brick_dark"], r.RL);
            Box(r.props, "FurnaceCrown", new Vector3(-1.35f, 2.2f, 15.05f), new Vector3(1.95f, 2.4f, r.z1), s_Surf["stone_trim"], r.RL, 0f, false);
            Box(r.props, "Flue", new Vector3(-0.2f, 2.4f, 15.9f), new Vector3(0.8f, r.h, r.z1), s_Surf["brick_dark"], r.RL);
            Box(r.props, "FireDoor", new Vector3(-0.45f, 0.45f, 15.12f), new Vector3(1.05f, 1.45f, 15.2f), s_Surf["iron"], r.RL, 0f, false);
            Box(r.props, "AshDoor", new Vector3(-0.25f, 0.08f, 15.12f), new Vector3(0.85f, 0.38f, 15.2f), s_Surf["iron"], r.RL, 0f, false);
            Box(r.props, "FireDoorBar", new Vector3(-0.6f, 0.92f, 15.04f), new Vector3(1.2f, 0.98f, 15.12f), s_Surf["iron"], r.RL, 0f, false);
            Put(r, "SM_Prop_Furnace_01", 3.2f, 16.6f, 180f, 0f, 1.25f);
            // pipes along the walls
            for (float x = -4.0f; x < 6.0f; x += 2.5f)
            {
                var p = Put(r, "SM_Gen_Bld_Pipe_Straight_02", x, r.z1 - 0.25f, 0f, 3.1f, 1f, null, null, null, false);
                p.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            for (float z = 10.0f; z < 17f; z += 2.4f)
            {
                Put(r, "SM_Gen_Bld_Pipe_Straight_01", r.x0 + 0.25f, z, 90f, 2.6f, 1f, null, null, null, false);
                Put(r, "SM_Gen_Bld_Pipe_Straight_01", r.x1 - 0.25f, z, 90f, 3.0f, 1f, null, null, null, false);
            }
            Put(r, "SM_Gen_Bld_Pipe_Valve_01", r.x1 - 0.25f, 12.4f, -90f, 1.4f, 1f, null, null, null, false);
            Put(r, "SM_Gen_Bld_Pipe_Valve_01", r.x0 + 0.25f, 15.0f, 90f, 1.3f, 1f, null, null, null, false);
            // coal heaps, tools where they fell
            int coal0 = r.props.childCount;
            foreach (var (x, z, s) in new[] { (-3.2f, 16.0f, 0.55f), (-2.4f, 16.6f, 0.45f), (-3.6f, 14.8f, 0.4f), (-3.9f, 16.7f, 0.5f) })
                Put(r, Pick(Rocks), x, z, Rf(0, 360), -0.08f, s);
            Scatter(r, Rocks, -4.2f, 13.6f, -1.4f, 17f, 14, 0f, 0.06f, 0.14f);
            var coal = MakeMat("coal", null, new Color(0.05f, 0.05f, 0.055f), 0.45f);
            for (int i = coal0; i < r.props.childCount; i++)
                foreach (var rend in r.props.GetChild(i).GetComponentsInChildren<Renderer>()) rend.sharedMaterial = coal;
            Put(r, "SM_Gen_Wep_Spade_01", -1.6f, 14.2f, 0f, 0.05f, 1f, null, null, null, false).transform.rotation = Quaternion.Euler(0f, 30f, 90f);
            Put(r, "SM_Gen_Wep_Pickaxe_01", 4.0f, 12.4f, 0f, 0.05f, 1f, null, null, null, false).transform.rotation = Quaternion.Euler(0f, -60f, 90f);
            Put(r, "SM_Wep_Spanner_01", 2.6f, 13.0f, 40f, 0.01f, 1.2f, null, null, null, false);
            Put(r, "SM_Wep_Hacksaw_01", -0.6f, 12.2f, 110f, 0.01f, 1f, null, null, null, false);
            Put(r, "SM_Wep_Screw_Driver_01", 0.4f, 11.6f, 200f, 0.02f, 1f, null, null, null, false).transform.rotation = Quaternion.Euler(0f, 200f, 90f);
            Put(r, "SM_Prop_Kitchen_Bench_01", 5.7f, 13.0f, -90f);
            PutOn(r, "SM_Prop_Lamp_03", 5.6f, 12.4f, 0.96f);
            Put(r, "SM_Gen_Prop_Barrel_Metal_03", -3.8f, 10.3f, 0f);
            Put(r, "SM_Gen_Prop_Barrel_Metal_01", -3.0f, 10.2f, 0f);
            Topple(Put(r, "SM_Gen_Prop_Barrel_Metal_02", -1.8f, 10.8f, 70f), 90f);
            Put(r, "SM_Prop_Wood_Stack_01", 4.8f, 16.6f, 0f);
            Put(r, "SM_Prop_Radiator_01", -4.4f, 12.0f, 90f).transform.position = new Vector3(r.x0 + 0.15f, 0f, 12.0f);
            Hang(r, "SM_Prop_Light_Bulb_01", 1.0f, 12.2f, 0.6f);
            Lamp(r, "Bulb", r.P(1.0f, 3.05f, 12.2f), new Color(0.9f, 0.8f, 0.7f), 1.4f, 8f, true);
            Lamp(r, "Fill", r.P(-3.0f, 2.0f, 15.6f), Cold, 0.4f, 5f);
            Lamp(r, "DoorFill", r.P(2.4f, 1.5f, 11.0f), Cold, 0.5f, 5.5f);
            Lamp(r, "FurnaceFill", r.P(1.0f, 2.2f, 13.6f), new Color(0.9f, 0.75f, 0.6f), 0.7f, 6f);
            Lamp(r, "EastFill", r.P(4.8f, 2.0f, 14.5f), Cold, 0.4f, 5f);
            Neglect(r, 6, 3);
        }

        // ================================================================ KITCHEN
        static void DressKitchen()
        {
            var r = Get("kitchen");
            KeepClear("kitchen", -9.8f, 17.8f, -8.2f, 19.6f);
            KeepClear("kitchen", -9.6f, 19.0f, -3.4f, 26.6f);
            KeepClear("kitchen", -8.8f, 25.4f, -7.2f, 27f);
            // the cold range on the west wall
            Put(r, "SM_Prop_Stove_01", r.x0 + 0.5f, 22.6f, 90f);
            Put(r, "SM_Prop_Stove_02", r.x0 + 0.45f, 20.2f, 90f);
            PutOn(r, "SM_Prop_Kitchen_Pan_01", r.x0 + 0.6f, 22.4f, 0.98f);
            PutOn(r, "SM_Prop_Kettle_01", r.x0 + 0.55f, 23.0f, 0.98f, 40f);
            // worktables with pans and crockery
            foreach (var z in new[] { 21.2f, 23.8f })
            {
                Put(r, "SM_Prop_Kitchen_Bench_01", -11.0f, z, 90f);
                PutOn(r, Pick("SM_Prop_Bowl_01", "SM_Prop_Bowl_02", "SM_Prop_Bowl_03"), -11.2f, z - 0.6f, 0.96f);
                PutOn(r, "SM_Prop_Kitchen_Pan_02", -10.8f, z + 0.3f, 0.96f, Rf(0, 360));
                PutOn(r, "SM_Gen_Prop_Food_Vegetable_0" + Ri(1, 7), -11.1f, z + 0.7f, 0.96f, Rf(0, 360));
                PutOn(r, "SM_Prop_Plate_02", -10.9f, z - 0.2f, 0.96f);
                var rack = Hang(r, z < 22f ? "SM_Prop_Kitchen_Pan_Rack_01" : "SM_Prop_Kitchen_Pan_Rack_02", -11.0f, z, 1f, 0.4f, false);
                rack.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                rack.transform.position = new Vector3(-11.0f, r.h - 0.75f, z);
            }
            // shelves of crockery on the north wall, much of it on the floor
            OnWall(r, "SM_Prop_Shelf_03", 'N', -12.0f, 1.25f, 0f, 1f, null, true);
            OnWall(r, "SM_Prop_Shelf_03", 'N', -5.4f, 1.25f, 0f, 1f, null, true);
            foreach (var x in new[] { -12.3f, -11.8f, -5.7f, -5.1f })
            {
                PutOn(r, "SM_Prop_Plate_01", x, r.z1 - 0.15f, 0.7f, 0f).transform.rotation = Quaternion.Euler(-75f, 0f, 0f);
                PutOn(r, Pick("SM_Prop_Bowl_01", "SM_Prop_Bowl_03"), x, r.z1 - 0.15f, 1.38f);
            }
            Scatter(r, new[] { "SM_Prop_Plate_01", "SM_Prop_Plate_02", "SM_Prop_Plate_03", "SM_Prop_Bowl_02", "SM_Prop_Spoon_01", "SM_Prop_Fork_01" }, -12.8f, 18f, -3.6f, 26.8f, 18, 70f);
            // the sink, sacks spilled, barrels, rotting food
            OnWall(r, "SM_Prop_Sink_02", 'E', 19.6f, 0f, 0.05f, 1f, null, true);
            Put(r, "SM_Gen_Prop_Sack_Stack_01", -12.3f, 26.2f, 20f);
            Topple(Put(r, "SM_Gen_Prop_Sack_02", -10.8f, 26.3f, 60f), 90f);
            Put(r, "SM_Gen_Prop_Barrel_Wood_03", -4.0f, 18.4f, 0f);
            Put(r, "SM_Gen_Prop_Barrel_Wood_01", -4.9f, 18.3f, 0f);
            Topple(Put(r, "SM_Prop_Chair_01", -7.4f, 20.6f, 30f), -90f);
            Put(r, "SM_Prop_Table_04", -5.5f, r.z1 - 0.4f, 0f);
            Scatter(r, new[] { "SM_Gen_Prop_Food_Vegetable_01", "SM_Gen_Prop_Food_Vegetable_03", "SM_Gen_Prop_Food_Bread_01", "SM_Prop_Can_01" }, -12.6f, 18.2f, -3.8f, 26.6f, 8, 80f);
            Hang(r, "SM_Prop_Light_Bulb_01", -8.0f, 22.4f, 0.6f);
            Lamp(r, "Bulb", r.P(-8.0f, 2.1f, 22.4f), new Color(0.85f, 0.9f, 1f), 1.4f, 8f, true);
            Lamp(r, "RangeFill", r.P(-11.6f, 1.8f, 24.6f), Cold, 0.5f, 5f);
            Lamp(r, "Window", r.P(-5.0f, 2.0f, 21.0f), Moon, 0.9f, 7f);
            Neglect(r, 6, 4);
        }

        // ================================================================ STOREROOM
        static void DressStore()
        {
            var r = Get("store");
            KeepClear("store", -8.8f, 27.4f, -7.2f, 30.4f);
            Put(r, "SM_Prop_Sheet_Cover_01", -11.2f, 32.2f, 10f);
            Put(r, "SM_Prop_Sheet_Cover_04", -5.6f, 32.2f, -15f);
            Put(r, "SM_Prop_Sheet_Cover_07", -11.6f, 29.4f, 80f);
            Put(r, "SM_Prop_Sheet_Cover_08", -8.2f, 32.6f, 0f);
            Put(r, "SM_Gen_Prop_Crate_Preset_01", -4.4f, 28.6f, 90f);
            Put(r, "SM_Gen_Prop_Crate_02", -4.3f, 30.2f, 20f);
            Put(r, "SM_Prop_Box_Pile_01", -12.3f, 31.0f, 0f);
            Put(r, "SM_Prop_Box_Pile_02", -6.0f, 29.0f, 140f);
            Put(r, "SM_Prop_Chest_01", -10.0f, 30.4f, 160f);
            Put(r, "SM_Gen_Prop_Chest_01", -9.0f, 31.6f, 200f);
            Put(r, "SM_Prop_Suitcase_01", -6.6f, 30.6f, 30f, 0f).transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Topple(Put(r, "SM_Prop_Suitcase_02", -10.6f, 28.4f, 110f), 90f);
            Put(r, "SM_Prop_Wardrobe_01", -12.5f, 33.0f, 90f);
            Put(r, "SM_Prop_Rocking_Horse_01", -7.0f, 33.2f, 200f);
            Put(r, "SM_Prop_Mirror_04", -3.9f, 33.2f, -110f);
            Scatter(r, new[] { "SM_Prop_Cardboard_Box_03", "SM_Prop_Book_Pile_04", "SM_Gen_Prop_Papers_02", "SM_Prop_Picture_Frame_02" }, -12.6f, 28f, -3.8f, 33.6f, 10, 60f);
            Hang(r, "SM_Prop_Light_Bulb_01", -8.2f, 30.6f, 0.6f);
            Lamp(r, "Bulb", r.P(-8.2f, 2.6f, 30.6f), Warm, 1.4f, 8.5f, true);
            Lamp(r, "Fill", r.P(-5.0f, 1.6f, 28.6f), Warm, 0.5f, 5f);
            Neglect(r, 6, 4);
        }

        // ================================================================ BACK STAIR
        static void DressBackStair()
        {
            var r = Get("stair");
            KeepClear("stair", -2.6f, 19.4f, 1.6f, 32.4f);
            Put(r, "SM_Prop_Serving_Trolley_01", -2.4f, 31.8f, 90f);
            // the servery: a dresser and a cupboard along the passage, stores under the landing
            Put(r, "SM_Prop_Cabinet_01", -2.55f, 28.6f, 90f);
            PutOn(r, "SM_Prop_Plate_Set_01", -2.5f, 28.2f, 2.1f, 90f);
            Put(r, "SM_Prop_Dresser_02", -2.62f, 23.1f, 90f);
            PutOn(r, "SM_Prop_Candle_Stand_02", -2.6f, 22.9f, 0.92f, 90f);
            Put(r, "SM_Prop_Table_04", -2.68f, 30.6f, 90f);
            PutOn(r, "SM_Prop_Bowl_02", -2.7f, 30.4f, 0.92f);
            Put(r, "SM_Gen_Prop_Crate_02", -2.4f, 20.0f, 15f, 0f, 0.85f);
            Put(r, "SM_Gen_Prop_Sack_Stack_02", -1.5f, 19.95f, 0f);
            Put(r, "SM_Gen_Prop_Barrel_Wood_02", -2.5f, 21.2f, 0f);
            Put(r, "SM_Prop_Wardrobe_02", 0.95f, 19.85f, 0f);
            OnWall(r, "SM_Prop_Shelf_02", 'W', 30.6f, 1.4f, 0f, 1f, null, true);
            PutOn(r, "SM_Prop_Plate_Set_01", -2.8f, 30.6f, 1.22f, 90f);
            OnWall(r, "SM_Prop_Light_Wall_02", 'W', 22.4f, 2.1f, 0f);
            Lamp(r, "PassageLamp", WallPoint(r, 'W', 22.4f, 2.2f, 0.4f), Candle, 1.0f, 6f);
            Lamp(r, "FootLamp", r.P(0.8f, 2.6f, 31.6f), Candle, 0.8f, 5f);
            Lamp(r, "LandingLamp", r.P(-1.0f, DeckTop + 2.2f, 21.8f), Candle, 1.2f, 7f);
            Lamp(r, "StairLamp", r.P(0.8f, 4.6f, 27.8f), Candle, 0.9f, 6f);
            Lamp(r, "UnderLanding", r.P(-0.4f, 2.6f, 21.6f), Candle, 0.7f, 5f);
            Lamp(r, "PassageFill", r.P(-1.6f, 2.4f, 27.0f), Candle, 0.6f, 6f);
            OnWall(r, "SM_Prop_Light_Wall_02", 'W', 21.8f, DeckTop + 1.9f, 0f);
            Portrait(r, 'W', 23.0f, DeckTop + 1.6f, 0.7f, 2, 5);
            Neglect(r, 4, 3);
        }
    }
}
