using UnityEngine;

/// <summary>
/// Gentle idle motion for a pedestal object: hovers (vertical bob) and slowly rotates
/// to attract attention. Disable this component to freeze the object (e.g., on grab/commit).
/// Captures its base local position on enable so toggling is seamless.
/// </summary>
public class FloatingObject : MonoBehaviour
{
    [Tooltip("Vertical bob amplitude in metres.")]
    [SerializeField] private float bobAmplitude = 0.06f;
    [Tooltip("Seconds for one full bob cycle.")]
    [SerializeField] private float bobPeriod = 4.5f;
    [Tooltip("Idle spin speed in degrees/second.")]
    [SerializeField] private float rotateSpeed = 25f;
    [Tooltip("Local axis to spin around.")]
    [SerializeField] private Vector3 spinAxis = Vector3.up;
    [Tooltip("Random phase offset so multiple objects don't bob in lockstep.")]
    [SerializeField] private float phaseOffset = 0f;

    private Vector3 _basePos;
    private bool _hasBase;

    private void OnEnable()
    {
        _basePos = transform.localPosition;
        _hasBase = true;
        if (phaseOffset == 0f) phaseOffset = Random.Range(0f, 6.283f);
    }

    private void Update()
    {
        if (!_hasBase) { _basePos = transform.localPosition; _hasBase = true; }

        float t = Time.time;
        float bob = Mathf.Sin((t / Mathf.Max(bobPeriod, 0.01f)) * Mathf.PI * 2f + phaseOffset) * bobAmplitude;
        transform.localPosition = _basePos + Vector3.up * bob;

        if (rotateSpeed != 0f && spinAxis.sqrMagnitude > 1e-6f)
        {
            transform.Rotate(spinAxis.normalized, rotateSpeed * Time.deltaTime, Space.Self);
        }
    }
}
