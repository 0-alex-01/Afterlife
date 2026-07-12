using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CaveWisdomTransitionTrigger : MonoBehaviour
{
    [Header("XR Player")]
    [Tooltip("Hier das komplette Complete XR Origin eintragen.")]
    [SerializeField] private Transform xrOrigin;

    [Tooltip("Hier die Main Camera innerhalb des XR Origin eintragen.")]
    [SerializeField] private Transform playerCamera;

    [Tooltip("Optionaler Fallback, falls keine XR-Origin-Referenz erkannt wird.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Trigger")]
    [SerializeField] private bool triggerOnlyOnce = true;

    [Tooltip("Optionale Verzögerung nach Betreten des Höhlen-Triggers.")]
    [SerializeField] private float startDelay = 0f;

    [Header("VR-Bewegung deaktivieren")]
    [Tooltip(
        "Zum Beispiel Continuous Move Provider, Dynamic Move Provider " +
        "oder Teleportation Provider eintragen."
    )]
    [SerializeField] private Behaviour[] movementScriptsToDisable;

    [Header("Automatisches VR-Canvas")]
    [Tooltip(
        "Erstellt automatisch ein Canvas vor der XR-Kamera. " +
        "Die UI-Referenzen dürfen dann leer bleiben."
    )]
    [SerializeField] private bool createUIAutomatically = true;

    [Tooltip("Abstand des Canvas vor der XR-Kamera.")]
    [SerializeField] private float canvasDistanceFromCamera = 0.3f;

    [SerializeField] private Vector2 canvasSize =
        new Vector2(3000f, 2000f);

    [SerializeField] private float canvasScale = 0.001f;

    [Header("Optionale eigene UI-Referenzen")]
    [Tooltip("Kann leer bleiben, wenn die UI automatisch erstellt wird.")]
    [SerializeField] private CanvasGroup transitionCanvasGroup;

    [Tooltip("Schwarzer Hintergrund. Kann automatisch erstellt werden.")]
    [SerializeField] private Image blackBackgroundImage;

    [Tooltip("Optionales Bild über dem schwarzen Hintergrund.")]
    [SerializeField] private Image optionalImageDisplay;

    [Tooltip("TextMeshPro-Text für die Weisheit.")]
    [SerializeField] private TMP_Text wisdomText;

    [Header("Schwarzblende")]
    [Tooltip("Dauer des Übergangs von der Spielwelt zu Schwarz.")]
    [SerializeField] private float fadeToBlackDuration = 0.5f;

    [Tooltip("Kurze Pause, nachdem der Bildschirm vollständig schwarz ist.")]
    [SerializeField] private float blackScreenDelay = 0.4f;

    [SerializeField] private Color backgroundColor = Color.black;

    [Header("Optionales Fade-Bild")]
    [Tooltip(
        "Optionales Bild, das nach der Schwarzblende eingeblendet wird. " +
        "Leer lassen, wenn nur Schwarz gezeigt werden soll."
    )]
    [SerializeField] private Sprite fadeImageSprite;

    [SerializeField] private Color fadeImageTint = Color.white;

    [Tooltip("Behält das Seitenverhältnis des Bildes bei.")]
    [SerializeField] private bool preserveImageAspect = true;

    [Tooltip("Dauer des Einblendens des optionalen Bildes.")]
    [SerializeField] private float imageFadeInDuration = 0.6f;

    [Header("Weisheitstext")]
    [TextArea(2, 6)]
    [SerializeField] private string wisdomMessage =
        "In harmony, nothing needs to be forced.\n" +
        "Sometimes, the path appears when you slow down.";

    [SerializeField] private float wisdomFontSize = 54f;
    [SerializeField] private Color wisdomTextColor = Color.white;

    [Tooltip("Dauer des Einblendens des Textes.")]
    [SerializeField] private float textFadeInDuration = 0.8f;

    [Tooltip("Wie lange der Text vollständig sichtbar bleibt.")]
    [SerializeField] private float textVisibleDuration = 4f;

    [Tooltip("Dauer des Ausblendens des Textes.")]
    [SerializeField] private float textFadeOutDuration = 0.8f;

    [Header("Optionales Audio")]
    [Tooltip(
        "Kann leer bleiben. Bei Bedarf wird automatisch eine AudioSource erstellt."
    )]
    [SerializeField] private AudioSource transitionAudioSource;

    [SerializeField] private bool createAudioSourceAutomatically = true;

    [Tooltip(
        "Optionales Musikstück, Ambiente oder Voice-over für den Übergang."
    )]
    [SerializeField] private AudioClip transitionAudioClip;

    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 0.8f;

    [SerializeField] private bool loopAudio = false;

    [Tooltip(
        "Blendet das Audio vor dem Szenenwechsel aus. " +
        "Bei einem kurzen Effekt kann dies deaktiviert bleiben."
    )]
    [SerializeField] private bool fadeAudioBeforeSceneChange = false;

    [SerializeField] private float audioFadeOutDuration = 0.5f;

    [Header("Szenenwechsel")]
    [Tooltip(
        "Exakter Name der nächsten Szene ohne .unity. " +
        "Leer lassen, um am Ende auf dem schwarzen Bildschirm zu bleiben."
    )]
    [SerializeField] private string nextSceneName;

    [Tooltip("Kurze Pause nach dem Text vor dem Szenenwechsel.")]
    [SerializeField] private float delayBeforeSceneLoad = 0.3f;

    private bool hasTriggered;

    private Collider triggerCollider;
    private CanvasGroup optionalImageCanvasGroup;
    private CanvasGroup wisdomTextCanvasGroup;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogWarning(
                "CaveWisdomTransitionTrigger: " +
                "Auf dem Trigger-Objekt fehlt ein Collider."
            );
        }
        else if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning(
                "CaveWisdomTransitionTrigger: " +
                "Der Collider sollte Is Trigger aktiviert haben."
            );
        }

        if (
            createUIAutomatically
            && transitionCanvasGroup == null
        )
        {
            CreateTransitionCanvas();
        }

        PrepareUI();

        SetTransitionAlpha(0f);
        SetImageAlpha(0f);
        SetTextAlpha(0f);

        PrepareAudioSource();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsXRPlayer(other))
            return;

        BeginTransition();
    }

    /// <summary>
    /// Kann auch von einem anderen Skript oder UnityEvent
    /// aufgerufen werden.
    /// </summary>
    public void BeginTransition()
    {
        if (triggerOnlyOnce && hasTriggered)
            return;

        hasTriggered = true;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        StartCoroutine(TransitionSequence());
    }

    private IEnumerator TransitionSequence()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        DisablePlayerMovement();
        PlayTransitionAudio();

        if (
            transitionCanvasGroup == null
            && createUIAutomatically
        )
        {
            CreateTransitionCanvas();
            PrepareUI();
        }

        if (transitionCanvasGroup == null)
        {
            Debug.LogWarning(
                "CaveWisdomTransitionTrigger: " +
                "Es konnte kein Übergangs-Canvas gefunden oder erstellt werden."
            );

            yield break;
        }

        // Schritt 1: Spielwelt langsam vollständig abdunkeln.
        yield return StartCoroutine(
            FadeCanvasGroup(
                transitionCanvasGroup,
                0f,
                1f,
                fadeToBlackDuration
            )
        );

        // Schritt 2: Optionales Bild über dem Schwarz einblenden.
        if (
            fadeImageSprite != null
            && optionalImageDisplay != null
            && optionalImageCanvasGroup != null
        )
        {
            optionalImageDisplay.sprite = fadeImageSprite;
            optionalImageDisplay.color = fadeImageTint;
            optionalImageDisplay.preserveAspect = preserveImageAspect;
            optionalImageDisplay.enabled = true;

            yield return StartCoroutine(
                FadeCanvasGroup(
                    optionalImageCanvasGroup,
                    0f,
                    1f,
                    imageFadeInDuration
                )
            );
        }

        if (blackScreenDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                blackScreenDelay
            );
        }

        // Schritt 3: Weisheitstext anzeigen.
        if (
            wisdomText != null
            && wisdomTextCanvasGroup != null
        )
        {
            wisdomText.text = wisdomMessage;
            wisdomText.fontSize = wisdomFontSize;
            wisdomText.color = wisdomTextColor;

            yield return StartCoroutine(
                FadeCanvasGroup(
                    wisdomTextCanvasGroup,
                    0f,
                    1f,
                    textFadeInDuration
                )
            );

            if (textVisibleDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    textVisibleDuration
                );
            }

            yield return StartCoroutine(
                FadeCanvasGroup(
                    wisdomTextCanvasGroup,
                    1f,
                    0f,
                    textFadeOutDuration
                )
            );
        }
        else
        {
            Debug.LogWarning(
                "CaveWisdomTransitionTrigger: " +
                "Der Weisheitstext konnte nicht gefunden werden."
            );
        }

        if (delayBeforeSceneLoad > 0f)
        {
            yield return new WaitForSecondsRealtime(
                delayBeforeSceneLoad
            );
        }

        if (
            fadeAudioBeforeSceneChange
            && transitionAudioSource != null
            && transitionAudioSource.isPlaying
        )
        {
            yield return StartCoroutine(FadeOutAudio());
        }

        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private IEnumerator FadeCanvasGroup(
        CanvasGroup canvasGroup,
        float startAlpha,
        float targetAlpha,
        float duration
    )
    {
        if (canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float timer = 0f;
        canvasGroup.alpha = startAlpha;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / duration
            );

            canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                progress
            );

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeOutAudio()
    {
        if (transitionAudioSource == null)
            yield break;

        if (audioFadeOutDuration <= 0f)
        {
            transitionAudioSource.Stop();
            yield break;
        }

        float startVolume = transitionAudioSource.volume;
        float timer = 0f;

        while (timer < audioFadeOutDuration)
        {
            timer += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                timer / audioFadeOutDuration
            );

            transitionAudioSource.volume = Mathf.Lerp(
                startVolume,
                0f,
                progress
            );

            yield return null;
        }

        transitionAudioSource.Stop();
        transitionAudioSource.volume = audioVolume;
    }

    private void PlayTransitionAudio()
    {
        if (transitionAudioClip == null)
            return;

        PrepareAudioSource();

        if (transitionAudioSource == null)
            return;

        transitionAudioSource.clip = transitionAudioClip;
        transitionAudioSource.volume = audioVolume;
        transitionAudioSource.loop = loopAudio;
        transitionAudioSource.Play();
    }

    private void PrepareAudioSource()
    {
        if (transitionAudioSource != null)
        {
            ConfigureAudioSource(transitionAudioSource);
            return;
        }

        if (!createAudioSourceAutomatically)
            return;

        GameObject audioObject =
            playerCamera != null
                ? playerCamera.gameObject
                : gameObject;

        transitionAudioSource =
            audioObject.AddComponent<AudioSource>();

        ConfigureAudioSource(transitionAudioSource);
    }

    private void ConfigureAudioSource(
        AudioSource audioSource
    )
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = loopAudio;
        audioSource.volume = audioVolume;

        // Nicht räumlicher Sound:
        // bleibt im gesamten Headset gleich hörbar.
        audioSource.spatialBlend = 0f;
    }

    private void CreateTransitionCanvas()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning(
                "CaveWisdomTransitionTrigger: " +
                "Ohne Player Camera kann kein VR-Canvas erstellt werden."
            );

            return;
        }

        GameObject canvasObject =
            new GameObject("Cave_Transition_Canvas");

        canvasObject.transform.SetParent(
            playerCamera,
            false
        );

        canvasObject.transform.localPosition =
            new Vector3(
                0f,
                0f,
                canvasDistanceFromCamera
            );

        canvasObject.transform.localRotation =
            Quaternion.identity;

        Canvas canvas =
            canvasObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 32767;
        canvas.overrideSorting = true;

        Camera cameraComponent =
            playerCamera.GetComponent<Camera>();

        if (cameraComponent != null)
        {
            canvas.worldCamera = cameraComponent;
        }

        RectTransform canvasRect =
            canvas.GetComponent<RectTransform>();

        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale =
            Vector3.one * canvasScale;

        transitionCanvasGroup =
            canvasObject.AddComponent<CanvasGroup>();

        transitionCanvasGroup.alpha = 0f;
        transitionCanvasGroup.blocksRaycasts = false;
        transitionCanvasGroup.interactable = false;

        CreateBlackBackground(canvasObject.transform);
        CreateOptionalImage(canvasObject.transform);
        CreateWisdomText(canvasObject.transform);
    }

    private void CreateBlackBackground(
        Transform canvasTransform
    )
    {
        GameObject backgroundObject =
            new GameObject("Black_Background");

        backgroundObject.transform.SetParent(
            canvasTransform,
            false
        );

        blackBackgroundImage =
            backgroundObject.AddComponent<Image>();

        blackBackgroundImage.color = backgroundColor;
        blackBackgroundImage.raycastTarget = false;

        StretchToCanvas(
            blackBackgroundImage.rectTransform
        );
    }

    private void CreateOptionalImage(
        Transform canvasTransform
    )
    {
        GameObject imageObject =
            new GameObject("Optional_Fade_Image");

        imageObject.transform.SetParent(
            canvasTransform,
            false
        );

        optionalImageDisplay =
            imageObject.AddComponent<Image>();

        optionalImageDisplay.sprite = fadeImageSprite;
        optionalImageDisplay.color = fadeImageTint;
        optionalImageDisplay.preserveAspect =
            preserveImageAspect;

        optionalImageDisplay.raycastTarget = false;

        StretchToCanvas(
            optionalImageDisplay.rectTransform
        );

        optionalImageCanvasGroup =
            imageObject.AddComponent<CanvasGroup>();

        optionalImageCanvasGroup.alpha = 0f;
        optionalImageCanvasGroup.blocksRaycasts = false;
        optionalImageCanvasGroup.interactable = false;

        optionalImageDisplay.enabled =
            fadeImageSprite != null;
    }

    private void CreateWisdomText(
        Transform canvasTransform
    )
    {
        GameObject textObject =
            new GameObject("Wisdom_Text");

        textObject.transform.SetParent(
            canvasTransform,
            false
        );

        TextMeshProUGUI textComponent =
            textObject.AddComponent<TextMeshProUGUI>();

        textComponent.text = wisdomMessage;
        textComponent.fontSize = wisdomFontSize;
        textComponent.color = wisdomTextColor;
        textComponent.alignment =
            TextAlignmentOptions.Center;

        textComponent.enableWordWrapping = true;
        textComponent.raycastTarget = false;

        RectTransform textRect =
            textComponent.rectTransform;

        textRect.anchorMin =
            new Vector2(0.12f, 0.25f);

        textRect.anchorMax =
            new Vector2(0.88f, 0.75f);

        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        wisdomText = textComponent;

        wisdomTextCanvasGroup =
            textObject.AddComponent<CanvasGroup>();

        wisdomTextCanvasGroup.alpha = 0f;
        wisdomTextCanvasGroup.blocksRaycasts = false;
        wisdomTextCanvasGroup.interactable = false;
    }

    private void PrepareUI()
    {
        if (blackBackgroundImage != null)
        {
            blackBackgroundImage.color =
                backgroundColor;

            blackBackgroundImage.raycastTarget = false;
        }

        if (optionalImageDisplay != null)
        {
            optionalImageDisplay.sprite =
                fadeImageSprite;

            optionalImageDisplay.color =
                fadeImageTint;

            optionalImageDisplay.preserveAspect =
                preserveImageAspect;

            optionalImageDisplay.raycastTarget = false;

            optionalImageDisplay.enabled =
                fadeImageSprite != null;

            optionalImageCanvasGroup =
                GetOrCreateCanvasGroup(
                    optionalImageDisplay.gameObject
                );
        }

        if (wisdomText != null)
        {
            wisdomText.text = wisdomMessage;
            wisdomText.fontSize = wisdomFontSize;
            wisdomText.color = wisdomTextColor;
            wisdomText.alignment =
                TextAlignmentOptions.Center;

            wisdomTextCanvasGroup =
                GetOrCreateCanvasGroup(
                    wisdomText.gameObject
                );
        }
    }

    private CanvasGroup GetOrCreateCanvasGroup(
        GameObject targetObject
    )
    {
        if (targetObject == null)
            return null;

        CanvasGroup canvasGroup =
            targetObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                targetObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        return canvasGroup;
    }

    private void StretchToCanvas(
        RectTransform rectTransform
    )
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void SetTransitionAlpha(float alpha)
    {
        if (transitionCanvasGroup != null)
        {
            transitionCanvasGroup.alpha = alpha;
        }
    }

    private void SetImageAlpha(float alpha)
    {
        if (optionalImageCanvasGroup != null)
        {
            optionalImageCanvasGroup.alpha = alpha;
        }
    }

    private void SetTextAlpha(float alpha)
    {
        if (wisdomTextCanvasGroup != null)
        {
            wisdomTextCanvasGroup.alpha = alpha;
        }
    }

    private void DisablePlayerMovement()
    {
        if (movementScriptsToDisable == null)
            return;

        foreach (
            Behaviour movementScript
            in movementScriptsToDisable
        )
        {
            if (movementScript != null)
            {
                movementScript.enabled = false;
            }
        }
    }

    private bool IsXRPlayer(Collider other)
    {
        if (other == null)
            return false;

        if (xrOrigin != null)
        {
            if (other.transform == xrOrigin)
                return true;

            if (other.transform.IsChildOf(xrOrigin))
                return true;

            if (other.attachedRigidbody != null)
            {
                Transform rigidbodyTransform =
                    other.attachedRigidbody.transform;

                if (rigidbodyTransform == xrOrigin)
                    return true;

                if (rigidbodyTransform.IsChildOf(xrOrigin))
                    return true;
            }
        }

        if (
            !string.IsNullOrWhiteSpace(playerTag)
            && other.CompareTag(playerTag)
        )
        {
            return true;
        }

        return false;
    }
}