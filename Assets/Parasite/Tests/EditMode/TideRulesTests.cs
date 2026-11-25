using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;

namespace Parasite.Tests
{
    public class TideRulesTests
    {
        [TestCase(0, TidePhase.Open)] [TestCase(5.99f, TidePhase.Open)]
        [TestCase(6, TidePhase.Warning)] [TestCase(7.99f, TidePhase.Warning)]
        [TestCase(8, TidePhase.Surge)] [TestCase(11.99f, TidePhase.Surge)] [TestCase(12, TidePhase.Open)]
        public void PhaseBoundariesGiveAFullTelegraphedWindow(float age, TidePhase expected) => Assert.AreEqual(expected, TideRules.Phase(age));
        [Test] public void PausedTimeDoesNotConsumeTheTraversalWindow()
        {
            Assert.AreEqual(2, TideRules.Advance(2, 20, false));
            Assert.AreEqual(3, TideRules.Advance(2, 1, true));
        }
        [Test] public void ExitNeedsAnActualTraversalFromOutside()
        {
            Assert.IsFalse(TideRules.ReachedExit(new Vector3(-26, .15f, -16), false));
            Assert.IsFalse(TideRules.ReachedExit(new Vector3(-26, .15f, -17), true));
            Assert.IsTrue(TideRules.ReachedExit(new Vector3(-26, .15f, -16), true));
        }
    }
}
