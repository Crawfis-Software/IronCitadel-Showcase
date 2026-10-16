# Iron Citadel conformance kit v2 (brief v2)

Scores a built Iron Citadel level against brief v2's "What must be true" (`cold/brief-v2-draft.md`). It reads the
level's own map (`map.json`, THE MAP in `cold/cold-prompt-v3.md`), bakes the scene's NavMesh three times in edit
mode, and writes `conformance.json`, `conformance.md` and `conformance-map.png`. Rows 1 to 10 are scored as pass,
fail or manual. Rows I1 to I3 are information and are never scored. The scene is never saved.

Kit v1 (`../conformance/`, brief v1, `level.json` on a 45 m grid) is separate and unchanged.

## Install

Copy `Editor/` into the scored project, for example at `Assets/_Scoring/conformance-v2/Editor`. It is one
editor-only assembly, `IronCitadel.ConformanceV2.Editor`, that references `Unity.AI.Navigation` (the AI
Navigation package). It needs nothing else from the project.

`Editor/Ours/` holds the exporter that writes `map.json` for our own v2 build. It finds the
`IronCitadel.Rooms.IronCitadelMarker` type by reflection, so it compiles in any project and can stay in the copy.

## Run

Close the project's editor first: a batch run needs the project's lock.

```
unity run <project> --timeout 900 --no-tail -- -executeMethod IronCitadel.ConformanceV2.ConformanceCli.Run \
    -scene Assets/Scenes/IronCitadel.unity -map C:/path/map.json -out C:/path/scores/x [-noWalk] \
    -logFile C:/path/scores/x/unity.log
```

- `-scene` is "Assets/..." or an absolute path inside the project.
- `-noWalk` skips row 10, which then reports as manual.
- On an error the run writes `<out>/conformance-error.txt` and exits 2.
- In the editor, use **Tools > Iron Citadel > Score Open Scene (brief v2)...**. It reloads the scene from disk
  afterwards.

For our own build, export the map and score in one run:

```
unity run ../MacroTileBuilder-agent --timeout 900 --no-tail -- -executeMethod IronCitadel.ConformanceV2.Ours.ExportCli.Run \
    -run C:/Repos/IronCitadel/v2 -scene Assets/CrawfisSoftware/AllSyntyDungeons/Levels/IronCitadelV2.unity \
    -out C:/Repos/IronCitadel/v2/map.json -score C:/Repos/IronCitadel/scores/v3/ours-v2 -logFile <log>
```

To score a cold run, work on a copy of it:

1. `robocopy C:\Repos\ColdRuns\<run> C:\Repos\ColdRuns\<run>-score /MIR /MT:16 /XD Temp Logs /XF *.csproj *.slnx`
2. `robocopy C:\Repos\IronCitadel\kit\conformance-v2\Editor C:\Repos\ColdRuns\<run>-score\Assets\_Scoring\conformance-v2\Editor /E`
3. Delete the copy's `Library/EditorInstance.json`, or `unity run` exits 6.
4. Run the command above against the copy, with the run's own `map.json`.

## Method

**Rooms come from the map.** Each NavMesh triangle belongs to:

- the gallery, if its centre is inside the deck outline and within 1 m of `deck_y`;
- otherwise the room whose outline holds the centre and whose `floor_y` band (-1 to +2.5 m) holds its height, with
  the nearest `floor_y` winning;
- otherwise no room.

**Openings.** The room graph comes from the NavMesh, not from the map's door list:

- Level triangles of two rooms that touch are an "edge" crossing. Touching contacts within 1 m of each other are
  one crossing.
