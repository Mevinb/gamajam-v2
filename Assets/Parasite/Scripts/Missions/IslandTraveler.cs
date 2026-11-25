using System;
using Parasite.Characters;
using Parasite.Possession;
using Parasite.Stealth;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>Peace followers retain agency; Dominion couriers require a physical controlled handoff and release.</summary>
    public sealed class IslandTraveler:MonoBehaviour,IInteractable
    {
        public string StableId{get;private set;}
        public HostCharacter Host{get;private set;}
        public bool Delivered=>RunState.Has(IslandRoster.Delivered(StableId));
        public bool Boarded=>RunState.Has(IslandRoster.Boarded(StableId));
        public Vector3 InteractPoint=>transform.position+Vector3.up*1.2f;
        public float InteractRange=>2.3f;
        public bool IsAvailable=>isActiveAndEnabled&&!Boarded&&(peace||!Delivered);
        int index,cover;
        bool peace,seeded,releasedAtGate,returning;
        AmoebaAgent leader;
        HostCharacter leaderHost;
        float exposure,scan;
        int leaderPossessionGeneration,leaderCaptureGeneration;
        int markedPossessionGeneration,markedCaptureGeneration;
        public Vector3 Waypoint=>peace&&leader!=null?Destination:!peace&&seeded?IslandRoster.Handoffs[index]:transform.position;
        public Vector3 Destination=>RunState.Has(IslandRoster.SignalMounted)?IslandRoster.Board[index]:peace?IslandRoster.Clinic[index]:IslandRoster.Handoffs[index];
        public void Initialize(string id,int slot,HostCharacter host,bool peaceful)
        {
            StableId=id;index=slot;Host=host;peace=peaceful;cover=IslandRoster.SavedCover(id);
            if(Host.Brain)Host.Brain.RoutineOverride=Drive;
            Interactables.Register(this);
        }
        public string GetPrompt(in InteractContext ctx)=>peace?(returning?"Regroup ":"Ask to follow — ")+IslandRoster.Names[index]:(ctx.host==Host?"Mark this courier's handoff [E]":"Courier "+IslandRoster.Names[index]+" — possess, mark and release at its lane");
        public bool CanInteract(in InteractContext ctx,out string reason)
        {
            reason=null;
            if(!GameManager.Instance||!GameManager.Instance.IsPlaying||!RunState.Has(IslandCampaign.InnerRefuge)||!IsAvailable){reason="Not ready to travel.";return false;}
            var pm=PossessionManager.Instance;
            if(!pm||ctx.agent==null||!pm.Agents.Contains(ctx.agent)||ctx.agent.InTransit||ctx.agent.CapturedPendingEject||!ctx.host||ctx.agent.Host!=ctx.host||ctx.host.Possessor!=ctx.agent||ctx.host.State!=HostState.Possessed){reason="Use an uncaptured human host.";return false;}
            if(peace)
            {
                if(Delivered&&!RunState.Has(IslandRoster.SignalMounted)){reason="Safe at the clinic. Mount the rescue signal before regrouping for the launch.";return false;}
                if(ctx.host.Profession!=Profession.Civilian&&ctx.host.Profession!=Profession.Scientist){reason="A civilian or scientist can reassure this survivor.";return false;}
            }
            else if(ctx.host!=Host){reason="Possess this courier first, then mark the route.";return false;}
            return true;
        }
        public void Interact(in InteractContext ctx)
        {
            if(!CanInteract(ctx,out var why)){GameEvents.RaiseToast(why,Palette.Warning);return;}
            if(peace)
            {
                leader=ctx.agent;leaderHost=ctx.host;returning=false;exposure=0;
                leaderPossessionGeneration=ctx.host.PossessionGeneration;
                leaderCaptureGeneration=ctx.host.CaptureGeneration;
                GameEvents.RaiseToast(IslandRoster.Names[index]+" follows this host, even while it is inactive. Keep close; after capture or release, regroup at visited cover.",Palette.Info);
            }
            else
            {
                seeded=true;releasedAtGate=false;
                markedPossessionGeneration=Host.PossessionGeneration;
                markedCaptureGeneration=Host.CaptureGeneration;
                GameEvents.RaiseToast("Handoff marked. Walk this courier to its lane, then release it [Q]; it must recover and continue on foot.",Palette.Info);
            }
        }
        void Update()
        {
            if(!Host||!GameManager.Instance||!GameManager.Instance.IsPlaying)return;
            Host.ProtectedMind=peace||Delivered;
            // Check transition history before any idle/clinic/boarded early return or AI notice delay.
            if(peace)ValidateLeaderOwnership();else ValidateCourierAuthorization();
            if(Boarded)return;
            if(!peace)
            {
                if(seeded&&Host.Possessor==null&&Host.State==HostState.Unconscious&&Vector3.Distance(transform.position,IslandRoster.Handoffs[index])<1.4f)releasedAtGate=true;
                return;
            }
            if(leader==null){exposure=0;return;}
            scan-=Time.deltaTime;
            if(scan>0||Delivered&&!RunState.Has(IslandRoster.SignalMounted))return;
            scan=.15f;
            bool seen=false;
            foreach(var guard in HostCharacter.All)
            {
                if(!guard||guard.Profession!=Profession.Guard||!guard.IsAwakeNpc||guard.Brain is GuardAI ai&&ai.GState==GuardState.Stunned||Vector3.Distance(guard.transform.position,transform.position)>20)continue;
                if(SuspicionSystem.Instance&&SuspicionSystem.Instance.CanSee(guard,Host.ChestPosition,Host.transform,20)){seen=true;break;}
            }
            exposure=seen?exposure+.15f:Mathf.Max(0,exposure-.3f);
            if(exposure>2&&!returning)
            {
                returning=true;leader=null;leaderHost=null;
                GameEvents.RaiseToast(IslandRoster.Names[index]+" turns back to the last cover. Draw the patrol away, then regroup.",Palette.Warning);
            }
        }
        bool LeaderOwnershipValid()
        {
            var pm=PossessionManager.Instance;
            return leader!=null&&pm&&pm.Agents.Contains(leader)&&!leader.InTransit&&!leader.CapturedPendingEject&&leaderHost
                &&leader.Host==leaderHost&&leaderHost.Possessor==leader&&leaderHost.State==HostState.Possessed
                &&leaderHost.PossessionGeneration==leaderPossessionGeneration&&leaderHost.CaptureGeneration==leaderCaptureGeneration;
        }
        void ValidateLeaderOwnership()
        {
            if(leader==null&&!leaderHost||LeaderOwnershipValid())return;
            // Ownership loss is latched: reacquiring this exact body is a new agreement and needs E again.
            leader=null;leaderHost=null;returning=true;exposure=0;
            if(!Boarded)GameEvents.RaiseToast(IslandRoster.Names[index]+" lost its leader. Return to visited cover and regroup [E].",Palette.Warning);
        }
        void ValidateCourierAuthorization()
        {
            if(!seeded)return;
            if(!Host||Host.PossessionGeneration!=markedPossessionGeneration||Host.CaptureGeneration!=markedCaptureGeneration||Host.State==HostState.Captured)
            {seeded=false;releasedAtGate=false;}
        }
        void Drive(float delta)
        {
            if(!Host||!Host.Brain)return;
            // HostBrain's notice/flee path may defer Drive; also validate here before any autonomous commit.
            if(peace)ValidateLeaderOwnership();else ValidateCourierAuthorization();
            if(Boarded){Host.Brain.ScriptStop();return;}
            if(peace&&Delivered&&!RunState.Has(IslandRoster.SignalMounted))
            {
                if(Vector3.Distance(transform.position,IslandRoster.Clinic[index])>.8f)Host.Brain.ScriptMove(IslandRoster.Clinic[index],1.6f);else Host.Brain.ScriptStop();
                return;
            }
            if(!peace)
            {
                if(Delivered&&RunState.Has(IslandRoster.SignalMounted))MoveAndCommit(Destination,true);
                else if(releasedAtGate&&Host.State==HostState.Normal)MoveAndCommit(IslandRoster.Handoffs[index]+Vector3.forward*2,false);
                else Host.Brain.ScriptStop();
                return;
            }
            if(!LeaderOwnershipValid()||returning)
            {
                var safe=Delivered?IslandRoster.Clinic[index]:cover>=0?IslandRoster.Cover[cover]+Vector3.right*((index-1)*1.5f):IslandRoster.Holding[index];
                if(Vector3.Distance(transform.position,safe)>1)Host.Brain.ScriptMove(safe,1.6f);else Host.Brain.ScriptStop();
                return;
            }
            if(Vector3.Distance(leaderHost.transform.position,Destination)<3.5f){MoveAndCommit(Destination,RunState.Has(IslandRoster.SignalMounted));return;}
            if(Vector3.Distance(transform.position,leaderHost.transform.position)>11){Host.Brain.ScriptStop();return;}
            for(int i=cover+1;i<IslandRoster.Cover.Length;i++)if(Vector3.Distance(transform.position,IslandRoster.Cover[i])<1.8f)
            {cover=i;MissionManager.SetFlag(IslandRoster.CoverFlag(StableId,i));}
            Vector3 follow=leaderHost.transform.position-leaderHost.transform.forward*1.5f+leaderHost.transform.right*((index-1)*.8f);
            if(Vector3.Distance(transform.position,follow)>1.2f)Host.Brain.ScriptMove(follow,1.9f);else Host.Brain.ScriptStop();
        }
        void MoveAndCommit(Vector3 destination,bool boarding)
        {
            // A boarded passenger must park at its berth, beyond the entrance, rather than stopping in the aisle.
            // Keep the stricter inset check even when this smaller arrival radius fits entirely within it.
            float tolerance=boarding?IslandRoster.BoardArrivalTolerance:.8f;
            if(Vector3.Distance(transform.position,destination)>tolerance||boarding&&!IslandRoster.DeckBounds.Contains(transform.position))
            {Host.Brain.ScriptMove(destination,1.8f);return;}
            Host.Brain.ScriptStop();
            if(!boarding)Host.ProtectedMind=true;
            MissionManager.SetFlag(boarding?IslandRoster.Boarded(StableId):IslandRoster.Delivered(StableId));
        }
        void OnDestroy()
        {
            Interactables.Unregister(this);
            if(Host&&Host.Brain&&Host.Brain.RoutineOverride==(Action<float>)Drive)Host.Brain.RoutineOverride=null;
        }
    }
}
