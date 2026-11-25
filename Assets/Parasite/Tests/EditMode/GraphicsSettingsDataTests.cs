using NUnit.Framework;
using UnityEngine;

namespace Parasite.Tests
{
    public class GraphicsSettingsDataTests
    {
        bool hadCurrent, hadLegacy, hadAudio;
        string oldCurrent;
        int oldLegacy;
        float oldAudio;
        const string AudioKey = "parasite.audio.master";

        [SetUp]
        public void SavePreferences()
        {
            hadCurrent = PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey); oldCurrent = PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey);
            hadLegacy = PlayerPrefs.HasKey(GraphicsSettingsStore.LegacyPresetKey); oldLegacy = PlayerPrefs.GetInt(GraphicsSettingsStore.LegacyPresetKey);
            hadAudio = PlayerPrefs.HasKey(AudioKey); oldAudio = PlayerPrefs.GetFloat(AudioKey);
        }

        [TearDown]
        public void RestorePreferences()
        {
            if (hadCurrent) PlayerPrefs.SetString(GraphicsSettingsStore.PreferenceKey, oldCurrent); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.PreferenceKey);
            if (hadLegacy) PlayerPrefs.SetInt(GraphicsSettingsStore.LegacyPresetKey, oldLegacy); else PlayerPrefs.DeleteKey(GraphicsSettingsStore.LegacyPresetKey);
            if (hadAudio) PlayerPrefs.SetFloat(AudioKey, oldAudio); else PlayerPrefs.DeleteKey(AudioKey);
            PlayerPrefs.Save();
        }

        [TestCase(0, .7f, 40f, false, 1)]
        [TestCase(1, .85f, 70f, false, 1)]
        [TestCase(2, 1f, 110f, true, 1)]
        [TestCase(3, 1.25f, 180f, true, 4)]
        public void MigratingLegacyPresetPreservesItsExistingImageBudget(int preset, float scale, float shadowDistance, bool ao, int msaa)
        {
            PlayerPrefs.DeleteKey(GraphicsSettingsStore.PreferenceKey);
            PlayerPrefs.SetInt(GraphicsSettingsStore.LegacyPresetKey, preset);
            PlayerPrefs.SetFloat(AudioKey, .37f);
            var migrated = GraphicsSettingsStore.Load(2);
            Assert.AreEqual(preset, migrated.preset); Assert.AreEqual(scale, migrated.renderScale);
            Assert.AreEqual(shadowDistance, migrated.shadowDistance); Assert.AreEqual(ao, migrated.ambientOcclusion);
            Assert.AreEqual(preset, migrated.reflectionLevel); Assert.AreEqual(msaa, migrated.msaaSamples);
            Assert.AreEqual(preset >= 2 ? 2 : 1, migrated.antiAliasing);
            Assert.IsTrue(migrated.vSync); Assert.AreEqual(-1, migrated.frameLimit);
            Assert.IsFalse(PlayerPrefs.HasKey(GraphicsSettingsStore.PreferenceKey), "Reading a legacy preference is non-destructive.");
            Assert.AreEqual(.37f, PlayerPrefs.GetFloat(AudioKey));
        }

        [Test]
        public void IndependentChoicesPersistWithoutMutatingCallerOrOtherPreferences()
        {
            var expected = GraphicsSettingsData.ForPreset(0);
            expected.renderScale = 1.1f; expected.reflectionLevel = 3; expected.ambientOcclusion = true;
            expected.vSync = false; expected.frameLimit = 60; expected.shadows = false;
            expected.bloom = expected.filmGrain = expected.chromaticAberration = false;
            expected.antiAliasing = 0; expected.msaaSamples = 2;
            PlayerPrefs.SetFloat(AudioKey, .41f);
            var caller = expected.Clone(); GraphicsSettingsStore.Save(caller);
            caller.renderScale = .5f; caller.reflectionLevel = 0;
            Assert.IsTrue(expected.Equals(GraphicsSettingsStore.Load(3)));
            Assert.AreEqual(0, PlayerPrefs.GetInt(GraphicsSettingsStore.LegacyPresetKey));
            Assert.AreEqual(.41f, PlayerPrefs.GetFloat(AudioKey));
            Assert.IsTrue(GraphicsSettingsStore.TryDeserialize(PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey), out var decoded));
            Assert.IsTrue(expected.Equals(decoded));
        }

        [TestCase("broken")]
        [TestCase("{}")]
        [TestCase("{\"version\":1,\"settings\":{\"version\":1}}")]
        [TestCase("{\"version\":1,\"settings\":{\"version\":1,\"preset\":0},\"checksum\":\"incomplete\"}")]
        [TestCase("{\"version\":2,\"settings\":{\"version\":2}}")]
        public void MalformedPartialOrFuturePayloadFallsBackWithoutRewritingData(string payload)
        {
            PlayerPrefs.SetString(GraphicsSettingsStore.PreferenceKey, payload);
            PlayerPrefs.SetInt(GraphicsSettingsStore.LegacyPresetKey, 1);
            PlayerPrefs.SetFloat(AudioKey, .19f);
            Assert.IsTrue(GraphicsSettingsData.ForPreset(1).Equals(GraphicsSettingsStore.Load(3)));
            Assert.AreEqual(payload, PlayerPrefs.GetString(GraphicsSettingsStore.PreferenceKey));
            Assert.AreEqual(.19f, PlayerPrefs.GetFloat(AudioKey));
        }

        [Test]
        public void EditedPayloadWithStaleChecksumIsNotSilentlyApplied()
        {
            string json = GraphicsSettingsStore.Serialize(GraphicsSettingsData.ForPreset(3));
            string changed = json.Replace("\"msaaSamples\":4", "\"msaaSamples\":2");
            Assert.AreNotEqual(json, changed);
            Assert.IsFalse(GraphicsSettingsStore.TryDeserialize(changed, out _));
        }

        [Test]
        public void InvalidNumbersAreNormalizedBeforeTheyReachRendererOrPersistence()
        {
            var data = GraphicsSettingsData.ForPreset(2);
            data.renderScale = float.NaN; data.shadowDistance = float.PositiveInfinity;
            data.frameLimit = 13; data.reflectionLevel = int.MaxValue; data.antiAliasing = -1; data.msaaSamples = 16;
            var safe = data.Normalized();
            Assert.AreEqual(1f, safe.renderScale); Assert.AreEqual(110f, safe.shadowDistance);
            Assert.AreEqual(-1, safe.frameLimit); Assert.AreEqual(3, safe.reflectionLevel);
            Assert.AreEqual(2, safe.antiAliasing); Assert.AreEqual(1, safe.msaaSamples);
            Assert.IsTrue(float.IsNaN(data.renderScale), "Normalization returns a copy.");
            Assert.IsTrue(GraphicsSettingsStore.TryDeserialize(GraphicsSettingsStore.Serialize(data), out var roundTrip));
            Assert.IsTrue(safe.Equals(roundTrip));
        }

        [Test]
        public void CloneCanBeEditedAsDraftWithoutChangingItsAppliedSnapshot()
        {
            var applied = GraphicsSettingsData.ForPreset(3);
            var draft = applied.Clone(); draft.renderScale = .8f; draft.vSync = false;
            Assert.AreEqual(1.25f, applied.renderScale); Assert.IsTrue(applied.vSync);
            Assert.IsFalse(applied.Equals(draft));
        }
    }
}
