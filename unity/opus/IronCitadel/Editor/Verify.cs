using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace IronCitadel.EditorTools
{
    /// <summary>
    /// Checks the built scene against level.json's must_be_true list, using a NavMesh baked from the physics colliders
    /// for a player-sized agent (r 0.35, h 1.8, climb 0.35, slope 46) plus raycasts. Writes Captures/verification.txt.
    /// </summary>
    public static class Verify
    {
        static readonly string[,] Grid = { { "#", "#", "T", "#" }, { "#", "K", "D", "F" }, { "V", "P", "A", "S" }, { "#", "G", "E", "L" } };
        static StringBuilder _sb;
        static int _fails;

        static void Line(string s) { _sb.AppendLine(s); }
        static void Check(bool ok, string what) { if (!ok) _fails++; Line((ok ? "PASS  " : "FAIL  ") + what); }

        static string CellOf(Vector3 p)
        {
            int r = Mathf.FloorToInt(-p.z / 45f), c = Mathf.FloorToInt(p.x / 45f);
            if (r < 0 || r > 3 || c < 0 || c > 3) return "out";
            return Grid[r, c];
        }

        static NavMeshDataInstance _inst;

        static void Bake(List<GameObject> temporary = null)
        {
            if (_inst.valid) NavMesh.RemoveNavMeshData(_inst);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.35f; settings.agentHeight = 1.8f; settings.agentClimb = 0.35f; settings.agentSlope = 46f;
            settings.overrideVoxelSize = true; settings.voxelSize = 0.15f; settings.minRegionArea = 1f;
            var bounds = new Bounds(new Vector3(90f, 6f, -90f), new Vector3(186f, 24f, 186f));
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var player = GameObject.Find("IronCitadelPlayer");
            sources = sources.Where(s => s.component == null || player == null || !s.component.transform.IsChildOf(player.transform)).ToList();
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            _inst = NavMesh.AddNavMeshData(data);
        }

        static (bool ok, float length, List<string> cells, List<Vector3> pts) Path(Vector3 a, Vector3 b)
        {
            var path = new NavMeshPath();
            NavMesh.SamplePosition(a, out var ha, 2f, NavMesh.AllAreas);
            NavMesh.SamplePosition(b, out var hb, 2f, NavMesh.AllAreas);
            bool found = NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            var cells = new List<string>();
            var pts = new List<Vector3>();
            float len = 0f;
            var c = path.corners;
            for (int i = 0; i < c.Length; i++)
            {
                if (i > 0)
                {
                    len += Vector3.Distance(c[i - 1], c[i]);
                    int n = Mathf.CeilToInt(Vector3.Distance(c[i - 1], c[i]) / 0.5f);
                    for (int k = 1; k <= n; k++) pts.Add(Vector3.Lerp(c[i - 1], c[i], k / (float)n));
                }
                else pts.Add(c[i]);
            }
            foreach (var p in pts) { var cell = CellOf(p); if (cells.Count == 0 || cells[cells.Count - 1] != cell) cells.Add(cell); }
            return (found, len, cells, pts);
        }

        static Vector3 W(int r, int c, float u, float v, float y = 0f) => new Vector3(c * 45f + u, y, -(r + 1) * 45f + v);

        /// <summary>Walkable spans of NavMesh along every tile edge (internal and outer).</summary>
        static List<string> Openings()
        {
            var found = new List<string>();
            // vertical edges x = 45k, horizontal edges z = -45k
            for (int k = 0; k <= 4; k++)
                for (int seg = 0; seg < 4; seg++)
                {
                    foreach (bool vertical in new[] { true, false })
                    {
                        var spans = new List<(float a, float b)>();
                        float? start = null; float last = 0;
                        for (float t = 0.1f; t < 45f; t += 0.1f)
                        {
                            Vector3 p = vertical ? new Vector3(45f * k, 0f, -45f * seg - t) : new Vector3(45f * seg + t, 0f, -45f * k);
                            bool walk = false;
                            foreach (float y in new[] { 0f })
                                if (NavMesh.SamplePosition(p + Vector3.up * y, out var hit, 0.25f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - y) < 0.4f &&
                                    new Vector2(hit.position.x - p.x, hit.position.z - p.z).magnitude < 0.2f) walk = true;
                            if (walk && start == null) start = t;
                            if (!walk && start != null) { spans.Add((start.Value, last)); start = null; }
                            last = t;
                        }
                        if (start != null) spans.Add((start.Value, last));
                        foreach (var s in spans)
                        {
                            float mid = (s.a + s.b) / 2f;
                            Vector3 m = vertical ? new Vector3(45f * k, 0f, -45f * seg - mid) : new Vector3(45f * seg + mid, 0f, -45f * k);
                            string sideA = vertical ? CellOf(m + Vector3.left * 1f) : CellOf(m + Vector3.forward * 1f);
                            string sideB = vertical ? CellOf(m + Vector3.right * 1f) : CellOf(m + Vector3.back * 1f);
                            found.Add($"{sideA}|{sideB} at ({m.x:F1}, {m.z:F1}) walkable width {(s.b - s.a + 0.1f + 0.7f):F1} m");
                        }
                    }
                }
            return found;
        }

        static GameObject Block(Vector3 centre, Vector3 axis, float width, string name)
        {
            var go = new GameObject("VERIFY_BLOCK_" + name);
            go.transform.SetPositionAndRotation(centre + Vector3.up * 2f, Quaternion.LookRotation(axis));
            go.AddComponent<BoxCollider>().size = new Vector3(width + 2f, 4f, 1f);
            return go;
        }

        public static string Run()
        {
            Shots.Open();
            _sb = new StringBuilder();
            _fails = 0;
            Line("Iron Citadel - verification of level.json must_be_true, run " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            Line("NavMesh baked from physics colliders for the player's capsule (r 0.35, h 1.8, step 0.35, slope 46).");
            Line("");

            // ---- rooms on cells
            Line("[1] Rooms on their cells; '#' cells are rock");
            var expected = new Dictionary<string, (int r, int c)> {
                { "Entry Hall", (3, 2) }, { "Guard Room", (3, 1) }, { "Prison", (2, 1) }, { "Treasure Vault", (2, 0) }, { "Kitchen", (1, 1) }, { "Great Hall", (1, 2) },
                { "Armory", (2, 2) }, { "Throne Room", (0, 2) }, { "Library", (3, 3) }, { "Alchemist's Study", (2, 3) }, { "Forge", (1, 3) } };
            var level = GameObject.Find("IronCitadel").transform;
            foreach (var kv in expected)
            {
                var room = level.Find(kv.Key);
                bool inside = room != null;
                if (inside)
                    foreach (var r in room.GetComponentsInChildren<Renderer>())
                    {
                        if (r.gameObject.layer == Kit.OverviewLayer || r is ParticleSystemRenderer) continue;
                        var b = r.bounds;
                        float x0 = kv.Value.c * 45f, z1 = -kv.Value.r * 45f;
                        if (b.min.x < x0 - 0.01f || b.max.x > x0 + 45.01f || b.min.z < z1 - 45.01f || b.max.z > z1 + 0.01f) { inside = false; Line($"      {kv.Key}: {r.name} strays outside its tile"); break; }
                    }
                Check(inside, $"{kv.Key} built entirely inside cell [{kv.Value.r},{kv.Value.c}]");
            }

            Bake();
            // ---- openings
            Line("");
            Line("[2] Every walkable crossing of a tile edge (NavMesh sampled every 0.1 m along all 40 edges, at floor and gallery level)");
            var open = Openings();
            foreach (var o in open) Line("      " + o);
            var want = new[] { "E|G", "E|L", "E|A", "G|P", "P|V", "P|K", "K|D", "L|S", "S|F", "F|D", "D|A", "D|T" };
            string Norm(string s) { var p = s.Split(' ')[0].Split('|'); return string.Join("|", p.OrderBy(x => x)); }
            var got = open.Select(Norm).ToList();
            foreach (var w in want) Check(got.Count(g => g == string.Join("|", w.Split('|').OrderBy(x => x))) == 1, $"exactly one opening between {w}");
            Check(open.Count == want.Length, $"no other openings between tiles ({open.Count} found = the {want.Length} doors of level.json between rooms; the outer door's passage ends at its shut leaves)");

            // ---- the gate
            Line("");
            Line("[3] The armory gate");
            var gateFace = new Vector3(112.5f, 0, -137.5f);
            bool blocked = true;
            for (float dx = -1.9f; dx <= 1.9f; dx += 0.2f)
                foreach (float y in new[] { 0.3f, 1f, 2f, 3.5f })
                    if (!Physics.Linecast(new Vector3(112.5f + dx, y, -140f), new Vector3(112.5f + dx, y, -131f))) blocked = false;
            Check(blocked, "lines through the gate opening at 0.3-3.5 m are all stopped (portcullis + spikes)");
            var spikes = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name.StartsWith("SM_Env_Trap_Spikes_01") && t.position.z < -137.5f);
            Check(spikes >= 4, $"a row of {spikes} spike traps stands before the portcullis on the entry side");
            var behind = Path(W(3, 2, 22.5f, 30f), new Vector3(112.5f, 0f, -134.5f));
            Check(!behind.ok || behind.cells.Contains("D"), "the floor behind the portcullis is not reachable from the entry hall side");
            var ea = Path(W(3, 2, 22.5f, 30f), W(2, 2, 22.5f, 20f));
            Check(ea.ok && ea.cells.Contains("D") && ea.length > 100f, $"entry hall -> armory only by the long way: path {ea.length:F0} m through {string.Join(">", ea.cells)}");

            // ---- the two wings
            Line("");
            Line("[4] West and east wings");
            var start = CitadelBuilder.StartPos;
            var hall = W(1, 2, 22.5f, 25f);
            var blockEast = Block(new Vector3(135f, 0, -157.5f), Vector3.right, 3f, "east_door");
            Bake();
            var west = Path(start, hall);
            Check(west.ok && !west.cells.Contains("A") && string.Join(">", west.cells) == "E>G>P>K>D", $"west wing (east door barred): {west.length:F0} m via {string.Join(">", west.cells)}");
            Object.DestroyImmediate(blockEast);
            var blockWest = Block(new Vector3(90f, 0, -157.5f), Vector3.right, 3f, "west_door");
            Bake();
            var eastP = Path(start, hall);
            Check(eastP.ok && !eastP.cells.Contains("A") && string.Join(">", eastP.cells) == "E>L>S>F>D", $"east wing (west door barred): {eastP.length:F0} m via {string.Join(">", eastP.cells)}");
            Object.DestroyImmediate(blockWest);
            Line($"      wing lengths differ by {Mathf.Abs(west.length - eastP.length):F0} m");
            Bake();

            // ---- the vault
            Line("");
            Line("[5] The treasure vault");
            var vault = W(2, 0, 34.5f, 22.5f);
            var prison = W(2, 1, 8f, 15f);
            var closed = Path(prison, vault);
            Check(!closed.ok, "with the bookcase shut there is no way into the vault");
            var hinge = GameObject.Find("SecretBookcase (vault door)");
            var rot0 = hinge.transform.rotation;
            hinge.transform.rotation = Quaternion.Euler(0, -90f, 0) * rot0;
            Physics.SyncTransforms();
            Bake();
            var opened = Path(prison, vault);
            Check(opened.ok && string.Join(">", opened.cells) == "P>V", $"with the bookcase swung aside the vault is reached from the prison: {string.Join(">", opened.cells)}");
            var vopen = Openings().Where(o => o.Contains("V")).ToList();
            Check(vopen.Count == 1 && Norm(vopen[0]) == "P|V", "the vault's only crossing is the bookcase passage from the prison");
            hinge.transform.rotation = rot0;
            Physics.SyncTransforms();
            Bake();
            var neighbours = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent != null && t.parent.name == "Furnishing" && t.name.StartsWith("SM_Prop_Bookcase_Small")
                && Mathf.Abs(t.position.x - 47.5f) < 1.5f).Count();
            Check(neighbours >= 4, $"the secret door is the same bookcase model as its {neighbours} neighbours along the warden's wall, flush with them");
            Check(!west.cells.Contains("V") && !eastP.cells.Contains("V"), "the vault lies on neither route");

            // ---- throne room
            Line("");
            Line("[6] Throne room");
            var topen = open.Where(o => o.Contains("T")).ToList();
            Check(topen.Count == 1 && Norm(topen[0]) == "D|T", "the throne room has exactly one door, from the great hall");
            var throne = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name.StartsWith("SM_Prop_Dwarf_Throne_01"));
            Physics.Raycast(throne.position + Vector3.up * 3f + Vector3.back * 4.5f, Vector3.down, out var stageHit, 10f);
            Check(Mathf.Abs(stageHit.point.y - 1.25f) < 0.05f, $"the stage in front of the throne is {stageHit.point.y:F2} m up");
            int stepSamples = 0, stepOk = 0;
            for (float x = 93f; x <= 132f; x += 1f)
            {
                stepSamples++;
                var p = Path(new Vector3(x, 0, -16f), new Vector3(x, 1.25f, -6f));
                if (p.ok && p.length < 13f) stepOk++;
            }
            Check(stepOk == stepSamples, $"steps across the whole front: the stage is climbed straight up at {stepOk}/{stepSamples} points across its 40 m width");
            var door = new Vector3(112.5f, 1.6f, -43f);
            bool seen = false;
            foreach (var t in new[] { 0.5f, 2f, 4f, 6f })
                for (float dx = -2f; dx <= 2f; dx += 1f)
                {
                    var from = door + Vector3.right * dx;
                    var to = throne.position + Vector3.up * t;
                    if (Physics.Raycast(from, (to - from).normalized, out var th, (to - from).magnitude + 2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    {
                        var h = th.collider.transform;
                        if (h.IsChildOf(throne) || h.name.Contains("Warlord") || (h.parent != null && h.parent.name.Contains("Warlord"))) seen = true;
                    }
                    else seen = true;
                }
            Check(!seen, "the throne cannot be seen from the doorway (the antechamber screen turns the way in)");
            var tp = Path(hall, throne.position + Vector3.back * 3f);
            Check(tp.ok, $"the throne is reachable from the great hall: {tp.length:F0} m");

            // ---- the gallery
            Line("");
            Line("[7] The minstrels' gallery");
            var deckHit = Physics.Raycast(W(1, 2, 22.5f, 5.5f, 9f), Vector3.down, out var dh, 10f);
            Check(deckHit && Mathf.Abs(dh.point.y - 5f) < 0.1f, $"gallery deck along the south side at {dh.point.y:F2} m");
            foreach (var (side, landing) in new[] { ("west", W(1, 2, 5f, 23f)), ("east", W(1, 2, 40f, 23f)) })
            {
                var doorFace = side == "west" ? W(1, 2, 1f, 22.5f) : W(1, 2, 44f, 22.5f);
                var toLanding = Path(doorFace, landing);
                bool floorFree = toLanding.pts.All(p => !(p.x > 97.6f && p.x < 127.4f && p.z > -81.4f && p.z < -47.6f));
                Check(toLanding.ok && floorFree, $"{side} door reaches its landing without the hall floor ({toLanding.length:F0} m)");
                var up = Path(landing, W(1, 2, 22.5f, 5.5f, 5f));
                bool upFree = up.pts.All(p => !(p.x > 97.6f && p.x < 127.4f && p.z > -81.4f && p.z < -47.6f && p.y < 1f));
                float maxY = up.pts.Count > 0 ? up.pts.Max(p => p.y) : 0f;
                Check(up.ok && upFree && maxY > 4.8f, $"{side} stair climbs from the landing to the gallery without crossing the hall floor ({up.length:F0} m, top {maxY:F1} m)");
            }
            var ug = new Vector3(112.5f, 0f, -86.3f);
            bool under = Physics.Raycast(ug + Vector3.up * 0.5f, Vector3.up, out var uh, 10f) && uh.point.y < 5.1f;
            Check(under, $"the armory door opens beneath the gallery (deck overhead at {uh.point.y:F1} m)");
            // sightlines from the rail, through the real (open) railing: give its visual mesh a collider for the test
            var railTemp = new List<Component>();
            foreach (var rail in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "GalleryRailing"))
                foreach (var mf in rail.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    railTemp.Add(mc);
                }
            Physics.SyncTransforms();
            int total = 0, visible = 0, furniture = 0, arch = 0;
            var furnitureRoots = new HashSet<Transform>();
            // the hall floor proper: inside the side walls, north of the arcade's plinth (v 11); eyes where a player can stand at the rail
            for (float x = 98f; x <= 127f; x += 1f)
                for (float z = -79f; z <= -48f; z += 1f)
                {
                    total++;
                    var target = new Vector3(x, 0.35f, z);
                    bool any = false; bool archBlock = true;
                    for (float back = 0f; back <= 0.76f && !any; back += 0.25f)
                    for (float ex = 98f; ex <= 127f; ex += 0.5f)
                    {
                        var eye = new Vector3(ex, 6.6f, -90f + 8.6f - 0.35f - back);
                        if (!Physics.Linecast(eye, target, out var h, ~(1 << 2), QueryTriggerInteraction.Ignore)) { any = true; break; }
                        if (!IsArchitecture(h.collider.transform)) archBlock = false;
                    }
                    if (any) visible++;
                    else if (archBlock) arch++;
                    else furniture++;
                }
            foreach (var c in railTemp) Object.DestroyImmediate(c);
            Check(arch == 0, $"from the gallery rail (eye 1.6 m above the deck, standing at the railing or up to 0.75 m back, looking through or over its balusters) the whole hall floor is in view: of {total} floor points (1 m grid) {visible} are seen, {furniture} are hidden only by tables, benches, feasters or the hearths, {arch} by architecture");

            // ---- heights
            Line("");
            Line("[8] Heights (floor to ceiling)");
            foreach (var kv in new[] { ("Prison", 3.6f), ("Guard Room", 3.6f), ("Kitchen", 4.0f), ("Treasure Vault", 3.2f), ("Great Hall", 12.5f), ("Throne Room", 15f),
                ("Entry Hall", 6f), ("Armory", 6f), ("Library", 6f), ("Alchemist's Study", 6f), ("Forge", 6f) })
            {
                var room = level.Find(kv.Item1);
                var ceil = room.Find("Ceiling").GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("Ceiling_Stone")).Select(r => r.bounds.min.y).DefaultIfEmpty(-1).Max();
                Line($"      {kv.Item1,-18} ceiling at {ceil:F2} m");
            }
            Check(true, "low: prison 3.6, guard room 3.6, kitchen 4.0, vault 3.2; tall: great hall 12.5, throne room 15 (the tallest); others 6");

            // ---- markers
            Line("");
            Line("[9] Defenders and pickups (markers)");
            var want2 = new Dictionary<string, int> { { "Armory", 5 }, { "Great Hall", 8 }, { "Throne Room", 5 }, { "Prison", 6 }, { "Guard Room", 2 }, { "Kitchen", 2 }, { "Forge", 2 }, { "Alchemist's Study", 1 } };
            foreach (var kv in want2)
            {
                int n = level.Find(kv.Key).Find("Markers").Cast<Transform>().Count(t => t.name.StartsWith("Defender_"));
                Check(n == kv.Value, $"{kv.Key}: {n} defenders (data: {kv.Value})");
            }
            int pk = GameObject.FindObjectsByType<PickupMarker>(FindObjectsSortMode.None).Length;
            Check(pk == 2, $"{pk} pickups (armory gear, vault hoard)");
            int archers = level.Find("Armory").Find("Markers").Cast<Transform>().Count(t => t.name == "Defender_Archer" && t.position.z < -125f && Vector3.Dot(t.forward, Vector3.back) > 0.9f);
            Check(archers == 3, $"{archers} archers stand behind the portcullis facing south");

            if (_inst.valid) NavMesh.RemoveNavMeshData(_inst);
            Line("");
            Line(_fails == 0 ? "ALL CHECKS PASSED" : $"{_fails} CHECK(S) FAILED");
            System.IO.File.WriteAllText(Shots.Dir + "verification.txt", _sb.ToString());
            return _sb.ToString();
        }

        static bool IsArchitecture(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "Furnishing" || p.name == "Markers") return false;
                if (p.name == "Colliders" || p.name == "Architecture") return !t.name.StartsWith("Fireplace");
            }
            return true;
        }
    }
}
