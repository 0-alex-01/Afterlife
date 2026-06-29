using System.Collections;
using UnityEngine;

/// <summary>
/// Keeps the three choice pedestals hidden beneath the floor during the mirror self-perception
/// sequence, then raises them with a smooth, staggered, ease-out animation once that sequence is
/// complete. Each pedestal breaks through the floor with dust/light particles, a soft base glow,
/// and a low ethereal rumble; afterwards its floating themed object fades in and begins hovering,
/// and only then is pick-up / hover-preview interaction unlocked.
///
/// Completion gate (any one triggers the rise):
///   - the public <see cref="selfPerceptionComplete"/> flag is set true by another system, OR
///   - the watched SoulMorphController reaches the final Light Form state, OR
///   - a dwell fallback timer elapses (so the experience never soft-locks).
/// </summary>
public class PedestalRiseSequence : MonoBehaviour
{
    [System.Serializable]
    public class RisingPedestal
    {
        [Tooltip("The pedestal root that rises out of the floor.")]
        public Transform pedestal;
        [Tooltip("The floating themed object that fades in once the pedestal is up.")]
        public GameObject floatingObject;
        [Tooltip("Idle hover/rotate driver on the object (enabled after rise).")]
        public FloatingObject floater;
        [Tooltip("Interaction relay on the object (enabled after rise to unlock pick-up).")]
        public ChoiceObjectRelay relay;
        [Tooltip("Light emitted by the floating object (ramped up with the object fade-in).")]
        public Light objectLight;
        [Tooltip("Themed light at the pedestal (ramped up during the rise).")]
        public Light pedestalLight;
        [Tooltip("Theme tint for the auto-created break-through dust/glow.")]
        public Color themeColor = new Color(0.4f, 0.8f, 0.95f, 1f);
        [Tooltip("Per-pedestal rising sound (low rumble / ethereal swell).")]
        public AudioSource riseAudio;

        // Runtime
        [HideInInspector] public float finalLocalY;
        [HideInInspector] public Vector3 objectFinalScale = Vector3.one;
        [HideInInspector] public float objectLightTarget;
        [HideInInspector] public float pedestalLightTarget;
        [HideInInspector] public ParticleSystem riseParticles;
        [HideInInspector] public Light baseGlow;
    }

    [Header("Gate")]
    [Tooltip("Other systems can set this true to start the rise.")]
    public bool selfPerceptionComplete = false;
    [Tooltip("Watch this mirror controller; reaching Light Form completes self-perception.")]
    [SerializeField] private SoulMorphController mirror;
    [Tooltip("Treat reaching the final Light Form morph state as completion.")]
    [SerializeField] private bool completeOnLightForm = true;
    [Tooltip("Fallback: seconds after start before the rise auto-triggers regardless (safety, 0 = never).")]
    [SerializeField] private float dwellFallbackSeconds = 0f;

    [Header("Rise Animation")]
    [Tooltip("How far below the final position the pedestals start (metres).")]
    [SerializeField] private float hideDepth = 1.9f;
    [Tooltip("Seconds for one pedestal to rise.")]
    [SerializeField] private float riseDuration = 2.6f;
    [Tooltip("Delay between consecutive pedestals emerging.")]
    [SerializeField] private float stagger = 0.35f;
    [Tooltip("Seconds for a floating object to fade/scale in after its pedestal is up.")]
    [SerializeField] private float objectFadeInDuration = 1.2f;

    [Header("Pedestals")]
    [SerializeField] private RisingPedestal[] pedestals = new RisingPedestal[3];

    private bool _triggered;
    private float _elapsed;

    public bool Risen { get; private set; }

    private void Start()
    {
        foreach (var p in pedestals)
        {
            if (p == null || p.pedestal == null) continue;

            // Record standing position, then sink below the floor.
            p.finalLocalY = p.pedestal.localPosition.y;
            Vector3 lp = p.pedestal.localPosition;
            lp.y = p.finalLocalY - hideDepth;
            p.pedestal.localPosition = lp;

            // Cache light targets, then switch them off (dramatic dark start).
            if (p.pedestalLight != null) { p.pedestalLightTarget = p.pedestalLight.intensity; p.pedestalLight.intensity = 0f; }
            if (p.objectLight != null) { p.objectLightTarget = p.objectLight.intensity; p.objectLight.intensity = 0f; }

            // Hide the floating object + lock interaction until risen.
            if (p.floatingObject != null)
            {
                p.objectFinalScale = p.floatingObject.transform.localScale;
                p.floatingObject.SetActive(false);
            }
            if (p.floater != null) p.floater.enabled = false;
            if (p.relay != null) p.relay.enabled = false;

            // Auto-create break-through VFX (dust particles + soft base glow) so no manual wiring needed.
            CreateRiseFX(p);
        }
    }

