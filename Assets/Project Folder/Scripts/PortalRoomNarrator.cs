using System.Collections;
using Autohand;
using UnityEngine;

/// <summary>
/// Spielt die beiden Einführungszeilen im ersten Portalraum genau einmal ab.
/// Die zweite Zeile beginnt erst, wenn die erste vollständig beendet ist.
/// </summary>
public sealed class PortalRoomNarrator : MonoBehaviour
{
    [Header("Voice Lines (Reihenfolge)")]
    [SerializeField] private AudioClip arrivalLine;
    [SerializeField] private AudioClip choiceLine;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float startDelay = 2.75f;
    [SerializeField, Min(0f)] private float gapAfterArrival = 1.75f;

    [Header("Audio")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
    [SerializeField] private AudioSource source;

    [Header("Raumatmosphäre")]
    [SerializeField] private AudioClip ambienceLoop;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.26f;

    [Header("Player Spawn")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 0.08f, -5.73f);
    [SerializeField] private Vector3 spawnEuler = Vector3.zero;
    [SerializeField, Min(0f)] private float spawnSettleDelay = 0.15f;
    [SerializeField] private float fallRecoveryY = -1.5f;

    private Coroutine narrationRoutine;
    private Coroutine spawnRoutine;
    private AudioSource ambienceSource;
    private AutoHandPlayer player;
    private Rigidbody playerBody;
    private bool spawnApplied;

    private void Awake()
    {
        if (source == null)
            source = GetComponent<AudioSource>();

        if (source == null)
            source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.priority = 64;

        VoiceLineReverb.Apply(source);

        if (ambienceLoop != null)
        {
            var ambienceObject = new GameObject("Portal Room Ambience");
            ambienceObject.transform.SetParent(transform, false);

            ambienceSource = ambienceObject.AddComponent<AudioSource>();
            ambienceSource.clip = ambienceLoop;
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.spatialBlend = 0f;
            ambienceSource.volume = ambienceVolume;
            ambienceSource.priority = 128;
        }
    }

    private void OnEnable()
    {
        narrationRoutine = StartCoroutine(NarrationFlow());
        spawnRoutine = StartCoroutine(PlacePlayerAfterInitialization());

        if (ambienceSource != null && ambienceSource.clip != null)
            ambienceSource.Play();
    }

    private void OnDisable()
    {
        if (narrationRoutine != null)
        {
            StopCoroutine(narrationRoutine);
            narrationRoutine = null;
        }

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (source != null)
            source.Stop();

        if (ambienceSource != null)
            ambienceSource.Stop();
    }

    private void FixedUpdate()
    {
        if (spawnApplied && player != null && player.transform.position.y < fallRecoveryY)
            ApplyPlayerSpawn();
    }

    private IEnumerator PlacePlayerAfterInitialization()
    {
        yield return null;
        yield return new WaitForFixedUpdate();

        if (spawnSettleDelay > 0f)
            yield return new WaitForSecondsRealtime(spawnSettleDelay);

        player = FindAnyObjectByType<AutoHandPlayer>();
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody>();
            ApplyPlayerSpawn();
        }

        spawnRoutine = null;
    }

    private void ApplyPlayerSpawn()
    {
        if (player == null)
            return;

        player.SetPosition(spawnPosition, Quaternion.Euler(spawnEuler));

        if (playerBody != null)
        {
            playerBody.isKinematic = false;
            playerBody.useGravity = true;
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
            playerBody.position = spawnPosition;
        }

        Physics.SyncTransforms();
        spawnApplied = true;
    }

    private IEnumerator NarrationFlow()
    {
        yield return Wait(startDelay);
        yield return PlayAndWait(arrivalLine);
        yield return Wait(gapAfterArrival);
        yield return PlayAndWait(choiceLine);
        narrationRoutine = null;
    }

    private IEnumerator PlayAndWait(AudioClip clip)
    {
        if (clip == null || source == null)
            yield break;

        source.clip = clip;
        source.Play();

        while (source.isPlaying)
            yield return null;
    }

    private static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(Mathf.Max(0f, seconds));
    }
}
