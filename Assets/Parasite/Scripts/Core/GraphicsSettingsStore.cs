using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Parasite
{
    /// <summary>Versioned graphics preferences; loading never rewrites corrupt or newer data, audio or input preferences.</summary>
    public static class GraphicsSettingsStore
    {
        public const string PreferenceKey = "parasite.graphics.settings.v1";
        public const string LegacyPresetKey = "parasite.graphics";
        [Serializable] sealed class Envelope
        {
            public int version;
            public GraphicsSettingsData settings;
            public string checksum;
        }

        public static GraphicsSettingsData Load(int defaultPreset = 2)
        {
            int legacy = Mathf.Clamp(PlayerPrefs.GetInt(LegacyPresetKey, defaultPreset), 0, 3);
            return TryDeserialize(PlayerPrefs.GetString(PreferenceKey, ""), out var settings)
                ? settings : GraphicsSettingsData.ForPreset(legacy);
        }

        public static bool TryDeserialize(string json, out GraphicsSettingsData settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16384) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope == null || envelope.version != GraphicsSettingsData.CurrentVersion || envelope.settings == null
                    || envelope.settings.version != GraphicsSettingsData.CurrentVersion) return false;
                var normalized = envelope.settings.Normalized();
                if (!string.Equals(envelope.checksum, Checksum(normalized), StringComparison.Ordinal)) return false;
                settings = normalized;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        public static string Serialize(GraphicsSettingsData settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var normalized = settings.Normalized();
            return JsonUtility.ToJson(new Envelope { version = GraphicsSettingsData.CurrentVersion, settings = normalized, checksum = Checksum(normalized) });
        }

        static string Checksum(GraphicsSettingsData settings)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(settings)))).Replace("-", "").ToLowerInvariant();
        }

        public static void Save(GraphicsSettingsData settings)
        {
            var normalized = settings?.Normalized() ?? throw new ArgumentNullException(nameof(settings));
            PlayerPrefs.SetString(PreferenceKey, Serialize(normalized));
            PlayerPrefs.SetInt(LegacyPresetKey, normalized.preset);
            PlayerPrefs.Save();
        }
    }
}
