# Iron Citadel, pattern-tile level: walk report

Scene `Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity`, level `Assets/CrawfisSoftware/PolygonDungeon/Levels/IronCitadel/iron-citadel.prefab`, 2026-10-07 08:23.
Written by `IronCitadel.Patterns.Editor.IronCitadelPatternsPlay.WalkAndCapture`. The solver's own promised-against-measured report is `solve-report.md` beside this file.

## What ran

- **Not the validator's nine checks.** Every `must_be_true` line in level.json names its rooms on 45 m cells (rooms on cells, doors, the armory gate, the wings, the vault, the throne room's door, the gallery, the stage, the heights). This level is 3 x 4 pattern slots of 100 m, so none of them has anything to measure; the conformance CLI would fail its check 1 on the grid alone.
- **Check 10's method, with the kit's own code.** The NavMesh is baked with the kit's `NavBake.Settings()` (bake r 0.4, h 1.9, step 0.25, slope 45, voxel 0.1) from physics colliders; each route is planned on it and then walked by the kit's `Walker.Walk`: a CharacterController r 0.5, h 2, step 0.25, skin 0.02, the Starter Assets PlayerCapsule, at 3 m/s. A route passes when the capsule reaches its end without sticking 2 s or falling.
- **The layout against the build.** Every stand cell of the solver's stitched layout (60 x 80 cells of 5 m) is looked up on the NavMesh and checked to be joined to the entry.

## Walks

**8 of 8 routes walked.**

| route | what it tests | result | planned m | walked m | note |
|---|---|---|---|---|---|
| entry to the stronghold (the goal) | the whole walk: choke, round the gate, west wing, cache side, gallery, keep | PASS | 565 | 566 | reached the end |
| entry to the exit, out of the north rim | in by the south door, out by the north one | PASS | 610 | 611 | reached the end |
| through the flank in Choke (entry) | the second way round the neck at (30,5), end to end | PASS | 118 | 118 | reached the end |
| through the flank in Flanking route (west wing) | the second way round the neck at (8,29), end to end | PASS | 298 | 299 | reached the end |
| entry up to the gallery walkway | the raised gallery, up its stairs | PASS | 531 | 530 | reached the end |
| entry to the gallery floor | the floor the walkway overlooks | PASS | 485 | 486 | reached the end |
| entry to the hidden cache | the pocket behind the hidden door | PASS | 452 | 452 | reached the end |
| entry up to the sniper's perch | the raised perch over the watched ground, by its way up | PASS | 167 | 167 | reached the end |

Stops, in order:

- entry to the stronghold (the goal): the entry (PlayerStart) (0, 0, -197.5) -> the stronghold's post (-2.5, 0, 152.5)
- entry to the exit, out of the north rim: the entry (PlayerStart) (0, 0, -197.5) -> the exit (29,79) (-2.5, 0, 197.5)
- through the flank in Choke (entry): the entry (PlayerStart) (0, 0, -197.5) -> flank end (33,2) (17.5, 0, -187.5) -> flank middle (35,11) (27.5, 0, -142.5) -> flank end (32,18) (12.5, 0, -107.5)
- through the flank in Flanking route (west wing): the entry (PlayerStart) (0, 0, -197.5) -> flank end (18,31) (-57.5, 0, -42.5) -> flank middle (8,34) (-107.5, 0, -27.5) -> flank end (3,32) (-132.5, 0.1, -37.5)
- entry up to the gallery walkway: the entry (PlayerStart) (0, 0, -197.5) -> the walkway (32,48) (12.5, 2.5, 42.5)
- entry to the gallery floor: the entry (PlayerStart) (0, 0, -197.5) -> the gallery floor (32,51) (12.4, 0, 57.4)
- entry to the hidden cache: the entry (PlayerStart) (0, 0, -197.5) -> the cache (11,51) (-92.5, 0.1, 57.5)
- entry up to the sniper's perch: the entry (PlayerStart) (0, 0, -197.5) -> the perch (29,27) (-2.5, 2.5, -62.5)

## The layout against the build

- Stand cells in the stitched layout: 553. Joined to the entry on the NavMesh: **552**. With NavMesh that is not joined: 0. With no NavMesh: 1.
- Rock (`#`) cells with NavMesh joined to the entry: 689 (712 with any NavMesh). These are floor the realized tiles carry beyond the layout's stand cells, such as side rooms and wide passages.
- NavMesh: 24549 m2 in all, 24231 m2 of it in the piece the entry stands on (99 %; the rest is wall tops, prop tops and closed-off pockets).

Stand cells not joined to the entry, by region:

- Gallery (great hall) · floor (no NavMesh): 1 cell(s): (34,53)

## Do the measured patterns hold in the build?

The solver measured its patterns on the stitched layout (stand cells only), not on the built tiles, and the built tiles carry floor the layout calls rock. So each claim that depends on the ways through is tested again on the build: tall boxes close the named cells, the NavMesh is re-baked with the same settings, and a path is looked for between the two sides.

**8 of 8 hold** (8 ran).

