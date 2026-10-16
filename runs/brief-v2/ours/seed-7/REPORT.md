# Iron Citadel v2 ("ours"): report

The brief is `C:/Repos/Github/Crawfis-Software/IronCitadel/cold/brief-v2-draft.md` and the task is `cold/pcg-level-prompt-v2.md`. PCGTools
designed the level, the themed-room library built it, and the built scene was checked by measurement and by walking
it in play mode. Nothing is committed. Clock times are EDT on 2026-10-13; `RUNLOG.md` has the full log.

**Result:**
- The design passes 27 of 27 checks. The as-built graph matches the design.
- All 4 tactical checks pass. All 6 walks pass on the built level.
- Every "what must be true" item holds. From the entry hall the choke guards come into view only at the doorway,
  within 3.6 m of it, which is what you want; the tactics rule was flipped after the run to check exactly that (see
  "After the run").
- Wall clock: 15:45:32 to 17:40, about 1 h 55 min.
- Re-running the whole pipeline on a new seed takes about 20 s, plus a 4 min walk. This was measured on four more
  seeds after the run, which also found and fixed one bug (see "After the run").

## What was built

A 3 × 6 grid of 45 m tiles (k = 9) with three cells empty, one room per kind. Row 0 is north.

```
E A D L H T        E entry hall   A armory     D dining hall   L library        H shrine   T throne room
R F K S C .        R storeroom    F forge      K kitchen       S alch. study    C crypt
V . G P X .        V treasure vault            G guard room    P prison         X torture chamber
```

**Routes:**
- **Head on:** entry hall → armory → dining hall. The choke is an open doorway.
  - 3 ranged enemies stand behind it, facing it. 4 melee fill the armory.
- **Big loop:** entry hall → armory → dining hall → kitchen → forge → storeroom → entry hall.
- **Way round:** entry hall → storeroom → forge → kitchen → dining hall.
  - It leaves the entry hall by a shut door.
  - The forge and kitchen joins are open doorways.
  - It comes into the hall by the gallery stairs.
- **Vault:** a hidden door behind a bookcase off the storeroom, with two decoy bookcases. It is a dead end.
- **Gallery:**
  - It runs along the hall's armory side, 5.0 m above the floor. 4 ranged enemies hold it and 6 melee hold the floor.
  - The flank door into the armory is a shut door in the walkway beneath it.
- **To the throne:** hall → shut door → library → shrine → shut door → throne room.
  - The throne stands at the far end on a 1.3 m stage, with steps at 0.6 m and 1.1 m.
  - The warlord stands on the stage and 3 guards stand in front of it.
- **Side wings, each behind a shut door:**
  - the cells (guard room → prison → torture chamber), off the kitchen;
  - the alchemist's study, off the library;
  - the crypt, off the shrine.
- **Doors:** 15 in all: 6 open doorways, 8 shut doors that open with E, and 1 hidden.
- **Enemies:** 33. Of these, 21 are placed from the brief and 12 are the Director's: one or two in each of eight other
  rooms. The storeroom and the forge, early on the way round, hold no one, and nor do the entry hall and the vault.
- **Pickups:**
  - The best gear is in the armory, a 40 m walk from the flank door and out of the choke guards' sight.
  - The hoard is at the vault's far end, a 56 m walk from its door.

**The rooms** (from `picks.json`; each lid is its ceiling height in metres):

| Room | Prefab family and state | Turn | Lid |
|---|---|---|---|
| entry hall | barracks mess, tossed | 90 | 10 |
| armory | armory arsenal, stocked | 90 | 10 |
| dining hall | hall_gallery back, ruined | 90 | 15 |
| library | library aisled, pillaged | 90 | 15 |
| shrine | shrine chapel, ruined | 90 | 15 |
| throne room | throne_room aisled, sacked | 270 | 15 |
| storeroom | storeroom stores, rotting | 0 | 10 |
| forge | forge cellar, cold | 90 | 15 |
| kitchen | kitchen notch, raided | 180 | 5 |
| alchemist's study | alchemy_lab box, derelict | 0 | 10 |
| crypt | crypt tombs, collapsed | 0 | 5 |
| treasure vault | treasury box, hoard | 0 | 3.6 |
| guard room | guard_room lobby, deserted | 0 | 5 |
| prison | prison block, riot | 90 | 3.6 |
| torture chamber | torture_chamber posts, forgotten | 270 | 5 |

