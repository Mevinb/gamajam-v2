using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Parasite
{
    /// <summary>One explicit user session owns autosaving. Scene/test fixtures cannot silently activate the real slot.</summary>
    public static class CampaignPersistence
    {
        static string directoryOverride, fingerprint;
        static bool active, dirty, pendingSummary, reportedFailure;
        static float nextCheck, nextPeriodic;
        static readonly string[] AutomationFlags = { "-autotest", "-autotestFilter", "-shortcuttest", "-benchmark", "-runTests", "-cinematicTestOut", "-graphicsSettingsTestOut" };
        static readonly bool automated = Environment.GetCommandLineArgs().Any(a => AutomationFlags.Contains(a));
        public static bool Enabled => directoryOverride != null || (!Application.isEditor && !Application.isBatchMode && !automated);
        public static bool SessionActive => active && Enabled;
        public static bool SummaryPending => pendingSummary;
        public static string LastError { get; private set; }
        static string DirectoryPath => directoryOverride ?? Application.persistentDataPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            active = dirty = pendingSummary = reportedFailure = false;
            fingerprint = null; nextCheck = nextPeriodic = 0; LastError = null;
        }
        public static void BeginNewGame()
        {
            ResetSession(); RunState.NewGame(); active = Enabled; dirty = active;
        }
        public static bool TryGetSavedCheckpoint(out CampaignSaveData data)
        {
            data = null;
            return Enabled && CampaignSaveStore.TryLoad(DirectoryPath, out data, out _, out _);
        }
        public static bool TryResume(out string error)
        {
            error = null;
            if (!Enabled) { error = "Campaign saving is disabled for this automated session."; return false; }
            if (!CampaignSaveStore.TryLoad(DirectoryPath, out var data, out _, out error) || !data.TryRestore(out error)) return false;
            ResetSession(); active = true; dirty = true; pendingSummary = data.summary;
            OpeningCinematic.CancelPendingNewGame();
            return true;
        }
        public static bool ConsumePendingSummary()
        {
            if (!pendingSummary || !SessionActive) return false;
            pendingSummary = false; dirty = true; return true;
        }
        public static void RequestSave() { if (SessionActive) dirty = true; }
        public static void LeaveSession() { ResetSession(); }

        static bool CanCapture(GameManager manager) => SessionActive && manager && !pendingSummary
            && RunState.HasCheckpoint && (manager.State == GameState.Playing || manager.State == GameState.Paused || manager.State == GameState.Ended)
            && !(OpeningCinematic.Instance && OpeningCinematic.Instance.Running);

        public static void Tick(GameManager manager)
        {
            if (!CanCapture(manager)) return;
            float now = Time.unscaledTime;
            if (!dirty && now < nextCheck) return;
            nextCheck = now + 1;
            var progress = CampaignSaveData.FromRunState(manager.State == GameState.Ended);
            // Check durable progress once a second, including direct flag removals. Stats are refreshed at least every30s.
            progress.playTime = progress.peakSuspicion = 0; progress.hostsPossessed = progress.captures = progress.resets = 0;
            string key = JsonUtility.ToJson(progress);
            if (dirty || key != fingerprint || now >= nextPeriodic)
            {
                if (Flush(manager)) fingerprint = key;
                nextPeriodic = now + 30; dirty = false;
            }
        }
        public static bool Flush(GameManager manager)
        {
            if (!CanCapture(manager)) return false; // Keep the last stable save during protected travel/dialogue endings.
            bool saved = CampaignSaveStore.TrySave(DirectoryPath, CampaignSaveData.FromRunState(manager.State == GameState.Ended), out var error);
            LastError = error;
            if (saved) { reportedFailure = false; dirty = false; return true; }
            if (!reportedFailure)
            {
                reportedFailure = true;
                GameEvents.RaiseToast("Checkpoint could not be saved. Your current session is still active.", Palette.Warning);
            }
            return false;
        }

        /// <summary>Explicit isolated persistence for tests; dispose before deleting the temporary directory.</summary>
        public static IDisposable UseTestDirectory(string directory) => new TestScope(directory);
        sealed class TestScope : IDisposable
        {
            readonly string previousDirectory, previousFingerprint, previousError;
            readonly bool previousActive, previousDirty, previousSummary, previousFailure;
            readonly float previousCheck, previousPeriodic;
            bool disposed;
            public TestScope(string directory)
            {
                previousDirectory = directoryOverride; previousFingerprint = fingerprint; previousError = LastError;
                previousActive = active; previousDirty = dirty; previousSummary = pendingSummary; previousFailure = reportedFailure;
                previousCheck = nextCheck; previousPeriodic = nextPeriodic;
                directoryOverride = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory))); ResetSession();
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                directoryOverride = previousDirectory; fingerprint = previousFingerprint; LastError = previousError;
                active = previousActive; dirty = previousDirty; pendingSummary = previousSummary; reportedFailure = previousFailure;
                nextCheck = previousCheck; nextPeriodic = previousPeriodic;
            }
        }
    }
}
