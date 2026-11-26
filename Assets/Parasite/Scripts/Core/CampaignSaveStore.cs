using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Parasite
{
    /// <summary>Atomic, versioned campaign slot with a previous valid backup. Reading never rewrites corrupt/future files.</summary>
    public static class CampaignSaveStore
    {
        public const string FileName = "campaign-v1.json";
        const int MaximumBytes = 512 * 1024;
        [Serializable] sealed class Envelope
        {
            public string format;
            public int version;
            public CampaignSaveData checkpoint;
            public string checksum;
        }
        static string Checksum(CampaignSaveData data)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)))).Replace("-", "").ToLowerInvariant();
        }
        public static string Serialize(CampaignSaveData data)
        {
            if (data == null || !data.IsValid(out _)) throw new ArgumentException("A valid resumable checkpoint is required.", nameof(data));
            return JsonUtility.ToJson(new Envelope { format = "PARASITE_CHECKPOINT", version = CampaignSaveData.CurrentSchema, checkpoint = data, checksum = Checksum(data) });
        }
        public static bool TryDeserialize(string json, out CampaignSaveData data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumBytes) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope == null || envelope.format != "PARASITE_CHECKPOINT" || envelope.version != CampaignSaveData.CurrentSchema
                    || envelope.checkpoint == null || !envelope.checkpoint.IsValid(out _)
                    || !string.Equals(envelope.checksum, Checksum(envelope.checkpoint), StringComparison.Ordinal)) return false;
                data = envelope.checkpoint; return true;
            }
            catch (ArgumentException) { return false; }
        }
        enum ReadKind { Missing, Valid, Invalid, Future }
        static ReadKind Read(string path, out CampaignSaveData data)
        {
            data = null;
            if (!File.Exists(path)) return ReadKind.Missing;
            if (new FileInfo(path).Length > MaximumBytes) return ReadKind.Invalid;
            string json = File.ReadAllText(path);
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope != null && envelope.format == "PARASITE_CHECKPOINT" && (envelope.version > CampaignSaveData.CurrentSchema
                    || envelope.checkpoint != null && envelope.checkpoint.schema > CampaignSaveData.CurrentSchema)) return ReadKind.Future;
            }
            catch (ArgumentException) { return ReadKind.Invalid; }
            return TryDeserialize(json, out data) ? ReadKind.Valid : ReadKind.Invalid;
        }
        public static bool TryLoad(string directory, out CampaignSaveData data, out bool recoveredBackup, out string error)
        {
            data = null; recoveredBackup = false; error = null;
            try
            {
                string path = Path.Combine(directory, FileName);
                var primary = Read(path, out data);
                if (primary == ReadKind.Valid) return true;
                // A newer game's slot is not corruption: never roll it back to an older backup and autosave over it.
                if (primary == ReadKind.Future) { error = "This checkpoint belongs to a newer game version."; return false; }
                if (Read(path + ".bak", out data) == ReadKind.Valid) { recoveredBackup = true; return true; }
                error = "No readable saved checkpoint."; return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { error = e.Message; return false; }
        }
        public static bool TrySave(string directory, CampaignSaveData data, out string error)
        {
            error = null; string temporary = null;
            try
            {
                string json = Serialize(data);
                string path = Path.Combine(directory, FileName);
                if (Read(path, out _) == ReadKind.Future) { error = "This checkpoint belongs to a newer game version."; return false; }
                Directory.CreateDirectory(directory);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                // Never rotate a corrupt/future primary over the last known-good backup.
                if (File.Exists(path))
                {
                    var primary = Read(path, out _);
                    if (primary == ReadKind.Future) { error = "This checkpoint belongs to a newer game version."; return false; }
                    if (primary == ReadKind.Valid) File.Replace(temporary, path, path + ".bak");
                    else File.Replace(temporary, path, null);
                }
                else File.Move(temporary, path);
                temporary = null; return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { error = e.Message; return false; }
            finally
            {
                if (temporary != null) try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
