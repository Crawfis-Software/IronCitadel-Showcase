# Conformance v2: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Map: `C:/Repos/ColdRuns/opus-v3-hm/Map/map.json`
- Run: 2026-10-14T11:50:59Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.0
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 1611 triangles from 856 colliders and 72 modifier boxes, 9 crossings, 0.058 s
- Bake "shut doors opened": 1655 triangles from 820 colliders and 72 modifier boxes, 16 crossings, 0.041 s
- Bake "all doors opened": 1669 triangles from 815 colliders and 72 modifier boxes, 17 crossings, 0.039 s
- Start: map.json start, at (14, 0.054, 1.2) in Foyer; the scene's own start: object tagged Player: PlayerCapsule at (14, 0.02, 1.2)
- Enemy bodies switched off for sight lines and the bake: 29
- NavMesh obstacles switched off (their carving would cut the kit's bake): 0
- Other Interact() receivers opened for the all-doors bake: 0

**Result: 11 pass, 0 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **PASS** | 2 openings: the entry hall (seen from it) and the dining hall beneath the gallery | with every door opened the armory's only openings are to Foyer at (14.2, 0.0, 8.4) and to the dining hall at (17.1, 0.0, 19.8), 0.2 m from the deck outline and below deck_y |
| 1b | Act one: the armory's guards come into view only at its doorway. | **PASS** | guards seen only within 0.0 m of the doorway; the start sees the doorway, no guard | of 20 entry-hall floor samples, 0 see an armory enemy (eye 1.6 m to chest 1.3 m), all within 5 m of the doorway; the start sees the doorway and none of the 9 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **PASS** | the way round: Foyer > Watch Room > Cellar Cells > Butcher's Room > Kitchen > Back Stair > Dining Hall (72.7 m) | with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through Foyer, Watch Room, Cellar Cells, Butcher's Room, Kitchen, Back Stair, Dining Hall |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **PASS** | deck 4.2 m up, 56.7 m2; reached from the way in without the floor; information, not scored: the rail sees 65% of the hall floor | the deck stands 4.2 m above the hall floor with 56.7 m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; information, not scored: from 14 rail eyes (leaned 0.55 m, eye 1.6 m) 33 of 51 open floor samples are seen |
| 4 | The throne stands on a stage with steps. | **PASS** | throne on a 1.20 m stage, climbed from the floor | the throne lies inside the stage outline; the stage top (260 NavMesh samples) stands 1.20 m above the floor; a complete path climbs onto it from the floor; information, not scored: the throne lies 94% of the depth from Laboratory - Ballroom (open) |
| 5 | The vault has one way in, and the player may have to search for it. | **PASS** | sealed when closed; opened, one way in from Library | with the shut doors opened and the hidden door closed there is no path from Library into the vault (PathPartial); with 5 colliders of the hidden door switched off, the vault's only opening is to Library and a complete path enters it |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **PASS** | low (<= 6.5 m, want 75%): Cellar Cells 100%, Watch Room 100%, Kitchen 100%, Strong Room 100%; tall (>= 8 m, want 25%): Dining Hall 74%, Ballroom 100%; tallest p90: Ballroom 12.0, Dining Hall 9.0, Chapel 8.0 m | every low room has at least 75% of its floor under at most 6.5 m, every tall room at least 25% under at least 8 m, and no room's p90 ceiling exceeds the throne room's + 0.5 m |
| 7 | Everything is indoors, and every room has a ceiling. | **PASS** | every room roofed; the least is Foyer at 100% | in every room at least 95% of the floor samples have a collider (or a wide renderer) above them |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **PASS** | 17 listed doors, all openings, none unlisted; 7/7 shut doors shut and opened by E | with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 58 tall and 42 low occluders (per room tall/low: Foyer 2/1, Gun Room 9/1, Watch Room 0/1, Cellar Cells 15/0, Butcher's Room 1/1, Boiler Room 0/1, Kitchen 0/2, Back Stair 1/0, Dining Hall 8/17, Library 8/0, Laboratory 0/5, Ballroom 10/5, Chapel 4/8) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **PASS** | 5/5 walks reached their end | a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) followed each walk to its end, opening shut doors by E |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Dining Hall #9 0% (0.9 m from the rail); ranged in Dining Hall #10 0% (0.9 m from the rail); ranged in Dining Hall #11 0% (0.9 m from the rail); ranged in Dining Hall #12 0% (0.9 m from the rail); ranged in Dining Hall #13 0% (0.9 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | the map matches the scene | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | yes: ranged behind the armory entrance (4 ranged, 4 within 8 m, 4 facing it); yes: melee in the rest of the armory (5); yes: ranged on the gallery (5); yes: the dining hall holds the largest group (12; next 9); yes: the warlord in the throne room (1 warlords, in Ballroom); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Back Stair - Dining Hall | doorway | (1.85, 0.048, 21.85) | 0.9 | #8 (open) | listed door |
| Back Stair - gallery | doorway | (-0.338, 4.058, 22.692) | 0.4 | #9 (shut) | listed door |
| Butcher's Room - Kitchen | doorway | (-9, 0.048, 17.65) | 0.4 | #5 (shut) | listed door |
| Cellar Cells - Boiler Room | doorway | (2.45, 0.048, 9.2) | 0.5 | #4 (open) | listed door |
| Cellar Cells - Butcher's Room | doorway | (-11.5, 0.048, 9.2) | 0.424 | #3 (open) | listed door |
| Chapel - Crypt | doorway | (35, 0.048, 23.85) | 0.2 | #13 (open) | listed door |
| Dining Hall - Chapel | edge | (30.063, 0.048, 29.696) | 8.77 | #12 (shut) | listed door |
| Dining Hall - Library | doorway | (16.233, 0.048, 36.1) | 1.432 | #11 (open) | listed door |
| Foyer - Gun Room | doorway | (14.2, 0.048, 8.433) | 1.1 | #0 (open) | listed door |
| Foyer - Watch Room | doorway | (8.85, 0.048, 4.5) | 0.4 | #1 (shut) | listed door |
| Gun Room - Dining Hall | doorway | (17.1, 0.048, 19.8) | 0.632 | #10 (shut) | listed door |
| Kitchen - Back Stair | edge | (-3.65, 0.048, 25.849) | 3.429 | #7 (open) | listed door |
| Kitchen - Storeroom | doorway | (-8, 0.048, 27.25) | 0.4 | #6 (shut) | listed door |
| Laboratory - Ballroom | doorway | (16.147, 0.048, 54.099) | 1.2 | #16 (open) | listed door |
| Library - Laboratory | doorway | (15.9, 0.048, 46.367) | 0.6 | #15 (shut) | listed door |
| Library - Strong Room | doorway | (3.667, 0.048, 40.767) | 0.4 | #14 (hidden) | listed door |
| Watch Room - Cellar Cells | doorway | (4, 0.048, 3.5) | 0.4 | #2 (open) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **PASS** | 72.187 / 72.71 | Foyer > Watch Room > Cellar Cells > Butcher's Room > Kitchen > Back Stair > Dining Hall | reached the end; opened by E: Level/Doors/Door Foyer - Watch Room (Foyer), Level/Doors/Door Butcher's Room - Kitchen (Butcher's Room) |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **PASS** | 22.133 / 23.63 | Dining Hall > Back Stair > gallery | reached the end; opened by E: Level/Doors/Door Back Stair - Dining Hall (no room) |
| the dining hall into the armory beneath the gallery, to the best gear | **PASS** | 13.655 / 13.751 | Dining Hall > Gun Room | reached the end; opened by E: Level/Doors/Door Gun Room - Dining Hall (Dining Hall) |
| into the vault by the hidden way, and back | **PASS** | 7.271 / 7.619 | Library > Strong Room > Library | reached the end; opened by E: Level/Doors/Door Library - Strong Room (Library) |
| the dining hall to the throne | **PASS** | 47.764 / 48.028 | Dining Hall > Library > Laboratory > Ballroom | reached the end; opened by E: Level/Doors/Door Library - Laboratory (Library) |

## Rooms (numbers on the map)

| # | name | kind | state | floor_y | outline m2 |
|---|---|---|---|---|---|
| 1 | Foyer | entry hall | decayed | 0 | 80 |
| 2 | Gun Room | armory | stocked | 0 | 145.6 |
| 3 | Watch Room | guard room | decayed | 0 | 26.4 |
| 4 | Cellar Cells | prison | decayed | 0 | 184.8 |
| 5 | Butcher's Room | torture chamber | decayed | 0 | 64 |
| 6 | Boiler Room | forge | decayed | 0 | 89.6 |
| 7 | Kitchen | kitchen | looted | 0 | 88.32 |
| 8 | Storeroom | storeroom | looted | 0 | 63.36 |
| 9 | Back Stair | extra | decayed | 0 | 59.8 |
| 10 | Dining Hall | dining hall | decayed | 0 | 448 |
| 11 | Library | library | looted | 0 | 230.4 |
| 12 | Strong Room | treasure vault | untouched | 0 | 16.8 |
| 13 | Laboratory | alchemist's study | decayed | 0 | 121.6 |
| 14 | Ballroom | throne room | decayed | 0 | 604.8 |
| 15 | Chapel | shrine | decayed | 0 | 115.2 |
| 16 | Crypt | crypt | decayed | 0 | 111.36 |

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
