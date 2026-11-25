using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Parasite.UI
{
    /// <summary>Use ordinary UI navigation events to adjust a focused choice without moving away from it.</summary>
    [DisallowMultipleComponent]
    public sealed class SettingsCycleControl : MonoBehaviour, IMoveHandler
    {
        Action<int> change;
        public void Initialize(Action<int> callback) => change = callback;
        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir != MoveDirection.Left && eventData.moveDir != MoveDirection.Right) return;
            change?.Invoke(eventData.moveDir == MoveDirection.Left ? -1 : 1);
            eventData.Use();
        }
    }
}
