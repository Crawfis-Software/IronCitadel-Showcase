# Ways 1 and 2: the cold-model runs, in Unity (prompt v2)

All three builds are Unity scenes, walked with the same first-person controller: Fable cold, Opus cold, and
our pipeline. A cold run gets the same engine, the same art and the same controller as ours. What it does
not get is the framework: no themed rooms, set pieces, pattern tiles, composer or Builder, and none of
their docs. So the comparison is about the framework, not about Three.js against Unity or primitives
against Synty.

This file replaces v1 (`UnityAssets/docs/research/AI-DungeonLevelComparison/cold-prompt.md`). `brief.md`
and `level.json` are unchanged. The evidence behind each change is in `C:/Repos/IronCitadel/cold/prompt-lessons.md`.
That file cites calls as `#n` (the n-th tool call in that run's `stream.jsonl`) and times as tM (minutes
into the run).

## Changes from v1

Each change is tagged:
- **friction**: time lost to the environment that tells us nothing about level design;
- **verification**: how the run checks its own work;
- **clarity**: a reading the runs had to guess.

**Skeptic** marks a change someone could call coaching toward the scorer, with the reason it stays.

1. **friction.** Do not pipe `unity open`. Opus's `unity open . 2>&1 | tail -5` hung for 180 s (#12) because
   the editor holds the pipe open.
2. **friction.** The lockfile pitfall now says to delete `Temp/UnityLockfile` only after a killed run, and
   never while an editor is up. Fable read v1's "delete that file between boots" as routine and deleted it
   before 8 boots.
3. **friction.** The option-order pitfall is scoped to `unity command`, and `unity job wait <id>
   --project-path .` is given. Opus's `unity job --project-path . wait` failed with "error: unknown option
   '--project-path'" (#81).
