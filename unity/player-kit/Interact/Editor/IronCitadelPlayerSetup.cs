using System.IO;
using UnityEditor;
using UnityEngine;

namespace IronCitadel.Interact.Editor
{
    /// <summary>
    /// Makes IronCitadelPlayer.prefab next to this add-on (the folder above this Editor folder): a variant of
    /// Starter Assets' first-person NestedParent_Unpack
    /// (PlayerCapsule, MainCamera, PlayerFollowCamera) with InteractOnE on the PlayerCapsule, sized to a person:
    /// CharacterController r 0.35, h 1.8, centre y 0.9, step 0.3, slope 45, skin 0.02; GroundedRadius 0.35;
    /// PlayerCameraRoot (the eye) at 1.6 m; the visible Capsule child scaled to the same 0.35 x 1.8. The root and
    /// the capsule's feet are at the origin; PlayerFollowCamera is a first-person camera on PlayerCameraRoot; the
    /// main camera clears to solid black.
    /// Starter Assets' own prefabs are never changed. Run it once after importing the Starter Assets package:
    ///   unity run &lt;project&gt; -- -executeMethod IronCitadel.Interact.Editor.IronCitadelPlayerSetup.MakePlayer -logFile &lt;log&gt;
    /// Writes IronCitadelPlayer.result.txt beside the prefab ("ok ..." or "error ...").
    /// </summary>
    public static class IronCitadelPlayerSetup
    {
        const string Source = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/NestedParent_Unpack.prefab";

        // A person, not Starter Assets' 1 m x 2 m capsule, which clears Synty's 2.05 m lintels by 1 cm.
        public const float Radius = 0.35f, Height = 1.8f, CentreY = 0.9f, StepOffset = 0.3f, SlopeLimit = 45f, Skin = 0.02f;
        public const float GroundedRadius = 0.35f, EyeHeight = 1.6f, FieldOfView = 60f;

        /// <summary>The add-on's folder: the parent of the Editor folder holding this script.</summary>
        public static string Folder()
        {
            foreach (var guid in AssetDatabase.FindAssets("IronCitadelPlayerSetup t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p) != "IronCitadelPlayerSetup.cs") continue;
                return Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');
            }
            return "Assets/Interact";
        }

        public static string TargetPath => Folder() + "/IronCitadelPlayer.prefab";

        [MenuItem("Tools/Iron Citadel/Make IronCitadelPlayer prefab")]
        public static void MakePlayer()
        {
            string dir = Folder(), target = dir + "/IronCitadelPlayer.prefab", result = dir + "/IronCitadelPlayer.result.txt";
            string outcome;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (src == null) outcome = $"error: no {Source}; import the Starter Assets package first";
            else
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                try
                {
                    Transform capsule = Find(inst.transform, "PlayerCapsule");
                    if (capsule == null) outcome = "error: no PlayerCapsule under " + Source;
                    else
                    {
                        if (capsule.GetComponent<InteractOnE>() == null) capsule.gameObject.AddComponent<InteractOnE>();
                        string sized = Size(capsule);
                        string rig = Rig(inst.transform, capsule);
                        inst.name = "IronCitadelPlayer";
                        PrefabUtility.SaveAsPrefabAsset(inst, target, out bool ok);
                        outcome = ok ? $"ok {target} (variant of {Source}, InteractOnE on {capsule.name}; {sized}; {rig})" : "error: SaveAsPrefabAsset failed";
                    }
                }
                finally { Object.DestroyImmediate(inst); }
            }
            File.WriteAllText(result, outcome + "\n");
            Debug.Log("[IronCitadelPlayerSetup] " + outcome);
            AssetDatabase.Refresh();
        }

        /// <summary>Sets the human-scale numbers on the instance (saved as overrides in the variant).</summary>
        static string Size(Transform capsule)
        {
            var notes = new System.Collections.Generic.List<string>();
            var cc = capsule.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.radius = Radius;
                cc.height = Height;
                cc.center = new Vector3(0f, CentreY, 0f);
                cc.stepOffset = StepOffset;
                cc.slopeLimit = SlopeLimit;
                cc.skinWidth = Skin;
                notes.Add($"CharacterController r {Radius} h {Height} centre y {CentreY} step {StepOffset} slope {SlopeLimit} skin {Skin}");
            }
            else notes.Add("no CharacterController on PlayerCapsule");

