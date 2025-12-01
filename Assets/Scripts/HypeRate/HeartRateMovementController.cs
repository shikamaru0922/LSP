using System.Collections.Generic;
using UnityEngine;
using StarterAssets;
using UnityEngine.UI;
namespace LSP.Gameplay
{
    /// <summary>
    /// Drives the player's movement speed directly from heart rate data using the
    /// provided physiological baseline and tuning curve.
    /// </summary>
    public class HeartRateMovementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FirstPersonController firstPersonController;

        [Header("Calibration")]
        [Tooltip("Automatically begins the resting heart rate calibration when the object awakens.")]
        [SerializeField] private bool autoStartCalibration = true;

        [Tooltip("Duration of the calibration window in seconds (samples are taken once per second).")]
        [SerializeField] private float calibrationDurationSeconds = 180f;

        [Tooltip("Initial portion of the calibration window to discard to avoid ramp-up noise.")]
        [SerializeField] private float calibrationDiscardInitialSeconds = 10f;

        [Tooltip("Minimum number of samples required to accept the calibration result.")]
        [SerializeField] private int calibrationMinimumSamples = 30;

        [Header("Heart Rate Targets")]
        [Tooltip("Heart rate must exceed the resting value by this offset to hit the neutral speed.")]
        [SerializeField] private float activationOffsetBpm = 8f;

        [Tooltip("Speed applied when the player's heart rate exactly meets the target.")]
        [SerializeField] private float normalSpeed = 3.5f;

        [Tooltip("Minimum speed applied when the heart rate is well below the target.")]
        [SerializeField] private float minimumSpeed = 0.5f;

        [Tooltip("Maximum speed applied when the heart rate is well above the target.")]
        [SerializeField] private float maximumSpeed = 6f;

        [Tooltip("Heart rate band below the target before speed reaches the minimum.")]
        [SerializeField] private float decelerationRangeBpm = 15f;

        [Tooltip("Heart rate band above the target before speed reaches the maximum.")]
        [SerializeField] private float accelerationRangeBpm = 25f;

        [Header("Low Heart Rate Feedback")]
        [Tooltip("Seconds the player can stay below the target without feedback.")]
        [SerializeField] private float belowTargetGraceSeconds = 5f;

        [Tooltip("Maximum shortfall (bpm) from the target that still triggers the warning overlay after the grace period.")]
        [SerializeField] private float belowTargetWarningBandBpm = 10f;

        [Tooltip("Optional UI object toggled when the player stays below the target.")]
        [SerializeField] private GameObject lowHeartRateWarningUI;

        [Tooltip("Optional overlay object that tints the screen red when below target beyond the grace period.")]
        [SerializeField] private GameObject redTintOverlay;

        [Header("Debug")]
        [Tooltip("Resting heart rate determined from calibration. Can be set manually for testing.")]
        [SerializeField] private float restingHeartRate = 70f;

        [Tooltip("Overrides the resting heart rate when set > 0, skipping calibration.")]
        [SerializeField] private float manualRestingHeartRate;

        [Header("Simulation / Testing")]
        [Tooltip("Always use the simulated heart rate instead of live device input.")]
        [SerializeField] private bool forceSimulatedHeartRate;

        [Tooltip("Heart rate value read when simulation is forced or no device is connected.")]
        [SerializeField] private int simulatedHeartRate = 80;

        [Header("Warning Pulse Settings")]
        [Tooltip("呼吸闪烁的速度")]
        [SerializeField] private float pulseSpeed = 2.0f; 

        [Tooltip("最小不透明度 (0是全透明，1是完全不透明)")]
        [Range(0f, 1f)]
        [SerializeField] private float minAlpha = 0.2f; 

        [Tooltip("最大不透明度")]
        [Range(0f, 1f)]
        [SerializeField] private float maxAlpha = 0.6f;
        
        private Image overlayImage;
        
        
        private readonly List<HeartSample> calibrationSamples = new List<HeartSample>();
        private float calibrationTimer;
        private bool isCalibrating;

        private float sprintToMoveRatio = 1f;
        private float belowTargetTimer;

        private struct HeartSample
        {
            public float Time;
            public int Bpm;
        }