    private void CreateRiseFX(RisingPedestal p)
    {
        // Soft base glow light (off until rise).
        var glowGo = new GameObject("RiseBaseGlow");
        glowGo.transform.SetParent(p.pedestal, false);
        glowGo.transform.localPosition = new Vector3(0f, -p.finalLocalY + 0.05f, 0f); // ~floor level in world
        p.baseGlow = glowGo.AddComponent<Light>();
        p.baseGlow.type = LightType.Point;
        p.baseGlow.color = p.themeColor;
        p.baseGlow.range = 2.2f;
        p.baseGlow.intensity = 0f;

        // Dust / light motes bursting at the breakthrough point.
        var psGo = new GameObject("RiseParticles");
        psGo.transform.SetParent(p.pedestal, false);
        psGo.transform.localPosition = new Vector3(0f, -p.finalLocalY + 0.05f, 0f);
        var ps = psGo.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 2.2f;
        main.startSpeed = 0.5f;
        main.startSize = 0.05f;
        main.startColor = p.themeColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;
        main.playOnAwake = false;
        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0f; // bursts only
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.35f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.6f); vel.space = ParticleSystemSimulationSpace.World;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[]{ new GradientColorKey(p.themeColor,0f), new GradientColorKey(p.themeColor,1f) },
                  new[]{ new GradientAlphaKey(0f,0f), new GradientAlphaKey(0.9f,0.25f), new GradientAlphaKey(0f,1f) });
        col.color = g;
        var psr = psGo.GetComponent<ParticleSystemRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);     // additive
        mat.SetColor("_BaseColor", p.themeColor);
        psr.material = mat;
        p.riseParticles = ps;
    }

    private void Update()
    {
        if (_triggered) return;
        _elapsed += Time.deltaTime;

        bool gate = selfPerceptionComplete;
        if (!gate && completeOnLightForm && mirror != null)
            gate = mirror.CurrentState == SoulMorphController.MorphState.Light;
        if (!gate && dwellFallbackSeconds > 0f && _elapsed >= dwellFallbackSeconds)
            gate = true;

        if (gate)
        {
            _triggered = true;
            selfPerceptionComplete = true;
            StartCoroutine(RiseAll());
        }
    }

    private IEnumerator RiseAll()
    {
        for (int i = 0; i < pedestals.Length; i++)
        {
            var p = pedestals[i];
            if (p != null && p.pedestal != null) StartCoroutine(RiseOne(p));
            if (i < pedestals.Length - 1) yield return new WaitForSeconds(stagger);
        }
        // Mark risen after the last pedestal + its fade-in finishes.
        yield return new WaitForSeconds(riseDuration + objectFadeInDuration);
        Risen = true;
    }

    private IEnumerator RiseOne(RisingPedestal p)
    {
        if (p.riseAudio != null) p.riseAudio.Play();
        if (p.riseParticles != null) p.riseParticles.Emit(60);

        float startY = p.finalLocalY - hideDepth;
        float t = 0f;
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / riseDuration);
            float ease = 1f - Mathf.Pow(1f - k, 3f); // ease-out cubic
            Vector3 lp = p.pedestal.localPosition;
            lp.y = Mathf.Lerp(startY, p.finalLocalY, ease);
            p.pedestal.localPosition = lp;

            if (p.pedestalLight != null) p.pedestalLight.intensity = Mathf.Lerp(0f, p.pedestalLightTarget, ease);
            if (p.baseGlow != null) p.baseGlow.intensity = Mathf.Sin(k * Mathf.PI) * 2.5f; // swell then settle
            yield return null;
        }
        Vector3 fp = p.pedestal.localPosition; fp.y = p.finalLocalY; p.pedestal.localPosition = fp;
        if (p.pedestalLight != null) p.pedestalLight.intensity = p.pedestalLightTarget;
        if (p.baseGlow != null) p.baseGlow.intensity = 0f;

        yield return StartCoroutine(FadeInObject(p));
    }

    private IEnumerator FadeInObject(RisingPedestal p)
    {
        if (p.floatingObject == null) yield break;
        p.floatingObject.SetActive(true);
        Vector3 end = p.objectFinalScale;
        Vector3 begin = end * 0.01f;

        float t = 0f;
        while (t < objectFadeInDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / objectFadeInDuration);
            // Gentle EaseOutBack for a graceful settle.
            const float s = 1.70158f;
            float eb = 1f + (s + 1f) * Mathf.Pow(k - 1f, 3f) + s * Mathf.Pow(k - 1f, 2f);
            p.floatingObject.transform.localScale = Vector3.LerpUnclamped(begin, end, eb);
            if (p.objectLight != null) p.objectLight.intensity = Mathf.Lerp(0f, p.objectLightTarget, k);
            yield return null;
        }
        p.floatingObject.transform.localScale = end;
        if (p.objectLight != null) p.objectLight.intensity = p.objectLightTarget;

        // Unlock idle motion + interaction now that the object is fully present.
        if (p.floater != null) p.floater.enabled = true;
        if (p.relay != null) p.relay.enabled = true;
    }
}
