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
        static readonly bool TraceRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "-traceCinematicLifetime") >= 0;
        void TraceLifetime(string action)
        {
            if (!TraceRequested) return;
            // Do not query native frame/time/texture properties while tracing a possible native media lock.
            Debug.Log($"[CinematicLifetime] film={Definition.ResourcePath} action={action} frame={Time.frameCount} real={Time.realtimeSinceStartupAsDouble:F4} started={started} ready={ready} failed={failed} disposed={disposed} seekPending={seekPending}");
        }
        public CinematicFilmSpec Definition { get; }
        readonly GameObject root, nativeRoot;
        readonly VideoPlayer player;
        readonly RawImage picture;
        readonly RenderTexture target;
        bool failed, nativeTerminalError, started, ready, disposed, seekPending, preparePending;
        bool retirementComplete, retirementQuarantined, retirementSeekAcknowledged, retirementSawSeek;
        int retirementStage, retirementFrame;
        double retirementDeadline;
        string retirementError;
        readonly float prepareStarted;
        float opacity = 1, lastSeekAt = -100, playStartedAt;
        public bool Ready => failed || ready || !player;
        public bool Failed => failed;
        public bool HasDecodedFrame => !failed && ready && player && player.frame >= 0;
        public VideoPlayer Player => player;
        public RawImage Picture => picture;
        public bool RetirementComplete => retirementComplete;
        public bool RetirementQuarantined => retirementQuarantined;
        public bool RetirementSeekAcknowledged => retirementSeekAcknowledged;
        public bool RetirementSawPendingSeek => retirementSawSeek;
        public string RetirementError => retirementError;
        internal bool DisposalRequested => disposed;
        internal bool PresentationOwnerGone => !root || !root.activeInHierarchy;
        public bool Seeking => seekPending;
        public int LoopCount { get; private set; }

        public CinematicFilm(string resource, Transform canvas)
            : this(CinematicFilmSpec.Legacy(resource), canvas, null, strictMissing: true) { }

        public static bool TryCreate(CinematicFilmSpec definition, Transform canvas, out CinematicFilm film)
        {
            film = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || CinematicFilmLifetime.IsQuitting || CinematicFilmLifetime.QuarantinedCount > 0) return false;
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
                // Native media never belongs to a destroyable scene/UI hierarchy.
                nativeRoot = CinematicFilmLifetime.CreateNativeHost(this, "RTX Cycles Native - " + clip.name);
                player = nativeRoot.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.source = VideoSource.VideoClip; player.clip = clip;
                player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = target;
                player.audioOutputMode = VideoAudioOutputMode.None; // Metadata validation rejects embedded soundtrack tracks.
                player.isLooping = definition.Loop; player.skipOnDrop = true; player.waitForFirstFrame = true;
                player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
                player.errorReceived += OnError; player.prepareCompleted += OnPrepared; player.seekCompleted += OnSeekCompleted; player.loopPointReached += OnLoop;
                preparePending = true;
                TraceLifetime("prepare.before"); player.Prepare(); TraceLifetime("prepare.after");
            }
            catch (Exception exception)
            {
                failed = true; Dispose();
                Debug.LogWarning("[CinematicFilm] Backend setup failed; live story continues: " + exception.Message);
            }
        }

        void OnError(VideoPlayer source, string message)
        {
            if (source != player) return;
            nativeTerminalError = true; // Only the real backend error callback proves terminal native failure.
            FailPresentation(message);
        }
        void FailPresentation(string message)
        {
            if (failed) return;
            failed = true;
            if (picture) picture.enabled = false;
            Debug.LogWarning("[CinematicFilm] Decode failed; live story continues: " + message);
            Dispose(); // Terminal errors retire through the same bounded native owner, not inside the callback.
        }
        void OnPrepared(VideoPlayer source) { if (source != player) return; preparePending = false; TraceLifetime("prepare.completed"); }
        void OnSeekCompleted(VideoPlayer source)
        {
            if (source != player) return;
            if (disposed && seekPending) retirementSeekAcknowledged = true;
            seekPending = false; TraceLifetime("seek.completed");
        }
        void OnLoop(VideoPlayer source) { if (Definition.Loop) LoopCount++; }

        public void SetOpacity(float value) { opacity = Mathf.Clamp01(value); }
        public void BringForward() { if (root) root.transform.SetAsLastSibling(); }
        public void Hide()
        {
            if (picture) picture.enabled = false;
            if (player && player.isPlaying) { TraceLifetime("hide.pause.before"); player.Pause(); TraceLifetime("hide.pause.after"); }
        }

        /// <summary>Decode and pause a neutral hold's first frame, without exposing it. Do not prewarm outcome clips.</summary>
        public void WarmFirstFrame() => Drive(0, 0, warmOnly: true);
        public void Tick(float storySeconds, float speed = 1f) => Drive(storySeconds, speed, warmOnly: false);

        void Drive(float storySeconds, float speed, bool warmOnly)
        {
            if (!root || failed || disposed) return;
            if ((!player.isPrepared && Time.realtimeSinceStartup - prepareStarted > 8f) ||
                (started && !ready && Time.realtimeSinceStartup - playStartedAt > 8f))
            { FailPresentation("First decoded image exceeded eight seconds."); return; }
            bool ended = !Definition.Loop && storySeconds >= Definition.Seconds;
            if (ended && !Definition.HoldFinalFrame)
            { Dispose(); return; } // Legacy inserts also retire safely if their last drift seek is still outstanding.
            double desired = Definition.TimeAt(storySeconds);
            if (player.isPrepared && !started)
            {
                started = true; playStartedAt = Time.realtimeSinceStartup;
                if (player.canSetTime && Math.Abs(player.time - desired) > .0001)
                { seekPending = true; lastSeekAt = Time.realtimeSinceStartup; player.time = desired; }
                player.Play(); // Even a paused preview needs a first decoded image before pausing.
            }
            if (started && player.frame >= 0) ready = true;
            if (ready)
            {
                if (seekPending && Time.realtimeSinceStartup - lastSeekAt > CinematicFilmLifetime.OperationTimeout)
                { FailPresentation("Owned seek acknowledgement exceeded eight seconds."); return; }
                bool pause = warmOnly || speed <= 0 || ended;
                double drift = Math.Abs(player.time - desired);
                if (Definition.Loop) drift = Math.Min(drift, Math.Abs(Definition.Seconds - drift));
                // One owned seek at a time: callbacks have no request ID, so overlapping requests cannot be safely acknowledged.
                if (!warmOnly && player.canSetTime && drift > .12 &&
                    !seekPending && Time.realtimeSinceStartup - lastSeekAt > .35f)
                {
                    lastSeekAt = Time.realtimeSinceStartup; seekPending = true;
                    TraceLifetime("seek.request.before"); player.time = desired; TraceLifetime("seek.request.after");
                }
                if (pause) { if (player.isPlaying) { TraceLifetime("ready.pause.before"); player.Pause(); TraceLifetime("ready.pause.after"); } }
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
            TraceLifetime("dispose.enter"); disposed = true;
            retirementSawSeek = seekPending;
            retirementDeadline = Time.realtimeSinceStartupAsDouble + CinematicFilmLifetime.OperationTimeout;
            // Visual skip/release is immediate. Do not disable or Stop the separate native player yet.
            if (picture) { picture.enabled = false; picture.texture = null; }
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            TraceLifetime("dispose.visuals-removed-native-retained");
            if (!nativeRoot)
            {
                ReleaseTarget(); retirementComplete = true;
            }
        }
        void RetirementFailure(string reason)
        {
            if (retirementError != null) return;
            retirementError = Definition.ResourcePath + ": " + reason;
            CinematicFilmLifetime.ReportFailure(retirementError);
        }
        internal void AdvanceRetirement(double now, int frame)
        {
            try { AdvanceRetirementCore(now, frame); }
            catch (Exception error)
            {
                RetirementFailure("native retirement API failed: " + error.Message);
                if ((preparePending || seekPending) && !nativeTerminalError)
                { retirementQuarantined = retirementComplete = true; return; } // An exception is not a native ACK either.
                try
                {
                    if (nativeRoot) UnityEngine.Object.Destroy(nativeRoot);
                    retirementStage = 2; retirementFrame = frame;
                }
                catch { retirementQuarantined = retirementComplete = true; }
            }
        }
        void AdvanceRetirementCore(double now, int frame)
        {
            if (retirementQuarantined)
            {
                // Retain the live reader/RT until its own late ACK or terminal backend error; a timer cannot make Stop safe.
                if (retirementStage == 0 && (nativeTerminalError || (!preparePending && !seekPending)))
                { retirementQuarantined = retirementComplete = false; TraceLifetime("retire.quarantine-operation-resolved"); }
                else if (!player && !nativeRoot)
                { ReleaseTarget(); retirementQuarantined = false; TraceLifetime("retire.quarantine-reclaimed"); return; }
                else return;
            }
            if (!disposed || retirementComplete) return;
            if (retirementStage == 0)
            {
                bool pending = preparePending || seekPending;
                if (pending && !nativeTerminalError)
                {
                    if (player && player.isActiveAndEnabled && now < retirementDeadline) return;
                    RetirementFailure("owned prepare/seek unacknowledged after timeout or reader disable; native reader and target retained");
                    retirementQuarantined = retirementComplete = true;
                    TraceLifetime("retire.unacknowledged-quarantined"); return;
                }
                TraceLifetime(nativeTerminalError ? "retire.cancel-terminal-backend-error" : "retire.acknowledged");
                if (player)
                {
                    player.errorReceived -= OnError; player.prepareCompleted -= OnPrepared;
                    player.seekCompleted -= OnSeekCompleted; player.loopPointReached -= OnLoop;
                    TraceLifetime("player.stop.before"); player.Stop(); TraceLifetime("player.stop.after");
                    TraceLifetime("player-target.detach.before"); player.targetTexture = null; TraceLifetime("player-target.detach.after");
                }
                retirementStage = 1; retirementFrame = frame; return;
            }
            if (frame <= retirementFrame) return;
            if (retirementStage == 1)
            {
                // Give Stop's native clear job a frame, then observe component destruction before releasing its RT.
                if (nativeRoot) { TraceLifetime("native-root.destroy-schedule.before"); UnityEngine.Object.Destroy(nativeRoot); TraceLifetime("native-root.destroy-schedule.after"); }
                retirementStage = 2; retirementFrame = frame; return;
            }
            if (player || nativeRoot)
            {
                if (now < retirementDeadline + 2) return;
                RetirementFailure("native host destruction was not observed; target retained rather than released under a native reader");
                retirementQuarantined = retirementComplete = true; return;
            }
            ReleaseTarget(); retirementComplete = true; TraceLifetime("retire.complete");
        }
        void ReleaseTarget()
        {
            if (!target) return;
            TraceLifetime("rt.release.before"); target.Release(); TraceLifetime("rt.release.after");
            UnityEngine.Object.Destroy(target); TraceLifetime("rt.destroy-schedule.after");
        }
        internal void HostOwnerDestroyed()
        {
            // Only forced engine/domain shutdown should reach this with owned work still present.
            if (retirementComplete && !retirementQuarantined) return;
            if (picture) { picture.enabled = false; picture.texture = null; }
            if (root) UnityEngine.Object.Destroy(root);
            if (player)
            {
                player.errorReceived -= OnError; player.prepareCompleted -= OnPrepared;
                player.seekCompleted -= OnSeekCompleted; player.loopPointReached -= OnLoop;
            }
            // Never explicitly Release a target while its native reader still exists.
            if (!player) ReleaseTarget();
            TraceLifetime("host-owner.forced-destroy");
        }
    }
}
