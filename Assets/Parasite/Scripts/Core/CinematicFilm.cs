using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Parasite
{
    /// <summary>Silent decoded footage below live story UI. The owner's unscaled story clock remains authoritative.</summary>
    public sealed class CinematicFilm : IDisposable
    {
        public const float Duration = 3f; // Existing 0.4 establishing-insert contract.
        public CinematicFilmSpec Definition { get; }
        readonly GameObject root;
        readonly VideoPlayer player;
        readonly RawImage picture;
        readonly RenderTexture target;
        bool failed, started, ready, disposed, seekPending;
        readonly float prepareStarted;
        float opacity = 1, lastSeekAt = -100, playStartedAt;
        public bool Ready => failed || ready || !player;
        public bool Failed => failed;
        public bool HasDecodedFrame => !failed && ready && player && player.frame >= 0;
        public VideoPlayer Player => player;
        public bool Seeking => seekPending;
        public int LoopCount { get; private set; }

        public CinematicFilm(string resource, Transform canvas)
            : this(CinematicFilmSpec.Legacy(resource), canvas, null, strictMissing: true) { }

        public static bool TryCreate(CinematicFilmSpec definition, Transform canvas, out CinematicFilm film)
        {
            film = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
            var clip = Resources.Load<VideoClip>(definition.ResourcePath);
            if (!clip) return false; // Optional full footage is not a dependency of playable fallback scenes.
            if (!definition.Validate(clip, out string reason))
            { Debug.LogWarning("[CinematicFilm] Rejected " + definition.ResourcePath + ": " + reason); return false; }
            film = new CinematicFilm(definition, canvas, clip, strictMissing: false);
            if (film.Failed) { film.Dispose(); film = null; return false; }
            return true;
        }

        CinematicFilm(CinematicFilmSpec definition, Transform canvas, VideoClip clip, bool strictMissing)
        {
            Definition = definition;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { failed = true; return; }
            if (!clip) clip = Resources.Load<VideoClip>(definition.ResourcePath);
            if (!clip)
            {
                if (strictMissing) throw new InvalidOperationException("Missing RTX-rendered cinematic film: " + definition.ResourcePath);
                failed = true; return;
            }
            if (!definition.Validate(clip, out string reason))
            { failed = true; Debug.LogWarning("[CinematicFilm] Rejected " + definition.ResourcePath + ": " + reason); return; }
            prepareStarted = Time.realtimeSinceStartup;
            try
            {
                root = new GameObject("RTX Cycles Film - " + clip.name, typeof(RectTransform), typeof(RawImage));
                root.transform.SetParent(canvas, false); root.transform.SetAsFirstSibling();
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                picture = root.GetComponent<RawImage>(); picture.raycastTarget = false; picture.enabled = false;
                var fit = root.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)definition.Width / definition.Height;
                target = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32)
                { name = clip.name + " decoded frame", hideFlags = HideFlags.DontSave };
                if (!target.Create()) throw new InvalidOperationException("Cannot allocate the decoded frame target.");
                picture.texture = target;
                player = root.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.source = VideoSource.VideoClip; player.clip = clip;
                player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = target;
                player.audioOutputMode = VideoAudioOutputMode.None; // Metadata validation rejects embedded soundtrack tracks.
                player.isLooping = definition.Loop; player.skipOnDrop = true; player.waitForFirstFrame = true;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.errorReceived += OnError; player.seekCompleted += OnSeekCompleted; player.loopPointReached += OnLoop;
                player.Prepare();
            }
            catch (Exception exception)
            {
                failed = true; Dispose();
                Debug.LogWarning("[CinematicFilm] Backend setup failed; live story continues: " + exception.Message);
            }
        }

        void OnError(VideoPlayer source, string message)
        {
            if (failed || disposed) return;
            failed = true;
            if (picture) picture.enabled = false;
            if (player) player.Stop();
            Debug.LogWarning("[CinematicFilm] Decode failed; live story continues: " + message);
        }
        void OnSeekCompleted(VideoPlayer source) => seekPending = false;
        void OnLoop(VideoPlayer source) { if (Definition.Loop) LoopCount++; }

        public void SetOpacity(float value) { opacity = Mathf.Clamp01(value); }
        public void BringForward() { if (root) root.transform.SetAsLastSibling(); }
        public void Hide() { if (picture) picture.enabled = false; if (player && player.isPlaying) player.Pause(); }

        /// <summary>Decode and pause a neutral hold's first frame, without exposing it. Do not prewarm outcome clips.</summary>
        public void WarmFirstFrame() => Drive(0, 0, warmOnly: true);
        public void Tick(float storySeconds, float speed = 1f) => Drive(storySeconds, speed, warmOnly: false);

        void Drive(float storySeconds, float speed, bool warmOnly)
        {
            if (!root || failed || disposed) return;
            if ((!player.isPrepared && Time.realtimeSinceStartup - prepareStarted > 8f) ||
                (started && !ready && Time.realtimeSinceStartup - playStartedAt > 8f))
            { OnError(player, "First decoded image exceeded eight seconds."); return; }
            bool ended = !Definition.Loop && storySeconds >= Definition.Seconds;
            if (ended && !Definition.HoldFinalFrame)
            { Hide(); if (player.isPlaying) player.Stop(); return; }
            double desired = Definition.TimeAt(storySeconds);
            if (player.isPrepared && !started)
            {
                started = true; playStartedAt = Time.realtimeSinceStartup;
                if (player.canSetTime) player.time = desired;
                player.Play(); // Even a paused preview needs a first decoded image before pausing.
            }
            if (started && player.frame >= 0) ready = true;
            if (ready)
            {
                bool pause = warmOnly || speed <= 0 || ended;
                double drift = Math.Abs(player.time - desired);
                if (Definition.Loop) drift = Math.Min(drift, Math.Abs(Definition.Seconds - drift));
                // Seeks are asynchronous. Correct sustained drift without requesting a new seek every frame.
                if (!warmOnly && player.canSetTime && drift > .12 &&
                    (!seekPending || Time.realtimeSinceStartup - lastSeekAt > 1f) && Time.realtimeSinceStartup - lastSeekAt > .35f)
                {
                    lastSeekAt = Time.realtimeSinceStartup; seekPending = true; player.time = desired;
                }
                if (pause) { if (player.isPlaying) player.Pause(); }
                else
                {
                    if (player.canSetPlaybackSpeed) player.playbackSpeed = Mathf.Clamp(speed, .01f, 10);
                    if (!player.isPlaying) player.Play();
                }
            }
            picture.enabled = !warmOnly && ready && player.texture && player.frame >= 0 && opacity > 0;
            float fade = Definition.FadeOut > 0 ? Mathf.Clamp01((Definition.Seconds - storySeconds) / Definition.FadeOut) : 1;
            picture.color = new Color(1, 1, 1, opacity * fade);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (player)
            {
                player.errorReceived -= OnError; player.seekCompleted -= OnSeekCompleted; player.loopPointReached -= OnLoop;
                player.Stop(); player.targetTexture = null;
            }
            if (picture) { picture.enabled = false; picture.texture = null; }
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            if (target) { target.Release(); UnityEngine.Object.Destroy(target); }
        }
    }
}
