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
        static readonly bool TraceRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "-traceCinematicLifetime") >= 0;
        void TraceLifetime(string action)
        {
            if (TraceRequested) Debug.Log($"[CinematicLifetime] owner=acceptance action={action} frame={Time.frameCount} real={Time.realtimeSinceStartupAsDouble:F4} passed={passed}");
        }
        void OnEnable() { if (TraceRequested) Application.quitting += OnApplicationQuitting; }
        void OnDisable() { if (TraceRequested) Application.quitting -= OnApplicationQuitting; }
        void OnApplicationQuitting() => TraceLifetime("application.quitting");

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
            yield return Finish();
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
            if (TraceRequested) Application.quitting -= OnApplicationQuitting;
            film?.Dispose(); Time.timeScale = 1f;
            TraceLifetime("owner.on-destroy.exit");
        }
    }
}
