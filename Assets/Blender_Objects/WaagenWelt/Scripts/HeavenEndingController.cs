using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeavenEndingController : MonoBehaviour
{
    [Header("Heaven Ending UI")]
    [SerializeField] private CanvasGroup heavenEndingCanvasGroup;
    [SerializeField] private Image heavenEndingImage;
    [SerializeField] private Sprite heavenEndingSprite;
    [SerializeField] private TextMeshProUGUI endingText;

    [TextArea(2, 5)]
    [SerializeField]
    private string endingMessage =
        "Was salvation earned... or simply believed?";

    [Header("Timing")]
    [Tooltip("Zeit nach dem Erreichen des Himmels bis zum Endbild.")]
    [Min(0f)]
    [SerializeField] private float delayBeforeEnding = 6f;

    [Min(0.01f)]
    [SerializeField] private float fadeDuration = 4f;

    [Tooltip("Der Hintergrund wird beim Abschluss vollständig weiß.")]
    [SerializeField] private Color closingWhite = Color.white;

    [Header("Harps / Audio")]
    [SerializeField] private AudioSource harpSource;
    [SerializeField] private AudioClip harpClip;

    [Range(0f, 1f)]
    [SerializeField] private float harpVolume = 0.8f;

    [Tooltip("Harfensound langsam einblenden.")]
    [SerializeField] private bool fadeInHarp = true;

    [Min(0.01f)]
    [SerializeField] private float harpFadeDuration = 2f;

    [Header("Player")]
    [SerializeField] private AfterlifeSequenceManager sequenceManager;
    [SerializeField] private bool lockMovementAtEnding = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine endingRoutine;
    private bool endingStarted;

    private void Awake()
    {
        if (heavenEndingCanvasGroup != null)
        {
            heavenEndingCanvasGroup.alpha = 0f;
            heavenEndingCanvasGroup.interactable = false;
            heavenEndingCanvasGroup.blocksRaycasts = false;
        }

        if (heavenEndingImage != null &&
            heavenEndingSprite != null)
        {
            heavenEndingImage.sprite = heavenEndingSprite;
        }

        if (endingText != null)
        {
            endingText.text = endingMessage;
            endingText.color = new Color(0.12f, 0.12f, 0.15f, 1f);
        }
    }

    public void BeginHeavenEnding()
    {
        if (endingStarted)
        {
            return;
        }

        endingStarted = true;
        endingRoutine = StartCoroutine(
            HeavenEndingRoutine()
        );
    }


    public void CancelHeavenEnding()
    {
        if (endingRoutine != null)
        {
            StopCoroutine(endingRoutine);
            endingRoutine = null;
        }

        endingStarted = false;

        if (heavenEndingCanvasGroup != null)
        {
            heavenEndingCanvasGroup.alpha = 0f;
            heavenEndingCanvasGroup.interactable = false;
            heavenEndingCanvasGroup.blocksRaycasts = false;
        }

        if (harpSource != null && harpSource.isPlaying)
        {
            harpSource.Stop();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenEndingController: Himmelsende wegen Höllenpfad abgebrochen.",
                this
            );
        }
    }

    private IEnumerator HeavenEndingRoutine()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenEndingController: Himmelsende gestartet.",
                this
            );
        }

        if (harpSource != null && harpClip != null)
        {
            harpSource.clip = harpClip;
            harpSource.loop = true;

            if (fadeInHarp)
            {
                harpSource.volume = 0f;
                harpSource.Play();

                yield return FadeAudio(
                    harpSource,
                    0f,
                    harpVolume,
                    harpFadeDuration
                );
            }
            else
            {
                harpSource.volume = harpVolume;
                harpSource.Play();
            }
        }

        if (delayBeforeEnding > 0f)
        {
            yield return new WaitForSeconds(
                delayBeforeEnding
            );
        }

        if (lockMovementAtEnding &&
            sequenceManager != null)
        {
            sequenceManager.LockPlayerLocomotionPublic();
        }

        if (endingText != null)
        {
            endingText.text = endingMessage;
        }

        if (heavenEndingImage != null &&
            heavenEndingSprite != null)
        {
            heavenEndingImage.sprite =
                heavenEndingSprite;
        }

        Image[] endingImages = heavenEndingCanvasGroup != null
            ? heavenEndingCanvasGroup.GetComponentsInChildren<Image>(true)
            : null;

        if (endingImages != null)
        {
            foreach (Image image in endingImages)
            {
                if (image != null && image.name == "BrightBackground")
                {
                    image.color = closingWhite;
                    image.rectTransform.anchorMin = Vector2.zero;
                    image.rectTransform.anchorMax = Vector2.one;
                    image.rectTransform.offsetMin = Vector2.zero;
                    image.rectTransform.offsetMax = Vector2.zero;
                }
            }
        }

        yield return FadeCanvas(
            heavenEndingCanvasGroup,
            0f,
            1f,
            fadeDuration
        );

        if (heavenEndingCanvasGroup != null)
        {
            heavenEndingCanvasGroup.blocksRaycasts =
                true;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                "HeavenEndingController: Endbild sichtbar.",
                this
            );
        }

        if (sequenceManager != null)
        {
            sequenceManager.MarkSequenceFinished();
        }

        endingRoutine = null;
    }

    private IEnumerator FadeCanvas(
        CanvasGroup canvasGroup,
        float startAlpha,
        float targetAlpha,
        float duration
    )
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            float smoothProgress =
                Mathf.SmoothStep(0f, 1f, progress);

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                smoothProgress
            );

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeAudio(
        AudioSource source,
        float startVolume,
        float targetVolume,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            source.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                progress
            );

            yield return null;
        }

        source.volume = targetVolume;
    }
}
