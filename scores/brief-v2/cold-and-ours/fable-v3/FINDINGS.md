# Fable v3.1 cold run (brief v2): score and findings

- Build: `C:/Repos/ColdRuns/fable-v3`, scene `Assets/Scenes/IronCitadel.unity`, map `Map/map.json`.
  - Prompt `cold/cold-prompt-v3.1.md` (v3 plus change 19, pack art only); brief `cold/brief-v2-draft.md`.
  - The run took 1:39:24 wall clock (09:21 to 11:00 on 2026-10-14), with 1:18:05 of API time, at effort xhigh.
  - It made 236 tool calls over 237 turns and cost $36.55 at list price, billed to the Max plan.
  - The audit found 0 hits; no web, skill or agent calls. Outputs: `cold/v3/fable/`.
  - A first launch at 08:53 was stopped at 9 minutes by Roger; its outputs are in `cold/v3/_aborted/`.
- Scored on a copy, `C:/Repos/ColdRuns/fable-v3-score`, with kit v2 1.1 and the run's own `map.json`.
- **Result: 10 pass, 1 fail, 0 manual** (`conformance.md`). All five walks reach their end. The run reported 13
  static checks, 5 walks and 4 play-mode checks, all passing.
- Film: `flythrough/cold-v3/fable/iron-citadel-v2-fable-1080p.mp4`, 2:16, shot on `fable-v3-score`.

## The fail: 1b, the guards are seen from the entry hall

The armory's entrance is an open doorway 2.84 m wide, and the start stands 8 m straight in front of it. Twenty
entry-hall samples more than 5 m from the doorway see an armory enemy, up to 10.5 m back, and the start sees the
ranged guard in line with the doorway. Every one of those sight lines passes through the doorway, not a wall gap.
The run's own check placed and faced the guards but never measured what the entry hall sees.

## Kit 1.0 scored it 8 pass, 3 fail

Rows 7 and 8 failed because the map lists the deck as an `extra` room, "Gallery", with its stair door "Stair hall -
Gallery". The kit counts the deck as the dining hall's. Kit 1.1 folds such a room into the gallery (kit README,
Changes). The 1.0 result is in `scores/v3/_kit-1.0/fable-v3/`.

## Notes for the talk

- **The vault's way in** is a bricked-up doorway in the crypt that sinks on E: not a bookcase. The vault probe's
  Fable answers chose a crypt niche when they did not choose a bookcase.
- Its own weaknesses: "the hidden door reads as a sealed doorway, which is itself a hint"; dark rooms are dark;
  stacked wall tiers show joints; the throne is small; map labels overlap.
- Its suggestions: say that CLI-driven play mode needs run-in-background and that recompiles need an asset refresh
  first; give the module sizes and which meshes are single-sided; allow a double-sided wall or a secret-door prefab.
