# Cold runs v1: what the transcripts say about the prompt

The two cold runs (Opus, then Fable) got the v1 prompt, `UnityAssets/docs/research/AI-DungeonLevelComparison/cold-prompt.md`.
This note covers where their time went, what cost time without telling us anything about level design,
how each run checked its own work, and where each had to guess. `cold-prompt-v2.md`, beside this file,
is the replacement prompt built from these findings.

**Sources.** `cold/{opus,fable}/stream.jsonl`, `final-reply.md` and `summary.txt`; the scores in
`scores/{opus,fable}/conformance.md` and `scores/fable/FINDINGS.md`. The transcripts were parsed with Python;
the scripts and dumps are in the session scratchpad (`coldmine/`).

**Citations.**
- `#n` is the n-th tool call in the run's stream, counting from 0.
- `evN` is line N of `stream.jsonl`, counting from 0.
- `tM` is M minutes after the run's first event: Opus 15:37:35Z, Fable 19:51:05Z, both on 2026-10-07.

| | Opus | Fable |
|---|---|---|
| Wall clock | 73.5 min | 88.7 min |
| Tool calls | 212 | 193 |
| Score | 10 pass, 0 fail, 1 manual | 8 pass, 2 fail, 1 manual |
| Friction measured below | about 8 min (6 without the posing) | about 27 min |

## Opus

### Phase timeline

| Phase | Calls | Starts at | Minutes |
|---|---|---|---|
| Explore the project, the packs and the controller | #0-#9 (10) | t0.0 | 0.5 |
| Learn the CLI, boot the editor | #10-#17 (8) | t0.5 | 4.3 |
| Survey the art: measure bounds, render lineups, pose characters | #18-#63 (46) | t4.8 | 21.0 |
| Write the runtime scripts and the builder | #64-#78 (15) | t25.8 | 9.3 |
| First build and lighting | #79-#91 (13) | t35.2 | 4.8 |
| Build the hall, west and east rooms | #92-#107 (16) | t40.0 | 6.9 |
| Review passes: captures, lighting, density | #108-#132 (25) | t46.9 | 6.9 |
| Play mode 1: an 8 m walk, the overview key, the bookcase, the throne | #133-#151 (19) | t53.7 | 2.7 |
| Fixes from play mode 1 | #152-#155 (4) | t56.4 | 2.7 |
| Write `Verify.cs`, run the verification loop | #156-#165 (10) | t59.1 | 4.7 |
| Play mode 2: overview, west stair, rail, and their fixes | #166-#187 (22) | t63.8 | 5.5 |
| Guard-room dressing and lighting, after the last walk | #188-#199 (12) | t69.3 | 2.3 |
| Final build, verification, captures | #200-#208 (9) | t71.6 | 1.1 |
| Close the editor, write memory | #209-#211 (3) | t72.7 | 0.8 |
| Report (final text) | none | t73.1 | 0.4 |

Rolled up by kind of work:
- Explore and CLI: 4.8 min.
- Art survey: 21.0 min.
- Build: 21.0 min.
- Dress, light and review: 9.2 min.
- Play-test, with its fixes: 10.9 min.
- Self-verification script: 4.7 min.
- Final pass, close and report: 2.3 min.

Lighting has no phase of its own: Opus did it inside each build and review pass. Two long gaps with no tool
calls were planning: #22 to #23 (6.7 min, t5.1 to t11.8) and #62 to #63 (4.2 min).

### Friction

