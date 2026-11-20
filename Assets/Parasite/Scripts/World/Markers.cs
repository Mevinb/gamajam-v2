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

    /// <summary>Spawns one human at runtime (built by HostFactory).</summary>
    public class NpcSpawn : MonoBehaviour
    {
        public Profession profession = Profession.Civilian;
        public string displayName = "Pedestrian";
        public NpcBehaviour behaviour = NpcBehaviour.Wander;
        public PatrolRoute route;
        public float wanderRadius = 14f;
        public bool isDrVale;
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

    /// <summary>Entering with the active body saves a checkpoint here.</summary>
    public class CheckpointZone : ZoneVolume
    {
        public static readonly List<CheckpointZone> All = new();
        public string checkpointName = "Checkpoint";
        public Transform respawnPoint;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        public Vector3 RespawnPosition => respawnPoint ? respawnPoint.position : transform.position;
        protected override Color GizmoColor => new(0.2f, 1f, 0.2f, 0.12f);
    }

    /// <summary>Being seen inside without the permission counts as trespassing.</summary>
    public class RestrictedZone : ZoneVolume
    {
        public static readonly List<RestrictedZone> All = new();
        public string zoneName = "Restricted Area";
        public AccessPermission required = AccessPermission.LabInterior;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        protected override Color GizmoColor => new(1f, 0.2f, 0.2f, 0.10f);

        public static RestrictedZone At(Vector3 p)
        {
            foreach (var z in All) if (z && z.Contains(p)) return z;
            return null;
        }
    }

    /// <summary>The lab interior. Entering marks the mission step and sets a checkpoint.</summary>
    public class LabInteriorZone : ZoneVolume
    {
        public static readonly List<LabInteriorZone> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        protected override Color GizmoColor => new(0.3f, 0.6f, 1f, 0.10f);
        public static bool InAny(Vector3 p) { foreach (var z in All) if (z && z.Contains(p)) return true; return false; }
    }

    /// <summary>Optional text sign rendered as world-space label hint for players (e.g. "MAIN GATE").</summary>
    public class SignLabel : MonoBehaviour
    {
        public string text = "SIGN";
        public Color color = Color.white;
    }
}
