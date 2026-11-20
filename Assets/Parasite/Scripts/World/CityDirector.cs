using Parasite.Characters;
using Parasite.Missions;
using Parasite.Possession;
using Parasite.Stealth;
using Unity.AI.Navigation;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>
    /// The only object the City scene needs to boot gameplay: creates managers, bakes the NavMesh,
    /// spawns NPCs from markers, spawns the amoeba (checkpoint aware), camera, HUD and ambience.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CityDirector : MonoBehaviour
    {
        public static CityDirector Instance { get; private set; }
        public NavMeshSurface Surface { get; private set; }
        public bool Ready { get; private set; }

        void Awake()
        {
            Instance = this;
            GameEvents.ClearAll();
            GameInput.Ensure();
            GameInput.Simulated = false;
            HostCharacter.All.RemoveAll(h => !h);
            Interactables.All.RemoveAll(i => i == null || (i is Object o && !o));

            gameObject.AddComponent<GameManager>();
            gameObject.AddComponent<SuspicionSystem>();
            gameObject.AddComponent<MissionManager>();
            gameObject.AddComponent<PossessionManager>();

            BakeNavMesh();
            foreach (var s in FindObjectsByType<NpcSpawn>(FindObjectsSortMode.None)) HostFactory.Create(s);

            PlayerCamera.Create();

            var spawn = FindFirstObjectByType<PlayerSpawn>();
            Vector3 p = spawn ? spawn.transform.position : Vector3.up;
            float yaw = spawn ? spawn.transform.eulerAngles.y : 0f;
            if (RunState.HasCheckpoint) p = RunState.CheckpointPosition;
            else if (spawn) GameManager.Instance.SetCheckpoint(p, "Storm Drain");
            PossessionManager.Instance.SpawnInitial(p + Vector3.up * 0.1f, yaw);

            UI.HUDController.Create();
            FX.Sfx.StartAmbience();
            Ready = true;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void BakeNavMesh()
        {
            Surface = FindFirstObjectByType<NavMeshSurface>();
            if (!Surface)
            {
                var root = GameObject.Find("World");
                Surface = (root ? root : gameObject).AddComponent<NavMeshSurface>();
                Surface.collectObjects = root ? CollectObjects.Children : CollectObjects.All;
            }
            Surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            Surface.layerMask = Layers.EnvironmentMask;
            Surface.BuildNavMesh();
        }
    }
}