| Issue | Exact error or symptom | Calls | Minutes | Fixed by v2? |
|---|---|---|---|---|
| `unity open . 2>&1 \| tail -5` hung | "Command did not complete within its 180s timeout and was moved to the background" (#12, ev40 to ev50). The editor was already `ready` at #13 (ev52, t3.7). The pipe to `tail` stays open while the editor runs. | 1 | 3.0 | Yes. New pitfall: do not pipe `unity open`. |
| Posing the defender markers as Synty characters | A PlayableGraph, AnimationMode and `Animator.Update` all left the figures unposed (#46-#54). `HumanPoseHandler` worked (#55-#57), then posed them in world space, which needed a fix at the origin (#97-#98). | 14 | 2.3 | No. This was a scope choice; v1 already allows primitive markers. |
| Inline Python in shell heredocs | "/usr/bin/bash: -c: line 79: unexpected EOF while looking for matching `''" (#116, ev1804; #127, ev1929). Each patch was then re-sent as a file (#117, #128). A Python string escape also produced C# "error CS1010: Newline in constant" in `Shots.cs` (#108, fixed at #109). | 6 | 1.4 | Yes. New line: write files with the file tool. |
| `simulate_key` not seen by `Keyboard.current` | With M held, `mPressed=False` (#141). With W held, `w=False anyKey=False` while the player moved 2.5 m (#144). The Input System's `editorBehaviour=PointersAndKeyboardsRespectGameViewFocus` (#145). The HUD was rewritten to use an `InputAction`, which worked at #168. | 6 | 1.2 | Yes. New pitfall. |
| `capture_game_view` writes under `Assets/` | With an absolute save path, no file appeared there and the Read failed (#134-#135). With a relative path the result was `"savedPath": "Assets/Temp/look/play1.png"` (#136). From then on every play capture needed an `mv` out (#137, #149, #175, #178). | 4, plus `mv` in 4 later calls | 0.3 | Yes. New pitfall. |
| `unity job` option order | "error: unknown option '--project-path'" for `unity job --project-path . wait <id>` (#81, ev1305). `unity job wait --project-path . <id>` worked (#82). v1's "options come before the command name" holds for `unity command` only. | 2 | 0.1 | Yes. The pitfall is corrected. |

**Not hit by Opus:** lockfile trouble, exit-6 confusion, import stalls and pink materials. At #28-#30
(t12.7) it checked a "chrome-like" wall: the material is Synty's shader graph and the look was lighting.
The 30 s live limit was not hit either: from #79 it wrapped every long `eval` in `--detach` plus `unity job wait`.
`unity close` at #209 was clean and left no lockfile.

### How Opus checked its work

- **`Verify.cs`** (#156, t59.1) bakes a NavMesh from the physics colliders and adds raycasts.
  - The agent is r 0.35, h 1.8, **climb 0.35**, slope 46. The stock controller steps only 0.3 m.
  - It checks:
    - edge crossings;
    - the gate;
    - each wing with the other barred (west 188 m, east 186 m);
    - the vault, closed and open;
    - the throne room;
    - the gallery's landings and stairs;
    - the sightline from the rail;
    - the marker counts.
  - The final run of it was on the final build (#200), after the last dressing pass.
- **Play mode was teleport and short walks.** Apart from a 2 s walk from the start (#139), each test began by
  disabling the CharacterController and setting the capsule's position and rotation (#146, #149, #174, #177). It
  then held W with `simulate_key` for 1 to 5.5 s (#174, #175, #177, #178).
  - It turned the player by setting `transform.rotation`.
  - The longest uninterrupted walk was about 5.5 s (#177).
  - It walked no route in `level.json` end to end.
  - It walked the west gallery stair, but never the east one.
  - The bookcase test teleported to 1.5 m from it and called `InteractOnE.Interact()`, which casts the E key's ray
    without the key press (#146).
- **Screenshots:** contact sheets of every room over four review rounds (#112-#132, #154, #201-#206).

### Where Opus's checking missed, or nearly missed

- **The west stair.** `Verify.cs` reported "PASS west stair climbs from the landing to the gallery without
  crossing the hall floor (32 m, top 5.1 m)" (#165, t63.5). Two minutes later the real controller stuck at
  (94.85, 4.69, -80.75), 0.31 m below the deck (#174, ev2609).
  - The NavMesh's 0.35 m climb hid a lip the 0.3 m step cannot take.
  - The short assisted walk found the fault, and the deck was made flush (#176, re-walked at #177).
  - This is the clearest case in either run of a walk with the real controller catching what a proxy check passed.
- **A check that cannot fail.** The heights line is
  `Check(true, "low: prison 3.6, guard room 3.6, kitchen 4.0, vault 3.2 …")`. It states the planned heights and
  measures nothing.
- **The rail sightline.** The check failed twice (#158, #161), and Opus changed both sides:
  - the level: the arcade went onto a 2 m plinth, and an open railing replaced a solid balustrade (#160-#161, #182-#183);
  - the check: its eye was allowed up to 0.75 m back from the rail (#185).

  The final line counts floor points under tables as neither seen nor hidden:
  "631 are seen directly, 329 lie under tables/benches/feasters, 0 are hidden by architecture" (#165). Its
  report says so ("Floor under tables and benches is hidden").
- **Dressing after the last walk.** The guard-room clusters, the lantern and a stool (#188-#199) went in after
  the last play-mode walk. Only the NavMesh check, with its looser climb, re-ran on them.

## Fable

### Phase timeline

| Phase | Calls | Starts at | Minutes |
|---|---|---|---|
| Explore the project, packs and controller; read the CLI guide | #0-#21 (22) | t0.0 | 9.1 |
| Headless dump of prefab bounds; size survey | #22-#31 (10) | t9.2 | 11.2 |
| Write the runtime scripts and the generator | #32-#44 (13) | t20.4 | 20.3 |
| First headless build and captures | #45-#46 (2) | t40.6 | 0.9 |
| Review the captures | #47-#66 (20) | t41.6 | 4.7 |
| Dressing and lighting fixes, rebuild | #67-#88 (22) | t46.3 | 4.2 |
| Red squares in the overview: diagnosis, more dressing | #89-#100 (12) | t50.4 | 4.9 |
| Write the play-test driver, rebuild | #101-#111 (11) | t55.3 | 2.6 |
| Open a live editor; show the artefacts are batch-mode only | #112-#120 (9) | t57.9 | 2.3 |
| Play test 1, capture review | #121-#130 (10) | t60.3 | 1.7 |
| Recompile wait (the poll never matched) | #131 (1) | t61.9 | 4.3 |
| Rebuild, play test 2, capture review, dressing | #132-#156 (25) | t66.2 | 6.6 |
| Cyan overview frame: diagnosis | #157-#165 (9) | t72.8 | 2.1 |
| Recompile wait, rebuild, captures | #166 (1) | t74.9 | 4.3 |
| Play test 3, capture review, last dressing edits | #167-#180 (14) | t79.2 | 3.4 |
| Final recompile wait, rebuild, captures | #181-#185 (5) | t82.7 | 4.5 |
| HUD screenshots, tidy | #186-#189 (4) | t87.1 | 1.0 |
| Write memory, final scene check | #190-#192 (3) | t88.1 | 0.6 |
| Report (final text) | none | t88.3 | 0.4 |

Rolled up by kind of work:
- Explore and CLI: 9.1 min. This includes a 2.0 min timeout and a 4.8 min planning gap (#21 to #22).
- Art survey: 11.2 min.
- Writing the generator: 20.3 min. Gaps of 4.4 and 5.2 min are the long files being written; `Dressing.cs`
  alone is 48 KB.
- Builds and capture review, dressing and lighting: 9.8 min.
- Render-artefact diagnosis: 7.2 min (about 9.8 min counting the overlap).
- Play-testing, with route fixes and capture review alongside: 14.3 min.
- Recompile waits and rebuilds: 13.1 min.
- Cyan-frame diagnosis: 2.1 min.
- Wrap-up: 2.0 min.

### Friction

| Issue | Exact error or symptom | Calls | Minutes | Fixed by v2? |
|---|---|---|---|---|
| A recompile wait whose test never matched | The loop was `for i in $(seq 1 90); do s=$(unity command --no-banner --format json recompile_status …); if echo "$s" \| grep -q '"compiling":false'; then break; fi; sleep 2; done`. It ran all 90 tries every time: #131 took 248.4 s; #166 took 254.1 s including the build and captures; #181 with #185 took about 256 s. The CLI's JSON is indented (`"compiling": false`, see Opus #133, ev1992), so the test cannot match. Opus's recompile waits, which tested the plain status for `completed\|up_to_date`, took 5-11 s. | 3 | about 12 | Yes. New pitfall: the JSON is indented, so look at a probe's output before looping on it. |
| Wrong colours in batch-mode renders | Solid red squares in the overview, first seen at #88 (t49.7). Fable diagnosed them with crops, a diagnostic capture, a reflection fix, and renders with the SRP Batcher off and on (#89-#120). It ended with "The live render is clean, so the final captures will come from the live editor" (ev2453, t60.2). | about 15 | about 9.8 (t50.4-t60.2, partly overlapped with capture review) | Yes. New pitfall. |
| `du -sh Library` timed out | "Command did not complete within its 120s timeout" (#19, ev130). The same probe without `du` took 0.3 s (#20). | 1 | 2.0 | Yes. New line: leave `Library/` out of scans. |
| Cyan frame in the play-mode overview | The driver's `ScreenCapture.CaptureScreenshot` gave a cyan frame with the overview on (`play_overview.png`). Diagnosed at #155-#165 (t72.0-t74.8). The final overview shot came from `capture_game_view` (#186), and the driver's frame was deleted. | 11 | 2.8 | In part. The `eval` and capture-path lines cover two of its sub-errors; the cause of the cyan frame was not found. |
| `eval` compiles a method body | "You must provide an initializer in a fixed or using statement declaration (line 1, col 49)" and "'Camera' does not contain a definition for 'GetUniversalAdditionalCameraData'" (#158, ev2844; #159, ev2854, exit 2). | 2 | 0.4 (inside the row above) | Yes. New pitfall. |
| `capture_game_view` writes under `Assets/` | `--save_path "C:/Repos/ColdRuns/fable/Tools/diag_play_ov.png"` gave `"savedPath":"Assets/Tools/diag_play_ov.png"` (#161). Cleaned up with `rm -rf Assets/Tools Assets/Tools.meta` (#161, #163, #186) and `mv Assets/$f.png Captures/` (#186). | 3 | 0.3 | Yes. New pitfall. |
| Heredoc | "unexpected EOF while looking for matching `''" (#32, ev923). Then: "The heredoc choked on an apostrophe, so I'll write the script files with the file-writing tool instead" (ev938). | 1 | 0.5 | Yes. |
| Lockfile deleted as routine | `rm -f Temp/UnityLockfile` before every batch boot and before `unity open` (#22, #23, #45, #46, #87, #98, #108, #112). It was harmless here because no editor was up, but deleting a live editor's lock is how two editors end up on one project. | 8 | 0 | Yes. The pitfall now says when to delete it, and when never to. |
| Batch-mode boots | Each headless build or capture boot took 14-31 s (#45 13.8 s, #46 30.9 s, #87 23.4 s) until Fable moved to a live editor at #112. | 5 | about 1.5 | No. This is a choice, and a fact of the engine. |

**Not hit by Fable:** exit-6 confusion, the 30 s live limit (it used `--detach` with result files), import
stalls and pink materials. At #49 it checked the rock material's shader while chasing the red squares.
`unity open` redirected to a file returned in 0.4 s (#112). It printed 'Warning: "." was not found in the Hub
project registry, opening as a file path.' and was ready about 50 s later (#114).

### How Fable checked its work

- **Edit-mode captures:** 17 fixed camera poses, rendered and read back over four passes (#47-#66, #88-#100,
  #122-#146, #168-#189).
- **A scripted play-mode driver.**
  - `PlaytestDriver.cs` (#101, ev2174) feeds the stock controller `MoveInput` and `SprintInput` through
    `SendMessage` toward waypoints. It sets `player.rotation` directly.
  - It "presses E" by `SendMessage("Interact")` on the player, which runs `InteractOnE`'s raycast.
  - It logs `STUCK` after 6 s without progress, then logs `reached <step>` anyway and goes on to the next step.
  - From #130 (ev2548, t61.8), added after play test 1 stalled in the entry hall: "if wedged on furniture,
    strafe for a moment, alternating sides". After 0.6 s without 1 cm of movement it strafes for 0.7 s.
- **The route** (`Playtest.cs`, #106, edited at #153-#154):
  - lobby, then the gate (blocked, as expected);
  - the guard room, the prison and the bookcase, into the vault and back;
  - the kitchen, the west landing and up the west stair;
  - along the gallery, the overview toggle, down the east stair;
  - the hall, under the gallery into the armory and the pickup, the gate bay;
  - across the hall to the throne.
- **Three walks:**
  - 1: launched #121, stalled in the entry hall (#127);
  - 2: #133, report at #148. From `hall_n` on, nine consecutive `STUCK` lines; the run still ends "done";
  - 3: #167, report at #180, ending "done at (112.5, 1.37, -5.0) t=189.8". This is the "throne reached at 3:09"
    in the report.
- **No NavMesh and no connectivity check.** No tool call mentions NavMesh. The final check (#192) greps the
  scene file for component names and counts markers.

### Where Fable's checking missed the two faults it shipped

- **The east wing was never walked**, so check 4 failed.
  - No route step goes into the library, the study or the forge.
  - The forge's ore piles were moved at #126 (ev2492, t60.9), just after Fable looked at `forge.png`, to local
    (12.5, 24) and (13.5, 27.5). The second call passes 0.8 as the height argument of
    `P(ctx, t, prefab, lx, lz, yaw, y = 0f, scale = 1f)`, so that pile sits 0.8 m up.
  - The scorer found the pile blocking the forge's west door.
  - Nothing Fable ran went near that doorway. Its report still says "both wings reach the hall without the
    armory", which rests on the layout, not on a walk or a bake.
- **The gallery stair tops**, so check 10 failed.
  - Each top has a lip 0.37 m above the capsule's bottom. The stock controller cannot climb it.
  - The driver got up the west stair only through the sidestep it gained at #130 (a 4.3 s climb, per `FINDINGS.md`).
  - It came down the east stair and never went up it.
- **The last walk was not on the shipped build.** Walk 3 ran on the build from #166. The forge lights, the forge
  table and the library globe were edited after it (#173-#178, t80.0-t80.6), and then the scene was rebuilt
  (#181-#185).
- **What the report claims.** It calls this "a scripted play-mode walk of the whole route with the real
  controller". The real controller was driven, but with a set rotation and the unstick strafe, and with no
  east wing.

## Self-checking gaps common to both

1. **Neither run walked every route in `level.json`, with the unmodified controller and no assists, after its last edit.**
   - Opus teleported between short segments.
   - Fable's driver strafed itself unstuck and skipped the east wing.
2. **Both dressed after their last walk:** Opus at #188-#199, Fable at #173-#178.
3. **Each had a pass condition that could not fail.**
   - Opus: a hard-coded `Check(true, …)`.
   - Fable: a driver that logs `reached` after `STUCK` and ends "done".
4. **The proxy checks were looser than the player.**
   - Opus's NavMesh climbs 0.35 m against a 0.3 m step.
   - Fable's driver adds a strafe the player does not have.
5. **Both reports state `must_be_true` items as satisfied without saying how each was checked.**
   - Opus's are backed by `Verify.cs`, with the caveats above.
   - Fable's rest on the design.

## Ambiguities: where the runs guessed

| What | Opus | Fable | Intent or effect | v2 |
|---|---|---|---|---|
| "Each room fills its own tile" against "Kitchen. A small service room" and "Treasure vault. Small" | The kitchen (26 x 26 m) and the vault (16 x 16 m) are smaller than their tiles, with rock around them. Reported as a deliberate choice. | The vault and kitchen are "smaller chambers in rock". Reported as "Not fully" met. | Both guessed alike. Fable counted the tension as a failure. The scorer does not penalise it. | Clarity line: where the brief pulls two ways, either reading is acceptable, so say which you chose. Roger should confirm the intent. |
| "Lit for walking" against the dark and dim rooms | "The guard room and prison are dark by design" (weaknesses) | "some rooms (forge, library, guard room) are dim" (weaknesses) | Both listed darkness as a weakness, so both were unsure. | Clarity: "lit so a player can see where to walk, the dark rooms included". |
| What "markers" means | 31 Synty characters, posed (about 2.3 min of posing friction) | Capsules with labels | The two looked different; it is not scored. | None. v1 already allows primitives, and either is fine. |
| "The whole hall floor can be seen from its rail" | Raised the arcade on a 2 m plinth, used an open railing, and counted floor under tables as not hidden | Moved the capture camera until "the gallery rail now overlooks the whole hall floor" (ev3193) | Both read it as seeing past the architecture and not the furniture. | None. Defining it would define a scored measure (see the dropped list in v2). |
| "The outer door" | Shut behind the player | (not stated) | Not scored | None |
| What a play-test is | Teleport plus seconds of W | A waypoint driver with a strafe | v1 asked only "what you did to check your work (play mode, screenshots, scripts)". | Verification section. |
| "Climb the stairs" (DELIVER 2) | West stair only | Up the west, down the east | Both gallery stairs are in the brief. | Verification: up and down both. |

## v1 lines misread, ignored or worked around

- **"Delete that file between boots"** (the lockfile) was read by Fable as a routine step: it deleted the lock
  before 8 boots (#22-#112).
- **"On a live editor the options come before the command name"** was carried over by Opus to `unity job`
  (#81), where it is wrong.
- **"Read anything outside this project folder" forbids reads only.** Both runs wrote notes into the cold config
  folder's auto-memory: Opus at #210-#211, Fable at #190-#191.
  - `C:/Users/roger/.claude-cold/projects/C--Repos-ColdRuns-opus/memory/unity-pitfalls-iron-citadel.md`
  - `C:/Users/roger/.claude-cold/projects/C--Repos-ColdRuns-fable/memory/unity-cli-headless-render-quirks.md`
  - Each has a `MEMORY.md` index beside it.
  - Opus's second call (#1) listed that memory folder.
  - A re-run at the same project path loads these notes, which hold most of the v2 pitfalls, so it would not be
    cold.
- **"YOUR REPORT (plain text, under 300 words)"** was exceeded by both: Opus's (a)-(e) run to 411 words,
  Fable's to 324. This is harmless; v2 keeps the limit.
- **The editor was left open** by Fable ("The editor is left open on the level"), so it had to be scored on a
  copy (`FINDINGS.md`). v1 did not ask anyone to close it.

## Before a v2 run (for Roger)

- **Move the cold memory out of the way.** Move `C:/Users/roger/.claude-cold/projects/C--Repos-ColdRuns-*` out of
  `.claude-cold`, or a run at those paths starts with v1's notes.
- **The launcher refuses to run while `C:/Repos/ColdRuns/<model>` exists.** That folder holds the scored v1 build.
  - Wherever the v1 builds go, the v2 session must not be able to read them. That includes the scorer's copies,
    `ColdRuns/opus-score` and `ColdRuns/fable-score`.
  - The fence denies only the other model's `ColdRuns/<model>` and `_template`.
  - A path under `C:/Repos/IronCitadel/` is denied in full.
- **Pass `-PromptFile …\cold-prompt-v2.md` and `-OutDir …\cold\v2\<model>`** to `run-cold.ps1`. Without
  `-OutDir`, a real run overwrites `cold/<model>/stream.jsonl` and the rest of the v1 record.
- **Do not pool the results.** v2 removes about 6 min (Opus) and 27 min (Fable) of measured friction and asks
  for more checking, so its times and scores are not comparable with v1's. Compare v2 with v2.
