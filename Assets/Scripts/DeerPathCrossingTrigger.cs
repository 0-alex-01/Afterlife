using System.Collections;
using UnityEngine;

public class DeerPathCrossingTrigger : MonoBehaviour
{
    [Header("XR Player")]
    [Tooltip("Hier das vollständige Complete XR Origin hineinziehen.")]
    [SerializeField] private Transform xrOrigin;

    [Header("Reh-Vorlagen")]
    [Tooltip("Ein oder mehrere Reh-Prefabs. Bei nur einem Prefab wird es mehrfach dupliziert.")]
    [SerializeField] private GameObject[] deerPrefabs;

    [Tooltip("Wie viele Rehe über den Weg laufen sollen.")]
    [Min(1)]
    [SerializeField] private int deerCount = 3;

    [Header("Positionen")]
    [Tooltip("Punkt links neben dem Weg, an dem die Rehe erscheinen.")]
    [SerializeField] private Transform leftSpawnPoint;

    [Tooltip("Punkt rechts hinter dem Weg, an dem die Rehe verschwinden.")]
    [SerializeField] private Transform rightDespawnPoint;

    [Tooltip("Optionaler Parent für die während des Spiels erzeugten Rehe.")]
    [SerializeField] private Transform spawnedDeerParent;

    [Header("Ablauf")]
    [SerializeField] private bool triggerOnlyOnce = true;

    [Tooltip("Verzögerung nach Betreten des Triggers, bevor das erste Reh erscheint.")]
    [SerializeField] private float startDelay = 0.2f;

    [Tooltip("Zufälliger zeitlicher Abstand zwischen den Rehen: X = Minimum, Y = Maximum.")]
    [SerializeField] private Vector2 spawnIntervalRange =
        new Vector2(0.4f, 0.8f);

    [Tooltip("Seitlicher Abstand zwischen den Rehen.")]
    [SerializeField] private float formationSpacing = 0.8f;

    [Tooltip("Zusätzliche zufällige Abweichung der einzelnen Laufbahnen.")]
    [SerializeField] private float pathVariation = 0.35f;

    [Header("Individuelle Bewegung")]
    [Tooltip("Jedes Reh erhält eine zufällige Laufgeschwindigkeit: X = Minimum, Y = Maximum.")]
    [SerializeField] private Vector2 runSpeedRange =
        new Vector2(3.4f, 4.8f);

    [Tooltip("Jedes Reh erhält eine etwas andere Drehgeschwindigkeit.")]
    [SerializeField] private Vector2 turnSpeedRange =
        new Vector2(7f, 12f);

    [Tooltip("Ab diesem Abstand zum Endpunkt wird das Reh entfernt.")]
    [SerializeField] private float despawnDistance = 0.15f;

    [Tooltip("Rehe behalten während des Laufens ihre Spawn-Höhe.")]
    [SerializeField] private bool keepSpawnHeight = true;

    [Header("Individuelle Animation")]
    [Tooltip("Exakter Name des Run-States im Animator.")]
    [SerializeField] private string runStateName = "Deer_001_run";

    [Tooltip("Jedes Reh erhält eine andere Animationsgeschwindigkeit.")]
    [SerializeField] private Vector2 animationSpeedRange =
        new Vector2(0.85f, 1.2f);

    [Tooltip("Kleine Größenunterschiede zwischen den Rehen.")]
    [SerializeField] private Vector2 scaleRange =
        new Vector2(0.95f, 1.05f);

    [Header("Model Direction Fix")]
    [Tooltip("Falls die Rehe rückwärts laufen, meistens 180 eintragen.")]
    [SerializeField] private float modelYawOffset = 0f;

    [Header("Bestehende Komponenten")]
    [Tooltip("Deaktiviert AnimalFleeAI auf den erzeugten Kopien.")]
    [SerializeField] private bool disableAnimalFleeAI = true;

    [Tooltip("Deaktiviert Collider der laufenden Rehe, damit der Player nicht blockiert wird.")]
    [SerializeField] private bool disableDeerColliders = true;

    [Header("Galopp-Sound")]
    [Tooltip("Optionaler, nahtloser Galopp-Loop.")]
    [SerializeField] private AudioClip gallopLoopClip;

    [Tooltip("Alternativ mehrere einzelne Hufschlag-Sounds.")]
    [SerializeField] private AudioClip[] gallopStepSounds;

    [SerializeField] private float gallopVolume = 0.8f;

    [Tooltip("Zeit zwischen einzelnen Hufschlägen, wenn kein Loop verwendet wird.")]
    [SerializeField] private float gallopStepInterval = 0.32f;

    [Tooltip("Zufällige Tonhöhenvariation der Galopp-Sounds.")]
    [SerializeField] private Vector2 gallopPitchRange =
        new Vector2(0.9f, 1.1f);

    [SerializeField] private float audioMinDistance = 1.5f;
    [SerializeField] private float audioMaxDistance = 18f;

    private bool hasTriggered;
    private bool isSpawning;

