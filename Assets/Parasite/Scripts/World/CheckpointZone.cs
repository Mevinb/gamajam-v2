using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
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
}
