I built the whole level and the checks pass: the last run on the delivered scene scored 42 of 42, with every required walk passing and no stalls. The scene is saved and the editor is closed.

**(a) What I built.** `Assets/Scenes/IronCitadel.unity`, about 53 × 78 m, all on one floor level except the gallery and the stage.
- **Construction:** one rebuildable editor script (`LevelBuilder.Build()`) makes the walls, floors and ceilings from boxes textured with the pack's wallpaper, wood, brick and tile, then dresses each room with around 1,450 Synty props.
- **Act 1:** the player starts at the front door facing the gun room's lit entrance, a 2 m doorway through a 1.2 m wall. A timber screen of gun cabinets just inside hides every guard from the foyer.
- **Act 2, the way round, a low service and cellar wing:** watch room → cellar cells → butcher's room → kitchen → back stair → dining hall.
- **The back stair is my one extra room.** It is an enclosed servants' stair, which lets the way round reach both the hall floor and the gallery door. Under the gallery, wooden screens make a passage that leads unseen to the gun room's back door.
- **Act 3:** dining hall → library (a bookcase there swings open onto the strong room) → laboratory → ballroom. The ballroom is 12 m tall, with the chair on a stepped stage. The chapel and crypt, storeroom and boiler room are dead-end side rooms.
- **Systems:** a HUD shows the room name and elapsed time, M toggles a top-down overview with ceilings hidden, and reaching the chair is announced.

**(b) The "what must be true" list.** Every item is met as measured. Two readings I chose:
- **Gallery view:** 72% of the open hall floor is visible from the rail. If the floor under tables and chairs is counted, it is 51.5%.
- **Unseen climb:** the climb up the back stair is unseen. Once the gallery door is open, three floor guards can see a player standing in the doorway.

**(c) Weak points.** Inside the gun cabinets the weapons are hard to see. The pack has no rifles, so "guns" are revolvers plus axes and tools on racks. Defenders are lightly posed character models standing on coloured rings. The look is very warm overall, and I didn't use the Cinema LUTs.

**(d) How I checked.** The walks drive the player as shipped, using only its move and sprint input, turning, and the E-key script's `Interact()`. They run in one continuous play session: W1 to W5, transits, then a tour of every room, opening all eight shut doors. Failures I found and fixed:
- door frames and hinges skewed by a rotated door root;
- a gap between the stair ramp and the landing;
- props blocking two doorways;
- guards visible under the cabinet legs;
- dark rooms;
- the marker scripts failing to load because they shared one file.

To re-run, use `bash Checks/run_checks.sh`, or `Hollowmere.EditorTools.CheckRunner.RunAll()`.

**(e) Time:** about 1 h 40 min in total (05:42–07:22).
- Planning: 19 min.
- Building: 49 min, which included writing the check suite.
- Dressing and lighting: about 13 min on its own (it also overlapped with building).
- Final checking and fixing: 20 min.

**(f) What would help next time.**
- Warn up front about URP rendering layers and the GPU Resident Drawer's one-frame lag.
- Say which way the props face.
- Give a rough room-size budget.
- A scripted way to capture the Game view at a fixed resolution would also save time.

Files are in `C:\Repos\ColdRuns\opus-v3-hm`:
- `Assets/Scenes/IronCitadel.unity`
- `Map/map.json`
- `Captures/` (start with `overview_top_down_with_walks.png`)
- `Checks/results.txt`
- `Checks/README.md`
