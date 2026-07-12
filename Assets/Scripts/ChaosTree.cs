using System.Collections;
using UnityEngine;

public class ChaosTree : MonoBehaviour
{
    [Header("XR Player")]
    [Tooltip("Hier den vollständigen Complete XR Origin hineinziehen.")]
    [SerializeField] private Transform xrOrigin;

    [Tooltip("Der Baum reagiert automatisch, wenn der XR Origin den Trigger betritt.")]
    [SerializeField] private bool triggerOnPlayerEnter = true;

    [Header("Baum-Drehpunkt")]
    [Tooltip("Leeres Objekt unten am Stamm. Wenn leer, wird dieses Objekt verwendet.")]
    [SerializeField] private Transform rotationPivot;

    [Header("Umkipp-Einstellungen")]
    [SerializeField] private float rotationDuration = 1.5f;
    [SerializeField] private float cooldown = 3f;

    [Tooltip("Kleinster Kippwinkel.")]
    [SerializeField] private float minimumFallAngle = 75f;

    [Tooltip("Größter Kippwinkel.")]
    [SerializeField] private float maximumFallAngle = 100f;

    [Tooltip("Baum kippt vom Player weg.")]
    [SerializeField] private bool fallAwayFromPlayer = true;

    [Header("Chaos-Verhalten")]
    [SerializeField] private bool returnToNormal = false;
    [SerializeField] private float returnDelay = 4f;

    private Quaternion originalRotation;
    private bool isAnimating;
    private float nextAllowedTime;

    private Transform ActivePivot =>
        rotationPivot != null ? rotationPivot : transform;

    private void Awake()
    {
        originalRotation = ActivePivot.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter)
            return;

        if (IsXRPlayer(other))
        {
            TriggerChaos();
        }
    }

    private bool IsXRPlayer(Collider other)
    {
        if (other == null)
            return false;

        // Erkennt den XR Origin selbst oder Collider innerhalb des XR Origins.
        if (xrOrigin != null)
        {
            if (other.transform == xrOrigin)
                return true;

            if (other.transform.IsChildOf(xrOrigin))
                return true;

            // Falls der Collider auf einem übergeordneten Objekt liegt.
            if (xrOrigin.IsChildOf(other.transform))
                return true;
        }

        // Zusätzlicher Fallback über den Player-Tag.
        if (other.CompareTag("Player"))
            return true;

        return false;
    }

    public void TriggerChaos()
    {
        if (isAnimating)
            return;

        if (Time.time < nextAllowedTime)
            return;

        StartCoroutine(ChaosRotation());
    }

    private IEnumerator ChaosRotation()
    {
        isAnimating = true;
        nextAllowedTime = Time.time + cooldown;

        Transform pivot = ActivePivot;

        Quaternion startRotation = pivot.rotation;
        Quaternion targetRotation = GetFallRotation();

        float elapsed = 0f;

        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / rotationDuration);

            // Sanftes Beschleunigen und Abbremsen.
            t = t * t * (3f - 2f * t);

            pivot.rotation = Quaternion.Slerp(
                startRotation,
                targetRotation,
                t
            );

            yield return null;
        }

        pivot.rotation = targetRotation;

        if (returnToNormal)
        {
            yield return new WaitForSeconds(returnDelay);

            elapsed = 0f;
            Quaternion fallenRotation = pivot.rotation;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / rotationDuration);
                t = t * t * (3f - 2f * t);

                pivot.rotation = Quaternion.Slerp(
                    fallenRotation,
                    originalRotation,
                    t
                );

                yield return null;
            }

            pivot.rotation = originalRotation;
        }

        isAnimating = false;
    }

    private Quaternion GetFallRotation()
    {
        Vector3 fallDirection;

        if (fallAwayFromPlayer && xrOrigin != null)
        {
            // Richtung vom Player zum Baum = vom Player weg.
            fallDirection = ActivePivot.position - xrOrigin.position;
            fallDirection.y = 0f;
        }
        else
        {
            // Zufällige Richtung auf der XZ-Ebene.
            float randomAngle = Random.Range(0f, 360f);

            fallDirection = Quaternion.Euler(
                0f,
                randomAngle,
                0f
            ) * Vector3.forward;
        }

        if (fallDirection.sqrMagnitude < 0.001f)
        {
            fallDirection = Vector3.forward;
        }

        fallDirection.Normalize();

        // Horizontale Achse, um die der Baum kippt.
        Vector3 rotationAxis = Vector3.Cross(
            Vector3.up,
            fallDirection
        ).normalized;

        float fallAngle = Random.Range(
            minimumFallAngle,
            maximumFallAngle
        );

        return Quaternion.AngleAxis(
            fallAngle,
            rotationAxis
        ) * originalRotation;
    }
}