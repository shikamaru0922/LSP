using System.Collections;
using LSP.Gameplay;
using StarterAssets;
using UnityEngine;

/// <summary>
/// Plays a simple washing animation for a cylindrical item when triggered by interaction.
/// During playback the player's movement and camera look are temporarily locked.
/// </summary>
[DisallowMultipleComponent]
public class WaterDispenserWashSequence : MonoBehaviour
{
    private enum RotationAxisMode
    {
        X = 0,
        Y = 1,
        Z = 2,
        Custom = 3
    }

    [Header("Wash Target")]
    [Tooltip("要被清洗演出的物体。建议拖入一个柱状物体。")]
    [SerializeField] private Transform washTarget;

    [Header("Prefab Target (Optional)")]
    [Tooltip("如果没有指定 washTarget，则会优先实例化这个 Prefab 作为清洗目标。")]
    [SerializeField] private GameObject washTargetPrefab;

    [SerializeField] private Transform washTargetSpawnAnchor;
    [SerializeField] private Vector3 prefabLocalPosition = new Vector3(0f, 0.35f, 0.2f);
    [SerializeField] private Vector3 prefabLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 prefabLocalScale = Vector3.one;

    [Tooltip("实例化 Prefab 后自动关闭其 Collider，避免挡住交互。")]
    [SerializeField] private bool disableSpawnedColliders = true;

    [Tooltip("如果目标没指定，自动生成一个临时柱体预览。")]
    [SerializeField] private bool spawnPreviewCylinderWhenMissing = true;

    [SerializeField] private Vector3 previewLocalPosition = new Vector3(0f, 0.35f, 0.2f);
    [SerializeField] private Vector3 previewLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 previewLocalScale = new Vector3(0.08f, 0.2f, 0.08f);

    [Header("Rotation")]
    [Tooltip("总旋转角度（度）。可填负值反向旋转。")]
    [SerializeField] private float totalRotationDegrees = 540f;

    [Tooltip("旋转速度（度/秒）。")]
    [SerializeField] private float rotationSpeedDegreesPerSecond = 360f;

    [Tooltip("选择沿模型自身局部坐标的哪个轴旋转。")]
    [SerializeField] private RotationAxisMode rotationAxisMode = RotationAxisMode.Z;

    [Tooltip("当轴模式为 Custom 时，使用模型自身局部坐标下的该轴向量。")]
    [SerializeField] private Vector3 customRotationAxis = Vector3.forward;

    [Header("Left-Right Motion")]
    [Tooltip("左右摆动轴（局部空间）。")]
    [SerializeField] private Vector3 lateralAxis = Vector3.right;

    [Tooltip("左右摆动幅度。")]
    [SerializeField] private float lateralAmplitude = 0.05f;

    [Tooltip("左右摆动频率（次/秒）。")]
    [SerializeField] private float lateralFrequency = 2.5f;

    [Tooltip("播放结束后是否复位到初始位置。")]
    [SerializeField] private bool resetPositionOnFinish = true;

    [Header("Visibility")]
    [Tooltip("待机时隐藏清洗物体（开始播放时显示）。")]
    [SerializeField] private bool hideWashTargetWhenIdle = true;

    [Tooltip("播放结束后再次隐藏清洗物体。")]
    [SerializeField] private bool hideWashTargetAfterFinish = true;

    [Header("Audio")]
    [Tooltip("清洗旋转时播放的水流音频源。")]
    [SerializeField] private AudioSource waterAudioSource;

    [Tooltip("清洗旋转时循环播放的水流音效。")]
    [SerializeField] private AudioClip waterLoopClip;

    [Header("Player Lock")]
    [Tooltip("演出期间锁定玩家移动和视角。")]
    [SerializeField] private bool lockPlayerControl = true;

    [SerializeField] private PlayerInteractionController interactionController;
    [SerializeField] private StarterAssetsInputs starterInputs;

