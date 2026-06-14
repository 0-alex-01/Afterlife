using UnityEngine;
using Autohand;

public class AfterlifeNoclip : MonoBehaviour
{
    public float flySpeed = 15f;
    public KeyCode toggleKey = KeyCode.F1;
    public float mouseSensitivity = 2.5f;

    private AutoHandPlayer player;
    private Rigidbody rb;
    private bool isNoclip = false;

    // Mouse look state
    private Behaviour trackedPoseDriver; // disabled during noclip so it doesn't override rotation
    private float yaw;
    private float pitch;

    private void Start()
    {
        player = GetComponent<AutoHandPlayer>();
        rb = GetComponent<Rigidbody>();

        // Find the camera's TrackedPoseDriver (SpatialTracking) so we can pause it in noclip
        if (player != null && player.headCamera != null)
        {
            foreach (var c in player.headCamera.GetComponents<Behaviour>())
            {
                if (c != null && c.GetType().Name.Contains("TrackedPoseDriver"))
                {
                    trackedPoseDriver = c;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleNoclip();
        }

        if (isNoclip)
        {
            HandleMouseLook();
            HandleNoclipMovement();
        }
    }

    private void ToggleNoclip()
    {
        isNoclip = !isNoclip;
        if (player != null)
        {
            player.useGrounding = !isNoclip;
            player.useMovement = !isNoclip;
            player.allowClimbing = !isNoclip;
        }

        if (rb != null)
        {
            rb.isKinematic = isNoclip;
            rb.useGravity = !isNoclip;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Mouse look: pause the VR tracking driver so the mouse controls the view
        if (trackedPoseDriver != null)
            trackedPoseDriver.enabled = !isNoclip;

        if (isNoclip && player != null && player.headCamera != null)
        {
            // Seed yaw/pitch from current camera orientation for a smooth handover
            Vector3 e = player.headCamera.transform.eulerAngles;
            yaw = e.y;
            pitch = e.x;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Debug.Log("Noclip: " + (isNoclip ? "ENABLED (WASD + Space/Shift, mouse to look)" : "DISABLED"));
    }

    private void HandleMouseLook()
    {
        if (player == null || player.headCamera == null) return;

        yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        player.headCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleNoclipMovement()
    {
        if (player == null) return;

        // Force properties every frame in case AutoHand resets them
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        player.useGrounding = false;
        player.useMovement = false;

        Vector3 dir = Vector3.zero;
        Transform refT = player.headCamera.transform;
        
        if (Input.GetKey(KeyCode.W)) dir += refT.forward;
        if (Input.GetKey(KeyCode.S)) dir -= refT.forward;
        if (Input.GetKey(KeyCode.A)) dir -= refT.right;
        if (Input.GetKey(KeyCode.D)) dir += refT.right;
        
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftShift)) dir -= Vector3.up;

        if (dir.sqrMagnitude > 0.001f)
        {
            // Compute target FIRST, then let SetPosition move body + tracking container
            // (camera) together. Pre-moving transform would make the delta zero and
            // the camera/tracking container would not follow (especially vertically).
            Vector3 target = transform.position + dir.normalized * flySpeed * Time.deltaTime;
            player.SetPosition(target);
        }
    }
}
