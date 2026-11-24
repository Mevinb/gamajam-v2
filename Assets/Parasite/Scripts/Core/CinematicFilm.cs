using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Parasite
{
    /// <summary>A short RTX/Cycles establishing insert beneath the live story captions; unscaled playback keeps story time frozen.</summary>
    public sealed class CinematicFilm : IDisposable
    {
        public const float Duration = 3f;
        readonly GameObject root;
        readonly VideoPlayer player;
        readonly RawImage picture;
        readonly RenderTexture target;
        bool failed, started, ready;
        readonly float prepareStarted;
        public bool Ready => failed || ready || !player;

        public CinematicFilm(string resource, Transform canvas)
        {
            // Headless logic acceptance has no graphics device or video decoder; GPU playback is verified separately.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { failed = true; return; }
            prepareStarted = Time.realtimeSinceStartup;
            var clip = Resources.Load<VideoClip>("Cinematics/Films/" + resource);
            if (!clip) throw new InvalidOperationException("Missing RTX-rendered cinematic film: " + resource);
            root = new GameObject("RTX Cycles Film - " + resource, typeof(RectTransform), typeof(RawImage));
            root.transform.SetParent(canvas, false); root.transform.SetAsFirstSibling();
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            picture = root.GetComponent<RawImage>(); picture.raycastTarget = false; picture.color = Color.white; picture.enabled = false;
            target = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32) { name = resource + " decoded frame" };
            target.Create(); picture.texture = target;
            player = root.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.source = VideoSource.VideoClip; player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = target;
            player.audioOutputMode = VideoAudioOutputMode.None; player.isLooping = false; player.skipOnDrop = true; player.waitForFirstFrame = true;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.errorReceived += OnError; player.Prepare();
        }

        void OnError(VideoPlayer source, string message)
        {
            failed = true; picture.enabled = false;
            Debug.LogWarning("[CinematicFilm] Decode failed; in-engine story continues: " + message);
        }

        public void Tick(float storySeconds, float speed = 1f)
        {
            if (!root || failed) return;
            if (!ready && Time.realtimeSinceStartup - prepareStarted > 5f)
            {
                OnError(player, "Video preparation exceeded five seconds."); return;
            }
            bool show = storySeconds < Duration;
            if (show && player.isPrepared && !started)
            {
                started = true;
                // Preparation can span several frames; join the current story beat instead of delaying input.
                player.time = Math.Min(storySeconds, Math.Max(0, player.length - .05)); player.Play();
            }
            player.playbackSpeed = Mathf.Clamp(speed, 0f, 10f);
            if (started && player.frame >= 0) ready = true;
            picture.enabled = show && started && player.texture && player.frame >= 0;
            picture.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Duration - storySeconds) / .18f));
            if (!show && player.isPlaying) player.Stop();
        }

        public void Dispose()
        {
            if (player) { player.errorReceived -= OnError; player.Stop(); player.targetTexture = null; }
            if (root) UnityEngine.Object.Destroy(root);
            if (target) { target.Release(); UnityEngine.Object.Destroy(target); }
        }
    }
}
