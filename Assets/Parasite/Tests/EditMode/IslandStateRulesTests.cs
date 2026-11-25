using NUnit.Framework;
using Parasite.Missions;
using Parasite.World;

namespace Parasite.Tests
{
    public class IslandStateRulesTests
    {
        [SetUp]public void Reset()=>RunState.NewGame();
        [TearDown]public void Cleanup()=>RunState.NewGame();
        [Test]public void IslandNeedsResolvedLakeAndCompletedFerry()
        {
            Assert.IsFalse(RunState.BeginIsland());
            RunState.RecordLakesideOutcome(EndingOutcome.Peace);RunState.Flags.Add(MissionFlags.Escaped);
            Assert.IsTrue(RunState.BeginAfterlake());
            Assert.IsFalse(RunState.BeginIsland());
            RunState.Flags.Add(AfterlakeCampaign.Departed);RunState.Flags.Remove(MissionFlags.Escaped);
            Assert.IsFalse(RunState.BeginIsland());
            RunState.Flags.Add(MissionFlags.Escaped);Assert.IsTrue(RunState.BeginIsland());
        }
        [TestCase(EndingOutcome.Peace)][TestCase(EndingOutcome.Dominion)]
        public void ChapterEntryPreservesHistoryAndCannotEraseALaterCheckpoint(EndingOutcome outcome)
        {
            RunState.RecordLakesideOutcome(outcome);RunState.Flags.Add(MissionFlags.Escaped);RunState.BeginAfterlake();RunState.Flags.Add(AfterlakeCampaign.Departed);
            RunState.Flags.Add(MissionFlags.SignedArchive);RunState.Route=EntryRoute.ServiceTunnel;
            RunState.HostsPossessed=9;RunState.Captures=1;RunState.Resets=2;RunState.PlayTime=123;
            Assert.IsTrue(RunState.BeginIsland());
            Assert.AreEqual(outcome,RunState.LakesideOutcome);Assert.IsTrue(RunState.Has(MissionFlags.SignedArchive));
            Assert.AreEqual(EntryRoute.ServiceTunnel,RunState.Route);Assert.AreEqual(9,RunState.HostsPossessed);
            Assert.AreEqual(1,RunState.Captures);Assert.AreEqual(2,RunState.Resets);Assert.AreEqual(123,RunState.PlayTime);
            Assert.AreEqual(17,RunState.MissionIndex);Assert.AreEqual(IslandCampaign.Spawn,RunState.CheckpointPosition);
            RunState.CheckpointName="Later island progress";
            Assert.IsFalse(RunState.BeginIsland());Assert.AreEqual("Later island progress",RunState.CheckpointName);
            Assert.IsFalse(RunState.RecordLakesideOutcome(outcome==EndingOutcome.Peace?EndingOutcome.Dominion:EndingOutcome.Peace));
        }
        [Test]public void FallFallbackUsesNewestChapterWhileKeepingLegacyValues()
        {
            Assert.AreEqual(CityLayout.PlayerSpawn,RunState.CurrentChapterSpawn);
            RunState.RecordLakesideOutcome(EndingOutcome.Peace);RunState.Flags.Add(MissionFlags.Escaped);RunState.BeginAfterlake();
            Assert.AreEqual(AfterlakeCampaign.Spawn,RunState.CurrentChapterSpawn);
            RunState.Flags.Add(AfterlakeCampaign.Departed);RunState.BeginIsland();
            Assert.AreEqual(IslandCampaign.Spawn,RunState.CurrentChapterSpawn);
            RunState.NewGame();Assert.IsFalse(RunState.IslandStarted);Assert.AreEqual(CityLayout.PlayerSpawn,RunState.CurrentChapterSpawn);
        }
    }
}
