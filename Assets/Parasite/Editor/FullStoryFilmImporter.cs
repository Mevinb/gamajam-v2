using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Parasite.EditorTools
{
    /// <summary>Preserve approved silent full-story streams, including the Northline departure.</summary>
    public sealed class FullStoryFilmImporter : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/Parasite/Resources/Cinematics/FullStory/",System.StringComparison.Ordinal) ||
                !(assetImporter is VideoClipImporter video)) return;
            var settings=video.defaultTargetSettings;
            settings.enableTranscoding=false; video.defaultTargetSettings=settings;
            video.ClearTargetSettings("Standalone"); // Linux/Windows use the unchanged default stream.
            video.importAudio=false; // Verify sourceAudioTrackCount separately before accepting a build.
            video.sRGBClip=true;
        }

        // Final release entrypoint remains strict: all eight assets are required.
        public static void VerifyAllImported(bool reimport = false) => VerifyImported(reimport, false);

        // Explicit Visual Preview policy: only an absent optional Northline movie
        // may fall back. A present Northline asset must pass every normal gate.
        public static void VerifyVisualPreviewImported(bool reimportExistingSeven = false) => VerifyImported(reimportExistingSeven, true);

        static void VerifyImported(bool reimport, bool visualPreview)
        {
            var specs = CinematicStoryPlan.FullSet.Concat(new[]
            {
                CinematicFilmSpec.Legacy("ContainmentOpening"), CinematicFilmSpec.Legacy("LakesideEnding"),
            });
            foreach (var spec in specs)
            {
                string path = "Assets/Parasite/Resources/" + spec.ResourcePath + ".webm";
                bool optionalNorthline = spec.ResourcePath == CinematicStoryPlan.NorthlineDeparture.ResourcePath;
                if (!File.Exists(path))
                {
                    if (visualPreview && optionalNorthline)
                    {
                        Debug.Log("[FilmImport] VISUAL_PREVIEW optional NorthlineDeparture absent; physical coach departure remains authoritative");
                        continue;
                    }
                    throw new InvalidOperationException("Missing required story film: " + path);
                }
                string guid = AssetDatabase.AssetPathToGUID(path);
                // The preview reimport is deliberately scoped to the seven existing
                // movies. If Northline is present, validate it without altering it.
                if (reimport && (!visualPreview || !optionalNorthline))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var video = AssetImporter.GetAtPath(path) as VideoClipImporter;
                if (!video) throw new InvalidOperationException("Missing video importer: " + path);
                var standalone = video.GetTargetSettings("Standalone");
                if (video.defaultTargetSettings.enableTranscoding || (standalone != null && standalone.enableTranscoding) || video.importAudio)
                    throw new InvalidOperationException("Story film must disable transcoding and audio import: " + path);
                // ImportAudio=false must never conceal an invalid embedded soundtrack.
                if (video.sourceAudioTrackCount != 0)
                    throw new InvalidOperationException("Story source must contain no audio tracks: " + path);
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                if (!clip) throw new InvalidOperationException("Required story film did not import: " + path);
                if (!spec.Validate(clip, out string reason)) throw new InvalidOperationException(path + ": " + reason);
                string afterGuid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(afterGuid) || (!string.IsNullOrEmpty(guid) && guid != afterGuid))
                    throw new InvalidOperationException("Story film GUID changed: " + path);
                Debug.Log($"[FilmImport] PASS {path} guid={afterGuid} frames={clip.frameCount} sourceAudio={video.sourceAudioTrackCount} importAudio={video.importAudio} transcode={video.defaultTargetSettings.enableTranscoding}");
            }
        }

        public static void ReimportVisualPreviewSevenBatch()
        {
            try
            {
                VerifyVisualPreviewImported(true);
                Debug.Log("[FilmImport] VISUAL_PREVIEW PASS seven mandatory existing films; any present NorthlineDeparture also validated");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        public static void ReimportAndVerifyBatch()
        {
            try
            {
                VerifyAllImported(true);
                Debug.Log("[FilmImport] PASS all eight required story films, including NorthlineDeparture");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
