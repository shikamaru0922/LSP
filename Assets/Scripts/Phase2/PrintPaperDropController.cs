using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PrintPaperDropController : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private GameObject paperPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform spawnedPaperParent;
    [SerializeField] private float emitInterval = 0.35f;
    [SerializeField] private Vector3 ejectionOffsetLocal = new Vector3(0f, 0f, 0.02f);
    [SerializeField] private bool disableSpawnedColliders = true;

    [Header("Drop")]
    [SerializeField] private Vector3 dropDirectionLocal = new Vector3(1f, 0f, 0f);
    [Min(0f)] [SerializeField] private float minEjectSpeed = 1.2f;
    [Min(0f)] [SerializeField] private float maxEjectSpeed = 2.2f;
    [SerializeField] private float ejectDuration = 0.2f;
    [SerializeField] private Vector2 randomPitchRange = new Vector2(-6f, 6f);
    [SerializeField] private Vector2 randomYawRange = new Vector2(-12f, 12f);
    [SerializeField] private Vector2 randomRollRange = new Vector2(0f, 0f);
    [SerializeField] private float horizontalSpeed = 0.12f;
    [SerializeField] private float fallSpeed = 0.9f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float maxGroundCheckDistance = 5f;
    [SerializeField] private float groundOffset = 0.01f;
    [SerializeField] private float maxDropTime = 6f;

    [Header("Sway Feel")]
    [SerializeField] private float swayAmplitude = 0.09f;
    [SerializeField] private float swayFrequency = 2.2f;
    [SerializeField] private float swayDamping = 0.65f;
    [SerializeField] private Vector3 swayRotationAmplitude = new Vector3(8f, 12f, 18f);
    [SerializeField] private float swayRotationFrequency = 2.4f;

    private Coroutine outputRoutine;
    private bool isOutputting;

    private void OnDisable()
    {
        StopPaperOutput();
    }

    public void OpenPaperOutput()
    {
        StartPaperOutput();
    }

    public void ClosePaperOutput()
    {
        StopPaperOutput();
    }

    public void StartPaperOutput()
    {
        if (paperPrefab == null)
        {
            Debug.LogWarning("PrintPaperDropController: paperPrefab is null.");
            return;
        }

        if (isOutputting)
        {
            return;
        }

        isOutputting = true;
        outputRoutine = StartCoroutine(OutputRoutine());
    }

    public void StopPaperOutput()
    {
        isOutputting = false;

        if (outputRoutine != null)
        {
            StopCoroutine(outputRoutine);
            outputRoutine = null;
        }
    }

    [ContextMenu("Spawn One Paper")]
    public void SpawnOnePaper()
    {
        if (paperPrefab == null)
        {
            Debug.LogWarning("PrintPaperDropController: paperPrefab is null.");
            return;
        }

        Transform anchor = spawnPoint != null ? spawnPoint : transform;

        GameObject spawned = Instantiate(paperPrefab);
        spawned.name = paperPrefab.name + "_Drop";
        spawned.transform.position = anchor.TransformPoint(ejectionOffsetLocal);
        spawned.transform.rotation = anchor.rotation;

        if (spawnedPaperParent != null)
        {
            spawned.transform.SetParent(spawnedPaperParent, true);
        }
        else
        {
            spawned.transform.SetParent(null, true);
        }

        var nestedSpawner = spawned.GetComponent<PrintPaperDropController>();
        if (nestedSpawner != null)
        {
            Destroy(nestedSpawner);
        }

        if (disableSpawnedColliders)
        {
            Collider[] colliders = spawned.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        if (!spawned.activeSelf)
        {
            spawned.SetActive(true);
        }

        PaperDropMotion motion = spawned.GetComponent<PaperDropMotion>();
        if (motion == null)
        {
            motion = spawned.AddComponent<PaperDropMotion>();
        }

        Vector3 worldDirection = GetRandomLaunchDirection(anchor);
        float launchSpeed = GetRandomEjectSpeed();

        motion.ConfigureSway(
            swayAmplitude,
            swayFrequency,
            swayDamping,
            swayRotationAmplitude,
            swayRotationFrequency);

        motion.BeginDrop(
            worldDirection,
            launchSpeed,
            ejectDuration,
            horizontalSpeed,
            fallSpeed,
            groundLayers,
            maxGroundCheckDistance,
            groundOffset,
            maxDropTime);
    }

    private IEnumerator OutputRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, emitInterval));

        while (isOutputting)
        {
            SpawnOnePaper();
            yield return wait;
        }

        outputRoutine = null;
    }

    private float GetRandomEjectSpeed()
    {
        float min = Mathf.Max(0f, minEjectSpeed);
        float max = Mathf.Max(0f, maxEjectSpeed);

        if (max < min)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }

    private Vector3 GetRandomLaunchDirection(Transform anchor)
    {
        Vector3 baseLocal = dropDirectionLocal.sqrMagnitude > Mathf.Epsilon
            ? dropDirectionLocal.normalized
            : Vector3.right;

        float pitch = GetRandomRangeValue(randomPitchRange);
        float yaw = GetRandomRangeValue(randomYawRange);
        float roll = GetRandomRangeValue(randomRollRange);
        Quaternion randomOffset = Quaternion.Euler(pitch, yaw, roll);

        Vector3 localDirection = randomOffset * baseLocal;
        Vector3 worldDirection = anchor.TransformDirection(localDirection);

        return worldDirection.sqrMagnitude > Mathf.Epsilon ? worldDirection.normalized : anchor.right;
    }

    private static float GetRandomRangeValue(Vector2 range)
    {
        float min = range.x;
        float max = range.y;

        if (max < min)
        {
            float temp = min;
            min = max;
            max = temp;
        }

        return Mathf.Approximately(min, max) ? min : Random.Range(min, max);
    }
}
