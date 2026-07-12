using System.Collections;
using UnityEngine;

public class HeavenRoomTrigger : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Tag des XR-Origin-Root-Objekts.")]
    [SerializeField]
    private string playerTag = "Player";

    [Tooltip(
        "Wie lange der Spieler im weißen Raum bleiben muss, " +
        "bevor das Himmel-Ende beginnt."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float requiredStayDuration = 4f;


    [Header("Heaven Sequence")]
    [SerializeField]
    private HeavenEndingController heavenEndingController;

    [Tooltip(
        "Optional: Der PlayerProgressMonitor wird beim Betreten deaktiviert, " +
        "damit danach weder Stillstand noch Zurückschauen die Hölle auslösen."
    )]
    [SerializeField]
    private PlayerProgressMonitor playerProgressMonitor;


    [Header("Room Audio")]
    [Tooltip("Harfen-AudioSource, die beim Betreten des Raums startet.")]
    [SerializeField]
    private AudioSource harpSource;

    [Tooltip(
        "Falls aktiviert, stoppt die Harfe wieder, " +
        "wenn der Spieler den Raum vorzeitig verlässt."
    )]
    [SerializeField]
    private bool stopHarpWhenLeavingEarly = false;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;


    private Coroutine stayRoutine;
    private bool playerInside;
    private bool endingTriggered;


    private void OnTriggerEnter(Collider other)
    {
        if (endingTriggered || !IsPlayer(other))
        {
            return;
        }

        playerInside = true;

        /*
         * Ab jetzt darf die Treppenlogik keine Hölle mehr auslösen.
         */
        if (playerProgressMonitor != null)
        {
            playerProgressMonitor.enabled = false;
        }

        if (harpSource != null &&
            !harpSource.isPlaying)
        {
            harpSource.Play();
        }

        if (stayRoutine != null)
        {
            StopCoroutine(stayRoutine);
        }

        stayRoutine = StartCoroutine(
            WaitForHeavenEnding()
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenRoomTrigger: Spieler hat den weißen Raum betreten. " +
                $"Himmel-Ende startet in {requiredStayDuration} Sekunden.",
                this
            );
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other) || endingTriggered)
        {
            return;
        }

        playerInside = false;

        if (stayRoutine != null)
        {
            StopCoroutine(stayRoutine);
            stayRoutine = null;
        }

        if (stopHarpWhenLeavingEarly &&
            harpSource != null)
        {
            harpSource.Stop();
        }

        /*
         * Verlässt der Spieler den Raum wieder, darf die Treppenüberwachung
         * erneut laufen. Das ist optional, aber logisch sauber.
         */
        if (playerProgressMonitor != null)
        {
            playerProgressMonitor.enabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenRoomTrigger: Spieler hat den Raum vorzeitig verlassen.",
                this
            );
        }
    }


    private IEnumerator WaitForHeavenEnding()
    {
        yield return new WaitForSeconds(
            requiredStayDuration
        );

        if (!playerInside || endingTriggered)
        {
            stayRoutine = null;
            yield break;
        }

        endingTriggered = true;
        stayRoutine = null;

        if (heavenEndingController == null)
        {
            Debug.LogError(
                "HeavenRoomTrigger: HeavenEndingController fehlt.",
                this
            );

            yield break;
        }

        heavenEndingController.BeginHeavenEnding();

        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenRoomTrigger: Himmel-Ende wurde gestartet.",
                this
            );
        }
    }


    private bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        Transform currentTransform =
            other.transform;

        while (currentTransform != null)
        {
            if (currentTransform.CompareTag(playerTag))
            {
                return true;
            }

            currentTransform =
                currentTransform.parent;
        }

        return false;
    }


    public void ResetTrigger()
    {
        if (stayRoutine != null)
        {
            StopCoroutine(stayRoutine);
            stayRoutine = null;
        }

        playerInside = false;
        endingTriggered = false;

        if (playerProgressMonitor != null)
        {
            playerProgressMonitor.enabled = true;
        }
    }
}