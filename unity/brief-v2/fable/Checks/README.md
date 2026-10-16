# Iron Citadel checks and walks

Everything here is re-run by one entry point in the project's editor scripts:

    IronCitadel.EditorTools.IronCitadelChecks.RunAll()
    (menu: Tools / Iron Citadel / Run checks and walks)

Source: `Assets/IronCitadel/Editor/IronCitadelChecks.cs` (checks, walk definitions, map export) and
`Assets/IronCitadel/Runtime/WalkDriver.cs` (the play-mode walker).

## How to run

With the project's editor open (the walks need play mode, so a batch-mode `-quit` run cannot do them):

    unity command --timeout 60 eval -- --code "IronCitadel.EditorTools.IronCitadelChecks.RunAll(); return 1;"

`RunAll()` opens `Assets/Scenes/IronCitadel.unity`, runs the static checks, writes `Map/map.json`, enters play
mode, performs the five walks, then writes `Checks/results.txt` and leaves play mode. The whole run takes about
three minutes. Wait for `results.txt` to appear (its timestamp is in its first line), for example:

    unity status --until-ready --project-path .
    cat Checks/results.txt

Static checks alone (no play mode) can be run with
`unity command --timeout 120 eval -- --code "return IronCitadel.EditorTools.IronCitadelChecks.RunStatic();" --timeout 110000`.

## What is checked

Static (measured on the saved scene with physics queries and scene data, never asserted from the build plan):

- one room of each kind, unique names, valid states;
- a ceiling over every room's whole floor (up-rays from a 0.5 m grid), and the median ceiling height per room;
- prison, guard room, kitchen and vault low (<= 3.6 m); dining hall and throne room tall (>= 8 m); throne room tallest;
- every listed door sits between its two rooms, with the frame's clear width and height measured by sphere sweeps and an
  up-ray (the door's own leaf or plug is ignored for the width); shut doors have a leaf with a collider and a
  `DoorInteract`; the hidden door is solid until used;
- no unlisted opening: every shared wall between adjacent rooms is scanned with linecasts every 0.2 m at two heights, and
  each gap found must be within 2.6 m of a listed door between those rooms;
- the armory has exactly two ways in, the back door lies under the gallery deck, ranged guards stand within 7 m of the
  guarded entrance facing the entry hall, and the entrance is in clear sight from the start;
- a door-graph path from the entry hall to the dining hall avoiding the armory;
- the gallery: deck >= 4 m above the hall floor along one long side, a rail along the edge, a door-graph path from where
  the way round enters to the gallery that avoids the hall, and the fraction of hall floor points (1 m grid, 1 m up) in line
  of sight from three points at the rail (rail balusters are open, so the rail colliders are ignored for this measurement);
- the throne stands on a raised stage and the surface descends to the floor in steps/ramps of at most 0.3 m per 0.25 m;
- the vault has exactly one door, hidden, with a `SecretDoor`;
- counts of free-standing tall (>= 1.7 m) and low (0.8..1.7 m) occluders per room;
- markers: each stands in the room it claims; the dining hall holds the largest group; the gallery has ranged enemies;
  the warlord and guards hold the throne room; the entry hall holds no one; exactly two pickups in the armory and the vault;
- the player instance is the shipped prefab, unchanged controller numbers, standing in the entry hall.

Walks (play mode, the shipped player driven only through `StarterAssetsInputs.move/sprint`, its yaw, and `InteractOnE.Interact()`):

1. the way round from the start to the dining hall, never entering the armory;
2. from there up to the gallery without stepping onto the open hall floor;
3. back down and along the strip beneath the gallery into the armory's back door, to the best gear;
4. from the armory back round to the crypt, through the hidden door into the vault to the hoard, and back out;
5. from the crypt back to the dining hall, across the hall, through the library, study, shrine and antechamber to the throne.

A walk fails if the player makes no progress toward its waypoint for 3 s, runs out of time, breaks its rule, a door it
uses does not open, or it ends in the wrong room. If a walk fails the later ones are not run (the player may only walk).

Play-mode checks after the walks: the HUD names the room the player stands in and shows mm:ss; the throne was announced;
M shows an orthographic top-down camera covering the level with the Ceilings layer culled. Two screenshots are taken:
`Captures/overview_map_ingame.png` (the M map) and `Captures/hud_throne_announced.png`.

`results.txt` in this folder is the output of the last run on the delivered scene.
