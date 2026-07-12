using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Drives the cemetery's emotional progression. As grey figures are comforted,
/// the world eases (gently, over time) from a cold, fog-choked grief toward a
/// warmer, clearer peace. When every figure is released, a trail of light is
/// revealed guiding the player toward the next location.
/// </summary>
public class CemeteryAtmosphereManager : MonoBehaviour
{
    public static CemeteryAtmosphereManager Instance { get; private set; }

    [Header("Progression Settings")]
    [SerializeField] private int figuresToComfort = 12;
    [Tooltip("How fast the world eases toward its target state (progress units per second). Small = slow & gentle.")]
    [SerializeField] private float transitionSpeed = 0.15f;

    [Header("Environment References")]
    public Volume globalVolume;
    public Light directionalLight;
    public ParticleSystem spiritMotes;

    [Header("Light Tones")]
    [SerializeField] private Color melancholicLight = new Color(0.50f, 0.50f, 0.60f);
    [SerializeField] private Color peacefulLight = new Color(1.00f, 0.93f, 0.78f);
    [SerializeField] private float melancholicIntensity = 0.5f;
    [SerializeField] private float peacefulIntensity = 0.85f;

    [Header("Fog (thins as figures are released)")]
    [Tooltip("Distance multiplier applied to fog start/end at full progress. >1 pushes fog back so the cemetery feels less endless/claustrophobic.")]
    [SerializeField] private float fogFarMultiplier = 2.4f;
    [SerializeField] private Color warmFogColor = new Color(0.55f, 0.52f, 0.50f);

    [Header("Spirit Motes tint")]
    [SerializeField] private Color motesGrey = new Color(0.60f, 0.60f, 0.65f);
    [SerializeField] private Color motesWarm = new Color(1.00f, 0.85f, 0.55f);

    [Header("Path Reveal (the final release)")]
    [Tooltip("Where the player is guided after every figure is released. Auto-finds 'EntrancePortal' if left empty.")]
    public Transform nextLocationPortal;
    [Tooltip("Where the guiding light trail begins. Auto-finds 'CentralGrave' if left empty, else uses this manager.")]
    public Transform trailOrigin;
    [SerializeField] private int trailLightCount = 12;
    [SerializeField] private Color trailColor = new Color(1f, 0.72f, 0.35f);
    [SerializeField] private float trailLightIntensity = 1.6f;
    [SerializeField] private float trailRevealDuration = 6f;

    private int comfortedCount = 0;
    private readonly List<CemeteryFigure> allFigures = new List<CemeteryFigure>();

    private float targetProgress = 0f;
    private float smoothedProgress = 0f;
    private bool pathRevealed = false;

    /// <summary>Wie viele Figuren bisher getröstet wurden (zählt nur progressionsrelevante).</summary>
    public int ComfortedCount => comfortedCount;
    /// <summary>Ob die Lichtspur zum Portal bereits freigeschaltet wurde.</summary>
    public bool PathRevealed => pathRevealed;

    // captured base environment (restored implicitly at end of play mode)
    private bool fogWasOn;
    private FogMode baseFogMode;
    private float baseFogStart, baseFogEnd, baseFogDensity;
    private Color baseFogColor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        fogWasOn = RenderSettings.fog;
        baseFogMode = RenderSettings.fogMode;
        baseFogStart = RenderSettings.fogStartDistance;
        baseFogEnd = RenderSettings.fogEndDistance;
        baseFogDensity = RenderSettings.fogDensity;
        baseFogColor = RenderSettings.fogColor;

