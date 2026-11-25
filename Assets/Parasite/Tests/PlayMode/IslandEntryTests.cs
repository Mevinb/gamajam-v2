using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class IslandEntryTests
    {
        [UnityTest,Timeout(240000)]public IEnumerator PeaceBlobCulvert()=>IslandEntryAcceptance.Route(EndingOutcome.Peace,true);
        [UnityTest,Timeout(240000)]public IEnumerator DominionBlobCulvert()=>IslandEntryAcceptance.Route(EndingOutcome.Dominion,true);
        [UnityTest,Timeout(240000)]public IEnumerator PeaceMaintenanceGate()=>IslandEntryAcceptance.Route(EndingOutcome.Peace,false);
        [UnityTest,Timeout(240000)]public IEnumerator DominionMaintenanceGate()=>IslandEntryAcceptance.Route(EndingOutcome.Dominion,false);
        [UnityTest,Timeout(240000)]public IEnumerator HumanDenialAndPreGateTideRetry()=>IslandEntryAcceptance.TideRetryAndHumanClearance();
        [UnityTest,Timeout(240000)]public IEnumerator ActualTunnelCameraClearance()=>IslandCameraAcceptance.Run(SettingsUITests.Capture);
        [UnityTest,Timeout(240000)]public IEnumerator ArrivalCueFiresOnce()=>IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace,false);
        [UnityTest,Timeout(240000)]public IEnumerator RenderedIslandFloorSeaAndSigns()=>IslandVisualAcceptance.Run(SettingsUITests.Capture);
        [UnityTearDown]public IEnumerator Cleanup()
        {
            GameInput.Simulated=false;GameInput.SimMove=Vector2.zero;
            GameInput.SimInteract=GameInput.SimLeave=GameInput.SimSplit=GameInput.SimSwitch=GameInput.SimJump=GameInput.SimSprint=false;
            Time.timeScale=1;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;
        }
    }
}
