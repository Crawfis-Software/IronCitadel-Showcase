# Hollowmere Manor (the mansion brief): Opus cold run vs ours, kit v2

The brief is v2 re-themed as a haunted manor (`cold/brief-v2-mansion.md`): the same acts, the same floor-plan rules
and the same 15 room kinds under mansion names (foyer, gun room, dining hall, ballroom, strong room, ...), with 3 m
storeys. Both sides build with Synty PolygonHorrorMansion (HM), and kit v2 scores both.

## Result

| | Opus cold run (`opus-hm/`) | ours (`ours-hm/`) |
|---|---|---|
| kit v2 1.1 | **11 pass, 0 fail** | **10 pass, 1 fail** |
| first score, before this round's kit fixes and the rail ruling | 9 pass, 2 fail | 8 pass, 3 fail |
| its own checks | 42 of 42 (static checks and play-mode walks) | tactics 4/4, fold 22/22, 6/6 walk legs |
| size | 53 x 78 m, 16 rooms (the extra is a back stair) | 270 x 135 m, 15 rooms |
| the way round | 72.7 m | 208.6 m |

- **Opus:** `C:/Repos/ColdRuns/opus-v3-hm`, scene `Assets/Scenes/IronCitadel.unity`, map `Map/map.json`.
  - Prompt `cold/cold-prompt-v3-hm.md`, brief `cold/brief-v2-mansion.md`.
  - 1:40:34 wall clock (05:42 to 07:22 on 2026-10-14), 1:10:32 of API time, 234 tool calls over 235 turns.
  - $34.88 at list price, billed to the API key.
  - The audit found 0 hits. Outputs are in `cold/v3-hm/opus/`.
  - Scored on the copy `C:/Repos/ColdRuns/opus-v3-hm-score`.
  - Film: `flythrough/cold-v3/opus-hm/iron-citadel-v2-opus-hm-1080p.mp4`, 2:26, through all 16 rooms, filmed from that
    copy (`OUT=opus-hm flythrough/cold-v3/render.sh opus-v3-hm`; the tool now opens its `SwingDoor` doors). Re-shot
    2026-10-15; the 2:08 cut that followed only the pattern beats is in `flythrough/cold-v3/opus-hm/frames-2026-10-14/`.
- **Ours:** `v2/hm/` (`run-hm.sh`): seed 1's level, picked from the 108 HM rooms, built by
  `IronCitadelV2Builder.BuildMansion` into `IronCitadelV2_HM.unity`, and exported by `Ours/OursMapExporter`.
  - Film: `flythrough/v2-hm/iron-citadel-v2-hm-1080p.mp4`, 5:18, through all 15 rooms (`v2/tools/render-film.sh HM`;
    re-shot 2026-10-15, the 3:16 cut that followed only the pattern beats is in `flythrough/v2-hm/frames-2026-10-14/`).
- **Synty's own demo scene,** for comparison: `flythrough/cold-v3/synty-hm/synty-horror-mansion-demo-1080p.mp4`, 2:47.
  It films HM's `Demo.unity` as Synty ships it, one glide per room on every floor, and cuts between rooms because
  its doors are shut leaves. It renders from the Opus scoring copy, which holds the pack
  (`PROJ=C:/Repos/ColdRuns/opus-v3-hm-score OUT=synty-hm flythrough/cold-v3/render.sh synty-hm 600`), and its
  `"camera": "Main Camera"` option keeps the scene's own sky.

## Side by side

| row | Opus | ours |
|---|---|---|
| 1 armory: two ways in, the flank door beneath the gallery | pass: flank door 0.2 m from the deck outline | **fail**: the flank door opens 5 m from the deck (as on the citadel) |
| 1b guards seen only at the doorway | pass: no foyer sample sees one | pass: only within 2.9 m of the doorway |
| 2 the way round | pass: Foyer > Watch Room > Cellar Cells > Butcher's Room > Kitchen > Back Stair > Dining Hall | pass: Entry Hall > Storeroom > Forge > Kitchen > Dining Hall |
| 3 gallery reached unseen | pass: deck 4.2 m up, reached by the back stair | pass: deck 3.0 m up, reached from the kitchen door |
| 3(c) the rail's view of the floor (information) | 65% (its own measure: 72%) | 72% (67% on the citadel) |
| 4 throne on a stage with steps | pass: 1.20 m stage | pass: 1.50 m stage |
| 5 the hidden vault | pass: a bookcase in the library | pass: a bookcase in the storeroom |
| 6 heights | pass: ballroom 12 m, dining hall 9 m | pass: throne room 89% over 8 m, dining hall 35% |
| 7 roofed | pass | pass |
| 8 doors | pass: 17 doors, 7/7 shut doors open by E | pass: 15 doors, 8/8 |
| 9 occluders | pass: 58 tall, 42 low | pass: 452 tall, 83 low |
| 10 five walks | 5/5 | 5/5 |
| I1 each gallery shooter's view of the floor | 0% for all five (they stand 0.9 m back from the rail) | 33% and 39% for the two at the rail; 0% for the two set back |

