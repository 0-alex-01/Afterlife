using UnityEngine;

public class SimpleTeleporter : MonoBehaviour
{
    [Header("Teleport-Ziel")]
    public Transform teleportZiel;

    private void OnTriggerEnter(Collider other)
    {
        // Dieser Text MUSS in der Konsole aufleuchten, wenn die Physik klappt!
        Debug.Log("PORTAL BERÜHRT von: " + other.gameObject.name);

        if (teleportZiel != null)
        {
            // Teleportation ausführen
            other.transform.position = teleportZiel.position;
            other.transform.rotation = teleportZiel.rotation;

            Debug.Log("ERFOLGREICH TELEPORTIERT: " + other.gameObject.name);
        }
        else
        {
            Debug.LogWarning("Teleport fehlgeschlagen: Kein Ziel im Inspector zugewiesen!");
        }
    }
}