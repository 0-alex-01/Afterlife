using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

public class SimpleTeleporter : MonoBehaviour
{
    [Header("Szenen-Wechsel")]
    [Tooltip("Der Name der Szene, in die teleportiert werden soll.")]
    public string targetSceneName;

    private void OnTriggerEnter(Collider other)
    {
        // Dieser Text MUSS in der Konsole aufleuchten, wenn die Physik klappt!
        Debug.Log("PORTAL BERÜHRT von: " + other.gameObject.name);

        // Prüfen, ob der Spieler (XR Origin / XR Rig) das Portal berührt hat
        if (IsPlayer(other))
        {
            if (!string.IsNullOrEmpty(targetSceneName))
            {
                Debug.Log("ERFOLGREICH: Lade Szene " + targetSceneName);
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                Debug.LogWarning("Teleport fehlgeschlagen: Kein Szenenname im Inspector zugewiesen!");
            }
        }
    }

    private bool IsPlayer(Collider other)
    {
        // Check for tag
        if (other.CompareTag("Player")) return true;

        // Check for XROrigin component in parent
        if (other.GetComponentInParent<XROrigin>() != null) return true;

        // Fallback checks for common XR Rig setups
        if (other.name.Contains("XR") || other.name.Contains("Hand") || other.name.Contains("Camera"))
        {
            // Usually we want to be sure, but for a "simple" script this might be enough
            return true;
        }

        return false;
    }
}