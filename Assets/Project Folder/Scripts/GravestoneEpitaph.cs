using UnityEngine;
using TMPro;

/// <summary>
/// Baut im Editor einmalig eine Grabinschrift als ECHTES Szenen-Objekt
/// ("Epitaph_Label" als Kind des Steins), angeschmiegt an die Steinfläche.
/// Danach ist das Label in der Scene View frei verschieb-/drehbar — das
/// Skript fasst ein vorhandenes Label NIE wieder an. Neu ausrichten geht
/// über das Kontextmenü des Components ("Inschrift neu aufbauen").
/// Im Spiel bleibt die Inschrift unsichtbar, bis die Figur an diesem Grab
/// getröstet wurde; Gräber ohne Figur zeigen sie von Anfang an.
/// </summary>
[ExecuteAlways]
public class GravestoneEpitaph : MonoBehaviour
{
    private const string LabelName = "Epitaph_Label";

    // Verhindert doppelte Inschriften innerhalb einer Session (Editor & Spiel).
    private static readonly System.Collections.Generic.HashSet<int> _usedIndices
        = new System.Collections.Generic.HashSet<int>();

    // Persönlichkeiten-Datenbank aus Resources/EpitaphSource.txt
    // (Format: Name|Lebensdaten|Epitaph — einfach Zeilen ergänzen).
    private static string[] _sourceEntries;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetUsed()
    {
        _usedIndices.Clear();
        _sourceEntries = null;
    }

