using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>Waypoints are the child transforms, in order.</summary>
    public class PatrolRoute : MonoBehaviour
    {
        public bool loop = true;
        public float waitAtPoint = 1.5f;

        public int Count => transform.childCount;
        public Vector3 Point(int i) => transform.GetChild(Mathf.Clamp(i, 0, transform.childCount - 1)).position;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < transform.childCount; i++)
            {
                var a = transform.GetChild(i).position;
                Gizmos.DrawSphere(a, 0.25f);
                if (i + 1 < transform.childCount) Gizmos.DrawLine(a, transform.GetChild(i + 1).position);
                else if (loop && transform.childCount > 1) Gizmos.DrawLine(a, transform.GetChild(0).position);
            }
        }
    }
}
