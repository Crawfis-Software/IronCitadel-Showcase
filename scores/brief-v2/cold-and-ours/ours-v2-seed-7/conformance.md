# Conformance v2: IronCitadelV2_seed-7

- Scene: `Assets/CrawfisSoftware/AllSyntyDungeons/Levels/IronCitadelV2_seed-7.unity`
- Map: `C:/Repos/Github/Crawfis-Software/IronCitadel/v2/seeds/seed-7/map.json`
- Run: 2026-10-15T00:43:43Z, Unity 6000.6.0f1, IronCitadel.ConformanceV2 1.1
- Agent (bake and walk): r 0.35, h 1.8, step 0.3, slope 45, skin 0.02
- Bake "as built": 9193 triangles from 6228 colliders and 209 modifier boxes, 8 crossings, 0.316 s
- Bake "shut doors opened": 9217 triangles from 6220 colliders and 209 modifier boxes, 16 crossings, 0.3 s
- Bake "all doors opened": 9221 triangles from 6218 colliders and 209 modifier boxes, 17 crossings, 0.297 s
- Start: map.json start, at (85.5, 0.095, -33.5) in Entry Hall; the scene's own start: object named PlayerStart at (85.5, 0.1, -33.5)
- Enemy bodies switched off for sight lines and the bake: 32
- NavMesh obstacles switched off (their carving would cut the kit's bake): 0
- Other Interact() receivers opened for the all-doors bake: 0

**Result: 10 pass, 1 fail, 0 manual.**

| # | must be true | result | measured | reason |
|---|---|---|---|---|
| 1 | The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in from the dining hall, beneath the gallery. | **FAIL** | the armory's opening to the dining hall is not beneath the gallery (want the crossing, or its route within 8 m of it, under the deck outline or within 1.5 m of it, below deck_y) | the armory's opening to the dining hall is not beneath the gallery (want the crossing, or its route within 8 m of it, under the deck outline or within 1.5 m of it, below deck_y) |
| 1b | Act one: the armory's guards come into view only at its doorway. | **PASS** | guards seen only within 4.3 m of the doorway; the start sees the doorway, no guard | of 70 entry-hall floor samples, 4 see an armory enemy (eye 1.6 m to chest 1.3 m), all within 5 m of the doorway; the start sees the doorway and none of the 7 armory enemies |
| 2 | A way leads from the entry hall to the dining hall without passing through the armory. | **PASS** | the way round: Entry Hall > Forge > Storeroom > Kitchen > Dining Hall (266.6 m) | with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through Entry Hall, Forge, Storeroom, Kitchen, Dining Hall |
| 3 | A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from its rail. | **PASS** | deck 5.0 m up, 207.9 m2; reached from the way in without the floor; information, not scored: the rail sees 67% of the hall floor | the deck stands 5.0 m above the hall floor with 207.9 m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; information, not scored: from 13 rail eyes (leaned 0.55 m, eye 1.6 m) 46 of 69 open floor samples are seen |
| 4 | The throne stands on a stage with steps. | **PASS** | throne on a 1.27 m stage, climbed from the floor | the throne lies inside the stage outline; the stage top (476 NavMesh samples) stands 1.27 m above the floor; a complete path climbs onto it from the floor; information, not scored: the throne lies 96% of the depth from Shrine - Throne Room (shut) |
| 5 | The vault has one way in, and the player may have to search for it. | **PASS** | sealed when closed; opened, one way in from Storeroom | with the shut doors opened and the hidden door closed there is no path from Storeroom into the vault (PathPartial); with 2 colliders of the hidden door switched off, the vault's only opening is to Storeroom and a complete path enters it |
| 6 | The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall, and no room is taller than the throne room. | **PASS** | low (<= 6.5 m, want 75%): Prison 100%, Guard Room 100%, Kitchen 100%, Treasure Vault 100%; tall (>= 8 m, want 25%): Dining Hall 34%, Throne Room 88%; tallest p90: Throne Room 15.0, Shrine 15.0, Library 14.9 m | every low room has at least 75% of its floor under at most 6.5 m, every tall room at least 25% under at least 8 m, and no room's p90 ceiling exceeds the throne room's + 0.5 m |
| 7 | Everything is indoors, and every room has a ceiling. | **PASS** | every room roofed; the least is Entry Hall at 100% | in every room at least 95% of the floor samples have a collider (or a wide renderer) above them |
| 8 | Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut door on a way through. | **PASS** | 15 listed doors, all openings, none unlisted; 8/8 shut doors shut and opened by E | with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side |
| 9 | The level has tall and low occluders the player can hide behind, standing or crouching. | **PASS** | 40 tall and 65 low occluders (per room tall/low: Entry Hall 0/1, Armory 0/1, Dining Hall 10/9, Library 8/18, Alchemist's Study 0/1, Torture Chamber 7/2, Forge 2/13, Storeroom 0/3, Kitchen 0/6, Shrine 4/2, Throne Room 1/3, Prison 7/6, Treasure Vault 1/0) | the level has both tall (stand behind) and low (crouch behind) occluders away from walls |
| 10 | WALK: a CharacterController walks five ways through the level (not a brief line). | **PASS** | 5/5 walks reached their end | a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) followed each walk to its end, opening shut doors by E |

## Information (not scored)

| # | what | measured | how |
|---|---|---|---|
| I1 | Information: what each gallery ranged enemy sees of the hall floor (eye 1.6 m). | ranged in Dining Hall #7 0% (4.5 m from the rail); ranged in Dining Hall #8 0% (4.5 m from the rail); ranged in Dining Hall #9 26% (0.5 m from the rail); ranged in Dining Hall #10 0% (8.5 m from the rail) | share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned |
| I2 | Information: the map's honesty against the scene. | 1 areas of NavMesh outside every outline (from 10 m2) | markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor |
| I3 | Information: defenders and pickups against the brief's paragraph. | yes: ranged behind the armory entrance (3 ranged, 3 within 8 m, 3 facing it); yes: melee in the rest of the armory (4); yes: ranged on the gallery (4); yes: the dining hall holds the largest group (10; next 7); yes: the warlord in the throne room (1 warlords, in Throne Room); yes: no one in the entry hall (0); yes: the best gear in the armory (1); yes: the hoard in the vault (1) | the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph |

## Room graph (every door opened)

| between | via | centre | NavMesh width (m) | listed door | role |
|---|---|---|---|---|---|
| Armory - Dining Hall | edge | (135, 0.1, -22.5) | 0.6 | #1 (shut) | listed door |
| Dining Hall - gallery | doorway | (146.433, 3.65, -37.033) | 5.944 |  | inside Dining Hall |
| Dining Hall - gallery | doorway | (146.6, 3.567, -6.867) | 5.944 |  | inside Dining Hall |
| Dining Hall - Kitchen | edge | (157.55, 0.075, -44.85) | 0.862 | #5 (open) | listed door |
| Dining Hall - Library | edge | (179.95, 0.075, -22.5) | 0.673 | #6 (shut) | listed door |
| Entry Hall - Armory | edge | (90, 0.1, -22.5) | 0.6 | #0 (open) | listed door |
| Entry Hall - Forge | edge | (67.55, 0.075, -44.9) | 0.923 | #2 (shut) | listed door |
| Forge - Guard Room | edge | (67.5, 0.05, -90) | 1 | #10 (shut) | listed door |
| Forge - Storeroom | edge | (89.95, 0.05, -67.5) | 0.671 | #3 (open) | listed door |
| Library - Alchemist's Study | edge | (224.9, 0.075, -22.45) | 0.923 | #12 (shut) | listed door |
| Library - Shrine | edge | (202.55, 0.075, -44.9) | 0.923 | #7 (open) | listed door |
| Prison - Guard Room | edge | (45, 0.05, -112.5) | 0.721 | #11 (open) | listed door |
| Shrine - Crypt | edge | (202.55, 0.05, -89.85) | 0.86 | #14 (shut) | listed door |
| Shrine - Throne Room | edge | (224.95, 0.05, -67.5) | 0.671 | #8 (shut) | listed door |
| Storeroom - Kitchen | edge | (134.9, 0.075, -67.45) | 0.923 | #4 (open) | listed door |
| Storeroom - Treasure Vault | edge | (112.55, 0.075, -89.9) | 0.923 | #9 (hidden) | listed door |
| Torture Chamber - Prison | edge | (22.5, 0.05, -90) | 0.721 | #13 (shut) | listed door |

## Walks

| route | result | walked / planned (m) | rooms planned | note |
|---|---|---|---|---|
| start to the dining hall, avoiding the armory | **PASS** | 264.572 / 266.597 | Entry Hall > Forge > Storeroom > Kitchen > Dining Hall | reached the end; opened by E: IronCitadel/Features/d_entry_hall__forge (Entry Hall) |
| the way-round entrance up to the gallery deck, avoiding the hall floor | **PASS** | 38.44 / 39.821 | Dining Hall > gallery | reached the end |
| the dining hall into the armory beneath the gallery, to the best gear | **PASS** | 98.624 / 99.732 | Dining Hall > Armory | through the armory's dining-hall door, which row 1 finds is not beneath the gallery: reached the end; opened by E: IronCitadel/Features/d_armory__dining_hall (Dining Hall) |
| into the vault by the hidden way, and back | **PASS** | 55.644 / 55.107 | Storeroom > Treasure Vault > Storeroom | reached the end; opened by E: IronCitadel/Features/d_storeroom__treasure_vault/SecretDoor (Storeroom) |
| the dining hall to the throne | **PASS** | 221.327 / 222.113 | Dining Hall > Library > Shrine > Throne Room | reached the end; opened by E: IronCitadel/Features/d_dining_hall__library (Dining Hall), IronCitadel/Features/d_shrine__throne_room (Shrine) |

## Notes

- row 5 (information): no complete NavMesh path from Storeroom to the hoard's floor at (112.3, 0.1, -128.5) (PathPartial; it ends 1.5 m short, at (111.0, 0.2, -127.8))

## Rooms (numbers on the map)

| # | name | kind | state | floor_y | outline m2 |
|---|---|---|---|---|---|
| 1 | Entry Hall | entry hall | looted | 0.095 | 260 |
| 2 | Armory | armory | stocked | 0.095 | 368 |
| 3 | Dining Hall | dining hall | decayed | 0 | 1101 |
| 4 | Library | library | looted | 0.095 | 841 |
| 5 | Alchemist's Study | alchemist's study | decayed | 0 | 240 |
| 6 | Torture Chamber | torture chamber | decayed | 0 | 492 |
| 7 | Forge | forge | looted | 0 | 787 |
| 8 | Storeroom | storeroom | decayed | 0 | 339 |
| 9 | Kitchen | kitchen | decayed | 0.095 | 497 |
| 10 | Shrine | shrine | looted | 0 | 981 |
| 11 | Throne Room | throne room | looted | 0 | 744 |
| 12 | Prison | prison | looted | 0 | 753 |
| 13 | Guard Room | guard room | decayed | 0 | 426 |
| 14 | Treasure Vault | treasure vault | untouched | 0.095 | 212 |
| 15 | Crypt | crypt | decayed | 0 | 262 |

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