    private static string[] GetSourceEntries()
    {
        if (_sourceEntries == null)
        {
            var txt = Resources.Load<TextAsset>("EpitaphSource");
            if (txt != null)
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var raw in txt.text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 3) continue;

                    string nm = parts[0].Trim(), yrs = parts[1].Trim(), tx = parts[2].Trim();
                    string entry = $"<size=115%>{nm}</size>";
                    if (yrs.Length > 0 && yrs != "—")
                        entry += $"\n<size=70%><i>{yrs}</i></size>";
                    entry += $"\n<size=78%>{tx}</size>";
                    list.Add(entry);
                }
                _sourceEntries = list.ToArray();
            }
            else
            {
                _sourceEntries = new string[0];
            }
        }
        return _sourceEntries;
    }

    [Tooltip("Datenbank mit den Inschriften.")]
    public EpitaphDatabase database;

    [Tooltip("Deterministisch pro Stein (Name als Seed). Aus = bei jedem Aufbau neu gewürfelt.")]
    public bool seedFromName = true;

    [Tooltip("Im Spiel bei jedem Start eine frische, zufällige Persönlichkeit aus dem Pool ziehen (ohne Wiederholung pro Sitzung). Der in der Szene gebakte Text ist dann nur die Scene-View-Vorschau.")]
    public bool randomizeAtRuntime = true;

    [Header("Platzierung beim (Neu-)Aufbau")]
    [Tooltip("Abstand über der Steinoberfläche in Metern (hauchdünn = Gravur-Look).")]
    public float engraveOffset = 0.002f;
    [Tooltip("Verschiebt den Text beim Aufbau nach oben (+) / unten (−), in Metern. " +
             "Feintuning danach: einfach das Epitaph_Label-Objekt in der Scene View verschieben.")]
    public float offsetUp = 0f;
    [Tooltip("Verschiebt den Text beim Aufbau nach rechts (+) / links (−) aus Lesersicht, in Metern.")]
    public float offsetSide = 0f;

    [Header("Freischaltung im Spiel")]
    [Tooltip("Inschrift erst zeigen, wenn die Figur an diesem Grab getröstet wurde. Gräber ohne Figur zeigen sie sofort.")]
    public bool revealOnComfort = true;
    [Tooltip("Wartezeit nach dem Trösten, bis die Gravur erscheint (Zerfall zuerst).")]
    public float revealDelay = 2.2f;
    [Tooltip("Dauer des Einblendens.")]
    public float revealFadeDuration = 2.5f;

    [Header("Schrift")]
    [Tooltip("Eigener TMP-Font. Leer = Cinzel Decorative aus den Projekt-Resources (Gravur-Look).")]
    public TMP_FontAsset fontOverride;
    [Tooltip("Dunkle Gravur-Farbe — liest sich auf der hellen Steintafel wie eingemeißelt.")]
    public Color textColor = new Color(0.14f, 0.11f, 0.07f, 1f);
    [Tooltip("Heller Rand für den gemeißelten Tiefen-Look.")]
    public Color outlineColor = new Color(0.75f, 0.73f, 0.66f, 0.6f);
    [Range(0f, 0.4f)] public float outlineWidth = 0.06f;

    private static TMP_FontAsset _engravedFont;
    private static Material _engravedMaterial;

    private static TMP_FontAsset GetEngravedFont()
    {
        // Persistenter Projekt-Font zuerst — nur ein echtes Asset übersteht
        // das Speichern der Szene (Labels sind jetzt Szenen-Objekte).
        if (_engravedFont == null)
            _engravedFont = Resources.Load<TMP_FontAsset>("Fonts/CinzelDecorativeSDF");
        if (_engravedFont == null)
        {
            var ttf = Resources.Load<Font>("Fonts/CinzelDecorative-Regular");
            if (ttf == null) ttf = Resources.Load<Font>("Righteous-Regular");
            if (ttf != null) _engravedFont = TMP_FontAsset.CreateFontAsset(ttf);
        }
        return _engravedFont;
    }

    private static Material GetEngravedMaterial()
    {
        if (_engravedMaterial == null)
            _engravedMaterial = Resources.Load<Material>("Fonts/CinzelEngraved");
        return _engravedMaterial;
    }

    private TextMeshPro _label;
    private CemeteryFigure _watchedFigure;
    private Color _fullFace, _fullOutline;
    private float _timer;
    private bool _triggered;

    private void OnEnable()
    {
        // Editor: Label als Szenen-Objekt anlegen, falls es noch fehlt.
        // Ein vorhandenes Label wird NIE angefasst — manuelle Verschiebungen
        // in der Scene View bleiben dauerhaft erhalten.
        if (!Application.isPlaying && FindLabel() == null)
            BuildLabel();
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _label = FindLabel();
        if (_label == null) _label = BuildLabel(); // Fallback: Szene ohne gebautes Label
        if (_label == null) return;

        // Jede Sitzung eine neue Persönlichkeit aus dem vollen Pool —
        // der gebakte Szenen-Text ist nur die Vorschau fürs Level-Design.
        if (randomizeAtRuntime) RandomizeLabelText();

        if (!revealOnComfort) return;

        _watchedFigure = FindNearestFigure(2.8f);
        if (_watchedFigure != null && !_watchedFigure.IsComforted)
        {
            _fullFace = _label.color;
            var mat = _label.fontSharedMaterial;
            _fullOutline = mat != null && mat.HasProperty("_OutlineColor")
                ? mat.GetColor("_OutlineColor")
                : outlineColor;
            SetLabelAlpha(0f);
        }
        else
        {
            _watchedFigure = null; // kein Trauernder hier -> Inschrift sofort sichtbar
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || _watchedFigure == null || _label == null) return;

        if (!_triggered)
        {
            if (_watchedFigure.IsComforted) _triggered = true;
            return;
        }

        _timer += Time.deltaTime;
        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_timer - revealDelay) / revealFadeDuration));
        SetLabelAlpha(a);
        if (a >= 1f) _watchedFigure = null; // Einblendung abgeschlossen
    }

    private void SetLabelAlpha(float a)
    {
        var f = _fullFace; f.a *= a;
        var o = _fullOutline; o.a *= a;
        _label.color = f;
        _label.outlineColor = o;
    }

    private TextMeshPro FindLabel()
    {
        foreach (var tmp in GetComponentsInChildren<TextMeshPro>(true))
            if (tmp.gameObject.name == LabelName) return tmp;
        return null;
    }

    [ContextMenu("Inschrift neu aufbauen (auto anschmiegen)")]
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

    /// <summary>Im Spiel: zufällige Persönlichkeit aus dem Pool einsetzen — ohne
    /// Wiederholung innerhalb einer Sitzung (echtes Random statt Namens-Seed).</summary>
    private void RandomizeLabelText()
    {
        string[] entries = GetSourceEntries();
        if (entries.Length == 0 && database != null && database.epitaphs != null)
            entries = database.epitaphs;
        int count = entries.Length;
        if (count == 0 || _label == null) return;

        int idx = Random.Range(0, count);
        if (_usedIndices.Count < count)
            while (_usedIndices.Contains(idx)) idx = (idx + 1) % count;
        _usedIndices.Add(idx);
        _label.text = entries[idx];
    }

    private TextMeshPro BuildLabel()
    {
        // Quelle: Persönlichkeiten-Datei aus Resources; Fallback: die alte Datenbank.
        string[] entries = GetSourceEntries();
        if (entries.Length == 0 && database != null && database.epitaphs != null)
            entries = database.epitaphs;

        int count = entries.Length;
        if (count == 0) return null;

        // Startindex per Seed, dann zum nächsten freien Eintrag weiterrücken,
        // damit keine zwei Steine dieselbe Inschrift tragen.
        int idx = seedFromName
            ? Mathf.Abs(name.GetHashCode() + transform.GetSiblingIndex() * 31) % count
            : Random.Range(0, count);
        if (_usedIndices.Count < count)
            while (_usedIndices.Contains(idx)) idx = (idx + 1) % count;
        _usedIndices.Add(idx);

        // Sicherheitsklammer gegen jede Index-Drift (z. B. geänderte Quelle).
        idx = ((idx % count) + count) % count;
        string text = entries[idx];
        if (string.IsNullOrEmpty(text)) return null;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return null;
        var b = rend.bounds;

        var go = new GameObject(LabelName);
        go.transform.SetParent(transform, false);

        // Blickrichtung: zur trauernden Figur am Grab (dort steht auch der
        // Spieler beim Trösten). Fallback: Stein-Vorderseite.
        var fwd = transform.forward;
        var nearestFig = FindNearestFigure(2.8f);
        if (nearestFig != null)
        {
            var d = nearestFig.transform.position - b.center;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) fwd = d.normalized;
        }

        // Steinmaße aus den LOKALEN Mesh-Bounds (die Welt-AABB bläht sich bei
        // gedrehten Steinen auf und macht die Textfläche zu breit).
        var mf = GetComponentInChildren<MeshFilter>();
        Vector3 localSize = mf != null && mf.sharedMesh != null
            ? Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale)
            : b.size;

        // Auf die Steinachse einrasten: Der Text muss PARALLEL zur Steinfläche
        // liegen, sonst schwebt eine Textkante sichtbar ab. Die Richtung zur
        // Figur wählt dabei nur aus, WELCHE Seite des Steins beschriftet wird.
        fwd = SnapToStoneAxis(fwd);
        var right = Vector3.Cross(Vector3.up, fwd).normalized;

        float faceW = Mathf.Max(localSize.x, localSize.z);
        float textHalfW = faceW * 0.25f;        // halbe Breite des Textfelds
        float textHalfH = localSize.y * 0.22f;  // halbe Höhe des Textfelds

        // Die Steinfläche NUR unter dem Textfeld vermessen — Zierrahmen, Sockel
        // und Bogenspitze ragen weiter vor und würden den Text von der Tafel
        // abheben. Oben/unten getrennt, um die Neigung der Fläche mitzunehmen,
        // damit sich der Text wie eine Gravur an den Stein schmiegt.
        float planeD = Mathf.Min(localSize.x, localSize.z) * 0.5f;
        float slope = 0f;
        if (mf != null && mf.sharedMesh != null)
        {
            var l2w = mf.transform.localToWorldMatrix;
            var verts = mf.sharedMesh.vertices;
            float maxD = float.NegativeInfinity;
            float dTop = float.NegativeInfinity, dBottom = float.NegativeInfinity;
            float yTop = 0f, yBottom = 0f;
            foreach (var v in verts)
            {
                var w = l2w.MultiplyPoint3x4(v);
                float dy = w.y - b.center.y;
                if (Mathf.Abs(dy) > textHalfH) continue;
                if (Mathf.Abs(Vector3.Dot(w - b.center, right)) > textHalfW) continue;
                float d = Vector3.Dot(w - b.center, fwd);
                if (d > maxD) maxD = d;
                if (dy >= 0f) { if (d > dTop) { dTop = d; yTop = dy; } }
                else if (d > dBottom) { dBottom = d; yBottom = dy; }
            }
            if (maxD > float.NegativeInfinity)
            {
                planeD = maxD;
                if (dTop > float.NegativeInfinity && dBottom > float.NegativeInfinity
                    && yTop - yBottom > 0.01f)
                {
                    // Ausreißer (Ornamente) sollen den Text nicht wegkippen.
                    slope = Mathf.Clamp((dTop - dBottom) / (yTop - yBottom), -0.45f, 0.45f);

                    // Geneigte Textebene durch die Textmitte legen und gerade so
                    // weit vorschieben, dass kein Flächenpunkt hindurchsticht.
                    float d0 = dBottom + (0f - yBottom) * slope;
                    float shift = 0f;
                    foreach (var v in verts)
                    {
                        var w = l2w.MultiplyPoint3x4(v);
                        float dy = w.y - b.center.y;
                        if (Mathf.Abs(dy) > textHalfH) continue;
                        if (Mathf.Abs(Vector3.Dot(w - b.center, right)) > textHalfW) continue;
                        float over = Vector3.Dot(w - b.center, fwd) - (d0 + slope * dy);
                        if (over > shift) shift = over;
                    }
                    planeD = d0 + shift;
                }
            }
        }

        // WICHTIG: Skalierung des Steins (z. B. 2.1) NICHT erben — sonst wird der
        // Text um diesen Faktor zu groß und quillt über den Bildrand.
        var ls = transform.lossyScale;
        go.transform.localScale = new Vector3(
            ls.x != 0 ? 1f / ls.x : 1f,
            ls.y != 0 ? 1f / ls.y : 1f,
            ls.z != 0 ? 1f / ls.z : 1f);

        go.transform.position = b.center + fwd * (planeD + engraveOffset);
        // TMP ist von seiner -Z-Seite lesbar: +Z vom Betrachter WEG drehen, damit
        // die Schrift zur Figur/zum Spieler hin korrekt (nicht spiegelverkehrt)
        // steht. Danach um die Querachse mitkippen, damit die Textebene der
        // geneigten Steinfläche folgt (Gravur statt schwebender Tafel).
        float tiltDeg = Mathf.Atan(slope) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.AngleAxis(tiltDeg, right) * Quaternion.LookRotation(-fwd);
        // Feinjustierung ENTLANG der (ggf. geneigten) Textebene, damit der Text
        // beim Verschieben auf der Steinfläche bleibt.
        go.transform.position += go.transform.up * offsetUp + go.transform.right * offsetSide;

        var label = go.AddComponent<TextMeshPro>();
        // Schmaler als die Frontfläche: der Text bleibt innerhalb des Zierrahmens.
        label.rectTransform.sizeDelta = new Vector2(faceW * 0.5f, localSize.y * 0.44f);
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 0.2f;
        label.fontSizeMax = 1.0f;
        label.enableWordWrapping = true;

        var engraved = fontOverride != null ? fontOverride : GetEngravedFont();
        if (engraved != null) label.font = engraved;
        label.color = textColor;
        // Outline über ein GETEILTES Material-Asset: Die outlineWidth/-Color-
        // Setter würden pro Label eine Material-Instanz erzeugen, die beim
        // Speichern der Szene verloren ginge (Editor loggt Material-Leaks).
        var engravedMat = GetEngravedMaterial();
        if (engravedMat != null && fontOverride == null)
        {
            label.fontSharedMaterial = engravedMat;
        }
        else if (Application.isPlaying)
        {
            label.outlineWidth = outlineWidth;
            label.outlineColor = outlineColor;
        }
        label.text = text;
        // Doppelseitig (Cull Off): Der Text schwebt hauchdünn vor der Tafel, der
        // opake Stein verdeckt die Rückseite — so ist er aus dem ganzen
        // Frontbereich sichtbar, egal aus welchem Winkel der Spieler herantritt.

