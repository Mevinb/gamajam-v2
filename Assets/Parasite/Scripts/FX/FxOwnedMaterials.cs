using UnityEngine;
using UnityEngine.Rendering;

namespace Parasite.FX
{
    /// <summary>Destroys runtime material instances owned by a GameObject.</summary>
    internal class FxOwnedMaterials : MonoBehaviour
    {
        public Material[] mats;
        void OnDestroy() { if (mats != null) foreach (var m in mats) if (m) Destroy(m); }
    }
}
