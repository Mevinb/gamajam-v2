using UnityEditor;

namespace Parasite.EditorTools
{
    /// <summary>Preserve the approved full-story stream and expose, rather than silently strip, unexpected audio tracks.</summary>
    public sealed class FullStoryFilmImporter : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/Parasite/Resources/Cinematics/FullStory/",System.StringComparison.Ordinal) ||
                !(assetImporter is VideoClipImporter video)) return;
            var settings=video.defaultTargetSettings;
            settings.enableTranscoding=false; video.defaultTargetSettings=settings;
            video.importAudio=true; // Valid movies are silent. Invalid embedded tracks remain visible to the runtime rejection gate.
            video.sRGBClip=true;
        }
    }
}
