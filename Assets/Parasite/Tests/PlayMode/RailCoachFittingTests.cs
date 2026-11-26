using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    // Explicit prepared-M23 chapter entry and onboard placement fixtures. This
    // tests reachable fittings with real physics/input, not earned rail progress.
    public sealed class RailCoachFittingTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator ActualSeatBlocksNormalFreeMovementWhileOriginalAisleAndControlRemainReachable()
        {
            LogAssert.Expect(LogType.Log, "[Northline] FIXTURE prior23 summary/flags and initialroleownership only; Northline progress uses actualinput/physics, no actorwarps/artificialcalm.");
            yield return RailAcceptance.Enter(EndingOutcome.Peace);
            var pm = PossessionManager.Instance; var agent = pm.Active; var blob = agent.Blob;
            var departure = RailDeparture.Instance; var coach = departure.Coach;
            var flags = new HashSet<string>(RunState.Flags); int mission = RunState.MissionIndex, resets = RunState.Resets, count = pm.Agents.Count;
            var seats = coach.GetComponentsInChildren<BoxCollider>(true).Where(c => c.name.StartsWith("Northline coach solid seat ")).OrderBy(c => c.name).ToArray();
            Assert.That(seats.Length, Is.EqualTo(32)); Assert.That(RailLayout.CoachSeatBoundsLocal.Length, Is.EqualTo(32));
            foreach (var seat in seats)
            {
                Assert.That(seat.enabled && !seat.isTrigger && seat.gameObject.activeInHierarchy && seat.attachedRigidbody == coach, Is.True);
                Assert.That(RailLayout.CoachSeatBoundsLocal.Any(b => Vector3.Distance(b.center, seat.transform.localPosition) < .00001f
                    && Vector3.Distance(b.size, Vector3.Scale(seat.size, seat.transform.localScale)) < .00001f), Is.True);
            }
            void SameOwnership()
            {
                Assert.That(PossessionManager.Instance == pm && pm.Active == agent && agent.Blob == blob && object.ReferenceEquals(pm.ActiveBody, blob)
                    && pm.Agents.Count == count && blob.Controller.enabled && GameManager.Instance.IsPlaying, Is.True);
                Assert.That(flags.SetEquals(RunState.Flags) && RunState.MissionIndex == mission && RunState.Resets == resets, Is.True);
            }
            void Stop()
            { GameInput.SimMove = Vector2.zero; GameInput.SimSprint = GameInput.SimJump = false; }
            GameInput.Simulated = true; Stop();
            PlayerCamera.Instance.SetYaw(0);
            blob.Place(coach.transform.TransformPoint(new Vector3(.05f, .78f, -3.6f)));
            Physics.SyncTransforms(); yield return null; yield return null;
            var cc = blob.Controller; var scale = cc.transform.lossyScale;
            float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(cc.height * Mathf.Abs(scale.y), radius * 2);
            Vector3 centre = cc.transform.TransformPoint(cc.center), up = cc.transform.up;
            Vector3 a = centre + up * (height * .5f - radius), b = centre - up * (height * .5f - radius);
            var hits = Physics.CapsuleCastAll(a, b, radius, coach.transform.right, 1.1f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider && !h.collider.transform.IsChildOf(blob.transform)).OrderBy(h => h.distance).ToArray();
            Assert.That(hits.Length, Is.GreaterThan(0)); Assert.That(seats.Contains(hits[0].collider), Is.True, "An actual seat must be the capsule's nearest physical obstruction.");
            Vector3 start = blob.transform.position; bool sides = false; float deadline = Time.realtimeSinceStartup + .85f;
            while (Time.realtimeSinceStartup < deadline)
            {
                SameOwnership(); GameInput.SimMove = Vector2.right; GameInput.SimJump = GameInput.SimSprint = false;
                PlayerCamera.Instance.SetYaw(0); sides |= (cc.collisionFlags & CollisionFlags.Sides) != 0; yield return null;
            }
            Stop(); SameOwnership(); Vector3 blocked = coach.transform.InverseTransformPoint(blob.transform.position);
            Assert.That(sides, Is.True); Assert.That(blocked.x, Is.InRange(.20f, .55f));
            Assert.That(Vector3.Distance(start, blob.transform.position), Is.LessThan(.60f), "Normal free movement cannot phase into the reachable bench.");

            // Reach the visible pan using the unchanged .55m hop, without a body warp.
            Assert.That(cc.isGrounded, Is.True);
            GameInput.SimJump = true; GameInput.SimMove = Vector2.right; yield return null; GameInput.SimJump = false;
            deadline = Time.realtimeSinceStartup + 3;
            bool landed = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                SameOwnership(); Vector3 local = coach.transform.InverseTransformPoint(blob.transform.position);
                GameInput.SimMove = local.x < .90f ? Vector2.right : Vector2.zero;
                if (cc.isGrounded && local.y > 1.22f) { landed = true; break; }
                yield return null;
            }
            Stop(); SameOwnership();
            Assert.That(landed, Is.True, "Normal jump/movement reaches the actual seat pan.");
            Vector3 landedLocal = coach.transform.InverseTransformPoint(blob.transform.position);
            Assert.That(landedLocal.y, Is.InRange(1.24f, 1.34f));
            Assert.That(Physics.Raycast(blob.transform.position + Vector3.up * .1f, Vector3.down, out var panHit, .3f, ~0, QueryTriggerInteraction.Ignore)
                && seats.Contains(panHit.collider) && Mathf.Abs(panHit.collider.bounds.max.y - (coach.position.y + 1.25f)) < .001f,
                Is.True, "Grounding belongs to the cushion, not an invisible whole-seat box.");

            // One continuous normal-input traversal beside BOTH south seat rows.
            // The old -2.3..2.3 segment stayed entirely in the empty control zone.
            // This retains the original three-second budget and has no navigation fallback, hop or warp.
            blob.Place(coach.transform.TransformPoint(new Vector3(0, .78f, -6.3f)));
            Physics.SyncTransforms(); yield return null; yield return null;
            Vector3 target = coach.transform.TransformPoint(new Vector3(0, .75f, -2.3f));
            deadline = Time.realtimeSinceStartup + 3;
            while (Vector2.Distance(new Vector2(blob.transform.position.x, blob.transform.position.z), new Vector2(target.x, target.z)) > .15f
                && Time.realtimeSinceStartup < deadline)
            {
                SameOwnership(); Vector3 delta = target - blob.transform.position; delta.y = 0; delta.Normalize();
                GameInput.SimMove = new Vector2(delta.x, delta.z); GameInput.SimJump = GameInput.SimSprint = false;
                PlayerCamera.Instance.SetYaw(0); yield return null;
            }
            Stop(); SameOwnership();
            Assert.That(Vector2.Distance(new Vector2(blob.transform.position.x, blob.transform.position.z), new Vector2(target.x, target.z)), Is.LessThan(.15f));
            Assert.That(cc.isGrounded, Is.True);
            var control = coach.GetComponentsInChildren<RailWorkControl>(true).Single(c => c.kind == RailWork.Departure);
            foreach (Vector3 original in RailLayout.BoardingStops)
            {
                blob.Place(original + Vector3.up * .03f); Physics.SyncTransforms(); yield return null; yield return null;
                SameOwnership();
                Vector3 delta = agent.Position - control.InteractPoint;
                Assert.That(new Vector2(delta.x, delta.z).magnitude, Is.LessThanOrEqualTo(control.InteractRange));
                Assert.That(Mathf.Abs(delta.y), Is.LessThanOrEqualTo(2.6f));
                Assert.That(RailCampaign.ClearLine(blob.EyePosition, control.InteractPoint, blob.transform, control.transform), Is.True,
                    "Seats may not obstruct either original boarding-stop-to-departure-control line.");
                var context = new InteractContext { agent = agent, host = agent.Host, position = agent.Position };
                Assert.That(RailCampaign.Ready(context, control.InteractPoint, control.transform, out string reason), Is.True, reason);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.SimMove = Vector2.zero; GameInput.Simulated = false;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