    private Coroutine washRoutine;
    private bool isPlaying;
    private bool controlsLocked;
    private bool cachedInteractionUiOpen;
    private bool cachedStarterInputsEnabled;
    private bool cachedCursorInputForLook;
    private Transform spawnedWashTarget;
    private AudioClip cachedAudioClip;
    private bool cachedAudioLoop;

    private void Awake()
    {
        TryResolveReferences();

        if (hideWashTargetWhenIdle && washTarget != null)
        {
            SetWashTargetVisible(false);
        }
    }

    public void PlayWashSequence()
    {
        if (isPlaying)
        {
            return;
        }

        EnsureWashTarget();
        if (washTarget == null)
        {
            Debug.LogWarning("WaterDispenserWashSequence: 未设置 washTarget，无法播放清洗演出。");
            return;
        }

        SetWashTargetVisible(true);
        washRoutine = StartCoroutine(WashRoutine());
    }

    private IEnumerator WashRoutine()
    {
        isPlaying = true;
        TryResolveReferences();
        SetPlayerControlLocked(true);
        BeginWaterAudio();

        Transform target = washTarget;
        Vector3 startLocalPosition = target.localPosition;
        Quaternion startLocalRotation = target.localRotation;

        float speed = Mathf.Max(1f, Mathf.Abs(rotationSpeedDegreesPerSecond));
        float totalDegrees = totalRotationDegrees;
        float duration = Mathf.Max(0.05f, Mathf.Abs(totalDegrees) / speed);
        Vector3 selectedLocalAxis = GetSelectedLocalAxis();
        // Convert the chosen local axis to parent space once so the object
        // rotates around its own initial local axis (X/Y/Z or custom).
        Vector3 safeRotationAxis = GetSafeAxis(startLocalRotation * selectedLocalAxis, Vector3.forward);
        Vector3 safeLateralAxis = GetSafeAxis(lateralAxis, Vector3.right);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);

            float currentAngle = totalDegrees * normalized;
            Quaternion rotationDelta = Quaternion.AngleAxis(currentAngle, safeRotationAxis);
            target.localRotation = startLocalRotation * rotationDelta;

            float lateralOffset = Mathf.Sin(elapsed * lateralFrequency * Mathf.PI * 2f) * lateralAmplitude;
            target.localPosition = startLocalPosition + safeLateralAxis * lateralOffset;