#if UNITY_EDITOR
        // Neues Szenen-Objekt -> Szene als geändert markieren, damit das Label
        // beim Speichern dauerhaft in der Szene landet.
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        return label;
    }

    /// <summary>Rastet die Blickrichtung auf die nächstliegende horizontale
    /// Steinachse ein (±forward/±right), damit der Text plan auf der
    /// Steinfläche liegt statt schräg zur Figur gedreht abzustehen.</summary>
    private Vector3 SnapToStoneAxis(Vector3 toward)
    {
        Vector3 best = transform.forward;
        float bestDot = float.NegativeInfinity;
        Vector3[] axes = { transform.forward, -transform.forward, transform.right, -transform.right };
        foreach (var a in axes)
        {
            var flat = new Vector3(a.x, 0f, a.z);
            if (flat.sqrMagnitude < 1e-4f) continue;
            flat.Normalize();
            float dot = Vector3.Dot(flat, toward);
            if (dot > bestDot) { bestDot = dot; best = flat; }
        }
        return best;
    }

    private CemeteryFigure FindNearestFigure(float maxDistance)
    {
        CemeteryFigure best = null;
        float bestSqr = maxDistance * maxDistance;
        // Inaktive einschließen: Die Trauernden starten deaktiviert und werden
        // erst im Spielverlauf eingeblendet — sie gehören trotzdem zu ihrem Grab.
        foreach (var fig in FindObjectsByType<CemeteryFigure>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            float sqr = (fig.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = fig; }
        }
        return best;
    }
}
