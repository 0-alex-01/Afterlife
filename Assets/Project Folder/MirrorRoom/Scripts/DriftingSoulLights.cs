using UnityEngine;

/// <summary>
/// Treibende Seelenlichter über dem Seelensee: kleine, leuchtende Kugeln, die
/// mit Perlin-Rauschen langsam über das Wasser wandern und dabei sanft auf-
/// und abschweben. Die ersten Lichter tragen echte Point Lights, alle melden
/// ihren weichen Reflex an die Seeoberfläche (<see cref="SoulLakeSurface"/>).
/// Erzeugt seine Kugeln zur Laufzeit — nichts muss von Hand platziert werden.
/// </summary>
public class DriftingSoulLights : MonoBehaviour
{
    [Header("Verteilung")]
    [Tooltip("Anzahl der Seelenlichter.")]
    [Range(1, SoulLakeSurface.MaxGlows)] public int count = 18;
    [Tooltip("Radius (Meter) um dieses Objekt, in dem die Lichter treiben.")]
    public float radius = 26f;
    [Tooltip("Mindestabstand zur Mitte (hält die Spiegelbühne frei).")]
    public float innerRadius = 5f;
    [Tooltip("Schwebehöhe über dem Wasser: min/max in Metern.")]
    public Vector2 heightRange = new Vector2(0.35f, 1.6f);

    [Header("Bewegung")]
    [Tooltip("Wandergeschwindigkeit (Meter/Sekunde).")]
    public float driftSpeed = 0.35f;
    [Tooltip("Vertikales Schweben: Amplitude in Metern.")]
    public float bobAmplitude = 0.18f;

    [Header("Licht")]
    [Tooltip("Warme Kerzenfarbe — ein Teil der Lichter.")]
    public Color warmColor = new Color(1f, 0.72f, 0.35f);
    [Tooltip("Kühle Seelenfarbe — der andere Teil.")]
    public Color coolColor = new Color(0.35f, 0.75f, 0.9f);
    [Tooltip("So viele der Lichter tragen ein echtes Point Light.")]
    public int realLightCount = 5;
    public float realLightIntensity = 1.1f;
    public float realLightRange = 7f;

    [Header("Referenzen")]
    [Tooltip("Die Seeoberfläche für die Licht-Reflexe. Leer = automatisch gesucht.")]
    public SoulLakeSurface lake;

    private Transform[] _motes;
    private Color[] _colors;
    private Vector2[] _noiseSeed;
    private float[] _bobPhase;

    private void Start()
    {
        if (lake == null) lake = FindFirstObjectByType<SoulLakeSurface>();

        _motes = new Transform[count];
        _colors = new Color[count];
        _noiseSeed = new Vector2[count];
        _bobPhase = new float[count];

        var moteShader = Shader.Find("Universal Render Pipeline/Unlit");

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"SoulLight_{i}";
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());

            float size = Random.Range(0.06f, 0.13f);
            go.transform.localScale = Vector3.one * size;

            // Startposition im Ring [innerRadius, radius].
            float ang = Random.value * Mathf.PI * 2f;
            float dist = Mathf.Sqrt(Random.value) * (radius - innerRadius) + innerRadius;
            float waterY = lake != null ? lake.WaterY : 0f;
            float h = Random.Range(heightRange.x, heightRange.y);
            go.transform.position = new Vector3(
                transform.position.x + Mathf.Cos(ang) * dist,
                waterY + h,
                transform.position.z + Mathf.Sin(ang) * dist);

            var color = Color.Lerp(warmColor, coolColor, i % 2 == 0 ? Random.Range(0f, 0.25f) : Random.Range(0.75f, 1f));
            _colors[i] = color;

            var rend = go.GetComponent<Renderer>();
            if (moteShader != null)
            {
                var mat = new Material(moteShader);
                mat.SetColor("_BaseColor", color * 2.2f); // heller als 1 -> Bloom-Glühen
                rend.material = mat;
            }

            if (i < realLightCount)
            {
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = realLightIntensity;
                l.range = realLightRange;
                l.shadows = LightShadows.None;
            }

            _motes[i] = go.transform;
            _noiseSeed[i] = new Vector2(Random.value * 100f, Random.value * 100f);
            _bobPhase[i] = Random.value * Mathf.PI * 2f;
        }
    }

    private void Update()
    {
        if (_motes == null) return;
        float waterY = lake != null ? lake.WaterY : 0f;
        float t = Time.time;

        for (int i = 0; i < _motes.Length; i++)
        {
            var m = _motes[i];
            if (m == null) continue;

            // Perlin-Wanderrichtung: langsam drehendes Richtungsfeld pro Licht.
            float dirAngle = (Mathf.PerlinNoise(_noiseSeed[i].x, t * 0.05f) - 0.5f) * Mathf.PI * 4f;
            var step = new Vector3(Mathf.Cos(dirAngle), 0f, Mathf.Sin(dirAngle)) * (driftSpeed * Time.deltaTime);
            var pos = m.position + step;

            // Im Ring bleiben: sanft zurück zur Mitte bzw. nach außen drücken.
            var flat = pos - transform.position;
            flat.y = 0f;
            float d = flat.magnitude;
            if (d > radius) pos -= flat.normalized * (d - radius) * 0.5f;
            else if (d < innerRadius && d > 0.01f) pos += flat.normalized * (innerRadius - d) * 0.5f;

            // Schweben.
            float baseH = Mathf.Lerp(heightRange.x, heightRange.y, Mathf.PerlinNoise(_noiseSeed[i].y, t * 0.03f));
            pos.y = waterY + baseH + Mathf.Sin(t * 0.7f + _bobPhase[i]) * bobAmplitude;
            m.position = pos;

            // Reflex auf dem Wasser: je tiefer das Licht, desto enger und heller.
            if (lake != null)
            {
                float h = Mathf.Max(0.05f, pos.y - waterY);
                float glowRadius = Mathf.Lerp(1.2f, 3.2f, Mathf.InverseLerp(heightRange.x, heightRange.y, h));
                float glowIntensity = Mathf.Lerp(0.55f, 0.15f, Mathf.InverseLerp(heightRange.x, heightRange.y, h));
                lake.SetGlow(i, pos, _colors[i], glowRadius, glowIntensity);
            }
        }
    }
}