The picker's walk-audit findings total 4 real ones: 2 in the hall, 1 in the kitchen and 1 in the crypt. No room
shares its state with a wing-mate or a neighbour.

**The pipeline.** Each stage re-runs and overwrites only its own files.

| # | Stage | Command | Run time |
|---|---|---|---|
| 1 | design | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Design -- design --seed 1 --out RUN` | 1.1 s |
| 2 | pick | `python C:/Repos/Github/Crawfis-Software/IronCitadel/kit/pick/pick_rooms_v2.py RUN/level.json` | 0.3 s |
| 3 | build | `IronCitadelV2.Editor.IronCitadelV2Builder.Build("RUN")`, by `unity command … eval` on the agent instance | 3.2 s |
| 4 | tactics | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Tactics -- --run RUN` | 9.2 s |
| 5 | build again | as 3; it now places the enemies and pickups | 3.2 s |
| 6 | fold | `dotnet run --project <PCGTools>/samples/IronCitadelV2/Design -- report --out RUN` | about 2 s |
| 7 | walk | `python tools/walk/make_walks.py RUN`, then `bash tools/walk/run-walk.sh RUN` | 3.7 min of play |

Here RUN is `C:/Repos/Github/Crawfis-Software/IronCitadel/v2` and `<PCGTools>` is `C:/Repos/Github/Crawfis-Software/PCGTools`.

- Stages 1 and 2 write the same bytes again for the same seed.
- Re-running stage 4 on the floor from the second build gave byte-identical placements, so the floor is stable.

**What the builder does** (MTB `Assets/IronCitadel/V2`):
- It places each prefab at its cell centre and turn.
- It closes every tile edge that has no door.
- Doors:
  - Open doorways have no leaf.
  - A shut door is an `SM_Env_Door_01` leaf that swings open with E.
  - The hidden door is v1's bookcase. It has no prompt, and its colliders switch off once it is open.
- It bakes a NavMesh with every door open: agent radius 0.35 m, 7,740 triangles, 8,105 m².
- It places the enemy markers:
  - ranged: a goblin shaman with a staff;
  - melee: a goblin warrior, or a skeleton in the prison, crypt and torture chamber;
  - boss: the war chief at 2×;
  - guards: warriors.
- It lights each room to a target for its class: 47 fill lights in all.
- It adds a grade, plus one ambience volume per room for its theme and state (15 looks).
- It puts the player prefab at the tactics program's start.
- It adds a HUD: the room name, a timer, the E prompt, and a banner at the throne. M shows v1's top-down map.
- Then it samples the floor (`floor.json`), rebuilds the room graph from the scene (`built.json`), and takes 23 captures.

**Captures**, in `captures/`:
- the top-down overview: `overview-rig`, plus `overview-night`, lit as walked;
- one eye-level shot per room: `room-*.png`;
- the choke from the entry hall: `start.png` from the player's start, and `extra-choke-through-doorway.png` from the
  doorway;
- the flank door under the gallery: `flank-door-d_armory__dining_hall.png`;
- the hall floor from the gallery's rail: `gallery-down.png`, and `gallery-up.png` from the floor;
- the throne on its stage: `throne.png` from the stage, and `extra-throne-stage-from-floor.png`, which shows the steps;
- the vault's hidden way in: `hidden-door-d_storeroom__treasure_vault.png`;
- a plan of the floor, doors, enemies, pickups and every walked leg: `walk-map.png`.

## Which PCGTools features decided what

**Design** (`Design/`, recorded in `design.json` → `pcgtools`):
- **`CyclicTopologyGenerator`** (ForeshadowingLoop, cycle 6, SideRoomChance 0.5) made the big loop.
  - Its short arc is the vista from the entry hall to the armory.
  - On the seed's third draw, it put side rooms on the hall, the storeroom and the kitchen. These became the
    library, the vault and the guard room.
- **`DungeonGraphBuilder`** added the shrine, the throne room, the study, the crypt, the prison and the torture
  chamber. It built two graphs over the same ids:
  - the intent graph, where the choke is a Vista;
  - the design graph, where the choke is walkable.
