# Fable cold run: score and findings

- Build: `C:/Repos/ColdRuns/fable`, scene `Assets/Scenes/IronCitadel.unity`, saved 2026-10-07 17:17:56.
  - The run took 1:28:48 wall clock: 193 tool calls, 197 turns, $45.41 at list price.
  - The audit found 0 tool calls that touched our paths.
- Scored on a copy, `C:/Repos/ColdRuns/fable-score`, because Fable's editor was still open. The scorer was the
  same conformance kit and `level.json` as for Opus and ours.
- **Result: 8 pass, 2 fail, 1 manual** (`conformance.md`).

## The two fails are real faults in the build, not the kit

The probes are in `logs/fable-diag/` (`probe.txt`, `probe2.txt`, and the map crops). The kit was not changed.

- **Check 4, east wing.** The forge room's west doorway, from its own passage (x 145, z -65..-60), is blocked.
  - The blocker is an ore pile, `Props/forge/SM_Env_Ore_Pile_01`, placed 0.8 m up and turned 60 degrees by
    `Dressing.Forge`. Its collider spans the inside of the door at y 0.69-1.58, leaving 0.59 m of headroom.
  - A second pile covers the south side. There is no NavMesh through the door.
  - A CharacterController gets in on only two of five lines, by riding up onto the pile and ending 1.4 m up.
  - Fable's own play test walked the west wing only.
- **Check 10, gallery stairs.** At each stair top, the shaft's end wall (`Structure/great_hall/SM_Env_Wall_01`,
  top at 5.151) stands 0.24 m above where the ramp meets it. That leaves about 0.37 m from the capsule's
  bottom, more than the 0.3 m step.
  - A copy of the Starter Assets controller, holding forward, sticks there at 30, 60 and 120 fps.
  - Fable's play-test driver got over only with its unstick sidestep: after 0.6 s stalled it strafes for
    0.7 s. Its log shows 4.3 s for the climb.

## A kit weakness found on the way (no verdict changes)

The WALK check's east-wing route "passed" by detouring about 460 m through the west wing. A generic tightening
would fail a wing walk whose planned path leaves the route's cells.
- Fable already fails check 10, so its score does not change.
- Opus and ours pass check 4, so a path inside each wing exists. Whether their planned walks stay in their own
  cells has not been verified.
