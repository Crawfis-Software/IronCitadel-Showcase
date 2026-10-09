# The Iron Citadel: one level brief, three builds

One dungeon level, *The Iron Citadel Infiltration*, built three ways in Unity 6 with the same Synty POLYGON
dungeon art:

- **Ours.** Built through our level framework. Eleven themed-room prefabs are placed on the level's 45 m
  grid. A second version of our level is assembled from pattern tiles.
- **Opus.** Claude Opus 5.5 (`claude-opus-5-5`, effort xhigh) in Claude Code, given the brief and a data
  file in an empty Unity project. It had no level framework and no access to our code.
- **Fable.** Claude Fable 5.1 (`claude-fable-5-1`, effort xhigh) on the same prompt, in its own copy of
  the same empty project.

Each AI build was one unattended run from one prompt. The same scorer measured all three builds against the
level's `must_be_true` list.

## Play it

The web builds run in the browser. Click the page to capture the mouse, then:

- WASD to walk, the mouse to look;
- E to open a door;
- M for the map;
- L to switch the headlight on or off.

The headlight is only in the web builds. The browser renders with tighter light limits than the films did,
so many rooms would otherwise be dark. The light is added when the page loads; the scenes themselves are
the ones that were scored.

Each build needs a desktop browser:

| Build | Page |
|---|---|
| Ours, rooms | [https://crawfis-software.github.io/IronCitadel-Showcase/web/rooms/](https://crawfis-software.github.io/IronCitadel-Showcase/web/rooms/) |
| Ours, patterns | [https://crawfis-software.github.io/IronCitadel-Showcase/web/patterns/](https://crawfis-software.github.io/IronCitadel-Showcase/web/patterns/) |
| Opus | [https://crawfis-software.github.io/IronCitadel-Showcase/web/opus/](https://crawfis-software.github.io/IronCitadel-Showcase/web/opus/) |
| Fable | [https://crawfis-software.github.io/IronCitadel-Showcase/web/fable/](https://crawfis-software.github.io/IronCitadel-Showcase/web/fable/) |

The landing page, [https://crawfis-software.github.io/IronCitadel-Showcase/](https://crawfis-software.github.io/IronCitadel-Showcase/), links all four and plays the films.

## Watch it

Each film is about three minutes long at 1080p. Each one follows the level's route through its three acts:

- the shut armory gate;
- the two wings around it;
- the great hall, its gallery and the throne room.

The films also show the secret bookcase door and the vault behind it. Captions name what the brief asked
for and what the build did.

- [`videos/iron-citadel-rooms-1080p.mp4`](videos/iron-citadel-rooms-1080p.mp4): ours, the rooms level
- [`videos/iron-citadel-opus-1080p.mp4`](videos/iron-citadel-opus-1080p.mp4): Opus
- [`videos/iron-citadel-fable-1080p.mp4`](videos/iron-citadel-fable-1080p.mp4): Fable

## Results

The scorer bakes a NavMesh and checks every `must_be_true` line. A first-person CharacterController
(radius 0.35 m, height 1.8 m, step 0.3 m) then walks six routes. One check, whether the closed secret door
looks like the bookcases beside it, is left to the eye.

| | Ours, rooms | Opus | Fable |
|---|---|---|---|
| Conformance (pass / fail / by eye) | **10 / 0 / 1** | **10 / 0 / 1** | **8 / 2 / 1** |
| Routes walked | 6 of 6 | 6 of 6 | 4 of 6 |
| Wall clock | (framework build) | 1:13:33 | 1:28:48 |
| Tool calls / turns | — | 212 / 214 | 193 / 197 |
| Cost at API list price | — | $29.57 | $45.41 |
| Prefab instances placed in the scene | 56 | 4,929 | 2,555 |
| Unique Synty pieces used | 438 | 291 | 202 |
| Renderers built from code (primitives, text) | 0 | 84 | 611 |
| Authored C# | framework | 19 files, 3,254 lines | 15 files, 2,707 lines |

The full measurements are in [`scores/STATS.md`](scores/STATS.md), and each build's report is
`scores/<build>/conformance.md`. The [comparison report](https://crawfis-software.github.io/IronCitadel-Showcase/report/)
(`report/`) shows the same numbers check by check, with the maps the scorer saw.

Our patterns level is 3 × 4 pattern slots of 100 m, not 45 m rooms, so the room checks have nothing to measure.
Instead it was walked: 8 of 8 routes, from the entry to the warlord and out
([`scores/patterns/walk.md`](scores/patterns/walk.md)).

### What the scorer found in Fable's build

- **The east wing is blocked.** Two ore piles that Fable placed in the forge's west doorway close it, and
  no NavMesh crosses. A walker gets in only by climbing onto a pile. Fable's own play test walked only the
  west wing. The Fable film shows the doorway.
- **The gallery stairs snag.** At the top of each stair, the shaft's end wall stands 0.24 m above the end of
  the ramp. From where the walker stands, that is a 0.37 m rise, more than its 0.3 m step. Fable's play-test driver got over it
  only with a sidestep it wrote to free itself.

The details are in [`scores/fable/FINDINGS.md`](scores/fable/FINDINGS.md).

## What is here

| Folder | Contents |
|---|---|
| `prompts/` | The designer's brief (`brief.md`), the level data (`level.json`), the prompt as it was sent to both models (`prompt-as-sent.md`), its earlier and later versions, and the lessons from writing it (`prompt-lessons.md`) |
| `runs/` | For each AI run: its summary (time, tool calls, tokens, cost), its final reply, its settings, and the audit showing it touched none of our files |
| `report/` | The comparison report: every check, the scorer's maps, and the side-by-side measurements, as one page |
| `scores/` | Conformance reports and maps for each build, the patterns walk, the side-by-side stats (`STATS.md`, `stats.json`) and the raw stats probes |
| `videos/` | The three films |
| `web/` | The four WebGL builds |
| `unity/` | The Unity sources of each build, without the Synty art (see below) |

## Opening the sources

The Synty art is licensed and is not in this repo. To open a build in the editor you need:

- Unity 6000.6.0f1 with URP;
- the Synty POLYGON packs Dungeons, Dungeon Realms and Fantasy Dungeon Map (with the POLYGON Generic
  folder they bring), imported at their default paths;
- Unity's Starter Assets first-person controller;
- the packages Input System, AI Navigation and Newtonsoft Json.

Give each build its own project:

- **Opus or Fable:** copy `unity/<build>/IronCitadel` and `unity/<build>/Scenes` into `Assets/`, and copy
  `unity/player-kit/Interact` (the player prefab both models were given) into `Assets/Interact`.
- **Ours:** copy `unity/ours/IronCitadel` and `unity/ours/CrawfisSoftware` into `Assets/`. Our build
  carries its own copy of the player kit in `IronCitadel/Walker`, so do not add `Interact` as well; the two
  share assembly names.

A few things need our framework, which is not public:

- The rooms and set pieces were made by our prefab synthesis.
- The patterns scene was assembled by a tool that needs our tile builder, `IronCitadel/Editor/Patterns`.

The scenes and prefabs that tooling produced are all here.

## License

- **Code** (the C# under `unity/`, and `index.html`) is under the MIT License, in [`LICENSE`](LICENSE).
- **Everything else we made** is under [CC BY 4.0](LICENSE-CC-BY-4.0.txt): the brief, the level data, the prompts,
  the reports, the films, and the scenes and prefabs under `unity/`.
- **Third-party parts keep their own terms.** The Synty art is drawn in the films and compiled into the web
  builds, and Synty's license still covers it. The same goes for the Unity runtime in the web builds and for
  Unity's Starter Assets controller that the player kit uses. None of the Synty or Starter Assets source files
  are in this repo.
