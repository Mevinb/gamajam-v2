using System.Collections;
using Parasite.Characters;
using Parasite.World;
using UnityEngine;

namespace Parasite.Missions
{
    /// <summary>
    /// Spawns mission-specific NPCs through the normal <see cref="HostFactory"/> path when their mission begins, so they
    /// never change earlier chapters (or the order of the city's original population).
    /// </summary>
    public static class MissionSpawner
    {
        public static HostCharacter Spawn(string displayName, Profession profession, Vector3 position, float yaw, int seed,
            NpcBehaviour behaviour = NpcBehaviour.Stationary, PatrolRoute route = null, float wanderRadius = 3f)
        {
            var marker = new GameObject("MissionSpawn_" + displayName.Replace(' ', '_'));
            marker.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var spawn = marker.AddComponent<NpcSpawn>();
            spawn.displayName = displayName; spawn.profession = profession; spawn.behaviour = behaviour;
            spawn.route = route; spawn.wanderRadius = wanderRadius; spawn.seed = seed;
            var host = HostFactory.Create(spawn);
            Object.Destroy(marker);
            return host;
        }

        public static HostCharacter Find(string displayName)
        {
            foreach (var h in HostCharacter.All)
                if (h && h.DisplayName == displayName) return h;
            return null;
        }
    }
}
