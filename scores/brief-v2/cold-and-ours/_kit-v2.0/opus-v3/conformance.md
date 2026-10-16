# Conformance v2: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Map: `C:/Repos/ColdRuns/opus-v3-score/Map/map.json`
- Run: 2026-10-14T01:34:10Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.0
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 1880 triangles from 961 colliders and 74 modifier boxes, 13 crossings, 0.072 s
- Bake "shut doors opened": 1919 triangles from 956 colliders and 74 modifier boxes, 13 crossings, 0.056 s
- Bake "all doors opened": 1924 triangles from 955 colliders and 74 modifier boxes, 13 crossings, 0.057 s
- Start: map.json start, at (7.5, -0.006, 2.2) in Entry Hall; the scene's own start: object tagged Player: PlayerCapsule at (7.5, 0.02, 2.2)
- Enemy bodies switched off for sight lines and the bake: 0
- Other Interact() receivers opened for the all-doors bake: 0

**Result: 3 pass, 8 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **FAIL** | the armory has 1 openings with every door opened (want 2); no opening joins the armory to the dining hall | the armory has 1 openings with every door opened (want 2); no opening joins the armory to the dining hall |
| 1b | Act one: the armory's guards come into view only at its doorway. | **PASS** | guards seen only within 0.0 m of the doorway; the start sees the doorway, no guard | of 35 entry-hall floor samples, 0 see an armory enemy (eye 1.6 m to chest 1.3 m), all within 5 m of the doorway; the start sees the doorway and none of the 9 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **FAIL** | no path to the dining hall without the armory (PathPartial) | no path to the dining hall without the armory (PathPartial) |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **FAIL** | no way round (row 2), so no way in to start from | no way round (row 2), so no way in to start from |
| 4 | The throne stands on a stage with steps. | **PASS** | throne on a 1.50 m stage, climbed from the floor | the throne lies inside the stage outline; the stage top (180 NavMesh samples) stands 1.50 m above the floor; a complete path climbs onto it from the floor; information, not scored: the throne lies 95% of the depth from Shrine - Throne Room (open) |
| 5 | The vault has one way in, and the player may have to search for it. | **FAIL** | closed: no vault floor on the NavMesh; opened, the vault opens to nothing (want Library only); opened, no floor point in the vault | closed: no vault floor on the NavMesh; opened, the vault opens to nothing (want Library only); opened, no floor point in the vault |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **FAIL** | low (<= 6.5 m, want 75%): Prison 100%, Guard Room 100%, Kitchen 100%; tall (>= 8 m, want 25%): Great Dining Hall 81%, Throne Room 100%; tallest p90: Throne Room 15.1, Shrine 10.2, Servery Stair 10.2 m | Treasure Vault has no measurable ceiling |
| 7 | Everything is indoors, and every room has a ceiling. | **FAIL** | Treasure Vault has no floor samples | Treasure Vault has no floor samples |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **FAIL** | 7 listed doors are not openings with every door opened; Armory - Great Dining Hall (shut): not an opening with every door opened; Entry Hall - Storeroom (shut): not an opening with every door opened; Kitchen - Servery Stair (shut): not an opening with every door opened; Library - Alchemist's Study (shut): not an opening with every door opened; Prison - Torture Chamber (shut): not an opening with every door opened | 7 listed doors are not openings with every door opened; Armory - Great Dining Hall (shut): not an opening with every door opened; Entry Hall - Storeroom (shut): not an opening with every door opened; Kitchen - Servery Stair (shut): not an opening with every door opened; Library - Alchemist's Study (shut): not an opening with every door opened; Prison - Torture Chamber (shut): not an opening with every door opened |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 46 tall and 39 low occluders (per room tall/low: Entry Hall 0/4, Armory 1/0, Storeroom 0/3, Kitchen 1/2, Servery Stair 2/2, Forge 0/5, Guard Room 1/2, Prison 24/0, Torture Chamber 3/1, Great Dining Hall 5/9, Library 5/4, Alchemist's Study 1/3, Shrine 3/1, Crypt 0/2, Throne Room 0/1) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **FAIL** | 1/5 walks reached their end | start to the dining hall, avoiding the armory: no complete NavMesh path for leg 1 (PathPartial) from (7.5, 0, 2.2) to (15, 0, 30); it ends at (15.9, 0, 19.1) in Prison; the way-round entrance up to the gallery deck, avoiding the hall floor: not walked: row 3 found no way in or no deck point reached; the dining hall into the armory beneath the gallery, to the best gear: not walked: the armory has no opening to the dining hall (row 1); into the vault by the hidden way, and back: not walked: no floor point beside the hidden door or in the vault |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Great Dining Hall #9 75% (0.6 m from the rail); ranged in Great Dining Hall #10 77% (0.6 m from the rail); ranged in Great Dining Hall #11 74% (0.6 m from the rail); ranged in Great Dining Hall #12 74% (0.6 m from the rail); ranged in Great Dining Hall #13 71% (0.6 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | the map matches the scene | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | yes: ranged behind the armory entrance (4 ranged, 4 within 8 m, 4 facing it); yes: melee in the rest of the armory (5); yes: ranged on the gallery (5); yes: the dining hall holds the largest group (11; next 9); yes: the warlord in the throne room (1 warlords, in Throne Room); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Entry Hall - Armory | doorway | (7.5, 0, 9.9) | 1.4 | #0 (open) | listed door |
| Entry Hall - Guard Room | edge | (18.37, 0.205, 7.538) | 14.73 | #14 (open) | listed door |
| Entry Hall - Guard Room | doorway | (15.033, 0.267, 7.567) | 0.469 | #14 (open) | listed door |
| Great Dining Hall - Library | doorway | (22.411, 0, 40.037) | 1.404 | #8 (open) | listed door |
| Guard Room - Prison | doorway | (17.633, 0, 10.167) | 0.707 | #15 (open) | listed door |
| Library - Shrine | doorway | (15, 0, 47.5) | 1.4 | #9 (open) | listed door |
| Servery Stair - gallery | doorway | (-2.268, 4.247, 24.678) | 0.862 | #6 (open) | listed door |
| Servery Stair - Great Dining Hall | doorway | (0, 0, 22.5) | 0.707 | #6 (open) | listed door |
| Shrine - Crypt | doorway | (-0.029, 0, 42.514) | 0.7 | #12 (open) | listed door |
| Shrine - Throne Room | doorway | (7.392, 0.024, 55.024) | 2.902 | #13 (open) | listed door |
| Storeroom - Forge | edge | (-11.039, 0, 7.371) | 2.147 | #4 (open) | listed door |
| Storeroom - Forge | doorway | (-10.367, 0, 7.733) | 1.712 | #4 (open) | listed door |
| Storeroom - Kitchen | doorway | (-7.513, 0, 10.001) | 0.707 | #3 (open) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **FAIL** | 0 / 0 |  | no complete NavMesh path for leg 1 (PathPartial) from (7.5, 0, 2.2) to (15, 0, 30); it ends at (15.9, 0, 19.1) in Prison |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **FAIL** | 0 / 0 |  | not walked: row 3 found no way in or no deck point reached |
| the dining hall into the armory beneath the gallery, to the best gear | **FAIL** | 0 / 0 |  | not walked: the armory has no opening to the dining hall (row 1) |
| into the vault by the hidden way, and back | **FAIL** | 0 / 0 |  | not walked: no floor point beside the hidden door or in the vault |
| the dining hall to the throne | **PASS** | 62.849 / 63.221 | Great Dining Hall > Library > Shrine > Throne Room | reached the end |

## Rooms (numbers on the map)

| # | name | kind | state | floor_y | outline m2 |
|---|---|---|---|---|---|
| 1 | Entry Hall | entry hall | decayed | -0.006 | 129.363 |
| 2 | Armory | armory | stocked | -0.003 | 129.344 |
| 3 | Storeroom | storeroom | looted | -0.006 | 83.485 |
| 4 | Kitchen | kitchen | decayed | -0.006 | 83.485 |
| 5 | Servery Stair | extra | decayed | -0.006 | 129.175 |
| 6 | Forge | forge | decayed | -0.03 | 83.466 |
| 7 | Guard Room | guard room | looted | -0.006 | 83.466 |
| 8 | Prison | prison | looted | -0.03 | 129.147 |
| 9 | Torture Chamber | torture chamber | decayed | -0.03 | 83.467 |
| 10 | Great Dining Hall | dining hall | looted | -0.006 | 557.547 |
| 11 | Library | library | looted | -0.006 | 200.053 |
| 12 | Alchemist's Study | alchemist's study | looted | -0.006 | 83.467 |
| 13 | Treasure Vault | treasure vault | untouched | 0.005 | 17.073 |
| 14 | Shrine | shrine | looted | -0.006 | 200.053 |
| 15 | Crypt | crypt | decayed | -0.03 | 82.699 |
| 16 | Throne Room | throne room | decayed | 0.005 | 703.227 |

## Map

`conformance-map.png`: north (+z) is up, 4 px per metre. The NavMesh is the all-doors-opened bake: green is floor-level NavMesh in a room, olive raised NavMesh (the gallery, the stage, furniture tops in a room's band), teal level NavMesh outside every outline (doorways in the walls' thickness), grey-blue NavMesh that is not level space. White lines are the map's room outlines, numbered as above; cyan the gallery deck and its rail, with the rail eyes as cyan dots; orange the stage outline and the throne. Dark red squares are hall-floor samples no rail eye sees. Doors: white dots are listed doors that are openings, yellow listed doors that are not, red openings no door lists. The blue dot is the start, red dots enemies, gold dots pickups; violet squares are tall occluders, light blue low ones. Pink lines are the walks, red crosses their snags; small red crosses are entry-hall samples that see an armory enemy from more than 5 m.

## Thresholds (Tune.cs)

- `AgentRadius` = 0.35
- `AgentHeight` = 1.8
- `AgentClimb` = 0.3
- `AgentSlope` = 45
- `VoxelSize` = 0.1
- `MinRegionArea` = 2
- `BakeMargin` = 20
- `BakeBottom` = -10
- `BakeTop` = 60
- `WeldXZ` = 0.05
- `WeldY` = 0.35
- `TJunctionMinOverlap` = 0.05
- `TriHeightSlack` = 1.5
- `LevelComponentMinFloorArea` = 25
- `RoomBandLow` = -1
- `RoomBandHigh` = 2.5
- `GalleryBand` = 1
- `FloorBandLow` = -1
- `FloorBandHigh` = 1
- `FloorProbeStep` = 1.5
- `GapJoin` = 1
- `PocketReportArea` = 10
- `DoorMatchSlack` = 2
- `StartSnapRadius` = 4
- `EyeHeight` = 1.6
- `InteractReach` = 3
- `DoorSideSearch` = 4
- `ShutDoorInteractRadius` = 1.5
- `VaultInteractReach` = 8
- `VaultDisableRadius` = 1.5
- `VaultFloorClearance` = 0.3
- `VaultDoorHeight` = 3.5
- `VaultSkipLargerThan` = 50
- `ReceiverMaxColliders` = 50
- `ReceiverMaxSize` = 12
- `DoorTargetLift` = 1
- `ChestHeight` = 1.3
- `ChokeSightBack` = 5
- `UnderGalleryReach` = 1.5
- `FlankRouteReach` = 8
- `ChokeFacingDot` = 0.5
- `ChokeGuardReach` = 8
- `GalleryMinRise` = 3
- `GalleryMinArea` = 20
- `HallFloorInset` = 3
- `HallFloorDeckClear` = 1.5
- `HallFloorBandHigh` = 1
- `RailLean` = 0.55
- `RailSampleStep` = 2
- `RailSearch` = 2
- `HallOpenAbove` = 1
- `FloorSampleStep` = 2
- `FloorTargetLift` = 0.2
- `MinVisibleFraction` = 0.75
- `StageMinRise` = 0.4
- `StageSampleStep` = 0.5
- `FarEndFraction` = 0.66
- `CeilingSampleStep` = 3
- `CeilingMaxRay` = 60
- `CeilingMinSamples` = 10
- `LowCeilingMax` = 6.5
- `LowMinFraction` = 0.75
- `TallCeilingMin` = 8
- `TallMinFraction` = 0.25
- `HeightTargetPercentile` = 0.9
- `TallestSlack` = 0.5
- `CeilingMinFraction` = 0.95
- `CoverMinWidth` = 0.6
- `CoverMaxWidth` = 8
- `CoverTall` = 1.8
- `CoverLowMin` = 0.8
- `CoverLowMax` = 1.3
- `CoverBaseTolerance` = 0.35
- `CoverClear` = 0.6
- `CoverMinOpenSides` = 5
- `WalkSpeed` = 3
- `WalkDt` = 0.05
- `WalkReach` = 0.3
- `WalkStuckSeconds` = 2
- `WalkStuckProgress` = 0.05
- `WalkFallDrop` = 2.5
- `WalkEndTolerance` = 1
- `WalkSkin` = 0.02
- `PathSampleStep` = 0.5
- `PickupSnap` = 4
- `SceneObjectNear` = 1
- `FloorYTolerance` = 0.3
- `HallFloorArea` = 28
- `ArmoryArea` = 29

Full measurements are in `conformance.json`.
