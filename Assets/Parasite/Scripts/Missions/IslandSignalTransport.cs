using Parasite.Characters;
using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>One physical signal item; carrier references are transient and never checkpoint state.</summary>
    public sealed class IslandSignalTransport:MonoBehaviour
    {
        public static IslandSignalTransport Instance{get;private set;}
        public bool Held=>carrier!=null&&PossessionManager.Instance&&PossessionManager.Instance.Agents.Contains(carrier)&&carrierHost&&carrier.Host==carrierHost&&carrierHost.Possessor==carrier&&carrierHost.State==HostState.Possessed&&!carrier.InTransit;
        public Transform CaseTransform=>flare?flare.transform:null;
        AmoebaAgent carrier;
        HostCharacter carrierHost;
        GameObject flare;
        readonly Vector3 cache=new(18,.97f,18),mount=new(32,1.15f,37);
        void Awake()=>Instance=this;
        void Start()
        {
            var art=GetComponent<IslandArt>();
            flare=art.Model("IslandSignalFlare",RunState.Has(IslandRoster.SignalMounted)?mount:cache,0,false);
        }
        public bool HeldBy(in InteractContext ctx)=>Held&&carrier==ctx.agent&&carrierHost==ctx.host;
        public bool PickUp(in InteractContext ctx)
        {
            if(!ReadyHuman(ctx)||!RunState.Has(IslandRoster.Ready)||RunState.Has(IslandRoster.SignalMounted)||Held||!InReach(ctx,cache))return false;
            carrier=ctx.agent;carrierHost=ctx.host;
            MissionManager.SetFlag(IslandRoster.SignalFound);
            if(GameManager.Instance)GameManager.Instance.SetCheckpoint(IslandCampaign.At(18,.15f,15),"Island signal cache approach");
            return true;
        }
        public bool Mount(in InteractContext ctx)
        {
            if(!ReadyHuman(ctx)||!Held||carrier!=ctx.agent||carrierHost!=ctx.host||RunState.Has(IslandRoster.SignalMounted)||!InReach(ctx,mount))return false;
            carrier=null;carrierHost=null;
            MissionManager.SetFlag(IslandRoster.SignalMounted);
            if(GameManager.Instance)GameManager.Instance.SetCheckpoint(IslandCampaign.At(34,.15f,35),"Island rescue jetty");
            PlaceWorld(mount);return true;
        }
        void Update()
        {
            var gm=GameManager.Instance;
            if(!gm||!flare)return;
            if(gm.State==GameState.Resetting){carrier=null;carrierHost=null;PlaceWorld(RunState.Has(IslandRoster.SignalMounted)?mount:cache);return;}
            if(!gm.IsPlaying)return;
            if(carrier!=null&&!Held){carrier=null;carrierHost=null;PlaceWorld(cache);GameEvents.RaiseToast("Signal case returned to its safe cache after the carrier was lost.",Palette.Warning);}
            if(!Held)return;
            var animator=carrierHost.Rig?carrierHost.Rig.Animator:null;
            var hand=animator&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.RightHand):null;
            flare.transform.SetParent(hand?hand:carrierHost.transform,false);
            flare.transform.localPosition=hand?new Vector3(0,-.08f,0):new Vector3(.35f,.85f,.2f);
            flare.transform.localRotation=Quaternion.identity;
        }
        void PlaceWorld(Vector3 local){if(!flare)return;flare.transform.SetParent(transform,false);flare.transform.localPosition=local;flare.transform.localRotation=Quaternion.identity;}
        bool InReach(in InteractContext ctx,Vector3 point)
        {
            Vector3 d=ctx.agent.Position-transform.TransformPoint(point);
            return Mathf.Abs(d.y)<2.6f&&new Vector2(d.x,d.z).magnitude<=2.3f;
        }
        public static bool ReadyHuman(in InteractContext ctx)
        {
            var pm=PossessionManager.Instance;
            return GameManager.Instance&&GameManager.Instance.IsPlaying&&pm&&ctx.agent!=null&&pm.Agents.Contains(ctx.agent)&&!ctx.agent.InTransit&&!ctx.agent.CapturedPendingEject&&ctx.host&&ctx.agent.Host==ctx.host&&ctx.host.Possessor==ctx.agent&&ctx.host.State==HostState.Possessed;
        }
        void OnDestroy(){if(flare)Destroy(flare);if(Instance==this)Instance=null;}
    }
}
