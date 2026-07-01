using UnityEngine;
using Autohand;

public class AfterlifeNoclip : MonoBehaviour
{
    public float flySpeed = 15f;
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode desktopToggleKey = KeyCode.F2;
    public float mouseSensitivity = 2.5f;

    [Header("Desktop Mode")]
    public Camera desktopCamera;
    private bool isDesktopMode = false;

    private AutoHandPlayer player;
    private Rigidbody rb;
    private bool isNoclip = false;

    private SoulMorphController morphController;
    private PedestalChoiceController choiceController;
    private Behaviour trackedPoseDriver; 
    private float yaw;
    private float pitch;

    private void Start()
    {
        ResolveReferences();
        if (desktopCamera != null) desktopCamera.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        AutoHandPlayer previousPlayer = player;

        if (player == null)
        {
            player = GetComponent<AutoHandPlayer>();
            if (player == null) player = FindAnyObjectByType<AutoHandPlayer>();
        }

        if (player != null && (rb == null || player != previousPlayer))
        {
            rb = player.GetComponent<Rigidbody>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }
        else if (rb == null) rb = GetComponent<Rigidbody>();

        if (morphController == null) morphController = FindAnyObjectByType<SoulMorphController>();
        if (choiceController == null) choiceController = FindAnyObjectByType<PedestalChoiceController>();

        if (player != null && player.headCamera != null && (player != previousPlayer || trackedPoseDriver == null))
        {
            trackedPoseDriver = null;
            foreach (var c in player.headCamera.GetComponents<Behaviour>())
            {
                if (c != null && c.GetType().Name.Contains("TrackedPoseDriver"))
                {
                    trackedPoseDriver = c;
                    break;
                }
            }

            if (isNoclip && player != previousPlayer)
            {
                player.useGrounding = false;
                player.useMovement = false;
                player.allowClimbing = false;
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                if (trackedPoseDriver != null) trackedPoseDriver.enabled = false;
            }
        }
    }

    private void Update()
    {
        ResolveReferences();

        if (Input.GetKeyDown(toggleKey)) ToggleNoclip();
        if (Input.GetKeyDown(desktopToggleKey)) ToggleDesktopMode();

        if (isNoclip || isDesktopMode)
        {
            HandleMouseLook();
            HandleNoclipMovement();
            HandleMorphPreviewKeys();
        }
    }

