using UnityEngine;

public class StaircaseAreaTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Der zentrale AfterlifeSequenceManager.")]
    [SerializeField]
    private AfterlifeSequenceManager sequenceManager;

    [Tooltip(
        "Character Controller auf dem Root des Spielers, " +
        "zum Beispiel AutoHandPlayer_Afterlife."
    )]
    [SerializeField]
    private CharacterController playerCharacterController;


    [Header("Trigger Settings")]
    [Tooltip(
        "Wenn aktiviert, kann dieser Trigger den Aufstieg " +
        "nur einmal auslösen."
    )]
    [SerializeField]
    private bool triggerOnlyOnce = true;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;


    private bool alreadyTriggered;


    private void OnTriggerEnter(Collider other)
    {
        TryStartAscent(other);
    }


    private void OnTriggerStay(Collider other)
    {
        /*
         * Zusätzliche Absicherung:
         * Falls der Spieler bereits innerhalb des Triggers
         * gespawnt oder hineinteleportiert wurde.
         */
        if (!alreadyTriggered)
        {
            TryStartAscent(other);
        }
    }


    private void TryStartAscent(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (triggerOnlyOnce &&
            alreadyTriggered)
        {
            return;
        }

        if (sequenceManager == null)
        {
            Debug.LogError(
                "StaircaseAreaTrigger: " +
                "AfterlifeSequenceManager wurde nicht zugewiesen.",
                this
            );

            return;
        }

        if (playerCharacterController == null)
        {
            Debug.LogError(
                "StaircaseAreaTrigger: " +
                "Player Character Controller wurde nicht zugewiesen.",
                this
            );

            return;
        }

        /*
         * Der berührende Collider kann beispielsweise
         * von einer Hand oder einem anderen Child-Objekt stammen.
         * Deshalb wird in den Parent-Objekten nach dem
         * Character Controller gesucht.
         */
        CharacterController enteredController =
            other.GetComponentInParent<CharacterController>();

        if (enteredController !=
            playerCharacterController)
        {
            return;
        }

        alreadyTriggered = true;

        /*
         * Ab jetzt befindet sich der Sequence Manager
         * im Zustand Ascending.
         *
         * Erst danach darf das Umdrehen die
         * Höllensequenz auslösen.
         */
        sequenceManager.StartAscent();

        if (showDebugLogs)
        {
            Debug.Log(
                "StaircaseAreaTrigger: " +
                "Spieler hat die Treppe betreten. " +
                "StartAscent wurde aufgerufen.",
                this
            );
        }
    }


    public void ResetTrigger()
    {
        alreadyTriggered = false;

        if (showDebugLogs)
        {
            Debug.Log(
                "StaircaseAreaTrigger: Trigger wurde zurückgesetzt.",
                this
            );
        }
    }
}