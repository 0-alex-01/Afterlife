using System.Collections;
using UnityEngine;

public class BabySwanChirp : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] chirpClips;
    [SerializeField] private float volume = 0.6f;

    [Header("Timing")]
    [SerializeField] private float startDelay = 0f;
    [SerializeField] private float minDelay = 4f;
    [SerializeField] private float maxDelay = 12f;

    [Header("Natural Variation")]
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.15f;

    [Header("3D Audio")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 12f;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // wichtig für VR / räumlichen Sound
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
    }

    private void Start()
    {
        StartCoroutine(ChirpRoutine());
    }

    private IEnumerator ChirpRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            PlayRandomChirp();

            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);
        }
    }

    private void PlayRandomChirp()
    {
        if (chirpClips == null || chirpClips.Length == 0)
            return;

        AudioClip clip = chirpClips[Random.Range(0, chirpClips.Length)];

        if (clip == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, volume);
    }
}