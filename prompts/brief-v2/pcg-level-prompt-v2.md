# Ours for brief v2: designed by PCGTools, built from the themed rooms (prompt v2)

This is "ours" for brief v2 (`brief-v2-draft.md`, beside this file), the follow-on to the comparison. Roger,
2026-10-13: "I will not run the warm on ours. We will run it and ask PCGTools to design the level." It is built
from `pcg-level-prompt-v1.md`, with the same tools, build and report. What changes is the level: it is the brief's,
not "one room of each theme, with keys and locks". It has no fairness rules, so it points at our tools. The cold
side is one Opus run on `cold-prompt-v3.md` with the same brief.

## Notes for Roger (not part of the prompt)

**PCGTools, re-surveyed 2026-10-13 at `c6cb9293`, the local copy equal to GitHub.** Roger: "PCG Tools has been
modified and may have some better features now." Since this morning, the only merges are #183 and #184,
`samples/RecipeAtlas`. It draws the six loop archetypes and the 15 tactical patterns as HTML, and tags each recipe
with the patterns it supports. The loop work merged on 10-08 and 10-09: `GrowLargestLoop`, `GateSafety`,
`MaxHopDistance` and growing the room-level loop before layout. The features that fit this brief were already in
`src/`:
- **The big loop:**
  - `CyclicTopologyGenerator` has six archetypes: LockAndKeyCycle, SneakPast, TwoKeys, DoubleBack,
    TwoAlternativePaths and ForeshadowingLoop.
  - `LargestLoop.Find` measures the loop and can force it through a room.
- **The choke point and the flank:**
  - `HeldGrounds.AtGate`, `AtGateFrom` and `InSightOf` give the ground the guards hold and the flanks into it.
    `ChokePlaces.Split` gives only the choke itself.
  - `FlankingRouteDetector` finds a hidden way in behind the guards.
- **The gallery and the kill zone:**
  - `GalleryDetector` and `SplitLevelDetector` find raised walkways.
  - `ExposureField`, `ExternalExposureField` and `RouteExposure` score how exposed a cell or a route is.
  - `VantageFieldBuilder` scores the places that overlook a target.
- **The vault:** `HiddenAreaDetector` finds a small, little-used pocket that the main route cannot see.
- **The enemies:**
  - `DungeonExperienceDirector` gives counts per room.
  - `DungeonBlocks.Rooms` fed to `BlockRegionSource` gives the grid director's cell positions.
  - Nothing gives facing.
- **Cover:** `CoverLayer` gives a cover height on each cell edge. Standing against crouching is not modelled.
- **The as-built check:** the tactical analysis takes any grid of cells with a movement graph, so the built rooms'
  floor, sampled into a grid, feeds it directly.

**Still missing in PCGTools:**
- room themes and states (only a text tag, `RoomTemplate.Theme`);
- a secret door (a sneak passage is two-way, like a door);
- NPC and key spawn categories, and facing;
- a JSON export that carries doors and NPCs;
- gates enforced when rasterising.

The gallery pattern's stair leaves from a doorway in the hall's wall, so reaching the gallery without crossing the
floor comes from the room picked, not from PCGTools. The flank spec still lists it as open.

**The Unity package lags `src/`.** It is still 5.0.0 from 10-06. It lacks 27 files, including `GateSafety` and all
the loop work, and the fixes to `LoopSampler`, `PathTracer` and `LoopCellRealizer`. So the design runs as a console
program against `src/`. `tools/sync-unity.ps1` would update the package; it writes files, so it is your call.
Separately, 41 `.meta` files from the 10-06 sync sit uncommitted in PCGTools' `upm/` folder.

