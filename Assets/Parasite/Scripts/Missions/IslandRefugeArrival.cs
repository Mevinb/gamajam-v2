using Parasite.Possession;
using UnityEngine;

namespace Parasite.Missions
{
    public sealed class IslandRefugeArrival : MonoBehaviour
    {
        public Vector3 size = new(7, 4, 7);
        void Update()
        {
            if (!GameManager.Instance || !GameManager.Instance.IsPlaying || RunState.Has(IslandCampaign.InnerRefuge)) return;
            var pm = PossessionManager.Instance;
            if (!pm || pm.ActiveBody == null) return;
            if (new Bounds(transform.position + Vector3.up * 2, size).Contains(pm.Active.Position))
                MissionManager.SetFlag(IslandCampaign.InnerRefuge);
        }
    }
}
