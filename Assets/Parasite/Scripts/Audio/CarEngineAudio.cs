using Parasite.Characters;
using Parasite.FX;
using Parasite.World;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>
    /// Added at runtime to every TrafficCar by SfxAmbience: engine loop pitched by speed, wet-tyre hiss (it always
    /// rains) and an impatient horn when the car has been stopped by a person or the amoeba in front of it.
    /// </summary>
    public sealed class CarEngineAudio : MonoBehaviour
    {
        TrafficCar car;
        AudioLoopEmitter engine, tyres;
        float basePitch, stoppedT, hornCd;

        void Start()
        {
            car = GetComponent<TrafficCar>();
            var rnd = new System.Random(GetInstanceID());
            basePitch = 0.88f + (float)rnd.NextDouble() * 0.24f;
            var eng = Resources.Load<AudioClip>("Audio/Ambience/amb_car_engine");
            var wet = Resources.Load<AudioClip>("Audio/Ambience/amb_car_tyres");
            engine = Child("Engine", eng, 0.7f, 3f, 38f);
            tyres = Child("Tyres", wet, 0.8f, 2.5f, 30f);
            hornCd = 4f + (float)rnd.NextDouble() * 6f;
        }

        AudioLoopEmitter Child(string n, AudioClip clip, float vol, float min, float max)
        {
            if (!clip) return null;
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.6f, 0.8f);
            var e = go.AddComponent<AudioLoopEmitter>();
            e.Setup(clip, vol, min, max, AudioLoopEmitter.Bus.Effects, 150);
            return e;
        }

        void Update()
        {
            if (!car) return;
            float s01 = Mathf.Clamp01(car.Speed / TrafficCar.CruiseSpeed);
            var gm = GameManager.Instance;
            float live = gm && gm.State != GameState.Playing && gm.State != GameState.Paused ? 0.5f : 1f;
            if (engine)
            {
                engine.Pitch = basePitch * Mathf.Lerp(0.72f, 1.3f, s01);
                engine.Modulation = Mathf.Lerp(0.45f, 1f, s01) * live;
            }
            if (tyres)
            {
                tyres.Pitch = Mathf.Lerp(0.85f, 1.15f, s01);
                tyres.Modulation = Mathf.Pow(s01, 1.4f) * live;
            }
            if (!gm || !gm.IsPlaying) return;
            float dt = Time.deltaTime;
            hornCd -= dt;
            stoppedT = car.Speed < 0.4f ? stoppedT + dt : 0f;
            if (stoppedT > 1.5f && hornCd <= 0f && SomeoneInFront())
            {
                hornCd = Random.Range(7f, 14f);
                Sfx.Play(SfxId.CarHorn, transform.position + transform.forward * 1.5f + Vector3.up * 0.8f);
            }
        }

        bool SomeoneInFront()
        {
            Vector3 p = transform.position, f = transform.forward;
            foreach (var h in HostCharacter.All)
            {
                if (!h) continue;
                Vector3 d = h.transform.position - p; d.y = 0f;
                if (d.sqrMagnitude < 49f && Vector3.Dot(d.normalized, f) > 0.6f) return true;
            }
            var pm = Possession.PossessionManager.Instance;
            if (pm)
                foreach (var a in pm.Agents)
                {
                    if (a == null || !a.Blob || !a.IsFree) continue;
                    Vector3 d = a.Blob.transform.position - p; d.y = 0f;
                    if (d.sqrMagnitude < 49f && Vector3.Dot(d.normalized, f) > 0.6f) return true;
                }
            return false;
        }
    }
}
