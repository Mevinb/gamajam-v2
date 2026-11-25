using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class MissionPropHoldTests
    {
        [UnityTest, Timeout(120000)] public IEnumerator CaptureCancelsBeforeAutoEject() => MissionPropAcceptance.RunCase("capture");
        [UnityTest, Timeout(120000)] public IEnumerator SwappingToAnotherHostCancels() => MissionPropAcceptance.RunCase("swap");
        [UnityTest, Timeout(120000)] public IEnumerator ReleasingOriginalHostCancels() => MissionPropAcceptance.RunCase("release");
        [UnityTest, Timeout(120000)] public IEnumerator LosingAuthorizationCancels() => MissionPropAcceptance.RunCase("authorization");
        [UnityTest, Timeout(120000)] public IEnumerator LosingPrerequisiteCancels() => MissionPropAcceptance.RunCase("prerequisite");
        [UnityTest, Timeout(120000)] public IEnumerator LeavingHoldToleranceCancels() => MissionPropAcceptance.RunCase("range");
        [UnityTest, Timeout(120000)] public IEnumerator ResettingCancels() => MissionPropAcceptance.RunCase("resetting");
        [UnityTest, Timeout(120000)] public IEnumerator InactiveSameAuthorizedHostCanFinish() => MissionPropAcceptance.RunCase("inactive");
        [UnityTest, Timeout(120000)] public IEnumerator PausePreservesHoldTimer() => MissionPropAcceptance.RunCase("pause");
        [UnityTest, Timeout(120000)] public IEnumerator LegitimateReachDriftRetainsExistingTolerance() => MissionPropAcceptance.RunCase("within tolerance");

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
