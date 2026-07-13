using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(Rigidbody))]
public class HellFallTrigger : MonoBehaviour
{
    [Header("Hell Sequence")]
    [SerializeField] private AfterlifeSequenceManager sequenceManager;

    [Header("AutoHand Player")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;

    private void Reset()
    {
        ConfigurePhysics();
    }

    private void Awake()
    {
        ConfigurePhysics();
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

        hasTriggered = true;

        if (sequenceManager == null)
        {
            Debug.LogError("HellFallTrigger: AfterlifeSequenceManager fehlt.", this);
            return;
        }

        Debug.Log("HellFallTrigger: Spieler ist von der Treppe gefallen. Höllensequenz startet.", this);
        sequenceManager.StartHellSequence();
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
