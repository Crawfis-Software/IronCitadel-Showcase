using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace IronCitadel.EditorTools
{
    /// <summary>Bakes one frame of a humanoid clip into a character's bones, so a static marker stands naturally.</summary>
    public static class Poser
    {
        public const string IdleClipPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/Stand--Idle.anim.fbx";
        static AnimationClip _idle;

        public static AnimationClip Idle
        {
            get
            {
                if (_idle == null)
                    foreach (var o in AssetDatabase.LoadAllAssetsAtPath(IdleClipPath))
                        if (o is AnimationClip c && !c.name.StartsWith("__preview")) { _idle = c; break; }
                return _idle;
            }
        }

        /// <summary>Reads the clip's humanoid muscle curves at a time and applies them with HumanPoseHandler.</summary>
        public static float SitHip = 0.9f, SitKnee = -0.95f, SitBody = -0.45f;

        public static bool Pose(GameObject character, float time = 0.6f, float armsDown = 0f, bool seated = false)
        {
            var anim = character.GetComponentInChildren<Animator>();
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman || Idle == null) return false;
            var root = anim.transform;
            // pose at the origin (the handler works in world space), then put the character back
            var top = character.transform;
            Vector3 p = top.position; Quaternion r = top.rotation;
            top.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var handler = new HumanPoseHandler(anim.avatar, root);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            var names = HumanTrait.MuscleName;
            var index = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < names.Length; i++) index[names[i]] = i;
            foreach (var b in AnimationUtility.GetCurveBindings(Idle))
            {
                string n = b.propertyName;
                if (index.TryGetValue(n, out int mi) || index.TryGetValue(n.Replace("LeftHand.", "Left ").Replace("RightHand.", "Right ").Replace(".", " "), out mi))
                {
                    var curve = AnimationUtility.GetEditorCurve(Idle, b);
                    pose.muscles[mi] = curve.Evaluate(time);
                }
            }
            if (armsDown != 0f)
            {
                if (index.TryGetValue("Left Arm Down-Up", out int la)) pose.muscles[la] += armsDown;
                if (index.TryGetValue("Right Arm Down-Up", out int ra)) pose.muscles[ra] += armsDown;
            }
            if (seated)
            {
                foreach (var side in new[] { "Left", "Right" })
                {
                    if (index.TryGetValue(side + " Upper Leg Front-Back", out int ul)) pose.muscles[ul] = SitHip;
                    if (index.TryGetValue(side + " Lower Leg Stretch", out int ll)) pose.muscles[ll] = SitKnee;
                    if (index.TryGetValue(side + " Upper Leg In-Out", out int io)) pose.muscles[io] = 0.1f;
                    if (index.TryGetValue(side + " Arm Front-Back", out int af)) pose.muscles[af] = 0.45f;
                    if (index.TryGetValue(side + " Forearm Stretch", out int fs)) pose.muscles[fs] = 0.1f;
                }
                pose.bodyPosition += Vector3.up * SitBody;
            }
            handler.SetHumanPose(ref pose);
            handler.Dispose();
            top.SetPositionAndRotation(p, r);
            anim.enabled = false; // the pose stays in the bones; nothing moves
            return true;
        }

        public static string DebugCurves()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var b in AnimationUtility.GetCurveBindings(Idle)) sb.Append(b.propertyName).Append("; ");
            return sb.ToString();
        }
    }
}
