using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite
{
    /// <summary>Owns only native cinematic players and their bounded retirement, independently of scene UI.</summary>
    public sealed class CinematicFilmLifetime : MonoBehaviour
    {
        public const float OperationTimeout = 8f;
        public const float DrainTimeout = 10f;
        static CinematicFilmLifetime instance;
        static bool allowQuit, shutdown;
        readonly List<CinematicFilm> films = new();
        bool quitRequested, stopEditorPlayMode, quitHadFailure;
        int requestedExitCode, quitFailureSerial, failureSerial;
        double quitDeadline;
        static readonly bool TraceRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "-traceCinematicLifetime") >= 0;
        public static bool IsQuitting => shutdown || (instance && instance.quitRequested);
        public static int ResourceCount => instance ? instance.films.Count : 0;
        public static int PendingCount
        {
            get
            {
                int count = 0;
                if (instance) foreach (var film in instance.films) if (film.DisposalRequested && !film.RetirementComplete) count++;
                return count;
            }
        }
        public static int QuarantinedCount
        {
            get
            {
                int count = 0;
                if (instance) foreach (var film in instance.films) if (film.RetirementQuarantined) count++;
                return count;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Application.wantsToQuit -= WantsToQuit; instance = null; allowQuit = shutdown = false;
        }
        void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this; DontDestroyOnLoad(gameObject);
        }
        void OnEnable() { Application.wantsToQuit -= WantsToQuit; Application.wantsToQuit += WantsToQuit; }
        void OnDisable() => Application.wantsToQuit -= WantsToQuit;
        void OnApplicationQuit() => shutdown = true;
        internal static GameObject CreateNativeHost(CinematicFilm film, string name)
        {
            if (IsQuitting || QuarantinedCount > 0 || !Application.isPlaying) throw new InvalidOperationException("Cannot allocate cinematic media during shutdown/quarantine/outside play mode.");
            if (!instance) new GameObject("[Cinematic native lifetimes]").AddComponent<CinematicFilmLifetime>();
            var host = new GameObject(name); host.transform.SetParent(instance.transform, false);
            instance.films.Add(film);
            return host;
        }
        internal static void ReportFailure(string reason)
        {
            if (instance) instance.failureSerial++;
            Debug.LogWarning("[CinematicLifetime] retirement failed: " + reason);
        }
        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = films.Count - 1; i >= 0; i--)
            {
                var film = films[i];
                if (!film.DisposalRequested && film.PresentationOwnerGone) film.Dispose();
                if (film.DisposalRequested) film.AdvanceRetirement(now, Time.frameCount);
                if (film.RetirementComplete && !film.RetirementQuarantined) films.RemoveAt(i);
            }
            if (quitRequested && (films.Count == 0 || now >= quitDeadline))
            {
                bool clean = films.Count == 0 && !quitHadFailure && failureSerial == quitFailureSerial;
                int code = requestedExitCode != 0 ? requestedExitCode : clean ? 0 : 1;
                if (TraceRequested) Debug.Log($"[CinematicLifetime] quit.drain.complete clean={clean} requested={requestedExitCode} actual={code} remaining={films.Count}");
                allowQuit = true; ExitNow(code, stopEditorPlayMode);
            }
        }
        public static void RetireAll()
        {
            if (!instance) return;
            foreach (var film in instance.films.ToArray()) film.Dispose();
        }
        /// <summary>Bounded observer only: a caller timeout never destroys a still-owned native target.</summary>
        public static IEnumerator Drain(float timeout, Action<bool> completed = null)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + Math.Max(0, timeout);
            int failures = instance ? instance.failureSerial : 0;
            while (PendingCount > 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            completed?.Invoke(PendingCount == 0 && QuarantinedCount == 0 && (!instance || instance.failureSerial == failures));
        }
        public static void Quit(int exitCode) => RequestQuit(exitCode, false);
        public static void QuitPlayMode() => RequestQuit(0, true);
        static void RequestQuit(int exitCode, bool stopPlayMode)
        {
            if (!instance || instance.films.Count == 0) { allowQuit = true; ExitNow(exitCode, stopPlayMode); return; }
            if (instance.quitRequested)
            {
                if (exitCode != 0)
                {
                    if (instance.requestedExitCode == 0) instance.requestedExitCode = exitCode;
                    if (!stopPlayMode) instance.stopEditorPlayMode = false; // Explicit batch failure must not become an Editor stop-play-mode request.
                }
                return;
            }
            instance.quitRequested = true; instance.requestedExitCode = exitCode; instance.stopEditorPlayMode = stopPlayMode;
            instance.quitFailureSerial = instance.failureSerial;
            instance.quitHadFailure = instance.films.Exists(film => film.RetirementError != null);
            instance.quitDeadline = Time.realtimeSinceStartupAsDouble + DrainTimeout;
            RetireAll();
        }
        static bool WantsToQuit()
        {
            if (allowQuit || !instance || instance.films.Count == 0) return true;
            // OS/window close is exit0. Project callers use Quit(code), preserving their explicit failure codes.
            RequestQuit(0, false); return false;
        }
        static void ExitNow(int code, bool stopPlayMode)
        {
#if UNITY_EDITOR
            if (stopPlayMode) UnityEditor.EditorApplication.isPlaying = false;
            else UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
        void OnDestroy()
        {
            Application.wantsToQuit -= WantsToQuit;
            if (instance != this) return;
            // Normal quit has already drained. Forced engine/domain teardown cannot run another async drain.
            foreach (var film in films) film.HostOwnerDestroyed();
            films.Clear(); instance = null;
        }
    }
}
