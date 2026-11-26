using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    // Shared ferry-guard regression through both real chapter drivers. The
    // separate AfterlakeCampaignTests still own their rendered UI captures.
    public sealed class AfterlakeGuardRegressionTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator LiveBodiesCompletePeaceAndCheckpointRecovery()
            => AfterlakeAcceptance.RunChapter(EndingOutcome.Peace);

        [UnityTest, Timeout(300000)]
        public IEnumerator LiveBodiesCompleteDominionAndCheckpointRecovery()
            => AfterlakeAcceptance.RunChapter(EndingOutcome.Dominion);

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = false;
            GameInput.SimJump = GameInput.SimSprint = GameInput.SimAbility = GameInput.Simulated = false;
            Time.timeScale = 1;
            RunState.NewGame();
            SceneManager.LoadScene(GameManager.MenuScene);
            yield return null;
        }
    }
}
