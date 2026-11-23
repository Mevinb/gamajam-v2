using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Characters
{
    /// <summary>
    /// Skinned, textured human (MPFB-generated, see Tools/blender/characters) chosen deterministically from
    /// Resources/Characters/CharacterCatalog by display name / Dr. Vale / profession + seed. Animation runs through the
    /// shared humanoid AnimatorController (HumanAnimParams contract); if that controller is missing a procedural
    /// bone-driven fallback (ProceduralHumanPose) keeps every state readable. Head look-at and the possessed head twitch
    /// are applied after animation in LateUpdate.
    /// </summary>
    public class HumanoidRig : MonoBehaviour
    {
        public Transform Model { get; private set; }
        public Transform Head { get; private set; }
        public Renderer[] Renderers { get; private set; }
        public Material[] UniqueMaterials { get; private set; }

        /// <summary>Optional display name (set before Build) that selects a named variant, e.g. "Dr. Hana Petrov".</summary>
        public string Identity { get; set; }
        /// <summary>Optional catalog id (set before Build) that forces a specific variant (tools / previews).</summary>
        public string VariantOverride { get; set; }
        /// <summary>Catalog id of the built variant (empty for the primitive emergency fallback).</summary>
        public string VariantId { get; private set; } = "";
        public Animator Animator { get; private set; }
        /// <summary>True when the humanoid AnimatorController drives the body (false = procedural fallback).</summary>
        public bool UsesAnimator => controllerOk;
        public ActorPose Pose { get; private set; } = ActorPose.None;
        /// <summary>Occasional talk gestures while standing idle (Animator only).</summary>
        public bool AmbientGestures { get; set; } = true;

        /// <summary>Story sequences run at timeScale 0: animate actors with unscaled time.</summary>
        public bool UseUnscaledTime
        {
            get => unscaled;
            set
            {
                unscaled = value;
                if (Animator)
                {
                    Animator.updateMode = value ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
                    if (value) Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
            }
        }

        static readonly int IdEmission = Shader.PropertyToID("_EmissionColor");
        static readonly int IdVein = Shader.PropertyToID("_VeinColor");
        static readonly int IdTintA = Shader.PropertyToID("_TintA");
        static readonly int IdTintB = Shader.PropertyToID("_TintB");
        static readonly int IdTintC = Shader.PropertyToID("_TintC");

        readonly List<Material> owned = new();
        Material skinMat, eyeMat, clothMat;
        bool eyesGlowOn, unscaled, controllerOk;
        bool hasSpeed, hasRate, hasGrounded, hasState, hasPose, hasVariant, hasProfession, hasGesture;
        ProceduralHumanPose fallback;
        Transform neck;
        Quaternion neckLook = Quaternion.identity, headLook = Quaternion.identity;
        Vector3 lookDir;
        bool hasLook;
        HostState lastState;
        float speedSmoothed, twitchT, gestureT, lastDt = 1f / 60f, phaseSeed, variant01;
        Profession profession;

        // ------------------------------------------------------------------ build

        public void Build(Profession profession, int seed, bool isDrVale)
        {
            this.profession = profession;
            var rnd = new System.Random(seed * 7919 + (int)profession * 31);
            phaseSeed = (float)rnd.NextDouble() * 10f;
            variant01 = (float)rnd.NextDouble();
            gestureT = 6f + (float)rnd.NextDouble() * 14f;

            var catalog = CharacterCatalog.Load();
            CharacterCatalog.Entry entry = null;
            if (catalog)
            {
                if (!string.IsNullOrEmpty(VariantOverride)) entry = catalog.FindById(VariantOverride);
                if (entry == null) entry = catalog.FindByName(Identity);
                if (entry == null && isDrVale) entry = catalog.FindById("DrVale");
                if (entry == null) entry = catalog.Pick(profession, seed);
            }
            if (entry == null || !entry.prefab)
            {
                BuildEmergencyBody(profession, rnd);
                return;
            }
            bool named = entry.unique || (!string.IsNullOrEmpty(Identity) && catalog.FindByName(Identity) == entry)
                         || (!string.IsNullOrEmpty(VariantOverride) && VariantOverride == entry.id);
            VariantId = entry.id;

            var go = Instantiate(entry.prefab, transform, false);
            go.name = "Model";
            Model = go.transform;
            Model.localPosition = Vector3.zero;
            Model.localRotation = Quaternion.identity;
            if (!named) Model.localScale = Vector3.one * (0.97f + (float)rnd.NextDouble() * 0.06f);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;

            Animator = go.GetComponent<Animator>();
            if (!Animator) Animator = go.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            Animator.cullingMode = (named || unscaled || Application.isBatchMode) ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
            Animator.updateMode = unscaled ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
            var controller = Resources.Load<RuntimeAnimatorController>(HumanAnimParams.ControllerResource);
            if (controller && Animator.avatar && Animator.avatar.isHuman)
            {
                Animator.runtimeAnimatorController = controller;
                controllerOk = true;
                foreach (var p in Animator.parameters)
                {
                    if (p.nameHash == HumanAnimParams.Speed) hasSpeed = true;
                    else if (p.nameHash == HumanAnimParams.LocomotionRate) hasRate = true;
                    else if (p.nameHash == HumanAnimParams.Grounded) hasGrounded = true;
                    else if (p.nameHash == HumanAnimParams.State) hasState = true;
                    else if (p.nameHash == HumanAnimParams.Pose) hasPose = true;
                    else if (p.nameHash == HumanAnimParams.Variant) hasVariant = true;
                    else if (p.nameHash == HumanAnimParams.Profession) hasProfession = true;
                    else if (p.nameHash == HumanAnimParams.Gesture) hasGesture = true;
                }
                if (hasVariant) Animator.SetFloat(HumanAnimParams.Variant, variant01);
                if (hasProfession) Animator.SetInteger(HumanAnimParams.Profession, (int)profession);
                if (hasGrounded) Animator.SetBool(HumanAnimParams.Grounded, true);
                if (hasRate) Animator.SetFloat(HumanAnimParams.LocomotionRate, 1f);
            }
            else
            {
                Animator.runtimeAnimatorController = null;
                fallback = new ProceduralHumanPose(Model, Animator, phaseSeed);
            }

            Head = Animator.isHuman ? Animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!Head) Head = FindDeep(Model, "Head");
            if (!Head) Head = Model;
            neck = Animator.isHuman ? Animator.GetBoneTransform(HumanBodyBones.Neck) : null;

            // per-instance materials (glow + tints are per character)
            var map = new Dictionary<Material, Material>();
            var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in renderers)
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var src = shared[i];
                    if (!src) continue;
                    if (!map.TryGetValue(src, out var clone))
                    {
                        clone = new Material(src) { name = src.name + " (instance)" };
                        map[src] = clone;
                        owned.Add(clone);
                        string n = src.name;
                        if (n.EndsWith("_Skin")) skinMat = clone;
                        else if (n.EndsWith("_Eyes")) eyeMat = clone;
                        else if (n.EndsWith("_Cloth")) clothMat = clone;
                    }
                    shared[i] = clone;
                }
                r.sharedMaterials = shared;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.updateWhenOffscreen = false;
            }
            if (clothMat && !named) ApplyRandomTints(entry, rnd);
            Renderers = renderers;
            var glow = new List<Material>();
            foreach (var m in owned) if (m && m != eyeMat) glow.Add(m);
            UniqueMaterials = glow.ToArray();
            ApplyGlowState();
        }

        void ApplyRandomTints(CharacterCatalog.Entry e, System.Random rnd)
        {
            // keep the authored look for ~1 in 4 anonymous instances, otherwise draw garment colours from the palettes
            if (rnd.NextDouble() < 0.25) return;
            if (e.hasTintA && CharacterPalettes.TryPick(e.paletteA, rnd, out var a)) clothMat.SetColor(IdTintA, a.gamma);
            if (e.hasTintB && CharacterPalettes.TryPick(e.paletteB, rnd, out var b)) clothMat.SetColor(IdTintB, b.gamma);
            if (e.hasTintC && CharacterPalettes.TryPick(e.paletteC, rnd, out var c)) clothMat.SetColor(IdTintC, c.gamma);
        }

        /// <summary>Only used if the character catalog is missing: a plain capsule body so gameplay keeps working.</summary>
        void BuildEmergencyBody(Profession p, System.Random rnd)
        {
            Model = new GameObject("Model").transform;
            Model.SetParent(transform, false);
            Color body = p switch
            {
                Profession.Guard => new Color(0.16f, 0.22f, 0.40f), Profession.Scientist => new Color(0.92f, 0.92f, 0.92f),
                Profession.Maintenance => new Color(0.95f, 0.5f, 0.1f), _ => new Color(0.35f, 0.4f, 0.5f),
            };
            var bodyMat = MatFactory.LitUnique(body, 0.3f);
            skinMat = MatFactory.LitUnique(new Color(0.85f, 0.66f, 0.52f), 0.4f);
            eyeMat = MatFactory.LitUnique(new Color(0.05f, 0.05f, 0.06f), 0.9f);
            owned.Add(bodyMat); owned.Add(skinMat); owned.Add(eyeMat);
            Prim.Make(PrimitiveType.Capsule, Model, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.75f, 0.3f), bodyMat);
            Head = new GameObject("Head").transform;
            Head.SetParent(Model, false);
            Head.localPosition = new Vector3(0, 1.62f, 0);
            Prim.Make(PrimitiveType.Sphere, Head, Vector3.zero, new Vector3(0.22f, 0.26f, 0.24f), skinMat);
            foreach (float x in new[] { -0.045f, 0.045f }) Prim.Make(PrimitiveType.Sphere, Head, new Vector3(x, 0.02f, 0.105f), Vector3.one * 0.03f, eyeMat);
            Renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in Renderers) r.gameObject.layer = gameObject.layer;
            UniqueMaterials = new[] { bodyMat, skinMat };
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ glow

        public void SetEyesGlow(bool on)
        {
            eyesGlowOn = on;
            ApplyGlowState();
        }

        void ApplyGlowState(float pulse = 1f)
        {
            Color eyes = eyesGlowOn ? Palette.Amoeba * 4f : Color.black;
            Color veins = eyesGlowOn ? Palette.Amoeba * 2.8f * pulse : Color.black;
            if (eyeMat) eyeMat.SetColor(IdEmission, eyes);
            if (skinMat) skinMat.SetColor(IdVein, veins);
        }

        void OnDestroy()
        {
            foreach (var m in owned)
            {
                if (!m) continue;
                if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
            owned.Clear();
        }

        // ------------------------------------------------------------------ animation API

        /// <summary>World-space point for the head to look toward (null clears).</summary>
        public void LookAt(Vector3? worldPoint)
        {
            hasLook = worldPoint.HasValue;
            if (hasLook) lookDir = worldPoint.Value - (Head ? Head.position : transform.position);
        }

        /// <summary>Hold a full-body pose (sit, talk, point...) layered over the Normal state.</summary>
        public void SetPose(ActorPose pose)
        {
            Pose = pose;
            if (controllerOk && hasPose) Animator.SetInteger(HumanAnimParams.Pose, (int)pose);
        }

        /// <summary>One-shot talk/point gesture (Animator only).</summary>
        public void PlayGesture()
        {
            if (controllerOk && hasGesture) Animator.SetTrigger(HumanAnimParams.Gesture);
        }

        /// <summary>Advance the body pose immediately (e.g. settle into a held Sit pose before the first rendered frame).</summary>
        public void FastForward(float seconds, float step = 0.1f)
        {
            if (!Model) return;
            if (controllerOk && Animator)
            {
                var prev = Animator.cullingMode;
                Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Animator.Update(0f);
                for (float t = 0f; t < seconds; t += step) Animator.Update(step);
                Animator.cullingMode = prev;
            }
            else
            {
                for (float t = 0f; t < seconds; t += step) Animate(Vector3.zero, true, lastState, 4.6f, step);
            }
        }

        /// <summary>Drive the animation. velocity in world space.</summary>
        public void Animate(Vector3 velocity, bool grounded, HostState state, float runSpeed, float dt)
        {
            if (!Model) return;
            lastDt = dt;
            lastState = state;
            float target = new Vector3(velocity.x, 0, velocity.z).magnitude;
            speedSmoothed = Mathf.Lerp(speedSmoothed, target, 1f - Mathf.Exp(-12f * dt));
            if (controllerOk)
            {
                float s = speedSmoothed < 0.05f ? 0f : speedSmoothed;
                if (hasSpeed) Animator.SetFloat(HumanAnimParams.Speed, s);
                if (hasRate) Animator.SetFloat(HumanAnimParams.LocomotionRate, s > HumanAnimParams.RunClipSpeed ? Mathf.Clamp(s / HumanAnimParams.RunClipSpeed, 1f, 1.5f) : 1f);
                if (hasGrounded) Animator.SetBool(HumanAnimParams.Grounded, grounded);
                if (hasState) Animator.SetInteger(HumanAnimParams.State, (int)state);
                if (AmbientGestures && hasGesture && state == HostState.Normal && Pose == ActorPose.None && s < 0.1f && profession != Profession.Guard)
                {
                    gestureT -= dt;
                    if (gestureT <= 0f) { Animator.SetTrigger(HumanAnimParams.Gesture); gestureT = 12f + Random.value * 16f; }
                }
            }
            else if (fallback != null && fallback.Valid)
            {
                float t = unscaled ? Time.unscaledTime : Time.time;
                float lie = fallback.Evaluate(velocity, grounded, state, Pose, runSpeed, dt, t);
                // lying down is a whole-body tilt only in the procedural fallback (the Animator has its own collapse clips)
                Model.localRotation = Quaternion.Euler(-90f * lie, 0, 0);
                Model.localPosition = new Vector3(0, 0.12f * lie, 0);
            }

            if (state == HostState.Possessed)
            {
                twitchT -= dt;
                if (twitchT < -0.18f) twitchT = Random.Range(2.5f, 6f);
                if (eyesGlowOn) ApplyGlowState(0.85f + 0.3f * Mathf.Abs(Mathf.Sin((unscaled ? Time.unscaledTime : Time.time) * 2.4f)));
            }
        }

        bool AnyVisible()
        {
            if (Application.isBatchMode) return true;
            if (Renderers == null) return false;
            foreach (var r in Renderers) if (r && r.isVisible) return true;
            return false;
        }

        void LateUpdate()
        {
            if (!Head || !neck) return;
            // With CullUpdateTransforms the Animator stops writing bones while invisible; additive tweaks would then
            // accumulate, so only apply them when the Animator actually posed the skeleton this frame.
            if (controllerOk && !AnyVisible()) return;
            float dt = Mathf.Max(lastDt, 0f);
            Quaternion headTarget = Quaternion.identity;
            if (hasLook && lookDir.sqrMagnitude > 0.01f && lastState == HostState.Normal && Pose != ActorPose.Sit)
            {
                var forward = Model.forward;
                var local = Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up)) * Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                var e = local.eulerAngles;
                float yaw = Mathf.Clamp(Mathf.DeltaAngle(0, e.y), -70f, 70f);
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, e.x), -30f, 30f);
                headTarget = Quaternion.Euler(pitch, yaw, 0);
            }
            float k = 1f - Mathf.Exp(-8f * dt);
            neckLook = Quaternion.Slerp(neckLook, Quaternion.Slerp(Quaternion.identity, headTarget, 0.4f), k);
            headLook = Quaternion.Slerp(headLook, Quaternion.Slerp(Quaternion.identity, headTarget, 0.6f), k);
            ApplyModelSpace(neck, neckLook);
            var twitch = Quaternion.identity;
            if (lastState == HostState.Possessed && twitchT < 0f)
                twitch = Quaternion.AngleAxis(25f * Mathf.Sin((unscaled ? Time.unscaledTime : Time.time) * 60f), Vector3.forward);
            ApplyModelSpace(Head, headLook * twitch);
        }

        /// <summary>Pre-rotate a bone by a rotation expressed in the model's axes (keeps the animated pose underneath).</summary>
        void ApplyModelSpace(Transform bone, Quaternion delta)
        {
            if (delta == Quaternion.identity) return;
            var m = Model.rotation;
            bone.rotation = m * delta * Quaternion.Inverse(m) * bone.rotation;
        }
    }
}
