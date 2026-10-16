# Opus v3 cold run (brief v2): score and findings

- Build: `C:/Repos/ColdRuns/opus-v3`, scene `Assets/Scenes/IronCitadel.unity`, map `Map/map.json`.
  - Prompt `cold/cold-prompt-v3.md`; brief `cold/brief-v2-draft.md` (act one reworded: the guards come into view
    only at the armory's doorway).
  - The run took 1:23:49 wall clock (20:08 to 21:32 on 2026-10-13), with 1:13:50 of API time.
  - It made 192 tool calls over 204 turns and cost $27.34 at list price, billed to the Max plan. Output tokens:
    391k, 56% of them thinking.
  - The audit found 0 hits, and no auto-memory was written.
  - Outputs: `cold/v3/opus/` (summary, audit, final reply, stream).
- Scored on a copy, `C:/Repos/ColdRuns/opus-v3-score`, with kit v2 version 1.1 (`kit/conformance-v2/`, run
  2026-10-14 16:27Z) and the run's own `map.json`. The kit's I2 row says the map matches the scene.
- **Result: 11 pass, 0 fail, 0 manual** (`conformance.md`), the same on kit 1.0 and 1.1. All five walks reach their
  end. The run itself reported 19 static checks and 7 play-mode walks, all passing.
- It meets v3.1's pack-art rule, which it never saw (checked 2026-10-14): its only built-in meshes are its 25
  enemy markers, 9 staffs, the warlord's crown and 2 pickup gems. See `cold/cold-prompt-v3.1.md`, change 19.
- Film: `flythrough/cold-v3/opus/iron-citadel-v2-opus-1080p.mp4`, 2:09.

## Kit history: 3 pass, 8 fail before 1.0; 11 pass on 1.0 and 1.1

The first score was the kit's fault. Kit 1.0 made three generic fixes, listed in the kit README's Changes section:

- The shut doors' leaves and the secret bookcase carry carving NavMesh obstacles. The kit switched the leaves'
  colliders off, but the obstacles kept cutting its bake, so every shut door and the hidden door read as walls.
  Rows 1, 2, 3, 5, 6, 7, 8 and 10 failed with them.
- Two doors at the same x and z, one on the floor and one onto the gallery: both crossings matched the floor door.
- The sealed vault is a 4.2 m² NavMesh island, under the 25 m² that level space needs.

Kit 1.1 (2026-10-14) made three more for the Fable and Haiku runs: a map room that is the gallery, the throne room's
floor measured off the stage, and 5 cm of slack on the height bars. None changes an Opus row.

The rail's view of the hall floor (row 3) is information, not scored, by Roger's ruling of 2026-10-14; Opus's rail
sees 100% of it. The pre-1.0 scores are kept in `scores/v3/_kit-v2.0/`, and the 1.0 scores in `scores/v3/_kit-1.0/`.

## Side by side with ours

Ours, seed 7, re-run on the current builder and re-scored on kit 1.1 (2026-10-14): 10 pass, 1 fail, every measure as
on 1.0 (`scores/v3/ours-v2-seed-7/`). The main scene is still on 1.0, read under the rail ruling: 10 pass, 1 fail.

| row | the run's own check | kit v2 1.1 on Opus | kit v2 on ours (seed 7 on 1.1; main on 1.0) |
|---|---|---|---|
| 1 armory: two ways in, flank beneath the gallery | S03 pass | pass: flank door 0.4 m from the deck outline | **fail**: the flank door is 6.2 m from the deck outline (bar 1.5 m) |
| 1b guards seen only at the doorway | S05 pass: 0 of 459 hall eye points see one | pass: no entry-hall sample sees one | pass: only within 4.3 m of the doorway |
| 2 the way round | S06, W1 pass (39.7 m walked) | pass: 54.5 m via the storeroom, kitchen and Servery Stair | pass: 216.9 m (seed 7: 266.6 m) |
| 3 gallery reached unseen | S07 pass | pass: deck 5.0 m up, 51 m², reached without the floor | pass |
| 3, information: the rail sees the floor | S08 pass: 52% of the whole floor, 64% not under the deck (its own bar: 50% and 60%) | 100% | 67% (the old bar was 75%) |
| 4 throne on a stage with steps | S09 pass | pass: 1.50 m stage | pass: 1.27 m stage |
| 5 the hidden vault | S10 pass | pass: sealed when closed; opens to the library only | pass |
| 6 heights | S11 pass | pass | pass |
| 7 indoors, every room roofed | S12 pass | pass | pass |
| 8 doors and doorways | S02, S13 pass | pass: 17 doors, 5/5 shut doors open by E | pass: 15 doors, 8/8 |
| 9 tall and low occluders | S14 pass | pass: 47 tall, 39 low | pass: 34 tall, 55 low |
| 10 five walks | 7/7 | 5/5 | 5/5 |
| I1 each gallery shooter's view of the floor | | 71 to 77% each (5 shooters) | 26% for the shooter at the rail; 0% for the other three |

## Beside Fable and Haiku

The same brief and kit, on prompt v3.1 (v3 plus the pack-art sentence above). FINDINGS in `scores/v3/fable-v3/` and
`scores/v3/haiku-v3/`.

| | Opus v3 | Fable v3.1 | Haiku v3.1 |
|---|---|---|---|
| kit v2 1.1 | 11 / 0 | 10 / 1 (1b) | 9 / 2 (1b, 6), after a reimport; 4 / 7 as delivered |
| wall clock, tool calls, list cost | 1:23:49, 192, $27.34 | 1:39:24, 236, $36.55 | 1:03:47, 166, $5.02 |
| rooms | 16 (adds the Servery Stair) | 17 (adds a stair hall and an antechamber) | 16 (adds an east passage) |
| the vault's way in | a sliding bookcase in the library | a bricked-up doorway in the crypt | a sinking slab in the forge |
| its own checks | 19 static, 7 walks | 13 static, 5 walks, 4 play-mode | 161 checks, walks A to D |
| prefab instances / different | 1,267 / 255 | 1,258 / 283 | 679 / 66 |
| code, non-blank lines | 22 files, 3,423 (builder 1,312) | 20 files, 2,903 | 11 files, 2,243 |

Opus is the only cold run to pass 1b, and the only one whose own check measured it the way the kit does: every
armory defender, from eye points over the whole entry hall. That check first failed, and Opus narrowed the doorway
and moved the guards. Fable checked only where its guards stood and faced; Haiku looked from one point in the hall.

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
- **The bookcase.** Brief v2 names no bookcase door. Asked only how it would make the vault's way in, on the bare
  API, Opus picks a bookcase 7 times in 10 (`cold/probe-vault/tally.txt`): the brief's library of tall bookcases
  and its hidden door point there.
- **A kit artifact, harmless here.** The room graph shows a 14.7 m "edge" crossing between the Entry Hall and the
  Guard Room. Long NavMesh triangles reach through the doorway, with their centres in the entry hall. It matches
  listed door #14, and the run's own wall scan (a ray every 0.1 m) found no unlisted gap.
