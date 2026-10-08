# Iron Citadel conformance kit

This kit scores a Unity scene against the `must_be_true` list in `level.json`. Every build is scored the
same way, whichever framework made it. The kit reads only physics colliders, NavMeshLinks and, as a
fallback for ceilings, renderer bounds. It contains no Crawfis code.

## Install

1. Copy this `conformance` folder anywhere under the project's `Assets/`. Copy it; do not junction it.
2. The project needs `com.unity.ai.navigation` 2.0.x, because the asmdef references `Unity.AI.Navigation`.
   It was developed on Unity 6000.6.0f1 with AI Navigation 2.0.14.

## Run

Batch:

```
unity run <project> --timeout 900 --no-tail -- -executeMethod IronCitadel.Conformance.ConformanceCli.Run
    -scene Assets/Scenes/<Level>.unity -level C:/path/level.json -out C:/path/scores/<build> [-noWalk]
    -logFile C:/path/scores/<build>/unity.log
```

In the editor:

- `Tools > Iron Citadel > Score Open Scene...` scores a saved scene. It refuses a scene with unsaved changes,
  and afterwards it reloads the scene from disk.
- `Tools > Iron Citadel > Build Greybox From level.json...` builds the reference greybox fixture.

A run writes three files to `-out`:

- `conformance.json`, with one row per `must_be_true` line (pass, fail or manual), its measured values and
  a reason.
- `conformance.md`, the same as tables, followed by the opening graph, the walks and the thresholds.
- `conformance-map.png`, a top view at 4 px/m, with north up.

On an error the run writes `conformance-error.txt` and exits with code 2.

## Method

The scene is never saved.

- **Bake.** The NavMesh is baked at run time from physics colliders with the shared walker's own numbers:
  radius 0.35, height 1.8, step 0.3, slope 45 (`kit/walker`'s IronCitadelPlayer, a person-sized Starter Assets
  PlayerCapsule). The scene's own NavMeshSurfaces and CharacterControllers, and the colliders of the player rig
  under each controller, are switched off for the run and switched back on afterwards. Its NavMeshLinks are
  re-added as links.
- **Cells.** The cells follow level.json's origin rule: cell [r,c] spans x 45c..45(c+1) and
  z -45(r+1)..-45r, and the floor is at y 0. A build that does not honour this rule fails check 1.
- **Openings.** Two cells are joined when NavMesh triangles in the two cells share an edge, or when a
  NavMeshLink connects them.
- **Heights.** All heights come from `NavMesh.SamplePosition`, which is built with a height mesh. This
  covers the floor, the stage, the gallery deck and the climbs. Triangle heights are not used, because one
  polygon can span both a stage and the floor below it.
- **Check 5.** Every collider in the bookcase doorway span is switched off and the NavMesh is re-baked, so
  the opened vault can be checked as well.
- **Check 8.** The steps are judged against the free width at the stage's front line: the NavMesh along a line
  1 m in front of the stage, from wall to wall at that line, not the room cell's width. Steps must come down
  from the stage along at least 80% of that width.
- **Check 9.** Scores the line as written. A low room (prison, guard room, kitchen, vault) has at least 75% of
  its floor samples under a ceiling of at most 6.5 m; a tall room (great hall, throne room) has at least 25%
  under a ceiling of at least 8 m. Standard rooms are reported, not scored. The throne room's 90th-percentile
  ceiling is reported against "about 15 m" as information.
- **Check 10.** A temporary CharacterController (r 0.35, h 1.8, step 0.3, skin 0.02) walks the key routes.

Every threshold is in the `Tune` block at the top of `Editor/ConformanceChecks.cs`.

Two things are judged by eye and are not scored: whether the closed bookcase door looks like the bookcases
beside it (row 5b), and the spikes in front of the portcullis.

## Changes

2026-10-07, while scoring the rooms build (`scores/rooms/`):

- **Walker agent.** The agent went from r 0.3 / h 1.8 / step 0.45 to r 0.5 / h 2 / step 0.25, the Starter
  Assets PlayerCapsule every build now ships. Re-score earlier results (the greybox fixture included) before
  comparing them with new ones.
- **Bake radius 0.4, walk radius 0.5.** At r 0.5, Recast closed or thinned 1.55 m doorways that the 0.5
  capsule clears by 0.27 m a side: colliders are rasterized a voxel fat, then eroded ceil(r / voxel) voxels,
  then simplified; the doors that survived were 0.45-0.86 m wide. The bake now uses `Tune.BakeRadius` 0.4;
  check 10's CharacterController keeps 0.5, so a gap the player cannot pass still fails.
