using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StaircaseDisappearController : MonoBehaviour
{
    [Header("Staircase Steps")]
    [Tooltip("Stufen von unten nach oben. Leer lassen, wenn die direkten Children korrekt sortiert sind.")]
    [SerializeField] private List<Transform> stepsInOrder =
        new List<Transform>();

    [SerializeField] private bool reverseOrder = false;

    [Header("Disappearance Settings")]
    [Min(0)]
    [SerializeField] private int safetyStepsBehindPlayer = 2;

    [Min(0f)]
    [SerializeField] private float disappearDelay = 0.4f;

    [Min(0.01f)]
    [SerializeField] private float disappearDuration = 1.1f;

    [Min(0f)]
    [SerializeField] private float sinkDistance = 1.5f;

    [SerializeField] private bool shrinkWhileDisappearing = true;

    [Range(0.01f, 1f)]
    [SerializeField] private float finalScaleMultiplier = 0.1f;

    [SerializeField] private bool disableColliderImmediately = true;

    [SerializeField] private AnimationCurve disappearCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource oneShotSource;
    [SerializeField] private AudioClip[] disappearSounds;

    [Range(0f, 1f)]
    [SerializeField] private float disappearSoundVolume = 0.55f;

    [SerializeField] private Vector2 randomPitchRange =
        new Vector2(0.94f, 1.05f);

    [Header("Optional Effects")]
    [SerializeField] private ParticleSystem disappearParticles;

    [Header("Laptop Debug Test")]
    [Tooltip("T simuliert die nächste erreichte Stufe. R setzt die Treppe zurück.")]
    [SerializeField] private bool enableKeyboardTesting = true;

    [SerializeField] private bool showDebugLogs = true;

    private readonly List<Vector3> originalPositions =
        new List<Vector3>();

    private readonly List<Quaternion> originalRotations =
        new List<Quaternion>();

    private readonly List<Vector3> originalScales =
        new List<Vector3>();

    private readonly List<Collider[]> stepColliders =
        new List<Collider[]>();

    private readonly List<Renderer[]> stepRenderers =
        new List<Renderer[]>();

    private readonly List<StairStepTrigger> stepTriggers =
        new List<StairStepTrigger>();

    private bool[] disappearanceStarted;
    private int highestReachedStep = -1;

    private void Awake()
    {
        CollectSteps();
        SaveStepData();
        ConfigureStepTriggers();
    }

    private void Start()
    {
        if (stepsInOrder.Count == 0)
        {
            Debug.LogError(
                "StaircaseDisappearController: Es wurden keine Stufen gefunden.",
                this
            );
        }
        else
        {
            Debug.Log(
                $"StaircaseDisappearController bereit: " +
                $"{stepsInOrder.Count} Stufen gefunden.",
                this
            );
        }
    }

    private void Update()
    {
        if (!enableKeyboardTesting ||
            Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            TestNextStep();
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            ResetDisappearingSteps();
        }
    }

    private void CollectSteps()
    {
        if (stepsInOrder.Count == 0)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    stepsInOrder.Add(child);
                }
            }
        }

        stepsInOrder.RemoveAll(step => step == null);

        if (reverseOrder)
        {
            stepsInOrder.Reverse();
        }

        if (showDebugLogs)
        {
            for (int i = 0; i < stepsInOrder.Count; i++)
            {
                Debug.Log(
                    $"Stufenreihenfolge {i}: {stepsInOrder[i].name}",
                    stepsInOrder[i]
                );
            }
        }
    }

    private void SaveStepData()
    {
        originalPositions.Clear();
        originalRotations.Clear();
        originalScales.Clear();
        stepColliders.Clear();
        stepRenderers.Clear();
        stepTriggers.Clear();

        disappearanceStarted =
            new bool[stepsInOrder.Count];

        foreach (Transform step in stepsInOrder)
        {
            originalPositions.Add(step.position);
            originalRotations.Add(step.rotation);
            originalScales.Add(step.localScale);

            stepColliders.Add(
                step.GetComponentsInChildren<Collider>(true)
            );

            stepRenderers.Add(
                step.GetComponentsInChildren<Renderer>(true)
            );

            stepTriggers.Add(null);
        }
    }

    private void ConfigureStepTriggers()
    {
        for (int i = 0; i < stepsInOrder.Count; i++)
        {
            Transform step = stepsInOrder[i];

            StairStepTrigger trigger =
                step.GetComponentInChildren<StairStepTrigger>(true);

            if (trigger == null)
            {
                Debug.LogError(
                    $"Auf Stufe '{step.name}' fehlt im Child " +
                    $"das StairStepTrigger-Script.",
                    step
                );

                continue;
            }

            Collider triggerCollider =
                trigger.GetComponent<Collider>();

            if (triggerCollider == null)
            {
                Debug.LogError(
                    $"Der Trigger '{trigger.name}' auf '{step.name}' " +
                    $"hat keinen Collider.",
                    trigger
                );
            }
            else if (!triggerCollider.isTrigger)
            {
                Debug.LogError(
                    $"Beim Collider von '{trigger.name}' ist " +
                    $"'Is Trigger' nicht aktiviert.",
                    trigger
                );
            }

            trigger.Initialize(this, i);
            stepTriggers[i] = trigger;
        }
    }

    public void ReportStepReached(int stepIndex)
    {
        if (stepIndex < 0 ||
            stepIndex >= stepsInOrder.Count)
        {
            Debug.LogError(
                $"Ungültiger Stufenindex: {stepIndex}",
                this
            );

            return;
        }

        if (stepIndex <= highestReachedStep)
        {
            return;
        }

        highestReachedStep = stepIndex;

        Debug.Log(
            $"STUFE ERREICHT: Index {stepIndex}, " +
            $"Name '{stepsInOrder[stepIndex].name}'.",
            this
        );

        int lastStepToDisappear =
            highestReachedStep -
            safetyStepsBehindPlayer -
            1;

        if (showDebugLogs)
        {
            Debug.Log(
                $"Bis einschließlich Index " +
                $"{lastStepToDisappear} darf verschwinden.",
                this
            );
        }

        for (int i = 0; i <= lastStepToDisappear; i++)
        {
            StartDisappearingStep(i);
        }
    }

    private void TestNextStep()
    {
        int nextStep = highestReachedStep + 1;

        if (nextStep >= stepsInOrder.Count)
        {
            Debug.Log(
                "Test abgeschlossen: letzte Stufe bereits erreicht.",
                this
            );

            return;
        }

        Debug.Log(
            $"T-TEST: Simuliere Stufe {nextStep}.",
            this
        );

        ReportStepReached(nextStep);
    }

    private void StartDisappearingStep(int index)
    {
        if (index < 0 ||
            index >= stepsInOrder.Count ||
            disappearanceStarted[index])
        {
            return;
        }

        disappearanceStarted[index] = true;

        Debug.Log(
            $"VERSCHWINDEN GESTARTET: Stufe {index}.",
            this
        );

        StartCoroutine(
            DisappearStepRoutine(index)
        );
    }

    private IEnumerator DisappearStepRoutine(int index)
    {
        if (disappearDelay > 0f)
        {
            yield return new WaitForSeconds(disappearDelay);
        }

        Transform step = stepsInOrder[index];

        if (step == null)
        {
            yield break;
        }

        PlayDisappearSound();

        if (disappearParticles != null)
        {
            disappearParticles.transform.position =
                step.position;

            disappearParticles.Play();
        }

        if (disableColliderImmediately)
        {
            SetCollidersEnabled(index, false);
        }

        Vector3 startPosition = step.position;

        Vector3 targetPosition =
            startPosition -
            Vector3.up * sinkDistance;

        Vector3 startScale = step.localScale;

        Vector3 targetScale =
            shrinkWhileDisappearing
                ? originalScales[index] *
                  finalScaleMultiplier
                : startScale;

        float elapsed = 0f;

        while (elapsed < disappearDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsed / disappearDuration
                );

            float curvedTime =
                disappearCurve.Evaluate(normalizedTime);

            step.position =
                Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    curvedTime
                );

            if (shrinkWhileDisappearing)
            {
                step.localScale =
                    Vector3.LerpUnclamped(
                        startScale,
                        targetScale,
                        curvedTime
                    );
            }

            yield return null;
        }

        step.position = targetPosition;
        step.localScale = targetScale;

        SetCollidersEnabled(index, false);
        SetRenderersEnabled(index, false);

        Debug.Log(
            $"STUFE VERSCHWUNDEN: Index {index}.",
            this
        );
    }

    private void SetCollidersEnabled(
        int index,
        bool enabled
    )
    {
        foreach (Collider stepCollider
                 in stepColliders[index])
        {
            if (stepCollider != null)
            {
                stepCollider.enabled = enabled;
            }
        }
    }

    private void SetRenderersEnabled(
        int index,
        bool enabled
    )
    {
        foreach (Renderer stepRenderer
                 in stepRenderers[index])
        {
            if (stepRenderer != null)
            {
                stepRenderer.enabled = enabled;
            }
        }
    }

    private void PlayDisappearSound()
    {
        if (oneShotSource == null ||
            disappearSounds == null ||
            disappearSounds.Length == 0)
        {
            return;
        }

        AudioClip selectedClip =
            disappearSounds[
                Random.Range(
                    0,
                    disappearSounds.Length
                )
            ];

        if (selectedClip == null)
        {
            return;
        }

        oneShotSource.pitch =
            Random.Range(
                randomPitchRange.x,
                randomPitchRange.y
            );

        oneShotSource.PlayOneShot(
            selectedClip,
            disappearSoundVolume
        );
    }

    public void ResetDisappearingSteps()
    {
        StopAllCoroutines();

        highestReachedStep = -1;

        for (int i = 0;
             i < stepsInOrder.Count;
             i++)
        {
            Transform step = stepsInOrder[i];

            if (step == null)
            {
                continue;
            }

            disappearanceStarted[i] = false;

            step.position =
                originalPositions[i];

            step.rotation =
                originalRotations[i];

            step.localScale =
                originalScales[i];

            SetRenderersEnabled(i, true);
            SetCollidersEnabled(i, true);

            if (stepTriggers[i] != null)
            {
                stepTriggers[i].ResetTrigger();
            }
        }

        Debug.Log(
            "TREPPE ZURÜCKGESETZT.",
            this
        );
    }
}