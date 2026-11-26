using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Parasite.Missions;
using UnityEngine;

namespace Parasite.Tests
{
    /// <summary>Real file replacements in an owned temporary directory; never the player's save slot.</summary>
    public sealed class CampaignSaveStoreTests
    {
        string directory, ownership, originalRunState;
        string Primary => Path.Combine(directory, CampaignSaveStore.FileName);
        string Backup => Primary + ".bak";

        [Serializable]
        sealed class IndependentEnvelope
        {
            public string format = "PARASITE_CHECKPOINT";
            public int version = CampaignSaveData.CurrentSchema;
            public CampaignSaveData checkpoint;
            public string checksum;
        }

        [SetUp]
        public void OwnTemporaryDirectory()
        {
            ownership = Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetTempPath(), "parasite-campaign-save-tests-" + ownership);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ".test-owner"), ownership);
            originalRunState = RunSignature();
        }

        [TearDown]
        public void LeaveGlobalRunAndPlayerFilesUntouched()
        {
            try { Assert.That(RunSignature(), Is.EqualTo(originalRunState), "Store/codec operations and rejected restores must not alter the live campaign."); }
            finally
            {
                string marker = Path.Combine(directory, ".test-owner");
                if (File.Exists(marker) && File.ReadAllText(marker) == ownership)
                    Directory.Delete(directory, true);
            }
        }

        static string RunSignature() => JsonUtility.ToJson(CampaignSaveData.FromRunState());

        static CampaignSaveData City(float seconds = 12.5f) => new()
        {
            hasCheckpoint = true, checkpointName = "Storm Drain", checkpointPosition = World.CityLayout.PlayerSpawn,
            route = EntryRoute.None, lakesideOutcome = EndingOutcome.Unchosen, playTime = seconds,
            hostsPossessed = 2, peakSuspicion = 15.25f, captures = 1, resets = 3,
            missionIndex = 0, flags = Array.Empty<string>(),
        };

        static CampaignSaveData Rail(EndingOutcome outcome)
        {
            var data = City(2125.75f);
            data.checkpointName = "Northline southern approach";
            data.checkpointPosition = RailLayout.Spawn;
            data.lakesideOutcome = outcome; data.labEntered = true; data.missionIndex = 23;
            var flags = new HashSet<string>(StringComparer.Ordinal)
            {
                MissionFlags.Escaped, MissionFlags.LabEntered,
                AfterlakeCampaign.Started, AfterlakeCampaign.Signal, AfterlakeCampaign.Floodgate,
                AfterlakeCampaign.WaterAnalysed, AfterlakeCampaign.CorridorOpen,
                AfterlakeCampaign.ChannelOpened, AfterlakeCampaign.Departed,
                IslandCampaign.Started, IslandCampaign.ArrivalSeen, IslandCampaign.ServiceOpen, IslandCampaign.InnerRefuge,
                IslandRoster.Ready, IslandRoster.SignalFound, IslandRoster.SignalMounted, IslandRoster.Departed,
                HarborCampaign.Started, HarborCampaign.ArrivalSeen, HarborCampaign.CanalOpen,
                HarborCampaign.ManifestComplete, HarborCampaign.RelayComplete,
                RailCampaign.Started, RailCampaign.ArrivalSeen,
            };
            foreach (string id in IslandRoster.Ids) { flags.Add(IslandRoster.Delivered(id)); flags.Add(IslandRoster.Boarded(id)); }
            for (int i = 0; i < 3; i++) flags.Add(HarborCampaign.ValveFlag(i));
            foreach (string id in HarborCampaign.RequiredCargoIdsFor(outcome))
            { flags.Add(HarborCampaign.ReroutedFlag(id)); flags.Add(HarborCampaign.DeliveredFlag(id)); }
            data.flags = flags.OrderBy(f => f, StringComparer.Ordinal).ToArray();
            return data;
        }

        static void ChangeFlags(CampaignSaveData data, IEnumerable<string> add = null, IEnumerable<string> remove = null)
        {
            var flags = new HashSet<string>(data.flags, StringComparer.Ordinal);
            if (add != null) flags.UnionWith(add);
            if (remove != null) flags.ExceptWith(remove);
            data.flags = flags.OrderBy(f => f, StringComparer.Ordinal).ToArray();
        }

        static void CompleteRail(CampaignSaveData data)
        {
            ChangeFlags(data, new[] { RailCampaign.CrossingPassed, RailCampaign.Validated, RailCampaign.FreightArrived,
                RailCampaign.SignalReleased, RailCampaign.Departed });
            ChangeFlags(data, data.lakesideOutcome == EndingOutcome.Peace ? new[] { RailCampaign.PointB } : new[] { RailCampaign.PointA });
            data.missionIndex = 26; data.summary = true;
        }

        // Independent checksum fixture permits invalid-but-intact payloads. A failed schema/flag test is not merely a bad digest.
        static string Envelope(CampaignSaveData data, int version = CampaignSaveData.CurrentSchema)
        {
            using var hash = SHA256.Create();
            string checksum = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)))).Replace("-", "").ToLowerInvariant();
            return JsonUtility.ToJson(new IndependentEnvelope { version = version, checkpoint = data, checksum = checksum });
        }

        static CampaignSaveData Decode(string json)
        {
            Assert.That(CampaignSaveStore.TryDeserialize(json, out var data), Is.True);
            return data;
        }

        void Save(CampaignSaveData data)
        {
            Assert.That(data.IsValid(out string reason), Is.True, reason);
            Assert.That(CampaignSaveStore.TrySave(directory, data, out string error), Is.True, error);
        }

        [TestCase(EndingOutcome.Peace)]
        [TestCase(EndingOutcome.Dominion)]
        public void BothBranchesAndEarnedSummaryRoundTripWithoutOwningCallerFlags(EndingOutcome outcome)
        {
            var source = Rail(outcome); CompleteRail(source);
            string original = JsonUtility.ToJson(source); var flags = source.flags;
            string json = CampaignSaveStore.Serialize(source);
            var restored = Decode(json);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            Assert.That(source.flags, Is.SameAs(flags)); Assert.That(restored.flags, Is.Not.SameAs(flags));
            Assert.That(restored.route, Is.EqualTo(EntryRoute.None), "The supported presentation route may resolve the lake without an infiltration-route selection.");
            restored.flags[0] = "caller-only edit";
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(Decode(json)), Is.EqualTo(original));
        }

        [Test]
        public void SuccessiveAtomicReplacementsKeepThePreviousValidCheckpoint()
        {
            Save(City(10)); Assert.That(File.Exists(Backup), Is.False);
            Save(City(20)); Assert.That(Decode(File.ReadAllText(Backup)).playTime, Is.EqualTo(10));
            Save(City(30));
            Assert.That(CampaignSaveStore.TryLoad(directory, out var loaded, out bool recovered, out string error), Is.True, error);
            Assert.That(loaded.playTime, Is.EqualTo(30)); Assert.That(recovered, Is.False);
            Assert.That(Decode(File.ReadAllText(Backup)).playTime, Is.EqualTo(20));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void ReadingCorruptPrimaryRecoversBackupWithoutRewritingEitherFile()
        {
            Save(City(10)); Save(City(20));
            File.WriteAllText(Primary, "{ interrupted checkpoint");
            byte[] primary = File.ReadAllBytes(Primary), backup = File.ReadAllBytes(Backup);
            Assert.That(CampaignSaveStore.TryLoad(directory, out var data, out bool recovered, out string error), Is.True, error);
            Assert.That(recovered, Is.True); Assert.That(data.playTime, Is.EqualTo(10));
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Primary));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Backup));
        }

        [Test]
        public void SavingAfterBackupRecoveryNeverRotatesTheCorruptPrimaryOverGoodBackup()
        {
            Save(City(10)); Save(City(20));
            File.WriteAllText(Primary, "{ interrupted checkpoint");
            byte[] goodBackup = File.ReadAllBytes(Backup);
            Assert.That(CampaignSaveStore.TryLoad(directory, out _, out bool recovered, out _), Is.True);
            Assert.That(recovered, Is.True);
            Save(City(30));
            CollectionAssert.AreEqual(goodBackup, File.ReadAllBytes(Backup));
            Assert.That(CampaignSaveStore.TryLoad(directory, out var data, out recovered, out _), Is.True);
            Assert.That(data.playTime, Is.EqualTo(30)); Assert.That(recovered, Is.False);
        }

        [Test]
        public void FailedReplacementPreservesExistingCheckpointAndCleansOnlyOwnedTemporaryFile()
        {
            Save(City(10)); byte[] previous = File.ReadAllBytes(Primary);
            Directory.CreateDirectory(Backup); // Force failure at replacement, after the temporary payload has been written.
            Assert.That(CampaignSaveStore.TrySave(directory, City(20), out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(Primary));
            Assert.That(Decode(File.ReadAllText(Primary)).playTime, Is.EqualTo(10));
            Assert.That(Directory.Exists(Backup), Is.True);
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntactFuturePayloadIsRejectedWithoutChangingDiskOrGlobalRun(bool futureEnvelope)
        {
            var data = City();
            if (!futureEnvelope) data.schema = CampaignSaveData.CurrentSchema + 1;
            string payload = Envelope(data, futureEnvelope ? CampaignSaveData.CurrentSchema + 1 : CampaignSaveData.CurrentSchema);
            File.WriteAllText(Primary, payload); byte[] before = File.ReadAllBytes(Primary);
            Assert.That(CampaignSaveStore.TryDeserialize(payload, out var decoded), Is.False); Assert.That(decoded, Is.Null);
            Assert.That(CampaignSaveStore.TryLoad(directory, out decoded, out bool recovered, out string error), Is.False);
            Assert.That(decoded, Is.Null); Assert.That(recovered, Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Primary)); Assert.That(File.Exists(Backup), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FuturePrimaryCannotFallBackToOrBeOverwrittenByAnOlderValidBackup(bool futureEnvelope)
        {
            Save(City(10)); Save(City(20));
            var future = City(40);
            if (!futureEnvelope) future.schema = CampaignSaveData.CurrentSchema + 1;
            string payload = Envelope(future, futureEnvelope ? CampaignSaveData.CurrentSchema + 1 : CampaignSaveData.CurrentSchema);
            File.WriteAllText(Primary, payload);
            byte[] primary = File.ReadAllBytes(Primary), backup = File.ReadAllBytes(Backup);
            Assert.That(Decode(File.ReadAllText(Backup)).playTime, Is.EqualTo(10), "A usable older backup makes silent rollback possible; the future primary must still block it.");

            Assert.That(CampaignSaveStore.TryLoad(directory, out var data, out bool recovered, out string error), Is.False);
            Assert.That(data, Is.Null); Assert.That(recovered, Is.False); Assert.That(error, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Primary));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Backup));

            Assert.That(CampaignSaveStore.TrySave(directory, City(50), out error), Is.False, "An older runtime must not replace a newer-format primary with its older recovered state.");
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Primary));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Backup));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{ interrupted")]
        public void MalformedPayloadCannotBecomeACheckpoint(string payload)
        {
            Assert.That(CampaignSaveStore.TryDeserialize(payload, out var data), Is.False);
            Assert.That(data, Is.Null);
        }

        [Test]
        public void ChangedPayloadWithOriginalChecksumCannotBeLoaded()
        {
            string json = CampaignSaveStore.Serialize(City(12.5f));
            string tampered = json.Replace("12.5", "15.5");
            Assert.That(tampered, Is.Not.EqualTo(json));
            Assert.That(CampaignSaveStore.TryDeserialize(tampered, out var data), Is.False); Assert.That(data, Is.Null);
        }

        [TestCase("outcome-without-escape")]
        [TestCase("escape-without-choice")]
        [TestCase("island-without-ferry")]
        [TestCase("orphan-shore-departure")]
        [TestCase("terminal-index-without-summary")]
        [TestCase("claimed-summary-without-completion")]
        [TestCase("complete-flag-before-terminal-index")]
        [TestCase("northline-arrival-on-wrong-points")]
        public void IntactButImpossibleCampaignCannotReplaceOrRestoreAnEarnedCheckpoint(string defect)
        {
            Save(City(10)); byte[] previous = File.ReadAllBytes(Primary);
            var data = City();
            switch (defect)
            {
                case "outcome-without-escape": data.lakesideOutcome = EndingOutcome.Peace; break;
                case "escape-without-choice": data.flags = new[] { MissionFlags.Escaped }; break;
                case "island-without-ferry":
                    data.lakesideOutcome = EndingOutcome.Peace; data.missionIndex = 17;
                    data.flags = new[] { MissionFlags.Escaped, AfterlakeCampaign.Started, IslandCampaign.Started }; break;
                case "orphan-shore-departure": data.flags = new[] { AfterlakeCampaign.Departed }; break;
                case "terminal-index-without-summary":
                    data.missionIndex = 14; data.lakesideOutcome = EndingOutcome.Peace; data.flags = new[] { MissionFlags.Escaped }; break;
                case "claimed-summary-without-completion": data.summary = true; break;
                case "complete-flag-before-terminal-index": data = Rail(EndingOutcome.Peace); CompleteRail(data); data.summary = false; data.missionIndex = 25; break;
                case "northline-arrival-on-wrong-points":
                    data = Rail(EndingOutcome.Peace); data.missionIndex = 25;
                    ChangeFlags(data, new[] { RailCampaign.CrossingPassed, RailCampaign.Validated, RailCampaign.FreightArrived, RailCampaign.PointA }); break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
            Assert.That(data.IsValid(out _), Is.False, defect);
            Assert.That(CampaignSaveStore.TryDeserialize(Envelope(data), out var decoded), Is.False, defect);
            Assert.That(decoded, Is.Null);
            Assert.That(data.TryRestore(out _), Is.False, defect);
            Assert.That(CampaignSaveStore.TrySave(directory, data, out string error), Is.False, defect);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(Primary));
            Assert.That(File.Exists(Backup), Is.False);
        }

        [Test]
        public void NonFiniteLocationAndNegativeStatisticsCannotBeSerializedOrWritten()
        {
            var data = City(); data.checkpointPosition = new Vector3(float.NaN, 0, 0);
            Assert.That(data.IsValid(out _), Is.False);
            Assert.That(CampaignSaveStore.TrySave(directory, data, out _), Is.False);
            Assert.Throws<ArgumentException>(() => CampaignSaveStore.Serialize(data));
            data = City(); data.captures = -1;
            Assert.That(data.IsValid(out _), Is.False);
            Assert.That(CampaignSaveStore.TrySave(directory, data, out _), Is.False);
            Assert.That(File.Exists(Primary), Is.False);
        }
    }
}
