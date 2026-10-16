I built the level and every check and walk passes on the saved scene: 19 static checks and 7 play-mode walks, last run at 21:29 on scene SHA-1 49a5b6734d56. The editor is closed.

**(a) What I built.** `Assets/Scenes/IronCitadel.unity` has 16 rooms on a 5 m grid, built by `Tools/BuildLevel.cs` from Synty pieces. Each room has its own walls, floor and ceiling. Heights are 3 m for low rooms, 5 m standard, 10 m tall and 15 m for the throne room.
- The entry hall looks straight at the armory's doorway.
- The way round runs entry hall → storeroom → kitchen → Servery Stair → dining hall. The Servery Stair is my one extra room: an enclosed stairwell, so the climb to the gallery never crosses or overlooks the hall floor.
- A shut door under the gallery leads into the back of the armory.
- Act three runs hall → library → shrine → throne room. Five side rooms are dead ends; the vault sits behind a sliding bookcase in the library.

**(b) What must be true.** I believe all nine items are met, each checked by measuring the built scene.
- **Readings I chose:** I took "guards" to mean every armory defender. "Much of the floor" I set as more than half the whole hall floor and 60% of the floor not under the gallery; measured 52% and 64%. The controller has no crouch, so I tested a 1.0 m eye height for crouching.
- **One fix to read:** a player against the rail could see too little of the floor, so I added a 0.28 m firing step along the rail. It is easy to step onto.

**(c) Weaknesses.**
- Enemies and pickups are coloured capsule markers.
- Lighting is realtime only, with no baked light, and the walls are plain modular brick.
- The throne steps have an invisible ramp helper over them.
- My walks plan their routes on a navigation mesh I baked, between waypoints I chose.
- Room shots are rendered with editor cameras. Only the HUD and M overview were tested in play.
- The overview and open-vault captures each need three separate commands, because renderer changes only show up on a later editor tick.

**(d) Checking.** Each walk drives the shipped player in one continuous session, using only move and sprint input, turning, and the E-key `Interact()`. A stall fails the walk. The walks are: the way round, up to the gallery, under the gallery to the best gear, into the vault and back, on to the throne, plus two extras that open the remaining shut doors.
- **What the walks caught:** the firing step blocked the gallery doorway; the closed bookcase had been left out of the navigation mesh, so the route ran into it; and my throne target was behind the throne. I narrowed the step, made doors and the bookcase cut the mesh, and fixed the target.
- **What the static checks caught:** the guards could be seen from the hall through the wide doorway, so I narrowed it and moved them. Only 12% of the floor showed from the rail, so I turned the tables north–south, lowered and opened the rail, and added the step. Also fixed: a gap in a wall, a dip between the stage steps and the stage, too few bookcases matching the secret one, and three rooms too dark.
- **Re-run:** with the editor open, `sh Checks/run_checks.sh` (details in `Checks/README.md`).

**(e) Time:** about 1 h 23 m in total (20:08–21:31). Planning 18 min, building 27, checking and fixing 22, dressing and lighting 7, final rebuild, captures and runs 8.

**(f) What I'd change.**
- Turn the soft words into measurable criteria: "much of the floor", "low", and crouch height.
- Say whether placing the player at the start of each walk is allowed.
- List the known rendering pitfalls up front: renderer changes lag an editor tick, and the GPU-resident drawer keeps stale transforms.
- Ship a crouch control, or drop crouching from the brief.

Files are in the project folder:
- `Assets/Scenes/IronCitadel.unity`
- `Map/map.json`
- `Captures/` (23 images)
- `Checks/results.txt`
- `Checks/README.md`
