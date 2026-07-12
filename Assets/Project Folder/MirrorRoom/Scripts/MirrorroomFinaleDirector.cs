using System.Collections;
using UnityEngine;

/// <summary>
/// Bridges the self-perception sequence and the final choice. Once all pedestals have risen,
/// their sigils awaken one by one, energy converges on the mirror, and the mirror answers with
/// a halo burst. Choice interaction remains locked until this reveal has finished.
/// </summary>
public class MirrorroomFinaleDirector : MonoBehaviour
{
    [Header("Existing Sequence")]
    [SerializeField] private PedestalRiseSequence riseSequence;
    [SerializeField] private PedestalChoiceController choiceController;
    [SerializeField] private SoulMorphController mirrorController;
    [SerializeField] private Transform mirror;
    [SerializeField] private Transform[] pedestals = new Transform[3];

    [Header("Finale Timing")]
    [SerializeField] private float settleAfterRise = 0.45f;
    [SerializeField] private float channelStagger = 0.7f;
    [SerializeField] private float mirrorAnswerDelay = 0.35f;
    [SerializeField] private float choiceUnlockDelay = 1.35f;

    [Header("Look")]
    [SerializeField] private Color voidColor = new Color(0.28f, 0.52f, 1.0f, 1f);
    [SerializeField] private Color rebirthColor = new Color(0.35f, 1.0f, 0.52f, 1f);
    [SerializeField] private Color judgementColor = new Color(1.0f, 0.68f, 0.25f, 1f);
    [SerializeField] private Color mirrorCoreColor = new Color(0.63f, 0.83f, 1.0f, 1f);
    [SerializeField] private float ringRadius = 0.9f;
    [SerializeField] private float tetherHeight = 1.35f;

    private sealed class PedestalFx
    {
        public Transform pedestal;
        public Color color;
        public LineRenderer baseRing;
        public LineRenderer orbitRing;
        public LineRenderer tether;
        public ParticleSystem motes;
        public Light glow;
        public float reveal;
        public float revealTarget;
        public float pulse;
    }

    private PedestalFx[] fx;
    private ChoiceObjectRelay[] relays;
    private Transform runtimeRoot;
    private LineRenderer mirrorHaloOuter;
    private LineRenderer mirrorHaloInner;
    private ParticleSystem mirrorBurst;
    private ParticleSystem mirrorInwardMotes;
    private Light mirrorGlow;
    private float mirrorReveal;
    private float mirrorRevealTarget;
    private float mirrorPulse;
    private float elapsed;
    private bool finaleComplete;
    private bool commitPulsePlayed;

    public bool FinaleComplete => finaleComplete;

    private void Awake()
    {
        DiscoverSceneReferences();
        BuildRuntimeFx();
        CacheAndLockChoiceRelays();
    }

    private void Start()
    {
        StartCoroutine(FinaleRoutine());
    }

    private void DiscoverSceneReferences()
    {
        if (riseSequence == null) riseSequence = FindObjectOfType<PedestalRiseSequence>(true);
        if (choiceController == null) choiceController = FindObjectOfType<PedestalChoiceController>(true);
        if (mirrorController == null) mirrorController = FindObjectOfType<SoulMorphController>(true);

        if (mirror == null)
        {
            GameObject mirrorObject = GameObject.Find("StandingMirror");
            if (mirrorObject != null) mirror = mirrorObject.transform;
        }

        if (pedestals == null || pedestals.Length != 3) pedestals = new Transform[3];
        string[] names = { "Pedestal_Void", "Pedestal_Rebirth", "Pedestal_Judgement" };
        for (int i = 0; i < pedestals.Length; i++)
        {
            if (pedestals[i] != null) continue;
            GameObject pedestalObject = GameObject.Find(names[i]);
            if (pedestalObject != null) pedestals[i] = pedestalObject.transform;
        }
    }

    private void CacheAndLockChoiceRelays()
    {
        relays = FindObjectsOfType<ChoiceObjectRelay>(true);
        SetRelaysEnabled(false);
    }

    private void SetRelaysEnabled(bool value)
    {
        if (relays == null) return;
        for (int i = 0; i < relays.Length; i++)
        {
            if (relays[i] != null) relays[i].enabled = value;
        }
    }

