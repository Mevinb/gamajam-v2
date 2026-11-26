using System;
using System.Collections.Generic;
using System.Linq;
using Parasite.Missions;
using UnityEngine;

namespace Parasite
{
    /// <summary>Durable checkpoint state only. Scene actors, ownership, holds and moving hardware are rebuilt normally.</summary>
    [Serializable]
    public sealed class CampaignSaveData
    {
        public const int CurrentSchema = 1;
        public int schema = CurrentSchema;
        public bool hasCheckpoint;
        public Vector3 checkpointPosition;
        public string checkpointName;
        public EntryRoute route;
        public EndingOutcome lakesideOutcome;
        public bool labEntered;
        public float playTime, peakSuspicion;
        public int hostsPossessed, captures, resets, missionIndex;
        public string[] flags;
        public bool summary;

        public static CampaignSaveData FromRunState(bool summary = false) => new()
        {
            hasCheckpoint = RunState.HasCheckpoint, checkpointPosition = RunState.CheckpointPosition,
            checkpointName = RunState.CheckpointName, route = RunState.Route, lakesideOutcome = RunState.LakesideOutcome,
            labEntered = RunState.LabEntered, playTime = RunState.PlayTime, peakSuspicion = RunState.PeakSuspicion,
            hostsPossessed = RunState.HostsPossessed, captures = RunState.Captures, resets = RunState.Resets,
            missionIndex = RunState.MissionIndex, flags = RunState.Flags.OrderBy(f => f, StringComparer.Ordinal).ToArray(), summary = summary,
        };

