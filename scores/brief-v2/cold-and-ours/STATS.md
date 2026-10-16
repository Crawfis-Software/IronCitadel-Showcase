# Brief v2 builds: what each one placed

Measured by `kit/stats/Editor/LevelStats.cs` on each scored scene (the copies the kit scored); code counted in each
run's own project. Written by `kit/stats/aggregate_v3.py`; every number comes from `logs/stats/probe-<label>.json`.

| | Opus v3 | Fable v3.1 | Haiku v3.1 | Ours v2 | Opus v3, manor | Ours, manor |
|---|---|---|---|---|---|---|
| Prefab instances placed (outermost) | 1,267 | 1,258 | 679 | 72 | 1,548 | 85 |
| Unique prefabs placed (outermost) | 255 | 283 | 66 | 29 | 268 | 29 |
| Unique Synty pieces (leaf prefabs) | 254 | 282 | 65 | 495 | 267 | 185 |
| Active renderers | 1,367 | 1,338 | 722 | 6,227 | 3,618 | 13,127 |
| Renderers outside any prefab | 62 | 64 | 33 | 0 | 708 | 0 |
| Built-in primitive renderers | 63 (27 cube, 26 capsule, 10 cylinder) | 65 (32 capsule, 32 cube, 1 sphere) | 34 (32 capsule, 2 cube) | 1 (1 capsule) | 57 (29 cylinder, 27 cube, 1 capsule) | 1 (1 capsule) |
| Triangles drawn | 1,011,857 | 984,404 | 507,652 | 4,039,679 | 1,188,055 | 4,005,753 |
| GameObjects (active) | 1,619 | 1,612 | 868 | 8,809 | 5,341 | 18,924 |
| Lights (active) | 86 (84 point, 2 spot) | 98 (98 point) | 44 (44 point) | 309 (306 point, 3 directional) | 101 (97 point, 4 spot) | 471 (468 point, 3 directional) |
| Colliders (active) | 967 | 1,041 | 802 | 6,278 | 890 | 10,642 |
| Authored code (non-blank lines) | 22 files, 3,423 lines (builder 1,312, runtime 285, checks and tools 1,826) | 20 files, 2,903 lines (builder 1,218, runtime 483, checks and tools 1,202) | 11 files, 2,243 lines (builder 712, runtime 224, checks and tools 1,307) | framework (see note) | 32 files, 4,403 lines (builder 2,259, runtime 389, checks and tools 1,755) | framework (see note) |

- **Outermost** prefab instances are the ones placed straight into the scene; a prefab nested in another counts once,
  as its outer prefab. **Synty pieces** are the distinct pack prefabs under every renderer, at any nesting depth.
- **Built-in primitives** are Unity cubes, capsules and the like: brief v2 allows them as enemy and pickup markers only.
- **Code** roles: builder = the scripts that make the scene; runtime = scripts the scene runs (doors, HUD, rooms);
  checks and tools = the run's own checks, walks, captures and probes. The template's `Assets/Interact` is not counted.
- Haiku is measured on `ColdRuns/haiku-v3-diag`, the copy whose runtime scripts were reimported (see its FINDINGS.md).

## Code by file

<details><summary>Opus v3: 22 files, 3,423 non-blank lines</summary>

| File | Role | Lines |
|---|---|---|
| `Tools/BuildLevel.cs` | builder | 1,312 |
| `Checks/Editor/CheckRunner.cs` | checks/tools | 115 |
| `Checks/Editor/MapExport.cs` | checks/tools | 124 |
| `Checks/Editor/SceneQuery.cs` | checks/tools | 213 |
| `Checks/Editor/StaticChecks.cs` | checks/tools | 459 |
| `Checks/Runtime/WalkRunner.cs` | checks/tools | 279 |
| `Checks/run_checks.sh` | checks/tools | 20 |
| `Tools/Captures.cs` | checks/tools | 150 |
| `Tools/Catalog.cs` | checks/tools | 65 |
| `Tools/Measure.cs` | checks/tools | 48 |
| `Tools/OverviewSteps.cs` | checks/tools | 42 |
| `Tools/Probe.cs` | checks/tools | 84 |
| `Tools/RailDebug.cs` | checks/tools | 29 |
| `Tools/Shot.cs` | checks/tools | 86 |
| `Tools/VaultShot.cs` | checks/tools | 96 |
| `Tools/sheet.py` | checks/tools | 16 |
| `Assets/IronCitadel/Scripts/DoorLink.cs` | runtime | 17 |
| `Assets/IronCitadel/Scripts/LevelHUD.cs` | runtime | 116 |
| `Assets/IronCitadel/Scripts/Marker.cs` | runtime | 14 |
| `Assets/IronCitadel/Scripts/Room.cs` | runtime | 53 |
| `Assets/IronCitadel/Scripts/SecretBookcase.cs` | runtime | 37 |
| `Assets/IronCitadel/Scripts/SwingDoor.cs` | runtime | 48 |

</details>

<details><summary>Fable v3.1: 20 files, 2,903 non-blank lines</summary>