    private void Awake()
    {
        if (deerPrefabs == null)
            return;

        // In dieser Szene sind die drei Vorlagen echte Szenenobjekte. Sie dürfen
        // erst als erzeugte Kopien sichtbar werden und nicht über dem Weg schweben.
        foreach (GameObject deerTemplate in deerPrefabs)
        {
            if (deerTemplate != null && deerTemplate.scene.IsValid())
                deerTemplate.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsXRPlayer(other))
            return;

        TriggerCrossing();
    }

    public void TriggerCrossing()
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        if (isSpawning)
            return;

        if (deerPrefabs == null || deerPrefabs.Length == 0)
        {
            Debug.LogWarning(
                "DeerPathCrossingTrigger: Es wurde kein Reh-Prefab eingetragen."
            );

            return;
        }

        if (leftSpawnPoint == null || rightDespawnPoint == null)
        {
            Debug.LogWarning(
                "DeerPathCrossingTrigger: Spawn Point oder Despawn Point fehlt."
            );

            return;
        }

        hasTriggered = true;
        isSpawning = true;

        StartCoroutine(CrossingSequence());
    }

    public void RebindPlayer(Transform livePlayer)
    {
        if (livePlayer != null)
            xrOrigin = livePlayer;
    }

    private IEnumerator CrossingSequence()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        Vector3 travelDirection =
            rightDespawnPoint.position - leftSpawnPoint.position;

        travelDirection.y = 0f;

        if (travelDirection.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning(
                "DeerPathCrossingTrigger: Spawn Point und Endpunkt liegen zu nah beieinander."
            );

            isSpawning = false;
            yield break;
        }

        travelDirection.Normalize();

        // Seitliche Richtung relativ zur Laufrichtung.
        Vector3 formationDirection =
            Vector3.Cross(Vector3.up, travelDirection).normalized;

        int safeDeerCount = Mathf.Max(1, deerCount);
        float centerIndex = (safeDeerCount - 1) * 0.5f;

        for (int i = 0; i < safeDeerCount; i++)
        {
            GameObject prefab =
                deerPrefabs[i % deerPrefabs.Length];

            if (prefab == null)
                continue;

            float formationOffset =
                (i - centerIndex) * formationSpacing;

            // Jedes Reh beginnt und endet etwas anders.
            float randomSpawnOffset =
                Random.Range(-pathVariation, pathVariation);

            float randomTargetOffset =
                Random.Range(-pathVariation, pathVariation);

            Vector3 spawnPosition =
                leftSpawnPoint.position
                + formationDirection
                * (formationOffset + randomSpawnOffset);

            Vector3 targetPosition =
                rightDespawnPoint.position
                + formationDirection
                * (formationOffset + randomTargetOffset);

            GameObject deer = Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity,
                spawnedDeerParent
            );
            deer.SetActive(true);

            // Jedes Reh erhält eigene Werte.
            float individualRunSpeed =
                RandomValue(runSpeedRange);

            float individualTurnSpeed =
                RandomValue(turnSpeedRange);

            float individualAnimationSpeed =
                RandomValue(animationSpeedRange);

            float individualScale =
                RandomValue(scaleRange);

            deer.transform.localScale *= individualScale;

            Animator deerAnimator = PrepareDeer(
                deer,
                individualAnimationSpeed
            );

            StartCoroutine(
                RunDeerAcrossPath(
                    deer,
                    deerAnimator,
                    targetPosition,
                    individualRunSpeed,
                    individualTurnSpeed
                )
            );

            if (i < safeDeerCount - 1)
            {
                float randomInterval =
                    RandomValue(spawnIntervalRange);

                if (randomInterval > 0f)
                {
                    yield return new WaitForSeconds(
                        randomInterval
                    );
                }
            }
        }

        isSpawning = false;
    }

    private Animator PrepareDeer(
        GameObject deer,
        float animationSpeed
    )
    {
        if (deer == null)
            return null;

        if (disableAnimalFleeAI)
        {
            AnimalFleeAI[] fleeScripts =
                deer.GetComponentsInChildren<AnimalFleeAI>(
                    true
                );

            foreach (AnimalFleeAI fleeScript in fleeScripts)
            {
                fleeScript.enabled = false;
            }
        }

        Animator animator =
            deer.GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            // Das Event-Skript bewegt das Reh.
            animator.applyRootMotion = false;

            // Jedes Reh läuft mit einer leicht anderen
            // Animationsgeschwindigkeit.
            animator.speed = animationSpeed;

            if (!string.IsNullOrWhiteSpace(runStateName))
            {
                // Zufällige Position innerhalb der Animation.
                // Dadurch beginnen die Beine nicht synchron.
                float randomAnimationStart =
                    Random.Range(0f, 1f);

                animator.CrossFade(
                    runStateName,
                    0.1f,
                    0,
                    randomAnimationStart
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "DeerPathCrossingTrigger: Auf einem erzeugten Reh wurde kein Animator gefunden."
            );
        }

        if (disableDeerColliders)
        {
            Collider[] deerColliders =
                deer.GetComponentsInChildren<Collider>(true);

            foreach (Collider deerCollider in deerColliders)
            {
                deerCollider.enabled = false;
            }
        }

        Rigidbody[] rigidbodies =
            deer.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody deerRigidbody in rigidbodies)
        {
            deerRigidbody.useGravity = false;
            deerRigidbody.isKinematic = true;
        }

        return animator;
    }

    private IEnumerator RunDeerAcrossPath(
        GameObject deer,
        Animator animator,
        Vector3 targetPosition,
        float individualRunSpeed,
        float individualTurnSpeed
    )
    {
        if (deer == null)
            yield break;

        Transform deerTransform = deer.transform;
        float fixedY = deerTransform.position.y;

        AudioSource audioSource =
            CreateGallopAudioSource(deer);

        // Auch die einzelnen Hufschläge beginnen nicht gleichzeitig.
        float nextStepSoundTime =
            Time.time
            + Random.Range(
                0f,
                Mathf.Max(0.01f, gallopStepInterval)
            );

        if (
            audioSource != null
            && gallopLoopClip != null
        )
        {
            audioSource.clip = gallopLoopClip;
            audioSource.loop = true;

            // Galopp-Loops starten nicht alle an derselben Stelle.
            if (gallopLoopClip.length > 0.1f)
            {
                audioSource.time =
                    Random.Range(
                        0f,
                        gallopLoopClip.length
                    );
            }

            audioSource.Play();
        }

        while (deer != null)
        {
            Vector3 currentTarget = targetPosition;

            if (keepSpawnHeight)
            {
                currentTarget.y = fixedY;
            }

            Vector3 direction =
                currentTarget - deerTransform.position;

            direction.y = 0f;

            if (direction.magnitude <= despawnDistance)
                break;

            Vector3 normalizedDirection =
                direction.normalized;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    normalizedDirection,
                    Vector3.up
                )
                * Quaternion.Euler(
                    0f,
                    modelYawOffset,
                    0f
                );

            deerTransform.rotation =
                Quaternion.Slerp(
                    deerTransform.rotation,
                    targetRotation,
                    individualTurnSpeed * Time.deltaTime
                );

            Vector3 newPosition =
                Vector3.MoveTowards(
                    deerTransform.position,
                    currentTarget,
                    individualRunSpeed * Time.deltaTime
                );

            if (keepSpawnHeight)
            {
                newPosition.y = fixedY;
            }

            deerTransform.position = newPosition;

            if (
                audioSource != null
                && gallopLoopClip == null
                && gallopStepSounds != null
                && gallopStepSounds.Length > 0
                && Time.time >= nextStepSoundTime
            )
            {
                AudioClip randomStep =
                    gallopStepSounds[
                        Random.Range(
                            0,
                            gallopStepSounds.Length
                        )
                    ];

                if (randomStep != null)
                {
                    audioSource.pitch =
                        RandomValue(gallopPitchRange);

                    audioSource.PlayOneShot(
                        randomStep,
                        gallopVolume
                    );
                }

                nextStepSoundTime =
                    Time.time
                    + gallopStepInterval
                    + Random.Range(-0.06f, 0.06f);
            }

            yield return null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (animator != null)
        {
            animator.speed = 1f;
        }

        if (deer != null)
        {
            Destroy(deer);
        }
    }

    private AudioSource CreateGallopAudioSource(
        GameObject deer
    )
    {
        bool hasLoop =
            gallopLoopClip != null;

        bool hasStepSounds =
            gallopStepSounds != null
            && gallopStepSounds.Length > 0;

        if (!hasLoop && !hasStepSounds)
            return null;

        AudioSource audioSource =
            deer.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = gallopVolume;

        audioSource.pitch =
            RandomValue(gallopPitchRange);

        // Vollständiger 3D-Sound.
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = audioMinDistance;
        audioSource.maxDistance = audioMaxDistance;

        audioSource.rolloffMode =
            AudioRolloffMode.Linear;

        return audioSource;
    }

    private float RandomValue(Vector2 range)
    {
        float minimum =
            Mathf.Min(range.x, range.y);

        float maximum =
            Mathf.Max(range.x, range.y);

        return Random.Range(minimum, maximum);
    }

    private bool IsXRPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (xrOrigin == null)
            return false;

        if (other.transform == xrOrigin)
            return true;

        if (other.transform.IsChildOf(xrOrigin))
            return true;

        if (other.attachedRigidbody != null)
        {
            Transform rigidbodyTransform =
                other.attachedRigidbody.transform;

            if (rigidbodyTransform == xrOrigin)
                return true;

            if (rigidbodyTransform.IsChildOf(xrOrigin))
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (
            leftSpawnPoint == null
            || rightDespawnPoint == null
        )
        {
            return;
        }

        Gizmos.color = Color.white;

        Gizmos.DrawLine(
            leftSpawnPoint.position,
            rightDespawnPoint.position
        );

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            leftSpawnPoint.position,
            0.3f
        );

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            rightDespawnPoint.position,
            0.3f
        );
    }
}
