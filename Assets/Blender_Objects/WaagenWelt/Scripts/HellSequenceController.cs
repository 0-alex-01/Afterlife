using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HellSequenceController : MonoBehaviour
{
    [Header("Player / XR Rig")]
    [Tooltip("Root des Complete XR Origin beziehungsweise AutoHandPlayers.")]
    [SerializeField]
    private Transform xrOriginRoot;

    [Tooltip("Main Camera innerhalb des XR Origin.")]
    [SerializeField]
    private Transform playerCamera;

    [Tooltip("Character Controller auf dem XR Origin.")]
    [SerializeField]
    private CharacterController characterController;

    [Tooltip("Spawnpunkt auf Bodenhöhe in der Höllenwelt.")]
    [SerializeField]
    private Transform hellSpawnPoint;


    [Header("Hell Messenger Approach")]
    [Tooltip(
        "Der Höllenbote oder dessen neutraler Parent. " +
        "Hier dasselbe Objekt eintragen wie im AfterlifeSequenceManager."
    )]
    [SerializeField]
    private Transform hellMessenger;

    [Tooltip(
        "Rotationskorrektur für Modelle, deren Vorderseite " +
        "nicht entlang der lokalen Z+-Achse zeigt."
    )]
    [SerializeField]
    private Vector3 messengerRotationOffset = Vector3.zero;

    [Tooltip("Kurze Pause, bevor sich der Bote bewegt.")]
    [Min(0f)]
    [SerializeField]
    private float delayBeforeMessengerMoves = 1.2f;

    [Tooltip("Wie lange der Bote für die Annäherung braucht.")]
    [Min(0.1f)]
    [SerializeField]
    private float messengerApproachDuration = 4.5f;

    [Tooltip("Horizontaler Abstand des Boten zur Kamera.")]
    [Min(0.5f)]
    [SerializeField]
    private float messengerStoppingDistance = 1.5f;

    [Tooltip(
        "Vertikaler Offset zur aktuellen Augenhöhe. " +
        "Ein negativer Wert positioniert den Boten etwas unter der Kamera."
    )]
    [SerializeField]
    private float messengerEyeHeightOffset = -0.2f;

    [Tooltip(
        "Wie lange der Bote den Spieler anstarrt, " +
        "bevor das Bild schwarz wird."
    )]
    [Min(0f)]
    [SerializeField]
    private float messengerStareDuration = 2.2f;

    [Tooltip(
        "Der Bote wird während der Bewegung ständig " +
        "zum Spieler gedreht."
    )]
    [SerializeField]
    private bool messengerAlwaysFacesPlayer = true;

    [Tooltip("Bewegungsverlauf des Höllenboten.")]
    [SerializeField]
    private AnimationCurve messengerMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    [Header("Scene Areas")]
    [Tooltip(
        "Nur die visuellen Objekte der Himmelswelt. " +
        "Keine Manager, Canvas-Objekte oder Player-Objekte."
    )]
    [SerializeField]
    private GameObject heavenArea;

    [Tooltip("Parent der Feuerwelt. Kann anfangs deaktiviert sein.")]
    [SerializeField]
    private GameObject hellArea;

    [Tooltip("Höllenbote nach dem Teleport ausblenden.")]
    [SerializeField]
    private bool hideMessengerAfterTeleport = true;


    [Header("Black Transition Fade")]
    [Tooltip("CanvasGroup des schwarzen Vollbild-Canvas.")]
    [SerializeField]
    private CanvasGroup blackFadeCanvasGroup;

    [Min(0.01f)]
    [SerializeField]
    private float fadeToBlackDuration = 1.1f;

    [Min(0f)]
    [SerializeField]
    private float blackHoldDuration = 0.8f;

    [Min(0.01f)]
    [SerializeField]
    private float fadeFromBlackDuration = 1.5f;


    [Header("Transition Audio")]
    [SerializeField]
    private AudioSource oneShotSource;

    [Tooltip("Tiefe Wucht, Windstoß oder Fallgeräusch.")]
    [SerializeField]
    private AudioClip transitionClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float transitionVolume = 0.9f;

    [Tooltip("Umgebungsgeräusch der Höllenwelt.")]
    [SerializeField]
    private AudioSource hellAmbienceSource;


    [Header("Hell Ending")]
    [Tooltip(
        "CanvasGroup mit Teufelstor, Hintergrund und Abschlussfrage."
    )]
    [SerializeField]
    private CanvasGroup hellEndingCanvasGroup;

    [Tooltip("UI-Image für das Teufelstor.")]
    [SerializeField]
    private Image devilGateImage;

    [Tooltip("Teufelstor-Sprite.")]
    [SerializeField]
    private Sprite devilGateSprite;

    [Tooltip("Textfeld für die offene Abschlussfrage.")]
    [SerializeField]
    private TextMeshProUGUI endingQuestionText;

    [TextArea(2, 5)]
    [SerializeField]
    private string endingQuestion =
        "Where does fate end, and responsibility begin?";

    [Tooltip("AudioSource für die gesprochene Abschlussfrage.")]
    [SerializeField]
    private AudioSource narratorSource;

    [SerializeField]
    private AudioClip endingNarrationClip;

    [Tooltip(
        "Das Endbild erscheint nach dieser Zeit " +
        "nach der Ankunft in der Hölle."
    )]
    [Min(1f)]
    [SerializeField]
    private float automaticEndingDelay = 15f;

    [Tooltip(
        "Optional: Endbild früher auslösen, wenn sich der Spieler bewegt."
    )]
    [SerializeField]
    private bool showEndingWhenPlayerMoves = false;

    [Tooltip(
        "Vor dieser Zeit kann eine Bewegung das Ende nicht auslösen."
    )]
    [Min(0f)]
    [SerializeField]
    private float minimumHellTimeBeforeMovementTrigger = 2.5f;

    [Tooltip(
        "Erforderliche horizontale Bewegung für ein frühes Endbild."
    )]
    [Min(0.01f)]
    [SerializeField]
    private float movementDistanceToTriggerEnding = 0.2f;

    [Tooltip("Dauer des Einblendens des Teufelstor-Endbildes.")]
    [Min(0.01f)]
    [SerializeField]
    private float endingFadeDuration = 2f;

    [Tooltip("Spielerbewegung beim Endbild erneut sperren.")]
    [SerializeField]
    private bool lockMovementAtEnding = true;


    [Header("Sequence Manager")]
    [SerializeField]
    private AfterlifeSequenceManager sequenceManager;

    [Tooltip(
        "Spieler darf sich nach der Ankunft in der Hölle wieder bewegen."
    )]
    [SerializeField]
    private bool unlockMovementAfterArrival = true;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;


    private Coroutine transitionRoutine;
    private Coroutine endingRoutine;

    private bool transitionStarted;
    private bool playerArrivedInHell;
    private bool endingStarted;

    private float hellArrivalTime;
    private Vector3 hellArrivalPosition;

    /*
     * Die Richtung vom Spieler zum Messenger wird zu Beginn
     * der Annäherung gespeichert. Dadurch bleibt die Bewegung
     * stabil und ändert nicht ständig ihre Richtung.
     */
    private Vector3 messengerApproachDirection =
        Vector3.forward;


    public bool HasTransitionStarted =>
        transitionStarted;


    private void Awake()
    {
        FindMissingReferences();
        PrepareCanvases();

        if (devilGateImage != null &&
            devilGateSprite != null)
        {
            devilGateImage.sprite =
                devilGateSprite;
        }

        if (endingQuestionText != null)
        {
            endingQuestionText.text =
                endingQuestion;
        }
    }


    private void Update()
    {
        if (!playerArrivedInHell ||
            endingStarted)
        {
            return;
        }

        float timeInHell =
            Time.time - hellArrivalTime;

        if (timeInHell >= automaticEndingDelay)
        {
            BeginHellEnding();
            return;
        }

        if (!showEndingWhenPlayerMoves ||
            timeInHell <
            minimumHellTimeBeforeMovementTrigger ||
            xrOriginRoot == null)
        {
            return;
        }

        Vector3 movement =
            xrOriginRoot.position -
            hellArrivalPosition;

        movement.y = 0f;

        if (movement.magnitude >=
            movementDistanceToTriggerEnding)
        {
            BeginHellEnding();
        }
    }


    private void FindMissingReferences()
    {
        if (playerCamera == null &&
            xrOriginRoot != null)
        {
            Camera foundCamera =
                xrOriginRoot.GetComponentInChildren<Camera>(
                    true
                );

            if (foundCamera != null)
            {
                playerCamera =
                    foundCamera.transform;
            }
        }

        if (characterController == null &&
            xrOriginRoot != null)
        {
            characterController =
                xrOriginRoot.GetComponent<CharacterController>();
        }
    }


    private void PrepareCanvases()
    {
        if (blackFadeCanvasGroup != null)
        {
            blackFadeCanvasGroup.alpha = 0f;
            blackFadeCanvasGroup.interactable = false;
            blackFadeCanvasGroup.blocksRaycasts = false;
        }

        if (hellEndingCanvasGroup != null)
        {
            hellEndingCanvasGroup.alpha = 0f;
            hellEndingCanvasGroup.interactable = false;
            hellEndingCanvasGroup.blocksRaycasts = false;
        }
    }


    /// <summary>
    /// Wird über das Event "On Hell Sequence Started"
    /// des AfterlifeSequenceManagers aufgerufen.
    ///
    /// Die Prüfung, ob der Spieler bereits auf der Treppe war,
    /// erfolgt ausschließlich im AfterlifeSequenceManager.
    /// </summary>
    public void BeginHellTransition()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: " +
                "BeginHellTransition wurde aufgerufen.",
                this
            );
        }

        if (transitionStarted)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    "HellSequenceController: " +
                    "Transition läuft bereits.",
                    this
                );
            }

            return;
        }

        /*
         * Hier werden zunächst nur die Referenzen geprüft,
         * die für die Bewegung des Messengers benötigt werden.
         *
         * Hell Spawn Point und Black Fade Canvas werden erst
         * nach der Messenger-Annäherung geprüft.
         */
        if (!ValidateMessengerReferences())
        {
            return;
        }

        transitionStarted = true;

        if (!hellMessenger.gameObject.activeSelf)
        {
            hellMessenger.gameObject.SetActive(true);
        }

        transitionRoutine =
            StartCoroutine(
                HellTransitionRoutine()
            );
    }


    private bool ValidateMessengerReferences()
    {
        bool valid = true;

        if (xrOriginRoot == null)
        {
            Debug.LogError(
                "HellSequenceController: XR Origin Root fehlt.",
                this
            );

            valid = false;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "HellSequenceController: Player Camera fehlt.",
                this
            );

            valid = false;
        }

        if (hellMessenger == null)
        {
            Debug.LogError(
                "HellSequenceController: Hell Messenger fehlt.",
                this
            );

            valid = false;
        }

        return valid;
    }


    private bool ValidateWorldTransitionReferences()
    {
        bool valid = true;

        if (hellSpawnPoint == null)
        {
            Debug.LogError(
                "HellSequenceController: Hell Spawn Point fehlt. " +
                "Der Messenger konnte sich bewegen, aber der Spieler " +
                "kann nicht in die Höllenwelt teleportiert werden.",
                this
            );

            valid = false;
        }

        if (blackFadeCanvasGroup == null)
        {
            Debug.LogError(
                "HellSequenceController: Black Fade CanvasGroup fehlt. " +
                "Der Messenger konnte sich bewegen, aber die " +
                "Schwarzblende kann nicht ausgeführt werden.",
                this
            );

            valid = false;
        }

        return valid;
    }


    private IEnumerator HellTransitionRoutine()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: Messenger-Sequenz beginnt.",
                this
            );
        }

        PrepareMessengerApproachDirection();

        if (delayBeforeMessengerMoves > 0f)
        {
            yield return new WaitForSeconds(
                delayBeforeMessengerMoves
            );
        }

        /*
         * Der Messenger ist eine Pflichtreferenz.
         * Deshalb wird diese Coroutine immer ausgeführt,
         * wenn BeginHellTransition erfolgreich gestartet ist.
         */
        yield return MoveMessengerTowardPlayer();

        if (messengerStareDuration > 0f)
        {
            yield return StareAtPlayerRoutine();
        }

        /*
         * Erst nach der sichtbaren Messenger-Annäherung
         * werden die Referenzen für Fade und Teleport geprüft.
         */
        if (!ValidateWorldTransitionReferences())
        {
            transitionStarted = false;
            transitionRoutine = null;

            Debug.LogError(
                "HellSequenceController: Die Messenger-Bewegung " +
                "wurde ausgeführt, aber die Welttransition wurde " +
                "wegen fehlender Referenzen abgebrochen.",
                this
            );

            yield break;
        }

        PlayTransitionSound();

        yield return FadeCanvas(
            blackFadeCanvasGroup,
            blackFadeCanvasGroup.alpha,
            1f,
            fadeToBlackDuration
        );

        if (hellArea != null)
        {
            hellArea.SetActive(true);
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(
                "HellSequenceController: Hell Area ist nicht zugewiesen.",
                this
            );
        }

        yield return TeleportPlayerToHell();

        if (heavenArea != null)
        {
            heavenArea.SetActive(false);
        }

        if (hideMessengerAfterTeleport &&
            hellMessenger != null)
        {
            hellMessenger.gameObject.SetActive(false);
        }

        if (hellAmbienceSource != null &&
            !hellAmbienceSource.isPlaying)
        {
            hellAmbienceSource.Play();
        }

        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSeconds(
                blackHoldDuration
            );
        }

        yield return FadeCanvas(
            blackFadeCanvasGroup,
            1f,
            0f,
            fadeFromBlackDuration
        );

        /*
         * Erst nach dem vollständigen Wiedereinblenden
         * darf sich der Spieler wieder bewegen.
         */
        if (unlockMovementAfterArrival &&
            sequenceManager != null)
        {
            sequenceManager.UnlockPlayerLocomotion();
        }

        RegisterHellArrival();

        transitionRoutine = null;

        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: Spieler ist in der Höllenwelt.",
                this
            );
        }
    }


    private void PrepareMessengerApproachDirection()
    {
        if (playerCamera == null ||
            hellMessenger == null)
        {
            messengerApproachDirection =
                Vector3.forward;

            return;
        }

        /*
         * Richtung von der Kamera zum ursprünglichen
         * Standort des Messengers.
         */
        messengerApproachDirection =
            hellMessenger.position -
            playerCamera.position;

        messengerApproachDirection.y = 0f;

        if (messengerApproachDirection.sqrMagnitude <
            0.001f)
        {
            messengerApproachDirection =
                -playerCamera.forward;

            messengerApproachDirection.y = 0f;
        }

        if (messengerApproachDirection.sqrMagnitude <
            0.001f)
        {
            messengerApproachDirection =
                Vector3.forward;
        }

        messengerApproachDirection.Normalize();
    }


    private IEnumerator MoveMessengerTowardPlayer()
    {
        if (hellMessenger == null ||
            playerCamera == null)
        {
            yield break;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"HellSequenceController: Messenger bewegt sich von " +
                $"{hellMessenger.position} zur Kamera bei " +
                $"{playerCamera.position}.",
                this
            );
        }

        Vector3 startPosition =
            hellMessenger.position;

        float elapsed = 0f;

        while (elapsed <
               messengerApproachDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    messengerApproachDuration
                );

            float curvedProgress =
                messengerMovementCurve != null
                    ? messengerMovementCurve.Evaluate(progress)
                    : progress;

            /*
             * Das Ziel folgt weiterhin der Kamera,
             * aber die grundsätzliche Annäherungsrichtung
             * bleibt stabil.
             */
            Vector3 currentTarget =
                CalculateMessengerTargetPosition();

            hellMessenger.position =
                Vector3.Lerp(
                    startPosition,
                    currentTarget,
                    curvedProgress
                );

            FaceMessengerTowardPlayer();

            yield return null;
        }

        hellMessenger.position =
            CalculateMessengerTargetPosition();

        FaceMessengerTowardPlayer();

        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: " +
                "Messenger hat seine Zielposition erreicht.",
                this
            );
        }
    }


    private Vector3 CalculateMessengerTargetPosition()
    {
        if (playerCamera == null ||
            hellMessenger == null)
        {
            return hellMessenger != null
                ? hellMessenger.position
                : Vector3.zero;
        }

        Vector3 horizontalDirection =
            messengerApproachDirection;

        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude <
            0.001f)
        {
            horizontalDirection =
                -playerCamera.forward;

            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude <
            0.001f)
        {
            horizontalDirection =
                Vector3.forward;
        }

        horizontalDirection.Normalize();

        Vector3 targetPosition =
            playerCamera.position +
            horizontalDirection *
            messengerStoppingDistance;

        /*
         * Der Bote wird auf Basis der aktuellen
         * Augenhöhe positioniert.
         */
        targetPosition.y =
            playerCamera.position.y +
            messengerEyeHeightOffset;

        return targetPosition;
    }


    private IEnumerator StareAtPlayerRoutine()
    {
        if (hellMessenger == null ||
            playerCamera == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed <
               messengerStareDuration)
        {
            elapsed += Time.deltaTime;

            FaceMessengerTowardPlayer();

            Vector3 targetPosition =
                CalculateMessengerTargetPosition();

            /*
             * Während des Anstarrens folgt der Bote
             * sanft der aktuellen Kopfposition.
             */
            hellMessenger.position =
                Vector3.Lerp(
                    hellMessenger.position,
                    targetPosition,
                    Time.deltaTime * 4f
                );

            yield return null;
        }
    }


    private void FaceMessengerTowardPlayer()
    {
        if (!messengerAlwaysFacesPlayer ||
            hellMessenger == null ||
            playerCamera == null)
        {
            return;
        }

        Vector3 direction =
            playerCamera.position -
            hellMessenger.position;

        if (direction.sqrMagnitude <
            0.001f)
        {
            return;
        }

        Quaternion lookRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        hellMessenger.rotation =
            lookRotation *
            Quaternion.Euler(
                messengerRotationOffset
            );
    }


    private void PlayTransitionSound()
    {
        if (oneShotSource == null ||
            transitionClip == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(
            transitionClip,
            transitionVolume
        );
    }


    private IEnumerator TeleportPlayerToHell()
    {
        if (xrOriginRoot == null ||
            hellSpawnPoint == null)
        {
            yield break;
        }

        bool controllerWasEnabled =
            characterController != null &&
            characterController.enabled;

        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        xrOriginRoot.SetPositionAndRotation(
            hellSpawnPoint.position,
            hellSpawnPoint.rotation
        );

        /*
         * Ein Frame warten, damit Unity Transform,
         * Kamera und Character Controller aktualisieren kann.
         */
        yield return null;

        if (controllerWasEnabled)
        {
            characterController.enabled = true;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"HellSequenceController: Teleport zu " +
                $"'{hellSpawnPoint.name}'.",
                this
            );
        }
    }


    private void RegisterHellArrival()
    {
        playerArrivedInHell = true;
        hellArrivalTime = Time.time;

        if (xrOriginRoot != null)
        {
            hellArrivalPosition =
                xrOriginRoot.position;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"HellSequenceController: Endsequenz startet " +
                $"spätestens nach {automaticEndingDelay} Sekunden.",
                this
            );
        }
    }


    public void BeginHellEnding()
    {
        if (endingStarted)
        {
            return;
        }

        endingStarted = true;

        if (endingRoutine != null)
        {
            StopCoroutine(
                endingRoutine
            );
        }

        endingRoutine =
            StartCoroutine(
                HellEndingRoutine()
            );
    }


    private IEnumerator HellEndingRoutine()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: Endbild wird eingeblendet.",
                this
            );
        }

        if (lockMovementAtEnding &&
            sequenceManager != null)
        {
            sequenceManager.LockPlayerLocomotionPublic();
        }

        if (endingQuestionText != null)
        {
            endingQuestionText.text =
                endingQuestion;
        }

        if (devilGateImage != null &&
            devilGateSprite != null)
        {
            devilGateImage.sprite =
                devilGateSprite;
        }

        yield return FadeCanvas(
            hellEndingCanvasGroup,
            hellEndingCanvasGroup != null
                ? hellEndingCanvasGroup.alpha
                : 0f,
            1f,
            endingFadeDuration
        );

        if (narratorSource != null &&
            endingNarrationClip != null)
        {
            narratorSource.clip =
                endingNarrationClip;

            narratorSource.Play();
        }

        if (hellEndingCanvasGroup != null)
        {
            hellEndingCanvasGroup.interactable = true;
            hellEndingCanvasGroup.blocksRaycasts = true;
        }

        endingRoutine = null;
    }


    private IEnumerator FadeCanvas(
        CanvasGroup targetCanvas,
        float startAlpha,
        float targetAlpha,
        float duration
    )
    {
        if (targetCanvas == null)
        {
            yield break;
        }

        /*
         * Falls nur das Canvas-Objekt selbst deaktiviert ist,
         * wird es für die Blende wieder aktiviert.
         */
        if (!targetCanvas.gameObject.activeSelf)
        {
            targetCanvas.gameObject.SetActive(true);
        }

        if (duration <= 0f)
        {
            targetCanvas.alpha =
                targetAlpha;

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float smoothProgress =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

            targetCanvas.alpha =
                Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    smoothProgress
                );

            yield return null;
        }

        targetCanvas.alpha =
            targetAlpha;
    }


    [ContextMenu("TEST - Höllentransition starten")]
    private void TestHellTransition()
    {
        BeginHellTransition();
    }


    public void ResetHellTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(
                transitionRoutine
            );

            transitionRoutine = null;
        }

        if (endingRoutine != null)
        {
            StopCoroutine(
                endingRoutine
            );

            endingRoutine = null;
        }

        transitionStarted = false;
        playerArrivedInHell = false;
        endingStarted = false;

        PrepareCanvases();

        if (showDebugLogs)
        {
            Debug.Log(
                "HellSequenceController: Transition wurde zurückgesetzt.",
                this
            );
        }
    }
}