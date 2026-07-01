using UnityEngine;
using Autohand;

public class DesktopTestToggle : MonoBehaviour
{
    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.F2;
    
    [Header("References")]
    public GameObject vrRigRoot;
    public Camera desktopCamera;
    public AfterlifeNoclip noclipScript;

    private bool isDesktopMode = false;

    private void Start()
    {
        // Default: VR Mode
        SetMode(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetMode(!isDesktopMode);
        }
    }

    public void SetMode(bool desktop)
    {
        isDesktopMode = desktop;
        
        if (desktopCamera != null)
            desktopCamera.gameObject.SetActive(desktop);
            
        if (vrRigRoot != null)
        {
            // We don't disable the whole root to keep scripts alive, 
            // but we disable the cameras and tracking.
            foreach (var cam in vrRigRoot.GetComponentsInChildren<Camera>(true))
            {
                if (cam != desktopCamera)
                    cam.enabled = !desktop;
            }
            
            var ahp = vrRigRoot.GetComponentInChildren<AutoHandPlayer>();
            if (ahp != null)
                ahp.enabled = !desktop;
        }

        // Auto-enable noclip when in desktop mode for movement
        if (noclipScript != null && desktop)
        {
            // Ensure noclip is on if we are in desktop mode
            var isNoclip = (bool)noclipScript.GetType().GetField("isNoclip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(noclipScript);
            if (!isNoclip)
            {
                noclipScript.Invoke("ToggleNoclip", 0);
            }
        }
        
        Debug.Log(desktop ? "Switched to Desktop Test Mode (F2 to toggle back)" : "Switched to VR Mode");
    }
}
