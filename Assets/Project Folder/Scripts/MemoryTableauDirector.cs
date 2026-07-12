using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inszeniert die vier Erinnerungsstationen der Baum-Szene als echte, aus den
/// vorhandenen Fragmenten gebaute Bilder. Audio-Pegel, Bewegung, Leuchten und
/// Funken reagieren auf das jeweilige Frequenzspektrum. Nach dem vierten Bild
/// verschmelzen die Fragmente in der Krone und steigen zum Portal auf.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class MemoryTableauDirector : MonoBehaviour
{
    private enum ShowState
    {
        Idle,
        ChapterGather,
        ChapterHold,
        ChapterReturn,
        FinaleGather,
        FinaleHold,
        FinaleAscend,
        Resolved
    }

    [Header("Automatisch gefundene Referenzen")]
    public Transform player;
    public AudioSource[] chapterSources = new AudioSource[4];
    public Transform[] tableauAnchors = new Transform[5];
    public AfterlifeAscensionController ascensionController;
    public TreePortalUnlock portalUnlock;

    [Header("Hoehen-Dramaturgie")]
    public float[] chapterTriggerHeights = { 56.1f, 59.9f, 63.3f, 66.1f };
    public float finaleTriggerHeight = 71.1f;
    public Vector3 treeCenter = new Vector3(-14.64f, 0f, 8.60f);

    [Header("Timing")]
    [Min(0.2f)] public float gatherDuration = 1.4f;
    [Min(0.2f)] public float returnDuration = 1.25f;
    [Min(0.2f)] public float finaleGatherDuration = 2.1f;
    [Min(0.2f)] public float finaleAscendDuration = 3.4f;

    [Header("Audio")]
    [Tooltip("Optionaler fuenfter Clip. Ohne Zuweisung wird ein dezenter Runtime-Klang erzeugt.")]
    public AudioClip finaleClip;
    public bool normalizeExistingClips = true;
    [Range(0.1f, 1f)] public float memoryVolume = 0.92f;
    [Range(0.1f, 1f)] public float ambientDuckDuringMemory = 0.43f;
    [Range(0.1f, 1f)] public float ambientDuckDuringFinale = 0.30f;

    [Header("Tableau")]
    [Range(12, 50)] public int fragmentsPerChapter = 32;
    [Range(20, 50)] public int fragmentsInFinale = 46;
    [Range(0.5f, 2f)] public float imageScale = 1f;

    public bool AllMemoriesResolved { get; private set; }
    public int CompletedMemoryCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < chapterCompleted.Length; i++)
                if (chapterCompleted[i]) count++;
            return count;
        }
    }
    public string CurrentAct => state.ToString();

    private sealed class FragmentPose
    {
        public AfterlifeFragment behaviour;
        public bool behaviourWasEnabled;
        public Transform transform;
        public Renderer[] renderers;
        public Collider[] colliders;
        public bool[] colliderWasEnabled;
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public Vector3 originalScale;
        public Vector3 phaseStartPosition;
        public Quaternion phaseStartRotation;
        public Vector3 phaseStartScale;
        public Vector2 shapePoint;
        public float seed;
    }

    private readonly List<FragmentPose> fragments = new List<FragmentPose>(50);
    private readonly List<FragmentPose> activeFragments = new List<FragmentPose>(50);
    private readonly List<LineRenderer> outlineLines = new List<LineRenderer>(6);
    private readonly List<AudioClip> generatedClips = new List<AudioClip>(6);
    private readonly float[] spectrum = new float[64];
    private readonly float[] waveform = new float[64];
    private readonly bool[] chapterCompleted = new bool[4];
    private readonly float[] targetRms = { 0.045f, 0.052f, 0.047f, 0.060f };

    private ShowState state;
    private int activeChapter = -1;
    private int nextChapter;
    private float stateTimer;
    private float lowBand;
    private float midBand;
    private float highBand;
    private float amplitude;
    private float ambientDuck = 1f;
    private float ambientDuckTarget = 1f;
    private float sparkleAccumulator;
    private AudioSource currentSource;
    private AudioSource finaleSource;
    private AudioClip runtimeFinaleClip;
    private Vector2[][] activePaths;
    private Vector3 tableauCenter;
    private Vector3 planeRight = Vector3.right;
    private Vector3 planeNormal = Vector3.forward;
    private Color activeColor = Color.white;
    private ParticleSystem sparks;
    private Light tableauLight;
    private Material lineMaterial;
    private Material particleMaterial;
    private MaterialPropertyBlock propertyBlock;
    private Transform portalTransform;

    private static readonly Color[] ChapterColors =
    {
        new Color(1.00f, 0.68f, 0.38f, 1f),
        new Color(0.90f, 0.47f, 0.78f, 1f),
        new Color(0.36f, 0.90f, 0.72f, 1f),
        new Color(1.00f, 0.86f, 0.48f, 1f)
    };

    private void Awake()
    {
        ResolveReferences();
        StopCompetingPlayback();
        CacheAndDisarmFragments();
        BuildVisualRig();
    }

    private void Start()
    {
        ConfigureChapterSources();
        if (portalUnlock != null)
            portalTransform = portalUnlock.transform;

        if (fragments.Count == 0)
            Debug.LogWarning("MemoryTableauDirector: Keine AfterlifeFragments gefunden; Linien und Partikel bleiben aktiv.", this);
        else
            Debug.Log("MemoryTableauDirector: " + fragments.Count + " Fragmente bereit. Erinnerungsfolge wartet auf den Aufstieg.", this);
    }

    private void Update()
    {
        ResolvePlayer();
        UpdateAmbientDuck();

        switch (state)
        {
            case ShowState.Idle:
                TryStartHeightTriggeredAct();
                break;
            case ShowState.ChapterGather:
            case ShowState.ChapterHold:
            case ShowState.ChapterReturn:
                UpdateChapter();
                break;
            case ShowState.FinaleGather:
            case ShowState.FinaleHold:
            case ShowState.FinaleAscend:
                UpdateFinale();
                break;
            case ShowState.Resolved:
                UpdatePortalHalo();
                break;
        }
    }

    private void ResolveReferences()
    {
        if (chapterSources == null || chapterSources.Length != 4)
            chapterSources = new AudioSource[4];
        if (tableauAnchors == null || tableauAnchors.Length != 5)
            tableauAnchors = new Transform[5];
        if (chapterTriggerHeights == null || chapterTriggerHeights.Length != 4)
            chapterTriggerHeights = new[] { 56.1f, 59.9f, 63.3f, 66.1f };

        for (int i = 0; i < 4; i++)
        {
            if (chapterSources[i] == null)
            {
                GameObject platform = GameObject.Find("BranchPlatform_" + (i + 1));
                if (platform != null) chapterSources[i] = platform.GetComponent<AudioSource>();
            }
        }

        for (int i = 0; i < 5; i++)
        {
            if (tableauAnchors[i] == null)
            {
                GameObject lantern = GameObject.Find("MemoryLantern_" + (i + 1));
                if (lantern != null) tableauAnchors[i] = lantern.transform;
            }
        }

        if (ascensionController == null)
            ascensionController = FindObjectOfType<AfterlifeAscensionController>();
        if (portalUnlock == null)
            portalUnlock = FindObjectOfType<TreePortalUnlock>();
        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        if (Camera.main != null)
        {
            player = Camera.main.transform;
            return;
        }

        try
        {
            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        catch (UnityException)
        {
            // Eine Main Camera reicht fuer die Szene; fehlender Tag ist unkritisch.
        }
    }

    private void StopCompetingPlayback()
    {
        IntermittentMemorySound[] intermittent = FindObjectsOfType<IntermittentMemorySound>(true);
        for (int i = 0; i < intermittent.Length; i++)
            intermittent[i].enabled = false;

        if (chapterSources == null) return;
        for (int i = 0; i < chapterSources.Length; i++)
        {
            AudioSource source = chapterSources[i];
            if (source == null) continue;
            source.Stop();
            source.playOnAwake = false;
            source.loop = false;
        }
    }

    private void CacheAndDisarmFragments()
    {
        AfterlifeFragment[] found = FindObjectsOfType<AfterlifeFragment>(true);
        Array.Sort(found, (a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < found.Length; i++)
        {
            AfterlifeFragment fragment = found[i];
            if (fragment == null || !fragment.gameObject.scene.IsValid()) continue;

            FragmentPose pose = new FragmentPose
            {
                behaviour = fragment,
                behaviourWasEnabled = fragment.enabled,
                transform = fragment.transform,
                renderers = fragment.GetComponentsInChildren<Renderer>(true),
                colliders = fragment.GetComponentsInChildren<Collider>(true),
                originalPosition = fragment.transform.position,
                originalRotation = fragment.transform.rotation,
                originalScale = fragment.transform.localScale,
                seed = Mathf.Repeat(i * 0.6180339f, 1f)
            };

            pose.colliderWasEnabled = new bool[pose.colliders.Length];
            for (int c = 0; c < pose.colliders.Length; c++)
            {
                pose.colliderWasEnabled[c] = pose.colliders[c] != null && pose.colliders[c].enabled;
                if (pose.colliders[c] != null) pose.colliders[c].enabled = false;
            }

            fragment.enabled = false;
            fragments.Add(pose);
        }
    }

    private void ConfigureChapterSources()
    {
        for (int i = 0; i < chapterSources.Length; i++)
        {
            AudioSource source = chapterSources[i];
            if (source == null)
            {
                Debug.LogWarning("MemoryTableauDirector: AudioSource fuer Erinnerung " + (i + 1) + " fehlt.", this);
                continue;
            }

            source.Stop();
            source.playOnAwake = false;
            source.loop = false;
            source.pitch = 1f;
            source.volume = 0f;
            source.spatialBlend = 0.88f;
            source.dopplerLevel = 0f;
            source.spread = 20f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.8f;
            source.maxDistance = 8f;

            if (normalizeExistingClips && source.clip != null)
            {
                AudioClip normalized = CreateNormalizedClip(source.clip, targetRms[i], i + 1);
                if (normalized != null) source.clip = normalized;
            }
        }
    }

    private AudioClip CreateNormalizedClip(AudioClip original, float desiredRms, int chapter)
    {
        int sampleCount = original.samples * original.channels;
        if (sampleCount <= 0) return original;

        float[] samples = new float[sampleCount];
        try
        {
            if (!original.GetData(samples, 0))
                return original;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("MemoryTableauDirector: Clip '" + original.name + "' konnte nicht normalisiert werden: " + exception.Message, this);
            return original;
        }

        double sum = 0d;
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float absolute = Mathf.Abs(samples[i]);
            peak = Mathf.Max(peak, absolute);
            sum += samples[i] * samples[i];
        }

        float rms = Mathf.Sqrt((float)(sum / Math.Max(1, samples.Length)));
        if (rms < 0.000001f || peak < 0.000001f) return original;

        float gain = Mathf.Clamp(desiredRms / rms, 0.08f, 24f);
        if (peak * gain > 0.88f) gain = 0.88f / peak;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= gain;

        AudioClip normalized = AudioClip.Create(
            original.name + "_RuntimeBalanced",
            original.samples,
            original.channels,
            original.frequency,
            false);
        normalized.SetData(samples, 0);
        generatedClips.Add(normalized);

        Debug.Log("MemoryTableauDirector: Erinnerung " + chapter + " Pegel ausgeglichen (Gain " + gain.ToString("0.00") + "x).", this);
        return normalized;
    }

    private void BuildVisualRig()
    {
        propertyBlock = new MaterialPropertyBlock();

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Unlit/Color");
        if (lineShader != null)
            lineMaterial = new Material(lineShader) { name = "MemoryTableau_Line_Runtime" };

        GameObject sparkObject = new GameObject("MemoryTableau_Sparks");
        sparkObject.transform.SetParent(transform, false);
        sparks = sparkObject.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sparks.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.085f);
        main.maxParticles = 320;
        main.gravityModifier = -0.015f;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.enabled = false;

        ParticleSystemRenderer sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (lineShader != null)
        {
            particleMaterial = new Material(lineShader) { name = "MemoryTableau_Particle_Runtime" };
            sparkRenderer.material = particleMaterial;
        }

        GameObject lightObject = new GameObject("MemoryTableau_Light");
        lightObject.transform.SetParent(transform, false);
        tableauLight = lightObject.AddComponent<Light>();
        tableauLight.type = LightType.Point;
        tableauLight.range = 5.5f;
        tableauLight.intensity = 0f;
        tableauLight.shadows = LightShadows.None;
        tableauLight.enabled = false;

        GameObject audioObject = new GameObject("MemoryFinale_Audio");
        audioObject.transform.SetParent(transform, false);
        finaleSource = audioObject.AddComponent<AudioSource>();
        finaleSource.playOnAwake = false;
        finaleSource.loop = false;
        finaleSource.spatialBlend = 0.72f;
        finaleSource.dopplerLevel = 0f;
        finaleSource.rolloffMode = AudioRolloffMode.Linear;
        finaleSource.minDistance = 1f;
        finaleSource.maxDistance = 14f;
        finaleSource.volume = 0f;
    }

    private void TryStartHeightTriggeredAct()
    {
        if (player == null) return;

        if (nextChapter < 4)
        {
            if (player.position.y >= chapterTriggerHeights[nextChapter])
                BeginChapter(nextChapter);
            return;
        }

        if (CompletedMemoryCount == 4 && !AllMemoriesResolved && player.position.y >= finaleTriggerHeight)
            BeginFinale();
    }

    private void BeginChapter(int index)
    {
        if (index < 0 || index >= 4 || state != ShowState.Idle) return;

        activeChapter = index;
        currentSource = chapterSources[index];
        activeColor = ChapterColors[index];
        activePaths = CreateChapterPaths(index);
        SetTableauFrame(tableauAnchors[index], 0.55f, 1.25f);
        SelectFragments(tableauCenter, fragmentsPerChapter, activePaths);
        PrepareOutline(activePaths);

        if (currentSource != null)
        {
            currentSource.Stop();
            currentSource.volume = 0f;
            if (currentSource.clip != null) currentSource.Play();
        }

        stateTimer = 0f;
        ambientDuckTarget = ambientDuckDuringMemory;
        state = ShowState.ChapterGather;
        SetVisualRigActive(true);
        Debug.Log("MemoryTableauDirector: Erinnerung " + (index + 1) + " formt sich — " + ChapterName(index) + ".", this);
    }

    private void UpdateChapter()
    {
        stateTimer += Time.deltaTime;
        AnalyseAudio(currentSource);

        if (state == ShowState.ChapterGather)
        {
            float k = Mathf.Clamp01(stateTimer / gatherDuration);
            float eased = Mathf.SmoothStep(0f, 1f, k);
            AnimateFragmentsIntoShape(eased, false);
            UpdateTableauVisuals(eased, false);
            if (currentSource != null) currentSource.volume = memoryVolume * eased;

            if (k >= 1f)
            {
                state = ShowState.ChapterHold;
                stateTimer = 0f;
            }
            return;
        }

        if (state == ShowState.ChapterHold)
        {
            AnimateFragmentsHolding(false);
            UpdateTableauVisuals(1f, false);
            if (currentSource != null) currentSource.volume = memoryVolume;

            bool noPlayableClip = currentSource == null || currentSource.clip == null;
            bool clipFinished = currentSource != null && currentSource.clip != null && !currentSource.isPlaying;
            if ((noPlayableClip && stateTimer >= 3.2f) || (clipFinished && stateTimer >= 0.35f))
            {
                CapturePhaseStarts();
                state = ShowState.ChapterReturn;
                stateTimer = 0f;
            }
            return;
        }

        float returnK = Mathf.Clamp01(stateTimer / returnDuration);
        float smoothReturn = Mathf.SmoothStep(0f, 1f, returnK);
        AnimateFragmentsHome(smoothReturn);
        UpdateTableauVisuals(1f - smoothReturn, false);
        if (currentSource != null) currentSource.volume = memoryVolume * (1f - smoothReturn);

        if (returnK >= 1f)
            CompleteChapter();
    }

    private void CompleteChapter()
    {
        if (currentSource != null)
        {
            currentSource.Stop();
            currentSource.volume = 0f;
        }

        chapterCompleted[activeChapter] = true;
        nextChapter = Mathf.Max(nextChapter, activeChapter + 1);
        RestoreActiveFragmentsToOrigins();
        HideOutlines();
        SetVisualRigActive(false);
        ambientDuckTarget = 1f;
        Debug.Log("MemoryTableauDirector: Erinnerung " + (activeChapter + 1) + " ist verankert.", this);

        activeChapter = -1;
        currentSource = null;
        state = ShowState.Idle;
        stateTimer = 0f;
    }

    private void BeginFinale()
    {
        if (state != ShowState.Idle || AllMemoriesResolved) return;

        activeColor = new Color(1f, 0.91f, 0.58f, 1f);
        activePaths = CreateFinalePaths();
        SetTableauFrame(tableauAnchors[4], 1.15f, 1.55f);
        SelectFragments(tableauCenter, fragmentsInFinale, activePaths);
        PrepareOutline(activePaths);

        if (finaleClip == null && runtimeFinaleClip == null)
            runtimeFinaleClip = CreateRuntimeFinaleClip();
        finaleSource.clip = finaleClip != null ? finaleClip : runtimeFinaleClip;
        finaleSource.transform.position = tableauCenter;
        finaleSource.volume = 0f;
        if (finaleSource.clip != null) finaleSource.Play();

        state = ShowState.FinaleGather;
        stateTimer = 0f;
        ambientDuckTarget = ambientDuckDuringFinale;
        SetVisualRigActive(true);
        Debug.Log("MemoryTableauDirector: Vier Erinnerungen antworten einander — Kronensynthese beginnt.", this);
    }

    private void UpdateFinale()
    {
        stateTimer += Time.deltaTime;
        AnalyseAudio(finaleSource);

        if (state == ShowState.FinaleGather)
        {
            float k = Mathf.Clamp01(stateTimer / finaleGatherDuration);
            float eased = Mathf.SmoothStep(0f, 1f, k);
            AnimateFragmentsIntoShape(eased, true);
            UpdateTableauVisuals(eased, true);
            finaleSource.volume = memoryVolume * eased;

            if (k >= 1f)
            {
                state = ShowState.FinaleHold;
                stateTimer = 0f;
            }
            return;
        }

        if (state == ShowState.FinaleHold)
        {
            AnimateFragmentsHolding(true);
            UpdateTableauVisuals(1f, true);
            finaleSource.volume = memoryVolume;

            bool finished = finaleSource.clip == null || !finaleSource.isPlaying;
            if (stateTimer >= 2.4f && finished)
            {
                CapturePhaseStarts();
                state = ShowState.FinaleAscend;
                stateTimer = 0f;
                HideOutlines();
            }
            return;
        }

        float ascendK = Mathf.Clamp01(stateTimer / finaleAscendDuration);
        AnimateFragmentsToPortal(ascendK);
        EmitSparks(tableauCenter, activeColor, 1 + Mathf.RoundToInt(5f * (1f - ascendK)));
        finaleSource.volume = memoryVolume * (1f - ascendK);
        tableauLight.intensity = Mathf.Lerp(3.2f, 1.7f, ascendK);

        if (ascendK >= 1f)
            CompleteFinale();
    }

    private void CompleteFinale()
    {
        finaleSource.Stop();
        finaleSource.volume = 0f;
        AllMemoriesResolved = true;
        state = ShowState.Resolved;
        stateTimer = 0f;
        ambientDuckTarget = 1f;
        activeColor = new Color(1f, 0.88f, 0.48f, 1f);
        if (portalUnlock != null) portalUnlock.Unlock();
        Debug.Log("MemoryTableauDirector: Kronensynthese abgeschlossen — Fragmentkranz und Portal sind aktiv.", this);
    }

    private void SelectFragments(Vector3 center, int requestedCount, Vector2[][] paths)
    {
        activeFragments.Clear();
        List<FragmentPose> ordered = new List<FragmentPose>(fragments);
        ordered.Sort((a, b) =>
            (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));

        int count = Mathf.Min(requestedCount, ordered.Count);
        List<Vector2> sampled = SamplePaths(paths, count);
        for (int i = 0; i < count; i++)
        {
            FragmentPose pose = ordered[i];
            pose.phaseStartPosition = pose.transform.position;
            pose.phaseStartRotation = pose.transform.rotation;
            pose.phaseStartScale = pose.transform.localScale;
            pose.shapePoint = sampled[i];
            activeFragments.Add(pose);
        }
    }

    private void CapturePhaseStarts()
    {
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            pose.phaseStartPosition = pose.transform.position;
            pose.phaseStartRotation = pose.transform.rotation;
            pose.phaseStartScale = pose.transform.localScale;
        }
    }

    private void AnimateFragmentsIntoShape(float k, bool finale)
    {
        Quaternion facing = Quaternion.LookRotation(planeNormal, Vector3.up);
        float reactiveScale = 0.82f + amplitude * 0.24f + lowBand * 0.22f;
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            Vector3 target = ShapePointToWorld(pose.shapePoint, 1f);
            float delay = Mathf.Clamp01(k * 1.22f - pose.seed * 0.22f);
            float localK = Mathf.SmoothStep(0f, 1f, delay);
            pose.transform.position = Vector3.Lerp(pose.phaseStartPosition, target, localK);
            Quaternion targetRotation = facing * Quaternion.Euler(0f, 0f, (pose.seed - 0.5f) * 36f);
            pose.transform.rotation = Quaternion.Slerp(pose.phaseStartRotation, targetRotation, localK);
            Vector3 targetScale = pose.originalScale * reactiveScale * (finale ? 1.05f : 1f);
            pose.transform.localScale = Vector3.Lerp(pose.phaseStartScale, targetScale, localK);
            TintFragment(pose, activeColor, 1.2f + localK * 2.4f);
        }
    }

    private void AnimateFragmentsHolding(bool finale)
    {
        Quaternion facing = Quaternion.LookRotation(planeNormal, Vector3.up);
        float pulse = 0.88f + amplitude * 0.36f + lowBand * 0.28f;
        float time = Time.time;

        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            float phase = pose.seed * 9f + time * (1.25f + midBand * 2.4f);
            Vector3 wobble = planeNormal * Mathf.Sin(phase) * (0.018f + midBand * 0.085f);
            wobble += Vector3.up * Mathf.Cos(phase * 0.73f) * (0.012f + midBand * 0.038f);
            pose.transform.position = ShapePointToWorld(pose.shapePoint, 1f + lowBand * 0.035f) + wobble;
            pose.transform.rotation = facing * Quaternion.Euler(0f, 0f, (pose.seed - 0.5f) * 34f + Mathf.Sin(phase) * 5f);
            pose.transform.localScale = pose.originalScale * pulse * (finale ? 1.08f : 1f);
            TintFragment(pose, activeColor, 2.8f + amplitude * 3.2f);
        }
    }

    private void AnimateFragmentsHome(float k)
    {
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            pose.transform.position = Vector3.Lerp(pose.phaseStartPosition, pose.originalPosition, k);
            pose.transform.rotation = Quaternion.Slerp(pose.phaseStartRotation, pose.originalRotation, k);
            pose.transform.localScale = Vector3.Lerp(pose.phaseStartScale, pose.originalScale, k);
            TintFragment(pose, activeColor, Mathf.Lerp(2.5f, 0f, k));
        }
    }

    private void AnimateFragmentsToPortal(float k)
    {
        Vector3 portal = GetPortalHaloCenter();
        float eased = Mathf.SmoothStep(0f, 1f, k);
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            float angle = pose.seed * Mathf.PI * 2f + k * Mathf.PI * 4.5f;
            float radius = Mathf.Lerp(0.65f, 1.55f, pose.seed) * Mathf.Lerp(1.2f, 0.55f, k);
            Vector3 ring = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 1.7f) * 0.35f, Mathf.Sin(angle)) * radius;
            Vector3 risingTarget = Vector3.Lerp(tableauCenter, portal, eased) + ring;
            pose.transform.position = Vector3.Lerp(pose.phaseStartPosition, risingTarget, eased);
            pose.transform.Rotate(35f * Time.deltaTime, 70f * Time.deltaTime, 25f * Time.deltaTime, Space.Self);
            pose.transform.localScale = pose.originalScale * Mathf.Lerp(1.05f, 0.72f, eased);
            TintFragment(pose, activeColor, Mathf.Lerp(4.8f, 3.2f, eased));
        }

        tableauCenter = Vector3.Lerp(tableauCenter, portal, Time.deltaTime * 1.8f);
        tableauLight.transform.position = tableauCenter;
        finaleSource.transform.position = tableauCenter;
    }

    private void UpdatePortalHalo()
    {
        stateTimer += Time.deltaTime;
        Vector3 center = GetPortalHaloCenter();
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            float angle = pose.seed * Mathf.PI * 2f + stateTimer * (0.16f + pose.seed * 0.08f);
            float radius = Mathf.Lerp(0.75f, 1.8f, pose.seed);
            float y = Mathf.Sin(angle * 1.8f + pose.seed * 5f) * 0.42f;
            pose.transform.position = center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            pose.transform.Rotate(8f * Time.deltaTime, 18f * Time.deltaTime, 6f * Time.deltaTime, Space.Self);
            pose.transform.localScale = pose.originalScale * (0.72f + 0.08f * Mathf.Sin(stateTimer * 1.4f + pose.seed * 8f));
            TintFragment(pose, activeColor, 3.4f);
        }

        tableauLight.enabled = true;
        tableauLight.transform.position = center;
        tableauLight.color = activeColor;
        tableauLight.range = 6.5f;
        tableauLight.intensity = 1.8f + Mathf.Sin(stateTimer * 1.2f) * 0.25f;
        sparkleAccumulator += Time.deltaTime * 3.5f;
        if (sparkleAccumulator >= 1f)
        {
            int count = Mathf.FloorToInt(sparkleAccumulator);
            sparkleAccumulator -= count;
            EmitSparks(center, activeColor, count);
        }
    }

    private Vector3 GetPortalHaloCenter()
    {
        if (portalTransform == null && portalUnlock != null)
            portalTransform = portalUnlock.transform;
        return portalTransform != null ? portalTransform.position + Vector3.up * 0.45f : new Vector3(treeCenter.x, 88f, treeCenter.z);
    }

    private void SetTableauFrame(Transform anchor, float verticalOffset, float outwardDistance)
    {
        Vector3 basePosition = anchor != null
            ? anchor.position
            : new Vector3(treeCenter.x, finaleTriggerHeight, treeCenter.z + 2f);
        Vector3 radial = new Vector3(basePosition.x - treeCenter.x, 0f, basePosition.z - treeCenter.z);
        if (radial.sqrMagnitude < 0.01f) radial = Vector3.forward;
        planeNormal = radial.normalized;
        planeRight = Vector3.Cross(Vector3.up, planeNormal).normalized;
        tableauCenter = basePosition + planeNormal * outwardDistance + Vector3.up * verticalOffset;
    }

    private Vector3 ShapePointToWorld(Vector2 point, float pulseScale)
    {
        return tableauCenter +
               planeRight * point.x * imageScale * pulseScale +
               Vector3.up * point.y * imageScale * pulseScale;
    }

    private void AnalyseAudio(AudioSource source)
    {
        float targetLow = 0f;
        float targetMid = 0f;
        float targetHigh = 0f;
        float targetAmplitude = 0f;

        if (source != null && source.isPlaying)
        {
            source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            source.GetOutputData(waveform, 0);

            for (int i = 0; i < 8; i++) targetLow += spectrum[i];
            for (int i = 8; i < 25; i++) targetMid += spectrum[i];
            for (int i = 25; i < 64; i++) targetHigh += spectrum[i];
            targetLow = Mathf.Clamp01(targetLow / 8f * 95f);
            targetMid = Mathf.Clamp01(targetMid / 17f * 190f);
            targetHigh = Mathf.Clamp01(targetHigh / 39f * 420f);

            float sum = 0f;
            for (int i = 0; i < waveform.Length; i++) sum += waveform[i] * waveform[i];
            targetAmplitude = Mathf.Clamp01(Mathf.Sqrt(sum / waveform.Length) * 9f);
        }

        float smoothing = 1f - Mathf.Exp(-Time.deltaTime * 8f);
        lowBand = Mathf.Lerp(lowBand, targetLow, smoothing);
        midBand = Mathf.Lerp(midBand, targetMid, smoothing);
        highBand = Mathf.Lerp(highBand, targetHigh, smoothing);
        amplitude = Mathf.Lerp(amplitude, targetAmplitude, smoothing);
    }

    private void UpdateTableauVisuals(float visibility, bool finale)
    {
        float pulse = 1f + lowBand * 0.06f + amplitude * 0.04f;
        UpdateOutlines(activePaths, visibility, pulse);

        tableauLight.enabled = visibility > 0.01f;
        tableauLight.transform.position = tableauCenter + planeNormal * 0.25f;
        tableauLight.color = activeColor;
        tableauLight.range = finale ? 7f : 5.5f;
        tableauLight.intensity = visibility * ((finale ? 2.5f : 1.55f) + lowBand * 1.7f + amplitude * 1.4f);

        sparkleAccumulator += Time.deltaTime * visibility * (1.2f + highBand * 18f);
        if (sparkleAccumulator >= 1f)
        {
            int count = Mathf.Min(8, Mathf.FloorToInt(sparkleAccumulator));
            sparkleAccumulator -= count;
            EmitSparks(tableauCenter, activeColor, count);
        }
    }

    private void SetVisualRigActive(bool active)
    {
        if (tableauLight != null) tableauLight.enabled = active;
        if (!active) sparkleAccumulator = 0f;
    }

    private void EmitSparks(Vector3 center, Color color, int count)
    {
        if (sparks == null || count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = center + planeRight * UnityEngine.Random.Range(-1.35f, 1.35f) +
                           Vector3.up * UnityEngine.Random.Range(-0.65f, 1.45f) +
                           planeNormal * UnityEngine.Random.Range(-0.2f, 0.35f),
                startColor = new Color(color.r, color.g, color.b, UnityEngine.Random.Range(0.45f, 0.95f)),
                startSize = UnityEngine.Random.Range(0.025f, 0.085f)
            };
            sparks.Emit(emit, 1);
        }
    }

    private void TintFragment(FragmentPose pose, Color color, float emissionStrength)
    {
        if (pose.renderers == null) return;
        Color baseColor = Color.Lerp(Color.white, color, 0.62f);
        Color emission = color * emissionStrength;
        for (int i = 0; i < pose.renderers.Length; i++)
        {
            Renderer renderer = pose.renderers[i];
            if (renderer == null) continue;
            propertyBlock.Clear();
            propertyBlock.SetColor("_BaseColor", baseColor);
            propertyBlock.SetColor("_Color", baseColor);
            propertyBlock.SetColor("_EmissionColor", emission);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void PrepareOutline(Vector2[][] paths)
    {
        EnsureOutlineCount(paths.Length);
        for (int i = 0; i < outlineLines.Count; i++)
            outlineLines[i].enabled = i < paths.Length;
    }

    private void EnsureOutlineCount(int count)
    {
        while (outlineLines.Count < count)
        {
            GameObject lineObject = new GameObject("MemoryStroke_" + (outlineLines.Count + 1));
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (lineMaterial != null) line.material = lineMaterial;
            outlineLines.Add(line);
        }
    }

    private void UpdateOutlines(Vector2[][] paths, float alpha, float pulse)
    {
        if (paths == null) return;
        Color lineColor = new Color(activeColor.r, activeColor.g, activeColor.b, Mathf.Clamp01(alpha) * 0.82f);
        for (int p = 0; p < outlineLines.Count; p++)
        {
            LineRenderer line = outlineLines[p];
            if (p >= paths.Length || alpha <= 0.005f)
            {
                line.enabled = false;
                continue;
            }

            line.enabled = true;
            Vector2[] path = paths[p];
            line.positionCount = path.Length;
            for (int i = 0; i < path.Length; i++)
                line.SetPosition(i, ShapePointToWorld(path[i], pulse) + planeNormal * 0.035f);
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.widthMultiplier = Mathf.Lerp(0.012f, 0.045f + highBand * 0.025f, alpha);
        }
    }

    private void HideOutlines()
    {
        for (int i = 0; i < outlineLines.Count; i++)
            outlineLines[i].enabled = false;
    }

    private void UpdateAmbientDuck()
    {
        ambientDuck = Mathf.MoveTowards(ambientDuck, ambientDuckTarget, Time.deltaTime * 0.85f);
        if (ascensionController == null)
            ascensionController = FindObjectOfType<AfterlifeAscensionController>();
        if (ascensionController != null)
            ascensionController.MemoryDuckMultiplier = ambientDuck;
    }

    private void RestoreActiveFragmentsToOrigins()
    {
        for (int i = 0; i < activeFragments.Count; i++)
        {
            FragmentPose pose = activeFragments[i];
            pose.transform.position = pose.originalPosition;
            pose.transform.rotation = pose.originalRotation;
            pose.transform.localScale = pose.originalScale;
            for (int r = 0; r < pose.renderers.Length; r++)
                if (pose.renderers[r] != null) pose.renderers[r].SetPropertyBlock(null);
        }
        activeFragments.Clear();
    }

    private void RestoreEverything()
    {
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentPose pose = fragments[i];
            if (pose.transform != null)
            {
                pose.transform.position = pose.originalPosition;
                pose.transform.rotation = pose.originalRotation;
                pose.transform.localScale = pose.originalScale;
            }
            if (pose.behaviour != null) pose.behaviour.enabled = pose.behaviourWasEnabled;
            for (int c = 0; c < pose.colliders.Length; c++)
                if (pose.colliders[c] != null) pose.colliders[c].enabled = pose.colliderWasEnabled[c];
            for (int r = 0; r < pose.renderers.Length; r++)
                if (pose.renderers[r] != null) pose.renderers[r].SetPropertyBlock(null);
        }
    }

    private AudioClip CreateRuntimeFinaleClip()
    {
        const int sampleRate = 44100;
        const float duration = 6.2f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.15f));
            float release = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((t - 4.65f) / 1.55f));
            float envelope = attack * release;
            float rise = Mathf.Lerp(96f, 188f, t / duration);
            float tone = Mathf.Sin(2f * Mathf.PI * rise * t) * 0.038f;
            tone += Mathf.Sin(2f * Mathf.PI * rise * 1.5f * t + 0.4f) * 0.022f;

            float chime = 0f;
            float[] starts = { 0.65f, 1.9f, 3.25f, 4.45f };
            float[] frequencies = { 392f, 523.25f, 659.25f, 783.99f };
            for (int c = 0; c < starts.Length; c++)
            {
                float local = t - starts[c];
                if (local >= 0f)
                    chime += Mathf.Sin(2f * Mathf.PI * frequencies[c] * local) * Mathf.Exp(-local * 1.9f) * 0.045f;
            }
            data[i] = (tone * envelope + chime) * 0.9f;
        }

        AudioClip clip = AudioClip.Create("Memory_05_RuntimeSynthesis", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        generatedClips.Add(clip);
        return clip;
    }

    private static string ChapterName(int index)
    {
        switch (index)
        {
            case 0: return "Wiege und Lebenskeim";
            case 1: return "Stimmenkreis";
            case 2: return "Waldatem und Vogel";
            default: return "Lichtbaum der Harmonie";
        }
    }

    private static Vector2[][] CreateChapterPaths(int index)
    {
        switch (index)
        {
            case 0:
                return new[]
                {
                    Points(-1.45f, 0.28f, -1.15f, -0.22f, -0.62f, -0.56f, 0f, -0.68f, 0.62f, -0.56f, 1.15f, -0.22f, 1.45f, 0.28f),
                    Points(-1.48f, 0.31f, -0.78f, 0.16f, 0f, 0.11f, 0.78f, 0.16f, 1.48f, 0.31f),
                    Points(-0.38f, 0.18f, -0.35f, 0.68f, 0f, 1.03f, 0.35f, 0.68f, 0.38f, 0.18f),
                    Points(-1.05f, 0.92f, -0.55f, 1.32f, 0f, 1.48f, 0.55f, 1.32f, 1.05f, 0.92f)
                };
            case 1:
                return new[]
                {
                    PersonPath(-0.92f, 0.05f, 0.31f),
                    PersonPath(0f, 0.28f, 0.34f),
                    PersonPath(0.92f, 0.05f, 0.31f),
                    CirclePath(0f, 0.15f, 1.28f, 18)
                };
            case 2:
                return new[]
                {
                    Points(-1.58f, 0.02f, -1.03f, 0.24f, -0.42f, 0.94f, -0.08f, 0.28f, 0.48f, 1.06f, 0.82f, 0.31f, 1.55f, 0.08f, 0.80f, -0.15f, 0.26f, -0.34f, -0.34f, -0.68f, -0.88f, -0.31f, -1.58f, 0.02f),
                    Points(-0.84f, 0.05f, -0.26f, 0.43f, 0.02f, 0.20f, 0.54f, 0.52f, 0.88f, 0.16f),
                    Points(0.80f, -0.15f, 1.18f, -0.52f, 0.55f, -0.35f)
                };
            default:
                return CreateTreePaths(false);
        }
    }

    private static Vector2[][] CreateFinalePaths()
    {
        Vector2[][] tree = CreateTreePaths(true);
        Vector2[][] result = new Vector2[tree.Length + 2][];
        for (int i = 0; i < tree.Length; i++) result[i] = tree[i];
        result[tree.Length] = CirclePath(0f, 0.55f, 1.62f, 24);
        result[tree.Length + 1] = Points(-1.42f, 1.16f, -0.72f, 1.65f, 0f, 1.98f, 0.74f, 1.64f, 1.42f, 1.16f);
        return result;
    }

    private static Vector2[][] CreateTreePaths(bool tall)
    {
        float h = tall ? 1.18f : 1f;
        return new[]
        {
            Points(-0.34f, -0.70f, -0.18f, 0.12f, -0.58f, 0.53f, -0.18f, 0.30f, 0f, 1.50f * h, 0.18f, 0.31f, 0.62f, 0.58f, 0.20f, 0.12f, 0.34f, -0.70f),
            Points(-1.12f, -0.73f, -0.42f, -0.50f, 0f, -0.10f, 0.44f, -0.50f, 1.12f, -0.73f),
            Points(-1.30f, 0.61f, -1.03f, 1.14f, -0.55f, 1.34f, -0.10f, 1.16f, 0.28f, 1.42f, 0.83f, 1.25f, 1.30f, 0.68f),
            Points(-0.98f, 0.77f, -0.62f, 0.62f, -0.16f, 0.86f, 0.28f, 0.70f, 0.76f, 0.91f, 1.08f, 0.72f)
        };
    }

    private static Vector2[] PersonPath(float x, float y, float size)
    {
        Vector2[] head = CirclePath(x, y + 0.72f, size, 10);
        Vector2[] path = new Vector2[head.Length + 6];
        Array.Copy(head, path, head.Length);
        int p = head.Length;
        path[p++] = new Vector2(x, y + 0.39f);
        path[p++] = new Vector2(x - 0.48f, y + 0.08f);
        path[p++] = new Vector2(x, y + 0.30f);
        path[p++] = new Vector2(x + 0.48f, y + 0.08f);
        path[p++] = new Vector2(x, y + 0.30f);
        path[p] = new Vector2(x, y - 0.62f);
        return path;
    }

    private static Vector2[] CirclePath(float x, float y, float radius, int segments)
    {
        Vector2[] points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            points[i] = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
        }
        return points;
    }

    private static Vector2[] Points(params float[] values)
    {
        Vector2[] points = new Vector2[values.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
        return points;
    }

    private static List<Vector2> SamplePaths(Vector2[][] paths, int count)
    {
        List<Vector2> result = new List<Vector2>(count);
        if (count <= 0) return result;

        List<float> segmentLengths = new List<float>();
        List<Vector2> starts = new List<Vector2>();
        List<Vector2> ends = new List<Vector2>();
        float total = 0f;
        for (int p = 0; p < paths.Length; p++)
        {
            for (int i = 0; i < paths[p].Length - 1; i++)
            {
                float length = Vector2.Distance(paths[p][i], paths[p][i + 1]);
                if (length <= 0.0001f) continue;
                starts.Add(paths[p][i]);
                ends.Add(paths[p][i + 1]);
                segmentLengths.Add(length);
                total += length;
            }
        }

        if (total <= 0.0001f)
        {
            for (int i = 0; i < count; i++) result.Add(Vector2.zero);
            return result;
        }

        for (int sample = 0; sample < count; sample++)
        {
            float distance = ((sample + 0.5f) / count) * total;
            float walked = 0f;
            Vector2 point = ends[ends.Count - 1];
            for (int segment = 0; segment < segmentLengths.Count; segment++)
            {
                float length = segmentLengths[segment];
                if (walked + length >= distance)
                {
                    point = Vector2.Lerp(starts[segment], ends[segment], (distance - walked) / length);
                    break;
                }
                walked += length;
            }
            result.Add(point);
        }
        return result;
    }

    /// <summary>Startet im Play Mode gezielt einen Akt; hilfreich fuer visuelle Abnahmen.</summary>
    public void DebugTriggerChapter(int index)
    {
        if (state == ShowState.Idle) BeginChapter(Mathf.Clamp(index, 0, 3));
    }

    /// <summary>Markiert die vier Kapitel als gesehen und startet direkt die Kronensynthese.</summary>
    public void DebugTriggerFinale()
    {
        if (state != ShowState.Idle) return;
        for (int i = 0; i < chapterCompleted.Length; i++) chapterCompleted[i] = true;
        nextChapter = 4;
        BeginFinale();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < chapterSources.Length; i++)
            if (chapterSources[i] != null) chapterSources[i].Stop();
        if (finaleSource != null) finaleSource.Stop();
        if (ascensionController != null) ascensionController.MemoryDuckMultiplier = 1f;
        RestoreEverything();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        for (int i = 0; i < generatedClips.Count; i++)
            if (generatedClips[i] != null) Destroy(generatedClips[i]);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
    }
}