        public float RestingHeartRate => restingHeartRate;
        public float TargetHeartRate => restingHeartRate + activationOffsetBpm;
        public bool IsCalibrating => isCalibrating;

        public float ActivationOffsetBpm
        {
            get => activationOffsetBpm;
            set => activationOffsetBpm = Mathf.Max(0f, value);
        }

        public float NormalSpeed
        {
            get => normalSpeed;
            set => normalSpeed = Mathf.Max(0f, value);
        }

        public float MinimumSpeed
        {
            get => minimumSpeed;
            set => minimumSpeed = Mathf.Max(0f, value);
        }

        public float MaximumSpeed
        {
            get => maximumSpeed;
            set => maximumSpeed = Mathf.Max(0f, value);
        }

        public float DecelerationRangeBpm
        {
            get => decelerationRangeBpm;
            set => decelerationRangeBpm = Mathf.Max(0f, value);
        }

        public float AccelerationRangeBpm
        {
            get => accelerationRangeBpm;
            set => accelerationRangeBpm = Mathf.Max(0f, value);
        }

        public float BelowTargetGraceSeconds
        {
            get => belowTargetGraceSeconds;
            set => belowTargetGraceSeconds = Mathf.Max(0f, value);
        }

        public float BelowTargetWarningBandBpm
        {
            get => belowTargetWarningBandBpm;
            set => belowTargetWarningBandBpm = Mathf.Max(0f, value);
        }

        public float ManualRestingHeartRate
        {
            get => manualRestingHeartRate;
            set => manualRestingHeartRate = Mathf.Max(0f, value);
        }

        public void SetRestingHeartRate(float value)
        {
            restingHeartRate = Mathf.Max(0f, value);
            isCalibrating = false;
        }

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

            if (firstPersonController != null && firstPersonController.MoveSpeed > 0f)
            {
                sprintToMoveRatio = firstPersonController.SprintSpeed / firstPersonController.MoveSpeed;
                normalSpeed = Mathf.Approximately(normalSpeed, 0f) ? firstPersonController.MoveSpeed : normalSpeed;
            }

            if (autoStartCalibration && manualRestingHeartRate <= 0f)
            {
                BeginCalibration();
            }
            else if (manualRestingHeartRate > 0f)
            {
                restingHeartRate = manualRestingHeartRate;
            }
            
