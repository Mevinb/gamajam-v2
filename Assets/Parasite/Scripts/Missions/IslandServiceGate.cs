using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Missions
{
    /// <summary>Moving collision is ignored by the static bake; carving is retired only as the physical panel opens.</summary>
    public sealed class IslandServiceGate : MonoBehaviour
    {
        public Vector3 openOffset;
        Vector3 closed;
        NavMeshObstacle obstacle;
        float t;
        void Awake()
        {
            closed=transform.localPosition;
            obstacle=GetComponent<NavMeshObstacle>();
            if(!obstacle)obstacle=gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape=NavMeshObstacleShape.Box;obstacle.carving=true;
            var box=GetComponent<BoxCollider>();obstacle.center=box.center;obstacle.size=box.size;
        }
        void Start(){t=RunState.Has(IslandCampaign.ServiceOpen)?1:0;Apply();}
        void Update()
        {
            if(!GameManager.Instance||!GameManager.Instance.IsPlaying)return;
            t=Mathf.MoveTowards(t,RunState.Has(IslandCampaign.ServiceOpen)?1:0,Time.deltaTime/.9f);Apply();
        }
        void Apply(){transform.localPosition=closed+openOffset*Mathf.SmoothStep(0,1,t);if(obstacle)obstacle.enabled=t<.6f;}
    }
}
