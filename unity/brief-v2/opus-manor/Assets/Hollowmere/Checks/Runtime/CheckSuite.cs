using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using IronCitadel.Interact;
using StarterAssets;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using static Hollowmere.Checks.LevelSurvey;

namespace Hollowmere.Checks
{
    /// <summary>
    /// Every check and walk on the level, run in play mode on the scene as loaded. Static checks measure the
    /// built scene (colliders, renderers, the NavMesh, sight lines, renders); the walks drive the shipped player
    /// through its own move/sprint input and its E-key script's Interact(), turning it to face where it goes.
    /// Started by Hollowmere.EditorTools.CheckRunner.RunAll(), which drops a request file and enters play mode.
    /// Writes Checks/results.txt and Checks/walk_trace.csv, then leaves play mode.
    /// </summary>
    public partial class CheckSuite : MonoBehaviour
    {
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string RequestFile => Path.Combine(Root, "Temp", "hollowmere_checks.request");
        public static string ResultFile => Path.Combine(Root, "Checks", "results.txt");
        public static string TraceFile => Path.Combine(Root, "Checks", "walk_trace.csv");
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly List<string> _out = new List<string>();
        readonly StringBuilder _trace = new StringBuilder("leg,t,x,y,z,room,hud\n");
        int _pass, _fail;
        List<RoomInfo> _rooms;
        List<DoorwayInfo> _doors;

        // the player as shipped
        Transform _player;
        CharacterController _cc;
        StarterAssetsInputs _input;
        InteractOnE _interact;

