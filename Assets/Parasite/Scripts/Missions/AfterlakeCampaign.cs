using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>The riverside chapter begins only after the player resolves the lake and asks to continue.</summary>
    public static class AfterlakeCampaign
    {
        public const int FirstMissionIndex = 14;
        public const string Started = "afterlake_started", Signal = "shore_signal", Floodgate = "shore_floodgate";
        public const string WaterAnalysed = "shore_water_analysed", CorridorOpen = "shore_corridor_open";
        public const string ChannelOpened = "shore_channel_open", Departed = "shore_departed";
        public const string CorridorControl = "shore_dispatch", RadioControl = "shore_radio_power";
        public static bool Peace => RunState.LakesideOutcome == EndingOutcome.Peace;
        public static readonly Vector3 Spawn = new(194, 0.15f, -23);
        public static readonly Vector3 Receiver = new(199, 0.15f, -16), FloodgatePanel = new(215, 0.15f, -19);
        public static readonly Vector3 Analysis = new(231, 0.15f, 4), Dispatch = new(242, 0.15f, 10);
        public static readonly Vector3 Records = new(232, 0.15f, 12), RadioPower = new(263, 0.15f, -2);
        public static readonly Vector3 Radio = new(263, 0.15f, 9), Ferry = new(270, 0.15f, -25.8f);

        static bool Held(string control) => CooperativeConsole.Find(control) && CooperativeConsole.Find(control).Held;

        public static void AppendMissions(List<Mission> missions)
        {
            missions.Add(new Mission
            {
                title = "AFTER THE STILL WATER",
                briefing = Peace ? "You promised a quiet life. Then Petrov's distress call reached the shore: Vale's runoff is poisoning a riverside shelter. Help without claiming another life."
                    : "The lake was a beginning. Petrov's distress call reveals a riverside shelter and a communications mast: the first foothold for a wider mind.",
                objectives =
                {
                    new() { text = "Borrow a host and answer the emergency receiver [hold E]", flag = Signal, waypoints = new[] { Receiver }, waypointLabel = "DISTRESS RECEIVER" },
                    new() { text = "As MAINTENANCE, isolate the contaminated floodgate (6 s); avoid pursuit", flag = Floodgate, waypoints = new[] { FloodgatePanel }, waypointLabel = "FLOODGATE BYPASS" },
                },
            });
            missions.Add(new Mission
            {
                title = Peace ? "BORROWED HEARTS" : "A WILLING NETWORK",
                briefing = Peace ? "Petrov needs proof of the poison and a safe route for the shelter residents. A guard must keep the cordon open while a scientist anonymises the evacuation records."
                    : "An isolated settlement needs a cure. Secure Petrov's analysis, then claim the shelter records while your other guard body holds the cordon open.",
                objectives =
                {
                    new() { text = "As a SCIENTIST, analyse the water in the field clinic (7 s)", flag = WaterAnalysed, waypoints = new[] { Analysis }, waypointLabel = "FIELD LAB" },
                    new() { text = "Split again [V]. As a GUARD, hold shelter dispatch [E]; leave that host here and [Tab]", doneWhen = () => RunState.Has(CorridorOpen) || Held(CorridorControl), waypoints = new[] { Dispatch }, waypointLabel = "SHELTER DISPATCH" },
                    new() { text = Peace ? "As your OTHER SCIENTIST host, erase identifying records and open the evacuation corridor (8 s)" : "As your OTHER SCIENTIST host, acquire shelter identities and open the recruitment corridor (8 s)", flag = CorridorOpen, waypoints = new[] { Records }, waypointLabel = Peace ? "PROTECT RESIDENTS" : "SHELTER REGISTRY" },
                },
            });
            missions.Add(new Mission
            {
                title = Peace ? "THE OPEN CHANNEL" : "THE FIRST MANY",
                briefing = Peace ? "Get the antidote formula to independent responders. Maintenance must keep the mast powered while a different civilian or scientist sends an anonymous call. Leave every host behind before boarding the ferry."
                    : "Turn the mast into your first relay. Maintenance holds the amplifier while a different guard or scientist seeds the network. Leave every host behind and carry the new signal across the water.",
                objectives =
                {
                    new() { text = "As MAINTENANCE, hold the radio power stabiliser [E]; leave that body and [Tab]", doneWhen = () => RunState.Has(ChannelOpened) || Held(RadioControl), waypoints = new[] { RadioPower }, waypointLabel = "RADIO POWER" },
                    new() { text = Peace ? "As your OTHER CIVILIAN or SCIENTIST, transmit the antidote and evacuation call (10 s); lose pursuit" : "As your OTHER GUARD or SCIENTIST, transmit the neural relay seed (10 s); lose pursuit", flag = ChannelOpened, waypoints = new[] { Radio }, waypointLabel = "SHORE TRANSMITTER" },
                    new() { text = "Release ALL hosts with [Tab]/[Q], then board the ferry intake as a FREE amoeba [E]", flag = Departed, waypoints = new[] { Ferry }, waypointLabel = "FERRY INTAKE" },
                },
            });
        }

        public static void AnnounceArrival()
        {
            MissionManager.Say(("DR. PETROV", Peace
                ? "I know you wanted to disappear. But the river carries Vale's waste into this shelter. I need your help, not another experiment."
                : "I tracked the contamination to this shelter. Its radio can reach the whole river. What you do with that reach is your choice."));
        }

        public static void OnFlag(string flag)
        {
            if (flag != Signal && flag != Floodgate && flag != WaterAnalysed && flag != CorridorOpen && flag != ChannelOpened) return;
            var pm = Possession.PossessionManager.Instance;
            if (GameManager.Instance && pm && pm.ActiveBody != null)
                GameManager.Instance.SetCheckpoint(pm.ActiveBody.Root.position, "Riverside · " + flag.Replace("shore_", "").Replace('_', ' '));
            if (flag == Floodgate) MissionManager.Say(("DR. PETROV", "The intake is closed. Bring the sample to the field clinic before the next tide."));
            if (flag == CorridorOpen) MissionManager.Say(("SPECIMEN 7", Peace ? "They leave with their names and their lives intact. Now get the formula onto the radio." : "A shelter today. A network tomorrow. Keep the mast alive."));
            if (flag == ChannelOpened) MissionManager.Say(("SHORE RADIO", Peace ? "Responders acknowledge. Antidote received. Ferry holds at the south pier." : "Relay seed received. Other minds are answering. Ferry holds at the south pier."));
        }
    }
}
