using NUnit.Framework;
using Parasite.Missions;

namespace Parasite.Tests
{
    public class AfterlakeRulesTests
    {
        [SetUp] public void Reset() => RunState.NewGame();
        [TearDown] public void Cleanup() => RunState.NewGame();

        [Test]
        public void ContinueRequiresBothAnExplicitOutcomeAndResolvedExtraction()
        {
            Assert.IsFalse(RunState.BeginAfterlake());
            RunState.Flags.Add(MissionFlags.Escaped);
            Assert.IsFalse(RunState.BeginAfterlake(), "An escaped flag alone cannot invent the lake choice.");
            Assert.IsFalse(RunState.RecordLakesideOutcome(EndingOutcome.Unchosen));
            Assert.IsFalse(RunState.RecordLakesideOutcome((EndingOutcome)123));
            Assert.IsTrue(RunState.RecordLakesideOutcome(EndingOutcome.Peace));
            RunState.Flags.Remove(MissionFlags.Escaped);
            Assert.IsFalse(RunState.BeginAfterlake(), "A choice alone cannot bypass extraction resolution.");
        }

        [TestCase(EndingOutcome.Peace)]
        [TestCase(EndingOutcome.Dominion)]
        public void ContinueKeepsTheFirstFutureAndCreatesExactlyOneRiversideCheckpoint(EndingOutcome outcome)
        {
            Assert.IsTrue(RunState.RecordLakesideOutcome(outcome));
            Assert.IsFalse(RunState.RecordLakesideOutcome(outcome == EndingOutcome.Peace ? EndingOutcome.Dominion : EndingOutcome.Peace));
            RunState.Flags.Add(MissionFlags.Escaped);
            Assert.IsTrue(RunState.BeginAfterlake());
            Assert.AreEqual(outcome, RunState.LakesideOutcome);
            Assert.AreEqual(AfterlakeCampaign.FirstMissionIndex, RunState.MissionIndex);
            Assert.IsTrue(RunState.HasCheckpoint);
            Assert.AreEqual(AfterlakeCampaign.Spawn, RunState.CheckpointPosition);
            RunState.CheckpointName = "Later progress";
            Assert.IsFalse(RunState.BeginAfterlake());
            Assert.AreEqual("Later progress", RunState.CheckpointName, "Duplicate continuation cannot erase a later checkpoint.");
        }

        [Test]
        public void NewGameResetsTheContinuationAndTheFutureTogether()
        {
            RunState.RecordLakesideOutcome(EndingOutcome.Dominion);
            RunState.Flags.Add(MissionFlags.Escaped);
            RunState.BeginAfterlake();
            RunState.Flags.Add(AfterlakeCampaign.ChannelOpened);
            RunState.NewGame();
            Assert.AreEqual(EndingOutcome.Unchosen, RunState.LakesideOutcome);
            Assert.IsFalse(RunState.AfterlakeStarted);
            Assert.IsFalse(RunState.HasCheckpoint);
            Assert.AreEqual(0, RunState.MissionIndex);
            Assert.IsEmpty(RunState.Flags);
        }
    }
}
