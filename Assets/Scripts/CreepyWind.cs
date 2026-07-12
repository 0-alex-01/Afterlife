using System.Collections;
using UnityEngine;

public class RandomCreepyWindSound : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip creepyWindClip;
    [SerializeField] private float volume = 0.7f;

    [Header("Timing")]
    [SerializeField] private float minDelay = 5f;
    [SerializeField] private float maxDelay = 15f;
    [SerializeField] private bool playOnStart = false;

    [Header("Variation")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.9f, 1.1f);

    [Header("3D Audio")]
    [SerializeField] private bool use3DSound = false;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 35f;

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
            audioSource.spatialBlend = 0f;
        }
    }

    private void Start()
    {
        StartCoroutine(WindRoutine());
    }

    private IEnumerator WindRoutine()
    {
        if (playOnStart)
        {
            PlayWindSound();
        }

        while (true)
        {
            float waitTime = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(waitTime);

            PlayWindSound();
        }
    }

    private void PlayWindSound()
    {
        if (creepyWindClip == null || audioSource == null)
            return;

        if (randomizePitch)
        {
            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
        }
        else
        {
            audioSource.pitch = 1f;
        }

        audioSource.PlayOneShot(creepyWindClip, volume);
    }
}
