using NUnit.Framework;
using Parasite.FX;
using UnityEngine;

namespace Parasite.Tests
{
    public class AudioPreferencesTests
    {
        const string MasterKey = "parasite.audio.master", AmbienceKey = "parasite.audio.ambience";
        bool hadMaster, hadAmbience;
        float oldMaster, oldAmbience, oldListener, oldEffectAmbience;

        [SetUp]
        public void SavePlayerPreferences()
        {
            hadMaster = PlayerPrefs.HasKey(MasterKey); hadAmbience = PlayerPrefs.HasKey(AmbienceKey);
            oldMaster = PlayerPrefs.GetFloat(MasterKey); oldAmbience = PlayerPrefs.GetFloat(AmbienceKey);
            oldListener = AudioListener.volume; oldEffectAmbience = Sfx.AmbienceVolume;
        }

        [TearDown]
        public void RestorePlayerPreferences()
        {
            if (hadMaster) PlayerPrefs.SetFloat(MasterKey, oldMaster); else PlayerPrefs.DeleteKey(MasterKey);
            if (hadAmbience) PlayerPrefs.SetFloat(AmbienceKey, oldAmbience); else PlayerPrefs.DeleteKey(AmbienceKey);
            AudioPreferences.Load();
            AudioListener.volume = oldListener; Sfx.AmbienceVolume = oldEffectAmbience;
            PlayerPrefs.Save();
        }

        [TestCase(0f, 0.65f)]
        [TestCase(0.35f, 0f)]
        public void ReloadRestoresVolumeIncludingMute(float master, float ambience)
        {
            AudioPreferences.SetMaster(master);
            AudioPreferences.SetAmbience(ambience);
            PlayerPrefs.Save();
            // The active scene's audio can be replaced before the saved preferences are reloaded.
            AudioListener.volume = 0.9f; Sfx.AmbienceVolume = 0.9f;
            AudioPreferences.Load();
            Assert.That(AudioListener.volume, Is.EqualTo(master).Within(0.0001f));
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(ambience).Within(0.0001f));
        }

        [Test]
        public void MasterAffectsListenerWithoutChangingEffectsOrAmbienceBalance()
        {
            float effectVolume = Sfx.Volume;
            AudioPreferences.SetAmbience(0.4f);
            AudioPreferences.SetMaster(0f);
            Assert.That(AudioListener.volume, Is.Zero);
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(Sfx.Volume, Is.EqualTo(effectVolume));
        }

        [Test]
        public void InvalidSavedValuesCannotPoisonAudio()
        {
            PlayerPrefs.SetFloat(MasterKey, float.NaN);
            PlayerPrefs.SetFloat(AmbienceKey, -10f);
            AudioPreferences.Load();
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
            Assert.That(Sfx.AmbienceVolume, Is.Zero);
        }
    }
}
