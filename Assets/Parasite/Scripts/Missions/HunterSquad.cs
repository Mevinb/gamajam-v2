using System.Collections.Generic;
using Parasite.Characters;
using Parasite.FX;
using Parasite.Stealth;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// HUNTER PROTOCOL: after Vale's reveal, deploys a Protocol Hound team (guards carrying <see cref="BioScanner"/>s)
    /// into the lab plus a duty officer in the security office. Once the lockdown breaker is pulled the scanners go
    /// dark and the hunters walk out of the building. Completed: never redeployed after a checkpoint reload.
    /// </summary>
    public class HunterSquad : MonoBehaviour
    {
        public static HunterSquad Instance { get; private set; }
        public string requiredFlag = MissionFlags.Confronted, doneFlag = MissionFlags.HuntersDown;
        public string[] hunterNames = { "Hunter Kade", "Hunter Sol" };
        public int[] hunterSeeds = { 301, 302 };
        public PatrolRoute[] hunterRoutes = new PatrolRoute[0];
        public Vector3 entryPoint, exitPoint;
        public string officerName = "Officer Ines Duarte";
        public Vector3 officerPost;
        public float officerYaw = 180f;
        public int officerSeed = 311;

        public readonly List<HostCharacter> Hunters = new();
        public HostCharacter Officer { get; private set; }
        public bool Deployed { get; private set; }
        public bool StoodDown { get; private set; }
        public bool Active => Deployed && !StoodDown;

        static readonly (string who, string line)[] DeployLines =
        {
            ("DR. VALE (PA)", "Protocol Hound is live. Hunter team, scanners on. Sweep every corridor."),
            ("DR. VALE (PA)", "Specimen 7 is wearing my staff. Any body that reads positive, bring it down."),
        };
        static readonly (string who, string line)[] StandDownLines =
        {
            ("SECURITY RADIO", "All-clear on Protocol Hound. Hunter team, stand down and leave the building."),
            ("DR. VALE (PA)", "All-clear? I gave no all-clear! …Fine. Let it come to me."),
        };

        void Awake() { Instance = this; BioScanTracker.Reset(); }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm && !gm.IsPlaying) return;
            BioScanTracker.Tick(Time.deltaTime);
            Hunters.RemoveAll(h => !h);
            if (!Deployed && RunState.Has(requiredFlag) && !RunState.Has(doneFlag)) Deploy();
            if (Deployed && !StoodDown && RunState.Has(doneFlag)) StandDown();
        }

        void Deploy()
        {
            Deployed = true;
            for (int i = 0; i < hunterNames.Length; i++)
            {
                var route = i < hunterRoutes.Length ? hunterRoutes[i] : null;
                Vector3 p = entryPoint + Vector3.right * ((i - (hunterNames.Length - 1) * 0.5f) * 1.2f);
                var h = MissionSpawner.Spawn(hunterNames[i], Profession.Guard, p, 0f, i < hunterSeeds.Length ? hunterSeeds[i] : 300 + i,
                    route ? NpcBehaviour.Patrol : NpcBehaviour.Stationary, route);
                h.gameObject.AddComponent<BioScanner>().Init(h);
                Hunters.Add(h);
            }
            Officer = MissionSpawner.Find(officerName);
            if (!Officer) Officer = MissionSpawner.Spawn(officerName, Profession.Guard, officerPost, officerYaw, officerSeed);
            Sfx.Ui(SfxId.AlarmSiren, 0.7f);
            MissionManager.Say(DeployLines);
            GameEvents.RaiseToast("PROTOCOL HOUND: hunters with bio-scanners sweep the lab. They read POSSESSED bodies — even one you left parked.", Palette.Danger);
        }

        void StandDown()
        {
            StoodDown = true;
            foreach (var h in Hunters)
            {
                if (!h) continue;
                var scanner = h.GetComponent<BioScanner>();
                if (scanner) scanner.SetActive(false);
                if (h.Brain) h.Brain.RoutineOverride = LeaveRoutine(h);
            }
            MissionManager.Say(StandDownLines);
        }

        System.Action<float> LeaveRoutine(HostCharacter h) => dt =>
        {
            if (!h || !h.Brain) return;
            h.Brain.ScriptMove(exitPoint, h.Profile.walkSpeed * 1.2f);
            Vector3 d = h.transform.position - exitPoint; d.y = 0f;
            if (h.IsAwakeNpc && (d.sqrMagnitude < 1.5f || (h.Brain.ScriptNavReady && h.Brain.ScriptArrived(0.9f) && d.sqrMagnitude < 9f)))
                Destroy(h.gameObject);
        };

        public BioScanner ScannerOf(HostCharacter h) => h ? h.GetComponent<BioScanner>() : null;
    }
}
