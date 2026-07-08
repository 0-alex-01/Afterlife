using UnityEngine;
using TMPro;

/// <summary>
/// Zeigt nach dem Trösten einer Trauernden ein Zitat einer historischen
/// Persönlichkeit als schwebendes Memorial über dem Grab an. Blendet sanft
/// ein (nach dem Zerfalls-Effekt) und bleibt als Andenken stehen.
/// Koppelt sich ohne Eingriff per Polling an CemeteryFigure.IsComforted.
/// </summary>
[RequireComponent(typeof(CemeteryFigure))]
public class MemorialQuote : MonoBehaviour
{
    [Header("Zitat")]
    [TextArea]
    public string quote;
    public string author;

    [Header("Darstellung")]
    [Tooltip("Bezugspunkt (der Grabstein). Leer = Figur selbst.")]
    public Transform anchor;
    [Tooltip("Abstand vom Grabstein Richtung Figur/Pfad in Metern.")]
    public float pathDistance = 1.25f;
    [Tooltip("Höhe des Textzentrums über dem Anker in Metern (Lesehöhe am Pfad).")]
    public float labelHeight = 0.75f;
    [Tooltip("Wartezeit nach dem Trösten, bis der Text erscheint (Zerfall zuerst).")]
    public float appearDelay = 2.2f;
    [Tooltip("Dauer des Einblendens.")]
    public float fadeDuration = 3f;
    public Color textColor = new Color(1f, 0.87f, 0.62f);
    [Tooltip("Eigener TMP-Font. Leer = Righteous aus den Projekt-Resources (wie die Grabinschriften).")]
    public TMPro.TMP_FontAsset fontOverride;

    private static TMPro.TMP_FontAsset _sharedFont;

    private static TMPro.TMP_FontAsset GetSharedFont()
    {
        if (_sharedFont == null)
        {
            var ttf = Resources.Load<Font>("Fonts/CinzelDecorative-Regular");
            if (ttf == null) ttf = Resources.Load<Font>("Righteous-Regular");
            if (ttf != null) _sharedFont = TMPro.TMP_FontAsset.CreateFontAsset(ttf);
        }
        return _sharedFont;
    }

    private CemeteryFigure _figure;
    private TextMeshPro _label;
    private float _timer;
    private bool _triggered;

    private void Start()
    {
        _figure = GetComponent<CemeteryFigure>();
    }

    private void Update()
    {
        if (!_triggered)
        {
            if (_figure != null && _figure.IsComforted) _triggered = true;
            return;
        }

        _timer += Time.deltaTime;
        if (_timer < appearDelay) return;

        if (_label == null) CreateLabel();

        float a = Mathf.Clamp01((_timer - appearDelay) / fadeDuration);
        var c = textColor;
        c.a = Mathf.SmoothStep(0f, 1f, a);
        _label.color = c;
    }

    private void LateUpdate()
    {
        if (_label == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // Billboard: der Text dreht sich immer zum Spieler.
        // TMP ist von seiner -Z-Seite lesbar -> +Z von der Kamera WEG drehen,
        // damit die Schrift nicht spiegelverkehrt erscheint.
        var dir = _label.transform.position - cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            _label.transform.rotation = Quaternion.LookRotation(dir);
    }

    private void CreateLabel()
    {
        var go = new GameObject("MemorialQuote_Label");
        var basePos = anchor != null ? anchor.position : transform.position;

        // Am Pfad vor dem Grab (Richtung der Figur, wo auch der Spieler steht),
        // auf angenehmer Lesehöhe — nicht hoch über dem Stein.
        var dir = transform.position - basePos;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f
            ? dir.normalized
            : (anchor != null ? anchor.forward : transform.forward);

        go.transform.position = basePos + dir * pathDistance + Vector3.up * labelHeight;

        _label = go.AddComponent<TextMeshPro>();
        _label.rectTransform.sizeDelta = new Vector2(1.5f, 0.95f);
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 0.35f;
        _label.fontSizeMax = 0.68f;
        _label.enableWordWrapping = true;

        var font = fontOverride != null ? fontOverride : GetSharedFont();
        if (font != null) _label.font = font;
        _label.outlineWidth = 0.16f;
        _label.outlineColor = new Color(0.08f, 0.06f, 0.02f, 1f);
        _label.text = $"„{quote}“\n<size=70%>— {author}</size>";

        var c = textColor;
        c.a = 0f;
        _label.color = c;
    }
}
