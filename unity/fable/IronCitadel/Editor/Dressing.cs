using System.Collections.Generic;
using UnityEngine;

namespace IronCitadel.Editor
{
    /// <summary>Furnishing, lighting and markers for each room, in tile-local metres from the tile's south-west corner.</summary>
    public static class Dressing
    {
        // Stairs_01 rises 2.5 m over its 5 m run toward local -z (verified in captures).
        const bool StairsRiseTowardMinusZ = true;
        // URP point lights fall off fast; every light's intensity is scaled by this.
        public const float LightBoost = 2.6f;

        public static void Dress(Ctx ctx, TileDef t)
        {
            switch (t.roomId)
            {
                case "entry_hall": Entry(ctx, t); break;
                case "guard_room": Guard(ctx, t); break;
                case "prison": Prison(ctx, t); break;
                case "vault": Vault(ctx, t); break;
                case "kitchen": Kitchen(ctx, t); break;
                case "great_hall": Hall(ctx, t); break;
                case "armory": Armory(ctx, t); break;
                case "throne_room": Throne(ctx, t); break;
                case "library": Library(ctx, t); break;
                case "study": Study(ctx, t); break;
                case "forge": Forge(ctx, t); break;
            }
            Fills(ctx, t);
        }

        /// <summary>Soft, steady fill light per room so every room is walkable; the brief's light levels set the strength.</summary>
        static void Fills(Ctx ctx, TileDef t)
        {
            var warm = new Color(1f, 0.82f, 0.6f);
            var cold = new Color(0.55f, 0.62f, 0.85f);
            switch (t.roomId)
            {
                case "entry_hall": Fill(ctx, t, 22.5f, 4.3f, 22.5f, 30f, 2.6f, warm); Fill(ctx, t, 22.5f, 4.3f, 40f, 14f, 1.4f, warm); break;
                case "guard_room": Fill(ctx, t, 22.5f, 2.4f, 22.5f, 22f, 1.5f, warm); break;
                case "prison":
                    Fill(ctx, t, 22.5f, 2.2f, 14f, 18f, 0.9f, cold); Fill(ctx, t, 22.5f, 2.2f, 28f, 18f, 0.9f, cold);
                    Fill(ctx, t, 5f, 2.3f, 25f, 13f, 1.1f, warm); Fill(ctx, t, 22.5f, 2.3f, 38f, 9f, 0.7f, cold); break;
                case "vault": Fill(ctx, t, 37.5f, 2.2f, 22.5f, 14f, 1.5f, new Color(1f, 0.85f, 0.5f)); break;
                case "kitchen": Fill(ctx, t, 22.5f, 2.6f, 25f, 22f, 2.2f, new Color(1f, 0.75f, 0.5f)); Fill(ctx, t, 24f, 2.4f, 8f, 10f, 0.8f, warm); break;
                case "great_hall":
                    Fill(ctx, t, 22.5f, 8.5f, 25f, 44f, 4.5f, warm); Fill(ctx, t, 12f, 7f, 18f, 24f, 2f, warm); Fill(ctx, t, 33f, 7f, 32f, 24f, 2f, warm);
                    Fill(ctx, t, 22.5f, 4f, 2.5f, 26f, 2.2f, warm); Fill(ctx, t, 2.5f, 4f, 12f, 12f, 1.4f, warm); Fill(ctx, t, 42.5f, 4f, 12f, 12f, 1.4f, warm); break;
                case "armory": Fill(ctx, t, 22.5f, 4.4f, 20f, 30f, 2.6f, warm); Fill(ctx, t, 22.5f, 4f, 3f, 12f, 1.2f, warm); break;
                case "throne_room": Fill(ctx, t, 22.5f, 9f, 30f, 42f, 5f, new Color(1f, 0.9f, 0.75f)); Fill(ctx, t, 22.5f, 6f, 42f, 16f, 2.2f, warm); Fill(ctx, t, 27f, 3.8f, 7f, 14f, 1.4f, warm); break;
                case "library": Fill(ctx, t, 22.5f, 4.4f, 22.5f, 30f, 2.6f, warm); break;
                case "study": Fill(ctx, t, 22.5f, 4.4f, 22.5f, 30f, 2.4f, warm); break;
                case "forge": Fill(ctx, t, 22.5f, 4.4f, 22.5f, 30f, 3.2f, new Color(1f, 0.7f, 0.45f)); break;
            }
        }

        static void Fill(Ctx ctx, TileDef t, float lx, float y, float lz, float range, float intensity, Color color)
        {
            var l = PointLight(ctx, t.World(lx, y, lz), color, range, intensity, false);
            l.gameObject.name = "Fill " + t.roomId;
        }

        // ------------------------------------------------------------------ helpers

        static Transform Grp(Ctx ctx, TileDef t)
        {
            var existing = ctx.props.Find(t.roomId);
            return existing != null ? existing : Kit.Group(t.roomId, ctx.props);
        }

        public static GameObject P(Ctx ctx, TileDef t, string prefab, float lx, float lz, float yaw, float y = 0f, float scale = 1f)
        {
            var go = Kit.Place(prefab, Grp(ctx, t), t.World(lx, y, lz), yaw, scale == 1f ? (Vector3?)null : Vector3.one * scale);
            if (go != null) ctx.propCount++;
            return go;
        }

        public static GameObject PE(Ctx ctx, TileDef t, string prefab, float lx, float lz, Vector3 euler, float y = 0f, Vector3? scale = null)
        {
            var go = Kit.PlaceRot(prefab, Grp(ctx, t), t.World(lx, y, lz), Quaternion.Euler(euler), scale);
            if (go != null) ctx.propCount++;
            return go;
        }

        public static Light PointLight(Ctx ctx, Vector3 pos, Color color, float range, float intensity, bool flicker, bool shadows = false)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(ctx.lights, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity * LightBoost;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            if (flicker) go.AddComponent<FlickerLight>();
            ctx.lightCount++;
            return l;
        }

        static readonly Color Warm = new Color(1f, 0.72f, 0.42f);
        static readonly Color Fire = new Color(1f, 0.55f, 0.25f);
        static readonly Color Candle = new Color(1f, 0.8f, 0.5f);

        static float Yaw(Dir d) => d == Dir.N ? 0f : d == Dir.E ? 90f : d == Dir.S ? 180f : 270f;

        /// <summary>A wall torch; facing is the direction it looks out from the wall (lx, lz sits just off the wall face).</summary>
        public static void Torch(Ctx ctx, TileDef t, float lx, float lz, Dir facing, float y = 2.4f, float intensity = 2.0f, float range = 11f)
        {
            var f = Layout.Vec(facing);
            P(ctx, t, Kit.TorchWall, lx, lz, Yaw(facing), y);
            var flame = Kit.PlaceRot(Kit.FxFireSmall, Grp(ctx, t), t.World(lx, y + 0.32f, lz) + f * 0.05f, Quaternion.identity, Vector3.one * 0.45f);
            PointLight(ctx, t.World(lx, y + 0.55f, lz) + f * 0.45f, Warm, range, intensity, true);
        }

        public static void Brazier(Ctx ctx, TileDef t, float lx, float lz, bool big = false, float intensity = 3.2f)
        {
            if (big)
            {
                P(ctx, t, Kit.BrazierBig, lx, lz, 0f);
                Kit.PlaceRot(Kit.FxFire, Grp(ctx, t), t.World(lx, 1.35f, lz), Quaternion.identity, Vector3.one * 1.1f);
                PointLight(ctx, t.World(lx, 2.3f, lz), Fire, 16f, intensity + 0.8f, true);
            }
            else
            {
                P(ctx, t, Kit.Brazier, lx, lz, 0f);
                Kit.PlaceRot(Kit.FxFire, Grp(ctx, t), t.World(lx, 0.55f, lz), Quaternion.identity, Vector3.one * 0.8f);
                PointLight(ctx, t.World(lx, 1.5f, lz), Fire, 13f, intensity, true);
            }
        }

        public static void CandleStand(Ctx ctx, TileDef t, float lx, float lz)
        {
            P(ctx, t, Kit.CandleStand, lx, lz, ctx.Rand(0f, 360f));
            PointLight(ctx, t.World(lx, 1.9f, lz), Candle, 6f, 1.1f, true);
        }

        public static void Candles(Ctx ctx, TileDef t, float lx, float lz, float y, string prefab = null)
        {
            P(ctx, t, prefab ?? Kit.Candles3, lx, lz, ctx.Rand(0f, 360f), y);
            PointLight(ctx, t.World(lx, y + 0.5f, lz), Candle, 4.5f, 0.8f, true);
        }