- **`MissionRoute.Measure`** gave the route the Director reads progress along: entry hall → storeroom → forge →
  kitchen → hall → library → shrine → throne room.
- **`DungeonExperienceDirector`** (a custom recipe, Intermediate) gave each room a risk and a threat budget. It
  sized the 12 enemies in rooms the brief says nothing about.
- **`PendantRegions`** found the side wings. Their mouths got shut doors, and the vault's mouth got the hidden door.
- **The checks:**
  - `LargestLoop` checked the big loop.
  - `PathQuery.NodeDisjointPathCount` and `AlternateRoutes` checked that there are two ways to the hall and two into
    the armory, and that there is no third way.
  - `GateValidation` and `GateBinding` checked that every room is reachable and that nothing needs a key.

**Tactics** (`Tactics/`, recorded in `tactics.json`):
- **The choke guards:** `GateDecomposition` and `HeldGrounds.AtGate`.
- **The armory fill and the Director's rooms:** `ExperienceDirectorBuilder`, with `DungeonBlocks.Rooms` and
  `BlockRegionSource`.
- **The gallery posts:** `VantageFieldBuilder`, with Sniper weights over the 202 hall-floor cells.
- **The player's start and every sight test:** `LaneVisibility`.
- **The checks:**
  - `HeldGrounds.AtGateFrom` and `FlankingRouteDetector`: the flank;
  - `MeanExposure`, `MedianExposure`, `MaxExposure` and `ExposureField`: the gallery;
  - `HiddenAreaDetector`: the vault.

**Ours, not PCGTools:**
- the fifteen kinds and their table;
- the wings;
- the grid embedding: a backtracking search, scored. Of 783 embeddings visited, 356 were valid; the best scores 49;
- the door rules;
- the brief's counts in five rooms;
- the hall-floor and throne-stage posts;
- the floor-to-grid import (two views of a 2.5D floor);
- JSON and SVG output.

## Gaps

**In PCGTools** (from both programs, in `design.json` → `gaps` and `tactics.json` → `pcgtools_gaps`):
- **Missing pieces:**
  - Rooms have no kind, theme, state or height, and nothing groups rooms into wings.
  - There is no graph-to-grid embedder. `RandomSpatialLayout` places breadth-first, may go diagonal and never closes
    a loop.
  - There are no door kinds: no open, shut, hidden or secret door on `GateData`.
  - There is no "deadly but passable" gate. ForeshadowingLoop's short arc is a Vista, which blocks movement, but the
    brief needs head-on to be possible. Hence the two graphs.
  - Side rooms are coin flips that cannot be asked for on given rooms, so the design redraws.
  - `DungeonGraphBuilder` cannot start from an existing graph, and `Build()` validates nothing.
- **Bugs:**
  - `AbstractRoom<R>._nextId` is a static counter shared across the whole process, so room ids depend on what ran
    before.
  - `ComputeBranchLevels` throws unless `ComputeEdgeFlows` ran first.
  - `MissionRouteResult`'s constructor is internal, so a route cannot be written by hand.
- **The Director** gives counts per archetype only: no post, facing or watch, and no "place N here".
  - Its strongest-first fill makes a small budget ranged, so the kitchen, guard room, prison and torture chamber
    each hold one lone goblin shaman.
- **The tactical tools:**
  - A grid has no layers, so a gallery deck over a walkway cannot be one graph.
  - `LaneVisibility` traces every cell up front; it cannot be asked about a few viewers only.
  - `CoverLayer` holds one height per edge, so an open door needs a second sight build.
  - The held-ground and flank tools need one graph, so the whole loop is analysed together.
- **No export:** there is no JSON or SVG writer for a dungeon graph.

