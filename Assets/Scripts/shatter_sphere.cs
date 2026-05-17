using UnityEngine;

public class Breakable : MonoBehaviour
{
    [SerializeField] GameObject intactSphere;
    [SerializeField] GameObject brokenSphere;

    private Collider col;
    private Camera cam;

    private void Awake()
    {
        intactSphere.SetActive(true);
        brokenSphere.SetActive(false);

        col = GetComponent<Collider>();
        cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mausklick erkannt");

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log("Raycast getroffen: " + hit.collider.gameObject.name);

                if (hit.collider.transform.IsChildOf(intactSphere.transform))
                {
                    Debug.Log("Kugel wurde angeklickt");
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
        intactSphere.SetActive(false);
        brokenSphere.SetActive(true);

        if (col != null)
            col.enabled = false;

        Rigidbody[] rigidbodies = brokenSphere.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.AddExplosionForce(
                500f,                  // Stärke
                transform.position,     // Explosionszentrum
                10f,                    // Radius
                0.5f                   // leichter Auftrieb nach oben
            );
        }
    }
}