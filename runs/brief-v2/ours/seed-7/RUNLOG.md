# Iron Citadel v2 (ours), run log

Clock times are EDT on 2026-10-13.

- 15:45:32: design phase starts. Re-surveyed PCGTools (c6cb9293), wrote the prompt and fixed the run's decisions.
- 15:51 to 16:01: the contract between the stages (CONTRACT.md) and a hand-written bootstrap level (sample/level.json).
- 16:03: four agents start in parallel:
  - A1: the design program (PCGTools/samples/IronCitadelV2/Design);
  - A2: the tactics program (PCGTools/samples/IronCitadelV2/Tactics);
  - B: the picker fork (kit/pick/pick_rooms_v2.py);
  - C: the Unity builder, floor sampler and as-built check (MTB Assets/IronCitadel/V2).
- 16:05 to 16:21: B, the picker fork, is done. It wrote `pick_rooms_v2.py` and `sample/picks.json` (all 15 rotations verified).
  - The entry hall is barracks `mess`: the beds sit behind a spine wall, with mess tables, benches and weapon racks.
  - The study is `alchemy_lab`.
  - The audit rule is v1's: a failed walk is refused, and rooms over the gate are ranked last.
- 16:04 to 17:00: A1, the design program, is done.
  - Seed 1 passes all 27 checks, and the same seed writes the same bytes.
  - The archetype is ForeshadowingLoop. The choke is turned from a vista into an open doorway, and the Director reads an "intent" graph that keeps the vista.
  - It wrote `level.json`, `design.json` and `graph.svg`.
- 17:00: I ran the picker on the real `level.json`: 15 picks, 4 real audit findings, and no related rooms share a state.
- 16:05 to 17:18: C, the builder, is done.
  - Sample and real builds each take about 4 s, with 0 problems.
  - Doors match, the big loop is intact and the vault is a dead end. The real level has no enemies yet.
  - The editor is still open on the agent instance.
- 17:20: the tactics agent is told the real floor exists and that the player's start must be at least 10 m from the choke.
- 17:19 to 17:24: the walk tool runs on the sample level in play mode: all six legs pass (way round, vault, way round 2, gallery, flank, throne), 4 min of play.
- 16:04 to 17:26: A2, the tactics program, is done. On the real level all four checks pass and 33 enemies are placed. The player-start rule fails: no entry-hall cell 10 to 20 m from the choke can see a guard, because the doorway is a 1 m slot between two parallel corridors.
- 17:27: the real level is rebuilt with the placements (3.2 s, 33 of 33 enemies on the NavMesh, 0 problems). Re-running tactics on the new floor gives byte-identical output (9.2 s), so the floor is stable.
- 17:28: the walk tool starts on the real level.
- 17:28 to 17:32: the walk on the real level: all six legs pass, 808 m in 3.7 min, with five doors opened with E (one of them the hidden bookcase).
- 17:32: two extra shots (the throne stage from the floor, the guards through the choke doorway); a ray cast up from each of the 7,378 reached floor samples hits a ceiling.
- 17:33: the fold: design.json takes in the picks, placements, tactics and as-built checks; every fold check passes.
- 17:34: the scene is saved and the agent editor closed (graceful; no lock file left).
- 17:34 to 17:40: REPORT.md. The run ends at 17:40.

## After the run

