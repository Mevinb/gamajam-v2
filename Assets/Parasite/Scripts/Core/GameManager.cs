using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
        public static EndingOutcome LakesideOutcome { get; private set; }
        public static bool AfterlakeStarted => Has(Missions.AfterlakeCampaign.Started);
        public static bool IslandStarted => Has(Missions.IslandCampaign.Started);
        public static bool HarborStarted => Has(Missions.HarborCampaign.Started);
        public static bool RailStarted => Has(Missions.RailCampaign.Started);
        public static Vector3 CurrentChapterSpawn => RailStarted ? Missions.RailLayout.Spawn : HarborStarted ? Missions.HarborLayout.Spawn : IslandStarted ? Missions.IslandCampaign.Spawn : AfterlakeStarted ? Missions.AfterlakeCampaign.Spawn : World.CityLayout.PlayerSpawn;

        /// <summary>The first resolved lakeside choice stays true through later chapters and reloads.</summary>
        public static bool RecordLakesideOutcome(EndingOutcome outcome)
        {
            if ((outcome != EndingOutcome.Peace && outcome != EndingOutcome.Dominion) || LakesideOutcome != EndingOutcome.Unchosen) return false;
            LakesideOutcome = outcome;
            return true;
        }

        public static bool BeginAfterlake()
        {
            if (LakesideOutcome == EndingOutcome.Unchosen || !Has(Missions.MissionFlags.Escaped) || AfterlakeStarted) return false;
            Flags.Add(Missions.AfterlakeCampaign.Started);
            MissionIndex = Missions.AfterlakeCampaign.FirstMissionIndex;
            HasCheckpoint = true;
            CheckpointPosition = Missions.AfterlakeCampaign.Spawn;
            CheckpointName = "Riverside landing";
            return true;
        }
        public static bool BeginIsland()
        {
            if (LakesideOutcome == EndingOutcome.Unchosen || !Has(Missions.MissionFlags.Escaped) || !AfterlakeStarted || !Has(Missions.AfterlakeCampaign.Departed) || IslandStarted) return false;
            Flags.Add(Missions.IslandCampaign.Started);
            MissionIndex = Missions.IslandCampaign.FirstMissionIndex;
            HasCheckpoint = true;
            CheckpointPosition = Missions.IslandCampaign.Spawn;
            CheckpointName = "Island outer jetty";
            return true;
        }
        public static bool Has(string flag) => Flags.Contains(flag);

        public static bool BeginHarbor()
        {
            if (LakesideOutcome == EndingOutcome.Unchosen || !Has(Missions.MissionFlags.Escaped) ||
                !AfterlakeStarted || !IslandStarted || !Has(Missions.IslandRoster.Departed) || HarborStarted) return false;
            Flags.Add(Missions.HarborCampaign.Started);
            MissionIndex = Missions.HarborCampaign.FirstMissionIndex;
            HasCheckpoint = true;
            CheckpointPosition = Missions.HarborLayout.Spawn;
            CheckpointName = "Harbor landing";
            return true;
        }

        public static bool BeginRail()
        {
            if (LakesideOutcome == EndingOutcome.Unchosen || !Has(Missions.MissionFlags.Escaped) || !AfterlakeStarted || !Has(Missions.AfterlakeCampaign.Departed)
                || !IslandStarted || !Has(Missions.IslandRoster.Departed) || !HarborStarted || !Has(Missions.HarborCampaign.CanalOpen)
                || !Has(Missions.HarborCampaign.ManifestComplete) || !Missions.HarborCampaign.RequiredCargoDelivered || !Has(Missions.HarborCampaign.RelayComplete) || RailStarted) return false;
            Flags.Add(Missions.RailCampaign.Started); MissionIndex = Missions.RailCampaign.FirstMissionIndex;
            HasCheckpoint = true; CheckpointPosition = Missions.RailLayout.Spawn; CheckpointName = "Northline southern approach";
            return true;
        }

        public static void NewGame()
        {
            HasCheckpoint = false; CheckpointPosition = Vector3.zero; CheckpointName = "";
            Route = EntryRoute.None; LabEntered = false; PlayTime = 0; HostsPossessed = 0;
            PeakSuspicion = 0; Captures = 0; Resets = 0;
            Flags.Clear(); MissionIndex = 0; LakesideOutcome = EndingOutcome.Unchosen;
        }
    }

    public class GameManager : MonoBehaviour
    {
        public const string MenuScene = "Bootstrap";
        public const string CityScene = "City";
        public const float FallRecoveryY = -30f;

        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;
        Missions.HarborRelay harborRelay;
        Missions.RailDeparture railDeparture;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            SetCursorLocked(true);
            PresentationShortcut.Ensure();
            // CityDirector adds this manager before baking its NavMesh and spawning markers.
            if (RunState.RailStarted)
            {
                Missions.RailDistrict.Build(); railDeparture = Missions.RailDeparture.Instance;
                if (railDeparture) railDeparture.Completed += OnRailDepartureComplete;
            }
            else if (RunState.HarborStarted)
            {
                Missions.HarborDistrict.Build();
                harborRelay = Missions.HarborRelay.Instance;
                if (harborRelay) harborRelay.Completed += OnHarborRelayComplete;
            }
            else if (RunState.IslandStarted) Missions.IslandDistrict.Build();
            else if (RunState.AfterlakeStarted) Missions.AfterlakeDistrict.Build();
        }

        void OnDestroy()
        {
            if (harborRelay) harborRelay.Completed -= OnHarborRelayComplete;
            if (railDeparture) railDeparture.Completed -= OnRailDepartureComplete;
            if (Instance == this) Instance = null;
        }

        IEnumerator Start()
        {
            if (!CampaignPersistence.SummaryPending) yield break;
            yield return null; // CityDirector and HUD must finish their normal scene construction first.
            if (Instance != this || !UI.HUDController.Instance || !Missions.MissionManager.Instance || !CampaignPersistence.ConsumePendingSummary()) yield break;
            State = GameState.Ended; Time.timeScale = 0; SetCursorLocked(false);
            GameEvents.RaiseEnding(new EndingData { route = RunState.Route, timeSeconds = RunState.PlayTime,
                hostsPossessed = RunState.HostsPossessed, peakSuspicion = RunState.PeakSuspicion,
                captures = RunState.Captures, outcome = RunState.LakesideOutcome });
        }

        void LateUpdate() => CampaignPersistence.Tick(this);
        void OnApplicationQuit() => CampaignPersistence.Flush(this);

        void Update()
        {
            // A disabled coordinator can be re-enabled after rollback; a destroyed one requires the ordinary chapter checkpoint.
            if (IsPlaying && RunState.RailStarted && !railDeparture && !RunState.Has(Missions.RailCampaign.Departed))
            { ResetToCheckpoint("Northline departure coordinator was lost"); return; }

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
            CampaignPersistence.RequestSave();
            GameEvents.RaiseToast($"Checkpoint — {name}", Palette.Info);
        }

        public void BeginEnding() { State = GameState.Ending; Time.timeScale = 0f; }

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
            State = GameState.Ending;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            UI.LakesideEpilogue.Begin(data);
        }

        /// <summary>Publish the final summary only after the player explicitly chooses their future.</summary>
        public void PublishFinalEnding(EndingData data)
        {
            if (State != GameState.Ending || (data.outcome != EndingOutcome.Peace && data.outcome != EndingOutcome.Dominion) || RunState.AfterlakeStarted) return;
            RunState.RecordLakesideOutcome(data.outcome);
            data.outcome = RunState.LakesideOutcome;
            Missions.MissionManager.SetFlag(Missions.MissionFlags.Escaped);
            State = GameState.Ended;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            GameEvents.RaiseEnding(data);
        }

        /// <summary>Continue deliberately from the resolved lake summary; checkpoint reloads keep this chapter.</summary>
        public bool ContinueAfterLake()
        {
            if (State != GameState.Ended || !RunState.BeginAfterlake()) return false;
            State = GameState.Resetting;
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
            return true;
        }

        /// <summary>One-shot continuation from the resolved ferry summary; preserve the first lake outcome.</summary>
        public bool ContinueIsland()
        {
            if (State != GameState.Ended || !Missions.AfterlakeDeparture.AllAmoebaeFree(out _) || !RunState.BeginIsland()) return false;
            State = GameState.Resetting;
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
            return true;
        }

        /// <summary>Deliberate continuation from the completed island rescue, retaining its original lake choice.</summary>
        public bool ContinueHarbor()
        {
            if (State != GameState.Ended || !Missions.AfterlakeDeparture.AllAmoebaeFree(out _) || !RunState.BeginHarbor()) return false;
            State = GameState.Resetting;
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
            return true;
        }

        /// <summary>One deliberate continuation from the completed M23 summary; never a checkpoint/timeout shortcut.</summary>
        public bool ContinueRail()
        {
            if (State != GameState.Ended || RunState.MissionIndex != 23 || !Missions.MissionManager.Instance || Missions.MissionManager.Instance.Missions.Count != 23
                || !Missions.AfterlakeDeparture.AllAmoebaeFree(out _) || !RunState.BeginRail()) return false;
            State = GameState.Resetting; Time.timeScale = 1; SceneManager.LoadScene(CityScene); return true;
        }

        void OnRailDepartureComplete() => FinishRail();
        public bool FinishRail()
        {
            if (!IsPlaying || !RunState.RailStarted || RunState.Has(Missions.RailCampaign.Departed) || !railDeparture || !railDeparture.DepartureAuthorized || !railDeparture.ReadyToDepart(out _)) return false;
            BeginEnding();
            if (Missions.RailResolution.Begin(this, railDeparture)) return true;
            railDeparture.CancelDepartureAuthorization(); ResumeAfterDialogue(); return false;
        }
        public bool PublishRailEnding(Missions.RailResolution owner)
        {
            if (!owner || !owner.Owns(this) || !owner.ReadyToPublish || State != GameState.Ending || !RunState.RailStarted || RunState.Has(Missions.RailCampaign.Departed)) return false;
            Missions.MissionManager.SetFlag(Missions.RailCampaign.Departed);
            State = GameState.Ended; Time.timeScale = 0; SetCursorLocked(false);
            GameEvents.RaiseEnding(new EndingData { route = RunState.Route, timeSeconds = RunState.PlayTime, hostsPossessed = RunState.HostsPossessed,
                peakSuspicion = RunState.PeakSuspicion, captures = RunState.Captures, outcome = RunState.LakesideOutcome });
            return true;
        }

        void OnHarborRelayComplete() => FinishHarbor();

        public bool FinishHarbor()
        {
            if (!IsPlaying || !RunState.HarborStarted || !RunState.Has(Missions.HarborCampaign.RelayComplete) ||
                !harborRelay || !harborRelay.Ready) return false;
            BeginEnding();
            StartCoroutine(HarborResolution());
            return true;
        }

        IEnumerator HarborResolution()
        {
            GameEvents.RaiseSubtitle("HARBOR RADIO", RunState.LakesideOutcome == EndingOutcome.Peace
                ? "The antidote is through. Two signals, one promise: every person keeps their own mind."
                : "Both relays are through. Every frequency carries your answer, and every borrowed body is free.");
            yield return new WaitForSecondsRealtime(4f);
            GameEvents.RaiseSubtitle("", "");
            if (State != GameState.Ending || !RunState.Has(Missions.HarborCampaign.RelayComplete)) yield break;
            State = GameState.Ended;
            Time.timeScale = 0;
            SetCursorLocked(false);
            GameEvents.RaiseEnding(new EndingData
            {
                route = RunState.Route, timeSeconds = RunState.PlayTime, hostsPossessed = RunState.HostsPossessed,
                peakSuspicion = RunState.PeakSuspicion, captures = RunState.Captures, outcome = RunState.LakesideOutcome,
            });
        }

        public void FinishIsland()
        {
            if(!Missions.IslandExtraction.ReadyToDepart(out _))return;
            BeginEnding();
            StartCoroutine(IslandDeparture());
        }

        IEnumerator IslandDeparture()
        {
            var ship=GameObject.Find("Island rescue launch");
            var pm=Possession.PossessionManager.Instance;
            if(ship&&pm)
            {
                foreach(var agent in pm.Agents)
                {
                    if(agent.Blob){agent.Blob.Controller.enabled=false;agent.Blob.transform.SetParent(ship.transform,true);}
                }
                var travelers=Missions.IslandTravelers.Instance;
                if(travelers)foreach(var traveler in travelers.Members)
                {
                    if(traveler.Host.Nav)traveler.Host.Nav.enabled=false;
                    traveler.Host.transform.SetParent(ship.transform,true);
                }
            }
            (string who,string line) cue=RunState.LakesideOutcome==EndingOutcome.Peace
                ?("RESCUE RADIO","Everyone aboard. No borrowed heart left behind.")
                :("RESCUE RADIO","Every courier answered. The next shore is listening.");
            GameEvents.RaiseSubtitle(cue.who,cue.line);
            float finishAt=Missions.IslandVoiceTiming.Window(cue.who,cue.line,2.5f);
            float age=0;
            while(age<finishAt)
            {
                float dt=Time.unscaledDeltaTime;
                float travel=Mathf.Min(dt,Mathf.Max(0,2.5f-age));age+=dt;
                if(ship)ship.transform.position+=Vector3.forward*travel*2;
                yield return null; // Hold the departed vessel after its original2.5s movement until this finite voice window ends.
            }
            GameEvents.RaiseSubtitle("", "");
            if(State!=GameState.Ending||!RunState.Has(Missions.IslandRoster.SignalMounted)||!Missions.IslandRoster.AllBoarded)yield break;
            Missions.MissionManager.SetFlag(Missions.IslandRoster.Departed);
            State=GameState.Ended;Time.timeScale=0;SetCursorLocked(false);
            GameEvents.RaiseEnding(new EndingData{route=RunState.Route,timeSeconds=RunState.PlayTime,hostsPossessed=RunState.HostsPossessed,peakSuspicion=RunState.PeakSuspicion,captures=RunState.Captures,outcome=RunState.LakesideOutcome});
        }

        public void FinishAfterlake()
        {
            if (!IsPlaying || !RunState.AfterlakeStarted || !RunState.Has(Missions.AfterlakeCampaign.ChannelOpened) ||
                RunState.Has(Missions.AfterlakeCampaign.Departed)) return;
            if (!Missions.AfterlakeDeparture.AllAmoebaeFree(out _)) return;
            Missions.MissionManager.SetFlag(Missions.AfterlakeCampaign.Departed);
            State = GameState.Ended;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            GameEvents.RaiseEnding(new EndingData
            {
                route = RunState.Route, timeSeconds = RunState.PlayTime, hostsPossessed = RunState.HostsPossessed,
                peakSuspicion = RunState.PeakSuspicion, captures = RunState.Captures, outcome = RunState.LakesideOutcome,
            });
        }

        /// <summary>Recover a real player's current body; inactive agents count, off-stage story actors do not.</summary>
        public bool TryRecoverOwnedFall(IControllable body)
        {
            if (!IsPlaying || body == null || (body is Object unityBody && !unityBody) ||
                !body.Root || body.Root.position.y >= FallRecoveryY) return false;
            var pm = Possession.PossessionManager.Instance;
            if (!pm) return false;
            foreach (var agent in pm.Agents)
            {
                if (agent == null || agent.InTransit) continue;
                bool owned = body is Possession.AmoebaController blob
                    ? agent.Host == null && agent.Blob == blob && blob.Agent == agent
                    : body is Characters.HostCharacter host && agent.Host == host && host.Possessor == agent;
                if (!owned) continue;
                EnsureSafeFallCheckpoint();
                ResetToCheckpoint("Swept beyond safe ground");
                return State == GameState.Resetting;
            }
            return false;
        }

        void EnsureSafeFallCheckpoint()
        {
            if (RunState.HasCheckpoint && NavMesh.SamplePosition(RunState.CheckpointPosition, out var checkpoint, 2f, NavMesh.AllAreas))
            {
                RunState.CheckpointPosition = checkpoint.position;
                return;
            }
            Vector3 fallback = RunState.CurrentChapterSpawn;
            if (NavMesh.SamplePosition(fallback, out var landing, 2f, NavMesh.AllAreas)) fallback = landing.position;
            RunState.HasCheckpoint = true;
            RunState.CheckpointPosition = fallback;
            RunState.CheckpointName = RunState.RailStarted ? "Northline southern approach" : RunState.HarborStarted ? "Harbor landing" : RunState.IslandStarted ? "Island outer jetty" : RunState.AfterlakeStarted ? "Riverside landing" : "Storm Drain";
        }

        /// <summary>No playable amoeba left: reload the city at the last checkpoint.</summary>
        public void ResetToCheckpoint(string reason)
        {
            if (State == GameState.Resetting || State == GameState.Ending || State == GameState.Ended) return;
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
            CampaignPersistence.BeginNewGame();
            Time.timeScale = 1f;
            SceneManager.LoadScene(CityScene);
        }

        public void QuitToMenu()
        {
            CampaignPersistence.Flush(this);
            CampaignPersistence.LeaveSession();
            Time.timeScale = 1f;
            SceneManager.LoadScene(MenuScene);
        }

        public static void QuitApp()
        {
            CampaignPersistence.Flush(Instance);
#if UNITY_EDITOR
            CinematicFilmLifetime.QuitPlayMode();
#else
            CinematicFilmLifetime.Quit(0);
#endif
        }
    }
}
