using Parasite.Missions;
using Parasite.World;
using UnityEngine;
using UnityEngine.AI;

namespace Parasite.Characters
{
    public static class HostFactory
    {
        /// <summary>Build a human from a spawn marker. NavMesh must already exist.</summary>
        public static HostCharacter Create(NpcSpawn spawn)
        {
            var go = new GameObject($"NPC_{spawn.displayName}") { layer = Layers.Characters };
            Vector3 pos = spawn.transform.position;
            if (NavMesh.SamplePosition(pos, out var hit, 4f, NavMesh.AllAreas)) pos = hit.position;
            go.transform.SetPositionAndRotation(pos, spawn.transform.rotation);
            go.AddComponent<CharacterController>();
            var nav = go.AddComponent<NavMeshAgent>();
            HostBrain brain = spawn.profession == Profession.Guard && !spawn.isDrVale ? go.AddComponent<GuardAI>() : go.AddComponent<HostBrain>();
            var host = go.AddComponent<HostCharacter>();
            host.Init(spawn.displayName, spawn.profession, spawn.isDrVale, spawn.seed);
            brain.Setup(host, spawn);
            if (spawn.isDrVale)
            {
                brain.behaviour = NpcBehaviour.Stationary;
                go.AddComponent<DrValeInteraction>();
            }
            if (!nav.isOnNavMesh) nav.Warp(pos);
            return host;
        }
    }
}
