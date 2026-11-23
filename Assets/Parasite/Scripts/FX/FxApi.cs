using UnityEngine;

namespace Parasite.FX
{
    public enum SfxId
    {
        PossessLaunch, PossessHit, Release, Split, Switch, DoorOpen, Denied, TerminalBeep,
        AlertCurious, AlertPursuit, Capture, Eject, BlobContained, Footstep, Jump, Land,
        UiClick, EndingSting, Heartbeat, BlobHop,
        AlarmSiren, RainAmbience, BioSquelch, LaserHum,
        // appended (audio pass) — never reorder; values are positional
        UiHover, UiOpen, UiClose, UiBack, ObjectiveUpdate, Checkpoint, MissionComplete, DoorClose,
        BlobLand, BlobSquish, CarHorn, RadioSquelch, Drip, LabBeep, ThunderDistant, DogBark, SirenDistant, Whoosh
    }

    /// <summary>
    /// CONTRACT — sound effects. Generated/processed clips in Resources/Audio/Sfx (variants name_1..name_N) are preferred,
    /// then the original Kenney CC0 clips in Resources/Audio, then the procedural synth. Bus volumes below are set by
    /// <see cref="Parasite.AudioPreferences"/>; the music/voice/ambience systems live in Scripts/Audio.
    /// </summary>
    public static partial class Sfx
    {
        public static void Play(SfxId id, Vector3 position, float volume = 1f) => SfxBackend.Play(id, position, volume, false);
        public static void Ui(SfxId id, float volume = 1f) => SfxBackend.Play(id, Vector3.zero, volume, true);
        /// <summary>Start the city ambience (beds, positional emitters, footsteps, NPC vocals) for the current scene.</summary>
        public static void StartAmbience() => SfxBackend.StartAmbience();
        /// <summary>Effects bus volume (0..1): one-shots, footsteps, heartbeat, engines.</summary>
        public static float Volume = 1f;
        /// <summary>Ambience bus volume (0..1): city/lab/tunnel beds and positional world loops.</summary>
        public static float AmbienceVolume = 1f;
        /// <summary>Music bus volume (0..1).</summary>
        public static float MusicVolume = 1f;
        /// <summary>Dialogue/voice-over bus volume (0..1), also NPC barks.</summary>
        public static float VoiceVolume = 1f;
    }

    /// <summary>CONTRACT — visual effects.</summary>
    public static partial class Fx
    {
        /// <summary>Flying amoeba used during a possession launch. Caller moves it, then calls DestroyProjectile.</summary>
        public static Transform CreateProjectile() => FxBackend.CreateProjectile();
        public static void DestroyProjectile(Transform t) => FxBackend.DestroyProjectile(t);
        /// <summary>Green splash where the amoeba enters/leaves a body.</summary>
        public static void Splat(Vector3 position) => FxBackend.Splat(position);
        /// <summary>Burst used when splitting into two.</summary>
        public static void SplitBurst(Vector3 position) => FxBackend.SplitBurst(position);
        /// <summary>Full-screen colour pulse (green on possession, red on capture).</summary>
        public static void ScreenPulse(Color color, float strength = 1f, float duration = 0.6f) => FxBackend.ScreenPulse(color, strength, duration);
        /// <summary>Called every frame by the suspicion system; drives danger vignette (0..1).</summary>
        public static void SetDanger(float t01) => FxBackend.SetDanger(t01);
    }
}
