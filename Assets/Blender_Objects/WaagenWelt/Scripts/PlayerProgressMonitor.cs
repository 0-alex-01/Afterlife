using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerProgressMonitor : MonoBehaviour
{
    [Serializable]
    public class IntEvent : UnityEvent<int>
    {
    }

    [Serializable]
    public class FloatEvent : UnityEvent<float>
    {
    }


    [Header("Player")]
    [Tooltip("Root des Complete XR Origin.")]
    [SerializeField]
    private Transform playerRoot;

    [Tooltip("Main Camera innerhalb des XR Origin.")]
    [SerializeField]
    private Transform playerCamera;


    [Header("Staircase")]
    [Tooltip("Anzahl aller Stufen mit StairStepTrigger.")]
    [Min(1)]
    [SerializeField]
    private int totalStepCount = 20;

    [Tooltip(
        "Referenz für die Richtung nach oben. " +
        "Der blaue Z-Pfeil muss entlang der Treppe nach oben zeigen."
    )]
    [SerializeField]
    private Transform staircaseForwardReference;


    [Header("Stillness Detection")]
    [Tooltip(
        "Wie weit sich der Spieler pro Frame mindestens bewegen muss, " +
        "damit dies als Bewegung gilt."
    )]
    [Min(0.0001f)]
    [SerializeField]
    private float movementThreshold = 0.015f;

    [Tooltip(
        "Nach wie vielen Sekunden Stillstand die Warnung beginnt."
    )]
    [Min(0f)]
    [SerializeField]
    private float warningAfterSeconds = 2.5f;

    [Tooltip(
        "Nach wie vielen Sekunden Stillstand die Hölle ausgelöst wird."
    )]
    [Min(0.1f)]
    [SerializeField]
    private float failureAfterSeconds = 5f;

    [Tooltip(
        "Vertikale Bewegungen werden bei der Stillstandserkennung ignoriert."
    )]
    [SerializeField]
    private bool ignoreVerticalMovement = false;


    [Header("Look Back Detection")]
    [Tooltip(
        "Der Spieler muss sich mindestens so weit von der Treppenrichtung " +
        "wegdrehen, damit dies als Zurückschauen gilt."
    )]
    [Range(90f, 180f)]
    [SerializeField]
    private float lookBackAngle = 125f;

    [Tooltip(
        "Wie lange zurückgeschaut werden muss, bevor die Hölle ausgelöst wird."
    )]
    [Min(0f)]
    [SerializeField]
    private float lookBackHoldDuration = 0.65f;

    [Tooltip(
        "Zurückschauen löst die Hölle erst aus, nachdem der Aufstieg begonnen hat."
    )]
    [SerializeField]
    private bool onlyDetectLookBackDuringAscent = true;


    [Header("Events")]
    [SerializeField]
    private UnityEvent onAscentStarted;

    [SerializeField]
    private IntEvent onHighestStepChanged;

    [SerializeField]
    private FloatEvent onProgressChanged;

    [SerializeField]
    private UnityEvent onStillnessWarning;

    [SerializeField]
    private UnityEvent onMovementResumed;

    [SerializeField]
    private UnityEvent onHellFailure;

    [SerializeField]
    private UnityEvent onSummitReached;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    [SerializeField]
    private int currentHighestStep = -1;

    [SerializeField]
    private float currentProgress;

    [SerializeField]
    private float currentStillnessTime;

    [SerializeField]
    private float currentLookBackTime;

    [SerializeField]
    private bool ascentStarted;

    [SerializeField]
    private bool warningActive;

    [SerializeField]
    private bool hellFailureTriggered;

    [SerializeField]
    private bool summitReached;


    private Vector3 previousPlayerPosition;


    public int CurrentHighestStep => currentHighestStep;

    public float CurrentProgress => currentProgress;

    public bool AscentStarted => ascentStarted;

    public bool HellFailureTriggered => hellFailureTriggered;

    public bool SummitReached => summitReached;


    private void Awake()
    {
        FindMissingReferences();

        if (playerRoot != null)
        {
            previousPlayerPosition = playerRoot.position;
        }

        ValidateSettings();
    }


    private void OnEnable()
    {
        StairStepTrigger.AnyPlayerStepReached += HandleStepReached;
    }


    private void OnDisable()
    {
        StairStepTrigger.AnyPlayerStepReached -= HandleStepReached;
    }


    private void Update()
    {
        if (hellFailureTriggered || summitReached)
        {
            return;
        }

        CheckStillness();
        CheckForLookingBack();
    }


    private void FindMissingReferences()
    {
        if (playerRoot == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                playerRoot = playerObject.transform;
            }
        }

        if (playerCamera == null && playerRoot != null)
        {
            Camera foundCamera =
                playerRoot.GetComponentInChildren<Camera>(true);

            if (foundCamera != null)
            {
                playerCamera = foundCamera.transform;
            }
        }
    }


    private void ValidateSettings()
    {
        totalStepCount = Mathf.Max(1, totalStepCount);

        warningAfterSeconds =
            Mathf.Max(0f, warningAfterSeconds);

        failureAfterSeconds =
            Mathf.Max(
                warningAfterSeconds + 0.1f,
                failureAfterSeconds
            );

        lookBackHoldDuration =
            Mathf.Max(0f, lookBackHoldDuration);
    }


    private void HandleStepReached(int stepIndex)
    {
        if (hellFailureTriggered || summitReached)
        {
            return;
        }

        if (!ascentStarted)
        {
            StartAscent();
        }

        if (stepIndex <= currentHighestStep)
        {
            return;
        }

        currentHighestStep = stepIndex;

        currentProgress = Mathf.Clamp01(
            (currentHighestStep + 1f) / totalStepCount
        );

        onHighestStepChanged?.Invoke(
            currentHighestStep
        );

        onProgressChanged?.Invoke(
            currentProgress
        );

        ResetStillnessTimer();

        if (showDebugLogs)
        {
            Debug.Log(
                $"PlayerProgressMonitor: Stufe {currentHighestStep} erreicht. " +
                $"Fortschritt: {currentProgress:P0}",
                this
            );
        }

        if (currentHighestStep >= totalStepCount - 1)
        {
            TriggerSummitReached();
        }
    }


    private void StartAscent()
    {
        ascentStarted = true;
        currentStillnessTime = 0f;
        currentLookBackTime = 0f;

        if (playerRoot != null)
        {
            previousPlayerPosition =
                playerRoot.position;
        }

        onAscentStarted?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "PlayerProgressMonitor: Aufstieg gestartet.",
                this
            );
        }
    }


    private void CheckStillness()
    {
        if (!ascentStarted ||
            playerRoot == null ||
            hellFailureTriggered ||
            summitReached)
        {
            return;
        }

        Vector3 currentPosition =
            playerRoot.position;

        Vector3 movement =
            currentPosition - previousPlayerPosition;

        if (ignoreVerticalMovement)
        {
            movement.y = 0f;
        }

        float movedDistance =
            movement.magnitude;

        previousPlayerPosition =
            currentPosition;

        bool playerMoved =
            movedDistance >= movementThreshold;

        if (playerMoved)
        {
            HandleMovementResumed();
            return;
        }

        currentStillnessTime +=
            Time.deltaTime;

        if (!warningActive &&
            currentStillnessTime >= warningAfterSeconds)
        {
            TriggerStillnessWarning();
        }

        if (currentStillnessTime >=
            failureAfterSeconds)
        {
            TriggerHellFailure(
                "Der Spieler ist auf der Treppe zu lange stehen geblieben."
            );
        }
    }


    private void TriggerStillnessWarning()
    {
        if (warningActive ||
            hellFailureTriggered ||
            summitReached)
        {
            return;
        }

        warningActive = true;

        onStillnessWarning?.Invoke();

        if (showDebugLogs)
        {
            Debug.LogWarning(
                "PlayerProgressMonitor: Stillstandswarnung.",
                this
            );
        }
    }


    private void HandleMovementResumed()
    {
        bool hadWarning =
            warningActive;

        currentStillnessTime = 0f;

        if (!hadWarning)
        {
            return;
        }

        warningActive = false;

        onMovementResumed?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "PlayerProgressMonitor: Bewegung wurde fortgesetzt.",
                this
            );
        }
    }


    private void ResetStillnessTimer()
    {
        bool hadWarning =
            warningActive;

        currentStillnessTime = 0f;
        warningActive = false;

        if (playerRoot != null)
        {
            previousPlayerPosition =
                playerRoot.position;
        }

        if (hadWarning)
        {
            onMovementResumed?.Invoke();
        }
    }


    private void CheckForLookingBack()
    {
        if (hellFailureTriggered ||
            summitReached ||
            playerCamera == null ||
            staircaseForwardReference == null)
        {
            currentLookBackTime = 0f;
            return;
        }

        if (onlyDetectLookBackDuringAscent &&
            !ascentStarted)
        {
            currentLookBackTime = 0f;
            return;
        }

        Vector3 cameraForward =
            playerCamera.forward;

        Vector3 staircaseForward =
            staircaseForwardReference.forward;

        cameraForward.y = 0f;
        staircaseForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.001f ||
            staircaseForward.sqrMagnitude < 0.001f)
        {
            currentLookBackTime = 0f;
            return;
        }

        cameraForward.Normalize();
        staircaseForward.Normalize();

        float angle =
            Vector3.Angle(
                cameraForward,
                staircaseForward
            );

        bool isLookingBack =
            angle >= lookBackAngle;

        if (!isLookingBack)
        {
            currentLookBackTime = 0f;
            return;
        }

        currentLookBackTime +=
            Time.deltaTime;

        if (currentLookBackTime >=
            lookBackHoldDuration)
        {
            TriggerHellFailure(
                "Der Spieler hat auf der Treppe zurückgeschaut."
            );
        }
    }


    private void TriggerHellFailure(string reason)
    {
        if (hellFailureTriggered ||
            summitReached)
        {
            return;
        }

        hellFailureTriggered = true;
        warningActive = false;
        currentStillnessTime = 0f;
        currentLookBackTime = 0f;

        onHellFailure?.Invoke();

        if (showDebugLogs)
        {
            Debug.LogWarning(
                $"PlayerProgressMonitor: Hölle ausgelöst. Grund: {reason}",
                this
            );
        }
    }


    private void TriggerSummitReached()
    {
        if (summitReached ||
            hellFailureTriggered)
        {
            return;
        }

        summitReached = true;
        warningActive = false;
        currentStillnessTime = 0f;
        currentLookBackTime = 0f;

        onSummitReached?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "PlayerProgressMonitor: Gipfel erreicht.",
                this
            );
        }
    }


    public void ResetProgress()
    {
        currentHighestStep = -1;
        currentProgress = 0f;
        currentStillnessTime = 0f;
        currentLookBackTime = 0f;

        ascentStarted = false;
        warningActive = false;
        hellFailureTriggered = false;
        summitReached = false;

        if (playerRoot != null)
        {
            previousPlayerPosition =
                playerRoot.position;
        }

        onHighestStepChanged?.Invoke(
            currentHighestStep
        );

        onProgressChanged?.Invoke(
            currentProgress
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "PlayerProgressMonitor: Fortschritt zurückgesetzt.",
                this
            );
        }
    }
}