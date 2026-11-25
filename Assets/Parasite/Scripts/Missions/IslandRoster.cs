using System.Linq;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Stable person IDs and safe authored poses; no runtime host reference is checkpoint state.</summary>
    public static class IslandRoster
    {
        public const string Ready="island_roster_ready",SignalFound="island_signal_found",SignalMounted="island_signal_mounted",Departed="island_departed";
        public static readonly string[] Ids={"riley","morgan","avery"};
        public static readonly string[] Names={"Riley","Morgan","Avery"};
        public static readonly Vector3[] Holding={IslandCampaign.At(11,.15f,-8),IslandCampaign.At(14,.15f,-6),IslandCampaign.At(17,.15f,-8)};
        public static readonly Vector3[] Clinic={IslandCampaign.At(-23,.15f,7),IslandCampaign.At(-20,.15f,7),IslandCampaign.At(-17,.15f,7)};
        public static readonly Vector3[] Handoffs={IslandCampaign.At(17,.15f,8),IslandCampaign.At(27,.15f,13),IslandCampaign.At(34,.15f,22)};
        public static readonly Vector3[] Board={IslandCampaign.At(32,.55f,44.5f),IslandCampaign.At(32,.55f,46.0f),IslandCampaign.At(36,.55f,44.5f)};
        public static readonly Vector3[] Cover={IslandCampaign.At(14,.15f,-4),IslandCampaign.At(2,.15f,0),IslandCampaign.At(-6,.15f,0),IslandCampaign.At(-20,.15f,1)};
        // Final parking must clear the entrance; default human navigation stops .25m from its destination.
        public const float BoardArrivalTolerance = .35f;
        public static Bounds DeckBounds => new(IslandCampaign.At(34, .875f, 45.35f), new Vector3(5.3f, .95f, 4));
        public static string Delivered(string id)=>"island_delivered_"+id;
        public static string Boarded(string id)=>"island_boarded_"+id;
        public static string CoverFlag(string id,int index)=>"island_cover_"+id+"_"+index;
        public static bool AllDelivered=>Ids.All(id=>RunState.Has(Delivered(id)));
        public static bool AllBoarded=>Ids.All(id=>RunState.Has(Boarded(id)));
        public static int SavedCover(string id)
        {
            for(int i=Cover.Length-1;i>=0;i--)if(RunState.Has(CoverFlag(id,i)))return i;
            return -1;
        }
    }
}