- After 17:40, Roger on the choke: "it's best that you don't see the guards until you get to the jump" (the doorway). The level already does that.
- 18:19 to 18:21: the tactics start rule is flipped. The start must see the doorway and no guard, and check 1 now needs every entry-hall cell that sees a guard to lie within 5 m of the doorway. Guard and start placement prefer the same. Re-run on the real level (8.7 s) and the sample: both placements are byte-identical, so nothing is rebuilt, and all checks and rules pass. The fold is re-run.
- During the flip, Roger: "Let's do 3 or 4 more seeds to test that 4 seconds". A design sweep of seeds 1 to 40 gives 5 layouts, all passing; seeds 2, 6, 7 and 40 take one of each of the other four.
- 18:23: the builder gets `BuildAs(run, suffix)`, which saves `IronCitadelV2_<suffix>.unity` with its own NavMesh, so `IronCitadelV2.unity` stays as it is. `tools/run-seed.sh` runs design, pick, build, tactics, build again and the fold, timing each.
- 18:23:46 to 18:24:23: the agent editor opens (37 s).
- 18:25:16 to 18:42:51: the four seeds, each followed by its walk: about 20 s from design to fold (24 s for the first), builds of 2 to 3.5 s, walks of about 4 min. Every seed passes design, tactics, rules and walks. Seeds 7 and 40 fail one fold check: only 1 of the 6 hall-floor melee is placed.
- 18:43 to 18:46: the cause. With the hall one tile east, the tactics program takes the gallery deck for the room proper, finding 3 floor cells instead of about 200, and the hall check still passes. The fix: look for the hall floor among floor-level cells only, and fail the check below 49 floor cells. Seeds 1, 2 and 6 and the sample come out byte-identical; seeds 7 and 40 find 204 floor cells and place all 6.
- 18:46: seeds 7 and 40 are rebuilt (3.2 s each), folded and walked again.
- 18:46:42 to 18:50:45: seed 7's walk again: 6 of 6, 839 m.
- 18:50:51 to 18:54:54: seed 40's walk again: 6 of 6, 845 m. All five levels now pass everything.
- Roger: "Create videos if they are quite different. Pick one if similar". They are similar: 10 of 15 rooms are the same prefab in all five, and the layouts fall into two families. One film, of seed 7, the second family.
- 18:56: the film scene, `IronCitadelV2_seed-7_film.unity`: a copy of seed 7 with every shut door swung open and the hidden door given v1's `SecretBookcaseDoor`, so v1's flythrough tool can path through the doors and swing the bookcase.
- 18:57:38: the agent editor is closed (graceful; the gate reads free).
- 19:00 and 19:07 to 19:08: two previews (every 10th frame) of 11 shots; every path complete, 0 frames inside a collider.
- 19:18 to 19:21: previews of the changed shots. The gallery stop moves to the gap at the parapet's south end, because from anywhere else on the deck the 1.3 m parapet hides the hall floor. A 4.5 s shot of the gallery from the hall floor is added. The flank caption is corrected: the flank passage runs behind the gallery's back wall, not under the deck.
- 19:21:45 to 19:28:00: the full render, 5,692 frames at 2560×1440 in ten chunks of up to 600 frames (`tools/render-film.sh`).
- 19:28 to 19:31: the compose. Its stills show two faults. Paired captions fade over each other, so they are now 0.8 s apart (the choke, vault and gallery shots). The outro title is cut off by the map, so it is now "The Iron Citadel", with the run and seed in the subtitle.
- 19:32:22 to 19:33:38: a 3D sight check in the agent editor. A `Physics.Linecast` runs from each gallery ranged enemy's eye to 287 open hall-floor points, with enemy colliders off. Only the enemy at the rail sees the floor: 130 points from a 1.6 m eye, 210 from a 1.9 m eye. The other three stand 4.5 to 8.5 m back from the parapet (top 6.2 m) and see none. Seed 1 shows the same. The cause: the tactics program's 2.5D sight lets a raised viewer see past the parapet at any range. REPORT.md says so and the level is not changed. The hall shot's caption now says so too.
- 19:33:47 to 19:34:11: the hall shot is re-rendered at 6.5 s for its longer caption.
- To 19:35:44: the film is re-composed: 3 min 13 s, 1080p, 235 MB (`flythrough/v2-seed-7/iron-citadel-v2-seed-7-1080p.mp4`).
- 19:36 and 19:39: two smaller copies to send, a 1080p one at 73 MB and a 720p one at 25 MB (the upload limit is 30 MiB).

## Re-run on kit 1.1, 2026-10-14 evening

Roger: "rerun our own pipeline, generate the videos and update the talk". The Builder had kit v2 1.0; it now has
1.1 (MacroTileBuilder `chore/conformance-v2-1.1`).

- 20:33:39 to 20:33:57: the agent editor opens (18 s).
- 20:34:07 to 20:34:33: `tools/run-seed.sh 7`: design 3.9 s, pick 0.3, build 3.6, tactics 11.4, build again 3.6,
  fold 2.0 (25 s). Design 27/27, tactics 4/4, fold 22/22. The design, level, picks, placements, tactics and floor
  are byte-identical to 10-13; built.json differs only in its time stamp.