    private void BuildRuntimeFx()
    {
        GameObject root = new GameObject("FinalAct_RuntimeVFX");
        root.transform.SetParent(transform, false);
        runtimeRoot = root.transform;

        Color[] colors = { voidColor, rebirthColor, judgementColor };
        fx = new PedestalFx[3];
        for (int i = 0; i < fx.Length; i++)
        {
            PedestalFx channelFx = new PedestalFx
            {
                pedestal = pedestals != null && i < pedestals.Length ? pedestals[i] : null,
                color = colors[i]
            };

            GameObject channel = new GameObject("ChoiceChannel_" + IndexToTheme(i));
            channel.transform.SetParent(runtimeRoot, false);
            channelFx.baseRing = CreateLine("SoulSigil_Base", channel.transform, channelFx.color, 84, true, 0.018f);
            channelFx.orbitRing = CreateLine("SoulSigil_Orbit", channel.transform, channelFx.color, 72, true, 0.012f);
            channelFx.tether = CreateLine("EnergyTether_ToMirror", channel.transform, channelFx.color, 36, false, 0.014f);
            channelFx.motes = CreatePedestalMotes("AscendingSoulMotes", channel.transform, channelFx.color);
            channelFx.glow = CreatePointLight("PedestalAura", channel.transform, channelFx.color, 2.5f);
            fx[i] = channelFx;
        }

        GameObject mirrorRoot = new GameObject("MirrorConvergence");
        mirrorRoot.transform.SetParent(runtimeRoot, false);
        mirrorHaloOuter = CreateLine("MirrorHalo_Outer", mirrorRoot.transform, mirrorCoreColor, 112, true, 0.018f);
        mirrorHaloInner = CreateLine("MirrorHalo_Inner", mirrorRoot.transform, mirrorCoreColor, 96, true, 0.011f);
        mirrorBurst = CreateMirrorBurst("MirrorSoulBurst", mirrorRoot.transform);
        mirrorInwardMotes = CreateMirrorInwardMotes("MirrorInwardMotes", mirrorRoot.transform);
        mirrorGlow = CreatePointLight("MirrorAnswerGlow", mirrorRoot.transform, mirrorCoreColor, 4.5f);
    }

    private LineRenderer CreateLine(string objectName, Transform parent, Color color, int count, bool loop, float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = count;
        line.widthMultiplier = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.material = CreateGlowMaterial(color, objectName + "_Mat");
        line.startColor = new Color(color.r, color.g, color.b, 0f);
        line.endColor = new Color(color.r, color.g, color.b, 0f);
        return line;
    }

