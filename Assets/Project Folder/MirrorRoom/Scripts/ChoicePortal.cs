using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A themed, walkable portal that materializes in front of the mirror after the player commits to a
/// choice. Starts hidden (visual scaled to ~0 + trigger collider disabled). <see cref="Materialize"/>
/// reveals it with a smooth eased grow / brighten-in and then enables its trigger collider so the
/// floating VR player can walk through it. On valid entry it triggers a VR-friendly fade to the
/// theme color and loads the corresponding Level 3 scene via <see cref="ScreenFader"/>.
/// All references are optional and null-checked so a partially-wired prefab still behaves safely.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChoicePortal : MonoBehaviour
{
    [Header("Choice")]
    [Tooltip("Which afterlife theme this portal represents.")]
    public ChoiceTheme theme = ChoiceTheme.None;

    [Tooltip("Name of the Level 3 scene to load when the player walks through (must be in Build Settings).")]
    public string targetSceneName = "";

    [Tooltip("Color the screen fades to as this portal loads its scene.")]
    public Color fadeColor = Color.black;

    [Tooltip("Seconds the fade-to-color takes before the scene loads.")]
    public float fadeDuration = 1.5f;

    [Header("References (all optional / null-safe)")]
    [Tooltip("The visual root that is scaled 0->1 when the portal materializes. If null, this transform is used.")]
    public Transform visualRoot;

    [Tooltip("Optional particle systems played on materialize.")]
    public ParticleSystem[] particles;

    [Tooltip("Optional portal light brightened from 0 to its authored intensity on materialize.")]
    public Light portalLight;

    [Tooltip("Optional extra lights (e.g. the second half of the Judgement doorway) brightened from 0 to their authored intensity on materialize alongside portalLight.")]
    public Light[] additionalLights;

    [Header("Materialize Tuning")]
    [Tooltip("Seconds the materialize grow/brighten-in takes.")]
    public float materializeDuration = 1.5f;

    [Tooltip("EaseOutBack overshoot amount (0 = none, higher = more bounce).")]
    public float easeBackOvershoot = 1.2f;

    [Header("Pulse (optional, e.g. Rebirth)")]
    [Tooltip("If true, the light gently pulses via a sine wave after materializing (VR-comfort: subtle, no strobe).")]
    public bool pulseLight = false;

    [Tooltip("Pulse amplitude as a fraction of the target light intensity.")]
    [Range(0f, 0.6f)] public float pulseAmount = 0.18f;

    [Tooltip("Pulse period in seconds (slow = comfortable).")]
    public float pulsePeriod = 4f;

    // --- Runtime state ---
    private Collider _trigger;
    private float _lightTargetIntensity;
    private float[] _additionalLightTargets;
    private bool _materialized;
    private bool _entered;
    private Coroutine _materializeRoutine;
    private Renderer _innerSurface;
    private Vector3 _innerSurfaceTargetScale = Vector3.one;
    private Camera _headCamera;
    private readonly List<Renderer> _choiceRenderers = new List<Renderer>();
    private readonly List<Renderer> _frameRenderers = new List<Renderer>();

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        if (_trigger != null)
        {
            _trigger.isTrigger = true;
            _trigger.enabled = false; // cannot be entered until materialized
        }

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            string n = renderer.gameObject.name;
            bool isChoiceVisual = n == "Portal_Curtain" || n == "VoidTear" ||
                                  n == "RebirthRing" || n == "TopGold" ||
                                  n == "BottomRed" || n == "Seam";
            if (isChoiceVisual)
                _choiceRenderers.Add(renderer);
            else if (!(renderer is ParticleSystemRenderer))
                _frameRenderers.Add(renderer);

            if (n == "Portal_Curtain")
            {
                _innerSurface = renderer;
                _innerSurfaceTargetScale = renderer.transform.localScale;
            }
        }
        SetChoiceVisuals(false);

        // Capture authored light intensity as the materialize target, then start dark.
        if (portalLight != null)
        {
            _lightTargetIntensity = portalLight.intensity;
            portalLight.intensity = 0f;
        }

        if (additionalLights != null)
        {
            _additionalLightTargets = new float[additionalLights.Length];
            for (int i = 0; i < additionalLights.Length; i++)
            {
                if (additionalLights[i] != null)
                {
                    _additionalLightTargets[i] = additionalLights[i].intensity;
                    additionalLights[i].intensity = 0f;
                }
            }
        }

        // Start hidden (scaled to ~0). Using scale keeps particle/light components alive but invisible.
        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.zero;
        }

        // Make sure any particle systems are not emitting yet.
        if (particles != null)
        {
            foreach (var ps in particles)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }

    private void Update()
    {
        // In a standalone OpenXR build the tracked head often has no physics collider and the
        // rig root is not necessarily named AutoHandPlayer. Polling the active head camera
        // against the portal bounds makes walking into the portal reliable in both cases.
        if (_materialized && !_entered && IsHeadInsideTrigger())
        {
            EnterPortal();
        }

        if (_materialized && pulseLight && portalLight != null && pulsePeriod > 0.01f)
        {
            float s = Mathf.Sin(Time.time * (Mathf.PI * 2f / pulsePeriod));
            portalLight.intensity = _lightTargetIntensity * (1f + s * pulseAmount);
        }
    }

    private void LateUpdate()
    {
        if (_materialized) SetChoiceVisuals(true);
    }

    /// <summary>
    /// Smoothly reveals the portal (scale 0->1 with a gentle EaseOutBack, light 0->target, particles
    /// play) and then enables the trigger collider so it can be walked through. Idempotent.
    /// </summary>
    public void Materialize()
    {
        if (_materialized)
        {
            return;
        }
        _materialized = true;
        ShowFrame();
        SetChoiceVisuals(true);
        if (_innerSurface != null)
        {
            _innerSurface.enabled = true;
            _innerSurface.transform.localScale = _innerSurfaceTargetScale;
        }

        if (particles != null)
        {
            foreach (var ps in particles)
            {
                if (ps != null)
                {
                    ps.Play();
                }
            }
        }

        if (_materializeRoutine != null)
        {
            StopCoroutine(_materializeRoutine);
        }
        _materializeRoutine = StartCoroutine(MaterializeRoutine());
    }

    private IEnumerator MaterializeRoutine()
    {
        float duration = Mathf.Max(0.01f, materializeDuration);
        float t = 0f;
        Transform vr = visualRoot != null ? visualRoot : transform;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = EaseOutBack(k, easeBackOvershoot);

            vr.localScale = Vector3.one;
            float lightK = Mathf.SmoothStep(0f, 1f, k);
            if (portalLight != null)
            {
                // Brighten without the overshoot so the light does not flash past target.
                portalLight.intensity = _lightTargetIntensity * lightK;
            }
            ApplyAdditionalLights(lightK);
            yield return null;
        }

        vr.localScale = Vector3.one;
        SetChoiceVisuals(true);
        if (_innerSurface != null)
            _innerSurface.transform.localScale = _innerSurfaceTargetScale;
        if (portalLight != null)
        {
            portalLight.intensity = _lightTargetIntensity;
        }
        ApplyAdditionalLights(1f);

        // Now walkable.
        if (_trigger != null)
        {
            _trigger.enabled = true;
        }
        _materializeRoutine = null;
    }

    private void ApplyAdditionalLights(float k)
    {
        if (additionalLights == null || _additionalLightTargets == null)
        {
            return;
        }
        for (int i = 0; i < additionalLights.Length; i++)
        {
            if (additionalLights[i] != null && i < _additionalLightTargets.Length)
            {
                additionalLights[i].intensity = _additionalLightTargets[i] * k;
            }
        }
    }

    /// <summary>
    /// Optional reverse of <see cref="Materialize"/> (used if a choice is reset). Minimal: hides the
    /// visual, dims the light, stops particles, and disables the trigger.
    /// </summary>
    public void Dematerialize()
    {
        _materialized = false;
        _entered = false;

        if (_materializeRoutine != null)
        {
            StopCoroutine(_materializeRoutine);
            _materializeRoutine = null;
        }

        Transform vr = visualRoot != null ? visualRoot : transform;
        vr.localScale = Vector3.one;
        SetChoiceVisuals(false);
        if (_innerSurface != null)
        {
            _innerSurface.enabled = false;
            _innerSurface.transform.localScale = _innerSurfaceTargetScale;
        }

        if (portalLight != null)
        {
            portalLight.intensity = 0f;
        }
        ApplyAdditionalLights(0f);
        if (particles != null)
        {
            foreach (var ps in particles)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
        if (_trigger != null)
        {
            _trigger.enabled = false;
        }
    }

    /// <summary>Shows the outer frame while keeping the themed oval inactive.</summary>
    public void ShowFrame()
    {
        gameObject.SetActive(true);
        Transform vr = visualRoot != null ? visualRoot : transform;
        vr.gameObject.SetActive(true);
        vr.localScale = Vector3.one;
        foreach (var renderer in _frameRenderers)
        {
            if (renderer == null) continue;
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
        }
        if (!_materialized) SetChoiceVisuals(false);
        if (_trigger != null && !_materialized) _trigger.enabled = false;
    }

    private void SetChoiceVisuals(bool visible)
    {
        foreach (var renderer in _choiceRenderers)
        {
            if (renderer == null) continue;
            if (visible) renderer.gameObject.SetActive(true);
            renderer.enabled = visible;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_materialized || _entered)
        {
            return;
        }
        if (!IsPlayer(other))
        {
            return;
        }

        EnterPortal();
    }

    private bool IsHeadInsideTrigger()
    {
        if (_trigger == null || !_trigger.enabled)
        {
            return false;
        }

        if (_headCamera == null || !_headCamera.isActiveAndEnabled)
        {
            _headCamera = Camera.main;
            if (_headCamera == null || !_headCamera.isActiveAndEnabled)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                    {
                        _headCamera = cameras[i];
                        break;
                    }
                }
            }
        }

        return _headCamera != null && _trigger.bounds.Contains(_headCamera.transform.position);
    }

    private void EnterPortal()
    {
        if (_entered)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(targetSceneName) ||
            !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"[ChoicePortal] Scene '{targetSceneName}' is not enabled in Build Settings.", this);
            return;
        }

        _entered = true; // debounce: fire only once
        ScreenFader.Instance.FadeOutAndLoad(fadeColor, targetSceneName, fadeDuration);
    }

    /// <summary>
    /// Robust detection of the flying-soul VR player (AutoHand). Checks, in order: a component
    /// named "AutoHandPlayer" anywhere up the parent chain; the transform root name containing
    /// "AutoHandPlayer"; or an attached Rigidbody whose root name suggests the player rig.
    /// </summary>
    private static bool IsPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        Transform root = other.transform.root;
        if (root != null &&
            (root.name.Contains("AutoHandPlayer") || root.name.Contains("XR Origin") ||
             root.name.Contains("XR Rig")))
        {
            return true;
        }

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            return true;
        }

        Camera head = Camera.main;
        if (head != null && other.transform.root == head.transform.root)
        {
            return true;
        }

        // Walk up parents looking for a component type named "AutoHandPlayer" (avoids a hard
        // assembly reference to the Autohand package).
        Transform t = other.transform;
        while (t != null)
        {
            var comps = t.GetComponents<MonoBehaviour>();
            for (int i = 0; i < comps.Length; i++)
            {
                var c = comps[i];
                if (c != null && c.GetType().Name == "AutoHandPlayer")
                {
                    return true;
                }
            }
            t = t.parent;
        }

        // Last-resort fallback: a rigidbody-driven body whose root is named like the player rig.
        var rb = other.attachedRigidbody;
        if (rb != null && rb.transform.root != null && rb.transform.root.name.Contains("AutoHandPlayer"))
        {
            return true;
        }

        return false;
    }

    /// <summary>EaseOutBack easing for a gentle overshoot on the grow-in.</summary>
    private static float EaseOutBack(float x, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot);
        float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
