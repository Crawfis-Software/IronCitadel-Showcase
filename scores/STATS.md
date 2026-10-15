# Iron Citadel: what each build is made of

Measured 2026-10-07 (Haiku 2026-10-11) with one Unity editor probe, run the same way on every scene (`kit/stats/Editor/LevelStats.cs`, Unity 6000.6.0f1). Fable, Opus and Haiku are the three cold AI runs on the v1 prompt. "Ours" is two levels built through the framework: the rooms level (themed-room prefabs) and the patterns level (pattern tiles). The raw numbers are in `scores/stats.json`, and each scene's probe output is in `logs/stats/probe-*.json`.

## Side by side

"ours combined" is filled in only where a union makes sense (unique assets). Totals such as renderers or lights are per level.

| measure | Fable | Opus | Haiku | ours rooms | ours patterns | ours combined |
|---|---|---|---|---|---|---|
| Scene file on disk | 8.4 MB | 18.5 MB | 8.2 MB | 372 KB | 166 KB | — |
| Level data on disk (scene + own nested prefabs) | 8.4 MB (scene only) | 18.5 MB (scene only) | 8.2 MB (scene only) | 19.8 MB (scene + 80 own prefab files) | 15.8 MB (scene + 130 own prefab files) | 35.6 MB (sum) |
| GameObjects (all / active) | 3,443 / 3,441 | 8,033 / 7,734 | 3,116 / 3,116 | 10,362 / 9,665 | 7,123 / 7,123 | — |
| Deepest hierarchy (levels) | 5 | 16 | 4 | 17 | 10 | — |
| Active renderers | 3,231 (3,073 mesh, 158 particle) | 5,215 (4,940 mesh, 244 particle, 31 skinned) | 2,874 (2,874 mesh) | 6,959 (6,483 mesh, 445 particle, 31 skinned) | 5,859 (5,764 mesh, 95 particle) | — |
| Triangles drawn (sum over renderers) | 1,833,802 | 3,219,567 | 1,808,102 | 4,346,759 | 3,565,048 | — |
| Triangles in the unique meshes | 205,720 | 293,019 | 48,310 | 357,241 | 158,346 | — |
| (a) Outermost prefab instances placed | 2,555 | 4,929 | 2,829 | 56 | 2 | — |
| (a) Unique outermost prefab assets | 203 | 292 | 55 | 33 | 2 | 34 (union) |
| Unique prefab files at any nesting depth | 208 | 297 | 60 | 527 | 338 | 704 (union) |
| Deepest prefab nesting under a renderer | 3 | 3 | 3 | 4 | 5 | — |
| (b) Unique leaf source assets (prefab or model) | 207 | 295 | 57 | 442 | 203 | 497 (union) |
| Unique Synty pieces (leaf prefabs from Synty packs) | 202 | 291 | 54 | 441 | 202 | 496 (union) |
| Prefab files at any depth, by kind | 202 Synty, 6 player kit | 291 Synty, 6 player kit | 54 Synty, 6 player kit | 11 rooms, 69 set pieces, 441 Synty, 6 player kit | 1 level, 6 pattern tiles, 123 set pieces, 202 Synty, 6 player kit | 1 level, 11 rooms, 6 pattern tiles, 184 set pieces, 496 Synty, 6 player kit (union) |
| Renderers in prefab instances | 2,620 | 5,131 | 2,841 | 6,959 | 5,859 | — |
| Renderers outside any prefab | 611 (578 primitives, 33 no mesh (text)) | 84 (73 primitives, 11 no mesh (text)) | 33 (33 primitives) | 0 | 0 | — |
| Built-in primitive renderers | 579 (545 cube, 32 capsule, 2 sphere) | 74 (37 cylinder, 36 cube, 1 capsule) | 34 (32 capsule, 2 sphere) | 1 (1 capsule) | 1 (1 capsule) | — |
| Unique meshes | 210 (0 procedural, 3 built-in) | 335 (0 procedural, 3 built-in) | 59 (0 procedural, 2 built-in) | 459 (0 procedural, 1 built-in) | 208 (0 procedural, 1 built-in) | 519 (union) |
| Mesh renderers per unique mesh (reuse) | 14.5 | 14.8 | 48.7 | 14.2 | 27.7 | — |
| Unique materials | 17 (0 scene-embedded, 1 Unity default) | 20 (0 scene-embedded, 2 Unity default) | 5 (0 scene-embedded, 0 Unity default) | 14 (0 scene-embedded, 0 Unity default) | 6 (0 scene-embedded, 0 Unity default) | 14 (union) |
| Unique textures | 14 (2 built-in) | 18 (2 built-in) | 5 (0 built-in) | 16 (0 built-in) | 6 (0 built-in) | 16 (union) |
| Lights | 178 (178 point; 178 realtime; 1 cast shadows) | 395 (395 point; 395 realtime; 0 cast shadows) | 213 (213 point; 213 realtime; 2 cast shadows) | 501 (498 point, 3 directional; 501 realtime; 0 cast shadows) | 97 (96 point, 1 directional; 97 realtime; 1 cast shadows) | — |
| Baked lightmaps | 0 | 0 | 0 | 0 | 0 | — |
| Particle systems | 158 | 244 | 0 | 445 | 95 | — |
| Colliders | 2,637 (1393 box, 1191 mesh, 32 capsule, 20 sphere, 1 CC) | 1,235 (624 box, 587 mesh, 23 capsule, 1 CC) | 2,943 (2314 box, 594 mesh, 32 capsule, 2 sphere, 1 CC) | 6,603 (4372 mesh, 2197 box, 32 capsule, 1 CC, 1 sphere) | 5,786 (3029 mesh, 2754 box, 1 capsule, 1 CC, 1 sphere) | — |
| Reflection probes / light probe groups / post volumes | 0 / 0 (0 probes) / 1 | 0 / 0 (0 probes) / 1 | 0 / 0 (0 probes) / 0 | 0 / 0 (0 probes) / 12 | 0 / 0 (0 probes) / 72 | — |
| Authored code | 15 C# files, 2,707 lines (+1 shader, 42 lines) | 19 C# files, 3,254 lines | 13 C# files, 1,568 lines | framework; rooms assembler 2,278 lines (see note) | framework; patterns scene builder 171 lines (see note) | — |

