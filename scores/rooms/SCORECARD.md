# Scorecard: IronCitadel_Rooms (our rooms level)

- Scene: `MacroTileBuilder/Assets/IronCitadel/Scenes/IronCitadel_Rooms.unity`, rebuilt by
  `IronCitadelRoomsAssembler.BuildAndScore` (log `logs/r9-rooms-build-score.log`).
- Scored 2026-10-07T22:13:29Z against the canonical `level.json`, with the same validator as the cold runs
  (`conformance.md`, `.json`, `-map.png` in this folder). That is the kit as fixed after the Opus score; see
  `kit/conformance/README.md`.
- Hall prefab: `HallGallery/HallGalleryBackFeastK9NESW_01352840.prefab`, rebuilt by PrefabSynthesis PR #206
  and PR #207. Kitchen prefab: `Kitchen/KitchenNotchCookingK9NE_01352840.prefab`, rebuilt by PR #208. Their paths
  and GUIDs did not change, so the picks did not either.

**Final: 10 pass, 0 fail, 1 manual**, from the framework's own prefabs: the round-4 scene patches now change
nothing. Opus's cold build scores the same with the same kit (`scores/opus/`); Fable's scores 8 / 2 / 1
(`scores/fable/`).

## must_be_true

| # | Item | Verdict | Measured |
|---|---|---|---|
| 1 | eleven rooms on their 45 m cells; # cells solid rock | PASS | 11 rooms hold NavMesh; rock cells empty |
| 2 | every listed door exists and meets its neighbour; no other opening | PASS | 10/10 doors, 0 missing, 0 extra |
| 3 | armory_gate with spikes cannot be passed; armory only from the hall | PASS | gate shut; the armory opens only to great_hall |
| 4 | west and east wings both join entry to hall, not through the armory | PASS | both wings reach the hall in their own cells |
| 5 | vault: one way in (bookcase door from the prison), on no route | PASS | sealed when closed; opened, a dead end off the prison |
| 5b | closed, the bookcase door looks like the bookcases beside it | MANUAL | by eye (`captures/rooms/feature-vault_door-closed.png`) it reads as one more bookcase in the row; Roger's call |
| 6 | throne room has exactly one door, from the great hall | PASS | one opening, to great_hall |
| 7 | gallery on the hall's south side about 5 m up, a stair at each end from landings reached without crossing the hall floor, under_gallery beneath, whole floor visible from the rail | PASS | deck 5.0 m on the south (279 m2); climbs west and east; both side paths clear of the floor; floor 92% visible; under_gallery beneath |
| 8 | throne on a stage 1.25 m up with steps across its whole front | PASS | stage 1.18 m; steps on 86% of the 15 m free width (want 80%) |
| 9 | prison, guard room, kitchen, vault low; great hall, throne room tall | PASS | low: guard_room, prison, vault and kitchen 100% (want 75%); tall: great_hall 34%, throne_room 89% (want 25%) |
| 10 | WALK (not a must_be_true line): a CharacterController walks the key routes | PASS | 6/6 routes, gallery stairs and throne stage included |

## Score history (pass / fail / manual)

| Round | Result | What changed |
|---|---|---|
| 1 | 4 / 6 / 1 | first build (after two 1/9/1 scoring rounds) |
| 2 pre | 7 / 3 / 1 | player, validator and room-pick fixes (`conformance-round2-pre.md`) |
| 3, run 1 | 8 / 2 / 1 | rebuilt over the fixed hall (PR #206); stage-front move added (check 8: 75% to 86%) |
| 3, run 2 | 8 / 2 / 1 | stage-front band narrowed to move only the lantern |
| 3, run 3 | 8 / 2 / 1 | the lantern's overlap test fixed; it now stands 2.75 m off the foot of the steps, not 6.5 m |
| 3, kit fix | 8 / 2 / 1 | re-scored with the kit as fixed after the Opus score; only check 5's wording changed (`round3-kitfix/`; the earlier kit's result is in `before-kit-fix/`) |
| 4 | 10 / 0 / 1 | scene copy: low rooms capped at a 5 m lid (check 9: kitchen 40% to 100%); the hall's east landing stub mirrored onto the west landing (check 7 passes) (`round4-scenefix/`) |
| **5 (final)** | **10 / 0 / 1** | the same two fixes at the source (PrefabSynthesis #207 hall, #208 kitchen), rebuilt and imported; every row measures as in round 4, and the scene steps log no change |

## Round 5: the two fixes at the source

- **PR #207** (stacked on #206): `fam_gallery_b` casts both landing mouths as `Wall_07` arches. The west one is
  named from its far cell, turned 180 degrees, so the pair mirror with the opening by the outer wall.
- **PR #208**: a ceiling mode, `low_course` (one 5 m course, no lowered lid, so the 5 m wall fireplace and timber
  baffle stand whole). The kitchen theme's Notch family takes it.
