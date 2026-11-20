using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite
{
    public enum GameState { Playing, Paused, Ending, Ended, Resetting }

    /// <summary>Survives checkpoint reloads (static). Reset by a fresh New Game.</summary>
    public static class RunState
    {
        public static bool HasCheckpoint;
        public static Vector3 CheckpointPosition;
        public static string CheckpointName = "";
        public static EntryRoute Route = EntryRoute.None;
        public static bool LabEntered;
        public static float PlayTime;
        public static int HostsPossessed;
        public static float PeakSuspicion;
        public static int Captures;
        public static int Resets;
        public static readonly HashSet<string> Flags = new();
        public static int MissionIndex;
        public static bool Has(string flag) => Flags.Contains(flag);

        public static void NewGame()
        {
            HasCheckpoint = false; CheckpointPosition = Vector3.zero; CheckpointName = "";
            Route = EntryRoute.None; LabEntered = false; PlayTime = 0; HostsPossessed = 0;
            PeakSuspicion = 0; Captures = 0; Resets = 0;
            Flags.Clear(); MissionIndex = 0;
        }
    }

    public class GameManager : MonoBehaviour
    {
        public const string MenuScene = "Bootstrap";
        public const string CityScene = "City";

        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            SetCursorLocked(true);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (State == GameState.Playing) RunState.PlayTime += Time.deltaTime;
            if (GameInput.PausePressed)
            {
                if (State == GameState.Playing) SetPaused(true);
                else if (State == GameState.Paused && !(UI.HUDController.Instance && UI.HUDController.Instance.TryClosePauseSubmenu())) SetPaused(false);
            }
        }

        public void SetPaused(bool paused)
        {
            if (paused && State != GameState.Playing) return;
            if (!paused && State != GameState.Paused) return;
            State = paused ? GameState.Paused : GameState.Playing;
            Time.timeScale = paused ? 0f : 1f;
            SetCursorLocked(!paused);
            GameEvents.RaisePause(paused);
        }

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public void SetCheckpoint(Vector3 pos, string name)
        {
            if (RunState.HasCheckpoint && RunState.CheckpointName == name) return;
            RunState.HasCheckpoint = true;
            RunState.CheckpointPosition = pos;
            RunState.CheckpointName = name;
            GameEvents.RaiseToast($"Checkpoint — {name}", Palette.Info);
        }

        public void BeginEnding() { State = GameState.Ending; }

        /// <summary>Return control after a story encounter that continues the campaign.</summary>
        public void ResumeAfterDialogue()
        {
            if (State != GameState.Ending) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            SetCursorLocked(true);
        }

        public void FinishEnding(EndingData data)
        {
            State = GameState.Ended;
            SetCursorLocked(false);
            GameEvents.RaiseEnding(data);
        }

        /// <summary>No playable amoeba left: reload the city at the last checkpoint.</summary>
        public void ResetToCheckpoint(string reason)
        {
            if (State == GameState.Resetting || State == GameState.Ended) return;
            State = GameState.Resetting;
            RunState.Resets++;
            GameEvents.RaiseToast(reason + " — returning to last checkpoint", Palette.Danger);
            StartCoroutine(ReloadAfter(2.2f));
        }

        IEnumerator ReloadAfter(float s)
        {
            yield return new WaitForSecondsRealtime(s);
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
        }

        public void RestartGame()
        {
            RunState.NewGame();
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
        }

        public void QuitToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        public static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
