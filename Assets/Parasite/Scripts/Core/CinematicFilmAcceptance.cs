using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Parasite.Testing
{
    /// <summary>Built-Linux-player video backend acceptance: -cinematicTestOut /absolute/report.txt.</summary>
    public sealed class CinematicFilmAcceptance : MonoBehaviour
    {
        string output;
        readonly StringBuilder report = new();
        bool passed = true;
        CinematicFilm film;
        int? quitDuringSeek;
        bool quitProbeStarted, quitProbeReady, quitObserved;
        static readonly bool TraceRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "-traceCinematicLifetime") >= 0;
        void TraceLifetime(string action)
        {
            if (TraceRequested) Debug.Log($"[CinematicLifetime] owner=acceptance action={action} frame={Time.frameCount} real={Time.realtimeSinceStartupAsDouble:F4} passed={passed}");
        }
        void OnEnable() => Application.quitting += OnApplicationQuitting;
        void OnDisable() => Application.quitting -= OnApplicationQuitting;
        void OnApplicationQuitting()
        {
            TraceLifetime("application.quitting");
            if (!quitProbeStarted) return;
            quitObserved = true;
            bool clean = quitProbeReady && film != null && film.RetirementComplete && film.RetirementSeekAcknowledged &&
                !film.RetirementQuarantined && film.RetirementError == null && CinematicFilmLifetime.ResourceCount == 0 &&
                CinematicFilmLifetime.PendingCount == 0 && CinematicFilmLifetime.QuarantinedCount == 0;
            Record(clean, $"Optional explicit quit{quitDuringSeek} reached application.quitting after actual seek ACK and complete native retirement; owned={CinematicFilmLifetime.ResourceCount}, pending={CinematicFilmLifetime.PendingCount}, quarantine={CinematicFilmLifetime.QuarantinedCount}");
            WriteQuitProbeReport();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-cinematicTestOut")
                {
                    var root = new GameObject("Standalone cinematic decode acceptance");
                    DontDestroyOnLoad(root);
                    root.AddComponent<CinematicFilmAcceptance>().output = args[i + 1];
                    return;
                }
        }

        IEnumerator Start()
        {
            // Let Bootstrap initialize its camera before freezing gameplay time.
            yield return null; yield return null;
            report.AppendLine("GPU: " + SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            bool noFinalSeek = Array.IndexOf(Environment.GetCommandLineArgs(), "-cinematicNoFinalSeek") >= 0;
            report.AppendLine("Experimental acceptance no-final-seek: " + noFinalSeek + "; default control retains Tick(end). This switch is not a runtime fix.");
            TraceLifetime("start no-final-seek=" + noFinalSeek);
            var arguments = Environment.GetCommandLineArgs();
            int quitIndex = Array.IndexOf(arguments, "-cinematicQuitDuringSeek");
            if (quitIndex >= 0)
            {
                if (quitIndex + 1 >= arguments.Length || !int.TryParse(arguments[quitIndex + 1], out int code) || (code != 0 && code != 1) || noFinalSeek)
                { Record(false, "Optional -cinematicQuitDuringSeek requires0or1 and cannot combine with experimental -cinematicNoFinalSeek."); yield return Finish(); yield break; }
                quitDuringSeek = code;
                report.AppendLine("Optional explicit quit during an actual owned seek: " + code + "; native exit must match this code independently of managed media gates.");
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Record(false, "A graphics device is required for actual Linux video decode."); yield return Finish(); yield break;
            }
            var canvasObject = new GameObject("Standalone film canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            Time.timeScale = 0f;
            bool requireFull = Array.IndexOf(Environment.GetCommandLineArgs(), "-requireFullStoryFilms") >= 0;
            bool requireLake = Array.IndexOf(Environment.GetCommandLineArgs(), "-requireLakeStoryFilms") >= 0;
            var definitions = new List<CinematicFilmSpec> { CinematicFilmSpec.Legacy("ContainmentOpening"), CinematicFilmSpec.Legacy("LakesideEnding") };
            foreach (var definition in CinematicStoryPlan.FullSet)
                if (requireFull || (requireLake && definition.ResourcePath != CinematicStoryPlan.Opening.ResourcePath) ||
                    Resources.Load<VideoClip>(definition.ResourcePath)) definitions.Add(definition);
            foreach (var definition in definitions)
            {
                string resource = definition.ResourcePath.Substring(definition.ResourcePath.LastIndexOf('/') + 1);
                if (!CinematicFilm.TryCreate(definition, canvas.transform, out film))
                { Record(false, definition.ResourcePath + ": missing or rejected footage metadata/audio contract"); continue; }
                var player = film.Player;
                float deadline = Time.realtimeSinceStartup + 15f;
                while (player && !film.Failed && player.frame < 2 && Time.realtimeSinceStartup < deadline)
                { film.Tick(.1f); yield return null; }
                bool decoded = player && player.isPrepared && player.frame >= 2;
                if (decoded)
                {
                    long frame = player.frame;
                    float observed = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - observed < .25f)
                    { film.Tick(.1f + Time.realtimeSinceStartup - observed); yield return null; }
                    decoded = player.frame > frame && Time.timeScale == 0;
                    string imageFailure = null;
                    try { decoded &= InspectFrame(player.texture as RenderTexture, resource, definition); }
                    catch (Exception e) { decoded = false; imageFailure = e.Message; }
                    if (imageFailure != null) report.AppendLine(resource + " readback: " + imageFailure);
                }
                Record(decoded, resource + $" decoded nonempty {definition.Width}x{definition.Height} image and advancing frames at timeScale=0; expected {definition.Seconds}s/24fps/silent");
                if (decoded && definition.Loop)
                {
                    // Settle the asynchronous seek near the seam before timing a natural wrap.
                    float seekUntil = Time.realtimeSinceStartup + 8;
                    while ((player.frame < (long)((definition.Seconds - .6f) * 24) || film.Seeking) && Time.realtimeSinceStartup < seekUntil)
                    { film.Tick(definition.Seconds - .5f, 0); yield return null; }
                    int previousLoops = film.LoopCount;
                    float loopStarted = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - loopStarted < 1.1f)
                    { film.Tick(definition.Seconds - .5f + Time.realtimeSinceStartup - loopStarted); yield return null; }
                    Record(film.LoopCount > previousLoops && player.isPlaying && Time.timeScale == 0, resource + " neutral hold wraps and continues without publishing a game outcome");
                }
                if (noFinalSeek)
                    TraceLifetime("experimental final-tick.omitted " + resource);
                else
                {
                    TraceLifetime("control final-tick.before " + resource);
                    film.Tick(definition.Seconds);
                    TraceLifetime("control final-tick.after " + resource);
                }
                TraceLifetime("film.dispose.before " + resource); var retiring = film; film.Dispose(); film = null; TraceLifetime("film.dispose.after " + resource);
                bool drained = false;
                yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, result => drained = result);
                if (!drained || !retiring.RetirementComplete || retiring.RetirementError != null)
                    Record(false, resource + " native retirement did not complete cleanly: " + retiring.RetirementError);
                TraceLifetime("post-dispose-drain " + resource + " clean=" + drained + " seekAck=" + retiring.RetirementSeekAcknowledged);
            }
            if (quitDuringSeek.HasValue && passed)
            { yield return QuitWithOwnedPendingSeek(canvas.transform); yield break; }
            yield return Finish();
        }

        IEnumerator QuitWithOwnedPendingSeek(Transform canvas)
        {
            var definition = CinematicStoryPlan.Arrival;
            if (!CinematicFilm.TryCreate(definition, canvas, out film))
            { Record(false, "Optional quit probe requires actual published LakeArrival."); yield return Finish(); yield break; }
            var player = film.Player;
            float until = Time.realtimeSinceStartup + 15;
            while (player && !film.Failed && player.frame < 2 && Time.realtimeSinceStartup < until)
            { film.Tick(.1f); yield return null; }
            bool decoded = player && !film.Failed && player.isPrepared && player.frame >= 2;
            if (decoded)
            {
                long frame = player.frame; float observed = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - observed < .25f)
                { film.Tick(.1f + Time.realtimeSinceStartup - observed); yield return null; }
                decoded = player.frame > frame && Time.timeScale == 0 && player.audioOutputMode == VideoAudioOutputMode.None;
                try { decoded &= InspectFrame(player.texture as RenderTexture, "LakeArrival-QuitProbe", definition); }
                catch (Exception error) { decoded = false; report.AppendLine("Optional quit readback: " + error.Message); }
            }
            Record(decoded, "Optional quit probe decoded actual silent1920x1080 LakeArrival with nonempty readback and advancing frames at timeScale=0");
            if (!decoded) { film.Dispose(); yield return Finish(); yield break; }
            film.Hide();
            until = Time.realtimeSinceStartup + CinematicFilmLifetime.OperationTimeout;
            while (film.Seeking && Time.realtimeSinceStartup < until) yield return null;
            film.Hide();
            float throttleUntil = Time.realtimeSinceStartup + .4f;
            while (Time.realtimeSinceStartup < throttleUntil) yield return null;
            Record(!film.Seeking && !player.isPlaying && CinematicFilmLifetime.ResourceCount == 1 && CinematicFilmLifetime.PendingCount == 0 && CinematicFilmLifetime.QuarantinedCount == 0,
                $"Optional quit setup has one paused real decoder; owned={CinematicFilmLifetime.ResourceCount}, pending={CinematicFilmLifetime.PendingCount}, quarantine={CinematicFilmLifetime.QuarantinedCount}");
            if (!passed) { film.Dispose(); yield return Finish(); yield break; }
            var picture = film.Picture; var target = player.targetTexture;
            film.Tick(definition.Seconds); // The real terminal request; never bypass the normal seek policy.
            Record(film.Seeking && picture.enabled && target && target.IsCreated(), "Optional explicit quit is requested while a real terminal seek is outstanding");
            if (!passed) { film.Dispose(); yield return Finish(); yield break; }
            quitProbeStarted = true;
            Debug.Log($"[CinematicQuitProbe] request code={quitDuringSeek.Value} owned={CinematicFilmLifetime.ResourceCount} pendingSeek={film.Seeking} timeScale={Time.timeScale}");
            CinematicFilmLifetime.Quit(quitDuringSeek.Value); // Intentionally no caller Dispose/Drain before this request.
            quitProbeReady = film.RetirementSawPendingSeek && film.DisposalRequested && !picture.enabled && !picture.texture &&
                player && player.isActiveAndEnabled && player.targetTexture == target && target.IsCreated() &&
                CinematicFilmLifetime.ResourceCount == 1 && CinematicFilmLifetime.PendingCount == 1;
            Record(quitProbeReady, $"Optional explicit quit immediately hidesUI and retains the pending native reader/target; owned={CinematicFilmLifetime.ResourceCount}, pending={CinematicFilmLifetime.PendingCount}");
            if (!quitProbeReady) CinematicFilmLifetime.Quit(1);
            WriteQuitProbeReport(); // Final ownership/ACK receipt is written from application.quitting.
        }
        void WriteQuitProbeReport()
        {
            string path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report + "EXPECTED_NATIVE_EXIT: " + quitDuringSeek + "\nQUIT_OBSERVED: " + quitObserved +
                "\nRESULT: " + (!quitObserved ? "PENDING" : passed ? "PASS" : "FAIL") + "\n");
        }

        bool InspectFrame(RenderTexture frame, string resource, CinematicFilmSpec definition)
        {
            if (!frame || frame.width != definition.Width || frame.height != definition.Height) return false;
            var old = RenderTexture.active;
            var pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = frame; pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply();
                float minimum = 1f, maximum = 0f, sum = 0f; int count = 0;
                for (int y = 10; y < frame.height; y += 23)
                    for (int x = 10; x < frame.width; x += 23)
                    { float value = pixels.GetPixel(x, y).grayscale; minimum = Mathf.Min(minimum, value); maximum = Mathf.Max(maximum, value); sum += value; count++; }
                string path = Path.GetFullPath(output) + "." + resource + ".png";
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, pixels.EncodeToPNG());
                report.AppendLine($"{resource}: image mean={sum / count:F3} range={maximum - minimum:F3}; {path}");
                return maximum > .15f && maximum - minimum > .1f && sum / count > .03f;
            }
            finally { RenderTexture.active = old; Destroy(pixels); }
        }

        void Record(bool success, string message)
        {
            passed &= success;
            string line = "[FilmTest] " + (success ? "PASS " : "FAIL ") + message;
            report.AppendLine(line); Debug.Log(line);
        }

        IEnumerator Finish()
        {
            TraceLifetime("finish.enter");
            bool drained = false;
            yield return CinematicFilmLifetime.Drain(CinematicFilmLifetime.DrainTimeout, result => drained = result);
            if (!drained) Record(false, "Native cinematic retirement drain failed before quit.");
            Time.timeScale = 1f;
            string path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path));
            report.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL")); File.WriteAllText(path, report.ToString());
            TraceLifetime("finish.report-written " + path);
            TraceLifetime("finish.pre-quit exit=" + (passed ? 0 : 1));
            CinematicFilmLifetime.Quit(passed ? 0 : 1);
            TraceLifetime("finish.post-quit-return");
        }

        void OnDestroy()
        {
            TraceLifetime("owner.on-destroy.enter");
            Application.quitting -= OnApplicationQuitting;
            film?.Dispose(); Time.timeScale = 1f;
            TraceLifetime("owner.on-destroy.exit");
        }
    }
}