        public bool IsValid(out string reason)
        {
            reason = null;
            bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
            if (schema != CurrentSchema) { reason = "Unsupported checkpoint version."; return false; }
            if (!hasCheckpoint || string.IsNullOrWhiteSpace(checkpointName) || checkpointName.Length > 160
                || !Finite(checkpointPosition.x) || !Finite(checkpointPosition.y) || !Finite(checkpointPosition.z)
                || Mathf.Abs(checkpointPosition.x) > 100000 || Mathf.Abs(checkpointPosition.y) > 100000 || Mathf.Abs(checkpointPosition.z) > 100000)
            { reason = "Invalid checkpoint location."; return false; }
            if (!Enum.IsDefined(typeof(EntryRoute), route) || !Enum.IsDefined(typeof(EndingOutcome), lakesideOutcome)
                || !Finite(playTime) || playTime < 0 || !Finite(peakSuspicion) || peakSuspicion < 0
                || hostsPossessed < 0 || captures < 0 || resets < 0)
            { reason = "Invalid campaign statistics."; return false; }
            if (flags == null || flags.Length > 2048 || flags.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 160)
                || new HashSet<string>(flags, StringComparer.Ordinal).Count != flags.Length)
            { reason = "Invalid campaign flags."; return false; }
            var earned = new HashSet<string>(flags, StringComparer.Ordinal);
            bool Has(string f) => earned.Contains(f);
            bool shore = Has(AfterlakeCampaign.Started), island = Has(IslandCampaign.Started), harbor = Has(HarborCampaign.Started), rail = Has(RailCampaign.Started);
            if ((!shore && flags.Any(f => f.StartsWith("shore_", StringComparison.Ordinal)))
                || (!island && flags.Any(f => f.StartsWith("island_", StringComparison.Ordinal)))
                || (!harbor && flags.Any(f => f.StartsWith("harbor_", StringComparison.Ordinal)))
                || (!rail && flags.Any(f => f.StartsWith("northline_", StringComparison.Ordinal))))
            { reason = "A task belongs to a chapter that has not started."; return false; }
            if ((shore && (lakesideOutcome == EndingOutcome.Unchosen || !Has(MissionFlags.Escaped)))
                || (island && (!shore || !Has(AfterlakeCampaign.Departed)))
                || (harbor && (!island || !Has(IslandRoster.Departed)))
                || (rail && (!harbor || !Has(HarborCampaign.CanalOpen) || !Has(HarborCampaign.ManifestComplete)
                    || !Has(HarborCampaign.RelayComplete) || HarborCampaign.RequiredCargoIdsFor(lakesideOutcome).Any(id => !Has(HarborCampaign.DeliveredFlag(id))))))
            { reason = "A chapter is missing its earned predecessor."; return false; }
            if ((lakesideOutcome != EndingOutcome.Unchosen) != Has(MissionFlags.Escaped)
                || (Has(AfterlakeCampaign.Departed) && !Has(AfterlakeCampaign.ChannelOpened))
                || (Has(IslandRoster.Ready) && IslandRoster.Ids.Any(id => !Has(IslandRoster.Delivered(id))))
                || (Has(IslandRoster.SignalMounted) && (!Has(IslandRoster.Ready) || !Has(IslandRoster.SignalFound)))
                || IslandRoster.Ids.Any(id => Has(IslandRoster.Boarded(id)) && (!Has(IslandRoster.Delivered(id)) || !Has(IslandRoster.SignalMounted)))
                || (Has(IslandRoster.Departed) && (!Has(IslandRoster.SignalMounted) || IslandRoster.Ids.Any(id => !Has(IslandRoster.Boarded(id)))))
                || (Has(HarborCampaign.RelayComplete) && (!Has(HarborCampaign.CanalOpen) || !Has(HarborCampaign.ManifestComplete)
                    || HarborCampaign.RequiredCargoIdsFor(lakesideOutcome).Any(id => !Has(HarborCampaign.DeliveredFlag(id)))))
                || (Has(RailCampaign.Validated) && (!rail || !Has(RailCampaign.CrossingPassed)))
                || (Has(RailCampaign.FreightArrived) && !Has(RailCampaign.Validated))
                || (Has(RailCampaign.FreightArrived) && (Has(RailCampaign.PointA) != (lakesideOutcome != EndingOutcome.Peace)
                    || Has(RailCampaign.PointB) != (lakesideOutcome == EndingOutcome.Peace)))
                || (Has(RailCampaign.SignalReleased) && !Has(RailCampaign.FreightArrived))
                || (Has(RailCampaign.Departed) && !Has(RailCampaign.SignalReleased)))
            { reason = "A completed task is missing its prerequisites."; return false; }
            int first = rail ? 23 : harbor ? 20 : island ? 17 : shore ? 14 : 0;
            int count = rail ? 26 : harbor ? 23 : island ? 20 : shore ? 17 : 14;
            bool completed = rail ? Has(RailCampaign.Departed) : harbor ? Has(HarborCampaign.RelayComplete)
                : island ? Has(IslandRoster.Departed) : shore ? Has(AfterlakeCampaign.Departed)
                : Has(MissionFlags.Escaped) && lakesideOutcome != EndingOutcome.Unchosen;
            if (missionIndex < first || missionIndex > count || completed != (missionIndex == count) || summary != completed)
            { reason = "The checkpoint cannot resume this mission phase."; return false; }
            if (rail && ((missionIndex >= 24 && !Has(RailCampaign.CrossingPassed)) || (missionIndex >= 25 && !Has(RailCampaign.FreightArrived))))
            { reason = "Northline progress is missing earned objectives."; return false; }
            return true;
        }

        public bool TryRestore(out string reason)
        {
            if (!IsValid(out reason)) return false;
            RunState.NewGame();
            RunState.HasCheckpoint = hasCheckpoint; RunState.CheckpointPosition = checkpointPosition; RunState.CheckpointName = checkpointName;
            RunState.Route = route; RunState.LabEntered = labEntered; RunState.PlayTime = playTime; RunState.PeakSuspicion = peakSuspicion;
            RunState.HostsPossessed = hostsPossessed; RunState.Captures = captures; RunState.Resets = resets; RunState.MissionIndex = missionIndex;
            foreach (string flag in flags) RunState.Flags.Add(flag);
            if (lakesideOutcome != EndingOutcome.Unchosen) RunState.RecordLakesideOutcome(lakesideOutcome);
            return true;
        }
    }
}
