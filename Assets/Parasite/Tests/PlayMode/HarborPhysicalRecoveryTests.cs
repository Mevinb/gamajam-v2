using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Parasite.Tests
{
    public sealed class HarborPhysicalRecoveryTests
    {
        [UnityTest, Timeout(240000)] public IEnumerator ActualBlockedSpurCannotDeliverUntilObstacleIsRemoved() => HarborAcceptance.BlockedCargoSpurWaitsForPhysicalClearance();
        [UnityTest, Timeout(300000)] public IEnumerator ActualInactiveFreeContainmentCancelsRunningLinkAndNewPairNeedsE() => HarborAcceptance.InactiveFreeContainmentCancelsRelayAndRequiresNewE();
        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
