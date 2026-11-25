using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Parasite.UI;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Parasite.Testing
{
    /// <summary>First-slice proof: deliberate ferry handoff, both physical entry routes and chapter-safe recovery.</summary>
    public static class IslandEntryAcceptance
    {
        static PossessionManager PM => PossessionManager.Instance;
        public static IEnumerator RunAll()
        {
            yield return Route(EndingOutcome.Peace,true);
            yield return Route(EndingOutcome.Dominion,true);
            yield return Route(EndingOutcome.Peace,false);
            yield return Route(EndingOutcome.Dominion,false);
            yield return TideRetryAndHumanClearance();
            yield return IslandCameraAcceptance.Run();
            yield return IslandVisualAcceptance.Run();
        }

        public static IEnumerator Route(EndingOutcome outcome,bool culvert)
        {
            yield return EnterFromFerry(outcome);
            if(culvert)
            {
                bool reached=false;
                yield return AutoTest.WalkTo(IslandCampaign.CulvertEntry,20,r=>reached=r,.5f);
                Check(reached,"Walk from the actual landing to the culvert approach.");
                yield return WalkDirect(IslandCampaign.CulvertRetry,4,.18f);
                float deadline=Time.realtimeSinceStartup+15;
                while((TidalCulvert.Instance.Phase!=TidePhase.Open || TideRules.Remaining(GetTideAge())<4.5f) && Time.realtimeSinceStartup<deadline)yield return null;
                Check(TidalCulvert.Instance.Phase==TidePhase.Open,"A complete low-tide window is available.");
                yield return WalkDirect(IslandCampaign.CulvertExit+Vector3.forward*.3f,6,.18f);
                yield return null;
                Check(RunState.Has(IslandCampaign.CulvertCrossed),"A real free-blob crossing sets the optional route flag.");
                Check(!RunState.Has(IslandCampaign.ServiceOpen),"The blob route never needs the human service latch.");
            }
            else
            {
                var latch=UnityEngine.Object.FindObjectsByType<MissionProp>(FindObjectsSortMode.None).Single(p=>p.flag==IslandCampaign.ServiceOpen);
                Check(!latch.CanInteract(Context(),out _),"A free amoeba cannot work the maintenance latch.");
                var host=HostCharacter.All.Single(h=>h&&h.CanBePossessed&&h.DisplayName=="Island jetty engineer");
                Check(CompletePath(IslandCampaign.Spawn,host.transform.position),"The public maintenance host is reachable from the landing before opening either route.");
                PM.ForcePossessImmediate(host);
                bool reached=false;
                yield return AutoTest.WalkTo(IslandCampaign.ServiceLatch+Vector3.back,20,r=>reached=r,.5f);
                Check(reached,"The maintenance body physically reaches its service latch: "+host.DisplayName+" at "+host.transform.position+", target "+(IslandCampaign.ServiceLatch+Vector3.back));
                Check(!CompletePath(IslandCampaign.ServiceLatch+Vector3.back,IslandCampaign.Refuge),"The closed moving gate actually carves the human route.");
                PlayerCamera.Instance.SetYaw(0);yield return new WaitForSeconds(.3f);
                var target=PM.ComputeTarget();
                Check(target.kind==TargetKind.Interact&&target.interactable==(IInteractable)latch&&target.allowed,"Real E targeting selects the authorized physical latch.");
                GameInput.SimInteract=true;yield return null;GameInput.SimInteract=false;yield return null;
                Check(RunState.Has(IslandCampaign.ServiceOpen),"The latch opens with an immediate physical interaction.");
                yield return new WaitForSeconds(1.2f);
                Check(CompletePath(PM.Active.Position,IslandCampaign.Refuge),"Ignoring the moving panel in the static bake permits a complete human path after opening.");
            }
            bool arrived=false;
            yield return AutoTest.WalkTo(IslandCampaign.Refuge,20,r=>arrived=r,.6f);
            Check(arrived&&RunState.Has(IslandCampaign.InnerRefuge),"The real body enters the inner refuge, not just a remote objective flag.");
            Check(RunState.CheckpointName=="Island inner refuge"&&Vector3.Distance(RunState.CheckpointPosition,IslandCampaign.Refuge)<.1f,"Checkpoint uses the authored safe refuge anchor.");
            var flags=new HashSet<string>(RunState.Flags);int resets=RunState.Resets;
            GameManager.Instance.ResetToCheckpoint("Island first-slice recovery");
            yield return new WaitForSecondsRealtime(2.4f);yield return Ready();
            Check(flags.IsSubsetOf(RunState.Flags)&&RunState.LakesideOutcome==outcome,"Progress and immutable branch survive a checkpoint reload.");
            Check(PM.Active.IsFree&&Vector3.Distance(PM.Active.Position,IslandCampaign.Refuge)<1.5f,"Checkpoint boots a free amoeba at the safe inner landing.");
            Check(RunState.Resets==resets+1&&RunState.IslandStarted,"Checkpoint cannot silently return to the old city or riverside chapter.");
            Debug.Log("[IslandEntry] PASS "+outcome+" "+(culvert?"culvert":"maintenance")+" · explicit ferry Continue, physical entry and safe checkpoint");
        }

        public static IEnumerator TideRetryAndHumanClearance()
        {
            yield return EnterFromFerry(EndingOutcome.Peace);
            var host=HostCharacter.All.First(h=>h&&h.CanBePossessed&&h.Profession==Profession.Maintenance&&h.transform.position.x>560);
            PM.ForcePossessImmediate(host);Place(host,IslandCampaign.CulvertEntry+Vector3.back*.3f);
            float until=Time.realtimeSinceStartup+1.2f;
            while(Time.realtimeSinceStartup<until){PlayerCamera.Instance.SetYaw(0);GameInput.SimMove=Vector2.up;yield return null;}
            GameInput.SimMove=Vector2.zero;
            Check(PM.Active.Position.z<IslandCampaign.Origin.z-28.5f,"The real1.8m human capsule cannot pass the1.2m tunnel.");
            Check(!RunState.Has(IslandCampaign.CulvertCrossed),"A blocked human does not claim the blob route.");
            Check(PM.LeaveHost(),"Return to the real blob.");PM.Active.Blob.Place(IslandCampaign.CulvertRetry);
            float deadline=Time.realtimeSinceStartup+15;
            while(TidalCulvert.Instance.Phase!=TidePhase.Surge&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(TidalCulvert.Instance.Phase==TidePhase.Surge,"Exercise the actual visible surge phase.");
            until=Time.realtimeSinceStartup+.5f;
            while(Time.realtimeSinceStartup<until){PlayerCamera.Instance.SetYaw(0);GameInput.SimMove=Vector2.up;yield return null;}
            GameInput.SimMove=Vector2.zero;
            Check(PM.Active.Position.z<IslandCampaign.Origin.z-28.5f,"A failed first crossing returns before the unpassed gate, not to the bay beyond it.");
            Check(Vector3.Distance(TidalCulvert.Instance.RetryPoint(PM.Active),IslandCampaign.CulvertRetry)<.1f,"Only a visited safe entry is armed before reaching the bay.");
            Check(Physics.Raycast(PM.Active.Position+Vector3.up,Vector3.down,2,Layers.EnvironmentMask,QueryTriggerInteraction.Ignore),"Retry lands on real physical footing.");
            deadline=Time.realtimeSinceStartup+15;
            while((TidalCulvert.Instance.Phase!=TidePhase.Open||TideRules.Remaining(GetTideAge())<4.5f)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return WalkDirect(IslandCampaign.At(-26,.15f,-23.85f),4,.15f);
            var bay=IslandCampaign.At(-24.8f,.17f,-23.85f);
            yield return WalkDirect(bay,2,.12f);
            yield return null;
            Check(Vector3.Distance(TidalCulvert.Instance.RetryPoint(PM.Active),bay)<.2f,"The sheltered bay becomes a retry point only after a real visit.");
            deadline=Time.realtimeSinceStartup+12;
            while(TidalCulvert.Instance.Phase!=TidePhase.Surge&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(PM.Active.Position,bay)<.5f,"A physically sheltered blob can wait through the surge.");
            Debug.Log("[IslandEntry] PASS human height denial, pre-gate retry and physically visited dry bay");
        }

        public static IEnumerator EnterFromFerry(EndingOutcome outcome,bool skipArrival=true,Func<string,IEnumerator> arrivalCapture=null)
        {
            yield return AutoTest.LoadCity();
            Check(!GameManager.Instance.ContinueIsland()&&!RunState.IslandStarted,"Ordinary city gameplay cannot bypass the completed-ferry gate.");
            RunState.RecordLakesideOutcome(outcome);RunState.Flags.Add(MissionFlags.Escaped);
            Check(RunState.BeginAfterlake(),"Fixture enters the existing river chapter first.");
            SceneManager.LoadScene(GameManager.CityScene);yield return Ready();
            foreach(string flag in new[]{AfterlakeCampaign.Signal,AfterlakeCampaign.Floodgate,AfterlakeCampaign.WaterAnalysed,AfterlakeCampaign.CorridorOpen,AfterlakeCampaign.ChannelOpened})MissionManager.SetFlag(flag);
            GameManager.Instance.FinishAfterlake();
            Check(GameManager.Instance.State==GameState.Ended&&!RunState.IslandStarted,"Waiting at the ferry summary does not start the island.");
            int hosts=RunState.HostsPossessed,captures=RunState.Captures,resets=RunState.Resets;
            var flags=new HashSet<string>(RunState.Flags);
            yield return new WaitForSecondsRealtime(.85f);
            var button=HUDController.Instance.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Btn_Continue · The Far Shore");
            Check(button.gameObject.activeInHierarchy&&button.IsInteractable(),"The ferry summary exposes its own explicit island continuation.");
            Check(!GameManager.Instance.ContinueAfterLake(),"Original one-shot ContinueAfterLake stays unchanged.");
            int radioCues=0;bool capturedCue=false;
            void OnSubtitle(string who,string line)
            {
                if(who=="ISLAND RADIO"&&line.StartsWith("Low water opens",StringComparison.Ordinal))radioCues++;
            }
            void OnLoaded(Scene scene,LoadSceneMode mode)
            {
                if(RunState.IslandStarted&&scene.name==GameManager.CityScene)GameEvents.Subtitle+=OnSubtitle;
            }
            SceneManager.sceneLoaded+=OnLoaded;
            try
            {
                button.onClick.Invoke();yield return Ready();
                GameInput.Simulated=true;
                float deadline=Time.realtimeSinceStartup+Mathf.Max(8,IslandArrival.PresentationSeconds+2);
                while(!GameManager.Instance.IsPlaying&&Time.realtimeSinceStartup<deadline)
                {
                    if(!skipArrival&&radioCues>0&&!capturedCue&&arrivalCapture!=null)
                    {
                        capturedCue=true;yield return new WaitForSecondsRealtime(.4f);
                        yield return arrivalCapture("island-arrival-radio.png");
                    }
                    if(skipArrival){GameInput.SimJump=true;yield return null;GameInput.SimJump=false;}
                    yield return null;
                }
            }
            finally
            {
                SceneManager.sceneLoaded-=OnLoaded;GameEvents.Subtitle-=OnSubtitle;GameInput.SimJump=false;
            }
            Check(GameManager.Instance.IsPlaying&&RunState.Has(IslandCampaign.ArrivalSeen),"Protected arrival ends through its unscaled release-aware handoff.");
            Check(skipArrival?radioCues<=1:radioCues==1,"Arrival radio subtitle is emitted once, not restarted every rendered frame.");
            Check(RunState.IslandStarted&&RunState.MissionIndex==17&&MissionManager.Instance.Missions.Count==20,"Only the implemented first island mission is advertised.");
            Check(flags.IsSubsetOf(RunState.Flags)&&RunState.LakesideOutcome==outcome,"Chapter handoff retains every earlier flag and the first lake outcome.");
            Check(RunState.HostsPossessed==hosts&&RunState.Captures==captures&&RunState.Resets==resets,"Travel is not a death/reset or a stat wipe.");
            Check(PM.Active.IsFree&&PM.Agents.Count==1&&Vector3.Distance(PM.Active.Position,IslandCampaign.Spawn)<1.5f,"Chapter travel reunites free halves and lands on the island jetty.");
            Check(RunState.CurrentChapterSpawn==IslandCampaign.Spawn&&!GameObject.Find(AfterlakeDistrict.RootName),"Island world/fallback takes priority over the older river branch.");
            Check(!RunState.BeginIsland(),"Duplicate island continuation cannot replace its later checkpoint.");
            Calm();
        }

        static float GetTideAge()=>TidalCulvert.Instance.Age;
        static bool CompletePath(Vector3 from,Vector3 to)
        {
            if(!NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas)||!NavMesh.SamplePosition(to,out var b,2,NavMesh.AllAreas))return false;
            var path=new NavMeshPath();return NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
        }
        public static IEnumerator WalkDirect(Vector3 target,float seconds,float tolerance)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(Time.realtimeSinceStartup<deadline)
            {
                var d=target-PM.Active.Position;d.y=0;
                if(d.magnitude<tolerance){GameInput.SimMove=Vector2.zero;yield break;}
                PlayerCamera.Instance.SetYaw(0);GameInput.SimMove=new Vector2(d.normalized.x,d.normalized.z);GameInput.SimSprint=false;
                Calm();yield return null;
            }
            GameInput.SimMove=Vector2.zero;throw new InvalidOperationException("Physical direct traversal did not reach "+target+"; actual "+PM.Active.Position);
        }
        static IEnumerator Ready()
        {
            yield return null;float deadline=Time.realtimeSinceStartup+30;
            while((!CityDirector.Instance||!CityDirector.Instance.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(CityDirector.Instance&&CityDirector.Instance.Ready,"Chapter world booted before its navigation/spawns.");
            yield return null;yield return null;
            GameInput.Simulated=true;GameInput.SimMove=Vector2.zero;
            GameInput.SimInteract=GameInput.SimLeave=GameInput.SimSplit=GameInput.SimSwitch=GameInput.SimJump=GameInput.SimSprint=false;
        }
        static InteractContext Context()=>new(){agent=PM.Active,host=PM.Active.Host,permissions=PM.Active.Host?PM.Active.Host.Permissions:AccessPermission.None,position=PM.Active.Position};
        static void Place(HostCharacter host,Vector3 position){bool enabled=host.Controller.enabled;host.Controller.enabled=false;host.transform.position=position;host.Controller.enabled=enabled;Physics.SyncTransforms();}
        static void Calm(){if(SuspicionSystem.Instance){SuspicionSystem.Instance.SetValue(0);SuspicionSystem.Instance.OnSuspectGone();}}
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