        /// <summary>Candle chandelier hung from a ceiling at ceilingY on chains.</summary>
        public static void Chandelier(Ctx ctx, TileDef t, float lx, float lz, float ceilingY, int chains, float intensity = 3.5f, float range = 18f)
        {
            float y = ceilingY;
            for (int i = 0; i < chains; i++) { P(ctx, t, Kit.Chain, lx, lz, 0f, y); y -= 2.5f; }
            P(ctx, t, Kit.Chandelier, lx, lz, ctx.Rand(0f, 360f), y);
            PointLight(ctx, t.World(lx, y - 0.9f, lz), Candle, range, intensity, true);
        }

        public static void Banner(Ctx ctx, TileDef t, string prefab, float lx, float lz, Dir facing, float y)
        {
            P(ctx, t, prefab, lx, lz, Yaw(facing), y);
        }

        /// <summary>Stacked 5 m square pillars from the floor to height.</summary>
        public static void Pillar(Ctx ctx, TileDef t, float lx, float lz, float height)
        {
            for (float y = 0f; y < height - 0.01f; y += 5f) P(ctx, t, Kit.Pillar5, lx, lz, 0f, y);
        }

        /// <summary>Invisible ramp collider from a bottom edge centre to a top edge centre so the controller glides up stairs.</summary>
        public static void Ramp(Ctx ctx, Vector3 bottom, Vector3 top, float width)
        {
            var go = new GameObject("StairRamp");
            go.transform.SetParent(ctx.structure, false);
            var dir = top - bottom;
            go.transform.position = (bottom + top) * 0.5f + Vector3.up * 0.05f;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, 0.25f, dir.magnitude + 0.4f);
            bc.center = new Vector3(0f, -0.125f, 0f);
        }

