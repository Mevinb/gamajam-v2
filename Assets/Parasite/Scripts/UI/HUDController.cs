using UnityEngine;

namespace Parasite.UI
{
    /// <summary>CONTRACT — in-game HUD. Placeholder; the UI pass replaces this file.</summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }
        public static HUDController Create()
        {
            var go = new GameObject("HUD");
            Instance = go.AddComponent<HUDController>();
            return Instance;
        }
    }
}
