# Checks: Hollowmere Manor (`Assets/Scenes/IronCitadel.unity`)

## How to re-run every check and walk

There is one entry point. Use either of these:

- **Script:** `bash Checks/run_checks.sh`, run from the project folder or from anywhere. It opens the editor if it isn't
  running, calls the editor method below, and waits for `Checks/results.txt` to end with its `SUMMARY` line.
  It exits 0 only if nothing failed.
- **Editor method:** `Hollowmere.EditorTools.CheckRunner.RunAll()`, also on the menu as **Hollowmere > Run All Checks**.
  From the CLI: `unity command --timeout 120 eval -- --code 'return Hollowmere.EditorTools.CheckRunner.RunAll();' --timeout 110000`.
  The method returns at once. Then wait until `Checks/results.txt` has a line that starts with `SUMMARY:`
  (about 4 minutes; the walks run in real time).

`RunAll()` does the following:

1. It opens the saved scene from disk. Any unsaved edits in the editor are dropped, so the run checks the scene as saved.
2. It writes `Map/map.json` from that scene, through `LevelSurvey.MapJson()`. Every position and outline is read
   from the scene's colliders and components.
3. It writes a request file and enters play mode.
4. In play mode, `Hollowmere.Checks.CheckSuite` starts automatically and runs every static check and every walk.
   It writes `Checks/results.txt`, `Checks/walk_trace.csv` and `Checks/screens/*.png`, then leaves play mode.

## Where the code is

Unity compiles code only under `Assets/`, so the checks live there:

| File | What it does |
|---|---|
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.cs` | Harness, sight-line helpers, results file |
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.Static.cs` | Static checks S01–S31: they measure the built scene |
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.Walks.cs` | Walks W1–W5, the transits and the room tour |
| `Assets/Hollowmere/Checks/Runtime/LevelSurvey.cs` | Measures rooms, openings, gallery, stage and markers from colliders; writes the map |
| `Assets/Hollowmere/Checks/Runtime/MiniJson.cs` | Reads `Map/map.json` back so the map can be checked against the scene |
| `Assets/Hollowmere/Editor/CheckRunner.cs` | The entry point described above |

The level itself is rebuilt by `Hollowmere.EditorTools.LevelBuilder.Build()` (menu **Hollowmere > Rebuild Level**).
That code is in `Assets/Hollowmere/Editor/LevelBuilder.*.cs`. Rebuilding overwrites the scene, so re-run the checks after it.

## How the walks drive the player

The walks use `Assets/Interact/IronCitadelPlayer.prefab` exactly as it ships: the same CharacterController and
controller settings, and the start pose from the scene. Nothing on it is changed.

The walker plans a route on the baked NavMesh. It then moves the player only in these ways:

- **Move and sprint input:** `StarterAssetsInputs.MoveInput(Vector2.up)` and `SprintInput()`. Sprint is used only on transits.
- **Turning:** it sets the player's yaw to face a point 0.8 m ahead on the route. The brief allows this.
- **Doors:** when a shut door on the route is within 1.9 m (2.7 m for the hidden bookcase, which swings toward the
  player), it stops, faces the door and calls the E-key script's `InteractOnE.Interact()`. That casts the
  player's own 3 m ray.

It never teleports the player, moves its transform, or unsticks, sidesteps or retries.

A walk **fails** in any of these cases, and the run stops at the first failed walk:

- the player stalls: less than 0.3 m of progress in any 2.5 s window;
- the walk times out;
- a door on the route does not open;
- the walk breaks its own rule: entering the gun room on the way round, or stepping onto the hall floor on the way up to the gallery;
- at the end, the HUD's room name disagrees with the room the player actually stands in.

The walks run in one continuous play session, so the player is never placed:

| Leg | Route |
|---|---|
| W1 | Start → the dining hall by the way round |
| W2 | Up the back stair to the gallery rail |
| T1 | Transit back down |
| W3 | Under the gallery and through the gun room's back door, to the best gear |
| T2 | Transit to the library |
| W4 | Open the hidden bookcase and reach the hoard, then back out (W4b) |
| T3 | Transit to the hall |
| W5 | Hall → library → laboratory → ballroom → up the stage steps to the master's chair |
| Tour | Every remaining room, opening the last shut doors |

`walk_trace.csv` logs the player's position, its room and the HUD's room name every 0.2 s.

## What the static checks measure

Each line in `results.txt` gives what was measured.

| Check | What it measures |
|---|---|
| S01–S02 | Room kinds and states |
| S03–S06 | Floor-to-ceiling heights from the floor and ceiling colliders |
| S07 | Ceiling coverage, from upward rays on a 1 m grid |
| S08 | Rays in 24 directions from every room, plus NavMesh triangles outside rooms (indoors) |
| S09 | Every listed opening: clear width, height, and open or closed |
| S10 | Probes through every wall every 0.25 m × 0.35 m, looking for openings that are not listed |
| S11–S15 | The gun room's two ways in; sight of the entrance from the start; guards hidden from the whole foyer (0.5 m grid, standing and crouched eyes, 6 body points per guard) and visible from the doorway; the ranged guards' facing |
| S16 | The way round on the NavMesh, with the gun room's floor area excluded |
| S17–S19 | The gallery's height and length; how much floor is visible from the rail; the route up to it with the hall floor excluded; whether any defender sees that route (140° view cones) |
| S20 | Stage height, riser heights, and the chair on the stage at the far end |
| S21–S22 | The strong room: a single hidden way in, and no other route to it |
| S23 | Every shut door has a solid collider under a parent that carries `Interact()` |
| S24 | Tall and low occluders; floor hidden from the gallery when standing and when crouched |
| S25–S26 | Where the markers stand, room by room |
| S27 | Sparseness: share of each room's floor under furniture |
| S28 | `map.json` read back and compared with the scene |
| S29–S30 | Rendered luminance per room |
| S31 | The M overview: orthographic, ceilings culled, the whole level in frame |
| S32 | The HUD clock and room name |
