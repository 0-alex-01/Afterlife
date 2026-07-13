using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class HellFallTrigger : MonoBehaviour
{
    [Header("Hell Sequence")]
    [SerializeField] private AfterlifeSequenceManager sequenceManager;
    [SerializeField] private HellSequenceController hellSequenceController;

    [Header("AutoHand Player")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Fall-Absicherung")]
    [SerializeField] private bool useHeightFallback = true;
    [SerializeField] private float fallbackTriggerHeight = -1f;

    private bool hasTriggered;

    private void Reset()
    {
        ConfigurePhysics();
    }

    private void Awake()
    {
        ConfigurePhysics();

        if (hellSequenceController == null)
            hellSequenceController = FindFirstObjectByType<HellSequenceController>();
    }

    private void Update()
    {
        if (!useHeightFallback || hasTriggered || playerRoot == null)
            return;

        if (playerRoot.position.y <= fallbackTriggerHeight)
            TriggerHellSequence("Höhen-Absicherung");
    }

    private void ConfigurePhysics()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((triggerOnlyOnce && hasTriggered) || !IsPlayer(other))
            return;

        TriggerHellSequence("Fall-Trigger");
    }

    private void TriggerHellSequence(string reason)
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        hasTriggered = true;

        if (sequenceManager == null && hellSequenceController == null)
        {
            Debug.LogError("HellFallTrigger: Keine Höllensequenz gefunden.", this);
            return;
        }

        Debug.Log($"HellFallTrigger: {reason} ausgelöst. Schneller Höllenteleport startet.", this);

        if (sequenceManager != null)
            sequenceManager.StartHellSequence();

        if (hellSequenceController != null)
            hellSequenceController.BeginImmediateHellTransition();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null)
            return false;

        Transform candidate = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        if (playerRoot != null &&
            (candidate == playerRoot || candidate.IsChildOf(playerRoot) || playerRoot.IsChildOf(candidate)))
            return true;

        for (Transform current = other.transform; current != null; current = current.parent)
        {
            if (!string.IsNullOrWhiteSpace(playerTag) && current.CompareTag(playerTag))
                return true;
        }

        return false;
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}
