using Autohand;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleTeleporter : MonoBehaviour
{
    [Header("Szenen-Wechsel (optional)")]
    public string targetSceneName;

    [Header("Teleport-Ziel (optional)")]
    public Transform teleportZiel;

    private bool transitionStarted;

    private void OnTriggerEnter(Collider other)
    {
        if (transitionStarted || !TryGetPlayer(other, out Transform playerRoot))
            return;

        Debug.Log("Portal beruehrt von: " + other.gameObject.name);

        if (!string.IsNullOrWhiteSpace(targetSceneName))
        {
            transitionStarted = true;
            SceneManager.LoadScene(targetSceneName);
            return;
        }

        if (teleportZiel != null)
        {
            playerRoot.SetPositionAndRotation(teleportZiel.position, teleportZiel.rotation);

            Debug.Log("Erfolgreich teleportiert: " + playerRoot.name);
        }
        else
        {
            Debug.LogWarning("Portal hat weder eine Zielszene noch ein Teleport-Ziel.", this);
        }
    }

    private static bool TryGetPlayer(Collider other, out Transform playerRoot)
    {
        AutoHandPlayer autoHandPlayer = other.GetComponentInParent<AutoHandPlayer>();
        if (autoHandPlayer != null)
        {
            playerRoot = autoHandPlayer.transform;
            return true;
        }

        Transform root = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform.root
            : other.transform.root;

        if (other.CompareTag("Player") ||
            root.name.IndexOf("AutoHandPlayer", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            playerRoot = root;
            return true;
        }

        playerRoot = null;
        return false;
    }
}
