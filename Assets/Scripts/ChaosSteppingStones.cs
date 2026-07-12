using System.Collections;
using UnityEngine;

public class ChaosSteppingStoneMovement : MonoBehaviour
{
    [Header("Steine")]
    [SerializeField] private Transform[] stones;

    [Header("Chaotische Bewegung")]
    [SerializeField] private float minMoveDuration = 0.15f;
    [SerializeField] private float maxMoveDuration = 0.45f;

    [SerializeField] private float minVerticalOffset = 0.2f;
    [SerializeField] private float maxVerticalOffset = 0.8f;

    [SerializeField] private float horizontalShakeAmount = 0.08f;

    [Header("Zurück zur Ausgangsposition")]
    [SerializeField] private bool returnToStartWhenPlayerLeaves = true;
    [SerializeField] private float returnDuration = 0.5f;

    [Header("Soundeffekte")]
    [SerializeField] private AudioClip[] stoneScratchSounds;
    [SerializeField] private float soundVolume = 0.8f;

    [Header("3D Sound Einstellungen")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 12f;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    private Vector3[] startPositions;
    private AudioSource[] audioSources;
    private Coroutine[] movementCoroutines;

    private bool playerInside = false;

    private void Start()
    {
        startPositions = new Vector3[stones.Length];
        audioSources = new AudioSource[stones.Length];
        movementCoroutines = new Coroutine[stones.Length];

        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null) continue;

            startPositions[i] = stones[i].position;

            AudioSource audioSource = stones[i].GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = stones[i].gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.volume = soundVolume;

            audioSources[i] = audioSource;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = true;

        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null) continue;

            if (movementCoroutines[i] == null)
            {
                movementCoroutines[i] = StartCoroutine(ChaoticStoneMovement(i));
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInside = false;
    }

    private IEnumerator ChaoticStoneMovement(int index)
    {
        Transform stone = stones[index];

        while (playerInside)
        {
            Vector3 currentPosition = stone.position;

            float verticalDirection = Random.value < 0.5f ? -1f : 1f;

            float randomY = Random.Range(minVerticalOffset, maxVerticalOffset) * verticalDirection;

            Vector3 targetPosition = startPositions[index] + new Vector3(
                Random.Range(-horizontalShakeAmount, horizontalShakeAmount),
                randomY,
                Random.Range(-horizontalShakeAmount, horizontalShakeAmount)
            );

            PlayRandomSound(index);

            float moveDuration = Random.Range(minMoveDuration, maxMoveDuration);
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;

                float t = elapsed / moveDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                stone.position = Vector3.Lerp(currentPosition, targetPosition, t);

                yield return null;
            }

            stone.position = targetPosition;
        }

        if (returnToStartWhenPlayerLeaves)
        {
            yield return StartCoroutine(ReturnStoneToStart(index));
        }

        movementCoroutines[index] = null;
    }

    private IEnumerator ReturnStoneToStart(int index)
    {
        Transform stone = stones[index];

        Vector3 currentPosition = stone.position;
        Vector3 targetPosition = startPositions[index];

        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / returnDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            stone.position = Vector3.Lerp(currentPosition, targetPosition, t);

            yield return null;
        }

        stone.position = targetPosition;
    }

    private void PlayRandomSound(int index)
    {
        if (stoneScratchSounds == null || stoneScratchSounds.Length == 0) return;
        if (audioSources[index] == null) return;

        int randomIndex = Random.Range(0, stoneScratchSounds.Length);
        AudioClip clip = stoneScratchSounds[randomIndex];

        if (clip == null) return;

        audioSources[index].PlayOneShot(clip, soundVolume);
    }
}
