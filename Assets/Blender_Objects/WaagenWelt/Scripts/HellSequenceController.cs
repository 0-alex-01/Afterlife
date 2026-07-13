using System.Collections;
using Autohand;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HellSequenceController : MonoBehaviour
{
    [Header("Player / XR Rig")]
    [Tooltip("Root des Complete XR Origin.")]
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

    [Tooltip("Direktes Ziel in der Höllenbox. In WaageScene wird automatisch 'bones (3)' verwendet.")]
    [SerializeField]
    private Transform hellArrivalTarget;

    [Tooltip("Unsichtbarer Boden unter dem Höllenziel.")]
    [SerializeField]
    private Transform hellArrivalSafetyFloor;

    [SerializeField]
    private string hellArrivalTargetName = "bones (3)";

    [Tooltip("Verwendet für die finale Waagen-Szene die feste, getestete Höllenposition.")]
    [SerializeField]
    private bool useFixedHellArrivalPosition = true;

    [SerializeField]
    private Vector3 fixedHellArrivalPosition =
        new Vector3(-7.274f, -6.465958f, -3.588f);


    [Header("Fall Catch (VR)")]
    [Tooltip("Physischer AutoHand-Spieler. Wird automatisch unter dem XR-Root gesucht.")]
    [SerializeField]
    private AutoHandPlayer autoHandPlayer;

    [Tooltip("Kleine Aufwärtsbewegung beim Auffangen, damit der Stopp sichtbar wirkt.")]
    [Min(0f)]
    [SerializeField]
    private float fallCatchLift = 0.25f;

    [Tooltip("Kurze Pause nach dem Auffangen, bevor der Sensenmann näherkommt.")]
    [Min(0f)]
    [SerializeField]
    private float fallCatchPause = 0.65f;

    [Tooltip("Schnellerer Anflug des Sensenmanns bei einem echten Sturz.")]
    [Min(0.1f)]
    [SerializeField]
    private float fallMessengerApproachDuration = 1.35f;

    [Tooltip("Blickkontakt nach dem Auffangen und vor der Schwarzblende.")]
    [Min(0f)]
    [SerializeField]
    private float fallMessengerStareDuration = 2.25f;


    [Header("Hell Messenger Approach")]
    [Tooltip("Der Höllenbote oder dessen neutraler Parent.")]
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
        "Ein negativer Wert positioniert ihn etwas unter der Kamera."
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
        "Keine Manager, Canvas-Objekte oder XR-Objekte."
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

    [Tooltip("Wird beim Höllenpfad abgebrochen, damit kein weißes Himmels-Canvas sichtbar bleibt.")]
    [SerializeField]
    private HeavenEndingController heavenEndingController;

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
    private bool immediateTeleportRequested;
    private bool playerArrivedInHell;
    private bool endingStarted;

    private Rigidbody autoHandBody;
    private bool fallCatchStateStored;
    private bool bodyWasKinematic;
    private bool bodyUsedGravity;
    private bool characterControllerWasEnabled;
    private bool autoHandPlayerWasEnabled;

    private float hellArrivalTime;
    private Vector3 hellArrivalPosition;


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
            timeInHell < minimumHellTimeBeforeMovementTrigger ||
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
        if (autoHandPlayer == null &&
            xrOriginRoot != null)
        {
            autoHandPlayer =
                xrOriginRoot.GetComponentInChildren<AutoHandPlayer>(true);
        }

        if (autoHandPlayer != null)
        {
            autoHandBody =
                autoHandPlayer.GetComponent<Rigidbody>();

            if (playerCamera == null &&
                autoHandPlayer.headCamera != null)
            {
                playerCamera =
                    autoHandPlayer.headCamera.transform;
            }
        }

        if (playerCamera == null &&
            xrOriginRoot != null)
        {
            Camera foundCamera =
                xrOriginRoot.GetComponentInChildren<Camera>(true);

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

        if (heavenEndingController == null)
        {
            heavenEndingController =
                FindFirstObjectByType<HeavenEndingController>(
                    FindObjectsInactive.Include
                );
        }

        if (hellArrivalTarget == null ||
            hellArrivalSafetyFloor == null)
        {
            Transform[] sceneTransforms =
                FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (Transform candidate in sceneTransforms)
            {
                if (candidate == null)
                {
                    continue;
                }

                if (hellArrivalTarget == null &&
                    candidate.name == hellArrivalTargetName &&
                    hellArea != null &&
                    candidate.IsChildOf(hellArea.transform))
                {
                    hellArrivalTarget = candidate;
                }

                if (hellArrivalSafetyFloor == null &&
                    candidate.name == "HellArrivalSafetyFloor")
                {
                    hellArrivalSafetyFloor = candidate;
                }
            }
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


    public void BeginHellTransition()
    {
        if (transitionStarted)
        {
            return;
        }

        if (!ValidateTransitionReferences())
        {
            return;
        }

        if (heavenEndingController != null)
        {
            heavenEndingController.CancelHeavenEnding();
        }

        transitionStarted = true;

        transitionRoutine =
            StartCoroutine(
                HellTransitionRoutine()
            );
    }


    /// <summary>
    /// Wird beim tatsächlichen Herunterfallen verwendet. Der physische AutoHand-
    /// Körper wird sofort in der Luft festgehalten. Danach bleibt genug Zeit für
    /// den Sensenmann-Anflug und den Blickkontakt, ohne dass der Spieler weiterfällt.
    /// </summary>
    public void BeginImmediateHellTransition()
    {
        immediateTeleportRequested = true;

        FindMissingReferences();

        // Erst prüfen, dann den VR-Körper einfrieren. So bleibt der Spieler
        // beweglich, falls eine Szenenreferenz fehlt und die Sequenz gar nicht
        // gestartet werden kann.
        if (!ValidateTransitionReferences())
        {
            immediateTeleportRequested = false;
            RestorePlayerAfterFallCatch();
            return;
        }

        FreezePlayerForFallCatch();

        if (!transitionStarted)
        {
            BeginHellTransition();
        }
    }


    private bool ValidateTransitionReferences()
    {
        if (xrOriginRoot == null)
        {
            Debug.LogError(
                "HellSequenceController: XR Origin Root fehlt.",
                this
            );

            return false;
        }

        if (playerCamera == null)
        {
            Debug.LogError(
                "HellSequenceController: Player Camera fehlt.",
                this
            );

            return false;
        }

        if (hellArrivalTarget == null &&
            hellSpawnPoint == null)
        {
            Debug.LogError(
                "HellSequenceController: Weder bones (3) noch Hell Spawn Point gefunden.",
                this
            );

            return false;
        }

        if (blackFadeCanvasGroup == null)
        {
            Debug.LogError(
                "HellSequenceController: Black Fade CanvasGroup fehlt.",
                this
            );

            return false;
        }

        return true;
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

        if (immediateTeleportRequested)
        {
            if (hellMessenger != null)
            {
                hellMessenger.gameObject.SetActive(true);
            }

            if (fallCatchPause > 0f)
            {
                yield return new WaitForSeconds(fallCatchPause);
            }
        }
        else if (delayBeforeMessengerMoves > 0f)
        {
            float delayElapsed = 0f;

            while (delayElapsed < delayBeforeMessengerMoves)
            {
                delayElapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (hellMessenger != null)
        {
            yield return MoveMessengerTowardPlayer();
        }

        float stareDuration = immediateTeleportRequested
            ? fallMessengerStareDuration
            : messengerStareDuration;

        if (stareDuration > 0f)
        {
            yield return StareAtPlayerRoutine(stareDuration);
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
         * Nach dem vollständigen Wiedereinblenden
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


    private IEnumerator MoveMessengerTowardPlayer()
    {
        Vector3 startPosition;

        if (immediateTeleportRequested && playerCamera != null)
        {
            Vector3 cameraForward = playerCamera.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude < 0.001f)
            {
                cameraForward = playerCamera.transform.forward;
            }

            cameraForward.Normalize();

            startPosition =
                playerCamera.position + cameraForward * 4f;
            startPosition.y =
                playerCamera.position.y + messengerEyeHeightOffset;

            hellMessenger.position = startPosition;
            FaceMessengerTowardPlayer();
        }
        else
        {
            startPosition = hellMessenger.position;
        }

        float elapsed = 0f;

        float approachDuration = immediateTeleportRequested
            ? fallMessengerApproachDuration
            : messengerApproachDuration;

        while (elapsed < approachDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    approachDuration
                );

            float curvedProgress =
                messengerMovementCurve.Evaluate(
                    progress
                );

            /*
             * Ziel wird jedes Frame neu aus der
             * aktuellen Kamera-Position berechnet.
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

        /*
         * Horizontale Linie von der Kamera
         * zum aktuellen Messenger.
         */
        Vector3 directionFromCameraToMessenger =
            immediateTeleportRequested
                ? playerCamera.forward
                : hellMessenger.position - playerCamera.position;

        directionFromCameraToMessenger.y = 0f;

        if (directionFromCameraToMessenger.sqrMagnitude <
            0.001f)
        {
            directionFromCameraToMessenger =
                -playerCamera.forward;

            directionFromCameraToMessenger.y = 0f;
        }

        directionFromCameraToMessenger.Normalize();

        Vector3 targetPosition =
            playerCamera.position +
            directionFromCameraToMessenger *
            messengerStoppingDistance;

        /*
         * Aktuelle Augenhöhe der Main Camera.
         * Dadurch funktioniert es auch oben auf der Treppe.
         */
        targetPosition.y =
            playerCamera.position.y +
            messengerEyeHeightOffset;

        return targetPosition;
    }


    private IEnumerator StareAtPlayerRoutine(float stareDuration)
    {
        float elapsed = 0f;

        while (elapsed < stareDuration)
        {
            elapsed += Time.deltaTime;

            FaceMessengerTowardPlayer();

            /*
             * Der Messenger bleibt während des
             * Anstarrens vor der aktuellen Kamera.
             */
            Vector3 targetPosition =
                CalculateMessengerTargetPosition();

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

        /*
         * Y bleibt enthalten, damit der Messenger
         * auch vertikal zur aktuellen Augenhöhe schaut.
         */
        if (direction.sqrMagnitude < 0.001f)
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
        Vector3 arrivalPosition =
            hellArrivalTarget != null
                ? hellArrivalTarget.position
                : useFixedHellArrivalPosition
                ? fixedHellArrivalPosition
                : hellSpawnPoint.position;

        Quaternion arrivalRotation =
            hellSpawnPoint != null
                ? hellSpawnPoint.rotation
                : xrOriginRoot.rotation;

        if (hellArrivalSafetyFloor != null)
        {
            // Der Collider ist 0,3 m hoch. So liegt seine Oberkante
            // exakt auf der Fußposition des AutoHand-Spielers.
            hellArrivalSafetyFloor.position =
                arrivalPosition + Vector3.down * 0.15f;
        }

        if (autoHandBody != null &&
            !autoHandBody.isKinematic)
        {
            autoHandBody.linearVelocity = Vector3.zero;
            autoHandBody.angularVelocity = Vector3.zero;
        }

        // In dieser Szene steckt der eigentliche AutoHandPlayer als Kind
        // in einem äußeren XR-Rig. Darum muss der gemeinsame Rig-Root
        // verschoben werden, sonst setzt AutoHand das Kind wieder zurück.
        xrOriginRoot.SetPositionAndRotation(
            arrivalPosition,
            arrivalRotation
        );

        if (autoHandBody != null &&
            autoHandPlayer != null)
        {
            autoHandBody.position =
                autoHandPlayer.transform.position;
            autoHandBody.rotation =
                autoHandPlayer.transform.rotation;
        }

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        // Ein zweites Setzen nach dem Physikschritt verhindert,
        // dass Tracking oder Rigidbody den äußeren Rig-Root zurückziehen.
        xrOriginRoot.SetPositionAndRotation(
            arrivalPosition,
            arrivalRotation
        );

        if (autoHandBody != null &&
            autoHandPlayer != null)
        {
            autoHandBody.position =
                autoHandPlayer.transform.position;
            autoHandBody.rotation =
                autoHandPlayer.transform.rotation;
        }

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        RestorePlayerAfterFallCatch();

        if (showDebugLogs)
        {
            Debug.Log(
                $"HellSequenceController: Teleport zu " +
                $"{arrivalPosition} " +
                $"({(hellArrivalTarget != null ? hellArrivalTarget.name : "Fallback")}).",
                this
            );
        }
    }


    private void FreezePlayerForFallCatch()
    {
        FindMissingReferences();

        if (!fallCatchStateStored)
        {
            fallCatchStateStored = true;

            if (autoHandBody != null)
            {
                bodyWasKinematic = autoHandBody.isKinematic;
                bodyUsedGravity = autoHandBody.useGravity;
            }

            if (autoHandPlayer != null)
            {
                autoHandPlayerWasEnabled = autoHandPlayer.enabled;
            }

            characterControllerWasEnabled =
                characterController != null && characterController.enabled;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // AutoHand schreibt sonst auch bei einem kinematischen Körper
        // in FixedUpdate Position und Geschwindigkeit zurück.
        if (autoHandPlayer != null)
        {
            autoHandPlayer.enabled = false;
        }

        if (autoHandBody != null)
        {
            if (!autoHandBody.isKinematic)
            {
                autoHandBody.linearVelocity = Vector3.zero;
                autoHandBody.angularVelocity = Vector3.zero;
            }

            autoHandBody.useGravity = false;
            autoHandBody.isKinematic = true;
        }

        if (xrOriginRoot != null && fallCatchLift > 0f)
        {
            xrOriginRoot.position +=
                Vector3.up * fallCatchLift;

            if (autoHandBody != null &&
                autoHandPlayer != null)
            {
                autoHandBody.position =
                    autoHandPlayer.transform.position;
            }
        }
    }


    private void RestorePlayerAfterFallCatch()
    {
        if (!fallCatchStateStored)
        {
            return;
        }

        if (autoHandBody != null)
        {
            autoHandBody.isKinematic = bodyWasKinematic;
            autoHandBody.useGravity = bodyUsedGravity;

            if (!autoHandBody.isKinematic)
            {
                autoHandBody.linearVelocity = Vector3.zero;
                autoHandBody.angularVelocity = Vector3.zero;
            }
        }

        if (autoHandPlayer != null)
        {
            autoHandPlayer.enabled = autoHandPlayerWasEnabled;
        }

        if (characterController != null)
        {
            characterController.enabled =
                characterControllerWasEnabled;
        }

        fallCatchStateStored = false;
    }


    private void RegisterHellArrival()
    {
        playerArrivedInHell = true;
        hellArrivalTime = Time.time;
        hellArrivalPosition =
            xrOriginRoot.position;

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


    private void OnDisable()
    {
        // Besonders wichtig bei abgebrochenem Play Mode: Ein laufender
        // Fall-Catch darf AutoHand und den Rigidbody nicht eingefroren im
        // Editor zurücklassen.
        RestorePlayerAfterFallCatch();
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
        immediateTeleportRequested = false;
        playerArrivedInHell = false;
        endingStarted = false;

        RestorePlayerAfterFallCatch();

        PrepareCanvases();
    }
}
