using NUnit.Framework;

namespace Parasite.Tests
{
    public class SuspicionRulesTests
    {
        [TestCase(0f, SuspicionLevel.Calm)]
        [TestCase(29.9f, SuspicionLevel.Calm)]
        [TestCase(30f, SuspicionLevel.Curious)]
        [TestCase(59.9f, SuspicionLevel.Curious)]
        [TestCase(60f, SuspicionLevel.Investigating)]
        [TestCase(84.9f, SuspicionLevel.Investigating)]
        [TestCase(85f, SuspicionLevel.Pursuing)]
        [TestCase(100f, SuspicionLevel.Pursuing)]
        public void LevelThresholds(float v, SuspicionLevel expected) => Assert.AreEqual(expected, SuspicionRules.LevelFor(v));

        [Test]
        public void AddClampsToRange()
        {
            Assert.AreEqual(100f, SuspicionRules.Add(90f, 50f));
            Assert.AreEqual(0f, SuspicionRules.Add(5f, -50f));
        }

        [Test]
        public void DecayOnlyAfterDelay()
        {
            Assert.AreEqual(50f, SuspicionRules.Decay(50f, SuspicionRules.DecayDelay - 0.1f, 1f));
            Assert.Less(SuspicionRules.Decay(50f, SuspicionRules.DecayDelay + 0.1f, 1f), 50f);
            Assert.AreEqual(0f, SuspicionRules.Decay(1f, 10f, 10f));
        }

        [Test]
        public void ContinuousViolations() 
        {
            Assert.IsTrue(SuspicionRules.IsContinuous(ViolationType.BlobSighted));
            Assert.IsTrue(SuspicionRules.IsContinuous(ViolationType.Trespassing));
            Assert.IsFalse(SuspicionRules.IsContinuous(ViolationType.WitnessedPossession));
        }
    }

    public class SplitRulesTests
    {
        [Test]
        public void MaxTwoAmoebas()
        {
            Assert.IsTrue(SplitRules.CanSplit(1));
            Assert.IsFalse(SplitRules.CanSplit(2));
            Assert.IsFalse(SplitRules.CanSplit(3));
        }

        [Test]
        public void SwitchCycles()
        {
            Assert.AreEqual(1, SplitRules.NextIndex(0, 2));
            Assert.AreEqual(0, SplitRules.NextIndex(1, 2));
            Assert.AreEqual(0, SplitRules.NextIndex(0, 1));
            Assert.AreEqual(-1, SplitRules.NextIndex(0, 0));
        }
    }

    public class AccessRulesTests
    {
        static AccessPermission P(Profession p) => ProfessionCatalog.Get(p).permissions;

        [Test]
        public void GuardOpensMainGateOnly()
        {
            Assert.IsTrue(AccessRules.Has(P(Profession.Guard), AccessPermission.SecurityGate));
            Assert.IsFalse(AccessRules.Has(P(Profession.Guard), AccessPermission.StaffTerminal));
            Assert.IsFalse(AccessRules.Has(P(Profession.Guard), AccessPermission.ServiceTunnel));
        }

        [Test]
        public void ScientistUsesTerminalOnly()
        {
            Assert.IsTrue(AccessRules.Has(P(Profession.Scientist), AccessPermission.StaffTerminal));
            Assert.IsFalse(AccessRules.Has(P(Profession.Scientist), AccessPermission.SecurityGate));
        }

        [Test]
        public void MaintenanceOpensTunnelOnly()
        {
            Assert.IsTrue(AccessRules.Has(P(Profession.Maintenance), AccessPermission.ServiceTunnel));
            Assert.IsFalse(AccessRules.Has(P(Profession.Maintenance), AccessPermission.SecurityGate));
        }

        [Test]
        public void CivilianDeniedEverywhereRestricted()
        {
            foreach (var need in new[] { AccessPermission.SecurityGate, AccessPermission.StaffTerminal, AccessPermission.ServiceTunnel, AccessPermission.LabInterior })
                Assert.IsFalse(AccessRules.Has(P(Profession.Civilian), need), need.ToString());
            Assert.IsTrue(AccessRules.Has(P(Profession.Civilian), AccessPermission.None));
        }

        [Test]
        public void StaffMayBeInsideLab()
        {
            foreach (var p in new[] { Profession.Guard, Profession.Scientist, Profession.Maintenance })
                Assert.IsTrue(AccessRules.Has(P(p), AccessPermission.LabInterior), p.ToString());
        }
    }
}
