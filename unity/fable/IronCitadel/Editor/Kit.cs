using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IronCitadel.Editor
{
    /// <summary>Prefab paths for the Synty pieces the citadel uses, plus placement helpers.</summary>
    public static class Kit
    {
        public const string DJ = "Assets/Synty/PolygonDungeon/Prefabs/";
        public const string DR = "Assets/Synty/PolygonDungeonRealms/Prefabs/";
        public const string DM = "Assets/Synty/PolygonDungeonMap/Prefabs/";
        public const string GN = "Assets/Synty/PolygonGeneric/Prefabs/";

        // structure (PolygonDungeon). Walls: 5 m wide, 5 m tall, pivot at the +x end, front face +z.
        public const string Wall01 = DJ + "Environments/Walls/SM_Env_Wall_01.prefab";
        public const string Wall02 = DJ + "Environments/Walls/SM_Env_Wall_02.prefab";
        public const string Wall03 = DJ + "Environments/Walls/SM_Env_Wall_03.prefab";
        public const string Wall04 = DJ + "Environments/Walls/SM_Env_Wall_04.prefab";
        public const string Wall05 = DJ + "Environments/Walls/SM_Env_Wall_05.prefab";
        public const string Wall06 = DJ + "Environments/Walls/SM_Env_Wall_06.prefab";
        public const string WallDoorSingle = DJ + "Environments/Walls/SM_Env_Wall_DoorFrame_01.prefab";
        public const string WallDoorDouble = DJ + "Environments/Walls/SM_Env_Wall_DoorFrame_Double_Round_01.prefab";
        public const string WallDoorDoubleFlat = DJ + "Environments/Walls/SM_Env_Wall_DoorFrame_Double_01.prefab";
        public const string WallArchway = DJ + "Environments/Walls/SM_Env_Wall_Archway_01.prefab";
        public const string WallFireplaceGap = DJ + "Environments/Walls/SM_Env_Wall_Fireplace_Gap_01.prefab";
        public const string WallBeams = DJ + "Environments/Walls/SM_Env_Wall_Wood_Beams_01.prefab";
        public const string DwarfArchway = DR + "Environments/SM_Env_Dwarf_Wall_Archway_01.prefab";
        public const string DwarfArchway3 = DR + "Environments/SM_Env_Dwarf_Wall_Archway_03.prefab";
        public const string DwarfWall = DR + "Environments/SM_Env_Dwarf_Wall_01.prefab";
        public const string WallEnd = DJ + "Environments/Walls/SM_Env_Wall_End_01.prefab";
        public const string WallQuarter = DJ + "Environments/Walls/SM_Env_Wall_Quarter_01.prefab";
        public const string TrimTop = DJ + "Environments/Walls/SM_Env_Wall_Trim_Top_02.prefab";

        public static readonly string[] Floors =
        {
            DJ + "Environments/Floors/SM_Env_Tiles_01.prefab", DJ + "Environments/Floors/SM_Env_Tiles_02.prefab",
            DJ + "Environments/Floors/SM_Env_Tiles_03.prefab", DJ + "Environments/Floors/SM_Env_Tiles_04.prefab",
            DJ + "Environments/Floors/SM_Env_Tiles_05.prefab", DJ + "Environments/Floors/SM_Env_Tiles_06.prefab",
            DJ + "Environments/Floors/SM_Env_Tiles_07.prefab", DJ + "Environments/Floors/SM_Env_Tiles_08.prefab",
            DJ + "Environments/Floors/SM_Env_Tiles_09.prefab", DJ + "Environments/Floors/SM_Env_Tiles_010.prefab",
        };
        public const string FloorPlain = DJ + "Environments/Floors/SM_Env_Tiles_03.prefab";
        public const string FloorOrnate = DJ + "Environments/Floors/SM_Env_Tiles_Ornate_01.prefab";
        public const string FloorDwarf = DR + "Environments/SM_Env_Dwarf_Floor_01.prefab";
        public static readonly string[] Ceilings =
        {
            DJ + "Environments/Walls/SM_Env_Ceiling_Stone_Flat_01.prefab",
            DJ + "Environments/Walls/SM_Env_Ceiling_Stone_Flat_02.prefab",
            DJ + "Environments/Walls/SM_Env_Ceiling_Stone_Flat_03.prefab",
        };
        public const string CeilingBasement = DJ + "Environments/Wood/SM_Env_Basement_Ceiling_01.prefab";
        public const string Stairs = DJ + "Environments/Floors/SM_Env_Stairs_01.prefab";
        public const string Steps = DJ + "Environments/Floors/SM_Env_Steps_01.prefab";
        public const string Railing = DJ + "Environments/Misc/SM_Env_Railing_01.prefab";
        public const string RailingPost = DJ + "Environments/Misc/SM_Env_Railing_Pillar_01.prefab";
        public const string Balustrade = DR + "Environments/SM_Env_Dwarf_Balustrade_01.prefab";   // 2.5 m, 0.87 m tall, open
        public const string Pillar5 = DJ + "Environments/Pillars/SM_Env_Pillar_Square_06.prefab";
        public const string PillarRound = DJ + "Environments/Pillars/SM_Env_Pillar_Round_01.prefab";
        public const string PillarLarge = DJ + "Environments/Walls/SM_Env_Wall_Pillar_Large_01.prefab";
        public const string PillarBase = DJ + "Environments/Pillars/SM_Env_Pillar_Square_Base_01.prefab";
        public const string DwarfPillar = DR + "Environments/SM_Env_Dwarf_Pillar_01.prefab";
        public const string BeamPole = DJ + "Environments/Wood/SM_Env_Basement_Support_Pole_01.prefab";
        public const string Beam = DJ + "Environments/Wood/SM_Env_Basement_Support_Beam_01.prefab";

        // doors and gates
        public const string Portcullis = DJ + "Environments/Misc/SM_Env_Portcullis_01.prefab";
        public const string LargeFrame1 = DJ + "Environments/Walls/SM_Env_Door_Large_Frame_01.prefab";
        public const string LargeFrame2 = DJ + "Environments/Walls/SM_Env_Door_Large_Frame_02.prefab";
        public const string LargeDoorL = DJ + "Environments/Walls/SM_Env_Door_Large_Wood_01.prefab";
        public const string LargeDoorR = DJ + "Environments/Walls/SM_Env_Door_Large_Wood_02.prefab";
        public const string DoubleDoorL = DJ + "Environments/Walls/SM_Env_DoorDouble_Round_01.prefab";
        public const string DoubleDoorR = DJ + "Environments/Walls/SM_Env_DoorDouble_Round_02.prefab";
        public const string DoorBars = DJ + "Environments/Walls/SM_Env_Door_Bars_01.prefab";
        public const string Door1 = DJ + "Environments/Walls/SM_Env_Door_01.prefab";
        public const string FencePanel = DJ + "Props/SM_Prop_Metal_Fence_01.prefab";
        public const string FenceWeak = DJ + "Props/SM_Prop_Metal_Fence_Weak_01.prefab";
        public const string FencePost = DJ + "Props/SM_Prop_Metal_Fence_Post_01.prefab";
        public const string SpikeFence = DJ + "Environments/Misc/SM_Env_Fence_Metal_Spikes_01.prefab";
        public const string SpikeTrap = DJ + "Environments/Misc/SM_Env_Trap_Spikes_01.prefab";

        // lights and fire
        public const string TorchWall = DJ + "Props/SM_Prop_Torch_Ornate_01.prefab";
        public const string TorchStick = DJ + "Props/SM_Prop_TorchStick_01.prefab";
        public const string TorchStand = DR + "Props/SM_Prop_Dwarf_Torch_07.prefab";
        public const string Brazier = DJ + "Props/SM_Prop_Brazier_01.prefab";
        public const string BrazierBig = DR + "Props/SM_Prop_Dwarf_Brazier_01.prefab";
        public const string Bonfire = DJ + "Props/SM_Prop_Bonfire_01.prefab";
        public const string Chandelier = DJ + "Props/Preset/SM_Prop_Candle_Chandelier_02_Preset.prefab";
        public const string ChandelierSmall = DJ + "Props/Preset/SM_Prop_Candle_Chandelier_01_Preset.prefab";
        public const string Chain = DJ + "Props/SM_Prop_Chain_01.prefab";
        public const string ChainShort = DJ + "Props/SM_Prop_Chain_02.prefab";
        public const string CandleStand = DJ + "Props/Preset/SM_Prop_Candle_Stand_01_Preset.prefab";
        public const string Candles1 = DJ + "Props/Preset/SM_Prop_Candles_01_Preset.prefab";
        public const string Candles2 = DJ + "Props/Preset/SM_Prop_Candles_02_Preset.prefab";
        public const string Candles3 = DJ + "Props/Preset/SM_Prop_Candles_03_Preset.prefab";
        public const string Candles4 = DJ + "Props/Preset/SM_Prop_Candles_04_Preset.prefab";
        public const string Candle1 = DJ + "Props/Preset/SM_Prop_Candle_01_Preset.prefab";
        public const string Lantern = DJ + "Props/SM_Prop_Lantern_01.prefab";
        public const string FxFireSmall = DJ + "FX/FX_Fire_02.prefab";
        public const string FxFire = DJ + "FX/FX_Fire_01.prefab";
        public const string FxFireLarge = DR + "FX/FX_Fire_Large_01.prefab";
        public const string FxFireRealmsSmall = DR + "FX/FX_Fire_Small_01.prefab";
        public const string FxSparks = DR + "FX/FX_Sparks_01.prefab";
        public const string FxSteam = DR + "FX/FX_Steam_01.prefab";
        public const string FxEmbers = DR + "FX/FX_Dust_Embers_Small_01.prefab";
        public const string FxGold = DR + "FX/FX_Gold_Sparkle_01.prefab";
        public const string FxMagic = DJ + "FX/FX_Magic_Swirl_01.prefab";
        public const string FxDust = DR + "FX/FX_Dust_Large_Soft_01.prefab";

        // furniture
        public const string Table = DJ + "Props/SM_Prop_Table_01.prefab";
        public const string TableRound = DJ + "Props/SM_Prop_Table_Round_01.prefab";
        public const string TableRoundBroken = DJ + "Props/SM_Prop_Table_Round_Broken_01.prefab";
        public const string StoneTable = DJ + "Props/SM_Prop_StoneTable_01.prefab";
        public const string DwarfTableLong = DR + "Props/SM_Prop_Dwarf_Table_03.prefab";
        public const string DwarfTable = DR + "Props/SM_Prop_Dwarf_Table_02.prefab";
        public const string DwarfBench = DR + "Props/SM_Prop_Dwarf_Chair_Bench_01.prefab";
        public const string DwarfBenchBroken = DR + "Props/SM_Prop_Dwarf_Chair_Bench_Broken_01.prefab";
        public const string Bench = DJ + "Props/SM_Prop_Bench_01.prefab";
        public const string Stool1 = DJ + "Props/SM_Prop_Stool_01.prefab";
        public const string Stool2 = DJ + "Props/SM_Prop_Stool_02.prefab";
        public const string Stool3 = DJ + "Props/SM_Prop_Stool_03.prefab";
        public const string DwarfStool = DR + "Props/SM_Prop_Dwarf_Chair_Stool_01.prefab";
        public const string Chair = DR + "Props/SM_Prop_Chair_01.prefab";
        public const string ChairBroken = DR + "Props/SM_Prop_Chair_Broken_01.prefab";
        public const string StoneChair = DJ + "Props/SM_Prop_StoneChair_01.prefab";
        public const string Bed = DJ + "Props/SM_Prop_Bed_01.prefab";
        public const string DwarfBed = DR + "Props/SM_Prop_Dwarf_Bed_01.prefab";
        public const string Rug1 = DJ + "Props/SM_Prop_Rug_01.prefab";
        public const string Rug2 = DJ + "Props/SM_Prop_Rug_02.prefab";
        public const string Rug3 = DJ + "Props/SM_Prop_Rug_03.prefab";
        public const string RealmRug = DR + "Props/SM_Prop_Rug_01.prefab";
        public const string Bookcase = DJ + "Props/SM_Prop_Bookcase_01.prefab";
        public const string BookcaseSmall = DM + "SM_Prop_Bookcase_Small_01.prefab";
        public const string BookcaseSmall2 = DM + "SM_Prop_Bookcase_Small_02.prefab";
        public const string BookcaseGrand1 = DM + "SM_Prop_Bookcase_Grand_01.prefab";
        public const string BookcaseGrand2 = DM + "SM_Prop_Bookcase_Grand_02.prefab";
        public const string BookcaseGrand3 = DM + "SM_Prop_Bookcase_Grand_03.prefab";
        public const string BookcaseTall1 = DM + "SM_Prop_Bookcase_01.prefab";
        public const string BookcaseTall2 = DM + "SM_Prop_Bookcase_02.prefab";
        public const string BookcaseTall3 = DM + "SM_Prop_Bookcase_03.prefab";
        public const string BookStand = DM + "SM_Prop_Book_Stand_01.prefab";
        public const string BookPile = DM + "SM_Prop_Book_Pile_01.prefab";
        public const string BookOpen = DM + "SM_Prop_Book_Open_01.prefab";
        public const string Papers = DM + "SM_Prop_Papers_01.prefab";
        public const string Globe = DM + "SM_Prop_Globe_01.prefab";
        public const string LibraryLadder = DM + "SM_Prop_Ladder_01.prefab";
        public const string KnightStand = DM + "SM_Prop_KnightStand_01.prefab";
        public const string KnightStandRoyal = DM + "SM_Prop_KnightStand_Royal_01.prefab";
        public const string Shelf = GN + "Props/SM_Gen_Prop_Shelf_02.prefab";
        public const string ShelfWall = GN + "Props/SM_Gen_Prop_Shelf_01.prefab";
        public const string DwarfShelf = DR + "Props/SM_Prop_Dwarf_Shelf_01.prefab";

        // weapons and armour
        public const string WeaponRack = DJ + "Props/SM_Prop_WeaponRack_01.prefab";
        public const string DwarfRack = DR + "Props/SM_Prop_Dwarf_Weapon_Rack_04.prefab";
        public const string DwarfRackLow = DR + "Props/SM_Prop_Dwarf_Weapon_Rack_03.prefab";
        public const string WeaponBarrel = DR + "Props/SM_Prop_Dwarf_Weapon_Barrel_02.prefab";
        public const string HangingWeapon1 = DR + "Props/SM_Prop_Hanging_Weapon_01.prefab";
        public const string HangingWeapon2 = DR + "Props/SM_Prop_Hanging_Weapon_02.prefab";
        public const string ShieldHeater = DJ + "Weapons/SM_Wep_Shield_Heater_01.prefab";
        public const string ShieldOrnate = DJ + "Weapons/SM_Wep_Shield_Ornate_01.prefab";
        public const string OrnateSword = DJ + "Weapons/SM_Wep_Ornate_Sword_01.prefab";
        public const string Spear = DJ + "Weapons/SM_Wep_Spear_01.prefab";
        public const string Sword = DJ + "Weapons/SM_Wep_Straightsword_01.prefab";
        public const string GreatSword = DJ + "Weapons/SM_Wep_GreatSword_01.prefab";
        public const string Axe = DJ + "Weapons/SM_Wep_Axe_01.prefab";
        public const string Halberd = DJ + "Weapons/SM_Wep_Halberd_06.prefab";
        public const string AxeLarge = DR + "Weapons/SM_Wep_Axe_Large_01.prefab";
        public const string SwordLarge = DR + "Weapons/SM_Wep_Sword_Large_01.prefab";

        // containers, food, misc
        public const string Barrel1 = DJ + "Props/SM_Prop_Barrel_01.prefab";
        public const string Barrel2 = DJ + "Props/SM_Prop_Barrel_02.prefab";
        public const string Barrel3 = DJ + "Props/SM_Prop_Barrel_03.prefab";
        public const string BarrelBroken = DJ + "Props/SM_Prop_Barrel_Broken_01.prefab";
        public const string BarrelStack = DR + "Props/SM_Prop_Barrel_Stack_01.prefab";
        public const string BarrelShelf = DR + "Props/SM_Prop_Barrel_Shelf_Small_01.prefab";
        public const string Crate = DJ + "Props/SM_Prop_Crate_Wood_01.prefab";
        public const string Crate2 = DJ + "Props/SM_Prop_Crate_Wood_02.prefab";
        public const string CrateMetal = DJ + "Props/SM_Prop_Crate_Metal_01.prefab";
        public const string CrateOrnate = DJ + "Props/SM_Prop_Crate_Ornate_01.prefab";
        public const string CrateStack = DR + "Props/SM_Prop_Crate_Stack_01.prefab";
        public const string Sack1 = GN + "Props/SM_Gen_Prop_Sack_01.prefab";
        public const string Sack2 = GN + "Props/SM_Gen_Prop_Sack_02.prefab";
        public const string SackStack1 = GN + "Props/SM_Gen_Prop_Sack_Stack_01.prefab";
        public const string SackStack2 = GN + "Props/SM_Gen_Prop_Sack_Stack_02.prefab";
        public const string Chest1 = DJ + "Props/SM_Prop_Chest_01.prefab";
        public const string Chest2 = DJ + "Props/SM_Prop_Chest_02.prefab";
        public const string Chest3 = DJ + "Props/SM_Prop_Chest_03.prefab";
        public const string Chest4 = DJ + "Props/SM_Prop_Chest_04.prefab";
        public const string ChestWood = DJ + "Props/SM_Prop_Chest_Wood_01.prefab";
        public const string TreasureChest = DR + "Props/SM_Prop_Dwarf_TreasureChest_01.prefab";
        public const string GoldPile1 = DR + "Props/SM_Prop_Gold_Pile_01.prefab";
        public const string GoldPile2 = DR + "Props/SM_Prop_Gold_Pile_02.prefab";
        public const string GoldPile3 = DR + "Props/SM_Prop_Gold_Pile_03.prefab";
        public const string GoldPileLarge = DR + "Props/SM_Prop_Gold_Pile_Large_02.prefab";
        public const string GoldCoins = DR + "Props/SM_Prop_Gold_Coins_01.prefab";
        public const string GemCluster = DR + "Props/SM_Prop_Gem_Cluster_01.prefab";
        public const string Gem = DJ + "Props/SM_Prop_Gem_01.prefab";
        public const string VaseGroup = DJ + "Props/SM_Prop_Vase_Group_01.prefab";
        public const string Vase = DJ + "Props/SM_Prop_Vase_01.prefab";
        public const string Goblet = DR + "Props/SM_Prop_Goblet_01.prefab";
        public const string Tankard = DR + "Props/SM_Prop_Tankard_01.prefab";
        public const string Mug = DR + "Props/SM_Prop_Mug_01.prefab";
        public const string Plate = GN + "Props/SM_Gen_Prop_Plate_01.prefab";
        public const string Bread = GN + "Props/SM_Gen_Prop_Food_Bread_01.prefab";
        public const string Meat = GN + "Props/SM_Gen_Prop_Food_Meat_01.prefab";
        public const string Meat2 = GN + "Props/SM_Gen_Prop_Food_Meat_05.prefab";
        public const string Veg = GN + "Props/SM_Gen_Prop_Food_Vegetable_01.prefab";
        public const string Veg2 = GN + "Props/SM_Gen_Prop_Food_Vegetable_03.prefab";
        public const string Pot = GN + "Props/SM_Gen_Prop_Pot_01.prefab";
        public const string Bottle = GN + "Props/SM_Gen_Prop_Bottle_01.prefab";
        public const string CampPot = DR + "Props/SM_Prop_Camp_Pot_02.prefab";
        public const string CampPot2 = DR + "Props/SM_Prop_Camp_Pot_05.prefab";
        public const string Cauldron = DJ + "Props/SM_Prop_Cauldron_01.prefab";
        public const string Rotisserie = DJ + "Props/SM_Prop_Goblin_Rotisserie_Meat_01.prefab";
        public const string FireplaceAlcove = DJ + "Environments/Walls/SM_Env_Wall_Fireplace_Alcove_01.prefab";
        public const string Map = DR + "Props/SM_Prop_Map_01.prefab";
        public const string Scroll = DR + "Props/SM_Prop_Scroll_Open_01.prefab";
        public const string Inkwell = DJ + "Items/SM_Item_Inkwell_01.prefab";
        public const string Scale = DJ + "Items/SM_Item_Scale_01.prefab";
        public const string Jar1 = DJ + "Items/SM_Item_Jar_01.prefab";
        public const string Jar2 = DJ + "Items/SM_Item_Jar_02.prefab";
        public const string Vial1 = DJ + "Items/SM_Item_Vial_01.prefab";
        public const string Vial2 = DJ + "Items/SM_Item_Vial_02.prefab";
        public const string Shrooms = DJ + "Items/SM_Item_Shrooms.prefab";
        public const string Skull = GN + "Props/SM_Gen_Prop_Skull_01.prefab";
        public const string PotionPole = DJ + "Items/SM_Item_Potion_Pole_01.prefab";
        public static readonly string[] Potions =
        {
            DJ + "Items/SM_Item_Potion_01.prefab", DJ + "Items/SM_Item_Potion_02.prefab", DJ + "Items/SM_Item_Potion_03.prefab",
            DJ + "Items/SM_Item_Potion_04.prefab", DJ + "Items/SM_Item_Potion_05.prefab", DJ + "Items/SM_Item_Potion_06.prefab",
            DJ + "Items/SM_Item_Potion_07.prefab", DJ + "Items/SM_Item_Potion_08.prefab", DJ + "Items/SM_Item_Potion_09.prefab",
        };

        // forge
        public const string ForgePit = DJ + "Props/SM_Prop_Forge_FirePit_01.prefab";
        public const string ForgeHood = DJ + "Props/SM_Prop_Forge_Ventilation_01.prefab";
        public const string ForgePipe = DJ + "Props/SM_Prop_Forge_Ventilation_Pipe_01.prefab";
        public const string Anvil = DR + "Props/SM_Prop_Anvil_01.prefab";
        public const string ForgeHammer = DR + "Props/SM_Prop_Forge_Hammer_01.prefab";
        public const string ForgeHammer2 = DR + "Props/SM_Prop_Forge_Hammer_02.prefab";
        public const string Tongs = DR + "Props/SM_Prop_Forge_Tongs_01.prefab";
        public const string Toolstrap = DR + "Props/SM_Prop_Toolstrap_01.prefab";
        public const string Smelter = DR + "Environments/SM_Env_Dwarf_Forge_Smelter_02.prefab";
        public const string OrePile = DR + "Environments/SM_Env_Ore_Pile_01.prefab";
        public const string CogPile = DR + "Props/SM_Prop_Dwarf_Cog_Pile_01.prefab";
        public const string Pickaxe = DR + "Props/SM_Prop_Forge_Pickaxe_01.prefab";

        // prison and guard
        public const string Stocks = DJ + "Props/SM_Prop_Toture_Stocks_01.prefab";
        public const string Cage = DJ + "Props/SM_Prop_Toture_Cage_01.prefab";
        public const string SkeletonShackles = DJ + "Props/SM_Prop_Skeleton_Slave_Shackles_01.prefab";
        public const string SkeletonLying = DJ + "Props/SM_Prop_Skeleton_Slave_Lying_01.prefab";
        public const string SkeletonSitting = DJ + "Props/SM_Prop_Skeleton_Slave_Wall_Sitting_01.prefab";
        public const string Bricks = DJ + "Props/SM_Prop_Bricks_01.prefab";
        public const string Plank = DJ + "Props/SM_Prop_Plank_01.prefab";
        public const string ChainWall = DJ + "Props/SM_Prop_Chain_04.prefab";

        // grand
        public const string Throne = DR + "Props/SM_Prop_Dwarf_Throne_01.prefab";
        public const string Statue1 = DJ + "Environments/Misc/SM_Env_Statue_01.prefab";
        public const string Statue2 = DJ + "Environments/Misc/SM_Env_Statue_02.prefab";
        public const string Statue3 = DJ + "Environments/Misc/SM_Env_Statue_03.prefab";
        public const string RealmStatue1 = DR + "Environments/SM_Env_Statue_01.prefab";
        public const string RealmStatueMale = DR + "Environments/SM_Env_Statue_Male_01.prefab";
        public const string WallBanner1 = DJ + "Props/SM_Prop_Wall_Banner_01.prefab";
        public const string WallBanner2 = DJ + "Props/SM_Prop_Wall_Banner_02.prefab";
        public const string WallBanner3 = DJ + "Props/SM_Prop_Wall_Banner_03.prefab";
        public const string WallBanner4 = DJ + "Props/SM_Prop_Wall_Banner_04.prefab";
        public const string WallBanner5 = DJ + "Props/SM_Prop_Wall_Banner_05.prefab";
        public const string WallBanner6 = DJ + "Props/SM_Prop_Wall_Banner_06.prefab";
        public const string RealmBanner2 = DR + "Props/SM_Prop_Banner_02.prefab";
        public const string RealmBanner4 = DR + "Props/SM_Prop_Banner_04.prefab";
        public const string Drum = DJ + "Props/SM_Prop_Goblin_Drum_01.prefab";
        public const string Horn = DR + "Props/SM_Prop_BigHorn_01.prefab";
        public const string Obelisk = DJ + "Environments/Misc/SM_Env_Obelisk_01.prefab";
        public const string Altar = DJ + "Environments/Misc/SM_Env_Alter_01.prefab";

        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();
        public static readonly List<string> Missing = new List<string>();

        public static GameObject Load(string path)
        {
            if (_cache.TryGetValue(path, out var g)) return g;
            g = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (g == null && !Missing.Contains(path)) Missing.Add(path);
            _cache[path] = g;
            return g;
        }

        /// <summary>Instantiates a prefab (keeping the prefab link) at a world position with a yaw in degrees.</summary>
        public static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, Vector3? scale = null, string name = null)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (name != null) go.name = name;
            return go;
        }

        public static GameObject PlaceRot(string path, Transform parent, Vector3 pos, Quaternion rot, Vector3? scale = null, string name = null)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            if (name != null) go.name = name;
            return go;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
        }

        public static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
