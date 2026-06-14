using UnityEngine;

// Steuert den Szenen-Nebel abhaengig von der Kletterhoehe des Spielers.
// Unten am Baum ist der Nebel dicht; je hoeher der Spieler klettert, desto mehr
// loest sich der Nebel auf ("klettern = Nebel aufloesen"). Zusaetzlich wird die
// geschlossene Nebelscheibe am Baumfuss mit ausgeblendet.
public class FogClimbController : MonoBehaviour
{
    [Header("Hoehen-Bereich (Welt-Y)")]
    [Tooltip("Hoehe, bei der der Nebel am dichtesten ist (Baumfuss)")]
    public float bottomHeight = 50f;
    [Tooltip("Hoehe, bei der der Nebel komplett aufgeloest ist (Baumkrone)")]
    public float topHeight = 70f;

    [Header("Nebel-Dichte (Exponential Squared)")]
    [Tooltip("Dichte unten (dichter Nebel)")]
    public float densityBottom = 0.06f;
    [Tooltip("Dichte oben (klar)")]
    public float densityTop = 0.0f;

    [Header("Aussehen")]
    public Color fogColor = new Color(0.55f, 0.62f, 0.72f, 1f);

    [Header("Bodennebel-Scheibe (optional)")]
    [Tooltip("Partikelsystem der geschlossenen Nebelscheibe am Baumfuss")]
    public ParticleSystem groundFog;
    [Tooltip("Maximale Sichtbarkeit (Alpha-Multiplikator) der Nebelscheibe unten")]
    [Range(0f, 1f)] public float groundFogMaxAlpha = 0.5f;

    [Header("Nebel-Chaos (unten chaotisch -> oben ruhig)")]
    [Tooltip("Steuert das Noise-Modul der Nebelscheibe abhaengig von der Kletterhoehe: unten wirbelnd/chaotisch, oben harmonisch/ruhig.")]
    public bool driveChaos = true;
    [Tooltip("Noise-Staerke unten (chaotisch)")]
    public float chaosStrengthBottom = 1.5f;
    [Tooltip("Noise-Staerke oben (ruhig)")]
    public float chaosStrengthTop = 0f;
    [Tooltip("Noise-Frequenz (Dichte der Wirbel)")]
    public float chaosFrequency = 0.4f;
    [Tooltip("Scroll-Geschwindigkeit der Wirbel unten (oben -> 0)")]
    public float chaosScrollBottom = 0.5f;

    [Header("Spieler")]
    [Tooltip("Wird automatisch per Tag 'Player' / Camera.main gesucht, falls leer")]
    public Transform player;
    public string playerTag = "Player";

    private ParticleSystem.MainModule groundFogMain;
    private ParticleSystem.NoiseModule groundFogNoise;
    private bool hasGroundFog;
    private Color groundFogBaseColor = Color.white;
    private float smoothedT;

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;

        if (groundFog != null)
        {
            hasGroundFog = true;
            groundFogMain = groundFog.main;
            groundFogBaseColor = groundFogMain.startColor.color;

            groundFogNoise = groundFog.noise;
            if (driveChaos)
            {
                groundFogNoise.enabled = true;
                groundFogNoise.frequency = chaosFrequency;
                groundFogNoise.quality = ParticleSystemNoiseQuality.Medium;
            }

            if (!groundFog.isPlaying) groundFog.Play();
        }

        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (player != null) return;

        if (!string.IsNullOrEmpty(playerTag))
        {
            var tagged = GameObject.FindWithTag(playerTag);
            if (tagged != null) { player = tagged.transform; return; }
        }

        if (Camera.main != null)
            player = Camera.main.transform;
    }

    private void Update()
    {
        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }

        // 0 = unten (dichter Nebel), 1 = oben (klar)
        float range = Mathf.Max(0.001f, topHeight - bottomHeight);
        float t = Mathf.Clamp01((player.position.y - bottomHeight) / range);

        // Etwas glaetten, damit es nicht ruckelt
        smoothedT = Mathf.MoveTowards(smoothedT, t, Time.deltaTime * 1.5f);

        RenderSettings.fogDensity = Mathf.Lerp(densityBottom, densityTop, smoothedT);
        RenderSettings.fogColor = fogColor;

        if (hasGroundFog)
        {
            Color c = groundFogBaseColor;
            c.a = groundFogBaseColor.a * groundFogMaxAlpha * (1f - smoothedT);
            groundFogMain.startColor = c;

            var emission = groundFog.emission;
            emission.enabled = smoothedT < 0.99f;

            if (driveChaos)
            {
                // chaos = 1 unten (chaotisch), 0 oben (ruhig/harmonisch)
                float chaos = 1f - smoothedT;
                groundFogNoise.strength = Mathf.Lerp(chaosStrengthTop, chaosStrengthBottom, chaos);
                groundFogNoise.scrollSpeed = Mathf.Lerp(0f, chaosScrollBottom, chaos);
            }
        }
    }
}
