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
    private Color _color;
    private Transform _portal;
    private bool _started;
    private float _timer;
    private int _spawned;

    private void Start()
    {
        if (choice == null) choice = FindFirstObjectByType<PedestalChoiceController>();
        if (lake == null) lake = FindFirstObjectByType<SoulLakeSurface>();
    }

    private void Update()
    {
        if (!_started)
        {
            if (choice == null || !choice.Committed) return;
            foreach (var portal in FindObjectsByType<ChoicePortal>(FindObjectsSortMode.None))
            {
                if (portal.gameObject.activeInHierarchy) { _portal = portal.transform; break; }
            }
            if (_portal == null) return;
            _color = ThemeColor(choice.HoverTheme);
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
        Vector3 pos = Vector3.Lerp(a, b, t);
        // S-Kurve quer zur Laufrichtung, an beiden Enden auf null auslaufend.
        Vector3 dir = (b - a).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        pos += side * (Mathf.Sin(t * Mathf.PI * 2f) * curveAmplitude * Mathf.Sin(t * Mathf.PI));
        pos.y = waterY + lightHeight;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = $"PathLight_{_spawned}";
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.14f;
        Destroy(go.GetComponent<Collider>());

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
}
