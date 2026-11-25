using Parasite.FX;
using UnityEngine;

namespace Parasite.Possession
{
    /// <summary>One "amoeba slot". Either free (blob body active) or inside a host.</summary>
    public class AmoebaAgent
    {
        public int Id;                       // 1 or 2, shown in the HUD
        public AmoebaController Blob;
        public Characters.HostCharacter Host;
        public bool InTransit;
        public bool CapturedPendingEject;
        public float CaptureTimer;

        public bool IsFree => Host == null && !InTransit;
        public IControllable Body => Host != null ? Host : (IControllable)Blob;
        public Vector3 Position => Host != null ? Host.transform.position : Blob ? Blob.transform.position : Vector3.zero;
        public string Describe() => Host != null ? $"{Host.DisplayName} — {Host.Profile.title}" : InTransit ? "Leaping…" : "Free amoeba";
    }

    /// <summary>Vulnerable free form: small, fast, hops. Visible to NPCs (raises suspicion).</summary>
    [RequireComponent(typeof(CharacterController))]
    public class AmoebaController : MonoBehaviour, IControllable
    {
        public const float Radius = 0.32f;
        public float walkSpeed = 3.8f, sprintSpeed = 6.4f, hopHeight = 0.55f, gravity = -20f;

        public AmoebaAgent Agent { get; set; }
        public AmoebaVisual Visual { get; private set; }
        public CharacterController Controller { get; private set; }
        public Transform Root => transform;
        public Transform CameraTarget { get; private set; }
        public float CameraDistance => 3.3f;
        public Vector3 EyePosition => transform.position + Vector3.up * 0.45f;
        public Vector3 Center => transform.position + Vector3.up * Radius;
        public Vector3 Velocity { get; private set; }

        ControlInput input;
        float vy;
        bool wasGrounded = true;

        public static AmoebaController Create(Vector3 position, AmoebaAgent agent)
        {
            var go = new GameObject($"Amoeba_{agent.Id}") { layer = Layers.Amoeba };
            go.transform.position = position;
            var cc = go.AddComponent<CharacterController>();
            cc.radius = Radius; cc.height = Radius * 2f; cc.center = new Vector3(0, Radius, 0);
            cc.stepOffset = 0.25f; cc.slopeLimit = 55f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;
            var a = go.AddComponent<AmoebaController>();
            a.Agent = agent;
            agent.Blob = a;
            return a;
        }

        void Awake()
        {
            Controller = GetComponent<CharacterController>();
            CameraTarget = new GameObject("CameraTarget").transform;
            CameraTarget.SetParent(transform, false);
            CameraTarget.localPosition = new Vector3(0, 0.6f, 0);
            Visual = gameObject.AddComponent<AmoebaVisual>();
            Visual.Build(Radius);
        }

        public void SetControlInput(in ControlInput i) => input = i;
        public void ClearControlInput() => input = default;

        /// <summary>Teleport safely (CharacterController must be disabled to move).</summary>
        public void Place(Vector3 position)
        {
            Controller.enabled = false;
            transform.position = position;
            Controller.enabled = true;
            vy = 0f;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            float dt = Time.deltaTime;
            if (!Controller.enabled) return;
            float speed = input.sprint ? sprintSpeed : walkSpeed;
            Vector3 horiz = input.worldMove * speed;
            bool grounded = Controller.isGrounded;
            if (grounded && vy < 0f) vy = -2f;
            if (grounded && input.jump)
            {
                vy = Mathf.Sqrt(-2f * gravity * hopHeight);
                Visual.Squash(1f);
                Sfx.Play(SfxId.BlobHop, transform.position, 0.6f);
            }
            input.jump = false;
            vy += gravity * dt;
            Controller.Move((horiz + Vector3.up * vy) * dt);
            Velocity = Controller.velocity;
            if (!wasGrounded && grounded) Visual.Squash(0.6f);
            wasGrounded = grounded;
            if (horiz.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(horiz), 1f - Mathf.Exp(-10f * dt));
            Visual.SetMotion(Velocity, grounded);
            if (transform.position.y < GameManager.FallRecoveryY)
            {
                if (gm) gm.TryRecoverOwnedFall(this);
                else Place(new Vector3(transform.position.x, 2f, transform.position.z)); // standalone/unit fixture fallback
            }
        }
    }
}
