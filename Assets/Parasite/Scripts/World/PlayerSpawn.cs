using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    public enum NpcBehaviour { Wander, Patrol, Stationary }

    /// <summary>Where the amoeba starts (storm drain). One per scene.</summary>
    public class PlayerSpawn : MonoBehaviour
    {
        void OnDrawGizmos() { Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.4f, 0.4f); }
    }
}