| pattern, between | closed | expected | found | result | detail |
|---|---|---|---|---|---|
| Choke (entry), slot 1,0: (30,2) to (30,9) | the neck (30,5) (30,6) | still a way through | a way through | HOLDS | path 155 m via Choke (entry) > Choke (entry) · flank > Choke (entry) · flank rejoins > Choke (entry); every cell it crosses is a layout stand cell |
| Choke (entry), slot 1,0: (30,2) to (30,9) | the flank at (35,10) (35,11) (35,12) | still a way through | a way through | HOLDS | path 35 m via Choke (entry) > Choke (entry) · the neck > Choke (entry); every cell it crosses is a layout stand cell |
| Choke (entry), slot 1,0: (30,2) to (30,9) | the neck and the flank | no way through | no way through | HOLDS | no complete path (PathPartial) |
| Flanking route (west wing), slot 0,1: (12,29) to (4,29) | the neck (7,29) (8,29) (9,29) | still a way through | a way through | HOLDS | path 159 m via Flanking route (west wing) > Flanking route (west wing) · flank rejoins > Flanking route (west wing) · flank > Flanking route (west wing); every cell it crosses is a layout stand cell |
| Flanking route (west wing), slot 0,1: (12,29) to (4,29) | the flank at (7,34) (8,34) (9,34) | still a way through | a way through | HOLDS | path 40 m via Flanking route (west wing) > Flanking route (west wing) · the neck > Flanking route (west wing); every cell it crosses is a layout stand cell |
| Flanking route (west wing), slot 0,1: (12,29) to (4,29) | the neck and the flank | no way through | no way through | HOLDS | no complete path (PathPartial) |
| Hidden cache (vault), slot 0,2: the entry to the cache (11,51) | the hidden door's mouth (13,53) | no way through | no way through | HOLDS | no complete path (PathPartial) |
| Stronghold (throne), slot 1,3: the entry to the keep (30,70) | both keep doors (29,67) (29,73) | no way through | no way through | HOLDS | no complete path (PathPartial) |

`gates-map.png`: the closed cells in magenta; a path found where none was expected in thick red, a path found where one was expected in thin green.

## Every pattern region from the entry

A NavMesh path from PlayerStart to the middle cell of each area the stitched layout names.

| region | at | NavMesh path | path m |
|---|---|---|---|
| Choke (entry) · flank  (1,0:flank) | (27.5, 0, -147.5) | PathComplete | 66 |
| Choke (entry) · the neck  (1,0:neck) | (2.5, 0, -172.5) | PathComplete | 25 |
| Choke (entry) · flank rejoins  (1,0:rejoin) | (12.5, 0, -107.5) | PathComplete | 91 |
| Flanking route (west wing) · flank  (0,1:flank) | (-102.5, 0, -27.5) | PathComplete | 244 |
| Flanking route (west wing) · the neck  (0,1:neck) | (-107.5, 0, -52.5) | PathComplete | 215 |
| Flanking route (west wing) · flank rejoins  (0,1:rejoin) | (-57.5, 0, -42.5) | PathComplete | 183 |
| Sniper location (gate) · sniper's perch  (1,1:perch) | (-2.5, 2.5, -62.5) | PathComplete | 167 |
| Sniper location (gate) · watched ground  (1,1:watched-ground) | (-2.5, 0, -52.5) | PathComplete | 180 |
| Sniper location (gate) · way up  (1,1:way-up) | (-17.5, 0, -77.5) | PathComplete | 134 |
| Hidden cache (vault) · hidden door  (0,2:door) | (-77.5, 0.1, 67.5) | PathComplete | 433 |
| Hidden cache (vault) · the cache  (0,2:pocket) | (-92.5, 0.1, 57.5) | PathComplete | 452 |
| Hidden cache (vault) · trunk passage  (0,2:trunk) | (-62.5, 0.1, 47.5) | PathComplete | 408 |
| Gallery (great hall) · floor  (1,2:floor) | (12.4, 0, 57.4) | PathComplete | 485 |
| Gallery (great hall) · stairs  (1,2:stairs) | (32.5, 1.4, 42.5) | PathComplete | 511 |
| Gallery (great hall) · walkway  (1,2:walkway) | (12.5, 2.5, 42.5) | PathComplete | 531 |
| Stronghold (throne) · keep door  (1,3:door-1) | (-2.5, 0, 137.5) | PathComplete | 550 |
| Stronghold (throne) · keep door  (1,3:door-2) | (-2.5, 0, 167.5) | PathComplete | 581 |
| Stronghold (throne) · the keep  (1,3:keep) | (2.5, 0, 152.5) | PathComplete | 565 |
| Stronghold (throne) · the post  (1,3:post) | (-2.5, 0, 147.5) | PathComplete | 560 |

## Files

- `walk-map.png`: top view at 4 px/m, north up, the 100 m slots in yellow. Each walk's trace is a coloured line in the order of the table (white, cyan, orange, violet, green, yellow, pink, light blue); a red X is a snag; the green box is the entry, the gold box the goal; red boxes are stand cells not joined to the entry.
- `walk.json`: the same, as data.
- Eye captures: `C:/Repos/IronCitadel/captures/patterns/`.
