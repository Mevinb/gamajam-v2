using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Possession;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Parasite.Tests
{
    // Explicit prepared signal/hardware/boarder fixture, not earned campaign progress.
    // It exercises the real TryDepart -> FinishRail -> physical8m motion -> publication/
    // coordinator-disable rollback with actual grounded capsules and colliders.
    public sealed class RailEndingCallbackTests
    {
        GameObject root;
        GameManager manager;
        PossessionManager possession;
        MissionManager missions;
        RailDeparture departure;
        RailWorkControl control;
        Rigidbody coach;
        AmoebaAgent[] agents;
        Vector3 coachStart;
        Vector3[] boarderStarts;
        Action<string, string> subtitle;
        Action<string, Color> toast;
        Action<EndingData> ending;
        IDisposable persistenceScope;
        string directory;

        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        GameObject Item(string name, Vector3 position)
        {
            var item = new GameObject(name); item.transform.SetParent(root.transform, false); item.transform.position = position; return item;
        }
        BoxCollider Box(string name, Vector3 position, Vector3 size)
        { var item = Item(name, position); var collider = item.AddComponent<BoxCollider>(); collider.size = size; return collider; }

        [UnitySetUp]
        public IEnumerator PreparePhysicalFixture()
        {
            RunState.NewGame(); RunState.RecordLakesideOutcome(EndingOutcome.Peace); OpeningCinematic.CancelPendingNewGame(); Time.timeScale = 1;
            SceneManager.LoadScene(GameManager.MenuScene); yield return null; yield return null;
            var sandbox = SceneManager.CreateScene("Northline callback fault fixture"); SceneManager.SetActiveScene(sandbox);
            yield return SceneManager.UnloadSceneAsync(GameManager.MenuScene);
            GameEvents.ClearAll();
            directory = Path.Combine(Path.GetTempPath(), "parasite-rail-callback-" + Guid.NewGuid().ToString("N"));
            persistenceScope = CampaignPersistence.UseTestDirectory(directory);
            GameInput.Simulated = true; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimJump = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimSprint = false;
            root = new GameObject("Prepared physical Northline callback fixture");
            manager = root.AddComponent<GameManager>(); possession = root.AddComponent<PossessionManager>();
            missions = root.AddComponent<MissionManager>(); missions.enabled = false;
            typeof(MissionManager).GetProperty(nameof(MissionManager.Missions)).GetSetMethod(true).Invoke(missions, new object[] {
                new List<Mission> {
                    new Mission { title = "Prepared final receipt A", completeWhen = () => RunState.Has(RailCampaign.Departed) },
                    new Mission { title = "Prepared final receipt B", completeWhen = () => RunState.Has(RailCampaign.Departed) }
                }
            });
            RunState.Flags.UnionWith(new[] { RailCampaign.Started, RailCampaign.ArrivalSeen, RailCampaign.CrossingPassed,
                RailCampaign.Validated, RailCampaign.FreightArrived, RailCampaign.SignalReleased });
            coachStart = RailLayout.CoachStart;
            var body = Item("Actual fixture coach", coachStart); coach = body.AddComponent<Rigidbody>(); coach.isKinematic = true; coach.detectCollisions = true;
            var deck = body.AddComponent<BoxCollider>(); deck.center = new Vector3(0, .67f, 0); deck.size = new Vector3(RailLayout.CoachWidth, .16f, RailLayout.CoachLength);
            var supports = new Collider[] {
                Box("Declared west support", coachStart + new Vector3(-.8f,.075f,4), new Vector3(1.6f,.15f,40)),
                Box("Declared east support", coachStart + new Vector3(.8f,.075f,4), new Vector3(1.6f,.15f,40))
            };
            var low = Box("Actual duct gate", coachStart + Vector3.left * 20, Vector3.one);
            var high = Box("Actual platform gate", coachStart + Vector3.left * 25, Vector3.one);
            var portal = Box("Actual raised portal", coachStart + new Vector3(0,2,18), new Vector3(4,4,.2f));
            departure = root.AddComponent<RailDeparture>(); departure.Configure(low.transform, high.transform, Item("Actual receiver", coachStart + Vector3.left * 30).transform, coach.transform);
            departure.SetPortal(portal.transform); departure.SetTransitPhysics(coach, supports, deck);
            Set(manager, "railDeparture", departure); departure.Completed += () => manager.FinishRail();
            agents = new[] { possession.SpawnInitial(RailLayout.BoardingStops[0] + Vector3.up * .03f, 0), new AmoebaAgent { Id = 2 } };
            AmoebaController.Create(RailLayout.BoardingStops[1] + Vector3.up * .03f, agents[1]); possession.Agents.Add(agents[1]);
            foreach (var agent in agents) agent.Blob.transform.SetParent(root.transform, true);
            control = Item("Actual departure control", RailLayout.BoardingStops[0] + Vector3.forward * .5f).AddComponent<RailWorkControl>(); control.kind = RailWork.Departure;
            Physics.SyncTransforms(); foreach (var agent in agents) agent.Blob.Controller.Move(Vector3.down * .10f);
            yield return null; yield return null;
            Assert.That(departure.TransitHardwareReady, Is.True);
            Assert.That(departure.ReadyToDepart(out string reason), Is.True, reason);
            Assert.That(agents[0] != agents[1] && agents[0].Id != agents[1].Id, Is.True);
            foreach (var agent in agents) Assert.That(agent.Blob.Controller.isGrounded && RailCampaign.FullyInside(agent, RailLayout.Boarding), Is.True);
            boarderStarts = new[] { agents[0].Position, agents[1].Position };
        }

        IEnumerator StartTrip()
        {
            var context = new InteractContext { agent = possession.Active, host = null, position = possession.Active.Position };
            Assert.That(departure.TryDepart(context, control, out string reason), Is.True, reason);
            Assert.That(manager.State, Is.EqualTo(GameState.Ending));
            yield return null;
            Assert.That(RailResolution.Instance && RailResolution.Instance.Running, Is.True);
            // Normal skip input only removes optional film delay, never physical travel.
            GameInput.SimJump = true; yield return null; GameInput.SimJump = false;
        }

        IEnumerator WaitUntil(Func<bool> predicate, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Bounded physical callback-fault scenario did not finish.");
        }

        void RestoredAt(float metres)
        {
            Assert.That(Vector3.Distance(coach.position, coachStart + Vector3.forward * metres), Is.LessThan(.006f));
            for (int i = 0; i < agents.Length; i++)
            {
                Assert.That(agents[i].Blob.Controller.enabled && RailCampaign.Free(agents[i]), Is.True);
                Assert.That(agents[i].Blob.transform.parent, Is.EqualTo(root.transform));
                Assert.That(Vector3.Distance(agents[i].Position, boarderStarts[i] + Vector3.forward * metres), Is.LessThan(.02f));
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ThrowingSubtitleCannotInterruptPhysicalDepartureOrPublication()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[NorthlineDeparture\] Subtitle listener failed: Injected subtitle failure"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[NorthlineDeparture\] Subtitle listener failed: Injected subtitle failure"));
            subtitle = (speaker, line) => throw new InvalidOperationException("Injected subtitle failure"); GameEvents.Subtitle += subtitle;
            int endings = 0; ending = data => endings++; GameEvents.Ending += ending;
            yield return StartTrip(); yield return WaitUntil(() => manager.State == GameState.Ended, 9);
            Assert.That(RunState.Has(RailCampaign.Departed) && RunState.MissionIndex == 2, Is.True); Assert.That(endings, Is.EqualTo(1));
            RestoredAt(RailResolution.TravelMetres); yield return null; Assert.That(RailResolution.Instance, Is.Null);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ThrowingCompletionToastSeesCommittedStateAndCannotPreventEarnedEnding()
        {
            int notifications = 0, endings = 0; GameState stateAtToast = GameState.Playing; int indexAtToast = -1;
            toast = (message, color) => {
                if (!message.StartsWith("MISSION COMPLETE", StringComparison.Ordinal)) return;
                notifications++; stateAtToast = manager.State; indexAtToast = RunState.MissionIndex;
                Assert.That(RunState.Has(RailCampaign.Departed), Is.True);
                throw new InvalidOperationException("Injected final progression notification failure");
            }; GameEvents.Toast += toast;
            ending = data => {
                endings++; Assert.That(manager.State, Is.EqualTo(GameState.Ended)); Assert.That(RunState.Has(RailCampaign.Departed), Is.True);
                Assert.That(RunState.MissionIndex, Is.EqualTo(2));
            }; GameEvents.Ending += ending;
            yield return StartTrip(); yield return WaitUntil(() => manager.State == GameState.Ended && endings == 1, 9);
            Assert.That(notifications, Is.EqualTo(1)); Assert.That(stateAtToast, Is.EqualTo(GameState.Ended)); Assert.That(indexAtToast, Is.EqualTo(2));
            RestoredAt(RailResolution.TravelMetres); yield return null; Assert.That(RailResolution.Instance, Is.Null);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ThrowingSubtitleCannotInterruptActualCoordinatorDisableRollback()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[NorthlineDeparture\] Subtitle listener failed: Injected subtitle failure"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[NorthlineDeparture\] Subtitle listener failed: Injected subtitle failure"));
            subtitle = (speaker, line) => throw new InvalidOperationException("Injected subtitle failure"); GameEvents.Subtitle += subtitle;
            yield return StartTrip();
            yield return WaitUntil(() => RailResolution.Instance && RailResolution.Instance.Travelled > .15f && RailResolution.Instance.Travelled < 7.5f, 2);
            departure.enabled = false;
            Assert.That(manager.State, Is.EqualTo(GameState.Playing)); Assert.That(RunState.Has(RailCampaign.Departed), Is.False);
            Assert.That(departure.DepartureAuthorized, Is.False); RestoredAt(0);
            yield return null; Assert.That(RailResolution.Instance, Is.Null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (subtitle != null) GameEvents.Subtitle -= subtitle;
            if (toast != null) GameEvents.Toast -= toast;
            if (ending != null) GameEvents.Ending -= ending;
            if (RailResolution.Instance) Object.Destroy(RailResolution.Instance.gameObject);
            if (root) Object.Destroy(root); yield return null;
            yield return CinematicFilmLifetime.Drain(10);
            GameEvents.ClearAll(); GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimJump = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); persistenceScope?.Dispose();
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
