using System.Collections;
using NUnit.Framework;
using Parasite.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Parasite.Tests
{
    public class IslandRescueTests
    {
        [UnityTest, Timeout(600000)] public IEnumerator PeacePeopleSignalAndLaunch() => IslandRescueAcceptance.Branch(EndingOutcome.Peace);
        [UnityTest, Timeout(600000)] public IEnumerator DominionCouriersSignalAndLaunch() => IslandRescueAcceptance.Branch(EndingOutcome.Dominion);
        [UnityTest, Timeout(300000)] public IEnumerator CapturedEscortAndPartialRosterReload() => IslandRescueAcceptance.PeaceLeaderAndPartialRecovery();
        [UnityTest, Timeout(300000)] public IEnumerator CourierWrongLaneAndCapturedRetry() => IslandRescueAcceptance.DominionWrongReleaseAndCapture();
        [UnityTest, Timeout(300000)] public IEnumerator SignalReachCustodyAndCheckpoint() => IslandRescueAcceptance.SignalCustodyAndCheckpoint();
        [UnityTest, Timeout(180000)] public IEnumerator AlliesSeeWithoutReportingAndNeutralStillReports() => IslandRescueAcceptance.AllyWitnessPolicy();
        [UnityTest, Timeout(180000)] public IEnumerator VisibleSentryBreaksEscortIntoCoverRegroup() => IslandRescueAcceptance.PeaceGuardExposure();

        [UnityTest, Timeout(300000)] public IEnumerator ReleasedLeaderSameBodyRegainNeedsNewE() => IslandRescueAcceptance.PeaceRegainedHostNeedsRegroup(false);
        [UnityTest, Timeout(300000)] public IEnumerator CapturedLeaderSameBodyRegainNeedsNewE() => IslandRescueAcceptance.PeaceRegainedHostNeedsRegroup(true);
        [UnityTest, Timeout(300000)] public IEnumerator RenewedCourierPossessionCancelsOldRelease() => IslandRescueAcceptance.CourierRenewedPossessionCancelsRelease();
        [UnityTest, Timeout(300000)] public IEnumerator CourierCaptureThenImmediateEjectCancelsMark() => IslandRescueAcceptance.CourierImmediateCaptureEjectCancelsMark();
        [UnityTest, Timeout(180000)] public IEnumerator WestOuterSlotCannotBoardBeforeInset() => IslandRescueAcceptance.BoardingToleranceRequiresInset(0);
        [UnityTest, Timeout(180000)] public IEnumerator EastOuterSlotCannotBoardBeforeInset() => IslandRescueAcceptance.BoardingToleranceRequiresInset(2);

        [UnityTest, Timeout(180000)] public IEnumerator ParkedPassengersLeavePhysicalGangwayUsable() => IslandRescueAcceptance.ParkedPassengersKeepGangwayClear();

        [UnityTest, Timeout(180000)] public IEnumerator DeliveredClinicPassengerCanBeSidesteppedWithRealInput() => IslandRescueAcceptance.DeliveredClinicPassengerRequiresPhysicalSidestep();

        [UnityTearDown] public IEnumerator Cleanup()
        {
            GameInput.Simulated = false; GameInput.SimMove = Vector2.zero;
            GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimJump = GameInput.SimSprint = false;
            Time.timeScale = 1; RunState.NewGame(); SceneManager.LoadScene(GameManager.MenuScene); yield return null;
        }
    }
}
