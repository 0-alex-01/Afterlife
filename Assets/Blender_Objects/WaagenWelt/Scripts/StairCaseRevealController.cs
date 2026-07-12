using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaircaseRevealController : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Main Camera innerhalb des XR Origin.")]
    [SerializeField]
    private Transform playerHead;

    [Tooltip(
        "Punkt, den der Spieler ansehen muss. " +
        "Kann beispielsweise vor der ersten Stufe liegen."
    )]
    [SerializeField]
    private Transform staircaseLookTarget;

    [Range(-1f, 1f)]
    [Tooltip(
        "Wie genau der Spieler zur Treppe schauen muss. " +
        "0.7 bis 0.85 ist sinnvoll."
    )]
    [SerializeField]
    private float lookThreshold = 0.72f;

    [Tooltip(
        "Wie lange der Spieler zur Treppe schauen muss, " +
        "bevor sie erscheint."
    )]
    [Min(0f)]
    [SerializeField]
    private float requiredLookDuration = 0.6f;


    [Header("Staircase Steps")]
    [Tooltip(
        "Optional manuell eintragen. Bleibt die Liste leer, " +
        "werden alle marmor_step-Objekte unterhalb dieses Parents gesucht."
    )]
    [SerializeField]
    private List<Transform> stepsInOrder = new List<Transform>();

    [Tooltip(
        "Aktivieren, wenn die automatisch gefundene Reihenfolge " +
        "umgekehrt werden soll."
    )]
    [SerializeField]
    private bool reverseOrder = false;

    [Tooltip("Auch deaktivierte Child-Objekte berücksichtigen.")]
    [SerializeField]
    private bool includeInactiveChildren = true;

    [Tooltip(
        "Namensanfang der Treppenstufen. " +
        "Beispiel: marmor_step"
    )]
    [SerializeField]
    private string stepNamePrefix = "marmor_step";


    [Header("Reveal Animation")]
    [Tooltip("Gesamtdauer der Treppensequenz.")]
    [Min(0.1f)]
    [SerializeField]
    private float totalRevealDuration = 3.5f;

    [Tooltip(
        "Animationsdauer einer einzelnen Stufe. " +
        "Die Animationen überlappen sich."
    )]
    [Min(0.05f)]
    [SerializeField]
    private float singleStepDuration = 0.45f;

    [Tooltip(
        "Wie weit jede Stufe unterhalb ihrer finalen Position startet."
    )]
    [Min(0f)]
    [SerializeField]
    private float riseOffset = 0.7f;

    [Tooltip(
        "Stufen starten klein und wachsen auf ihre Originalgröße."
    )]
    [SerializeField]
    private bool animateScale = true;

    [Range(0.01f, 1f)]
    [SerializeField]
    private float hiddenScaleMultiplier = 0.15f;

    [Tooltip(
        "Ab wann der Collider während der Animation aktiviert wird."
    )]
    [Range(0f, 1f)]
    [SerializeField]
    private float colliderEnableProgress = 0.65f;

    [SerializeField]
    private AnimationCurve movementCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.65f, 0.9f),
            new Keyframe(1f, 1f)
        );


    [Header("Audio – Atmosphäre")]
    [Tooltip(
        "Loopender Chor. Play On Awake sollte deaktiviert sein."
    )]
    [SerializeField]
    private AudioSource choirSource;

    [Tooltip("Maximale Lautstärke des Chors.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float choirTargetVolume = 0.55f;

    [Tooltip("Wie lange der Chor eingeblendet wird.")]
    [Min(0.01f)]
    [SerializeField]
    private float choirFadeDuration = 3f;

    [Tooltip(
        "AudioSource für Stufen-, Harfen- und Glockenklänge."
    )]
    [SerializeField]
    private AudioSource oneShotSource;

    [Tooltip("Kurzer Klang zu Beginn der Erscheinung.")]
    [SerializeField]
    private AudioClip revealStartClip;

    [Tooltip(
        "Stein-, Wind- oder magische Klänge " +
        "beim Erscheinen einzelner Stufen."
    )]
    [SerializeField]
    private AudioClip[] stepRiseClips;

    [Tooltip(
        "Harfen-, Glocken- oder Chorakzente, " +
        "die nicht bei jeder Stufe abgespielt werden."
    )]
    [SerializeField]
    private AudioClip[] accentClips;

    [Tooltip(
        "Alle wie viele Stufen ein musikalischer Akzent erklingt."
    )]
    [Min(1)]
    [SerializeField]
    private int accentEveryNthStep = 4;

    [Tooltip("Klang nach dem vollständigen Aufbau der Treppe.")]
    [SerializeField]
    private AudioClip completionClip;

    [Range(0f, 1f)]
    [SerializeField]
    private float stepSoundVolume = 0.55f;

    [Range(0f, 1f)]
    [SerializeField]
    private float accentVolume = 0.7f;

    [SerializeField]
    private Vector2 randomPitchRange =
        new Vector2(0.95f, 1.05f);


    [Header("Optional Effects")]
    [Tooltip(
        "Optionales Partikelsystem, das zur aktuell " +
        "erscheinenden Stufe bewegt wird."
    )]
    [SerializeField]
    private ParticleSystem revealParticles;


    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    [SerializeField]
    private bool revealImmediatelyForTesting = false;


    private readonly List<Vector3> finalWorldPositions =
        new List<Vector3>();

    private readonly List<Quaternion> finalWorldRotations =
        new List<Quaternion>();

    private readonly List<Vector3> finalLocalScales =
        new List<Vector3>();

    private readonly List<Collider[]> stepColliders =
        new List<Collider[]>();

    private readonly List<Renderer[]> stepRenderers =
        new List<Renderer[]>();

    private float currentLookDuration;
    private bool revealStarted;
    private bool initializationComplete;


    private void Awake()
    {
        CollectStepsIfNecessary();

        if (stepsInOrder.Count == 0)
        {
            Debug.LogWarning(
                "StaircaseRevealController: Keine Stufen gefunden. " +
                $"Gesucht wurde nach Objekten mit dem Namensanfang " +
                $"'{stepNamePrefix}'.",
                this
            );

            initializationComplete = true;
            return;
        }

        SaveFinalTransforms();
        HideAllSteps();

        initializationComplete = true;
    }


    private void Start()
    {
        if (revealImmediatelyForTesting)
        {
            StartReveal();
        }
    }


    private void Update()
    {
        if (!initializationComplete || revealStarted)
        {
            return;
        }

        if (playerHead == null ||
            staircaseLookTarget == null)
        {
            return;
        }

        if (IsPlayerLookingAtStaircase())
        {
            currentLookDuration += Time.deltaTime;

            if (currentLookDuration >=
                requiredLookDuration)
            {
                StartReveal();
            }
        }
        else
        {
            currentLookDuration = 0f;
        }
    }


    private bool IsPlayerLookingAtStaircase()
    {
        Vector3 playerForward =
            playerHead.forward;

        Vector3 directionToTarget =
            staircaseLookTarget.position -
            playerHead.position;

        playerForward.y = 0f;
        directionToTarget.y = 0f;

        if (playerForward.sqrMagnitude < 0.001f ||
            directionToTarget.sqrMagnitude < 0.001f)
        {
            return false;
        }

        playerForward.Normalize();
        directionToTarget.Normalize();

        float dot =
            Vector3.Dot(
                playerForward,
                directionToTarget
            );

        return dot >= lookThreshold;
    }


    private void CollectStepsIfNecessary()
    {
        if (stepsInOrder == null)
        {
            stepsInOrder =
                new List<Transform>();
        }

        RemoveNullEntries();

        /*
         * Wenn manuell bereits Stufen eingetragen wurden,
         * werden diese verwendet und nur sortiert.
         */
        if (stepsInOrder.Count > 0)
        {
            SortStepsNumerically();

            if (reverseOrder)
            {
                stepsInOrder.Reverse();
            }

            LogFoundSteps("manuell eingetragen");

            return;
        }

        stepsInOrder.Clear();

        Transform[] allChildren =
            GetComponentsInChildren<Transform>(
                includeInactiveChildren
            );

        foreach (Transform child in allChildren)
        {
            if (child == transform)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(stepNamePrefix))
            {
                continue;
            }

            if (!child.name.StartsWith(
                    stepNamePrefix,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            stepsInOrder.Add(child);
        }

        SortStepsNumerically();

        if (reverseOrder)
        {
            stepsInOrder.Reverse();
        }

        LogFoundSteps("automatisch gefunden");
    }


    private void LogFoundSteps(string source)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"StaircaseRevealController: " +
            $"{stepsInOrder.Count} Stufen {source}.",
            this
        );
    }


    private void RemoveNullEntries()
    {
        for (int i = stepsInOrder.Count - 1;
             i >= 0;
             i--)
        {
            if (stepsInOrder[i] == null)
            {
                stepsInOrder.RemoveAt(i);
            }
        }
    }


    private void SortStepsNumerically()
    {
        stepsInOrder.Sort(
            (firstStep, secondStep) =>
            {
                int firstNumber =
                    ExtractStepNumber(
                        firstStep.name
                    );

                int secondNumber =
                    ExtractStepNumber(
                        secondStep.name
                    );

                int numberComparison =
                    firstNumber.CompareTo(
                        secondNumber
                    );

                /*
                 * Falls beide Objekte dieselbe Nummer liefern,
                 * zusätzlich nach Namen sortieren.
                 */
                if (numberComparison == 0)
                {
                    return string.Compare(
                        firstStep.name,
                        secondStep.name,
                        System.StringComparison.OrdinalIgnoreCase
                    );
                }

                return numberComparison;
            }
        );
    }


    private int ExtractStepNumber(string objectName)
    {
        /*
         * Unterstützt beispielsweise:
         *
         * marmor_step
         * marmor_step (1)
         * marmor_step (54)
         * marmor_step_12
         */

        int openBracket =
            objectName.LastIndexOf('(');

        int closeBracket =
            objectName.LastIndexOf(')');

        if (openBracket >= 0 &&
            closeBracket > openBracket)
        {
            string numberText =
                objectName.Substring(
                    openBracket + 1,
                    closeBracket - openBracket - 1
                );

            if (int.TryParse(
                    numberText,
                    out int bracketNumber))
            {
                return bracketNumber;
            }
        }

        string trailingDigits = "";

        for (int i = objectName.Length - 1;
             i >= 0;
             i--)
        {
            if (char.IsDigit(objectName[i]))
            {
                trailingDigits =
                    objectName[i] +
                    trailingDigits;
            }
            else if (trailingDigits.Length > 0)
            {
                break;
            }
        }

        if (int.TryParse(
                trailingDigits,
                out int trailingNumber))
        {
            return trailingNumber;
        }

        /*
         * Das Objekt ohne Nummer wird als erste Stufe behandelt.
         */
        return 0;
    }


    private void SaveFinalTransforms()
    {
        finalWorldPositions.Clear();
        finalWorldRotations.Clear();
        finalLocalScales.Clear();
        stepColliders.Clear();
        stepRenderers.Clear();

        foreach (Transform step in stepsInOrder)
        {
            finalWorldPositions.Add(
                step.position
            );

            finalWorldRotations.Add(
                step.rotation
            );

            finalLocalScales.Add(
                step.localScale
            );

            stepColliders.Add(
                step.GetComponentsInChildren<Collider>(true)
            );

            stepRenderers.Add(
                step.GetComponentsInChildren<Renderer>(true)
            );
        }
    }


    private void HideAllSteps()
    {
        if (!HasValidStoredData())
        {
            return;
        }

        for (int i = 0;
             i < stepsInOrder.Count;
             i++)
        {
            Transform step =
                stepsInOrder[i];

            if (step == null)
            {
                continue;
            }

            step.position =
                finalWorldPositions[i] -
                Vector3.up * riseOffset;

            step.rotation =
                finalWorldRotations[i];

            if (animateScale)
            {
                step.localScale =
                    finalLocalScales[i] *
                    hiddenScaleMultiplier;
            }

            SetRenderersEnabled(
                i,
                false
            );

            SetCollidersEnabled(
                i,
                false
            );
        }
    }


    private bool HasValidStoredData()
    {
        return
            finalWorldPositions.Count ==
            stepsInOrder.Count &&
            finalWorldRotations.Count ==
            stepsInOrder.Count &&
            finalLocalScales.Count ==
            stepsInOrder.Count &&
            stepColliders.Count ==
            stepsInOrder.Count &&
            stepRenderers.Count ==
            stepsInOrder.Count;
    }


    public void StartReveal()
    {
        if (revealStarted)
        {
            return;
        }

        if (stepsInOrder == null ||
            stepsInOrder.Count == 0)
        {
            Debug.LogWarning(
                "StaircaseRevealController: Reveal konnte nicht starten, " +
                "weil keine Stufen vorhanden sind.",
                this
            );

            return;
        }

        if (!HasValidStoredData())
        {
            Debug.LogError(
                "StaircaseRevealController: Gespeicherte " +
                "Stufendaten sind unvollständig.",
                this
            );

            return;
        }

        revealStarted = true;

        if (showDebugLogs)
        {
            Debug.Log(
                $"StaircaseRevealController: Reveal beginnt mit " +
                $"{stepsInOrder.Count} Stufen.",
                this
            );
        }

        StartCoroutine(
            RevealSequence()
        );
    }


    private IEnumerator RevealSequence()
    {
        PlayOneShot(
            revealStartClip,
            accentVolume
        );

        if (choirSource != null)
        {
            StartCoroutine(
                FadeInChoir()
            );
        }

        float delayBetweenSteps =
            CalculateDelayBetweenSteps();

        for (int i = 0;
             i < stepsInOrder.Count;
             i++)
        {
            StartCoroutine(
                RevealSingleStep(i)
            );

            PlayRandomClip(
                stepRiseClips,
                stepSoundVolume
            );

            if (accentEveryNthStep > 0 &&
                (i + 1) %
                accentEveryNthStep == 0)
            {
                PlayRandomClip(
                    accentClips,
                    accentVolume
                );
            }

            if (delayBetweenSteps > 0f)
            {
                yield return new WaitForSeconds(
                    delayBetweenSteps
                );
            }
        }

        yield return new WaitForSeconds(
            singleStepDuration
        );

        PlayOneShot(
            completionClip,
            accentVolume
        );

        if (showDebugLogs)
        {
            Debug.Log(
                "StaircaseRevealController: " +
                "Treppe vollständig aufgebaut.",
                this
            );
        }
    }


    private float CalculateDelayBetweenSteps()
    {
        if (stepsInOrder.Count <= 1)
        {
            return 0f;
        }

        float safeTotalDuration =
            Mathf.Max(
                totalRevealDuration,
                singleStepDuration
            );

        float availableDelayTime =
            Mathf.Max(
                0f,
                safeTotalDuration -
                singleStepDuration
            );

        return availableDelayTime /
               (stepsInOrder.Count - 1);
    }


    private IEnumerator RevealSingleStep(int index)
    {
        if (index < 0 ||
            index >= stepsInOrder.Count)
        {
            yield break;
        }

        Transform step =
            stepsInOrder[index];

        if (step == null)
        {
            yield break;
        }

        Vector3 startPosition =
            finalWorldPositions[index] -
            Vector3.up * riseOffset;

        Vector3 targetPosition =
            finalWorldPositions[index];

        Vector3 startScale =
            animateScale
                ? finalLocalScales[index] *
                  hiddenScaleMultiplier
                : finalLocalScales[index];

        Vector3 targetScale =
            finalLocalScales[index];

        step.position =
            startPosition;

        step.rotation =
            finalWorldRotations[index];

        step.localScale =
            startScale;

        SetRenderersEnabled(
            index,
            true
        );

        if (revealParticles != null)
        {
            revealParticles.transform.position =
                targetPosition;

            revealParticles.Play();
        }

        float elapsed = 0f;
        bool colliderWasEnabled = false;

        while (elapsed <
               singleStepDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime =
                Mathf.Clamp01(
                    elapsed /
                    singleStepDuration
                );

            float curvedTime =
                movementCurve.Evaluate(
                    normalizedTime
                );

            step.position =
                Vector3.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    curvedTime
                );

            if (animateScale)
            {
                step.localScale =
                    Vector3.LerpUnclamped(
                        startScale,
                        targetScale,
                        curvedTime
                    );
            }

            if (!colliderWasEnabled &&
                normalizedTime >=
                colliderEnableProgress)
            {
                SetCollidersEnabled(
                    index,
                    true
                );

                colliderWasEnabled = true;
            }

            yield return null;
        }

        step.position =
            targetPosition;

        step.rotation =
            finalWorldRotations[index];

        step.localScale =
            targetScale;

        SetCollidersEnabled(
            index,
            true
        );
    }


    private IEnumerator FadeInChoir()
    {
        if (choirSource == null)
        {
            yield break;
        }

        choirSource.volume = 0f;

        if (!choirSource.isPlaying)
        {
            choirSource.Play();
        }

        float elapsed = 0f;

        while (elapsed <
               choirFadeDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    choirFadeDuration
                );

            choirSource.volume =
                Mathf.Lerp(
                    0f,
                    choirTargetVolume,
                    progress
                );

            yield return null;
        }

        choirSource.volume =
            choirTargetVolume;
    }


    private void PlayRandomClip(
        AudioClip[] clips,
        float volume)
    {
        if (clips == null ||
            clips.Length == 0)
        {
            return;
        }

        AudioClip selectedClip =
            clips[
                Random.Range(
                    0,
                    clips.Length
                )
            ];

        PlayOneShot(
            selectedClip,
            volume
        );
    }


    private void PlayOneShot(
        AudioClip clip,
        float volume)
    {
        if (oneShotSource == null ||
            clip == null)
        {
            return;
        }

        oneShotSource.pitch =
            Random.Range(
                randomPitchRange.x,
                randomPitchRange.y
            );

        oneShotSource.PlayOneShot(
            clip,
            volume
        );
    }


    private void SetCollidersEnabled(
        int index,
        bool enabled)
    {
        if (index < 0 ||
            index >= stepColliders.Count)
        {
            return;
        }

        foreach (Collider stepCollider
                 in stepColliders[index])
        {
            if (stepCollider != null)
            {
                stepCollider.enabled =
                    enabled;
            }
        }
    }


    private void SetRenderersEnabled(
        int index,
        bool enabled)
    {
        if (index < 0 ||
            index >= stepRenderers.Count)
        {
            return;
        }

        foreach (Renderer stepRenderer
                 in stepRenderers[index])
        {
            if (stepRenderer != null)
            {
                stepRenderer.enabled =
                    enabled;
            }
        }
    }


    [ContextMenu("Recollect Staircase Steps")]
    private void RecollectStaircaseSteps()
    {
        stepsInOrder.Clear();
        CollectStepsIfNecessary();

        Debug.Log(
            $"StaircaseRevealController: " +
            $"{stepsInOrder.Count} Stufen neu gesammelt.",
            this
        );
    }


    [ContextMenu("Reset Staircase Preview")]
    public void ResetStaircasePreview()
    {
        StopAllCoroutines();

        revealStarted = false;
        currentLookDuration = 0f;

        if (choirSource != null)
        {
            choirSource.Stop();
            choirSource.volume = 0f;
        }

        if (!HasValidStoredData())
        {
            CollectStepsIfNecessary();
            SaveFinalTransforms();
        }

        HideAllSteps();

        if (showDebugLogs)
        {
            Debug.Log(
                "StaircaseRevealController: " +
                "Treppe zurückgesetzt.",
                this
            );
        }
    }


    [ContextMenu("Reveal Staircase Now")]
    private void RevealStaircaseNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Der Test funktioniert nur im Play Mode.",
                this
            );

            return;
        }

        StartReveal();
    }
}