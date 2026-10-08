The level is built and saved as `Assets/Scenes/IronCitadel.unity`, and my automated check against every `must_be_true` item reports all passing (`Captures/verification.txt`).

**(a) What I built.** An editor script (`Assets/IronCitadel/Editor/CitadelBuilder.cs`) builds the scene from scratch out of Synty pieces. The 11 rooms sit on their 45 m tiles with 5 m of rock between rooms and in the `#` cells. Each door is a 5 m tunnel centred on its tile edge. Most rooms have a screen or antechamber, so you don't see straight through from the door. In play mode you can walk, sprint and climb the stairs. M shows a top-down overview with the ceilings hidden, room labels, red defender markers, gold pickups and a green player marker. The HUD shows the room (and part of it) and the elapsed time, and announces the throne. E swings the secret bookcase. Defenders are the data's 31 Synty characters, posed and static.

**(b) Requirements.** All nine pass. The check bakes a navigation mesh sized to the player and adds raycasts.
- **Doors:** the 12 door openings between rooms are the only walkable crossings of any tile edge.
- **Gate:** the portcullis and spikes stop everything; the armory is reached only through the great hall (213 m).
- **Wings:** with one wing barred, the other still reaches the hall without the armory: west 188 m, east 186 m.
- **Vault:** sealed until the bookcase swings.
- **Throne room:** one door, the throne hidden from it, a 1.25 m stage with steps across all 40 m.
- **Gallery:** 5 m deck with stair shafts reached from the side doors without crossing the hall floor.
- **Heights:** prison, guard room, kitchen and vault are 3.2–4 m; hall 12.5 m; throne room 15 m.

**(c) Weaknesses.**
- The kitchen (26×26 m) and vault (16×16 m) are deliberately smaller than their tiles, since the brief calls them small; rock fills the rest.
- To make the whole hall floor visible from the gallery rail, the arcade stands on a 2 m plinth. Floor under tables and benches is hidden.
- The outer door is shut behind the player.
- The guard room and prison are dark by design.
- Lighting is realtime only.

**(d) Checking.** I tested in play mode: walking, the overview, the bookcase via the E raycast, climbing the gallery stair and stage steps, and the throne announcement. That caught and fixed two bugs: a lip at the stair top that blocked the player, and a solid balustrade hiding the hall floor. I also reviewed several rounds of screenshots.

**(e) Time:** about 1 h 15 min.

Files are in `Captures/`:
- `overview.png`
- `01_entry_hall.png` … `11_forge.png`
- `extras/` (key features such as the gallery, gate and secret bookcase)
- `verification.txt`
