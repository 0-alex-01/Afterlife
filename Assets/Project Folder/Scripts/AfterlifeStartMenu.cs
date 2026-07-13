using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AfterlifeStartMenu : MonoBehaviour
{
    private static readonly Color Background = new Color32(7, 7, 15, 255);
    private static readonly Color Raised = new Color32(14, 15, 30, 245);
    private static readonly Color Ink = new Color32(211, 207, 226, 255);
    private static readonly Color InkDim = new Color32(141, 137, 166, 255);
    private static readonly Color Line = new Color32(35, 35, 58, 255);
    private static readonly Color Soul = new Color32(127, 224, 212, 255);
    private static readonly Color Ekg = new Color32(77, 240, 140, 255);

    private const string VolumeKey = "Afterlife.MasterVolume";
    private const string QualityKey = "Afterlife.QualityLevel";

    private CanvasGroup rootGroup;
    private GameObject startPanel;
    private GameObject settingsPanel;
    private TextMeshProUGUI title;
    private TextMeshProUGUI volumeValue;
    private TextMeshProUGUI qualityValue;
    private Button startButton;
    private Button settingsButton;
    private Button backButton;
    private AfterlifeMenuEkgGraphic ekgGlow;
    private AfterlifeMenuEkgGraphic ekgLine;
    private TMP_FontAsset displayFont;
    private float introTime;
    private bool leaving;

    public bool Completed { get; private set; }

    public static IEnumerator WaitForStart()
    {
        GameObject menuObject = new GameObject("Afterlife Start Menu", typeof(RectTransform));
        AfterlifeStartMenu menu = menuObject.AddComponent<AfterlifeStartMenu>();

        while (menu != null && !menu.Completed)
            yield return null;
    }

    private void Awake()
    {
        displayFont = Resources.Load<TMP_FontAsset>("Fonts/CinzelDecorativeSDF");
        if (displayFont == null)
            displayFont = TMP_Settings.defaultFontAsset;

        BuildEventSystem();
        BuildMenu();
        ApplySavedSettings();
    }

    private IEnumerator Start()
    {
        yield return null;
        startButton.Select();
    }

    private void Update()
    {
        introTime += Time.unscaledDeltaTime;

        float trace = Mathf.Clamp01((introTime - 0.2f) / 2.8f);
        ekgGlow.Progress = trace;
        ekgLine.Progress = trace;

        float titleReveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((introTime - 0.75f) / 1.8f));
        title.alpha = titleReveal;
        title.characterSpacing = Mathf.Lerp(34f, 12f, titleReveal);
    }

    private void BuildMenu()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        rootGroup = gameObject.AddComponent<CanvasGroup>();
        // Der schwarze Hintergrund ist ab dem ersten Frame deckend, damit die EKG-Sequenz
        // niemals vor dem eigentlichen Startmenue durchscheint.
        rootGroup.alpha = 1f;

        RectTransform rootRect = GetComponent<RectTransform>();
        Stretch(rootRect);

        Image backdrop = CreateImage("Background", transform, Background);
        Stretch(backdrop.rectTransform);

        AfterlifeMenuMotes motes = CreateUiObject("Soul Motes", transform).AddComponent<AfterlifeMenuMotes>();
        Stretch(motes.rectTransform);
        motes.raycastTarget = false;

        startPanel = CreateUiObject("Start", transform);
        Stretch(startPanel.GetComponent<RectTransform>());

        CreateEkg(startPanel.transform);

        title = CreateText("Title", startPanel.transform, "AFTERLIFE", 112f, Ink, TextAlignmentOptions.Center, displayFont);
        Place(title.rectTransform, new Vector2(0f, 70f), new Vector2(1600f, 150f));
        title.alpha = 0f;

        TextMeshProUGUI subtitle = CreateText(
            "Subtitle",
            startPanel.transform,
            "EINE VR-REISE ÜBER DAS LOSLASSEN",
            23f,
            InkDim,
            TextAlignmentOptions.Center,
            TMP_Settings.defaultFontAsset);
        subtitle.characterSpacing = 18f;
        Place(subtitle.rectTransform, new Vector2(0f, -48f), new Vector2(1300f, 60f));

        startButton = CreateButton("Start Button", startPanel.transform, "SPIEL STARTEN", new Vector2(0f, -205f));
        startButton.onClick.AddListener(BeginGame);

        settingsButton = CreateButton("Settings Button", startPanel.transform, "EINSTELLUNGEN", new Vector2(0f, -288f));
        settingsButton.onClick.AddListener(OpenSettings);

        TextMeshProUGUI hint = CreateText(
            "Input Hint",
            startPanel.transform,
            "ENTER  /  CONTROLLER-TRIGGER",
            14f,
            new Color(InkDim.r, InkDim.g, InkDim.b, 0.62f),
            TextAlignmentOptions.Center,
            TMP_Settings.defaultFontAsset);
        hint.characterSpacing = 12f;
        Place(hint.rectTransform, new Vector2(0f, -415f), new Vector2(900f, 40f));

        BuildSettingsPanel();
    }

    private void CreateEkg(Transform parent)
    {
        ekgGlow = CreateUiObject("EKG Glow", parent).AddComponent<AfterlifeMenuEkgGraphic>();
        Place(ekgGlow.rectTransform, new Vector2(0f, 270f), new Vector2(1500f, 170f));
        ekgGlow.color = new Color(Ekg.r, Ekg.g, Ekg.b, 0.16f);
        ekgGlow.Thickness = 13f;
        ekgGlow.raycastTarget = false;

        ekgLine = CreateUiObject("EKG Line", parent).AddComponent<AfterlifeMenuEkgGraphic>();
        Place(ekgLine.rectTransform, new Vector2(0f, 270f), new Vector2(1500f, 170f));
        ekgLine.color = Ekg;
        ekgLine.Thickness = 3f;
        ekgLine.raycastTarget = false;
    }

    private void BuildSettingsPanel()
    {
        settingsPanel = CreateUiObject("Settings", transform);
        Stretch(settingsPanel.GetComponent<RectTransform>());

        Image shade = CreateImage("Settings Background", settingsPanel.transform, new Color(Background.r, Background.g, Background.b, 0.985f));
        Stretch(shade.rectTransform);

        TextMeshProUGUI heading = CreateText("Heading", settingsPanel.transform, "EINSTELLUNGEN", 64f, Ink, TextAlignmentOptions.Center, displayFont);
        heading.characterSpacing = 12f;
        Place(heading.rectTransform, new Vector2(0f, 275f), new Vector2(1200f, 110f));

        TextMeshProUGUI volumeLabel = CreateText("Volume Label", settingsPanel.transform, "GESAMTLAUTSTÄRKE", 20f, InkDim, TextAlignmentOptions.Left, TMP_Settings.defaultFontAsset);
        volumeLabel.characterSpacing = 8f;
        Place(volumeLabel.rectTransform, new Vector2(-250f, 105f), new Vector2(430f, 48f));

        Slider volume = CreateSlider(settingsPanel.transform, new Vector2(55f, 105f));
        volume.minValue = 0f;
        volume.maxValue = 1f;
        volume.value = PlayerPrefs.GetFloat(VolumeKey, 1f);
        volume.onValueChanged.AddListener(SetVolume);

        volumeValue = CreateText("Volume Value", settingsPanel.transform, "100 %", 20f, Soul, TextAlignmentOptions.Right, TMP_Settings.defaultFontAsset);
        Place(volumeValue.rectTransform, new Vector2(385f, 105f), new Vector2(150f, 48f));

        TextMeshProUGUI qualityLabel = CreateText("Quality Label", settingsPanel.transform, "GRAFIKQUALITÄT", 20f, InkDim, TextAlignmentOptions.Left, TMP_Settings.defaultFontAsset);
        qualityLabel.characterSpacing = 8f;
        Place(qualityLabel.rectTransform, new Vector2(-250f, 5f), new Vector2(430f, 48f));

        Button qualityButton = CreateButton("Quality Button", settingsPanel.transform, string.Empty, new Vector2(165f, 5f), new Vector2(440f, 62f));
        qualityValue = qualityButton.GetComponentInChildren<TextMeshProUGUI>();
        qualityButton.onClick.AddListener(CycleQuality);

        TextMeshProUGUI note = CreateText(
            "VR Note",
            settingsPanel.transform,
            "Für VR empfiehlt sich bei Rucklern zunächst eine niedrigere Grafikqualität.",
            17f,
            InkDim,
            TextAlignmentOptions.Center,
            TMP_Settings.defaultFontAsset);
        Place(note.rectTransform, new Vector2(0f, -105f), new Vector2(1050f, 50f));

        backButton = CreateButton("Back Button", settingsPanel.transform, "ZURÜCK", new Vector2(0f, -245f));
        backButton.onClick.AddListener(CloseSettings);

        settingsPanel.SetActive(false);
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2? size = null)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = Raised;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.72f, 1f, 0.94f, 1f);
        colors.pressedColor = new Color(0.48f, 0.86f, 0.79f, 1f);
        colors.selectedColor = new Color(0.72f, 1f, 0.94f, 1f);
        colors.disabledColor = new Color(0.35f, 0.35f, 0.42f, 0.65f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = Line;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Place(buttonObject.GetComponent<RectTransform>(), position, size ?? new Vector2(390f, 64f));

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 19f, Ink, TextAlignmentOptions.Center, TMP_Settings.defaultFontAsset);
        text.characterSpacing = 8f;
        Stretch(text.rectTransform, 14f);

        return button;
    }

    private Slider CreateSlider(Transform parent, Vector2 position)
    {
        GameObject sliderObject = CreateUiObject("Volume Slider", parent);
        Place(sliderObject.GetComponent<RectTransform>(), position, new Vector2(430f, 44f));

        Slider slider = sliderObject.AddComponent<Slider>();

        Image background = CreateImage("Background", sliderObject.transform, Line);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.offsetMin = new Vector2(0f, -3f);
        backgroundRect.offsetMax = new Vector2(0f, 3f);

        GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), 6f);
        Image fill = CreateImage("Fill", fillArea.transform, Soul);
        Stretch(fill.rectTransform);

        GameObject handleArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea.GetComponent<RectTransform>(), 10f);
        Image handle = CreateImage("Handle", handleArea.transform, Ink);
        handle.rectTransform.sizeDelta = new Vector2(22f, 22f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private void ApplySavedSettings()
    {
        float volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        AudioListener.volume = volume;
        UpdateVolumeLabel(volume);

        int maxQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
        int quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, maxQuality);
        if (QualitySettings.names.Length > 0)
            QualitySettings.SetQualityLevel(quality, true);
        UpdateQualityLabel();
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        UpdateVolumeLabel(value);
    }

    private void UpdateVolumeLabel(float value)
    {
        if (volumeValue != null)
            volumeValue.text = Mathf.RoundToInt(value * 100f) + " %";
    }

    private void CycleQuality()
    {
        if (QualitySettings.names.Length == 0)
            return;

        int quality = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
        QualitySettings.SetQualityLevel(quality, true);
        PlayerPrefs.SetInt(QualityKey, quality);
        UpdateQualityLabel();
    }

    private void UpdateQualityLabel()
    {
        if (qualityValue == null)
            return;

        string[] names = QualitySettings.names;
        qualityValue.text = names.Length == 0
            ? "STANDARD"
            : names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1)].ToUpperInvariant();
    }

    private void OpenSettings()
    {
        startPanel.SetActive(false);
        settingsPanel.SetActive(true);
        backButton.Select();
    }

    private void CloseSettings()
    {
        settingsPanel.SetActive(false);
        startPanel.SetActive(true);
        settingsButton.Select();
    }

    private void BeginGame()
    {
        if (!leaving)
            StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        leaving = true;
        startButton.interactable = false;
        settingsButton.interactable = false;
        PlayerPrefs.Save();

        float from = rootGroup.alpha;
        float time = 0f;
        const float duration = 0.85f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, time / duration));
            yield return null;
        }

        Completed = true;
        Destroy(gameObject);
    }

    private static void BuildEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
            return;

        GameObject eventObject = new GameObject("EventSystem");
        eventSystem = eventObject.AddComponent<EventSystem>();

        Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null)
        {
            Component module = eventObject.AddComponent(inputSystemModule);
            inputSystemModule.GetMethod("AssignDefaultActions")?.Invoke(module, null);
        }
        else
        {
            eventObject.AddComponent<StandaloneInputModule>();
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateUiObject(name, parent).AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        TextMeshProUGUI text = CreateUiObject(name, parent).AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}

