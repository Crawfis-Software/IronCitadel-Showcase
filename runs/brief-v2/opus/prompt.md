You are taking part in an experiment: "how good a level can a frontier model build in Unity from a
designer's brief, using commercial art packs but no level-generation framework."

THE SETUP. This folder is a Unity 6 project (URP) with three Synty POLYGON dungeon packs already
imported (Dungeons, Dungeon Realms, Fantasy Dungeon Map), Cinema Suite's Cinema Themes colour-grading
LUTs, the Input System, AI Navigation, and Unity's Starter Assets first-person controller, ready to place
as `Assets/Interact/IronCitadelPlayer.prefab`. Its E key casts a 3 m ray from the camera and calls
`SendMessageUpwards("Interact")` on the first solid (non-trigger) collider it hits, so anything the
player opens, such as a secret door, needs a collider and a `void Interact()` method on that object or a
parent. The player's size, movement and controls are fixed. How the level looks through its camera
(lighting, post-processing, volumes, fog) is yours to set. The brief is pasted below, and is also in
`Brief/brief.md`. The `unity` command-line tool drives the editor, and the Unity Pipeline package is
installed: `unity open` starts the editor, `unity command`
runs editor commands on it, and `unity run --command` runs one headless. `unity command` with no
arguments lists the command groups (scenes, game objects, scripts and running C#, captures, play
mode, the console, and more); `--tag <group>` lists a group's commands, and `--query <word>` searches
them. `UNITY_PROJECT_PATH` is set to this folder, so `unity command` and `unity job` find this
project's editor without `--project-path`. Unity's agent skills are installed: its Claude Code
plugin (`unity:unity-cli` and the rest) and `unity-pipeline`.

UNITY CLI PITFALLS, found the hard way on this machine (CLI 1.0.0-beta.8 to beta.13, the version
installed now):
- `unity open .` returns as soon as the editor is launching; then
  `unity status --until-ready --project-path .` waits until this project's editor answers (up to
  300 s; add `--timeout <seconds>` for longer). `unity open` and `unity close` take the project's
  path as an argument (`unity open .`); they reject `--project-path`.
- While the editor is still importing or compiling, `unity status` can show `ready` while commands
  are still refused. A command then waits out the CLI's `--timeout` (30 s by default) and fails with
  "did not accept '<name>' within 30s. It was last busy (settling)". That is the editor, not your
  code: send it again, or give it a longer `--timeout`.
- `unity run` runs the editor in batch mode and reserves `-quit`, `-batchmode` and `-projectPath`:
  passing any of them exits 6 and launches nothing. Editor flags go after `--`:
  `unity run . -- -executeMethod My.Tool.Run -logFile run.log`.
- Exit code 2 means the command line was wrong (an unknown option, say) and nothing ran. Exit code 6
  covers most other failures: a failed command, a reserved flag, a project not found, no editor
  running. Treat any non-zero exit as a failure. Judge success by the files your method writes and
  by the log's tail, not by the exit code alone; the log's `ExitCode:` line only says whether your
  method returned without throwing.
- One editor per project. A batch run needs the project's lock, so it fails while an editor has the
  project open. Close the editor first (`unity close .`, which does not save) or use `unity command`.
- `--timeout` on `unity run` or `unity test` kills the editor and can leave `Temp/UnityLockfile`
  behind; the next boot then fails with exit 6 and no log. Only in that case, and only once
  `unity status` shows no editor on this project, delete that file. Never delete it while an editor
  has the project open.
- `unity command` takes a command's main argument bare (`unity command eval "return 1;"`). Its other
  parameters are named and go after `--`:
  `unity command capture_game_view -- --source camera --save_path Captures/a.png`. The CLI's own
  options (`--project-path`, `--timeout`, `--detach`, `--json`) go anywhere before the `--`, so a
  `--timeout` before the `--` is the CLI's wait, in seconds, and one after it is the command's own
  `timeout` parameter. Headless, it is `unity run . --command <name> -- --<parameter> <value>`.
  `unity job` takes its subcommand first (`unity job wait <id>`, `unity job status <id> --json`);
  `unity job --project-path . wait` is an error.
