# Conformance: IronCitadel

- Scene: `Assets/Scenes/IronCitadel.unity`
- Level: The Iron Citadel Infiltration (`C:\Repos\Github\Crawfis-Software\UnityAssets\docs\research\AI-DungeonLevelComparison\level.json`)
- Run: 2026-10-07T19:04:00Z, Unity 6000.6.0f1
- Bake: 4902 NavMesh triangles from 1218 colliders in 0.091 s; agent r 0.35, h 1.8, step 0.3, slope 45, skin 0.02 (bake and walk)
- Start: object tagged Player: PlayerCapsule, at (112.5, 0, -176.6) in entry_hall

**Result: 10 pass, 0 fail, 1 manual.**

| # | must_be_true | result | measured | reason |
|---|---|---|---|---|
| 1 | the eleven rooms stand on the cells the grid gives them, on 45 m tiles; every # cell is solid rock | **PASS** | 11 rooms hold NavMesh; rock cells are empty | every room cell holds at least 40 m2 of level NavMesh and no # cell holds more than 1 m2 |
| 2 | every door listed exists and meets its neighbour's; there is no other opening between tiles | **PASS** | 10/10 doors; 0 missing; 0 extra | the NavMesh opens exactly the 10 listed doors (outer_door left out; the portcullis and the secret door are checks 3 and 5) |
| 3 | the armory_gate, with spikes in front of it, cannot be passed; the armory is entered only from the great hall | **PASS** | gate shut; armory opens only to the hall | no NavMesh crosses armory_gate, and the armory's only opening is to great_hall. The spikes are judged by eye. |
| 4 | west_wing and east_wing both join the entry hall to the great hall, and neither passes through the armory | **PASS** | west and east wings both reach the hall | both wings link up cell by cell, the NavMesh joins the start to the hall inside each wing's own cells (so not through the armory), and a real path start -> hall is complete |
| 5 | the vault has exactly one way in, the bookcase door from the prison; closed, it looks like the bookcases beside it; the vault lies on no route | **PASS** | sealed when closed; opened, a dead end off the prison | closed, a passage crosses the prison edge but the prison has no path into the vault; with the 9 colliders within 1.5 m of the vault_door span or with an Interact() method within 8 m of it switched off and the NavMesh re-baked, its only opening is prison, so it is a dead end and on no route |
| 5b | the vault has exactly one way in, the bookcase door from the prison; closed, it looks like the bookcases beside it; the vault lies on no route [the look] | **MANUAL** | judge in the editor | Whether the closed bookcase door looks like the bookcases beside it is a visual call. Look at the prison's west end. |
| 6 | the throne room has exactly one door, from the great hall | **PASS** | one opening, to the hall | the throne room's only opening is to great_hall |
| 7 | the gallery runs along the great hall's south side about 5 m up, with a stair at each end, west and east; each stair climbs from a landing that the hall's west or east door reaches without crossing the hall floor; under_gallery opens beneath it; the whole hall floor is visible from its rail | **PASS** | deck 5.0 m, climbs at both ends, side paths clear, 96% of floor seen | a 248 m2 deck 5.0 m up along the south side, reachable from the floor; a climb at each end; both side doorways reach it without the central floor; the rail sees 96% of the floor; under_gallery opens beneath it |
| 8 | the throne stands on a stage 1.25 m up with steps across its whole front | **PASS** | stage 1.25 m, steps on 100% of the 39 m free width at its front | a 313 m2 stage 1.25 m up on the north side, 40 m wide, reached by steps along 100% of the 39 m free width at its front line (the room cell's floor is 40 m wide) |
| 9 | the prison, guard room, kitchen and vault are low; the great hall and throne room are tall | **PASS** | low (<= 6.5 m, want 75%): guard_room 100%, prison 100%, vault 100%, kitchen 100%; tall (>= 8 m, want 25%): great_hall 80%, throne_room 99% | every low room has at least 75% of its floor under a ceiling of at most 6.5 m, and every tall room at least 25% under one of at least 8 m; standard rooms are reported, not scored. Information, not scored: throne_room p90 15.0 m against about 15 m. |
| 10 | WALK: a first-person CharacterController walks the key routes (not a must_be_true line) | **PASS** | 6/6 routes walked | a CharacterController (r 0.35, h 1.8, step 0.3) followed every route to its end |

## Opening graph

| between | cells | kind | via | shared edge (m) | gaps | crossing | off edge centre (m) |
|---|---|---|---|---|---|---|---|
| entry_hall - library | [3,2] [3,3] | edge | navmesh | 6.641 | 1 | (134.95, 0.01, -157.45) | 0.071 |
| forge - study | [1,3] [2,3] | edge | navmesh | 6.641 | 1 | (157.45, 0.01, -90.05) | 0.071 |
| great_hall - armory | [1,2] [2,2] | edge | navmesh | 6.641 | 1 | (112.45, 0.01, -90.05) | 0.071 |
| great_hall - forge | [1,2] [1,3] | edge | navmesh | 6.641 | 1 | (134.95, 0.01, -67.45) | 0.071 |
| guard_room - entry_hall | [3,1] [3,2] | edge | navmesh | 6.641 | 1 | (90.05, 0.01, -157.45) | 0.071 |
| kitchen - great_hall | [1,1] [1,2] | edge | navmesh | 6.641 | 1 | (90.05, 0.01, -67.45) | 0.071 |
| kitchen - prison | [1,1] [2,1] | edge | navmesh | 11.006 | 1 | (67.478, 0.01, -90.5) | 0.5 |
| prison - guard_room | [2,1] [3,1] | edge | navmesh | 6.641 | 1 | (67.55, 0.01, -134.95) | 0.071 |
| study - library | [2,3] [3,3] | edge | navmesh | 6.641 | 1 | (157.45, 0.01, -134.95) | 0.071 |
| throne_room - great_hall | [0,2] [1,2] | edge | navmesh | 7.517 | 1 | (112.45, 0.01, -44.95) | 0.071 |
| vault - prison | [2,0] [2,1] | edge | navmesh | 16.61 | 1 | (44.328, 0.01, -112.685) | 0.697 |

## Walks

| route | result | walked / planned (m) | note |
|---|---|---|---|
| start to hall, west wing | **PASS** | 187.43 / 188.131 | reached the end |
| start to hall, east wing | **PASS** | 187.125 / 187.868 | reached the end |
| hall west door to gallery, west stair | **PASS** | 22.35 / 23.397 | reached the end |
| hall east door to gallery, east stair | **PASS** | 22.35 / 23.397 | reached the end |
| hall to the throne stage | **PASS** | 65.991 / 66.396 | reached the end |
| prison to vault (opened) | **PASS** | 40.35 / 40.505 | reached the end |

## Map

`conformance-map.png`: north is up, 4 px per metre. Green is floor-level NavMesh, olive raised NavMesh; orange, cyan and yellow are the samples checks 8 and 7 found as the stage, the gallery deck and the climbs; grey-blue is NavMesh that is not level space (roofs, wall tops, table tops). White dots are listed doors, red dots openings that should not exist, the blue dot is the start, pink lines are the walks and red crosses their snags.

## Thresholds (Tune, top of ConformanceChecks.cs)

- `AgentRadius` = 0.35
- `AgentHeight` = 1.8
- `AgentClimb` = 0.3
- `AgentSlope` = 45
- `BakeRadius` = 0.35
- `BakeHeight` = 1.8
- `VoxelSize` = 0.1
- `MinRegionArea` = 2
- `BakeMargin` = 45
- `BakeBottom` = -10
- `BakeTop` = 60
- `WeldXZ` = 0.05
- `WeldY` = 0.35
- `TJunctionMinOverlap` = 0.05
- `GapClusterDist` = 6
- `FloorBandLow` = -1
- `FloorBandHigh` = 1
- `FloorProbeStep` = 1.5
- `TriHeightSlack` = 1.5
- `LevelComponentMinFloorArea` = 25
- `RoomMinNavArea` = 40
- `RockMaxNavArea` = 1
- `StartSnapRadius` = 4
- `VaultSpanHalf` = 2
- `VaultDoorHeight` = 3.5
- `VaultDisableRadius` = 1.5
- `VaultFloorClearance` = 0.3
- `VaultSkipLargerThan` = 50
- `VaultInteractReach` = 8
- `SampleStep` = 0.5
- `ClimbSampleRise` = 0.5
- `GalleryHeightTolerance` = 1
- `GallerySideBand` = 15
- `GalleryMinArea` = 20
- `ClimbBandLow` = 1
- `ClimbBandTopMargin` = 0.75
- `ClimbEndZone` = 12
- `CentralInsetEnds` = 7
- `CentralInsetGallerySide` = 7
- `CentralInsetFar` = 3
- `FloorLevelMaxRise` = 1
- `PathSampleStep` = 0.5
- `EyeHeight` = 1.6
- `RailLean` = 0.55
- `RailSampleStep` = 2
- `HallOpenAbove` = 1
- `FloorSampleStep` = 2
- `FloorTargetLift` = 0.2
- `VisibilityInsetEnds` = 6
- `MinVisibleFraction` = 0.9
- `UnderGalleryMaxDist` = 3
- `StageTolerance` = 0.25
- `StageMinArea` = 20
- `StageFrontLineOffset` = 1
- `StageFrontProbe` = 4
- `StageMinStepFraction` = 0.8
- `CeilingSampleStep` = 3
- `CeilingMaxRay` = 60
- `LowCeilingMax` = 6.5
- `LowMinFraction` = 0.75
- `TallCeilingMin` = 8
- `TallMinFraction` = 0.25
- `HeightTargetPercentile` = 0.9
- `HeightTargetTolerance` = 3
- `WalkSpeed` = 3
- `WalkDt` = 0.05
- `WalkReach` = 0.3
- `WalkStuckSeconds` = 2
- `WalkStuckProgress` = 0.05
- `WalkFallDrop` = 2.5
- `WalkEndTolerance` = 1
- `WalkSkin` = 0.02

Full measurements are in `conformance.json`.
