using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Parasite.Tests
{
    public class IslandWallVisualTests
    {
        [UnityTest,Timeout(240000)]public IEnumerator AuthoredWallMapsPhysicsAndRenderedApertures()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Actual wall pixel proof requires a GPU.");
            yield return IslandWallAcceptance.Run(SettingsUITests.Capture);
        }
        [UnityTest,Explicit("Optional staged art-QA pairs; select this exact method intentionally."),Timeout(240000)]public IEnumerator StagedNormalShadedWallArtBeforeAfterPairs()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)Assert.Ignore("Staged normal-shaded art-QA requires a GPU.");
            yield return IslandWallArtCapture.Run();
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            GameInput.Simulated=false;GameInput.SimMove=Vector2.zero;
            GameInput.SimInteract=GameInput.SimLeave=GameInput.SimSplit=GameInput.SimSwitch=GameInput.SimJump=GameInput.SimSprint=GameInput.SimAbility=false;
            Time.timeScale=1;RunState.NewGame();SceneManager.LoadScene(GameManager.MenuScene);yield return null;
        }
    }
}
