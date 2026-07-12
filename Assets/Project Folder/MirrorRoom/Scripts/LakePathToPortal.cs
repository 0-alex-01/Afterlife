using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Wasserpfad zum Endportal: Sobald der Spieler seine Wahl getroffen hat
/// (PedestalChoiceController.Committed), entzündet sich Licht für Licht ein
/// leuchtender Pfad über den Seelensee — vom Spiegel bis zum materialisierten
/// Wahl-Portal auf der fernen Insel. Jedes Licht tuscht beim Entzünden eine
/// Welle aufs Wasser; die Farbe folgt dem gewählten Thema. Die Lichter pulsieren
/// danach sanft und weisen den Weg. Läuft komplett zur Laufzeit.
/// </summary>
public class LakePathToPortal : MonoBehaviour
{
    [Header("Referenzen")]
    [Tooltip("Die Wahl-Steuerung (liefert Commit + Thema).")]
    public PedestalChoiceController choice;
    [Tooltip("Podest-Aufstieg; danach werden alle drei dunklen Pfadäste sichtbar.")]
    public PedestalRiseSequence pedestalRise;
    [Tooltip("Die Seeoberfläche für Wellen und Licht-Reflexe. Leer = automatisch.")]
    public SoulLakeSurface lake;
    [Tooltip("Startpunkt des Pfads (vor/neben dem Spiegel). Leer = dieses Objekt.")]
    public Transform pathStart;

    [Header("Pfad")]
    [Tooltip("Anzahl der Pfadlichter bis zum Portal.")]
    [Range(4, 24)] public int lightCount = 12;
    [Tooltip("Sekunden zwischen dem Entzünden zweier Lichter.")]
    public float igniteInterval = 0.35f;
    [Tooltip("Höhe der Lichter über dem Wasser (Meter).")]
    public float lightHeight = 0.35f;
    [Tooltip("Seitliche S-Kurve des Pfads (Meter, 0 = gerade Linie).")]
    public float curveAmplitude = 1.4f;
    [Tooltip("Jedes n-te Licht trägt ein echtes Point Light.")]
    public int realLightEvery = 3;

    [Header("Themenfarben")]
    public Color voidColor = new Color(0.35f, 0.5f, 0.95f);
    public Color rebirthColor = new Color(0.4f, 0.95f, 0.5f);
    public Color judgementColor = new Color(1f, 0.75f, 0.3f);

    // Reflex-Slots auf dem See: 18-23 gehören dem Pfad (0-17 den Seelenlichtern).
    private const int GlowSlotStart = 18;

    private readonly List<Transform> _lights = new List<Transform>();
    private readonly List<float> _birth = new List<float>();
    private readonly Dictionary<ChoiceTheme, List<Transform>> _branches = new Dictionary<ChoiceTheme, List<Transform>>();
    private Color _color;
    private Transform _portal;
    private bool _started;
    private float _timer;
    private int _spawned;
    private bool _branchesBuilt;
    private ChoiceTheme _chosenTheme = ChoiceTheme.None;

    /// <summary>Ob der Pfad bereits läuft (Commit oder Demo-Start).</summary>
    public bool Started => _started;

    /// <summary>
    /// Demo/Debug: startet den Pfad (neu) für das gegebene Thema und Ziel-Portal —
    /// unabhängig vom Commit-Zustand. Bereits entzündete Pfadlichter werden entfernt,
    /// danach entzündet sich der Pfad wie gewohnt Licht für Licht in der Themenfarbe.
    /// </summary>
    public void DebugRestart(ChoiceTheme theme, Transform portal)
    {
        foreach (var l in _lights)
            if (l != null) Destroy(l.gameObject);
        _lights.Clear();
        _birth.Clear();
        _spawned = 0;
        _timer = igniteInterval; // erstes Licht sofort
        _color = ThemeColor(theme);
        _portal = portal;
        _started = portal != null;
    }

    private void Start()
    {
        if (choice == null) choice = FindFirstObjectByType<PedestalChoiceController>();
        if (pedestalRise == null) pedestalRise = FindFirstObjectByType<PedestalRiseSequence>();
        if (lake == null) lake = FindFirstObjectByType<SoulLakeSurface>();
    }

