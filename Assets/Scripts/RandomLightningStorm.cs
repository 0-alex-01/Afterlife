using System.Collections;
using UnityEngine;

public class RandomLightningStormController : MonoBehaviour
{
    private enum StrikeTargetMode
    {
        RandomArea,
        RandomTargetPoints,
        SequentialTargetPoints
    }

    [Header("Lightning Objects")]
    [SerializeField] private GameObject[] lightningObjects;
    [SerializeField] private bool disableAllOnStart = true;

    [Header("Lightning Child Names")]
    [SerializeField] private string startChildName = "LightningStart";
    [SerializeField] private string endChildName = "LightningEnd";

    [Header("Timing")]
    [SerializeField] private Vector2 timeBetweenStrikes = new Vector2(2f, 6f);
    [SerializeField] private Vector2 visibleDurationRange = new Vector2(0.08f, 0.2f);

    [Header("Multi Flash")]
    [SerializeField] private bool useMultipleFlashes = true;
    [SerializeField] private Vector2Int flashesPerStrikeRange = new Vector2Int(1, 3);
    [SerializeField] private Vector2 timeBetweenFlashesRange = new Vector2(0.04f, 0.12f);

    [Header("Position Mode")]
    [SerializeField] private StrikeTargetMode targetMode = StrikeTargetMode.RandomArea;

    [Header("Random Strike Area")]
    [SerializeField] private Vector2 xRange = new Vector2(-31f, 20f);
    [SerializeField] private Vector2 zRange = new Vector2(1f, 71f);
    [SerializeField] private Vector2 endYRange = new Vector2(5f, 12f);

    [Header("Sky Start Settings")]
    [SerializeField] private Vector2 startYRange = new Vector2(35f, 55f);
    [SerializeField] private float skyOffsetRadius = 8f;

    [Header("Optional Target Points")]
    [SerializeField] private Transform[] strikeTargetPoints;
    [SerializeField] private float targetPointRandomOffset = 3f;

    [Header("Thunder Sound Optional")]
    [SerializeField] private AudioSource thunderAudioSource;
    [SerializeField] private AudioClip[] thunderSounds;
    [SerializeField] private Vector2 thunderDelayRange = new Vector2(0.3f, 1.2f);
    [SerializeField] private float thunderVolume = 0.8f;
    [SerializeField] private bool playThunder = true;

    private int sequentialIndex = 0;

    private void Start()
    {
        if (disableAllOnStart)
        {
            SetAllLightningActive(false);
        }

        StartCoroutine(StormRoutine());
    }

    private IEnumerator StormRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(timeBetweenStrikes.x, timeBetweenStrikes.y);
            yield return new WaitForSeconds(waitTime);

            GameObject selectedLightning = GetRandomLightningObject();

            if (selectedLightning == null)
                continue;

            SetLightningPosition(selectedLightning);

            if (playThunder)
            {
                StartCoroutine(PlayThunderDelayed());
            }