    private Material CreateGlowMaterial(Color color, string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.DontSave,
            renderQueue = 3100
        };
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color * 2.2f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color * 2.2f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_EMISSION");
        return material;
    }

    private Light CreatePointLight(string objectName, Transform parent, Color color, float range)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(parent, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        return light;
    }

    private ParticleSystem CreatePedestalMotes(string objectName, Transform parent, Color color)
    {
        ParticleSystem ps = CreateParticleSystem(objectName, parent);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
        main.startColor = color;
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.72f;
        shape.radiusThickness = 0.55f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Unity requires all three velocity axes to use the same curve mode.
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.13f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.15f;

        SetParticleFade(ps, color, Color.Lerp(color, Color.white, 0.3f));
        ps.GetComponent<ParticleSystemRenderer>().material = CreateGlowMaterial(color, objectName + "_Mat");
        ps.Play();
        return ps;
    }

    private ParticleSystem CreateMirrorBurst(string objectName, Transform parent)
    {
        ParticleSystem ps = CreateParticleSystem(objectName, parent);
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.11f);
        main.startColor = mirrorCoreColor;
        main.maxParticles = 260;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.32f;
        shape.radiusThickness = 0.35f;
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.35f;

        SetParticleFade(ps, Color.white, new Color(0.48f, 0.22f, 1f));
        ps.GetComponent<ParticleSystemRenderer>().material = CreateGlowMaterial(mirrorCoreColor, objectName + "_Mat");
        return ps;
    }

    private ParticleSystem CreateMirrorInwardMotes(string objectName, Transform parent)
    {
        ParticleSystem ps = CreateParticleSystem(objectName, parent);
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.1f, 3.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-0.5f, -0.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.045f);
        main.startColor = mirrorCoreColor;
        main.maxParticles = 180;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.75f;
        shape.radiusThickness = 0.15f;

        SetParticleFade(ps, new Color(0.45f, 0.22f, 1f), mirrorCoreColor);
        ps.GetComponent<ParticleSystemRenderer>().material = CreateGlowMaterial(mirrorCoreColor, objectName + "_Mat");
        ps.Play();
        return ps;
    }

    private ParticleSystem CreateParticleSystem(string objectName, Transform parent)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem ps = particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private void SetParticleFade(ParticleSystem ps, Color start, Color end)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.16f), new GradientAlphaKey(0.5f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private IEnumerator FinaleRoutine()
    {
        if (riseSequence != null)
        {
            while (!riseSequence.Risen)
            {
                SetRelaysEnabled(false);
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }

        yield return new WaitForSeconds(settleAfterRise);
        for (int i = 0; i < fx.Length; i++)
        {
            if (fx[i] == null) continue;
            fx[i].revealTarget = 1f;
            fx[i].pulse = 1.35f;
            yield return new WaitForSeconds(channelStagger);
        }

        yield return new WaitForSeconds(mirrorAnswerDelay);
        mirrorRevealTarget = 1f;
        mirrorPulse = 1.5f;
        for (int i = 0; i < fx.Length; i++)
        {
            if (fx[i] != null) fx[i].pulse = 1.1f;
        }

        if (mirrorBurst != null)
        {
            mirrorBurst.transform.position = MirrorCenter();
            mirrorBurst.Play();
            mirrorBurst.Emit(180);
        }

        yield return new WaitForSeconds(choiceUnlockDelay);
        finaleComplete = true;
        SetRelaysEnabled(true);
    }

    private void Update()
    {
        if (runtimeRoot == null) return;
        float delta = Time.deltaTime;
        elapsed += delta;

        if (!finaleComplete) SetRelaysEnabled(false);

        ChoiceTheme hover = ChoiceTheme.None;
        if (finaleComplete && choiceController != null && !choiceController.Committed)
            hover = choiceController.HoverTheme;

        for (int i = 0; i < fx.Length; i++)
        {
            PedestalFx channelFx = fx[i];
            if (channelFx == null || channelFx.pedestal == null) continue;

            channelFx.reveal = Mathf.MoveTowards(channelFx.reveal, channelFx.revealTarget, delta * 1.65f);
            channelFx.pulse = Mathf.MoveTowards(channelFx.pulse, 0f, delta * 0.72f);

            ChoiceTheme theme = IndexToTheme(i);
            float focus = hover == ChoiceTheme.None ? 0.35f : (hover == theme ? 1f : 0.08f);
            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * 1.35f + i * 2.1f);
            float intensity = channelFx.reveal * (0.22f + breathe * 0.18f + focus * 0.38f + channelFx.pulse * 0.72f);

            UpdatePedestalRing(channelFx, i, intensity);
            UpdateTether(channelFx, i, focus, intensity);

            if (channelFx.motes != null)
            {
                channelFx.motes.transform.position = channelFx.pedestal.position + Vector3.up * 0.15f;
                ParticleSystem.EmissionModule emission = channelFx.motes.emission;
                emission.rateOverTime = channelFx.reveal * (5f + 10f * focus + 12f * channelFx.pulse);
            }
            if (channelFx.glow != null)
            {
                channelFx.glow.transform.position = channelFx.pedestal.position + Vector3.up * 0.35f;
                channelFx.glow.intensity = channelFx.reveal * (0.15f + focus * 0.42f + channelFx.pulse * 1.8f);
            }
        }

        mirrorReveal = Mathf.MoveTowards(mirrorReveal, mirrorRevealTarget, delta * 0.9f);
        mirrorPulse = Mathf.MoveTowards(mirrorPulse, 0f, delta * 0.52f);
        UpdateMirrorFx(hover);

        if (!commitPulsePlayed && mirrorController != null && mirrorController.ChoiceCommitted)
        {
            commitPulsePlayed = true;
            StartCoroutine(CommitSurge(mirrorController.ActiveChoice));
        }
    }

    private void UpdatePedestalRing(PedestalFx channelFx, int index, float intensity)
    {
        Vector3 center = channelFx.pedestal.position + Vector3.up * 0.04f;
        float phase = elapsed * (index % 2 == 0 ? 22f : -19f) + index * 37f;
        SetHorizontalRing(channelFx.baseRing, center, ringRadius, 0.08f, phase, 0.035f);
        Color color = Color.Lerp(channelFx.color, Color.white, Mathf.Clamp01(channelFx.pulse * 0.35f));
        SetLineVisual(channelFx.baseRing, color, Mathf.Clamp01(intensity), 0.014f + intensity * 0.018f);

        Vector3 orbitCenter = channelFx.pedestal.position + Vector3.up * (0.92f + Mathf.Sin(elapsed * 0.8f + index) * 0.04f);
        Quaternion tilt = Quaternion.Euler(63f + index * 7f, phase * 0.55f, 18f - index * 16f);
        SetTiltedRing(channelFx.orbitRing, orbitCenter, 0.53f, tilt, phase);
        SetLineVisual(channelFx.orbitRing, color, Mathf.Clamp01(intensity * 0.82f), 0.008f + intensity * 0.012f);
    }

    private void SetHorizontalRing(LineRenderer line, Vector3 center, float radius, float ellipse, float phaseDegrees, float ripple)
    {
        if (line == null) return;
        int count = line.positionCount;
        float phase = phaseDegrees * Mathf.Deg2Rad;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + phase;
            float wave = 1f + Mathf.Sin(angle * 6f - elapsed * 2.4f) * ripple;
            Vector3 point = center + new Vector3(
                Mathf.Cos(angle) * radius * wave,
                Mathf.Sin(angle * 3f + elapsed) * ellipse,
                Mathf.Sin(angle) * radius * wave);
            line.SetPosition(i, point);
        }
    }

    private void SetTiltedRing(LineRenderer line, Vector3 center, float radius, Quaternion tilt, float phaseDegrees)
    {
        if (line == null) return;
        int count = line.positionCount;
        float phase = phaseDegrees * Mathf.Deg2Rad;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + phase;
            float variedRadius = radius * (1f + Mathf.Sin(angle * 5f + elapsed * 1.8f) * 0.045f);
            line.SetPosition(i, center + tilt * new Vector3(Mathf.Cos(angle) * variedRadius, 0f, Mathf.Sin(angle) * variedRadius));
        }
    }

    private void UpdateTether(PedestalFx channelFx, int index, float focus, float intensity)
    {
        if (channelFx.tether == null || mirror == null) return;
        Vector3 start = channelFx.pedestal.position + Vector3.up * tetherHeight;
        Vector3 end = MirrorCenter() - mirror.forward * 0.09f;
        Vector3 control = Vector3.Lerp(start, end, 0.5f) + Vector3.up * (1f + index * 0.12f);
        Vector3 side = Vector3.Cross((end - start).normalized, Vector3.up);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        side.Normalize();

        int count = channelFx.tether.positionCount;
        for (int pointIndex = 0; pointIndex < count; pointIndex++)
        {
            float t = pointIndex / (float)(count - 1);
            Vector3 first = Vector3.Lerp(start, control, t);
            Vector3 second = Vector3.Lerp(control, end, t);
            Vector3 point = Vector3.Lerp(first, second, t);
            float envelope = Mathf.Sin(t * Mathf.PI);
            point += side * Mathf.Sin(t * 18f - elapsed * (5f + index)) * envelope * (0.018f + focus * 0.025f);
            point += Vector3.up * Mathf.Sin(t * 11f + elapsed * 3f) * envelope * 0.012f;
            channelFx.tether.SetPosition(pointIndex, point);
        }

        float alpha = channelFx.reveal * (0.05f + focus * 0.22f + channelFx.pulse * 0.72f);
        Color color = Color.Lerp(channelFx.color, Color.white, channelFx.pulse * 0.3f);
        SetLineVisual(channelFx.tether, color, Mathf.Clamp01(alpha), 0.008f + intensity * 0.016f);
    }

    private void UpdateMirrorFx(ChoiceTheme hover)
    {
        if (mirror == null) return;
        Vector3 center = MirrorCenter() - mirror.forward * 0.075f;
        Color color = hover == ChoiceTheme.None ? mirrorCoreColor : Color.Lerp(mirrorCoreColor, ThemeColor(hover), 0.7f);
        float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * 1.1f);
        float hoverBoost = hover == ChoiceTheme.None ? 0f : 0.45f;
        float intensity = mirrorReveal * (0.16f + breathe * 0.13f + hoverBoost) + mirrorPulse * 0.8f;

        SetMirrorEllipse(mirrorHaloOuter, center, 0.91f, 1.57f, 0.045f, elapsed * 0.8f);
        SetMirrorEllipse(mirrorHaloInner, center, 0.78f, 1.43f, 0.028f, -elapsed * 1.15f);
        SetLineVisual(mirrorHaloOuter, color, Mathf.Clamp01(intensity), 0.014f + intensity * 0.022f);
        SetLineVisual(mirrorHaloInner, Color.Lerp(color, Color.white, 0.2f), Mathf.Clamp01(intensity * 0.72f), 0.008f + intensity * 0.012f);

        if (mirrorGlow != null)
        {
            mirrorGlow.transform.position = center - mirror.forward * 0.25f;
            mirrorGlow.color = color;
            mirrorGlow.intensity = mirrorReveal * (0.18f + hoverBoost * 0.9f) + mirrorPulse * 3.6f;
        }
        if (mirrorInwardMotes != null)
        {
            mirrorInwardMotes.transform.position = center;
            ParticleSystem.EmissionModule emission = mirrorInwardMotes.emission;
            emission.rateOverTime = mirrorReveal * (8f + hoverBoost * 18f + mirrorPulse * 26f);
        }
    }

    private void SetMirrorEllipse(LineRenderer line, Vector3 center, float radiusX, float radiusY, float wave, float phase)
    {
        if (line == null || mirror == null) return;
        int count = line.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + phase;
            float radius = 1f + Mathf.Sin(angle * 7f + elapsed * 2f) * wave;
            Vector3 point = center
                + mirror.right * (Mathf.Cos(angle) * radiusX * radius)
                + mirror.up * (Mathf.Sin(angle) * radiusY * radius)
                - mirror.forward * (Mathf.Sin(angle * 3f + elapsed) * 0.025f);
            line.SetPosition(i, point);
        }
    }

    private void SetLineVisual(LineRenderer line, Color color, float alpha, float width)
    {
        if (line == null) return;
        Color visible = new Color(color.r, color.g, color.b, alpha);
        line.startColor = visible;
        line.endColor = visible;
        line.widthMultiplier = width;
        Color hdr = new Color(color.r * 2.5f, color.g * 2.5f, color.b * 2.5f, alpha);
        if (line.material.HasProperty("_BaseColor")) line.material.SetColor("_BaseColor", hdr);
        if (line.material.HasProperty("_Color")) line.material.SetColor("_Color", hdr);
    }

    private IEnumerator CommitSurge(ChoiceTheme theme)
    {
        int index = ThemeToIndex(theme);
        if (index >= 0 && index < fx.Length && fx[index] != null) fx[index].pulse = 2f;
        mirrorPulse = 1.8f;
        if (mirrorBurst != null)
        {
            mirrorBurst.transform.position = MirrorCenter();
            mirrorBurst.Play();
            mirrorBurst.Emit(220);
        }
        yield return new WaitForSeconds(0.25f);
        for (int i = 0; i < fx.Length; i++)
        {
            if (fx[i] != null && i != index) fx[i].revealTarget = 0.22f;
        }
    }

    private Vector3 MirrorCenter()
    {
        return mirror != null ? mirror.position + mirror.up * 0.05f : Vector3.zero;
    }

    private ChoiceTheme IndexToTheme(int index)
    {
        if (index == 0) return ChoiceTheme.Void;
        if (index == 1) return ChoiceTheme.Rebirth;
        if (index == 2) return ChoiceTheme.Judgement;
        return ChoiceTheme.None;
    }

    private int ThemeToIndex(ChoiceTheme theme)
    {
        if (theme == ChoiceTheme.Void) return 0;
        if (theme == ChoiceTheme.Rebirth) return 1;
        if (theme == ChoiceTheme.Judgement) return 2;
        return -1;
    }

    private Color ThemeColor(ChoiceTheme theme)
    {
        if (theme == ChoiceTheme.Void) return voidColor;
        if (theme == ChoiceTheme.Rebirth) return rebirthColor;
        if (theme == ChoiceTheme.Judgement) return judgementColor;
        return mirrorCoreColor;
    }
}
