You are taking part in an experiment: "how good a level can a frontier model build in Unity from a
designer's brief and a data file, using a commercial art pack but no level-generation framework."

THE SETUP. This folder is a Unity 6 project (URP) with three Synty POLYGON dungeon packs already
imported (Dungeons, Dungeon Realms, Fantasy Dungeon Map), the Input System, AI Navigation, and Unity's
Starter Assets first-person controller, ready to place as `Assets/Interact/IronCitadelPlayer.prefab`.
Its E key casts a 3 m ray from the camera and calls `SendMessageUpwards("Interact")` on the first solid
(non-trigger) collider it hits, so anything the player opens, such as a secret door, needs a collider
and a `void Interact()` method on that object or a parent. The `unity` command-line tool drives the
editor, and the Unity Pipeline package is installed: `unity open` starts the editor, `unity command`
runs editor commands on it (with no arguments it lists them: running C#, the scene hierarchy, game- and
scene-view captures, play mode, the console), and `unity run --command` runs one headless.
`unity skill show` prints Unity's guide to the CLI.

UNITY CLI PITFALLS, found the hard way on this machine (CLI 1.0.0-beta.8 to beta.11):
- `unity run` runs the editor in batch mode and reserves `-quit`, `-batchmode` and `-projectPath`.
  Passing any of them exits 6 and launches nothing, although `unity run --help`'s own examples pass
  `-quit`. Editor flags go after `--`: `unity run . -- -executeMethod My.Tool.Run -logFile run.log`.
- Exit code 6 covers many failures: project not found, a reserved flag, bad parameters, a failed
  command, or `unity status` with no editor running. Treat any non-zero exit as a failure. Judge
  success by the files your method writes and by the log's tail, not by the exit code alone; the
  log's `ExitCode:` line only says whether your method returned without throwing.
- One editor per project. A batch run needs the project's lock, so it fails while an editor has the
  project open. Close the editor first (`unity close`, which does not save) or use `unity command`.
- `--timeout` on `unity run` or `unity test` kills the editor and can leave `Temp/UnityLockfile`
  behind; the next boot then fails with exit 6 and no log. Delete that file between boots.
- A live `unity command` gives up after 30 seconds whatever `--timeout` says, and commands that need
  the main thread time out while the editor is busy importing. Run long work with `--detach` and
  poll it with `unity job`; `unity status` answers even when the editor is busy.
- On a live editor the options come before the command name and its arguments follow it with no
  `--` (`unity command --project-path . <name> <args>`); a `--` there silently adds an empty
  parameter. Headless, it is `unity run . --command <name> -- <args>`.
- `unity status` with no editor running exits 6 and says "Open Unity and install the Pipeline
  package". It means nothing is running; the package is installed.
- For tests use `unity test`, never `-runTests` with `-quit`: the editor quits before the test
  runner and writes no results.
- `[Licensing::Client] HandshakeResponse … 505` in the editor log is noise.

WHAT YOU MAY DO. Anything a developer would do inside this project: read and write its files, write
editor scripts, run the editor in batch mode or open it, enter play mode, take screenshots and look at
them, search the web for documentation, install packages from the Unity registry, and iterate as much as
you like. There is no time limit; stop when you judge the level done.

WHAT YOU MAY NOT DO. Read anything outside this project folder. Use art other than what is already in
the project (no downloaded models or textures; primitives are fine for markers and invisible helpers).
Ask questions: make a call and note it in your report.

HOW THIS RUNS. You are running unattended: nobody will answer, approve or read anything until you
finish, and the session ends as soon as you end your reply. Do the whole job in this one reply. When you
start something long (an editor boot, an import, a bake), wait for it to finish rather than ending your
reply to wait.

DELIVER:
1. `Assets/Scenes/IronCitadel.unity`: the whole level at true scale (4 × 4 tiles of 45 m, 180 m a side,
   1 unit = 1 metre), every room, door, feature and route in the data file, ceilings on, lit for walking.
2. The first-person controller at the start, facing north. In play mode: walk, sprint, climb the stairs,
   M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names the room
   the player is in and shows elapsed time, and reaching the throne is announced.
3. `Captures/`: one top-down overview of the whole level and one eye-level shot of each room.
4. A report (below).

QUALITY BAR. It should read as a place, not a diagram. Each room should read as its kind from its shape,
height, light and furnishing, as the brief describes. Satisfy every item in `must_be_true`; where you
cannot, say so. Do not invent extra doors or openings. Defenders and pickups are markers.

THE DESIGNER'S BRIEF: 

# The Iron Citadel Infiltration — level brief

