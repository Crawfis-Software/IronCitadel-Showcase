// IronCitadel conformance kit v2: the rows. Each returns pass (true), fail (false) or manual (null) and fills
// row.Measured, row.Short and row.Reason.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace IronCitadel.ConformanceV2
{
    public sealed partial class Scorer
    {
        List<Vector3> entrySamples, deckPoints, climbPath;
        Gap flankAny;
        /// <summary>Row 8's doors for the map: 0 a listed door that is an opening, 1 a listed door that is not, 2 an
        /// opening between rooms that no listed door matches.</summary>
        public readonly List<(Vector3 p, int kind)> DoorDots = new List<(Vector3, int)>();
        public List<Vector3> ClimbPath => climbPath;
        public List<Vector3> WayRound => wayRound;
        float deckHeight = float.NaN;

        static float Median(IEnumerable<float> xs)
        {
            var l = xs.OrderBy(x => x).ToList();
            return l.Count == 0 ? float.NaN : l[l.Count / 2];
        }

        static bool Fail(CheckRow row, string why)
        {
            row.Short = why; row.Reason = why;
            return false;
        }

        // ---------------------------------------------------------------------------------------------
        // 1. the armory: exactly two openings (every door opened); one to the entry hall, seen from it; one to the
        //    dining hall beneath the gallery

        bool? Row1_Armory(CheckRow row)
        {
            if (armory == null) return Fail(row, "the map lists no armory");
            var reasons = new List<string>();
            var gaps = M2.GapsOf(armory.Index);
            row.Measured.Add("openings_all_doors_opened", gaps.Select(g => (object)new JObj().Add("to", M2.NodeName(g.Other(armory.Index)))
                .Add("via", g.Via).Add("centre", g.Centre).Add("navmesh_width_m", g.Width).Add("door", MatchDoor(M2, g)?.Label)).ToList());
            if (gaps.Count != 2) reasons.Add("the armory has " + gaps.Count + " openings with every door opened (want 2)");

            var entrySide = new HashSet<int>();
            if (entry != null)
            {
                entrySide.Add(entry.Index);
                foreach (var r in Map.Rooms.Where(r => r.Kind == "extra"))
                    if (M2.Gaps.Any(g => g.Touches(r.Index) && g.Touches(entry.Index))) entrySide.Add(r.Index);
            }
            choke = gaps.Where(g => entrySide.Contains(g.Other(armory.Index)))
                .OrderBy(g => entry != null && g.Other(armory.Index) == entry.Index ? 0 : 1).FirstOrDefault();
            if (choke == null) reasons.Add(entry == null ? "the map lists no entry hall" : "no opening joins the armory to the entry hall's side");

            if (hall == null) reasons.Add("the map lists no dining hall");
            else
            {
                var hallSide = gaps.Where(g => { int o = g.Other(armory.Index); return o == hall.Index || o == Map.GalleryNode; }).ToList();
                var fj = new List<object>();
                foreach (var g in hallSide)
                {
                    float dd = Map.HasGallery ? Map.DistToDeck(g.Centre.x, g.Centre.z) : float.NaN;
                    bool below = Map.HasGallery && g.Centre.y < Map.DeckY;
                    bool under = Map.HasGallery && dd <= Tune.UnderGalleryReach && below;
                    var gj = new JObj().Add("centre", g.Centre).Add("from_deck_outline_m", dd).Add("below_deck_y", below).Add("crossing_beneath_gallery", under);
                    // the crossing may sit at the end of a short passage: then where its route enters the hall counts
                    if (!under && Map.HasGallery && hallPoint != null)
                    {
                        var corners = PathCorners(g.Centre, hallPoint.Value, M2.Filter, out var st);
                        var dense = Densify(corners, Tune.PathSampleStep);
                        float along = 0f;
                        for (int k = 0; k < dense.Count; k++)
                        {
                            if (k > 0) along += Vector3.Distance(dense[k - 1], dense[k]);
                            if (along > Tune.FlankRouteReach) break;
                            var p = dense[k];
                            int n = M2.NodeOf(p);
                            if ((n != hall.Index && n != Map.GalleryNode) || p.y >= Map.DeckY - 1f || Map.DistToDeck(p.x, p.z) > Tune.UnderGalleryReach) continue;
                            under = true;
                            gj.Add("route_enters_beneath_gallery_at", p).Add("route_m_from_crossing", along);
                            break;
                        }
                        if (!under) gj.Add("route_status", st.ToString()).Add("route_checked_m", Mathf.Min(PathLength(corners), Tune.FlankRouteReach));
                    }
                    gj.Add("beneath_gallery", under);
                    fj.Add(gj);
                    if (under && flank == null) flank = g;
                    if (flankAny == null) flankAny = g;
                }
                row.Measured.Add("dining_hall_side", fj);
                if (hallSide.Count == 0) reasons.Add("no opening joins the armory to the dining hall");
                else if (flank == null)
                    reasons.Add(Map.HasGallery ? "the armory's opening to the dining hall is not beneath the gallery (want the crossing, or its route within " + Tune.FlankRouteReach +
                                                 " m of it, under the deck outline or within " + Tune.UnderGalleryReach + " m of it, below deck_y)" : "the map has no gallery");
            }

            if (choke != null && entry != null)
            {
                var cd = MatchDoor(M2, choke);
                var basePos = cd != null ? cd.Position : choke.Centre;
                chokeTarget = basePos + Vector3.up * Tune.DoorTargetLift;
                entrySamples = FloorSamples(M2, entry, Tune.FloorSampleStep);
                int seen = entrySamples.Count(s => Clear(s + Vector3.up * Tune.EyeHeight, chokeTarget.Value));
                row.Measured.Add("choke", new JObj().Add("door", cd?.Label).Add("doorway", basePos).Add("entry_hall_samples", entrySamples.Count)
                    .Add("samples_that_see_the_doorway", seen).Add("eye_m", Tune.EyeHeight).Add("target_above_floor_m", Tune.DoorTargetLift));
                if (seen == 0) reasons.Add("no entry-hall floor sample sees the armory's doorway (eye " + Tune.EyeHeight + " m)");
            }

            bool ok = reasons.Count == 0;
            row.Short = ok ? "2 openings: the entry hall (seen from it) and the dining hall beneath the gallery" : string.Join("; ", reasons);
            row.Reason = ok
                ? "with every door opened the armory's only openings are to " + M2.NodeName(choke.Other(armory.Index)) + " at " + Fmt(choke.Centre) +
                  " and to the dining hall at " + Fmt(flank.Centre) + ", " + N1(Map.DistToDeck(flank.Centre.x, flank.Centre.z)) + " m from the deck outline and below deck_y" +
                  (Map.DistToDeck(flank.Centre.x, flank.Centre.z) > Tune.UnderGalleryReach ? ", whose route enters the hall beneath the gallery within " + Tune.FlankRouteReach + " m" : "")
                : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 1b. act one: no entry-hall sample further than 5 m from the doorway sees an armory enemy; the start sees the
        //     doorway and no armory enemy

        bool? Row1b_Choke(CheckRow row)
        {
            if (armory == null || entry == null || choke == null || chokeTarget == null) return Fail(row, "row 1 found no armory doorway on the entry hall's side");
            var guards = Map.Enemies.Where(e => RoomNamed(e.Room) == armory).ToList();
            if (guards.Count == 0) return Fail(row, "the map lists no enemy in the armory");
            var door = chokeTarget.Value - Vector3.up * Tune.DoorTargetLift;
            var samples = entrySamples ?? FloorSamples(M2, entry, Tune.FloorSampleStep);
            int far = 0, seeing = 0;
            float farthestSeeing = 0f;
            var viol = new List<object>();
            foreach (var s in samples)
            {
                var eye = s + Vector3.up * Tune.EyeHeight;
                float dist = HDist(s, door);
                bool isFar = dist > Tune.ChokeSightBack;
                if (isFar) far++;
                foreach (var g in guards)
                {
                    if (!Clear(eye, g.Position + Vector3.up * Tune.ChestHeight)) continue;
                    seeing++;
                    farthestSeeing = Mathf.Max(farthestSeeing, dist);
                    if (isFar)
                    {
                        ChokeViolations.Add(s);
                        if (viol.Count < 20) viol.Add(new JObj().Add("sample", s).Add("from_doorway_m", dist).Add("sees", g.Label));
                    }
                    break;
                }
            }
            var startEye = StartPosition + Vector3.up * Tune.EyeHeight;
            bool startDoor = Clear(startEye, chokeTarget.Value);
            var startSees = guards.Where(g => Clear(startEye, g.Position + Vector3.up * Tune.ChestHeight)).Select(g => (object)g.Label).ToList();
            row.Measured.Add("armory_enemies", guards.Count).Add("entry_hall_samples", samples.Count)
                .Add("samples_beyond_m", Tune.ChokeSightBack).Add("samples_beyond", far)
                .Add("samples_seeing_an_enemy", seeing).Add("farthest_sample_seeing_an_enemy_m", seeing > 0 ? (object)farthestSeeing : null)
                .Add("violations_first_20", viol)
                .Add("start", StartPosition).Add("start_from_doorway_m", HDist(StartPosition, door))
                .Add("start_sees_doorway", startDoor).Add("start_sees_enemies", startSees)
                .Add("eye_m", Tune.EyeHeight).Add("chest_m", Tune.ChestHeight);
            var reasons = new List<string>();
            if (ChokeViolations.Count > 0) reasons.Add(ChokeViolations.Count + " entry-hall samples more than " + Tune.ChokeSightBack + " m from the doorway see an armory enemy (farthest " + N1(farthestSeeing) + " m)");
            if (!startDoor) reasons.Add("the start does not see the armory's doorway");
            if (startSees.Count > 0) reasons.Add("the start sees " + startSees.Count + " armory enemies");
            bool ok = reasons.Count == 0;
            row.Short = ok ? "guards seen only within " + N1(seeing > 0 ? farthestSeeing : 0f) + " m of the doorway; the start sees the doorway, no guard" : string.Join("; ", reasons);
            row.Reason = ok
                ? "of " + samples.Count + " entry-hall floor samples, " + seeing + " see an armory enemy (eye " + Tune.EyeHeight + " m to chest " + Tune.ChestHeight +
                  " m), all within " + Tune.ChokeSightBack + " m of the doorway; the start sees the doorway and none of the " + guards.Count + " armory enemies"
                : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 2. the way round: a path from the start to the dining hall with the armory marked off

        bool? Row2_WayRound(CheckRow row)
        {
            if (entry == null || hall == null || armory == null) return Fail(row, "the map lacks the entry hall, the dining hall or the armory");
            if (hallPoint == null) return Fail(row, "no floor point in the dining hall");
            var f = NavBake.FilterWithout(M2, Tune.ArmoryArea);
            var corners = PathCorners(StartPosition, hallPoint.Value, f, out var st);
            var dense = Densify(corners, Tune.PathSampleStep);
            bool through = dense.Any(p => M2.NodeOf(p) == armory.Index);
            float marked = M2.AreaOfNavArea(Tune.ArmoryArea, -1), armoryNav = M2.AreaOfNode(armory.Index, true);
            row.Measured.Add("from", StartPosition).Add("to", hallPoint.Value).Add("status", st.ToString()).Add("length_m", PathLength(corners))
                .Add("rooms", RoomsVisited(M2, dense)).Add("armory_marked_m2", marked).Add("armory_navmesh_m2", armoryNav)
                .Add("method", "the armory's outline within its floor band marked as NavMesh area " + Tune.ArmoryArea + " by " + armoryBoxes + " modifier boxes, and that area left out of the query");
            if (armoryNav > 0 && marked < 0.5f * armoryNav) Notes.Add("row 2: only " + N1(marked) + " of the armory's " + N1(armoryNav) + " m2 of NavMesh was marked off");
            bool ok = st == NavMeshPathStatus.PathComplete && !through;
            if (ok) wayRound = dense;
            row.Short = ok ? "the way round: " + string.Join(" > ", RoomsVisited(M2, dense)) + " (" + N1(PathLength(corners)) + " m)"
                           : st != NavMeshPathStatus.PathComplete ? "no path to the dining hall without the armory (" + st + ")" : "the path still crosses the armory";
            row.Reason = ok ? "with the armory marked off, a complete NavMesh path runs from the start to the dining hall's floor through " + string.Join(", ", RoomsVisited(M2, dense))
                            : row.Short;
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 3. the gallery: high, on NavMesh, reached from the way-in without the hall floor, the rail sees the floor

        List<Vector3> DeckPoints()
        {
            var l = new List<Vector3>();
            if (!Map.HasGallery) return l;
            foreach (var d in Map.Deck)
                for (float x = d.XMin + 0.5f; x < d.XMax; x += 1f)
                for (float z = d.ZMin + 0.5f; z < d.ZMax; z += 1f)
                {
                    if (!d.Contains(x, z)) continue;
                    var probe = new Vector3(x, Map.DeckY + 0.5f, z);
                    if (!NavMesh.SamplePosition(probe, out var hit, 1.5f, M2.Filter)) continue;
                    var p = hit.position;
                    if (HDist(p, probe) > 0.5f || M2.NodeOf(p) != Map.GalleryNode) continue;
                    int t = M2.FindTri(p, 0.6f);
                    if (t < 0 || !M2.IsLevel(t)) continue;
                    l.Add(p);
                }
            return l;
        }

        bool? Row3_Gallery(CheckRow row)
        {
            if (hall == null) return Fail(row, "the map lists no dining hall");
            if (!Map.HasGallery) return Fail(row, "the map has no gallery deck");
            var reasons = new List<string>();
            float floor = FloorOf(M2, hall);
            deckPoints = DeckPoints();
            deckHeight = deckPoints.Count > 0 ? Median(deckPoints.Select(p => p.y)) : float.NaN;
            float rise = (float.IsNaN(deckHeight) ? Map.DeckY : deckHeight) - floor;
            float mapRise = Map.DeckY - hall.FloorY;
            float area = M2.AreaOfNode(Map.GalleryNode, true);
            row.Measured.Add("a_height", new JObj().Add("hall_floor_y", floor).Add("deck_y_map", Map.DeckY).Add("deck_y_measured", deckHeight)
                .Add("rise_m", rise).Add("map_rise_m", mapRise).Add("min_rise_m", Tune.GalleryMinRise)
                .Add("deck_navmesh_m2", area).Add("min_deck_m2", Tune.GalleryMinArea).Add("deck_samples", deckPoints.Count));
            if (rise < Tune.GalleryMinRise - Tune.GalleryRiseSlack) reasons.Add("the deck is " + N1(rise) + " m above the hall floor (want " + Tune.GalleryMinRise + ")");
            if (mapRise < Tune.GalleryMinRise - Tune.GalleryRiseSlack) reasons.Add("the map's deck_y is " + N1(mapRise) + " m above the hall's floor_y");
            if (area < Tune.GalleryMinArea) reasons.Add("the deck has " + N1(area) + " m2 of level NavMesh (want " + Tune.GalleryMinArea + ")");

            // (b) from the way in, with the hall floor marked off
            var jb = new JObj();
            bool reach = false;
            if (wayRound == null) reasons.Add("no way round (row 2), so no way in to start from");
            else
            {
                int k = wayRound.FindIndex(p => { int n = M2.NodeOf(p); return n == hall.Index || n == Map.GalleryNode; });
                if (k < 0) reasons.Add("the way round never reaches the hall");
                else
                {
                    var entryP = wayRound[k];
                    var fNo = NavBake.FilterWithout(M2, Tune.HallFloorArea);
                    wayInPoint = Snap(M2, entryP, 1.5f, fNo) ?? entryP;
                    var wg = M2.GapsOf(hall.Index).Concat(M2.GapsOf(Map.GalleryNode)).OrderBy(g => g.DistXZ(entryP)).FirstOrDefault();
                    jb.Add("way_in", wayInPoint.Value).Add("way_in_from", wg != null ? M2.NodeName(wg.Other(M2.NodeOf(entryP) == Map.GalleryNode ? Map.GalleryNode : hall.Index)) : null)
                      .Add("way_in_door", wg != null ? MatchDoor(M2, wg)?.Label : null)
                      .Add("hall_floor", "the hall's NavMesh more than " + Tune.HallFloorInset + " m inside its outline and more than " + Tune.HallFloorDeckClear +
                                         " m from the deck outline, floor_y - 1 to + " + Tune.HallFloorBandHigh + " m: area " + Tune.HallFloorArea + ", " + hallFloorBoxes + " boxes, left out of the query")
                      .Add("hall_floor_m2", M2.AreaOfNavArea(Tune.HallFloorArea, -1));
                    if (M2.NodeOf(entryP) == Map.GalleryNode) { reach = true; deckPoint = wayInPoint; jb.Add("status", "the way round arrives on the deck"); }
                    else if (deckPoints.Count == 0) reasons.Add("no deck NavMesh to reach");
                    else
                    {
                        var c = deckPoints.OrderBy(p => Vector3.Distance(p, wayInPoint.Value)).ToList();
                        var tries = new List<Vector3> { c[0], c[c.Count / 2], c[c.Count - 1] };
                        var tj = new List<object>();
                        foreach (var t in tries)
                        {
                            var corners = PathCorners(wayInPoint.Value, t, fNo, out var st);
                            tj.Add(new JObj().Add("to", t).Add("status", st.ToString()).Add("length_m", PathLength(corners)));
                            if (st != NavMeshPathStatus.PathComplete) continue;
                            reach = true; deckPoint = t; climbPath = Densify(corners, Tune.PathSampleStep);
                            var onBand = climbPath.Count(p => M2.NodeOf(p) == hall.Index && Mathf.Abs(p.y - floor) <= Tune.FloorBandHigh);
                            jb.Add("climb_rooms", RoomsVisited(M2, climbPath)).Add("climb_m_at_hall_floor_level_near_walls", onBand * Tune.PathSampleStep);
                            break;
                        }
                        jb.Add("tries", tj);
                        if (!reach)
                        {
                            PathCorners(wayInPoint.Value, c[0], M2.Filter, out var stAll);
                            jb.Add("with_hall_floor_allowed", stAll.ToString());
                            reasons.Add("from where the way round comes in, the deck is not reached without the hall floor" +
                                        (stAll == NavMeshPathStatus.PathComplete ? " (it is, across the floor)" : " (nor across it: " + stAll + ")"));
                        }
                    }
                }
            }
            row.Measured.Add("b_reach", jb);

            // (c) the rail sight, v1's method: eyes on the deck's NavMesh edge under the rail, leaned out RailLean
            DeckEyes.Clear(); HallFloorSamples.Clear(); HallFloorHidden.Clear();
            float deckY = float.IsNaN(deckHeight) ? Map.DeckY : deckHeight;
            for (int i = 0; i + 1 < Map.Rail.Count; i++)
            {
                var a = Map.Rail[i]; var b = Map.Rail[i + 1];
                var d = b - a; float len = d.magnitude;
                if (len < 1e-3f) continue;
                d /= len;
                var n1 = new Vector2(-d.y, d.x);
                var mid = (a + b) * 0.5f;
                bool n1Out = !Map.InDeck(mid.x + n1.x, mid.y + n1.y), n2Out = !Map.InDeck(mid.x - n1.x, mid.y - n1.y);
                var hc = hall.Centroid;
                var n = n1Out && !n2Out ? n1 : n2Out && !n1Out ? -n1 : (Vector2.Dot(hc - mid, n1) >= 0 ? n1 : -n1);
                var n3 = new Vector3(n.x, 0, n.y);
                for (float s = Tune.RailSampleStep * 0.5f; s < len; s += Tune.RailSampleStep)
                {
                    var q = a + d * s;
                    var probe = new Vector3(q.x - n.x * 0.8f, deckY + 0.5f, q.y - n.y * 0.8f);
                    if (!NavMesh.SamplePosition(probe, out var hit, Tune.RailSearch, M2.Filter)) continue;
                    if (M2.NodeOf(hit.position) != Map.GalleryNode) continue;
                    var edge = hit.position;
                    if (NavMesh.Raycast(hit.position, hit.position + n3 * 5f, out var eh, M2.Filter)) edge = eh.position;
                    DeckEyes.Add(edge + n3 * Tune.RailLean + Vector3.up * Tune.EyeHeight);
                }
            }
            float openAbove = (deckY - floor) + Tune.HallOpenAbove;
            int samples = 0, seen = 0, underLid = 0;
            for (float x = hall.XMin + Tune.FloorSampleStep * 0.5f; x < hall.XMax; x += Tune.FloorSampleStep)
            for (float z = hall.ZMin + Tune.FloorSampleStep * 0.5f; z < hall.ZMax; z += Tune.FloorSampleStep)
            {
                if (!hall.ContainsXZ(x, z)) continue;
                var probe = new Vector3(x, floor + 0.5f, z);
                if (!NavMesh.SamplePosition(probe, out var hit, 0.6f, M2.Filter)) continue;
                var hp = hit.position;
                if (HDist(hp, probe) > 0.3f || Mathf.Abs(hp.y - floor) > 0.5f || M2.NodeOf(hp) != hall.Index) continue;
                int t = M2.FindTri(hp, 1f);
                if (t < 0 || !M2.IsLevel(t)) continue;
                if (Physics.Raycast(hp + Vector3.up * 0.1f, Vector3.up, out var lid, Tune.CeilingMaxRay, ~0, QueryTriggerInteraction.Ignore)
                    && lid.distance + 0.1f < openAbove) { underLid++; continue; }
                samples++;
                var target = hp + Vector3.up * Tune.FloorTargetLift;
                bool vis = false;
                foreach (var e in DeckEyes) if (Clear(e, target)) { vis = true; break; }
                if (vis) { seen++; HallFloorSamples.Add(hp); }
                else HallFloorHidden.Add(hp);
            }
            float frac = samples > 0 ? seen / (float)samples : 0f;
            row.Measured.Add("c_rail_sight", new JObj().Add("eyes", DeckEyes.Count).Add("rail_points", Map.Rail.Count).Add("floor_samples", samples)
                .Add("skipped_under_lid_below_m", openAbove).Add("skipped_samples", underLid).Add("visible_fraction", frac)
                .Add("threshold", Tune.MinVisibleFraction).Add("lean_m", Tune.RailLean).Add("eye_m", Tune.EyeHeight)
                .Add("hidden_first_40", HallFloorHidden.Take(40).Select(p => (object)p).ToList()));
            if (DeckEyes.Count == 0) reasons.Add("no deck NavMesh under the rail to look from");
            // (c) is information, not scored (Roger, 2026-10-14): the floor is seen from a gallery by construction, as a
            // player leans over the rail
            string sight = "information, not scored: the rail sees " + Pc(frac) + " of the hall floor";

            bool ok = reasons.Count == 0;
            row.Short = ok ? "deck " + N1(rise) + " m up, " + N1(area) + " m2; reached from the way in without the floor; " + sight
                           : string.Join("; ", reasons) + (DeckEyes.Count > 0 ? "; " + sight : "");
            row.Reason = ok ? "the deck stands " + N1(rise) + " m above the hall floor with " + N1(area) + " m2 of level NavMesh; from where the way round comes in a complete path reaches it with the hall floor marked off; " +
                              "information, not scored: from " + DeckEyes.Count + " rail eyes (leaned " + N2(Tune.RailLean) + " m, eye " + Tune.EyeHeight + " m) " + seen + " of " + samples + " open floor samples are seen"
                            : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 4. the throne on a stage with steps

        bool? Row4_Stage(CheckRow row)
        {
            if (throne == null) return Fail(row, "the map lists no throne room");
            if (!Map.HasStage) return Fail(row, "the map has no stage");
            if (Map.Throne == null) return Fail(row, "the map has no throne position");
            var reasons = new List<string>();
            var tp = Map.Throne.Value;
            bool inStage = Map.Stage.Contains(tp.x, tp.z);
            if (!inStage) reasons.Add("the throne is not inside the stage outline (" + N1(Map.Stage.Distance(tp.x, tp.z)) + " m outside)");
            float floor = FloorOf(M2, throne);
            StageSamples.Clear();
            var S = Map.Stage;
            for (float x = S.XMin + Tune.StageSampleStep * 0.5f; x < S.XMax; x += Tune.StageSampleStep)
            for (float z = S.ZMin + Tune.StageSampleStep * 0.5f; z < S.ZMax; z += Tune.StageSampleStep)
            {
                if (!S.Contains(x, z)) continue;
                var probe = new Vector3(x, Map.StageTopY + 0.5f, z);
                if (!NavMesh.SamplePosition(probe, out var hit, 1.0f, M2.Filter)) continue;
                if (HDist(hit.position, probe) > 0.35f || !S.Contains(hit.position.x, hit.position.z) || hit.position.y < floor + 0.2f) continue;
                StageSamples.Add(hit.position);
            }
            float top = float.NaN;
            if (StageSamples.Count > 0)
            {
                float max = StageSamples.Max(p => p.y);
                top = Median(StageSamples.Where(p => p.y >= max - 0.25f).Select(p => p.y));
            }
            float rise = float.IsNaN(top) ? Map.StageTopY - throne.FloorY : top - floor;
            if (rise < Tune.StageMinRise) reasons.Add("the stage top is " + N2(rise) + " m above the throne room's floor (want " + Tune.StageMinRise + ")");
            if (float.IsNaN(top)) reasons.Add("no NavMesh on the stage top");

            // a walkable climb: a complete path from the room's floor, off the stage, onto its top
            string climb = "not tried";
            if (!float.IsNaN(top))
            {
                var topPts = StageSamples.Where(p => p.y >= top - 0.15f).OrderBy(p => HDist(p, tp)).ToList();
                var c = throne.Centroid;
                var foot = FloorPointNear(M2, throne, new Vector3(c.x, floor, c.y), Mathf.Max(throne.XMax - throne.XMin, throne.ZMax - throne.ZMin),
                    p => S.Distance(p.x, p.z) > 0.5f);
                if (foot == null || topPts.Count == 0) { climb = "no floor point off the stage"; reasons.Add("no floor of the throne room off the stage to climb from"); }
                else
                {
                    thronePoint = topPts[0];
                    var corners = PathCorners(foot.Value, thronePoint.Value, M2.Filter, out var st);
                    var dense = Densify(corners, Tune.PathSampleStep);
                    var rooms = RoomsVisited(M2, dense);
                    climb = st.ToString();
                    row.Measured.Add("climb", new JObj().Add("from", foot.Value).Add("to", thronePoint.Value).Add("status", climb)
                        .Add("length_m", PathLength(corners)).Add("rooms", rooms));
                    if (st != NavMeshPathStatus.PathComplete) reasons.Add("no walkable climb from the floor onto the stage (" + st + ")");
                    else if (rooms.Any(r => (string)r != throne.Name)) reasons.Add("the only climb onto the stage leaves the throne room (" + string.Join(" > ", rooms) + ")");
                }
            }

            // information: the far end, against the throne room's doors
            var far = new List<object>();
            foreach (var d in Map.Doors.Where(d => d.Joins(throne)))
            {
                var cc = throne.Centroid;
                var inward = new Vector2(cc.x - d.Position.x, cc.y - d.Position.z);
                if (inward.sqrMagnitude < 1e-4f) continue;
                inward.Normalize();
                float depth = throne.Floor.SelectMany(p => p.P).Max(v => Vector2.Dot(v - new Vector2(d.Position.x, d.Position.z), inward));
                float along = Vector2.Dot(new Vector2(tp.x - d.Position.x, tp.z - d.Position.z), inward);
                far.Add(new JObj().Add("door", d.Label).Add("throne_along_m", along).Add("room_depth_m", depth)
                    .Add("fraction", depth > 0 ? along / depth : float.NaN).Add("far_end", depth > 0 && along / depth >= Tune.FarEndFraction));
            }
            row.Measured.Add("throne", tp).Add("throne_in_stage", inStage).Add("room_floor_y", floor).Add("stage_top_map", Map.StageTopY)
                .Add("stage_top_measured", top).Add("rise_m", rise).Add("min_rise_m", Tune.StageMinRise).Add("stage_samples", StageSamples.Count)
                .Add("far_end_information", far);
            bool ok = reasons.Count == 0;
            var farText = far.Count > 0 ? "; information, not scored: the throne lies " + string.Join(", ", far.Cast<JObj>().Select(j => Pc(Convert.ToSingle(j["fraction"])) + " of the depth from " + j["door"])) : "";
            row.Short = ok ? "throne on a " + N2(rise) + " m stage, climbed from the floor" : string.Join("; ", reasons);
            row.Reason = (ok ? "the throne lies inside the stage outline; the stage top (" + StageSamples.Count + " NavMesh samples) stands " + N2(rise) +
                               " m above the floor; a complete path climbs onto it from the floor" : string.Join("; ", reasons)) + farText;
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 5. the vault: sealed when closed; opened, a path from its one neighbour

        bool? Row5_Vault(CheckRow row)
        {
            if (vault == null) return Fail(row, "the map lists no treasure vault");
            if (hidden == null) return Fail(row, "the map lists no hidden door");
            if (vaultNb == null) return Fail(row, "the hidden door (" + hidden.Label + ") does not join the vault");
            var reasons = new List<string>();
            row.Measured.Add("hidden_door", DoorText(hidden)).Add("neighbour", vaultNb.Name)
                .Add("closed_status_neighbour_to_vault", vaultClosedStatus).Add("closed_vault_openings", vaultClosedGaps);
            bool closedOk = vaultClosedStatus != NavMeshPathStatus.PathComplete.ToString();
            if (!closedOk) reasons.Add("closed (shut doors opened, the hidden door closed), " + vaultNb.Name + " already has a path into the vault");
            if (vaultClosedStatus != null && vaultClosedStatus.StartsWith("no ")) reasons.Add("closed: " + vaultClosedStatus);

            var opened = hiddenOpen != null ? hiddenOpen.How.Cast<object>().ToList() : new List<object>();
            row.Measured.Add("opened_by_switching_off", hiddenOpen?.Colliders.Count ?? 0).Add("opened_how", opened)
                .Add("e_from_neighbour", hiddenOpen?.Sides);
            if (hiddenOpen == null || hiddenOpen.Colliders.Count == 0) reasons.Add("nothing found to open at the hidden door (no Interact() within " + Tune.VaultInteractReach + " m, nothing in its span)");

            var gaps = M2.GapsOf(vault.Index);
            var names = gaps.Select(g => M2.NodeName(g.Other(vault.Index))).ToList();
            row.Measured.Add("opened_vault_openings", names.Cast<object>().ToList()).Add("opened_vault_navmesh_m2", M2.AreaOfNode(vault.Index, true));
            if (gaps.Count != 1 || gaps[0].Other(vault.Index) != vaultNb.Index)
                reasons.Add("opened, the vault opens to " + (names.Count == 0 ? "nothing" : string.Join(", ", names)) + " (want " + vaultNb.Name + " only)");

            var hp = vaultNbPoint = FloorPointNear(M2, vaultNb, hidden.Position, 8f, p => HDist(p, hidden.Position) >= 1.5f) ?? FloorPointNear(M2, vaultNb, hidden.Position, 8f);
            var hoard = Map.Pickups.FirstOrDefault(p => p.Kind == "hoard");
            var vc = vault.Centroid;
            vaultPoint = FloorPointNear(M2, vault, new Vector3(vc.x, vault.FloorY, vc.y), 30f);
            var hoardPoint = hoard != null && RoomNamed(hoard.Room) == vault ? FloorPointNear(M2, vault, hoard.Position, Tune.PickupSnap) : null;
            if (hp == null || vaultPoint == null) reasons.Add("opened, no floor point in " + (hp == null ? vaultNb.Name : "the vault"));
            else
            {
                var corners = PathCorners(hp.Value, vaultPoint.Value, M2.Filter, out var st);
                row.Measured.Add("opened_status", st.ToString()).Add("opened_path_m", PathLength(corners)).Add("opened_to", vaultPoint.Value);
                if (st != NavMeshPathStatus.PathComplete)
                {
                    var end = corners.Count > 0 ? corners[corners.Count - 1] : hp.Value;
                    row.Measured.Add("opened_path_ends_at", end).Add("opened_path_ends_in", M2.NodeName(M2.NodeOf(end)))
                        .Add("opened_path_short_by_m", Vector3.Distance(end, vaultPoint.Value));
                    reasons.Add("opened, no complete path from " + vaultNb.Name + " into the vault (" + st + "; it ends at " + Fmt(end) + " in " +
                                M2.NodeName(M2.NodeOf(end)) + ", " + N1(Vector3.Distance(end, vaultPoint.Value)) + " m short of " + Fmt(vaultPoint.Value) + ")");
                }
                // information: the hoard's own floor
                if (hoardPoint != null)
                {
                    var hc = PathCorners(hp.Value, hoardPoint.Value, M2.Filter, out var hs);
                    var hend = hc.Count > 0 ? hc[hc.Count - 1] : hp.Value;
                    row.Measured.Add("hoard_floor", hoardPoint.Value).Add("hoard_status", hs.ToString()).Add("hoard_path_m", PathLength(hc));
                    if (hs != NavMeshPathStatus.PathComplete)
                    {
                        row.Measured.Add("hoard_path_ends_at", hend).Add("hoard_path_short_by_m", Vector3.Distance(hend, hoardPoint.Value));
                        Notes.Add("row 5 (information): no complete NavMesh path from " + vaultNb.Name + " to the hoard's floor at " + Fmt(hoardPoint.Value) +
                                  " (" + hs + "; it ends " + N1(Vector3.Distance(hend, hoardPoint.Value)) + " m short, at " + Fmt(hend) + ")");
                    }
                }
                else row.Measured.Add("hoard_floor", null);
            }
            bool ok = reasons.Count == 0;
            row.Short = ok ? "sealed when closed; opened, one way in from " + vaultNb.Name : string.Join("; ", reasons);
            row.Reason = ok ? "with the shut doors opened and the hidden door closed there is no path from " + vaultNb.Name + " into the vault (" + vaultClosedStatus +
                              "); with " + hiddenOpen.Colliders.Count + " colliders of the hidden door switched off, the vault's only opening is to " + vaultNb.Name + " and a complete path enters it"
                            : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 6 and 7: ceilings (v1's method): a ray up from each floor sample; a renderer fallback per sample

        void MeasureCeilings()
        {
            List<Renderer> renderers = null;
            foreach (var r in Map.Rooms)
            {
                float floor = FloorOf(M2, r);
                var pts = FloorSamples(M2, r, Tune.CeilingSampleStep);
                if (pts.Count < Tune.CeilingMinSamples) pts = FloorSamples(M2, r, 1f);
                var hits = new List<float>();
                var open = new List<Vector3>();
                foreach (var p in pts)
                {
                    if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, out var rh, Tune.CeilingMaxRay, ~0, QueryTriggerInteraction.Ignore))
                        hits.Add(rh.distance + 0.1f);
                    else open.Add(p);
                }
                string source = "physics";
                if (open.Count > 0)
                {
                    renderers = renderers ?? scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).ToList();
                    int before = hits.Count;
                    foreach (var p in open)
                    {
                        float bestH = float.MaxValue;
                        foreach (var rd in renderers)
                        {
                            var b = rd.bounds;
                            if (b.size.x * b.size.z < 4f || b.min.y < p.y + 2f) continue;
                            if (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z) continue;
                            bestH = Mathf.Min(bestH, b.min.y - p.y);
                        }
                        if (bestH < float.MaxValue) hits.Add(bestH);
                    }
                    int found = hits.Count - before;
                    if (found > 0) source = before == 0 ? "renderer bounds (no ceiling colliders)" : "physics; renderer bounds for " + found + " samples";
                }
                hits.Sort();
                float lf = pts.Count > 0 ? hits.Count(x => x <= Tune.LowCeilingMax + Tune.HeightSlack) / (float)pts.Count : 0f;
                float tf = pts.Count > 0 ? hits.Count(x => x >= Tune.TallCeilingMin - Tune.HeightSlack) / (float)pts.Count : 0f;
                float At(float q) => hits.Count > 0 ? hits[Mathf.Clamp(Mathf.FloorToInt(q * hits.Count), 0, hits.Count - 1)] : float.NaN;
                ceil[r] = (pts.Count, hits.Count, lf, tf, At(Tune.HeightTargetPercentile), At(0.5f));
                ceilings[r] = new JObj().Add("kind", r.Kind).Add("floor_y", floor).Add("samples", pts.Count).Add("with_ceiling", hits.Count)
                    .Add("ceiling_fraction", pts.Count > 0 ? hits.Count / (float)pts.Count : 0f)
                    .Add("low_fraction", lf).Add("tall_fraction", tf).Add("median_ceiling_m", At(0.5f)).Add("p90_ceiling_m", At(Tune.HeightTargetPercentile))
                    .Add("source", source);
            }
        }

        static readonly string[] LowKinds = { "prison", "guard room", "kitchen", "treasure vault" };
        static readonly string[] TallKinds = { "dining hall", "throne room" };

        bool? Row6_Heights(CheckRow row)
        {
            var reasons = new List<string>();
            var lowParts = new List<string>(); var tallParts = new List<string>();
            var j = new JObj();
            foreach (var kv in ceilings) j.Add(kv.Key.Name, kv.Value);
            row.Measured.Add("rooms", j);
            foreach (var k in LowKinds)
            {
                var r = Map.RoomOfKind(k);
                if (r == null) { reasons.Add("no " + k + " in the map"); continue; }
                var c = ceil[r];
                if (c.hits == 0) { reasons.Add(r.Name + " has no measurable ceiling"); continue; }
                lowParts.Add(r.Name + " " + Pc(c.low));
                if (c.low < Tune.LowMinFraction) reasons.Add(r.Name + " is not low: " + Pc(c.low) + " of its floor under a ceiling of at most " + Tune.LowCeilingMax + " m (want " + Pc(Tune.LowMinFraction) + ")");
            }
            foreach (var k in TallKinds)
            {
                var r = Map.RoomOfKind(k);
                if (r == null) { reasons.Add("no " + k + " in the map"); continue; }
                var c = ceil[r];
                if (c.hits == 0) { reasons.Add(r.Name + " has no measurable ceiling"); continue; }
                tallParts.Add(r.Name + " " + Pc(c.tall));
                if (c.tall < Tune.TallMinFraction) reasons.Add(r.Name + " is not tall: " + Pc(c.tall) + " of its floor under a ceiling of at least " + Tune.TallCeilingMin + " m (want " + Pc(Tune.TallMinFraction) + ")");
            }
            string tallest = "";
            if (throne != null && ceil.TryGetValue(throne, out var tc) && tc.hits > 0)
            {
                float cap = tc.p90 + Tune.TallestSlack;
                var over = ceil.Where(kv => kv.Key != throne && kv.Value.hits > 0 && kv.Value.p90 > cap).Select(kv => kv.Key.Name + " p90 " + N1(kv.Value.p90) + " m").ToList();
                row.Measured.Add("throne_room_p90_m", tc.p90).Add("tallest_cap_m", cap);
                if (over.Count > 0) reasons.Add("taller than the throne room (p90 " + N1(tc.p90) + " m + " + Tune.TallestSlack + "): " + string.Join(", ", over));
                var top = ceil.Where(kv => kv.Value.hits > 0).OrderByDescending(kv => kv.Value.p90).Take(3).Select(kv => kv.Key.Name + " " + N1(kv.Value.p90)).ToList();
                tallest = "; tallest p90: " + string.Join(", ", top) + " m";
            }
            bool ok = reasons.Count == 0;
            row.Short = "low (<= " + Tune.LowCeilingMax + " m, want " + Pc(Tune.LowMinFraction) + "): " + string.Join(", ", lowParts) +
                        "; tall (>= " + Tune.TallCeilingMin + " m, want " + Pc(Tune.TallMinFraction) + "): " + string.Join(", ", tallParts) + tallest;
            row.Reason = ok ? "every low room has at least " + Pc(Tune.LowMinFraction) + " of its floor under at most " + Tune.LowCeilingMax + " m, every tall room at least " +
                              Pc(Tune.TallMinFraction) + " under at least " + Tune.TallCeilingMin + " m, and no room's p90 ceiling exceeds the throne room's + " + Tune.TallestSlack + " m"
                            : string.Join("; ", reasons);
            return ok;
        }

        bool? Row7_Ceilings(CheckRow row)
        {
            var reasons = new List<string>();
            var j = new JObj();
            float worst = float.MaxValue; string worstRoom = null;
            foreach (var r in Map.Rooms)
            {
                var c = ceil[r];
                float f = c.pts > 0 ? c.hits / (float)c.pts : 0f;
                j.Add(r.Name, new JObj().Add("samples", c.pts).Add("with_ceiling", c.hits).Add("fraction", f));
                if (c.pts == 0) { reasons.Add(r.Name + " has no floor samples"); continue; }
                if (f < worst) { worst = f; worstRoom = r.Name; }
                if (f < Tune.CeilingMinFraction) reasons.Add(r.Name + ": " + Pc(f) + " of its floor samples have a ceiling (want " + Pc(Tune.CeilingMinFraction) + ")");
            }
            row.Measured.Add("rooms", j).Add("threshold", Tune.CeilingMinFraction);
            bool ok = reasons.Count == 0;
            row.Short = ok ? "every room roofed; the least is " + worstRoom + " at " + Pc(worst) : string.Join("; ", reasons);
            row.Reason = ok ? "in every room at least " + Pc(Tune.CeilingMinFraction) + " of the floor samples have a collider (or a wide renderer) above them" : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 8. doors: every opening is listed, every listed door is an opening, every shut door is shut and opens by E

        bool? Row8_Doors(CheckRow row)
        {
            var reasons = new List<string>();
            var matched = new HashSet<MapDoor>();
            var unlisted = new List<object>();
            foreach (var g in M2.Gaps)
            {
                var ra = RoomOfNode(g.A); var rb = RoomOfNode(g.B);
                if (ra != null && ra == rb) continue;   // inside the dining hall: its floor and its gallery
                var d = MatchDoor(M2, g);
                if (d != null) { matched.Add(d); continue; }
                DoorDots.Add((g.Centre, 2));
                unlisted.Add(new JObj().Add("between", new List<object> { M2.NodeName(g.A), M2.NodeName(g.B) }).Add("via", g.Via).Add("centre", g.Centre).Add("navmesh_width_m", g.Width));
            }
            var missing = Map.Doors.Where(d => !matched.Contains(d)).Select(d => (object)DoorText(d)).ToList();
            foreach (var d in Map.Doors) DoorDots.Add((d.Position, matched.Contains(d) ? 0 : 1));
            if (unlisted.Count > 0) reasons.Add(unlisted.Count + " openings between rooms are not listed doors");
            if (missing.Count > 0) reasons.Add(missing.Count + " listed doors are not openings with every door opened");

            var shutJ = new List<object>();
            int shutOk = 0, shutN = 0;
            foreach (var d in Map.Doors.Where(x => x.State == "shut"))
            {
                shutN++;
                var o = shutOpen.TryGetValue(d, out var oo) ? oo : null;
                bool closedAsBuilt = !M0.Gaps.Any(g => d.RA != null && d.RB != null &&
                    ((RoomOfNode(g.A) == d.RA && RoomOfNode(g.B) == d.RB) || (RoomOfNode(g.A) == d.RB && RoomOfNode(g.B) == d.RA)) &&
                    g.DistNear(d.Position, Tune.DoorMatchYBand) <= d.Width * 0.5f + Tune.DoorMatchSlack);
                bool eOk = o != null && o.SidesFound > 0 && o.SidesOk == o.SidesFound;
                var why = new List<string>();
                if (!closedAsBuilt) why.Add("open as built (the NavMesh crosses it with every door as the scene has it)");
                if (o == null || o.SidesFound == 0) why.Add("no floor beside it to press E from");
                else if (o.SidesOk < o.SidesFound) why.Add("E fails from " + (o.SidesFound - o.SidesOk) + " of " + o.SidesFound + " sides");
                if (!matched.Contains(d)) why.Add("not an opening with every door opened");
                bool ok1 = why.Count == 0;
                if (ok1) shutOk++;
                shutJ.Add(new JObj().Add("door", DoorText(d)).Add("shut_as_built", closedAsBuilt).Add("e_sides", o?.Sides)
                    .Add("switched_off", o?.Colliders.Count ?? 0).Add("ok", ok1).Add("why", string.Join("; ", why)));
                if (!ok1) reasons.Add(d.Label + ": " + string.Join("; ", why));
            }
            // information: listed open doors closed as built
            foreach (var d in Map.Doors.Where(x => x.State == "open"))
            {
                bool openAsBuilt = M0.Gaps.Any(g => ((RoomOfNode(g.A) == d.RA && RoomOfNode(g.B) == d.RB) || (RoomOfNode(g.A) == d.RB && RoomOfNode(g.B) == d.RA)) &&
                                                   g.DistNear(d.Position, Tune.DoorMatchYBand) <= d.Width * 0.5f + Tune.DoorMatchSlack);
                if (!openAsBuilt) Notes.Add("row 8: " + d.Label + " is listed open but the NavMesh does not cross it as built");
            }
            row.Measured.Add("listed_doors", Map.Doors.Count).Add("openings_between_rooms", M2.Gaps.Count(g => RoomOfNode(g.A) == null || RoomOfNode(g.A) != RoomOfNode(g.B)))
                .Add("unlisted_openings", unlisted).Add("listed_doors_without_an_opening", missing)
                .Add("shut_doors", shutJ).Add("passages", M2.Passages)
                .Add("e_contract", "a " + Tune.InteractReach + " m ray from eye height " + Tune.EyeHeight + " m, from floor on each side toward the door; the first solid collider must reach an Interact() by SendMessageUpwards");
            bool ok = reasons.Count == 0;
            row.Short = ok ? Map.Doors.Count + " listed doors, all openings, none unlisted; " + shutOk + "/" + shutN + " shut doors shut and opened by E"
                           : string.Join("; ", reasons);
            row.Reason = ok ? "with every door opened each opening between two rooms matches a listed door and each listed door is an opening; every shut door blocks the NavMesh as built and its first solid collider on the E ray reaches Interact() from each side"
                            : string.Join("; ", reasons);
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 9. occluders: solid colliders standing on a room's floor, away from walls, at least 0.6 m wide

        bool? Row9_Occluders(CheckRow row)
        {
            Covers.Clear();
            var per = new Dictionary<MapRoom, (int tall, int low)>();
            foreach (var r in Map.Rooms) per[r] = (0, 0);
            var seenRoots = new HashSet<UnityEngine.Object>();
            foreach (var c in allColliders)
            {
                if (!Doors.Solid(c)) continue;
                var b = c.bounds;
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w < Tune.CoverMinWidth || w > Tune.CoverMaxWidth) continue;
                MapRoom room = null; float floor = 0f;
                foreach (var r in Map.Rooms)
                {
                    if (!r.ContainsXZ(b.center.x, b.center.z)) continue;
                    float f = floorCache.TryGetValue((M2, r.Index), out var cf) ? cf : FloorOf(M2, r);
                    if (Mathf.Abs(b.min.y - f) > Tune.CoverBaseTolerance) continue;
                    room = r; floor = f; break;
                }
                if (room == null) continue;
                float h = b.max.y - floor;
                bool tall = h >= Tune.CoverTall, low = h >= Tune.CoverLowMin && h <= Tune.CoverLowMax;
                if (!tall && !low) continue;
                int openSides = 0;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float reach = Mathf.Abs(d.x) * b.extents.x + Mathf.Abs(d.z) * b.extents.z + Tune.CoverClear;
                    var q = new Vector3(b.center.x, floor + 0.3f, b.center.z) + d * reach;
                    if (!NavMesh.SamplePosition(q, out var hit, 0.5f, M2.Filter)) continue;
                    if (HDist(hit.position, q) > 0.4f || Mathf.Abs(hit.position.y - floor) > 0.5f || M2.NodeOf(hit.position) != room.Index) continue;
                    openSides++;
                }
                if (openSides < Tune.CoverMinOpenSides) continue;
                UnityEngine.Object key = c.attachedRigidbody != null ? (UnityEngine.Object)c.attachedRigidbody : c.transform;
                if (!seenRoots.Add(key)) continue;
                var cur = per[room];
                per[room] = tall ? (cur.tall + 1, cur.low) : (cur.tall, cur.low + 1);
                Covers.Add((b.center, tall));
            }
            int T = per.Values.Sum(v => v.tall), L = per.Values.Sum(v => v.low);
            var j = new JObj();
            foreach (var kv in per) j.Add(kv.Key.Name, new JObj().Add("tall", kv.Value.tall).Add("low", kv.Value.low));
            row.Measured.Add("tall_total", T).Add("low_total", L).Add("per_room", j)
                .Add("rule", "solid collider " + Tune.CoverMinWidth + " to " + Tune.CoverMaxWidth + " m wide, its bottom within " + Tune.CoverBaseTolerance +
                             " m of a room's floor; tall: top >= floor + " + Tune.CoverTall + " m; low: top at floor + " + Tune.CoverLowMin + " to " + Tune.CoverLowMax +
                             " m; away from walls: floor found " + Tune.CoverClear + " m out on at least " + Tune.CoverMinOpenSides + " of 8 sides");
            bool ok = T > 0 && L > 0;
            var rooms = per.Where(kv => kv.Value.tall + kv.Value.low > 0).Select(kv => kv.Key.Name + " " + kv.Value.tall + "/" + kv.Value.low).ToList();
            row.Short = T + " tall and " + L + " low occluders (per room tall/low: " + string.Join(", ", rooms) + ")";
            row.Reason = ok ? "the level has both tall (stand behind) and low (crouch behind) occluders away from walls" : "the level has " + (T == 0 ? "no tall" : "no low") + " occluder away from walls";
            return ok;
        }

        // ---------------------------------------------------------------------------------------------
        // 10. five walks

        void RunWalks()
        {
            if (!doWalk) return;
            var all = M2.Filter;
            Walks.Add(hall == null || armory == null || hallPoint == null
                ? WalkResult.Missing("start to the dining hall, avoiding the armory", "no dining hall, armory or hall floor point")
                : Walker.Walk(M2, "start to the dining hall, avoiding the armory", new List<Vector3?> { StartPosition, hallPoint }, NavBake.FilterWithout(M2, Tune.ArmoryArea)));
            Walks.Add(wayInPoint == null || deckPoint == null
                ? WalkResult.Missing("the way-round entrance up to the gallery deck, avoiding the hall floor", "row 3 found no way in or no deck point reached")
                : Walker.Walk(M2, "the way-round entrance up to the gallery deck, avoiding the hall floor", new List<Vector3?> { wayInPoint, deckPoint }, NavBake.FilterWithout(M2, Tune.HallFloorArea)));
            var gear = Map.Pickups.FirstOrDefault(p => p.Kind == "best gear");
            var gearRoom = gear != null ? RoomNamed(gear.Room) ?? RoomOfNode(Map.NodeAt(gear.Position)) : null;
            var gearPoint = gear != null && gearRoom != null ? FloorPointNear(M2, gearRoom, gear.Position, Tune.PickupSnap) : null;
            var fg = flank ?? flankAny;
            var flankPoint = fg != null ? Snap(M2, fg.Centre, 1.5f, all) : null;
            const string w3 = "the dining hall into the armory beneath the gallery, to the best gear";
            if (flankPoint == null || gearPoint == null || hallPoint == null)
                Walks.Add(WalkResult.Missing(w3, fg == null ? "the armory has no opening to the dining hall (row 1)" : gearPoint == null ? "no floor by the best gear" : "no hall floor point"));
            else
            {
                var w = Walker.Walk(M2, w3, new List<Vector3?> { hallPoint, flankPoint, gearPoint }, all);
                if (flank == null) w.Note = "through the armory's dining-hall door, which row 1 finds is not beneath the gallery: " + w.Note;
                Walks.Add(w);
            }
            Walks.Add(vaultNbPoint == null || vaultPoint == null
                ? WalkResult.Missing("into the vault by the hidden way, and back", "no floor point beside the hidden door or in the vault")
                : Walker.Walk(M2, "into the vault by the hidden way, and back", new List<Vector3?> { vaultNbPoint, vaultPoint, vaultNbPoint }, all));
            var tp = thronePoint ?? (Map.Throne != null ? Snap(M2, Map.Throne.Value + Vector3.up * 0.3f, 3f, all) : null);
            Walks.Add(hallPoint == null || tp == null
                ? WalkResult.Missing("the dining hall to the throne", "no hall floor point or no NavMesh by the throne")
                : Walker.Walk(M2, "the dining hall to the throne", new List<Vector3?> { hallPoint, tp }, all));
        }

        bool? Row10_Walks(CheckRow row)
        {
            if (!doWalk) { row.Short = "walks skipped (-noWalk)"; row.Reason = row.Short; return null; }
            var bad = Walks.Where(w => !w.Pass).ToList();
            row.Measured.Add("routes", Walks.Select(w => (object)new JObj().Add("route", w.Route).Add("pass", w.Pass)
                .Add("walked_m", w.Walked).Add("planned_m", w.Planned).Add("snags", w.Snags.Count).Add("opened_by_e", w.Opened.Count).Add("note", w.Note)).ToList());
            row.Short = (Walks.Count - bad.Count) + "/" + Walks.Count + " walks reached their end";
            row.Reason = bad.Count == 0 ? "a CharacterController (r " + Tune.AgentRadius + ", h " + Tune.AgentHeight + ", step " + Tune.AgentClimb + ", skin " + Tune.WalkSkin +
                                          ") followed each walk to its end, opening shut doors by E"
                                        : string.Join("; ", bad.Select(w => w.Route + ": " + w.Note));
            return bad.Count == 0;
        }

        // ---------------------------------------------------------------------------------------------
        // information rows

        void Info1_GallerySight(CheckRow row)
        {
            var targets = HallFloorSamples.Concat(HallFloorHidden).ToList();
            if (hall == null || !Map.HasGallery || targets.Count == 0) { row.Short = "no gallery or no hall floor samples"; row.Reason = row.Short; return; }
            float deckY = float.IsNaN(deckHeight) ? Map.DeckY : deckHeight;
            var gal = Map.Enemies.Where(e => e.Kind == "ranged" && (Map.DistToDeck(e.Position.x, e.Position.z) <= 1f && Mathf.Abs(e.Position.y - deckY) <= 1.5f)).ToList();
            var l = new List<object>(); var parts = new List<string>();
            foreach (var e in gal)
            {
                var eye = e.Position + Vector3.up * Tune.EyeHeight;
                int seen = targets.Count(t => Clear(eye, t + Vector3.up * Tune.FloorTargetLift));
                float railDist = float.NaN;
                for (int i = 0; i + 1 < Map.Rail.Count; i++)
                {
                    var a = Map.Rail[i]; var b = Map.Rail[i + 1]; var ab = b - a; var p = new Vector2(e.Position.x, e.Position.z);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float dd = (a + ab * t - p).magnitude;
                    railDist = float.IsNaN(railDist) ? dd : Mathf.Min(railDist, dd);
                }
                l.Add(new JObj().Add("enemy", e.Label).Add("position", e.Position).Add("from_rail_m", railDist).Add("floor_seen", seen).Add("floor_samples", targets.Count)
                    .Add("fraction", seen / (float)targets.Count));
                parts.Add(e.Label + " " + Pc(seen / (float)targets.Count) + " (" + N1(railDist) + " m from the rail)");
            }
            row.Measured.Add("gallery_ranged", l).Add("eye_m", Tune.EyeHeight);
            row.Short = gal.Count == 0 ? "no ranged enemy on the gallery" : string.Join("; ", parts);
            row.Reason = "share of the open hall floor samples (row 3) each gallery ranged enemy sees from its own eye, not leaned";
        }

        void Info2_Honesty(CheckRow row)
        {
            var issues = new List<string>();
            // markers: a scene object within 1 m
            var objs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(false))
                .Where(t => t.parent != null && (t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null || t.childCount > 0)).ToList();
            var missing = new List<object>();
            foreach (var a in Map.Enemies.Concat(Map.Pickups))
            {
                bool found = objs.Any(t => HDist(t.position, a.Position) <= Tune.SceneObjectNear && Mathf.Abs(t.position.y - a.Position.y) <= 2f);
                if (!found) missing.Add(a.Label + " at " + Fmt(a.Position));
            }
            if (missing.Count > 0) issues.Add(missing.Count + " markers with no scene object within " + Tune.SceneObjectNear + " m");
            // markers in the room they name
            var wrongRoom = new List<object>();
            foreach (var a in Map.Enemies.Concat(Map.Pickups))
            {
                int n = Map.NodeAt(a.Position);
                var r = RoomOfNode(n);
                if (r == null || r != RoomNamed(a.Room)) wrongRoom.Add(a.Label + " stands in " + (r != null ? r.Name : "no room's outline"));
            }
            if (wrongRoom.Count > 0) issues.Add(wrongRoom.Count + " markers outside the room they name");
            // outlines that overlap (same storey)
            var overlaps = new List<object>();
            for (int i = 0; i < Map.Rooms.Count; i++)
            for (int k = i + 1; k < Map.Rooms.Count; k++)
            {
                var a = Map.Rooms[i]; var b = Map.Rooms[k];
                if (Mathf.Abs(a.FloorY - b.FloorY) >= Tune.RoomBandHigh - Tune.RoomBandLow) continue;
                float x0 = Mathf.Max(a.XMin, b.XMin), x1 = Mathf.Min(a.XMax, b.XMax), z0 = Mathf.Max(a.ZMin, b.ZMin), z1 = Mathf.Min(a.ZMax, b.ZMax);
                if (x1 <= x0 || z1 <= z0) continue;
                int cells = 0;
                for (float x = x0 + 0.25f; x < x1; x += 0.5f)
                for (float z = z0 + 0.25f; z < z1; z += 0.5f)
                    if (a.ContainsXZ(x, z) && b.ContainsXZ(x, z)) cells++;
                if (cells * 0.25f >= 0.5f) overlaps.Add(a.Name + " / " + b.Name + ": " + N1(cells * 0.25f) + " m2");
            }
            if (overlaps.Count > 0) issues.Add(overlaps.Count + " pairs of outlines overlap");
            // kinds and states
            var kinds = Map.Rooms.Select(r => r.Kind).ToList();
            var missingKinds = MapSpec.Kinds.Where(k => !kinds.Contains(k)).Cast<object>().ToList();
            var dupKinds = kinds.Where(k => k != "extra").GroupBy(k => k).Where(g => g.Count() > 1).Select(g => (object)(g.Key + " x" + g.Count())).ToList();
            var unknownKinds = Map.Rooms.Where(r => r.Kind != "extra" && !MapSpec.Kinds.Contains(r.Kind)).Select(r => (object)(r.Name + ": '" + r.RawKind + "'")).ToList();
            var badStates = Map.Rooms.Where(r => !MapSpec.States.Contains(r.State)).Select(r => (object)(r.Name + ": '" + r.State + "'")).ToList();
            if (missingKinds.Count > 0) issues.Add("rooms missing a kind: " + string.Join(", ", missingKinds));
            if (dupKinds.Count > 0) issues.Add("kinds used twice: " + string.Join(", ", dupKinds));
            if (unknownKinds.Count > 0) issues.Add("unknown kinds: " + string.Join(", ", unknownKinds));
            if (badStates.Count > 0) issues.Add("unknown states: " + string.Join(", ", badStates));
            // floor_y against the measured floor
            var floorOff = new List<object>();
            foreach (var r in Map.Rooms)
            {
                float f = FloorOf(M2, r);
                if (Mathf.Abs(f - r.FloorY) > Tune.FloorYTolerance) floorOff.Add(r.Name + ": map " + N2(r.FloorY) + ", measured " + N2(f));
            }
            if (floorOff.Count > 0) issues.Add(floorOff.Count + " rooms' floor_y off the measured floor by more than " + Tune.FloorYTolerance + " m");
            // pockets: NavMesh outside every outline that touches one room
            var pockets = M2.Pockets.Where(p => p.Area >= Tune.PocketReportArea).Select(p => (object)(M2.NodeName(p.Node) + ": " + N1(p.Area) + " m2 at " + Fmt(p.Centre))).ToList();
            if (pockets.Count > 0) issues.Add(pockets.Count + " areas of NavMesh outside every outline (from " + Tune.PocketReportArea + " m2)");
            row.Measured.Add("markers_without_scene_object", missing).Add("markers_outside_their_room", wrongRoom).Add("outline_overlaps", overlaps)
                .Add("missing_kinds", missingKinds).Add("kinds_used_twice", dupKinds).Add("unknown_kinds", unknownKinds).Add("unknown_states", badStates)
                .Add("floor_y_off", floorOff).Add("unassigned_navmesh", pockets).Add("map_problems", Map.Problems.Cast<object>().ToList())
                .Add("room_navmesh_m2", Map.Rooms.Select(r => (object)(r.Name + " " + N1(M2.AreaOfNode(r.Index, true)))).ToList());
            if (Map.Problems.Count > 0) issues.Add(Map.Problems.Count + " problems reading the map");
            row.Short = issues.Count == 0 ? "the map matches the scene" : string.Join("; ", issues);
            row.Reason = "markers against scene objects and their rooms, outlines against each other, kinds, states, floor_y against the measured floor";
        }

        void Info3_Defenders(CheckRow row)
        {
            var lines = new List<string>();
            var j = new JObj();
            int InRoom(MapRoom r, string kind = null) => r == null ? 0 : Map.Enemies.Count(e => RoomNamed(e.Room) == r && (kind == null || e.Kind == kind));
            // ranged behind the armory entrance, facing the entry hall
            if (armory != null && choke != null)
            {
                var doorP = chokeTarget.HasValue ? chokeTarget.Value - Vector3.up * Tune.DoorTargetLift : choke.Centre;
                var r = Map.Enemies.Where(e => RoomNamed(e.Room) == armory && e.Kind == "ranged").ToList();
                int near = r.Count(e => HDist(e.Position, doorP) <= Tune.ChokeGuardReach);
                int facing = r.Count(e => e.HasFacing && Vector2.Dot(e.Facing.normalized, new Vector2(doorP.x - e.Position.x, doorP.z - e.Position.z).normalized) >= Tune.ChokeFacingDot);
                j.Add("armory_ranged", r.Count).Add("within_m_of_the_doorway", Tune.ChokeGuardReach).Add("armory_ranged_near_doorway", near).Add("armory_ranged_facing_doorway", facing);
                lines.Add((r.Count > 0 && near > 0 && facing > 0 ? "yes" : "NO") + ": ranged behind the armory entrance (" + r.Count + " ranged, " + near + " within " + Tune.ChokeGuardReach + " m, " + facing + " facing it)");
            }
            else lines.Add("NO: no armory entrance to stand behind");
            int melee = InRoom(armory, "melee");
            j.Add("armory_melee", melee);
            lines.Add((melee > 0 ? "yes" : "NO") + ": melee in the rest of the armory (" + melee + ")");
            float deckY = float.IsNaN(deckHeight) ? Map.DeckY : deckHeight;
            int galR = Map.HasGallery ? Map.Enemies.Count(e => e.Kind == "ranged" && Map.DistToDeck(e.Position.x, e.Position.z) <= 1f && Mathf.Abs(e.Position.y - deckY) <= 1.5f) : 0;
            j.Add("gallery_ranged", galR);
            lines.Add((galR > 0 ? "yes" : "NO") + ": ranged on the gallery (" + galR + ")");
            var counts = Map.Rooms.Select(r => (r, n: InRoom(r))).OrderByDescending(x => x.n).ToList();
            int hallN = InRoom(hall);
            int otherMax = counts.Where(x => x.r != hall).Select(x => x.n).DefaultIfEmpty(0).Max();
            j.Add("per_room", counts.Where(x => x.n > 0).Select(x => (object)(x.r.Name + " " + x.n)).ToList());
            lines.Add((hall != null && hallN > otherMax ? "yes" : hall != null && hallN == otherMax ? "TIE" : "NO") + ": the dining hall holds the largest group (" + hallN + "; next " + otherMax + ")");
            var warlords = Map.Enemies.Where(e => e.Kind == "warlord").ToList();
            bool wl = warlords.Count == 1 && RoomNamed(warlords[0].Room) == throne;
            j.Add("warlords", warlords.Select(e => (object)e.Label).ToList());
            lines.Add((wl ? "yes" : "NO") + ": the warlord in the throne room (" + warlords.Count + " warlords" + (warlords.Count > 0 ? ", in " + string.Join(", ", warlords.Select(e => e.Room)) : "") + ")");
            int eh = InRoom(entry);
            lines.Add((eh == 0 ? "yes" : "NO") + ": no one in the entry hall (" + eh + ")");
            var gear = Map.Pickups.Where(p => p.Kind == "best gear").ToList();
            var hoard = Map.Pickups.Where(p => p.Kind == "hoard").ToList();
            bool g1 = gear.Count == 1 && RoomNamed(gear[0].Room) == armory, h1 = hoard.Count == 1 && RoomNamed(hoard[0].Room) == vault;
            lines.Add((g1 ? "yes" : "NO") + ": the best gear in the armory (" + gear.Count + ")");
            lines.Add((h1 ? "yes" : "NO") + ": the hoard in the vault (" + hoard.Count + ")");
            var otherKinds = Map.Enemies.Where(e => e.Kind != "ranged" && e.Kind != "melee" && e.Kind != "warlord").Select(e => (object)e.Label).ToList();
            if (otherKinds.Count > 0) lines.Add("NOTE: enemies of other kinds: " + string.Join(", ", otherKinds));
            j.Add("pickups", Map.Pickups.Select(p => (object)p.Label).ToList()).Add("lines", lines.Cast<object>().ToList());
            row.Measured.Add("defenders", j);
            row.Short = string.Join("; ", lines);
            row.Reason = "the map's enemies and pickups against the brief's 'Defenders and pickups' paragraph";
        }
    }
}