- **Bake height 1.9, walk height 2.** At h 2 the bake also shut Synty doorways whose lintel stands 2.05-2.13 m
  over a 0.09 m floor tile (floors round up and lintels round down by up to a voxel height), which the 2 m
  capsule passes under. In the rooms build that shut six of the ten listed doors (cell_block, prison_kitchen,
  kitchen_hall, forge_hall, under_gallery, throne_doors), every one with a 0.09 m floor under its lintel, and
  checks 3-7 and 10 fell with them. The bake now uses `Tune.BakeHeight` 1.9; check 10 keeps 2.
- **Walker skin 0.02.** Check 10's CharacterController now uses the PlayerCapsule's skin width (was 0.03).
  Under those 2.05 m lintels the capsule plus skin needs 2.04 m with 0.02 and 2.06 m with 0.03, so the old
  value would fail doorways that the shipped player walks through. The margin is 1 cm, so walk-test them.
- **Rail eye.** `RailLean` is now `BakeRadius + 0.2`, so the eye stays 0.2 m past the rail's inner face
  whatever the bake radius (with r 0.5 and a fixed 0.5 lean it sat on the rail and the rail hid the floor
  under it).
- **Check 7 (d), the hall floor.** Floor under a ceiling lower than the deck + 1 m (`HallOpenAbove`) is
  skipped and counted in `skipped_samples`: it is a side room, passage or vestibule sharing the hall's cell
  under its own lid, which no rail can see through a wall. The rooms build's hall cell holds such rooms
  behind the hall's north wall.
- **Check 9.** Each room also reports `upper_quartile_m`, for information only; the verdict still uses the
  median, which in a cell with low passages or an under-gallery walkway can sit well below the main lid.

2026-10-07, round 2 (a person-sized walker; checks 8 and 9 rescoped):

- **Walker agent r 0.35 / h 1.8 / step 0.3.** The shared player (`kit/walker`, IronCitadelPlayer) is now sized
  to a person: CharacterController radius 0.35, height 1.8, centre 0.9, step 0.3, slope 45, skin 0.02. The
  PlayerCapsule's 0.5 / 2 cleared Synty's 2.05 m lintels by 1 cm. The bake and check 10's CharacterController
  both use these numbers (`AgentRadius`, `AgentHeight`, `AgentClimb`; `WalkSkin` 0.02).
- **The 0.4 / 1.9 bake workaround is gone.** The bake uses the agent size. `BakeRadius` and `BakeHeight` stay
  only as aliases of `AgentRadius` and `AgentHeight`, because the Builder's patterns report prints them.
  `RailLean` is now `AgentRadius + 0.2`. Earlier scores are not comparable: re-score.
- **The player rig's own colliders are off during a run.** Starter Assets' PlayerCapsule carries a visible
  capsule with its own CapsuleCollider. It was baked as an obstacle at PlayerStart and sat where check 10's
  walker starts. `SceneGuard` now switches off every collider under each CharacterController, and restores them.
- **Check 8, the stage.** The brief wants "a full-width row of steps". The steps are now judged against the
  FREE width at the stage's front line: the NavMesh along a line `StageFrontLineOffset` (1 m) in front of the
  stage's median front edge, sampled every 0.5 m at floor, mid-step and stage height, and contiguous from the
  stage's centre to a wall each way. At least `StageMinStepFraction` (80%) of that free width (less 0.5 m at
  each wall) must step down from the stage. A column of the free width with no stage counts as no step.
  `StageMinWidthFraction` (60% of the room cell's floor width) is removed. The room cell's floor width and the
  stage's share of the free width are still reported. An aisled throne room whose stage spans the nave wall to
  wall now passes, where it failed at 41% of the 34 m room.
- **Check 9, heights.** It now scores exactly the must_be_true line, "the prison, guard room, kitchen and vault
  are low; the great hall and throne room are tall", instead of ordering medians low < standard < tall. LOW: at
  least `LowMinFraction` (75%) of the room's floor samples sit under a ceiling of at most `LowCeilingMax`
  (6.5 m). TALL: at least `TallMinFraction` (25%) under a ceiling of at least `TallCeilingMin` (8 m). Samples
  with no ceiling above them count as neither. Standard rooms are reported (low and tall fractions, median,
  p90), not scored. The throne room's "about 15 m" is no longer scored: its 90th-percentile ceiling
  (`HeightTargetPercentile`) is reported against 15 m as information. The median and the upper quartile are
  gone from the verdict.