- Both batches (HallGallery, Kitchen) were rebuilt, materialized 50/50 and imported. Then the scene was rebuilt.
  - The build log says "low lid: kitchen already at 5 m or under; left as shipped", and no "hall landing" line.
  - `logs/rooms-fix-probe.txt`: kitchen 100% of 430 floor samples under 6.5 m, nothing above 5.5 m. The west
    landing edge is one prefab `Wall_07` at yaw 0.
  - By eye: `captures/rooms/landing-edge-{west,east}.png` mirror each other, and `eye-kitchen.png` is a low, lit
    cooking kitchen. Round 4's captures are in `captures/rooms-round4/`.

## Round 4: the two fixes (scene copy only, like the lantern move; now no-ops)

- **Check 9, the kitchen.** No catalog kitchen is low and walkable between two adjacent doors
  (`logs/rooms-picks-trial.txt`), and the Notch kitchen's hearth hall has a 10 m lid.
  - `CapLowLids` acts on every room level.json calls low. It drops each ceiling piece more than 0.5 m above
    5 m by whole 5 m bands, and switches off the wall pieces then wholly above the new lid.
  - In the kitchen, that dropped 26 lid pieces by 5 m and switched off 29 upper-band walls, including the top
    half of the beam screen. The guard room, prison and vault were already at 5 m and are untouched.
  - By eye (`captures/rooms/eye-kitchen.png` against `captures/rooms-round3/eye-kitchen.png`), the hall
    reads as a low, coffered kitchen with no gap at the walls.
- **Check 7, the west landing.** The hall prefab meets its two stair shafts unevenly.
  - On the east, the landing's edge to the shaft is an `SM_Env_Wall_07` stub with the gap by the outer wall.
  - On the west, it is a pair of round door frames with the gap in the middle, 6.7 to 8.3 m in. So the path
    from the west door ran more than 7 m in at floor level, inside check 7's hall-floor inset, though it never
    left the side hall.
  - `MirrorHallLandingStub` mirrors the east stub onto the west edge and switches the frames off.
  - `captures/rooms/landing-edge-west.png` and `landing-edge-east.png` now show the same arched stub,
    mirrored.

## Open decisions for Roger

1. **Merge the source fixes**: PrefabSynthesis #206, then #207 (retarget it to master), and #208. Done and
   scored here from the branches; the Builder already holds the rebuilt prefabs.
2. **5b.** Judge the closed bookcase door by eye, in the editor or from `feature-vault_door-closed.png`.
3. **Picks that depart from level.json.** These are unchanged, except that the kitchen is now low in the scene:
   - the guard room is 'manned' in the Watch layout, where level.json asks for 'night watch';
   - the entry hall has a 5 m lid and the library 15 m, where level.json asks for standard;
   - the outer door is shut.
4. **The moved lantern.** The throne room's stone lantern stands 2.75 m off the foot of the steps (scene copy only),
   not 0.25 m. Keep the move, or change the ThroneRoomAisledCourt prefab at its source.
5. **Other hall prefabs.** HallGalleryP2Feast still has the stair-head wall, if another level ever needs it.

## Captures (`C:/Repos/IronCitadel/captures/rooms/`, read)

- `overview.png`: all eleven rooms on their cells.
- `eye-*.png`: one per room. `eye-kitchen.png` shows the 5 m lid.
- `gallery-stair-west.png`, `gallery-stair-east.png`: from each landing, up the walled stair to its head.
- `landing-edge-west.png`, `landing-edge-east.png`: each landing's edge to its stair shaft, from the side hall
  (written by `IronCitadelFixProbe`).
- `gallery-rail.png`: on the deck behind the rail, looking east along the gallery, with the hall below on
  the left.
- Viewpoints are in `captures.txt`. Round 3's set is in `captures/rooms-round3/`, round 2's in
  `captures/rooms-round2-pre/`.
