# Iron Citadel: the rooms level

`level.json` (a copy of `UnityAssets/docs/research/AI-DungeonLevelComparison/level.json`) built from
the themed-room prefabs under `Assets/CrawfisSoftware/AllSyntyDungeons/Rooms/`, one K9 room per room
cell, into `Scenes/IronCitadel_Rooms.unity`.

## Rebuild

- In the editor: **Iron Citadel > Rooms > Build scene**. It also writes the door check.
- Headless, with this project's editor closed:

  ```
  unity run <MacroTileBuilder> --timeout 1800 -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.Build -logFile <log>
  ```

  This builds and saves the scene, then writes `C:/Repos/IronCitadel/logs/rooms-build.txt`,
  `C:/Repos/IronCitadel/logs/rooms-doorcheck.txt` and the captures in `C:/Repos/IronCitadel/captures/rooms/`
  (`overview.png`, `overview-play.png`, `eye-<room>.png` for all eleven rooms, `feature-*.png`, the three
  gallery shots below, and `captures.txt` with each eye shot's viewpoint). It exits 0 when the door check
  passes and 2 when it does not.
- The gallery shots: `gallery-stair-west.png` and `gallery-stair-east.png` stand on each landing, 8 m out from
  the foot of the hall's stair, and look up it to its head. `gallery-rail.png` stands on the deck behind the
  west end of the rail and looks east along the gallery, with the hall below on the left. The great hall's
  eye shot skips any viewpoint with something within 5 m of the frame's centre or sides, so no door leaf
  fills it.
- **Iron Citadel > Rooms > Check doors** and **Capture overview** rerun those two steps on the open scene.

The assembler owns the scene root named `IronCitadel` and nothing else. A rebuild opens the saved
scene, replaces that root and saves, so objects at other roots survive. The walker, the HUD and the
overview are under that root (`IronCitadel/Play`), so every rebuild puts them back. Every
build also resets the scene's lighting settings: no skybox, no sun, and a flat night ambient. This
project's URP asset ignores the ambient colour, so the fill lights do that job.

## Play mode

Open `Scenes/IronCitadel_Rooms.unity` and press Play.

| Key | Action |
|---|---|
| W A S D, mouse | walk and look (Unity Starter Assets first-person controller) |
| Shift | sprint |
| Space | jump |
| E | use: opens the vault's bookcase door from the prison side (`Walker/InteractOnE`) |
| M | top-down overview of the whole level, ceilings hidden, the player as a red dot with a yellow heading tick; M again to walk |

The HUD (top left) names the room under the player and shows the elapsed time. The room comes from a
cell lookup in `level.json` (cell [r,c] spans x 45c..45(c+1), z -45(r+1)..-45r). When the player stands
within 3 m of the throne, up on the stage, a banner announces it and the timer stops.

| Object | What it is |
|---|---|
| `IronCitadel/Play/IronCitadelPlayer` | `Walker/IronCitadelPlayer.prefab`: Starter Assets' `NestedParent_Unpack` (PlayerCapsule, MainCamera, PlayerFollowCamera) with `InteractOnE` on the PlayerCapsule, sized to a person (CharacterController r 0.35, h 1.8, step 0.3; eye at 1.6 m; see `Walker/README.md`), placed at `PlayerStart`, facing north |
| `IronCitadel/Play/HUD` | `IronCitadelHud` (room, time, throne) and `IronCitadelOverview` (M) |
| `IronCitadel/Play/OverviewCamera` | the orthographic camera M switches to; disabled until then |
| `IronCitadel/Play/OverviewLight` | a soft overhead directional light that is on only in the overview, so the map reads at night |

The play scripts are in `Play/` (asmdef `IronCitadel.Play`). The walker add-on is in `Walker/`, copied
from `C:/Repos/IronCitadel/kit/walker/Interact/`. Its setup writes the prefab to `Walker/` here, not
`Assets/Interact/`. Starter Assets itself is imported at `Assets/Starter Assets/` and adds
`com.unity.cinemachine` to the manifest. The door check and the captures switch `Play` off while they run.

## Conformance

`Conformance/` is a copy of `C:/Repos/IronCitadel/kit/conformance/` without its `Fixtures` folder. It is the
same validator the cold runs are scored with. Run it headless, with both Builder instances free:

```
unity run <MacroTileBuilder> --timeout 1500 -- -executeMethod IronCitadel.Conformance.ConformanceCli.Run
    -scene Assets/IronCitadel/Scenes/IronCitadel_Rooms.unity
    -level C:/Repos/Github/Crawfis-Software/UnityAssets/docs/research/AI-DungeonLevelComparison/level.json
    -out C:/Repos/IronCitadel/scores/rooms -logFile C:/Repos/IronCitadel/logs/rooms-score.log
```

The scores are in `C:/Repos/IronCitadel/scores/rooms/` (`conformance.md`, `.json`, `-map.png`).

One run can rebuild, check, capture and score:

```
unity run <MacroTileBuilder> --timeout 2700 -- -executeMethod IronCitadel.Rooms.Editor.IronCitadelRoomsAssembler.BuildAndScore
    [-makePlayer] [-trialPicks guard_room,library [-trialAll]] -logFile C:/Repos/IronCitadel/logs/rooms-build-score.log
```

`-makePlayer` first remakes `Walker/IronCitadelPlayer.prefab`. `-trialPicks` first runs the picks trial (below);
`-trialAll` widens it to every K9 door set of each room's theme. Then it builds and runs the door check. It
writes the captures and the four probes:

- `rooms-probe-lines.txt`.
- `rooms-vault-probe.txt`: each vault doorway, scanned with the walker's capsule, and the NavMesh path from
  the prison to the treasury.
- `rooms-stage-probe.txt`: what stands on and before the throne steps.
- `rooms-hall-probe.txt`: the NavMesh path from 1 m outside the great hall's west and east doorways to the
  gallery deck, every 0.5 m with its distance in from the hall cell's west and south edges (u, v) and its height.
  Points inside conformance check 7's central floor (u 7..38, v 7..42, under 1 m up) are starred. It also lists
  the colliders around the first starred point and draws the hall cell as an ASCII map.

Last, it scores the saved scene against the canonical level.json into `scores/rooms/` (`-level` and `-out`
override those). `IronCitadelRoomsAssembler.Probes` reruns only the four probes. It exits 0 when the door
check passes, 2 when it does not and 1 on an exception.

## The door check

`rooms-doorcheck.txt` scans every tile edge of every room every 0.25 m. At each point it tests the
floor 0.6 m in on each side and the line across the edge at 0.5, 1.2 and 1.8 m. It runs three
passes:

1. **Features hidden.** Every listed door must join its two tiles over at least 0.9 m, and the outer
   door must open on the entry side. An unlisted edge must have no passage and no doorway that opens
   onto a wall or the void.
2. **Features shown.** The portcullis, the closed bookcase and the shut outer door must each block
   their doorway.
3. **Secret door swung open.** With its colliders still on, the bookcase must clear the doorway.

The build also gives a collider to every floor or wall piece that ships without one, in the scene
only, and lists them in `rooms-build.txt`. The `SM_Env_Tiles_Texture_10` floor tile and the
`SM_Env_Door_Frame_01` cell door frames have none in the room prefabs.

The vault's treasury prefab ships three of its five metal doors (`SM_Prop_Door_Metal_02`) shut across
their `SM_Env_Door_Frame_01`, and two (`SM_Prop_Door_Metal_01`) already open. The build leaves an open leaf
as shipped. It swings each shut leaf open 90 degrees about a hinge at one end of the leaf. It picks the
end and the side whose swept leaf hits nothing but its own frame and stays in the vault cell, then the
pose that leaves the walker's capsule the widest straight lane through the frame. `rooms-build.txt` lists
each door's pose and lane ("vault door ...") and a total ("vault doors: ...").

The throne room prefab (ThroneRoomAisledCourt) stands a stone lantern (`SM_Env_Stone_Lantern_01`, 1.9 m)
0.25 m off the foot of the stage's steps, before their west half. It cut those steps off for the walker, and
conformance check 8 found 75% of the front stepped (want 80%). The build moves every Furniture or Clutter
prop taller than 0.3 m that stands within 2 m of the foot of the steps straight back toward the door, to
2.75 m off the foot. It then moves it on in 0.25 m steps while it would overlap another collider. Props
within 1.1 m of either end of the steps stay, so the knight stands and the nave's side rows that flank the
steps keep their places. Only the lantern moves; the brazier (2.69 m off) and the candle stand (4.1 m off)
are already clear. `rooms-build.txt` lists each move ("stage front ..."). Check 8 now finds 86%.

level.json calls the guard room, prison, vault and kitchen low, but no catalog kitchen is both low and
walkable between two adjacent doors. The Notch kitchen's hearth hall has a 10 m lid, and only 40% of its
floor was under 6.5 m (check 9 wants 75%). In every room level.json calls low, the build drops each ceiling
piece more than 0.5 m above 5 m by whole 5 m bands. It switches off the wall pieces left standing wholly
above the new lid; in the kitchen that is 26 lid pieces dropped 5 m and 29 upper-band walls off, including
the top half of the beam screen. The guard room, prison and vault are already at 5 m and are left as shipped.
`rooms-build.txt` says what changed ("low lid ..."). The kitchen is now 100% under 6.5 m.
Since PrefabSynthesis PR #208 (2026-10-07) the Notch kitchens ship low, one 5 m course under the ring's own
lid (ceiling `low_course`). So this step now logs "kitchen already at 5 m or under; left as shipped" and
changes nothing. It stays as a guard for any later pick.

The hall prefab (HallGalleryBackFeast) meets its two stair shafts unevenly. On the east, the landing's edge
to the shaft is an `SM_Env_Wall_07` stub with the gap by the outer wall. On the west it is a pair of round
door frames with the gap in the middle, 6.7 to 8.3 m in from the west wall. So the path from the west door
to the gallery ran more than 7 m in at floor level, and check 7 counted it as crossing the hall floor,
though it never left the side hall. The build mirrors the east stub onto the west edge (x to -x in the
hall's frame) and switches the frames off ("hall landing ..."). PrefabSynthesis PR #207 (2026-10-07, stacked
on #206) fixes this at the source: `fam_gallery_b` casts both landing mouths as `Wall_07` arches, the west one
turned 180 degrees so the pair mirror. The rebuilt hall has no frame pair, so this step finds nothing and
logs nothing.

## What is in the scene

| Object | What it is |
|---|---|
| `IronCitadel/Lighting` | the night fill: three dim (0.13), cool directional lights without shadows, 120 degrees apart, so unlit corners stay readable under the rooms' own torches. The URP asset samples probe volumes, so a flat ambient colour does nothing in this unbaked scene. |
| `IronCitadel/Rooms/<room id>` | one room prefab per room, at its cell centre, rotated per the picks; `#` cells are empty |
| `IronCitadel/Features/armory_gate` | a lowered `SM_Env_Portcullis_01` fitted to the arch, a `GateBlocker` box, and a row of three goblin spikes 1.8 m out on the entry side |
| `IronCitadel/Features/vault_door` | `SecretDoor` (`SecretBookcaseDoor`) holding the leaf bookcase and its blocker on a hinge, and `Decoys`, matching bookcases along the same wall |
| `IronCitadel/Features/outer_door` | a shut door and blocker in the entry's south doorway; level.json has no outside to walk into |
| `IronCitadel/Markers/Defenders` | 31 static Synty characters, each with an `IronCitadelMarker` and a capsule collider. The Animator is off and the arms are lowered out of the T-pose. Goblins are the garrison and the WarChief is the warlord, at twice a goblin's size on the stage (`WarlordScale`; his collider and clearance scale with him); Nomads are the prisoners and dwarves the smiths. |
| `IronCitadel/Markers/Pickups` | a chest, with coins in the vault, and a small glow light |
| `IronCitadel/Markers/PlayerStart` | inside outer_door, facing north (tag `Respawn`) |
| `IronCitadel/Markers/Goal` | at the throne, with a 2 m trigger (tag `Finish`) |

`SecretBookcaseDoor.Interact()` swings the leaf 90 degrees about its hinge over 1 s, then switches
its colliders off. A walker reaches it by raycasting and calling
`hit.collider.SendMessageUpwards("Interact", SendMessageOptions.DontRequireReceiver)`. The decoys sit
under a sibling, so using one does nothing.

`IronCitadelMarker` (kind, room, note) tags every defender, pickup, feature, start and goal, so a
scorer can find them without parsing names.

## Picks (`level.picks.json`)

Rotation is the yaw in degrees. +90 turns a prefab's canonical N door to the east.

| Room | Cell | Needs | Prefab (under `Rooms/`) | Canonical | Rot | Lid |
|---|---|---|---|---|---|---|
| entry_hall | 3,2 | NESW | GuardRoom/GuardRoomLobbyMannedK9NESW | NESW | 0 | 5 m (asked standard) |
| guard_room | 3,1 | NE | GuardRoom/GuardRoomWatchMannedK9NE | NE | 0 | low (all of it under 6.5 m) |
| prison | 2,1 | NSW | Prison/PrisonBlockRiotK9NES | NES | 180 | 5 m low |
| vault | 2,0 | E | Treasury/TreasuryLobbySealedK9N | N | 90 | 5 m low |
| kitchen | 1,1 | SE | Kitchen/KitchenNotchCookingK9NE | NE | 90 | 5 m low (PR #208; was 10 m over the hearth) |
| great_hall | 1,2 | NESW | HallGallery/HallGalleryBackFeastK9NESW | NESW | 0 | 15 m tall, gallery on the south |
| armory | 2,2 | NS | Armory/ArmoryRacksStockedK9NS | NS | 0 | 10 m |
| throne_room | 0,2 | S | ThroneRoom/ThroneRoomAisledCourtK9N | N | 180 | 15 m tall |
| library | 3,3 | NW | Library/LibraryBoxOpenK9NE | NE | 270 | 15 m (asked standard) |
| study | 2,3 | NS | AlchemyLab/AlchemyLabWarrenBrewingK9NS | NS | 0 | 10 m |
| forge | 1,3 | SW | Forge/ForgeCellarWorkingK9NE | NE | 180 | 15 m flue, 3.6-5 m halls |

Each prefab file name ends in `_01352840`. The `why` field in `level.picks.json` gives the reason for
each pick.

These picks depart from level.json:

- (Fixed at the source by PR #208; kept for the record.) The kitchen's main room had a 10 m lid, but
  level.json's `must_be_true` asks for a low kitchen. No
  K9 cooking kitchen with two adjacent doors is low enough: KitchenSmallCooking has 61% of its floor under
  6.5 m and KitchenNotchCooking 40% (check 9 wants 75%). No K9 kitchen of any state or door set is low
  enough either (`-trialAll`, 2026-10-07): the lowest are LarderCookingK9NESW at 69%, SmallColdK9NESW 68%,
  SmallCookingK9NES 65% and SuiteCookingK9NESW 64%. Of the two-door kitchens the walker can cross, only
  HearthCold (43%, the wrong state) and NotchCooking (39%) qualify.
- (Fixed at the source by PR #207; kept for the record.) Check 7 failed on the great hall prefab
  (HallGalleryBackFeastK9NESW), not on a pick or a pose. The west
  landing's door into the side hall is a `SM_Env_Wall_DoorFrame_Round_01` pair 1.6 m wide. Its collider gap
  leaves the walker's centre a lane 7.0 to 7.9 m in from the hall's west edge. Check 7 counts anything more
  than 7 m in as hall floor, so every path from the west doorway "crosses the hall floor" there, though it
  never leaves the walled side hall (`logs/rooms-hall-probe.txt`). The east landing's matching edge is a
  2.5 m gap by the wall, and its path passes. BackFeast is the only Back + Feast K9NESW hall.
- Three first picks were cut for a person-sized walker (r 0.35, h 1.8, step 0.3): no NavMesh path joined
  their two doors. GuardRoomPostNight became GuardRoomWatchManned, because no other 'night' guard room has
  the door set, and 'manned' is the nearest state. LibraryBaysOpen became LibraryBoxOpen, in the same
  state. KitchenSmallCooking became KitchenNotchCooking, in the same state. `TryRoomPicks` writes the
  trial (`logs/rooms-picks-trial.txt`).
- The entry hall has a 5 m lid and the library 15 m, where level.json asks for standard.
- The outer door is shut, because level.json models no outside.

## Other files

- `Editor/IronCitadelInspect.cs`: **Iron Citadel > Rooms > Inspect candidate prefabs** dumps the
  candidate rooms (groups, ceilings, doorways) and the Synty props used here, to
  `C:/Repos/IronCitadel/logs/rooms-inspect.txt`.
- `Editor/IronCitadelCapture.cs`: top-down and eye-height captures, with ceilings hidden for map views.
- `IronCitadelRoomsAssembler.ProbeDoorways` (no menu item; `-executeMethod` only) prints each door's
  raycast profile to `C:/Repos/IronCitadel/logs/rooms-probe.txt`, to diagnose a failed door check.
- `IronCitadelRoomsAssembler.ProbeLines` (`-executeMethod` only) walks fixed lines through the hall
  stairs, the guard room, the library and the vault every 0.1 m. It prints the floor, the headroom and
  whatever the walker's capsule (r 0.35, h 1.8 plus 0.02 skin) touches, to `logs/rooms-probe-lines.txt`.
- `IronCitadelRoomsAssembler.TryRoomPicks` (`-executeMethod`, `-rooms guard_room,library`) places every
  K9 prefab of a room's theme that has the pick's door set alone on that room's cell, at the pick's rotation.
  It bakes each one at the walker's size and reports whether a NavMesh path joins the room's two doors,
  and how much of its floor lies under a low (6.5 m or less) and a tall (8 m or more) ceiling, to
  `logs/rooms-picks-trial.txt`. With `-trialAll` (through `BuildAndScore -trialPicks`) it also places the
  theme's other K9 door sets and reports only their heights.