Ours keeps the citadel's flank-door miss: our hall-gallery rooms put the deck 5 m in from the wall that the armory
door is in.

**Ours rebuilt, 2026-10-14 evening.** The HM rooms now stand a shut double door in every doorway, and the builder hangs
those leaves in the design doors (MacroTileBuilder #76). That rebuild (17:14) scored 9 pass, 2 fail on kit 1.1: row 1b
failed because the armory doorway is an open door whose two leaves stood open 1.1 m into the entry hall, and the
start, 12 m off at a slant, lost sight of the doorway. The builder now stands an open door's leaves into the room
further from the start (`fix/v2-open-leaves-away-from-start`, MacroTileBuilder #78); rebuilt with it, ours scores 10 pass, 1 fail
again. The guards then stood in a line beside the doorway, and ours' tactics check `flank_comes_in_behind` (the flank's
landing at least 90 degrees off every choke guard's facing) failed: 74.5 degrees at 17:14 and 86.5 after the leaf
fix; this morning's build had 143.7. The guard pick never looked at the flank. It now reads where the flank lands
from its first pick and, if a guard faces the landing, picks again with "the landing behind" ranked just under cover
(PCGTools `samples/IronCitadelV2/Tactics`, which is not in git). Rebuilt with it (21:23), the manor passes every check
of its own (tactics 4/4, fold 22/22, walks 6/6), the landing is 112.5 degrees or more off every facing, and kit 1.1
scores 10 pass, 1 fail, with row 1b at 2.9 m. Every citadel run (the main level, the sample, and seeds 2, 6, 7 and 40)
comes out of the new pick byte-identical.
The kit 1.0 scores of this morning's build are in `_kit-1.0/`.

**The rail's view is information, not scored** (Roger, 2026-10-14). A gallery sees the floor by construction: a
player leans over the rail and sees it all. The 75% bar had failed both sides here (Opus 65%, ours 72%) and ours on
the citadel (67%). By the same rule, ours on the citadel scores 10 pass, 1 fail (re-scored on 1.1, 2026-10-14).

## This round's kit fixes (both sides)

The kit README lists them under Changes (2026-10-14), with the rail ruling. The citadel scores are unaffected: each fix can only turn a
failure into a pass, and none of those builds failed for these reasons.

- **The deck's rise.** Row 3 now allows 5 cm under 3 m. With 3 m storeys, our deck sat a few millimetres under 3 m
  above the floor, so the exporter found no deck and row 3 failed.
- **The walker at a slant.** On the way to the throne, the walker hit our shut shrine door at a slant, slid past
  it, and stalled against the wall with its E ray on the wall. Now it also aims E at the nearest unopened door in
  reach, then walks back a corner before going on.
- **Row 8 for a door above the floor.** Opus's gallery door, 4.2 m up, was tested from the hall floor beneath it.
  It is now tested from its own height.
- **Row 8 for a pair of leaves.** A ray aimed at the centre of Opus's double chapel door threaded the seam between
  its leaves. The ray now also tries 0.25 m either side.
- **Ours only.** The exporter fills 1-cell bays beside doors, where an open leaf stands. The armory's 3 x 1 m
  notch read as a second doorway.

## What else differs

- **Construction.** Opus built the walls, floors and ceilings as boxes textured with HM's wallpaper, wood, brick
  and tile, and dressed the rooms with about 1,450 Synty props in all. Ours casts the rooms from HM wall, floor and roof
  pieces (16 kit pieces), plus 796 HM set-piece variants.
- **Helpers Opus disclosed:**
  - an invisible ramp over every flight of steps;
  - defenders standing on coloured rings;
  - a HUD with the room name and the time, and an M-key overview;
  - no Cinema LUTs.
- **Stand-ins on both sides.** HM has no rifles, so Opus's gun room holds revolvers, axes and tools. Ours uses a
  safe for the hoard, a lockbox for the best gear and goblets for the coins. Ours casts the defenders as the Count (the master), the Witch (ranged),
  Butcher/Werewolf (melee), Ghoul/Yokai (undead) and Gimp/Demon (guards), with ArmChair_05 as the master's chair.
  Opus posed HM characters lightly as its defenders.
- **Opus's suggestions** (report (f)):
  - warn about URP rendering layers and the GPU Resident Drawer's one-frame lag;
  - say which way the props face;
  - give a room-size budget;
  - ship a scripted Game-view capture.
- **Ours' gaps:**
  - HM set-piece coverage is 148 of 217 looks.
  - The vault step was skipped: there is no Kit unitypackage in the vault.
  - The room audit's void_sight averages about 115 per HM room (0 in R8). That's an audit artifact: rays run
    exactly along the 2.5 m wall-half seams.
