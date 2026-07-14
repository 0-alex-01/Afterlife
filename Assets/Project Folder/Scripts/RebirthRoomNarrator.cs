using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the authored Rebirth introduction once the VR rig has settled.
/// </summary>
public sealed class RebirthRoomNarrator : MonoBehaviour
{
    [SerializeField] private AudioSource voiceSource;
    [SerializeField] private AudioClip introduction;
    [SerializeField, Min(0f)] private float startDelay = 2.25f;

    private IEnumerator Start()
    {
        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>();

        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();

        if (introduction == null)
            yield break;

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        VoiceLineReverb.Apply(voiceSource);

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        voiceSource.clip = introduction;
        voiceSource.Play();
    }
}
