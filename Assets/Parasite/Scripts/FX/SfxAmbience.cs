using System.Collections;
using System.Collections.Generic;
using Parasite.Audio;
using Parasite.Missions;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;

namespace Parasite.FX
{
    /// <summary>
    /// Per-scene world sound (created by Sfx.StartAmbience from CityDirector):
    /// 2D beds crossfaded by acoustic zone (dusk city + rain outdoors, lab HVAC, service-tunnel drips/rumble; rain is
    /// low-passed "on the roof" indoors), distant one-shot events around the listener (sirens, dogs, horns, dusk birds,
    /// thunder; lab machine beeps; tunnel drips), positional loops (Plaza fountain, crowd murmur at the Plaza and café,
    /// alley AC units, steam vents, storm-drain trickle), engine/tyre/horn audio on traffic cars and servo whirr on CCTV.
    /// Also hosts the Footsteps and NpcVocals systems. Falls back to the procedural city bed if assets are missing.
    /// </summary>
    internal class SfxAmbience : MonoBehaviour
    {
        public static SfxAmbience Instance { get; private set; }
        const string Folder = "Audio/Ambience/";
        AudioSource city, rain, lab, tunnel;
        AudioLowPassFilter rainLp;
        float fadeIn, wOut = 1f, wLab, wTunnel, eventT = 6f, thunderT = 30f, labT = 3f, dripT = 1f, scanT;
        bool built;
        readonly List<(AccessDoor door, bool open)> doors = new();

        // AC units placed by CityBuilder (south alley + east service alley dressing)
        static readonly Vector3[] AirCons =
        {
            new(-30.05f, 2.7f, -80f), new(-30.05f, 2.9f, -52f), new(-35.95f, 2.6f, -47f), new(-30.05f, 7.6f, -70f),
            new(-35.95f, 8.1f, -58f), new(-35.95f, 2.5f, -81f), new(61.95f, 2.8f, 66f), new(61.95f, 2.6f, 97f), new(61.95f, 7.4f, 74f),
        };

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        static AudioClip Clip(string n) => Resources.Load<AudioClip>(Folder + n);

        AudioSource Bed(string n, AudioClip clip)
        {
            if (!clip) return null;
            var a = AudioBus.Make2D(transform, n, 40);
            a.clip = clip; a.loop = true;
            a.timeSamples = Random.Range(0, Mathf.Max(1, clip.samples - 1));
            a.Play();
            return a;
        }

        IEnumerator Start()
        {
            yield return null;
            var cityClip = Clip("amb_city");
            city = Bed("City", cityClip ? cityClip : SfxSynth.CityBed());
            yield return null;
            rain = Bed("Rain", Clip("amb_rain"));
            if (rain) { rainLp = rain.gameObject.AddComponent<AudioLowPassFilter>(); rainLp.cutoffFrequency = 22000f; }
            lab = Bed("Lab", Clip("amb_lab"));
            tunnel = Bed("Tunnel", Clip("amb_tunnel"));
            yield return null;
            SpawnEmitters();
            gameObject.AddComponent<Footsteps>();
            gameObject.AddComponent<NpcVocals>();
            built = true;
            yield return SfxBackend.Warmup();
        }

        void SpawnEmitters()
        {
            var root = new GameObject("Emitters").transform;
            root.SetParent(transform, false);
            AudioLoopEmitter.Create(root, "Fountain", CityLayout.Fountain + Vector3.up * 0.8f, Clip("amb_fountain"), 0.85f, 2.5f, 30f);
            AudioLoopEmitter.Create(root, "Crowd Plaza", CityLayout.PlazaSpot + new Vector3(-3f, 1.5f, 4f), Clip("amb_crowd"), 0.55f, 4f, 30f);
            var cafe = AudioLoopEmitter.Create(root, "Crowd Cafe", CityLayout.CafeSpot + Vector3.up * 1.5f, Clip("amb_crowd"), 0.6f, 3f, 24f);
            if (cafe) cafe.Pitch = 0.96f;
            AudioLoopEmitter.Create(root, "Storm Drain", CityLayout.StormDrain + Vector3.up * 0.1f, Clip("amb_drain"), 0.7f, 1.2f, 14f);
            var ac = Clip("amb_aircon");
            for (int i = 0; i < AirCons.Length; i++)
            {
                var e = AudioLoopEmitter.Create(root, "AC " + i, AirCons[i], ac, 0.45f, 1.5f, 14f);
                if (e) e.Pitch = 0.93f + 0.03f * (i % 5);
            }
            foreach (var d in FindObjectsByType<AccessDoor>(FindObjectsSortMode.None)) doors.Add((d, d.IsOpen));
            var steam = Clip("amb_steam");
            foreach (var v in FindObjectsByType<SteamVent>(FindObjectsSortMode.None))
                AudioLoopEmitter.Create(root, "Steam", v.transform.position + Vector3.up * 0.2f, steam, 0.3f, 1f, 10f);
        }

