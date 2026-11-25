using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    /// <summary>Null-device chapter logic and actual simulated-input coverage; no rendered-camera claim.</summary>
    public class HarborCampaignTests
    {
        [UnityTest, Timeout(120000)] public IEnumerator HarborContinuationRequiresResolvedIslandSummary() => HarborAcceptance.ContinuationRules();
        [UnityTest, Timeout(240000)] public IEnumerator PressureReliefPauseAndTransientCheckpoint() => HarborAcceptance.PressureOverflowPauseAndRecovery();
        [UnityTest, Timeout(600000)] public IEnumerator PeacePressureCargoAndTwoFreeBodyRelay() => HarborAcceptance.RunChapter(EndingOutcome.Peace);
        [UnityTest, Timeout(600000)] public IEnumerator DominionPressureCargoAndTwoFreeBodyRelay() => HarborAcceptance.RunChapter(EndingOutcome.Dominion);

        [UnityTest, Timeout(240000)] public IEnumerator TwoInflightCargoReroutesSurviveCheckpointWithoutPhysicalOverlap() => HarborAcceptance.TwoInflightCargoReroutesRecoverWithoutOverlap();

        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
