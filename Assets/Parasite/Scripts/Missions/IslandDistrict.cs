using System.Collections.Generic;
using System.Linq;
using Parasite.Characters;
using Parasite.World;
using Unity.AI.Navigation;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>The Far Shore first slice, built under World before the existing director bakes navigation/spawns actors.</summary>
    public static class IslandDistrict
    {
        public const string RootName="Island Far Shore";
        public static void Build()
        {
            if(GameObject.Find(RootName))return;
            var world=GameObject.Find("World");if(!world)world=new GameObject("World");
            var go=new GameObject(RootName);go.transform.SetParent(world.transform,false);go.transform.localPosition=IslandCampaign.Origin;
            var root=go.transform;var art=go.AddComponent<IslandArt>();
            go.AddComponent<IslandTravelers>();
            var concrete=art.Surface("IslandConcrete");var metal=art.Surface("IslandMetal");var timber=art.Surface("IslandTimber");
            var safety=art.Surface("IslandSafety");
            GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool solid=true)
            {
                var item=Prim.Make(PrimitiveType.Cube,root,position,size,material,solid,name);
                if(!solid){var c=item.GetComponent<Collider>();if(c)c.enabled=false;}
                art.ProjectBoxUV(item);
                return item;
            }
            var shore=Box("Island physical shore",new Vector3(0,-.45f,1),new Vector3(104,1.2f,78),concrete);
            shore.GetComponent<Renderer>().enabled=false;
            // Keep physical footing at .15m, but render the underlay8mm below authored floors to avoid coplanar flicker.
            Box("Island shore surface",new Vector3(0,-.458f,1),new Vector3(104,1.2f,78),concrete,false);
            // Visible coast guards prevent accidental unsupported walking while preserving actual water outside the shore.
            Box("West coast retaining wall",new Vector3(-51.7f,.75f,1),new Vector3(.6f,1.2f,78),concrete);
            Box("East coast retaining wall",new Vector3(51.7f,.75f,1),new Vector3(.6f,1.2f,78),concrete);
            Box("North coast west wall",new Vector3(-9.75f,.75f,39.7f),new Vector3(84.5f,1.2f,.6f),concrete);
            Box("North coast east wall",new Vector3(44.25f,.75f,39.7f),new Vector3(15.5f,1.2f,.6f),concrete);
            Box("South coast west wall",new Vector3(-44.75f,.75f,-37.7f),new Vector3(14.5f,1.2f,.6f),concrete);
            Box("South coast east wall",new Vector3(8.75f,.75f,-37.7f),new Vector3(86.5f,1.2f,.6f),concrete);
            // Permanent apertures in static wall: low culvert and human service gate. Wings meet water beyond land edges.
            foreach(var span in new[]{(-53f,-26.7f),(-25.3f,-8.2f),(-5.8f,53f)})
                Box("Quarantine wall",new Vector3((span.Item1+span.Item2)*.5f,1.95f,-18),new Vector3(span.Item2-span.Item1,3.6f,.4f),concrete);
            Box("Low passage lintel",new Vector3(-26,2.55f,-18),new Vector3(1.4f,2.4f,.4f),concrete);
            Box("Service entrance lintel",new Vector3(-7,3.25f,-18),new Vector3(2.4f,1f,.4f),concrete);
            // Dress these opaque boxes only after the authored visual kit has loaded. Physics/LOS/nav retain the exact boxes.
            art.Model("IslandQuarantineWalls",Vector3.zero,colliders:false);
            var dressedWalls=new[]{"Quarantine wall","Low passage lintel","Service entrance lintel","South coast west wall","South coast east wall"};
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.transform.parent==root && dressedWalls.Contains(renderer.name)) renderer.enabled=false;
            art.Model("IslandTidalCulvert",new Vector3(-26,.15f,-30));
            var indicator=art.Model("IslandTideIndicator",new Vector3(-26,1.35f,-29.2f));
            var tide=go.AddComponent<TidalCulvert>();
            tide.lamp=indicator.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r=>r.sharedMaterials.Any(m=>m&&m.name.Contains("IslandSignal")));
            var shutter=Box("Tidal sluice shutter",new Vector3(-26,2.35f,-25),new Vector3(1.4f,1.2f,.18f),metal);
            shutter.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            tide.gate=shutter.transform;
            foreach(var p in new[]{new Vector3(-26,1.1f,-28),new Vector3(-24.8f,1.1f,-23.85f)})
            {
                var lightObject=new GameObject("Culvert guide light");lightObject.transform.SetParent(root,false);lightObject.transform.localPosition=p;
                var light=lightObject.AddComponent<Light>();light.type=LightType.Point;light.range=4;light.intensity=1.1f;light.color=new Color(.52f,.78f,.66f);light.shadows=LightShadows.None;
            }

            var gateObject=Box("Service sliding gate",new Vector3(-7,1.45f,-18),new Vector3(2.4f,2.6f,.16f),metal);
            gateObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
            var gateDriver=gateObject.AddComponent<IslandServiceGate>();gateDriver.openOffset=new Vector3(3,0,0);
            var latch=art.Model("IslandServiceLatch",new Vector3(-9.4f,.15f,-21.2f));
            var prop=latch.AddComponent<MissionProp>();prop.flag=IslandCampaign.ServiceOpen;prop.displayName="Mechanical service latch";
            prop.verb="Release";prop.requiredPermission=AccessPermission.ServiceTunnel;prop.requiredFlag=IslandCampaign.Started;prop.holdSeconds=0;
            prop.doneToast="Service gate released. Walk through to the inner refuge.";
            art.Model("IslandRefugeClinic",new Vector3(-20,.15f,8));
            foreach(var p in new[]{new Vector3(17,.15f,8),new Vector3(27,.15f,13),new Vector3(34,.15f,22)})art.Model("IslandHandoffGate",p);
            foreach(var p in new[]{new Vector3(14,.15f,-4),new Vector3(2,.15f,0),new Vector3(-6,.15f,0)})
                Box("Visible survivor cover",p+new Vector3(0,1,1.25f),new Vector3(3,2,.45f),concrete);

            var refuge=new GameObject("Island inner refuge");refuge.transform.SetParent(root,false);refuge.transform.localPosition=new Vector3(-18,.15f,-8);refuge.AddComponent<IslandRefugeArrival>();
            // Existing detailed work ferry; collision remains the reviewed static deck/cabin and gangway.
            var surfaces=go.AddComponent<AfterlakeProps>();
            var prefab=Resources.Load<GameObject>("Riverside/RiversideFerry");
            if(!prefab)throw new System.InvalidOperationException("The verified ferry model is missing.");
            var ferry=Object.Instantiate(prefab,root,false);ferry.transform.localPosition=new Vector3(-36,.15f,-46);ferry.name="Island arrival ferry";
            foreach(var renderer in ferry.GetComponentsInChildren<MeshRenderer>())
            {
                var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=surfaces.Surface(slots[i]?slots[i].name:"CinemaDark");renderer.sharedMaterials=slots;
            }
            var deck=Box("Arrival ferry deck",new Vector3(-36,.35f,-46),new Vector3(6,.4f,8),timber);deck.GetComponent<Renderer>().enabled=false;
            var cabin=Box("Arrival ferry cabin collision",new Vector3(-36,1.6f,-48),new Vector3(3.5f,2.2f,2.5f),metal);cabin.GetComponent<Renderer>().enabled=false;
            var gangway=Box("Arrival physical gangway",new Vector3(-36,.35f,-40),new Vector3(1.8f,.15f,4.6f),timber);gangway.transform.localRotation=Quaternion.Euler(7,0,0);
            var rescue=Object.Instantiate(prefab,root,false);rescue.transform.localPosition=new Vector3(34,.15f,47);rescue.transform.localRotation=Quaternion.Euler(0,180,0);rescue.name="Island rescue launch";
            foreach(var renderer in rescue.GetComponentsInChildren<MeshRenderer>())
            {var slots=renderer.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=surfaces.Surface(slots[i]?slots[i].name:"CinemaDark");renderer.sharedMaterials=slots;}
            var rescueDeck=Box("Rescue static deck",new Vector3(34,.35f,47),new Vector3(6,.4f,8),timber);rescueDeck.GetComponent<Renderer>().enabled=false;
            var rescueCabin=Box("Rescue cabin collision",new Vector3(34,1.6f,49),new Vector3(3.5f,2.2f,2.5f),metal);rescueCabin.GetComponent<Renderer>().enabled=false;
            var rescueGangway=Box("Rescue physical gangway",new Vector3(34,.35f,41.8f),new Vector3(1.8f,.15f,4.2f),timber);rescueGangway.transform.localRotation=Quaternion.Euler(-7,0,0);
            art.Model("IslandFlareCache",new Vector3(18,.15f,18)).AddComponent<IslandSignalPickup>();
            art.Model("IslandFlareMount",new Vector3(32,.15f,37)).AddComponent<IslandSignalMount>();
            go.AddComponent<IslandSignalTransport>();
            var departure=new GameObject("Island rescue intake");departure.transform.SetParent(root,false);departure.transform.localPosition=new Vector3(34,.55f,42.8f);departure.AddComponent<IslandExtraction>();
            Box("Signal cache safe cover",new Vector3(18,1.15f,13.5f),new Vector3(4,2,.4f),concrete);
            var shader=Resources.Load<Shader>("Shaders/RiverWater");
            if(shader)
            {
                var material=new Material(shader);material.SetFloat("_CleanWater",1);material.SetFloat("_ShoreZ",IslandCampaign.Origin.z-38);
                var water=Prim.Make(PrimitiveType.Plane,root,new Vector3(0,-.3f,0),new Vector3(28,1,22),material,false,"Island tidal sea");
                var collider=water.GetComponent<Collider>();if(collider)collider.enabled=false;
                water.layer=4;
                var cutout=go.AddComponent<SkylineWaterCutout>();
                if(!cutout.ApplyToSkyline(new[]{water.GetComponent<MeshRenderer>().bounds}))throw new System.InvalidOperationException("Island water cutout failed: "+cutout.LastFailure);
                go.AddComponent<IslandOwnedMaterial>().material=material;
            }
            var fontMaterial=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).FirstOrDefault(m=>m&&m.name.StartsWith("WorldText",System.StringComparison.Ordinal));
            void Sign(string text,Vector3 position,float size=.12f)
            {
                var sign=new GameObject("Island sign "+text);sign.transform.SetParent(root,false);sign.transform.localPosition=position;sign.transform.localRotation=Quaternion.Euler(0,180,0);
                var mesh=sign.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.fontSize=64;mesh.characterSize=size;
                mesh.font=Resources.Load<Font>("UI/Rajdhani-Medium");mesh.color=new Color(.88f,.91f,.8f);
                if(fontMaterial)sign.AddComponent<WorldTextFont>().Configure(fontMaterial);
            }
            Sign("LOW TIDE / BLOB PASSAGE",new Vector3(-26,2.8f,-29.3f));
            Sign("SERVICE / MAINTENANCE",new Vector3(-7,3.35f,-18.25f));
            Sign("DRY REFUGE",new Vector3(-18,2.5f,-8));
            void Spawn(string name,Profession profession,Vector3 position,int seed,NpcBehaviour behavior=NpcBehaviour.Stationary,PatrolRoute route=null,bool ally=false)
            {
                var marker=new GameObject("Island spawn "+name);marker.transform.SetParent(root,false);marker.transform.localPosition=position;
                var spawn=marker.AddComponent<NpcSpawn>();spawn.displayName=name;spawn.profession=profession;spawn.seed=seed;spawn.behaviour=behavior;spawn.route=route;spawn.wanderRadius=3;spawn.reportsPlayerViolations=!ally;
            }
            Spawn("Island jetty engineer",Profession.Maintenance,new Vector3(-13,.15f,-30),1801);
            Spawn("Island landing volunteer",Profession.Civilian,new Vector3(-31,.15f,-35),1802);
            Spawn("Island refuge engineer",Profession.Maintenance,new Vector3(-14,.15f,-7),1803);
            Spawn("Island relief scientist",Profession.Scientist,new Vector3(-15,.15f,-5),1804);
            Spawn("Island refuge volunteer",Profession.Civilian,new Vector3(-20,.15f,-5),1805);
            var patrolObject=new GameObject("Island outer patrol");patrolObject.transform.SetParent(root,false);var route=patrolObject.AddComponent<PatrolRoute>();route.waitAtPoint=2;
            foreach(var point in new[]{new Vector3(-3,.15f,-32),new Vector3(11,.15f,-32),new Vector3(11,.15f,-23),new Vector3(-3,.15f,-23)})
            {var p=new GameObject("Patrol point");p.transform.SetParent(patrolObject.transform,false);p.transform.localPosition=point;}
            Spawn("Island quarantine sentry",Profession.Guard,new Vector3(11,.15f,-32),1806,NpcBehaviour.Patrol,route);
            var innerRouteObject=new GameObject("Island inner patrol");innerRouteObject.transform.SetParent(root,false);var innerRoute=innerRouteObject.AddComponent<PatrolRoute>();innerRoute.waitAtPoint=2;
            foreach(var point in new[]{new Vector3(15,.15f,6),new Vector3(3,.15f,6),new Vector3(-3,.15f,-3),new Vector3(9,.15f,-3)})
            {var p=new GameObject("Inner patrol point");p.transform.SetParent(innerRouteObject.transform,false);p.transform.localPosition=point;}
            Spawn("Island curfew patrol",Profession.Guard,new Vector3(15,.15f,6),1906,NpcBehaviour.Patrol,innerRoute);
            Spawn("Island cover medic",Profession.Scientist,new Vector3(2,.15f,-3),1907);
            Spawn("Island supply marshal",Profession.Civilian,new Vector3(20,.15f,16),2001);
            Spawn("Island rescue volunteer",Profession.Civilian,new Vector3(30,.15f,35),2002,ally:true);
            Spawn("Island launch engineer",Profession.Maintenance,new Vector3(38,.15f,35),2003,ally:true);

        }
    }
}
