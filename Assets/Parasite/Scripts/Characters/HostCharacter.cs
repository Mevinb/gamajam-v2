using System.Collections.Generic;
using Parasite.FX;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Characters
{
    /// <summary>
    /// A human that can be possessed. Unpossessed: driven by its HostBrain through a NavMeshAgent.
    /// Possessed: driven by player input through a CharacterController. Released: unconscious for 8 s, then recovers.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HostCharacter : MonoBehaviour, IControllable
    {
        public static readonly List<HostCharacter> All = new();
        public const float UnconsciousSeconds = 8f;
        public const float JumpHeight = 1.05f;
        public const float Gravity = -22f;

        public string DisplayName = "Pedestrian";
        public Profession Profession = Profession.Civilian;
        public bool IsDrVale;

        public HostState State { get; private set; } = HostState.Normal;
        public AmoebaAgent Possessor { get; private set; }
        public HumanoidRig Rig { get; private set; }
        public HostGlow Glow { get; private set; }
        public NavMeshAgent Nav { get; private set; }
        public HostBrain Brain { get; private set; }
        public CapsuleCollider HitBox { get; private set; }
        public CharacterController Controller { get; private set; }

        public ProfessionProfile Profile => ProfessionCatalog.Get(Profession);
        public AccessPermission Permissions => Profile.permissions;
        public bool IsPossessed => State == HostState.Possessed;
        public bool IsAwakeNpc => State == HostState.Normal;
        public bool CanBePossessed => State == HostState.Normal && !IsDrVale;
        public float UnconsciousRemaining => State == HostState.Unconscious ? stateTimer : 0f;
        public int KnockoutId { get; private set; }
        public bool KnockoutDiscovered { get; set; }
        public Vector3 Velocity { get; private set; }
        public bool Grounded { get; private set; } = true;

        public Transform Root => transform;
        public Transform CameraTarget { get; private set; }
        public float CameraDistance => 4.2f;
        public Vector3 EyePosition => transform.position + Vector3.up * 1.62f;
        public Vector3 ChestPosition => transform.position + Vector3.up * 1.25f;

        ControlInput input;
        float vy, stateTimer, footstepT;
        float unconsciousDuration = UnconsciousSeconds;

        /// <summary>Called by HostFactory after components exist.</summary>
        public void Init(string displayName, Profession profession, bool isVale, int seed)
        {
            DisplayName = displayName; Profession = profession; IsDrVale = isVale;
            gameObject.layer = Layers.Characters;

            Controller = GetComponent<CharacterController>();
            Controller.radius = 0.3f; Controller.height = 1.8f; Controller.center = new Vector3(0, 0.9f, 0);
            Controller.stepOffset = 0.4f; Controller.slopeLimit = 50f; Controller.skinWidth = 0.04f;
            Controller.enabled = false;

            var hb = new GameObject("HitBox") { layer = Layers.Characters };
            hb.transform.SetParent(transform, false);
            HitBox = hb.AddComponent<CapsuleCollider>();
            HitBox.radius = 0.3f; HitBox.height = 1.8f; HitBox.center = new Vector3(0, 0.9f, 0);

            CameraTarget = new GameObject("CameraTarget").transform;
            CameraTarget.SetParent(transform, false);
            CameraTarget.localPosition = new Vector3(0, 1.55f, 0);

            Rig = gameObject.AddComponent<HumanoidRig>();
            Rig.Identity = displayName; // named NPCs get their own authored variant
            Rig.Build(profession, seed, isVale);
            Glow = gameObject.AddComponent<HostGlow>();
            Glow.Init(Rig.UniqueMaterials);

            Nav = GetComponent<NavMeshAgent>();
            if (Nav)
            {
                Nav.radius = 0.3f; Nav.height = 1.8f; Nav.speed = Profile.walkSpeed;
                Nav.angularSpeed = 540f; Nav.acceleration = 12f; Nav.stoppingDistance = 0.25f;
                Nav.autoBraking = true;
                Nav.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                Nav.avoidancePriority = 30 + (seed % 40);
            }
            Brain = GetComponent<HostBrain>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // ---------- IControllable ----------
        public void SetControlInput(in ControlInput i) => input = i;
        public void ClearControlInput() => input = default;

        // ---------- state transitions ----------
        public void BeginPossession(AmoebaAgent agent)
        {
            Possessor = agent;
            State = HostState.Possessed;
            SetNavMode(false);
            vy = 0f;
            Glow.SetPossessed(true);
            Rig.SetEyesGlow(true);
            Brain?.OnPossessed();
            RunState.HostsPossessed++;
        }

        /// <summary>Amoeba leaves voluntarily or via eject: host collapses for 8 s.</summary>
        public void EndPossession()
        {
            Possessor = null;
            Glow.SetPossessed(false);
            Glow.SetCaptured(false);
            Rig.SetEyesGlow(false);
            KnockOut(UnconsciousSeconds);
        }

        public void KnockOut(float seconds)
        {
            State = HostState.Unconscious;
            unconsciousDuration = seconds;
            stateTimer = seconds;
            KnockoutId++;
            KnockoutDiscovered = false;
            SetNavMode(false, keepControllerOff: true);
            Velocity = Vector3.zero;
            Glow.SetUnconscious(true);
            Sfx.Play(SfxId.Release, transform.position);
        }

        /// <summary>A guard grabbed this possessed host. The amoeba must eject.</summary>
        public void Capture()
        {
            if (State != HostState.Possessed) return;
            State = HostState.Captured;
            Velocity = Vector3.zero;
            input = default;
            SetController(false);
            Glow.SetCaptured(true);
            Sfx.Play(SfxId.Capture, transform.position);
        }

        void Recover()
        {
            State = HostState.Normal;
            Glow.SetUnconscious(false);
            SetNavMode(true);
            Brain?.OnRecovered();
        }

        void SetNavMode(bool nav, bool keepControllerOff = false)
        {
            if (nav)
            {
                SetController(false);
                if (Nav)
                {
                    Nav.enabled = true;
                    if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas)) Nav.Warp(hit.position);
                    if (Nav.isOnNavMesh) Nav.isStopped = false;
                }
            }
            else
            {
                if (Nav && Nav.enabled) { if (Nav.isOnNavMesh) Nav.ResetPath(); Nav.enabled = false; }
                SetController(!keepControllerOff);
            }
        }

        /// <summary>Exactly one of CharacterController (player-driven) or HitBox (AI/idle) is active, so they never collide.</summary>
        void SetController(bool on)
        {
            Controller.enabled = on;
            HitBox.enabled = !on;
        }

        // ---------- update ----------
        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            float dt = Time.deltaTime;
            switch (State)
            {
                case HostState.Possessed: PlayerLocomotion(dt); break;
                case HostState.Normal:
                    Velocity = Nav && Nav.enabled ? Nav.velocity : Vector3.zero;
                    Grounded = true;
                    Brain?.Tick(dt);
                    break;
                case HostState.Unconscious:
                    stateTimer -= dt;
                    Velocity = Vector3.zero;
                    if (stateTimer <= 0f) Recover();
                    break;
                case HostState.Captured:
                    Velocity = Vector3.zero;
                    break;
            }
            if (gm && transform.position.y < GameManager.FallRecoveryY) gm.TryRecoverOwnedFall(this);
            Rig.Animate(Velocity, Grounded, State, Profile.runSpeed, dt);
            Footsteps(dt);
        }

        void PlayerLocomotion(float dt)
        {
            if (!Controller.enabled) return;
            var p = Profile;
            float speed = input.sprint ? p.runSpeed : p.walkSpeed * 1.35f;
            Vector3 horiz = input.worldMove * speed;
            Grounded = Controller.isGrounded;
            if (Grounded && vy < 0f) vy = -2f;
            if (Grounded && input.jump) { vy = Mathf.Sqrt(-2f * Gravity * JumpHeight); Sfx.Play(SfxId.Jump, transform.position, 0.6f); }
            input.jump = false;
            vy += Gravity * dt;
            Controller.Move((horiz + Vector3.up * vy) * dt);
            Velocity = new Vector3(Controller.velocity.x, 0, Controller.velocity.z);
            if (horiz.sqrMagnitude > 0.01f)
            {
                var targetRot = Quaternion.LookRotation(new Vector3(horiz.x, 0, horiz.z));
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-12f * dt));
            }
            if (!GameManager.Instance && transform.position.y < GameManager.FallRecoveryY)
            { Controller.enabled = false; transform.position = new Vector3(transform.position.x, 2f, transform.position.z); Controller.enabled = true; }
        }

        void Footsteps(float dt)
        {
            float s = Velocity.magnitude;
            if (s < 0.5f || !Grounded) return;
            footstepT -= dt * s;
            if (footstepT <= 0f) { footstepT = 1.4f; if (IsPossessed) Sfx.Play(SfxId.Footstep, transform.position, 0.35f); }
        }

        public float UnconsciousProgress01 => State == HostState.Unconscious ? 1f - stateTimer / Mathf.Max(0.01f, unconsciousDuration) : 0f;
    }
}
