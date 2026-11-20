using Unity.Cinemachine;
using UnityEngine;

namespace Parasite.Possession
{
    /// <summary>
    /// Third-person camera: a pivot we rotate with mouse/stick, followed by a Cinemachine camera
    /// (ThirdPersonFollow + RotateWithFollowTarget, with obstacle avoidance). Retargeting glides the pivot
    /// smoothly between bodies, which produces the possession camera transition.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerCamera : MonoBehaviour
    {
        public static PlayerCamera Instance { get; private set; }

        public Camera Cam { get; private set; }
        public float Yaw => yaw;
        public Vector3 Forward => Cam ? Cam.transform.forward : transform.forward;
        public Vector3 Position => Cam ? Cam.transform.position : transform.position;

        Transform pivot, target;
        CinemachineCamera vcam;
        CinemachineThirdPersonFollow follow;
        float yaw, pitch = 14f, distance = 4f, targetDistance = 4f;
        float transitionT = 1f, transitionDuration = 0.35f, fovKick;
        Vector3 transitionFrom;
        const float BaseFov = 58f;

        public static PlayerCamera Create()
        {
            var go = new GameObject("PlayerCamera");
            return go.AddComponent<PlayerCamera>();
        }

        void Awake()
        {
            Instance = this;
            Cam = Camera.main;
            if (!Cam)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                Cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            Cam.nearClipPlane = 0.08f;
            Cam.farClipPlane = 900f;
            var brain = Cam.GetComponent<CinemachineBrain>();
            if (!brain) brain = Cam.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.4f);
            var urp = Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (!urp) urp = Cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(transform, false);

            var vgo = new GameObject("CM PlayerCamera");
            vgo.transform.SetParent(transform, false);
            vcam = vgo.AddComponent<CinemachineCamera>();
            vcam.Target.TrackingTarget = pivot;
            var lens = vcam.Lens; lens.FieldOfView = BaseFov; lens.NearClipPlane = 0.08f; lens.FarClipPlane = 900f; vcam.Lens = lens;
            follow = vgo.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.45f, 0.15f, 0f);
            follow.VerticalArmLength = 0.25f;
            follow.CameraSide = 0.7f;
            follow.CameraDistance = distance;
            follow.Damping = new Vector3(0.1f, 0.25f, 0.2f);
            follow.AvoidObstacles.Enabled = true;
            follow.AvoidObstacles.CollisionFilter = Layers.EnvironmentMask;
            follow.AvoidObstacles.CameraRadius = 0.2f;
            follow.AvoidObstacles.DampingIntoCollision = 0.05f;
            follow.AvoidObstacles.DampingFromCollision = 0.4f;
            vgo.AddComponent<CinemachineRotateWithFollowTarget>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Follow a new body. snap = no glide.</summary>
        public void SetTarget(Transform t, float dist, bool snap = false, float glideSeconds = 0.35f)
        {
            if (!t) return;
            transitionFrom = pivot.position;
            target = t;
            targetDistance = dist;
            transitionDuration = Mathf.Max(0.01f, glideSeconds);
            transitionT = snap ? 1f : 0f;
            if (snap) { pivot.position = t.position; distance = dist; }
        }

        public void SetYaw(float y) { yaw = y; }
        public void Kick(float fovDegrees) { fovKick = Mathf.Max(fovKick, fovDegrees); }

        void Update()
        {
            bool allowLook = GameManager.Instance == null || GameManager.Instance.State is GameState.Playing or GameState.Resetting;
            if (allowLook)
            {
                var d = GameInput.LookDelta;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -25f, 65f);
            }
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (target)
            {
                if (transitionT < 1f)
                {
                    transitionT = Mathf.Min(1f, transitionT + dt / transitionDuration);
                    float e = transitionT * transitionT * (3f - 2f * transitionT);
                    pivot.position = Vector3.Lerp(transitionFrom, target.position, e);
                }
                else pivot.position = target.position;
            }
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-5f * dt));
            follow.CameraDistance = distance;
            pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 25f);
            var lens = vcam.Lens; lens.FieldOfView = BaseFov + fovKick; vcam.Lens = lens;
        }

        /// <summary>Camera-relative movement on the ground plane.</summary>
        public Vector3 ToWorldMove(Vector2 move)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            var v = q * new Vector3(move.x, 0f, move.y);
            return Vector3.ClampMagnitude(v, 1f);
        }
    }
}
