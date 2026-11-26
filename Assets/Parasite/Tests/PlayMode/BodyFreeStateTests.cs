using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public sealed class BodyFreeStateTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DestroyedInactiveBodyCannotAuthorizeTheFerryOrFinalization()
        {
            yield return BodyFreeStateAcceptance.DestroyedInactiveBodyCannotAuthorizeTheFerryOrFinalization(line =>
            {
                LogAssert.Expect(LogType.Log, line); // Only the exact fixture-owned measurement is expected.
                Debug.Log(line);
            });
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LiveHiddenAndDisabledBlobRemainsFreeWithoutChangingIdentity()
        {
            BodyFreeStateAcceptance.LiveHiddenAndDisabledBlobRemainsFreeWithoutChangingIdentity();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = GameInput.SimAbility = false;
            GameInput.Simulated = false; Time.timeScale = 1;
            RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