public class AfterlifeMenuEkgGraphic : MaskableGraphic
{
    private static readonly Vector2[] Shape =
    {
        new Vector2(0f, .5f), new Vector2(.13f, .5f),
        new Vector2(.142f, .46f), new Vector2(.151f, .56f), new Vector2(.158f, .19f),
        new Vector2(.168f, .88f), new Vector2(.177f, .26f), new Vector2(.184f, .5f),
        new Vector2(.33f, .5f), new Vector2(.342f, .46f), new Vector2(.351f, .56f),
        new Vector2(.358f, .19f), new Vector2(.368f, .88f), new Vector2(.377f, .26f),
        new Vector2(.384f, .5f), new Vector2(.53f, .5f), new Vector2(.539f, .47f),
        new Vector2(.546f, .54f), new Vector2(.551f, .33f), new Vector2(.559f, .72f),
        new Vector2(.566f, .36f), new Vector2(.572f, .5f), new Vector2(1f, .5f)
    };

    [NonSerialized] public float Thickness = 3f;

    private float progress;
    public float Progress
    {
        get => progress;
        set
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, next))
                return;
            progress = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (progress <= 0f)
            return;

        Rect rect = rectTransform.rect;
        float total = (Shape.Length - 1) * progress;
        int fullSegments = Mathf.FloorToInt(total);
        float partial = total - fullSegments;

        int lastFull = Mathf.Min(fullSegments, Shape.Length - 1);
        for (int i = 0; i < lastFull; i++)
            AddSegment(vh, ToRect(Shape[i], rect), ToRect(Shape[i + 1], rect));

        if (lastFull < Shape.Length - 1 && partial > 0f)
        {
            Vector2 from = Shape[lastFull];
            Vector2 to = Vector2.Lerp(from, Shape[lastFull + 1], partial);
            AddSegment(vh, ToRect(from, rect), ToRect(to, rect));
        }
    }

    private static Vector2 ToRect(Vector2 point, Rect rect)
    {
        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, point.x),
            Mathf.Lerp(rect.yMin, rect.yMax, point.y));
    }

    private void AddSegment(VertexHelper vh, Vector2 start, Vector2 end)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * Thickness * 0.5f;
        int index = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - normal; vh.AddVert(vertex);
        vertex.position = start + normal; vh.AddVert(vertex);
        vertex.position = end + normal; vh.AddVert(vertex);
        vertex.position = end - normal; vh.AddVert(vertex);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}

