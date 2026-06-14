using System.Collections.Generic;
using UnityEngine;

// Freier Noclip-Flugmodus für Desktop/Tests - zusätzlich zum VR-Modus.
// Auf eine EIGENE Kamera legen (nicht die VR-Kamera). Mit der Toggle-Taste an/aus.
// Bewegung ist rein transform-basiert -> fliegt durch alle Wände (noclip).
[RequireComponent(typeof(Camera))]
public class NoclipFlyController : MonoBehaviour
{
    [Header("Steuerung")]
    public KeyCode toggleKey = KeyCode.F1;
    public float moveSpeed = 8f;
    public float sprintMultiplier = 3f;
    public float lookSensitivity = 2f;
    public bool startActive = false;

    [Header("Beim Aktivieren deaktivieren (optional)")]
    [Tooltip("z.B. der VR-Rig oder der normale Player, damit der nicht mitläuft")]
    public GameObject[] disableWhileActive;

    [Header("Startpunkt (beim Aktivieren)")]
    [Tooltip("Beim Einschalten an diese Position/Blickrichtung springen (vor dem Portal)")]
    public bool moveToStartOnActivate = true;
    public Vector3 startPosition = new Vector3(-1.6f, 51.2f, 0.8f);
    public Vector3 startEuler = new Vector3(0f, -60f, 0f);

    [Header("Portale")]
    [Tooltip("Trigger-Collider + Rigidbody anlegen, damit man im Noclip durch Portale teleportiert wird")]
    public bool triggerPortals = true;

    private Camera cam;
    private bool active;
    private float yaw, pitch;
    private readonly List<Camera> disabledCameras = new List<Camera>();

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (triggerPortals) SetupPortalTrigger();
        SetActive(startActive);
        Debug.Log("NoclipFlyController bereit auf '" + name + "' - Umschalten mit: " + toggleKey, this);
    }

    // Trigger-Collider + kinematischer Rigidbody, damit die Kamera Portal-Trigger auslöst
    private void SetupPortalTrigger()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (GetComponent<Collider>() == null)
        {
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.3f;
            sc.isTrigger = true;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetActive(!active);
            Debug.Log("Noclip umgeschaltet: " + (active ? "AN" : "AUS"), this);
        }

        if (!active)
            return;

        // Umsehen mit der Maus
        yaw += Input.GetAxis("Mouse X") * lookSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.eulerAngles = new Vector3(pitch, yaw, 0f);

        // Bewegung relativ zur Blickrichtung
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;
        if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.Q)) dir -= Vector3.up;

        transform.position += dir.normalized * speed * Time.deltaTime;
    }

    private void SetActive(bool on)
    {
        active = on;

        if (cam != null)
        {
            cam.enabled = on;
            if (on) cam.depth = 100f; // über alle anderen Kameras zeichnen
        }

        if (on)
        {
            // Vor dem Portal starten
            if (moveToStartOnActivate)
            {
                transform.position = startPosition;
                transform.eulerAngles = startEuler;
            }

            // Alle anderen aktiven Kameras (auch die VR-Kamera) abschalten,
            // damit die Noclip-Kamera die Ansicht übernimmt.
            disabledCameras.Clear();
            foreach (var c in Camera.allCameras)
            {
                if (c != cam && c.enabled)
                {
                    c.enabled = false;
                    disabledCameras.Add(c);
                }
            }

            Vector3 e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // Vorher abgeschaltete Kameras wieder einschalten
            foreach (var c in disabledCameras)
                if (c != null) c.enabled = true;
            disabledCameras.Clear();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (disableWhileActive != null)
            foreach (var go in disableWhileActive)
                if (go != null) go.SetActive(!on);
    }
}
