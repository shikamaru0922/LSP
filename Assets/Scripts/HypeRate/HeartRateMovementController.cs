using System;
using UnityEngine;
using StarterAssets;

namespace LSP.Gameplay
{
    /// <summary>
    /// Dynamically adjusts the player's movement speed based on live heart rate data.
    /// Supports two detection strategies that can be swapped at runtime so designers
    /// can compare the behaviours in-game.
    /// </summary>
    public class HeartRateMovementController : MonoBehaviour
    {
        public enum DetectionMode
        {
            MhrPercentage = 0,
            RateOfChangeAndHrv = 1
        }

        [Header("References")]
        [SerializeField] private FirstPersonController firstPersonController;

        [Header("Movement Speeds")]
        [Tooltip("Speed applied while the player is considered to be actively exercising.")]
        [SerializeField] private float activeMovementSpeed = 3.5f;

        [Tooltip("Speed applied when the player is idle/under threshold.")]
        [SerializeField] private float inactiveMovementSpeed = 0.1f;

        [Tooltip("If enabled, the inactive speed is forced to 0 so the player cannot move unless in exercise mode.")]
        [SerializeField] private bool lockMovementWhenInactive;

        [Header("Mode Switching")]
        [Tooltip("Allows switching between detection schemes at runtime without the debug panel.")]
        [SerializeField] private bool enableModeHotkeys = true;

        [Tooltip("Press to toggle between the two detection modes.")]
        [SerializeField] private KeyCode toggleModeKey = KeyCode.Tab;

        [Tooltip("Directly select the MHR% mode.")]
        [SerializeField] private KeyCode selectMhrModeKey = KeyCode.Alpha1;

        [Tooltip("Directly select the HRV + change-rate mode.")]
        [SerializeField] private KeyCode selectHrvModeKey = KeyCode.Alpha2;

        [Header("Detection Mode")]
        [SerializeField] private DetectionMode detectionMode = DetectionMode.MhrPercentage;

        [Header("MHR Percentage Settings")]
        [Tooltip("Player age used to calculate max heart rate.")]
        [SerializeField] private int playerAge = 25;

        [Tooltip("Percentage of MHR required to be considered exercising.")]
        [SerializeField, Range(0.1f, 1f)] private float activationPercent = 0.6f;

        [Tooltip("Time (seconds) that heart rate must stay above the threshold before entering exercise mode.")]
        [SerializeField] private float enterBufferSeconds = 3f;

        [Tooltip("Time (seconds) that heart rate must stay below the threshold minus hysteresis before exiting exercise mode.")]
        [SerializeField] private float exitBufferSeconds = 5f;

        [Tooltip("BPM offset applied below the threshold to create hysteresis and avoid rapid toggling.")]
        [SerializeField] private float hysteresisBpm = 5f;

        [Header("Rate of Change & HRV Settings")]
        [Tooltip("Delta BPM per second required to detect a burst of activity.")]
        [SerializeField] private float slopeThresholdBpmPerSecond = 2f;

        [Tooltip("Heart rate gate applied alongside low HRV to confirm high sympathetic tone.")]
        [SerializeField] private int hrvHeartRateGate = 100;

        [Tooltip("Baseline HRV in milliseconds gathered when the player is resting.")]
        [SerializeField] private float baselineHrvMs = 120f;

        [Tooltip("Multiplier applied to the baseline to determine the low HRV threshold.")]
        [SerializeField, Range(0.1f, 1f)] private float hrvDropMultiplier = 0.6f;

        [Tooltip("Latest HRV reading in milliseconds. This can be updated from an external sensor script.")]
        [SerializeField] private float currentHrvMs = 120f;

        private float enterTimer;
        private float exitTimer;
        private bool isExerciseActive;
        private float sprintToMoveRatio = 1f;

        private int lastHeartRate;
        private float lastHeartRateSampleTime;

        public DetectionMode CurrentDetectionMode
        {
            get => detectionMode;
            set
            {
                if (detectionMode != value)
                {
                    detectionMode = value;
                    ResetDetectionState();
                }
            }
        }

        public float ActiveMovementSpeed
        {
            get => activeMovementSpeed;
            set => activeMovementSpeed = Mathf.Max(0f, value);
        }

        public float InactiveMovementSpeed
        {
            get => inactiveMovementSpeed;
            set => inactiveMovementSpeed = Mathf.Max(0f, value);
        }

        public bool LockMovementWhenInactive
        {
            get => lockMovementWhenInactive;
            set => lockMovementWhenInactive = value;
        }

        public int PlayerAge
        {
            get => playerAge;
            set => playerAge = Mathf.Max(1, value);
        }

        public float ActivationPercent
        {
            get => activationPercent;
            set => activationPercent = Mathf.Clamp01(value);
        }

        public float BaselineHrvMs
        {
            get => baselineHrvMs;
            set => baselineHrvMs = Mathf.Max(0f, value);
        }

        public float CurrentHrvMs
        {
            get => currentHrvMs;
            set => currentHrvMs = Mathf.Max(0f, value);
        }

        public float EnterBufferSeconds
        {
            get => enterBufferSeconds;
            set => enterBufferSeconds = Mathf.Max(0f, value);
        }

