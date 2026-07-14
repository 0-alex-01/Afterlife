using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the authored Rebirth introduction once the VR rig has settled.
/// </summary>
public sealed class RebirthRoomNarrator : MonoBehaviour
{
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip introduction;
    [SerializeField] private AudioClip completion;
    [SerializeField, Min(0f)] private float startDelay = 2.25f;
    [SerializeField, Min(0f)] private float completionDelay = 0.75f;

    private TreeRevival treeRevival;
    private bool introductionFinished;
    private bool completionRequested;
    private bool completionPlayed;

    private IEnumerator Start()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        VoiceLineReverb.Apply(voiceSource);

        treeRevival = FindFirstObjectByType<TreeRevival>();
        if (treeRevival != null)
            treeRevival.RevivalCompleted += HandleRevivalCompleted;

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (introduction != null)
        {
            voiceSource.clip = introduction;
            voiceSource.Play();
            yield return new WaitWhile(() => voiceSource != null && voiceSource.isPlaying);
        }

        introductionFinished = true;
        if (completionRequested || (treeRevival != null && treeRevival.IsFullyRevived))
            yield return PlayCompletion();
    }

    private void HandleRevivalCompleted()
    {
        completionRequested = true;
        if (introductionFinished && !completionPlayed)
            StartCoroutine(PlayCompletion());
    }

    private IEnumerator PlayCompletion()
    {
        if (completionPlayed || completion == null || voiceSource == null)
            yield break;

        completionPlayed = true;
        if (completionDelay > 0f)
            yield return new WaitForSeconds(completionDelay);

        voiceSource.clip = completion;
        voiceSource.Play();
    }

    private void OnDestroy()
    {
        if (treeRevival != null)
            treeRevival.RevivalCompleted -= HandleRevivalCompleted;
    }
}
