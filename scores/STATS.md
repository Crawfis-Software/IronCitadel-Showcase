# Iron Citadel: what each build is made of

Measured 2026-10-07 with one Unity editor probe, run the same way on every scene (`unity/ours/IronCitadel/Editor/LevelStats.cs`, Unity 6000.6.0f1). Fable and Opus are the two cold AI runs. "Ours" is two levels built through the framework: the rooms level (themed-room prefabs) and the patterns level (pattern tiles). The raw numbers are in `scores/stats.json`, and each scene's probe output is in `scores/probes/probe-*.json`.

## Side by side

"ours combined" is filled in only where a union makes sense (unique assets). Totals such as renderers or lights are per level.

| measure | Fable | Opus | ours rooms | ours patterns | ours combined |
|---|---|---|---|---|---|
| Scene file on disk | 8.4 MB | 18.5 MB | 348 KB | 21 KB | — |
| Level data on disk (scene + own nested prefabs) | 8.4 MB (scene only) | 18.5 MB (scene only) | 17.4 MB (scene + 78 own prefab files) | 12.0 MB (scene + 7 own prefab files) | 29.4 MB (sum) |
| GameObjects (all / active) | 3,443 / 3,441 | 8,033 / 7,734 | 9,494 / 8,797 | 5,215 / 5,215 | — |
| Deepest hierarchy (levels) | 5 | 16 | 17 | 6 | — |
| Active renderers | 3,231 (3,073 mesh, 158 particle) | 5,215 (4,940 mesh, 244 particle, 31 skinned) | 6,183 (6,058 mesh, 94 particle, 31 skinned) | 4,890 (4,890 mesh) | — |
| Triangles drawn (sum over renderers) | 1,833,802 | 3,219,567 | 3,819,386 | 2,842,753 | — |
| Triangles in the unique meshes | 205,720 | 293,019 | 355,571 | 144,443 | — |
| (a) Outermost prefab instances placed | 2,555 | 4,929 | 56 | 2 | — |
| (a) Unique outermost prefab assets | 203 | 292 | 33 | 2 | 34 (union) |
| Unique prefab files at any nesting depth | 208 | 297 | 522 | 191 | 584 (union) |
| Deepest prefab nesting under a renderer | 3 | 3 | 4 | 3 | — |
| (b) Unique leaf source assets (prefab or model) | 207 | 295 | 439 | 179 | 494 (union) |
| Unique Synty pieces (leaf prefabs from Synty packs) | 202 | 291 | 438 | 178 | 493 (union) |
| Prefab files at any depth, by kind | 202 Synty, 6 player kit | 291 Synty, 6 player kit | 11 rooms, 67 set pieces, 438 Synty, 6 player kit | 1 level, 6 pattern tiles, 178 Synty, 6 player kit | 1 level, 11 rooms, 6 pattern tiles, 67 set pieces, 493 Synty, 6 player kit (union) |
| Renderers in prefab instances | 2,620 | 5,131 | 6,183 | 4,890 | — |
| Renderers outside any prefab | 611 (578 primitives, 33 no mesh (text)) | 84 (73 primitives, 11 no mesh (text)) | 0 | 0 | — |
| Built-in primitive renderers | 579 (545 cube, 32 capsule, 2 sphere) | 74 (37 cylinder, 36 cube, 1 capsule) | 1 (1 capsule) | 1 (1 capsule) | — |
| Unique meshes | 210 (0 procedural, 3 built-in) | 335 (0 procedural, 3 built-in) | 460 (0 procedural, 1 built-in) | 189 (0 procedural, 1 built-in) | 520 (union) |
| Mesh renderers per unique mesh (reuse) | 14.5 | 14.8 | 13.2 | 25.9 | — |
| Unique materials | 17 (0 scene-embedded, 1 Unity default) | 20 (0 scene-embedded, 2 Unity default) | 11 (0 scene-embedded, 0 Unity default) | 4 (0 scene-embedded, 0 Unity default) | 11 (union) |
| Unique textures | 14 (2 built-in) | 18 (2 built-in) | 15 (0 built-in) | 6 (0 built-in) | 15 (union) |
| Lights | 178 (178 point; 178 realtime; 1 cast shadows) | 395 (395 point; 395 realtime; 0 cast shadows) | 461 (458 point, 3 directional; 461 realtime; 0 cast shadows) | 1 (1 directional; 1 realtime; 1 cast shadows) | — |
| Baked lightmaps | 0 | 0 | 0 | 0 | — |
| Particle systems | 158 | 244 | 94 | 0 | — |
| Colliders | 2,637 (1393 box, 1191 mesh, 32 capsule, 20 sphere, 1 CC) | 1,235 (624 box, 587 mesh, 23 capsule, 1 CC) | 6,144 (3948 mesh, 2162 box, 32 capsule, 1 CC, 1 sphere) | 4,893 (2641 box, 2249 mesh, 1 capsule, 1 CC, 1 sphere) | — |
| Reflection probes / light probe groups / post volumes | 0 / 0 (0 probes) / 1 | 0 / 0 (0 probes) / 1 | 0 / 0 (0 probes) / 0 | 0 / 0 (0 probes) / 0 | — |
| Authored code | 15 C# files, 2,707 lines (+1 shader, 42 lines) | 19 C# files, 3,254 lines | framework; rooms assembler 2,056 lines (see note) | framework; patterns scene builder 169 lines (see note) | — |

