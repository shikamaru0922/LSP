using System;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Provides global coordination for major world events such as the "world abnormal" trigger.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private static GameManager instance;

        /// <summary>
        /// Raised whenever the world abnormal state changes. Argument is the new state value.
        /// </summary>
        public static event Action<bool> WorldAbnormalStateChanged;

        [SerializeField]
        [Tooltip("Controls whether the world abnormal event is active. Can be toggled at runtime for testing.")]
        private bool worldAbnormal;

        private bool isWorldAbnormal;

        public static GameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameManager>();
                }

                return instance;
            }
        }

        public bool IsWorldAbnormal => isWorldAbnormal;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Multiple GameManager instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this;
            SetWorldAbnormalState(worldAbnormal, true);
        }

        /// <summary>
        /// Sets the world abnormal state and notifies listeners if it changed.
        /// </summary>
        public void SetWorldAbnormalState(bool value)
        {
            SetWorldAbnormalState(value, false);
        }

        /// <summary>
        /// Convenience helper used by external scripts to trigger the abnormal event.
        /// </summary>
        public void TriggerWorldAbnormalEvent()
        {
            SetWorldAbnormalState(true);
        }

        private void SetWorldAbnormalState(bool value, bool forceNotify)
        {
            if (isWorldAbnormal == value && !forceNotify)
            {
                return;
            }

            isWorldAbnormal = value;
            worldAbnormal = value;
            WorldAbnormalStateChanged?.Invoke(isWorldAbnormal);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SetWorldAbnormalState(worldAbnormal, false);
        }
#endif

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
