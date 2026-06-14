using UnityEngine;

// Teleportiert den Spieler beim Betreten des Portal-Triggers nach unten
// auf das Terrain (feste Zielposition).
// Auf ein Objekt mit Trigger-Collider am Portal (oben) legen.
[RequireComponent(typeof(Collider))]
public class PortalTeleporter : MonoBehaviour
{
    [Header("Ziel")]
    [Tooltip("Feste Weltposition verwenden (sonst das Target-Transform unten)")]
    public bool useFixedPosition = true;

    [Tooltip("Feste Zielposition auf dem Terrain")]
    public Vector3 targetPosition = new Vector3(0f, 0f, 26.9f);

    [Tooltip("Optionales Ziel-Transform (nur falls 'Use Fixed Position' aus ist)")]
    public Transform target;

    [Header("Optionen")]
    [Tooltip("Blickrichtung des Spielers beibehalten?")]
    public bool keepRotation = true;
    [Tooltip("Sekunden, bevor erneut teleportiert werden kann")]
    public float cooldown = 1f;

    private float lastTeleport = -999f;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastTeleport < cooldown)
            return;

        bool useFixed = useFixedPosition || target == null;

        if (!useFixed && target == null)
        {
            Debug.LogWarning("PortalTeleporter: Kein Ziel zugewiesen!", this);
            return;
        }

        Transform root = ResolvePlayerRoot(other);

        // CharacterController kurz abschalten, sonst überschreibt er die neue Position
        var cc = root.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (useFixed)
        {
            root.position = targetPosition;
        }
        else
        {
            root.position = target.position;
            if (!keepRotation) root.rotation = target.rotation;
        }

        if (cc != null) cc.enabled = true;

        lastTeleport = Time.time;
        Debug.Log("Portal: teleportiert " + root.name + " -> " + (useFixed ? targetPosition.ToString() : target.name), this);
    }

    // Findet das eigentliche Spieler-Wurzelobjekt (nicht nur den Collider eines Körperteils)
    private Transform ResolvePlayerRoot(Collider other)
    {
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc != null) return cc.transform;

        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.transform;

        return other.transform.root;
    }
}
