using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Raised when the recorded disabler fragment count for a given identifier changes.
        /// </summary>
        public static event Action<string, int> DisablerFragmentCountChanged;

        [SerializeField]
        [Tooltip("Controls whether the world abnormal event is active. Can be toggled at runtime for testing.")]
        private bool worldAbnormal;

        private bool isWorldAbnormal;
        private readonly Dictionary<string, int> disablerFragments = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> DisablerFragments => disablerFragments;

        public int TotalDisablerFragments { get; private set; }

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
        /// Records a disabler fragment pickup so UI systems can display totals.
        /// </summary>
        /// <param name="fragmentId">Identifier representing the fragment source/type.</param>
        /// <param name="amount">Number of fragments acquired.</param>
        public void RegisterDisablerFragmentPickup(string fragmentId, int amount)
        {
            if (string.IsNullOrEmpty(fragmentId) || amount <= 0)
            {
                return;
            }

            if (disablerFragments.TryGetValue(fragmentId, out int current))
            {
                disablerFragments[fragmentId] = current + amount;
            }
            else
            {
                disablerFragments[fragmentId] = amount;
            }

            TotalDisablerFragments += amount;
            DisablerFragmentCountChanged?.Invoke(fragmentId, disablerFragments[fragmentId]);
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

        /// <summary>
        /// Toggle helper: flip current abnormal state.
        /// </summary>
        public void ToggleWorldAbnormal()
        {
            SetWorldAbnormalState(!isWorldAbnormal);
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

        private void Update()
        {
            // 按一次 O 就切换一次状态
            if (Input.GetKeyDown(KeyCode.O))
            {
                ToggleWorldAbnormal();
            }
        }
    }
}