| File | Role | Lines |
|---|---|---|
| `Assets/IronCitadel/Editor/IronCitadelBuilder.cs` | builder | 536 |
| `Assets/IronCitadel/Editor/IronCitadelDressing.cs` | builder | 682 |
| `Assets/IronCitadel/Editor/IronCitadelCaptures.cs` | checks/tools | 105 |
| `Assets/IronCitadel/Editor/IronCitadelChecks.cs` | checks/tools | 717 |
| `Tools/ProbeBounds.cs` | checks/tools | 41 |
| `Tools/ProbeFront.cs` | checks/tools | 71 |
| `Tools/ProbePieces.cs` | checks/tools | 95 |
| `Tools/RailShow.cs` | checks/tools | 33 |
| `Tools/Showcase.cs` | checks/tools | 102 |
| `Tools/rebuild.sh` | checks/tools | 26 |
| `Tools/runchecks.sh` | checks/tools | 12 |
| `Assets/IronCitadel/Runtime/DoorInteract.cs` | runtime | 28 |
| `Assets/IronCitadel/Runtime/IronDoor.cs` | runtime | 13 |
| `Assets/IronCitadel/Runtime/IronMarker.cs` | runtime | 11 |
| `Assets/IronCitadel/Runtime/IronRoom.cs` | runtime | 64 |
| `Assets/IronCitadel/Runtime/LevelHud.cs` | runtime | 67 |
| `Assets/IronCitadel/Runtime/OverviewMap.cs` | runtime | 97 |
| `Assets/IronCitadel/Runtime/SecretDoor.cs` | runtime | 23 |
| `Assets/IronCitadel/Runtime/ThroneTrigger.cs` | runtime | 19 |
| `Assets/IronCitadel/Runtime/WalkDriver.cs` | runtime | 161 |

</details>

<details><summary>Haiku v3.1: 11 files, 2,243 non-blank lines</summary>

| File | Role | Lines |
|---|---|---|
| `Build/BuildLevel.cs` | builder | 712 |
| `Build/ExportMap.cs` | checks/tools | 182 |
| `Build/Measure.cs` | checks/tools | 38 |
| `Build/Probe.cs` | checks/tools | 55 |
| `Build/Shots.cs` | checks/tools | 98 |
| `Checks/RunChecks.cs` | checks/tools | 934 |
| `Assets/IronCitadel/Runtime/LevelDoor.cs` | runtime | 32 |
| `Assets/IronCitadel/Runtime/LevelFeature.cs` | runtime | 14 |
| `Assets/IronCitadel/Runtime/LevelHud.cs` | runtime | 146 |
| `Assets/IronCitadel/Runtime/LevelMarker.cs` | runtime | 16 |
| `Assets/IronCitadel/Runtime/LevelRoom.cs` | runtime | 16 |

</details>

<details><summary>Opus v3, manor: 32 files, 4,403 non-blank lines</summary>

| File | Role | Lines |
|---|---|---|
| `Assets/Hollowmere/Editor/LevelBuilder.Core.cs` | builder | 309 |
| `Assets/Hollowmere/Editor/LevelBuilder.DressA.cs` | builder | 485 |
| `Assets/Hollowmere/Editor/LevelBuilder.DressB.cs` | builder | 465 |
| `Assets/Hollowmere/Editor/LevelBuilder.Helpers.cs` | builder | 98 |
| `Assets/Hollowmere/Editor/LevelBuilder.Layout.cs` | builder | 222 |
| `Assets/Hollowmere/Editor/LevelBuilder.Shell.cs` | builder | 340 |
| `Assets/Hollowmere/Editor/LevelBuilder.Systems.cs` | builder | 340 |
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.Static.cs` | checks/tools | 562 |
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.Walks.cs` | checks/tools | 309 |
| `Assets/Hollowmere/Checks/Runtime/CheckSuite.cs` | checks/tools | 170 |
| `Assets/Hollowmere/Checks/Runtime/LevelSurvey.cs` | checks/tools | 164 |
| `Assets/Hollowmere/Checks/Runtime/MiniJson.cs` | checks/tools | 63 |
| `Assets/Hollowmere/Editor/CheckRunner.cs` | checks/tools | 42 |
| `Assets/Hollowmere/Editor/ShotList.cs` | checks/tools | 72 |
| `Assets/Hollowmere/Editor/Shots.cs` | checks/tools | 90 |
| `Checks/run_checks.sh` | checks/tools | 19 |
| `Tools/Measure.cs` | checks/tools | 52 |
| `Tools/Thumbs.cs` | checks/tools | 59 |
| `Tools/ThumbsZ.cs` | checks/tools | 59 |
| `Tools/label_overview.py` | checks/tools | 62 |
| `Tools/montage.py` | checks/tools | 10 |
| `Tools/quad.py` | checks/tools | 9 |
| `Tools/sheet.py` | checks/tools | 13 |
| `Assets/Hollowmere/Scripts/CeilingPart.cs` | runtime | 6 |
| `Assets/Hollowmere/Scripts/ChairGoal.cs` | runtime | 32 |
| `Assets/Hollowmere/Scripts/DoorwayInfo.cs` | runtime | 22 |
| `Assets/Hollowmere/Scripts/EnemyMarker.cs` | runtime | 14 |
| `Assets/Hollowmere/Scripts/LevelHud.cs` | runtime | 104 |
| `Assets/Hollowmere/Scripts/OverviewMode.cs` | runtime | 67 |
| `Assets/Hollowmere/Scripts/PickupMarker.cs` | runtime | 11 |
| `Assets/Hollowmere/Scripts/RoomInfo.cs` | runtime | 70 |
| `Assets/Hollowmere/Scripts/SwingDoor.cs` | runtime | 63 |

</details>