            // 2. 尝试获取红色遮罩上的 Image 组件
            if (redTintOverlay != null)
            {
                overlayImage = redTintOverlay.GetComponent<Image>();
                if (overlayImage == null)
                {
                    // 如果直接挂在子物体上，也可以尝试 GetComponentInChildren<Image>();
                    overlayImage = redTintOverlay.GetComponentInChildren<Image>();
                }
            }
        }

        private void Update()
        {
            int currentHeartRate = GetCurrentHeartRate();

            if (isCalibrating)
            {
                UpdateCalibration(Time.deltaTime, currentHeartRate);
            }

            
            
            ApplyMovementSpeed(currentHeartRate);
            UpdateLowHeartRateFeedback(currentHeartRate, Time.deltaTime);
            UpdateWarningPulse();
        }

        // 新增：专门处理呼吸效果的方法
        private void UpdateWarningPulse()
        {
            // 只有当红色遮罩是激活状态，且我们找到了Image组件时，才执行动画
            if (redTintOverlay != null && redTintOverlay.activeSelf && overlayImage != null)
            {
                // 使用 PingPong 算法在 0 到 1 之间循环
                // Lerp 会根据这个循环值，在 minAlpha 和 maxAlpha 之间平滑过渡
                float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
                float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);

                // 获取当前颜色，修改 alpha，再赋值回去
                Color color = overlayImage.color;
                color.a = currentAlpha;
                overlayImage.color = color;
            }
        }
        
        private int GetCurrentHeartRate()
        {
            bool hasLiveDevice = HyperateGlobal.Instance != null && HyperateGlobal.Instance.CurrentHeartRate > 0;

            if (forceSimulatedHeartRate || !hasLiveDevice)
            {
                return Mathf.Max(0, simulatedHeartRate);
            }

            return HyperateGlobal.Instance.CurrentHeartRate;
        }

        public void BeginCalibration()
        {
            calibrationSamples.Clear();
            calibrationTimer = 0f;
            isCalibrating = true;
        }

        private void UpdateCalibration(float deltaTime, int currentHeartRate)
        {
            calibrationTimer += deltaTime;
            float elapsedSeconds = calibrationTimer;

            if (elapsedSeconds >= calibrationDurationSeconds)
            {
                CompleteCalibration();
                return;
            }

            if (currentHeartRate <= 0)
            {
                return;
            }

            if (Mathf.FloorToInt(elapsedSeconds) > calibrationSamples.Count)
            {
                calibrationSamples.Add(new HeartSample { Time = elapsedSeconds, Bpm = currentHeartRate });
            }
        }

        private void CompleteCalibration()
        {
            isCalibrating = false;

            List<int> usableSamples = new List<int>();
            foreach (HeartSample sample in calibrationSamples)
            {
                if (sample.Time >= calibrationDiscardInitialSeconds)
                {
                    usableSamples.Add(sample.Bpm);
                }
            }

            if (usableSamples.Count < calibrationMinimumSamples)
            {
                return;
            }

            usableSamples.Sort();
            if (usableSamples.Count > 2)
            {
                usableSamples.RemoveAt(0);
                usableSamples.RemoveAt(usableSamples.Count - 1);
            }

            float sum = 0f;
            foreach (int bpm in usableSamples)
            {
                sum += bpm;
            }

            restingHeartRate = sum / usableSamples.Count;
        }

        private void ApplyMovementSpeed(int currentHeartRate)
        {
            if (firstPersonController == null)
            {
                return;
            }

            float targetSpeed = CalculateSpeed(currentHeartRate);
            firstPersonController.MoveSpeed = targetSpeed;
            firstPersonController.SprintSpeed = targetSpeed * sprintToMoveRatio;
        }

        private float CalculateSpeed(int currentHeartRate)
        {
            float target = TargetHeartRate;
            float delta = currentHeartRate - target;

            if (Mathf.Approximately(delta, 0f))
            {
                return normalSpeed;
            }

            if (delta < 0f)
            {
                float shortfall = Mathf.Abs(delta);
                float t = decelerationRangeBpm > 0f ? Mathf.Min(1f, shortfall / decelerationRangeBpm) : 1f;
                return Mathf.Lerp(normalSpeed, minimumSpeed, t);
            }

            float climb = accelerationRangeBpm > 0f ? Mathf.Min(1f, delta / accelerationRangeBpm) : 1f;
            return Mathf.Lerp(normalSpeed, maximumSpeed, climb);
        }

        private void UpdateLowHeartRateFeedback(int currentHeartRate, float deltaTime)
        {
            bool belowTarget = currentHeartRate > 0 && currentHeartRate < TargetHeartRate;

            if (belowTarget)
            {
                belowTargetTimer += deltaTime;
            }
            else
            {
                belowTargetTimer = 0f;
                SetWarningActive(false);
                return;
            }

            if (belowTargetTimer <= belowTargetGraceSeconds)
            {
                SetWarningActive(false);
                return;
            }

            float shortfall = TargetHeartRate - currentHeartRate;
            bool withinBand = belowTargetWarningBandBpm <= 0f || shortfall <= belowTargetWarningBandBpm;
            SetWarningActive(withinBand);
        }

        private void SetWarningActive(bool active)
        {
            // 控制文字提示 UI
            if (lowHeartRateWarningUI != null && lowHeartRateWarningUI.activeSelf != active)
            {
                lowHeartRateWarningUI.SetActive(active);
            }

            // 控制红色遮罩 UI
            if (redTintOverlay != null && redTintOverlay.activeSelf != active)
            {
                redTintOverlay.SetActive(active);

                // 可选优化：当刚激活时，重置一下颜色，防止它卡在上一次的状态
                if (active && overlayImage != null)
                {
                    Color c = overlayImage.color;
                    c.a = minAlpha; // 从最淡开始闪烁
                    overlayImage.color = c;
                }
            }
        }
    }
}
