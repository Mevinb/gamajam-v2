using System.Collections;
using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;

namespace Parasite.Possession
{
    /// <summary>
    /// Owns the (max two) amoeba agents, which one is active, and every possession action:
    /// E possess/interact/eject · Q leave host · V split · Tab switch. Routes input + camera to the active body.
    /// </summary>
    public class PossessionManager : MonoBehaviour
    {
        public static PossessionManager Instance { get; private set; }

        public const float PossessRange = 5f;
        public const float AimConeDegrees = 15f;
        public const float StrongAimDegrees = 7f;
        const float CloseAimHintRange = 2f;
        public const float LaunchSeconds = 0.42f;
        public const float AutoEjectSeconds = 3f;

        public readonly List<AmoebaAgent> Agents = new();
        public int ActiveIndex { get; private set; }
        public AmoebaAgent Active => ActiveIndex >= 0 && ActiveIndex < Agents.Count ? Agents[ActiveIndex] : null;
        public IControllable ActiveBody => Active?.Body;
        public TargetInfo CurrentTarget { get; private set; } = TargetInfo.None;
        public bool InputLocked { get; set; }

        PlayerCamera cam;
        HostCharacter highlighted;
        readonly RaycastHit[] hits = new RaycastHit[16];

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ------------------------------------------------------------------ setup
        public AmoebaAgent SpawnInitial(Vector3 position, float yaw)
        {
            cam = PlayerCamera.Instance;
            var agent = new AmoebaAgent { Id = 1 };
            AmoebaController.Create(position, agent);
            agent.Blob.transform.rotation = Quaternion.Euler(0, yaw, 0);
            Agents.Add(agent);
            ActiveIndex = 0;
            if (cam) { cam.SetYaw(yaw); cam.SetTarget(agent.Blob.CameraTarget, agent.Blob.CameraDistance, snap: true); }
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
            return agent;
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            if (!cam) cam = PlayerCamera.Instance;
            var gm = GameManager.Instance;
            bool playing = gm == null || gm.IsPlaying;

            for (int i = 0; i < Agents.Count; i++)
                if (i != ActiveIndex || !playing || InputLocked) Agents[i].Body?.ClearControlInput();

            if (!playing) { SetTarget(TargetInfo.None); return; }
            TickCaptures();
            if (InputLocked || Active == null) { SetTarget(TargetInfo.None); return; }

            var body = Active.Body;
            if (body != null && !Active.InTransit)
            {
                var ci = new ControlInput
                {
                    worldMove = cam ? cam.ToWorldMove(GameInput.MoveValue) : new Vector3(GameInput.MoveValue.x, 0, GameInput.MoveValue.y),
                    sprint = GameInput.SprintHeld,
                    jump = GameInput.JumpPressed,
                };
                if (Active.Host != null && Active.Host.State == HostState.Captured) ci = default;
                body.SetControlInput(ci);
            }

            SetTarget(ComputeTarget());

            if (GameInput.InteractPressed) DoPrimary();
            else if (GameInput.LeavePressed) LeaveHost();
            else if (GameInput.SplitPressed) TrySplit();
            else if (GameInput.SwitchPressed) SwitchActive();

            CheckpointPolling();
        }

        void SetTarget(TargetInfo t)
        {
            CurrentTarget = t;
            var h = t.kind == TargetKind.Possess ? t.host : null;
            if (h != highlighted)
            {
                if (highlighted) highlighted.Glow.SetTargeted(false);
                highlighted = h;
                if (highlighted) highlighted.Glow.SetTargeted(true);
            }
        }

        // ------------------------------------------------------------------ targeting
        InteractContext Context(AmoebaAgent a) => new()
        {
            agent = a,
            host = a.Host,
            permissions = a.Host != null ? a.Host.Permissions : AccessPermission.None,
            position = a.Position,
        };

