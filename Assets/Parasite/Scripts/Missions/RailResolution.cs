using System;
using System.Collections;
using System.Collections.Generic;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.Missions
{
    /// <summary>Real protected coach motion. Arrival credit follows the actual endpoint, never a clock or interrupted caption.</summary>
    public sealed class RailResolution : MonoBehaviour
    {
        public static RailResolution Instance { get; private set; }
        public const float TravelMetres = 8f, Speed = 2f, MaximumSeconds = 12f;
        public float Elapsed { get; private set; }
        public float Travelled { get; private set; }
        public bool Running => initialized && !finished && !retired;
        public string Failure { get; private set; } = "";
        GameManager manager; RailDeparture departure; PossessionManager possession; Rigidbody capturedCoach; Transform capturedCarriage; int scene;
        Vector3 coachStart, endpoint; Quaternion coachRotation; RigidbodyInterpolation originalInterpolation;
        bool initialized, finished, finishing, retired, published, released, skipRequested, coachCaptured;
        sealed class Passenger
        {
            public AmoebaAgent agent; public AmoebaController body; public CharacterController controller;
            public Transform parent; public Vector3 position; public Quaternion rotation; public bool enabled;
        }
        readonly List<Passenger> passengers = new();
        void Awake() => Instance = this;
        public static bool Begin(GameManager owner, RailDeparture actualDeparture)
        {
            if (!owner || owner != GameManager.Instance || owner.State != GameState.Ending || !actualDeparture || !actualDeparture.DepartureAuthorized
                || !actualDeparture.TransitHardwareReady || RunState.Has(RailCampaign.Departed) || Instance && Instance.Running) return false;
            var item = new GameObject("Northline protected physical departure").AddComponent<RailResolution>();
            try { item.Initialize(owner, actualDeparture); return true; }
            catch (Exception error)
            {
                item.Failure = error.Message; item.RestoreOwned(false); item.finished = true; item.Retire();
                Debug.LogWarning("[NorthlineDeparture] Could not attach actual boarders: " + error.Message); return false;
            }
        }
        void Initialize(GameManager owner, RailDeparture actualDeparture)
        {
            manager = owner; departure = actualDeparture; scene = owner.gameObject.scene.handle; possession = PossessionManager.Instance;
            if (!possession || possession.Agents.Count != 2 || possession.Agents[0] == possession.Agents[1] || possession.Agents[0].Id == possession.Agents[1].Id)
                throw new InvalidOperationException("Two distinct actual boarders are required.");
            capturedCoach = departure.Coach; capturedCarriage = departure.carriage;
            coachStart = capturedCoach.position; coachRotation = capturedCoach.rotation; endpoint = coachStart + Vector3.forward * TravelMetres;
            originalInterpolation = capturedCoach.interpolation; coachCaptured = true;
            foreach (var agent in possession.Agents)
            {
                if (!RailCampaign.Free(agent) || !agent.Blob.Controller.isGrounded || !RailCampaign.FullyInside(agent, RailLayout.Boarding))
                    throw new InvalidOperationException("A free boarder's actual capsule left the departure mark.");
                var body = agent.Blob;
                passengers.Add(new Passenger { agent = agent, body = body, controller = body.Controller, parent = body.transform.parent,
                    position = body.transform.position, rotation = body.transform.rotation, enabled = body.Controller.enabled });
            }
            initialized = true; departure.AvailabilityLost += OnCoordinatorUnavailable;
            capturedCoach.interpolation = RigidbodyInterpolation.None;
            foreach (var passenger in passengers)
            { passenger.body.ClearControlInput(); passenger.controller.enabled = false; passenger.body.transform.SetParent(capturedCarriage, true); }
            Physics.SyncTransforms();
        }
        bool OwnsCapturedManager(GameManager owner) => initialized && manager && manager == owner && manager == GameManager.Instance && Instance == this
            && manager.State == GameState.Ending && manager.gameObject.scene.handle == scene && gameObject.scene.handle == scene && SceneManager.GetActiveScene().handle == scene;
        // Cleanup authority deliberately survives retirement/destruction of the departure coordinator and coach.
        public bool Owns(GameManager owner) => OwnsCapturedManager(owner) && departure && !departure.PermanentlyRetired && RailDeparture.Instance == departure && departure.isActiveAndEnabled && departure.gameObject.activeInHierarchy
            && departure.gameObject.scene.handle == scene && departure.DepartureAuthorized && departure.Coach == capturedCoach && departure.carriage == capturedCarriage
            && capturedCoach && capturedCarriage && !RunState.Has(RailCampaign.Departed);
        void OnCoordinatorUnavailable()
        {
            if (finished || finishing) return;
            Failure = "The captured departure coordinator retired during the protected trip.";
            try { StopAllCoroutines(); } finally { FinishOrRollback(); Retire(); }
        }
        bool ActualPassengersAttached()
        {
            if (!possession || possession != PossessionManager.Instance || possession.Agents.Count != 2 || passengers.Count != 2 || !capturedCarriage) return false;
            foreach (var passenger in passengers)
                if (passenger.agent == null || !possession.Agents.Contains(passenger.agent) || !passenger.agent.IsFree || passenger.agent.InTransit || passenger.agent.CapturedPendingEject
                    || !passenger.body || passenger.agent.Blob != passenger.body || !passenger.body.gameObject.activeInHierarchy || !passenger.controller || passenger.controller.enabled
                    || !passenger.body.transform.IsChildOf(capturedCarriage)) return false;
            return true;
        }
        public bool ReadyToPublish
        {
            get
            {
                if (!Owns(manager) || !departure.TransitHardwareReady || !ActualPassengersAttached() || Vector3.Distance(capturedCoach.position, endpoint) > .005f
                    || Vector3.Distance(capturedCarriage.position, endpoint) > .005f || Travelled < TravelMetres - .005f) return false;
                foreach (var passenger in passengers)
                    if (Vector3.Distance(passenger.body.transform.position, passenger.position + Vector3.forward * TravelMetres) > .01f) return false;
                return true;
            }
        }
        IEnumerator Start()
        {
            if (!Owns(manager) || retired) { FinishOrRollback(); Retire(); yield break; }
            released = !GameInput.InteractPressed && !GameInput.JumpPressed;
            GameEvents.RaiseSubtitle("NORTHLINE RADIO", RailCampaign.Peace ? "The medical carriage has its signal. Both free bodies are aboard. Northline is moving."
                : "Your relay has its line. Both free bodies are aboard. The next city is listening.");
            double started = Time.realtimeSinceStartupAsDouble;
            try
            {
                while ((Elapsed = (float)(Time.realtimeSinceStartupAsDouble - started)) < MaximumSeconds && Owns(manager))
                {
                    if (!ActualPassengersAttached() || !departure.TransitHardwareReady) { Failure = "A real passenger or the physical coach/portal changed."; break; }
                    if (!GameInput.InteractPressed && !GameInput.JumpPressed) released = true;
                    if (released && (GameInput.InteractPressed || GameInput.JumpPressed || GameInput.PausePressed)) skipRequested = true;
                    Vector3 position = capturedCoach.position;
                    if (Vector3.Distance(position, endpoint) > .001f)
                    {
                        Vector3 next = Vector3.MoveTowards(position, endpoint, Speed * Time.unscaledDeltaTime);
                        if (!departure.ClearTransit(next - position)) { Failure = "The actual departure coach sweep is obstructed."; break; }
                        // The world is deliberately frozen. Update only this owned kinematic presentation body and its attached real boarders.
                        capturedCoach.position = next; capturedCarriage.SetPositionAndRotation(next, coachRotation); Physics.SyncTransforms();
                        Travelled = Vector3.Distance(coachStart, capturedCoach.position);
                    }
                    if (ReadyToPublish && (skipRequested || Elapsed >= 5f)) break;
                    yield return null;
                }
                if (!ReadyToPublish && string.IsNullOrEmpty(Failure)) Failure = "The bounded departure ended before the actual endpoint.";
            }
            finally { FinishOrRollback(); }
            Retire();
        }
        void FinishOrRollback()
        {
            if (finished || finishing) return;
            finishing = true; bool owned = OwnsCapturedManager(manager), endpointVerified = false;
            try
            {
                if (owned && ReadyToPublish)
                {
                    endpointVerified = true; GameEvents.RaiseSubtitle("", ""); published = manager.PublishRailEnding(this);
                    if (!published) Failure = "The owning manager rejected the actual endpoint handoff.";
                }
            }
            catch (Exception error)
            {
                // An ending listener may fail after the owner has already recorded the physically verified result.
                published = endpointVerified && manager && manager.State == GameState.Ended && RunState.Has(RailCampaign.Departed);
                Failure = "Departure handoff listener: " + error.Message;
            }
            finally
            {
                try
                {
                    RestoreOwned(published);
                    if (owned && !published && manager && manager == GameManager.Instance && manager.State == GameState.Ending)
                    {
                        if (departure) departure.CancelDepartureAuthorization();
                        GameEvents.RaiseSubtitle("", ""); manager.ResumeAfterDialogue();
                        int usable = 0;
                        if (possession && possession == PossessionManager.Instance)
                            foreach (var agent in possession.Agents) if (RailCampaign.Free(agent)) usable++;
                        bool invalidRegistry = !possession || possession != PossessionManager.Instance || usable == 0 || usable != possession.Agents.Count || !RailCampaign.Free(possession.Active);
                        bool lostHardware = !departure || departure.PermanentlyRetired || RailDeparture.Instance != departure || !capturedCoach || !capturedCarriage || departure.Coach != capturedCoach || departure.carriage != capturedCarriage;
                        if (invalidRegistry || lostHardware) manager.ResetToCheckpoint("Northline departure lost an actual body or its captured coach");
                        else GameEvents.RaiseToast("Departure interrupted. Both free bodies are back aboard; press E to retry after the coordinator is available.", Palette.Warning);
                    }
                }
                finally { finished = true; finishing = false; }
            }
        }
        void RestoreOwned(bool completed)
        {
            // Exact captured objects only; never discover or move a replacement player after a scene change.
            foreach (var passenger in passengers)
            {
                if (!passenger.body || !passenger.controller || passenger.body.gameObject.scene.handle != scene) continue;
                passenger.controller.enabled = false;
                passenger.body.transform.SetParent(passenger.parent ? passenger.parent : null, true);
                passenger.body.transform.SetPositionAndRotation(completed ? passenger.position + Vector3.forward * TravelMetres : passenger.position, passenger.rotation);
                passenger.body.ClearControlInput();
                bool stillOwnedFree = possession && possession == PossessionManager.Instance && possession.Agents.Contains(passenger.agent)
                    && passenger.agent.Blob == passenger.body && passenger.agent.IsFree && !passenger.agent.InTransit && !passenger.agent.CapturedPendingEject;
                passenger.controller.enabled = passenger.enabled && stillOwnedFree;
            }
            if (coachCaptured && capturedCoach && capturedCarriage && capturedCoach.gameObject.scene.handle == scene && capturedCarriage.gameObject.scene.handle == scene)
            {
                if (!completed) { capturedCoach.position = coachStart; capturedCarriage.SetPositionAndRotation(coachStart, coachRotation); }
                capturedCoach.interpolation = originalInterpolation;
            }
            Physics.SyncTransforms();
        }
        void Retire() { if (retired) return; retired = true; if (departure) departure.AvailabilityLost -= OnCoordinatorUnavailable; if (Instance == this) Instance = null; Destroy(gameObject); }
        void OnDisable() { try { StopAllCoroutines(); } finally { FinishOrRollback(); Retire(); } }
        void OnDestroy() { FinishOrRollback(); if (departure) departure.AvailabilityLost -= OnCoordinatorUnavailable; if (Instance == this) Instance = null; }
    }
}