            // Starter Assets' FirstPersonController, set by name so this assembly needs no Starter Assets reference
            MonoBehaviour fpc = null;
            foreach (var mb in capsule.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "FirstPersonController") { fpc = mb; break; }
            var gr = fpc != null ? new SerializedObject(fpc) : null;
            var prop = gr?.FindProperty("GroundedRadius");
            if (prop != null)
            {
                prop.floatValue = GroundedRadius;
                gr.ApplyModifiedPropertiesWithoutUndo();
                notes.Add($"GroundedRadius {GroundedRadius}");
            }
            else notes.Add("no FirstPersonController.GroundedRadius");

            var eye = Find(capsule, "PlayerCameraRoot");
            if (eye != null)
            {
                eye.localPosition = new Vector3(eye.localPosition.x, EyeHeight, eye.localPosition.z);
                notes.Add($"PlayerCameraRoot y {EyeHeight}");
            }
            else notes.Add("no PlayerCameraRoot");

            // the visible capsule (a 1 m x 2 m mesh with its own collider) shrunk to the controller's size
            var body = capsule.Find("Capsule");
            if (body != null)
            {
                body.localPosition = new Vector3(body.localPosition.x, CentreY, body.localPosition.z);
                body.localScale = new Vector3(Radius / 0.5f, Height / 2f, Radius / 0.5f);
                notes.Add($"Capsule child scaled to r {Radius} h {Height}");
            }
            return string.Join(", ", notes);
        }

        /// <summary>
        /// The rig itself. Starter Assets' NestedParent_Unpack sits at (22.2, -8.79, 23.9) with its children offset
        /// back to the origin; here the root is at the origin and the PlayerCapsule's feet are on the root, so
        /// placing the prefab places the player. Its nested override still sets Cinemachine 2's m_Follow, which
        /// Cinemachine 3 ignores, so the follow camera had no target. It is now a plain first-person camera: hard
        /// locked to PlayerCameraRoot (the eye, 1.6 m up) and turning with it, in place of the zero-distance
        /// third-person follow. The main camera clears to solid black.
        /// </summary>
        static string Rig(Transform root, Transform capsule)
        {
            var notes = new System.Collections.Generic.List<string>();
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Transform c in root)
            {
                bool cam = c.name == "MainCamera" || c.name == "PlayerFollowCamera";
                c.SetLocalPositionAndRotation(cam ? new Vector3(0f, EyeHeight, 0f) : Vector3.zero, Quaternion.identity);
            }
            notes.Add("root and PlayerCapsule at the origin");

            var eye = Find(capsule, "PlayerCameraRoot");
            var follow = Find(root, "PlayerFollowCamera");
            var cmCamera = follow != null ? Component(follow, "CinemachineCamera") : null;
            if (cmCamera != null && eye != null)
            {
                var so = new SerializedObject(cmCamera);
                var tt = so.FindProperty("Target.TrackingTarget");
                if (tt != null) { tt.objectReferenceValue = eye; notes.Add("follow camera tracks PlayerCameraRoot"); }
                else notes.Add("no Target.TrackingTarget on CinemachineCamera");
                // Starter Assets' 40 degree lens is narrow indoors
                var fov = so.FindProperty("Lens.FieldOfView");
                if (fov != null) { fov.floatValue = FieldOfView; notes.Add($"vertical FOV {FieldOfView}"); }
                else notes.Add("no Lens.FieldOfView on CinemachineCamera");
                so.ApplyModifiedPropertiesWithoutUndo();

                var third = Component(follow, "CinemachineThirdPersonFollow");
                if (third != null) Object.DestroyImmediate(third);
                foreach (var name in new[] { "CinemachineHardLockToTarget", "CinemachineRotateWithFollowTarget" })
                {
                    if (Component(follow, name) != null) continue;
                    var type = System.Type.GetType("Unity.Cinemachine." + name + ", Unity.Cinemachine");
                    if (type != null) follow.gameObject.AddComponent(type);
                    else notes.Add("no type " + name);
                }
                notes.Add("first-person: HardLockToTarget + RotateWithFollowTarget");
            }
            else notes.Add("no CinemachineCamera on PlayerFollowCamera, or no PlayerCameraRoot");

            var main = Find(root, "MainCamera");
            var camera = main != null ? main.GetComponent<Camera>() : null;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                notes.Add("main camera clears to black");
            }
            else notes.Add("no Camera on MainCamera");
            return string.Join(", ", notes);
        }

        static Component Component(Transform t, string typeName)
        {
            foreach (var c in t.GetComponents<Component>())
                if (c != null && c.GetType().Name == typeName) return c;
            return null;
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
