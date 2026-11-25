using System;
using UnityEngine;

namespace Parasite
{
    /// <summary>Requested graphics choices. The named preset remains the baseline for unexposed quality budgets.</summary>
    [Serializable]
    public sealed class GraphicsSettingsData : IEquatable<GraphicsSettingsData>
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public int preset = 3;
        public float renderScale = 1.25f;
        public bool vSync = true;
        public int frameLimit = -1;
        public bool shadows = true;
        public float shadowDistance = 180f;
        public bool ambientOcclusion = true;
        public int reflectionLevel = 3;
        public bool bloom = true;
        public bool filmGrain = true;
        public bool chromaticAberration = true;
        public int antiAliasing = 2;
        public int msaaSamples = 4;

        static readonly float[] Scales = { .7f, .85f, 1f, 1.25f };
        static readonly float[] Shadows = { 40f, 70f, 110f, 180f };

        public static GraphicsSettingsData ForPreset(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            return new GraphicsSettingsData
            {
                preset = level, renderScale = Scales[level], shadowDistance = Shadows[level],
                ambientOcclusion = level >= 2, reflectionLevel = level,
                filmGrain = level >= 2, chromaticAberration = level >= 2,
                antiAliasing = level >= 2 ? 2 : 1, msaaSamples = level == 3 ? 4 : 1,
            };
        }

        public GraphicsSettingsData Clone() => (GraphicsSettingsData)MemberwiseClone();

        public GraphicsSettingsData Normalized()
        {
            var copy = Clone();
            copy.version = CurrentVersion;
            copy.preset = Mathf.Clamp(copy.preset, 0, 3);
            var fallback = ForPreset(copy.preset);
            copy.renderScale = Finite(copy.renderScale) ? Mathf.Clamp(copy.renderScale, .5f, 1.5f) : fallback.renderScale;
            copy.shadowDistance = Finite(copy.shadowDistance) ? Mathf.Clamp(copy.shadowDistance, 0f, 200f) : fallback.shadowDistance;
            copy.frameLimit = ValidFrameLimit(copy.frameLimit) ? copy.frameLimit : -1;
            copy.reflectionLevel = Mathf.Clamp(copy.reflectionLevel, 0, 3);
            if (copy.antiAliasing < 0 || copy.antiAliasing > 2) copy.antiAliasing = fallback.antiAliasing;
            if (copy.msaaSamples != 1 && copy.msaaSamples != 2 && copy.msaaSamples != 4) copy.msaaSamples = fallback.msaaSamples;
            return copy;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool ValidFrameLimit(int value) => value == -1 || value == 30 || value == 60 || value == 90 || value == 120 || value == 144 || value == 240;

        public bool Equals(GraphicsSettingsData other)
            => other != null && version == other.version && preset == other.preset && renderScale.Equals(other.renderScale)
                && vSync == other.vSync && frameLimit == other.frameLimit && shadows == other.shadows
                && shadowDistance.Equals(other.shadowDistance) && ambientOcclusion == other.ambientOcclusion
                && reflectionLevel == other.reflectionLevel && bloom == other.bloom && filmGrain == other.filmGrain
                && chromaticAberration == other.chromaticAberration && antiAliasing == other.antiAliasing && msaaSamples == other.msaaSamples;
        public override bool Equals(object obj) => obj is GraphicsSettingsData other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = version;
                hash = hash * 31 + preset; hash = hash * 31 + renderScale.GetHashCode();
                hash = hash * 31 + vSync.GetHashCode(); hash = hash * 31 + frameLimit;
                hash = hash * 31 + shadows.GetHashCode(); hash = hash * 31 + shadowDistance.GetHashCode();
                hash = hash * 31 + ambientOcclusion.GetHashCode(); hash = hash * 31 + reflectionLevel;
                hash = hash * 31 + bloom.GetHashCode(); hash = hash * 31 + filmGrain.GetHashCode();
                hash = hash * 31 + chromaticAberration.GetHashCode(); hash = hash * 31 + antiAliasing;
                return hash * 31 + msaaSamples;
            }
        }
    }
}
