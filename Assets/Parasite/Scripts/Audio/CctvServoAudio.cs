using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Audio
{
    /// <summary>Added at runtime to each SecurityCamera: servo whirr whose level follows the head's sweep speed.</summary>
    public sealed class CctvServoAudio : MonoBehaviour
    {
        SecurityCamera cam;
        AudioLoopEmitter servo;
        float lastYaw, smoothed;

        void Start()
        {
            cam = GetComponent<SecurityCamera>();
            var clip = Resources.Load<AudioClip>("Audio/Ambience/amb_cctv_servo");
            if (!cam || !clip) { enabled = false; return; }
            var head = cam.head ? cam.head : transform;
            servo = AudioLoopEmitter.Create(head, "Servo", head.position, clip, 0.55f, 1f, 13f, AudioLoopEmitter.Bus.Effects, 190);
            lastYaw = head.localEulerAngles.y;
        }

        void Update()
        {
            if (!cam || !servo) return;
            var head = cam.head ? cam.head : transform;
            float yaw = head.localEulerAngles.y;
            float dt = Time.deltaTime;
            float speed = dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt : 0f;
            lastYaw = yaw;
            if (dt > 0f) smoothed = Mathf.Lerp(smoothed, speed, 1f - Mathf.Exp(-8f * dt));
            float k = cam.Disabled ? 0f : Mathf.Clamp01(smoothed / 20f);
            servo.Modulation = k;
            servo.Pitch = 0.85f + 0.3f * k;
        }
    }
}
