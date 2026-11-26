using System;
using Parasite.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Parasite.Missions
{
    /// <summary>Optional Blender footage; the real coach and boarders retain all departure authority.</summary>
    internal sealed class RailDepartureFilm : IDisposable
    {
        Canvas canvas;
        CinematicFilm film;
        float age;
        public bool Finished => film == null || film.Failed || age >= CinematicStoryPlan.NorthlineDeparture.Seconds;

        public static RailDepartureFilm Create(Transform owner)
        {
            var presentation = new RailDepartureFilm();
            try
            {
                presentation.canvas = UIKit.CreateCanvas("Northline departure footage", 55, owner);
                if (!CinematicFilm.TryCreate(CinematicStoryPlan.NorthlineDeparture, presentation.canvas.transform, out presentation.film))
                { presentation.Dispose(); return null; }
                var root = (RectTransform)presentation.canvas.transform;
                var shade = UIKit.Img(root, "Radio caption shade", UIKit.GradientV, new Color(.015f, .02f, .025f, .85f));
                shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(1, .22f);
                shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
                shade.raycastTarget = false;
                string line = RailCampaign.Peace
                    ? "The medical carriage has its signal. Both free bodies are aboard. Northline is moving."
                    : "Your relay has its line. Both free bodies are aboard. The next city is listening.";
                var caption = UIKit.Txt(root, "Northline radio", line, 27, Color.white, TextAnchor.MiddleCenter, Weight.Medium, true);
                caption.rectTransform.Place(new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 100), new Vector2(1450, 100));
                caption.raycastTarget = false;
                var hint = UIKit.Txt(root, "Skip departure film", "E / SPACE / ESC  ·  SKIP", 18, new Color(.85f, .87f, .88f), TextAnchor.MiddleRight);
                hint.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 35), new Vector2(360, 30));
                hint.raycastTarget = false;
                return presentation;
            }
            catch (Exception error)
            {
                presentation.Dispose();
                Debug.LogWarning("[NorthlineDeparture] Footage unavailable; actual coach departure continues: " + error.Message);
                return null;
            }
        }

        public void Tick(float delta)
        {
            if (film == null || film.Failed) return;
            try
            {
                film.Tick(age);
                if (film.HasDecodedFrame) age += Mathf.Max(0, delta);
            }
            catch (Exception error)
            {
                Dispose();
                Debug.LogWarning("[NorthlineDeparture] Footage interrupted; actual coach departure continues: " + error.Message);
            }
        }

        public void Dispose()
        {
            try { film?.Dispose(); }
            catch (Exception error) { Debug.LogWarning("[NorthlineDeparture] Footage retirement: " + error.Message); }
            finally
            {
                film = null;
                if (canvas) UnityEngine.Object.Destroy(canvas.gameObject);
                canvas = null;
            }
        }
    }
}
