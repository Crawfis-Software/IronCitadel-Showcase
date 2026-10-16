# Conformance v2: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Map: `C:/Repos/ColdRuns/fable-v3/Map/map.json`
- Run: 2026-10-14T16:27:21Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.1
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 1986 triangles from 1000 colliders and 51 modifier boxes, 10 crossings, 0.153 s
- Bake "shut doors opened": 2033 triangles from 993 colliders and 51 modifier boxes, 17 crossings, 0.134 s
- Bake "all doors opened": 2041 triangles from 990 colliders and 51 modifier boxes, 18 crossings, 0.145 s
- Start: map.json start, at (-7.5, 0.095, -8) in Entry hall; the scene's own start: object tagged Player: PlayerCapsule at (-7.5, 0.15, -8)
- Enemy bodies switched off for sight lines and the bake: 31
- NavMesh obstacles switched off (their carving would cut the kit's bake): 0
- Other Interact() receivers opened for the all-doors bake: 1 (Level/Doors/Door Guard room - Prison/Leaf SM_Env_Door_Bars_01 at (-21.8, 0.1, 0.0))
- Map rooms that are the gallery deck, counted as the gallery (the dining hall's): Gallery

**Result: 10 pass, 1 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **PASS** | 2 openings: the entry hall (seen from it) and the dining hall beneath the gallery | with every door opened the armory's only openings are to Entry hall at (-7.5, 0.1, 0.0) and to the dining hall at (0.0, 0.1, 7.5), 0.0 m from the deck outline and below deck_y |
| 1b | Act one: the armory's guards come into view only at its doorway. | **FAIL** | 20 entry-hall samples more than 5 m from the doorway see an armory enemy (farthest 10.5 m); the start sees 1 armory enemies | 20 entry-hall samples more than 5 m from the doorway see an armory enemy (farthest 10.5 m); the start sees 1 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **PASS** | the way round: Entry hall > Guard room > Prison > Torture chamber > Crypt > Stair hall > Dining hall (82.7 m) | with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through Entry hall, Guard room, Prison, Torture chamber, Crypt, Stair hall, Dining hall |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **PASS** | deck 5.0 m up, 118.8 m2; reached from the way in without the floor; information, not scored: the rail sees 78% of the hall floor | the deck stands 5.0 m above the hall floor with 118.8 m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; information, not scored: from 17 rail eyes (leaned 0.55 m, eye 1.6 m) 70 of 90 open floor samples are seen |
| 4 | The throne stands on a stage with steps. | **PASS** | throne on a 1.01 m stage, climbed from the floor | the throne lies inside the stage outline; the stage top (191 NavMesh samples) stands 1.01 m above the floor; a complete path climbs onto it from the floor; information, not scored: the throne lies 97% of the depth from Antechamber - Throne room (open) |
| 5 | The vault has one way in, and the player may have to search for it. | **PASS** | sealed when closed; opened, one way in from Crypt | with the shut doors opened and the hidden door closed there is no path from Crypt into the vault (PathPartial); with 2 colliders of the hidden door switched off, the vault's only opening is to Crypt and a complete path enters it |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **PASS** | low (<= 6.5 m, want 75%): Prison 100%, Guard room 100%, Kitchen 100%, Treasure vault 100%; tall (>= 8 m, want 25%): Dining hall 61%, Throne room 100%; tallest p90: Throne room 15.1, Dining hall 10.1, Stair hall 10.1 m | every low room has at least 75% of its floor under at most 6.5 m, every tall room at least 25% under at least 8 m, and no room's p90 ceiling exceeds the throne room's + 0.5 m |
| 7 | Everything is indoors, and every room has a ceiling. | **PASS** | every room roofed; the least is Alchemist's study at 100% | in every room at least 95% of the floor samples have a collider (or a wide renderer) above them |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **PASS** | 18 listed doors, all openings, none unlisted; 7/7 shut doors shut and opened by E | with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 54 tall and 43 low occluders (per room tall/low: Alchemist's study 0/2, Antechamber 1/0, Armory 2/0, Crypt 2/0, Dining hall 11/17, Entry hall 1/5, Forge 0/8, Guard room 0/1, Kitchen 1/2, Library 4/3, Prison 14/1, Shrine 2/1, Stair hall 6/0, Storeroom 0/2, Throne room 8/0, Torture chamber 2/1) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **PASS** | 5/5 walks reached their end | a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) followed each walk to its end, opening shut doors by E |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Dining hall #27 0% (2.4 m from the rail); ranged in Dining hall #28 0% (2.4 m from the rail); ranged in Dining hall #29 0% (2.4 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | the map matches the scene | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | yes: ranged behind the armory entrance (3 ranged, 3 within 8 m, 3 facing it); yes: melee in the rest of the armory (4); yes: ranged on the gallery (3); yes: the dining hall holds the largest group (10; next 7); yes: the warlord in the throne room (1 warlords, in Throne room); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Alchemist's study - Library | edge | (35, 0.14, 17.5) | 0.632 | #12 (shut) | listed door |
| Alchemist's study - Shrine | edge | (37.5, 0.14, 20) | 0.721 | #0 (open) | listed door |
| Antechamber - Shrine | edge | (35, 0.14, 27.5) | 0.632 | #14 (shut) | listed door |
| Antechamber - Throne room | edge | (30.05, 0.14, 29.9) | 2.326 | #1 (open) | listed door |
| Armory - Dining hall | edge | (0, 0.14, 7.5) | 0.6 | #2 (shut) | listed door |
| Armory - Entry hall | edge | (-7.45, 0.14, 0) | 2.1 | #7 (open) | listed door |
| Crypt - Stair hall | edge | (-15, 0.14, 27.5) | 0.632 | #3 (open) | listed door |
| Crypt - Torture chamber | edge | (-22.5, 0.14, 25) | 0.721 | #17 (open) | listed door |
| Crypt - Treasure vault | edge | (-30, 0.14, 27.5) | 0.632 | #4 (hidden) | listed door |
| Dining hall - Kitchen | edge | (20, 0.14, 7.5) | 0.632 | #5 (shut) | listed door |
| Dining hall - Library | edge | (20, 0.14, 17.5) | 1.612 | #6 (open) | listed door |
| Dining hall - Stair hall | edge | (0, 0.14, 27.5) | 1.4 | #15 (open) | listed door |
| Entry hall - Guard room | edge | (-12.877, 0.14, -7.604) | 9.528 | #8 (shut) | listed door |
| Forge - Stair hall | edge | (-7.5, 0.14, 25) | 0.632 | #9 (shut) | listed door |
| Guard room - Prison | edge | (-22.5, 0.14, 0) | 0.6 | #10 (open) | listed door |
| Kitchen - Storeroom | edge | (30, 0.14, 2.5) | 0.632 | #11 (open) | listed door |
| Prison - Torture chamber | edge | (-17.45, 0.14, 15.15) | 0.86 | #13 (shut) | listed door |
| Stair hall - gallery | doorway | (-4.879, 3.495, 31.439) | 0.6 | #16 (open) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **PASS** | 82.311 / 82.731 | Entry hall > Guard room > Prison > Torture chamber > Crypt > Stair hall > Dining hall | reached the end; opened by E: Level/Doors/Door Entry hall - Guard room/Leaf SM_Env_Door_03 (Entry hall), Level/Doors/Door Prison - Torture chamber/Leaf SM_Env_Door_04 (Prison) |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **PASS** | 35.228 / 36.624 | Dining hall > Stair hall > gallery | reached the end |
| the dining hall into the armory beneath the gallery, to the best gear | **PASS** | 22.283 / 22.263 | Dining hall > Armory | reached the end; opened by E: Level/Doors/Door Armory - Dining hall/Leaf SM_Env_Door_04 (Dining hall) |
| into the vault by the hidden way, and back | **PASS** | 7.394 / 8 | Crypt > Treasure vault > Crypt | reached the end; opened by E: Level/Doors/Door Crypt - Treasure vault/SecretPlug (Crypt) |
| the dining hall to the throne | **PASS** | 64.234 / 64.56 | Dining hall > Library > Alchemist's study > Shrine > Antechamber > Throne room | reached the end; opened by E: Level/Doors/Door Library - Alchemist's study/Leaf SM_Env_Door_03 (Library), Level/Doors/Door Shrine - Antechamber/Leaf SM_Env_Door_05 (Shrine) |

