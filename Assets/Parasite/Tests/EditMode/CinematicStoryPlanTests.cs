using NUnit.Framework;

namespace Parasite.Tests
{
    public class CinematicStoryPlanTests
    {
        [TestCase(1920u,1080u,24,80,0,true)]
        [TestCase(1280u,720u,24,80,0,false)]
        [TestCase(1920u,1080u,30,80,0,false)]
        [TestCase(1920u,1080u,24,3,0,false)]
        [TestCase(1920u,1080u,24,80,1,false)]
        public void FullOpeningRejectsWrongFormatOrEmbeddedSoundtrack(uint width,uint height,double fps,double duration,int tracks,bool accepted)
        {
            Assert.AreEqual(accepted,CinematicStoryPlan.Opening.ValidateMetadata(width,height,fps,duration,tracks,out string reason));
            Assert.AreEqual(accepted,reason == null);
        }

        [Test]
        public void NeutralHoldRepeatsWithoutConvertingTimeIntoAnOutcome()
        {
            Assert.IsTrue(CinematicStoryPlan.Hold.Loop);
            Assert.That(CinematicStoryPlan.Hold.TimeAt(25.25),Is.EqualTo(1.25).Within(.001));
            Assert.That(CinematicStoryPlan.Arrival.TimeAt(1000),Is.LessThan(15));
            Assert.IsFalse(CinematicStoryPlan.ValidOutcome(EndingOutcome.Unchosen));
            Assert.IsFalse(CinematicStoryPlan.ValidOutcome((EndingOutcome)999));
            Assert.IsTrue(CinematicStoryPlan.ValidOutcome(EndingOutcome.Peace));
            Assert.IsTrue(CinematicStoryPlan.ValidOutcome(EndingOutcome.Dominion));
        }
    }
}