        if (nextLocationPortal == null)
        {
            var p = GameObject.Find("EntrancePortal");
            if (p != null) nextLocationPortal = p.transform;
        }
        if (trailOrigin == null)
        {
            var g = GameObject.Find("CentralGrave");
            trailOrigin = (g != null) ? g.transform : transform;
        }
    }

    public void RegisterFigure(CemeteryFigure figure)
    {
        if (figure != null && !allFigures.Contains(figure))
            allFigures.Add(figure);
    }

    /// <summary>Called by a CemeteryFigure once the player has comforted it.</summary>
    public void OnFigureComforted(CemeteryFigure figure)
    {
        // Nur Figuren an ihrem eigenen Grab treiben die Pfad-Freischaltung voran.
        // Andere (z. B. die zentrale Trauergruppe) spenden trotzdem Wärme, zählen aber nicht.
        bool counts = (figure == null) || figure.countsTowardProgression;
        if (!counts)
        {
            Debug.Log("Figure comforted (zählt nicht zur Freischaltung).");
            return;
        }

        comfortedCount = Mathf.Min(comfortedCount + 1, figuresToComfort);
        targetProgress = (float)comfortedCount / Mathf.Max(1, figuresToComfort);
        Debug.Log($"Figure comforted! {comfortedCount}/{figuresToComfort}");

        if (comfortedCount >= figuresToComfort && !pathRevealed)
            RevealPath();
    }

    private void Update()
    {
        // Ease toward the target so the atmosphere warms slowly and gently, never snapping.
        smoothedProgress = Mathf.MoveTowards(smoothedProgress, targetProgress, transitionSpeed * Time.deltaTime);
        ApplyAtmosphere(smoothedProgress);
    }

    private void ApplyAtmosphere(float t)
    {
        if (directionalLight != null)
        {
            directionalLight.color = Color.Lerp(melancholicLight, peacefulLight, t);
            directionalLight.intensity = Mathf.Lerp(melancholicIntensity, peacefulIntensity, t);
        }

        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = Color.Lerp(baseFogColor, warmFogColor, t);
            if (baseFogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = Mathf.Lerp(baseFogStart, baseFogStart * fogFarMultiplier, t);
                RenderSettings.fogEndDistance = Mathf.Lerp(baseFogEnd, baseFogEnd * fogFarMultiplier, t);
            }
            else
            {
                // exponential fog: lower density = thinner
                RenderSettings.fogDensity = Mathf.Lerp(baseFogDensity, baseFogDensity / Mathf.Max(1f, fogFarMultiplier), t);
            }
        }

        if (spiritMotes != null)
        {
            var main = spiritMotes.main;
            main.startColor = Color.Lerp(motesGrey, motesWarm, t);
        }
    }

    private void RevealPath()
    {
        pathRevealed = true;
        Debug.Log("Path revealed to next location!");

        // Das Szenen-Portal am Ziel entsperren, damit der Spieler weiterreisen kann.
        if (nextLocationPortal != null)
        {
            var portal = nextLocationPortal.GetComponentInChildren<ScenePortal>(true);
            if (portal != null) portal.Unlock();
        }

        StartCoroutine(RevealPathRoutine());
    }

    private IEnumerator RevealPathRoutine()
    {
        Vector3 start = (trailOrigin != null ? trailOrigin.position : transform.position) + Vector3.up * 0.5f;
        Vector3 end = (nextLocationPortal != null ? nextLocationPortal.position : start + transform.forward * 12f);

        var trail = new GameObject("PathOfLight");
        for (int i = 0; i < trailLightCount; i++)
        {
            float f = (i + 1f) / trailLightCount;
            var go = new GameObject($"PathLight_{i}");
            go.transform.SetParent(trail.transform, true);
            go.transform.position = Vector3.Lerp(start, end, f);

            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = trailColor;
            l.range = 5.5f;
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            StartCoroutine(FadeLight(l, trailLightIntensity, trailRevealDuration, i * 0.25f));
        }

        // Give the destination portal a warm beacon glow so it reads as the goal.
        if (nextLocationPortal != null)
        {
            var pl = nextLocationPortal.GetComponentInChildren<Light>();
            if (pl == null)
            {
                var lgo = new GameObject("PortalBeacon");
                lgo.transform.SetParent(nextLocationPortal, false);
                lgo.transform.localPosition = Vector3.zero;
                pl = lgo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.color = trailColor;
                pl.range = 10f;
                pl.shadows = LightShadows.None;
                pl.intensity = 0f;
            }
            StartCoroutine(FadeLight(pl, trailLightIntensity * 1.6f, trailRevealDuration, 0f));
        }

        yield break;
    }

    private IEnumerator FadeLight(Light l, float target, float duration, float delay)
    {
        if (l == null) yield break;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float from = l.intensity;
        float e = 0f;
        while (e < duration && l != null)
        {
            e += Time.deltaTime;
            l.intensity = Mathf.Lerp(from, target, Mathf.Clamp01(e / duration));
            yield return null;
        }
        if (l != null) l.intensity = target;
    }
}