## Rooms (numbers on the map)

| # | name | kind | state | floor_y | outline m2 |
|---|---|---|---|---|---|
| 1 | Alchemist's study | alchemist's study | looted | 0.09 | 100 |
| 2 | Antechamber | extra | decayed | 0.09 | 50 |
| 3 | Armory | armory | stocked | 0.09 | 225 |
| 4 | Crypt | crypt | decayed | 0.09 | 150 |
| 5 | Dining hall | dining hall | decayed | 0.09 | 700 |
| 6 | Entry hall | entry hall | decayed | 0.09 | 150 |
| 7 | Forge | forge | decayed | 0.09 | 150 |
| 8 | Guard room | guard room | looted | 0.09 | 100 |
| 9 | Kitchen | kitchen | looted | 0.09 | 100 |
| 10 | Library | library | decayed | 0.09 | 225 |
| 11 | Prison | prison | looted | 0.09 | 150 |
| 12 | Shrine | shrine | decayed | 0.1 | 100 |
| 13 | Stair hall | extra | decayed | 0.09 | 150 |
| 14 | Storeroom | storeroom | looted | 0.09 | 100 |
| 15 | Throne room | throne room | decayed | 0.09 | 500 |
| 16 | Torture chamber | torture chamber | decayed | 0.09 | 100 |
| 17 | Treasure vault | treasure vault | untouched | 0.1 | 25 |

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
