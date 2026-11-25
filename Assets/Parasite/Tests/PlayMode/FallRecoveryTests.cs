using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class FallRecoveryTests
    {
        [UnityTest, Timeout(180000)] public IEnumerator ActiveBlobRiverFall() => FallRecoveryAcceptance.RunCase("active blob");
        [UnityTest, Timeout(180000)] public IEnumerator ActiveHostRiverFall() => FallRecoveryAcceptance.RunCase("active host");
        [UnityTest, Timeout(180000)] public IEnumerator InactiveBlobRiverFall() => FallRecoveryAcceptance.RunCase("inactive blob");
        [UnityTest, Timeout(180000)] public IEnumerator InactiveHostRiverFall() => FallRecoveryAcceptance.RunCase("inactive host");
        [UnityTest, Timeout(180000)] public IEnumerator SimultaneousFallsResetOnlyOnce() => FallRecoveryAcceptance.RunCase("simultaneous");
        [UnityTest, Timeout(180000)] public IEnumerator InvalidCheckpointFallsBackToSafeLanding() => FallRecoveryAcceptance.RunCase("invalid checkpoint");
        [UnityTest, Timeout(180000)] public IEnumerator StoryNpcAndStandaloneFixturesAreExcluded() => FallRecoveryAcceptance.Exclusions();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
