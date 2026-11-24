using System;
using System.Collections;
using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.UI;
using Parasite.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Parasite
{
    /// <summary>A new-game-only, in-engine backstory. Historical actors are visual stand-ins, never gameplay hosts.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class OpeningCinematic : MonoBehaviour
    {
        public const float Duration = 80f;
        public static OpeningCinematic Instance { get; private set; }
        public static string LastExitReason { get; private set; } = "";
        public bool Running { get; private set; }
        public float ElapsedSeconds => elapsed;
        /// <summary>Preview/test clock; normal play always starts at 1. Does not alter world time.</summary>
        public float PlaybackSpeed { get; set; } = 1f;
        static bool requested;

        GameManager manager;
        PossessionManager possession;
        PlayerCamera playerCamera;
        CinemachineBrain brain;
        Camera cam;
        Canvas hudCanvas, overlay;
        Image fade;
        Text speaker, caption, skip, title, location;
        Transform stage, lab, duct, subject, lobeA, lobeB, latch;
        HumanoidRig petrov, vale, worker;
        Material statusMaterial;
        CinematicFilm film;
        readonly List<Material> owned = new();
        bool oldPlayerEnabled, oldBrainEnabled, oldHudEnabled, oldInputLock;
        bool capturedState, cleaned, finishing, skipArmed;
        Vector3 oldCameraPosition;
        Quaternion oldCameraRotation;
        float oldFov, oldNear, oldFar, oldAmbienceGain, elapsed, realElapsed;
        CameraClearFlags oldClearFlags;
        Color oldBackground;
        int shot = -1, nextCaption, nextSound;
        Vector3 initialPlayerPosition;
        static readonly Vector3 StageOrigin = new(0f, -1000f, 0f);

        readonly struct CaptionCue
        {
            public readonly float at;
            public readonly string who, text;
            public CaptionCue(float at, string who, string text) { this.at = at; this.who = who; this.text = text; }
        }
        static readonly CaptionCue[] Captions =
        {
            new(0.5f, "SPECIMEN 7 · MEMORY", "Before faces, there was a rhythm."),
            new(5f, "", ""), new(6f, "DR. HANA PETROV", "Four taps. Then we begin."),
            new(11f, "", ""), new(16f, "DR. HANA PETROV", "You remembered."),
            new(19.5f, "DR. VALE", "It anticipated a stimulus."),
            new(22f, "DR. HANA PETROV", "It knew I was here."),
            new(26.8f, "SPECIMEN 7 · MEMORY", "Two places. The same remembering."),
            new(33f, "", ""), new(34.8f, "MAINTENANCE WORKER", "That wasn't me."),
            new(37.4f, "DR. HANA PETROV", "End contact."),
            new(39f, "SPECIMEN 7 · MEMORY", "A hand that wasn't mine. A heart that was."),
            new(44f, "DR. HANA PETROV", "Seven. Stay with the rhythm."),
            new(50f, "", ""), new(53f, "DR. HANA PETROV", "Who released the interlock?"),
            new(56.5f, "SPECIMEN 7 · MEMORY", "Then the wall stopped."),
            new(59f, "", ""), new(67.6f, "SPECIMEN 7 · MEMORY", "Outside, nobody knew the rhythm."),
            new(75.2f, "SPECIMEN 7 · MEMORY", "I could make the next beat."),
        };
        static readonly float[] TapTimes = { 1f, 1.3f, 1.6f, 2.3f, 6f, 6.3f, 6.6f, 7.3f, 12.5f, 12.8f, 13.1f, 13.8f, 14.5f, 14.8f, 15.1f, 15.8f, 16.5f, 16.8f, 17.1f, 17.8f, 45f, 45.3f, 45.6f };
        static readonly float[] ShotStarts = { 0f, 5f, 12f, 19f, 26f, 34f, 43f, 51f, 59f, 67f, 75f, Duration };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { requested = false; Instance = null; LastExitReason = ""; }

        public static void RequestForNewGame() => requested = true;

        /// <summary>Restore the live scene synchronously before a presentation shortcut starts another story.</summary>
        public static void CancelForPresentationShortcut()
        {
            requested = false;
            var opening = Instance;
            if (!opening) return;
            // A disabled component can retain coroutines; stop them and deactivate its whole object.
            opening.StopAllCoroutines();
            opening.gameObject.SetActive(false);
            opening.Cleanup(true);
            LastExitReason = "Presentation";
            if (Instance == opening) Instance = null;
            Destroy(opening.gameObject);
        }

        /// <summary>Called by CityDirector only after managers, player, camera and HUD exist.</summary>
        public static void StartIfRequested()
        {
            bool play = requested;
            requested = false;
            if (!play || Instance || !GameManager.Instance || !GameManager.Instance.IsPlaying) return;
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg == "-autotest" || arg == "-benchmark") return;
            if (RunState.MissionIndex != 0 || RunState.HostsPossessed != 0 || RunState.Flags.Count != 0 || RunState.Resets != 0) return;
            var go = new GameObject("OpeningCinematic");
            if (CityDirector.Instance) go.transform.SetParent(CityDirector.Instance.transform, false);
            go.AddComponent<OpeningCinematic>();
        }

        void Awake()
        {
            Instance = this;
            manager = GameManager.Instance; possession = PossessionManager.Instance; playerCamera = PlayerCamera.Instance;
            cam = playerCamera ? playerCamera.Cam : Camera.main;
            if (!manager || !possession || possession.ActiveBody == null || !cam)
            { LastExitReason = "Unavailable"; Destroy(gameObject); return; }
            try
            {
                oldCameraPosition = cam.transform.position; oldCameraRotation = cam.transform.rotation;
                oldFov = cam.fieldOfView; oldNear = cam.nearClipPlane; oldFar = cam.farClipPlane;
                oldClearFlags = cam.clearFlags; oldBackground = cam.backgroundColor;
                initialPlayerPosition = possession.ActiveBody.Root.position;
                brain = cam.GetComponent<CinemachineBrain>();
                hudCanvas = HUDController.Instance ? HUDController.Instance.GetComponentInChildren<Canvas>(true) : null;
                oldPlayerEnabled = playerCamera && playerCamera.enabled; oldBrainEnabled = brain && brain.enabled;
                oldHudEnabled = hudCanvas && hudCanvas.enabled; oldInputLock = possession.InputLocked;
                oldAmbienceGain = Sfx.AmbienceVolume;
                capturedState = true;
                manager.BeginEnding(); possession.InputLocked = true;
                if (playerCamera) playerCamera.enabled = false;
                if (brain) brain.enabled = false;
                if (hudCanvas) hudCanvas.enabled = false;
                GameManager.SetCursorLocked(true);
                cam.nearClipPlane = 0.025f; cam.farClipPlane = 80f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.002f, 0.009f, 0.014f);
                BuildOverlay(); BuildStage();
                film = new CinematicFilm("ContainmentOpening", overlay.transform);
                LastExitReason = ""; Running = true;
                EvaluateShot();
            }
            catch (Exception e)
            {
                Debug.LogException(e); LastExitReason = "Interrupted";
                Cleanup(true); Destroy(gameObject);
            }
        }

        void Update()
        {
            if (!Running || finishing) return;
            try
            {
                realElapsed += Time.unscaledDeltaTime;
                bool held = SkipHeld();
                if (realElapsed > 0.25f && !held) skipArmed = true;
                if (skipArmed && held) { StartCoroutine(Finish(true)); return; }
                elapsed = Mathf.Min(Duration, elapsed + (film == null || film.Ready ? Time.unscaledDeltaTime : 0f) * Mathf.Clamp(PlaybackSpeed, 0f, 80f));
                EvaluateShot();
                if (elapsed >= Duration) StartCoroutine(Finish(false));
            }
            catch (Exception e)
            {
                Debug.LogException(e); LastExitReason = "Interrupted";
                Cleanup(true); Destroy(gameObject);
            }
        }

        static bool SkipHeld()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            return (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed || kb.numpadEnterKey.isPressed || kb.escapeKey.isPressed)) ||
                   (pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed));
        }

        IEnumerator Finish(bool skipped)
        {
            finishing = true;
            if (skipped) { speaker.text = ""; caption.text = ""; skip.text = "Release to begin playing"; }
            for (float t = 0; t < 0.15f; t += Time.unscaledDeltaTime)
            { fade.color = new Color(0, 0, 0, Mathf.Clamp01(t / 0.15f)); yield return null; }
            fade.color = Color.black;
            while (SkipHeld()) yield return null;
            // Let the input system advance past the skip press/release before gameplay sees it.
            yield return null;
            LastExitReason = skipped ? "Skipped" : "Watched";
            Cleanup(true);
            Destroy(gameObject);
        }

        void Cleanup(bool resume)
        {
            if (cleaned) return;
            cleaned = true; Running = false;
            film?.Dispose(); film = null;
            if (stage) { stage.gameObject.SetActive(false); Destroy(stage.gameObject); }
            if (overlay) { overlay.gameObject.SetActive(false); Destroy(overlay.gameObject); }
            foreach (var m in owned) if (m) Destroy(m);
            owned.Clear();
            if (!capturedState) return;
            Sfx.AmbienceVolume = oldAmbienceGain;
            if (cam)
            {
                cam.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation);
                cam.fieldOfView = oldFov; cam.nearClipPlane = oldNear; cam.farClipPlane = oldFar;
                cam.clearFlags = oldClearFlags; cam.backgroundColor = oldBackground;
            }
            if (playerCamera)
            {
                playerCamera.SetYaw(0f);
                if (possession && possession.ActiveBody != null) playerCamera.SetTarget(possession.ActiveBody.CameraTarget, possession.ActiveBody.CameraDistance, snap: true);
                playerCamera.enabled = oldPlayerEnabled;
            }
            if (brain) brain.enabled = oldBrainEnabled;
            if (hudCanvas) hudCanvas.enabled = oldHudEnabled;
            if (possession)
            {
                if (possession.Active != null && possession.Active.Blob && possession.Active.IsFree)
                    possession.Active.Blob.Place(initialPlayerPosition);
                foreach (var a in possession.Agents) a.Body?.ClearControlInput();
                possession.InputLocked = oldInputLock;
            }
            if (GameInput.Simulated)
            {
                GameInput.SimMove = Vector2.zero;
                GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
            }
            // Never resume or announce into a replacement scene during unloading.
            if (resume && manager && manager == GameManager.Instance && manager.gameObject.scene == gameObject.scene)
            {
                GameEvents.RaiseSubtitle("", "");
                manager.ResumeAfterDialogue();
                if (MissionManager.Instance) MissionManager.Instance.ReannounceCurrentMission();
            }
        }

        void OnDisable() { if (!cleaned) { LastExitReason = "Interrupted"; Cleanup(true); } }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void BuildOverlay()
        {
            overlay = UIKit.CreateCanvas("Opening Canvas", 100, transform);
            var top = UIKit.Img(overlay.transform, "Top Letterbox", UIKit.White, Color.black);
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.pivot = new Vector2(0.5f, 1); top.rectTransform.sizeDelta = new Vector2(0, 112); top.rectTransform.anchoredPosition = Vector2.zero;
            var bottom = UIKit.Img(overlay.transform, "Bottom Letterbox", UIKit.White, Color.black);
            bottom.rectTransform.anchorMin = Vector2.zero; bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0); bottom.rectTransform.sizeDelta = new Vector2(0, 112); bottom.rectTransform.anchoredPosition = Vector2.zero;
            location = UIKit.Txt(overlay.transform, "Chapter", "VALE BIOTECH  /  SUBJECT 07", 24, UIKit.Accent, TextAnchor.MiddleLeft, Weight.Semi);
            location.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -34), new Vector2(1300, 42));
            speaker = UIKit.Txt(overlay.transform, "Speaker", "", 23, UIKit.Accent, TextAnchor.MiddleCenter, Weight.Bold);
            speaker.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 238), new Vector2(1400, 38));
            caption = UIKit.Txt(overlay.transform, "Caption", "", 34, Color.white, TextAnchor.UpperCenter, Weight.Medium, true);
            caption.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 218), new Vector2(1350, 94));
            skip = UIKit.Txt(overlay.transform, "Skip", "Space / Enter / Esc  ·  Skip opening     |     Gamepad A / B", 21, UIKit.TextDim, TextAnchor.MiddleRight, Weight.Medium);
            skip.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 30), new Vector2(1300, 40));
            title = UIKit.Txt(overlay.transform, "Title", "PARASITE", 130, Color.white, TextAnchor.MiddleCenter, Weight.Bold, true);
            title.rectTransform.Place(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 200));
            title.gameObject.SetActive(false);
            fade = UIKit.Img(overlay.transform, "Fade", UIKit.White, Color.black);
            fade.rectTransform.Stretch(); fade.raycastTarget = false;
        }

        Material Own(Material material) { owned.Add(material); return material; }
        Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material material) =>
            Prim.Make(PrimitiveType.Cube, parent, pos, size, material, name: name).transform;

        HumanoidRig Actor(string name, Profession profession, Vector3 p, float yaw, int seed, bool doctor = false, string identity = null)
        {
            var go = new GameObject(name); go.transform.SetParent(lab, false);
            go.transform.localPosition = p; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var rig = go.AddComponent<HumanoidRig>();
            rig.Identity = identity; rig.UseUnscaledTime = true; rig.AmbientGestures = false; // story runs at timeScale 0
            rig.Build(profession, seed, doctor);
            foreach (var m in rig.UniqueMaterials) if (m) owned.Add(m);
            return rig;
        }

        Transform VisualBlob(string name, Transform parent, Vector3 p, float radius)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            go.AddComponent<AmoebaVisual>().Build(radius);
            return go.transform;
        }

        void BuildStage()
        {
            stage = new GameObject("Opening Historical Set").transform; stage.SetParent(transform, false); stage.position = StageOrigin;
            lab = new GameObject("Historical Laboratory").transform; lab.SetParent(stage, false);
            CinematicArt.Instantiate("ContainmentLaboratory", lab, 0, owned);
            var metal = MatFactory.Lit(new Color(0.08f, 0.12f, 0.14f), 0.65f, 0.7f);
            var glass = MatFactory.Transparent(new Color(0.25f, 0.6f, 0.64f, 0.055f));
            Prim.Make(PrimitiveType.Cylinder, lab, new Vector3(0, 1.4f, 0), new Vector3(0.9f, 0.92f, 0.9f), glass, name: "Observation Glass");
            subject = VisualBlob("Historical Specimen 7", lab, new Vector3(0, 1.28f, 0), 0.2f);
            lobeA = VisualBlob("First Lobe", lab, new Vector3(-0.17f, 1.3f, 0), 0.15f);
            lobeB = VisualBlob("Second Lobe", lab, new Vector3(0.17f, 1.3f, 0), 0.15f);
            petrov = Actor("Historical Dr Hana Petrov", Profession.Scientist, new Vector3(-0.8f, 0, 0.4f), 150f, 490, identity: "Dr. Hana Petrov");
            vale = Actor("Historical Dr Vale", Profession.Scientist, new Vector3(0.9f, 0, 1.05f), 220f, 491, true);
            worker = Actor("Historical Contact Volunteer", Profession.Maintenance, new Vector3(1.5f, 0, -0.9f), 210f, 492);
            petrov.SetPose(ActorPose.Point); // Petrov points at the specimen throughout the historical lab shots
            statusMaterial = Own(MatFactory.LitUnique(new Color(1f, 0.46f, 0.08f)));
            Box(lab, "Interlock Housing", new Vector3(1.35f, 1.05f, -0.2f), new Vector3(0.42f, 0.55f, 0.16f), metal);
            Box(lab, "Interlock Lamp", new Vector3(1.35f, 1.14f, -0.3f), new Vector3(0.22f, 0.14f, 0.04f), statusMaterial);
            latch = Box(lab, "Containment Latch", new Vector3(0, 0.65f, -0.49f), new Vector3(0.5f, 0.07f, 0.05f), metal);
            var lightGo = new GameObject("Lab Key Light"); lightGo.transform.SetParent(lab, false); lightGo.transform.localPosition = new Vector3(-1, 3.6f, -1.5f);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(0.7f, 0.88f, 1f); light.intensity = 3.8f; light.range = 12f; light.shadows = LightShadows.Soft; light.shadowStrength = 0.75f;
            var rimGo = new GameObject("Green Rim Light"); rimGo.transform.SetParent(lab, false); rimGo.transform.localPosition = new Vector3(0, 2.4f, 1.4f);
            var rim = rimGo.AddComponent<Light>(); rim.type = LightType.Point; rim.color = Palette.Amoeba; rim.intensity = 1.4f; rim.range = 5f;
            duct = new GameObject("Historical Service Pipe").transform; duct.SetParent(stage, false); duct.localPosition = new Vector3(0, -4f, 0);
            CinematicArt.Instantiate("ServiceConduit", duct, 0, owned);
            // Lab lights turn off with the historical room; recessed duct fixtures light the escape shot independently.
            for (int i = 0; i < 4; i++)
            {
                var fixture = new GameObject("Service conduit guide light " + (i + 1));
                fixture.transform.SetParent(duct, false); fixture.transform.localPosition = new Vector3(0.43f, 0.26f, i * 3f);
                var guide = fixture.AddComponent<Light>(); guide.type = LightType.Point;
                guide.color = new Color(1f, 0.65f, 0.31f); guide.intensity = 0.65f; guide.range = 3.4f;
            }
            VisualBlob("Escaping Culture", duct, new Vector3(0, -0.25f, 5f), 0.18f);
        }

        void SetCamera(Vector3 p, Vector3 target, float fov, bool local = true)
        {
            if (local) { p += StageOrigin; target += StageOrigin; }
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(target - p)); cam.fieldOfView = fov;
        }

        void EvaluateShot()
        {
            film?.Tick(elapsed, PlaybackSpeed);
            int index = 0;
            while (index < ShotStarts.Length - 2 && elapsed >= ShotStarts[index + 1]) index++;
            bool changed = index != shot;
            shot = index;
            float t = Mathf.InverseLerp(ShotStarts[shot], ShotStarts[shot + 1], elapsed);
            float e = t * t * (3f - 2f * t);
            // Temporary scene gain only: saved preferences/master gain are never changed.
            Sfx.AmbienceVolume = oldAmbienceGain * Mathf.Lerp(0.12f, 1f, Mathf.Clamp01((elapsed - 67f) / 8f));
            lab.gameObject.SetActive(shot < 8); duct.gameObject.SetActive(shot == 8);
            // The macro specimen shots exclude actors: an arm can otherwise cross a close lens.
            petrov.gameObject.SetActive(shot > 0 && shot < 7);
            vale.gameObject.SetActive(shot == 3 || shot == 6 || shot == 7);
            worker.gameObject.SetActive(shot == 5);
            subject.gameObject.SetActive(shot != 4); lobeA.gameObject.SetActive(shot == 4); lobeB.gameObject.SetActive(shot == 4);
            title.gameObject.SetActive(shot == 10 && t > 0.15f && t < 0.87f);
            location.text = shot < 8 ? "VALE BIOTECH  /  SUBJECT 07" : shot == 8 ? "SERVICE CONDUIT" : "THE CITY  /  OUTSIDE CONTAINMENT";
            fade.color = new Color(0, 0, 0, shot == 0 ? Mathf.Clamp01(1f - elapsed / 0.65f) : 0f);
            float pulse = 1f + 0.045f * Mathf.Sin(elapsed * 5f);
            subject.localScale = Vector3.one * pulse;
            subject.localPosition = shot == 5 ? Vector3.Lerp(new Vector3(1.47f, 1.1f, -1.03f), new Vector3(0, 1.28f, 0), Mathf.Clamp01((t - 0.18f) / 0.25f)) : new Vector3(0, 1.28f + Mathf.Sin(elapsed * 1.2f) * 0.015f, 0);
            lobeA.localScale = lobeB.localScale = Vector3.one * pulse;
            float dt = Time.unscaledDeltaTime;
            petrov.transform.localPosition = shot == 5 ? new Vector3(0.8f, 0, -0.7f) : new Vector3(-0.8f, 0, 0.4f);
            petrov.transform.localRotation = Quaternion.Euler(0, shot == 5 ? 105f : 150f, 0);
            petrov.Animate(Vector3.zero, true, HostState.Normal, 4.6f, dt);
            vale.Animate(Vector3.zero, true, HostState.Normal, 4.6f, dt);
            worker.Animate(Vector3.zero, true, shot == 5 && t > 0.3f ? HostState.Unconscious : HostState.Normal, 5f, dt);
            statusMaterial.SetColor("_EmissionColor", shot >= 7 ? Palette.Amoeba * 2f : Palette.Warning * 1.8f);
            latch.localPosition = new Vector3(shot >= 7 ? Mathf.Lerp(0, 0.38f, Mathf.Clamp01(t * 2f)) : 0, 0.65f, -0.49f);
            switch (shot)
            {
                case 0: SetCamera(Vector3.Lerp(new Vector3(0.12f, 1.42f, -1.26f), new Vector3(-0.08f, 1.39f, -1.10f), e), new Vector3(0, 1.29f, 0), 36f); break;
                case 1: SetCamera(Vector3.Lerp(new Vector3(-0.65f, 1.5f, -2.2f), new Vector3(-1.55f, 1.72f, -2.85f), e), new Vector3(-0.12f, 1.3f, 0.3f), Mathf.Lerp(42f, 48f, e)); break;
                case 2: SetCamera(Vector3.Lerp(new Vector3(-2.3f, 1.59f, -1.8f), new Vector3(-1.85f, 1.61f, -1.7f), e), new Vector3(-0.74f, 1.47f, 0.4f), 40f); break;
                case 3: SetCamera(Vector3.Lerp(new Vector3(2.5f, 1.62f, -1.35f), new Vector3(2.1f, 1.6f, -1.1f), e), new Vector3(0.84f, 1.5f, 1.05f), 39f); break;
                case 4: SetCamera(Vector3.Lerp(new Vector3(-0.55f, 1.55f, -1.7f), new Vector3(0.75f, 1.35f, -1.55f), e), Vector3.Lerp(new Vector3(0, 1.48f, 0), new Vector3(1.35f, 1.14f, -0.2f), Mathf.Clamp01((t - 0.6f) / 0.4f)), 42f); break;
                case 5: SetCamera(Vector3.Lerp(new Vector3(2.4f, 1.7f, -2f), new Vector3(2.2f, 1.5f, -1.9f), e), new Vector3(1.5f, 1.15f, -0.9f), 45f); break;
                case 6: SetCamera(Vector3.Lerp(new Vector3(-2.8f, 1.55f, -2.5f), new Vector3(-2.2f, 1.6f, -2.2f), e), new Vector3(0, 1.35f, 0.65f), 44f); break;
                case 7: SetCamera(Vector3.Lerp(new Vector3(2.15f, 1.35f, -0.75f), new Vector3(2.05f, 1.25f, -0.65f), e), new Vector3(1.35f, 1.14f, -0.2f), 36f); break;
                case 8:
                    SetCamera(new Vector3(0, -3.82f, Mathf.Lerp(1.1f, 4.3f, e)), new Vector3(0, -3.82f, Mathf.Lerp(5f, 8f, e)), 48f);
                    duct.Find("Escaping Culture").localPosition = new Vector3(0, -0.25f, Mathf.Lerp(4f, 7.4f, e)); break;
                case 9: SetCamera(CityLayout.StormDrain + new Vector3(0, 0.24f, -0.75f), CityLayout.PlayerSpawn + new Vector3(0, 0.55f, 0), 52f, false); break;
                default:
                    SetCamera(Vector3.Lerp(CityLayout.StormDrain + new Vector3(0, 0.24f, -0.75f), oldCameraPosition, e), Vector3.Lerp(CityLayout.PlayerSpawn + Vector3.up * 0.55f, oldCameraPosition + oldCameraRotation * Vector3.forward * 8f, e), Mathf.Lerp(52f, oldFov, e), false); break;
            }
            while (nextCaption < Captions.Length && elapsed >= Captions[nextCaption].at)
            {
                var c = Captions[nextCaption++]; speaker.text = c.who; caption.text = c.text;
                GameEvents.RaiseSubtitle(c.who, c.text);
            }
            while (nextSound < TapTimes.Length && elapsed >= TapTimes[nextSound])
            { nextSound++; Sfx.Ui(SfxId.TerminalBeep, 0.13f); }
            if (!changed) return;
            if (shot == 0 || shot == 4 || shot == 10) Sfx.Ui(SfxId.Heartbeat, 0.35f);
            if (shot == 5) Sfx.Ui(SfxId.PossessHit, 0.25f);
            if (shot == 7) Sfx.Ui(SfxId.DoorOpen, 0.55f);
            if (shot == 8) Sfx.Ui(SfxId.BlobHop, 0.3f);
        }
    }
}
