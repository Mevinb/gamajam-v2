using System;
using System.Collections;
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
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Record(false, "A graphics device is required for actual Linux video decode."); Finish(); yield break;
            }
            var canvasObject = new GameObject("Standalone film canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            Time.timeScale = 0f;
            foreach (string resource in new[] { "ContainmentOpening", "LakesideEnding" })
            {
                string failure = null;
                try { film = new CinematicFilm(resource, canvas.transform); }
                catch (Exception e) { failure = e.Message; }
                if (failure != null) { Record(false, resource + ": " + failure); continue; }
                var player = canvasObject.GetComponentInChildren<VideoPlayer>();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (player && player.frame < 2 && Time.realtimeSinceStartup < deadline)
                { film.Tick(.1f); yield return null; }
                bool decoded = player && player.isPrepared && player.frame >= 2;
                if (decoded)
                {
                    long frame = player.frame;
                    yield return new WaitForSecondsRealtime(.25f);
                    decoded = player.frame > frame && Time.timeScale == 0;
                    string imageFailure = null;
                    try { decoded &= InspectFrame(player.texture as RenderTexture, resource); }
                    catch (Exception e) { decoded = false; imageFailure = e.Message; }
                    if (imageFailure != null) report.AppendLine(resource + " readback: " + imageFailure);
                }
                Record(decoded, resource + " decoded nonempty 1280x720 image and advancing frames at timeScale=0");
                film.Tick(CinematicFilm.Duration); film.Dispose(); film = null;
                yield return null;
            }
            Finish();
        }

        bool InspectFrame(RenderTexture frame, string resource)
        {
            if (!frame || frame.width != 1280 || frame.height != 720) return false;
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

        void Finish()
        {
            Time.timeScale = 1f;
            string path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path));
            report.AppendLine("RESULT: " + (passed ? "PASS" : "FAIL")); File.WriteAllText(path, report.ToString());
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            Application.Quit(passed ? 0 : 1);
#endif
        }

        void OnDestroy() { film?.Dispose(); Time.timeScale = 1f; }
    }
}
