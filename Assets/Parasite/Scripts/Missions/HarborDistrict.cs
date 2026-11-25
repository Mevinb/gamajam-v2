using System;
using System.Linq;
using Parasite.World;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Bounded Harbor chapter. All physical geometry exists before CityDirector builds navigation and actors.</summary>
    public static class HarborDistrict
    {
        public static void Build()
        {
            if (!RunState.Has(HarborCampaign.Started) || GameObject.Find(HarborLayout.RootName)) return;
            var world = GameObject.Find("World"); if (!world) world = new GameObject("World");
            var go = new GameObject(HarborLayout.RootName); go.transform.SetParent(world.transform,false); go.transform.localPosition = HarborLayout.Origin;
            var root = go.transform; var art = go.AddComponent<HarborArt>();
            GameObject Box(string name, Vector3 at, Vector3 size, int tile=0, bool solid=true) => art.Box(name,at,size,tile,solid);
            var footing = Box("Harbor physical yard footing",new Vector3(0,-.5f,3),new Vector3(80,1.3f,90));
            footing.GetComponent<Renderer>().enabled=false;art.Paving();
            // Paving sits1mm below the same physical slab top. Every boundary has a matching visible solid.
            foreach (float x in new[] { -39.8f,39.8f }) Box("Harbor perimeter side",new Vector3(x,1.65f,3),new Vector3(.4f,3,90));
            foreach (float z in new[] { -41.8f,47.8f }) Box("Harbor perimeter end",new Vector3(0,1.65f,z),new Vector3(79.2f,3,.4f));
            Box("Harbor quarantine west wall",new Vector3(-26,2.05f,0),new Vector3(28,3.8f,.45f));
            Box("Harbor quarantine east wall",new Vector3(16,2.05f,0),new Vector3(48,3.8f,.45f));
            foreach(float x in new[] { -12.25f,-7.75f }) Box("Harbor lift gantry post",new Vector3(x,3.95f,0),new Vector3(.4f,7.6f,.6f),1);
            Box("Harbor lift gantry crown",new Vector3(-10,7.85f,0),new Vector3(4.9f,.25f,.65f),1);
            var moving = new GameObject("Harbor pressure lift gate"); moving.transform.SetParent(root,false); moving.transform.localPosition=HarborLayout.GateLocal;
            moving.AddComponent<BoxCollider>().size=HarborLayout.GateSize;
            moving.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            var carving=moving.AddComponent<NavMeshObstacle>();carving.shape=NavMeshObstacleShape.Box;carving.size=HarborLayout.GateSize;carving.carving=true;carving.carveOnlyStationary=false;
            art.Model("LiftGate",Vector3.zero,parent:moving.transform);
            var gauge=art.Model("GaugeBase",HarborLayout.GaugeLocal);
            var needle=art.Model("GaugeNeedle",new Vector3(0,0,-.18f),parent:gauge.transform);
            var gaugeCollider=gauge.AddComponent<BoxCollider>();gaugeCollider.center=new Vector3(0,-.52f,.05f);gaugeCollider.size=new Vector3(.84f,1.9f,.5f);
            var pressure=go.AddComponent<HarborPressureWorks>();
            pressure.Configure(moving.transform,needle.transform,art.Lamp("Harbor pressure status",HarborLayout.GaugeLocal+new Vector3(.52f,0,0)));
            pressure.openOffset=HarborLayout.GateOpenOffset;
            for(int i=0;i<HarborLayout.ValveLocal.Length;i++)
            {
                var station=art.Model("ValveBase",HarborLayout.ValveLocal[i]);station.name="Harbor valve "+(char)('A'+i);
                var physical=station.AddComponent<BoxCollider>();physical.center=new Vector3(0,.72f,0);physical.size=new Vector3(1.05f,1.44f,.82f);
                var wheel=art.Model("ValveWheel",new Vector3(0,1.1f,-.36f),parent:station.transform);
                station.AddComponent<HarborValve>().Configure(pressure,i,wheel.transform,art.Lamp("Harbor valve status",new Vector3(.30f,.83f,-.28f),station.transform));
            }
            // Controls occupy the public west aisle. Their visible pedestal and collider agree.
            GameObject Console(string name,Vector3 at)
            {
                var item=art.Model("ValveBase",at);item.name=name;var c=item.AddComponent<BoxCollider>();c.center=new Vector3(0,.72f,0);c.size=new Vector3(1.05f,1.44f,.82f);return item;
            }
            var cargo=go.AddComponent<HarborCargoDirector>();
            var scanner=Console("Harbor public cargo scanner",HarborLayout.ScannerLocal);scanner.AddComponent<HarborCargoScanner>().Configure(cargo,art.Lamp("Harbor scanner status",new Vector3(0,1.2f,-.35f),scanner.transform));
            var brake=Console("Harbor public conveyor brake",HarborLayout.BrakeLocal);brake.AddComponent<HarborBrake>().Configure(cargo,art.Lamp("Harbor brake status",new Vector3(0,1.2f,-.35f),brake.transform));
            // The fenced cargo work area is explicitly excluded from navigation. Narrow real bars stop capsules but admit scanner rays.
            var exclusion=new GameObject("Harbor cargo nonwalkable volume");exclusion.transform.SetParent(root,false);
            var nonwalkable=exclusion.AddComponent<NavMeshModifierVolume>();nonwalkable.center=HarborLayout.CargoWorkBounds.center-HarborLayout.Origin;nonwalkable.size=HarborLayout.CargoWorkBounds.size+Vector3.up*3;nonwalkable.area=1;
            void BarFence(bool alongZ,float fixedAxis,float start,float end)
            {
                for(float p=start+.15f;p<end;p+=.43f)
                    Box("Harbor cargo physical grille bar",alongZ?new Vector3(fixedAxis,1.6f,p):new Vector3(p,1.6f,fixedAxis),alongZ?new Vector3(.08f,2.9f,.08f):new Vector3(.08f,2.9f,.08f),1);
                foreach(float y in new[] { .24f,2.98f }) Box("Harbor cargo visible grille rail",alongZ?new Vector3(fixedAxis,y,(start+end)*.5f):new Vector3((start+end)*.5f,y,fixedAxis),alongZ?new Vector3(.10f,.10f,end-start):new Vector3(end-start,.10f,.10f),1);
            }
            BarFence(true,10,8,37);BarFence(true,35,8,37);BarFence(false,8,10,35);BarFence(false,37,10,35);
            // Modular authored conveyors: their roller crowns sit at .55m, matching the crate-bottom route contract.
            void Belt(Vector3 at,float yaw=0)
            { var model=art.Model("Conveyor",at+new Vector3(0,-.223f,0),yaw);model.transform.localScale=new Vector3(.75f,1,1); }
            foreach(float z in new[] { 10f,22f,25f }) foreach(float x in (z==25?new[] { 17f,23f,29f }:new[] { 17f,23f })) Belt(new Vector3(x,0,z));
            foreach(float x in new[] { 14f,26f }) foreach(float z in new[] { 13f,19f }) Belt(new Vector3(x,0,z),90);
            foreach(float x in new[] { 30f,32f }) foreach(float z in new[] { 28f,34f }) Belt(new Vector3(x,0,z),90);
            // Diagonal scan gantry leaves the west-to-crate line at z16 unobstructed by its two posts.
            Box("Harbor scan arch left post",new Vector3(12.4f,1.7f,15.3f),new Vector3(.14f,3.1f,.14f),2);
            Box("Harbor scan arch right post",new Vector3(15.6f,1.7f,16.7f),new Vector3(.14f,3.1f,.14f),2);
            var cross=Box("Harbor scan arch crosshead",new Vector3(14,3.24f,16),new Vector3(3.65f,.14f,.20f),2,false);cross.transform.localRotation=Quaternion.Euler(0,-23.63f,0);
            var crates=new Transform[4];
            for(int i=0;i<crates.Length;i++)
            {
                var crate=art.Model("SealedCrate",HarborLayout.CargoLoopLocal[i]);crate.name="Harbor moving sealed cargo "+i;
                var c=crate.AddComponent<BoxCollider>();c.center=new Vector3(0,.5f,0);c.size=new Vector3(1.2f,1,1.4f);
                var rb=crate.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;crates[i]=crate.transform;
            }
            cargo.Configure(crates,HarborLayout.CargoLoopLocal.Select(HarborLayout.World).ToArray(),HarborLayout.ScanBounds,HarborLayout.CargoBayLocal.Select(HarborLayout.World).ToArray());
            var relay=go.AddComponent<HarborRelay>();var sights=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var pad=new GameObject("Harbor relay pad "+(i==0?"A":"B"));pad.transform.SetParent(root,false);pad.transform.localPosition=HarborLayout.RelayLocal[i];
                art.Model("RelayPad",new Vector3(0,-.132f,0),parent:pad.transform);
                // Pad tread is visual-only over continuous yard footing, preserving the contract footing y=.15.
                art.Model("Emitter",HarborLayout.RelayLocal[i]+new Vector3(0,0,.9f));
                var sight=new GameObject("Harbor relay sight point "+i);sight.transform.SetParent(root,false);sight.transform.position=HarborLayout.RelaySightPoint(i);sights[i]=sight.transform;
                Box("Harbor relay protected optical arm",HarborLayout.RelayLocal[i]+new Vector3(0,.8f,.45f),new Vector3(.045f,.045f,.9f),1,false);
                pad.AddComponent<HarborRelayNode>().Configure(relay,art.Lamp("Harbor relay actual sight lens",sight.transform.localPosition));
            }
            relay.Configure(HarborLayout.RelayPad(0),HarborLayout.RelayPad(1),sights);
            // Short cover pockets leave both the amoeba route and human aisles at least2m wide.
            foreach(var p in new[] { new Vector3(-29,1.10f,-25),new Vector3(-7,1.1f,-11),new Vector3(-21,1.1f,12),new Vector3(2,1.1f,25) })
                Box("Harbor fair patrol cover",p,new Vector3(3.5f,1.9f,.6f));
            void Spawn(string name,Profession job,Vector3 at,int seed,NpcBehaviour behaviour=NpcBehaviour.Stationary,PatrolRoute route=null)
            {
                var marker=new GameObject("Harbor spawn "+name);marker.transform.SetParent(root,false);marker.transform.localPosition=at;
                var spawn=marker.AddComponent<NpcSpawn>();spawn.displayName=name;spawn.profession=job;spawn.seed=seed;spawn.behaviour=behaviour;spawn.route=route;spawn.wanderRadius=2;spawn.reportsPlayerViolations=job==Profession.Guard;
            }
            Spawn("Harbor public maintenance engineer",Profession.Maintenance,new Vector3(-26,.15f,-28),2101);
            Spawn("Harbor public scientist",Profession.Scientist,new Vector3(-22,.15f,-28),2102);
            Spawn("Harbor relay scientist",Profession.Scientist,new Vector3(3,.15f,29),2301);
            PatrolRoute Route(string name,Vector3[] points)
            {
                var r=new GameObject(name);r.transform.SetParent(root,false);var patrol=r.AddComponent<PatrolRoute>();patrol.waitAtPoint=2;
                foreach(var point in points){var p=new GameObject("Harbor patrol waypoint");p.transform.SetParent(r.transform,false);p.transform.localPosition=point;}return patrol;
            }
            var outer=Route("Harbor outer fair patrol",new[] { new Vector3(-3,.15f,-33),new Vector3(10,.15f,-33),new Vector3(10,.15f,-9),new Vector3(-3,.15f,-9) });
            var inner=Route("Harbor inner fair patrol",new[] { new Vector3(-19,.15f,10),new Vector3(-4,.15f,10),new Vector3(-4,.15f,23),new Vector3(-19,.15f,23) });
            Spawn("Harbor outer quarantine guard",Profession.Guard,new Vector3(10,.15f,-33),2103,NpcBehaviour.Patrol,outer);
            Spawn("Harbor inner logistics guard",Profession.Guard,new Vector3(-4,.15f,23),2203,NpcBehaviour.Patrol,inner);
            var fontMaterial=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m&&m.name.StartsWith("WorldText",StringComparison.Ordinal));
            void Sign(string text,Vector3 at)
            {
                var sign=new GameObject("Harbor instruction "+text);sign.transform.SetParent(root,false);sign.transform.localPosition=at;sign.transform.localRotation=Quaternion.Euler(0,180,0);
                var label=sign.AddComponent<TextMesh>();label.text=text;label.font=Resources.Load<Font>("UI/Rajdhani-Medium");label.fontSize=64;label.characterSize=.10f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.92f,.78f,.43f);
                if(fontMaterial)sign.AddComponent<WorldTextFont>().Configure(fontMaterial);
            }
            for(int i=0;i<3;i++)Sign(i==0?"A / PUMP +3":i==1?"B / PUMP +2":"C / BLEED -1",HarborLayout.ValveLocal[i]+new Vector3(0,1.65f,-.37f));
            Sign("CANAL WORKS / MAX PRESSURE 4",new Vector3(-18,3.2f,-14));
            Sign("QUARANTINE / PRESSURE LOCK",new Vector3(-10,4.3f,-.4f));
            Sign("CARGO MANIFEST / SCAN IN THE ARCH",new Vector3(7,2.1f,16));
            Sign("TWIN RELAY / FREE AMOEBA ON EACH PAD",new Vector3(0,2.6f,36));
            for(int i=0;i<crates.Length;i++)
            {
                string id=HarborCargoDirector.Ids[i];crates[i].name="Harbor moving sealed cargo "+id;
                foreach(bool west in new[] { false,true })
                {
                    var lot=new GameObject("Harbor visible lot "+id);lot.transform.SetParent(crates[i],false);lot.transform.localPosition=west?new Vector3(-.612f,.63f,0):new Vector3(0,.63f,-.747f);lot.transform.localRotation=Quaternion.Euler(0,west?-90:180,0);
                    var text=lot.AddComponent<TextMesh>();text.text=id;text.font=Resources.Load<Font>("UI/Rajdhani-Medium");text.fontSize=64;text.characterSize=.075f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.96f,.83f,.48f);
                    if(fontMaterial)lot.AddComponent<WorldTextFont>().Configure(fontMaterial);
                }
            }
            art.BuildWater();
            var ferryPrefab=Resources.Load<GameObject>("Riverside/RiversideFerry");
            if(ferryPrefab)
            {
                var ferry=UnityEngine.Object.Instantiate(ferryPrefab,root,false);ferry.name="Harbor distant existing work ferry";ferry.transform.localPosition=new Vector3(-28,.15f,-48);
                var surfaces=go.AddComponent<AfterlakeProps>();
                foreach(var renderer in ferry.GetComponentsInChildren<MeshRenderer>()) { var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=surfaces.Surface(materials[i]?materials[i].name:"CinemaDark");renderer.sharedMaterials=materials; }
            }
        }
    }
}
