// IronCitadel conformance kit v2: every threshold in one place. Values marked (v1) are kit v1's, kept where the
// brief v2 line is the same; the rest are v2's own, and README.md says why.
namespace IronCitadel.ConformanceV2
{
    public static class Tune
    {
        // ---- the agent, for the bake and the walker (v1: the shared walker's PlayerCapsule)
        public const float AgentRadius = 0.35f, AgentHeight = 1.8f, AgentClimb = 0.3f, AgentSlope = 45f;
        public const float VoxelSize = 0.1f;              // (v1)
        public const float MinRegionArea = 2f;            // (v1) m2; smaller islands are dropped by the baker
        public const float BakeMargin = 20f;              // m baked round the map's outlines (v1 baked 45 m round its grid)
        public const float BakeBottom = -10f, BakeTop = 60f;   // (v1) round the lowest and highest floor_y

        // ---- the NavMesh graph (v1)
        public const float WeldXZ = 0.05f, WeldY = 0.35f, TJunctionMinOverlap = 0.05f;
        public const float TriHeightSlack = 1.5f;         // m a NavMesh polygon may sit off the real surface
        public const float LevelComponentMinFloorArea = 25f;   // m2 of room floor an island needs to count as level space

        // ---- rooms from the map (v2)
        public const float RoomBandLow = -1f, RoomBandHigh = 2.5f;   // a point is in a room inside its outline, this far from floor_y
        public const float GalleryBand = 1f;              // ... and on the gallery inside the deck outline, this close to deck_y
        public const float GalleryRoomOverlap = 0.5f;     // (1.1) an "extra" room at deck_y this much inside the deck outline is the gallery
        public const float FloorBandLow = -1f, FloorBandHigh = 1f;   // (v1) "floor level"
        public const float FloorProbeStep = 1.5f;         // (v1) m between the samples that find a room's floor height
        public const float GapJoin = 1f;                  // contact edges closer than this are one crossing
        public const float PocketReportArea = 10f;        // m2: unassigned NavMesh touching one room, reported from this size
        public const float DoorMatchSlack = 2f;           // a crossing matches a listed door within width / 2 + this
        public const float DoorMatchYBand = 1.5f;         // ... at points within this of the door's height
        public const float StartSnapRadius = 4f;          // (v1)

        // ---- doors: the E contract, and v1 check 5 for the hidden door
        public const float EyeHeight = 1.6f;              // (v1)
        public const float InteractReach = 3f;            // E: a 3 m ray from eye height, SendMessageUpwards("Interact")
        public const float DoorSideSearch = 4f;           // m round a shut door searched for floor on each side
        public const float EAimSpread = 0.25f;            // ... and E aimed at the door, then this far either side along it (a pair's seam)
        public const float ShutDoorInteractRadius = 1.5f; // also opened: Interact() colliders this close to a shut door
        public const float VaultInteractReach = 8f;       // (v1) the hidden door: Interact() colliders within this
        public const float VaultDisableRadius = 1.5f;     // (v1) fallback when there is none: colliders this close ...
        public const float VaultFloorClearance = 0.3f;    // (v1) ... whose top is above floor + this ...
        public const float VaultDoorHeight = 3.5f;        // (v1) ... up to this above the floor ...
        public const float VaultSkipLargerThan = 50f;     // (v1) ... and no wider than this
        public const int ReceiverMaxColliders = 50;       // a receiver holding more colliders than this opens only the one hit
        public const float ReceiverMaxSize = 12f;         // ... or one wider than this (m)

        // ---- 1: the armory
        public const float DoorTargetLift = 1f;           // a doorway is looked at this far above its floor
        public const float ChestHeight = 1.3f;            // an enemy is seen when a line reaches this far above its feet
        public const float ChokeSightBack = 5f;           // 1b: entry-hall samples further than this from the doorway
        public const float UnderGalleryReach = 1.5f;      // the flank doorway: under the deck outline or this close to it ...
        public const float FlankRouteReach = 8f;          // ... at its crossing, or on its route into the hall within this (a passage through the wall)
        public const float ChokeFacingDot = 0.5f;         // information: a choke guard faces the doorway within 60 degrees
        public const float ChokeGuardReach = 8f;          // information: ... and stands this close to it