public class AfterlifeMenuMotes : MaskableGraphic
{
    private const int Count = 54;
    private readonly Vector2[] positions = new Vector2[Count];
    private readonly float[] sizes = new float[Count];
    private readonly float[] speeds = new float[Count];

    protected override void Awake()
    {
        base.Awake();
        UnityEngine.Random.State previous = UnityEngine.Random.state;
        UnityEngine.Random.InitState(271828);
        for (int i = 0; i < Count; i++)
        {
            positions[i] = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            sizes[i] = UnityEngine.Random.Range(1.2f, 3.8f);
            speeds[i] = UnityEngine.Random.Range(0.006f, 0.02f);
        }
        UnityEngine.Random.state = previous;
        color = new Color(0.5f, 0.88f, 0.83f, 0.32f);
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        for (int i = 0; i < Count; i++)
        {
            positions[i].y += speeds[i] * delta;
            positions[i].x += Mathf.Sin((Time.unscaledTime + i) * 0.22f) * delta * 0.0008f;
            if (positions[i].y > 1.03f)
                positions[i].y = -0.03f;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        for (int i = 0; i < Count; i++)
        {
            Vector2 center = new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, positions[i].x),
                Mathf.Lerp(rect.yMin, rect.yMax, positions[i].y));
            float half = sizes[i] * 0.5f;
            Color32 moteColor = color;
            moteColor.a = (byte)Mathf.RoundToInt(color.a * 255f * Mathf.Lerp(0.35f, 1f, Mathf.Repeat(i * 0.37f, 1f)));

            int index = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = moteColor;
            vertex.position = center + new Vector2(0f, half); vh.AddVert(vertex);
            vertex.position = center + new Vector2(half, 0f); vh.AddVert(vertex);
            vertex.position = center + new Vector2(0f, -half); vh.AddVert(vertex);
            vertex.position = center + new Vector2(-half, 0f); vh.AddVert(vertex);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
