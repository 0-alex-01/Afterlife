using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class ECGLineUI : MaskableGraphic
{
    public enum BeatMode
    {
        AudioBeatTimes, // Spikes exakt an vorgegebenen Schlag-Zeitpunkten des Clips (empfohlen)
        AudioSpectrum,  // Schläge aus dem Frequenzspektrum erkennen (unzuverlässig)
        Timed           // ohne Audio, rein zeitgesteuert
    }

    [Header("Trace")]
    public float speed = 180f;          // Pixel/Sekunde, mit der die Kurve nach links läuft
    public float amplitude = 20f;       // Höhe des R-Zackens
    public float lineThickness = 1.5f;
    public int samples = 220;

    [Header("Schlag-Quelle")]
    public BeatMode beatMode = BeatMode.AudioBeatTimes;
    public AudioSource audioSource;     // Herzschlag-Quelle (für AudioBeatTimes & AudioSpectrum)

    [Header("AudioBeatTimes - exakte Synchronisation")]
    [Tooltip("Zeitpunkte der echten Herzschläge in Clip-Sekunden. Ein Spike erscheint, sobald die Abspielposition den Wert erreicht - dadurch automatisch synchron, auch wenn der Clip langsamer abgespielt wird.")]
    public float[] beatClipTimes;

    [Header("Piep pro Spike")]
    public AudioSource beepSource;      // separate Quelle für den Monitor-Piep
    public AudioClip beepClip;          // kurzer Piep, der bei jedem Spike abgespielt wird
    [Range(0f, 1f)] public float beepVolume = 1f;

    [Header("AudioSpectrum (Fallback)")]
    public float sensitivity = 1.4f;    // wie stark der Bass über dem Schnitt liegen muss (kleiner = empfindlicher)
    public float minBeatGap = 0.3f;     // Mindestabstand zwischen erkannten Schlägen (Sek.)
    public int bassBins = 8;            // wie viele tiefe Frequenzbänder ausgewertet werden
    public float minLevel = 0.0005f;    // Grundrauschen ignorieren (verhindert Schläge in der Stille)

    [Header("Timed (Fallback ohne Audio)")]
    public float startInterval = 0.8f;
    public float endInterval = 2.5f;
    public float flatlineTime = 42f;

    public System.Action OnBeat; // Event für Synchronisation

    private float timer;
private readonly List<float> beatTimes = new List<float>();
    private float nextBeatTime;
    private float lastBeatTime;
    private int nextBeatIndex;

    private float[] spectrum;
    private float[] energyHistory;
    private int historyIndex;

    protected override void OnEnable()
    {
        base.OnEnable();
        timer = 0f;
        beatTimes.Clear();
        nextBeatTime = startInterval;
        lastBeatTime = -minBeatGap;
        nextBeatIndex = 0;
        historyIndex = 0;
        if (spectrum == null) spectrum = new float[512];
        if (energyHistory == null) energyHistory = new float[43]; // ~1 Sekunde Verlauf
        else System.Array.Clear(energyHistory, 0, energyHistory.Length);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        switch (beatMode)
        {
            case BeatMode.AudioBeatTimes:
                DetectBeatsFromBeatTimes();
                break;
            case BeatMode.AudioSpectrum:
                if (audioSource != null) DetectBeatsFromAudio();
                else ScheduleBeatsByTime();
                break;
            default:
                ScheduleBeatsByTime();
                break;
        }

        float oldestVisible = timer - (rectTransform.rect.width / Mathf.Max(speed, 1f)) - 1f;
        while (beatTimes.Count > 0 && beatTimes[0] < oldestVisible)
            beatTimes.RemoveAt(0);

        SetVerticesDirty();
    }

    // Exakte Synchronisation: ein Spike erscheint, sobald die Abspielposition einen vorgegebenen
    // Schlag-Zeitpunkt erreicht. Da wir die Clip-Zeit (audioSource.time) abfragen, bleiben die Spikes
    // automatisch synchron - auch wenn der Herzschlag langsamer (pitch < 1) abgespielt wird.
    private void DetectBeatsFromBeatTimes()
    {
        if (audioSource == null || beatClipTimes == null || beatClipTimes.Length == 0)
            return;
        if (!audioSource.isPlaying)
            return;

        float clipTime = audioSource.time;
        while (nextBeatIndex < beatClipTimes.Length && beatClipTimes[nextBeatIndex] <= clipTime)
        {
            RegisterBeat();
            nextBeatIndex++;
        }
    }

    // Registriert einen Schlag zum aktuellen Zeitpunkt und spielt den Monitor-Piep ab
    private void RegisterBeat()
    {
        beatTimes.Add(timer);
        lastBeatTime = timer;

        if (beepSource != null && beepClip != null)
            beepSource.PlayOneShot(beepClip, beepVolume);

        OnBeat?.Invoke(); // Event feuern
    }

    // Erkennt Herzschläge an Bass-Energie, die über dem gleitenden Durchschnitt liegt
    private void DetectBeatsFromAudio()
    {
        if (!audioSource.isPlaying)
            return;

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // Energie in den tiefen Bändern aufsummieren (da sitzt der Herzschlag)
        float bass = 0f;
        int n = Mathf.Clamp(bassBins, 1, spectrum.Length);
        for (int i = 0; i < n; i++)
            bass += spectrum[i];

        // gleitender Durchschnitt der bisherigen Bass-Energie
        float avg = 0f;
        for (int i = 0; i < energyHistory.Length; i++)
            avg += energyHistory[i];
        avg /= energyHistory.Length;

        bool isBeat = bass > minLevel
                      && bass > avg * sensitivity
                      && (timer - lastBeatTime) > minBeatGap;

        if (isBeat)
            RegisterBeat();

        energyHistory[historyIndex] = bass;
        historyIndex = (historyIndex + 1) % energyHistory.Length;
    }

    // Fallback: Schläge nach Zeit, die immer langsamer werden
    private void ScheduleBeatsByTime()
    {
        while (nextBeatTime <= timer && nextBeatTime < flatlineTime)
        {
            beatTimes.Add(nextBeatTime);
            float t = Mathf.Clamp01(nextBeatTime / flatlineTime);
            nextBeatTime += Mathf.Lerp(startInterval, endInterval, t * t);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        float width = rect.width;

        List<Vector2> points = new List<Vector2>(samples);

        for (int i = 0; i < samples; i++)
        {
            float x = Mathf.Lerp(-width / 2f, width / 2f, i / (float)(samples - 1));
            float worldTime = timer - (width / 2f - x) / speed;
            points.Add(new Vector2(x, SampleY(worldTime)));
        }

        for (int i = 0; i < points.Count - 1; i++)
            AddLine(vh, points[i], points[i + 1], lineThickness);
    }

    private float SampleY(float worldTime)
    {
        if (!Application.isPlaying)
            return BeatShape(Mathf.Repeat(worldTime, startInterval));

        if (audioSource == null && worldTime >= flatlineTime)
            return Mathf.Sin(worldTime * 80f) * 0.8f;

        float beat = float.NegativeInfinity;
        for (int i = beatTimes.Count - 1; i >= 0; i--)
        {
            if (beatTimes[i] <= worldTime) { beat = beatTimes[i]; break; }
        }

        if (float.IsNegativeInfinity(beat))
            return 0f; // noch kein Schlag -> flache Grundlinie

        return BeatShape(worldTime - beat);
    }

    // Realistischer Herzschlag (QRS + T-Welle), p = Sekunden seit dem erkannten Schlag
    private float BeatShape(float p)
    {
        if (p < 0.02f) return Mathf.Lerp(0f, -amplitude * 0.12f, p / 0.02f);                      // Q: kleiner Abwärtshaken
        if (p < 0.05f) return Mathf.Lerp(-amplitude * 0.12f, amplitude, (p - 0.02f) / 0.03f);     // R: scharfer hoher Ausschlag
        if (p < 0.09f) return Mathf.Lerp(amplitude, -amplitude * 0.35f, (p - 0.05f) / 0.04f);     // S: runter unter die Linie
        if (p < 0.14f) return Mathf.Lerp(-amplitude * 0.35f, 0f, (p - 0.09f) / 0.05f);            // zurück zur Grundlinie
        if (p < 0.24f) return 0f;                                                                 // ST-Segment: flach
        if (p < 0.40f) return Mathf.Sin((p - 0.24f) / 0.16f * Mathf.PI) * amplitude * 0.28f;      // T-Welle: runder Hügel
        return 0f;                                                                                // flache Grundlinie bis zum nächsten Schlag
    }

    private void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * thickness * 0.5f;

        int index = vh.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - normal;
        vh.AddVert(vertex);

        vertex.position = start + normal;
        vh.AddVert(vertex);

        vertex.position = end + normal;
        vh.AddVert(vertex);

        vertex.position = end - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