    private void ToggleDesktopMode()
    {
        isDesktopMode = !isDesktopMode;

        if (isDesktopMode && desktopCamera == null && player != null && player.headCamera != null)
        {
            var go = new GameObject("DesktopCamera (auto)");
            go.transform.SetParent(player.transform, false);
            desktopCamera = go.AddComponent<Camera>();
            var hc = player.headCamera.GetComponent<Camera>();
            if (hc != null)
            {
                desktopCamera.fieldOfView = hc.fieldOfView;
                desktopCamera.nearClipPlane = 0.02f; 
                desktopCamera.farClipPlane = hc.farClipPlane;
                desktopCamera.cullingMask = hc.cullingMask;
                desktopCamera.clearFlags = hc.clearFlags;
                desktopCamera.backgroundColor = hc.backgroundColor;
            }
            desktopCamera.cullingMask &= ~(1 << 10); // SoulMirror layer remains mirror-only
            desktopCamera.gameObject.SetActive(false);
            Debug.Log("[AfterlifeNoclip] DesktopCamera created.");
        }

        if (desktopCamera != null)
        {
            desktopCamera.gameObject.SetActive(isDesktopMode);
            desktopCamera.tag = isDesktopMode ? "MainCamera" : "Untagged";
            // Move camera forward enough to clear the VR head and body geometry (0.6m forward)
            desktopCamera.transform.localPosition = new Vector3(0, 1.7f, 0.6f); 
        }
            
        if (player != null)
        {
            player.enabled = !isDesktopMode;
            
            // Aggressively hide ALL renderers that might be player-related (Hands, Body, Head)
            // We search globally because AutoHand sometimes instantiates hands outside the player root.
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rend in allRenderers)
            {
                // Don't hide the SoulTwin or special mirror layers (layer 10)
                if (rend.gameObject.layer == 10) continue;
                
                // Don't hide environment or mirror itself
                if (rend.gameObject.name.Contains("Mirror") || rend.gameObject.name.Contains("Glass") || rend.gameObject.name.Contains("Frame")) continue;
                if (rend.gameObject.name.Contains("Chamber") || rend.gameObject.name.Contains("Corridor") || rend.gameObject.name.Contains("Path")) continue;
                if (rend.gameObject.name.Contains("Pedestal") || rend.gameObject.name.Contains("Obj_")) continue;

                // Hide things with typical player-mesh names
                string n = rend.name.ToLower();
                if (n.Contains("mesh") || n.Contains("hand") || n.Contains("arm") || n.Contains("head") || n.Contains("body") || n.Contains("controller") || n.Contains("model"))
                {
                    rend.enabled = !isDesktopMode;
                }
            }

            // Disable all other cameras
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != desktopCamera && cam != null && !cam.name.Contains("Reflection"))
                {
                    cam.enabled = !isDesktopMode;
                    if (isDesktopMode) cam.tag = "Untagged";
                    else if (cam.name.Contains("head")) cam.tag = "MainCamera";
                }
            }
            
            // Hide Canvases attached to the rig
            foreach (var canv in player.GetComponentsInChildren<Canvas>(true))
            {
                canv.enabled = !isDesktopMode;
            }
        }

        if (isDesktopMode && !isNoclip) SetNoclipState(true);
        else if (!isDesktopMode && isNoclip) SetNoclipState(false);
        
        Debug.Log(isDesktopMode ? "Desktop Mode: ACTIVE" : "VR Mode: ACTIVE");
    }

    private void ToggleNoclip()
    {
        SetNoclipState(!isNoclip);
    }

    private void SetNoclipState(bool state)
    {
        isNoclip = state;
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
            if (isNoclip) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }

        if (trackedPoseDriver != null) trackedPoseDriver.enabled = !isNoclip;

        if (isNoclip)
        {
            Transform lookSource = (isDesktopMode && desktopCamera != null) ? desktopCamera.transform : (player != null && player.headCamera != null ? player.headCamera.transform : transform);
            yaw = lookSource.eulerAngles.y;
            pitch = lookSource.eulerAngles.x;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        
        Debug.Log("Noclip: " + (isNoclip ? "ENABLED" : "DISABLED"));
    }

    private void HandleMouseLook()
    {
        Transform targetT = (isDesktopMode && desktopCamera != null) ? desktopCamera.transform : (player != null ? player.headCamera?.transform : null);
        if (targetT == null) return;

        yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        targetT.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleNoclipMovement()
    {
        if (player == null) return;
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        player.useGrounding = false;
        player.useMovement = false;

        Vector3 dir = Vector3.zero;
        Transform refT = (isDesktopMode && desktopCamera != null) ? desktopCamera.transform : player.headCamera.transform;
        
        if (Input.GetKey(KeyCode.W)) dir += refT.forward;
        if (Input.GetKey(KeyCode.S)) dir -= refT.forward;
        if (Input.GetKey(KeyCode.A)) dir -= refT.right;
        if (Input.GetKey(KeyCode.D)) dir += refT.right;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftShift)) dir -= Vector3.up;

        if (dir.sqrMagnitude > 0.001f)
        {
            Vector3 target = transform.position + dir.normalized * flySpeed * Time.deltaTime;
            player.SetPosition(target);
        }
    }

    private void HandleMorphPreviewKeys()
    {
        if (morphController != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) morphController.SetState(SoulMorphController.MorphState.Clear);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) morphController.SetState(SoulMorphController.MorphState.Bloom);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) morphController.SetState(SoulMorphController.MorphState.Decay);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) morphController.SetState(SoulMorphController.MorphState.Chaos);
            else if (Input.GetKeyDown(KeyCode.Alpha5)) morphController.SetState(SoulMorphController.MorphState.Light);
            else if (Input.GetKeyDown(KeyCode.Tab)) morphController.AdvanceState();
        }

        // Debug commit keys: morph the mirror into a themed portal exactly like grabbing the
        // matching pedestal object in VR does (6=Leere/Void, 7=Wiedergeburt/Rebirth, 8=Gericht/Judgement).
        if (choiceController != null)
        {
            if (Input.GetKeyDown(KeyCode.Alpha6)) choiceController.OnCommit(ChoiceTheme.Void);
            else if (Input.GetKeyDown(KeyCode.Alpha7)) choiceController.OnCommit(ChoiceTheme.Rebirth);
            else if (Input.GetKeyDown(KeyCode.Alpha8)) choiceController.OnCommit(ChoiceTheme.Judgement);
        }
    }
}