- On a live editor, `eval` and `eval_file` stop after 5 seconds of work ("Main thread operation timed
  out after 5000ms") unless you pass their own `timeout`, in milliseconds:
  `unity command eval -- --code '<C#>' --timeout 20000`. The CLI also stops waiting for any command
  after its own `--timeout`, 30 s by default; the editor carries on with that command, and the next
  one waits behind it. For longer work, write a `.cs` file outside `Assets/` and run a static method
  in it with `run_script`, which compiles the file in memory and runs it, with no import and no
  reload. Its own `timeout_ms` is 30000 by default, so raise both:
  `unity command --timeout 300 run_script -- --file Tools/Job.cs --entry Job.Run --timeout_ms 290000`.
  Or put `--detach` before the command name: it prints a job id at once, and `unity job wait <id>`
  waits for the job.
- After a script under `Assets/` changes, the editor recompiles and reloads. Until it has reloaded,
  commands fail at once with "Network error: An error occurred while sending the request". That is
  the reload, not your code: wait with `unity status --until-ready --project-path .`, then send the
  command again. `unity recompile --project-path .` compiles at once and prints any compile errors.
- With no editor running, `unity command` exits 6 and asks you to make sure the editor is running
  "with the Pipeline package installed", and `unity status` prints an empty table and exits 6. Both
  mean nothing is running; the package is installed.
- `--json`, `--format json` and `--result-only` print indented JSON, with a space after each colon
  (`"compiling": false`). Run a command once and read its output before you write a loop or a parser
  around it; a loop whose test never matches just runs until its cap.
- `eval` and `eval_file` compile your code as the body of a method: no `using` lines. Write full type
  names, and call extension methods through their static class. A `run_script` file is a whole C#
  file and may have them.
- In play mode, `simulate_key` reaches the controller's input actions (W moves the player), but code
  that reads `Keyboard.current` does not see it. The E-key script reads `Keyboard.current`; to use E
  from a script, stand the player where a player would stand and call that script's `Interact()`,
  which casts the same ray.
- `capture_game_view` captures the Game view window by default, overlay UI included, and fails with
  "No Game view render target to capture" when none is open;
  `UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game")` opens one.
  `-- --source camera` renders `Camera.main`, or else the first enabled camera, without overlay UI,
  and fails with "No camera found to capture" when the open scene has neither. Every `--save_path`
  lands under `Assets/`: `X/y.png`, `Assets/X/y.png` and `<this folder>/X/y.png` all save to
  `Assets/X/y.png`, and a path outside the project, or with `..`, is refused. Unity then imports the
  file: move it out and delete its `.meta`.
- Renders from a batch-mode editor came out with garbage colours (solid red squares) on materials
  created earlier in the same session; the same scene rendered in an open editor was clean. Judge how
  the level looks from an open editor's renders.
- For tests use `unity test`, never `-runTests` with `-quit`: the editor quits before the test
  runner and writes no results.
- `[Licensing::Client] HandshakeResponse … 505` in the editor log is noise.
- `Library/` holds several GB of import cache. Leave it out of listings, searches and `du`.
- Write and change source files with your file tools (write and edit). Shell heredocs and inline
  scripts break on quoting in this shell, and a script that rewrites source turns a `\n` meant for a
  C# string into a real line break, which does not compile.

WHAT YOU MAY DO. Anything a developer would do inside this project: read and write its files, write
editor scripts, run the editor in batch mode or open it, enter play mode, take screenshots and look at
them, search the web for documentation, install packages from the Unity registry, and iterate as much as
you like. There is no time limit; stop when you judge the level done.

WHAT YOU MAY NOT DO. Read or write anything outside this project folder: keep your notes, scripts and
logs inside it. Search for, open or use any existing build, code, capture or write-up of this level or
of this experiment, including anything named "Iron Citadel" that was made for it. Use art other than
what is already in the project (no downloaded models or textures; primitives are fine for markers and
invisible helpers). Ask questions: make a call and note it in your report.

HOW THIS RUNS. You are running unattended: nobody will answer, approve or read anything until you
finish, and the session ends as soon as you end your reply. Do the whole job in this one reply. When you
start something long (an editor boot, an import, a bake), wait for it to finish rather than ending your
reply to wait. When you finish, save the scene and close the editor (`unity close .` does not save), so
the project is free for whoever opens it next.

DELIVER:
1. `Assets/Scenes/IronCitadel.unity`: the whole level at true scale (1 unit = 1 metre), with every room
   and feature the brief asks for, ceilings on, and lit so a player can see where to walk, the dark rooms
   included.
2. The first-person controller at the start, in the entry hall. In play mode: walk, sprint, climb the
   stairs, M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names
   the room the player is in and shows elapsed time, and reaching the throne is announced.
3. `Map/map.json`: the level as built, in the form below, for someone who has only the scene. Read every
   position and outline from the saved scene, not from your plan.
4. `Captures/`: one top-down overview of the whole level, one eye-level shot of each room, and one each
   of the armory's guarded entrance from the entry hall, the way into the armory beneath the gallery, the
   hall floor from the gallery's rail, the throne on its stage, and the vault's hidden way in.
5. `Checks/`: the checks and walks you ran, kept so someone else can run them. One entry point (a
   script or an editor method; `Checks/README.md` says which, and how to run it) re-runs every check
   and walk on the saved scene and writes `Checks/results.txt`. Deliver that file as written by its
   last run, on the scene you deliver.
6. A report (below).

THE MAP. Metres, in the scene's world space, y up. An outline is a list of `[x, z]` corners in order.

```json
{
  "start": { "position": [x, y, z], "facing": [x, z] },
  "rooms": [
    { "name": "…", "kind": "…", "state": "…", "floor_y": y, "floor": [ [[x, z], [x, z], [x, z], …] ] }
  ],
  "doors": [
    { "rooms": ["…", "…"], "state": "open | shut | hidden", "position": [x, y, z], "width": w }
  ],
  "gallery": { "deck_y": y, "deck": [ [[x, z], …] ], "rail": [[x, z], …] },
  "stage": { "top_y": y, "outline": [[x, z], …] },
  "throne": [x, y, z],
  "enemies": [ { "kind": "ranged | melee | warlord", "room": "…", "position": [x, y, z], "facing": [x, z] } ],
  "pickups": [ { "kind": "best gear | hoard", "room": "…", "position": [x, y, z] } ]
}
```

- `rooms`: every room, each with a unique `name`. `kind` is the brief's name for it: `entry hall`,
  `armory`, `guard room`, `prison`, `kitchen`, `library`, `alchemist's study`, `forge`, `treasure vault`,
  `dining hall`, `throne room`, `crypt`, `storeroom`, `shrine` or `torture chamber`, or `extra` for a room
  you added. `state` is `decayed`, `looted`, `stocked` or `untouched`. `floor` is the room's floor as one
  or more outlines, at height `floor_y`.
- `doors`: every opening between two rooms, naming both. `open` is a doorway or an open door, `shut` a
  door the player opens, and `hidden` the vault's way in. `position` is the middle of the opening at
  floor level, and `width` its clear width. The scene has no opening between rooms that is not listed.
- `gallery`: its walkway's outline and height, and `rail`, the line of its edge over the hall floor.
- `enemies` and `pickups`: every marker, with the room it stands in.

QUALITY BAR. It should read as a place, not a diagram. Each room should read as its kind from its shape,
height, light and furnishing, as the brief describes. Satisfy every item under "What must be true". If
you measure an item as only partly met, it is not met: change the level until it is, or report it as
not met. Defenders and pickups are markers. Where the brief's words pull two ways, either reading is
acceptable; say which you chose.

CHECKING YOUR WORK. Test the level the way a player will meet it, and check the scene you deliver, not
the one you planned.
- Play-test with the player prefab as shipped. In play mode, walk each of these in one go: the way
  round, from the start to the dining hall without passing through the armory; up to the gallery from
  where the way round comes into the hall, without crossing the hall floor; from the dining hall into the
  armory beneath the gallery, to its best gear; the hidden way into the vault and back; and from the
  dining hall on to the throne. Walk; do not jump.
- Drive the player only through its own controls: move and sprint input, and the E-key script's
  `Interact()`. Turning it to face where it is going is fine. Do not teleport it or move its
  transform, change its CharacterController or controller settings, or add unstick, sidestep or retry
  logic. If it cannot get through somewhere without help, neither can a player: fix the level, not the
  test.
- A walk passes only if the player reaches the end without stalling. If your log records a stall and
  carries on, that walk failed.
- Walk again after the last change. Any edit to the scene after a walk (geometry, props, dressing,
  lights) means making every walk again on the rebuilt scene. `Checks/results.txt` is from that last
  run.
- Make every check able to fail: measure the built scene; never assert a value you meant to build.
- Check each item under "What must be true" against the built scene, and say how you checked it. If you
  could not check one, say so.

THE DESIGNER'S BRIEF: 

# The Iron Citadel Infiltration — level brief

**Premise.** An abandoned dungeon in disarray. The player starts in a safe room and has to reach the warlord,
the level's boss, in the throne room. They are ill-equipped for it: the weapons and armour are in the armory,
and its entrance is heavily guarded. Do they take it head on (and die), or find the long way round for a
surprise attack?

**Where you are.** At the entrance, with the obvious path leading to the guarded armory. Another path exists,
if you can find it.

**The level, in three acts.**

*Act one is the problem: the choke point.* The player comes in at the entry hall, and the obvious way on is
through the armory. The armory's entrance on this side is guarded, but the guards come into view only at its
doorway. The entry hall is where the player sees that entrance.

*Act two is the way round.* Another way leads round the armory, through a wing of rooms that belong together
thematically, to a large dining hall. It does not pass through the armory. A hidden doorway, door or passage
leads to a small treasure vault. Nothing points to it, and it lies on no way through.

*Act three is the gallery, the flank and the throne.* The dining hall is the level's centrepiece: large and
tall, with long tables. It once held great feasts. A gallery runs along one side of it, high above the floor,
and ranged enemies hold it: a player who walks out onto the hall floor takes heavy damage. Instead, the player
can climb to the gallery from where they come in, without crossing the hall floor or being seen, and take out
the ranged enemies there first. From its rail, the player sees much of the floor below and has an advantage
in taking out enemies. Beneath the gallery, a way leads into the armory from behind: a player who takes it
comes in behind the guards watching the entrance, and takes the armory's best gear. Then they cross the hall
and make their way through more rooms to the throne room, where the throne stands on an elevated stage at
the far end.

**The rooms.** One room of each kind below. You may add a few more where your layout needs them (a passage,
an antechamber); say why. The dungeon was abandoned long ago, and its rooms show it: each is either left to
decay or stripped and wrecked by looters. The armory, still stocked, and the hidden vault, which nobody
found, are the exceptions. Each room should read as its kind from its shape, its height, its light and what
fills it, not from a label.

- *Entry hall.* The safe room where the player starts: a guard lobby, long deserted, with benches, weapon
  stands and a table. Standard height.
- *Armory.* Dense rows of weapon racks and armour stands, still fully stocked. Standard height.
- *Guard room.* A guard post, deserted or wrecked in a brawl. Low and dim.
- *Prison.* Low and dark. Cells with barred doors in rows, forced in a riot long over; bedding and furniture
  overturned.
- *Kitchen.* A cold hearth, worktables, barrels and sacks, raided or left to spoil. Low.
- *Library.* Tall bookcases packed close, with reading tables; books pulled down and scattered, or left to
  rot.
- *Alchemist's study.* Benches of glassware, much of it smashed, and shelves of jars, spilled and stained.
- *Forge.* Gone cold: a hearth under a hood whose flue climbs to the ceiling, with anvils, quench barrels and
  tools left where they fell.
- *Treasure vault.* Small, low and sealed. A hoard of chests and coffers, untouched.
- *Dining hall.* As above. Tall and bright, its long tables overturned or still laid under dust, with
  chandeliers hanging from the ceiling and banners in tatters on the walls.
- *Throne room.* Tall, bright, sparse and grand, though fallen: no room is taller, and none is sparser. A torn
  runner leads to the throne.
- *Crypt.* Low and dark. Tombs and burial niches, broken open or collapsed.
- *Storeroom.* Crates, barrels and sacks, plundered or left to rot. Standard height.
- *Shrine.* Tall and still: an altar, candles and statues, desecrated or in ruins.
- *Torture chamber.* Low and dark. A rack, chains and cages, and a brazier long cold.

**What must be true.**

- The armory has two ways in: its guarded entrance, which the player sees from the entry hall, and a way in
  from the dining hall, beneath the gallery.
- A way leads from the entry hall to the dining hall without passing through the armory.
- A gallery runs along one side of the dining hall, high above the floor. The player can reach it from where
  the way round comes into the hall, without crossing the hall floor, and much of the floor can be seen from
  its rail.
- The throne stands on a stage with steps.
- The vault has one way in, and the player may have to search for it: a hidden door, a way behind obstacles,
  or the like.
- The prison, the guard room, the kitchen and the vault are low. The dining hall and the throne room are tall,
  and no room is taller than the throne room.
- Everything is indoors, and every room has a ceiling.
- Every opening between rooms is a door or a doorway, some open and some shut. The player can open any shut
  door on a way through.
- The level has tall and low occluders the player can hide behind, standing or crouching.

**What I don't care about.** How big the level is, and which room sits where. Which layout each room uses, as
long as it reads as its kind. Exact heights, as long as low, standard and tall read as such. Exact counts of
tables, racks and bookcases. The colour of the banners. The seed. Gameplay, animations and NPC behaviour: the
acts say how the level should play, but you build the spaces, not the play.

**Defenders and pickups.** They are markers; nothing moves. Ranged enemies stand behind the armory's guarded
entrance, facing the entry hall, and melee enemies fill the rest of the armory. Ranged enemies also hold the
gallery. The dining hall holds the largest group, and the warlord and guards hold the throne room. Every
other room holds whoever belongs in it, if anyone; the entry hall holds no one. There are two pickups: the
armory's best gear and the vault's hoard.


YOUR REPORT (plain text, under 400 words): (a) what you built and how the level is put together; (b)
which items under "What must be true" you believe are satisfied, which are not (an item you measured as
only partly met is not met), and why; (c) known weaknesses; (d) what you did to check your work: which
walks you made and how, on which build, what failed and what you changed, and how to re-run
`Checks/`; (e) time spent, in total and for each phase (planning, building, dressing and lighting,
checking and fixing), from the clock time you note as you start each one; (f) what you would change in
this prompt, the brief or the setup to get a better level, or the same level faster.
