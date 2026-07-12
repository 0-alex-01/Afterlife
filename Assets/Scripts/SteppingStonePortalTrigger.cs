using UnityEngine;

public class SteppingStonePortalTrigger : MonoBehaviour
{
    [Header("Referenzen")]
    [SerializeField] private MysticalPortalController portalController;

    [Tooltip("Das komplette XR Origin Objekt.")]
    [SerializeField] private Transform xrOrigin;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnlyOnce = true;

    private bool hasTriggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        if (!IsPlayer(other))
            return;

        hasTriggered = true;

        if (portalController != null)
        {
            portalController.ActivateFromSteppingStoneTrigger();
        }
        else
        {
            Debug.LogWarning("SteppingStonePortalTrigger: Kein Portal Controller zugewiesen.");
        }
    }

    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag("Player"))
            return true;

        if (xrOrigin == null)
            return false;

        return other.transform == xrOrigin || other.transform.IsChildOf(xrOrigin);
    }
}