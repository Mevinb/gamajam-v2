using System.Collections;
using System.Linq;
using NUnit.Framework;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.Testing;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Parasite.Tests
{
    /// <summary>Both explicit lake choices continue through real targeting/E input and held two-body operations.</summary>
    public class AfterlakeCampaignTests
    {

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = false;
            GameInput.SimMove = Vector2.zero; GameInput.Simulated = false; Time.timeScale = 1;
            RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PeaceContinuesBeyondLakeWithProtectedResidentsAndCheckpointRecovery() => AfterlakeAcceptance.RunChapter(EndingOutcome.Peace, SettingsUITests.Capture);

        [UnityTest, Timeout(300000)]
        public IEnumerator DominionContinuesBeyondLakeWithItsOwnRelayAudience() => AfterlakeAcceptance.RunChapter(EndingOutcome.Dominion, SettingsUITests.Capture);

    }
}