        public TargetInfo ComputeTarget()
        {
            var a = Active;
            if (a == null || a.InTransit) return TargetInfo.None;
            if (a.CapturedPendingEject)
                return new TargetInfo { kind = TargetKind.Eject, prompt = "EJECT! Escape the host", allowed = true, worldPos = a.Position + Vector3.up * 1.6f };

            var body = a.Body;
            Vector3 eye = body.EyePosition;
            Vector3 camPos = cam ? cam.Position : eye;
            Vector3 camFwd = cam ? cam.Forward : body.Root.forward;

            // possession candidate: inside the aim cone, within range, clear line of sight
            HostCharacter bestHost = null; float bestAngle = float.MaxValue;
            HostCharacter farHost = null; float farAngle = float.MaxValue;
            HostCharacter closeAimHost = null; float closeAimDistance = float.MaxValue;
            foreach (var h in HostCharacter.All)
            {
                if (!h || h == a.Host || !h.CanBePossessed) continue;
                Vector3 chest = h.ChestPosition;
                float angle = Vector3.Angle(camFwd, chest - camPos);
                float dist = Vector3.Distance(eye, chest);
                if (angle > AimConeDegrees)
                {
                    // At the free body's upward camera stop, a nearby chest can be above the legal aim cone.
                    // Explain the physical retreat instead of presenting an empty prompt or accepting an invalid target.
                    if (a.IsFree && cam && !cam.IsLowClearance && cam.Pitch <= cam.MinimumPitch + .05f &&
                        chest.y > camPos.y && dist <= CloseAimHintRange && dist < closeAimDistance &&
                        Vector3.Angle(Vector3.ProjectOnPlane(camFwd, Vector3.up), Vector3.ProjectOnPlane(chest - camPos, Vector3.up)) <= StrongAimDegrees &&
                        LineOfSight(eye, chest, body.Root, h.transform))
                    {
                        closeAimHost = h;
                        closeAimDistance = dist;
                    }
                    continue;
                }
                if (dist > PossessRange)
                {
                    if (dist < PossessRange * 2.2f && angle < farAngle) { farAngle = angle; farHost = h; }
                    continue;
                }
                if (angle >= bestAngle) continue;
                if (!LineOfSight(eye, chest, body.Root, h.transform)) continue;
                bestAngle = angle; bestHost = h;
            }

            // interactable candidate: nearest in reach, roughly in front of the camera
            var ctx = Context(a);
            IInteractable bestI = null; float bestD = float.MaxValue;
            Vector3 flatFwd = new Vector3(camFwd.x, 0, camFwd.z).normalized;
            foreach (var it in Interactables.All)
            {
                if (it == null || !it.IsAvailable) continue;
                Vector3 p = it.InteractPoint;
                Vector3 d = p - body.Root.position;
                if (Mathf.Abs(d.y) > 2.6f) continue;
                d.y = 0;
                float dist = d.magnitude;
                if (dist > it.InteractRange || dist >= bestD) continue;
                if (dist > 0.8f && Vector3.Angle(flatFwd, d) > 80f) continue;
                bestD = dist; bestI = it;
            }

            if (bestHost && (bestI == null || bestAngle <= StrongAimDegrees))
                return new TargetInfo
                {
                    kind = TargetKind.Possess, host = bestHost, allowed = true,
                    prompt = $"Possess {bestHost.DisplayName} ({bestHost.Profile.title})",
                    worldPos = bestHost.transform.position + Vector3.up * 2.15f,
                };
            if (bestI != null)
            {
                bool ok = bestI.CanInteract(ctx, out var reason);
                return new TargetInfo
                {
                    kind = TargetKind.Interact, interactable = bestI, allowed = ok,
                    prompt = ok ? bestI.GetPrompt(ctx) : $"{bestI.GetPrompt(ctx)}  —  {reason}",
                    worldPos = bestI.InteractPoint + Vector3.up * 0.4f,
                };
            }
            if (bestHost)
                return new TargetInfo
                {
                    kind = TargetKind.Possess, host = bestHost, allowed = true,
                    prompt = $"Possess {bestHost.DisplayName} ({bestHost.Profile.title})",
                    worldPos = bestHost.transform.position + Vector3.up * 2.15f,
                };
            if (closeAimHost)
                return new TargetInfo
                {
                    kind = TargetKind.None, host = closeAimHost, allowed = false,
                    prompt = $"Step back to aim at {closeAimHost.DisplayName}",
                    worldPos = closeAimHost.transform.position + Vector3.up * 2.15f,
                };
            if (farHost)
                return new TargetInfo
                {
                    kind = TargetKind.None, host = farHost, allowed = false,
                    prompt = $"{farHost.DisplayName} is too far — get within {PossessRange:0} m",
                    worldPos = farHost.transform.position + Vector3.up * 2.15f,
                };
            return TargetInfo.None;
        }

