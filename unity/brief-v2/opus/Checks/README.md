# Iron Citadel checks

Everything here re-runs on the saved scene `Assets/Scenes/IronCitadel.unity`. The folder is a local Unity
package (`com.ironcitadel.checks`, listed in `Packages/manifest.json` as `file:../Checks`), so Unity compiles
its code: `Editor/` (static checks, map export, the runner) and `Runtime/` (the play-mode walker).

## Run it

One entry point: the editor method `IronCitadel.Checks.CheckRunner.RunAll()`, also on the menu
**Tools > Iron Citadel > Run all checks**. With the project's editor open:

    sh Checks/run_checks.sh

which calls

    unity command --timeout 120 eval -- --code 'IronCitadel.Checks.CheckRunner.RunAll(); return 1;' --timeout 110000

and waits until `Checks/results.txt` ends with a new `END OF RUN <id>` line (about a minute and a half).
`RunAll` returns at once and then:

1. re-opens the scene from disk, so nothing unsaved is checked;
2. writes `Map/map.json` from it (every number measured from the built geometry or read from the objects in
   the scene: floor tiles and wall faces for room outlines, door openings and their clear widths with the
   leaves set aside, the gallery deck and rail, the stage, the throne, the markers);
3. runs the static checks S01-S19 (below) on it;
4. re-opens the scene again, enters play mode and runs the walks with the shipped player prefab;
5. leaves play mode and writes `Checks/results.txt` (scene size, save time and SHA-1 at the top; each check
   and walk as PASS/FAIL with what was measured; a summary).

## The static checks (Editor/StaticChecks.cs, Editor/SceneQuery.cs)

- S01 one room of each of the brief's 15 kinds, unique names, states as the brief says.
- S02 every wall gap is a listed door and every listed door a real gap; no room opens to the outside.
  Rays are cast across every room's walls every 0.1 m along and 0.25 m up, door leaves and the secret
  bookcase set aside; runs of gaps are clustered into openings and matched to the doors.
- S03 the armory has exactly two ways in, one from the entry hall and one from the dining hall beneath the
  gallery deck.
- S04 the guarded entrance is in sight from most of the entry hall (0.5 m grid of eye points) and from the start.
- S05 the ranged guards stand behind that entrance facing it, no armory defender (head, chest, shoulders) is
  in sight from any entry-hall eye point more than 0.8 m back from the opening, and each guard is in sight
  from the doorway itself.
- S06 the door graph has a way from the entry hall to the dining hall avoiding the armory.
- S07 the gallery deck is at least 4 m above the hall floor and runs along a whole side.
- S08 standing against the rail, more than half of the whole hall floor and 60% of the floor not under the
  gallery is in sight (0.5 m grid of floor points; anything solid, tables included, blocks).
- S09 the throne stands on the stage; the stage is at the far end; rays down the approach find steps
  (risers of at most 0.31 m) up to it, the invisible ramp helper over the step noses ignored.
- S10 the vault has one opening, marked hidden, blocked while the bookcase stands, hidden by a bookcase the
  same as three or more others in the library, no light beside it, and the vault is a dead end.
- S11 ceiling heights measured by rays up from the floor: prison, guard room, kitchen and vault low (< 3.6 m),
  dining hall and throne room tall (>= 6.5 m), the throne room taller than every other room.
- S12 every room has its ceiling over every 1 m floor sample.
- S13 open, shut and hidden openings all exist; shut doors have a SwingDoor and a solid collider.
- S14 in the armory, dining hall and throne room there are floor points hidden from a defender's eyes when
  standing, and points hidden only when crouching, by things in the room (the occluders are listed).
- S15 defenders and pickups are where the brief puts them; the dining hall holds the largest group.
- S16 each room holds the furnishings of its kind (counted from the prefabs placed in it).
- S17 the throne room has the fewest furnishings per square metre.
- S18 lighting: each room rendered from its middle (four views, markers and player hidden); the floor part of
  every view is above a minimum luminance, the dark rooms are darker than the dining hall and throne room,
  and those two are brighter than the median room.
- S19 the player is the unmodified `Assets/Interact/IronCitadelPlayer.prefab` in the entry hall; the HUD and
  the M overview camera are present.

## The walks (Runtime/WalkRunner.cs)

One continuous play session from the start, driving the player only through its own controls: move and
sprint input on `StarterAssetsInputs`, turning the player to face where it is going, and the E-key script's
`InteractOnE.Interact()` in front of shut doors and the bookcase. No jumping, no transform moves, no
unstick or retry: if the player makes less than 0.25 m of progress in 1.5 s, the walk fails. Routes come
from the baked NavMesh between waypoints read from the scene (door openings, markers, the throne); shut
doors and the bookcase carve the NavMesh, so a route never runs through one.

- W1 the way round: start to the dining hall, never inside the armory.
- W2 from the servery stair (where the way round enters the hall) up the stair to the gallery, never on the
  hall floor.
- W3 from the dining hall through the door beneath the gallery to the armory's best gear.
- W4 open the secret bookcase in the library, into the vault to the hoard, and back out.
- W5 from the dining hall through the library and shrine to the throne; the HUD must announce it.

Short connecting walks, driven the same way, take the player from the end of one walk to the start of the
next; they are reported too.

## Other scripts (not checks)

`Tools/BuildLevel.cs` builds and saves the scene from scratch; `Tools/Captures.cs` and
`Tools/OverviewSteps.cs` render `Captures/`. Each says how to run it in its header.
