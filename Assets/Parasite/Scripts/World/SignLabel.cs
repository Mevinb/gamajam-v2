using System.Collections.Generic;
using UnityEngine;

namespace Parasite.World
{
    /// <summary>Optional text sign rendered as world-space label hint for players (e.g. "MAIN GATE").</summary>
    public class SignLabel : MonoBehaviour
    {
        public string text = "SIGN";
        public Color color = Color.white;
    }
}
