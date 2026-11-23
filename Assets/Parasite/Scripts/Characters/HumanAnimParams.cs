using UnityEngine;

namespace Parasite.Characters
{
    /// <summary>
    /// CONTRACT between the skinned character models (HumanoidRig) and the humanoid AnimatorController
    /// (Resources/Characters/HumanLocomotion.controller). Add members freely; never rename existing ones.
    /// </summary>
    public static class HumanAnimParams
    {
        /// <summary>Resources path of the shared humanoid AnimatorController.</summary>
        public const string ControllerResource = "Characters/HumanLocomotion";

        /// <summary>float — horizontal speed in m/s (0 idle, ~WalkClipSpeed walk, ~RunClipSpeed run).</summary>
        public static readonly int Speed = Animator.StringToHash("Speed");
        /// <summary>float — playback multiplier for the locomotion state so feet do not slide (default 1).</summary>
        public static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
        /// <summary>bool — false plays the airborne/fall loop; true again plays the landing.</summary>
        public static readonly int Grounded = Animator.StringToHash("Grounded");
        /// <summary>int — (int)HostState: 0 Normal, 1 Possessed, 2 Unconscious (collapse + lie + get up), 3 Captured (kneel).</summary>
        public static readonly int State = Animator.StringToHash("State");
        /// <summary>int — (int)ActorPose, a held full-body pose layered over Normal state.</summary>
        public static readonly int Pose = Animator.StringToHash("Pose");
        /// <summary>float 0..1 — per-character variety (idle/gait variant selection).</summary>
        public static readonly int Variant = Animator.StringToHash("Variant");
        /// <summary>int — (int)Profession, for profession-specific idles/gait styles.</summary>
        public static readonly int Profession = Animator.StringToHash("Profession");
        /// <summary>trigger — one-shot talking/pointing gesture.</summary>
        public static readonly int Gesture = Animator.StringToHash("Gesture");

        /// <summary>Natural in-place speed of the Walk clip in m/s (kept in sync with the clip by the animation pipeline).</summary>
        public const float WalkClipSpeed = 2.02f;
        /// <summary>Natural in-place speed of the Run clip in m/s.</summary>
        public const float RunClipSpeed = 5.36f;

        // ---- appended by the animation pipeline (Tools/blender/animation, Editor/HumanAnimatorBuilder) ----

        /// <summary>Animator.humanScale of the reference skeleton the clip speeds above were measured on.
        /// Natural speeds scale with Animator.humanScale / ReferenceHumanScale on other avatars.</summary>
        public const float ReferenceHumanScale = 1.027f;
        /// <summary>Speed (m/s) above which the Idle states hand over to the Move blend tree.</summary>
        public const float MoveEnterSpeed = 0.2f;
        /// <summary>Speed (m/s) below which the Move blend tree returns to the Idle states.</summary>
        public const float MoveExitSpeed = 0.15f;
        /// <summary>Held poses (except Sit) are only entered below this speed and released above PoseBreakSpeed.</summary>
        public const float PoseMaxSpeed = 0.5f;
        public const float PoseBreakSpeed = 0.6f;

        /// <summary>Lengths (s) of the one-shot state clips, for gameplay timing (e.g. keep the NavMeshAgent stopped
        /// for GetUpSeconds after State returns from Unconscious to Normal).</summary>
        public const float CollapseSeconds = 2.4f;
        public const float GetUpSeconds = 2.1f;
        public const float CapturedKneelSeconds = 1.4f;

        /// <summary>Seated pose geometry (character local space, metres, measured in Unity on the reference skeleton,
        /// scales with Animator.humanScale): Hips bone height and how far behind the character's origin it sits.
        /// The thighs rest on a seat surface about SitSeatHeight high whose front edge is near the origin; feet are
        /// planted ~0.15 m in front of the origin. Put the character's origin at the front edge of the bench/ledge.</summary>
        public const float SitHipsHeight = 0.58f;
        public const float SitHipsBack = 0.33f;

        /// <summary>The Unconscious lying pose lies on its back along local -Z: toes about +0.25 m in front of the
        /// origin, hips about 0.51 m and the top of the head about 1.3 m behind it (LyingLength ~1.55 m in total).</summary>
        public const float LyingLength = 1.55f;
        /// <summary>Approximate seat surface height for SitHipsHeight (thigh underside).</summary>
        public const float SitSeatHeight = 0.47f;

        /// <summary>Full-path state names for Animator.Play (e.g. start the lakeside girl already seated).</summary>
        public const string StateSitLoop = "Base Layer.Sit_Loop";
        public const string StateUnconsciousLoop = "Base Layer.Unconscious_Loop";
        public const string StateCapturedLoop = "Base Layer.Captured_Loop";
        public const string StateIdleCivilian = "Base Layer.Idle_Civilian";

        /// <summary>Recommended LocomotionRate so the feet do not slide: speed in m/s, humanScale = Animator.humanScale.
        /// Below walk speed the walk slows down (min 0.5x), between walk and run the blend tree matches speed by itself,
        /// above run speed the run speeds up.</summary>
        public static float LocomotionRateFor(float speed, float humanScale)
        {
            float s = humanScale > 0.01f ? humanScale / ReferenceHumanScale : 1f;
            float r = speed < WalkClipSpeed ? Mathf.Max(0.5f, speed / WalkClipSpeed)
                : speed > RunClipSpeed ? speed / RunClipSpeed : 1f;
            return r / s;
        }
    }

    /// <summary>Held poses selected with <see cref="HumanAnimParams.Pose"/>.</summary>
    public enum ActorPose { None = 0, Sit = 1, Talk = 2, UseConsole = 3, Panic = 4, Alert = 5, Point = 6 }
}
