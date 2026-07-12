using System.Collections.Generic;
using UnityEngine;

public class ContinuousRandomGlitchColorsOptimized : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform[] targetRoots;
    [SerializeField] private Renderer[] extraRenderers;
    [SerializeField] private bool collectRenderersOnStart = true;
    [SerializeField] private bool onlyAffectVisibleRenderers = false;

    [Header("Glitch Colors")]
    [SerializeField] private Color[] glitchColors =
    {
        Color.magenta,
        Color.cyan,
        Color.green,
        Color.red,
        Color.blue,
        Color.yellow,
        new Color(1f, 0.3f, 0f),
        new Color(0.4f, 0f, 1f)
    };

    [Header("Continuous Glitch Behaviour")]
    [Range(0f, 1f)]
    [SerializeField] private float activeGlitchChancePerObject = 0.25f;

    [SerializeField] private float colorChangeInterval = 0.08f;
    [SerializeField] private Vector2 individualGlitchDurationRange = new Vector2(0.15f, 0.45f);
    [SerializeField] private Vector2 individualPauseRange = new Vector2(0.05f, 0.35f);

    [Header("Performance")]
    [SerializeField] private float logicTickInterval = 0.04f;
    [SerializeField] private bool limitSimultaneousGlitches = true;
    [SerializeField] private int maxSimultaneousGlitches = 75;

    [Header("Intensity")]
    [SerializeField] private bool affectEmission = true;
    [SerializeField] private float emissionIntensity = 1.5f;
    [SerializeField] private bool restoreOriginalWhenNotGlitching = true;

    [Header("Shader Property Names")]
    [SerializeField] private string baseColorPropertyURP = "_BaseColor";
    [SerializeField] private string baseColorPropertyBuiltIn = "_Color";
    [SerializeField] private string emissionColorProperty = "_EmissionColor";

    private class RendererCache
    {
        public Renderer renderer;
        public Material[] materials;
        public MaterialPropertyBlock[] originalBlocks;
        public bool[] hasBaseColorURP;
        public bool[] hasBaseColorBuiltIn;
        public bool[] hasEmission;
    }

    private readonly List<Renderer> collectedRenderers = new List<Renderer>();
    private RendererCache[] cachedRenderers;

    private bool[] isGlitching;
    private float[] stateTimers;

    private int currentActiveGlitches = 0;

    private float logicTimer;
    private float colorTimer;

    private MaterialPropertyBlock reusableBlock;

    private void Awake()
    {
        reusableBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (collectRenderersOnStart)
        {
            CollectRenderers();
        }

        Initialize();
    }

    private void Update()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            return;

        logicTimer -= Time.deltaTime;
        colorTimer -= Time.deltaTime;

        bool shouldRunLogicTick = logicTimer <= 0f;
        bool shouldChangeColors = colorTimer <= 0f;

        if (!shouldRunLogicTick && !shouldChangeColors)
            return;

        if (shouldRunLogicTick)
            logicTimer = logicTickInterval;

        if (shouldChangeColors)
            colorTimer = colorChangeInterval;

        float tickDelta = Mathf.Max(logicTickInterval, Time.deltaTime);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            RendererCache cache = cachedRenderers[i];

            if (cache == null || cache.renderer == null)
                continue;

            if (!cache.renderer.gameObject.activeInHierarchy)
                continue;

            if (onlyAffectVisibleRenderers && !cache.renderer.isVisible)
                continue;

            if (shouldRunLogicTick)
            {
                stateTimers[i] -= tickDelta;

                if (stateTimers[i] <= 0f)
                {
                    DecideNextState(i);
                }
            }

            if (isGlitching[i] && shouldChangeColors)
            {
                ApplyRandomGlitchColor(cache);
            }
        }
    }

    private void CollectRenderers()
    {
        collectedRenderers.Clear();

        HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();

        if (targetRoots != null)
        {
            foreach (Transform root in targetRoots)
            {
                if (root == null)
                    continue;

                Renderer[] foundRenderers = root.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in foundRenderers)
                {
                    if (renderer != null && uniqueRenderers.Add(renderer))
                    {
                        collectedRenderers.Add(renderer);
                    }
                }
            }
        }

        if (extraRenderers != null)
        {
            foreach (Renderer renderer in extraRenderers)
            {
                if (renderer != null && uniqueRenderers.Add(renderer))
                {
                    collectedRenderers.Add(renderer);
                }
            }
        }

        Debug.Log("Collected glitch renderers: " + collectedRenderers.Count);
    }

    private void Initialize()
    {
        int count = collectedRenderers.Count;

        cachedRenderers = new RendererCache[count];
        isGlitching = new bool[count];
        stateTimers = new float[count];

        currentActiveGlitches = 0;

        for (int i = 0; i < count; i++)
        {
            cachedRenderers[i] = BuildRendererCache(collectedRenderers[i]);

            if (cachedRenderers[i] == null)
            {
                isGlitching[i] = false;
                stateTimers[i] = Random.Range(individualPauseRange.x, individualPauseRange.y);
                continue;
            }

            DecideNextState(i);
        }

        logicTimer = logicTickInterval;
        colorTimer = colorChangeInterval;
    }

    private RendererCache BuildRendererCache(Renderer renderer)
    {
        if (renderer == null)
            return null;

        RendererCache cache = new RendererCache();

        cache.renderer = renderer;
        cache.materials = renderer.sharedMaterials;

        if (cache.materials == null)
            return null;

        int materialCount = cache.materials.Length;

        cache.originalBlocks = new MaterialPropertyBlock[materialCount];
        cache.hasBaseColorURP = new bool[materialCount];
        cache.hasBaseColorBuiltIn = new bool[materialCount];
        cache.hasEmission = new bool[materialCount];

        for (int i = 0; i < materialCount; i++)
        {
            MaterialPropertyBlock originalBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(originalBlock, i);
            cache.originalBlocks[i] = originalBlock;

            Material mat = cache.materials[i];

            if (mat == null)
                continue;

            cache.hasBaseColorURP[i] = mat.HasProperty(baseColorPropertyURP);
            cache.hasBaseColorBuiltIn[i] = mat.HasProperty(baseColorPropertyBuiltIn);
            cache.hasEmission[i] = mat.HasProperty(emissionColorProperty);
        }

        return cache;
    }

    private void DecideNextState(int index)
    {
        if (cachedRenderers == null || index < 0 || index >= cachedRenderers.Length)
            return;

        RendererCache cache = cachedRenderers[index];

        if (cache == null || cache.renderer == null)
        {
            isGlitching[index] = false;
            stateTimers[index] = Random.Range(individualPauseRange.x, individualPauseRange.y);
            return;
        }

        bool wasGlitching = isGlitching[index];

        bool canStartNewGlitch = true;

        if (limitSimultaneousGlitches && !wasGlitching)
        {
            canStartNewGlitch = currentActiveGlitches < maxSimultaneousGlitches;
        }

        bool shouldGlitch = canStartNewGlitch && Random.value < activeGlitchChancePerObject;

        isGlitching[index] = shouldGlitch;

        if (wasGlitching && !shouldGlitch)
        {
            currentActiveGlitches = Mathf.Max(0, currentActiveGlitches - 1);

            if (restoreOriginalWhenNotGlitching)
            {
                RestoreOriginalColor(index);
            }
        }
        else if (!wasGlitching && shouldGlitch)
        {
            currentActiveGlitches++;
        }

        if (shouldGlitch)
        {
            stateTimers[index] = Random.Range(
                individualGlitchDurationRange.x,
                individualGlitchDurationRange.y
            );

            ApplyRandomGlitchColor(cache);
        }
        else
        {
            stateTimers[index] = Random.Range(
                individualPauseRange.x,
                individualPauseRange.y
            );
        }
    }

    private void ApplyRandomGlitchColor(RendererCache cache)
    {
        if (cache == null)
            return;

        if (cache.renderer == null)
            return;

        if (cache.materials == null)
            return;

        if (cache.hasBaseColorURP == null ||
            cache.hasBaseColorBuiltIn == null ||
            cache.hasEmission == null)
            return;

        if (glitchColors == null || glitchColors.Length == 0)
            return;

        if (reusableBlock == null)
            reusableBlock = new MaterialPropertyBlock();

        Color glitchColor = glitchColors[Random.Range(0, glitchColors.Length)];

        int materialCount = cache.materials.Length;

        for (int i = 0; i < materialCount; i++)
        {
            if (i >= cache.hasBaseColorURP.Length ||
                i >= cache.hasBaseColorBuiltIn.Length ||
                i >= cache.hasEmission.Length)
            {
                continue;
            }

            Material mat = cache.materials[i];

            if (mat == null)
                continue;

            reusableBlock.Clear();
            cache.renderer.GetPropertyBlock(reusableBlock, i);

            if (cache.hasBaseColorURP[i])
            {
                reusableBlock.SetColor(baseColorPropertyURP, glitchColor);
            }
            else if (cache.hasBaseColorBuiltIn[i])
            {
                reusableBlock.SetColor(baseColorPropertyBuiltIn, glitchColor);
            }

            if (affectEmission && cache.hasEmission[i])
            {
                reusableBlock.SetColor(emissionColorProperty, glitchColor * emissionIntensity);
            }

            cache.renderer.SetPropertyBlock(reusableBlock, i);
        }
    }

    private void RestoreOriginalColor(int index)
    {
        if (cachedRenderers == null || index < 0 || index >= cachedRenderers.Length)
            return;

        RendererCache cache = cachedRenderers[index];

        if (cache == null || cache.renderer == null)
            return;

        if (cache.originalBlocks == null)
            return;

        for (int i = 0; i < cache.originalBlocks.Length; i++)
        {
            if (cache.originalBlocks[i] != null)
            {
                cache.renderer.SetPropertyBlock(cache.originalBlocks[i], i);
            }
        }
    }

    private void OnDisable()
    {
        if (cachedRenderers == null)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            RestoreOriginalColor(i);
        }

        currentActiveGlitches = 0;
    }
}