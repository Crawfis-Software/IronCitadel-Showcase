# Conformance v2: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Map: `C:/Repos/ColdRuns/opus-v3/Map/map.json`
- Run: 2026-10-14T16:27:09Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.1
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 1877 triangles from 961 colliders and 74 modifier boxes, 13 crossings, 0.084 s
- Bake "shut doors opened": 1902 triangles from 956 colliders and 74 modifier boxes, 19 crossings, 0.061 s
- Bake "all doors opened": 1908 triangles from 955 colliders and 74 modifier boxes, 20 crossings, 0.06 s
- Start: map.json start, at (7.5, -0.006, 2.2) in Entry Hall; the scene's own start: object tagged Player: PlayerCapsule at (7.5, 0.02, 2.2)
- Enemy bodies switched off for sight lines and the bake: 0
- NavMesh obstacles switched off (their carving would cut the kit's bake): 6
- Other Interact() receivers opened for the all-doors bake: 0

**Result: 11 pass, 0 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **PASS** | 2 openings: the entry hall (seen from it) and the dining hall beneath the gallery | with every door opened the armory's only openings are to Entry Hall at (7.5, 0.0, 9.9) and to the dining hall at (7.5, 0.0, 20.0), 0.4 m from the deck outline and below deck_y |
| 1b | Act one: the armory's guards come into view only at its doorway. | **PASS** | guards seen only within 0.0 m of the doorway; the start sees the doorway, no guard | of 35 entry-hall floor samples, 0 see an armory enemy (eye 1.6 m to chest 1.3 m), all within 5 m of the doorway; the start sees the doorway and none of the 9 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **PASS** | the way round: Entry Hall > Storeroom > Kitchen > Servery Stair > Great Dining Hall (54.5 m) | with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through Entry Hall, Storeroom, Kitchen, Servery Stair, Great Dining Hall |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **PASS** | deck 5.0 m up, 51.0 m2; reached from the way in without the floor; information, not scored: the rail sees 100% of the hall floor | the deck stands 5.0 m above the hall floor with 51.0 m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; information, not scored: from 15 rail eyes (leaned 0.55 m, eye 1.6 m) 65 of 65 open floor samples are seen |
| 4 | The throne stands on a stage with steps. | **PASS** | throne on a 1.50 m stage, climbed from the floor | the throne lies inside the stage outline; the stage top (180 NavMesh samples) stands 1.50 m above the floor; a complete path climbs onto it from the floor; information, not scored: the throne lies 95% of the depth from Shrine - Throne Room (open) |
| 5 | The vault has one way in, and the player may have to search for it. | **PASS** | sealed when closed; opened, one way in from Library | with the shut doors opened and the hidden door closed there is no path from Library into the vault (PathPartial); with 1 colliders of the hidden door switched off, the vault's only opening is to Library and a complete path enters it |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **PASS** | low (<= 6.5 m, want 75%): Prison 100%, Guard Room 100%, Kitchen 100%, Treasure Vault 100%; tall (>= 8 m, want 25%): Great Dining Hall 81%, Throne Room 100%; tallest p90: Throne Room 15.1, Shrine 10.2, Servery Stair 10.2 m | every low room has at least 75% of its floor under at most 6.5 m, every tall room at least 25% under at least 8 m, and no room's p90 ceiling exceeds the throne room's + 0.5 m |
| 7 | Everything is indoors, and every room has a ceiling. | **PASS** | every room roofed; the least is Entry Hall at 100% | in every room at least 95% of the floor samples have a collider (or a wide renderer) above them |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **PASS** | 17 listed doors, all openings, none unlisted; 5/5 shut doors shut and opened by E | with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 47 tall and 39 low occluders (per room tall/low: Entry Hall 0/4, Armory 1/0, Storeroom 0/3, Kitchen 1/2, Servery Stair 2/2, Forge 0/5, Guard Room 1/2, Prison 24/0, Torture Chamber 3/1, Great Dining Hall 5/9, Library 6/4, Alchemist's Study 1/3, Shrine 3/1, Crypt 0/2, Throne Room 0/1) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **PASS** | 5/5 walks reached their end | a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) followed each walk to its end, opening shut doors by E |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Great Dining Hall #9 75% (0.6 m from the rail); ranged in Great Dining Hall #10 77% (0.6 m from the rail); ranged in Great Dining Hall #11 74% (0.6 m from the rail); ranged in Great Dining Hall #12 74% (0.6 m from the rail); ranged in Great Dining Hall #13 71% (0.6 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | the map matches the scene | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | yes: ranged behind the armory entrance (4 ranged, 4 within 8 m, 4 facing it); yes: melee in the rest of the armory (5); yes: ranged on the gallery (5); yes: the dining hall holds the largest group (11; next 9); yes: the warlord in the throne room (1 warlords, in Throne Room); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Armory - Great Dining Hall | doorway | (7.504, 0, 20.048) | 0.707 | #1 (shut) | listed door |
| Entry Hall - Armory | doorway | (7.5, 0, 9.9) | 1.4 | #0 (open) | listed door |
| Entry Hall - Guard Room | edge | (18.37, 0.205, 7.538) | 14.73 | #14 (open) | listed door |
| Entry Hall - Guard Room | doorway | (15.033, 0.267, 7.567) | 0.469 | #14 (open) | listed door |
| Entry Hall - Storeroom | doorway | (-0.122, 0, 2.544) | 0.707 | #2 (shut) | listed door |
| Great Dining Hall - Library | doorway | (22.411, 0, 40.037) | 1.404 | #8 (open) | listed door |
| Guard Room - Prison | doorway | (17.633, 0, 10.167) | 0.707 | #15 (open) | listed door |
| Kitchen - Servery Stair | doorway | (-7.456, 0, 19.856) | 0.707 | #5 (shut) | listed door |
| Library - Alchemist's Study | edge | (29.6, 0, 42.25) | 0.806 | #10 (shut) | listed door |
| Library - Alchemist's Study | doorway | (29.933, 0, 42.633) | 0.943 | #10 (shut) | listed door |
| Library - Shrine | doorway | (15, 0, 47.5) | 1.4 | #9 (open) | listed door |
| Library - Treasure Vault | doorway | (29.878, 0.033, 52.544) | 0.707 | #11 (hidden) | listed door |
| Prison - Torture Chamber | doorway | (30, 0, 12.5) | 4.243 | #16 (shut) | listed door |
| Servery Stair - gallery | doorway | (-2.268, 4.247, 24.678) | 0.862 | #7 (open) | listed door |
| Servery Stair - Great Dining Hall | doorway | (0, 0, 22.5) | 0.707 | #6 (open) | listed door |
| Shrine - Crypt | doorway | (-0.029, 0, 42.514) | 0.7 | #12 (open) | listed door |
| Shrine - Throne Room | doorway | (7.392, 0.024, 55.024) | 2.902 | #13 (open) | listed door |
| Storeroom - Forge | edge | (-11.039, 0, 7.371) | 2.147 | #4 (open) | listed door |
| Storeroom - Forge | doorway | (-10.367, 0, 7.733) | 1.712 | #4 (open) | listed door |
| Storeroom - Kitchen | doorway | (-7.513, 0, 10.001) | 0.707 | #3 (open) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **PASS** | 54.046 / 54.483 | Entry Hall > Storeroom > Kitchen > Servery Stair > Great Dining Hall | reached the end; opened by E: IronCitadel/Doors/Door Entry Hall - Storeroom/Hinge (Entry Hall), IronCitadel/Doors/Door Kitchen - Servery Stair/Hinge (Kitchen) |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **PASS** | 28.105 / 29.924 | Great Dining Hall > Servery Stair > gallery | reached the end |
| the dining hall into the armory beneath the gallery, to the best gear | **PASS** | 18.439 / 18.395 | Great Dining Hall > Armory | reached the end; opened by E: IronCitadel/Doors/Door Armory - Great Dining Hall/Hinge (Great Dining Hall) |
| into the vault by the hidden way, and back | **PASS** | 7.984 / 7.818 | Library > Treasure Vault > Library | reached the end; opened by E: IronCitadel/Rooms/Library/Props/Secret Bookcase/Bookcase (secret) (Library) |
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
- `GalleryRoomOverlap` = 0.5
- `FloorBandLow` = -1
- `FloorBandHigh` = 1
- `FloorProbeStep` = 1.5
- `GapJoin` = 1
- `PocketReportArea` = 10
- `DoorMatchSlack` = 2
- `DoorMatchYBand` = 1.5
- `StartSnapRadius` = 4
- `EyeHeight` = 1.6
- `InteractReach` = 3
- `DoorSideSearch` = 4
- `EAimSpread` = 0.25
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
- `GalleryRiseSlack` = 0.05
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
- `HeightSlack` = 0.05
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
