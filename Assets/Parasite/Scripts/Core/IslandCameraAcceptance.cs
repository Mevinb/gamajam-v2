using System;
using System.Collections;
using Parasite.Missions;
using Parasite.Possession;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.Testing
{
    /// <summary>Rendered-camera proof, sampled after Cinemachine updates rather than before LateUpdate.</summary>
    public static class IslandCameraAcceptance
    {
        public static IEnumerator Run(Func<string,IEnumerator> capture=null)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Island camera clearance requires a real graphics device.");
            yield return IslandEntryAcceptance.EnterFromFerry(EndingOutcome.Peace);
            // Isolate camera clearance from containment during the long tide/target-switch observations.
            // Entry/stealth routes have separate fixtures; all physical walls, NPCs and tide mechanics remain.
            foreach(var person in Characters.HostCharacter.All)
                if(person&&person.transform.position.x>560&&person.Brain is Characters.GuardAI guard)guard.Stun(600);
            var pm=PossessionManager.Instance;var actor=pm.Active;var blob=actor.Blob;
            var camera=PlayerCamera.Instance;var brain=camera.Cam.GetComponent<CinemachineBrain>();
            var vcam=camera.GetComponentInChildren<CinemachineCamera>();
            var follow=camera.GetComponentInChildren<CinemachineThirdPersonFollow>();
            Check(vcam&&follow&&!camera.IsLowClearance,"Snapshot the real normal profile before approaching the culvert.");
            Transform ownedPivot=vcam.Target.TrackingTarget;
            Vector3 normalShoulder=follow.ShoulderOffset,normalDamping=follow.Damping;
            float normalArm=follow.VerticalArmLength,normalInto=follow.AvoidObstacles.DampingIntoCollision,normalFrom=follow.AvoidObstacles.DampingFromCollision;
            Vector3 originalMarker=blob.CameraTarget.localPosition;
            string failure=null,phase="setup";int samples=0,restoredSamples=0,failedSamples=0;float highest=blob.transform.position.y;
            bool monitor=false,verifyDestroyedOwner=false;
            void SetPhase(string value)
            {
                phase=value;
                Check(blob,"The camera fixture lost its original free body before phase "+phase+"; agents="+pm.Agents.Count+" captures="+RunState.Captures+" resets="+RunState.Resets);
                Debug.Log("[IslandCamera] phase="+phase+" frame="+Time.frameCount+" blob="+(blob.transform.position-IslandCampaign.Origin).ToString("F4"));
            }
            void Fail(string reason,Vector3 viewport)
            {
                failedSamples++;
                if(failure!=null)return; // Keep the first bad sample; a later pose must not overwrite its evidence.
                var obstacle=follow.CurrentObstacle;
                failure=reason+" phase="+phase+" frame="+Time.frameCount+" sample="+samples+
                    " blob="+(blob.transform.position-IslandCampaign.Origin).ToString("F4")+" centre="+(blob.Center-IslandCampaign.Origin).ToString("F4")+
                    " pivot="+(ownedPivot.position-IslandCampaign.Origin).ToString("F4")+" camera="+(camera.Position-IslandCampaign.Origin).ToString("F4")+
                    " forward="+camera.Forward.ToString("F4")+" viewport="+viewport.ToString("F4")+
                    " low="+camera.IsLowClearance+" pitch="+camera.Pitch.ToString("F3")+" yaw="+camera.Yaw.ToString("F3")+
                    " boom="+follow.CameraDistance.ToString("F4")+" actualDistance="+Vector3.Distance(camera.Position,blob.Center).ToString("F4")+
                    " damping="+follow.Damping.ToString("F4")+" collisionInto="+follow.AvoidObstacles.DampingIntoCollision.ToString("F4")+
                    " collisionFrom="+follow.AvoidObstacles.DampingFromCollision.ToString("F4")+" obstacle="+(obstacle?obstacle.name:"none")+
                    " active="+(brain.ActiveVirtualCamera!=null?brain.ActiveVirtualCamera.Name:"none")+" blending="+brain.IsBlending;
                Debug.Log("[IslandCamera] FIRST FAILURE "+failure);
            }
            void Sample(CinemachineBrain updated)
            {
                if(!monitor||updated!=brain||!camera||!blob||pm.Active!=actor||!actor.IsFree)return;
                samples++;
                var viewport=camera.Cam.WorldToViewportPoint(blob.Center);
                if(blob.CameraTarget.localPosition!=originalMarker)Fail("Low camera changed the canonical body marker.",viewport);
                if(verifyDestroyedOwner&&!TidalCulvert.Instance)
                {
                    restoredSamples++;
                    if(camera.IsLowClearance||vcam.Target.TrackingTarget!=ownedPivot||!ownedPivot.position.Equals(blob.Center))Fail("Destroying the active context did not restore normal free-body centre tracking through the same owned pivot.",viewport);
                    if(!blob.CameraTarget.localPosition.Equals(originalMarker)||!follow.ShoulderOffset.Equals(normalShoulder)||follow.VerticalArmLength!=normalArm)Fail("Destroying the active context changed the canonical marker or failed to restore the exact normal rig offsets.",viewport);
                    if(!follow.Damping.Equals(normalDamping)||follow.AvoidObstacles.DampingIntoCollision!=normalInto||follow.AvoidObstacles.DampingFromCollision!=normalFrom)Fail("Destroying the active context failed to restore the exact normal damping.",viewport);
                }
                if(Physics.CheckSphere(camera.Position,.18f,Layers.EnvironmentMask,QueryTriggerInteraction.Ignore))Fail("Camera sphere penetrated actual tunnel/ground collision.",viewport);
                if(viewport.z<=camera.Cam.nearClipPlane||viewport.x<.01f||viewport.x>.99f||viewport.y<.01f||viewport.y>.99f)Fail("Blob centre was behind the camera or outside its viewport.",viewport);
                if(camera.IsLowClearance&&!follow.Damping.Equals(Vector3.zero))Fail("The short low-clearance boom retained positional lag.",viewport);
                var p=blob.transform.position-IslandCampaign.Origin;
                if(camera.IsLowClearance&&p.z>-29.1f&&p.z<-17.1f&&(camera.Position.y<.28f||camera.Position.y>1.22f))Fail("Low camera left the physically clear floor/roof interval.",viewport);
                highest=Mathf.Max(highest,blob.transform.position.y);
            }
            CinemachineCore.CameraUpdatedEvent.AddListener(Sample);
            try
            {
                // Include the original normal-to-low boundary, not just the already-low retry position.
                monitor=true;SetPhase("normal-approach");
                bool reached=false;
                yield return AutoTest.WalkTo(IslandCampaign.CulvertEntry,20,r=>reached=r,.5f);
                Check(reached,"Camera fixture reaches the real portal.");
                SetPhase("entry-retry");
                yield return IslandEntryAcceptance.WalkDirect(IslandCampaign.CulvertRetry,4,.18f);
                SetPhase("wait-entry-tide");
                float deadline=Time.realtimeSinceStartup+15;
                while((TidalCulvert.Instance.Phase!=TidePhase.Open||TideRules.Remaining(TidalCulvert.Instance.Age)<5.5f)&&Time.realtimeSinceStartup<deadline)yield return null;
                SetPhase("low-entry");
                yield return IslandEntryAcceptance.WalkDirect(IslandCampaign.At(-26,.15f,-27.2f),2,.15f);
                yield return null;yield return null;
                Check(camera.IsLowClearance,"Entering the physical low corridor selects its contextual profile.");
                if(capture!=null)yield return capture("island-camera-entry.png");
                SetPhase("pitch-down");camera.SetPitch(-80);yield return new WaitForSeconds(.15f);Check(Mathf.Abs(camera.Pitch+5)<.01f,"Low view clamps the downward pitch without changing jump.");
                SetPhase("pitch-up");camera.SetPitch(80);yield return new WaitForSeconds(.15f);Check(Mathf.Abs(camera.Pitch-12)<.01f,"Low view clamps upward pitch.");
                SetPhase("yaw-right");camera.SetYaw(80);yield return new WaitForSeconds(.2f);
                SetPhase("yaw-left");camera.SetYaw(-80);yield return new WaitForSeconds(.2f);
                SetPhase("yaw-forward");camera.SetYaw(0);yield return new WaitForSeconds(.15f);
                SetPhase("hop");float floor=blob.transform.position.y;
                GameInput.SimJump=true;yield return null;GameInput.SimJump=false;
                yield return new WaitForSeconds(.25f);
                if(capture!=null)yield return capture("island-camera-hop.png");
                yield return new WaitForSeconds(.45f);
                Check(highest>floor+.3f,"The actual blob still hops; the profile does not disable jumping to pass clearance.");
                Check(blob.CameraTarget.localPosition==originalMarker,"Hop leaves the exact original marker unchanged.");
                // Leave the body in exposed flow, then observe the actual tide teleport and camera reset.
                SetPhase("wait-wash");deadline=Time.realtimeSinceStartup+15;
                while(blob.transform.position.z>IslandCampaign.Origin.z-28.5f&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return null;yield return null;
                Check(Vector3.Distance(blob.transform.position,IslandCampaign.CulvertRetry)<.6f,"The real tide wash moved the same body back to its visited entry.");
                SetPhase("washed-retry");
                if(capture!=null)yield return capture("island-camera-wash.png");
                Check(failure==null,failure??"Camera checks passed.");
                // Change body/host targets while the first blob remains in a cramped context.
                SetPhase("switch-secondary");
                Check(pm.TrySplit()&&pm.SwitchActive(),"A second body exercises target changes.");
                var second=pm.Active;
                var host=Characters.HostCharacter.All.Find(h=>h&&h.CanBePossessed&&h.Profession==Profession.Maintenance&&h.transform.position.x>560);
                Check(host,"An actual island host exists for camera target switching.");
                Vector3 hostMarker=host.CameraTarget.localPosition;
                SetPhase("possess-host");pm.ForcePossessImmediate(host);yield return null;yield return null;
                Check(!camera.IsLowClearance&&blob.CameraTarget.localPosition==originalMarker&&host.CameraTarget.localPosition==hostMarker,"Changing to a host restores the normal profile and preserves both canonical markers exactly.");
                SetPhase("switch-original");Check(pm.SwitchActive()&&pm.Active==actor,"Return to the original blob.");yield return null;yield return null;
                Check(blob.CameraTarget.localPosition==originalMarker,"Switching back cannot accumulate a marker-height offset.");
                SetPhase("wait-exit-tide");deadline=Time.realtimeSinceStartup+15;
                while((TidalCulvert.Instance.Phase!=TidePhase.Open||TideRules.Remaining(TidalCulvert.Instance.Age)<4.5f)&&Time.realtimeSinceStartup<deadline)yield return null;
                SetPhase("low-exit");camera.SetYaw(0);camera.SetPitch(12);
                yield return IslandEntryAcceptance.WalkDirect(IslandCampaign.At(-26,.15f,-13.4f),6,.2f);
                yield return null;yield return null;
                Check(!camera.IsLowClearance&&blob.CameraTarget.localPosition==originalMarker,"Leaving the padded tunnel context restores the exact normal target.");
                if(capture!=null)yield return capture("island-camera-exit.png");
                // Reenter the padded, physically open exit approach so teardown begins with low mode active.
                SetPhase("reenter-context");
                yield return IslandEntryAcceptance.WalkDirect(IslandCampaign.At(-26,.15f,-14.2f),2,.08f);
                yield return null;yield return null;
                Check(camera.IsLowClearance,"Owner-destruction fixture starts with the contextual profile active.");
                SetPhase("destroy-active-owner");verifyDestroyedOwner=true;
                UnityEngine.Object.Destroy(TidalCulvert.Instance);yield return null;yield return null;
                Check(!TidalCulvert.Instance&&restoredSamples>0&&!camera.IsLowClearance,"Post-Cinemachine samples observe active-owner destruction restoring the normal profile.");
                Check(blob.CameraTarget.localPosition.Equals(originalMarker)&&host.CameraTarget.localPosition.Equals(hostMarker),"Destroying the contextual owner cannot alter either exact canonical camera marker.");
                Check(samples>10&&failure==null,failure??"Cinemachine produced real post-update camera samples.");
                Debug.Log("[IslandCamera] PASS "+samples+" post-Cinemachine samples (failed samples "+failedSamples+"): entry, pitch extremes, side look, hop, wash, host changes and exit/destruction");
            }
            finally
            {
                monitor=false;CinemachineCore.CameraUpdatedEvent.RemoveListener(Sample);
                GameInput.SimMove=Vector2.zero;GameInput.SimJump=false;
            }
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
