using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Amb = IronCitadel.Rooms.Editor.IronCitadelAmbience;
using Object = UnityEngine.Object;

namespace IronCitadelV2.Editor
{
    public static partial class IronCitadelV2Builder
    {
        // ------------------------------------------------------------------ light

        /// <summary>A floor point counts as lit when the point and spot lights give it this much at 0.5 m over the floor:
        /// intensity x URP's smooth range fall-off / d^2, with a clear line from the light. Under the scene grade
        /// (+2.4 EV) that reads as a floor you can see to walk on.</summary>
        const float LitE = 0.08f;
        const string GradeName = "IronCitadelV2 Grade";
        const string GradePath = AmbienceDir + "/grade.asset";
        const float GradePriority = 1f, RoomPriority = 10f, RoomBlend = 3f;
        // the grade's own values, which a room's look replaces when it overrides the same parameter
        const float GradeSaturation = 8f, GradeContrast = 8f, GradeVignette = 0.28f;

        enum Lightness { Dark, Dim, Normal, Bright }

        /// <summary>The brief's light per kind: the dining hall and the throne room tall and bright; the guard room
        /// dim; the prison, the crypt and the torture chamber dark; the rest as they come.</summary>
        static Lightness ClassOf(string kind)
        {
            switch (kind)
            {
                case "D": case "T": return Lightness.Bright;
                case "G": return Lightness.Dim;
                case "P": case "C": case "X": return Lightness.Dark;
                default: return Lightness.Normal;
            }
        }

        /// <summary>The share of a room's reachable floor (2 m points) that must be lit.</summary>
        static float LitTarget(Lightness b) => b == Lightness.Bright ? 0.7f : b == Lightness.Normal ? 0.45f : b == Lightness.Dim ? 0.35f : 0.25f;
        static int MaxFills(Lightness b) => b == Lightness.Bright ? 16 : b == Lightness.Normal ? 10 : b == Lightness.Dim ? 6 : 4;

        static Color FillColour(string mood)
        {
            switch (mood)
            {
                case "crypt": return new Color(0.70f, 0.80f, 1.00f);
                case "prison": return new Color(0.78f, 0.88f, 0.78f);
                case "forge": return new Color(1.00f, 0.70f, 0.45f);
                case "library": return new Color(1.00f, 0.84f, 0.60f);
                case "regal": return new Color(1.00f, 0.88f, 0.62f);
                case "hall": return new Color(1.00f, 0.86f, 0.68f);
                case "guard": return new Color(0.90f, 0.93f, 1.00f);
                case "hearth": return new Color(1.00f, 0.80f, 0.60f);
                case "alchemy": return new Color(0.78f, 1.00f, 0.70f);
                case "shrine": return new Color(0.86f, 0.76f, 1.00f);
                case "torture": return new Color(1.00f, 0.62f, 0.55f);
                case "grotto": return new Color(0.60f, 1.00f, 0.92f);
                case "storeroom": return new Color(1.00f, 0.90f, 0.74f);
                default: return new Color(1.00f, 0.88f, 0.72f);
            }
        }

        /// <summary>Check each room's own lights against how bright its kind should be, and add soft fill where it
        /// falls short. The room's reachable floor is taken every 2 m (floor.json's samples); each point's light is
        /// summed from every enabled point and spot light in reach (rooms' own, a neighbour's through a doorway). While
        /// the lit share is under the kind's target, a shadowless point light in the mood's colour goes over the unlit
        /// point that would light the most others (one at a time, at most a few per room), high enough to read as
        /// light from above: clamp(lid x 0.45, 2.2, 6) m, range clamp(lid, 6, 12) m, bright enough to light floor
        /// half its range away. The rooms' own lights are left exactly as they are.</summary>
        static void LightRooms(Ctx c)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var lightsJ = new JArray();
            var all = c.root.GetComponentsInChildren<Light>(false)
                .Where(l => l.enabled && l.intensity > 0f && (l.type == LightType.Point || l.type == LightType.Spot)).ToList();
            int fillsAll = 0;
            foreach (var room in c.level.rooms)
            {
                var pick = c.picks[room.id];
                var go = c.rooms[room.id];
                var mood = MoodOf(pick, room);
                var cls = ClassOf(room.kind);
                float lid = c.Lid(room.id);
                var own = go.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point || l.type == LightType.Spot).ToList();
                var on = own.Where(l => l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0f).ToList();
                float ownSum = on.Sum(l => l.intensity);

