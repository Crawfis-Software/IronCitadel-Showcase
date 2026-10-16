# Checks and walks for the Iron Citadel

One entry point re-runs every check and walk on the saved scene and writes `Checks/results.txt`:

    RunChecks.Run     (in Checks/RunChecks.cs)

## How to run

1. Open the project in the Unity editor (`unity open .`, then `unity status --until-ready --project-path .`).
2. Start the run. It returns at once; the work runs in the editor's update loop:

       unity command run_script -- --file Checks/RunChecks.cs --entry RunChecks.Run --timeout_ms 60000

3. Wait for `Checks/results.txt` to end with the line `DONE`. A full run takes about 3 minutes.
   Everything it does is in the file: `PASS` or `FAIL`, the measured value, and for walks the rooms in order.

Run it after any change to `Assets/Scenes/IronCitadel.unity`. Run `Build/ExportMap.cs` first if the map changes,
since the map check compares `Map/map.json` with the scene.

## What it does

- **Static checks (edit mode, the saved scene):** room kinds, one ceiling per floor tile, measured heights of each
  room against its brief band, door positions on the walls they open in, route reachability through the door graph
  (the way round avoids the armory; the vault lies on no way through), the gallery's height, stair and rail, the
  stage and throne, the markers' rooms, and that `Map/map.json` has the scene's rooms and doors.
- **Lighting (rendered):** each room is rendered from standing eye height; mean luminance must reach 0.06.
- **Physics checks (play mode):** floors and ceilings are found by rays on a 1 m grid; a sweep of 0.5 m rays across
  every room boundary at 0.5, 1.5 and 2.2 m finds any gap, which must be a listed open doorway; each door's clear
  width is measured; the stair and throne steps must rise at most 0.3 m (the player's step offset); the gallery view
  of the floor from the rail; the armory guards' sight lines; the tall and low cover.
- **Controls (play mode):** walking and sprinting move the player at different speeds.
- **Walks (play mode, one session each, all from the start):**
  - A: the way round (entry hall, guard room, prison, torture chamber, crypt, east passage) to the dining hall's arrival
    door, then up the stair to the gallery. Must not enter the armory or step on the hall floor after the arrival.
  - B: the same way round, then across the dining hall beneath the gallery into the armory, to its best gear.
  - C: entry hall, kitchen, storeroom, forge, E on the hidden door, the vault, and back to the forge.
  - D: the way round, then the library, the shrine and the throne room, up the steps to the throne.
  - Doors are opened only by aiming the camera and calling `InteractOnE.Interact()`. The player is moved only by
    `StarterAssetsInputs.MoveInput` and `LookInput`. A walk fails if it makes no progress for 4 s, and reports what
    it is touching when it does.
- **HUD (after walk D):** the HUD names the room, the throne is announced, and `SetMap(true)` (the code M calls)
  turns the overview on with the ceilings culled. M itself is a key press, so the key is not exercised.

## Other files

- `Build/BuildLevel.cs` builds the scene; `Build/ExportMap.cs` writes `Map/map.json`; `Build/Shots.cs` renders
  `Captures/`; `Build/Measure.cs` and `Build/Probe.cs` measured the kit (`Build/kit.txt`).
- The run sets the project's play-mode option (no domain reload, scene reload per session) and puts it back when it
  finishes.
