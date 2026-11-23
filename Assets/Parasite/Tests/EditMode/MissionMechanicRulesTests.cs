using System.Collections.Generic;
using NUnit.Framework;
using Parasite.Missions;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Tests
{
    public class TailRulesTests
    {
        static TailSample S(float d, bool seen = false, bool blob = false, bool moving = false, bool running = false, bool glance = false, bool grace = false) =>
            new() { distance = d, seenByTarget = seen, followerIsBlob = blob, followerMoving = moving, followerRunning = running, targetGlancing = glance, ignoreCrowding = grace };

        [TestCase(2f, TailBand.TooClose)]
        [TestCase(TailRules.TooClose, TailBand.InBand)]
        [TestCase(12f, TailBand.InBand)]
        [TestCase(TailRules.BandMax, TailBand.InBand)]
        [TestCase(23f, TailBand.Far)]
        [TestCase(30f, TailBand.Lost)]
        public void Bands(float d, TailBand expected) => Assert.AreEqual(expected, TailRules.Band(d));

        [Test]
        public void ComfortableUnseenFollowerCalmsHerDown()
        {
            Assert.Less(TailRules.AlertnessRate(S(10f)), 0f);
            Assert.AreEqual(0f, TailRules.Step(5f, S(10f), 1f));
        }

        [Test]
        public void CrowdingRaisesAlertnessUnlessSheIsStillPacking()
        {
            Assert.Greater(TailRules.AlertnessRate(S(2f)), 0f);
            Assert.Greater(TailRules.AlertnessRate(S(2f, seen: true)), TailRules.AlertnessRate(S(2f)));
            Assert.Less(TailRules.AlertnessRate(S(2f, grace: true)), 0f);
        }

        [Test]
        public void FreezingDuringAGlanceIsSaferThanMoving()
        {
            float still = TailRules.AlertnessRate(S(9f, seen: true, glance: true));
            float moving = TailRules.AlertnessRate(S(9f, seen: true, moving: true, glance: true));
            float running = TailRules.AlertnessRate(S(9f, seen: true, moving: true, running: true, glance: true));
            Assert.Greater(still, 0f);
            Assert.Greater(moving, still);
            Assert.Greater(running, moving);
            Assert.Less(TailRules.AlertnessRate(S(TailRules.GlanceRange + 1f, seen: true, moving: true, glance: true)), 0f, "glances only reach so far");
        }

        [Test]
        public void SeeingAFreeAmoebaIsTheFastestWayToBlowIt()
        {
            Assert.Greater(TailRules.AlertnessRate(S(6f, seen: true, blob: true)), TailRules.AlertnessRate(S(6f, seen: true, moving: true, glance: true)));
            Assert.Less(TailRules.AlertnessRate(S(6f, seen: false, blob: true)), 0f, "an unseen amoeba is fine");
        }

        [Test]
        public void SpottedAndLostThresholds()
        {
            float a = 0f;
            for (int i = 0; i < 100 && !TailRules.Spotted(a); i++) a = TailRules.Step(a, S(1f, seen: true), 0.1f);
            Assert.IsTrue(TailRules.Spotted(a));
            Assert.AreEqual(TailRules.SpottedAt, a);
            float lost = 0f;
            for (int i = 0; i < 49; i++) lost = TailRules.LostTimer(lost, 30f, 0.1f);
            Assert.IsFalse(TailRules.Lost(lost));
            lost = TailRules.LostTimer(lost, 30f, 0.2f);
            Assert.IsTrue(TailRules.Lost(lost));
            Assert.AreEqual(0f, TailRules.LostTimer(lost, 10f, 0.1f), "closing in resets the lost timer");
        }

        [Test]
        public void GlanceRhythmCycles()
        {
            Assert.AreEqual(TailRules.GlanceInterval(0), TailRules.GlanceInterval(4));
            Assert.AreEqual(TailRules.GlanceInterval(-1), TailRules.GlanceInterval(3));
            Assert.Greater(TailRules.GlanceInterval(1), 0f);
        }
    }

    public class BioScanRulesTests
    {
        [Test]
        public void OnlyPossessedHumansCarryASignature()
        {
            Assert.IsTrue(BioScanRules.Detectable(HostState.Possessed, false));
            Assert.IsFalse(BioScanRules.Detectable(HostState.Possessed, true));
            Assert.IsFalse(BioScanRules.Detectable(HostState.Normal, false));
            Assert.IsFalse(BioScanRules.Detectable(HostState.Unconscious, false));
        }

        [Test]
        public void ConeGeometry()
        {
            Vector3 o = Vector3.zero, f = Vector3.forward;
            Assert.IsTrue(BioScanRules.InCone(o, f, new Vector3(0, 0, 5), BioScanRules.Range, BioScanRules.HalfAngle));
            Assert.IsTrue(BioScanRules.InCone(o, f, new Vector3(1.5f, 0.3f, 5), BioScanRules.Range, BioScanRules.HalfAngle));
            Assert.IsFalse(BioScanRules.InCone(o, f, new Vector3(4f, 0, 4), BioScanRules.Range, BioScanRules.HalfAngle), "outside the half-angle");
            Assert.IsFalse(BioScanRules.InCone(o, f, new Vector3(0, 0, -4), BioScanRules.Range, BioScanRules.HalfAngle), "behind the hunter");
            Assert.IsFalse(BioScanRules.InCone(o, f, new Vector3(0, 0, BioScanRules.Range + 0.5f), BioScanRules.Range, BioScanRules.HalfAngle), "out of range");
        }

        [Test]
        public void CloserReadsFasterAndFadesWhenBroken()
        {
            Assert.Greater(BioScanRules.ScanRate(1.5f, BioScanRules.Range), BioScanRules.ScanRate(7f, BioScanRules.Range));
            float m = 0f;
            for (int i = 0; i < 40 && !BioScanRules.Burned(m); i++) m = BioScanRules.Step(m, true, BioScanRules.ScanRate(3f, BioScanRules.Range), 0.1f);
            Assert.IsTrue(BioScanRules.Burned(m), "a few seconds in the cone burns the body");
            Assert.Less(BioScanRules.Step(0.6f, false, 0f, 1f), 0.6f);
            Assert.AreEqual(0f, BioScanRules.Step(0.1f, false, 0f, 2f));
        }
    }

    public class BlackoutRulesTests
    {
        [Test]
        public void WindowTiming()
        {
            Assert.AreEqual(BlackoutRules.WindowSeconds, BlackoutRules.Remaining(-1f, 100f, BlackoutRules.WindowSeconds), "no window before the first cut");
            Assert.AreEqual(25f, BlackoutRules.Remaining(10f, 20f, 35f), 1e-4f);
            Assert.AreEqual(0f, BlackoutRules.Remaining(10f, 90f, 35f));
            Assert.IsFalse(BlackoutRules.Expired(-1f, 999f, 35f));
            Assert.IsFalse(BlackoutRules.Expired(10f, 44f, 35f));
            Assert.IsTrue(BlackoutRules.Expired(10f, 46f, 35f));
        }

        [Test]
        public void EveryIsolatorNeedsADifferentBody()
        {
            var used = new HashSet<int>();
            Assert.IsTrue(BlackoutRules.CanOperate(7, used));
            used.Add(7);
            Assert.IsFalse(BlackoutRules.CanOperate(7, used));
            Assert.IsTrue(BlackoutRules.CanOperate(8, used));
        }

        [Test]
        public void ThreeCutsCompleteTheBlackout()
        {
            Assert.IsFalse(BlackoutRules.Complete(2));
            Assert.IsTrue(BlackoutRules.Complete(BlackoutRules.Required));
        }
    }
}
