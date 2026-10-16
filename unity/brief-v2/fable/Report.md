# Iron Citadel — build report

(a) What was built. Assets/Scenes/IronCitadel.unity: 15 required rooms plus a stair hall and an antechamber, laid out on
a 5 m grid of Synty POLYGON Dungeon wall, floor and ceiling modules (walls paired back to back where two rooms share
them, because the meshes are single-sided). Entry hall (start) → great doors into the armory, guarded by three ranged
markers that only come into view at the doorway. The way round: a side door → guard room → prison (barred cells both
sides) → torture chamber → crypt → stair hall → dining hall, entering beneath the gallery. The stair hall holds two
ramp-collider flights with a landing up to the gallery's own doorway at 5 m. The gallery deck runs the whole west side
of the 20 × 35 × 10 m dining hall behind an open balustrade; the armory's back door is in the hall's west wall under the
deck. A bricked-up doorway in the crypt (E sinks it) is the vault's only way in. From the hall: library → study →
shrine → antechamber → 15 m-tall throne room, with the throne on a 1 m stepped stage. Forge off the stair hall;
kitchen and storeroom off the hall. Lighting is realtime URP (torches, chandeliers, braziers, cold fills), ACES
tonemapping, bloom, vignette, grain; the Cinema LUTs tinted green and were dropped. HUD (room name, time, throne
announcement) and the M overview map are built in code. Markers are primitives (blue ranged, red melee, purple warlord,
gold pickups) with a white nose for facing.

(b) Must-be-true. All nine items are satisfied as measured by Checks/results.txt (13 static checks, 5 walks, 4
play-mode checks, all passed on the delivered scene): two armory ways in; way round avoiding the armory (walked);
gallery 5.0 m up, reached without touching the open floor (walked), 70–75 % of the floor in sight from the rail;
throne stage 1.0 m with ≤ 0.08 m surface changes per 0.25 m; vault with one hidden door; low rooms 2.9–3.05 m,
hall 9.9 m, throne room 14.9 m tallest; ceilings over 100 % of every floor; every wall gap matches a listed door;
65 tall and 83 low free-standing occluders.

(c) Weaknesses. The hidden door reads as a sealed doorway (a hint); dark rooms are genuinely dark; stacked wall tiers
show joints at 5 m; the throne is small; map labels overlap in places; the Steps block is the only stage style.

(d) Checking. Checks/README.md: `IronCitadelChecks.RunAll()` runs the static checks, exports Map/map.json, then
drives the shipped player through its own inputs along five walks in play mode. Failures found and fixed: stale
compiles (added an asset refresh), the unfocused editor pausing play (run in background), a third armory door (forge
moved), the upper gallery doorway blocked by the hall's second wall tier, misplaced half walls at the great door, guard
room debris on the lane, a cell fence post on a diagonal, E pressed before the camera turned.

(e) Time. Total 1 h 39 min (09:21–11:00): planning 18 min, building 26 min, dressing and lighting 22 min, checking
and fixing 33 min.

(f) Suggestions. State up front that the editor must run in background for CLI-driven play mode and that `unity
recompile` needs an asset refresh first; give the Synty module sizes and which meshes are single-sided; allow a
tiny amount of double-sided wall or a secret-door prefab so the hidden door needs no bricked-up plug.
