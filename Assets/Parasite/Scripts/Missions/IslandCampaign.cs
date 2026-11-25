using System.Collections.Generic;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Three playable Far Shore missions: entry, branch-specific moving people and physical rescue assembly.</summary>
    public static class IslandCampaign
    {
        public const int FirstMissionIndex = 17;
        public const string Started = "island_started", ArrivalSeen = "island_arrival_seen";
        public const string CulvertCrossed = "island_culvert_crossed", ServiceOpen = "island_service_open", InnerRefuge = "island_inner_refuge";
        public static readonly Vector3 Origin = new(620, 0, -220);
        public static Vector3 At(float x, float y, float z) => Origin + new Vector3(x, y, z);
        public static readonly Vector3 Spawn = At(-36, .15f, -38);
        public static readonly Vector3 CulvertEntry = At(-26, .15f, -30), CulvertExit = At(-26, .15f, -16.2f);
        public static readonly Vector3 CulvertRetry = At(-26, .15f, -29.6f);
        public static readonly Vector3 Refuge = At(-18, .15f, -8), ServiceLatch = At(-9.4f, .15f, -21.2f);

        public static void AppendMissions(List<Mission> missions)
        {
            missions.Add(new Mission
            {
                title = "UNDER THE WATERLINE",
                briefing = RunState.LakesideOutcome == EndingOutcome.Peace
                    ? "Across the river, quarantine fences still divide the island. Find the inner refuge; the survivors must remain their own people."
                    : "The ferry reached another shore. Cross the quarantine boundary unseen, then discover who carries the island's routes.",
                completeWhen = () => RunState.Has(InnerRefuge),
                objectives =
                {
                    new() { text = "A · As a FREE amoeba, cross the low tidal culvert; watch its warning lamps and shelter on a visited dry ledge",
                        flag = CulvertCrossed, optional = true, waypoints = new[] { CulvertEntry }, waypointLabel = "TIDAL CULVERT",
                        extra = () => TidalCulvert.Instance ? "  (" + TidalCulvert.Instance.StatusLine + ")" : "" },
                    new() { text = "B · Or borrow MAINTENANCE on the public jetty and release the physical service latch [E]",
                        flag = ServiceOpen, optional = true, waypoints = new[] { ServiceLatch }, waypointLabel = "SERVICE LATCH" },
                    new() { text = "Reach the inner refuge", flag = InnerRefuge, waypointLabel = "INNER REFUGE",
                        liveWaypoint = () => RunState.Has(CulvertCrossed) || RunState.Has(ServiceOpen) ? Refuge : (Vector3?)null },
                },
            });
            AppendTravelerMission(missions);
            AppendSignalMission(missions);
        }

        static void AppendTravelerMission(List<Mission> missions)
        {
            bool peace=RunState.LakesideOutcome==EndingOutcome.Peace;
            var mission=new Mission
            {
                title=peace?"THE QUARANTINE WALK":"EVERY VOICE",
                briefing=peace?"Three people are trapped in the island holding court. Lead them with a civilian or scientist. Use real cover and civilian [F] distractions, or split [V] and use a second guard body to dispatch patrols. Their minds remain their own."
                    :"Three couriers carry the island routes. Take one at a time, mark its handoff [E], steer it to the lane, then release [Q] so it can recover and continue. Two amoebae remain your limit.",
                completeWhen=()=>IslandRoster.AllDelivered,
            };
            for(int i=0;i<IslandRoster.Ids.Length;i++)
            {
                int slot=i;string id=IslandRoster.Ids[i];
                mission.objectives.Add(new Objective
                {
                    text=peace?"Lead "+IslandRoster.Names[i]+" to the clinic; regroup at cover if exposed":"Mark, steer and release courier "+IslandRoster.Names[i]+" at its physical handoff lane",
                    flag=IslandRoster.Delivered(id),waypointLabel=IslandRoster.Names[i].ToUpperInvariant(),
                    liveWaypoint=()=>
                    {
                        var director=IslandTravelers.Instance;
                        if(director!=null)foreach(var traveler in director.Members)if(traveler&&traveler.StableId==id)return traveler.Delivered?(Vector3?)null:traveler.Waypoint;
                        return IslandRoster.Holding[slot];
                    },
                });
            }
            missions.Add(mission);
        }

        static void AppendSignalMission(List<Mission> missions)
        {
            missions.Add(new Mission
            {
                title=RunState.LakesideOutcome==EndingOutcome.Peace?"A LIGHT IN THE FOG":"A SIGNAL BEYOND THE ISLAND",
                briefing=RunState.LakesideOutcome==EndingOutcome.Peace
                    ?"Carry the signal in the same host and mount it at the jetty. Lead survivors via the clinic front exit, west wall and north shore. Regroup after patrols turn away; bring everyone aboard."
                    :"Recover the physical signal, carry it in the same host to the northern mount, then bring every person and surviving free amoeba aboard the docked launch. Sailing begins only after everyone is safe.",
                objectives=
                {
                    new(){text="As a human, recover the physical signal case; a lost carrier returns it to the safe cache",doneWhen=()=>RunState.Has(IslandRoster.SignalMounted)||(IslandSignalTransport.Instance&&IslandSignalTransport.Instance.Held),waypoints=new[]{At(18,.15f,18)},waypointLabel="SIGNAL CACHE"},
                    new(){text="Carry the case in the same body and mount it at the northern jetty [E]",flag=IslandRoster.SignalMounted,waypoints=new[]{At(32,.15f,37)},waypointLabel="SIGNAL MOUNT"},
                    new(){text=RunState.LakesideOutcome==EndingOutcome.Peace?"Lead all three aboard; try the clinic front exit, west wall, then north shore":"Let all three released couriers follow the mounted signal onto the rescue deck",doneWhen=()=>IslandRoster.AllBoarded,waypoints=new[]{At(34,.55f,44.5f)},waypointLabel="RESCUE DECK"},
                    new(){text="Release ALL hosts, bring every free amoeba aboard with [Tab], then signal departure [E]",flag=IslandRoster.Departed,waypoints=new[]{At(34,.55f,42.8f)},waypointLabel="RESCUE LAUNCH"},
                },
            });
        }

        public static void OnFlag(string flag)
        {
            if (flag != InnerRefuge) return;
            if (GameManager.Instance) GameManager.Instance.SetCheckpoint(Refuge, "Island inner refuge");
            MissionManager.Say(("ISLAND RADIO", RunState.LakesideOutcome == EndingOutcome.Peace
                ? "You made it to the dry refuge. No one here belongs to Vale. Let us keep it that way."
                : "The island has more than one route out. From this refuge, every route can become a connection."));
        }
    }
}