        void ScanDynamic()
        {
            var ts = TrafficSystem.Instance;
            if (ts)
                foreach (var lane in ts.Lanes)
                    foreach (var car in lane.cars)
                        if (car && !car.GetComponent<CarEngineAudio>()) car.gameObject.AddComponent<CarEngineAudio>();
            foreach (var cam in SecurityCamera.All)
                if (cam && !cam.GetComponent<CctvServoAudio>()) cam.gameObject.AddComponent<CctvServoAudio>();
        }

        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            fadeIn = Mathf.MoveTowards(fadeIn, 1f, udt / 3f);
            var zone = AudioBus.ZoneAt(AudioBus.FocusPosition);
            float k = 1f - Mathf.Exp(-udt / 0.6f);
            wOut = Mathf.Lerp(wOut, AudioBus.Indoors(zone) ? 0f : 1f, k);
            wLab = Mathf.Lerp(wLab, zone == AudioZone.Lab ? 1f : 0f, k);
            wTunnel = Mathf.Lerp(wTunnel, zone == AudioZone.Tunnel ? 1f : 0f, k);
            float g = AudioBus.AmbienceGain * fadeIn;
            if (city) city.volume = g * 0.8f * Mathf.Lerp(0.12f, 1f, wOut);
            if (rain)
            {
                rain.volume = g * Mathf.Lerp(wTunnel > 0.5f ? 0.08f : 0.35f, 0.75f, wOut);
                if (rainLp) rainLp.cutoffFrequency = Mathf.Lerp(700f, 22000f, wOut * wOut);
            }
            if (lab) lab.volume = g * 0.9f * wLab;
            if (tunnel) tunnel.volume = g * 0.9f * wTunnel;

            if (!built) return;
            for (int i = 0; i < doors.Count; i++)
            {
                var (door, wasOpen) = doors[i];
                if (!door || door.IsOpen == wasOpen) continue;
                if (wasOpen) Sfx.Play(SfxId.DoorClose, door.InteractPoint);   // AccessDoor.Open() already plays DoorOpen
                doors[i] = (door, door.IsOpen);
            }
            scanT -= udt;
            if (scanT <= 0f) { scanT = 2f; ScanDynamic(); }
            var gm = GameManager.Instance;
            if (!gm || !gm.IsPlaying || Time.deltaTime <= 0f) return;
            float dt = Time.deltaTime;
            Vector3 L = AudioBus.ListenerPosition;
            if (wOut > 0.5f)
            {
                eventT -= dt;
                if (eventT <= 0f) { eventT = Random.Range(6f, 15f); DistantEvent(L); }
                thunderT -= dt;
                if (thunderT <= 0f) { thunderT = Random.Range(45f, 110f); Sfx.Ui(SfxId.ThunderDistant, Random.Range(0.35f, 0.7f) * AudioBus.AmbienceGain); }
            }
            if (wLab > 0.5f)
            {
                labT -= dt;
                if (labT <= 0f) { labT = Random.Range(2.5f, 7f); Sfx.Play(SfxId.LabBeep, L + Around(3f, 9f, 0.5f, 2f), AudioBus.AmbienceGain); }
            }
            if (wTunnel > 0.5f)
            {
                dripT -= dt;
                if (dripT <= 0f)
                {
                    dripT = Random.Range(0.5f, 2.2f);
                    var p = L + Around(1.5f, 8f, -1.6f, 1.2f);
                    p.z = Mathf.Clamp(p.z, 88.8f, 91.2f);
                    Sfx.Play(SfxId.Drip, p, Random.Range(0.4f, 1f) * AudioBus.AmbienceGain);
                }
            }
        }

        static Vector3 Around(float rMin, float rMax, float yMin, float yMax)
        {
            float a = Random.value * Mathf.PI * 2f, r = Random.Range(rMin, rMax);
            return new Vector3(Mathf.Cos(a) * r, Random.Range(yMin, yMax), Mathf.Sin(a) * r);
        }

        static void DistantEvent(Vector3 L)
        {
            float roll = Random.value, g = AudioBus.AmbienceGain;
            if (roll < 0.38f)
            {
                var birds = SfxBackend.Variants("Sfx/bird");
                if (birds != null)
                    SfxBackend.PlayClip(SfxBackend.Pick(birds, "bird"), L + Around(14f, 30f, 5f, 12f), Random.Range(0.35f, 0.7f) * g,
                        Random.Range(0.95f, 1.05f), SfxPool.TagAmbientEvent, 200, 6f, 45f, 2, false, 0.4f);
            }
            else if (roll < 0.6f) Sfx.Play(SfxId.CarHorn, L + Around(40f, 70f, 0f, 2f), Random.Range(0.25f, 0.5f) * g);
            else if (roll < 0.82f) Sfx.Play(SfxId.DogBark, L + Around(35f, 70f, 0f, 3f), Random.Range(0.4f, 0.8f) * g);
            else Sfx.Play(SfxId.SirenDistant, L + Around(80f, 140f, 0f, 5f), Random.Range(0.5f, 0.9f) * g);
        }
    }
}
