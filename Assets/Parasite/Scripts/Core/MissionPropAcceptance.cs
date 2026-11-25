using System;
using System.Collections;
using System.Linq;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Parasite.Testing
{
    /// <summary>Shared real-component hold regressions: original owner, capture, authorization and pause semantics.</summary>
    public static class MissionPropAcceptance
    {
        static readonly string[] Cases = { "capture", "swap", "release", "authorization", "prerequisite", "range", "resetting", "inactive", "pause", "within tolerance" };
        public static IEnumerator RunAll()
        {
            foreach (string kind in Cases) yield return RunCase(kind);
        }

        public static IEnumerator RunCase(string kind)
        {
            yield return AutoTest.LoadCity();
            var gm = GameManager.Instance;
            var pm = PossessionManager.Instance;
            var hosts = HostCharacter.All.Where(h => h && h.CanBePossessed && h.Profession == Profession.Scientist).Take(2).ToArray();
            Check(hosts.Length == 2, "Two real scientist resources are available for the hold fixture.");
            var host = hosts[0]; var other = hosts[1];
            pm.ForcePossessImmediate(host);
            Vector3 origin = World.CityLayout.PlayerSpawn;
            Place(host, origin); Place(other, origin + Vector3.right * 1.5f);
            yield return null; yield return null;
            Calm();
            const string ready = "hold_acceptance_ready", complete = "hold_acceptance_complete";
            RunState.Flags.Add(ready);
            var propObject = new GameObject("Hold ownership acceptance");
            propObject.transform.position = origin;
            var prop = propObject.AddComponent<MissionProp>();
            prop.flag = complete; prop.displayName = "Ownership test control"; prop.holdSeconds = 2f;
            prop.requiredFlag = ready; prop.requiredPermission = AccessPermission.StaffTerminal;
            if (kind == "within tolerance") Place(host, origin + Vector3.forward * 2.2f);
            var agent = pm.Active;
            try
            {
                prop.Interact(Context(agent));
                Check(prop.Busy, "A valid owned scientist starts the real hold timer.");
                yield return new WaitForSeconds(0.1f);
                switch (kind)
                {
                    case "capture":
                        host.Capture();
                        var guard = HostCharacter.All.Select(h => h ? h.Brain as GuardAI : null).First(g => g);
                        pm.OnHostCaptured(host, guard);
                        Check(host.State == HostState.Captured && agent.CapturedPendingEject, "The actual capture path leaves the host attached until its3s eject timer.");
                        break;
                    case "swap": pm.ForcePossessImmediate(other); break;
                    case "release": Check(pm.LeaveHost(), "Release the original host."); break;
                    case "authorization": host.Profession = Profession.Guard; break;
                    case "prerequisite": RunState.Flags.Remove(ready); break;
                    case "range": Place(host, origin + Vector3.forward * 4f); break;
                    case "resetting": gm.ResetToCheckpoint("Hold cancellation fixture"); break;
                    case "inactive":
                        Check(pm.TrySplit() && pm.SwitchActive(), "The same working host becomes inactive, not unpossessed.");
                        Check(agent.Host == host && host.State == HostState.Possessed, "The original owner and host are intact after switching selection.");
                        break;
                    case "pause":
                        float progress = prop.Progress01;
                        gm.SetPaused(true);
                        yield return new WaitForSecondsRealtime(0.2f);
                        Check(prop.Busy && Mathf.Abs(prop.Progress01 - progress) < 0.0001f && !RunState.Has(complete), "Pause preserves a valid hold without advancing its timer.");
                        gm.SetPaused(false);
                        break;
                    case "within tolerance": Place(host, origin + Vector3.forward * 2.8f); break;
                    default: throw new InvalidOperationException("Unknown hold fixture: " + kind);
                }
                bool shouldFinish = kind == "inactive" || kind == "pause" || kind == "within tolerance";
                if (shouldFinish)
                {
                    float deadline = Time.realtimeSinceStartup + 4;
                    while (!RunState.Has(complete) && Time.realtimeSinceStartup < deadline) { Calm(); yield return null; }
                    Check(RunState.Has(complete) && !prop.Busy, "The authorized original body completes under " + kind + ".");
                    if (kind == "within tolerance")
                    {
                        Vector3 offset = host.transform.position - prop.InteractPoint; offset.y = 0;
                        Check(offset.sqrMagnitude > prop.range * prop.range,
                            "The actual host stayed beyond planar start reach while retaining the existing hold tolerance.");
                    }
                }
                else
                {
                    yield return null; yield return null;
                    if (kind == "range") Check((host.transform.position - prop.InteractPoint).sqrMagnitude > (prop.range + 1.2f) * (prop.range + 1.2f),
                        "The actual host remained outside hold tolerance on clear alley footing.");
                    Check(!prop.Busy && !RunState.Has(complete), kind + " immediately cancels without committing the objective.");
                    if (kind == "capture")
                    {
                        yield return new WaitForSeconds(2.1f);
                        Check(agent.Host == host && host.State == HostState.Captured, "The cancelled hold remains below the3s auto-eject window.");
                        Check(!RunState.Has(complete), "Capture cannot finish a short hold before automatic ejection.");
                    }
                }
                Debug.Log("[MissionPropHold] PASS " + kind + " with real ownership/state/timer semantics");
            }
            finally
            {
                UnityEngine.Object.Destroy(propObject);
                Time.timeScale = 1; GameInput.Simulated = false;
                SceneManager.LoadScene(GameManager.MenuScene);
            }
            yield return null;
        }

        static InteractContext Context(AmoebaAgent agent) => new()
        {
            agent = agent, host = agent.Host, permissions = agent.Host ? agent.Host.Permissions : AccessPermission.None, position = agent.Position,
        };
        static void Place(HostCharacter host, Vector3 position)
        {
            if (host.Nav && host.Nav.enabled) host.Nav.Warp(position);
            else
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
