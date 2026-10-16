# Ways 1 and 2: the cold-model runs, in Unity (prompt v3.1)

**v3.1, 2026-10-14 (Roger).** v3 with one change, 19 below: the art rule now closes the gap v3 left (meshes
built in code, modelling tools, Starter Assets' grey blocks) while still allowing primitives for markers. Roger:
"replace all results in the talk with only the v3 prompt so we can compare apples to apples", and asked whether
v3 "include[s] language that says you can only use the packages given so Fable doesn't invent something". It
runs Fable and Haiku (Roger, 2026-10-14: Max plan, `xhigh`). Opus v3 is not re-run: its scene already meets
v3.1 (change 19). Everything below this paragraph is v3's text except the title, change 19, the launch lines and
one sentence of the task prompt.

**Status, 2026-10-13 (Roger).** v3 goes with brief v2 (`brief-v2-draft.md`, beside this file) for a follow-on talk
or future work, and runs on Opus only. Ours for brief v2 was designed with PCGTools (`pcg-level-prompt-v2.md`) and
run on 2026-10-13 (`../v2/REPORT.md`); `warm-prompt-v1.md` is shelved. The task prompt was reworked for brief v2 on
2026-10-13 (changes 14 to 18): no data file and no grid, and the run hands back a map of what it built. Roger,
2026-10-13: "Do the opus run".

Every build is a Unity scene walked with the same first-person controller. A cold run gets the same engine, the
same art and the same controller as ours. What it does not get is the framework: no themed rooms, set pieces,
pattern tiles, composer, Builder or PCGTools, and none of their docs. Ours for brief v2 ran from
`pcg-level-prompt-v2.md`, beside this file: the same brief, the same walks and captures, and the same asks for
timing and suggestions.

This file replaces v2 (`cold-prompt-v2.md`). Changes 1 to 13 come from the four runs since v2 was written:

| Label | Model | Art | Prompt | Record | Score |
|---|---|---|---|---|---|
| R3 | Haiku | Synty | v1 | `cold/haiku/` | 7 / 3 / 1 (10 / 0 / 1 with box steps) |
| R4 | Opus | Remesh, own meshes allowed | v2 | `cold/v2-remesh/opus/` | 8 / 2 / 1 |
| R5 | Opus | Remesh, pack only | v2 | `cold/v2-remesh-packonly/opus/` | 8 / 2 / 1 |
| R6 | Fable | Remesh, pack only | v2 | `cold/v2-remesh-packonly/fable/` | 10 / 0 / 1 |

Scores are in `scores/{haiku,opus-remesh,opus-remesh-packonly,fable-remesh-packonly}/`. `R5 #142` is the 142nd
tool call in R5's `stream.jsonl`, counting from 0; `t48.5` is 48.5 minutes into the run.

## What v2 fixed, and keeps

- R4, R5 and R6 each walked every route again on the final build, and each closed the editor at the end.
- No v2 run piped `unity open`, deleted the lockfile with an editor up, got the `unity job` option order wrong,
  or wrote a status loop whose test never matched.
- v2's checking section worked: R5 wrote 86 checks and R6 87, and R6 walked 8 times. No v2 walker script moves
  the player's transform or has unstick logic (a search of the three runs' scripts; R6's says so in its header).

## Changes from v2

Each change is tagged:
- **friction**: time lost to the environment that tells us nothing about level design;
- **fairness**: something ours had that a cold run lacked, or read as forbidden, that is not the framework;
- **fence**: keeps the run away from our work;
- **verification**: how the run checks its own work;
- **clarity**: a reading the runs had to guess, or a report that said less than the run had measured.

**Skeptic** marks a change someone could call coaching toward the scorer, with the reason it stays.

1. **friction. The 5-second eval limit.** v2 said a live `unity command` gives up after 30 s. For `eval` and
   `eval_file` it is 5 s: each takes its own `timeout` argument, in milliseconds, default 5000 (the schema, R6
   #44). The CLI's `--timeout` before the command name does not raise it.
   - R6 passed `--timeout 120` and still got "Main thread operation timed out after 5000ms" (#28, t7.2).
   - R4 hit it at #79 (t53.0) and wrote a `--detach` wrapper, `ueval.sh`, three minutes later (#89). R5 hit it at
     #72.
   - All three then ran long work with `--detach` and `unity job wait`. The pitfall now says that.
2. **friction. Change source with the edit tool.** v2 said to write source files with the file tool. The runs
   did, then patched them with Python heredocs.
   - R5 #142 and R6 #137: a `\n` in the Python text became a real line break inside a C# string, which does not
     compile. Both fixed it with the edit tool two calls later.
   - R4's inline patch broke on quoting (#118) and was re-sent as a file (#119).
   - The pitfall now covers changes as well as new files.
3. **friction. The reload after a recompile.** Until the editor has reloaded its scripts, commands fail with
   "Network error: An error occurred while sending the request" (R3 #64). New pitfall: wait for `ready`.
4. **friction. `unity open` takes the path as an argument.** R3's `unity open --project-path …` failed with
   "unknown option '--project-path'" (#31). v2's option-order pitfall is about `unity command` and reads as if
   `--project-path` were general. One line added.
5. **friction. `capture_game_view` needs a camera.** R3's captures failed with "No camera found to capture"
   (#101, #159). One line added to the capture pitfall.
6. **friction. The brief and the data file as files.** R3 retyped `level.json` into a file of its own. The
   launcher now copies both into `Brief/` in the project, and the prompt says so. Ours reads the same file.
7. **fairness. The player's camera renders post-processing, as ours does.**
   - Ours is seen through the Starter Assets `MainCamera` prefab with post-processing on (`m_RenderPostProcessing:
     1`, a local change in the Builder). It is graded by a global volume (ACES tonemapping, +2.4 EV, bloom,
     vignette; `Assets/IronCitadel/Ambience/Profiles/grade.asset`) and a LUT mood per room.
   - The cold template ships the same prefab with post-processing off (`m_RenderPostProcessing: 0`). v2 says to
     play-test "with the player prefab exactly as shipped" and not to change its "controller settings".
   - R4's report: no tonemapping or bloom, because it left the shipped player camera untouched.
   - So the template's `MainCamera` gets post-processing on, as ours. THE SETUP now says the player's size,
     movement and controls are fixed, and that how the level looks through its camera is the run's to set.
   - **Skeptic:** this points at post-processing. Kept: it removes a limit ours never had, and names no setting
     or value.
8. **fairness. Cinema Suite's LUTs.** Ours grades rooms with Cinema Suite's Cinema Themes LUTs (`Assets/Cinema
   Suite/`, untracked in the Builder). The template lacks them. Import the package into the template and name it in
   THE SETUP, as the Synty packs are named.
   - The alternative is to score ours with its LUT moods off. Adding the package is simpler and leaves ours as it is.
9. **clarity. Partly met is not met.** R5 measured the gallery rail's view as short of the whole floor ("a strip
   about 2.7 m wide at the arcade's foot is hidden by the gallery's own edge"), yet reported "All items pass (86
   checks, 0 fails)", with that as a caveat. The kit scored the check at 74%.
   - The quality bar and report (b) now say that an item measured as only partly met is not met: change the level
     until it is, or report it as not met.
   - **Skeptic:** this could read as a hint that the scorer is strict. Kept: it is what "satisfy every item"
     already says. It names no item, measure or threshold. And it makes report (b) comparable with the kit's
     verdicts, which the comparison reads side by side.
10. **verification. Deliver the checks.** R5's 86 checks and R6's 87, and their walkers, were written, run and
    left in the project unlisted; nothing asked for them.
    - v3 asks for `Checks/`: one entry point that re-runs every check and walk on the saved scene and writes
      `Checks/results.txt`, delivered as written by its last run on the delivered scene.
    - This lets us re-run each run's own checks after a kit change, set its claims beside the kit's, and compare
      the results file's time with the scene's (walk again after the last change).
    - **Skeptic:** keeping the checks may lead a run to check more. v2 already asks for the checks; this asks only
      that they be kept and runnable.
11. **fence. No looking for this level.** The level is public now (IronCitadel-Showcase on GitHub Pages,
    2026-10-08), and v2 allows web searches for documentation. v3 takes the three.js prompt's line: do not search
    for, open or use any build, code, capture or write-up of this level or of this experiment. The launcher's
    audit now covers web searches and fetches.
12. **friction. Unity's CLI and its own agent guidance.** Roger, 2026-10-13: "We can upgrade the CLI and use the
    Pipeline as well as Unity Skills which I do not think are installed." The CLI is now beta.13, and Unity's
    plugin and `unity-pipeline` are installed in both configs (Setup, below). THE SETUP names the skills, says how
    to list the commands, and says `UNITY_PROJECT_PATH` is set. The pitfalls were then rechecked on beta.13 with
    Pipeline 0.8.0-exp.1 against a warm editor. The evidence is `cli-probes-beta13.txt`, beside this file; its
    probe labels are in brackets.
    - `run_script` [f to h3] compiles a `.cs` file outside `Assets/` in memory and runs it, with no import and no
      reload, and the file may have `using` lines. It is the way past the eval limit that R4, R5 and R6 worked
      around with `--detach` (item 1). It needs the CLI's `--timeout` past the work as well: at the default 30 s
      the CLI gave up while the editor carried on [h1], and the next command waited behind it [h1b].
    - `eval`'s own `timeout` is named after `--`: `eval -- --code '<C#>' --timeout 15000` ran a 7 s job [b], and
      the CLI's `--timeout` still does not raise it [c]. v2's "a `--` there silently adds an empty parameter" is
      stale [d], and the CLI's options may follow the command name [e].
    - `unity status --until-ready` replaces the polling loop [j, n2, q0]. Piping `unity open` returned in 2.1 s
      [n1], so the pipe warning is gone.
    - New: during the agent instance's 35-minute re-import, `unity status --until-ready` reported `ready` while
      every command was refused as "busy (settling)" for its whole `--timeout` (Part 1).
    - Kept: the reload's "Network error" [i1, i2], which beta.12's "waits out a reload" does not cover; `using`
      failing in `eval` [e2]; and `unity run`'s reserved flags (Part 3), though its help no longer passes `-quit`.
    - Changed: an unknown option now exits 2, not 6 [o2, o3]; with no editor, `unity status` prints an empty table
      and `unity command` names the Pipeline package [o1, o4]; `capture_game_view` defaults to the Game view
      window [l3, p5, p6]; and every `--save_path` still lands under `Assets/` [l5, p2, p3], while a path outside
      the project or with `..` is refused [l6, p4].
    - Not rechecked: `simulate_key` and `Keyboard.current`, batch-mode render colours, and `--timeout` stranding
      the lockfile.
    - **Skeptic:** three of the plugin's 32 skills touch what the kit scores: `initialize-ai-navigation`,
      `urp-postprocessing` and `physics-3d-collision`. Kept: they are Unity's own documentation, every run gets
      the same set, and the prompt names none of them for any task.
13. **clarity. Timing and the run's own suggestions.** Roger, 2026-10-13: "Ask both for improvements to the
    prompt or process as well as timing." Report (e) now asks for each phase's time, from clock times the run
    notes as it starts each phase, not from memory. New item (f) asks what the run would change in the prompt, the
    brief or the setup. The report's limit rises from 300 words to 400 to fit them. The PCGTools prompt asks the
    same.
14. **clarity. Brief v2, and no data file.** Roger, 2026-10-13, asked to "get rid of the level.json and talk at a
    high level about the key features". The prompt pastes brief v2 alone, and the launcher copies it to
    `Brief/brief.md`. The grid, the tile size, the data file's routes and `must_be_true` are gone. The brief's
    "What must be true" takes the list's place, and the level's size is the run's call ("What I don't care
    about: how big the level is"). "Do not invent extra doors or openings" went with the data file's doors.
15. **verification. The map.** With no data file, nothing says where the rooms are, so the kit cannot score a
    build. The run delivers `Map/map.json`, read from the saved scene. It holds the start, each room's kind and
    floor outline, and each opening and the rooms it joins. It also holds the gallery's deck and rail, the
    stage and throne, and each enemy and pickup. The kit measures everything else in the scene itself. Ours
    delivered the same in `design.json`.
    - **Skeptic:** writing the map could make the run plan more carefully. Kept: ours wrote the same, the map
      names no layout, and the brief already asks for everything in it.
16. **verification. The walks come from the brief.** v1 and v2 walked the data file's routes. v3 names the
    brief's five, which are the five ours walked:
    - the way round to the dining hall;
    - the climb to the gallery without crossing the hall floor;
    - the way into the armory beneath the gallery;
    - the hidden way into the vault and back;
    - on to the throne.

    "Confirm that the blocked route stays blocked" went with the gate.
17. **clarity. The choke, as ruled.** Roger's ruling for ours was "it's best that you don't see the guards until
    you get to the jump", meaning the doorway. On 2026-10-13 he chose to put it in brief v2's act one: the
    guards come into view only at the armory's doorway, and the entry hall is where the player sees that
    entrance. Ours already does this.
18. **clarity. Ours' five views.** `Captures/` adds the five views ours captured:
    - the armory's guarded entrance from the entry hall;
    - the way into the armory beneath the gallery;
    - the hall floor from the gallery's rail;
    - the throne on its stage;
    - the vault's hidden way in.
19. **fairness. Pack art only, markers excepted (v3.1).** v3's rule, "Use art other than what is already in the
    project (no downloaded models or textures; primitives are fine for markers and invisible helpers)", bars
    outside art but not art the run makes. Fable v1 on Synty drew 611 renderers from code; R4 generated every wall
    and ceiling. A run that models its own level is a test of procedural modelling, not of building from the
    packs, and ours uses pack art only. v3.1 takes R5 and R6's pack-only wording (`cold-prompt-v2-remesh-packonly.md`)
    and keeps v3's one allowance: primitives for the defender and pickup markers, which brief v2 calls markers.
    - **Opus v3 already meets it** (checked 2026-10-14 on `ColdRuns/opus-v3`). The scene's only built-in meshes are
      25 capsules, 27 cubes and 10 cylinders: its 25 defenders (body and visor), 9 ranged staffs, the warlord's
      crown and 2 pickup gems. No `new Mesh` and no ProBuilder in its code; no Starter Assets environment asset in
      the scene. Its code makes one material (URP Lit), which the rule is silent on, as in R5 and R6.
    - **Skeptic:** Opus v3 did not see the sentence. Kept: the sentence forbids only what it did not do, and
      re-running it costs $27 to $35.
    - **The bookcase.** Both Opus v3 runs hid the vault behind a bookcase in the library, though brief v2 names no
      bookcase door. No leak was found (new project folder, auto-memory off, audit 0, nothing in the template). The
      brief and setup point there: the library has "tall bookcases packed close", the vault "a hidden door ... the
      player may have to search for it", and the SETUP and `InteractOnE.cs` say "such as a secret door". Left as is,
      to keep v3.1 paired with Opus v3. The cold config still holds v1 notes for runs named `opus` and `fable`
      (v1's brief named the bookcase), so v3.1 runs take new names.

**Considered and left out**, as coaching or as the kit's problem:
- "Keep the stage steps clear of props." R4's brazier pedestals and R5's fire bowls stand on the stage front
  (check 8 at 73% and 77%), and both reported the item as met. Neither measured it, so item 9 does not catch it.
  Saying it names their failure, as "keep props out of doorways" would have named Fable v1's ore pile.
- Box-step colliders (R3: 7 / 3 / 1 with mesh steps, 10 / 0 / 1 with boxes). The kit's bake is at fault, so the
  kit gets the fix.
- R5's invisible ramps under its stairs. Invisible helpers are allowed, and a ramp collider under steps is common
  practice.
- A frame-rate target ("It should run smoothly on a recent laptop", from the three.js prompt). Neither v1 nor v2
  asked for one, and the kit does not measure it. Add it if the comparison will show web builds; Roger's call.
- Lighting numbers (exposure, lux, light counts). They would set the look. Item 7 opens the look to the run;
  "lit so a player can see where to walk" stays the bar.
- As in v2: no NavMesh suggestion, no controller numbers, no definition of "the whole hall floor", no Synty
  module sizes, nothing about the kit.

**Comparability.** v3 runs brief v2, cuts friction, changes the player's camera and adds deliverables. It also runs
on a newer CLI with Unity's agent skills installed. So its times and scores are not comparable with v1's or v2's.
Roger, 2026-10-13: Opus only. Its comparison is ours for brief v2, in `../v2/`. v3.1 (2026-10-14) adds Fable and
Haiku on the same brief, template, kit and launcher; Opus v3 stands for Opus.

## Setup (once, before any v3 run)

**Done on 2026-10-13, for every run on this machine:**
- **The CLI is 1.0.0-beta.13**, from beta.11 (`unity self-update`). The old binary is kept as `unity.exe.previous`,
  and `unity self-update --rollback` restores it.
- **Unity's Claude Code plugin**, `unity@unity-agent-plugin` 0.1.8-beta with 32 skills, is installed at user scope
  in both configs: by `unity setup claude` in `~/.claude`, and in `~/.claude-cold` by its two `claude plugin`
  commands run with `CLAUDE_CONFIG_DIR` set. It adds about 2,900 tokens to every session, and `unity-cli` about
  19,000 when it loads.
- **The `unity-pipeline` skill** from Unity's skills repo is copied into `skills/unity-pipeline/` in both configs,
  at commit `9d28210054`. It was published on 2026-10-08, after the plugin's last release. Its method-reload
  section uses names this package version lacks: it says `methodreload_status`, and 0.8.0-exp.1 has
  `codereload_status`. The rest matches the package's catalog.
- **The Pipeline package** is already the latest, 0.8.0-exp.1, in the Builder, the agent instance and the cold
  template (`unity pipeline list-versions`), so it needs no upgrade.

1. **The kit.** Brief v2 needs its own kit, which reads the run's map; it scores this run and ours for brief v2.
   The three fixes proposed for the v1 kit are for re-scoring the v1 builds, and this run does not need them:
   - where the bake finds no path over a stair, fall back to a CharacterController climb (R3's mesh steps);
   - cast ceilings against meshes, not bounds (R4);
   - keep each wing's walk inside its own route's cells (Fable v1's 460 m detour).
2. **The template**, `C:/Repos/ColdRuns/_template/` (Synty, as v1). Done 2026-10-13, 20:01:
   - `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/MainCamera.prefab`: post-processing on. The rest
     of the file matches the Builder's. The old file is in the session scratchpad.
   - Cinema Suite copied from the Builder, with its `.meta` files, and imported once in the template's editor:
     72 LUTs, no compile errors. The editor closed gracefully.
3. **The launcher**, `run-cold.ps1`. Done 2026-10-13:
   - auto-memory is off for the session: `autoMemoryEnabled: false` in its settings file,
     `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1` after the scrub, and deny rules on Edit and Write to the cold config's
     memory folders;
   - `UNITY_PROJECT_PATH` is set to the copy after the step-4 scrub, so `unity command`, `unity status` and
     `unity job` reach the copy's editor from any folder;
   - `-BriefFile` names the brief. When the prompt names `Brief/`, the brief goes into `<copy>/Brief/brief.md`, and
     `level.json` goes beside it only when the prompt has its placeholder;
   - when the session ends, if an editor still has the copy open, it writes `editor_left_open: true` to
     `summary.txt` and closes the editor without saving, so the saved scene is what gets scored;
   - it writes the time, size and SHA-256 of `Assets/Scenes/IronCitadel.unity`, `Checks/results.txt` and
     `Map/map.json` to `summary.txt`;
   - the audit also flags `WebSearch` and `WebFetch` inputs matching `iron.?citadel|crawfis|IronCitadel-Showcase`.
4. **New names.** `-Name opus-v3`, `-OutDir C:\Repos\Github\Crawfis-Software\IronCitadel\cold\v3\opus`. A new name also keeps an old
   project-memory folder from matching.
5. **Cost, before any run.** Past Opus runs cost $22 to $30 at API list price. All six earlier cold runs billed the
   Max plan (`-Billing Subscription`). Name the billing and get a yes first.

The launch, from a terminal (or a background shell; the launcher scrubs the parent session's variables):

```
pwsh -NoProfile -File C:\Repos\Github\Crawfis-Software\IronCitadel\cold\run-cold.ps1 -Model opus -Billing Subscription `
    -PromptFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\cold-prompt-v3.md -BriefFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\brief-v2-draft.md `
    -Name opus-v3 -OutDir C:\Repos\Github\Crawfis-Software\IronCitadel\cold\v3\opus
```

v3.1, one run at a time so the wall clocks are not shared (Roger, 2026-10-14: Max plan; effort is the launcher's
`xhigh`, as every cold run):

```
pwsh -NoProfile -File C:\Repos\Github\Crawfis-Software\IronCitadel\cold\run-cold.ps1 -Model fable -Billing Subscription `
    -PromptFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\cold-prompt-v3.1.md -BriefFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\brief-v2-draft.md `
    -Name fable-v3 -OutDir C:\Repos\Github\Crawfis-Software\IronCitadel\cold\v3\fable
pwsh -NoProfile -File C:\Repos\Github\Crawfis-Software\IronCitadel\cold\run-cold.ps1 -Model haiku -Billing Subscription `
    -PromptFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\cold-prompt-v3.1.md -BriefFile C:\Repos\Github\Crawfis-Software\IronCitadel\cold\brief-v2-draft.md `
    -Name haiku-v3 -OutDir C:\Repos\Github\Crawfis-Software\IronCitadel\cold\v3\haiku
```

## Harness notes

Every v2 run read its memory folder in its first two calls. The v1 notes still sit in
`C:/Users/roger/.claude-cold/projects/C--Repos-ColdRuns-{opus,fable}/memory/`, so a run named `opus` or `fable`
would load them. From the Claude Code docs (memory, env-vars, settings, permissions pages), checked 2026-10-13:

- **Turn auto-memory off** in two ways:
  - add `"autoMemoryEnabled": false` to the settings file the launcher passes with `--settings`;
  - set `CLAUDE_CODE_DISABLE_AUTO_MEMORY=1` in the session's environment. Never set it to `0`, which forces memory
    on.
  - The launcher's step 4 removes every `CLAUDE_CODE_*` variable, so set this one after that step.
- **Block memory writes** with a deny rule in the same settings file, `Edit(//c/Users/roger/.claude-cold/projects/**/memory/**)`.
  Deny rules hold even with permissions skipped. A shell redirect is not covered, so the audit still matters.
- **Clear the old notes** before the first v3 run: move `.claude-cold/projects/*` out of the config folder, or
  `claude purge <path>` with the cold config (needs Claude Code 2.1.288 or later). Not done for `opus-v3`: its
  project folder is new, memory is off, and the audit reads the old transcripts when re-summarizing old runs.
- **Leave CLAUDE.md alone.** `--bare` would skip CLAUDE.md and memory too, but it also drops skills, subagents and
  the claude.ai login, so it changes the run. The cold copies hold no CLAUDE.md, so it is not needed.
- **The skills a run loads.** The cold config is signed in to claude.ai, so every run already loads the account's
  synced plugins and skills. R6's first event lists `engineering`, `marketing` and the `anthropic-skills` set,
  `anthropic-skills:unity-ui-toolkit` among them. v3 adds Unity's plugin and `unity-pipeline`. Every run gets the
  same set. Roger's `recent-prompts` mod comes in through `CLAUDE_CODE_PLUGIN_DIRS`, which the scrub removes, and
  R6 did not load it.
- **Check it once** before the matrix. Plant a unique line in `.claude-cold/projects/<slug>/memory/MEMORY.md`, run
  one cheap `claude -p` in a scratch copy asking it to repeat any memory it has, and diff `memory/` before and after.
  That is a paid call, so ask first. Skipped for `opus-v3`; after the run, check that
  `.claude-cold/projects/C--Repos-ColdRuns-opus-v3/` has no `memory/` folder.

## The task prompt (verbatim; paste brief v2 where marked)

> You are taking part in an experiment: "how good a level can a frontier model build in Unity from a
> designer's brief, using commercial art packs but no level-generation framework."
>
> THE SETUP. This folder is a Unity 6 project (URP) with three Synty POLYGON dungeon packs already
> imported (Dungeons, Dungeon Realms, Fantasy Dungeon Map), Cinema Suite's Cinema Themes colour-grading
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
> what is already in the project (no downloaded models or textures). Make any model or mesh of your own:
> no meshes built in code, no ProBuilder or other modelling tools. Every model and mesh the player can
> see in the level must be the dungeon packs' own models and prefabs, which you may place, rotate, scale
> and combine as you like. Starter Assets is there for the player only; its sample environment and robot
> are not level art. The exceptions: primitives (cubes, capsules, cylinders and the like) for the
> defender and pickup markers; invisible helpers, such as colliders and triggers; the M overview map
> (anything you draw on it); and text, such as the HUD and the throne announcement. Ask questions: make
> a call and note it in your report.
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
> 2. The first-person controller at the start, in the entry hall. In play mode: walk, sprint, climb the
>    stairs, M toggles a top-down overview of the whole level with the ceilings hidden, a small HUD names
>    the room the player is in and shows elapsed time, and reaching the throne is announced.
> 3. `Map/map.json`: the level as built, in the form below, for someone who has only the scene. Read every
>    position and outline from the saved scene, not from your plan.
> 4. `Captures/`: one top-down overview of the whole level, one eye-level shot of each room, and one each
>    of the armory's guarded entrance from the entry hall, the way into the armory beneath the gallery, the
>    hall floor from the gallery's rail, the throne on its stage, and the vault's hidden way in.
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
>   "throne": [x, y, z],
>   "enemies": [ { "kind": "ranged | melee | warlord", "room": "…", "position": [x, y, z], "facing": [x, z] } ],
>   "pickups": [ { "kind": "best gear | hoard", "room": "…", "position": [x, y, z] } ]
> }
> ```
>
> - `rooms`: every room, each with a unique `name`. `kind` is the brief's name for it: `entry hall`,
>   `armory`, `guard room`, `prison`, `kitchen`, `library`, `alchemist's study`, `forge`, `treasure vault`,
>   `dining hall`, `throne room`, `crypt`, `storeroom`, `shrine` or `torture chamber`, or `extra` for a room
>   you added. `state` is `decayed`, `looted`, `stocked` or `untouched`. `floor` is the room's floor as one
>   or more outlines, at height `floor_y`.
> - `doors`: every opening between two rooms, naming both. `open` is a doorway or an open door, `shut` a
>   door the player opens, and `hidden` the vault's way in. `position` is the middle of the opening at
>   floor level, and `width` its clear width. The scene has no opening between rooms that is not listed.
> - `gallery`: its walkway's outline and height, and `rail`, the line of its edge over the hall floor.
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
>   round, from the start to the dining hall without passing through the armory; up to the gallery from
>   where the way round comes into the hall, without crossing the hall floor; from the dining hall into the
>   armory beneath the gallery, to its best gear; the hidden way into the vault and back; and from the
>   dining hall on to the throne. Walk; do not jump.
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
