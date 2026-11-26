using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public sealed class PossessSetupObstructionTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator OwnedOpaqueWallForcesSameNpcFallbackThenRealPossessionAndMovement()
            => AutoTest.Possess02OwnedObstructionAcceptance();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            GameInput.Simulated = false; Time.timeScale = 1f;
            RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