        public float ExitBufferSeconds
        {
            get => exitBufferSeconds;
            set => exitBufferSeconds = Mathf.Max(0f, value);
        }

        public float HysteresisBpm
        {
            get => hysteresisBpm;
            set => hysteresisBpm = Mathf.Max(0f, value);
        }

        public float SlopeThresholdBpmPerSecond
        {
            get => slopeThresholdBpmPerSecond;
            set => slopeThresholdBpmPerSecond = Mathf.Max(0f, value);
        }

        public int HrvHeartRateGate
        {
            get => hrvHeartRateGate;
            set => hrvHeartRateGate = Mathf.Max(0, value);
        }

        public float HrvDropMultiplier
        {
            get => hrvDropMultiplier;
            set => hrvDropMultiplier = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Exposes whether the controller believes the player is in an active exercise state.
        /// </summary>
        public bool IsExerciseActive => isExerciseActive;

        private void Reset()
        {
            firstPersonController = GetComponent<FirstPersonController>();
        }

        private void Awake()
        {
            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (firstPersonController != null)
            {
                sprintToMoveRatio = firstPersonController.MoveSpeed > 0f
                    ? firstPersonController.SprintSpeed / firstPersonController.MoveSpeed
                    : 1f;

                if (Mathf.Approximately(activeMovementSpeed, 0f))
                {
                    activeMovementSpeed = firstPersonController.MoveSpeed;
                }
            }

            ResetDetectionState();
        }

        private void Update()
        {
            if (firstPersonController == null)
            {
                return;
            }

            HandleModeHotkeys();

            int currentHeartRate = HyperateGlobal.Instance != null ? HyperateGlobal.Instance.CurrentHeartRate : 0;
            bool newExerciseState = detectionMode switch
            {
                DetectionMode.RateOfChangeAndHrv => EvaluateRateOfChange(currentHeartRate),
                _ => EvaluateMhrPercentage(currentHeartRate)
            };

            if (newExerciseState != isExerciseActive)
            {
                isExerciseActive = newExerciseState;
            }

            ApplyMovementSpeed();
        }

        private void HandleModeHotkeys()
        {
            if (!enableModeHotkeys)
            {
                return;
            }

            if (Input.GetKeyDown(toggleModeKey))
            {
                CycleDetectionMode();
                return;
            }

            if (Input.GetKeyDown(selectMhrModeKey))
            {
                CurrentDetectionMode = DetectionMode.MhrPercentage;
                return;
            }

            if (Input.GetKeyDown(selectHrvModeKey))
            {
                CurrentDetectionMode = DetectionMode.RateOfChangeAndHrv;
            }
        }

        private void CycleDetectionMode()
        {
            CurrentDetectionMode = detectionMode == DetectionMode.MhrPercentage
                ? DetectionMode.RateOfChangeAndHrv
                : DetectionMode.MhrPercentage;
        }

        private void ApplyMovementSpeed()
        {
            float targetSpeed = isExerciseActive ? activeMovementSpeed : (lockMovementWhenInactive ? 0f : inactiveMovementSpeed);
            firstPersonController.MoveSpeed = targetSpeed;
            firstPersonController.SprintSpeed = targetSpeed * sprintToMoveRatio;
        }

        public void ResetDetectionState()
        {
            enterTimer = 0f;
            exitTimer = 0f;
            lastHeartRate = 0;
            lastHeartRateSampleTime = 0f;
            isExerciseActive = false;
        }

        private bool EvaluateMhrPercentage(int currentHeartRate)
        {
            int maxHeartRate = Mathf.Max(1, 220 - PlayerAge);
            float targetValue = maxHeartRate * ActivationPercent;

            if (currentHeartRate > targetValue)
            {
                enterTimer += Time.deltaTime;
                exitTimer = 0f;
                if (enterTimer >= enterBufferSeconds)
                {
                    return true;
                }
            }
            else if (currentHeartRate < targetValue - hysteresisBpm)
            {
                exitTimer += Time.deltaTime;
                enterTimer = 0f;
                if (exitTimer >= exitBufferSeconds)
                {
                    return false;
                }
            }
            else
            {
                enterTimer = 0f;
                exitTimer = 0f;
            }

            return isExerciseActive;
        }

        private bool EvaluateRateOfChange(int currentHeartRate)
        {
            bool slopeTriggered = false;
            float now = Time.time;

            if (lastHeartRateSampleTime > 0f)
            {
                float deltaTime = Mathf.Max(Time.deltaTime, now - lastHeartRateSampleTime);
                float slope = (currentHeartRate - lastHeartRate) / deltaTime;
                slopeTriggered = slope > slopeThresholdBpmPerSecond;
            }

            lastHeartRate = currentHeartRate;
            lastHeartRateSampleTime = now;

            bool hasBaseline = baselineHrvMs > 0f && currentHrvMs > 0f;
            bool lowHrv = hasBaseline && currentHrvMs < baselineHrvMs * hrvDropMultiplier;
            bool sympatheticHigh = currentHeartRate > hrvHeartRateGate && lowHrv;

            return slopeTriggered || sympatheticHigh;
        }
    }
}
