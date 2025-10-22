using System;
using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// Manages the player's eye wetness resource, supporting manual blinking and forced closing.
    /// </summary>
    public class PlayerEyeControl : MonoBehaviour
    {
        public enum BlinkType
        {
            Manual,
            Forced
        }

        [Header("Wetness Settings")]
        [SerializeField]
        private float maximumWetness = 5f;

        [SerializeField]
        private float dryingRate = 1f;

        [SerializeField]
        private float recoveryRate = 2f;

        [Tooltip("Wetness restored instantly whenever the player blinks manually.")]
        [SerializeField]
        private float restoreWetnessPerManualBlink = 1.5f;

        [Header("Blink Durations")]
        [Tooltip("Seconds the screen remains closed during a forced blink.")]
        [SerializeField]
        private float forcedBlinkDuration = 2f;

        [Tooltip("Seconds the screen remains closed during a manual blink.")]
        [SerializeField]
        private float manualBlinkDuration = 0.5f;

        [Header("Input")]
        [Tooltip("Mouse button used to trigger a manual blink.")]
        [SerializeField]
        private int manualBlinkMouseButton = 0;

        private float currentWetness;
        private float forcedBlinkTimer;
        private float manualBlinkTimer;
        private bool eyesOpen = true;

        public float CurrentWetness => currentWetness;

        public float MaximumWetness
        {
            get => maximumWetness;
            set
            {
                maximumWetness = Mathf.Max(0.01f, value);
                currentWetness = Mathf.Clamp(currentWetness, 0f, maximumWetness);
            }
        }

        public float DryingRate
        {
            get => dryingRate;
            set => dryingRate = Mathf.Max(0f, value);
        }

        public float RecoveryRate
        {
            get => recoveryRate;
            set => recoveryRate = Mathf.Max(0f, value);
        }

        public float RestoreWetnessPerManualBlink
        {
            get => restoreWetnessPerManualBlink;
            set => restoreWetnessPerManualBlink = Mathf.Max(0f, value);
        }

        public float ForcedBlinkDuration
        {
            get => forcedBlinkDuration;
            set => forcedBlinkDuration = Mathf.Max(0f, value);
        }

        public float ManualBlinkDuration
        {
            get => manualBlinkDuration;
            set => manualBlinkDuration = Mathf.Max(0f, value);
        }

        public bool EyesOpen => eyesOpen;
        public bool IsForcedClosing => forcedBlinkTimer > 0f;
        public bool IsManualBlinking => manualBlinkTimer > 0f;
        public bool IsBlinking => IsForcedClosing || IsManualBlinking;

        public event Action EyesForcedClosed;
        public event Action EyesForcedOpened;
        public event Action<BlinkType, float> BlinkStarted;
        public event Action<BlinkType> BlinkEnded;

        private void Awake()
        {
            currentWetness = Mathf.Clamp(currentWetness <= 0f ? maximumWetness : currentWetness, 0f, maximumWetness);
            eyesOpen = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateBlinkTimers(deltaTime);
            HandleInput();
            UpdateWetness(deltaTime);
        }

        private void HandleInput()
        {
            if (IsForcedClosing || IsManualBlinking)
            {
                return;
            }

            if (Input.GetMouseButtonDown(manualBlinkMouseButton))
            {
                BeginManualBlink();
            }
        }

        private void UpdateBlinkTimers(float deltaTime)
        {
            if (manualBlinkTimer > 0f)
            {
                manualBlinkTimer -= deltaTime;
                if (manualBlinkTimer <= 0f)
                {
                    manualBlinkTimer = 0f;
                    if (!IsForcedClosing)
                    {
                        EndManualBlink();
                    }
                }
            }

            if (forcedBlinkTimer > 0f)
            {
                forcedBlinkTimer -= deltaTime;
                if (forcedBlinkTimer <= 0f)
                {
                    forcedBlinkTimer = 0f;
                    EndForcedBlink();
                }
            }
        }

        private void UpdateWetness(float deltaTime)
        {
            if (EyesOpen)
            {
                currentWetness -= dryingRate * deltaTime;
                if (currentWetness <= 0f)
                {
                    currentWetness = 0f;
                    BeginForcedBlink();
                }
            }
            else
            {
                currentWetness += recoveryRate * deltaTime;
            }

            currentWetness = Mathf.Clamp(currentWetness, 0f, maximumWetness);
        }

        private void BeginManualBlink()
        {
            manualBlinkTimer = manualBlinkDuration;
            eyesOpen = false;
            currentWetness = Mathf.Clamp(currentWetness + restoreWetnessPerManualBlink, 0f, maximumWetness);
            BlinkStarted?.Invoke(BlinkType.Manual, manualBlinkDuration);
        }

        private void EndManualBlink()
        {
            eyesOpen = !IsForcedClosing;
            BlinkEnded?.Invoke(BlinkType.Manual);
        }

        private void BeginForcedBlink()
        {
            if (IsForcedClosing)
            {
                return;
            }

            forcedBlinkTimer = forcedBlinkDuration;
            eyesOpen = false;
            EyesForcedClosed?.Invoke();
            BlinkStarted?.Invoke(BlinkType.Forced, forcedBlinkDuration);
        }

        private void EndForcedBlink()
        {
            if (!IsManualBlinking)
            {
                eyesOpen = true;
                BlinkEnded?.Invoke(BlinkType.Forced);
            }
            else
            {
                eyesOpen = false;
            }

            EyesForcedOpened?.Invoke();
        }

        /// <summary>
        /// Instantly restores the wetness resource. Useful when restarting a level.
        /// </summary>
        public void ResetWetness()
        {
            currentWetness = maximumWetness;
            forcedBlinkTimer = 0f;
            manualBlinkTimer = 0f;
            eyesOpen = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumWetness = Mathf.Max(0.01f, maximumWetness);
            dryingRate = Mathf.Max(0f, dryingRate);
            recoveryRate = Mathf.Max(0f, recoveryRate);
            restoreWetnessPerManualBlink = Mathf.Max(0f, restoreWetnessPerManualBlink);
            forcedBlinkDuration = Mathf.Max(0f, forcedBlinkDuration);
            manualBlinkDuration = Mathf.Max(0f, manualBlinkDuration);
        }
#endif
    }
}
