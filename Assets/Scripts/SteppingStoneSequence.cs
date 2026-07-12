using System.Collections;
using UnityEngine;

public class SteppingStoneSequence : MonoBehaviour
{
    [Header("Steine in gewünschter Reihenfolge")]
    [SerializeField] private Transform[] stonesInOrder;

    [Header("Auftauch-Einstellungen")]
    [SerializeField] private float submergedOffset = 1.5f;
    [SerializeField] private float defaultRiseDuration = 1.2f;
    [SerializeField] private float delayBetweenStones = 0.4f;

    [Header("Soundeffekte")]
    [SerializeField] private AudioClip[] stoneScratchSounds;
    [SerializeField] private float soundVolume = 0.8f;
    [SerializeField] private bool matchRiseDurationToSoundLength = true;
    [SerializeField] private bool randomizeSounds = false;

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
            audioSource.spatialBlend = 1f; // 3D Sound
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

            yield return StartCoroutine(RiseStone(i));

            yield return new WaitForSeconds(delayBetweenStones);
        }
    }

    private IEnumerator RiseStone(int index)
    {
        Transform stone = stonesInOrder[index];

        Vector3 startPosition = stone.position;
        Vector3 endPosition = targetPositions[index];

        AudioClip clip = GetSoundForStone(index);

        float riseDuration = defaultRiseDuration;

        if (matchRiseDurationToSoundLength && clip != null)
        {
            riseDuration = clip.length;
        }

        if (clip != null && stoneAudioSources[index] != null)
        {
            stoneAudioSources[index].clip = clip;
            stoneAudioSources[index].volume = soundVolume;
            stoneAudioSources[index].Play();
        }

        float elapsed = 0f;

        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;

            float t = elapsed / riseDuration;
            t = Mathf.SmoothStep(0f, 1f, t);

            stone.position = Vector3.Lerp(startPosition, endPosition, t);

            yield return null;
        }

        stone.position = endPosition;

        if (disableCollidersUntilRisen && stoneColliders[index] != null)
        {
            foreach (Collider col in stoneColliders[index])
            {
                col.enabled = true;
            }
        }
    }

    private AudioClip GetSoundForStone(int index)
    {
        if (stoneScratchSounds == null || stoneScratchSounds.Length == 0)
        {
            return null;
        }

        if (randomizeSounds)
        {
            int randomIndex = Random.Range(0, stoneScratchSounds.Length);
            return stoneScratchSounds[randomIndex];
        }

        return stoneScratchSounds[index % stoneScratchSounds.Length];
    }
}