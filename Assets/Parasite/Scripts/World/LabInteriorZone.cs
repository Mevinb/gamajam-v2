using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>The lab interior. Entering marks the mission step and sets a checkpoint.</summary>
    public class LabInteriorZone : ZoneVolume
    {
        public static readonly List<LabInteriorZone> All = new();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
        protected override Color GizmoColor => new(0.3f, 0.6f, 1f, 0.10f);
        public static bool InAny(Vector3 p) { foreach (var z in All) if (z && z.Contains(p)) return true; return false; }
    }
}