        /// <summary>True if nothing but the two endpoints' own colliders lies between a and b.</summary>
        public bool LineOfSight(Vector3 a, Vector3 b, Transform ignoreRoot, Transform targetRoot)
        {
            Vector3 d = b - a; float len = d.magnitude;
            if (len < 0.01f) return true;
            int n = Physics.RaycastNonAlloc(a, d / len, hits, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = hits[i].transform;
                if (ignoreRoot && t.IsChildOf(ignoreRoot)) continue;
                if (targetRoot && t.IsChildOf(targetRoot)) continue;
                if (t.gameObject.layer == Layers.Characters || t.gameObject.layer == Layers.Amoeba) continue;
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ actions
        void DoPrimary()
        {
            var t = CurrentTarget;
            switch (t.kind)
            {
                case TargetKind.Eject: Eject(Active); break;
                case TargetKind.Possess: if (t.host) StartCoroutine(Launch(Active, t.host)); break;
                case TargetKind.Interact: t.interactable?.Interact(Context(Active)); break;
                default:
                    if (t.host) { GameEvents.RaiseToast(t.prompt, Palette.Warning); Sfx.Ui(SfxId.Denied, 0.5f); }
                    break;
            }
        }

        /// <summary>Public for tests/autoplay: possess immediately via the normal launch.</summary>
        public void RequestPossess(HostCharacter target)
        {
            if (Active != null && target && target.CanBePossessed && !Active.InTransit) StartCoroutine(Launch(Active, target));
        }

        IEnumerator Launch(AmoebaAgent agent, HostCharacter target)
        {
            if (agent == null || agent.InTransit || !target || !target.CanBePossessed) yield break;
            agent.InTransit = true;
            Vector3 from;
            HostCharacter previous = agent.Host;
            if (previous)
            {
                from = previous.EyePosition;
                agent.Host = null;
                previous.EndPossession();
                Fx.Splat(from);
            }
            else
            {
                from = agent.Blob.Center;
                agent.Blob.gameObject.SetActive(false);
            }
            Sfx.Play(SfxId.PossessLaunch, from);
            var proj = Fx.CreateProjectile();
            proj.position = from;
            if (agent == Active && cam) { cam.SetTarget(proj, 3.6f, false, 0.15f); cam.Kick(8f); }
            GameEvents.RaiseControlChanged();

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / LaunchSeconds;
                if (!target) break;
                Vector3 to = target.EyePosition - Vector3.up * 0.1f;
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.2f;
                if (proj) { if ((p - proj.position).sqrMagnitude > 1e-6f) proj.rotation = Quaternion.LookRotation(p - proj.position); proj.position = p; }
                yield return null;
            }

            Vector3 landing = proj ? proj.position : from;
            Fx.DestroyProjectile(proj);
            agent.InTransit = false;

            if (target && target.CanBePossessed)
            {
                target.BeginPossession(agent);
                agent.Host = target;
                agent.Blob.transform.position = target.transform.position;
                Fx.Splat(target.EyePosition);
                Sfx.Play(SfxId.PossessHit, target.EyePosition);
                GameEvents.RaisePossessionSplash(target.EyePosition);
                if (agent == Active)
                {
                    Fx.ScreenPulse(Palette.Amoeba, 1f, 0.7f);
                    if (cam) { cam.SetTarget(target.CameraTarget, target.CameraDistance, false, 0.3f); cam.Kick(10f); }
                }
                SuspicionSystem.Instance?.ReportViolation(ViolationType.WitnessedPossession, target.ChestPosition, target, 1f, previous ? previous.transform : null);
                GameEvents.RaiseToast($"Possessed {target.DisplayName} — {target.Profile.abilityText}", Palette.Amoeba);
            }
            else
            {
                // target became invalid mid-air: drop out as a blob
                agent.Blob.Place(FindFreeSpot(landing, Vector3.zero));
                agent.Blob.gameObject.SetActive(true);
                if (agent == Active && cam) cam.SetTarget(agent.Blob.CameraTarget, agent.Blob.CameraDistance);
            }
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
        }

        /// <summary>Q — leave the current host; it collapses for 8 s.</summary>
        public bool LeaveHost()
        {
            var a = Active;
            if (a == null || a.Host == null || a.InTransit) { if (a != null && a.Host == null) GameEvents.RaiseToast("You're not inside a host.", Palette.Warning); return false; }
            if (a.Host.State == HostState.Captured) { Eject(a); return true; }
            ExitHost(a, false);
            return true;
        }

        void ExitHost(AmoebaAgent a, bool ejected)
        {
            var host = a.Host;
            Vector3 exitPos = FindFreeSpot(host.transform.position, -host.transform.forward);
            a.Host = null;
            a.CapturedPendingEject = false;
            host.EndPossession();
            a.Blob.gameObject.SetActive(true);
            a.Blob.Place(exitPos);
            a.Blob.transform.rotation = host.transform.rotation;
            a.Blob.Visual.Squash(1f);
            Fx.Splat(host.ChestPosition);
            Sfx.Play(ejected ? SfxId.Eject : SfxId.Release, host.ChestPosition);
            if (a == Active && cam) cam.SetTarget(a.Blob.CameraTarget, a.Blob.CameraDistance, false, 0.3f);
            if (ejected)
            {
                SuspicionSystem.Instance?.OnAmoebaEjected(a.Blob, exitPos);
                GameEvents.RaiseToast("EJECTED! Run — the guards are stunned for a moment.", Palette.Warning);
                Fx.ScreenPulse(Palette.Amoeba, 0.8f, 0.5f);
            }
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
        }

        /// <summary>Called by GuardAI when a possessed host is grabbed.</summary>
        public void OnHostCaptured(HostCharacter host, GuardAI by)
        {
            var a = host.Possessor;
            if (a == null) return;
            a.CapturedPendingEject = true;
            a.CaptureTimer = AutoEjectSeconds;
            RunState.Captures++;
            GameEvents.RaiseCaptured();
            if (a == Active)
            {
                Fx.ScreenPulse(Palette.Danger, 1f, 0.8f);
                GameEvents.RaiseToast($"CAPTURED by {by.GetComponent<HostCharacter>().DisplayName}! Press E to eject!", Palette.Danger);
            }
            else GameEvents.RaiseToast($"Amoeba #{a.Id}'s host was captured — auto-ejecting", Palette.Danger);
            GameEvents.RaiseControlChanged();
        }

        void TickCaptures()
        {
            foreach (var a in Agents.ToArray())
            {
                if (!a.CapturedPendingEject) continue;
                a.CaptureTimer -= Time.deltaTime;
                if (a.CaptureTimer <= 0f) Eject(a);
            }
        }

        public void Eject(AmoebaAgent a)
        {
            if (a == null || a.Host == null) return;
            ExitHost(a, true);
        }

        /// <summary>A guard caught a free amoeba: it is destroyed. If none remain, reset to checkpoint.</summary>
        public void ContainBlob(AmoebaAgent a, GuardAI by)
        {
            if (a == null || !Agents.Contains(a) || a.Host != null) return;
            var pos = a.Blob ? a.Blob.Center : a.Position;
            Fx.Splat(pos);
            Sfx.Play(SfxId.BlobContained, pos);
            if (a.Blob) Destroy(a.Blob.gameObject);
            int idx = Agents.IndexOf(a);
            Agents.Remove(a);
            SuspicionSystem.Instance?.OnSuspectGone();
            if (Agents.Count == 0)
            {
                GameManager.Instance?.ResetToCheckpoint("SPECIMEN CONTAINED");
                GameEvents.RaiseControlChanged();
                return;
            }
            if (ActiveIndex >= Agents.Count || idx == ActiveIndex)
            {
                ActiveIndex = 0;
                FocusCamera(Active, false);
            }
            else if (idx < ActiveIndex) ActiveIndex--;
            GameEvents.RaiseToast($"Amoeba #{a.Id} was contained! Control → #{Active.Id}", Palette.Danger);
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
        }

        /// <summary>V — split off a second free amoeba (max two). The original keeps its current state.</summary>
        public bool TrySplit()
        {
            var a = Active;
            if (a == null || a.InTransit) return false;
            if (!SplitRules.CanSplit(Agents.Count))
            {
                GameEvents.RaiseToast($"You can only split into {SplitRules.MaxAmoebas}. [Tab] switches.", Palette.Warning);
                Sfx.Ui(SfxId.Denied, 0.5f);
                return false;
            }
            if (a.CapturedPendingEject) return false;
            var body = a.Body;
            Vector3 origin = body.Root.position;
            Vector3 spot = FindFreeSpot(origin, -body.Root.right);
            int id = 1; foreach (var o in Agents) if (o.Id == id) id = 2;
            var n = new AmoebaAgent { Id = id };
            AmoebaController.Create(spot, n);
            n.Blob.transform.rotation = body.Root.rotation;
            n.Blob.Visual.Squash(1f);
            Agents.Add(n);
            Fx.SplitBurst(spot + Vector3.up * 0.3f);
            Sfx.Play(SfxId.Split, spot);
            SuspicionSystem.Instance?.ReportViolation(ViolationType.WitnessedSplit, spot + Vector3.up * 0.3f, n.Blob, 1f,
                a.Host ? a.Host.transform : null);
            GameEvents.RaiseToast($"Split! Amoeba #{n.Id} created. [Tab] to switch.", Palette.Amoeba);
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
            return true;
        }

        /// <summary>Tab — switch control to the other amoeba.</summary>
        public bool SwitchActive()
        {
            if (Agents.Count < 2) { GameEvents.RaiseToast("Only one amoeba. Press [V] to split.", Palette.Warning); return false; }
            Active?.Body?.ClearControlInput();
            ActiveIndex = SplitRules.NextIndex(ActiveIndex, Agents.Count);
            FocusCamera(Active, false);
            Sfx.Ui(SfxId.Switch);
            GameEvents.RaiseToast($"Control → Amoeba #{Active.Id} ({Active.Describe()})", Palette.Info);
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
            return true;
        }

        void FocusCamera(AmoebaAgent a, bool snap)
        {
            if (a == null || !cam) return;
            var b = a.Body;
            if (b != null) cam.SetTarget(b.CameraTarget, b.CameraDistance, snap, 0.45f);
        }

        void RefreshActiveVisuals()
        {
            foreach (var a in Agents)
                if (a.Blob) a.Blob.Visual.SetActive(a == Active);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Find a nearby free ground spot for a blob, preferring the given direction.</summary>
        public static Vector3 FindFreeSpot(Vector3 origin, Vector3 preferDir)
        {
            var dirs = new List<Vector3>();
            if (preferDir.sqrMagnitude > 0.01f) dirs.Add(preferDir.normalized);
            for (int i = 0; i < 8; i++) dirs.Add(Quaternion.Euler(0, i * 45f, 0) * Vector3.forward);
            int mask = Layers.EnvironmentMask;
            foreach (float r in new[] { 0.9f, 1.4f, 0.55f })
                foreach (var d in dirs)
                {
                    Vector3 p = origin + d * r + Vector3.up * 0.5f;
                    if (Physics.CheckSphere(p, AmoebaController.Radius * 0.95f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(origin + Vector3.up * 0.5f, p, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(p, Vector3.down, out var hit, 3f, mask, QueryTriggerInteraction.Ignore))
                        return hit.point + Vector3.up * 0.02f;
                }
            return origin + Vector3.up * 0.05f;
        }

        void CheckpointPolling()
        {
            var b = ActiveBody;
            if (b == null) return;
            var p = b.Root.position;
            foreach (var z in CheckpointZone.All)
                if (z && z.Contains(p)) { GameManager.Instance?.SetCheckpoint(z.RespawnPosition, z.checkpointName); break; }
        }

        // ------------------------------------------------------------------ test hooks
        public void ForcePossessImmediate(HostCharacter target)
        {
            var a = Active;
            if (a == null || !target || !target.CanBePossessed) return;
            if (a.Host) { var prev = a.Host; a.Host = null; prev.EndPossession(); }
            else a.Blob.gameObject.SetActive(false);
            target.BeginPossession(a);
            a.Host = target;
            if (cam) cam.SetTarget(target.CameraTarget, target.CameraDistance, true);
            RefreshActiveVisuals();
            GameEvents.RaiseControlChanged();
        }
    }
}
