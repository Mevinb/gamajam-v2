using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class IslandBoardingLivePatrolTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator RileyClinicToDeckUsesPhysicalViaRouteAndNaturalPatrols()
            => IslandRescueAcceptance.PeaceClinicBoardingWithLivePatrols();

        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = GameInput.SimAbility = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
