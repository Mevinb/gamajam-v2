using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.Audio
{
    /// <summary>
    /// Listener-level effects: the pause-menu "snapshot" (a low-pass on the active AudioListener; UI clicks and voice
    /// bypass it) plus a lighter muffle while a containment reset is pending, and zone reverb — one AudioReverbZone that
    /// follows the listener and switches preset by acoustic zone (street, alley, lab, service tunnel).
    /// </summary>
    public sealed class ListenerFx : MonoBehaviour
    {
        AudioListener bound;
        AudioLowPassFilter lowPass;
        AudioReverbZone reverb;
        AudioReverbPreset preset = AudioReverbPreset.Off;
        float muffle;

        void Awake()
        {
            var go = new GameObject("Zone Reverb");
            go.transform.SetParent(transform, false);
            reverb = go.AddComponent<AudioReverbZone>();
            reverb.minDistance = 8f;
            reverb.maxDistance = 12f;
            reverb.reverbPreset = AudioReverbPreset.Off;
        }

        static AudioReverbPreset PresetFor(AudioZone z) => z switch
        {
            AudioZone.Tunnel => AudioReverbPreset.SewerPipe,
            AudioZone.Lab => AudioReverbPreset.Hallway,
            AudioZone.Alley => AudioReverbPreset.Alley,
            AudioZone.Plaza => AudioReverbPreset.City,
            _ => AudioReverbPreset.City,
        };

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var l = AudioBus.Listener;
            if (l != bound)
            {
                bound = l;
                lowPass = null;
                if (l && !l.TryGetComponent(out lowPass)) lowPass = l.gameObject.AddComponent<AudioLowPassFilter>();
                if (lowPass) { lowPass.cutoffFrequency = 22000f; lowPass.lowpassResonanceQ = 1.1f; lowPass.enabled = false; }
            }
            var gm = GameManager.Instance;
            float target = 0f;
            if (gm && gm.State == GameState.Paused) target = 1f;
            else if (gm && gm.State == GameState.Resetting) target = 0.55f;
            muffle = Mathf.MoveTowards(muffle, target, dt / 0.3f);
            AudioBus.Muffle = muffle;
            if (lowPass)
            {
                lowPass.enabled = muffle > 0.002f;
                lowPass.cutoffFrequency = 22000f * Mathf.Pow(700f / 22000f, muffle);
            }

            bool city = gm && SceneManager.GetActiveScene().name == GameManager.CityScene && !UI.LakesideEpilogue.Instance;
            var want = city ? PresetFor(AudioBus.ZoneAt(AudioBus.FocusPosition)) : AudioReverbPreset.Off;
            if (want != preset) { preset = want; reverb.reverbPreset = want; }
            reverb.transform.position = AudioBus.ListenerPosition;
        }
    }
}
