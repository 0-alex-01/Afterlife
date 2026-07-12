using System.Collections;
using UnityEngine;

public class ChaosSteppingStoneSequence : MonoBehaviour
{
    [Header("Steine in gewünschter Reihenfolge")]
    [SerializeField] private Transform[] stonesInOrder;

    [Header("Startposition")]
    [SerializeField] private float submergedOffset = 1.5f;
    [SerializeField] private float delayBetweenStones = 0.4f;

    [Header("Chaotische Auftauch-Bewegung")]
    [SerializeField] private float chaosRiseDuration = 3f;
    [SerializeField] private float minStepDuration = 0.12f;
    [SerializeField] private float maxStepDuration = 0.35f;
    [SerializeField] private float verticalChaosAmount = 0.8f;
    [SerializeField] private float horizontalShakeAmount = 0.15f;
    [SerializeField] private float finalSnapDuration = 0.4f;

    [Header("Soundeffekte")]
    [SerializeField] private AudioClip[] stoneScratchSounds;
    [SerializeField] private float soundVolume = 0.8f;

    [Header("3D Sound Einstellungen")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 12f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool startOnlyOnce = true;

    [Header("Kollision")]
    [SerializeField] private bool disableCollidersUntilRisen = true;

    private Vector3[] targetPositions;
    private Collider[][] stoneColliders;
    private AudioSource[] stoneAudioSources;

    private bool hasStarted = false;

    private void Start()
    {
        targetPositions = new Vector3[stonesInOrder.Length];
        stoneColliders = new Collider[stonesInOrder.Length][];
        stoneAudioSources = new AudioSource[stonesInOrder.Length];

        for (int i = 0; i < stonesInOrder.Length; i++)
        {
            if (stonesInOrder[i] == null) continue;

            targetPositions[i] = stonesInOrder[i].position;

            Vector3 hiddenPosition = targetPositions[i] - new Vector3(0f, submergedOffset, 0f);
            stonesInOrder[i].position = hiddenPosition;

            stoneColliders[i] = stonesInOrder[i].GetComponentsInChildren<Collider>();

            if (disableCollidersUntilRisen)
            {
                foreach (Collider col in stoneColliders[i])
                {
                    col.enabled = false;
                }
            }

            AudioSource audioSource = stonesInOrder[i].GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = stonesInOrder[i].gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.volume = soundVolume;

            stoneAudioSources[i] = audioSource;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (startOnlyOnce && hasStarted) return;

        hasStarted = true;
        StartCoroutine(RiseStonesSequence());
    }

    private IEnumerator RiseStonesSequence()
    {
        for (int i = 0; i < stonesInOrder.Length; i++)
        {
            if (stonesInOrder[i] == null) continue;

            yield return StartCoroutine(ChaosRiseStone(i));

            yield return new WaitForSeconds(delayBetweenStones);
        }
    }

    private IEnumerator ChaosRiseStone(int index)
    {
        Transform stone = stonesInOrder[index];

        Vector3 startPosition = stone.position;
        Vector3 finalPosition = targetPositions[index];

        float elapsed = 0f;
        bool moveUp = true;

        while (elapsed < chaosRiseDuration)
        {
            float stepDuration = Random.Range(minStepDuration, maxStepDuration);

            Vector3 currentPosition = stone.position;

            float nextProgress = Mathf.Clamp01((elapsed + stepDuration) / chaosRiseDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, nextProgress);

            Vector3 baseTarget = Vector3.Lerp(startPosition, finalPosition, smoothProgress);

            float verticalDirection = moveUp ? 1f : -1f;

            Vector3 chaosOffset = new Vector3(
                Random.Range(-horizontalShakeAmount, horizontalShakeAmount),
                verticalDirection * Random.Range(0.2f, verticalChaosAmount),
                Random.Range(-horizontalShakeAmount, horizontalShakeAmount)
            );

            Vector3 chaoticTarget = baseTarget + chaosOffset;

            PlayRandomStoneSound(index);

            float stepElapsed = 0f;

            while (stepElapsed < stepDuration)
            {
                stepElapsed += Time.deltaTime;
                elapsed += Time.deltaTime;

                float t = stepElapsed / stepDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                stone.position = Vector3.Lerp(currentPosition, chaoticTarget, t);

                yield return null;
            }

            moveUp = !moveUp;
        }

        yield return StartCoroutine(SnapToFinalPosition(stone, finalPosition));

        if (disableCollidersUntilRisen && stoneColliders[index] != null)
        {
            foreach (Collider col in stoneColliders[index])
            {
                col.enabled = true;
            }
        }
    }

    private IEnumerator SnapToFinalPosition(Transform stone, Vector3 finalPosition)
    {
        Vector3 startPosition = stone.position;
        float elapsed = 0f;

        while (elapsed < finalSnapDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / finalSnapDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            stone.position = Vector3.Lerp(startPosition, finalPosition, t);

            yield return null;
        }

        stone.position = finalPosition;
    }

    private void PlayRandomStoneSound(int index)
    {
        if (stoneScratchSounds == null || stoneScratchSounds.Length == 0) return;
        if (stoneAudioSources[index] == null) return;

        int randomIndex = Random.Range(0, stoneScratchSounds.Length);
        AudioClip randomClip = stoneScratchSounds[randomIndex];

        if (randomClip == null) return;

        stoneAudioSources[index].PlayOneShot(randomClip, soundVolume);
    }
}