**In the room library:**
- **Door approaches run along the tile edge.**
  - The barracks mess's N door and the arsenal's S door each open into a corridor along the shared edge. The choke is
    therefore a 1 m slot between two parallel corridors.
  - From the 46 entry-hall cells 10 to 20 m back, `LaneVisibility` sees no armory cell at all. Only cells within 3.6 m
    of the doorway see a guard.
  - The start, 11.9 m back, sees the doorway at the end of its corridor, but not the guards. That is what you want:
    the guards stay hidden until the player reaches the doorway. The start rule as first written asked for the
    opposite and failed; it was flipped after the run and now passes (`tactics.json` → `rules.player_start_pass`).
  - Other rooms meet their doors head-on (the kitchen's doors, for one), and such a pair would show the guards from
    the start. Nothing in the index says which approach a door has, so the picker cannot choose either way.
- **The entry hall is `tossed`, not "long deserted".** Barracks mess `deserted` has one real overlap finding. The
  picker takes a clean room first and keeps states apart from the neighbouring rooms. No lobby family is standard
  height.
- **Unreachable floor:** 583 of 7,961 floor samples are pockets no walk reaches: 236 in the crypt (collapsed tombs)
  and 122 in the prison (closed cells).
- **The throne stage's first step** is 0.6 m, which is more than a floor link allows. Only a NavMesh path joins the
  stage to the floor; the walk climbed it.
- **The shut door's leaf** pushes the player back about 1.2 m as it swings. The walker copes, but a player will
  notice.

## What was checked, and how

**The design** (27 of 27, `design.json` → `checks.design`):
- every kind once, every room in its own cell, and every door between neighbours;
- every door set reachable by a turn, and the hall turned to the armory;
- the big loop, the routes and the vault as a dead end, read back through the PCGTools types named above;
- the same seed writes the same bytes.

**As built** (`built.json`; the fold re-checks it, `design.json` → `checks.fold`):
- **The method:** with every door open, each shared tile edge was scanned every 0.25 m on the baked NavMesh, and each
  run of crossings was path-tested.
- **The results:**
  - all 15 design doors are walkable and of their kind;
  - there are 0 openings the design has no door for;
  - the big loop is intact;
  - the vault has one crossing, its hidden door;
  - all 33 enemies are on the NavMesh;
  - every room has its picked prefab and turn.

**Tactics on the built floor** (4 of 4, `tactics.json`):
- **The guards hold the entrance:**
  - all 3 choke guards stand in the 85-cell held ground behind the gate and see the doorway;
  - head on is still possible: it is an open doorway.
- **The flank comes in behind:**
  - `FlankingRouteDetector` reports Detected;
  - the way in lands at least 105° off every guard's facing, outside every guard's sight.
- **The hall floor is exposed and the climb is not:**
  - every one of the 202 hall-floor cells is seen by a gallery enemy. That holds in the 2.5D sight model only: a 3D
    check after the run found just the enemy at the rail sees the floor (see "After the run");
  - the route across the floor has median exposure 1.0 (mean 0.54);
  - the climb from the way-round door touches no hall-floor cell, and its mean exposure is 0.18;
  - 11 of its 37 cells are in some gallery enemy's sight. `tactics.json` does not say which ones.
- **The vault is out of sight:**
  - no main-route cell sees a vault cell, even with its door open;
  - `HiddenAreaDetector` reports Detected.

**The walks.** The walks were in play mode, after the last change to the scene. They used the player prefab as it is,
steered through its look and move inputs, with E pressed at doors and no jumps or teleports. The enemy capsules carve
the NavMesh, so the walk goes round them.

| Leg | Rooms | Walked | Time | Doors opened with E |
|---|---|---|---|---|
| way_round | entry hall → storeroom | 34 m | 13 s | entry hall–storeroom |
| vault | storeroom → vault → storeroom | 151 m | 41 s | the bookcase |
| way_round_2 | storeroom → forge → kitchen → dining hall | 168 m | 43 s | none |
| gallery | dining hall, onto the deck | 31 m | 8 s | none |
| flank | dining hall → armory, to the best gear | 127 m | 35 s | armory–hall |
| throne | armory → hall → library → shrine → throne room | 297 m | 85 s | hall–library, shrine–throne room |

All six pass, 808 m in 3.7 min (`walks.json`, `walks-summary.json`). The sample level passed the same six legs at
17:24.
- **The gallery leg** reached the top of the stairs, 4.4 m up and 0.8 m from the deck point (the deck is at 4.8 to
  5.0 m).
- **The way in:** at floor level it stayed within 10.2 m of the way-in door, along the hall's south end.
- **The flank leg's planned length** is 128 m. Most of it is the walk back down from the deck and round to the flank
  door.

**The brief's "what must be true":**

| Must | How it was checked | Holds |
|---|---|---|
| The armory has two ways in: the guarded entrance, seen from the entry hall, and a way from the hall beneath the gallery | `built.json` doors; tactics checks 1 and 2; the flank walk; `start.png` and `extra-choke-through-doorway.png`; LaneVisibility from the start | Yes. The start sees the doorway; the guards come into view only within 3.6 m of it, as you want |
| A way from the entry hall to the hall that avoids the armory | the way_round walks: entry hall → storeroom → forge → kitchen → hall; `AlternateRoutes` on the design | Yes |
| A gallery high above one side of the hall; reachable from where the way round comes in without crossing the floor; much of the floor seen from its rail | the deck is at 5.0 m (`floor.json`); the gallery walk; tactics check 3: the climb touches no floor cell and all 202 floor cells are seen; `gallery-down.png` | Yes, from the rail. But only 1 of the 4 gallery enemies stands at it, and the other 3 see no floor (a 3D check after the run) |
| The throne stands on a stage with steps | `floor.json`: a 1.3 m stage about 9 × 13 m, with steps at 0.6 and 1.1 m; the throne walk ends on it at y = 1.29; `extra-throne-stage-from-floor.png` | Yes |
| The vault has one way in, and the player may have to search for it | `built.json`: 1 crossing, hidden; tactics check 4; the vault walk opened the bookcase with E | Yes |
| The prison, guard room, kitchen and vault are low; the hall and throne room are tall; nothing is taller than the throne room | headroom measured over every reached floor sample: low rooms 3.4 to 5.0 m, standard 9.9 to 10.0, tall 14.9 to 15.0; the throne room is 15.0 and nothing exceeds it | Yes |
| Everything is indoors and every room has a ceiling | a ray cast up from each of the 7,378 reached floor samples: every one hits something within 60 m, and headroom never exceeds the room's lid | Yes |
| Every opening is a door or doorway, some open and some shut, and the player can open any shut door on a way through | 15 crossings = 15 design doors (6 open, 8 shut, 1 hidden), 0 extra; the walks opened every shut door on a way through | Yes |
| Tall and low occluders to hide behind, standing or crouching | `tactics.json` → `cover`: 1,349 low blocks (0.5 to 1.6 m: crouch) and 2,362 tall (above 1.6 m: stand) over the level, apart from walls | Yes |

The brief's "nothing points to it" for the vault means no main-route cell sees it. The room kinds reading as
themselves is for you to judge from the room captures.

## Time

**Wall clock: 15:45:32 to 17:40, so about 1 h 55 min.** The phases overlapped because four agents worked in
parallel.

| Phase | Clock | Length | What |
|---|---|---|---|
| Design, setup | 15:45:32 to 16:03 | 18 min | re-survey PCGTools; the run's decisions; `CONTRACT.md`; the hand-written sample level |
| Design, the program | 16:04 to 17:00 | 56 min | A1: the design program; seed 1 chosen from a sweep of 1 to 40 |
| Build: picker | 16:05 to 16:21 | 16 min | B: `pick_rooms_v2.py` |
| Build: Unity builder | 16:05 to 17:18 | 73 min | C: the builder, the floor sampler, the as-built check, the captures |
| Build: tactics | 16:04:47 to 17:26:48 | 82 min | A2: the tactics program, the critical path; the real floor arrived at 17:15 |
| Build: the real level | 17:00 to 17:27 | 27 min | pick, build, tactics, build again: about 20 s of compute, the rest waiting on A2 |
| Checking | 17:19 to 17:34 | 15 min | both walks, the extra shots, the ceiling rays, the fold, saving and closing the editor |
| Report | 17:34 to 17:40 | 6 min | this file |

## What I would change

**The prompt** (`pcg-level-prompt-v2.md`):
1. Ship the stage contract with the prompt. `CONTRACT.md` and the sample level took 10 minutes, and they are what
   let four agents start at once.
2. Say in the brief or the prompt that the guards stay out of sight until the player reaches the doorway, and make
   it a check: the start, at least 10 m back, sees the doorway and no guard, and no entry-hall cell more than 5 m
   from the doorway sees one. The brief's act one ("the player can see the guards") reads the other way.
3. Name the floor sampler and the play-mode walker as deliverables, with the files they write. The checks depend on
   both, and the walker was written outside the four agents.
4. State the tile facts in the prompt: k = 9, doors at the middle of each edge, and where the hall's gallery stairs
   start. Every agent needed them.

**PCGTools**, in order of what would make the same level faster:
1. A graph-to-grid embedder with door-side constraints.
2. Door kinds on `GateData`, plus a passable "guarded" short arc for ForeshadowingLoop. This removes the two-graph
   workaround.
3. A layered floor-grid import from sampled floors. The tactics program had to write its own import (two views of
   the 2.5D floor, cropped per question) before any PCGTools tactic could run.
4. The small fixes: the static id counter, `ComputeBranchLevels`, a public route constructor, side rooms on chosen
   hosts, and JSON/SVG export.
5. Posts and facings from the Director, and "place N here".
6. Sync the Unity package (5.0.0, 10-06) with `src/`, so the design can run in the editor.
7. A sight test that knows where a rail is. `LaneVisibility`'s height advantage lets a raised viewer see past any
   obstruction its eye clears, so posts back from a gallery's rail pass as overlooks (see "After the run").

**The room library:**
1. Record each door's approach in `rooms-index.json`: along the edge or head-on, and its straight length. The
   picker can then choose on purpose. Here the along-the-edge corridors are what hide the guards until the doorway;
   a head-on pair would show them from the start.
2. A clean `deserted` barracks mess, or a standard-height guard lobby, for the entry hall.
3. A lower first step on the aisled throne stage, and fewer sealed pockets in the collapsed crypt and the riot
   prison.
4. A door leaf that does not shove the player back as it opens.

**The process:**
1. Give each agent its own copy of the run folder. The builder rewrote `floor.json` three times under the tactics
   agent.
2. Build the floor sampler first. The tactics program, the critical path, waited until 17:15 for a real floor and
   worked on a synthetic one before that.
3. Script the must-list (a `musts.py` reading `built.json`, `floor.json`, `tactics.json` and `walks-summary.json`), so
   the last table here is one command.
4. Give the picker the design's seed, so rooms of equal rank vary from seed to seed. Today it always takes the
   top-ranked room for a door set (see "After the run").
5. Give every check that finds a region a minimum size. The hall check passed on 3 cells in seeds 7 and 40.

## After the run

**The guards stay out of sight until the doorway.** After the run you wrote: "it's best that you don't see the
guards until you get to the jump". The level already worked that way, so only the tactics rule changed (18:19 to
18:21):
- the start must see the doorway and no guard;
- check 1 fails if an entry-hall cell that sees a guard lies more than 5 m from the doorway;
- guard and start placement prefer the same.