(a) counts the prefab instances placed at the top of the scene hierarchy. (b) follows every active renderer down through its prefab nesting to the asset its mesh really comes from: a Synty prefab, a model file, or the mesh itself when the renderer is in no prefab. "Synty pieces" is (b) limited to the Synty packs and keyed by pack and file name, so the same piece counts once in every project.

## How the pieces are put together

- **Fable** placed 2,555 prefab instances straight into the scene, one per wall, tile, torch or prop: 203 different prefabs, almost all Synty. Beside them it built 611 renderers from code: 545 Unity cubes (513 flat `Rock` slabs under the solid-rock cells, 31 facing markers on the defender stand-ins and a stage face), 32 capsules (31 capsule stand-ins for the defenders, plus the player), 2 pickup spheres and 33 text labels. Its five own materials colour those primitives.
- **Opus** did the same at a larger scale: 4,929 flat instances of 292 prefabs. It placed real Synty characters (31 skinned meshes) where Fable used capsules. Only 84 of its renderers are built from code: 36 cube rock caps, 37 flat cylinders (33 map icons and 4 water or brew surfaces) and 11 text labels.
- **Ours, rooms** places 56 instances: the 11 themed-room prefabs (one per room cell), the player, and 44 Synty pieces that the assembler adds for this level (31 characters, plus the portcullis and its spikes, the secret door's bookcases, the outer door, chests and coins). The rooms nest 67 different set-piece prefabs (80 instances: 41 from PolygonDungeon and 26 from PolygonDungeonRealms). The rooms and set pieces in turn nest the Synty pieces. Every renderer is inside a prefab, and none is built from code.
- **Ours, patterns** places one level prefab (`iron-citadel`) and the player. The level prefab nests 6 d-20 pattern tiles, each used once, and the tiles nest only Synty PolygonDungeon pieces. Every renderer is exactly three prefabs deep.

The 11 rooms of the rooms level, largest first:

| room prefab | renderers | triangles |
|---|---:|---:|
| `HallGalleryBackFeastK9NESW_01352840.prefab` | 924 | 511,987 |
| `KitchenNotchCookingK9NE_01352840.prefab` | 775 | 283,150 |
| `AlchemyLabWarrenBrewingK9NS_01352840.prefab` | 763 | 253,993 |
| `LibraryBoxOpenK9NE_01352840.prefab` | 615 | 552,502 |
| `ThroneRoomAisledCourtK9N_01352840.prefab` | 501 | 541,780 |
| `ArmoryRacksStockedK9NS_01352840.prefab` | 490 | 430,403 |
| `PrisonBlockRiotK9NES_01352840.prefab` | 478 | 250,769 |
| `GuardRoomLobbyMannedK9NESW_01352840.prefab` | 434 | 232,820 |
| `GuardRoomWatchMannedK9NE_01352840.prefab` | 426 | 219,330 |
| `ForgeCellarWorkingK9NE_01352840.prefab` | 420 | 236,981 |
| `TreasuryLobbySealedK9N_01352840.prefab` | 284 | 173,952 |

The pattern tiles of the patterns level are `d-20-flanking-route-003.prefab`, `d-20-flanking-route-010.prefab`, `d-20-gallery-010.prefab`, `d-20-hidden-cache-005.prefab`, `d-20-sniper-location-013.prefab`, `d-20-stronghold-007.prefab`.

## Pack mix

### Unique leaf source assets by source

The innermost prefab or model each renderer comes from (its mesh, for a renderer in no prefab).

| source | Fable | Opus | ours rooms | ours patterns |
|---|---:|---:|---:|---:|
| Synty PolygonDungeon | 125 (60%) | 109 (37%) | 226 (51%) | 178 (99%) |
| Synty PolygonDungeonRealms | 47 (23%) | 122 (41%) | 182 (41%) | — |
| Synty PolygonDungeonMap | 16 (8%) | 30 (10%) | 30 (7%) | — |
| other Synty | 14 (7%) | 30 (10%) | — | — |
| player kit (Starter Assets / walker) | 1 (0%) | 1 (0%) | 1 (0%) | 1 (1%) |
| Unity built-in | 3 (1%) | 2 (1%) | — | — |
| no mesh | 1 (0%) | 1 (0%) | — | — |

### Unique meshes by source

Each unique mesh counted once.

| source | Fable | Opus | ours rooms | ours patterns |
|---|---:|---:|---:|---:|
| Synty PolygonDungeon | 127 (60%) | 113 (34%) | 223 (48%) | 188 (99%) |
| Synty PolygonDungeonRealms | 50 (24%) | 159 (47%) | 206 (45%) | — |
| Synty PolygonDungeonMap | 16 (8%) | 30 (9%) | 30 (7%) | — |
| other Synty | 14 (7%) | 30 (9%) | — | — |
| Unity built-in | 3 (1%) | 3 (1%) | 1 (0%) | 1 (1%) |

### Mesh renderers by source of their mesh

Every active renderer that draws a mesh, by where that mesh comes from.

| source | Fable | Opus | ours rooms | ours patterns |
|---|---:|---:|---:|---:|
| Synty PolygonDungeon | 1,954 (64%) | 2,283 (46%) | 4,105 (67%) | 4,889 (100%) |
| Synty PolygonDungeonRealms | 268 (9%) | 1,912 (39%) | 1,631 (27%) | — |
| Synty PolygonDungeonMap | 98 (3%) | 273 (6%) | 352 (6%) | — |
| other Synty | 141 (5%) | 418 (8%) | — | — |
| Unity built-in | 579 (19%) | 74 (1%) | 1 (0%) | 1 (0%) |

### Unique materials by source

Materials on active renderers.

| source | Fable | Opus | ours rooms | ours patterns |
|---|---:|---:|---:|---:|
| Synty PolygonDungeon | 3 (18%) | 3 (15%) | 5 (45%) | 3 (75%) |
| Synty PolygonDungeonRealms | 6 (35%) | 8 (40%) | 5 (45%) | — |
| other Synty | 1 (6%) | 1 (5%) | — | — |
| project's own (generated / authored for this level) | 5 (29%) | 5 (25%) | — | — |
| player kit (Starter Assets / walker) | 1 (6%) | 1 (5%) | 1 (9%) | 1 (25%) |
| Unity built-in | 1 (6%) | 2 (10%) | — | — |

### The same Synty pieces across builds

This table counts the leaf Synty pieces each pair of builds has in common, by pack and file name. The bold diagonal is each build's own count.

| shared Synty pieces | Fable | Opus | ours rooms | ours patterns | ours combined |
|---|---:|---:|---:|---:|---:|
| Fable | **202** | 132 | 137 | 64 | 145 |
| Opus | 132 | **291** | 172 | 51 | 181 |
| ours rooms | 137 | 172 | **438** | 123 | 438 |
| ours patterns | 64 | 51 | 123 | **178** | 178 |
| ours combined | 145 | 181 | 438 | 178 | **493** |

## The 15 most-instanced assets

These are leaf source assets, ranked by placed copies. An instance is the nearest prefab instance that holds the renderer. Triangles are summed over all copies. FX prefabs draw particles, so they add no mesh triangles.

### Fable

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `Cube (Unity built-in primitive)` | Unity built-in | 545 | 545 | 6,540 |
| 2 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 282 | 282 | 194,016 |
| 3 | `SM_Env_Ceiling_Stone_Flat_01.prefab` | Synty PolygonDungeon | 144 | 144 | 19,008 |
| 4 | `SM_Env_Ceiling_Stone_Flat_02.prefab` | Synty PolygonDungeon | 126 | 126 | 19,152 |
| 5 | `SM_Env_Ceiling_Stone_Flat_03.prefab` | Synty PolygonDungeon | 122 | 122 | 20,984 |
| 6 | `SM_Env_Tiles_03.prefab` | Synty PolygonDungeon | 120 | 120 | 81,840 |
| 7 | `SM_Env_Wall_03.prefab` | Synty PolygonDungeon | 100 | 100 | 76,000 |
| 8 | `FX_Fire_02.prefab` | Synty PolygonDungeon | 89 | 89 | 0 |
| 9 | `SM_Prop_Torch_Ornate_01.prefab` | Synty PolygonDungeon | 87 | 87 | 47,328 |
| 10 | `SM_Env_Tiles_Ornate_01.prefab` | Synty PolygonDungeon | 70 | 70 | 134,960 |
| 11 | `SM_Env_Wall_04.prefab` | Synty PolygonDungeon | 70 | 70 | 53,900 |
| 12 | `SM_Env_Pillar_Square_06.prefab` | Synty PolygonDungeon | 58 | 58 | 23,200 |
| 13 | `SM_Env_Wall_DoorFrame_Double_Round_01.prefab` | Synty PolygonDungeon | 51 | 51 | 30,906 |
| 14 | `SM_Prop_WeaponRack_01.prefab` | Synty PolygonDungeon | 42 | 42 | 8,988 |
| 15 | `SM_Gen_Prop_Plate_01.fbx` | other Synty | 35 | 35 | 5,040 |

### Opus

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `SM_Env_Ceiling_Stone_Flat_01.prefab` | Synty PolygonDungeon | 629 | 629 | 83,028 |
| 2 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 253 | 253 | 174,064 |
| 3 | `SM_Env_Dwarf_Wall_01.prefab` | Synty PolygonDungeonRealms | 208 | 208 | 44,304 |
| 4 | `SM_Env_Dwarf_Floor_02.prefab` | Synty PolygonDungeonRealms | 154 | 154 | 77,000 |
| 5 | `SM_Env_Dwarf_Floor_01.prefab` | Synty PolygonDungeonRealms | 148 | 148 | 148,592 |
| 6 | `SM_Env_Basement_Support_Beam_01.prefab` | Synty PolygonDungeon | 142 | 142 | 5,112 |
| 7 | `FX_Fire_01.prefab` | Synty PolygonDungeon | 139 | 139 | 0 |
| 8 | `SM_Env_Dwarf_Floor_05.prefab` | Synty PolygonDungeonRealms | 120 | 120 | 81,840 |
| 9 | `SM_Prop_Dwarf_Chair_Bench_01.prefab` | Synty PolygonDungeonRealms | 117 | 117 | 23,634 |
| 10 | `SM_Prop_Dwarf_Torch_01.prefab` | Synty PolygonDungeonRealms | 109 | 109 | 27,032 |
| 11 | `SM_Env_Dwarf_Wall_02.prefab` | Synty PolygonDungeonRealms | 97 | 97 | 19,691 |
| 12 | `SM_Env_Tiles_01.prefab` | Synty PolygonDungeon | 66 | 66 | 45,804 |
| 13 | `SM_Gen_Prop_Shelf_01.prefab` | other Synty | 66 | 66 | 7,392 |
| 14 | `SM_Env_Tiles_03.prefab` | Synty PolygonDungeon | 57 | 57 | 38,874 |
| 15 | `SM_Env_Dwarf_Wall_06.prefab` | Synty PolygonDungeonRealms | 57 | 57 | 5,358 |

### ours rooms

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 247 | 247 | 169,936 |
| 2 | `SM_Env_Ceiling_Stone_Flat_02.prefab` | Synty PolygonDungeon | 206 | 206 | 31,312 |
| 3 | `SM_Env_Wall_01_DoubleSided.prefab` | Synty PolygonDungeon | 172 | 172 | 202,272 |
| 4 | `SM_Env_Ceiling_Stone_Flat_01.prefab` | Synty PolygonDungeon | 165 | 165 | 21,780 |
| 5 | `SM_Prop_Torch_Ornate_02.prefab` | Synty PolygonDungeon | 147 | 147 | 99,078 |
| 6 | `SM_Env_Ceiling_Stone_Flat_03.prefab` | Synty PolygonDungeon | 134 | 134 | 23,048 |
| 7 | `SM_Prop_Torch_Ornate_01.prefab` | Synty PolygonDungeon | 129 | 129 | 70,176 |
| 8 | `SM_Env_Wall_03.prefab` | Synty PolygonDungeon | 109 | 109 | 82,840 |
| 9 | `SM_Env_Wall_05.prefab` | Synty PolygonDungeon | 109 | 109 | 85,238 |
| 10 | `SM_Prop_Tankard_01.prefab` | Synty PolygonDungeonRealms | 102 | 102 | 33,456 |
| 11 | `SM_Item_Potion_09.prefab` | Synty PolygonDungeon | 89 | 89 | 29,904 |
| 12 | `SM_Env_Wall_04.prefab` | Synty PolygonDungeon | 81 | 81 | 62,370 |
| 13 | `SM_Item_Potion_07.prefab` | Synty PolygonDungeon | 79 | 79 | 16,432 |
| 14 | `SM_Item_Potion_05.prefab` | Synty PolygonDungeon | 68 | 68 | 11,424 |
| 15 | `SM_Prop_Goblet_01.prefab` | Synty PolygonDungeonRealms | 67 | 67 | 20,100 |

### ours patterns

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `SM_Env_Wall_03.prefab` | Synty PolygonDungeon | 331 | 331 | 251,560 |
| 2 | `SM_Env_Tiles_Texture_05.prefab` | Synty PolygonDungeon | 328 | 328 | 656 |
| 3 | `SM_Env_Wall_05.prefab` | Synty PolygonDungeon | 230 | 230 | 179,860 |
| 4 | `SM_Env_Wall_04.prefab` | Synty PolygonDungeon | 225 | 225 | 173,250 |
| 5 | `SM_Env_Tiles_Texture_06.prefab` | Synty PolygonDungeon | 223 | 223 | 446 |
| 6 | `SM_Env_Wall_01_Alt.prefab` | Synty PolygonDungeon | 219 | 219 | 161,622 |
| 7 | `SM_Env_Wall_01_DoubleSided.prefab` | Synty PolygonDungeon | 218 | 218 | 256,368 |
| 8 | `SM_Env_Tiles_Texture_03.prefab` | Synty PolygonDungeon | 206 | 206 | 412 |
| 9 | `SM_Env_Tiles_03.prefab` | Synty PolygonDungeon | 202 | 202 | 137,764 |
| 10 | `SM_Env_Tiles_Texture_02.prefab` | Synty PolygonDungeon | 171 | 171 | 342 |
| 11 | `SM_Prop_Torch_Ornate_02.prefab` | Synty PolygonDungeon | 156 | 156 | 105,144 |
| 12 | `SM_Prop_Torch_Ornate_01.prefab` | Synty PolygonDungeon | 153 | 153 | 83,232 |
| 13 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 136 | 136 | 93,568 |
| 14 | `SM_Prop_Stool_01.prefab` | Synty PolygonDungeon | 95 | 95 | 13,395 |
| 15 | `SM_Env_Tiles_Texture_08.prefab` | Synty PolygonDungeon | 91 | 91 | 182 |

For the cold runs, ranking the outermost prefabs gives the same list without the primitives, because they place pieces flat. For ours, the outermost prefabs are the rooms and the level prefab, listed above.

## How measured

- **Probe.** One file, `LevelStats.cs` (published as `unity/ours/IronCitadel/Editor/LevelStats.cs`), was copied unchanged into scoring copies of the Fable and Opus projects and into the project that holds our levels. Each run was `unity run <project> --timeout 1800 --no-tail -- -executeMethod LevelStatsProbe.LevelStats.Run -scene <scene> -out <json> -label <name>`, one scene at a time. The scene is opened in batch mode and never saved. A small aggregation script turns the probe output into this file.
- **Scenes.** Fable: `fable-score/Assets/Scenes/IronCitadel.unity`. Opus: `opus-score/Assets/Scenes/IronCitadel.unity`, the scene its conformance run scored. `opus-score` is a robocopy of `ColdRuns/opus` made for this measurement, so neither original was opened or written. Ours: `Assets/IronCitadel/Scenes/IronCitadel_Rooms.unity`, measured after the rebuild that `logs/rooms-rebuilt.ok` records, and `Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity`.
- **What is walked.** The probe walks every GameObject and every active renderer (enabled, in an active hierarchy). Triangles come from the index counts of each renderer's mesh, summed over renderers. None of the four scenes has LOD groups, so nothing is double counted. Lights, colliders, probes and volumes count active, enabled components.
- **Nesting.** (a) is `PrefabUtility.IsOutermostPrefabInstanceRoot`. (b) is `GetCorrespondingObjectFromOriginalSource` on each renderer, or the mesh's asset for a renderer in no prefab. "Prefab files at any depth" walks `GetCorrespondingObjectFromSource` from every active GameObject up through each nesting level.
- **Unique meshes, materials and textures** are counted by asset path and object name. Procedural meshes (no asset path), Unity built-in primitives and Unity default materials are counted separately. No build has a procedural mesh; the table lists the primitives. Textures are only those that the materials' shaders use.
- **Pack** is read from the asset path. Our project keeps Synty under `Assets/_THIRD_PARTY/Synty/` (PolygonDungeonMap under `Assets/Synty/`), and the cold runs keep it under `Assets/Synty/`. Both map to the same pack names. "other Synty" is PolygonGeneric, which the cold-run template also ships.
- **The player rig** (the Starter Assets controller and the walker) is in every scene and is counted the same way everywhere: one leaf, one capsule and six prefab files.
- **Authored code, cold runs.** This counts the C# the model wrote under `Assets/` that is not in the cold-run template (`ColdRuns/_template`), so Starter Assets, the walker (`Assets/Interact`), TutorialInfo and `_Scoring` are left out. Lines are non-blank lines. Fable wrote 15 files with 2,707 lines (2,543 without comment lines), plus one 42-line shader. Opus wrote 19 files with 3,254 lines (3,041 without comments). Both also left small helper scripts outside `Assets/`, in `Tools/`, which are not counted: 43 lines of sh and py for Opus, and a few one-line diagnostic snippets for Fable.
- **Authored code, ours.** Ours is built on the framework (MacroTileBuilder, PrefabSynthesis, and the room and set-piece libraries), which is not counted. For reference, the Iron Citadel code in `MacroTileBuilder/Assets/IronCitadel` has 2,225 lines of level assembly, 1,457 lines of play and HUD, and 535 lines of capture and inspection tools. The assembly is the rooms assembler (2,056 lines, which also runs the door check, the captures and the lighting reset) and the patterns scene builder (169 lines). The conformance kit and the walker copies are left out everywhere.
- **Level data on disk** is the scene file plus our own prefab files that the scene nests: the rooms and set pieces for the rooms level, and the level prefab and its pattern tiles for the patterns level. The cold runs keep everything in the scene file. The room and set-piece prefabs come from the pre-built libraries and were not made for this level.

### Caveats

- **The primitives are by design, not a flaw.** Fable built its solid rock as 513 flat cubes and its defenders as capsule stand-ins. Opus capped the rock with 36 cubes and drew map icons as flat cylinders. Those renderers are outside any prefab on purpose, which makes the cold runs' prefab counts a little smaller than their renderer counts.
- **Prefab counts measure different things in flat and nested builds.** The cold runs' outermost count (a) equals their piece count. Ours does not, because the rooms and the level prefab hold the pieces. To compare pieces, use (b), the Synty-pieces row or unique meshes.
- **Synty textures are atlases.** One material and one texture sheet cover most of a pack, so every build has few materials and textures. Those rows show which packs were used more than they show variety.
- **The patterns level is a different size.** It is 3 x 4 slots of 100 m (300 m x 400 m), not eleven 45 m cells, so its totals (renderers, triangles, colliders) are not like-for-like with the other three, which share the 45 m grid.
- **Lighting.** No build bakes anything: there are no lightmaps, light probes or reflection probes. The patterns level has no point lights at all. Its 309 torches are meshes only, so it is lit by one directional light and the default skybox ambient. Each cold run adds a global post-processing Volume to its scene. Ours have no Volume in the scene, so any post-processing comes from the project's default volume profile.
- **Scene file size does not measure content for ours.** The rooms and patterns scenes are small because the content lives in prefab files. The "level data on disk" row adds those files back.