    private void Update()
    {
        if (!_branchesBuilt && (pedestalRise == null || pedestalRise.Risen))
            BuildBranches();

        if (!_started)
        {
            if (choice == null || !choice.Committed) return;
            foreach (var portal in FindObjectsByType<ChoicePortal>(FindObjectsSortMode.None))
            {
                if (portal.gameObject.activeInHierarchy && portal.theme == choice.CommittedTheme)
                {
                    _portal = portal.transform;
                    break;
                }
            }
            if (_portal == null) return;
            _chosenTheme = choice.CommittedTheme;
            _color = ThemeColor(_chosenTheme);
            _started = true;
            _timer = igniteInterval; // erstes Licht sofort
        }

        // Nach und nach entzünden.
        if (_spawned < lightCount)
        {
            _timer += Time.deltaTime;
            if (_timer >= igniteInterval)
            {
                _timer = 0f;
                IgniteNext();
            }
        }

        // Sanftes Pulsieren der brennenden Lichter.
        for (int i = 0; i < _lights.Count; i++)
        {
            var l = _lights[i];
            if (l == null) continue;
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 2.2f + i * 0.9f);
            l.localScale = Vector3.one * 0.14f * pulse;
        }
    }

    private Color ThemeColor(ChoiceTheme theme)
    {
        switch (theme)
        {
            case ChoiceTheme.Void: return voidColor;
            case ChoiceTheme.Rebirth: return rebirthColor;
            case ChoiceTheme.Judgement: return judgementColor;
            default: return Color.white;
        }
    }

    private void IgniteNext()
    {
        float waterY = lake != null ? lake.WaterY : 0f;
        Vector3 a = pathStart != null ? pathStart.position : transform.position;
        Vector3 b = _portal.position;
        a.y = waterY; b.y = waterY;

        float t = (_spawned + 1f) / (lightCount + 1f);
        Vector3 pos = PathPosition(a, b, t, waterY);

        Transform lightTransform = null;
        if (_branches.TryGetValue(_chosenTheme, out var branch) && _spawned < branch.Count)
            lightTransform = branch[_spawned];

        GameObject go;
        if (lightTransform != null)
        {
            go = lightTransform.gameObject;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"PathLight_{_spawned}";
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            Destroy(go.GetComponent<Collider>());
        }
        go.transform.localScale = Vector3.one * 0.14f;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            var mat = new Material(shader);
            // Nur leicht überstrahlen, damit die Themenfarbe erkennbar bleibt.
            mat.SetColor("_BaseColor", _color * 1.7f);
            go.GetComponent<Renderer>().material = mat;
        }

        if (realLightEvery > 0 && _spawned % realLightEvery == 0)
        {
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = _color;
            l.intensity = 1.4f;
            l.range = 7f;
            l.shadows = LightShadows.None;
        }

        // Welle beim Entzünden + dauerhafter Reflex auf dem Wasser (6 Slots,
        // gleichmäßig über den Pfad verteilt).
        if (lake != null)
        {
            lake.AddRipple(new Vector3(pos.x, waterY, pos.z), 1f);
            if (_spawned % 2 == 0)
            {
                int slot = GlowSlotStart + (_spawned / 2) % (SoulLakeSurface.MaxGlows - GlowSlotStart);
                lake.SetGlow(slot, pos, _color, 2.2f, 0.5f);
            }
        }

        _lights.Add(go.transform);
        _birth.Add(Time.time);
        _spawned++;
    }

    private void BuildBranches()
    {
        _branchesBuilt = true;
        float waterY = lake != null ? lake.WaterY : 0f;
        Vector3 start = pathStart != null ? pathStart.position : transform.position;
        start.y = waterY;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        foreach (var portal in FindObjectsByType<ChoicePortal>(FindObjectsSortMode.None))
        {
            if (portal == null || portal.theme == ChoiceTheme.None) continue;
            var branch = new List<Transform>(lightCount);
            Vector3 end = portal.transform.position;
            end.y = waterY;

            for (int i = 0; i < lightCount; i++)
            {
                float t = (i + 1f) / (lightCount + 1f);
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Path_{portal.theme}_{i}";
                go.transform.SetParent(transform, false);
                go.transform.position = PathPosition(start, end, t, waterY);
                go.transform.localScale = Vector3.one * 0.09f;
                Destroy(go.GetComponent<Collider>());

                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.SetColor("_BaseColor", new Color(0.025f, 0.035f, 0.055f, 1f));
                    go.GetComponent<Renderer>().material = mat;
                }
                branch.Add(go.transform);
            }
            _branches[portal.theme] = branch;
        }
    }

    private Vector3 PathPosition(Vector3 a, Vector3 b, float t, float waterY)
    {
        Vector3 pos = Vector3.Lerp(a, b, t);
        Vector3 dir = (b - a).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        pos += side * (Mathf.Sin(t * Mathf.PI * 2f) * curveAmplitude * Mathf.Sin(t * Mathf.PI));
        pos.y = waterY + lightHeight;
        return pos;
    }
}
