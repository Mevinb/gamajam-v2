using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Parasite.Testing
{
    /// <summary>Shared Editor/built-player river-fall regression, driven by real controller gravity and scene reloads.</summary>
    public static class FallRecoveryAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        static readonly Vector3 BerthDrop = new(270, 0.65f, -24.9f);

        public static IEnumerator RunAll()
        {
            foreach (string kind in new[] { "active blob", "active host", "inactive blob", "inactive host", "simultaneous", "invalid checkpoint" })
                yield return RunCase(kind);
            yield return Exclusions();
        }

        public static IEnumerator RunCase(string kind)
        {
            yield return LoadRiverside();
            var gm = GameManager.Instance;
            if (kind == "inactive blob" || kind == "inactive host" || kind == "simultaneous")
            {
                Check(PM.TrySplit(), "A second amoeba is available for the fall fixture.");
                if (kind == "inactive host") Check(PM.SwitchActive(), "Select the second host fixture.");
            }
            if (kind == "active host" || kind == "inactive host")
            {
                var host = HostCharacter.All.FirstOrDefault(h => h && h.CanBePossessed && h.Profession == Profession.Scientist && h.transform.position.x > 180);
                Check(host, "A real riverside scientist exists.");
                PM.ForcePossessImmediate(host);
                Check(PM.Active.Host == host, "The fall body is actually possessed.");
                if (kind == "inactive host") Check(PM.SwitchActive(), "Leave the second possessed body inactive.");
            }
            yield return null; yield return null;
            Calm();
            gm.SetCheckpoint(AfterlakeCampaign.Spawn, "River fall verification");
            if (kind == "invalid checkpoint")
            {
                RunState.CheckpointPosition = new Vector3(350, 0.15f, -85);
                Check(!NavMesh.SamplePosition(RunState.CheckpointPosition, out _, 2, NavMesh.AllAreas), "The invalid checkpoint is outside navigable footing.");
            }
            var flags = new HashSet<string>(RunState.Flags);
            int mission = RunState.MissionIndex, resets = RunState.Resets, captures = RunState.Captures;
            int possessions = RunState.HostsPossessed;
            var outcome = RunState.LakesideOutcome;
            var body = kind.StartsWith("inactive", StringComparison.Ordinal)
                ? PM.Agents.First(agent => agent != PM.Active).Body : PM.ActiveBody;
            Check(!Physics.Raycast(BerthDrop + Vector3.up * 2, Vector3.down, 4, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore),
                "The test drop is over real collision-free berth water, not a teleport below the threshold.");
            Place(body, BerthDrop);
            if (kind == "simultaneous") Place(PM.Agents.First(agent => agent != PM.Active).Body, BerthDrop + Vector3.right * 2);
            float minimumY = BerthDrop.y;
            float deadline = Time.realtimeSinceStartup + 8;
            while (RunState.Resets == resets && Time.realtimeSinceStartup < deadline)
            {
                minimumY = Mathf.Min(minimumY, body.Root.position.y);
                yield return null;
            }
            Check(minimumY < -1, "The real " + kind + " controller fell below the river surface.");
            Check(RunState.Resets == resets + 1, "The fall requests exactly one checkpoint reset.");
            deadline = Time.realtimeSinceStartup + 20;
            while ((!GameManager.Instance || GameManager.Instance == gm || !GameManager.Instance.IsPlaying ||
                !CityDirector.Instance || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.Instance && GameManager.Instance != gm && GameManager.Instance.IsPlaying, "The checkpoint reload returns gameplay.");
            yield return new WaitForSeconds(0.4f);
            Check(RunState.Resets == resets + 1, "No repeated reset follows the fall or reload.");
            Check(flags.SetEquals(RunState.Flags), "Mission flags survive recovery exactly.");
            Check(RunState.MissionIndex == mission && RunState.LakesideOutcome == outcome, "Mission index and the immutable lake outcome survive recovery.");
            Check(RunState.Captures == captures && RunState.HostsPossessed == possessions, "A fall is not miscounted as capture or another possession.");
            Check(PM.Active != null && PM.Active.IsFree && PM.Agents.Count == 1, "Checkpoint recovery creates the normal free-amoeba state.");
            Check(Vector3.Distance(PM.Active.Position, AfterlakeCampaign.Spawn) < 1.5f, "The recovered amoeba lands at valid riverside checkpoint footing.");
            Check(NavMesh.SamplePosition(PM.Active.Position, out _, 0.8f, NavMesh.AllAreas), "Recovered position is navigable.");
            Check(Physics.Raycast(PM.Active.Position + Vector3.up, Vector3.down, 2, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore), "A physical floor supports the recovered amoeba.");
            Debug.Log("[FallRecovery] PASS " + kind + " river fall → one reset → valid checkpoint, progress preserved");
        }

        public static IEnumerator Exclusions()
        {
            yield return LoadRiverside();
            var gm = GameManager.Instance;
            int resets = RunState.Resets;
            Vector3 safe = PM.Active.Position;
            var body = PM.ActiveBody;
            gm.SetPaused(true);
            Place(body, new Vector3(270, -1000, -25));
            Check(!gm.TryRecoverOwnedFall(body), "Paused gameplay cannot start recovery.");
            yield return new WaitForSecondsRealtime(0.1f);
            Check(gm.State == GameState.Paused && RunState.Resets == resets && body.Root.position.y < -999, "Pause leaves off-stage placement and reset count unchanged.");
            gm.SetPaused(false); Place(body, safe);
            gm.BeginEnding();
            Place(body, new Vector3(270, -1000, -25));
            Check(!gm.TryRecoverOwnedFall(body), "Protected story state cannot start recovery.");
            yield return new WaitForSecondsRealtime(0.1f);
            Check(gm.State == GameState.Ending && RunState.Resets == resets && body.Root.position.y < -999, "Protected story actors stay frozen and do not reset.");
            gm.ResumeAfterDialogue(); Place(body, safe);
            var orphan = AmoebaController.Create(new Vector3(310, -1000, -80), new AmoebaAgent { Id = 99 });
            var npc = HostCharacter.All.First(h => h && h.Possessor == null && h.transform.position.x > 180);
            if (npc.Nav) npc.Nav.enabled = false;
            npc.Controller.enabled = false; npc.transform.position = new Vector3(320, -1000, -80);
            Check(!gm.TryRecoverOwnedFall(orphan) && !gm.TryRecoverOwnedFall(npc), "Unregistered stand-ins and unpossessed NPCs cannot reset the campaign.");
            yield return null; yield return null; yield return null;
            Check(RunState.Resets == resets && gm.IsPlaying && orphan.transform.position.y < -999, "Off-stage stand-ins remain off-stage without a world reset or Y teleport.");
            UnityEngine.Object.Destroy(orphan.gameObject);
            // Preserve the historical local correction for isolated fixtures without a game manager.
            SceneManager.LoadScene(GameManager.MenuScene); Time.timeScale = 1; yield return null;
            Check(!GameManager.Instance, "The title scene has no gameplay manager.");
            var standalone = AmoebaController.Create(new Vector3(0, -1000, 0), new AmoebaAgent { Id = 98 });
            yield return null;
            Check(standalone.transform.position.y > 1, "No-manager blob fixtures retain their local fallback.");
            UnityEngine.Object.Destroy(standalone.gameObject);
            var hostObject = new GameObject("Standalone fall fixture") { layer = Layers.Characters };
            hostObject.AddComponent<CharacterController>();
            var host = hostObject.AddComponent<HostCharacter>();
            host.Init("Standalone scientist", Profession.Scientist, false, 901);
            host.BeginPossession(new AmoebaAgent { Id = 97, Host = host });
            Place(host, new Vector3(0, -1000, 0));
            yield return null;
            Check(host.transform.position.y > 1, "No-manager possessed-host fixtures retain their local fallback.");
            UnityEngine.Object.Destroy(hostObject);
            Check(RunState.Resets == resets, "Excluded actors and standalone fixtures never increment campaign resets.");
            Debug.Log("[FallRecovery] PASS paused/story/unowned exclusions and no-manager fixture fallbacks");
        }

        static IEnumerator LoadRiverside()
        {
            yield return AutoTest.LoadCity();
            RunState.RecordLakesideOutcome(EndingOutcome.Peace);
            RunState.Flags.Add(MissionFlags.Escaped);
            Check(RunState.BeginAfterlake(), "Fall fixture starts after the lake.");
            SceneManager.LoadScene(GameManager.CityScene); yield return null;
            float deadline = Time.realtimeSinceStartup + 20;
            while ((!CityDirector.Instance || !CityDirector.Instance.Ready) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(CityDirector.Instance && CityDirector.Instance.Ready, "Riverside fall fixture booted.");
            GameInput.Simulated = true;
            GameInput.SimMove = Vector2.zero;
            GameInput.SimSprint = GameInput.SimJump = GameInput.SimInteract = GameInput.SimLeave = GameInput.SimSplit = GameInput.SimSwitch = GameInput.SimAbility = false;
            MissionManager.SetFlag(AfterlakeCampaign.Signal);
            MissionManager.SetFlag(AfterlakeCampaign.Floodgate);
            yield return new WaitForSeconds(0.2f);
            Calm();
        }

        static void Place(IControllable body, Vector3 position)
        {
            if (body is AmoebaController blob) blob.Place(position);
            else if (body is HostCharacter host)
            {
                bool enabled = host.Controller.enabled;
                host.Controller.enabled = false; host.transform.position = position; host.Controller.enabled = enabled;
            }
            Physics.SyncTransforms();
        }
        static void Calm() { if (SuspicionSystem.Instance) { SuspicionSystem.Instance.SetValue(0); SuspicionSystem.Instance.OnSuspectGone(); } }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