- 20:34:55 to 20:38:47: the walk: 6 of 6, 839 m. `walk-map.png` is redrawn with `walk_map.py`.
- 00:43:43Z: kit 1.1 on seed 7 (`scores/v3/ours-v2-seed-7/`): 10 pass, 1 fail (row 1, the flank door 6.2 m from
  the deck's outline). Every measure is as on 1.0; the 1.0 score moved to `scores/v3/_kit-1.0/`.
- The manor (`v2/hm`). The 17:14 rebuild (another session, after MacroTileBuilder #76 hung the HM rooms' own door
  leaves) scored 9 pass, 2 fail on kit 1.1: the armory doorway's open leaves stood 1.1 m into the entry hall and hid
  the doorway from the start (row 1b). The builder now stands an open door's leaves into the room further from the
  start (MacroTileBuilder `fix/v2-open-leaves-away-from-start`).
- 20:46:30 to 20:46:55: `hm/run-hm.sh` with the fix: pick 0.2 s, build 2.5, tactics 15.7, build again 3.9, fold 1.8.
  Six open doors now stand into the deeper room. Fold 21/22: tactics' `flank_comes_in_behind` fails, the flank landing
  86.5 degrees off a choke guard's facing (it wants 90; 74.5 at 17:14, 143.7 this morning). The guards now stand in a
  line beside the doorway. Not fixed; the kit does not score it.
- 20:48 to 20:51: the manor walk: 6 of 6.
- Kit 1.1 on the manor (`scores/v3-hm/ours-hm/`): 10 pass, 1 fail (the flank door); row 1b passes at 4.5 m. The 1.0
  score moved to `scores/v3-hm/_kit-1.0/`.
- 00:52:37Z: the stats probe on both scenes (`logs/stats/probe-ours-v2.json`, `probe-ours-hm.json`), then
  `kit/stats/aggregate_v3.py` rewrites `scores/v3/STATS.md`. Seed 7: 72 prefab instances of 29, 495 Synty pieces,
  6,227 renderers, 309 lights. The manor: 85 of 29, 185 pieces, 13,127 renderers, 471 lights.
- 20:53 to 20:59:31: the film re-rendered from the re-run's film scene (`tools/render-film.sh 7`), ten chunks, the
  same 5,797 frames per shot, 0 frames inside a collider. Yesterday's output is in `_before-2026-10-14/`.
- To 21:01:10: composed, 3 min 13 s, 235 MB; the share copy is 73 MB and the phone copy 26.5 MB. Its frames match
  yesterday's within flicker.

## The manor's guard pick and its film, 2026-10-14 night

Roger: "YES GO" to fixing ours' failing tactics check on the manor and to a manor film.

- The cause: the choke guards are picked for staying out of sight from the entry hall, cover and distance; the
  flank never entered it. With the rooms' own door leaves, the best cells became a line beside the doorway, and the
  flank's landing (52.5, -40.5) was 86.5 degrees off the third guard's facing.
- The fix (PCGTools `samples/IronCitadelV2/Tactics/Tactics.cs`, `PlaceChoke` and a new `LandingOf`; not in git): pick
  as before, read where the way round to those guards lands (as `CheckFlank` does), and if any guard faces it, pick
  again with "the landing behind" worth 1 point, just under cover. Tried on copies: the main level, the sample and
  seeds 2, 6, 7 and 40 come out byte-identical (placements and tactics); the manor re-picks once and passes.
- 21:22:11 to 21:22:48: the agent editor opens.
- 21:22:48 to 21:23:14: `hm/run-hm.sh`: pick 0.2 s, build 3.0, tactics 16.3, build again 3.9, fold 2.1 (25.5 s).
  Tactics 4/4, the landing 112.5 degrees or more off every facing; fold 22/22. Only placements.json changed among the
  inputs to the build.
- 21:23:24 to 21:26:50: the walk: 6 of 6, 693 m.
- 21:27: the film scene `IronCitadelV2_HM_film.unity` (8 shut doors swung open, the storeroom bookcase given
  `SecretBookcaseDoor`); the editor closed.
- 21:27:45 to 21:28:00: kit 1.1: 10 pass, 1 fail (the flank door); row 1b passes at 2.9 m.
- 21:28 to 21:32: three previews of `flythrough/v2-hm/shots.json` (11 shots, written for seed 1's layout from the walk
  traces). Fixed: the vault stop faced a wall, the flank stop sat behind the armory's inner wall, the hall shot's
  camera started inside a wall, and the library shot's first leg cut through a collider. `tools/render-film.sh` now
  takes a name (`HM`) as well as a seed.
- 21:32:35 to 21:38:30: the film rendered (`tools/render-film.sh HM 600`), ten chunks, 5,828 frames over 10 shots,
  0 frames with the camera inside a collider.
- 21:38:50 to 21:40:26: composed, 3 min 16 s. The outro's title, "Hollowmere Manor", ran past its block beside the
  map and was cut off; `flythrough/compose.py` now shrinks a title wider than its block (every other film's titles
  fit at the old size and render as before). Recomposed 21:41:35 to 21:43:00.
- To 21:43:53: the share copy (80.0 MiB) and the phone copy (720p, 25.6 MiB), encoded as seed 7's.
