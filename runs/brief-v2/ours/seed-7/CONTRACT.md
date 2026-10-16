# Iron Citadel v2 ("ours") — the files between the stages

The brief is `C:/Repos/Github/Crawfis-Software/IronCitadel/cold/brief-v2-draft.md`. The task is the "task prompt" section of
`C:/Repos/Github/Crawfis-Software/IronCitadel/cold/pcg-level-prompt-v2.md`. Read both before you start. This file fixes only what passes
between the stages, so that the design program, the picker and the Unity builder can be written in parallel.
`RUN` below is `C:/Repos/Github/Crawfis-Software/IronCitadel/v2/`.

## Pipeline

| # | Stage | Command | Reads | Writes |
|---|---|---|---|---|
| 1 | design | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Design -- design --seed <n> --out RUN` | brief, room index | `level.json`, `design.json`, `graph.svg` |
| 2 | pick | `python C:/Repos/Github/Crawfis-Software/IronCitadel/kit/pick/pick_rooms_v2.py RUN/level.json` | `level.json`, room index | `picks.json` |
| 3 | build | Unity, agent instance: `IronCitadelV2.Editor.IronCitadelV2Builder.Build("<RUN>")` | `level.json`, `picks.json`, `placements.json` if present | the scene, `floor.json`, `built.json` |
| 4 | tactics | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Tactics -- --run RUN` | `level.json`, `floor.json` | `placements.json`, `tactics.json` |
| 5 | build again | as 3; it now finds `placements.json` and places enemies and pickups | | the scene, `floor.json`, `built.json` |
| 6 | fold | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Design -- report --out RUN` | everything above | `design.json`, with picks, tactics and the as-built checks folded in |

`<PCGTools>` is `C:/Repos/Github/Crawfis-Software/PCGTools`. The room index is
`C:/Repos/Github/Crawfis-Software/MacroTileBuilder/Assets/CrawfisSoftware/AllSyntyDungeons/Rooms/rooms-index.json`.
The scene is `Assets/CrawfisSoftware/AllSyntyDungeons/Levels/IronCitadelV2.unity`.

Every stage can run again and overwrites only its own outputs. A stage that finds an input missing says so and
stops; it never invents the input. JSON is UTF-8 with LF line endings and 2-space indents. Unknown keys are ignored
on read, so a stage may add keys for its own use.

## Conventions (every file)

- Units are metres and degrees.
- **Tiles.** Every room is one 45 m tile (`k` = 9 in the index). Cell `[r, c]`: row 0 is the north row, column 0
  the west column. Cell `[r, c]` spans x from 45c to 45c + 45 and z from −45r − 45 to −45r. Its centre is
  (45c + 22.5, 0, −45r − 22.5). Every room's floor is at y = 0.
- **Sides.** `"N"` is +z, `"E"` is +x, `"S"` is −z and `"W"` is −x. A door sits at the middle of the shared tile
  edge (the index's `crossing`, ±22.5 m from the tile centre at k = 9).
- **Turns.** A room's `rotation` is 0, 90, 180 or 270 degrees about +Y. A turn of +90 carries the prefab's canonical
  N side onto world E (N→E→S→W). A prefab's pivot is its tile centre, so place it at the cell centre with
  `Quaternion.Euler(0, rotation, 0)`.
- **Facing.** `yaw` is in degrees about +Y; 0 faces +z (north) and 90 faces +x (east).
- **Room ids.** These are snake_case. The fifteen kinds use the ids in the table below. Rooms the design adds (a
  passage, an antechamber) are `passage_1`, `antechamber_1` and so on.

### The kinds

The letters are v1's, so the v1 conformance kit's letters mean the same thing.

| Letter | Room id | Theme in the index | Height class | States the picker may use |
|---|---|---|---|---|
| E | `entry_hall` | `barracks` or `common_room`, at standard height (the brief asks for it; `guard_room`'s lobby is low) | standard | barracks `deserted`, `tossed`; common_room `derelict`, `brawl` |
| A | `armory` | `armory` | standard | `stocked` |
| G | `guard_room` | `guard_room` | low | `deserted`, `brawl` |
| P | `prison` | `prison` | low | `forgotten`, `riot` |
| K | `kitchen` | `kitchen`, `notch` family (the only low one) | low | `spoiled`, `raided` |
| L | `library` | `library` (all tall) | tall | `ruined`, `pillaged` |
| S | `alchemists_study` | `alchemy_lab` or `study` (standard families) | standard | alchemy_lab `derelict`, `ransacked`; study `dusty`, `searched` |
| F | `forge` | `forge` (all tall) | tall | `cold`, `stripped` |
| V | `treasure_vault` | `treasury` | low | `hoard` |
| D | `dining_hall` | `hall_gallery`, `back` family | tall | `ruined`, `aftermath` |
| T | `throne_room` | `throne_room`, `aisled` family (it has the stage) | tall | `fallen`, `sacked` |
| C | `crypt` | `crypt` | low | `collapsed`, `desecrated` |
| R | `storeroom` | `storeroom` | standard | `rotting`, `plundered` |
| H | `shrine` | `shrine` | tall | `ruined`, `desecrated` |
| X | `torture_chamber` | `torture_chamber` | low | `forgotten`, `purged` |

The heights in the index are as follows. Every tall theme has a 15 m lid, so nothing is taller than the throne room.
Standard rooms have 10 m lids. The low ones have lids of 3.6 m or 5 m.

### Facts about the hall and the throne room that the layout must respect

- **`hall_gallery` / `back` at rotation 0:**
  - Its gallery runs along the canonical S wall, high above the floor.
  - Its canonical S door opens in the walkway beneath the gallery. That door is the flank door into the armory.
  - The stairs up to the gallery start at the landings of the canonical E and W doors. The way round must come in
    at one of those doors, so the player can climb without crossing the floor.
  - Its canonical N door is across the floor from the gallery.
  - So on the grid, the armory and the way-round room sit on *perpendicular* sides of the hall.
  - The door sets are N, NE, NS, NES and NESW. NES has S (the armory), E (the way round) and N (across the floor).
- **`throne_room` / `aisled`:** the throne stands on a stage at the far end from a single door. Keep the throne room a
  dead end with one door; door set N.

## `level.json` — written by the design program; read by the picker and the builder

This is "the level file the build reads".

```json
{
  "name": "iron-citadel-v2",
  "version": 2,
  "seed": 7,
  "tile_m": 45,
  "k": 9,
  "grid": { "rows": 3, "cols": 6 },
  "rooms": [
    {
      "id": "entry_hall", "kind": "E", "name": "Entry Hall", "cell": [0, 0],
      "themes": ["barracks", "common_room"], "family_hint": ["mess"], "height": "standard",
      "states": ["deserted", "tossed"], "wing": "gate", "role": "start",
      "note": "why this room is here, one line"
    }
  ],
  "doors": [
    {
      "id": "d_entry_hall__armory", "a": "entry_hall", "b": "armory", "side_of_a": "E",
      "kind": "open", "roles": ["choke", "big_loop"]
    }
  ],
  "big_loop": ["entry_hall", "armory", "dining_hall", "kitchen", "storeroom", "forge"],
  "routes": {
    "head_on":   ["entry_hall", "armory", "dining_hall"],
    "way_round": ["entry_hall", "forge", "storeroom", "kitchen", "dining_hall"],
    "flank":     ["dining_hall", "armory"],
    "to_throne": ["dining_hall", "library", "shrine", "throne_room"],
    "vault":     ["storeroom", "treasure_vault"]
  },
  "gallery": { "room": "dining_hall", "toward": "armory", "way_in_from": "kitchen" },
  "start": { "room": "entry_hall", "looks_at": "armory" },
  "goal": { "room": "throne_room" },
  "enemies": [
    { "room": "armory", "kind": "ranged", "count": 3, "post": "choke", "watches": "entry_hall", "source": "brief" }
  ],
  "pickups": [
    { "id": "best_gear", "room": "armory", "what": "the armory's best gear" },
    { "id": "hoard", "room": "treasure_vault", "what": "the vault's hoard" }
  ]
}
```

- **`rooms`:**
  - `themes` lists themes in order of preference. `family_hint` is optional and ordered.
  - `height` is `low`, `standard` or `tall`.
  - `states` is the allowed list; the picker varies the states across rooms.
- **`doors`:**
  - `a` and `b` are room ids in cells that share an edge. `side_of_a` is the world side of `a` that the door is on.
  - `kind` is one of three:
    - `open`: a doorway with no leaf.
    - `shut`: a door leaf that opens with E.
    - `hidden`: a door that does not read as a door and opens with E. The vault's.
  - `roles` is a list of tags: `choke`, `flank`, `way_round`, `big_loop`, `to_throne`, `vault` and `branch`.
- **Gaps.** A tile edge between two rooms with no door entry is a wall. A tile edge on the level's border is a wall.
- **`enemies`:**
  - `kind` is one of `ranged`, `melee`, `boss` or `guard`. Guards are the warlord's melee bodyguard.
  - `post` names what the group holds: `choke`, `gallery`, `floor`, `fill`, `throne` or `room`.
  - `watches` is a room id or a door id.
  - `source` says where the count came from: `brief` or `director`.
  - The positions are not set here; `placements.json` gives them.

## `picks.json` — written by the picker; read by the builder

```json
{
  "level": "iron-citadel-v2",
  "picks": [
    {
      "room": "entry_hall", "kind": "E", "cell": [0, 0], "needs": ["E", "S"],
      "theme": "barracks", "family": "mess", "state": "deserted", "state_kind": "abandoned",
      "ceiling": "std", "lid_m": 10, "door_set": "NE", "rotation": 90,
      "prefab": "Assets/CrawfisSoftware/AllSyntyDungeons/Rooms/Barracks/....prefab", "guid": "…",
      "walk_ok": true, "audit_total": 0, "why": "one line"
    }
  ]
}
```

- `needs` is the world sides that carry the room's doors.
- `door_set` is the prefab's canonical set. Turned by `rotation`, the set must equal `needs` exactly, so a room
  never has an opening that the design lacks.
- `prefab` is the full asset path as the index gives it.

## `floor.json` — written by the builder after the NavMesh bake; read by the tactics program

This is a 2.5D sample of the built level's walkable floor, taken on a regular XZ grid of columns.

```json
{
  "version": 1,
  "level": "iron-citadel-v2",
  "cell_m": 1.0,
  "origin": [0.0, -135.0],
  "nx": 270, "nz": 135,
  "agent": { "radius": 0.35, "height": 1.8, "climb": 0.3, "slope": 45 },
  "samples": [
    { "id": 0, "i": 12, "j": 40, "y": 0.0, "room": "entry_hall", "reach": true, "clear": 9.6 }
  ],
  "links": [[0, 1]],
  "blocks": [
    { "i": 13, "j": 40, "room": "entry_hall", "top": 0.9 }
  ],
  "doors": [
    { "door": "d_entry_hall__armory", "samples": [101, 102, 103] }
  ]
}
```

- **Columns.** Column `(i, j)` has its centre at (origin.x + (i + 0.5)·cell_m, origin.z + (j + 0.5)·cell_m). `i` runs
  along +x and `j` along +z.
- **`samples`:**
  - Each sample is one walkable surface in a column: a point on the baked NavMesh.
  - A column may hold more than one sample. The hall's gallery stands over the walkway beneath it, so those columns
    carry both.
  - `y` is the surface height. `clear` is the headroom above it, up to the ceiling.
  - `reach` is true when the sample connects to the player's start on the NavMesh with every door open.
- **`links`:** walkable moves between samples in 4-neighbouring columns. A link exists when the NavMesh straight path
  between the two is clear. Links are undirected, with the lower id first.
- **`blocks`:**
  - These are columns inside a room's tile that have no walkable sample at floor level.
  - `top` is the obstacle's height above that floor. A wall, a pillar or anything that reaches the ceiling gets
    `top` = the room's lid.
  - The tactics program draws the line between low cover (crouch) and tall cover (stand) and reports where.
- **`doors`:** for each design door, the samples within 1.5 m of the door's crossing, taken with the door open.

## `placements.json` — written by the tactics program; read by the builder

```json
{
  "level": "iron-citadel-v2",
  "enemies": [
    {
      "id": "armory_ranged_1", "room": "armory", "kind": "ranged", "post": "choke",
      "pos": [67.0, 0.0, -21.0], "yaw": 270, "watches": "d_entry_hall__armory", "watch_point": [45.0, 1.5, -22.5],
      "why": "HeldGrounds.AtGate: held cell 1234, cover 0.9 m"
    }
  ],
  "pickups": [ { "id": "best_gear", "room": "armory", "pos": [80.0, 0.0, -30.0] } ],
  "player_start": { "pos": [20.0, 0.0, -22.5], "yaw": 90 }
}
```

- `pos` is a floor point: a sample's x, y and z.
- `yaw` faces `watch_point`.

## `tactics.json` — written by the tactics program

It holds the four checks from the task, each with `pass` (true or false), the numbers behind it and a line saying
how it was measured:
- the armory's guards hold its entrance;
- the flank comes in behind them;
- the hall floor is exposed to the gallery, and the climb to the gallery is not;
- the vault is out of sight.

It also says where low cover ends and tall cover begins, and which PCGTools type made each decision.

## `built.json` — written by the builder; the room graph as built

```json
{
  "level": "iron-citadel-v2",
  "scene": "Assets/CrawfisSoftware/AllSyntyDungeons/Levels/IronCitadelV2.unity",
  "built_at": "2026-10-13T17:00:00-04:00",
  "rooms": [ { "id": "entry_hall", "cell": [0, 0], "prefab": "…", "rotation": 90, "navmesh_m2": 1200.5 } ],
  "doors": [
    { "a": "entry_hall", "b": "armory", "side_of_a": "E", "kind": "open", "walkable": true, "how": "NavMesh path across the edge, doors open" }
  ],
  "extra_openings": [],
  "enemies": [ { "id": "armory_ranged_1", "on_navmesh": true, "pos": [67.0, 0.0, -21.0] } ],
  "checks": { "doors_match_design": true, "big_loop_intact": true, "vault_dead_end": true, "notes": [] }
}
```

The builder finds the doors from the scene, not from `level.json`. For every pair of neighbouring tiles it asks the
baked NavMesh, with every door open, whether the shared edge can be crossed. Any crossing on an edge the design has
no door for goes in `extra_openings`.