Re-running it on the real level (8.7 s) and on the sample gave byte-identical placements, so nothing was rebuilt.
Every check and rule passes.

**Four more seeds.** You asked: "Let's do 3 or 4 more seeds to test that 4 seconds". A design sweep of seeds 1 to 40
gives 5 layouts, and all of them pass. Seeds 2, 6, 7 and 40 cover the four layouts the run did not use.
- `tools/run-seed.sh <n>` runs design, pick, build, tactics, build again and the fold, timing each stage.
- The builder's new `BuildAs(run, suffix)` saves each seed as its own scene with its own NavMesh, so
  `IronCitadelV2.unity` is untouched.

| Seed | Layout, rows 0 to 2 | Design | Pick | Build | Tactics | Build again | Fold | Total | Walks |
|---|---|---|---|---|---|---|---|---|---|
| 1 (the run) | `EADLHT RFKSC. V.GPX.` | 1.1 | 0.3 | 3.2 | 9.2 | 3.2 | 2 | 19 | 808 m, 3.7 min |
| 2 | `EADLHT RFKSC. VGPX..` | 2.4 | 0.3 | 3.0 | 13.5 | 3.5 | 1.7 | 24.4 | 843 m, 3.9 min |
| 6 | `EADLHT FRKSC. .VGPX.` | 1.9 | 0.2 | 2.0 | 10.9 | 3.0 | 1.7 | 19.7 | 797 m, 3.7 min |
| 7 | `.EADLS XFRKHT PGV.C.` | 2.0 | 0.3 | 2.6 | 10.4 | 3.1 | 1.7 | 20.1 | 839 m, 3.8 min |
| 40 | `.EADLS GFRKHT PXV.C.` | 1.9 | 0.2 | 2.3 | 10.5 | 3.2 | 1.7 | 19.8 | 845 m, 3.9 min |

