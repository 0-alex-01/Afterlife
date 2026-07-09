using UnityEngine;
using TMPro;

/// <summary>
/// Baut im Editor für jede Trauernde ein Zitat-Label als ECHTES Szenen-Objekt
/// auf dem Lichterpfad (unter dem Root "MemorialQuotes") — in der Scene View
/// sichtbar und frei verschiebbar; ein vorhandenes Label wird nie angefasst.
/// Neu positionieren geht über das Kontextmenü des Components.
/// Im Spiel bleibt das Label unsichtbar, bis die Figur getröstet wurde; dann
/// wird ein zufälliges Zitat aus Resources/QuoteSource.txt eingesetzt und
/// sanft eingeblendet (Billboard zum Spieler).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CemeteryFigure))]
public class MemorialQuote : MonoBehaviour
{
    private const string RootName = "MemorialQuotes";

    [Header("Zitat")]
    [Tooltip("Zufälliges Zitat aus Resources/QuoteSource.txt (Format: Zitat|Autor), ohne Wiederholung pro Sitzung. Aus = festes Zitat unten.")]
    public bool useRandomQuote = true;
    [TextArea]
    [Tooltip("Festes Zitat — Vorschau in der Scene View; im Spiel nur genutzt, wenn useRandomQuote aus ist oder die Datenbank fehlt.")]
    public string quote;
    public string author;

    [Header("Lichterpfad")]
    [Tooltip("Wurzelobjekt des Laternenwegs, auf dem die Zitate erscheinen.")]
    public string pathRootName = "PathToFuneral";
    [Tooltip("Höhe des Textzentrums über dem Pfad (Lesehöhe in Metern) beim Aufbau.")]
    public float quoteHeight = 1.45f;
    [Tooltip("Bezugspunkt (der Grabstein) — nur Fallback, falls kein Lichterpfad gefunden wird.")]
    public Transform anchor;

    [Header("Einblendung")]
    [Tooltip("Wartezeit nach dem Trösten, bis der Text erscheint (Zerfall zuerst).")]
    public float appearDelay = 2.2f;
    [Tooltip("Dauer des Einblendens.")]
    public float fadeDuration = 3f;
    public Color textColor = new Color(1f, 0.87f, 0.62f);
    [Tooltip("Eigener TMP-Font. Leer = Cinzel Decorative aus den Projekt-Resources (wie die Grabinschriften).")]
    public TMP_FontAsset fontOverride;

    [Header("Auslösung (beim Näherkommen)")]
    [Tooltip("Das Zitat blendet sich ein, sobald der Spieler diesem Label näher als dieser Radius (Meter) kommt.")]
    public float revealDistance = 6f;
    [Tooltip("Damit die Zitate nicht schon am Start (direkt neben dem Portal) erscheinen: Sie werden erst 'scharfgeschaltet', nachdem der Spieler dem Hauptgrab näher als dieser Radius war.")]
    public float armGraveDistance = 10f;
    [Tooltip("Name des Hauptgrabs, das die Zitate scharfschaltet.")]
    public string centralGraveName = "CentralGrave";
    [Tooltip("Sanftes Aufsteigen des Textes während des Einblendens (Meter). 0 = aus.")]
    public float riseHeight = 0.15f;

    private static TMP_FontAsset _sharedFont;
    private static Material _sharedMaterial;

    // Zitat-Datenbank aus Resources/QuoteSource.txt ([i] = {Zitat, Autor});
    // _usedQuotes verhindert doppelte Zitate innerhalb einer Spielsitzung.
    private static string[][] _quoteEntries;
    private static readonly System.Collections.Generic.HashSet<int> _usedQuotes
        = new System.Collections.Generic.HashSet<int>();