        const int SightMask = ~((1 << LayerSeeThrough) | (1 << 2));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!File.Exists(RequestFile)) return;
            File.Delete(RequestFile);
            new GameObject("CheckSuite").AddComponent<CheckSuite>();
        }

        void Log(string s) { _out.Add(s); Debug.Log("[Checks] " + s); }

        void Check(string id, string what, bool ok, string measured)
        {
            if (ok) _pass++; else _fail++;
            Log($"[{(ok ? "PASS" : "FAIL")}] {id} {what}\n         measured: {measured}");
        }

        static string F(float v) => v.ToString("0.00", Inv);
        static string P(Vector3 v) => $"({F(v.x)}, {F(v.y)}, {F(v.z)})";

        IEnumerator Start()
        {
            var t0 = DateTime.Now;
            yield return null; yield return null; yield return new WaitForSeconds(0.5f);
            Log("Hollowmere Manor (Assets/Scenes/IronCitadel.unity) - checks and walks");
            Log($"run: {t0:yyyy-MM-dd HH:mm:ss}, Unity {Application.unityVersion}, scene file written {File.GetLastWriteTime(Path.Combine(Root, "Assets/Scenes/IronCitadel.unity")):yyyy-MM-dd HH:mm:ss}");
            Log("Every value below is measured on the scene as loaded; nothing is copied from the plan.");
            Log("");
            _rooms = Rooms();
            _doors = Doors();
            var fpc = Object.FindAnyObjectByType<FirstPersonController>();
            _player = fpc.transform; _cc = fpc.GetComponent<CharacterController>(); _input = fpc.GetComponent<StarterAssetsInputs>(); _interact = fpc.GetComponent<InteractOnE>();

            Log("== Static checks ==");
            Run(StaticRooms); Run(StaticHeights); Run(StaticCeilings); Run(StaticIndoors); Run(StaticOpenings);
            Run(StaticGunRoom); Run(StaticGuardsHidden); Run(StaticWayRound); Run(StaticGallery); Run(StaticStage);
            Run(StaticStrongRoom); Run(StaticDoorsOpenable); Run(StaticOccluders); Run(StaticMarkers); Run(StaticSparse);
            Run(StaticMap);
            yield return StartCoroutine(Brightness());
            yield return StartCoroutine(OverviewToggle());
            Log("");
            Log("== Walks (player prefab as shipped; move/sprint input and InteractOnE.Interact() only) ==");
            yield return StartCoroutine(Walks());
            Log("");
            Log($"SUMMARY: {_pass} passed, {_fail} failed  (finished {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {(DateTime.Now - t0).TotalSeconds:F0} s)");
            Directory.CreateDirectory(Path.GetDirectoryName(ResultFile));
            File.WriteAllText(ResultFile, string.Join("\n", _out) + "\n");
            File.WriteAllText(TraceFile, _trace.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public static string ScreensDir => Path.Combine(Root, "Checks", "screens");

        /// <summary>Saves what the Game view shows (HUD included) at this point of the run.</summary>
        IEnumerator Screen(string name, float settle = 0.6f)
        {
            yield return new WaitForSeconds(settle);
            Directory.CreateDirectory(ScreensDir);
            string path = Path.Combine(ScreensDir, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            for (int attempt = 0; attempt < 2 && !File.Exists(path); attempt++)
            {
                ScreenCapture.CaptureScreenshot(path);
                float t = Time.realtimeSinceStartup;
                while (!File.Exists(path) && Time.realtimeSinceStartup - t < 2f) yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            Log(File.Exists(path) ? $"         screenshot: Checks/screens/{name}.png" : $"         screenshot {name}.png was not written");
        }

        void Run(Action a)
        {
            try { a(); }
            catch (Exception e) { Check("ERR", a.Method.Name + " threw", false, e.GetType().Name + ": " + e.Message); }
        }

        // ---------------------------------------------------------------- helpers

        RoomInfo Room(string kind) => _rooms.FirstOrDefault(r => r.kind == kind);
        Rect Outline(RoomInfo r) => FloorRects(r)[0];

        bool IsPlayer(Collider c) => _player != null && c.transform.IsChildOf(_player.root);

        /// <summary>First non-player, non-trigger hit along a segment within the mask.</summary>
        bool FirstHit(Vector3 a, Vector3 b, int mask, out RaycastHit hit)
        {
            var d = b - a; float len = d.magnitude;
            var hits = Physics.RaycastAll(a, d / len, len, mask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits) { if (IsPlayer(h.collider)) continue; hit = h; return true; }
            hit = default; return false;
        }

        /// <summary>Can an eye at a see the target collider's point b? True when nothing else is hit first.</summary>
        bool Sees(Vector3 eye, Vector3 b, Collider target)
        {
            if (!FirstHit(eye, b + (b - eye).normalized * 0.05f, SightMask, out var h)) return true;
            return h.collider == target;
        }

        IEnumerable<Vector3> Grid(Rect q, float step, float inset, float y)
        {
            for (float x = q.xMin + inset; x <= q.xMax - inset + 1e-3f; x += step)
                for (float z = q.yMin + inset; z <= q.yMax - inset + 1e-3f; z += step)
                    yield return new Vector3(x, y, z);
        }

        static readonly Vector3[] BodyPoints = { new Vector3(0, 1.72f, 0), new Vector3(0, 1.35f, 0), new Vector3(0, 0.95f, 0), new Vector3(0.24f, 1.4f, 0), new Vector3(-0.24f, 1.4f, 0), new Vector3(0, 0.45f, 0) };

        bool SeesBody(Vector3 eye, EnemyMarker e)
        {
            var col = e.GetComponent<Collider>();
            foreach (var bp in BodyPoints)
            {
                var p = e.transform.TransformPoint(bp);
                if (Sees(eye, p, col)) return true;
            }
            return false;
        }

        /// <summary>
        /// Would this defender see a player standing at p? Within its view cone (140 degrees, centred on the way it
        /// faces) and with a clear line from its eye to the player's head, chest or knees.
        /// </summary>
        bool EnemySees(EnemyMarker e, Vector3 p)
        {
            var eye = e.Eye;
            var to = p + Vector3.up * 1.1f - eye; to.y = 0f;
            if (to.sqrMagnitude > 0.01f && Vector3.Angle(e.transform.forward, to) > 70f) return false;
            foreach (var h in new[] { 1.65f, 1.1f, 0.6f })
            {
                var target = p + Vector3.up * h;
                if (!FirstHit(eye, target, SightMask, out var hit) || hit.collider == e.GetComponent<Collider>()) return true;
                // the first thing hit is the enemy's own body when it looks out of itself: skip it
                if (hit.collider.transform.IsChildOf(e.transform))
                {
                    var hits = Physics.RaycastAll(eye, (target - eye).normalized, (target - eye).magnitude, SightMask, QueryTriggerInteraction.Ignore)
                        .Where(x => !x.collider.transform.IsChildOf(e.transform) && !IsPlayer(x.collider)).ToArray();
                    if (hits.Length == 0) return true;
                }
            }
            return false;
        }
    }
}