**Premise.** A keep cut into the rock, on the night of a feast. The player comes in by the south door with
one job: reach the warlord in the throne room. The straight way north runs through the armory, and the
armory's gate is down. The long way round is the point of the level.

**Where you are.** One storey of rooms inside the rock: eleven rooms on a four-by-four grid of 45-metre
square tiles, 180 m on a side. The five empty cells are solid rock, and nothing is outdoors. Each room
fills its own tile. Doors sit at the middle of a tile's edge and meet the neighbour's door exactly. A room
is entered through a short hall or antechamber that turns, so you rarely see straight through a room from
the door you came in by. Every room has a ceiling.

**The route, in three acts.**

*Act one is the problem.* The entry hall is a guard lobby with four doors: the south door you came in by,
a door west, a door east, and ahead, to the north, the armory's gate. The gate is a lowered portcullis
behind a row of spikes, with archers behind it. Nobody gets through it. The entry hall is where the player sees that.

*Act two is the way round, two ways.* West is the cell block. A guard room on night watch leads to the
prison, which is mid-riot: low, dark, rows of cells, some doors forced and some still shut. At the
prison's west end, the warden's corner has shelves against the wall, and one of its bookcases swings aside
onto a small treasure vault. The vault lies on no route, and nothing points to it. Past the prison, a kitchen cooking for the feast opens onto the great hall. East is the
scholars' wing. A library of tall bookcases leads to an alchemist's study with its brewing, then to a
working forge, which opens onto the great hall from the other side. The forge has a hearth under a hood
whose flue climbs to the ceiling, with anvils, quench barrels and tools round it. Either wing works, and
neither is shorter.

*Act three is the great hall and the turn back.* The great hall is the level's centrepiece. It is tall,
with long tables set for a feast, chandeliers hanging from the ceiling and banners on the walls. Along its
south side runs a minstrels' gallery about 5 m up, and its two stairs come from behind. At the west and
east ends of the hall, each stair climbs inside a walled shaft from a landing that the passage from that
side's door reaches, and the landing opens into the hall by an arch. So a player who comes in from the
kitchen or the forge can go up to the gallery without crossing the hall floor, and from the gallery rail
sees the whole hall floor. Under the gallery, behind an arcade, a walkway runs the width of the hall, and
the door south into the armory opens off it. A player who goes under the gallery comes into the armory
from behind, behind the archers who are watching the gate, and takes the armory's best gear. Then they go
north across the hall to the throne room, which is tall, bright, sparse and grand. A runner leads to the
throne. It stands on a stage at the far end, with a full-width row of steps 1.25 m high in front of it.
The way in turns, so the throne is not seen from the door.

**The rooms, as the generator should see them.** Each room should read as its kind from its shape, its
height, its light and what fills it, not from a label.

- *Entry hall.* A manned guard lobby: benches, weapon stands, a table. Standard height.
- *Guard room.* A guard post at night. Low and dim.
- *Prison.* Low and dark. Cells with barred doors in rows; in the riot, doors are forced and bedding and
  furniture overturned. The warden's corner at the west end has shelves and bookcases.
- *Treasure vault.* Small, low, sealed. A hoard of chests and coffers.
- *Kitchen.* A small service room with a lower ceiling than the hall: a hearth, worktables, barrels and
  sacks, cooking under way.
- *Great hall.* As above. Tall and bright, its tables dense.
- *Armory.* Dense rows of weapon racks and armour stands, fully stocked. The gate is on its south side.
- *Throne room.* As above. About 15 m tall, the tallest room in the level and the sparsest.
- *Library.* Tall bookcases packed close, with reading tables. Open, in use.
- *Alchemist's study.* Benches of glassware, shelves of jars, a brew going.
- *Forge.* As above. Working, and lit by its fire.

**What must be true.**

- The eleven rooms stand exactly where the data file puts them, on 45 m tiles; every other cell is rock.
- Every door in the data file exists and meets its neighbour's door. There is no other opening between
  tiles.
- The portcullis, with spikes in front of it, cannot be passed: the armory is entered only from the great
  hall.
- Two routes lead from the entry hall to the great hall, west and east, and neither passes through the
  armory.
- The treasure vault has one way in: a bookcase in the prison's west wall that swings aside. Closed, it
  looks like the bookcases beside it. The vault lies on no route.
- The throne room has one door, from the great hall.
- The gallery runs along the hall's south side about 5 m up, with a stair at each end, west and east. Each
  stair climbs from a landing that the hall's west or east door reaches without crossing the hall floor.
  The door to the armory opens under the gallery, and the whole hall floor can be seen from its rail.
- The throne stands on a stage 1.25 m up, with steps across its whole front.
- The prison, the guard room, the kitchen and the vault are low; the great hall and the throne room are
  tall.

