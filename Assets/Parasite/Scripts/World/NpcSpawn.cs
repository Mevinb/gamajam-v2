using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>Spawns one human at runtime (built by HostFactory).</summary>
    public class NpcSpawn : MonoBehaviour
    {
        public Profession profession = Profession.Civilian;
        public string displayName = "Pedestrian";
        public NpcBehaviour behaviour = NpcBehaviour.Wander;
        public PatrolRoute route;
        public float wanderRadius = 14f;
        public bool isDrVale;
        public bool reportsPlayerViolations = true;
        public int seed = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = profession switch
            {
                Profession.Guard => Color.blue, Profession.Scientist => Color.white,
                Profession.Maintenance => new Color(1f, 0.5f, 0f), _ => Color.gray
            };
            if (isDrVale) Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
            Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
        }
    }
}
