# Ways 1 and 2: the cold-model runs, in Unity

All three builds are Unity scenes, walked with the same first-person controller: Fable cold, Opus cold, and
our pipeline. A cold run gets the same engine, the same art and the same controller as ours. What it does
not get is the framework: no themed rooms, set pieces, pattern tiles, composer or Builder, and none of
their docs. So the comparison is about the framework, not about Three.js against Unity or primitives
against Synty.

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
2. **One copy per model**, e.g. `C:/Repos/ColdRuns/fable/` and `C:/Repos/ColdRuns/opus/`, run one at a
   time, Opus first (Roger, 2026-10-07). `C:/Repos/IronCitadel/cold/run-cold.ps1` makes the copy and starts
   a fresh headless Claude Code session in it (`claude -p`, effort xhigh as ours, permissions skipped since
   nobody is there to approve). The session gets its own Claude Code config folder
   (`C:/Users/roger/.claude-cold`), so none of Roger's memory, CLAUDE.md, synced skills, plugins or
   transcripts load; MCP servers are off and `ASSET_CORPUS` is unset. The file tools are denied our repos
   (the asset corpus and classification included), `C:/Repos/IronCitadel` and `~/.claude`. Shell commands
   cannot be fenced that way (tested 2026-10-07: wildcard Bash deny rules do not match), so the launcher
   audits every command in the transcript for those paths; a hit is reported in `audit.txt`.
3. **No time limit.** Record wall-clock time, tokens and the API-equivalent cost for all three ways, ours
   included; the launcher writes them to `C:/Repos/IronCitadel/cold/<model>/summary.txt`.

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
>   behind; the next boot then fails with exit 6 and no log. Delete that file between boots.
> - A live `unity command` gives up after 30 seconds whatever `--timeout` says, and commands that need
>   the main thread time out while the editor is busy importing. Run long work with `--detach` and
>   poll it with `unity job`; `unity status` answers even when the editor is busy.
> - On a live editor the options come before the command name and its arguments follow it with no
>   `--` (`unity command --project-path . <name> <args>`); a `--` there silently adds an empty
>   parameter. Headless, it is `unity run . --command <name> -- <args>`.
> - `unity status` with no editor running exits 6 and says "Open Unity and install the Pipeline
>   package". It means nothing is running; the package is installed.
> - For tests use `unity test`, never `-runTests` with `-quit`: the editor quits before the test
>   runner and writes no results.
> - `[Licensing::Client] HandshakeResponse … 505` in the editor log is noise.
>
> WHAT YOU MAY DO. Anything a developer would do inside this project: read and write its files, write
> editor scripts, run the editor in batch mode or open it, enter play mode, take screenshots and look at
> them, search the web for documentation, install packages from the Unity registry, and iterate as much as
> you like. There is no time limit; stop when you judge the level done.
>
> WHAT YOU MAY NOT DO. Read anything outside this project folder. Use art other than what is already in
> the project (no downloaded models or textures; primitives are fine for markers and invisible helpers).
> Ask questions: make a call and note it in your report.
>
> HOW THIS RUNS. You are running unattended: nobody will answer, approve or read anything until you
> finish, and the session ends as soon as you end your reply. Do the whole job in this one reply. When you
> start something long (an editor boot, an import, a bake), wait for it to finish rather than ending your
> reply to wait.
>
> DELIVER:
> 1. `Assets/Scenes/IronCitadel.unity`: the whole level at true scale (4 × 4 tiles of 45 m, 180 m a side,
>    1 unit = 1 metre), every room, door, feature and route in the data file, ceilings on, lit for walking.
> 2. The first-person controller at the start, facing north. In play mode: walk, sprint, climb the stairs,
>    M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names the room
>    the player is in and shows elapsed time, and reaching the throne is announced.
> 3. `Captures/`: one top-down overview of the whole level and one eye-level shot of each room.
> 4. A report (below).
>
> QUALITY BAR. It should read as a place, not a diagram. Each room should read as its kind from its shape,
> height, light and furnishing, as the brief describes. Satisfy every item in `must_be_true`; where you
> cannot, say so. Do not invent extra doors or openings. Defenders and pickups are markers.
>
> THE DESIGNER'S BRIEF: *[brief.md pasted in full]*
>
> THE DATA FILE (level.json): *[level.json pasted in full]*
>
> YOUR REPORT (plain text, under 300 words): (a) what you built and how the level is put together; (b)
> which `must_be_true` items you believe are satisfied, which are not, and why; (c) known weaknesses; (d)
> what you did to check your work (play mode, screenshots, scripts); (e) time spent.
