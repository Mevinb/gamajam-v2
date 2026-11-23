using UnityEngine;

namespace Parasite.FX
{
    public enum SfxId
    {
        PossessLaunch, PossessHit, Release, Split, Switch, DoorOpen, Denied, TerminalBeep,
        AlertCurious, AlertPursuit, Capture, Eject, BlobContained, Footstep, Jump, Land,
        UiClick, EndingSting, Heartbeat, BlobHop,
        AlarmSiren, RainAmbience, BioSquelch, LaserHum
    }

    /// <summary>CONTRACT — sound effects (Kenney CC0 clips where available, procedural synth otherwise).</summary>
    public static partial class Sfx
    {
        public static void Play(SfxId id, Vector3 position, float volume = 1f) => SfxBackend.Play(id, position, volume, false);
        public static void Ui(SfxId id, float volume = 1f) => SfxBackend.Play(id, Vector3.zero, volume, true);
        /// <summary>Start looping city ambience / music bed for the current scene.</summary>
        public static void StartAmbience() => SfxBackend.StartAmbience();
        /// <summary>Global SFX volume (0..1), applied to one-shots, heartbeat and ambience.</summary>
        public static float Volume = 1f;
        /// <summary>Extra ambience/music-bed volume multiplier (0..1).</summary>
        public static float AmbienceVolume = 1f;
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
