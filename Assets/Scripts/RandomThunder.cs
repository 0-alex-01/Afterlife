using System.Collections;
using UnityEngine;

public class RandomThunderAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] thunderClips;
    [SerializeField] private float volume = 0.8f;

    [Header("Timing")]
    [SerializeField] private float minDelay = 8f;
    [SerializeField] private float maxDelay = 20f;
    [SerializeField] private bool playOnStart = false;

    [Header("Variation")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.05f);

    [Header("Overlap")]
    [SerializeField] private bool waitUntilClipFinished = true;

    [Header("Audio Mode")]
    [SerializeField] private bool use3DSound = false;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 80f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        if (use3DSound)
        {
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
        }
        else
        {
            audioSource.spatialBlend = 0f; // Donner als allgemeiner Himmelssound
        }
    }

    private void Start()
    {
        StartCoroutine(ThunderRoutine());
    }

    private IEnumerator ThunderRoutine()
    {
        if (playOnStart)
        {
            PlayThunder();
        }

        while (true)
        {
            float waitTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(waitTime);

            PlayThunder();

            if (waitUntilClipFinished && audioSource.clip != null)
            {
                yield return new WaitForSeconds(audioSource.clip.length);
            }
        }
    }

    private void PlayThunder()
    {
        if (thunderClips == null || thunderClips.Length == 0)
        {
            Debug.LogWarning($"{name}: Keine Thunder Clips eingetragen.");
            return;
        }

        AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];

        if (clip == null)
            return;

        if (randomizePitch)
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        else
            audioSource.pitch = 1f;

        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.Play();
    }
}