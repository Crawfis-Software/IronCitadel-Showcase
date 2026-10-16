// IronCitadel conformance kit v2: the build's map.json (THE MAP in cold-prompt-v3.md), read once, with the
// point-in-room geometry the checks need. Rooms come from the map's outlines, not from a grid.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IronCitadel.ConformanceV2
{
    /// <summary>One outline, [x, z] corners in order; even-odd point-in-polygon.</summary>
    public sealed class Poly
    {
        public readonly Vector2[] P;
        public readonly float XMin, XMax, ZMin, ZMax;

        public Poly(Vector2[] p)
        {
            P = p;
            XMin = p.Min(v => v.x); XMax = p.Max(v => v.x);
            ZMin = p.Min(v => v.y); ZMax = p.Max(v => v.y);
        }

        public bool Contains(float x, float z)
        {
            if (x < XMin || x > XMax || z < ZMin || z > ZMax) return false;
            bool inside = false;
            for (int i = 0, j = P.Length - 1; i < P.Length; j = i++)
            {
                var a = P[i]; var b = P[j];
                if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        public float Area
        {
            get
            {
                double s = 0;
                for (int i = 0, j = P.Length - 1; i < P.Length; j = i++) s += (double)P[j].x * P[i].y - (double)P[i].x * P[j].y;
                return Mathf.Abs((float)(s * 0.5));
            }
        }

        public Vector2 Centroid
        {
            get
            {
                double a = 0, cx = 0, cz = 0;
                for (int i = 0, j = P.Length - 1; i < P.Length; j = i++)
                {
                    double f = (double)P[j].x * P[i].y - (double)P[i].x * P[j].y;
                    a += f; cx += (P[j].x + P[i].x) * f; cz += (P[j].y + P[i].y) * f;
                }
                if (Math.Abs(a) < 1e-6) return new Vector2(P.Average(v => v.x), P.Average(v => v.y));
                return new Vector2((float)(cx / (3 * a)), (float)(cz / (3 * a)));
            }
        }

        /// <summary>Horizontal distance from a point to the outline's edges (0 inside).</summary>
        public float Distance(float x, float z)
        {
            if (Contains(x, z)) return 0f;
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            for (int i = 0, j = P.Length - 1; i < P.Length; j = i++)
            {
                var a = P[j]; var b = P[i]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }
    }

    public sealed class MapRoom
    {
        public int Index;
        public string Name, Kind, State, RawKind;
        public float FloorY;
        public readonly List<Poly> Floor = new List<Poly>();
        public float XMin = float.MaxValue, XMax = float.MinValue, ZMin = float.MaxValue, ZMax = float.MinValue;

        public bool ContainsXZ(float x, float z)
        {
            if (x < XMin || x > XMax || z < ZMin || z > ZMax) return false;
            foreach (var p in Floor) if (p.Contains(x, z)) return true;
            return false;
        }

        public float Area => Floor.Sum(p => p.Area);

        public Vector2 Centroid
        {
            get
            {
                float a = 0; var c = Vector2.zero;
                foreach (var p in Floor) { float w = p.Area; a += w; c += p.Centroid * w; }
                return a > 0 ? c / a : new Vector2((XMin + XMax) * 0.5f, (ZMin + ZMax) * 0.5f);
            }
        }

        /// <summary>True when a disc of radius <paramref name="d"/> round the point lies inside the outline (16 + 8 probes).</summary>
        public bool InsetInside(float x, float z, float d)
        {
            if (!ContainsXZ(x, z)) return false;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                if (!ContainsXZ(x + d * Mathf.Cos(a), z + d * Mathf.Sin(a))) return false;
            }
            for (int k = 0; k < 8; k++)
            {
                float a = (k + 0.5f) * Mathf.PI / 4f;
                if (!ContainsXZ(x + 0.5f * d * Mathf.Cos(a), z + 0.5f * d * Mathf.Sin(a))) return false;
            }
            return true;
        }
    }

    public sealed class MapDoor
    {
        public int Index;
        public string A, B, State;
        public MapRoom RA, RB;
        public Vector3 Position;
        public float Width;
        public string Label => A + " - " + B + " (" + State + ")";
        public bool Joins(MapRoom r) => r != null && (RA == r || RB == r);
        public MapRoom Other(MapRoom r) => RA == r ? RB : RA;
    }

    public sealed class MapActor
    {
        public int Index;
        public string Kind, Room;
        public Vector3 Position;
        public Vector2 Facing;
        public bool HasFacing;
        public string Label => Kind + " in " + Room + " #" + Index;
    }

    public sealed class MapSpec
    {
        public string Path;
        public readonly List<string> Problems = new List<string>();   // what is wrong with the map as a document
        public Vector3? StartPos;
        public Vector2? StartFacing;
        public readonly List<MapRoom> Rooms = new List<MapRoom>();
        public readonly List<MapDoor> Doors = new List<MapDoor>();
        public readonly List<MapActor> Enemies = new List<MapActor>();
        public readonly List<MapActor> Pickups = new List<MapActor>();
        public bool HasGallery;
        public readonly List<string> FoldedIntoGallery = new List<string>();   // rooms that are the deck itself (1.1)
        public float DeckY;
        public readonly List<Poly> Deck = new List<Poly>();
        public readonly List<Vector2> Rail = new List<Vector2>();
        public bool HasStage;
        public float StageTopY;
        public Poly Stage;
        public Vector3? Throne;
        public float XMin = float.MaxValue, XMax = float.MinValue, ZMin = float.MaxValue, ZMax = float.MinValue;
        public float FloorMin = float.MaxValue, FloorMax = float.MinValue;

        public static readonly string[] Kinds =
        {
            "entry hall", "armory", "guard room", "prison", "kitchen", "library", "alchemist's study", "forge",
            "treasure vault", "dining hall", "throne room", "crypt", "storeroom", "shrine", "torture chamber"
        };
        public static readonly string[] States = { "decayed", "looted", "stocked", "untouched" };

        public int GalleryNode => Rooms.Count;

        public static string NormKind(string k)
        {
            if (k == null) return "";
            var s = k.Trim().ToLowerInvariant().Replace('’', '\'').Replace('_', ' ').Replace('-', ' ');
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            switch (s)
            {
                case "armoury": return "armory";
                case "guardroom": return "guard room";
                case "vault": return "treasure vault";
                case "alchemists study": case "alchemist study": case "alchemist's lab": return "alchemist's study";
                case "torture room": return "torture chamber";
                case "store room": return "storeroom";
                case "great hall": case "feast hall": case "banquet hall": return "dining hall";
                // the mansion brief's names (brief-v2-mansion.md, 2026-10-14): each room keeps its v2 role
                case "foyer": case "front hall": return "entry hall";
                case "gun room": return "armory";
                case "watch room": case "watchman's post": return "guard room";
                case "cellar cells": case "cells": case "cellar": return "prison";
                case "laboratory": case "lab": case "master's laboratory": return "alchemist's study";
                case "boiler room": return "forge";
                case "strong room": case "strongroom": return "treasure vault";
                case "ballroom": return "throne room";
                case "chapel": return "shrine";
                case "butcher's room": case "butchers room": case "butcher room": return "torture chamber";
                default: return s;
            }
        }

        /// <summary>An enemy's kind, lower case; the mansion's master (and "the master") is the warlord.</summary>
        public static string NormEnemy(string k)
        {
            var s = (k ?? "").Trim().ToLowerInvariant();
            return s == "master" || s == "the master" ? "warlord" : s;
        }

        public MapRoom RoomOfKind(string kind) => Rooms.FirstOrDefault(r => r.Kind == kind);

        public MapRoom Room(string name)
        {
            if (name == null) return null;
            return Rooms.FirstOrDefault(r => r.Name == name) ??
                   Rooms.FirstOrDefault(r => string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public bool InDeck(float x, float z)
        {
            if (!HasGallery) return false;
            foreach (var d in Deck) if (d.Contains(x, z)) return true;
            return false;
        }

        public float DistToDeck(float x, float z)
        {
            if (!HasGallery || Deck.Count == 0) return float.MaxValue;
            float best = float.MaxValue;
            foreach (var d in Deck) best = Mathf.Min(best, d.Distance(x, z));
            return best;
        }

        /// <summary>The node a point belongs to: the gallery (inside the deck outline, near deck_y), else the room whose
        /// outline holds it and whose floor_y band holds its height (the nearest floor_y wins), else -1.</summary>
        public int NodeAt(Vector3 p)
        {
            if (HasGallery && Mathf.Abs(p.y - DeckY) <= Tune.GalleryBand && InDeck(p.x, p.z)) return GalleryNode;
            int best = -1; float bd = float.MaxValue;
            foreach (var r in Rooms)
            {
                float dy = p.y - r.FloorY;
                if (dy < Tune.RoomBandLow || dy > Tune.RoomBandHigh) continue;
                if (!r.ContainsXZ(p.x, p.z)) continue;
                float d = Mathf.Abs(dy);
                if (d < bd) { bd = d; best = r.Index; }
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------- reading

        public static MapSpec Load(string path)
        {
            var root = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (root == null) throw new FormatException("map.json: the top level is not an object");
            var m = new MapSpec { Path = System.IO.Path.GetFullPath(path).Replace('\\', '/') };

            if (Get(root, "start") is Dictionary<string, object> st)
            {
                m.StartPos = V3(Get(st, "position"));
                m.StartFacing = V2(Get(st, "facing"));
            }
            if (m.StartPos == null) m.Problems.Add("no start position");

            if (Get(root, "rooms") is List<object> rooms)
                foreach (var o in rooms)
                {
                    if (!(o is Dictionary<string, object> d)) continue;
                    var r = new MapRoom
                    {
                        Index = m.Rooms.Count, Name = Str(Get(d, "name")) ?? ("room " + m.Rooms.Count),
                        RawKind = Str(Get(d, "kind")), State = (Str(Get(d, "state")) ?? "").Trim().ToLowerInvariant(),
                        FloorY = F(Get(d, "floor_y")) ?? 0f
                    };
                    r.Kind = NormKind(r.RawKind);
                    if (F(Get(d, "floor_y")) == null) m.Problems.Add(r.Name + ": no floor_y (0 used)");
                    foreach (var poly in Polys(Get(d, "floor")))
                    {
                        r.Floor.Add(poly);
                        r.XMin = Mathf.Min(r.XMin, poly.XMin); r.XMax = Mathf.Max(r.XMax, poly.XMax);
                        r.ZMin = Mathf.Min(r.ZMin, poly.ZMin); r.ZMax = Mathf.Max(r.ZMax, poly.ZMax);
                    }
                    if (r.Floor.Count == 0) { m.Problems.Add(r.Name + ": no floor outline"); r.XMin = r.XMax = r.ZMin = r.ZMax = 0f; }
                    if (m.Rooms.Any(x => x.Name == r.Name)) m.Problems.Add("room name used twice: " + r.Name);
                    m.Rooms.Add(r);
                }
            if (m.Rooms.Count == 0) throw new FormatException("map.json lists no rooms");
            foreach (var r in m.Rooms)
            {
                if (r.Floor.Count == 0) continue;
                m.XMin = Mathf.Min(m.XMin, r.XMin); m.XMax = Mathf.Max(m.XMax, r.XMax);
                m.ZMin = Mathf.Min(m.ZMin, r.ZMin); m.ZMax = Mathf.Max(m.ZMax, r.ZMax);
                m.FloorMin = Mathf.Min(m.FloorMin, r.FloorY); m.FloorMax = Mathf.Max(m.FloorMax, r.FloorY);
            }

            if (Get(root, "doors") is List<object> doors)
                foreach (var o in doors)
                {
                    if (!(o is Dictionary<string, object> d)) continue;
                    var names = Get(d, "rooms") as List<object>;
                    var door = new MapDoor
                    {
                        Index = m.Doors.Count,
                        A = names != null && names.Count > 0 ? Str(names[0]) : null,
                        B = names != null && names.Count > 1 ? Str(names[1]) : null,
                        State = (Str(Get(d, "state")) ?? "").Trim().ToLowerInvariant(),
                        Position = V3(Get(d, "position")) ?? Vector3.zero,
                        Width = F(Get(d, "width")) ?? 1.5f
                    };
                    door.RA = m.Room(door.A); door.RB = m.Room(door.B);
                    if (door.RA == null || door.RB == null) m.Problems.Add("door " + door.Index + " names a room the map does not list: " + door.A + " / " + door.B);
                    if (V3(Get(d, "position")) == null) m.Problems.Add("door " + door.Index + " (" + door.A + " - " + door.B + ") has no position");
                    if (door.State != "open" && door.State != "shut" && door.State != "hidden") m.Problems.Add("door " + door.Index + ": unknown state '" + door.State + "'");
                    m.Doors.Add(door);
                }

            if (Get(root, "gallery") is Dictionary<string, object> g)
            {
                var dy = F(Get(g, "deck_y"));
                m.Deck.AddRange(Polys(Get(g, "deck")));
                if (Get(g, "rail") is List<object> rail)
                    foreach (var p in rail) { var v = V2(p); if (v != null) m.Rail.Add(v.Value); }
                m.HasGallery = dy != null && m.Deck.Count > 0;
                m.DeckY = dy ?? 0f;
                if (!m.HasGallery) m.Problems.Add("gallery: no deck_y or no deck outline");
                if (m.Rail.Count < 2) m.Problems.Add("gallery: the rail has fewer than two points");
            }
            else m.Problems.Add("no gallery");

            if (Get(root, "stage") is Dictionary<string, object> s)
            {
                var ty = F(Get(s, "top_y"));
                var polys = Polys(Get(s, "outline"));
                if (ty != null && polys.Count > 0)
                {
                    m.HasStage = true; m.StageTopY = ty.Value;
                    m.Stage = polys.OrderByDescending(p => p.Area).First();
                    if (polys.Count > 1) m.Problems.Add("stage: " + polys.Count + " outlines; the largest is used");
                }
                else m.Problems.Add("stage: no top_y or no outline");
            }
            else m.Problems.Add("no stage");

            m.Throne = V3(Get(root, "throne")) ?? V3(Get(root, "chair"));   // the mansion's: the master's chair
            if (m.Throne == null) m.Problems.Add("no throne position");

            if (Get(root, "enemies") is List<object> en)
                foreach (var o in en)
                {
                    if (!(o is Dictionary<string, object> d)) continue;
                    var f = V2(Get(d, "facing"));
                    m.Enemies.Add(new MapActor
                    {
                        Index = m.Enemies.Count, Kind = NormEnemy(Str(Get(d, "kind"))), Room = Str(Get(d, "room")),
                        Position = V3(Get(d, "position")) ?? Vector3.zero, Facing = f ?? Vector2.zero, HasFacing = f != null
                    });
                }
            if (Get(root, "pickups") is List<object> pk)
                foreach (var o in pk)
                {
                    if (!(o is Dictionary<string, object> d)) continue;
                    m.Pickups.Add(new MapActor
                    {
                        Index = m.Pickups.Count, Kind = (Str(Get(d, "kind")) ?? "").Trim().ToLowerInvariant().Replace('_', ' '),
                        Room = Str(Get(d, "room")), Position = V3(Get(d, "position")) ?? Vector3.zero
                    });
                }
            m.FoldGalleryRooms();
            return m;
        }

        /// <summary>Kit 1.1: a room that is the gallery deck itself (an "extra" room at deck height whose outline lies
        /// mostly inside the deck outline) is folded into the gallery, which the kit counts as the dining hall's. It
        /// leaves the room list, doors naming it name the dining hall, and markers in it stand in the dining hall.</summary>
        void FoldGalleryRooms()
        {
            var hall = RoomOfKind("dining hall");
            if (!HasGallery || hall == null) return;
            var fold = Rooms.Where(r => r.Kind == "extra" && Mathf.Abs(r.FloorY - DeckY) <= Tune.GalleryBand &&
                                        DeckShare(r) >= Tune.GalleryRoomOverlap).ToList();
            foreach (var g in fold)
            {
                Rooms.Remove(g);
                FoldedIntoGallery.Add(g.Name);
                foreach (var d in Doors)
                {
                    if (d.RA == g) { d.RA = hall; d.A = hall.Name; }
                    if (d.RB == g) { d.RB = hall; d.B = hall.Name; }
                    if (d.RA == hall && d.RB == hall) Problems.Add("door " + d.Index + " joins " + g.Name + " to the dining hall, which the kit counts as one");
                }
                foreach (var a in Enemies.Concat(Pickups))
                    if (string.Equals(a.Room?.Trim(), g.Name, StringComparison.OrdinalIgnoreCase)) a.Room = hall.Name;
            }
            for (int i = 0; i < Rooms.Count; i++) Rooms[i].Index = i;
        }

        /// <summary>The share of a room's outline (0.5 m grid) that lies inside the deck outline.</summary>
        float DeckShare(MapRoom r)
        {
            const float step = 0.5f;
            int inRoom = 0, onDeck = 0;
            for (float x = r.XMin + step * 0.5f; x < r.XMax; x += step)
            for (float z = r.ZMin + step * 0.5f; z < r.ZMax; z += step)
            {
                if (!r.ContainsXZ(x, z)) continue;
                inRoom++;
                if (InDeck(x, z)) onDeck++;
            }
            return inRoom > 0 ? onDeck / (float)inRoom : 0f;
        }

        static object Get(Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) ? v : null;
        static string Str(object o) => o == null ? null : Convert.ToString(o, CultureInfo.InvariantCulture);

        static float? F(object o)
        {
            if (o is double d) return (float)d;
            if (o is string s && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return f;
            return null;
        }

        static Vector3? V3(object o)
        {
            if (!(o is List<object> l) || l.Count < 3) return null;
            var x = F(l[0]); var y = F(l[1]); var z = F(l[2]);
            if (x == null || y == null || z == null) return null;
            return new Vector3(x.Value, y.Value, z.Value);
        }

        static Vector2? V2(object o)
        {
            if (!(o is List<object> l) || l.Count < 2) return null;
            var x = F(l[0]); var z = F(l[l.Count == 3 ? 2 : 1]);
            if (x == null || z == null) return null;
            return new Vector2(x.Value, z.Value);
        }

        /// <summary>One or more outlines: [[[x,z],...], ...], or leniently a single [[x,z],...].</summary>
        static List<Poly> Polys(object o)
        {
            var res = new List<Poly>();
            if (!(o is List<object> l) || l.Count == 0) return res;
            bool single = l[0] is List<object> l0 && l0.Count > 0 && !(l0[0] is List<object>);
            var lists = single ? new List<object> { l } : l;
            foreach (var po in lists)
            {
                if (!(po is List<object> pts)) continue;
                var vs = new List<Vector2>();
                foreach (var p in pts) { var v = V2(p); if (v != null) vs.Add(v.Value); }
                if (vs.Count >= 2 && (vs[0] - vs[vs.Count - 1]).sqrMagnitude < 1e-6f) vs.RemoveAt(vs.Count - 1);
                if (vs.Count >= 3) res.Add(new Poly(vs.ToArray()));
            }
            return res;
        }
    }
}
