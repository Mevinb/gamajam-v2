using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Parasite.Tests
{
    public sealed class RailCampaignTests
    {
        [UnityTest, Timeout(60000)] public IEnumerator ContinuationRequiresTheCompleted23Summary() => RailAcceptance.ContinuationRules();
        [UnityTest, Timeout(600000)] public IEnumerator PeacePhysicalChapterAndRecovery() => RailAcceptance.RunChapter(EndingOutcome.Peace, true);
        [UnityTest, Timeout(420000)] public IEnumerator DominionPhysicalChapter() => RailAcceptance.RunChapter(EndingOutcome.Dominion, false);
        [UnityTest, Timeout(600000)] public IEnumerator ActualCoordinatorDisableDuringMotion() => RailAcceptance.ActualCoordinatorDisableCancelsTrip();
        [UnityTest, Timeout(600000)] public IEnumerator ActualCoordinatorDestroyDuringMotion() => RailAcceptance.ActualCoordinatorDestroyUsesCheckpointRecovery();
        [UnityTest, Timeout(600000)] public IEnumerator ActualCoachDestroyDuringMotion() => RailAcceptance.ActualCoachDestroyUsesCheckpointRecovery();
        [UnityTest, Timeout(600000)] public IEnumerator ActualCoachRebindDuringMotion() => RailAcceptance.ActualCoachRebindCannotMoveReplacement();
        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