            yield return null;
        }

        target.localRotation = startLocalRotation * Quaternion.AngleAxis(totalDegrees, safeRotationAxis);
        target.localPosition = resetPositionOnFinish ? startLocalPosition : target.localPosition;

        EndWaterAudio();
        if (hideWashTargetAfterFinish)
        {
            SetWashTargetVisible(false);
        }

        SetPlayerControlLocked(false);
        washRoutine = null;
        isPlaying = false;
    }

    private void OnDisable()
    {
        if (washRoutine != null)
        {
            StopCoroutine(washRoutine);
            washRoutine = null;
            isPlaying = false;
        }

        EndWaterAudio();
        if (hideWashTargetWhenIdle)
        {
            SetWashTargetVisible(false);
        }

        SetPlayerControlLocked(false);
    }

    private void TryResolveReferences()
    {
        if (interactionController == null)
        {
            interactionController = FindObjectOfType<PlayerInteractionController>();
        }

        if (starterInputs == null)
        {
            starterInputs = FindObjectOfType<StarterAssetsInputs>();
        }
    }

    private void EnsureWashTarget()
    {
        if (washTarget != null)
        {
            return;
        }

        if (TrySpawnPrefabTarget())
        {
            return;
        }

        if (!spawnPreviewCylinderWhenMissing)
        {
            return;
        }

        GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        preview.name = "WashPreviewCylinder";
        preview.transform.SetParent(transform, false);
        preview.transform.localPosition = previewLocalPosition;
        preview.transform.localRotation = Quaternion.Euler(previewLocalEuler);
        preview.transform.localScale = previewLocalScale;

        Collider previewCollider = preview.GetComponent<Collider>();
        if (previewCollider != null)
        {
            Destroy(previewCollider);
        }

        washTarget = preview.transform;
    }

    private bool TrySpawnPrefabTarget()
    {
        if (washTargetPrefab == null)
        {
            return false;
        }

        if (spawnedWashTarget != null)
        {
            washTarget = spawnedWashTarget;
            return true;
        }

        Transform parent = washTargetSpawnAnchor != null ? washTargetSpawnAnchor : transform;
        GameObject spawned = Instantiate(washTargetPrefab, parent, false);
        spawned.name = washTargetPrefab.name + "_WashTarget";
        spawned.transform.localPosition = prefabLocalPosition;
        spawned.transform.localRotation = Quaternion.Euler(prefabLocalEuler);
        spawned.transform.localScale = prefabLocalScale;

        if (disableSpawnedColliders)
        {
            Collider[] colliders = spawned.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (hideWashTargetWhenIdle)
        {
            spawned.SetActive(false);
        }

        spawnedWashTarget = spawned.transform;
        washTarget = spawnedWashTarget;
        return true;
    }

    private void BeginWaterAudio()
    {
        if (waterAudioSource == null || waterLoopClip == null)
        {
            return;
        }

        cachedAudioClip = waterAudioSource.clip;
        cachedAudioLoop = waterAudioSource.loop;
        waterAudioSource.clip = waterLoopClip;
        waterAudioSource.loop = true;
        waterAudioSource.Play();
    }

    private void EndWaterAudio()
    {
        if (waterAudioSource == null)
        {
            return;
        }

        if (waterAudioSource.isPlaying)
        {
            waterAudioSource.Stop();
        }

        waterAudioSource.clip = cachedAudioClip;
        waterAudioSource.loop = cachedAudioLoop;
    }

    private void SetPlayerControlLocked(bool locked)
    {
        if (!lockPlayerControl)
        {
            return;
        }

        if (locked)
        {
            if (controlsLocked)
            {
                return;
            }

            if (interactionController != null)
            {
                cachedInteractionUiOpen = interactionController.IsUiOpen;
                interactionController.IsUiOpen = true;
            }

            if (starterInputs != null)
            {
                cachedStarterInputsEnabled = starterInputs.enabled;
                cachedCursorInputForLook = starterInputs.cursorInputForLook;
                starterInputs.MoveInput(Vector2.zero);
                starterInputs.LookInput(Vector2.zero);
                starterInputs.cursorInputForLook = false;
                starterInputs.enabled = false;
            }

            controlsLocked = true;
            return;
        }

        if (!controlsLocked)
        {
            return;
        }

        if (interactionController != null)
        {
            interactionController.IsUiOpen = cachedInteractionUiOpen;
        }

        if (starterInputs != null)
        {
            starterInputs.enabled = cachedStarterInputsEnabled;
            starterInputs.cursorInputForLook = cachedCursorInputForLook;
            starterInputs.MoveInput(Vector2.zero);
            starterInputs.LookInput(Vector2.zero);
        }

        controlsLocked = false;
    }

    private static Vector3 GetSafeAxis(Vector3 axis, Vector3 fallback)
    {
        return axis.sqrMagnitude > Mathf.Epsilon ? axis.normalized : fallback;
    }

    private Vector3 GetSelectedLocalAxis()
    {
        switch (rotationAxisMode)
        {
            case RotationAxisMode.X:
                return Vector3.right;
            case RotationAxisMode.Y:
                return Vector3.up;
            case RotationAxisMode.Z:
                return Vector3.forward;
            case RotationAxisMode.Custom:
                return GetSafeAxis(customRotationAxis, Vector3.forward);
            default:
                return Vector3.forward;
        }
    }

    private void SetWashTargetVisible(bool visible)
    {
        if (washTarget == null)
        {
            return;
        }

        if (washTarget.gameObject.activeSelf != visible)
        {
            washTarget.gameObject.SetActive(visible);
        }
    }
}
