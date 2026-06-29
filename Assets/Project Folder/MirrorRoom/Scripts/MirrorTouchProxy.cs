using UnityEngine;

/// <summary>
/// Optional convenience helper to place on the glass collider (or a hand).
/// On trigger/collision with an object on the configured hand layer or tag, forwards the
/// contact point to the controller's OnMirrorTouch. The controller already has a distance-based
/// fallback, so this is purely additive. Fully null-safe.
/// </summary>
public class MirrorTouchProxy : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The controller to notify on touch.")]
    [SerializeField] private SoulMorphController controller;

    [Header("Filtering")]
    [Tooltip("Only objects with this tag count as a hand. Leave empty to accept any.")]
    [SerializeField] private string handTag = "Hand";
    [Tooltip("Only objects on these layers count as a hand. Set to Everything to accept any.")]
    [SerializeField] private LayerMask handLayers = ~0;

    private bool IsHand(GameObject go)
    {
        if (go == null) return false;
        if ((handLayers.value & (1 << go.layer)) == 0) return false;
        if (!string.IsNullOrEmpty(handTag) && !go.CompareTag(handTag)) return false;
        return true;
    }

    private void Notify(Vector3 point, Transform hand)
    {
        if (controller == null) return;
        controller.OnMirrorTouch(point, hand);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHand(other.gameObject)) return;
        Vector3 point = other.ClosestPoint(transform.position);
        Notify(point, other.transform);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        if (!IsHand(collision.gameObject)) return;
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : collision.transform.position;
        Notify(point, collision.transform);
    }
}