                var cellMin = new Vector3(room.c * c.level.tile, 0f, -(room.r + 1) * c.level.tile);
                var cellBox = new Bounds(cellMin + new Vector3(c.level.tile / 2f, 0f, c.level.tile / 2f), new Vector3(c.level.tile, 200f, c.level.tile));
                var near = all.Where(l => l != null && cellBox.SqrDistance(l.transform.position) <= l.range * l.range).ToList();
                var src = c.floor.samples.Where(s => s.room == room.id && s.reach && (s.i & 1) == 0 && (s.j & 1) == 0).ToList();
                var pts = src.Select(s => new Vector3(c.floor.ox + s.i + 0.5f, s.y, c.floor.oz + s.j + 0.5f)).ToList();
                var E = new float[pts.Count];
                for (int k = 0; k < pts.Count; k++)
                {
                    var q = pts[k] + Vector3.up * 0.5f;
                    foreach (var l in near) E[k] += Contribution(l.transform.position, l.intensity, l.range, l.type == LightType.Spot ? l.transform.forward : (Vector3?)null, l.spotAngle, q);
                }
                float target = LitTarget(cls);
                float before = pts.Count > 0 ? E.Count(e => e >= LitE) / (float)pts.Count : 1f;

                // fills: over an unlit point, under whatever roofs it (its floor.json headroom), never in a solid;
                // each bright enough to light the floor half its range away at the height it ends up at
                float R = Mathf.Clamp(lid, 6f, 12f), H = Mathf.Clamp(lid * 0.45f, 2.2f, 6f), rLit = 0.5f * R;
                float IntensityAt(float hy)
                {
                    float d2 = rLit * rLit + (hy - 0.5f) * (hy - 0.5f);
                    float fall = 1f - (d2 / (R * R)) * (d2 / (R * R));
                    return LitE * d2 / Mathf.Max(0.05f, fall * fall);
                }
                float I = IntensityAt(H);
                var colour = FillColour(mood?.Name);
                if (pick.stateKind == "abandoned") colour = Color.Lerp(colour, new Color(0.8f, 0.85f, 1f), 0.25f);
                var fills = new List<(Vector3 pos, float intensity)>();
                Transform fillT = null;
                while (pts.Count > 0 && E.Count(e => e >= LitE) < target * pts.Count && fills.Count < MaxFills(cls))
                {
                    int best = -1, bestScore = 0; Vector3 bestPos = default; float bestI = 0f;
                    for (int k = 0; k < pts.Count; k++)
                    {
                        if (E[k] >= LitE) continue;
                        var p = pts[k];
                        float hy = Mathf.Min(H, src[k].clear - 0.5f, lid - 0.6f - p.y);
                        if (hy < 1.9f) continue;
                        var lp = new Vector3(p.x, p.y + hy, p.z);
                        if (Physics.CheckSphere(lp, 0.2f, AllLayers, QueryTriggerInteraction.Ignore)) continue;
                        float Ik = IntensityAt(hy);
                        int score = 0;
                        for (int m = 0; m < pts.Count; m++)
                        {
                            if (E[m] >= LitE || V2Model.Flat(pts[m] - p).sqrMagnitude > R * R) continue;
                            if (E[m] + Contribution(lp, Ik, R, null, 0f, pts[m] + Vector3.up * 0.5f) >= LitE) score++;
                        }
                        if (score > bestScore) { best = k; bestScore = score; bestPos = lp; bestI = Ik; }
                    }
                    if (best < 0 || bestScore < 3) break;
                    if (fillT == null) { fillT = Child(c.lightT, room.id); }
                    var lgo = new GameObject($"Fill {fills.Count + 1}");
                    lgo.transform.SetParent(fillT, false);
                    lgo.transform.position = bestPos;
                    var lt = lgo.AddComponent<Light>();
                    lt.type = LightType.Point; lt.color = colour; lt.intensity = bestI; lt.range = R; lt.shadows = LightShadows.None;
                    fills.Add((bestPos, bestI));
                    for (int m = 0; m < pts.Count; m++) E[m] += Contribution(bestPos, bestI, R, null, 0f, pts[m] + Vector3.up * 0.5f);
                }
                fillsAll += fills.Count;
                float after = pts.Count > 0 ? E.Count(e => e >= LitE) / (float)pts.Count : 1f;
                string verdict = after + 1e-4f >= target ? "meets" : "short of";
                c.Log($"light {room.id} ({KindWord(room.kind)}, {cls.ToString().ToLowerInvariant()}, {pick.stateKind}): {on.Count}/{own.Count} own lights on (sum {F(ownSum, "0.0")}, " +
                      $"{on.Count(l => l.shadows != LightShadows.None)} with shadows), {near.Count} in reach; {pts.Count} floor points, lit {before:P0} before, " +
                      $"{fills.Count} fill(s){(fills.Count > 0 ? $" of {F(fills.Min(f => f.intensity), "0.0")}..{F(fills.Max(f => f.intensity), "0.0")} at {F(fills.Min(f => f.pos.y), "0.0")}..{F(fills.Max(f => f.pos.y), "0.0")} m" : "")}, range {F(R, "0")} m -> {after:P0} ({verdict} the {target:P0} target)");
                if (after + 1e-4f < target && fills.Count >= MaxFills(cls))
                    c.Log($"  {room.id}: left at {after:P0} (the fill cap for a {cls.ToString().ToLowerInvariant()} room is {MaxFills(cls)})");
                lightsJ.Add(new JObject
                {
                    ["room"] = room.id, ["class"] = cls.ToString().ToLowerInvariant(), ["mood"] = mood?.Name, ["state_kind"] = pick.stateKind,
                    ["own_lights"] = own.Count, ["own_on"] = on.Count, ["own_intensity_sum"] = Math.Round(ownSum, 2),
                    ["floor_points"] = pts.Count, ["lit_before"] = Math.Round(before, 3), ["fills"] = fills.Count,
                    ["fill_intensity_max"] = Math.Round(fills.Count > 0 ? fills.Max(f => f.intensity) : I, 2), ["fill_range"] = R,
                    ["lit_after"] = Math.Round(after, 3), ["target"] = target
                });
            }
            c.built["lighting"] = new JObject { ["lit_when"] = $"intensity x smooth fall-off / d^2 >= {LitE} at 0.5 m over the floor, clear line", ["rooms"] = lightsJ };
            c.Log($"light: {fillsAll} fill light(s) added under IronCitadel/Lighting in {sw.Elapsed.TotalSeconds:0.0} s");
        }

        /// <summary>One light's share at <paramref name="q"/>: URP's smooth range window over 1/d^2, a spot's cone,
        /// and nothing when a collider stands between (the last 0.4 m before the point are not tested, so the floor
        /// and the thing the point sits on do not shade it).</summary>
        static float Contribution(Vector3 lp, float intensity, float range, Vector3? spotDir, float spotAngle, Vector3 q)
        {
            var d = q - lp;
            float dd = d.sqrMagnitude, rr = range * range;
            if (dd >= rr) return 0f;
            if (spotDir.HasValue && Vector3.Angle(spotDir.Value, d) > spotAngle / 2f) return 0f;
            float len = Mathf.Sqrt(dd);
            if (len > 0.45f && Physics.Raycast(lp, d / len, len - 0.4f, AllLayers, QueryTriggerInteraction.Ignore)) return 0f;
            float f = 1f - (dd / rr) * (dd / rr);
            return intensity * f * f / Mathf.Max(dd, 0.25f);
        }

        static Amb.Mood MoodOf(V2Pick pick, V2Room room)
        {
            var m = Amb.MoodFor(pick.theme);
            if (m != null) return m;
            foreach (var t in room.themes) { m = Amb.MoodFor(t); if (m != null) return m; }
            return Amb.MoodFor(KindWord(room.kind));
        }

        static string KindWord(string kind)
        {
            switch (kind)
            {
                case "E": return "entry hall"; case "A": return "armory"; case "D": return "dining hall"; case "L": return "library";
                case "H": return "shrine"; case "T": return "throne room"; case "F": return "forge"; case "R": return "storeroom";
                case "K": return "kitchen"; case "S": return "study"; case "C": return "crypt"; case "V": return "treasury";
                case "G": return "guard room"; case "P": return "prison"; case "X": return "torture chamber";
                default: return kind;
            }
        }

        // ------------------------------------------------------------------ grade

        /// <summary>One look: v1's mood for the room's theme, moved by its state and by how bright its kind is. A
        /// parameter a room overrides replaces the scene grade's, so the moves start from the grade's value when the
        /// mood leaves it alone. Brightness rides in the colour filter (a multiplier), never in post-exposure, which
        /// would replace the grade's +2.4 EV.</summary>
        sealed class Look
        {
            public string name, mood, lut;
            public float lutAmount = 1f;
            public float? temperature, tint, saturation, contrast, vignette, bloom;
            public Color? filter, vignetteColor, bloomTint;

            public string Signature()
            {
                string N(float? v) => v.HasValue ? v.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";
                string Cc(Color? v) => v.HasValue ? $"{N(v.Value.r)},{N(v.Value.g)},{N(v.Value.b)}" : "-";
                return $"v2look1|{mood}|{lut}|{N(lutAmount)}|{N(temperature)}|{N(tint)}|{N(saturation)}|{N(contrast)}|{N(vignette)}|{N(bloom)}|{Cc(filter)}|{Cc(vignetteColor)}|{Cc(bloomTint)}";
            }
        }

        static Look LookFor(Amb.Mood m, string stateKind, Lightness cls)
        {
            var k = new Look
            {
                mood = m.Name, lut = m.Lut, lutAmount = m.LutAmount, temperature = m.Temperature, tint = m.Tint,
                saturation = m.Saturation, contrast = m.Contrast, vignette = m.Vignette, bloom = m.Bloom,
                filter = m.Filter, vignetteColor = m.VignetteColor, bloomTint = m.BloomTint
            };
            switch (stateKind)
            {
                case "abandoned":   // left to decay: colder, greyer, closer
                    k.saturation = (k.saturation ?? GradeSaturation) - 8f;
                    k.vignette = (k.vignette ?? GradeVignette) + 0.04f;
                    k.temperature = (k.temperature ?? 0f) - 6f;
                    break;
                case "looted":      // stripped and wrecked: harder, a little greyer
                    k.contrast = (k.contrast ?? GradeContrast) + 4f;
                    k.saturation = (k.saturation ?? GradeSaturation) - 4f;
                    k.vignette = (k.vignette ?? GradeVignette) + 0.02f;
                    break;
            }
            float mul = cls == Lightness.Bright ? 1.2f : cls == Lightness.Dim ? 0.95f : cls == Lightness.Dark ? 0.9f : 1f;
            if (Mathf.Abs(mul - 1f) > 1e-3f)
            {
                var f = k.filter ?? Color.white;
                k.filter = new Color(f.r * mul, f.g * mul, f.b * mul, 1f);
            }
            k.name = $"{m.Name}-{stateKind ?? "use"}-{cls.ToString().ToLowerInvariant()}";
            return k;
        }

        /// <summary>The grade and one local volume per room. The scene grade (a global volume, priority 1) is v1's:
        /// ACES, +2.4 EV, contrast and saturation 8, bloom 0.6, vignette 0.28; its profile is made once under
        /// <see cref="AmbienceDir"/> and never rebuilt (tune it in the Inspector). Each room's look is its own profile
        /// there too, named mood-state-light and rebuilt only when its values change (the signature rides in the
        /// asset's import settings), so a re-run with the same inputs leaves every asset as it was. A room's volume is
        /// a trigger box over its cell, inset by the 3 m blend so neighbours never overlap, from 0.5 m under the floor
        /// to 0.5 m over the lid, priority 10. v1's ambience tool and profiles are not touched.</summary>
        static void Ambience(Ctx c)
        {
            EnsureFolder(AmbienceDir);
            var grade = EnsureGrade(c);
            var gv = new GameObject(GradeName);
            gv.transform.SetParent(c.lightT, false);
            gv.layer = 0;
            var vol = gv.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = GradePriority;
            vol.sharedProfile = grade;

            var looksJ = new JArray();
            var made = new Dictionary<string, VolumeProfile>(StringComparer.Ordinal);
            int rebuilt = 0;
            float side = c.level.tile - 2f * RoomBlend;
            foreach (var room in c.level.rooms)
            {
                var pick = c.picks[room.id];
                var mood = MoodOf(pick, room);
                if (mood == null) { c.Log($"grade {room.id}: theme {pick.theme} has no mood; the scene grade alone"); continue; }
                var look = LookFor(mood, pick.stateKind, ClassOf(room.kind));
                if (!made.TryGetValue(look.name, out var profile))
                {
                    profile = EnsureLook(look, out bool re);
                    made[look.name] = profile;
                    if (re) rebuilt++;
                }
                float lid = Mathf.Clamp(c.Lid(room.id), 3f, 25f);
                float h = 0.5f + lid + 0.5f;
                var go = new GameObject("Ambience");
                go.transform.SetParent(c.rooms[room.id].transform, false);
                go.layer = 0;
                go.transform.localPosition = new Vector3(0f, -0.5f + h / 2f, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var box = go.AddComponent<BoxCollider>();   // before isGlobal = false, which collects the colliders
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = new Vector3(side, h, side);
                var v = go.AddComponent<Volume>();
                v.isGlobal = false;
                v.priority = RoomPriority;
                v.blendDistance = RoomBlend;
                v.weight = 1f;
                v.sharedProfile = profile;
                c.Log($"grade {room.id}: {pick.theme} -> {mood.Name} ({mood.Look}), {pick.stateKind}, {ClassOf(room.kind).ToString().ToLowerInvariant()} -> {look.name}");
                looksJ.Add(new JObject { ["room"] = room.id, ["profile"] = AmbienceDir + "/" + look.name + ".asset", ["mood"] = mood.Name, ["lut"] = look.lut });
            }
            var lighting = (JObject)c.built["lighting"] ?? new JObject();
            lighting["grade"] = new JObject { ["global"] = GradePath, ["priority"] = GradePriority, ["rooms"] = looksJ, ["room_priority"] = RoomPriority, ["blend_m"] = RoomBlend };
            c.built["lighting"] = lighting;
            c.Log($"grade: {GradeName} (global, priority {GradePriority}, {GradePath}) and {looksJ.Count} room volume(s) over {made.Count} look(s), {rebuilt} profile(s) (re)built");
        }

        static VolumeProfile EnsureGrade(Ctx c)
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GradePath);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, GradePath);
            AddTo<Tonemapping>(p).mode.Override(TonemappingMode.ACES);
            var ca = AddTo<ColorAdjustments>(p);
            ca.postExposure.Override(2.4f);
            ca.contrast.Override(GradeContrast);
            ca.saturation.Override(GradeSaturation);
            var b = AddTo<Bloom>(p);
            b.intensity.Override(0.6f);
            b.threshold.Override(0.95f);
            var v = AddTo<Vignette>(p);
            v.intensity.Override(GradeVignette);
            v.smoothness.Override(0.45f);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            c.Log("grade: made " + GradePath + " (v1's values)");
            return p;
        }

        static VolumeProfile EnsureLook(Look k, out bool rebuilt)
        {
            string path = AmbienceDir + "/" + k.name + ".asset";
            string sig = k.Signature();
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p != null && AssetImporter.GetAtPath(path)?.userData == sig) { rebuilt = false; return p; }
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(p, path);
            }
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(sub is VolumeComponent old)) continue;
                AssetDatabase.RemoveObjectFromAsset(old);
                Object.DestroyImmediate(old, true);
            }
            p.components.Clear();
            if (k.lut != null)
            {
                string lutPath = $"{Amb.LutDir}/{k.lut}_URP.png";
                var lut = AssetDatabase.LoadAssetAtPath<Texture2D>(lutPath);
                if (lut == null) throw new System.IO.FileNotFoundException($"look {k.name}: no LUT at {lutPath}");
                var cl = AddTo<ColorLookup>(p);
                cl.texture.Override(lut);
                cl.contribution.Override(Mathf.Clamp01(k.lutAmount));
            }
            if (k.temperature.HasValue || k.tint.HasValue)
            {
                var wb = AddTo<WhiteBalance>(p);
                if (k.temperature.HasValue) wb.temperature.Override(k.temperature.Value);
                if (k.tint.HasValue) wb.tint.Override(k.tint.Value);
            }
            if (k.filter.HasValue || k.saturation.HasValue || k.contrast.HasValue)
            {
                var ca = AddTo<ColorAdjustments>(p);
                if (k.filter.HasValue) ca.colorFilter.Override(k.filter.Value);
                if (k.saturation.HasValue) ca.saturation.Override(k.saturation.Value);
                if (k.contrast.HasValue) ca.contrast.Override(k.contrast.Value);
            }
            if (k.vignette.HasValue)
            {
                var v = AddTo<Vignette>(p);
                v.intensity.Override(k.vignette.Value);
                v.smoothness.Override(0.45f);
                if (k.vignetteColor.HasValue) v.color.Override(k.vignetteColor.Value);
            }
            if (k.bloom.HasValue || k.bloomTint.HasValue)
            {
                var b = AddTo<Bloom>(p);
                if (k.bloom.HasValue) b.intensity.Override(k.bloom.Value);
                if (k.bloomTint.HasValue) b.tint.Override(k.bloomTint.Value);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            var imp = AssetImporter.GetAtPath(path);
            if (imp != null)
            {
                imp.userData = sig;
                EditorUtility.SetDirty(imp);
                AssetDatabase.WriteImportSettingsIfDirty(path);
            }
            rebuilt = true;
            return p;
        }

        static T AddTo<T>(VolumeProfile p) where T : VolumeComponent
        {
            var comp = p.Add<T>(false);
            AssetDatabase.AddObjectToAsset(comp, p);
            return comp;
        }
    }
}
