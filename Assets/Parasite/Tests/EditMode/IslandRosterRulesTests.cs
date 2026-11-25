using NUnit.Framework;
using Parasite.Missions;

namespace Parasite.Tests
{
    public class IslandRosterRulesTests
    {
        [SetUp] public void Reset() => RunState.NewGame();
        [TearDown] public void Cleanup() => RunState.NewGame();
        [Test] public void AnUnvisitedCoverCannotBecomeTheRecoveryAnchor()
        {
            Assert.AreEqual(-1, IslandRoster.SavedCover("riley"));
            RunState.Flags.Add(IslandRoster.CoverFlag("morgan", 3));
            Assert.AreEqual(-1, IslandRoster.SavedCover("riley"));
            RunState.Flags.Add(IslandRoster.CoverFlag("riley", 0));
            Assert.AreEqual(0, IslandRoster.SavedCover("riley"));
            RunState.Flags.Add(IslandRoster.CoverFlag("riley", 2));
            Assert.AreEqual(2, IslandRoster.SavedCover("riley"));
        }
        [Test] public void PartialCompletionAndBoardingDoNotInventAMissingPerson()
        {
            foreach (string id in IslandRoster.Ids) RunState.Flags.Add(IslandRoster.Boarded(id));
            RunState.Flags.Add(IslandRoster.Delivered("riley"));
            RunState.Flags.Add(IslandRoster.Delivered("morgan"));
            RunState.Flags.Add(IslandRoster.Delivered("unrelated"));
            Assert.IsTrue(IslandRoster.AllBoarded);
            Assert.IsFalse(IslandRoster.AllDelivered);
            RunState.Flags.Add(IslandRoster.Delivered("avery"));
            Assert.IsTrue(IslandRoster.AllDelivered);
            RunState.NewGame();
            Assert.IsFalse(IslandRoster.AllDelivered);
            Assert.IsFalse(IslandRoster.AllBoarded);
        }
        [Test] public void AssemblyVolumeFitsTheRealForedeckAndRejectsTheGangwayAndWater()
        {
            foreach (var slot in IslandRoster.Board) Assert.IsTrue(IslandRoster.DeckBounds.Contains(slot));
            Assert.IsFalse(IslandRoster.DeckBounds.Contains(IslandCampaign.At(34, .55f, 42.8f)));
            Assert.IsFalse(IslandRoster.DeckBounds.Contains(IslandCampaign.At(34, -.3f, 44.5f)));
            Assert.IsFalse(IslandRoster.DeckBounds.Contains(IslandCampaign.At(37, .55f, 44.5f)));
        }
        [Test] public void PassengerParkingKeepsTheGangwayAisleAndCabinApproachClear()
        {
            const float humanRadius = .3f;
            float occupiedRadius = humanRadius + IslandRoster.BoardArrivalTolerance;
            float aisleMin = IslandCampaign.At(33.1f, 0, 0).x, aisleMax = IslandCampaign.At(34.9f, 0, 0).x;
            foreach (var berth in IslandRoster.Board)
            {
                Assert.IsTrue(berth.x + occupiedRadius < aisleMin || berth.x - occupiedRadius > aisleMax,
                    "Worst-case permitted parking still leaves the full physical gangway lane clear.");
                Assert.Less(berth.z + occupiedRadius, IslandCampaign.At(0, 0, 47.75f).z,
                    "The body and permitted arrival radius stay in front of the actual cabin collision.");
            }
            Assert.Greater(IslandRoster.BoardArrivalTolerance, .25f,
                "Final arrival remains above the existing .25m human stopping distance.");
        }
    }
}