- Times are wall-clock seconds per stage. The letters are those in "What was built", and each `.` is an empty cell.
- Seed 1's times come from the run itself. The other seeds were timed by `run-seed.sh` between 18:25 and 18:43.
- The "4 seconds" holds. By the builder's own clock a build takes 1.7 to 3.2 s, and design to folded scene is about
  20 s. Tactics, at 10 to 14 s, is half of that.
- Every seed passes design (27 of 27), tactics (4 of 4), the rules, the fold (after the fix below) and all 6 walks.

**A bug the seeds found.** Seeds 7 and 40 put the hall one tile east. There the tactics program took the gallery deck
for the hall's room proper:
- it found 3 floor cells instead of about 200;
- it placed 1 of the 6 floor melee;
- the hall check still passed. The fold caught it.

The fix looks for the hall floor among floor-level cells only, and fails the check below 49 cells.
- Seeds 1, 2 and 6 and the sample come out byte-identical.
- Seeds 7 and 40 find 204 floor cells and place all 6 melee.
- Both were rebuilt (3.2 s each), folded and walked again: 6 of 6 walks each.

**How much the seeds vary.** The layout changes, but most of the rooms do not.
- 10 of the 15 rooms use the same prefab in all five levels, and 7 of those at the same turn.
- The other five (the forge, kitchen, prison, shrine and study) switch to another prefab of the same kind, only
  because their door set changed.
