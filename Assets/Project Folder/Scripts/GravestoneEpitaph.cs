using UnityEngine;
using TMPro;

/// <summary>
/// Schreibt beim Spielstart eine zufällige epische Grabinschrift aus der
/// <see cref="EpitaphDatabase"/> flach auf die Vorderseite dieses Grabsteins.
/// Standard: deterministisch pro Stein (Name als Seed), damit jedes Grab
/// seine eigene, wiedererkennbare Geschichte behält.
/// </summary>
public class GravestoneEpitaph : MonoBehaviour
{
    // Verhindert doppelte Inschriften innerhalb einer Spielsitzung.
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

    [Tooltip("Deterministisch pro Stein (Name als Seed). Aus = bei jedem Start neu gewürfelt.")]
    public bool seedFromName = true;

    [Header("Platzierung auf der Steinfläche")]
    [Tooltip("Abstand über der Steinoberfläche in Metern.")]
    public float surfaceOffset = 0.008f;

    [Header("Schrift")]
    [Tooltip("Eigener TMP-Font. Leer = Cinzel Decorative aus den Projekt-Resources (Gravur-Look).")]
    public TMP_FontAsset fontOverride;
    [Tooltip("Dunkle Gravur-Farbe — liest sich auf der hellen Steintafel wie eingemeißelt.")]
    public Color textColor = new Color(0.14f, 0.11f, 0.07f, 1f);
    [Tooltip("Heller Rand für den gemeißelten Tiefen-Look.")]
    public Color outlineColor = new Color(0.75f, 0.73f, 0.66f, 0.6f);
    [Range(0f, 0.4f)] public float outlineWidth = 0.06f;

    // Einmal pro Sitzung aus der TTF erzeugt und von allen Steinen geteilt.
    private static TMP_FontAsset _engravedFont;

    private static TMP_FontAsset GetEngravedFont()
    {
        if (_engravedFont == null)
        {
            // Cinzel Decorative: klassischer Gravur-Stil roemischer Steininschriften.
            var ttf = Resources.Load<Font>("Fonts/CinzelDecorative-Regular");
            if (ttf == null) ttf = Resources.Load<Font>("Righteous-Regular");
            if (ttf != null) _engravedFont = TMP_FontAsset.CreateFontAsset(ttf);
        }
        return _engravedFont;
    }

    private void Start()
    {
        // Quelle: Persönlichkeiten-Datei aus Resources; Fallback: die alte Datenbank.
        string[] entries = GetSourceEntries();
        if (entries.Length == 0 && database != null && database.epitaphs != null)
            entries = database.epitaphs;

        int count = entries.Length;
        if (count == 0) return;

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
        if (string.IsNullOrEmpty(text)) return;

        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;
        var b = rend.bounds;

        var go = new GameObject("Epitaph_Label");
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

        // VOR den vordersten Punkt der SCHRIFTTAFEL legen (nur das Höhenband um
        // die Steinmitte — Sockel und Bogenspitze ragen weiter vor und würden
        // den Text sonst zu weit abheben lassen). So sitzt der Text knapp vor
        // dem Zierrahmen und wird nie vom Stein verdeckt (auch aus 1 m / schräg).
        float frontMost = Mathf.Min(localSize.x, localSize.z) * 0.5f;
        if (mf != null && mf.sharedMesh != null)
        {
            var l2w = mf.transform.localToWorldMatrix;
            float bandHalf = localSize.y * 0.3f;   // Panel-Band um die Mitte
            float maxD = float.NegativeInfinity;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = l2w.MultiplyPoint3x4(v);
                if (Mathf.Abs(w.y - b.center.y) > bandHalf) continue; // Sockel/Spitze aus
                float d = Vector3.Dot(w - b.center, fwd);
                if (d > maxD) maxD = d;
            }
            if (maxD > float.NegativeInfinity) frontMost = maxD;
        }

        // WICHTIG: Skalierung des Steins (z. B. 2.1) NICHT erben — sonst wird der
        // Text um diesen Faktor zu groß und quillt über den Bildrand.
        var ls = transform.lossyScale;
        go.transform.localScale = new Vector3(
            ls.x != 0 ? 1f / ls.x : 1f,
            ls.y != 0 ? 1f / ls.y : 1f,
            ls.z != 0 ? 1f / ls.z : 1f);

        go.transform.position = b.center + fwd * (frontMost + surfaceOffset);
        // TMP ist von seiner -Z-Seite lesbar: +Z vom Betrachter WEG drehen, damit
        // die Schrift zur Figur/zum Spieler hin korrekt (nicht spiegelverkehrt) steht.
        go.transform.rotation = Quaternion.LookRotation(-fwd);

        var label = go.AddComponent<TextMeshPro>();
        float faceW = Mathf.Max(localSize.x, localSize.z);
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
        label.outlineWidth = outlineWidth;
        label.outlineColor = outlineColor;
        label.text = text;
        // Doppelseitig (Cull Off): Der Text schwebt vor der Tafel, der opake Stein
        // verdeckt die Rückseite — so ist er aus dem ganzen Frontbereich sichtbar,
        // unabhängig davon, aus welchem Winkel der Spieler herantritt.
    }

    private CemeteryFigure FindNearestFigure(float maxDistance)
    {
        CemeteryFigure best = null;
        float bestSqr = maxDistance * maxDistance;
        foreach (var fig in FindObjectsOfType<CemeteryFigure>())
        {
            float sqr = (fig.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = fig; }
        }
        return best;
    }
}