    // Zitate werden erst freigeschaltet, nachdem der Spieler den Friedhof
    // (das Hauptgrab) erreicht hat — sonst würden sie schon am Start neben dem
    // Portal erscheinen. Danach blendet sich jedes Zitat beim Näherkommen ein.
    private static bool _armed;
    private static Transform _centralGrave;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _quoteEntries = null;
        _usedQuotes.Clear();
        _armed = false;
        _centralGrave = null;
    }

    private static TMP_FontAsset GetSharedFont()
    {
        // Persistenter Projekt-Font zuerst — nur ein echtes Asset übersteht
        // das Speichern der Szene (Labels sind Szenen-Objekte).
        if (_sharedFont == null)
            _sharedFont = Resources.Load<TMP_FontAsset>("Fonts/CinzelDecorativeSDF");
        if (_sharedFont == null)
        {
            var ttf = Resources.Load<Font>("Fonts/CinzelDecorative-Regular");
            if (ttf == null) ttf = Resources.Load<Font>("Righteous-Regular");
            if (ttf != null) _sharedFont = TMP_FontAsset.CreateFontAsset(ttf);
        }
        return _sharedFont;
    }

    private static Material GetSharedMaterial()
    {
        // Geteiltes Material-Asset mit dunkler Outline — die outline-Setter
        // würden pro Label eine Material-Instanz erzeugen, die beim Speichern
        // der Szene verloren ginge.
        if (_sharedMaterial == null)
            _sharedMaterial = Resources.Load<Material>("Fonts/CinzelQuote");
        return _sharedMaterial;
    }

    private static string[][] GetQuoteEntries()
    {
        if (_quoteEntries == null)
        {
            var list = new System.Collections.Generic.List<string[]>();
            var txt = Resources.Load<TextAsset>("QuoteSource");
            if (txt != null)
            {
                foreach (var raw in txt.text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;
                    list.Add(new[] { parts[0].Trim(), parts[1].Trim() });
                }
            }
            _quoteEntries = list.ToArray();
        }
        return _quoteEntries;
    }

    private CemeteryFigure _figure;
    private TextMeshPro _label;
    private Material _matInstance;
    private Color _fullOutline;
    private float _timer;
    private bool _triggered;
    private bool _prepared;
    private Vector3 _basePos;

    private void OnEnable()
    {
        // Editor: Label als Szenen-Objekt anlegen, falls es noch fehlt. Ein
        // vorhandenes Label wird NIE angefasst — manuelle Verschiebungen bleiben.
        if (!Application.isPlaying && FindLabel() == null)
            BuildLabel();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _figure = GetComponent<CemeteryFigure>();
        _label = FindLabel();
        if (_label == null) _label = BuildLabel(); // Fallback: Szene ohne gebautes Label
        if (_label == null) return;

        // Ausgangsposition für die Aufsteig-Animation merken.
        _basePos = _label.transform.position;

        // Hauptgrab auflösen — es „schaltet" die Zitate frei, sobald der Spieler
        // den Friedhof erreicht hat (siehe TryTrigger).
        if (_centralGrave == null)
        {
            var cg = GameObject.Find(centralGraveName);
            if (cg != null) _centralGrave = cg.transform;
        }

        // Verstecken bis zur Auslösung: Fläche über Vertex-Alpha, Outline über
        // eine Material-Instanz (nur im Play Mode erzeugt — Asset bleibt sauber).
        var c = _label.color;
        c.a = 0f;
        _label.color = c;
        _matInstance = _label.fontMaterial;
        if (_matInstance != null && _matInstance.HasProperty("_OutlineColor"))
        {
            _fullOutline = _matInstance.GetColor("_OutlineColor");
            var oc = _fullOutline;
            oc.a = 0f;
            _matInstance.SetColor("_OutlineColor", oc);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || _label == null) return;

        if (!_triggered)
        {
            if (TryTrigger()) _triggered = true;
            else return;
        }

        _timer += Time.deltaTime;
        if (_timer < appearDelay) return;

        if (!_prepared) PrepareRevealText();

        float a = Mathf.Clamp01((_timer - appearDelay) / fadeDuration);
        float alpha = a >= 1f ? 1f : Mathf.SmoothStep(0f, 1f, a);
        var c = textColor;
        c.a = alpha;
        _label.color = c;
        if (_matInstance != null && _matInstance.HasProperty("_OutlineColor"))
        {
            var oc = _fullOutline;
            oc.a *= alpha;
            _matInstance.SetColor("_OutlineColor", oc);
        }

        // Sanftes Aufsteigen: startet leicht unterhalb und schwebt beim
        // Einblenden auf die Ausgangshöhe hoch.
        if (riseHeight > 0f)
            _label.transform.position = _basePos + Vector3.up * (riseHeight * (alpha - 1f));
    }

    /// <summary>Zeigt das Zitat, sobald der Spieler ihm nahe kommt — aber erst,
    /// nachdem er den Friedhof (das Hauptgrab) erreicht hat. So erscheinen die
    /// Zitate auf dem Rückweg Richtung Portal nacheinander und nicht schon am
    /// Start direkt neben dem Portal.</summary>
    private bool TryTrigger()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        // 1) Scharfschalten: Spieler muss zuerst zum Hauptgrab vorgedrungen sein.
        if (!_armed && _centralGrave != null)
        {
            var dg = cam.transform.position - _centralGrave.position;
            dg.y = 0f;
            if (dg.sqrMagnitude <= armGraveDistance * armGraveDistance) _armed = true;
        }
        if (!_armed) return false;

        // 2) Auf dem Weg zum Portal: erscheinen, sobald der Spieler dieses Zitat erreicht.
        var d = cam.transform.position - _label.transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= revealDistance * revealDistance;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || _label == null || !_triggered) return;
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

    /// <summary>Im Spiel: zufälliges Zitat aus der Datenbank einsetzen
    /// (ohne Wiederholung), Fallback: das feste Zitat aus dem Inspector.</summary>
    private void PrepareRevealText()
    {
        _prepared = true;
        string q = quote, a = author;
        var entries = GetQuoteEntries();
        if (useRandomQuote && entries.Length > 0)
        {
            int idx = Random.Range(0, entries.Length);
            if (_usedQuotes.Count < entries.Length)
                while (_usedQuotes.Contains(idx)) idx = (idx + 1) % entries.Length;
            _usedQuotes.Add(idx);
            q = entries[idx][0];
            a = entries[idx][1];
        }
        _label.text = $"„{q}“\n<size=70%>— {a}</size>";
    }

    private TextMeshPro FindLabel()
    {
        var root = GameObject.Find(RootName);
        if (root == null) return null;
        var child = root.transform.Find("MemorialQuote_" + name);
        return child != null ? child.GetComponent<TextMeshPro>() : null;
    }

    [ContextMenu("Zitat-Label neu positionieren (auf Lichterpfad)")]
    private void RebuildLabel()
    {
        var old = FindLabel();
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        _label = BuildLabel();
    }

    private TextMeshPro BuildLabel()
    {
        // Pfad-Enden bestimmen: die beiden am weitesten auseinanderliegenden
        // Wegplatten; das portalnähere Ende ist das Ziel.
        Vector3 pathStart = Vector3.zero, pathEnd = Vector3.zero;
        bool hasPath = ResolvePath(ref pathStart, ref pathEnd);

        // Deterministische Station pro Figur (nach Namen sortiert), gleichmäßig
        // zwischen den Pfad-Enden verteilt.
        Vector3 pos;
        Quaternion rot;
        if (hasPath)
        {
            var all = FindObjectsByType<MemorialQuote>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var names = new System.Collections.Generic.List<string>();
            foreach (var mq in all) names.Add(mq.name);
            names.Sort(System.StringComparer.Ordinal);
            int slots = Mathf.Max(1, names.Count);
            int station = Mathf.Max(0, names.IndexOf(name));
            float t = (station + 1f) / (slots + 1f);
            pos = Vector3.Lerp(pathStart, pathEnd, t) + Vector3.up * quoteHeight;

            // Lesbar für Spieler, die vom Pfadanfang Richtung Portal schauen
            // (TMP liest sich von der -Z-Seite; im Spiel übernimmt das Billboard).
            var fwd = pathEnd - pathStart;
            fwd.y = 0f;
            rot = fwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(fwd) : Quaternion.identity;
        }
        else
        {
            // Fallback ohne Lichterpfad: vor dem Grab auf Lesehöhe.
            var basePos = anchor != null ? anchor.position : transform.position;
            var dir = transform.position - basePos;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f
                ? dir.normalized
                : (anchor != null ? anchor.forward : transform.forward);
            pos = basePos + dir * 1.25f + Vector3.up * quoteHeight;
            rot = Quaternion.LookRotation(-dir);
        }

        var root = GameObject.Find(RootName);
        if (root == null) root = new GameObject(RootName);

        var go = new GameObject("MemorialQuote_" + name);
        go.transform.SetParent(root.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);

        var label = go.AddComponent<TextMeshPro>();
        label.rectTransform.sizeDelta = new Vector2(1.8f, 1.05f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 0.35f;
        label.fontSizeMax = 0.75f;
        label.enableWordWrapping = true;
        // Fett: Die feinen Cinzel-Serifen wirken über dem hellen Nebel sonst
        // dünn und halb durchsichtig.
        label.fontStyle = FontStyles.Bold;

        var font = fontOverride != null ? fontOverride : GetSharedFont();
        if (font != null) label.font = font;
        var sharedMat = GetSharedMaterial();
        if (sharedMat != null && fontOverride == null)
        {
            label.fontSharedMaterial = sharedMat;
        }
        else if (Application.isPlaying)
        {
            label.outlineWidth = 0.16f;
            label.outlineColor = new Color(0.08f, 0.06f, 0.02f, 1f);
        }

        // Vorschau-Text für die Scene View; im Spiel ersetzt PrepareRevealText()
        // ihn durch das zufällige Zitat aus der Datenbank.
        string q = string.IsNullOrEmpty(quote) ? "Zitat erscheint nach dem Trösten" : quote;
        string a = string.IsNullOrEmpty(author) ? "…" : author;
        label.text = $"„{q}“\n<size=70%>— {a}</size>";
        label.color = textColor;

#if UNITY_EDITOR
        // Neues Szenen-Objekt -> Szene als geändert markieren, damit das Label
        // beim Speichern dauerhaft in der Szene landet.
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        return label;
    }

    private bool ResolvePath(ref Vector3 start, ref Vector3 end)
    {
        var root = GameObject.Find(pathRootName);
        if (root == null) return false;

        var tiles = new System.Collections.Generic.List<Vector3>();
        foreach (Transform c in root.transform)
            if (c.name.Contains("Road")) tiles.Add(c.position);
        if (tiles.Count < 2)
        {
            tiles.Clear();
            foreach (Transform c in root.transform) tiles.Add(c.position);
        }
        if (tiles.Count < 2) return false;

        int bi = 0, bj = 1;
        float bestSqr = -1f;
        for (int i = 0; i < tiles.Count; i++)
            for (int j = i + 1; j < tiles.Count; j++)
            {
                float s = (tiles[i] - tiles[j]).sqrMagnitude;
                if (s > bestSqr) { bestSqr = s; bi = i; bj = j; }
            }

        start = tiles[bi];
        end = tiles[bj];
        // Die Zitat-Stationen wandern Richtung Portal.
        var portal = GameObject.Find("EntrancePortal");
        if (portal != null &&
            (start - portal.transform.position).sqrMagnitude <
            (end - portal.transform.position).sqrMagnitude)
        {
            (start, end) = (end, start);
        }
        return true;
    }
}
