using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parasite.UI
{
    /// <summary>Optional lake segments below interactive UI. No timer or decoder callback may choose a future.</summary>
    public sealed class LakesideFilmSequence : IDisposable
    {
        const float BlendSeconds = .18f;
        readonly GameObject layer;
        readonly CinematicFilm arrival, hold, peace, dominion;
        readonly List<CinematicFilm> owned = new();
        CinematicFilm current, outgoing;
        float blendAge = BlendSeconds, currentTime, outgoingTime;
        bool fallbackTransition, disposed, selectedOnce;
        public bool UsingFullArrival => arrival != null && !arrival.Failed && arrival.Definition.ResourcePath == CinematicStoryPlan.Arrival.ResourcePath;
        public CinematicFilm Current => current;

        public LakesideFilmSequence(Transform canvas)
        {
            layer = new GameObject("Lake film layer", typeof(RectTransform));
            layer.transform.SetParent(canvas, false); layer.transform.SetAsFirstSibling();
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            arrival = Load(CinematicStoryPlan.Arrival) ?? Load(CinematicFilmSpec.Legacy("LakesideEnding"));
            hold = Load(CinematicStoryPlan.Hold);
            peace = Load(CinematicStoryPlan.Peace);
            dominion = Load(CinematicStoryPlan.Dominion);
        }

        CinematicFilm Load(CinematicFilmSpec definition)
        {
            if (!CinematicFilm.TryCreate(definition, layer.transform, out var film)) return null;
            film.SetOpacity(0); owned.Add(film); return film;
        }

        public bool TickArrival(float seconds, float dt)
        {
            hold?.WarmFirstFrame(); // Neutral image only. Branches may prepare metadata but never start playback here.
            return SwitchAndTick(arrival, seconds, dt);
        }
        public bool TickChoice(float seconds, float dt) => SwitchAndTick(hold, seconds, dt);
        public bool TickResolution(EndingOutcome outcome, float seconds, float dt)
        {
            var wanted = outcome == EndingOutcome.Peace ? peace : outcome == EndingOutcome.Dominion ? dominion : null;
            return SwitchAndTick(wanted, seconds, dt);
        }

        bool SwitchAndTick(CinematicFilm wanted, float seconds, float dt)
        {
            if (disposed) return true;
            if (wanted != null && wanted.Failed) wanted = null;
            if (wanted != current)
            {
                if (wanted != null)
                {
                    wanted.SetOpacity(0); wanted.Tick(seconds);
                    if (!wanted.Ready)
                    {
                        currentTime += dt; current?.Tick(currentTime);
                        return false; // The new phase's captions/voice/clock wait for its first image.
                    }
                    if (wanted.Failed) wanted = null;
                }
                outgoing?.Hide(); outgoing = current; outgoingTime = currentTime; current = wanted;
                currentTime = seconds;
                // Preserve the existing first-frame insert behavior; only subsequent segment changes crossfade.
                blendAge = selectedOnce ? 0 : BlendSeconds; selectedOnce = true;
                fallbackTransition = current == null;
                current?.BringForward();
            }
            // A decoder can fail after a segment became visible; return smoothly to the already-running live set.
            if (current != null && current.Failed)
            { current.Hide(); current = null; outgoing?.Hide(); outgoing = null; blendAge = BlendSeconds; }
            currentTime = seconds;
            blendAge = Mathf.Min(BlendSeconds, blendAge + Mathf.Max(0, dt));
            float blend = blendAge / BlendSeconds;
            if (current != null)
            {
                current.SetOpacity(blend); current.Tick(seconds);
                // Keep the old image fully opaque until the incoming image covers it; no transparency hole to the set.
                outgoing?.SetOpacity(1);
            }
            else if (fallbackTransition && outgoing != null)
            { outgoing.SetOpacity(1 - blend); outgoing.Tick(outgoingTime); }
            if (blendAge >= BlendSeconds && outgoing != null) { outgoing.Hide(); outgoing = null; }
            return current == null || current.Ready;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var film in owned) film.Dispose();
            owned.Clear();
            if (layer) { layer.SetActive(false); UnityEngine.Object.Destroy(layer); }
        }
    }
}