4. **friction.** The CLI prints indented JSON, so look at a probe's output before looping on it. Fable's three
   recompile waits tested for `"compiling":false` and each ran to its 90-try cap: about 12 min (#131, #166,
   #181).
5. **friction.** `eval` compiles a method body, so no `using` lines. Fable #158-#159.
6. **friction.** `simulate_key` is seen by the controller's input actions but not by `Keyboard.current`, which
   the E-key script reads, so call its `Interact()` instead. Opus lost 1.2 min to this (#140-#145), and both
   runs ended up calling `Interact()`.
7. **friction.** `capture_game_view --save_path` always writes under `Assets/`. Both runs hit it (Opus
   #134-#137, Fable #161-#163, #186).
8. **friction.** Renders from batch mode showed garbage colours on new materials, so judge looks from an open
   editor. Fable spent about 10 min on this (t50.4-t60.2).
9. **friction.** Leave `Library/` out of scans. Fable's `du -sh Library` timed out after 120 s (#19).
10. **friction.** Write files with the file tool, not shell heredocs. This failed three times (Opus #116 and
    #127, Fable #32).
11. **friction.** At the end, save the scene and close the editor. Fable left it open, so it had to be scored
    on a copy.
12. **clarity.** Write nothing outside the project folder. Both runs wrote notes into the cold config folder's
    memory, and a re-run at the same path would load them.
13. **clarity.** "Lit for walking" becomes "lit so a player can see where to walk, the dark rooms included".
    Both runs listed their dark rooms as a weakness, unsure whether the dark was wanted.
14. **clarity.** Where the brief pulls two ways, either reading is acceptable; say which you chose. Both runs
    shrank the "small" kitchen and vault inside their tiles; Fable counted that as failing "fills its own
    tile".
    - **Skeptic:** this tells the run it will not be marked down for the reading it picks. That is true of the
      scorer, but it reveals no measure or threshold.
    - **Roger to confirm the intent.** If rooms must fill their tiles, replace the line with that statement.
15. **verification.** A new CHECKING YOUR WORK section.
    - It asks the run to:
      - walk every route in `level.json` end to end, in play mode, with the player prefab as shipped and no
        assists;
      - after the last change to the scene;
      - up and down both gallery stairs, and into the vault through the secret door;
      - treat a stalled walk as failed;
      - make every check able to fail;
      - check each `must_be_true` item against the built scene.
    - The runs' gaps it answers:
      - neither walked every route;
      - Opus teleported between short segments;
      - Fable's driver strafed itself unstuck and never entered the east wing;
      - both dressed after their last walk;
      - Opus's heights check was `Check(true, …)`.
    - **Skeptic, on walking every route and both stairs:** the scorer also walks routes and stairs. Kept,
      because:
      - the routes are in the data file the run is given, and both stairs are deliverables;
      - the instruction names no check, no method, no agent size and no threshold;
      - it is the play-test any developer would run before handing over a level.
    - **Skeptic, on "do not jump":** it pins "walk" to its plain meaning, so a jump over a lip does not count as
      a stair that works. Kept for that reason.
16. **verification.** Report item (d) now asks which routes were walked, how, on which build, and what failed.
    This is for our record and adds no check.

**Considered and left out**, because a skeptic would rightly call them coaching or hints from our side:
- Suggesting a NavMesh bake to check connectivity. That is the scorer's own method. Opus did it unprompted.
- Stating the controller's radius, height or step. They are in the prefab the run is given; restating them
  points at the scorer's walker.
- Saying what "the whole hall floor" means at the rail, or where the eye is. That would define a scored measure.
- "Keep props out of doorways and off the walking line." That names Fable's actual failure; the walk-after-the-
  last-change rule covers it in general terms.
- The Synty module sizes and which wall and stair pieces fit. That is knowledge our framework encodes, and the
  art survey (Opus 21 min, Fable 11 min) is part of what is measured.
- Anything about the kit, its checks or its numbers.

**Comparability.** v2 removes about 6 min (Opus) and 27 min (Fable) of measured friction, and asks for more
checking. Its times and scores are not comparable with v1's, so compare v2 runs with each other.

## Setup (once, before any run)

1. **A template project outside `Crawfis-Software/`**, e.g. `C:/Repos/ColdRuns/_template/`. Nothing under
   that parent may hold a `CLAUDE.md`, `AGENTS.md` or `.claude/` that names the pipeline.
   - Unity `6000.6.0f1` (the version in this project's `ProjectVersion.txt`), the URP 3D template.
   - Input System and AI Navigation (`com.unity.ai.navigation`), the same versions the Builder uses.
   - Synty POLYGON Dungeons, Dungeon Realms and Fantasy Dungeon Map. These releases use Synty's Shader
     Graph materials, so there is no URP upgrade package to import.
   - The first-person controller we walk our level with: Unity's Starter Assets (the Asset Store package
     "First Person Third Person Character Controllers", which adds Cinemachine). Our E-key add-on,
     `Assets/Interact/`, turns it into the prefab `Assets/Interact/IronCitadelPlayer.prefab`. The source is
     `C:/Repos/IronCitadel/kit/walker/`.
   - The Unity Pipeline package (`com.unity.pipeline` 0.8.0-exp.1, the Builder's version), so the `unity`
     CLI can drive the editor: `unity command` on an open editor, `unity run --command` headless.
   - Nothing from `Assets/CrawfisSoftware/`, `Assets/MacroTileBuilder/` or `Assets/Synth*/`.
2. **One copy per model**, run one at a time.
   - `C:/Repos/IronCitadel/cold/run-cold.ps1` makes the copy and starts a fresh headless Claude Code session in
     it (`claude -p`, effort xhigh as ours, permissions skipped since nobody is there to approve).
   - The session gets its own Claude Code config folder (`C:/Users/roger/.claude-cold`), so none of Roger's
     memory, CLAUDE.md, synced skills, plugins or transcripts load.
   - MCP servers are off and `ASSET_CORPUS` is unset.
   - The file tools are denied our repos (the asset corpus and classification included), `C:/Repos/IronCitadel`
     and `~/.claude`.
   - Shell commands cannot be fenced that way. Wildcard Bash deny rules do not match (tested 2026-10-07), so
     the launcher audits every command in the transcript for those paths and reports any hit in `audit.txt`.
3. **No time limit.** Record wall-clock time, tokens and the API-equivalent cost for all three ways, ours
   included. The launcher writes them to `summary.txt` in its output folder.
4. **For a v2 run, on top of the above:**
   - Move `C:/Users/roger/.claude-cold/projects/C--Repos-ColdRuns-*` (both v1 runs wrote memory notes there)
     out of `.claude-cold`, so the new session does not load them.
   - Move the v1 builds out of `C:/Repos/ColdRuns/<model>`; the launcher refuses an existing copy. Move the
     scorer's copies (`ColdRuns/opus-score`, `ColdRuns/fable-score`) too: the fence denies only the other
     model's `ColdRuns/<model>` and `_template`. Put them somewhere the session's file tools are denied, such
     as under `C:/Repos/IronCitadel/`.
   - Run with `-PromptFile C:\Repos\IronCitadel\cold\cold-prompt-v2.md -OutDir C:\Repos\IronCitadel\cold\v2\<model>`,
     so the v1 record in `cold/<model>/` is not overwritten.

## The task prompt (verbatim; paste `brief.md` and `level.json` where marked)

> You are taking part in an experiment: "how good a level can a frontier model build in Unity from a
> designer's brief and a data file, using a commercial art pack but no level-generation framework."
>
> THE SETUP. This folder is a Unity 6 project (URP) with three Synty POLYGON dungeon packs already
> imported (Dungeons, Dungeon Realms, Fantasy Dungeon Map), the Input System, AI Navigation, and Unity's
> Starter Assets first-person controller, ready to place as `Assets/Interact/IronCitadelPlayer.prefab`.
> Its E key casts a 3 m ray from the camera and calls `SendMessageUpwards("Interact")` on the first solid
> (non-trigger) collider it hits, so anything the player opens, such as a secret door, needs a collider
> and a `void Interact()` method on that object or a parent. The `unity` command-line tool drives the
> editor, and the Unity Pipeline package is installed: `unity open` starts the editor, `unity command`
> runs editor commands on it (with no arguments it lists them: running C#, the scene hierarchy, game- and
> scene-view captures, play mode, the console), and `unity run --command` runs one headless.
> `unity skill show` prints Unity's guide to the CLI.
>
> UNITY CLI PITFALLS, found the hard way on this machine (CLI 1.0.0-beta.8 to beta.11):
> - `unity open .` returns as soon as the editor is launching. Do not pipe its output (`| tail`,
>   `| head`): the editor inherits the pipe and holds it open, so the command hangs until the editor
>   exits. Send its output to a file, then poll `unity status` until this project shows `ready`.
> - `unity run` runs the editor in batch mode and reserves `-quit`, `-batchmode` and `-projectPath`.
>   Passing any of them exits 6 and launches nothing, although `unity run --help`'s own examples pass
>   `-quit`. Editor flags go after `--`: `unity run . -- -executeMethod My.Tool.Run -logFile run.log`.
> - Exit code 6 covers many failures: project not found, a reserved flag, bad parameters, a failed
>   command, or `unity status` with no editor running. Treat any non-zero exit as a failure. Judge
>   success by the files your method writes and by the log's tail, not by the exit code alone; the
>   log's `ExitCode:` line only says whether your method returned without throwing.
> - One editor per project. A batch run needs the project's lock, so it fails while an editor has the
>   project open. Close the editor first (`unity close`, which does not save) or use `unity command`.
> - `--timeout` on `unity run` or `unity test` kills the editor and can leave `Temp/UnityLockfile`
>   behind; the next boot then fails with exit 6 and no log. Only in that case, and only once
>   `unity status` shows no editor on this project, delete that file. Never delete it while an editor
>   has the project open.
> - A live `unity command` gives up after 30 seconds whatever `--timeout` says, and commands that need
>   the main thread time out while the editor is busy importing. Run long work with `--detach` and
>   wait for it with `unity job wait <id> --project-path .`; `unity status` answers even when the
>   editor is busy.
> - For `unity command` on a live editor, the options come before the command name and its arguments
>   follow it with no `--` (`unity command --project-path . <name> <args>`); a `--` there silently adds
>   an empty parameter. Headless, it is `unity run . --command <name> -- <args>`. `unity job` is the
>   other way round: the subcommand comes first (`unity job wait <id> --project-path .`,
>   `unity job status <id> --json`), and `unity job --project-path . wait` is an error.
> - `unity status` with no editor running exits 6 and says "Open Unity and install the Pipeline
>   package". It means nothing is running; the package is installed.
> - `--json`, `--format json` and `--result-only` print indented JSON, with a space after each colon
>   (`"compiling": false`). Run a status command once and read its output before you write a loop that
>   waits on it; a loop whose test never matches just runs until its cap.
> - `eval` and `eval_file` compile your code as the body of a method: no `using` lines. Write full type
>   names, and call extension methods through their static class.
> - In play mode, `simulate_key` reaches the controller's input actions (W moves the player), but code
>   that reads `Keyboard.current` does not see it. The E-key script reads `Keyboard.current`; to use E
>   from a script, stand the player where a player would stand and call that script's `Interact()`,
>   which casts the same ray.
> - `capture_game_view --save_path` writes under `Assets/` whatever path you give (`X/y.png` lands at
>   `Assets/X/y.png`, an absolute path included), and Unity then imports it. Move the file out and
>   delete its `.meta`.
> - Renders from a batch-mode editor came out with garbage colours (solid red squares) on materials
>   created earlier in the same session; the same scene rendered in an open editor was clean. Judge how
>   the level looks from an open editor's renders.
> - For tests use `unity test`, never `-runTests` with `-quit`: the editor quits before the test
>   runner and writes no results.
> - `[Licensing::Client] HandshakeResponse … 505` in the editor log is noise.
> - `Library/` holds several GB of import cache. Leave it out of listings, searches and `du`.
> - Long shell heredocs and inline scripts break on quoting in this shell. Write source files with
>   your file-writing tool.
>
> WHAT YOU MAY DO. Anything a developer would do inside this project: read and write its files, write
> editor scripts, run the editor in batch mode or open it, enter play mode, take screenshots and look at
> them, search the web for documentation, install packages from the Unity registry, and iterate as much as
> you like. There is no time limit; stop when you judge the level done.
>
> WHAT YOU MAY NOT DO. Read or write anything outside this project folder: keep your notes, scripts and
> logs inside it. Use art other than what is already in the project (no downloaded models or textures;
> primitives are fine for markers and invisible helpers). Ask questions: make a call and note it in your
> report.
>
> HOW THIS RUNS. You are running unattended: nobody will answer, approve or read anything until you
> finish, and the session ends as soon as you end your reply. Do the whole job in this one reply. When you
> start something long (an editor boot, an import, a bake), wait for it to finish rather than ending your
> reply to wait. When you finish, save the scene and close the editor (`unity close` does not save), so the
> project is free for whoever opens it next.
>
> DELIVER:
> 1. `Assets/Scenes/IronCitadel.unity`: the whole level at true scale (4 × 4 tiles of 45 m, 180 m a side,
>    1 unit = 1 metre), every room, door, feature and route in the data file, ceilings on, and lit so a
>    player can see where to walk, the dark rooms included.
> 2. The first-person controller at the start, facing north. In play mode: walk, sprint, climb the stairs,
>    M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names the room
>    the player is in and shows elapsed time, and reaching the throne is announced.
> 3. `Captures/`: one top-down overview of the whole level and one eye-level shot of each room.
> 4. A report (below).
>
> QUALITY BAR. It should read as a place, not a diagram. Each room should read as its kind from its shape,
> height, light and furnishing, as the brief describes. Satisfy every item in `must_be_true`; where you
> cannot, say so. Do not invent extra doors or openings. Defenders and pickups are markers. Where the
> brief's words pull two ways (a room that fills its tile but is called small, say), either reading is
> acceptable; say which you chose.
>
> CHECKING YOUR WORK. Test the level the way a player will meet it, and check the scene you deliver, not
> the one you planned.
> - Play-test with the player prefab exactly as shipped. In play mode, walk every route in the data file
>   from its first cell to its last in one go, walk up and down both gallery stairs, open the secret door
>   and walk into the vault and back, and confirm that the blocked route stays blocked. Walk; do not jump.
> - Drive the player only through its own controls: move and sprint input, and the E-key script's
>   `Interact()`. Turning it to face where it is going is fine. Do not teleport it or move its
>   transform, change its CharacterController or controller settings, or add unstick, sidestep or retry
>   logic. If it cannot get through somewhere without help, neither can a player: fix the level, not the
>   test.
> - A walk passes only if the player reaches the end without stalling. If your log records a stall and
>   carries on, that walk failed.
> - Walk again after the last change. Any edit to the scene after a walk (geometry, props, dressing,
>   lights) means walking every route again on the rebuilt scene.
> - Make every check able to fail: measure the built scene; never assert a value you meant to build.
> - Check each `must_be_true` item against the built scene, and say how you checked it. If you could not
>   check one, say so.
>
> THE DESIGNER'S BRIEF: *[brief.md pasted in full]*
>
> THE DATA FILE (level.json): *[level.json pasted in full]*
>
> YOUR REPORT (plain text, under 300 words): (a) what you built and how the level is put together; (b)
> which `must_be_true` items you believe are satisfied, which are not, and why; (c) known weaknesses; (d)
> what you did to check your work: which routes you walked and how, on which build, what failed and what
> you changed, plus screenshots and scripts; (e) time spent.
