using System;
using UnityEngine;
using UnityEngine.Video;

namespace Parasite
{
    /// <summary>Footage contract. Dialogue/music remain live game audio; encoded movies must contain no audio tracks.</summary>
    public readonly struct CinematicFilmSpec
    {
        public readonly string ResourcePath;
        public readonly float Seconds;
        public readonly uint Width, Height;
        public readonly bool Loop, HoldFinalFrame;
        public readonly float FadeOut;
        public const double Fps = 24;

        public CinematicFilmSpec(string path, float seconds, uint width, uint height, bool loop = false, bool holdFinal = true, float fadeOut = 0)
        { ResourcePath = path; Seconds = seconds; Width = width; Height = height; Loop = loop; HoldFinalFrame = holdFinal; FadeOut = fadeOut; }

        public double TimeAt(double storySeconds)
        {
            double time = Math.Max(0, storySeconds);
            return Loop ? time % Seconds : Math.Min(time, Math.Max(0, Seconds - 1 / Fps));
        }

        public bool ValidateMetadata(uint width, uint height, double fps, double seconds, int audioTracks, out string reason)
        {
            reason = null;
            if (double.IsNaN(fps) || double.IsInfinity(fps) || double.IsNaN(seconds) || double.IsInfinity(seconds) || fps <= 0 || seconds <= 0)
                reason = "Movie timing metadata must be finite and positive.";
            else if (width != Width || height != Height) reason = $"Expected {Width}x{Height}, found {width}x{height}.";
            else if (Math.Abs(fps - Fps) > .01) reason = $"Expected 24 fps, found {fps}.";
            else if (Math.Abs(seconds - Seconds) > 1 / Fps + .005) reason = $"Expected {Seconds}s, found {seconds:F3}s.";
            else if (audioTracks != 0) reason = "Encoded story footage must be silent; live voices/music own the soundtrack.";
            return reason == null;
        }

        public bool Validate(VideoClip clip, out string reason)
        {
            if (!ValidateMetadata(clip.width, clip.height, clip.frameRate, clip.length, clip.audioTrackCount, out reason)) return false;
            ulong expected = (ulong)Math.Round(Seconds * Fps);
            if (clip.frameCount != expected) { reason = $"Expected {expected} frames, found {clip.frameCount}."; return false; }
            return true;
        }

        public static CinematicFilmSpec Legacy(string name) => new("Cinematics/Films/" + name, 3, 1280, 720, holdFinal: false, fadeOut: .18f);
    }

    public static class CinematicStoryPlan
    {
        public static readonly CinematicFilmSpec Opening = new("Cinematics/FullStory/OpeningFull", 80, 1920, 1080);
        public static readonly CinematicFilmSpec Arrival = new("Cinematics/FullStory/LakeArrival", 15, 1920, 1080);
        public static readonly CinematicFilmSpec Hold = new("Cinematics/FullStory/LakeHold", 6, 1920, 1080, loop: true);
        public static readonly CinematicFilmSpec Peace = new("Cinematics/FullStory/LakePeace", 7, 1920, 1080);
        public static readonly CinematicFilmSpec Dominion = new("Cinematics/FullStory/LakeDominion", 7, 1920, 1080);
        public static readonly CinematicFilmSpec NorthlineDeparture = new("Cinematics/FullStory/NorthlineDeparture", 14, 1920, 1080);
        public static CinematicFilmSpec[] FullSet => new[] { Opening, Arrival, Hold, Peace, Dominion, NorthlineDeparture };
        public static CinematicFilmSpec[] LakeSet => new[] { Arrival, Hold, Peace, Dominion };
        public static bool ValidOutcome(EndingOutcome outcome) => outcome == EndingOutcome.Peace || outcome == EndingOutcome.Dominion;
    }
}