        // ---- 3: the gallery
        public const float GalleryMinRise = 3f;           // deck_y at least this above the hall floor ...
        public const float GalleryRiseSlack = 0.05f;      // ... less this: NavMesh heights carry centimetres (a 3 m storey's deck reads 2.99 to 3.0)
        public const float GalleryMinArea = 20f;          // (v1) m2 of deck NavMesh
        public const float HallFloorInset = 3f;           // the hall floor: this far inside the hall outline ...
        public const float HallFloorDeckClear = 1.5f;     // ... and further than this from the deck outline (not beneath it)
        public const float HallFloorBandHigh = 1f;        // ... from floor_y - 1 up to floor_y + this
        public const float RailLean = AgentRadius + 0.2f; // (v1)
        public const float RailSampleStep = 2f;           // (v1)
        public const float RailSearch = 2f;               // m from the rail line searched for deck NavMesh
        public const float HallOpenAbove = 1f;            // (v1) floor samples under a lid lower than deck + this are skipped
        public const float FloorSampleStep = 2f;          // (v1)
        public const float FloorTargetLift = 0.2f;        // (v1)
        public const float MinVisibleFraction = 0.75f;    // (v1) Roger, 2026-10-13; information only since 2026-10-14 (row 3c)

        // ---- 4: the stage
        public const float StageMinRise = 0.4f;
        public const float StageSampleStep = 0.5f;
        public const float FarEndFraction = 0.66f;        // information: the throne beyond this share of the room's depth

        // ---- 6 and 7: heights and ceilings (v1)
        public const float CeilingSampleStep = 3f, CeilingMaxRay = 60f;
        public const int CeilingMinSamples = 10;          // fewer at 3 m and the room is sampled every 1 m
        public const float LowCeilingMax = 6.5f, LowMinFraction = 0.75f;
        public const float TallCeilingMin = 8f, TallMinFraction = 0.25f;
        public const float HeightSlack = 0.05f;           // (1.1) both bars give this: ray heights carry centimetres, as GalleryRiseSlack
        public const float HeightTargetPercentile = 0.9f;
        public const float TallestSlack = 0.5f;           // no room's p90 ceiling above the throne room's p90 + this
        public const float CeilingMinFraction = 0.95f;    // 7: share of each room's floor samples with a ceiling

        // ---- 9: occluders
        public const float CoverMinWidth = 0.6f, CoverMaxWidth = 8f;
        public const float CoverTall = 1.8f, CoverLowMin = 0.8f, CoverLowMax = 1.3f;
        public const float CoverBaseTolerance = 0.35f;    // its bottom this close to the room's floor: it stands on it
        public const float CoverClear = 0.6f;             // floor looked for this far out from each side
        public const int CoverMinOpenSides = 5;           // of 8 directions: away from walls

        // ---- 10: walks (v1)
        public const float WalkSpeed = 3f, WalkDt = 0.05f, WalkReach = 0.3f;
        public const float WalkStuckSeconds = 2f, WalkStuckProgress = 0.05f;
        public const float WalkFallDrop = 2.5f, WalkEndTolerance = 1f, WalkSkin = 0.02f;
        public const float PathSampleStep = 0.5f;         // (v1)
        public const float PickupSnap = 4f;               // m from a pickup to the level floor point a walk ends at

        // ---- information
        public const float SceneObjectNear = 1f;          // a marker's scene object within this
        public const float FloorYTolerance = 0.3f;        // map floor_y against the measured floor

        // ---- NavMesh areas the kit marks with modifier boxes (no project setting needed)
        public const int HallFloorArea = 28, ArmoryArea = 29;
    }
}
