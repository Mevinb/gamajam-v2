using System.Collections.Generic;
using Parasite.Characters;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Creates named story people only after the entry mission, restoring durable IDs rather than duplicated actors.</summary>
    public sealed class IslandTravelers : MonoBehaviour
    {
        public static IslandTravelers Instance{get;private set;}
        readonly List<IslandTraveler> members=new();
        bool spawned;
        public IReadOnlyList<IslandTraveler> Members=>members;
        void Awake()=>Instance=this;
        void Update()
        {
            if(!GameManager.Instance||!GameManager.Instance.IsPlaying||!RunState.Has(IslandCampaign.InnerRefuge))return;
            if(!spawned)SpawnRoster();
            if(IslandRoster.AllDelivered&&!RunState.Has(IslandRoster.Ready))
            {
                MissionManager.SetFlag(IslandRoster.Ready);
                GameManager.Instance.SetCheckpoint(IslandCampaign.At(-20,.15f,-1),"Island clinic approach");
                MissionManager.Say(("ISLAND RADIO",RunState.LakesideOutcome==EndingOutcome.Peace
                    ?"All three reached the clinic. The rescue crew needs a mounted signal and everyone at the northern launch."
                    :"Three couriers carry the relay. Mount the signal, then let the handoff routes converge at the northern launch."));
            }
        }
        void SpawnRoster()
        {
            spawned=true;
            bool peace=RunState.LakesideOutcome==EndingOutcome.Peace;
            for(int i=0;i<IslandRoster.Ids.Length;i++)
            {
                string id=IslandRoster.Ids[i];
                int cover=IslandRoster.SavedCover(id);
                Vector3 position=RunState.Has(IslandRoster.Boarded(id))?IslandRoster.Board[i]
                    :RunState.Has(IslandRoster.Delivered(id))?(peace?IslandRoster.Clinic[i]:IslandRoster.Handoffs[i])
                    :cover>=0?IslandRoster.Cover[cover]+Vector3.right*((i-1)*1.5f):IslandRoster.Holding[i];
                var host=MissionSpawner.Spawn(IslandRoster.Names[i]+(peace?" · island survivor":" · island courier"),Profession.Civilian,position,180,1911+i,NpcBehaviour.Stationary);
                host.ProtectedMind=peace||RunState.Has(IslandRoster.Delivered(id));
                host.ReportsPlayerViolations=false;
                var traveler=host.gameObject.AddComponent<IslandTraveler>();traveler.Initialize(id,i,host,peace);members.Add(traveler);
            }
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