- The choke, the gallery hall and the throne room are the same everywhere.

The picker always takes the top-ranked room for a door set; given the design's seed, it could vary rooms of equal
rank. The layouts fall into two families: seeds 1, 2 and 6 put the throne at the end of the top row, and seeds 7 and
40 bring the shrine and the throne down to the middle row.

**The film.** You asked: "Create videos if they are quite different. Pick one if similar". They are similar, so there
is one film, of seed 7 from the second family: `C:/Repos/Github/Crawfis-Software/IronCitadel/flythrough/v2-seed-7/iron-citadel-v2-seed-7-1080p.mp4`,
3 min 13 s at 1080p.
- **How it was made:** v1's flythrough tool and compositor ran on `IronCitadelV2_seed-7_film.unity`. That scene is a
  copy of the seed's scene with every shut door swung open, and the hidden door given v1's `SecretBookcaseDoor` so the
  bookcase swings on camera.
- **Rendering:** `tools/render-film.sh 7` renders 5,752 frames at 2560 × 1440, 600 frames per process, in about 6 min.
  Composing takes about 1.5 min.
- **The 12 shots:**
  - the establishing aerial;
  - the choke, whose guards appear only at the doorway;
  - the way round, through the forge;
  - the storeroom's bookcase swinging open onto the vault;
  - the kitchen and the gallery stairs, ending at the parapet over the hall floor;
  - the flank back into the armory, behind the guards;
  - the gallery seen from the hall floor;
  - the library, the shrine and the throne on its stage;
  - the rise, and the outro map.
- **What the previews caught:** from the deck, a standing eye cannot see the hall floor over the 1.3 m parapet. The
  gallery shot therefore ends in the 3 m gap at the parapet's south end.

**The gallery's ranged enemies mostly cannot see the hall floor.** The film led to a 3D check in the editor. It
casts Unity physics lines from each gallery enemy's eye to 287 open points of the hall floor, with the enemies'
own colliders off.

| Gallery enemy | Where it stands | Floor points seen, eye at 1.6 m | Eye at 1.9 m |
|---|---|---|---|
| ranged 3 | at the rail | 130 of 287 | 210 |
| ranged 1 | 4.5 m back, at the south stair head | 0 | 0 |
| ranged 2 | 4.5 m back, at the north stair head | 0 | 0 |
| ranged 4 | 8.5 m back, near the deck's back wall | 0 | 0 |

The rows are seed 7. Seed 1 has the same pattern, and seeds 2 and 6 use seed 1's posts: one enemy at the rail sees
the floor and three see none.
- **Why tactics passed:** its sight is PCGTools' `LaneVisibility`, which uses height advantage. A viewer whose eye
  (5.0 + 1.6 = 6.6 m) clears an obstruction's top (the parapet, 6.2 m) sees past it at any distance behind it. A real
  line from 4.5 to 8.5 m back clears the parapet only at a grazing angle and lands beyond the hall.
