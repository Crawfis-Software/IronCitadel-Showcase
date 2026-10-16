# Haiku v3.1 cold run (brief v2): score and findings

- Build: `C:/Repos/ColdRuns/haiku-v3`, scene `Assets/Scenes/IronCitadel.unity`, map `Map/map.json`.
  - Prompt `cold/cold-prompt-v3.1.md` (v3 plus change 19, pack art only); brief `cold/brief-v2-draft.md`.
  - The run took 1:03:47 wall clock (11:01 to 12:05 on 2026-10-14), with 41:08 of API time, at effort xhigh.
  - It made 166 tool calls over 167 turns and cost $5.02 at list price, billed to the Max plan.
  - The audit found 0 hits; no web, skill or agent calls. Outputs: `cold/v3/haiku/`.
- Scored with kit v2 1.1 and the run's own `map.json` on `C:/Repos/ColdRuns/haiku-v3-diag`, a copy whose five
  runtime scripts were reimported (below).
- **Result: 9 pass, 2 fail, 0 manual** (`conformance.md`). All five walks reach their end. The run reported 161
  checks passing and four walks, A to D.
- Film: `flythrough/cold-v3/haiku/iron-citadel-v2-haiku-1080p.mp4`, 2:39, shot on the reimported copy
  `haiku-v3-diag`.

## As delivered, no door opens (Roger's ruling: score after the reimport, report this)

Haiku wrote the `.meta` files of its five runtime scripts (`LevelDoor`, `LevelRoom`, `LevelMarker`, `LevelHud`,
`LevelFeature`) by hand, two lines each with no importer section. When the project is reopened, every one of them
is unbound: the `MonoScript` has the right GUID, but its class is null. So every door, room, marker and HUD
component in the scene is a missing script, and E opens nothing. A forced reimport of those five files binds them
again. Its own checks passed because they ran in the editor session that wrote the files.

As delivered, kit 1.0 scored it 4 pass, 7 fail: rows 2, 3, 8 and 10 failed on the doors
(`scores/v3/haiku-v3-as-delivered-kit-1.0/`, with the `door-diag*` files that found it).

## The fails

- **1b, the guards are seen from the entry hall.** The armory's entrance is an open doorway in line with the start.
  Twenty-five entry-hall samples more than 5 m from it see an armory enemy, up to 15.4 m back; every line checked
  passes through the doorway. The start sees a melee guard. Its own guard-sight check passed; the kit samples the
  whole entry hall.
- **6, the dining hall is not tall.** Its gallery deck, 20 by 15 m at 4.6 m, covers half of the 30 by 20 m hall.
  60% of the hall floor is under 6.5 m or less, and 16% sees the 8 m ceiling (want 25%).

## Kit 1.0 artifacts, fixed in 1.1

- Row 4: the stage spans the throne room's width and holds more NavMesh than the floor before it, so the kit took
  the stage top for the floor. Kit 1.1 measures the floor off the stage.
- Row 6's ceiling reads 7.99 m against the 8 m bar; 1.1 gives 5 cm. It still fails, on coverage.

## Notes for the talk

- **The vault's way in** is a stone slab in the forge that sinks on E: not a bookcase. Haiku chose crates or cells,
  never a bookcase, in the vault probe.
- It reports cutting corners: four walks, not five; M and E called as functions, not pressed; a rail bar of 50% it
  chose itself; about a third of the rooms reviewed closely.
- Its suggestions: a kit sheet with module sizes, door holes and stair sizes; say how the stair meets a gallery
  along one side; give "much of the floor" a number; edit-mode raycasts miss mesh colliders; document the look dead
  zone and offer an input-injection path.
