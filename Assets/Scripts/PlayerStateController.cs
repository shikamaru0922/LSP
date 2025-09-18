using System;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Holds core player attributes such as health state and movement speed.
    /// This script intentionally keeps the data minimal so other systems
    /// (movement, vision, etc.) can query the current player status.
    /// </summary>
    public class PlayerStateController : MonoBehaviour
    {
        [Header("Attributes")]
        [Tooltip("Base movement speed used by the PlayerMovement component.")]
        [SerializeField]
        private float movementSpeed = 3.5f;

        public bool IsAlive { get; private set; } = true;

        /// <summary>
        /// The speed value that should be used by movement scripts.
        /// </summary>
        public float MovementSpeed => movementSpeed;

        public event Action PlayerKilled;

        /// <summary>
        /// Kills the player. Subsequent calls are ignored to avoid multiple death events.
        /// </summary>
        public void Kill()
        {
            if (!IsAlive)
            {
                return;
            }

            IsAlive = false;
            PlayerKilled?.Invoke();
        }

        /// <summary>
        /// Resets the player to a living state. This can be used by restart logic.
        /// </summary>
        public void Revive()
        {
            IsAlive = true;
        }
    }
}
