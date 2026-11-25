using UnityEngine;
namespace Parasite.Missions
{
    public sealed class IslandOwnedMaterial : MonoBehaviour
    {
        public Material material;
        void OnDestroy(){if(material)Destroy(material);}
    }
}
