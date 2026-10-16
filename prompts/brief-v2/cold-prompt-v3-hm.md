# Ways 1 and 2: the cold-model run, in Unity, on the mansion (prompt v3-hm)

**Status, 2026-10-14 (Roger).** The mansion variant of v3: the same task prompt, run on Synty's POLYGON Horror
Mansion pack and on the mansion brief (`brief-v2-mansion.md`, beside this file). Roger, 2026-10-14: "Let's see
what the mansion data would look like with the v3 prompt and our framework", with the brief re-themed for a
mansion, 3 m storeys for ours, and this cold run in parallel. Ours is built from the same brief and the same
floor plan as ours v2.

## What differs from v3

Only what the art and the re-themed brief force; everything else in the task prompt is v3's, word for word.

1. **The art.** THE SETUP names one pack, Synty POLYGON Horror Mansion, in place of the three dungeon packs, and
   the opening line says "a commercial art pack". As in v1 to v3, PolygonGeneric stays in the project unnamed: the
   pack's materials use its shaders.
2. **The brief.** `brief-v2-mansion.md` is brief v2 with each room re-themed and its role kept: the choke, the
   way round, the gallery, the flank, the stage and the hidden room are v2's, in the same words.

   | v2 | mansion |
   |---|---|
   | entry hall | foyer |
   | armory | gun room |
   | guard room | watch room |
   | prison | cellar cells |
   | alchemist's study | laboratory |
   | forge | boiler room |
   | treasure vault | strong room |
   | throne room, throne | ballroom, the master's chair |
   | shrine | chapel |
   | torture chamber | butcher's room |
   | warlord | the master |

   The kitchen, library, dining hall, crypt and storeroom keep their names.
3. **The names the prompt uses** follow the brief: the start room, the five captures, the five walks, the map's
   `kind` list, `chair` for `throne` and `master` for `warlord` in the map. The kit reads both sets of names.
4. **Unchanged on purpose:** the scene path `Assets/Scenes/IronCitadel.unity` and the player prefab
   `Assets/Interact/IronCitadelPlayer.prefab` (the launcher and the kit read them), and the fence line about this
   experiment.

## Setup

- **The template**, `C:/Repos/ColdRuns/_template-hm/`, made 2026-10-14 from today's `_template` (the v3
  template: post-processing on the player camera, Cinema Suite), without `Library/`, and without
  `Synty/PolygonDungeon`, `PolygonDungeonMap` and `PolygonDungeonRealms`. `Synty/PolygonHorrorMansion` was copied
  from the Builder with its `.meta` files (URP materials, same GUIDs), then imported once in batch mode.
- **The launch**, from a terminal or a background shell. Name the billing and get a yes first: past Opus runs cost
  $22 to $30 at API list price.

```
pwsh -NoProfile -File C:\Repos\Github\Crawfis-Software\IronCitadel\cold\run-cold.ps1 -Model opus -Billing Subscription `
    -Template C:\Repos\ColdRuns\_template-hm -PromptFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\cold-prompt-v3-hm.md `
    -BriefFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\brief-v2-mansion.md -Name opus-v3-hm -OutDir C:\Repos\Github\Crawfis-Software\IronCitadel\cold\v3-hm\opus
```

## The task prompt (verbatim; paste brief v2 where marked)

