using System;
using UnityEngine;

public class StairStepTrigger : MonoBehaviour
{
    public static event Action<int> AnyPlayerStepReached;


    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";


    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;


    private StaircaseDisappearController controller;
    private int stepIndex = -1;
    private bool alreadyTriggered;


    public void Initialize(
        StaircaseDisappearController staircaseController,
        int index
    )
    {
        controller = staircaseController;
        stepIndex = index;
        alreadyTriggered = false;

        if (showDebugLogs)
        {
            Debug.Log(
                $"Trigger '{name}' wurde als Stufe {stepIndex} eingerichtet.",
                this
            );
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        TryReportPlayer(other);
    }


    private void OnTriggerStay(Collider other)
    {
        /*
         * Absicherung, falls der Player teleportiert oder direkt
         * innerhalb des Triggers platziert wurde.
         */
        if (!alreadyTriggered)
        {
            TryReportPlayer(other);
        }
    }


    private void TryReportPlayer(Collider other)
    {
        if (alreadyTriggered)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        if (stepIndex < 0)
        {
            Debug.LogError(
                $"StairStepTrigger '{name}' wurde nicht initialisiert.",
                this
            );

            return;
        }

        alreadyTriggered = true;

        if (showDebugLogs)
        {
            Debug.Log(
                $"PLAYER ERKANNT: Stufe {stepIndex}.",
                this
            );
        }

        /*
         * Meldung an den Disappear-Controller.
         */
        if (controller != null)
        {
            controller.ReportStepReached(stepIndex);
        }
        else
        {
            Debug.LogWarning(
                $"StairStepTrigger '{name}': " +
                $"Kein StaircaseDisappearController vorhanden.",
                this
            );
        }

        /*
         * Globale Meldung an den PlayerProgressMonitor.
         */
        AnyPlayerStepReached?.Invoke(stepIndex);
    }


    private bool IsPlayer(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            return true;
        }

        Transform current = other.transform;

        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }


    public void ResetTrigger()
    {
        alreadyTriggered = false;
    }
}
