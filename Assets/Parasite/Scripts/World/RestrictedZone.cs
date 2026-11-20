using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
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
}
