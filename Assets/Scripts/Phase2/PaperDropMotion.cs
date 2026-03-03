using UnityEngine;

public class PaperDropMotion : MonoBehaviour
{
    [SerializeField] private Vector3 horizontalDirection = Vector3.right;
    [SerializeField] private float ejectSpeed = 1.6f;
    [SerializeField] private float ejectDuration = 0.2f;
    [SerializeField] private float horizontalSpeed = 0.12f;
    [SerializeField] private float fallSpeed = 0.9f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float maxGroundCheckDistance = 5f;
    [SerializeField] private float groundOffset = 0.01f;
    [SerializeField] private float maxDropTime = 6f;
    [SerializeField] private bool alignToGroundOnLanding = true;
    [SerializeField] private Vector3 landingEulerOffset = Vector3.zero;
    [SerializeField] private float swayAmplitude = 0.09f;
    [SerializeField] private float swayFrequency = 2.2f;
    [SerializeField] private float swayDamping = 0.65f;
    [SerializeField] private Vector3 swayRotationAmplitude = new Vector3(8f, 12f, 18f);
    [SerializeField] private float swayRotationFrequency = 2.4f;

    private bool isDropping;
    private float elapsed;
    private Vector3 simulatedPosition;
    private Quaternion baseRotation;
    private Vector3 swayAxis;
    private float swayPhase;
    private float rotationPhase;

    public void ConfigureSway(
        float amplitude,
        float frequency,
        float damping,
        Vector3 rotationAmplitude,
        float rotationFrequency)
    {
        swayAmplitude = Mathf.Max(0f, amplitude);
        swayFrequency = Mathf.Max(0f, frequency);
        swayDamping = Mathf.Max(0f, damping);
        swayRotationAmplitude = rotationAmplitude;
        swayRotationFrequency = Mathf.Max(0f, rotationFrequency);
    }

    public void BeginDrop(
        Vector3 direction,
        float ejectSpd,
        float ejectTime,
        float horizontal,
        float down,
        LayerMask layers,
        float groundDistance,
        float offset,
        float dropTime)
    {
        horizontalDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.right;
        ejectSpeed = Mathf.Max(0f, ejectSpd);
        ejectDuration = Mathf.Max(0f, ejectTime);
        horizontalSpeed = Mathf.Max(0f, horizontal);
        fallSpeed = Mathf.Max(0.01f, down);
        groundLayers = layers;
        maxGroundCheckDistance = Mathf.Max(0.1f, groundDistance);
        groundOffset = offset;
        maxDropTime = Mathf.Max(0.1f, dropTime);

        simulatedPosition = transform.position;
        baseRotation = transform.rotation;
        swayAxis = GetSwayAxis(horizontalDirection);
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
        rotationPhase = Random.Range(0f, Mathf.PI * 2f);

        elapsed = 0f;
        isDropping = true;
        enabled = true;
    }

    private void Update()
    {
        if (!isDropping)
        {
            return;
        }

        float dt = Time.deltaTime;
        elapsed += dt;

        if (elapsed <= ejectDuration)
        {
            simulatedPosition += horizontalDirection * ejectSpeed * dt;
            transform.position = simulatedPosition;
            transform.rotation = baseRotation;
            return;
        }

        simulatedPosition += horizontalDirection * horizontalSpeed * dt;

        float nextY = simulatedPosition.y - fallSpeed * dt;

        if (TryGetGroundHit(simulatedPosition, out RaycastHit hit))
        {
            float groundY = hit.point.y + groundOffset;
            if (nextY <= groundY)
            {
                simulatedPosition = hit.point + hit.normal * groundOffset;
                transform.position = simulatedPosition;
                ApplyLandingRotation(hit.normal);
                isDropping = false;
                enabled = false;
                return;
            }
        }

        simulatedPosition.y = nextY;
        ApplySwayTransform(Mathf.Max(0f, elapsed - ejectDuration));

        if (elapsed >= maxDropTime)
        {
            isDropping = false;
            enabled = false;
        }
    }

    private bool TryGetGroundHit(Vector3 startPosition, out RaycastHit hit)
    {
        if (Physics.Raycast(
            startPosition + Vector3.up * 0.05f,
            Vector3.down,
            out hit,
            maxGroundCheckDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        hit = default;
        return false;
    }

    private void ApplySwayTransform(float swayElapsed)
    {
        float envelope = Mathf.Exp(-swayDamping * swayElapsed);
        float swayWave = Mathf.Sin(swayElapsed * swayFrequency * Mathf.PI * 2f + swayPhase);
        float swayOffset = swayWave * swayAmplitude * envelope;

        transform.position = simulatedPosition + swayAxis * swayOffset;

        float rotWaveA = Mathf.Sin(swayElapsed * swayRotationFrequency * Mathf.PI * 2f + rotationPhase);
        float rotWaveB = Mathf.Cos(swayElapsed * swayRotationFrequency * Mathf.PI * 1.7f + rotationPhase * 0.5f);

        Vector3 rotOffset = new Vector3(
            rotWaveA * swayRotationAmplitude.x,
            rotWaveB * swayRotationAmplitude.y,
            rotWaveA * swayRotationAmplitude.z);

        transform.rotation = baseRotation * Quaternion.Euler(rotOffset);
    }

    private void ApplyLandingRotation(Vector3 groundNormal)
    {
        if (!alignToGroundOnLanding)
        {
            transform.rotation = baseRotation;
            return;
        }

        Vector3 up = groundNormal.sqrMagnitude > Mathf.Epsilon ? groundNormal.normalized : Vector3.up;
        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(horizontalDirection, up);

        if (forwardOnPlane.sqrMagnitude <= Mathf.Epsilon)
        {
            forwardOnPlane = Vector3.ProjectOnPlane(transform.forward, up);
        }

        if (forwardOnPlane.sqrMagnitude <= Mathf.Epsilon)
        {
            forwardOnPlane = Vector3.Cross(up, Vector3.right);
            if (forwardOnPlane.sqrMagnitude <= Mathf.Epsilon)
            {
                forwardOnPlane = Vector3.forward;
            }
        }

        Quaternion flatRotation = Quaternion.LookRotation(forwardOnPlane.normalized, up);
        transform.rotation = flatRotation * Quaternion.Euler(landingEulerOffset);
    }

    private static Vector3 GetSwayAxis(Vector3 forwardDirection)
    {
        Vector3 axis = Vector3.Cross(Vector3.up, forwardDirection);
        if (axis.sqrMagnitude <= Mathf.Epsilon)
        {
            axis = Vector3.right;
        }

        return axis.normalized;
    }
}