        static void DisableColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        }

        public static void Marker(Ctx ctx, TileDef t, float lx, float lz, float yaw, string label, float y = 0f)
        {
            var root = new GameObject("Defender " + label);
            root.transform.SetParent(ctx.markers, false);
            root.transform.SetPositionAndRotation(t.World(lx, y, lz), Quaternion.Euler(0f, yaw, 0f));
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = ctx.matGuard;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.5f, 0.42f);
            nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.5f);
            nose.GetComponent<Renderer>().sharedMaterial = ctx.matDark;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            Label(ctx, root.transform, label, 2.15f, new Color(1f, 0.55f, 0.45f));
        }

        public static void Pickup(Ctx ctx, TileDef t, float lx, float lz, string label, float y = 0f)
        {
            var root = new GameObject("Pickup " + label);
            root.transform.SetParent(ctx.markers, false);
            root.transform.position = t.World(lx, y, lz);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            orb.transform.localScale = Vector3.one * 0.5f;
            orb.GetComponent<Renderer>().sharedMaterial = ctx.matGold;
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            Kit.PlaceRot(Kit.FxGold, root.transform, root.transform.position + Vector3.up * 1.3f, Quaternion.identity);
            PointLight(ctx, root.transform.position + Vector3.up * 1.6f, new Color(1f, 0.85f, 0.4f), 7f, 1.6f, false);
            Label(ctx, root.transform, label, 1.9f, new Color(1f, 0.9f, 0.5f));
        }

        static void Label(Ctx ctx, Transform parent, string text, float y, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = ctx.font;
            tm.fontSize = 48;
            tm.characterSize = 0.09f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            if (ctx.matLabel != null) mr.sharedMaterial = ctx.matLabel;
            else if (ctx.font != null) mr.sharedMaterial = ctx.font.material;
            var sync = go.AddComponent<LabelFontSync>();
            sync.font = ctx.font;
            go.AddComponent<Billboard>();
        }

        /// <summary>Places count markers from the list of (lx, lz, yaw) candidates.</summary>
        static void Defenders(Ctx ctx, TileDef t, string label, params float[] xzy)
        {
            int count = ctx.defenderCounts.TryGetValue(t.roomId, out var n) ? n : 0;
            for (int k = 0; k < count && k * 3 + 2 < xzy.Length; k++)
                Marker(ctx, t, xzy[k * 3], xzy[k * 3 + 1], xzy[k * 3 + 2], label);
        }

        /// <summary>A weapon rack with three weapons standing in it (the rack prefab itself is bare).</summary>
        static void StockedRack(Ctx ctx, TileDef t, float lx, float lz, float yaw)
        {
            P(ctx, t, Kit.WeaponRack, lx, lz, yaw);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            string[] set = { Kit.Spear, Kit.Sword, Kit.Axe, Kit.Halberd, Kit.GreatSword, Kit.SwordLarge, Kit.AxeLarge };
            for (int k = -1; k <= 1; k++)
            {
                var off = rot * new Vector3(0.62f * k, 0f, 0.05f);
                string w = set[ctx.Range(0, set.Length)];
                float y = w == Kit.Spear || w == Kit.Halberd ? 0.75f : 0.42f;
                PE(ctx, t, w, lx + off.x, lz + off.z, new Vector3(-8f, yaw + ctx.Rand(-10f, 10f), ctx.Rand(-6f, 6f)), y);
            }
        }

        static void FeastTable(Ctx ctx, TileDef t, float lx, float lz, float yaw)
        {
            P(ctx, t, Kit.DwarfTableLong, lx, lz, yaw);
            float h = 0.92f;
            var rot = Quaternion.Euler(0f, yaw, 0f);
            void On(string prefab, float ox, float oz, float oy = 0f)
            {
                var off = rot * new Vector3(ox, 0f, oz);
                P(ctx, t, prefab, lx + off.x, lz + off.z, ctx.Rand(0f, 360f), h + oy);
            }
            On(Kit.Plate, -1.0f, 0.3f); On(Kit.Bread, -1.0f, 0.3f, 0.03f);
            On(Kit.Plate, 0.2f, -0.35f); On(Kit.Meat, 0.2f, -0.35f, 0.03f);
            On(Kit.Goblet, -0.4f, 0.45f); On(Kit.Tankard, 0.9f, 0.4f); On(Kit.Goblet, 1.2f, -0.3f);
            On(Kit.Veg, 0.6f, 0.1f); On(Kit.Meat2, -1.3f, -0.4f);
            if (ctx.rng.NextDouble() < 0.6) { On(Kit.Candles3, 0f, 0f); PointLight(ctx, t.World(lx, h + 0.6f, lz), Candle, 5f, 0.9f, true); }
            else On(Kit.CampPot, 0f, 0f);
        }

        // ------------------------------------------------------------------ rooms

        static void Entry(Ctx ctx, TileDef t)
        {
            // lobby lx 10..35, lz 10..35; gate approach lx 15..30, lz 35..45; vestibule lx 20..25, lz 0..10
            // benches along the lobby's west and east walls
            foreach (float lz in new[] { 14f, 18.5f, 26.5f, 31f })
            {
                P(ctx, t, Kit.Bench, 11.1f, lz, 90f);
                P(ctx, t, Kit.Bench, 33.9f, lz, 270f);
            }
            // a guards' table on the lobby's west side, off the walkway from the vestibule to the gate
            P(ctx, t, Kit.Table, 15f, 22.5f, 90f);
            P(ctx, t, Kit.Stool1, 13.8f, 21.3f, 20f); P(ctx, t, Kit.Stool2, 13.7f, 23.7f, 0f); P(ctx, t, Kit.Stool1, 16.3f, 22.6f, 270f);
            P(ctx, t, Kit.Map, 15f, 22.4f, 105f, 0.97f);
            P(ctx, t, Kit.Tankard, 15.4f, 21.5f, 0f, 0.97f); P(ctx, t, Kit.Mug, 14.6f, 23.6f, 0f, 0.97f);
            Candles(ctx, t, 15.4f, 23.4f, 0.97f);
            // weapon stands and armour on the south wall either side of the vestibule door
            P(ctx, t, Kit.WeaponRack, 14f, 10.6f, 0f); P(ctx, t, Kit.WeaponRack, 31f, 10.6f, 0f);
            P(ctx, t, Kit.KnightStand, 18.2f, 10.7f, 0f); P(ctx, t, Kit.KnightStand, 26.8f, 10.7f, 0f);
            P(ctx, t, Kit.WeaponBarrel, 12f, 33.4f, 0f); P(ctx, t, Kit.ShieldHeater, 33.6f, 33.5f, 300f, 1.2f);
            P(ctx, t, Kit.Rug2, 22.5f, 24f, 0f);
            P(ctx, t, Kit.Barrel1, 33.6f, 11.4f, 0f); P(ctx, t, Kit.Crate, 11.5f, 11.4f, 15f);
            // banners on the north wall flanking the gate approach
            Banner(ctx, t, Kit.WallBanner1, 12.5f, 34.6f, Dir.S, 4.6f);
            Banner(ctx, t, Kit.WallBanner2, 32.5f, 34.6f, Dir.S, 4.6f);
            // torches
            Torch(ctx, t, 10.45f, 15f, Dir.E); Torch(ctx, t, 10.45f, 30f, Dir.E);
            Torch(ctx, t, 34.55f, 15f, Dir.W); Torch(ctx, t, 34.55f, 30f, Dir.W);
            Torch(ctx, t, 13f, 34.55f, Dir.S); Torch(ctx, t, 32f, 34.55f, Dir.S);
            Torch(ctx, t, 15.45f, 40f, Dir.E); Torch(ctx, t, 29.55f, 40f, Dir.W);
            Torch(ctx, t, 20.45f, 5f, Dir.E);
            Torch(ctx, t, 5f, 20.45f, Dir.N); Torch(ctx, t, 40f, 20.45f, Dir.N);
            Chandelier(ctx, t, 17f, 22.5f, 5f, 0, 2.6f, 14f);
            Chandelier(ctx, t, 28f, 22.5f, 5f, 0, 2.6f, 14f);
            // braziers flanking the gate approach
            Brazier(ctx, t, 16.5f, 33.2f); Brazier(ctx, t, 28.5f, 33.2f);
            // the row of spikes before the portcullis, and floor spikes
            for (int k = 0; k < 3; k++) P(ctx, t, Kit.SpikeFence, 20f + 5f * k, 42.6f, 0f);
            foreach (float lx in new[] { 17f, 22.5f, 28f }) P(ctx, t, Kit.SpikeTrap, lx, 41f, ctx.Rand(0f, 360f));
            // spikes are a barrier of their own: the row is a solid collider line
            var bar = new GameObject("SpikeBarrier");
            bar.transform.SetParent(ctx.structure, false);
            bar.transform.position = t.World(22.5f, 1f, 42.6f);
            var bc = bar.AddComponent<BoxCollider>();
            bc.size = new Vector3(15f, 2f, 0.6f);
            Pillar(ctx, t, 15.6f, 35.4f, 5f); Pillar(ctx, t, 29.4f, 35.4f, 5f);
        }

        static void Guard(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 10..35 (low, dim)
            P(ctx, t, Kit.Table, 22.5f, 22.5f, 0f);
            P(ctx, t, Kit.Stool1, 20.8f, 21.2f, 30f); P(ctx, t, Kit.Stool2, 24.2f, 21.3f, 0f);
            P(ctx, t, Kit.Stool3, 21f, 23.9f, 180f); P(ctx, t, Kit.Stool1, 24.3f, 23.8f, 200f);
            P(ctx, t, Kit.Map, 22.3f, 22.6f, 10f, 0.97f); P(ctx, t, Kit.Tankard, 21.4f, 22f, 0f, 0.97f);
            P(ctx, t, Kit.Tankard, 23.7f, 23.1f, 0f, 0.97f); P(ctx, t, Kit.Bread, 23.4f, 22.1f, 0f, 0.97f);
            Candles(ctx, t, 22.9f, 22.9f, 0.97f);
            // bunks along the south wall
            foreach (float lx in new[] { 12.5f, 16f, 19.5f }) P(ctx, t, Kit.DwarfBed, lx, 11.9f, 0f);
            foreach (float lx in new[] { 29f, 32.5f }) P(ctx, t, Kit.Bed, lx, 12f, 0f);
            // racks, barrels, crates
            P(ctx, t, Kit.WeaponRack, 14f, 34.4f, 180f); P(ctx, t, Kit.DwarfRack, 11f, 30f, 90f);
            P(ctx, t, Kit.WeaponBarrel, 33.5f, 33.5f, 0f);
            P(ctx, t, Kit.Barrel1, 32.8f, 31f, 0f); P(ctx, t, Kit.Barrel3, 33.6f, 29.6f, 40f);
            P(ctx, t, Kit.Crate, 12f, 20f, 10f); P(ctx, t, Kit.Crate2, 12f, 21.2f, 0f, 1.1f);
            P(ctx, t, Kit.Stocks, 30f, 17f, 70f);
            P(ctx, t, Kit.Cage, 33.2f, 20f, 0f);
            P(ctx, t, Kit.SkeletonShackles, 10.5f, 25f, 90f);
            P(ctx, t, Kit.ChainWall, 10.4f, 27f, 90f, 2.6f);
            P(ctx, t, Kit.Shelf, 22f, 34.4f, 180f); P(ctx, t, Kit.Bottle, 21.6f, 34.3f, 0f, 1.2f);
            P(ctx, t, Kit.Rug3, 22.5f, 22.5f, 90f);
            // dim: one brazier, one candle on the table, torches only at the doors
            Brazier(ctx, t, 28.5f, 29f, false, 2.4f);
            Torch(ctx, t, 37.5f, 24.55f, Dir.S, 2.4f, 1.6f, 9f);
            Torch(ctx, t, 27f, 39.55f, Dir.S, 2.4f, 1.6f, 9f);
            Torch(ctx, t, 10.45f, 32f, Dir.E, 2.4f, 1.4f, 9f);
            // the night watch
            Defenders(ctx, t, "Guard", 20.6f, 25.2f, 150f, 25.5f, 19.5f, 320f);
        }

        static void Prison(Ctx ctx, TileDef t)
        {
            // aisle lx 15..30, lz 5..35; west cells lx 10..15 at j 1,2,3,5,6; east cells lx 30..35 at j 1..6
            int k = 0;
            foreach (int j in new[] { 1, 2, 3, 5, 6 }) CellFurnish(ctx, t, 10f, 5f * j, true, k++);
            foreach (int j in new[] { 1, 2, 3, 4, 5, 6 }) CellFurnish(ctx, t, 30f, 5f * j, false, k++);
            // the riot's debris in the aisle
            P(ctx, t, Kit.TableRoundBroken, 22f, 18f, 30f);
            PE(ctx, t, Kit.Stool1, 24f, 12f, new Vector3(90f, 40f, 0f), 0.3f);
            PE(ctx, t, Kit.Stool2, 19f, 27f, new Vector3(0f, 10f, 100f), 0.33f);
            P(ctx, t, Kit.BarrelBroken, 26f, 30f, 60f); P(ctx, t, Kit.Bricks, 17f, 22f, 20f);
            P(ctx, t, Kit.Plank, 21f, 9f, 70f); P(ctx, t, Kit.Plank, 28f, 24f, 15f);
            PE(ctx, t, Kit.Brazier, 23.5f, 31f, new Vector3(0f, 0f, 95f), 0.5f);
            P(ctx, t, Kit.SkeletonLying, 26f, 8f, 120f);
            P(ctx, t, Kit.Bench, 17.5f, 33.5f, 180f);
            // dark: two guttering torches at the aisle ends and one in the cross passage
            Torch(ctx, t, 22.5f, 5.45f, Dir.N, 2.3f, 1.3f, 9f);
            Torch(ctx, t, 15.45f, 25f, Dir.E, 2.3f, 1.2f, 8f);
            Torch(ctx, t, 29.55f, 12f, Dir.W, 2.3f, 1.2f, 8f);
            Torch(ctx, t, 20f, 39.55f, Dir.S, 2.3f, 1.4f, 8f);
            P(ctx, t, Kit.ChainWall, 22.5f, 34.5f, 180f, 2.8f);
            // warden's corner lx 0..10, lz 15..35: shelves and bookcases on the west wall, the middle one the secret door
            // facing east (yaw 90) the case extends from its pivot toward +z, so the pivot sits 1.235 m south of its centre
            foreach (float lz in new[] { 17.5f, 20f, 25f, 27.5f }) P(ctx, t, Kit.BookcaseSmall2, 0.32f, lz - 1.235f, 90f);
            P(ctx, t, Kit.Table, 5.5f, 31f, 0f); P(ctx, t, Kit.StoneChair, 5.5f, 29.6f, 0f);
            P(ctx, t, Kit.Papers, 5.1f, 31.1f, 20f, 0.97f); P(ctx, t, Kit.Inkwell, 6.3f, 31.3f, 0f, 0.97f);
            P(ctx, t, Kit.BookPile, 4.6f, 31.4f, 0f, 0.97f);
            Candles(ctx, t, 6.4f, 30.6f, 0.97f);
            P(ctx, t, Kit.Shelf, 5f, 34.4f, 180f); P(ctx, t, Kit.Jar1, 4.7f, 34.3f, 0f, 1.2f); P(ctx, t, Kit.Jar2, 5.4f, 34.3f, 0f, 1.2f);
            P(ctx, t, Kit.Chest1, 8.8f, 16.2f, 10f); P(ctx, t, Kit.Crate, 7.4f, 16f, 0f);
            P(ctx, t, Kit.WeaponRack, 9.4f, 20f, 270f);
            P(ctx, t, Kit.Rug3, 5f, 24f, 0f);
            Torch(ctx, t, 5f, 15.45f, Dir.N, 2.3f, 1.6f, 9f);
            Torch(ctx, t, 9.55f, 32f, Dir.W, 2.3f, 1.4f, 9f);
            P(ctx, t, Kit.Lantern, 6.2f, 30.3f, 0f, 0.97f);
            // the rioters
            Defenders(ctx, t, "Prisoner", 18f, 12f, 60f, 26f, 15f, 250f, 20.5f, 24f, 120f, 27f, 28f, 200f, 17.5f, 30.5f, 100f, 24f, 8.5f, 20f);
        }

        /// <summary>Furnishes one 5 m prison cell whose south-west corner is (lx, lz); bars face east when faceEast.</summary>
        static void CellFurnish(Ctx ctx, TileDef t, float lx, float lz, bool faceEast, int k)
        {
            float back = faceEast ? lx + 0.75f : lx + 4.25f;   // against the back wall
            float yaw = faceEast ? 90f : 270f;
            int kind = k % 3;
            if (kind == 2)
            {
                // forced: bed on its side, stool thrown, straw everywhere
                PE(ctx, t, Kit.DwarfBed, back + (faceEast ? 0.7f : -0.7f), lz + 2.2f, new Vector3(0f, yaw, 80f), 0.65f);
                P(ctx, t, Kit.SkeletonSitting, back, lz + 4.2f, yaw);
            }
            else
            {
                P(ctx, t, Kit.DwarfBed, back, lz + 2.3f, yaw);
                P(ctx, t, Kit.Stool3, lx + 2.5f, lz + 4.3f, ctx.Rand(0f, 360f));
                P(ctx, t, Kit.CampPot2, lx + 2.4f + (faceEast ? 1.2f : -1.2f), lz + 0.7f, 0f);
            }
            if (k % 4 == 1) P(ctx, t, Kit.SkeletonShackles, back + (faceEast ? -0.2f : 0.2f), lz + 4.1f, yaw);
            if (k % 5 == 2) P(ctx, t, Kit.ChainWall, back + (faceEast ? -0.3f : 0.3f), lz + 1f, yaw, 2.4f);
            if (k % 3 == 0) P(ctx, t, Kit.Bricks, lx + 2.5f, lz + 1.2f, ctx.Rand(0f, 360f));
        }

        static void Vault(Ctx ctx, TileDef t)
        {
            // chamber lx 30..45, lz 15..30; the door on the east wall at lz 22.5
            P(ctx, t, Kit.TreasureChest, 34f, 22.5f, 90f);
            P(ctx, t, Kit.GoldPileLarge, 34.5f, 22.5f, 0f);
            P(ctx, t, Kit.GoldPile1, 39f, 27f, 30f); P(ctx, t, Kit.GoldPile2, 33f, 17.5f, 0f);
            P(ctx, t, Kit.GoldPile3, 41f, 17f, 0f); P(ctx, t, Kit.GoldPile3, 32.5f, 27.5f, 70f);
            P(ctx, t, Kit.Chest2, 31.2f, 19.5f, 80f); P(ctx, t, Kit.Chest3, 31.2f, 25.8f, 100f);
            P(ctx, t, Kit.Chest1, 37.5f, 28.8f, 180f); P(ctx, t, Kit.Chest4, 40.5f, 28.8f, 170f);
            P(ctx, t, Kit.ChestWood, 43.5f, 26f, 270f); P(ctx, t, Kit.Chest2, 43.5f, 18.5f, 260f);
            P(ctx, t, Kit.CrateOrnate, 36f, 16.2f, 10f); P(ctx, t, Kit.CrateOrnate, 37.1f, 16.2f, 0f); P(ctx, t, Kit.CrateOrnate, 36.5f, 16.2f, 5f, 1.02f);
            P(ctx, t, Kit.VaseGroup, 42.5f, 27.8f, 0f); P(ctx, t, Kit.Vase, 31f, 22.8f, 0f);
            foreach (var (x, z) in new[] { (38f, 20f), (36f, 25.5f), (40f, 24.5f), (39.5f, 18.5f) }) P(ctx, t, Kit.GoldCoins, x, z, ctx.Rand(0f, 360f));
            P(ctx, t, Kit.GemCluster, 38.4f, 21f, 0f); P(ctx, t, Kit.Gem, 35.8f, 25f, 0f, 0.3f); P(ctx, t, Kit.Gem, 42f, 26.5f, 0f, 0.3f);
            P(ctx, t, Kit.ShieldOrnate, 30.5f, 22.5f, 90f, 1.8f);
            Kit.PlaceRot(Kit.FxGold, Grp(ctx, t), t.World(34.5f, 0.8f, 22.5f), Quaternion.identity, Vector3.one * 1.5f);
            // dim: a candle stand and a lantern
            CandleStand(ctx, t, 41.5f, 20.5f);
            P(ctx, t, Kit.Lantern, 37.6f, 28.3f, 0f, 0.7f);
            PointLight(ctx, t.World(37.6f, 1.3f, 28.3f), Candle, 6f, 1.0f, true);
            PointLight(ctx, t.World(35f, 1.6f, 22.5f), new Color(1f, 0.85f, 0.45f), 9f, 1.4f, false);
            Pickup(ctx, t, 35.5f, 20f, "Pickup: the hoard");
        }

        static void Kitchen(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 15..35 (low, firelit); hearth on the north wall at lx 22.5
            P(ctx, t, Kit.Rotisserie, 22.5f, 33.1f, 0f);
            P(ctx, t, Kit.Cauldron, 19.8f, 33.6f, 0f); P(ctx, t, Kit.Bonfire, 19.8f, 33.6f, 0f, 0.6f);
            Kit.PlaceRot(Kit.FxFireSmall, Grp(ctx, t), t.World(19.8f, 0.3f, 33.6f), Quaternion.identity, Vector3.one * 0.8f);
            Kit.PlaceRot(Kit.FxSteam, Grp(ctx, t), t.World(19.8f, 0.9f, 33.6f), Quaternion.identity, Vector3.one * 0.6f);
            PointLight(ctx, t.World(19.8f, 1.2f, 33.6f), Fire, 8f, 1.8f, true);
            // worktables
            foreach (var (x, z, yaw) in new[] { (16f, 27f, 0f), (22.5f, 24f, 0f), (29f, 27f, 0f) })
            {
                P(ctx, t, Kit.DwarfTableLong, x, z, yaw);
                P(ctx, t, Kit.Plate, x - 1f, z + 0.2f, 0f, 0.92f); P(ctx, t, Kit.Bread, x - 1f, z + 0.2f, 20f, 0.95f);
                P(ctx, t, Kit.Veg, x + 0.4f, z - 0.3f, 0f, 0.92f); P(ctx, t, Kit.Veg2, x + 0.7f, z - 0.1f, 0f, 0.92f);
                P(ctx, t, Kit.Meat, x + 1.1f, z + 0.3f, 40f, 0.92f); P(ctx, t, Kit.Pot, x - 0.2f, z + 0.4f, 0f, 0.92f);
                P(ctx, t, Kit.Bottle, x + 1.3f, z - 0.4f, 0f, 0.92f);
            }
            Candles(ctx, t, 22.5f, 24.5f, 0.92f); Candles(ctx, t, 16.4f, 26.6f, 0.92f, Kit.Candles2);
            P(ctx, t, Kit.StoneTable, 22.5f, 30f, 90f); P(ctx, t, Kit.CampPot, 22.5f, 30f, 0f, 0.84f); P(ctx, t, Kit.Meat2, 22.8f, 29.5f, 0f, 0.84f);
            // stores: barrels, sacks, crates, shelves
            P(ctx, t, Kit.BarrelStack, 12f, 32.5f, 90f);
            P(ctx, t, Kit.Barrel1, 11f, 29f, 0f); P(ctx, t, Kit.Barrel2, 11.2f, 27.6f, 30f); P(ctx, t, Kit.Barrel3, 12.3f, 28.2f, 0f);
            P(ctx, t, Kit.SackStack1, 33.5f, 33.5f, 0f); P(ctx, t, Kit.SackStack2, 32f, 33.6f, 40f); P(ctx, t, Kit.Sack1, 33.8f, 32f, 0f); P(ctx, t, Kit.Sack2, 33f, 31.2f, 60f);
            P(ctx, t, Kit.Crate, 11f, 16.2f, 0f); P(ctx, t, Kit.Crate2, 12.2f, 16.1f, 10f); P(ctx, t, Kit.Crate, 11.5f, 16.2f, 0f, 1.1f);
            P(ctx, t, Kit.Shelf, 15f, 15.7f, 0f); P(ctx, t, Kit.Pot, 14.6f, 15.7f, 0f, 1.25f); P(ctx, t, Kit.Jar1, 15.4f, 15.7f, 0f, 1.25f);
            P(ctx, t, Kit.Shelf, 30f, 15.7f, 0f); P(ctx, t, Kit.Bottle, 29.6f, 15.7f, 0f, 1.25f); P(ctx, t, Kit.Jar2, 30.4f, 15.7f, 0f, 1.25f);
            P(ctx, t, Kit.BarrelShelf, 34.3f, 20f, 270f);
            P(ctx, t, Kit.Barrel2, 33.8f, 25f, 0f); P(ctx, t, Kit.Stool1, 25.5f, 22.5f, 0f);
            P(ctx, t, Kit.Bench, 22.5f, 18f, 0f);
            P(ctx, t, Kit.Shelf, 27f, 34.3f, 180f); P(ctx, t, Kit.Pot, 26.6f, 34.2f, 0f, 1.25f); P(ctx, t, Kit.CampPot, 27.5f, 34.2f, 0f, 1.25f);
            P(ctx, t, Kit.Shelf, 17f, 34.3f, 180f); P(ctx, t, Kit.Bread, 17f, 34.2f, 0f, 1.25f);
            // light: the hearth does most of it; a torch at each passage
            Torch(ctx, t, 34.55f, 30f, Dir.W, 2.3f, 1.6f);
            Torch(ctx, t, 10.45f, 22f, Dir.E, 2.3f, 1.6f);
            Torch(ctx, t, 27.5f, 10.45f, Dir.N, 2.3f, 1.4f, 9f);
            Torch(ctx, t, 39.55f, 22.5f, Dir.W, 2.3f, 1.4f, 9f);
            Torch(ctx, t, 20.45f, 5f, Dir.E, 2.3f, 1.4f, 9f);
            Defenders(ctx, t, "Cook", 22.5f, 31.5f, 0f, 17f, 25.3f, 0f);
        }

        static void Hall(Ctx ctx, TileDef t)
        {
            var grp = Grp(ctx, t);
            // ---- the minstrels' gallery deck over the south row, 5 m up
            for (int i = 0; i < 9; i++)
            {
                Kit.Place(Kit.FloorPlain, grp, t.World(5f * i + 5f, 5f, 0f), 0f);
                Kit.PlaceRot(Kit.Wall01, grp, t.World(5f * i + 5f, 5f, 0f), Quaternion.identity);           // south wall above the deck
                var c = Kit.Place(Kit.Ceilings[i % 3], ctx.ceilings, t.World(5f * i + 5f, 10f, 0f), 0f);
                if (c != null) Kit.SetLayerRecursive(c, ctx.ceilingLayer);
            }
            Kit.PlaceRot(Kit.Wall01, grp, t.World(0f, 5f, 0f), Quaternion.Euler(0f, 90f, 0f));            // west end of the deck
            Kit.PlaceRot(Kit.Wall01, grp, t.World(45f, 5f, 5f), Quaternion.Euler(0f, 270f, 0f));          // east end of the deck
            // an open, waist-high balustrade along the deck's north edge so the whole hall floor shows from the rail
            for (int k = 0; k < 14; k++) Kit.PlaceRot(Kit.Balustrade, grp, t.World(5f + 2.5f * k, 5f, 5f), Quaternion.identity);
            Kit.Place(Kit.RailingPost, grp, t.World(5f, 5f, 5f), 0f);
            Kit.Place(Kit.RailingPost, grp, t.World(40f, 5f, 5f), 0f);
            // a solid lip under the balustrade so nothing can slip off the deck edge
            var lip = new GameObject("DeckEdge");
            lip.transform.SetParent(grp, false);
            lip.transform.position = t.World(22.5f, 5.45f, 5f);
            var lipCol = lip.AddComponent<BoxCollider>();
            lipCol.size = new Vector3(35f, 0.9f, 0.25f);
            // gallery dressing: stools, drums, candles, a horn
            foreach (float lx in new[] { 12f, 17f, 28f, 33f }) P(ctx, t, Kit.Stool2, lx, 2.3f, ctx.Rand(0f, 360f), 5f);
            P(ctx, t, Kit.Drum, 14.5f, 1.6f, 20f, 5f); P(ctx, t, Kit.Drum, 30.5f, 1.6f, 0f, 5f);
            P(ctx, t, Kit.Horn, 40.5f, 1.3f, 200f, 5f);
            P(ctx, t, Kit.CandleStand, 7f, 1.3f, 0f, 5f); PointLight(ctx, t.World(7f, 6.9f, 1.3f), Candle, 7f, 1.2f, true);
            P(ctx, t, Kit.CandleStand, 38f, 1.3f, 0f, 5f); PointLight(ctx, t.World(38f, 6.9f, 1.3f), Candle, 7f, 1.2f, true);
            Torch(ctx, t, 12f, 0.45f, Dir.N, 7.3f, 1.8f); Torch(ctx, t, 22.5f, 0.45f, Dir.N, 7.3f, 1.8f); Torch(ctx, t, 33f, 0.45f, Dir.N, 7.3f, 1.8f);
            // ---- stairs in the walled shafts: west (lx 0..5) and east (lx 40..45), rising south from lz 15 to lz 5
            foreach (float sx in new[] { 0f, 40f })
            {
                GameObject s1, s2;
                if (StairsRiseTowardMinusZ)
                {
                    s1 = Kit.Place(Kit.Stairs, grp, t.World(sx, 0f, 15f), 0f, null, "ShaftStairLower");
                    s2 = Kit.Place(Kit.Stairs, grp, t.World(sx, 2.5f, 10f), 0f, null, "ShaftStairUpper");
                }
                else
                {
                    s1 = Kit.Place(Kit.Stairs, grp, t.World(sx + 5f, 0f, 10f), 180f, null, "ShaftStairLower");
                    s2 = Kit.Place(Kit.Stairs, grp, t.World(sx + 5f, 2.5f, 5f), 180f, null, "ShaftStairUpper");
                }
                DisableColliders(s1); DisableColliders(s2);
                Ramp(ctx, t.World(sx + 2.5f, 0f, 15f), t.World(sx + 2.5f, 5f, 5f), 4.6f);
                // a wedge under each flight so nothing shows beneath, and a torch in the shaft
                Torch(ctx, t, sx < 20f ? 0.45f : 44.55f, 12f, sx < 20f ? Dir.E : Dir.W, 4.2f, 1.8f);
                Torch(ctx, t, sx < 20f ? 0.45f : 44.55f, 20f, sx < 20f ? Dir.E : Dir.W, 2.6f, 1.8f);
            }
            // ---- the hall floor lx 5..40, lz 5..45: two long table rows and a high table across the north end
            foreach (float row in new[] { 13.5f, 31.5f })
                for (int k = 0; k < 7; k++)
                {
                    float lz = 11f + 3.4f * k;
                    FeastTable(ctx, t, row, lz, 90f);
                    P(ctx, t, Kit.DwarfBench, row - 1.15f, lz, 90f);
                    P(ctx, t, Kit.DwarfBench, row + 1.15f, lz, 90f);
                }
            // two high tables across the north end, leaving the axis clear from the runner to the great doors
            foreach (float hx in new[] { 15.5f, 29.5f })
            {
                FeastTable(ctx, t, hx, 38f, 0f);
                P(ctx, t, Kit.DwarfBench, hx, 39.15f, 0f);
                P(ctx, t, Kit.Chair, hx, 36.85f, 180f);
            }
            // runner from the arcade to the great doors
            for (int k = 0; k < 3; k++) P(ctx, t, Kit.Rug1, 22.5f, 12.5f + 12.9f * k, 90f);
            // chandeliers from the 10 m ceiling
            foreach (var (x, z) in new[] { (13.5f, 15f), (13.5f, 30f), (31.5f, 15f), (31.5f, 30f), (22.5f, 22.5f), (22.5f, 38f) })
                Chandelier(ctx, t, x, z, 10f, 1, 4.2f, 20f);
            // banners and torches on the long walls, engaged pillars between
            foreach (float lz in new[] { 10f, 20f, 30f, 40f })
            {
                Pillar(ctx, t, 5.6f, lz - 5f + 0.0f, 10f); Pillar(ctx, t, 39.4f, lz - 5f, 10f);
                Banner(ctx, t, lz % 20f == 0f ? Kit.WallBanner4 : Kit.WallBanner1, 5.4f, lz, Dir.E, 8.8f);
                Banner(ctx, t, lz % 20f == 0f ? Kit.WallBanner4 : Kit.WallBanner1, 39.6f, lz, Dir.W, 8.8f);
                Torch(ctx, t, 5.45f, lz - 2.5f, Dir.E, 3.2f, 2.2f, 12f);
                Torch(ctx, t, 39.55f, lz - 2.5f, Dir.W, 3.2f, 2.2f, 12f);
            }
            Pillar(ctx, t, 5.6f, 44.4f, 10f); Pillar(ctx, t, 39.4f, 44.4f, 10f);
            foreach (float lx in new[] { 10f, 16f, 29f, 35f })
            {
                Banner(ctx, t, Kit.WallBanner5, lx, 44.6f, Dir.S, 9.2f);
                Torch(ctx, t, lx + 3f, 44.55f, Dir.S, 3.2f, 2.2f, 12f);
            }
            Brazier(ctx, t, 8.5f, 43f, true); Brazier(ctx, t, 36.5f, 43f, true);
            // under the gallery: torches on the south wall, barrels, the armory door at lx 22.5
            foreach (float lx in new[] { 8f, 15f, 30f, 37f }) Torch(ctx, t, lx, 0.45f, Dir.N, 2.5f, 1.8f, 10f);
            P(ctx, t, Kit.Barrel1, 6.2f, 1f, 0f); P(ctx, t, Kit.Barrel3, 7.3f, 0.9f, 30f); P(ctx, t, Kit.Crate, 38.8f, 1f, 10f);
            // landings: a torch each, by the kitchen and forge doors
            Torch(ctx, t, 2.5f, 24.55f, Dir.S, 2.6f, 2f); Torch(ctx, t, 42.5f, 24.55f, Dir.S, 2.6f, 2f);
            // the feasters: the level's largest group
            Defenders(ctx, t, "Feaster",
                12.2f, 14.5f, 90f, 14.8f, 21f, 270f, 12.2f, 27.8f, 90f, 14.8f, 31.2f, 270f,
                30.2f, 17.9f, 90f, 32.8f, 24.6f, 270f, 30.2f, 31.2f, 90f, 29.5f, 35.3f, 0f);
        }

        static void Armory(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 5..35; gate bay lx 15..30, lz 0..5 with the portcullis on its south face
            // rows of racks: along both long walls and two free-standing double rows
            for (int k = 0; k < 6; k++)
            {
                float lz = 9f + 4.2f * k;
                StockedRack(ctx, t, 10.5f, lz, 90f);
                StockedRack(ctx, t, 34.5f, lz, 270f);
                StockedRack(ctx, t, 17.5f, lz, 270f); StockedRack(ctx, t, 18.1f, lz, 90f);
                StockedRack(ctx, t, 26.9f, lz, 270f); StockedRack(ctx, t, 27.5f, lz, 90f);
            }
            foreach (float lz in new[] { 11f, 15.2f, 19.4f, 23.6f, 27.8f, 32f })
            {
                P(ctx, t, Kit.DwarfRack, 14f, lz, 0f);
                P(ctx, t, Kit.DwarfRackLow, 31f, lz, 0f);
            }
            // armour stands along the north wall and flanking the gate bay
            foreach (float lx in new[] { 11.5f, 13.5f, 15.5f, 29.5f, 31.5f, 33.5f }) P(ctx, t, Kit.KnightStand, lx, 34.2f, 180f);
            foreach (float lx in new[] { 11.5f, 13.5f, 31.5f, 33.5f }) P(ctx, t, Kit.KnightStand, lx, 5.8f, 0f);
            P(ctx, t, Kit.WeaponBarrel, 11f, 7.2f, 0f); P(ctx, t, Kit.WeaponBarrel, 34f, 7.2f, 0f);
            P(ctx, t, Kit.CrateMetal, 33.6f, 30f, 0f); P(ctx, t, Kit.CrateMetal, 33.6f, 31.2f, 0f, 1.05f); P(ctx, t, Kit.Crate, 11.4f, 30.5f, 20f);
            foreach (float lz in new[] { 10f, 20f, 30f }) { P(ctx, t, Kit.HangingWeapon1, 10.35f, lz, 90f, 2.6f); P(ctx, t, Kit.HangingWeapon2, 34.65f, lz, 270f, 2.6f); }
            foreach (float lx in new[] { 18f, 27f }) P(ctx, t, Kit.ShieldHeater, lx, 34.6f, 180f, 2.4f);
            P(ctx, t, Kit.Table, 22.5f, 33.8f, 0f); P(ctx, t, Kit.ForgeHammer, 22f, 33.8f, 0f, 1.1f); P(ctx, t, Kit.Spear, 23.3f, 34f, 80f, 1f);
            // the best gear: a royal stand and an ornate chest in the middle aisle
            // the display stands in one line across the aisle's middle so nothing can trap the player around it
            P(ctx, t, Kit.KnightStandRoyal, 22.5f, 21.5f, 180f);
            P(ctx, t, Kit.CrateOrnate, 20.7f, 21.6f, 10f); P(ctx, t, Kit.Chest3, 24.3f, 21.6f, 350f);
            P(ctx, t, Kit.ShieldOrnate, 22.5f, 20.9f, 0f, 0.5f);
            Pickup(ctx, t, 22.5f, 19.6f, "Pickup: the armory’s best gear");
            // gate bay: the archers' post
            P(ctx, t, Kit.Barrel1, 16f, 4f, 0f); P(ctx, t, Kit.Crate, 29f, 4f, 20f);
            // torches
            Torch(ctx, t, 10.45f, 7f, Dir.E); Torch(ctx, t, 10.45f, 20f, Dir.E); Torch(ctx, t, 10.45f, 33f, Dir.E);
            Torch(ctx, t, 34.55f, 7f, Dir.W); Torch(ctx, t, 34.55f, 20f, Dir.W); Torch(ctx, t, 34.55f, 33f, Dir.W);
            Torch(ctx, t, 15.45f, 2.5f, Dir.E); Torch(ctx, t, 29.55f, 2.5f, Dir.W);
            Torch(ctx, t, 22.5f, 34.55f, Dir.S); Torch(ctx, t, 17.5f, 39.55f, Dir.S);
            Chandelier(ctx, t, 22.5f, 13f, 5f, 0, 2.8f, 15f); Chandelier(ctx, t, 22.5f, 27f, 5f, 0, 2.8f, 15f);
            // three archers at the gate facing south, two among the racks
            Defenders(ctx, t, "Archer", 18.5f, 2.2f, 180f, 22.5f, 2.6f, 180f, 26.5f, 2.2f, 180f, 15.5f, 24f, 180f, 30f, 12f, 0f);
        }

        static void Throne(Ctx ctx, TileDef t)
        {
            var grp = Grp(ctx, t);
            // hall lx 10..35, lz 15..45, 15 m tall; stage over lz 40..45 at 1.25 m, steps lz 35..40 across the whole width
            for (int i = 2; i <= 6; i++)
            {
                Kit.Place(Kit.FloorOrnate, grp, t.World(5f * i + 5f, 1.25f, 40f), 0f);
                var st = Kit.Place(Kit.Steps, grp, t.World(5f * i + 5f, 0f, 35f), 0f, new Vector3(1f, 1.25f, 1f), "StageSteps");
                DisableColliders(st);
            }
            // the stage's front face below the steps' top, so nothing is hollow
            var face = GameObject.CreatePrimitive(PrimitiveType.Cube);
            face.name = "StageFace";
            face.transform.SetParent(grp, false);
            face.transform.position = t.World(22.5f, 0.6f, 39.95f);
            face.transform.localScale = new Vector3(25f, 1.25f, 0.1f);
            face.GetComponent<Renderer>().sharedMaterial = ctx.matDark;
            Ramp(ctx, t.World(22.5f, 0f, 35f), t.World(22.5f, 1.25f, 40f), 25f);
            // stage top collider
            var top = new GameObject("StageTop");
            top.transform.SetParent(grp, false);
            top.transform.position = t.World(22.5f, 0.625f, 42.5f);
            var tb = top.AddComponent<BoxCollider>();
            tb.size = new Vector3(25f, 1.25f, 5f);
            // the throne, facing south down the runner
            P(ctx, t, Kit.Throne, 22.5f, 43.6f, 180f, 1.25f);
            var goal = new GameObject("ThroneGoal");
            goal.transform.SetParent(grp, false);
            goal.transform.position = t.World(22.5f, 2f, 41.5f);
            var sc = goal.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 4f;
            goal.AddComponent<ThroneGoal>();
            P(ctx, t, Kit.Statue1, 12.5f, 43f, 200f, 1.25f); P(ctx, t, Kit.Statue1, 32.5f, 43f, 160f, 1.25f);
            foreach (float lx in new[] { 16f, 29f }) { P(ctx, t, Kit.CandleStand, lx, 42.5f, 0f, 1.25f); PointLight(ctx, t.World(lx, 3.2f, 42.5f), Candle, 8f, 1.4f, true); }
            // the runner
            P(ctx, t, Kit.Rug1, 22.5f, 22f, 90f); P(ctx, t, Kit.Rug1, 22.5f, 31.2f, 90f);
            P(ctx, t, Kit.RealmRug, 22.5f, 37.5f, 0f, 0.02f);
            // columns, banners, braziers
            foreach (float lz in new[] { 17.5f, 22.5f, 27.5f, 32.5f, 37.5f, 42.5f })
            {
                Pillar(ctx, t, 10.7f, lz, 15f); Pillar(ctx, t, 34.3f, lz, 15f);
            }
            foreach (float lz in new[] { 20f, 25f, 30f, 35f, 40f })
            {
                Banner(ctx, t, Kit.WallBanner5, 10.4f, lz, Dir.E, 13.2f);
                Banner(ctx, t, Kit.WallBanner5, 34.6f, lz, Dir.W, 13.2f);
            }
            Banner(ctx, t, Kit.WallBanner4, 17f, 44.6f, Dir.S, 12.5f); Banner(ctx, t, Kit.WallBanner4, 28f, 44.6f, Dir.S, 12.5f);
            foreach (var (x, z) in new[] { (13f, 19f), (32f, 19f), (13f, 31f), (32f, 31f) }) Brazier(ctx, t, x, z, true, 4f);
            foreach (float lz in new[] { 20f, 28f, 36f }) Chandelier(ctx, t, 22.5f, lz, 15f, 2, 5f, 26f);
            foreach (float lz in new[] { 17.5f, 25f, 32.5f, 40f })
            {
                Torch(ctx, t, 10.45f, lz, Dir.E, 3f, 2.4f, 13f);
                Torch(ctx, t, 34.55f, lz, Dir.W, 3f, 2.4f, 13f);
            }
            // the antechamber that turns: statues at the corner, torches
            P(ctx, t, Kit.Statue2, 33.4f, 11.4f, 225f);
            P(ctx, t, Kit.Statue3, 21.2f, 6.4f, 135f);
            Torch(ctx, t, 27.5f, 9.55f, Dir.S, 2.5f, 1.8f); Torch(ctx, t, 34.55f, 12.5f, Dir.W, 2.5f, 1.8f);
            Torch(ctx, t, 24.55f, 2.5f, Dir.W, 2.5f, 1.8f);
            // the warlord on the stage and four guards before it
            Defenders(ctx, t, "Warlord", 22.5f, 41.8f, 180f);
            int count = ctx.defenderCounts.TryGetValue(t.roomId, out var n) ? n : 0;
            var guards = new[] { (17f, 33.5f), (28f, 33.5f), (19.5f, 30f), (25.5f, 30f) };
            for (int k = 0; k < count - 1 && k < guards.Length; k++) Marker(ctx, t, guards[k].Item1, guards[k].Item2, 180f, "Guard");
            // rename the warlord marker for clarity
            var w = ctx.markers.Find("Defender Warlord");
            if (w != null) w.position = t.World(22.5f, 1.25f, 41.8f);
        }

        static void Library(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 10..35; stacks: tall cases against the walls and two double rows
            string[] cases = { Kit.BookcaseGrand1, Kit.BookcaseGrand2, Kit.BookcaseGrand3, Kit.BookcaseTall1, Kit.BookcaseTall2, Kit.BookcaseTall3 };
            string Case() => cases[ctx.Range(0, cases.Length)];
            // the 2.5 m cases have their pivot at the back right corner seen from the front:
            // facing east (yaw 90) a case spans z [pivot, pivot + 2.5]; facing west (270) z [pivot - 2.5, pivot];
            // facing north (0) x [pivot - 2.5, pivot]; facing south (180) x [pivot, pivot + 2.5].
            // west wall, facing east, leaving the west door at lz 25..30
            for (float lz = 12.5f; lz <= 32.5f; lz += 2.5f)
            {
                if (lz >= 24.9f && lz < 30f) continue;
                P(ctx, t, Case(), 10.35f, lz, 90f);
            }
            // east wall facing west
            for (float lz = 12.5f; lz <= 32.5f; lz += 2.5f) P(ctx, t, Case(), 34.65f, lz + 2.5f, 270f);
            // south wall facing north
            for (float lx = 12.5f; lx <= 32.5f; lx += 2.5f) P(ctx, t, Case(), lx + 2.5f, 10.35f, 0f);
            // north wall facing south, leaving the door at lx 15..20
            foreach (float lx in new[] { 12.5f, 22.5f, 25f, 27.5f, 30f, 32.5f }) P(ctx, t, Case(), lx, 34.65f, 180f);
            // two free-standing double rows running north-south, back to back
            foreach (float row in new[] { 17f, 28f })
                for (float lz = 14f; lz <= 29f; lz += 2.5f)
                {
                    P(ctx, t, Case(), row - 0.05f, lz, 90f);          // faces east
                    P(ctx, t, Case(), row + 0.05f, lz + 2.5f, 270f);  // faces west
                }
            // reading tables in the centre aisle
            foreach (float lz in new[] { 17f, 26f })
            {
                P(ctx, t, Kit.Table, 22.5f, lz, 0f);
                P(ctx, t, Kit.Chair, 21.6f, lz - 1.1f, 0f); P(ctx, t, Kit.Chair, 23.4f, lz + 1.1f, 180f);
                P(ctx, t, Kit.BookOpen, 22f, lz + 0.2f, 20f, 0.97f); P(ctx, t, Kit.BookPile, 23.4f, lz - 0.3f, 0f, 0.97f);
                P(ctx, t, Kit.Papers, 21.3f, lz - 0.4f, 70f, 0.97f); P(ctx, t, Kit.Inkwell, 23.7f, lz + 0.4f, 0f, 0.97f);
                Candles(ctx, t, 22.8f, lz + 0.45f, 0.97f, Kit.Candles2);
            }
            P(ctx, t, Kit.BookStand, 22.5f, 31.5f, 180f); P(ctx, t, Kit.Globe, 19.4f, 13.2f, 0f);
            P(ctx, t, Kit.LibraryLadder, 20.5f, 29.5f, 180f); P(ctx, t, Kit.Scroll, 22.3f, 21.6f, 0f);
            P(ctx, t, Kit.BookPile, 19.3f, 21f, 30f); P(ctx, t, Kit.BookPile, 25.6f, 23.5f, 0f);
            P(ctx, t, Kit.Rug2, 22.5f, 21.5f, 90f);
            // light: candle stands by the tables, lamps at the ends, torches in the passages
            CandleStand(ctx, t, 20.3f, 14.8f); CandleStand(ctx, t, 24.7f, 28.2f);
            CandleStand(ctx, t, 20.3f, 23.5f); CandleStand(ctx, t, 24.7f, 19.5f);
            Chandelier(ctx, t, 22.5f, 21.5f, 5f, 0, 2.8f, 16f);
            Torch(ctx, t, 7.5f, 24.55f, Dir.S); Torch(ctx, t, 5.45f, 27.5f, Dir.E);
            Torch(ctx, t, 20.45f, 37.5f, Dir.E); Torch(ctx, t, 22.5f, 39.55f, Dir.S);
            Torch(ctx, t, 12.5f, 35.0f - 0.45f, Dir.S, 3.8f, 1.6f); Torch(ctx, t, 32.5f, 10.45f, Dir.N, 3.8f, 1.6f);
        }

        static void Study(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 10..35
            P(ctx, t, Kit.Cauldron, 22.5f, 22.5f, 0f); P(ctx, t, Kit.Bonfire, 22.5f, 22.5f, 0f, 0.7f);
            Kit.PlaceRot(Kit.FxFireSmall, Grp(ctx, t), t.World(22.5f, 0.25f, 22.5f), Quaternion.identity, Vector3.one * 0.9f);
            Kit.PlaceRot(Kit.FxSteam, Grp(ctx, t), t.World(22.5f, 0.95f, 22.5f), Quaternion.identity, Vector3.one * 0.8f);
            Kit.PlaceRot(Kit.FxMagic, Grp(ctx, t), t.World(22.5f, 1.1f, 22.5f), Quaternion.identity, Vector3.one * 0.5f);
            PointLight(ctx, t.World(22.5f, 1.6f, 22.5f), new Color(0.45f, 1f, 0.55f), 9f, 2.2f, true);
            // benches of glassware
            foreach (var (x, z, yaw) in new[] { (15.5f, 18f, 90f), (29.5f, 18f, 90f), (15.5f, 28f, 90f), (29.5f, 28f, 90f), (22.5f, 31.5f, 0f) })
            {
                P(ctx, t, Kit.Table, x, z, yaw);
                var rot = Quaternion.Euler(0f, yaw, 0f);
                for (int k = 0; k < 6; k++)
                {
                    var off = rot * new Vector3(ctx.Rand(-1.2f, 1.2f), 0f, ctx.Rand(-0.45f, 0.45f));
                    string item = k < 3 ? Kit.Potions[ctx.Range(0, Kit.Potions.Length)] : (k == 3 ? Kit.Vial1 : k == 4 ? Kit.Jar1 : Kit.Vial2);
                    P(ctx, t, item, x + off.x, z + off.z, ctx.Rand(0f, 360f), 0.96f);
                }
                if (ctx.rng.NextDouble() < 0.7) Candles(ctx, t, x + ctx.Rand(-0.8f, 0.8f), z + ctx.Rand(-0.3f, 0.3f), 0.96f, Kit.Candles4);
            }
            P(ctx, t, Kit.Scale, 22.5f, 31.6f, 0f, 0.96f); P(ctx, t, Kit.BookOpen, 21.4f, 31.3f, 10f, 0.96f);
            P(ctx, t, Kit.Skull, 23.6f, 31.8f, 30f, 0.96f); P(ctx, t, Kit.Shrooms, 15.3f, 27.5f, 0f, 1.0f);
            P(ctx, t, Kit.PotionPole, 19f, 22.5f, 0f); P(ctx, t, Kit.PotionPole, 26f, 22.5f, 180f);
            P(ctx, t, Kit.Chair, 22.5f, 30.2f, 0f); P(ctx, t, Kit.Stool2, 16.8f, 20f, 0f);
            // shelves of jars along the walls
            foreach (float lz in new[] { 14f, 22.5f, 31f }) { P(ctx, t, Kit.BookcaseSmall, 10.35f, lz + 1.235f - 0.0f, 90f); }
            foreach (float lz in new[] { 14f, 22.5f, 31f }) { P(ctx, t, Kit.BookcaseSmall2, 34.65f, lz - 1.235f + 0.0f, 270f); }
            foreach (float lx in new[] { 13f, 29f, 32f }) { P(ctx, t, Kit.Shelf, lx, 10.6f, 0f); P(ctx, t, Kit.Jar1, lx - 0.4f, 10.6f, 0f, 1.25f); P(ctx, t, Kit.Jar2, lx + 0.4f, 10.6f, 0f, 1.25f); P(ctx, t, Kit.Potions[ctx.Range(0, 9)], lx, 10.6f, 0f, 0.55f); }
            foreach (float lx in new[] { 13f, 16f, 29f }) { P(ctx, t, Kit.Shelf, lx, 34.4f, 180f); P(ctx, t, Kit.Vial1, lx - 0.3f, 34.4f, 0f, 1.25f); P(ctx, t, Kit.Jar2, lx + 0.3f, 34.4f, 0f, 1.25f); }
            P(ctx, t, Kit.Bookcase, 19.5f, 34.3f, 180f); P(ctx, t, Kit.Bookcase, 32.5f, 34.3f, 180f);
            P(ctx, t, Kit.Barrel2, 33.6f, 12f, 0f); P(ctx, t, Kit.Crate, 11.4f, 33.5f, 0f); P(ctx, t, Kit.Bed, 32.8f, 15.5f, 270f);
            P(ctx, t, Kit.BookPile, 20.2f, 16.3f, 30f); P(ctx, t, Kit.Rug3, 22.5f, 17f, 0f);
            // light: candle stands, a chandelier, torches at the passages
            CandleStand(ctx, t, 19f, 26f); CandleStand(ctx, t, 26f, 26f); CandleStand(ctx, t, 26f, 15f);
            Chandelier(ctx, t, 22.5f, 18f, 5f, 0, 2.6f, 14f);
            Torch(ctx, t, 10.45f, 18f, Dir.E); Torch(ctx, t, 34.55f, 24f, Dir.W); Torch(ctx, t, 20f, 34.55f, Dir.S);
            Torch(ctx, t, 17.5f, 9.55f, Dir.S); Torch(ctx, t, 24.55f, 37.5f, Dir.W);
            Defenders(ctx, t, "Alchemist", 22.5f, 20.4f, 0f);
        }

        static void Forge(Ctx ctx, TileDef t)
        {
            // room lx 10..35, lz 10..35; hearth on the north wall, hood and flue above it
            P(ctx, t, Kit.ForgePit, 22.5f, 32.5f, 0f);
            Kit.PlaceRot(Kit.FxFireLarge, Grp(ctx, t), t.World(22.5f, 1.0f, 32.5f), Quaternion.identity, Vector3.one * 0.8f);
            Kit.PlaceRot(Kit.FxSparks, Grp(ctx, t), t.World(22.5f, 1.3f, 32.5f), Quaternion.identity);
            Kit.PlaceRot(Kit.FxEmbers, Grp(ctx, t), t.World(22.5f, 1.5f, 32.5f), Quaternion.identity);
            PointLight(ctx, t.World(22.5f, 2.2f, 32.5f), Fire, 24f, 9f, true, true);
            PointLight(ctx, t.World(22.5f, 4.3f, 31.2f), Fire, 9f, 2.5f, true);   // lights the hood and flue from the front
            P(ctx, t, Kit.ForgeHood, 22.5f, 32.5f, 0f, 3.0f);
            P(ctx, t, Kit.ForgePipe, 22.5f, 32.5f, 0f, 3.8f);
            P(ctx, t, Kit.ForgePipe, 22.5f, 32.5f, 0f, 5.5f);
            P(ctx, t, Kit.Smelter, 14f, 32f, 0f);
            // anvils, quench barrels, tools
            foreach (float lx in new[] { 19f, 26f })
            {
                P(ctx, t, Kit.Anvil, lx, 27.5f, 0f);
                P(ctx, t, lx < 22f ? Kit.ForgeHammer : Kit.ForgeHammer2, lx + 0.4f, 27.5f, 30f, 1.2f);
            }
            P(ctx, t, Kit.Tongs, 20.3f, 31.4f, 0f, 1.2f);
            P(ctx, t, Kit.Barrel2, 17f, 31f, 0f); P(ctx, t, Kit.Barrel2, 28f, 31f, 0f);
            P(ctx, t, Kit.Table, 30f, 22f, 90f); P(ctx, t, Kit.Toolstrap, 30f, 22f, 90f, 0.97f); P(ctx, t, Kit.ForgeHammer2, 30.2f, 21f, 0f, 1.15f);
            P(ctx, t, Kit.Pickaxe, 29.8f, 23f, 90f, 1.15f);
            P(ctx, t, Kit.WeaponRack, 34.5f, 15f, 270f); P(ctx, t, Kit.WeaponRack, 34.5f, 28f, 270f);
            P(ctx, t, Kit.DwarfRack, 14f, 16f, 90f);
            P(ctx, t, Kit.HangingWeapon1, 34.65f, 21f, 270f, 2.4f); P(ctx, t, Kit.HangingWeapon2, 10.35f, 22f, 90f, 2.4f);
            P(ctx, t, Kit.OrePile, 12.5f, 24f, 0f); P(ctx, t, Kit.OrePile, 13.5f, 27.5f, 60f, 0.8f);
            P(ctx, t, Kit.CogPile, 31.5f, 12.5f, 0f); P(ctx, t, Kit.CrateMetal, 33.6f, 33.5f, 0f); P(ctx, t, Kit.CrateMetal, 33.6f, 33.5f, 20f, 1f);
            P(ctx, t, Kit.BarrelStack, 11.6f, 20f, 90f);
            Brazier(ctx, t, 28.5f, 15f);
            P(ctx, t, Kit.Stool2, 24f, 25f, 0f);
            // torches at the passages
            Torch(ctx, t, 10.45f, 27f, Dir.E); Torch(ctx, t, 34.55f, 10.5f + 0f, Dir.W, 2.4f, 1.6f);
            Torch(ctx, t, 7.5f, 24.55f, Dir.S); Torch(ctx, t, 17.5f, 9.55f, Dir.S); Torch(ctx, t, 5.45f, 22.5f, Dir.E);
            Defenders(ctx, t, "Smith", 19f, 26f, 0f, 26f, 26f, 0f);
        }
    }
}