**From the Builder side** (this morning's survey, still true):
- **The assembler is tied to the v1 brief.**
  - `IronCitadelRoomsAssembler` has fixed paths and a 45 m tile. It expects a room with id `throne_room` and
    starts the player in `entry_hall`. Its fixes go by kind letter, and it picks characters by room id.
  - Its generic core: placing picks at cell centres with rotation, `PatchMissingColliders` and `CheckDoors`.
  - Fork it; do not change it.
- **The picker (`kit/pick/pick_rooms.py`) knows only v1's kinds and its "in use" states.** The fork adds the crypt,
  storeroom, shrine and torture chamber, and every theme's abandoned and looted states.
- **Refused rooms.** The picker refuses the rooms whose walk audit failed. Walk every pick anyway: three
  index-clean rooms were cut in the 2026-10-07 trial.
- **No archer and no bow exists in the dungeon packs.** The goblin shaman with a staff is the ranged enemy. The
  goblin warriors and skeleton soldiers are melee, and the war chief is the warlord. `BuildMarkers` already poses
  static characters with a capsule collider.
- **The entry hall has no standard-height lobby.** The only lobby family, `guard_room`'s, is low. The prompt lets
  the run choose between it and a standard-height common room or barracks, and say why.
- **Generic tools:** conformance checks 1 and 2, `run-audit.ps1 -generic` and `LevelStats.Run`. The ambience tool
  gives all 21 themes a mood.

**Cost.** It runs interactively, as a paid Opus session. Going by v1's estimate, that is about $30 to $60. Announce
it first.

**Your calls before a run:**
1. Where the design program lives: `PCGTools/samples/`, beside RecipeAtlas (recommended), or a new folder in the
   Builder's `tools/`.
2. Whether to sync the Unity package first. The design does not need it.

## The task prompt (verbatim; paste `brief-v2-draft.md` where marked)

> You are building a dungeon level end to end with our own tools, from the brief below. PCGTools designs it:
> which rooms there are and how they connect, which doors are shut or hidden, and who stands where. The
> Builder's themed-room library builds it in Unity. Roger will walk it, so it has to play well, not just
> pass checks.
>
> THE BRIEF: *[brief-v2-draft.md pasted in full]*
>
> WHAT THE BRIEF MEANS WITH OUR TOOLS.
> - Each room kind is one room from the library, of the theme below. Every room is in an abandoned or a
>   looted state, apart from the armory and the vault; vary them so the rooms do not all look alike.
>
>   | Kind | Theme | States (abandoned / looted) |
>   |---|---|---|
>   | Entry hall | `guard_room`'s lobby family, which is low, or a standard-height `common_room` or `barracks`; say which and why | deserted / brawl, or the chosen theme's |
>   | Armory | `armory` | stocked |
>   | Guard room | `guard_room` | deserted / brawl |
>   | Prison | `prison` | forgotten / riot |
>   | Kitchen | `kitchen`, with a low ceiling | spoiled / raided |
>   | Library | `library` | ruined / pillaged |
>   | Alchemist's study | `alchemy_lab`, or `study` | derelict / ransacked |
>   | Forge | `forge` | cold / stripped |
>   | Treasure vault | `treasury` | sealed or hoard |
>   | Dining hall | `hall_gallery` | ruined / aftermath |
>   | Throne room | `throne_room` | fallen / sacked |
>   | Crypt | `crypt` | collapsed / desecrated |
>   | Storeroom | `storeroom` | rotting / plundered |
>   | Shrine | `shrine` | ruined / desecrated |
>   | Torture chamber | `torture_chamber` | forgotten / purged |
>
> - The rooms stand on the library's tiles, at one tile size. The throne room, the dining hall and the shrine
>   are all 15 m tall, which meets "no room is taller than the throne room".
> - The room graph has one big loop: the way through the armory and the way round it, between the entry hall
>   and the dining hall. Add no other way between those two rooms. Elsewhere, loops are yours to choose, and
>   small loops inside a room are fine.
> - Enemies are the art packs' characters, and they stand still. The ranged ones are goblin shamans with
>   staffs, since the packs have no archers. Goblin warriors or skeleton soldiers are the melee ones, and the
>   goblin war chief is the warlord.
>
> DESIGN IT WITH PCGTOOLS (`../PCGTools`). Use it for every design decision it can make, and say where it
> could not make one.
> - The room graph comes from its Dungeons module. The cyclic generator's archetypes (SneakPast and
>   TwoAlternativePaths among them) and `LargestLoop` give the big loop. The brief has no keys; add a lock
>   only if its gate checks show that it matters.
> - Its tactical analysis places the set pieces and the enemies:
>   - `HeldGrounds` gives the ground the armory's guards hold, and `FlankingRouteDetector` gives the way in
>     behind them.
>   - `ExposureField` and `RouteExposure` show that the hall floor is exposed and the climb to the gallery is
>     not.
>   - `VantageFieldBuilder` places the gallery's ranged enemies.
>   - `HiddenAreaDetector` shows that the vault is out of sight.
>   - `CoverLayer` gives the cover. Tall versus low is your split; say where you drew it.
> - How many enemies each room holds comes from its placement director, read along the route the player has
>   to take. `DungeonBlocks.Rooms` with `BlockRegionSource` gives their cells. Facing is yours: each enemy
>   faces what it watches.
> - Write the design as a .NET console program against PCGTools' source projects, with a seed, so the same
>   seed makes the same level. Do not use the Unity package, which lags the source. `docs/modules/dungeons.md`
>   shows how to start, and `samples/RecipeAtlas` runs the tactical pipeline from a console program.
> - Read the room library's index before you design: it says what each theme offers.
> - Where PCGTools lacks something you need, build it in your program and list it as a gap. Do not patch it by
>   hand in Unity.
>
> WHAT WE KNOW ALREADY.
> - PCGTools has none of these:
>   - room themes or states, so carry them in your own room data;
>   - a secret door, because a sneak passage moves like any door;
>   - an NPC or key spawn category, or facing;
>   - a JSON export of rooms, doors and NPCs, so writing one is part of the job.
>
>   Rasterising does not enforce gates.
> - The tactical analysis takes any grid of cells with a movement graph, so the built rooms' floor, sampled
>   into a grid, feeds it.
> - PCGTools' gallery pattern starts its stair from a doorway in the hall's wall. Reaching the gallery without
>   crossing the floor therefore comes from the `hall_gallery` room you pick. Check it on the built room, and
>   turn the room so that its gallery side faces the armory.
> - The Iron Citadel's level file, room picker and rooms assembler show how a level file becomes a scene. They
>   are tied to that brief, so fork them; do not change them. The picker knows neither the crypt, storeroom,
>   shrine and torture chamber nor any abandoned or looted state.
> - The picker refuses rooms whose walk audit failed. Walk every room you pick anyway.
> - The E key's script and the Iron Citadel's secret bookcase door show how to make a door that opens.
>
> BUILD IT IN UNITY, in the Builder, from the themed-room library at one of its tile sizes. Use one room per
> kind, chosen and turned so its doors fall where the design's doors are. Close every other tile edge: no
> opening the design does not have.
> - Build the scene from `design.json` with a builder you can run again, so a change to the design is a
>   re-run, not a hand edit.
> - Shut doors are real doors that open with E. The vault's way in is hidden. The player is
>   `Assets/IronCitadel/Walker/IronCitadelPlayer.prefab`. Its E key calls `SendMessageUpwards("Interact")` on
>   the first solid collider within 3 m, so a door needs a collider and a `void Interact()`.
> - Enemies stand where the design puts them, facing what they watch. Bake a NavMesh over the walkable floor;
>   every enemy stands on it.
> - The player starts in the entry hall. A small HUD names the room and shows the time. M toggles a top-down
>   map with the ceilings hidden. Reaching the throne is announced.
> - Light and grade each room for its theme and state, so a player can see where to walk. The Iron Citadel's
>   ambience tool already gives every theme a mood.
> - Save the scene under the AllSyntyDungeons package's `Levels/` folder.
>
> CHECKING. Check the level you built, not the one you designed. Do not write test suites; run the level.
> - Rebuild the room graph from the scene, with its rooms and doors as built, and compare it with the design.
>   The rooms and doors must match, the big loop must be intact, and the vault must lie on no way through.
> - Run the tactical analysis on the built floor:
>   - the armory's guards hold its entrance;
>   - the flank comes in behind them;
>   - the hall floor is exposed to the gallery, and the climb to the gallery is not;
>   - the vault is out of sight.
> - In play mode, with the player prefab as it is, walk each of these:
>   - the way round, from the entry hall to the dining hall;
>   - up to the gallery without crossing the hall floor;
>   - the flank into the armory;
>   - the hidden way into the vault and back;
>   - on to the throne.
>
>   Walk; do not jump, teleport or move the player by hand.
> - Check each "what must be true" item in the brief against the built scene, and say how.
> - Walk again after the last change to the scene.
>
> DELIVER:
> 1. The design program, with a README line on how to run it.
> 2. `design.json`, with a picture of its graph. It lists:
>    - every room: kind, theme, state, tile and turn;
>    - every door: which two rooms, and whether it is open, shut or hidden;
>    - every enemy: kind, room, position, facing and what it watches;
>    - the two pickups.
>
>    It also holds the big loop, the seed, and the checks' results for the design and for the built scene.
>    Deliver the level file your build reads beside it.
> 3. The scene, saved, with the editor closed.
> 4. Captures:
>    - a top-down overview;
>    - one eye-level shot per room;
>    - one each of the choke point from the entry hall, the flank's door under the gallery, the hall floor
>      from the gallery's rail, the throne on its stage and the vault's hidden way in.
> 5. A report covering:
>    - what you built and how;
>    - which PCGTools features made which decisions;
>    - the gaps you found in PCGTools and in the room library;
>    - what you checked and how;
>    - the time you took, in total and for each phase (design, build, checking), from the clock time you note
>      as you start each one;
>    - what you would change in this prompt, in PCGTools, in the room library or in the process to get a
>      better level, or the same level faster.
>
> HOW THIS RUNS.
> - AGENTS.md's Unity rules apply: work in the agent instance, ask the registry before you open an editor,
>   and never wait on another project's editor.
> - Unity's agent skills are installed (`unity:unity-cli`, `unity-pipeline` and the rest of Unity's plugin).
>   Use them to drive the editor.
> - On this CLI:
>   - a command's own parameters go after `--`;
>   - the CLI gives up on any command after its `--timeout` (30 s by default), while the editor carries on
>     with it;
>   - every capture's `save_path` lands under `Assets/`, which the agent instance shares with the Builder, so
>     move each capture out and delete its `.meta`.
> - Other sessions share these checkouts, so do not commit, push or switch branches. List every file you added
>   or changed in the report.
> - Nobody will answer questions while you work: make a call and note it in the report.