            yield return StartCoroutine(StrikeRoutine(selectedLightning));
        }
    }

    private IEnumerator StrikeRoutine(GameObject lightning)
    {
        int flashCount = 1;

        if (useMultipleFlashes)
        {
            flashCount = Random.Range(
                flashesPerStrikeRange.x,
                flashesPerStrikeRange.y + 1
            );
        }

        for (int i = 0; i < flashCount; i++)
        {
            float visibleDuration = Random.Range(
                visibleDurationRange.x,
                visibleDurationRange.y
            );

            lightning.SetActive(true);
            yield return new WaitForSeconds(visibleDuration);
            lightning.SetActive(false);

            if (i < flashCount - 1)
            {
                float pause = Random.Range(
                    timeBetweenFlashesRange.x,
                    timeBetweenFlashesRange.y
                );

                yield return new WaitForSeconds(pause);
            }
        }
    }

    private GameObject GetRandomLightningObject()
    {
        if (lightningObjects == null || lightningObjects.Length == 0)
        {
            Debug.LogWarning($"{name}: Keine Lightning Objects eingetragen.");
            return null;
        }

        return lightningObjects[Random.Range(0, lightningObjects.Length)];
    }

    private void SetLightningPosition(GameObject lightning)
    {
        Transform start = FindDeepChild(lightning.transform, startChildName);
        Transform end = FindDeepChild(lightning.transform, endChildName);

        if (start == null || end == null)
        {
            Debug.LogWarning($"{lightning.name}: LightningStart oder LightningEnd nicht gefunden.");
            return;
        }

        Vector3 endPosition = GetStrikeEndPosition();

        Vector2 skyOffset = Random.insideUnitCircle * skyOffsetRadius;

        Vector3 startPosition = new Vector3(
            endPosition.x + skyOffset.x,
            Random.Range(startYRange.x, startYRange.y),
            endPosition.z + skyOffset.y
        );

        start.position = startPosition;
        end.position = endPosition;
    }

    private Vector3 GetStrikeEndPosition()
    {
        switch (targetMode)
        {
            case StrikeTargetMode.RandomTargetPoints:
                return GetRandomTargetPointPosition();

            case StrikeTargetMode.SequentialTargetPoints:
                return GetSequentialTargetPointPosition();

            case StrikeTargetMode.RandomArea:
            default:
                return GetRandomAreaPosition();
        }
    }

    private Vector3 GetRandomAreaPosition()
    {
        return new Vector3(
            Random.Range(xRange.x, xRange.y),
            Random.Range(endYRange.x, endYRange.y),
            Random.Range(zRange.x, zRange.y)
        );
    }

    private Vector3 GetRandomTargetPointPosition()
    {
        if (strikeTargetPoints == null || strikeTargetPoints.Length == 0)
            return GetRandomAreaPosition();

        Transform point = strikeTargetPoints[Random.Range(0, strikeTargetPoints.Length)];

        if (point == null)
            return GetRandomAreaPosition();

        return ApplyTargetPointOffset(point.position);
    }

    private Vector3 GetSequentialTargetPointPosition()
    {
        if (strikeTargetPoints == null || strikeTargetPoints.Length == 0)
            return GetRandomAreaPosition();

        Transform point = strikeTargetPoints[sequentialIndex];

        sequentialIndex++;
        if (sequentialIndex >= strikeTargetPoints.Length)
            sequentialIndex = 0;

        if (point == null)
            return GetRandomAreaPosition();

        return ApplyTargetPointOffset(point.position);
    }

    private Vector3 ApplyTargetPointOffset(Vector3 position)
    {
        Vector2 offset = Random.insideUnitCircle * targetPointRandomOffset;

        return new Vector3(
            position.x + offset.x,
            position.y,
            position.z + offset.y
        );
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);

            if (result != null)
                return result;
        }

        return null;
    }

    private void SetAllLightningActive(bool active)
    {
        if (lightningObjects == null)
            return;

        foreach (GameObject lightning in lightningObjects)
        {
            if (lightning != null)
                lightning.SetActive(active);
        }
    }

    private IEnumerator PlayThunderDelayed()
    {
        if (thunderAudioSource == null)
            yield break;

        if (thunderSounds == null || thunderSounds.Length == 0)
            yield break;

        float delay = Random.Range(thunderDelayRange.x, thunderDelayRange.y);
        yield return new WaitForSeconds(delay);

        AudioClip clip = thunderSounds[Random.Range(0, thunderSounds.Length)];

        if (clip == null)
            yield break;

        thunderAudioSource.pitch = Random.Range(0.9f, 1.05f);
        thunderAudioSource.PlayOneShot(clip, thunderVolume);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 center = new Vector3(
            (xRange.x + xRange.y) / 2f,
            (endYRange.x + endYRange.y) / 2f,
            (zRange.x + zRange.y) / 2f
        );

        Vector3 size = new Vector3(
            Mathf.Abs(xRange.y - xRange.x),
            Mathf.Abs(endYRange.y - endYRange.x),
            Mathf.Abs(zRange.y - zRange.x)
        );

        Gizmos.DrawWireCube(center, size);

        if (strikeTargetPoints != null)
        {
            Gizmos.color = Color.yellow;

            foreach (Transform point in strikeTargetPoints)
            {
                if (point == null) continue;

                Gizmos.DrawWireSphere(point.position, targetPointRandomOffset);
            }
        }
    }
}