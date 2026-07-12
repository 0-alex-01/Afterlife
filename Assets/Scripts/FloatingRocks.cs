using UnityEngine;

public class FloatingLakeStones : MonoBehaviour
{
    [Header("Stones")]
    [SerializeField] private Transform[] stones;
    [SerializeField] private bool collectChildrenAutomatically = true;

    [Header("Lake Area")]
    [SerializeField] private bool keepInsideLakeArea = true;
    [SerializeField] private Vector2 xRange = new Vector2(-31f, 20f);
    [SerializeField] private Vector2 zRange = new Vector2(1f, 71f);

    [Header("Height")]
    [SerializeField] private float waterY = 4f;
    [SerializeField] private float baseHeightAboveWater = 3f;
    [SerializeField] private float randomHeightVariation = 1f;

    [Header("Vertical Hover")]
    [SerializeField] private float bobAmplitude = 0.8f;
    [SerializeField] private float bobSpeed = 1.1f;

    [Header("Occasional Side Drift")]
    [SerializeField] private float sideDriftRadius = 1.5f;
    [SerializeField] private float sideDriftSpeed = 0.6f;
    [SerializeField] private Vector2 sideMoveTimeRange = new Vector2(3f, 7f);

    [Header("Sound / Wucht")]
    [SerializeField] private AudioClip[] movementSounds;
    [SerializeField] private float soundVolume = 0.7f;
    [SerializeField] private float soundChance = 0.5f;
    [SerializeField] private float minSoundCooldown = 2f;

    [Header("3D Audio")]
    [SerializeField] private float audioMinDistance = 2f;
    [SerializeField] private float audioMaxDistance = 30f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.85f, 1.05f);

    private Vector3[] originalBasePositions;
    private Vector3[] currentBasePositions;
    private Vector3[] targetBasePositions;

    private float[] phaseOffsets;
    private float[] sideMoveTimers;
    private float[] heightOffsets;
    private float[] lastSoundTimes;

    private AudioSource[] audioSources;

    private void Start()
    {
        if (collectChildrenAutomatically)
        {
            CollectChildStones();
        }

        InitializeStones();
    }

    private void Update()
    {
        if (stones == null)
            return;

        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null)
                continue;

            UpdateStone(i);
        }
    }

    private void CollectChildStones()
    {
        stones = new Transform[transform.childCount];

        for (int i = 0; i < transform.childCount; i++)
        {
            stones[i] = transform.GetChild(i);
        }
    }

    private void InitializeStones()
    {
        originalBasePositions = new Vector3[stones.Length];
        currentBasePositions = new Vector3[stones.Length];
        targetBasePositions = new Vector3[stones.Length];

        phaseOffsets = new float[stones.Length];
        sideMoveTimers = new float[stones.Length];
        heightOffsets = new float[stones.Length];
        lastSoundTimes = new float[stones.Length];

        audioSources = new AudioSource[stones.Length];

        for (int i = 0; i < stones.Length; i++)
        {
            if (stones[i] == null)
                continue;

            Vector3 startPos = stones[i].position;

            float randomHeight = Random.Range(-randomHeightVariation, randomHeightVariation);
            startPos.y = waterY + baseHeightAboveWater + randomHeight;

            stones[i].position = startPos;

            originalBasePositions[i] = startPos;
            currentBasePositions[i] = startPos;
            targetBasePositions[i] = startPos;

            phaseOffsets[i] = Random.Range(0f, 100f);
            sideMoveTimers[i] = Random.Range(sideMoveTimeRange.x, sideMoveTimeRange.y);
            heightOffsets[i] = randomHeight;
            lastSoundTimes[i] = -999f;

            SetupAudioSource(i);
        }
    }

    private void UpdateStone(int index)
    {
        sideMoveTimers[index] -= Time.deltaTime;

        if (sideMoveTimers[index] <= 0f)
        {
            PickNewSideTarget(index);
            sideMoveTimers[index] = Random.Range(sideMoveTimeRange.x, sideMoveTimeRange.y);
            TryPlayMovementSound(index);
        }

        currentBasePositions[index] = Vector3.Lerp(
            currentBasePositions[index],
            targetBasePositions[index],
            sideDriftSpeed * Time.deltaTime
        );

        float bob = Mathf.Sin(Time.time * bobSpeed + phaseOffsets[index]) * bobAmplitude;

        Vector3 finalPosition = currentBasePositions[index];
        finalPosition.y = waterY + baseHeightAboveWater + heightOffsets[index] + bob;

        stones[index].position = finalPosition;
    }

    private void PickNewSideTarget(int index)
    {
        Vector2 randomOffset = Random.insideUnitCircle * sideDriftRadius;

        Vector3 newTarget = originalBasePositions[index] + new Vector3(
            randomOffset.x,
            0f,
            randomOffset.y
        );

        if (keepInsideLakeArea)
        {
            newTarget.x = Mathf.Clamp(newTarget.x, xRange.x, xRange.y);
            newTarget.z = Mathf.Clamp(newTarget.z, zRange.x, zRange.y);
        }

        targetBasePositions[index] = newTarget;
    }

    private void SetupAudioSource(int index)
    {
        AudioSource source = stones[index].GetComponent<AudioSource>();

        if (source == null)
        {
            source = stones[index].gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.minDistance = audioMinDistance;
        source.maxDistance = audioMaxDistance;
        source.volume = soundVolume;

        audioSources[index] = source;
    }

    private void TryPlayMovementSound(int index)
    {
        if (movementSounds == null || movementSounds.Length == 0)
            return;

        if (audioSources == null || audioSources[index] == null)
            return;

        if (Time.time - lastSoundTimes[index] < minSoundCooldown)
            return;

        if (Random.value > soundChance)
            return;

        AudioClip clip = movementSounds[Random.Range(0, movementSounds.Length)];

        if (clip == null)
            return;

        audioSources[index].pitch = Random.Range(pitchRange.x, pitchRange.y);
        audioSources[index].PlayOneShot(clip, soundVolume);

        lastSoundTimes[index] = Time.time;
    }

    private void OnDrawGizmosSelected()
    {
        if (!keepInsideLakeArea)
            return;

        Gizmos.color = Color.cyan;

        Vector3 center = new Vector3(
            (xRange.x + xRange.y) / 2f,
            waterY + baseHeightAboveWater,
            (zRange.y + zRange.x) / 2f
        );

        Vector3 size = new Vector3(
            Mathf.Abs(xRange.y - xRange.x),
            0.1f,
            Mathf.Abs(zRange.y - zRange.x)
        );

        Gizmos.DrawWireCube(center, size);
    }
}