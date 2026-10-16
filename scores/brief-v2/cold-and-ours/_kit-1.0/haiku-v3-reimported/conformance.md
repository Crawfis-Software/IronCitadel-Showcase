# Conformance v2: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Map: `C:/Repos/ColdRuns/haiku-v3/Map/map.json`
- Run: 2026-10-14T16:17:11Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.0
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 1284 triangles from 800 colliders and 78 modifier boxes, 5 crossings, 0.055 s
- Bake "shut doors opened": 1391 triangles from 780 colliders and 78 modifier boxes, 16 crossings, 0.04 s
- Bake "all doors opened": 1402 triangles from 778 colliders and 78 modifier boxes, 17 crossings, 0.037 s
- Start: map.json start, at (4, 0.005, 7.5) in Entry hall; the scene's own start: object tagged Player: PlayerCapsule at (4, 0, 7.5)
- Enemy bodies switched off for sight lines and the bake: 0
- NavMesh obstacles switched off (their carving would cut the kit's bake): 0
- Other Interact() receivers opened for the all-doors bake: 0

**Result: 8 pass, 3 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **PASS** | 2 openings: the entry hall (seen from it) and the dining hall beneath the gallery | with every door opened the armory's only openings are to Entry hall at (15.0, 0.1, 7.5) and to the dining hall at (30.0, 0.1, 12.5), 0.0 m from the deck outline and below deck_y |
| 1b | Act one: the armory's guards come into view only at its doorway. | **FAIL** | 25 entry-hall samples more than 5 m from the doorway see an armory enemy (farthest 15.4 m); the start sees 1 armory enemies | 25 entry-hall samples more than 5 m from the doorway see an armory enemy (farthest 15.4 m); the start sees 1 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **PASS** | the way round: Entry hall > Guard room > Prison > Torture chamber > Crypt > East passage > Dining hall (89.6 m) | with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through Entry hall, Guard room, Prison, Torture chamber, Crypt, East passage, Dining hall |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **PASS** | deck 4.6 m up, 262.8 m2; reached from the way in without the floor; information, not scored: the rail sees 85% of the hall floor | the deck stands 4.6 m above the hall floor with 262.8 m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; information, not scored: from 10 rail eyes (leaned 0.55 m, eye 1.6 m) 34 of 40 open floor samples are seen |
| 4 | The throne stands on a stage with steps. | **FAIL** | no NavMesh on the stage top | no NavMesh on the stage top; information, not scored: the throne lies 89% of the depth from Shrine - Throne room (shut) |
| 5 | The vault has one way in, and the player may have to search for it. | **PASS** | sealed when closed; opened, one way in from Forge | with the shut doors opened and the hidden door closed there is no path from Forge into the vault (PathPartial); with 2 colliders of the hidden door switched off, the vault's only opening is to Forge and a complete path enters it |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **FAIL** | low (<= 6.5 m, want 75%): Prison 100%, Guard room 100%, Kitchen 100%, Treasure vault 100%; tall (>= 8 m, want 25%): Dining hall 0%, Throne room 100%; tallest p90: Throne room 9.3, Dining hall 8.0, Library 7.0 m | Dining hall is not tall: 0% of its floor under a ceiling of at least 8 m (want 25%) |
| 7 | Everything is indoors, and every room has a ceiling. | **PASS** | every room roofed; the least is Entry hall at 100% | in every room at least 95% of the floor samples have a collider (or a wide renderer) above them |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **PASS** | 16 listed doors, all openings, none unlisted; 10/10 shut doors shut and opened by E | with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 11 tall and 46 low occluders (per room tall/low: Entry hall 0/4, Armory 0/2, Dining hall 0/5, Guard room 0/3, Prison 4/3, Torture chamber 3/0, Crypt 0/2, East passage 0/2, Kitchen 0/6, Storeroom 0/5, Forge 0/4, Treasure vault 0/3, Library 0/2, Shrine 3/1, Alchemist's study 0/4, Throne room 1/0) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **PASS** | 5/5 walks reached their end | a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) followed each walk to its end, opening shut doors by E |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Dining hall #21 0% (12.0 m from the rail); ranged in Dining hall #22 0% (12.0 m from the rail); ranged in Dining hall #23 3% (12.0 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | 1 rooms' floor_y off the measured floor by more than 0.3 m | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | NO: ranged behind the armory entrance (3 ranged, 3 within 8 m, 0 facing it); yes: melee in the rest of the armory (4); yes: ranged on the gallery (3); yes: the dining hall holds the largest group (10; next 7); yes: the warlord in the throne room (1 warlords, in Throne room); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Armory - Dining hall | edge | (30, 0.05, 12.5) | 0.6 | #12 (open) | listed door |
| Crypt - East passage | edge | (60, 0.05, 22.5) | 0.632 | #0 (open) | listed door |
| Dining hall - East passage | edge | (60, 0.1, 7.5) | 0.64 | #15 (shut) | listed door |
| Dining hall - gallery | doorway | (53.67, 3.128, 12.702) | 4.5 |  | inside Dining hall |
| Dining hall - Library | edge | (52.5, 0.05, 0) | 0.6 | #14 (shut) | listed door |
| Entry hall - Armory | edge | (15, 0.05, 7.5) | 0.6 | #11 (open) | listed door |
| Entry hall - Guard room | edge | (7.5, 0.05, 20) | 0.632 | #8 (shut) | listed door |
| Entry hall - Kitchen | edge | (7.5, 0.05, 0) | 0.6 | #9 (shut) | listed door |
| Forge - Treasure vault | edge | (15, 0.05, -22.5) | 0.632 | #10 (hidden) | listed door |
| Guard room - Prison | edge | (15, 0.05, 22.5) | 0.632 | #1 (shut) | listed door |
| Kitchen - Storeroom | edge | (15, 0.05, -7.5) | 0.632 | #7 (open) | listed door |
| Library - Alchemist's study | edge | (52.55, 0.05, -14.85) | 0.86 | #6 (shut) | listed door |
| Library - Shrine | edge | (45, 0.05, -7.5) | 0.632 | #13 (open) | listed door |
| Prison - Torture chamber | edge | (30, 0.05, 22.5) | 0.632 | #2 (shut) | listed door |
| Shrine - Throne room | edge | (37.55, 0.05, -14.85) | 0.86 | #5 (shut) | listed door |
| Storeroom - Forge | edge | (22.55, 0.05, -14.85) | 0.86 | #4 (shut) | listed door |
| Torture chamber - Crypt | edge | (45, 0.05, 22.5) | 0.632 | #3 (shut) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **PASS** | 89.763 / 89.563 | Entry hall > Guard room > Prison > Torture chamber > Crypt > East passage > Dining hall | reached the end; opened by E: Level/Entry hall/Door: Entry hall - Guard room (Entry hall), Level/Guard room/Door: Guard room - Prison (Guard room), Level/Prison/Door: Prison - Torture chamber (Prison), Level/Torture chamber/Door: Torture chamber - Crypt (Torture chamber), Level/Dining hall/Door: East passage - Dining hall (East passage) |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **PASS** | 14.401 / 15.77 | Dining hall > gallery | reached the end |
| the dining hall into the armory beneath the gallery, to the best gear | **PASS** | 26.827 / 27.107 | Dining hall > Armory | reached the end |
| into the vault by the hidden way, and back | **PASS** | 17.887 / 18.126 | Forge > Treasure vault > Forge | reached the end; opened by E: Level/Forge/Door: Forge - Treasure vault (Forge) |
| the dining hall to the throne | **PASS** | 42.953 / 43.004 | Dining hall > Library > Shrine > Throne room | reached the end; opened by E: Level/Dining hall/Door: Dining hall - Library (Dining hall), Level/Shrine/Door: Shrine - Throne room (Shrine) |

## Rooms (numbers on the map)

| # | name | kind | state | floor_y | outline m2 |
|---|---|---|---|---|---|
| 1 | Entry hall | entry hall | decayed | 0 | 300 |
| 2 | Armory | armory | stocked | 0 | 300 |
| 3 | Dining hall | dining hall | decayed | 0 | 600 |
| 4 | Guard room | guard room | looted | 0 | 150 |
| 5 | Prison | prison | looted | 0 | 150 |
| 6 | Torture chamber | torture chamber | decayed | 0 | 150 |
| 7 | Crypt | crypt | decayed | 0 | 150 |
| 8 | East passage | extra | decayed | 0 | 125 |
| 9 | Kitchen | kitchen | looted | 0 | 225 |
| 10 | Storeroom | storeroom | looted | 0 | 225 |
| 11 | Forge | forge | decayed | 0 | 225 |
| 12 | Treasure vault | treasure vault | untouched | 0 | 225 |
| 13 | Library | library | decayed | 0 | 225 |
| 14 | Shrine | shrine | decayed | 0 | 225 |
| 15 | Alchemist's study | alchemist's study | looted | 0 | 225 |
| 16 | Throne room | throne room | decayed | 0 | 225 |

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
