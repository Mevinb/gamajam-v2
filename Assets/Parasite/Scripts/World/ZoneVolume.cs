using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>Axis-aligned volume (uses the GameObject's BoxCollider-free bounds: position + size). Polled, not trigger-based.</summary>
    public abstract class ZoneVolume : MonoBehaviour
    {
        public Vector3 size = new(10, 4, 10);
        public Bounds WorldBounds => new(transform.position + Vector3.up * size.y * 0.5f, size);
        public bool Contains(Vector3 p) => WorldBounds.Contains(p);
        protected virtual Color GizmoColor => new(0, 1, 0, 0.15f);
        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor; var b = WorldBounds; Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = new Color(GizmoColor.r, GizmoColor.g, GizmoColor.b, 0.8f); Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
