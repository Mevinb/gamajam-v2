using NUnit.Framework;
using Parasite.Audio;
using Parasite.FX;
using UnityEngine;

namespace Parasite.Tests
{
    public class AudioPreferencesTests
    {
        const string MasterKey = "parasite.audio.master", AmbienceKey = "parasite.audio.ambience";
        static readonly string[] Keys = { AudioPreferences.MasterKey, AudioPreferences.AmbienceKey, AudioPreferences.MusicKey,
                                          AudioPreferences.EffectsKey, AudioPreferences.VoiceKey };
        readonly bool[] had = new bool[5];
        readonly float[] old = new float[5];
        float oldListener, oldEffectAmbience, oldMusic, oldEffects, oldVoice;

        [SetUp]
        public void SavePlayerPreferences()
        {
            for (int i = 0; i < Keys.Length; i++) { had[i] = PlayerPrefs.HasKey(Keys[i]); old[i] = PlayerPrefs.GetFloat(Keys[i]); }
            oldListener = AudioListener.volume; oldEffectAmbience = Sfx.AmbienceVolume;
            oldMusic = Sfx.MusicVolume; oldEffects = Sfx.Volume; oldVoice = Sfx.VoiceVolume;
        }

        [TearDown]
        public void RestorePlayerPreferences()
        {
            for (int i = 0; i < Keys.Length; i++) { if (had[i]) PlayerPrefs.SetFloat(Keys[i], old[i]); else PlayerPrefs.DeleteKey(Keys[i]); }
            AudioPreferences.Load();
            AudioListener.volume = oldListener; Sfx.AmbienceVolume = oldEffectAmbience;
            Sfx.MusicVolume = oldMusic; Sfx.Volume = oldEffects; Sfx.VoiceVolume = oldVoice;
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
            PlayerPrefs.SetFloat(AudioPreferences.MusicKey, float.PositiveInfinity);
            PlayerPrefs.SetFloat(AudioPreferences.EffectsKey, 7f);
            PlayerPrefs.SetFloat(AudioPreferences.VoiceKey, float.NegativeInfinity);
            AudioPreferences.Load();
            Assert.That(AudioListener.volume, Is.EqualTo(1f));
            Assert.That(Sfx.AmbienceVolume, Is.Zero);
            Assert.That(Sfx.MusicVolume, Is.EqualTo(1f));
            Assert.That(Sfx.Volume, Is.EqualTo(1f));
            Assert.That(Sfx.VoiceVolume, Is.EqualTo(1f));
        }

        [TestCase(0f, 0.5f, 1f)]
        [TestCase(0.8f, 0f, 0.25f)]
        public void MusicEffectsAndVoiceBusesPersistIndependently(float music, float effects, float voice)
        {
            AudioPreferences.SetAmbience(0.33f);
            AudioPreferences.SetMusic(music);
            AudioPreferences.SetEffects(effects);
            AudioPreferences.SetVoice(voice);
            PlayerPrefs.Save();
            Sfx.MusicVolume = Sfx.Volume = Sfx.VoiceVolume = 0.9f;
            AudioPreferences.Load();
            Assert.That(Sfx.MusicVolume, Is.EqualTo(music).Within(0.0001f));
            Assert.That(Sfx.Volume, Is.EqualTo(effects).Within(0.0001f));
            Assert.That(Sfx.VoiceVolume, Is.EqualTo(voice).Within(0.0001f));
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.33f).Within(0.0001f), "Other buses leave ambience alone.");
            Assert.That(AudioPreferences.MusicVolume, Is.EqualTo(music).Within(0.0001f));
        }

        [Test]
        public void MusicSliderDoesNotChangeAmbienceOrEffects()
        {
            AudioPreferences.SetAmbience(0.6f);
            AudioPreferences.SetEffects(0.7f);
            AudioPreferences.SetMusic(0.1f);
            Assert.That(Sfx.AmbienceVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(Sfx.Volume, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(Sfx.MusicVolume, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void VoiceKeysMatchTheOfflineGenerator()
        {
            // Values produced by Tools/audio/gen_voice.py (FNV-1a 64 of "SPEAKER|line").
            Assert.AreEqual("vo_12d6ccc867d54c64", VoiceOver.KeyFor("DR. VALE", "There you are. Specimen 7. You took longer than I predicted."));
            Assert.AreEqual("vo_cbd913758555a8e2", VoiceOver.KeyFor("DR. PETROV", "Hello? …Is somebody following me?"));
            // Case/whitespace/rich-text differences and an empty speaker (narrator) resolve to the same clip.
            Assert.AreEqual(VoiceOver.KeyFor("DR. VALE", "There you are. Specimen 7. You took longer than I predicted."),
                VoiceOver.KeyFor("  dr. vale ", "There you are.\nSpecimen 7.  You took <b>longer</b> than I predicted. "));
            Assert.AreEqual("vo_82c91c18362d33e7", VoiceOver.KeyFor("", "What will you become?"));
            Assert.AreEqual(0f, VoiceOver.Duration("NOBODY", "A line that was never generated."));
        }

        [Test]
        public void SfxIdsAreAppendOnly()
        {
            // Values are positional; new ids must be appended so serialized/indexed uses keep their meaning.
            Assert.AreEqual(0, (int)SfxId.PossessLaunch);
            Assert.AreEqual(19, (int)SfxId.BlobHop);
            Assert.AreEqual(23, (int)SfxId.LaserHum);
            Assert.AreEqual(24, (int)SfxId.UiHover);
        }

        [Test]
        public void AcousticZonesMatchTheCityLayout()
        {
            Assert.AreEqual(AudioZone.Tunnel, AudioBus.ZoneAt(new Vector3(39f, 1f, 90f)));
            Assert.AreEqual(AudioZone.Lab, AudioBus.ZoneAt(new Vector3(0f, 1f, 86f)));
            Assert.AreEqual(AudioZone.Alley, AudioBus.ZoneAt(new Vector3(-33f, 1f, -80f)));
            Assert.AreEqual(AudioZone.Plaza, AudioBus.ZoneAt(new Vector3(-36f, 1f, 9f)));
            Assert.AreEqual(AudioZone.Street, AudioBus.ZoneAt(new Vector3(0f, 1f, -60f)));
            Assert.AreEqual("asphalt", Footsteps.SurfaceAt(new Vector3(0f, 0f, -60f), 0f));
            Assert.AreEqual("cobble", Footsteps.SurfaceAt(new Vector3(-30f, 0.15f, 9f), 0.15f));
            Assert.AreEqual("tile", Footsteps.SurfaceAt(new Vector3(0f, 0.17f, 86f), 0.17f));
        }
    }
}
