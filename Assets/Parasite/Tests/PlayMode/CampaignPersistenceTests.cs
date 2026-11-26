using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Parasite.Tests
{
    // Explicit durable-state fixtures exercise real disk, menu callbacks and normal scene reconstruction.
    // They do not claim to earn the prepared story flags through a human/physical campaign playthrough.
    public sealed class CampaignPersistenceTests
    {
        string directory, ownership;
        IDisposable persistenceScope;
        string Primary => Path.Combine(directory, CampaignSaveStore.FileName);

        [UnitySetUp]
        public IEnumerator StartIsolatedGame()
        {
            ownership = Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetTempPath(), "parasite-persistence-playmode-" + ownership);
            Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, ".test-owner"), ownership);
            persistenceScope = CampaignPersistence.UseTestDirectory(directory);
            CampaignPersistence.BeginNewGame(); OpeningCinematic.CancelPendingNewGame();
            SceneManager.LoadScene(GameManager.CityScene); yield return null; yield return null;
            Assert.That(GameManager.Instance.IsPlaying && PossessionManager.Instance.Active.IsFree, Is.True);
        }

        static Button Button(Transform root, string label) => root.GetComponentsInChildren<Button>(true)
            .Single(b => b.GetComponentInChildren<Text>(true).text == label);

        [UnityTest, Timeout(60000)]
        public IEnumerator ActualPauseQuitAndTitleResumeRebuildTheSavedFreeBodyAndProgress()
        {
            var manager = GameManager.Instance;
            MissionManager.SetFlag(MissionFlags.NewsRead);
            RunState.PlayTime = 321.75f; RunState.HostsPossessed = 7; RunState.Resets = 2;
            // Use the real authored checkpoint. Inventing a new name inside Storm Drain's trigger
            // is immediately re-labelled by that same checkpoint zone after an ordinary reload.
            manager.SetPaused(true); yield return null;
            Button(HUDController.Instance.transform, "Quit to Menu").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameManager.MenuScene));
            Assert.That(CampaignSaveStore.TryLoad(directory, out var expected, out _, out var error), Is.True, error);
            Assert.That(expected.flags, Does.Contain(MissionFlags.NewsRead));
            RunState.NewGame(); // Simulate lost process memory; the title action must read the actual file.
            OpeningCinematic.RequestForNewGame(); // A stale new-game request must never hijack Resume.
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            var resume = Button(menu.transform, "Continue checkpoint"); Assert.That(resume.interactable, Is.True);
            resume.onClick.Invoke(); yield return null; yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameManager.CityScene));
            Assert.That(GameManager.Instance.IsPlaying, Is.True);
            Assert.That(RunState.Has(MissionFlags.NewsRead), Is.True);
            Assert.That(RunState.HostsPossessed, Is.EqualTo(7)); Assert.That(RunState.Resets, Is.EqualTo(2));
            Assert.That(RunState.PlayTime, Is.InRange(expected.playTime, expected.playTime + 2));
            Assert.That(RunState.CheckpointName, Is.EqualTo(expected.checkpointName));
            Assert.That(PossessionManager.Instance.Agents.Count, Is.EqualTo(1));
            Assert.That(PossessionManager.Instance.Active.IsFree, Is.True, "Normal checkpoint reconstruction makes one fresh free body; it never serializes a host/controller.");
            Assert.That(Vector3.Distance(PossessionManager.Instance.Active.Position, expected.checkpointPosition), Is.LessThan(.6f));
            Assert.That(OpeningCinematic.Instance && OpeningCinematic.Instance.Running, Is.False);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator EarnedSummaryResumeShowsActualContinueAndPreservesTheChosenLakeOutcome()
        {
            // This is an explicit prepared earned-summary save fixture, not fresh ending credit.
            RunState.RecordLakesideOutcome(EndingOutcome.Peace); RunState.Flags.Add(MissionFlags.Escaped); RunState.MissionIndex = 14;
            Assert.That(CampaignSaveStore.TrySave(directory, CampaignSaveData.FromRunState(true), out var error), Is.True, error);
            CampaignPersistence.LeaveSession(); RunState.NewGame();
            SceneManager.LoadScene(GameManager.MenuScene); yield return null; yield return null;
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            Button(menu.transform, "Continue checkpoint").onClick.Invoke();
            yield return null; yield return null; yield return null;
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Ended));
            Assert.That(RunState.MissionIndex, Is.EqualTo(14)); Assert.That(RunState.LakesideOutcome, Is.EqualTo(EndingOutcome.Peace));
            var next = Button(HUDController.Instance.transform, "Continue · After the lake");
            Assert.That(next.gameObject.activeInHierarchy && next.interactable, Is.True);
            next.onClick.Invoke(); yield return null; yield return null;
            Assert.That(RunState.AfterlakeStarted && GameManager.Instance.IsPlaying, Is.True);
            Assert.That(RunState.MissionIndex, Is.EqualTo(AfterlakeCampaign.FirstMissionIndex));
            Assert.That(RunState.LakesideOutcome, Is.EqualTo(EndingOutcome.Peace));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FlagAndCheckpointStackIsSavedAfterMissionAdvancementFinishes()
        {
            // Explicit logical progress fixture: the test concerns the save boundary, not earning these story tasks.
            MissionManager.SetFlag(MissionFlags.FirstHost);
            MissionManager.SetFlag(MissionFlags.NewsRead); MissionManager.SetFlag(MissionFlags.Eavesdropped);
            MissionManager.SetFlag(MissionFlags.TailDone); MissionManager.SetFlag(MissionFlags.DeadDrop);
            int advanced = RunState.MissionIndex; Assert.That(advanced, Is.GreaterThan(0));
            yield return null; yield return null;
            Assert.That(CampaignSaveStore.TryLoad(directory, out var saved, out _, out var error), Is.True, error);
            Assert.That(saved.missionIndex, Is.EqualTo(advanced));
            Assert.That(saved.flags, Does.Contain(MissionFlags.DeadDrop));
            Assert.That(saved.checkpointName, Is.EqualTo(RunState.CheckpointName));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BareFixtureNewGameAndSceneLoadsCannotActivateOrOverwriteTheSaveSlot()
        {
            Assert.That(CampaignPersistence.Flush(GameManager.Instance), Is.True);
            byte[] before = File.ReadAllBytes(Primary);
            CampaignPersistence.LeaveSession(); RunState.NewGame();
            SceneManager.LoadScene(GameManager.CityScene); yield return null; yield return null;
            MissionManager.SetFlag(MissionFlags.NewsRead); yield return null;
            Assert.That(CampaignPersistence.SessionActive, Is.False);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Primary));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DisappearingCheckpointAtClickDoesNotMutateRunOrLoadGameplay()
        {
            Assert.That(CampaignPersistence.Flush(GameManager.Instance), Is.True);
            GameManager.Instance.QuitToMenu(); yield return null; yield return null;
            var menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
            var resume = Button(menu.transform, "Continue checkpoint");
            string before = JsonUtility.ToJson(CampaignSaveData.FromRunState());
            File.Delete(Primary); File.Delete(Primary + ".bak");
            resume.onClick.Invoke(); yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(GameManager.MenuScene));
            Assert.That(JsonUtility.ToJson(CampaignSaveData.FromRunState()), Is.EqualTo(before));
            Assert.That(resume.interactable, Is.False);
            Assert.That(resume.GetComponentInChildren<Text>().text, Is.EqualTo("Checkpoint unavailable"));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            CampaignPersistence.LeaveSession(); OpeningCinematic.CancelPendingNewGame(); RunState.NewGame();
            GameInput.Simulated = false; Time.timeScale = 1;
            SceneManager.LoadScene(GameManager.MenuScene); yield return null;
            persistenceScope?.Dispose(); persistenceScope = null;
            string marker = Path.Combine(directory, ".test-owner");
            if (File.Exists(marker) && File.ReadAllText(marker) == ownership) Directory.Delete(directory, true);
        }
    }
}
