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
            Forced,
            Warning
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
        [Tooltip("Key used to trigger a manual blink.")]
        [SerializeField]
        private KeyCode manualBlinkKey = KeyCode.Space;

        [Header("Low Wetness Warning")]
        [Tooltip("Fraction of the maximum wetness that triggers the warning blink behaviour.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float warningWetnessThresholdFraction = 0.25f;

        [Tooltip("How many quick flashes play while wetness is low.")]
        [SerializeField]
        private int warningBlinkFlashCount = 3;

        [Tooltip("Seconds spent darkening the screen for each low wetness warning flash.")]
        [SerializeField]
        private float warningBlinkDarkenDuration = 0.08f;

        [Tooltip("Seconds spent brightening the screen after each warning flash.")]
        [SerializeField]
        private float warningBlinkLightenDuration = 0.08f;

        [Tooltip("Delay between warning blink sequences while the wetness remains below the threshold.")]
        [SerializeField]
        private float warningBlinkCooldown = 4f;

        private float currentWetness;
        private float forcedBlinkTimer;
        private float manualBlinkTimer;
        private float warningBlinkTimer;
        private float warningBlinkCooldownTimer;
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
        public bool IsWarningBlinking => warningBlinkTimer > 0f;
        public bool IsBlinking => IsForcedClosing || IsManualBlinking || IsWarningBlinking;

        public int WarningBlinkFlashCount
        {
            get => Mathf.Max(1, warningBlinkFlashCount);
            set => warningBlinkFlashCount = Mathf.Max(1, value);
        }

        public float WarningBlinkDarkenDuration
        {
            get => Mathf.Max(0f, warningBlinkDarkenDuration);
            set => warningBlinkDarkenDuration = Mathf.Max(0f, value);
        }

        public float WarningBlinkLightenDuration
        {
            get => Mathf.Max(0f, warningBlinkLightenDuration);
            set => warningBlinkLightenDuration = Mathf.Max(0f, value);
        }

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
            UpdateWarningBlink(deltaTime);
        }

        private void HandleInput()
        {
            if (IsForcedClosing || IsManualBlinking)
            {
                return;
            }

            if (Input.GetKeyDown(manualBlinkKey))
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

        private void UpdateWarningBlink(float deltaTime)
        {
            if (warningBlinkCooldownTimer > 0f)
            {
                warningBlinkCooldownTimer -= deltaTime;
                if (warningBlinkCooldownTimer < 0f)
                {
                    warningBlinkCooldownTimer = 0f;
                }
            }

            if (warningBlinkTimer > 0f)
            {
                warningBlinkTimer -= deltaTime;
                if (warningBlinkTimer <= 0f)
                {
                    warningBlinkTimer = 0f;
                    EndWarningBlink();
                }

                return;
            }

            if (IsForcedClosing || IsManualBlinking)
            {
                CancelWarningBlink(false);
                return;
            }

            bool lowWetness = maximumWetness > Mathf.Epsilon && currentWetness <= maximumWetness * warningWetnessThresholdFraction;
            if (!lowWetness)
            {
                CancelWarningBlink(true);
                warningBlinkCooldownTimer = 0f;
                return;
            }

            if (warningBlinkCooldownTimer > 0f)
            {
                return;
            }

            BeginWarningBlink();
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
            CancelWarningBlink(false);
            warningBlinkCooldownTimer = warningBlinkCooldown;
            manualBlinkTimer = manualBlinkDuration;
            eyesOpen = false;
            var allBlindObjects = FindObjectsOfType<BlinkObject>();
        
            foreach (var obj in allBlindObjects)
            {
                obj.SetBlindMode(!eyesOpen);
            }
            currentWetness = Mathf.Clamp(currentWetness + restoreWetnessPerManualBlink, 0f, maximumWetness);
            BlinkStarted?.Invoke(BlinkType.Manual, manualBlinkDuration);
        }

        private void EndManualBlink()
        {
            eyesOpen = !IsForcedClosing && !IsWarningBlinking;
            BlinkEnded?.Invoke(BlinkType.Manual);
        }

        private void BeginForcedBlink()
        {
            if (IsForcedClosing)
            {
                return;
            }

            CancelWarningBlink(false);
            warningBlinkCooldownTimer = warningBlinkCooldown;
            forcedBlinkTimer = forcedBlinkDuration;
            eyesOpen = false;
            EyesForcedClosed?.Invoke();
            BlinkStarted?.Invoke(BlinkType.Forced, forcedBlinkDuration);
        }

        private void EndForcedBlink()
        {
            if (!IsManualBlinking && !IsWarningBlinking)
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

        private void BeginWarningBlink()
        {
            float duration = GetWarningBlinkDuration();
            warningBlinkTimer = duration;
            BlinkStarted?.Invoke(BlinkType.Warning, duration);

            if (warningBlinkTimer <= 0f)
            {
                EndWarningBlink();
            }
        }

        private void EndWarningBlink()
        {
            warningBlinkCooldownTimer = warningBlinkCooldown;

            BlinkEnded?.Invoke(BlinkType.Warning);
        }

        private void CancelWarningBlink(bool notify)
        {
            if (warningBlinkTimer <= 0f)
            {
                return;
            }

            warningBlinkTimer = 0f;

            if (notify)
            {
                BlinkEnded?.Invoke(BlinkType.Warning);
            }
        }

        /// <summary>
        /// Instantly restores the wetness resource. Useful when restarting a level.
        /// </summary>
        public void ResetWetness()
        {
            CancelWarningBlink(true);
            currentWetness = maximumWetness;
            forcedBlinkTimer = 0f;
            manualBlinkTimer = 0f;
            warningBlinkTimer = 0f;
            warningBlinkCooldownTimer = 0f;
            eyesOpen = true;
        }

        private float GetWarningBlinkDuration()
        {
            int flashes = Mathf.Max(1, warningBlinkFlashCount);
            float darken = Mathf.Max(0f, warningBlinkDarkenDuration);
            float lighten = Mathf.Max(0f, warningBlinkLightenDuration);

            return flashes * (darken + lighten);
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
            warningWetnessThresholdFraction = Mathf.Clamp01(warningWetnessThresholdFraction);
            warningBlinkFlashCount = Mathf.Max(1, warningBlinkFlashCount);
            warningBlinkDarkenDuration = Mathf.Max(0f, warningBlinkDarkenDuration);
            warningBlinkLightenDuration = Mathf.Max(0f, warningBlinkLightenDuration);
            warningBlinkCooldown = Mathf.Max(0f, warningBlinkCooldown);
        }
#endif
    }
}