- Unassigned NavMesh that touches exactly two rooms is a "doorway" (a door in a wall's thickness).
- Unassigned NavMesh that touches three or more rooms is a "passage".
- The scene's NavMeshLinks are crossings too.
- A crossing matches a listed door when it joins the same two rooms and lies within width / 2 + 2 m of the
  door's position, counting only its points within 1.5 m of the door's height. The gallery counts as the dining
  hall.

**Three bakes**, with the agent at r 0.35, h 1.8, step 0.3, slope 45 and voxel 0.1 (v1's):

1. As built.
2. Shut doors opened. A shut door is opened by casting the E ray from floor on each side of it, plus any
   `Interact()` receiver within 1.5 m of it. Opening switches the receiver's colliders off; a door's own swing
   does not run in edit mode.
3. Every door opened. The hidden door is opened like v1 check 5: `Interact()` colliders within 8 m of it, plus
   the E ray from its neighbour. Failing those, v1's span is used. Every other `Interact()` receiver small
   enough to be a door (at most 50 colliders and 12 m across) is opened too, so an unlisted door neither blocks
   a path nor hides an opening from row 8. The report lists them.

The door colliders are switched back on after the last bake, so sight lines see the doors as built.

The enemies' own bodies are switched off for the bakes and the sight lines. These are small colliders centred on
each enemy's position: they are markers, not level geometry.

Every `NavMeshObstacle` is switched off for the whole run, like the scene's NavMeshSurfaces and links. A carving
obstacle on a door leaf would keep cutting the kit's bake after the leaf's colliders are off. The header counts them.

**The E contract.** From a floor point within 4 m of the door, cast a 3 m ray from eye height (1.6 m) toward the
door. The first solid collider it hits must reach a method named `Interact` (taking one parameter at most) through
`SendMessageUpwards`. CharacterControllers are skipped: the walker's own capsule holds its eye.

**Areas.** Two NavMesh areas are marked with modifier boxes, so no project setting is needed. Each is left out of
the path queries that must avoid it:

- The armory, area 29: its outline within its floor band. Used by row 2 and walk 1.
- The hall floor, area 28: the dining hall's NavMesh more than 3 m inside its outline and more than 1.5 m from the
  deck outline, from floor_y - 1 to floor_y + 1. Used by row 3 and walk 2.

## Rows and thresholds

| row | test |
|---|---|
| 1 | With every door opened, the armory has exactly two crossings. **Choke:** one joins the entry hall (or an "extra" room touching it), and at least one entry-hall floor sample (2 m grid, eye 1.6 m) sees a point 1 m above the doorway. **Flank:** one joins the dining hall or the gallery, under the deck outline or within 1.5 m of it, and below `deck_y`. A crossing at the end of a short passage also counts if its NavMesh route toward the hall's middle passes there within 8 m of the crossing. |
| 1b | No entry-hall sample more than 5 m from the doorway sees an armory enemy (eye 1.6 m to 1.3 m above the enemy's feet). The start sees the doorway and no armory enemy. |
| 2 | A complete path runs from the start to the dining hall's floor with the armory's area left out, and it never enters the armory. |
| 3 | **(a)** The deck's measured height is at least 3 m above the hall floor, less 5 cm of NavMesh noise (the map's `deck_y` too), with at least 20 m² of level NavMesh. **(b)** From the first point where the way round (row 2) enters the hall, a complete path reaches the deck with the hall floor's area left out. The nearest, middle and farthest deck samples are tried. **(c)** Information, not scored: v1's rail sight, an eye every 2 m along the rail, on the deck's NavMesh edge, leaned 0.55 m out, 1.6 m up. It reports the share of the hall's floor samples it sees (2 m grid, 0.2 m up). Samples under a lid lower than the deck + 1 m are skipped. |
| 4 | The throne lies inside the stage outline. The stage top (the NavMesh samples on it, 0.5 m grid; the median of the top cluster) is at least 0.4 m above the throne room's floor. A complete path climbs onto it from the room's floor without leaving the room. **Information, unscored:** the throne's position along the room's depth from each door. Past 66% counts as the far end. |
| 5 | **Closed** (shut doors opened, the hidden door closed): no path from the hidden door's neighbour into the vault. **Opened:** a complete path from the neighbour to the vault's floor nearest its outline's centroid, and the vault's only crossing is to that neighbour. Whether the hoard's own floor (within 4 m of it) is reachable is reported as information. |
| 6 | v1's ceiling method: a ray up from floor samples (3 m grid, or 1 m when that gives fewer than 10), with renderer bounds as a fallback. **Low rooms** (prison, guard room, kitchen, vault): at least 75% of samples under a ceiling of 6.5 m or less. **Tall rooms** (dining hall, throne room): at least 25% under 8 m or more. **Tallest:** no room's p90 ceiling above the throne room's p90 + 0.5 m. Equal heights pass. |
| 7 | In every room, at least 95% of the floor samples have a ceiling (a collider above, or a renderer at least 2 m × 2 m). |
| 8 | With every door opened, each crossing between two different rooms matches a listed door, and each listed door is a crossing. Each shut door has no as-built crossing near it. The E contract works from every side where floor was found, and there is at least one such side. |
| 9 | Occluders: solid colliders 0.6 to 8 m wide whose base is within 0.35 m of a room's floor. **Tall:** top at least 1.8 m above the floor. **Low:** top 0.8 to 1.3 m above it. **Away from walls:** floor NavMesh of the same room found 0.6 m out on at least 5 of 8 sides. The row passes when the level has both kinds; it reports them per room. |
| 10 | Five walks by a CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02; v1's walker). When blocked, it opens a shut door by E, aimed at its next corner, the one after, then the nearest unopened door within 3 m. Each walk passes only if it reaches its end without stalling. The walks are: (1) the start to the dining hall, avoiding the armory; (2) the way-round entrance to the gallery deck, avoiding the hall floor; (3) the dining hall into the armory by the flank door, to the best gear (when row 1 finds no flank door beneath the gallery, the walk uses the armory's dining-hall door and says so); (4) into the vault by the hidden way, and back; (5) the dining hall to the throne. |
| I1 | How much of the hall floor each gallery ranged enemy sees from its own eye (not leaned). |
| I2 | Map honesty. A scene object within 1 m of each enemy and pickup. Markers inside the room they name. Overlapping outlines. Kinds missing, used twice or unknown. Unknown states. `floor_y` against the measured floor (0.3 m). NavMesh outside every outline. |
| I3 | Defenders and pickups against the brief. Ranged behind the armory entrance (within 8 m, facing within 60°). Melee in the rest of the armory. Ranged on the gallery. The hall holds the largest group. The warlord is in the throne room. No one is in the entry hall. The best gear is in the armory and the hoard in the vault. |

Every threshold is in `Editor/Tune.cs`. Values marked `(v1)` are v1's. `conformance.md` lists them all.

## Differences from v1

- **Map.** v1 read `level.json` on a fixed 45 m grid. v2 reads the build's `map.json`, so rooms are outlines of
  any shape and height, and the gallery and the stage are declared.
- **Openings.** v1 counted crossings of grid-cell edges. v2 counts NavMesh crossings between outlines, including
  unassigned NavMesh in a wall's thickness.
- **Doors.** v1 opened only the hidden door (check 5). v2 bakes three times: as built, shut doors opened, and
  every door opened. It tests each shut door through the E contract, and the walker opens doors by E when blocked.
- **Areas.** v2 marks the armory and the hall floor as NavMesh areas, so rows 2 and 3 can avoid them.
- **Enemies.** v2 switches the enemies' bodies off. v1 had none to switch off.
- **Ceilings.** Same method as v1. The tallest rule is now "no room above the throne room's p90 + 0.5 m", and row
  7 requires a ceiling over 95% of each room.
- **Rail sight.** v1's method and its 75% threshold, from the map's rail instead of a grid side.
- **New rows.** The choke sight (1b), the stage climb (4), occluders (9) and the five walks (10) are new. The info
  rows check the map's honesty and the brief's defender lines.

## Changes

- **2026-10-13, after the first score of the Opus v3 cold run (3 pass, 8 fail).** Three fixes. Ours scores the
  same before and after: 9 pass, 2 fail on the main scene and on seed 7.
  - **NavMesh obstacles.** The scene guard now switches off every `NavMeshObstacle`. Opus's door leaves and the
    secret bookcase carry carving obstacles, which kept cutting the kit's bake after their colliders were off.
    All five shut doors and the hidden door read as walls, and rows 1, 2, 3, 5, 6, 7, 8 and 10 failed with them.
  - **Doors at two heights.** A crossing matches a listed door only at its points within 1.5 m of the door's
    height (`DoorMatchYBand`). Opus lists two doors at the same x and z, one on the floor and one onto the
    gallery. Both crossings matched the floor door, so the gallery door read as missing.
  - **A small sealed vault.** With the hidden door closed, the vault is an island. Opus's vault has 4.2 m² of
    NavMesh, under the 25 m² that level space needs, so row 5 found "no vault floor". The closed check now falls
    back to any floor NavMesh in the vault.

- **2026-10-14, after the first score of ours on Hollowmere Manor (the mansion brief, 3 m storeys; 8 pass, 3 fail).**
  Two kit fixes and one exporter fix. Ours scores 9 pass, 2 fail after them, the same two misses as on the citadel.
  - **The deck's rise.** Row 3(a) allows 5 cm under 3 m (`GalleryRiseSlack`). A 3 m storey's deck sat a few millimetres
    under 3 m above the floor plate, so the exporter found no deck and row 3 measured none.
  - **Doors approached at a slant.** The walker slid along a shut leaf and past it before it stalled, and the E ray,
    aimed at the next corners, then hit the wall. Stalled with no receiver on those rays, the walker now aims E at
    the nearest unopened door within 3 m, and after opening it walks back to the previous corner before going on.
  - **Ours' exporter** fills 1-cell bays beside a door's crossing (an opened leaf's footprint). The armory's
    outline had a 3 x 1 m notch there, and the kit counted its NavMesh as a second doorway into the dining hall.
  - **Row 8's E test, after the first score of the Opus mansion cold run (9 pass, 2 fail; 10 pass, 1 fail after).**
    A door well above a room's floor (Opus's gallery door, 4.2 m up) is now tested from NavMesh at the door's own
    height on that side, with the deck counting as the hall's; it was tested from the hall floor beneath it, and the
    ray passed under the door. The ray is also aimed 0.25 m either side along the door (`EAimSpread`) when the one at
    its centre finds no receiver: aimed at the centre of a pair of leaves, it threaded the seam between them.
  - **Row 3(c), the rail's view of the floor, is information, not scored** (Roger's ruling). A gallery sees the
    floor by construction: a player leans over the rail and sees it all. The 75% bar had failed ours on both
    briefs (67% and 72%) and Opus on the mansion (65%).