- **What still holds:** the brief's "much of the floor seen from its rail" holds, but only one of the four enemies is
  at the rail.
- **The fix, not made:** place gallery posts at the rail, or check each post's sight in 3D (a parapet-aware height
  advantage would do). The film's caption on the hall-floor shot says this plainly.

## Files added or changed

Nothing is committed; every file below is new.

- **PCGTools** `samples/IronCitadelV2/`:
  - `README.md`;
  - `Design/`: `IronCitadelV2.Design.csproj`, `Program.cs`, `Kinds.cs`, `Designer.cs`, `Checks.cs`, `Output.cs`,
    `Json.cs`, `Svg.cs`, `Report.cs`;
  - `Tactics/`: `IronCitadelV2.Tactics.csproj`, `Program.cs`, `Files.cs`, `Level.cs`, `Synth.cs`, `Region.cs`,
    `Tactics.cs`, `Director.cs`, `Checks.cs`.

  The Unity package is untouched; the untracked `upm/**.cs.meta` files predate this run (10-06).
- **IronCitadel:**
  - `kit/pick/pick_rooms_v2.py`;
  - `v2/`:
    - `CONTRACT.md`, `RUNLOG.md`, `REPORT.md`;
    - `level.json`, `design.json`, `graph.svg`, `picks.json`, `placements.json`, `tactics.json`;
    - `floor.json`, `built.json`, `build-log.txt`;
    - `walks.txt`, `walks.json`, `walks-summary.json`, `walk-run.txt`, `shots.txt`;
    - `captures/` (23 from the builder, 2 extras and `walk-map.png`);
    - `sample/`: the same files for the hand-written bootstrap level;
    - `tools/walk/`: `V2WalkDriver.cs`, `IronCitadel.V2.Walk.asmdef`, `make_walks.py`, `walk_report.py`,
      `run-walk.sh`, `walk_map.py`;
    - `tools/shots/`: `make_shots.py`, `V2Shots.cs`;
    - after the run: `tools/run-seed.sh`, `tools/render-film.sh`, and `seeds/seed-{2,6,7,40}/`, each with the
      same files as the run plus `times.tsv`;
  - `flythrough/v2-seed-7/`: `shots.json` and `labels.json` for the film. The frames, previews, poses and the MP4
    there are ignored by the repo's `.gitignore`.

  The prompt and brief drafts in `cold/` come from earlier in this session.
- **MacroTileBuilder**, shared with the agent instance:
  - `Assets/IronCitadel/V2.meta`;
  - `Assets/IronCitadel/V2/Runtime/`: `IronCitadel.V2.asmdef`, `IronCitadelV2Door.cs`, `IronCitadelV2NavMesh.cs`,
    `IronCitadelV2Hud.cs`;
  - `Assets/IronCitadel/V2/Editor/`: `IronCitadel.V2.Editor.asmdef`, `IronCitadelV2Builder.cs`, `V2Model.cs`,
    `V2Doors.cs`, `V2Floor.cs`, `V2Lighting.cs`, `V2Captures.cs`;
  - `Assets/IronCitadel/V2/Walk/`: `IronCitadel.V2.Walk.asmdef`, `V2WalkDriver.cs`;
  - each of these with its `.meta`.

  The scene is `Assets/CrawfisSoftware/AllSyntyDungeons/Levels/IronCitadelV2.unity`, with
  `Levels/IronCitadelV2/IronCitadelV2.NavMesh.asset` and `Levels/IronCitadelV2/Ambience/` (the grade and 15
  profiles). It was saved at 17:27 and again before the editor closed at 17:34. MTB has no changes to tracked files.

  After the run, `IronCitadelV2Builder.cs` gained `BuildAs`. Each seed has its own scene,
  `Levels/IronCitadelV2_seed-<n>.unity`, with its NavMesh in `Levels/IronCitadelV2/`. The film's scene is
  `Levels/IronCitadelV2_seed-7_film.unity`.
- **PCGTools**, after the run: `Tactics/Tactics.cs` and `Tactics/Checks.cs` changed twice, for the start rule and for
  the hall-floor fix.

**Not done:** the Opus cold run. It needs v3's rework for a brief with no grid, and its own go-ahead and cost.
