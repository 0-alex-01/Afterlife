using UnityEngine;

public class Breakable : MonoBehaviour
{
    [SerializeField] GameObject intactSphere;
    [SerializeField] GameObject brokenSphere;

    [Header("Audio")]
    [SerializeField] AudioClip shatterSound;
    [SerializeField] AudioSource audioSource;

    private Collider col;
    private Camera cam;
    private bool isBroken = false;

    private void Awake()
    {
        intactSphere.SetActive(true);
        brokenSphere.SetActive(false);

        col = GetComponent<Collider>();
        cam = Camera.main;

        // Falls kein AudioSource im Inspector gesetzt wurde,
        // versucht das Script automatisch einen am Objekt zu finden.
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Update()
    {
        if (isBroken) return;

        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mausklick erkannt");

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("Raycast getroffen: " + hit.collider.gameObject.name);

                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                {
                    Debug.Log("Spiegel wurde angeklickt");
                    Break();
                }
            }
            else
            {
                Debug.Log("Raycast hat nichts getroffen");
            }
        }
    }

    private void Break()
    {
        if (isBroken) return;
        isBroken = true;

        Debug.Log("Break() gestartet");

        // Glasbruch-Sound abspielen
        if (shatterSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(shatterSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(shatterSound, transform.position);
            }
        }

        intactSphere.SetActive(false);
        brokenSphere.SetActive(true);

        if (col != null)
            col.enabled = false;

        Rigidbody[] rigidbodies = brokenSphere.GetComponentsInChildren<Rigidbody>();
        Debug.Log("Rigidbodies gefunden: " + rigidbodies.Length);

        Vector3 center = intactSphere.transform.position;

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 dir = (rb.worldCenterOfMass - center).normalized;

            Vector3 force = dir * 1.5f + Vector3.up * 0.5f;

            rb.AddForce(force, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 1f, ForceMode.Impulse);
        }
    }
}