using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AfterlifeSequenceManager : MonoBehaviour
{
    public enum SequenceState
    {
        Waiting,
        Ascending,
        Warning,
        HellSequence,
        HeavenSequence,
        Finished
    }

    [Header("Current State")]
    [SerializeField]
    private SequenceState currentState = SequenceState.Waiting;


    [Header("Player / XR Origin")]
    [Tooltip("Der Root des vollständigen XR Origin.")]
    [SerializeField]
    private Transform xrOriginRoot;

    [Tooltip("Die Main Camera innerhalb des XR Origin.")]
    [SerializeField]
    private Transform playerCamera;

    [Tooltip("Character Controller auf dem XR-Origin-Root.")]
    [SerializeField]
    private CharacterController characterController;

    [Tooltip("Desktop-Steuerung für Laptop-Tests.")]
    [SerializeField]
    private DesktopPlayerController desktopController;

    [Tooltip(
        "Hier alle VR-Bewegungskomponenten eintragen, " +
        "z. B. Continuous Move, Continuous Turn, Snap Turn " +
        "und Teleportation Provider."
    )]
    [SerializeField]
    private Behaviour[] vrLocomotionBehaviours;


    [Header("Warning Atmosphere")]
    [SerializeField]
    private AudioSource choirSource;

    [Range(0f, 1f)]
    [SerializeField]
    private float normalChoirVolume = 0.55f;

    [Min(0.01f)]
    [SerializeField]
    private float warningAudioFadeDuration = 0.8f;

    [SerializeField]
    private AudioSource oneShotSource;

    [SerializeField]
    private AudioClip warningImpactClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float warningImpactVolume = 0.8f;

    [Tooltip(
        "Optionaler Schatten oder eine Vorwarnung. " +
        "Kann leer bleiben."
    )]
    [SerializeField]
    private GameObject warningEntity;

    [SerializeField]
    private Light mainSceneLight;

    [SerializeField]
    private float normalLightIntensity = 1f;

    [SerializeField]
    private float warningLightIntensity = 0.35f;

    [Min(0.01f)]
    [SerializeField]
    private float lightFadeDuration = 1.2f;


    [Header("Hell Sequence")]
    [SerializeField]
    private GameObject hellMessenger;

    [Tooltip(
        "Optionaler fester Erscheinungspunkt des Höllenboten. " +
        "Leer lassen, wenn der Bote bereits korrekt in der Szene steht."
    )]
    [SerializeField]
    private Transform hellMessengerSpawnPoint;

    [Tooltip(
        "XR Origin beim Erscheinen horizontal zum Boten ausrichten."
    )]
    [SerializeField]
    private bool faceMessengerOnHellStart = true;

    [Tooltip(
        "Dauer der weichen Drehung zum Höllenboten. " +
        "0 bedeutet sofortige Drehung."
    )]
    [Min(0f)]
    [SerializeField]
    private float faceMessengerDuration = 0.6f;

    [Tooltip(
        "Der Bote wird horizontal zum Spieler gedreht."
    )]
    [SerializeField]
    private bool messengerFacesPlayer = true;

    [SerializeField]
    private GameObject hellOpening;

    [SerializeField]
    private AudioClip hellStartClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float hellStartVolume = 0.9f;

    [Min(0f)]
    [SerializeField]
    private float hellSequenceDelay = 1.2f;


    [Header("Heaven Sequence")]
    [SerializeField]
    private GameObject heavenRoom;

    [SerializeField]
    private GameObject heavenEffects;

    [SerializeField]
    private AudioClip heavenStartClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float heavenStartVolume = 0.8f;

    [Min(0f)]
    [SerializeField]
    private float heavenSequenceDelay = 0.4f;


    [Header("Optional Events")]
    [SerializeField]
    private UnityEvent onAscentStarted;

    [SerializeField]
    private UnityEvent onWarningStarted;

    [SerializeField]
    private UnityEvent onWarningCancelled;

    [SerializeField]
    private UnityEvent onHellSequenceStarted;

    [SerializeField]
    private UnityEvent onHeavenSequenceStarted;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;


    private Coroutine choirFadeRoutine;
    private Coroutine lightFadeRoutine;
    private Coroutine activeSequenceRoutine;

    private bool[] originalLocomotionStates;

    public SequenceState CurrentState => currentState;


    private void Awake()
    {
        FindMissingPlayerReferences();
        SaveLocomotionStates();

        if (choirSource != null)
        {
            normalChoirVolume = choirSource.volume;
        }

        if (mainSceneLight != null)
        {
            normalLightIntensity = mainSceneLight.intensity;
        }

        SetOptionalObjectsToInitialState();
    }


    private void FindMissingPlayerReferences()
    {
        if (xrOriginRoot == null)
        {
            Debug.LogWarning(
                "AfterlifeSequenceManager: XR Origin Root fehlt.",
                this
            );

            return;
        }

        if (playerCamera == null)
        {
            Camera foundCamera =
                xrOriginRoot.GetComponentInChildren<Camera>(true);

            if (foundCamera != null)
            {
                playerCamera = foundCamera.transform;
            }
        }

        if (characterController == null)
        {
            characterController =
                xrOriginRoot.GetComponent<CharacterController>();
        }

        if (desktopController == null)
        {
            desktopController =
                xrOriginRoot.GetComponent<DesktopPlayerController>();
        }
    }


    private void SaveLocomotionStates()
    {
        if (vrLocomotionBehaviours == null)
        {
            originalLocomotionStates = new bool[0];
            return;
        }

        originalLocomotionStates =
            new bool[vrLocomotionBehaviours.Length];

        for (int i = 0; i < vrLocomotionBehaviours.Length; i++)
        {
            originalLocomotionStates[i] =
                vrLocomotionBehaviours[i] != null &&
                vrLocomotionBehaviours[i].enabled;
        }
    }


    private void SetOptionalObjectsToInitialState()
    {
        if (warningEntity != null)
        {
            warningEntity.SetActive(false);
        }

        if (hellMessenger != null)
        {
            hellMessenger.SetActive(false);
        }

        if (hellOpening != null)
        {
            hellOpening.SetActive(false);
        }

        if (heavenEffects != null)
        {
            heavenEffects.SetActive(false);
        }
    }


    public void StartAscent()
    {
        if (currentState != SequenceState.Waiting)
        {
            return;
        }

        currentState = SequenceState.Ascending;
        onAscentStarted?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "AfterlifeSequenceManager: Aufstieg begonnen.",
                this
            );
        }
    }


    public void StartWarning()
    {
        if (currentState != SequenceState.Ascending)
        {
            return;
        }

        currentState = SequenceState.Warning;
        StopFadeCoroutines();

        if (choirSource != null)
        {
            choirFadeRoutine = StartCoroutine(
                FadeAudio(
                    choirSource,
                    choirSource.volume,
                    0f,
                    warningAudioFadeDuration
                )
            );
        }

        if (mainSceneLight != null)
        {
            lightFadeRoutine = StartCoroutine(
                FadeLight(
                    mainSceneLight,
                    mainSceneLight.intensity,
                    warningLightIntensity,
                    lightFadeDuration
                )
            );
        }

        if (warningEntity != null)
        {
            warningEntity.SetActive(true);
        }

        PlayOneShot(
            warningImpactClip,
            warningImpactVolume
        );

        onWarningStarted?.Invoke();

        if (showDebugLogs)
        {
            Debug.LogWarning(
                "AfterlifeSequenceManager: Warnung gestartet.",
                this
            );
        }
    }


    public void CancelWarning()
    {
        if (currentState != SequenceState.Warning)
        {
            return;
        }

        currentState = SequenceState.Ascending;
        StopFadeCoroutines();

        if (choirSource != null)
        {
            if (!choirSource.isPlaying)
            {
                choirSource.Play();
            }

            choirFadeRoutine = StartCoroutine(
                FadeAudio(
                    choirSource,
                    choirSource.volume,
                    normalChoirVolume,
                    warningAudioFadeDuration
                )
            );
        }

        if (mainSceneLight != null)
        {
            lightFadeRoutine = StartCoroutine(
                FadeLight(
                    mainSceneLight,
                    mainSceneLight.intensity,
                    normalLightIntensity,
                    lightFadeDuration
                )
            );
        }

        if (warningEntity != null)
        {
            warningEntity.SetActive(false);
        }

        onWarningCancelled?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "AfterlifeSequenceManager: Warnung aufgehoben.",
                this
            );
        }
    }


    public void StartHellSequence()
    {
        /*
         * Die Höllensequenz darf erst ausgelöst werden,
         * nachdem der Spieler die Treppe betreten und
         * StartAscent() den Zustand auf Ascending gesetzt hat.
         *
         * Dadurch bewirkt ein Umdrehen vor der Treppe nichts.
         */
        if (currentState != SequenceState.Ascending &&
            currentState != SequenceState.Warning)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    $"AfterlifeSequenceManager: Höllensequenz blockiert. " +
                    $"Aktueller Zustand: {currentState}. " +
                    "Der Spieler muss zuerst die Treppe betreten.",
                    this
                );
            }

            return;
        }

        StopCurrentSequence();

        currentState = SequenceState.HellSequence;

        activeSequenceRoutine =
            StartCoroutine(HellSequenceRoutine());
    }


    private IEnumerator HellSequenceRoutine()
    {
        LockPlayerLocomotion();
        StopFadeCoroutines();

        if (choirSource != null)
        {
            yield return FadeAudio(
                choirSource,
                choirSource.volume,
                0f,
                0.5f
            );
        }

        if (warningEntity != null)
        {
            warningEntity.SetActive(false);
        }

        PositionHellMessenger();

        if (hellMessenger != null)
        {
            hellMessenger.SetActive(true);
        }

        OrientMessengerTowardPlayer();

        PlayOneShot(
            hellStartClip,
            hellStartVolume
        );

        onHellSequenceStarted?.Invoke();

        if (showDebugLogs)
        {
            Debug.LogError(
                "AfterlifeSequenceManager: Höllensequenz gestartet.",
                this
            );
        }

        if (faceMessengerOnHellStart)
        {
            yield return RotatePlayerTowardMessenger();
        }

        if (hellSequenceDelay > 0f)
        {
            yield return new WaitForSeconds(
                hellSequenceDelay
            );
        }

        if (hellOpening != null)
        {
            hellOpening.SetActive(true);
        }

        /*
         * Als Nächstes kann hier der HellSequenceController
         * Fall, Schwarzblende und Szenenwechsel übernehmen.
         */

        activeSequenceRoutine = null;
    }


    private void PositionHellMessenger()
    {
        if (hellMessenger == null ||
            hellMessengerSpawnPoint == null)
        {
            return;
        }

        hellMessenger.transform.SetPositionAndRotation(
            hellMessengerSpawnPoint.position,
            hellMessengerSpawnPoint.rotation
        );
    }


    private void OrientMessengerTowardPlayer()
    {
        if (!messengerFacesPlayer ||
            hellMessenger == null ||
            playerCamera == null)
        {
            return;
        }

        Vector3 direction =
            playerCamera.position -
            hellMessenger.transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        hellMessenger.transform.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );
    }


    private IEnumerator RotatePlayerTowardMessenger()
    {
        if (xrOriginRoot == null ||
            playerCamera == null ||
            hellMessenger == null)
        {
            yield break;
        }

        Vector3 targetDirection =
            hellMessenger.transform.position -
            playerCamera.position;

        targetDirection.y = 0f;

        if (targetDirection.sqrMagnitude < 0.001f)
        {
            yield break;
        }

        Vector3 cameraForward = playerCamera.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude < 0.001f)
        {
            yield break;
        }

        cameraForward.Normalize();
        targetDirection.Normalize();

        float requiredYaw =
            Vector3.SignedAngle(
                cameraForward,
                targetDirection,
                Vector3.up
            );

        Quaternion startRotation =
            xrOriginRoot.rotation;

        Quaternion targetRotation =
            Quaternion.AngleAxis(
                requiredYaw,
                Vector3.up
            ) * startRotation;

        if (faceMessengerDuration <= 0f)
        {
            xrOriginRoot.rotation = targetRotation;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < faceMessengerDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / faceMessengerDuration
            );

            float smoothProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            xrOriginRoot.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    smoothProgress
                );

            yield return null;
        }

        xrOriginRoot.rotation = targetRotation;
    }


    public void StartHeavenSequence()
    {
        if (currentState == SequenceState.HeavenSequence ||
            currentState == SequenceState.HellSequence ||
            currentState == SequenceState.Finished)
        {
            return;
        }

        StopCurrentSequence();

        currentState = SequenceState.HeavenSequence;

        activeSequenceRoutine =
            StartCoroutine(HeavenSequenceRoutine());
    }


    private IEnumerator HeavenSequenceRoutine()
    {
        LockPlayerLocomotion();

        if (heavenSequenceDelay > 0f)
        {
            yield return new WaitForSeconds(
                heavenSequenceDelay
            );
        }

        if (heavenRoom != null)
        {
            heavenRoom.SetActive(true);
        }

        if (heavenEffects != null)
        {
            heavenEffects.SetActive(true);
        }

        PlayOneShot(
            heavenStartClip,
            heavenStartVolume
        );

        onHeavenSequenceStarted?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                "AfterlifeSequenceManager: Himmelssequenz gestartet.",
                this
            );
        }

        activeSequenceRoutine = null;
    }


    private void LockPlayerLocomotion()
    {
        if (desktopController != null)
        {
            desktopController.enabled = false;
        }

        if (vrLocomotionBehaviours != null)
        {
            foreach (Behaviour locomotionBehaviour
                     in vrLocomotionBehaviours)
            {
                if (locomotionBehaviour != null)
                {
                    locomotionBehaviour.enabled = false;
                }
            }
        }

        /*
         * Character Controller und Main Camera bleiben aktiv.
         * Dadurch bleibt das Headset-Tracking erhalten.
         */
    }


    public void UnlockPlayerLocomotion()
    {
        if (desktopController != null)
        {
            desktopController.enabled = true;
        }

        if (vrLocomotionBehaviours == null)
        {
            return;
        }

        for (int i = 0;
             i < vrLocomotionBehaviours.Length;
             i++)
        {
            if (vrLocomotionBehaviours[i] != null)
            {
                bool shouldBeEnabled =
                    i < originalLocomotionStates.Length &&
                    originalLocomotionStates[i];

                vrLocomotionBehaviours[i].enabled =
                    shouldBeEnabled;
            }
        }
    }

    public void LockPlayerLocomotionPublic()
    {
        LockPlayerLocomotion();
    }
    
    public void MarkSequenceFinished()
    {
        currentState = SequenceState.Finished;

        if (showDebugLogs)
        {
            Debug.Log(
                "AfterlifeSequenceManager: Sequenz abgeschlossen.",
                this
            );
        }
    }


    private IEnumerator FadeAudio(
        AudioSource source,
        float startVolume,
        float targetVolume,
        float duration
    )
    {
        if (source == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            source.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / duration);

            source.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                progress
            );

            yield return null;
        }

        source.volume = targetVolume;
    }


    private IEnumerator FadeLight(
        Light targetLight,
        float startIntensity,
        float targetIntensity,
        float duration
    )
    {
        if (targetLight == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            targetLight.intensity = targetIntensity;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsed / duration);

            targetLight.intensity = Mathf.Lerp(
                startIntensity,
                targetIntensity,
                progress
            );

            yield return null;
        }

        targetLight.intensity = targetIntensity;
    }


    private void PlayOneShot(
        AudioClip clip,
        float volume
    )
    {
        if (oneShotSource == null || clip == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(
            clip,
            volume
        );
    }


    private void StopFadeCoroutines()
    {
        if (choirFadeRoutine != null)
        {
            StopCoroutine(choirFadeRoutine);
            choirFadeRoutine = null;
        }

        if (lightFadeRoutine != null)
        {
            StopCoroutine(lightFadeRoutine);
            lightFadeRoutine = null;
        }
    }


    private void StopCurrentSequence()
    {
        if (activeSequenceRoutine != null)
        {
            StopCoroutine(activeSequenceRoutine);
            activeSequenceRoutine = null;
        }
    }


    public void ResetSequence()
    {
        StopCurrentSequence();
        StopFadeCoroutines();

        currentState = SequenceState.Waiting;

        UnlockPlayerLocomotion();


        if (choirSource != null)
        {
            choirSource.volume = normalChoirVolume;

            if (!choirSource.isPlaying)
            {
                choirSource.Play();
            }
        }

        if (mainSceneLight != null)
        {
            mainSceneLight.intensity =
                normalLightIntensity;
        }

        SetOptionalObjectsToInitialState();

        if (showDebugLogs)
        {
            Debug.Log(
                "AfterlifeSequenceManager: Zurückgesetzt.",
                this
            );
        }
    }
}