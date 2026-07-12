using UnityEngine;
using UnityEngine.Rendering;

// Zentraler "Aufstiegs"-Controller fuer den Afterlife-Baum-Raum.
// Berechnet den normalisierten Kletterfortschritt t (0 = Baumfuss/Chaos, 1 = Krone/Harmonie)
// anhand der Spielerhoehe und koppelt daran:
//   - die Ueberblendung des Harmony-Post-Processing-Volumes (melancholisch -> warm/leuchtend)
//   - das Richtungslicht (kuehl & schwach -> warm & hell) + Ambient-Licht
//   - schwebende Geister-Partikel (chaotisch & kuehl -> ruhig & warm)
//   - ein optionales Ambient-Drone-Bett (leiser unten -> praesenter oben)
// Passt zum bestehenden FogClimbController, der denselben Hoehenbereich nutzt.
[DefaultExecutionOrder(50)]
public class AfterlifeAscensionController : MonoBehaviour
{
    [Header("Hoehen-Bereich (Welt-Y)")]
    [Tooltip("Hoehe am Baumfuss (Chaos / Beginn)")]
    public float bottomHeight = 50f;
    [Tooltip("Hoehe in der Krone (Harmonie / Ziel)")]
    public float topHeight = 70f;
    [Tooltip("Glaettung des Uebergangs")]
    public float smoothSpeed = 1.5f;

    [Header("Spieler")]
    [Tooltip("Wird automatisch per Tag 'Player' / Camera.main gesucht, falls leer")]
    public Transform player;
    public string playerTag = "Player";

    [Header("Post-Processing Ueberblendung")]
    [Tooltip("Harmony-Volume, dessen Gewicht von 0 (unten) auf 1 (oben) faehrt")]
    public Volume harmonyVolume;

    [Header("Richtungslicht (Licht-Reise)")]
    public Light sun;
    [Tooltip("Lichtfarbe am Baumfuss (kuehl, melancholisch)")]
    public Color sunColorBottom = new Color(0.55f, 0.62f, 0.80f, 1f);
    [Tooltip("Lichtfarbe in der Krone (warm, friedlich)")]
    public Color sunColorTop = new Color(1f, 0.93f, 0.78f, 1f);
    public float sunIntensityBottom = 0.55f;
    public float sunIntensityTop = 1.4f;

    [Header("Ambient-Licht (Render Settings)")]
    public bool driveAmbient = true;
    public Color ambientBottom = new Color(0.20f, 0.24f, 0.32f, 1f);
    public Color ambientTop = new Color(0.55f, 0.52f, 0.45f, 1f);

    [Header("Geister-Partikel")]
    [Tooltip("Schwebende Seelen-/Staubpartikel; Farbe & Bewegung folgen dem Aufstieg")]
    public ParticleSystem spiritMotes;
    public Color moteColorBottom = new Color(0.55f, 0.62f, 0.78f, 0.55f);
    public Color moteColorTop = new Color(1f, 0.90f, 0.70f, 0.75f);
    [Tooltip("Noise-Staerke unten (chaotisches Wirbeln)")]
    public float moteChaosBottom = 0.6f;
    [Tooltip("Noise-Staerke oben (ruhiges Schweben)")]
    public float moteChaosTop = 0.12f;

    [Header("Ambient-Drone (optional)")]
    public AudioSource ambientDrone;
    [Range(0f, 1f)] public float droneVolumeBottom = 0.35f;
    [Range(0f, 1f)] public float droneVolumeTop = 0.6f;

    // Wird vom MemoryTableauDirector weich gefahren. Dadurch kann das
    // Erinnerungsbild das Ambient-Bett ducken, ohne dass dieser Controller den
    // Wert im naechsten Frame wieder ueberschreibt.
    private float memoryDuckMultiplier = 1f;
    public float MemoryDuckMultiplier
    {
        get => memoryDuckMultiplier;
        set => memoryDuckMultiplier = Mathf.Clamp01(value);
    }

    private float smoothedT;
    private bool hasMotes;
    private ParticleSystem.MainModule moteMain;
    private ParticleSystem.NoiseModule moteNoise;

    private void Start()
    {
        if (spiritMotes != null)
        {
            hasMotes = true;
            moteMain = spiritMotes.main;
            moteNoise = spiritMotes.noise;
            moteNoise.enabled = true;
            if (!spiritMotes.isPlaying) spiritMotes.Play();
        }

        ResolvePlayer();
        // Initialwerte direkt setzen, damit der erste Frame schon stimmt
        if (player != null)
            smoothedT = ComputeTarget();
        Apply(smoothedT);
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        if (!string.IsNullOrEmpty(playerTag))
        {
            var tagged = GameObject.FindWithTag(playerTag);
            if (tagged != null) { player = tagged.transform; return; }
        }
        if (Camera.main != null) player = Camera.main.transform;
    }

    private float ComputeTarget()
    {
        float range = Mathf.Max(0.001f, topHeight - bottomHeight);
        return Mathf.Clamp01((player.position.y - bottomHeight) / range);
    }

    private void Update()
    {
        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }

        float t = ComputeTarget();
        smoothedT = Mathf.MoveTowards(smoothedT, t, Time.deltaTime * smoothSpeed);
        Apply(smoothedT);
    }

    private void Apply(float t)
    {
        // Post-processing: melancholische Basis bleibt, Harmonie blendet ein
        if (harmonyVolume != null)
            harmonyVolume.weight = t;

        // Licht-Reise
        if (sun != null)
        {
            sun.color = Color.Lerp(sunColorBottom, sunColorTop, t);
            sun.intensity = Mathf.Lerp(sunIntensityBottom, sunIntensityTop, t);
        }

        if (driveAmbient)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(ambientBottom, ambientTop, t);
        }

        // Geister-Partikel: chaotisch & kuehl unten -> ruhig & warm oben
        if (hasMotes)
        {
            moteMain.startColor = Color.Lerp(moteColorBottom, moteColorTop, t);
            float chaos = 1f - t;
            moteNoise.strength = Mathf.Lerp(moteChaosTop, moteChaosBottom, chaos);
        }

        // Ambient-Drone praesenter Richtung Krone
        if (ambientDrone != null)
            ambientDrone.volume = Mathf.Lerp(droneVolumeBottom, droneVolumeTop, t) * memoryDuckMultiplier;
    }
}