> You are taking part in an experiment: "how good a level can a frontier model build in Unity from a
> designer's brief, using a commercial art pack but no level-generation framework."
>
> THE SETUP. This folder is a Unity 6 project (URP) with the Synty POLYGON Horror Mansion pack already
> imported, Cinema Suite's Cinema Themes colour-grading
> LUTs, the Input System, AI Navigation, and Unity's Starter Assets first-person controller, ready to place
> as `Assets/Interact/IronCitadelPlayer.prefab`. Its E key casts a 3 m ray from the camera and calls
> `SendMessageUpwards("Interact")` on the first solid (non-trigger) collider it hits, so anything the
> player opens, such as a secret door, needs a collider and a `void Interact()` method on that object or a
> parent. The player's size, movement and controls are fixed. How the level looks through its camera
> (lighting, post-processing, volumes, fog) is yours to set. The brief is pasted below, and is also in
> `Brief/brief.md`. The `unity` command-line tool drives the editor, and the Unity Pipeline package is
> installed: `unity open` starts the editor, `unity command`
> runs editor commands on it, and `unity run --command` runs one headless. `unity command` with no
> arguments lists the command groups (scenes, game objects, scripts and running C#, captures, play
> mode, the console, and more); `--tag <group>` lists a group's commands, and `--query <word>` searches
> them. `UNITY_PROJECT_PATH` is set to this folder, so `unity command` and `unity job` find this
> project's editor without `--project-path`. Unity's agent skills are installed: its Claude Code
> plugin (`unity:unity-cli` and the rest) and `unity-pipeline`.
>
> UNITY CLI PITFALLS, found the hard way on this machine (CLI 1.0.0-beta.8 to beta.13, the version
> installed now):
> - `unity open .` returns as soon as the editor is launching; then
>   `unity status --until-ready --project-path .` waits until this project's editor answers (up to
>   300 s; add `--timeout <seconds>` for longer). `unity open` and `unity close` take the project's
>   path as an argument (`unity open .`); they reject `--project-path`.
> - While the editor is still importing or compiling, `unity status` can show `ready` while commands
>   are still refused. A command then waits out the CLI's `--timeout` (30 s by default) and fails with
>   "did not accept '<name>' within 30s. It was last busy (settling)". That is the editor, not your
>   code: send it again, or give it a longer `--timeout`.
> - `unity run` runs the editor in batch mode and reserves `-quit`, `-batchmode` and `-projectPath`:
>   passing any of them exits 6 and launches nothing. Editor flags go after `--`:
>   `unity run . -- -executeMethod My.Tool.Run -logFile run.log`.
> - Exit code 2 means the command line was wrong (an unknown option, say) and nothing ran. Exit code 6
>   covers most other failures: a failed command, a reserved flag, a project not found, no editor
>   running. Treat any non-zero exit as a failure. Judge success by the files your method writes and
>   by the log's tail, not by the exit code alone; the log's `ExitCode:` line only says whether your
>   method returned without throwing.
> - One editor per project. A batch run needs the project's lock, so it fails while an editor has the
>   project open. Close the editor first (`unity close .`, which does not save) or use `unity command`.
> - `--timeout` on `unity run` or `unity test` kills the editor and can leave `Temp/UnityLockfile`
>   behind; the next boot then fails with exit 6 and no log. Only in that case, and only once
>   `unity status` shows no editor on this project, delete that file. Never delete it while an editor
>   has the project open.
> - `unity command` takes a command's main argument bare (`unity command eval "return 1;"`). Its other
>   parameters are named and go after `--`:
>   `unity command capture_game_view -- --source camera --save_path Captures/a.png`. The CLI's own
>   options (`--project-path`, `--timeout`, `--detach`, `--json`) go anywhere before the `--`, so a
>   `--timeout` before the `--` is the CLI's wait, in seconds, and one after it is the command's own
>   `timeout` parameter. Headless, it is `unity run . --command <name> -- --<parameter> <value>`.
>   `unity job` takes its subcommand first (`unity job wait <id>`, `unity job status <id> --json`);
>   `unity job --project-path . wait` is an error.
> - On a live editor, `eval` and `eval_file` stop after 5 seconds of work ("Main thread operation timed
>   out after 5000ms") unless you pass their own `timeout`, in milliseconds:
>   `unity command eval -- --code '<C#>' --timeout 20000`. The CLI also stops waiting for any command
>   after its own `--timeout`, 30 s by default; the editor carries on with that command, and the next
>   one waits behind it. For longer work, write a `.cs` file outside `Assets/` and run a static method
>   in it with `run_script`, which compiles the file in memory and runs it, with no import and no
>   reload. Its own `timeout_ms` is 30000 by default, so raise both:
>   `unity command --timeout 300 run_script -- --file Tools/Job.cs --entry Job.Run --timeout_ms 290000`.
>   Or put `--detach` before the command name: it prints a job id at once, and `unity job wait <id>`
>   waits for the job.
> - After a script under `Assets/` changes, the editor recompiles and reloads. Until it has reloaded,
>   commands fail at once with "Network error: An error occurred while sending the request". That is
>   the reload, not your code: wait with `unity status --until-ready --project-path .`, then send the
>   command again. `unity recompile --project-path .` compiles at once and prints any compile errors.
> - With no editor running, `unity command` exits 6 and asks you to make sure the editor is running
>   "with the Pipeline package installed", and `unity status` prints an empty table and exits 6. Both
>   mean nothing is running; the package is installed.
> - `--json`, `--format json` and `--result-only` print indented JSON, with a space after each colon
>   (`"compiling": false`). Run a command once and read its output before you write a loop or a parser
>   around it; a loop whose test never matches just runs until its cap.
> - `eval` and `eval_file` compile your code as the body of a method: no `using` lines. Write full type
>   names, and call extension methods through their static class. A `run_script` file is a whole C#
>   file and may have them.
> - In play mode, `simulate_key` reaches the controller's input actions (W moves the player), but code
>   that reads `Keyboard.current` does not see it. The E-key script reads `Keyboard.current`; to use E
>   from a script, stand the player where a player would stand and call that script's `Interact()`,
>   which casts the same ray.
> - `capture_game_view` captures the Game view window by default, overlay UI included, and fails with
>   "No Game view render target to capture" when none is open;
>   `UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Game")` opens one.
>   `-- --source camera` renders `Camera.main`, or else the first enabled camera, without overlay UI,
>   and fails with "No camera found to capture" when the open scene has neither. Every `--save_path`
>   lands under `Assets/`: `X/y.png`, `Assets/X/y.png` and `<this folder>/X/y.png` all save to
>   `Assets/X/y.png`, and a path outside the project, or with `..`, is refused. Unity then imports the
>   file: move it out and delete its `.meta`.
> - Renders from a batch-mode editor came out with garbage colours (solid red squares) on materials
>   created earlier in the same session; the same scene rendered in an open editor was clean. Judge how
>   the level looks from an open editor's renders.
> - For tests use `unity test`, never `-runTests` with `-quit`: the editor quits before the test
>   runner and writes no results.
> - `[Licensing::Client] HandshakeResponse … 505` in the editor log is noise.
> - `Library/` holds several GB of import cache. Leave it out of listings, searches and `du`.
> - Write and change source files with your file tools (write and edit). Shell heredocs and inline
>   scripts break on quoting in this shell, and a script that rewrites source turns a `\n` meant for a
>   C# string into a real line break, which does not compile.
>
> WHAT YOU MAY DO. Anything a developer would do inside this project: read and write its files, write
> editor scripts, run the editor in batch mode or open it, enter play mode, take screenshots and look at
> them, search the web for documentation, install packages from the Unity registry, and iterate as much as
> you like. There is no time limit; stop when you judge the level done.
>
> WHAT YOU MAY NOT DO. Read or write anything outside this project folder: keep your notes, scripts and
> logs inside it. Search for, open or use any existing build, code, capture or write-up of this level or
> of this experiment, including anything named "Iron Citadel" that was made for it. Use art other than
> what is already in the project (no downloaded models or textures; primitives are fine for markers and
> invisible helpers). Ask questions: make a call and note it in your report.
>
> HOW THIS RUNS. You are running unattended: nobody will answer, approve or read anything until you
> finish, and the session ends as soon as you end your reply. Do the whole job in this one reply. When you
> start something long (an editor boot, an import, a bake), wait for it to finish rather than ending your
> reply to wait. When you finish, save the scene and close the editor (`unity close .` does not save), so
> the project is free for whoever opens it next.
>
> DELIVER:
> 1. `Assets/Scenes/IronCitadel.unity`: the whole level at true scale (1 unit = 1 metre), with every room
>    and feature the brief asks for, ceilings on, and lit so a player can see where to walk, the dark rooms
>    included.
> 2. The first-person controller at the start, in the foyer. In play mode: walk, sprint, climb the
>    stairs, M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names
>    the room the player is in and shows elapsed time, and reaching the master's chair is announced.
> 3. `Map/map.json`: the level as built, in the form below, for someone who has only the scene. Read every
>    position and outline from the saved scene, not from your plan.
> 4. `Captures/`: one top-down overview of the whole level, one eye-level shot of each room, and one each
>    of the gun room's guarded entrance from the foyer, the way into the gun room beneath the gallery, the
>    hall floor from the gallery's rail, the master's chair on its stage, and the strong room's hidden way
>    in.
> 5. `Checks/`: the checks and walks you ran, kept so someone else can run them. One entry point (a
>    script or an editor method; `Checks/README.md` says which, and how to run it) re-runs every check
>    and walk on the saved scene and writes `Checks/results.txt`. Deliver that file as written by its
>    last run, on the scene you deliver.
> 6. A report (below).
>
> THE MAP. Metres, in the scene's world space, y up. An outline is a list of `[x, z]` corners in order.
>
> ```json
> {
>   "start": { "position": [x, y, z], "facing": [x, z] },
>   "rooms": [
>     { "name": "…", "kind": "…", "state": "…", "floor_y": y, "floor": [ [[x, z], [x, z], [x, z], …] ] }
>   ],
>   "doors": [
>     { "rooms": ["…", "…"], "state": "open | shut | hidden", "position": [x, y, z], "width": w }
>   ],
>   "gallery": { "deck_y": y, "deck": [ [[x, z], …] ], "rail": [[x, z], …] },
>   "stage": { "top_y": y, "outline": [[x, z], …] },
>   "chair": [x, y, z],
>   "enemies": [ { "kind": "ranged | melee | master", "room": "…", "position": [x, y, z], "facing": [x, z] } ],
>   "pickups": [ { "kind": "best gear | hoard", "room": "…", "position": [x, y, z] } ]
> }
> ```
>
> - `rooms`: every room, each with a unique `name`. `kind` is the brief's name for it: `foyer`,
>   `gun room`, `watch room`, `cellar cells`, `kitchen`, `library`, `laboratory`, `boiler room`, `strong room`,
>   `dining hall`, `ballroom`, `crypt`, `storeroom`, `chapel` or `butcher's room`, or `extra` for a room
>   you added. `state` is `decayed`, `looted`, `stocked` or `untouched`. `floor` is the room's floor as one
>   or more outlines, at height `floor_y`.
> - `doors`: every opening between two rooms, naming both. `open` is a doorway or an open door, `shut` a
>   door the player opens, and `hidden` the strong room's way in. `position` is the middle of the opening at
>   floor level, and `width` its clear width. The scene has no opening between rooms that is not listed.
> - `gallery`: its walkway's outline and height, and `rail`, the line of its edge over the hall floor.
>   `chair` is the master's chair.
> - `enemies` and `pickups`: every marker, with the room it stands in.
>
> QUALITY BAR. It should read as a place, not a diagram. Each room should read as its kind from its shape,
> height, light and furnishing, as the brief describes. Satisfy every item under "What must be true". If
> you measure an item as only partly met, it is not met: change the level until it is, or report it as
> not met. Defenders and pickups are markers. Where the brief's words pull two ways, either reading is
> acceptable; say which you chose.
>
> CHECKING YOUR WORK. Test the level the way a player will meet it, and check the scene you deliver, not
> the one you planned.
> - Play-test with the player prefab as shipped. In play mode, walk each of these in one go: the way
>   round, from the start to the dining hall without passing through the gun room; up to the gallery from
>   where the way round comes into the hall, without crossing the hall floor; from the dining hall into the
>   gun room beneath the gallery, to its best gear; the hidden way into the strong room and back; and from
>   the dining hall on to the master's chair. Walk; do not jump.
> - Drive the player only through its own controls: move and sprint input, and the E-key script's
>   `Interact()`. Turning it to face where it is going is fine. Do not teleport it or move its
>   transform, change its CharacterController or controller settings, or add unstick, sidestep or retry
>   logic. If it cannot get through somewhere without help, neither can a player: fix the level, not the
>   test.
> - A walk passes only if the player reaches the end without stalling. If your log records a stall and
>   carries on, that walk failed.
> - Walk again after the last change. Any edit to the scene after a walk (geometry, props, dressing,
>   lights) means making every walk again on the rebuilt scene. `Checks/results.txt` is from that last
>   run.
> - Make every check able to fail: measure the built scene; never assert a value you meant to build.
> - Check each item under "What must be true" against the built scene, and say how you checked it. If you
>   could not check one, say so.
>
> THE DESIGNER'S BRIEF: *[brief.md pasted in full]*
>
> YOUR REPORT (plain text, under 400 words): (a) what you built and how the level is put together; (b)
> which items under "What must be true" you believe are satisfied, which are not (an item you measured as
> only partly met is not met), and why; (c) known weaknesses; (d) what you did to check your work: which
> walks you made and how, on which build, what failed and what you changed, and how to re-run
> `Checks/`; (e) time spent, in total and for each phase (planning, building, dressing and lighting,
> checking and fixing), from the clock time you note as you start each one; (f) what you would change in
> this prompt, the brief or the setup to get a better level, or the same level faster.