(a) counts the prefab instances placed at the top of the scene hierarchy. (b) follows every active renderer down through its prefab nesting to the asset its mesh really comes from: a Synty prefab, a model file, or the mesh itself when the renderer is in no prefab. "Synty pieces" is (b) limited to the Synty packs and keyed by pack and file name, so the same piece counts once in every project.

## Geometry audit

One headless prop audit, run the same way on the three cold scenes and on our rooms scene (the patterns level has no room cells, so it has no audit yet). Every loose piece is dropped, pushed and ray-cast against the structure the build named: a prop inside another prop, a prop poking through the ceiling, a prop sunk into a wall, a prop hanging in the air over nothing, a torch or banner mounted on nothing. "Dark floor" is the share of free floor points that no light reaches, at 1 m spacing. The per-room table sets each room's dark share against the light word the brief gave it.

| audit | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---:|---:|---:|---:|---:|
| pieces audited | 2,959 | 4,764 | 2,829 | 5,963 | — |
| props tested (loose, not structure) | 824 | 2,029 | 357 | 3,208 | — |
| overlaps (prop in prop) | 15 | 99 | 0 | 4 | — |
| through the lid | 74 | 43 | 2 | 1 | — |
| wall clips | 59 | 163 | 3 | 2 | — |
| floating, confirmed | 6 | 3 | 14 | 4 | — |
| unattached wall mounts and hangings | 16 | 22 | 0 | 6 | — |
| dark floor, whole level | 14.1% | 6.7% | 31.7% | 27.4% | — |
| lights realtime / mixed / baked | 178 / 0 / 0 | 395 / 0 / 0 | 213 / 0 / 0 | 487 / 0 / 0 | — |
| structure by name: floor / wall / ceiling / rock | 406 / 621 / 393 / 513 | 690 / 1080 / 630 / 36 | 1028 / 484 / 892 / 0 | 443 / 1051 / 549 / 0 | — |
| skipped: markers / characters; pieces outside the cells | 66 / 0; 5 | 37 / 93; 5 | 33 / 0; 130 | 0 / 80; 1 | — |

Dark floor per room, with the room's light count in brackets:

| room | brief | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---|---:|---:|---:|---:|---:|
| entry_hall | moderate | 4% (18) | 1% (46) | 25% (19) | 18% (39) | — |
| guard_room | dim | 24% (6) | 8% (31) | 26% (17) | 17% (29) | — |
| prison | dark | 21% (11) | 19% (28) | 38% (18) | 82% (11) | — |
| vault | dim | 1% (5) | 1% (7) | 25% (18) | 75% (6) | — |
| kitchen | firelit | 10% (11) | 7% (16) | 25% (18) | 13% (55) | — |
| great_hall | bright | 14% (52) | 11% (61) | 53% (23) | 19% (88) | — |
| armory | moderate | 4% (15) | 1% (40) | 17% (20) | 10% (29) | — |
| throne_room | bright | 4% (23) | 12% (47) | 36% (23) | 17% (76) | — |
| library | moderate | 42% (14) | 7% (41) | 64% (19) | 27% (41) | — |
| study | moderate | 12% (14) | 2% (46) | 23% (19) | 22% (52) | — |
| forge | firelit | 18% (9) | 3% (32) | 22% (19) | 4% (61) | — |

The full finding lists are in `logs/audit/<build>/rooms-audit.md`; the thresholds are at the top of each.

## How the pieces are put together

- **Fable** placed 2,555 prefab instances straight into the scene, one per wall, tile, torch or prop: 203 different prefabs, almost all Synty. Beside them it built 611 renderers from code: 545 Unity cubes (513 flat `Rock` slabs under the solid-rock cells, 31 facing markers on the defender stand-ins and a stage face), 32 capsules (31 capsule stand-ins for the defenders, plus the player), 2 pickup spheres and 33 text labels. Its five own materials colour those primitives.
- **Opus** did the same at a larger scale: 4,929 flat instances of 292 prefabs. It placed real Synty characters (31 skinned meshes) where Fable used capsules. Only 84 of its renderers are built from code: 36 cube rock caps, 37 flat cylinders (33 map icons and 4 water or brew surfaces) and 11 text labels.
- **Haiku** also places pieces flat: 2,829 instances, but of only 55 prefabs, a far smaller kit than Fable's or Opus's (57 leaf assets against 207 and 295). Like Fable, it stands capsules in for the defenders: 33 renderers are built from code, 31 capsule stand-ins and 2 gold pickup spheres, coloured by its two own materials. Every stair tread and stage step is Synty's `SM_Env_Steps_01` scaled into a block (`scores/haiku/FINDINGS.md`).
- **Ours, rooms** places 56 instances: the 11 themed-room prefabs (one per room cell), the player, and 44 Synty pieces that the assembler adds for this level (31 characters, plus the portcullis and its spikes, the secret door's bookcases, the outer door, chests and coins). The rooms nest 69 different set-piece prefabs (86 instances: 37 from PolygonDungeon and 32 from PolygonDungeonRealms). The rooms and set pieces in turn nest the Synty pieces. Every renderer is inside a prefab, and none is built from code.
- **Ours, patterns** places one level prefab (`iron-citadel`) and the player. The level prefab nests 6 d-20 pattern tiles, each used once. The tiles nest 123 different set-piece prefabs (185 instances) and Synty PolygonDungeon pieces; the deepest renderer is 5 prefabs down.

The 11 rooms of the rooms level, largest first:

| room prefab | renderers | triangles |
|---|---:|---:|
| `HallGalleryBackFeastK9NESW_01352840.prefab` | 996 | 517,074 |
| `ArmoryAisledStockedK9NS_01352840.prefab` | 987 | 929,488 |
| `AlchemyLabWarrenBrewingK9NS_01352840.prefab` | 824 | 265,294 |
| `KitchenNotchCookingK9NE_01352840.prefab` | 817 | 284,447 |
| `ThroneRoomAisledCourtK9N_01352840.prefab` | 580 | 542,017 |
| `LibraryBoxOpenK9NE_01352840.prefab` | 561 | 545,868 |
| `PrisonBlockRiotK9NES_01352840.prefab` | 487 | 254,442 |
| `ForgeCellarWorkingK9NE_01352840.prefab` | 463 | 232,797 |
| `GuardRoomWatchMannedK9NE_01352840.prefab` | 447 | 223,504 |
| `GuardRoomLobbyMannedK9NESW_01352840.prefab` | 433 | 228,183 |
| `TreasuryLobbySealedK9N_01352840.prefab` | 291 | 191,926 |

The pattern tiles of the patterns level are `d-20-flanking-route-003.prefab`, `d-20-flanking-route-010.prefab`, `d-20-gallery-010.prefab`, `d-20-hidden-cache-005.prefab`, `d-20-sniper-location-013.prefab`, `d-20-stronghold-007.prefab`.

## Pack mix

### Unique leaf source assets by source

The innermost prefab or model each renderer comes from (its mesh, for a renderer in no prefab).

| source | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---:|---:|---:|---:|---:|
| Synty PolygonDungeon | 125 (60%) | 109 (37%) | 48 (84%) | 231 (52%) | 188 (93%) |
| Synty PolygonDungeonRealms | 47 (23%) | 122 (41%) | 5 (9%) | 181 (41%) | — |
| Synty PolygonDungeonMap | 16 (8%) | 30 (10%) | 1 (2%) | 29 (7%) | 14 (7%) |
| other Synty | 14 (7%) | 30 (10%) | — | — | — |
| player kit (Starter Assets / walker) | 1 (0%) | 1 (0%) | 1 (2%) | 1 (0%) | 1 (0%) |
| Unity built-in | 3 (1%) | 2 (1%) | 2 (4%) | — | — |
| no mesh | 1 (0%) | 1 (0%) | — | — | — |

### Unique meshes by source

Each unique mesh counted once.

| source | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---:|---:|---:|---:|---:|
| Synty PolygonDungeon | 127 (60%) | 113 (34%) | 51 (86%) | 226 (49%) | 193 (93%) |
| Synty PolygonDungeonRealms | 50 (24%) | 159 (47%) | 5 (8%) | 203 (44%) | — |
| Synty PolygonDungeonMap | 16 (8%) | 30 (9%) | 1 (2%) | 29 (6%) | 14 (7%) |
| other Synty | 14 (7%) | 30 (9%) | — | — | — |
| Unity built-in | 3 (1%) | 3 (1%) | 2 (3%) | 1 (0%) | 1 (0%) |

### Mesh renderers by source of their mesh

Every active renderer that draws a mesh, by where that mesh comes from.

| source | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---:|---:|---:|---:|---:|
| Synty PolygonDungeon | 1,954 (64%) | 2,283 (46%) | 2,759 (96%) | 4,344 (67%) | 5,718 (99%) |
| Synty PolygonDungeonRealms | 268 (9%) | 1,912 (39%) | 16 (1%) | 1,878 (29%) | — |
| Synty PolygonDungeonMap | 98 (3%) | 273 (6%) | 65 (2%) | 291 (4%) | 45 (1%) |
| other Synty | 141 (5%) | 418 (8%) | — | — | — |
| Unity built-in | 579 (19%) | 74 (1%) | 34 (1%) | 1 (0%) | 1 (0%) |

### Unique materials by source

Materials on active renderers.

| source | Fable | Opus | Haiku | ours rooms | ours patterns |
|---|---:|---:|---:|---:|---:|
| Synty PolygonDungeon | 3 (18%) | 3 (15%) | 1 (20%) | 6 (43%) | 5 (83%) |
| Synty PolygonDungeonRealms | 6 (35%) | 8 (40%) | 1 (20%) | 7 (50%) | — |
| other Synty | 1 (6%) | 1 (5%) | — | — | — |
| project's own (generated / authored for this level) | 5 (29%) | 5 (25%) | 2 (40%) | — | — |
| player kit (Starter Assets / walker) | 1 (6%) | 1 (5%) | 1 (20%) | 1 (7%) | 1 (17%) |
| Unity built-in | 1 (6%) | 2 (10%) | — | — | — |

### The same Synty pieces across builds

This table counts the leaf Synty pieces each pair of builds has in common, by pack and file name. The bold diagonal is each build's own count.

| shared Synty pieces | Fable | Opus | Haiku | ours rooms | ours patterns | ours combined |
|---|---:|---:|---:|---:|---:|---:|
| Fable | **202** | 132 | 35 | 140 | 73 | 148 |
| Opus | 132 | **291** | 29 | 175 | 66 | 183 |
| Haiku | 35 | 29 | **54** | 36 | 34 | 39 |
| ours rooms | 140 | 175 | 36 | **441** | 147 | 441 |
| ours patterns | 73 | 66 | 34 | 147 | **202** | 202 |
| ours combined | 148 | 183 | 39 | 441 | 202 | **496** |

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

### Haiku

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `SM_Env_Tiles_01.prefab` | Synty PolygonDungeon | 898 | 898 | 623,212 |
| 2 | `SM_Env_Ceiling_Stone_Flat_01.prefab` | Synty PolygonDungeon | 891 | 891 | 117,612 |
| 3 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 146 | 146 | 100,448 |
| 4 | `SM_Env_Steps_01.prefab` | Synty PolygonDungeon | 130 | 130 | 20,280 |
| 5 | `SM_Env_Wall_04.prefab` | Synty PolygonDungeon | 102 | 102 | 78,540 |
| 6 | `SM_Env_Wall_05.prefab` | Synty PolygonDungeon | 100 | 100 | 78,200 |
| 7 | `SM_Env_Wall_03.prefab` | Synty PolygonDungeon | 98 | 98 | 74,480 |
| 8 | `SM_Prop_Bookcase_01.prefab` | Synty PolygonDungeonMap | 65 | 65 | 392,470 |
| 9 | `SM_Prop_Bookcase_02.prefab` | Synty PolygonDungeon | 54 | 54 | 83,538 |
| 10 | `SM_Prop_Bench_01.prefab` | Synty PolygonDungeon | 40 | 40 | 21,760 |
| 11 | `SM_Prop_WeaponRack_01.prefab` | Synty PolygonDungeon | 33 | 33 | 7,062 |
| 12 | `Capsule (Unity built-in primitive)` | Unity built-in | 31 | 31 | 25,792 |
| 13 | `SM_Item_Jar_01.prefab` | Synty PolygonDungeon | 28 | 28 | 14,224 |
| 14 | `SM_Prop_Table_01.prefab` | Synty PolygonDungeon | 25 | 25 | 49,100 |
| 15 | `SM_Prop_Candles_01.prefab` | Synty PolygonDungeon | 23 | 23 | 3,059 |

### ours rooms

| # | asset | source | instances | renderers | triangles |
|---:|---|---|---:|---:|---:|
| 1 | `FX_Fire_02.prefab` | Synty PolygonDungeon | 301 | 301 | 0 |
| 2 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 248 | 248 | 170,624 |
| 3 | `SM_Env_Ceiling_Stone_Flat_02.prefab` | Synty PolygonDungeon | 213 | 213 | 32,376 |
| 4 | `SM_Env_Wall_01_DoubleSided.prefab` | Synty PolygonDungeon | 187 | 187 | 219,912 |
| 5 | `SM_Env_Ceiling_Stone_Flat_01.prefab` | Synty PolygonDungeon | 177 | 177 | 23,364 |
| 6 | `SM_Prop_Torch_Ornate_02.prefab` | Synty PolygonDungeon | 162 | 162 | 109,188 |
| 7 | `SM_Prop_Torch_Ornate_01.prefab` | Synty PolygonDungeon | 148 | 148 | 80,512 |
| 8 | `SM_Env_Ceiling_Stone_Flat_03.prefab` | Synty PolygonDungeon | 138 | 138 | 23,736 |
| 9 | `SM_Env_Wall_05.prefab` | Synty PolygonDungeon | 118 | 118 | 92,276 |
| 10 | `SM_Env_Wall_03.prefab` | Synty PolygonDungeon | 109 | 109 | 82,840 |
| 11 | `SM_Prop_Tankard_01.prefab` | Synty PolygonDungeonRealms | 99 | 99 | 32,472 |
| 12 | `SM_Item_Potion_09.prefab` | Synty PolygonDungeon | 92 | 92 | 30,912 |
| 13 | `SM_Env_Wall_04.prefab` | Synty PolygonDungeon | 81 | 81 | 62,370 |
| 14 | `SM_Item_Potion_07.prefab` | Synty PolygonDungeon | 81 | 81 | 16,848 |
| 15 | `SM_Item_Potion_05.prefab` | Synty PolygonDungeon | 68 | 68 | 11,424 |

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
| 11 | `SM_Prop_Torch_Ornate_02.prefab` | Synty PolygonDungeon | 161 | 161 | 108,514 |
| 12 | `SM_Prop_Torch_Ornate_01.prefab` | Synty PolygonDungeon | 157 | 157 | 85,408 |
| 13 | `SM_Prop_Stool_01.prefab` | Synty PolygonDungeon | 137 | 137 | 19,317 |
| 14 | `SM_Env_Wall_01.prefab` | Synty PolygonDungeon | 136 | 136 | 93,568 |
| 15 | `SM_Prop_Bed_01.prefab` | Synty PolygonDungeon | 110 | 110 | 149,600 |

For the cold runs, ranking the outermost prefabs gives the same list without the primitives, because they place pieces flat. For ours, the outermost prefabs are the rooms and the level prefab, listed above.

## How measured

- **Probe.** One file, `C:/Repos/Github/Crawfis-Software/IronCitadel/kit/stats/Editor/LevelStats.cs`, was copied unchanged into `Assets/_Scoring/stats/Editor/` of `C:/Repos/ColdRuns/fable-score`, `C:/Repos/ColdRuns/opus-score` and `C:/Repos/ColdRuns/haiku-score`, and into `Assets/IronCitadel/Editor/` of MacroTileBuilder. Each run was `unity run <project> --timeout 1800 --no-tail -- -executeMethod LevelStatsProbe.LevelStats.Run -scene <scene> -out <json> -label <name>`, through the runner `logs/stats/run-stats.ps1`. The scene is opened in batch mode and never saved. `kit/stats/aggregate.py` turns the probe output into this file.
- **Scenes.** Fable: `fable-score/Assets/Scenes/IronCitadel.unity`. Opus: `opus-score/Assets/Scenes/IronCitadel.unity`, the scene its conformance run scored. `opus-score` is a robocopy of `ColdRuns/opus` made for this measurement, so neither original was opened or written. Haiku: `haiku-score/Assets/Scenes/IronCitadel.unity`, the robocopy its conformance run scored. Ours: `Assets/IronCitadel/Scenes/IronCitadel_Rooms.unity`, measured after the rebuild that `logs/rooms-rebuilt.ok` records, and `Assets/IronCitadel/Scenes/IronCitadel_Patterns.unity`.
- **What is walked.** The probe walks every GameObject and every active renderer (enabled, in an active hierarchy). Triangles come from the index counts of each renderer's mesh, summed over renderers. None of the five scenes has LOD groups, so nothing is double counted. Lights, colliders, probes and volumes count active, enabled components.
- **Nesting.** (a) is `PrefabUtility.IsOutermostPrefabInstanceRoot`. (b) is `GetCorrespondingObjectFromOriginalSource` on each renderer, or the mesh's asset for a renderer in no prefab. "Prefab files at any depth" walks `GetCorrespondingObjectFromSource` from every active GameObject up through each nesting level.
- **Unique meshes, materials and textures** are counted by asset path and object name. Procedural meshes (no asset path), Unity built-in primitives and Unity default materials are counted separately. No build has a procedural mesh; the table lists the primitives. Textures are only those that the materials' shaders use.
- **Pack** is read from the asset path. Our project keeps Synty under `Assets/_THIRD_PARTY/Synty/` (PolygonDungeonMap under `Assets/Synty/`), and the cold runs keep it under `Assets/Synty/`. Both map to the same pack names. "other Synty" is PolygonGeneric, which the cold-run template also ships.
- **The player rig** (the Starter Assets controller and the walker) is in every scene and is counted the same way everywhere: one leaf, one capsule and six prefab files.
- **Authored code, cold runs.** This counts the C# the model wrote under `Assets/` that is not in the cold-run template (`ColdRuns/_template`), so Starter Assets, the walker (`Assets/Interact`), TutorialInfo and `_Scoring` are left out. Lines are non-blank lines. Fable wrote 15 files with 2,707 lines (2,543 without comment lines), plus one 42-line shader. Opus wrote 19 files with 3,254 lines (3,041 without comments). Haiku wrote 13 files with 1,568 lines (1,410 without comments). Fable and Opus also left small helper scripts outside `Assets/`, in `Tools/`, which are not counted: 43 lines of sh and py for Opus, and a few one-line diagnostic snippets for Fable. Haiku kept its snippets in `Temp/` and deleted them before it finished.
- **Authored code, ours.** Ours is built on the framework (MacroTileBuilder, PrefabSynthesis, and the room and set-piece libraries), which is not counted. For reference, the Iron Citadel code in `MacroTileBuilder/Assets/IronCitadel` has 2,449 lines of level assembly, 1,457 lines of play and HUD, and 535 lines of capture and inspection tools. The assembly is the rooms assembler (2,278 lines, which also runs the door check, the captures and the lighting reset) and the patterns scene builder (171 lines). The conformance kit and the walker copies are left out everywhere.
- **Level data on disk** is the scene file plus our own prefab files that the scene nests: the rooms and set pieces for the rooms level, and the level prefab and its pattern tiles for the patterns level. The cold runs keep everything in the scene file. The room and set-piece prefabs come from the pre-built libraries and were not made for this level.
- **Geometry audit.** One file, `MacroTileBuilder/Assets/IronCitadel/Editor/IronCitadelAudit.cs` (its copy is `kit/audit/Editor/IronCitadelAudit.cs`), run with `-generic` through `kit/audit/run-audit.ps1`, which copies it with a two-class shim into `Assets/_Scoring/audit/Editor/` of each score copy and runs `unity run <project> -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelAudit.Run -generic -scene <scene> -level <level.json> -out logs/audit/<build>`. The scene is opened in batch mode, gets a mesh collider on every renderer, and is never saved. Rooms are the 45 m cells of `level.json`. A piece is the outermost Synty prefab instance over each renderer. Structure is read from the piece name: floor (`SM_Env_*Tiles*`, `*Floor*`, `*Stairs*`; turned over or 7 m up it is a ceiling), walls (`SM_Env_*Wall*`, door, arch, pillar, column, beam, support, railing, fence, trim, window) and ceiling (`*Ceiling*`); a built-in cube or plane counts as structure when its GameObject is named Floor, Wall, Lintel, UnderStair, Ceiling, Roof or Rock (Rock is solid fill, neither lid nor wall); other primitives (map icons, capsule stand-ins) and characters are skipped. Everything else is a prop, a wall mount (torches, banners, shelves) or a hanging (chandeliers, chains). Rays hit back faces, so a ray that starts inside a one-sided shelf board hits the board, not the floor under it. A prop is "floating, confirmed" when its smallest drop to the surface below is over 0.05 m AND nothing touches it within 0.05 m; "dark" is a free floor point 1.2 m up that no enabled light within its range (capped at 9 m) can see along a clear ray. The floor grid is sampled at x.37 so that it never lands on a tile seam.

### Caveats

- **The primitives are by design, not a flaw.** Fable built its solid rock as 513 flat cubes and its defenders as capsule stand-ins. Opus capped the rock with 36 cubes and drew map icons as flat cylinders. Haiku, like Fable, used capsules for its defenders. Those renderers are outside any prefab on purpose, which makes the cold runs' prefab counts a little smaller than their renderer counts.
- **Prefab counts measure different things in flat and nested builds.** The cold runs' outermost count (a) equals their piece count. Ours does not, because the rooms and the level prefab hold the pieces. To compare pieces, use (b), the Synty-pieces row or unique meshes.
- **Synty textures are atlases.** One material and one texture sheet cover most of a pack, so every build has few materials and textures. Those rows show which packs were used more than they show variety.
- **The patterns level is a different size.** It is 3 x 4 slots of 100 m (300 m x 400 m), not eleven 45 m cells, so its totals (renderers, triangles, colliders) are not like-for-like with the other four, which share the 45 m grid.
- **Lighting.** No build bakes anything: there are no lightmaps, light probes or reflection probes. The patterns level has 96 point lights against 318 Synty torch meshes, so most torches cast no light. Fable and Opus each add a global post-processing Volume to their scenes. Haiku has none, so its post-processing comes from the project's default volume profile. Ours add one global grade and a local mood volume per room (11 in the rooms level, 71 in the patterns level).
- **Scene file size does not measure content for ours.** The rooms and patterns scenes are small because the content lives in prefab files. The "level data on disk" row adds those files back.
- **The audit's seat artifacts are left out of the table.** "floating-touching" (a drop over 0.05 m but something within 0.05 m, mostly items seated a centimetre into a shelf), "void-below" (nothing within 6 m under the piece, mostly props resting on a single-sided floor plane the ray passes through) and "sunk" (bottom under the floor top, same cause) are seat and collider artifacts in every build, so they are counted in `stats.json` under `seatArtifacts` but not shown. Walls through lids are not tested. Fable's 513 rock cubes and Opus's 36 are structure ("rock") and block sight rays, but are neither a lid nor a wall for the prop tests. Haiku keeps 130 pieces outside the eleven room cells, which are not tested, and its open room walls give it the only void sight lines (341 free points see out of the level). Opus's invisible gameplay colliders (Floor, Wall, Lintel) have no renderer and are not pieces.
- **The audit's names are the builds'.** Ours mounts its torches on pieces the names call walls; Fable's 16 unattached are mostly torches on rock cubes or on nothing, and Opus's 22 are all torches on nothing. Opus's 163 wall clips are mostly 66 study shelves and 64 prison fence posts and panels set into the walls; Fable's 59 are fences, beds, axes and shelves.
