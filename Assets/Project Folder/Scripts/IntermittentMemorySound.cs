using UnityEngine;
using System.Collections;

// Reliable intermittent playback for a memory sound.
// Plays the clip once, waits a randomized silent gap, then plays again — so it
// feels like a recurring memory fragment instead of an annoying tight loop,
// without relying on AudioRandomContainer automatic triggering.
[RequireComponent(typeof(AudioSource))]
public class IntermittentMemorySound : MonoBehaviour
{
    [Tooltip("The clip to play intermittently")]
    public AudioClip clip;
    [Tooltip("Minimum silent gap between plays (seconds)")]
    public float minGap = 1.5f;
    [Tooltip("Maximum silent gap between plays (seconds)")]
    public float maxGap = 4f;
    [Tooltip("Slight pitch variation so each repeat feels different")]
    public Vector2 pitchRange = new Vector2(0.96f, 1.04f);
    [Tooltip("Start with a small random delay so multiple sounds don't sync")]
    public float maxStartDelay = 2f;

    private AudioSource src;

    private void Awake()
    {
        src = GetComponent<AudioSource>();
        // This script drives playback; make sure the source itself doesn't auto-loop/play.
        src.playOnAwake = false;
        src.loop = false;
        if (clip != null) src.clip = clip;
    }

    private void OnEnable()
    {
        StartCoroutine(PlayLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator PlayLoop()
    {
        if (clip == null) yield break;

        yield return new WaitForSeconds(Random.Range(0f, maxStartDelay));

        while (true)
        {
            src.pitch = Random.Range(pitchRange.x, pitchRange.y);
            src.Play();

            // wait for the clip to finish (account for pitch affecting duration)
            float playDuration = clip.length / Mathf.Max(0.01f, src.pitch);
            yield return new WaitForSeconds(playDuration);

            // silent gap before the memory returns
            yield return new WaitForSeconds(Random.Range(minGap, maxGap));
        }
    }
}