**What I don't care about.** Which layout each room uses, as long as it reads as its kind. Exact counts of
tables, racks and bookcases. The colour of the banners. The seed.

**Numbers.** Defenders and pickups are markers; nothing moves. Where they stand is in the data file: the
largest group in the great hall, three archers at the portcullis, the warlord and four guards in the
throne room. There are two pickups, the armory's gear and the vault's hoard.


THE DATA FILE (level.json): 

```json
{
  "name": "The Iron Citadel Infiltration",
  "version": "2026-10-07",
  "units": {
    "length": "metres",
    "grid": "cells are [row, col]; row 0 is the north edge, col 0 is the west edge; +x is east, +z is north, +y is up",
    "origin": "the north-west corner of cell [0, 0] is at world (0, 0, 0), so cell [r, c] spans x from 45c to 45(c+1) and z from -45(r+1) to -45r; the ground floor is at y = 0"
  },
  "world": {
    "tile_m": 45,
    "rows": 4,
    "cols": 4,
    "size_m": 180,
    "storeys": 1,
    "time_of_day": "night, during a feast in the great hall",
    "outdoors": "none; every # cell is solid rock",
    "ceilings": "every room has one; a top-down overview hides them"
  },
  "kinds": {
    "E": "entry hall: a guard lobby",
    "G": "guard room",
    "P": "prison: a cell block",
    "V": "treasure vault",
    "K": "kitchen",
    "D": "great hall: a feasting hall with a minstrels' gallery",
    "A": "armory",
    "T": "throne room",
    "L": "library",
    "S": "alchemist's study",
    "F": "forge",
    "#": "solid rock"
  },
  "grid": [
    "##T#",
    "#KDF",
    "VPAS",
    "#GEL"
  ],
  "rooms": [
    { "id": "entry_hall",  "kind": "E", "cell": [3, 2], "state": "manned",                "height": "standard", "light": "moderate", "fill": "moderate" },
    { "id": "guard_room",  "kind": "G", "cell": [3, 1], "state": "night watch",           "height": "low",      "light": "dim",      "fill": "moderate" },
    { "id": "prison",      "kind": "P", "cell": [2, 1], "state": "riot",                  "height": "low",      "light": "dark",     "fill": "moderate" },
    { "id": "vault",       "kind": "V", "cell": [2, 0], "state": "hoard, sealed",         "height": "low",      "light": "dim",      "fill": "moderate" },
    { "id": "kitchen",     "kind": "K", "cell": [1, 1], "state": "cooking for the feast", "height": "low",      "light": "firelit",  "fill": "dense" },
    { "id": "great_hall",  "kind": "D", "cell": [1, 2], "state": "feast",                 "height": "tall",     "light": "bright",   "fill": "dense tables" },
    { "id": "armory",      "kind": "A", "cell": [2, 2], "state": "stocked",               "height": "standard", "light": "moderate", "fill": "dense" },
    { "id": "throne_room", "kind": "T", "cell": [0, 2], "state": "court",                 "height": "tall, about 15 m", "light": "bright", "fill": "sparse" },
    { "id": "library",     "kind": "L", "cell": [3, 3], "state": "open, in use",          "height": "standard", "light": "moderate", "fill": "dense" },
    { "id": "study",       "kind": "S", "cell": [2, 3], "state": "brewing",               "height": "standard", "light": "moderate", "fill": "dense" },
    { "id": "forge",       "kind": "F", "cell": [1, 3], "state": "working",               "height": "standard", "light": "firelit",  "fill": "moderate" }
  ],
  "doors": [
    { "id": "outer_door",  "between": [[3, 2], "outside, south"], "kind": "door",       "passable": true,  "note": "the way in; the player starts here" },
    { "id": "west_door",   "between": [[3, 2], [3, 1]],           "kind": "open",       "passable": true },
    { "id": "east_door",   "between": [[3, 2], [3, 3]],           "kind": "open",       "passable": true },
    { "id": "armory_gate", "between": [[3, 2], [2, 2]],           "kind": "portcullis", "passable": false, "note": "lowered, with a row of spikes in front of it on the entry side; three archers stand behind it facing south" },
    { "id": "cell_block",  "between": [[3, 1], [2, 1]],           "kind": "open",       "passable": true },
    { "id": "vault_door",  "between": [[2, 1], [2, 0]],           "kind": "secret door", "passable": true,  "note": "a bookcase in the warden's corner at the prison's west end that swings aside; closed, it looks like the bookcases beside it" },
    { "id": "prison_kitchen", "between": [[2, 1], [1, 1]],        "kind": "open",       "passable": true },
    { "id": "kitchen_hall",   "between": [[1, 1], [1, 2]],        "kind": "open",       "passable": true },
    { "id": "library_study",  "between": [[3, 3], [2, 3]],        "kind": "open",       "passable": true },
    { "id": "study_forge",    "between": [[2, 3], [1, 3]],        "kind": "open",       "passable": true },
    { "id": "forge_hall",     "between": [[1, 3], [1, 2]],        "kind": "open",       "passable": true },
    { "id": "under_gallery",  "between": [[1, 2], [2, 2]],        "kind": "open",       "passable": true, "note": "opens onto the walkway under the minstrels' gallery" },
    { "id": "throne_doors",   "between": [[1, 2], [0, 2]],        "kind": "great doors, open", "passable": true }
  ],
  "features": [
    { "id": "gallery",   "room": "great_hall",  "side": "south", "deck_m": 5,
      "note": "a minstrels' gallery whose stairs come from behind: at each end, west and east, a stair climbs a walled shaft from a landing that the hall's west or east door reaches, and the landing opens into the hall by an arch; under the gallery, behind an arcade, a walkway the hall's width with the armory door off it; the whole hall floor is visible from its rail" },
    { "id": "stage",     "room": "throne_room", "side": "north, the far end from the door", "rise_m": 1.25,
      "note": "a full-width row of steps and no rail; the throne faces the door down a runner; the way in turns, so the throne is not seen from the door" },
    { "id": "portcullis", "door": "armory_gate", "note": "down; impassable; a row of spikes in front of it" },
    { "id": "bookcase_door", "door": "vault_door", "note": "the vault's only way in; it swings aside when the player uses it" }
  ],
  "routes": [
    { "id": "front",     "cells": [[3, 2], [2, 2]], "blocked": true, "note": "the obvious way, closed by the portcullis" },
    { "id": "west_wing", "cells": [[3, 2], [3, 1], [2, 1], [1, 1], [1, 2]] },
    { "id": "east_wing", "cells": [[3, 2], [3, 3], [2, 3], [1, 3], [1, 2]] },
    { "id": "flank",     "cells": [[1, 2], [2, 2], [1, 2], [0, 2]], "note": "in from the kitchen or the forge, up the stair to the gallery to look over the hall, back down, under the gallery into the armory from behind, take the gear, then north to the throne" }
  ],
  "start": { "cell": [3, 2], "at": "outer_door", "facing": "north" },
  "goal":  { "cell": [0, 2], "at": "the throne" },
  "defenders": [
    { "room": "armory",      "count": 5, "note": "3 archers at the portcullis facing south; 2 among the racks" },
    { "room": "great_hall",  "count": 8, "note": "at the tables; the level's largest group" },
    { "room": "throne_room", "count": 5, "note": "the warlord on the stage and 4 guards before it" },
    { "room": "prison",      "count": 6, "note": "rioting prisoners" },
    { "room": "guard_room",  "count": 2 },
    { "room": "kitchen",     "count": 2, "note": "cooks" },
    { "room": "forge",       "count": 2, "note": "smiths" },
    { "room": "study",       "count": 1 }
  ],
  "pickups": [
    { "room": "armory", "note": "the armory's best gear, the level's upgrade" },
    { "room": "vault",  "note": "the hoard" }
  ],
  "markers": "defenders and pickups are static markers; nothing moves and there is no combat",
  "must_be_true": [
    "the eleven rooms stand on the cells the grid gives them, on 45 m tiles; every # cell is solid rock",
    "every door listed exists and meets its neighbour's; there is no other opening between tiles",
    "the armory_gate, with spikes in front of it, cannot be passed; the armory is entered only from the great hall",
    "west_wing and east_wing both join the entry hall to the great hall, and neither passes through the armory",
    "the vault has exactly one way in, the bookcase door from the prison; closed, it looks like the bookcases beside it; the vault lies on no route",
    "the throne room has exactly one door, from the great hall",
    "the gallery runs along the great hall's south side about 5 m up, with a stair at each end, west and east; each stair climbs from a landing that the hall's west or east door reaches without crossing the hall floor; under_gallery opens beneath it; the whole hall floor is visible from its rail",
    "the throne stands on a stage 1.25 m up with steps across its whole front",
    "the prison, guard room, kitchen and vault are low; the great hall and throne room are tall"
  ],
  "dont_care": [
    "which layout each room uses, as long as it reads as its kind",
    "exact counts of tables, racks and bookcases",
    "the colour of the banners",
    "the seed"
  ]
}
```


YOUR REPORT (plain text, under 300 words): (a) what you built and how the level is put together; (b)
which `must_be_true` items you believe are satisfied, which are not, and why; (c) known weaknesses; (d)
what you did to check your work (play mode, screenshots, scripts); (e) time spent.
