# Opus v3 cold run (brief v2): score and findings

- Build: `C:/Repos/ColdRuns/opus-v3`, scene `Assets/Scenes/IronCitadel.unity`, map `Map/map.json`.
  - Prompt `cold/cold-prompt-v3.md`; brief `cold/brief-v2-draft.md` (act one reworded: the guards come into view
    only at the armory's doorway).
  - The run took 1:23:49 wall clock (20:08 to 21:32 on 2026-10-13), with 1:13:50 of API time.
  - It made 192 tool calls over 204 turns and cost $27.34 at list price, billed to the Max plan.
  - The audit found 0 hits, and no auto-memory was written.
  - Outputs: `cold/v3/opus/` (summary, audit, final reply, stream).
- Scored on a copy, `C:/Repos/ColdRuns/opus-v3-score`, with kit v2 (`kit/conformance-v2/`) and the run's own
  `map.json`. The kit's I2 row says the map matches the scene.
- **Result: 11 pass, 0 fail, 0 manual** (`conformance.md`). All five walks reach their end. The run itself reported
  19 static checks and 7 play-mode walks, all passing.

## The first score (3 pass, 8 fail) was the kit's fault

Three generic fixes, listed in the kit README's Changes section:

- The shut doors' leaves and the secret bookcase carry carving NavMesh obstacles. The kit switched the leaves'
  colliders off, but the obstacles kept cutting its bake, so every shut door and the hidden door read as walls.
  Rows 1, 2, 3, 5, 6, 7, 8 and 10 failed with them.
- Two doors at the same x and z, one on the floor and one onto the gallery: both crossings matched the floor door.
- The sealed vault is a 4.2 m² NavMesh island, under the 25 m² that level space needs.

The first scores of all three builds are kept in `scores/v3/_kit-v2.0/`. Ours scores the same with the fixed kit,
row for row: 9 pass, 2 fail on the main scene and on seed 7.

## Side by side

| row | the run's own check | kit v2 on Opus | kit v2 on ours (main and seed 7) |
|---|---|---|---|
| 1 armory: two ways in, flank beneath the gallery | S03 pass | pass: flank door 0.4 m from the deck outline | **fail**: the flank door is not beneath the gallery |
| 1b guards seen only at the doorway | S05 pass: 0 of 459 hall eye points see one | pass: no entry-hall sample sees one | pass: only within 4.3 m of the doorway |
| 2 the way round | S06, W1 pass (39.7 m walked) | pass: 54.5 m via the storeroom, kitchen and Servery Stair | pass: 216.9 m (seed 7: 266.6 m) |
| 3 gallery reached unseen; the rail sees the floor | S07, S08 pass: 52% of the whole floor, 64% not under the deck (its own bar: 50% and 60%) | pass: 100% of the open floor from the leaned rail eyes | **fail**: 67% (want 75%) |
| 4 throne on a stage with steps | S09 pass | pass: 1.50 m stage | pass: 1.27 m stage |
| 5 the hidden vault | S10 pass | pass: sealed when closed; opens to the library only | pass |
| 6 heights | S11 pass | pass | pass |
| 7 indoors, every room roofed | S12 pass | pass | pass |
| 8 doors and doorways | S02, S13 pass | pass: 17 doors, 5/5 shut doors open by E | pass: 15 doors, 8/8 |
| 9 tall and low occluders | S14 pass | pass: 47 tall, 39 low | pass: 34 tall, 55 low |
| 10 five walks | 7/7 | 5/5 | 5/5 |
| I1 each gallery shooter's view of the floor | | 71 to 77% each (5 shooters) | 26% for the shooter at the rail; 0% for the other three |

## What else differs

- **Scale.** Opus built 16 boxy rooms on a 5 m grid: 15 kinds plus the Servery Stair, an enclosed stairwell from
  the kitchen up to the gallery. Its way round is a quarter of ours.
- **Helpers it disclosed:**
  - a 0.28 m firing step along the rail;
  - an invisible ramp over the throne steps;
  - coloured capsule markers for enemies (they have no colliders);
  - realtime lights only.
- **Its time** (report (e)): planning 18 min, building 27, checking and fixing 22, dressing and lighting 7, final
  rebuild, captures and runs 8.
- **Its suggestions** (report (f)):
  - make "much of the floor", "low" and the crouch height measurable;
  - say whether a walk may place the player at its start;
  - list the rendering pitfalls (a renderer change shows one editor tick late; the GPU-resident drawer keeps stale
    transforms);
  - ship a crouch control, or drop crouching from the brief.
- **A kit artifact, harmless here.** The room graph shows a 14.7 m "edge" crossing between the Entry Hall and the
  Guard Room. Long NavMesh triangles reach through the doorway, with their centres in the entry hall. It matches
  listed door #14, and the run's own wall scan (a ray every 0.1 m) found no unlisted gap.
