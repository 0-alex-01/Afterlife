using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// VR-friendly fade-to-color overlay + scene loader.
/// On demand, smoothly fades the player's view to a solid color over a duration, then loads a
/// scene by name. Uses a small quad parented to the active VR head camera (positioned ~0.3m in
/// front, unlit transparent URP material, rendered last) with its alpha animated 0->1 via
/// SmoothStep so there is no harsh flash. Comfort-safe for VR.
/// Lazy-creates a DontDestroyOnLoad singleton when first accessed.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    private static ScreenFader _instance;

    /// <summary>
    /// Global access point. Lazily creates a DontDestroyOnLoad instance if none exists.
    /// </summary>
    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ScreenFader (auto)");
                _instance = go.AddComponent<ScreenFader>();
                // Awake handles DontDestroyOnLoad / dedup.
            }
            return _instance;
        }
    }

    private Camera _cachedCamera;
    private Transform _overlayTransform;
    private Renderer _overlayRenderer;
    private Material _overlayMaterial;
    private MaterialPropertyBlock _mpb;
    private bool _fading;

    private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // The fader survives scene changes, but its old camera/overlay does not. The previous
        // implementation left _fading true forever, so every portal after the first transition
        // was silently ignored. Release the transition immediately for the newly loaded scene.
        if (_overlayRenderer != null)
            _overlayRenderer.enabled = false;
        if (_overlayTransform != null)
            Destroy(_overlayTransform.gameObject);
        if (_overlayMaterial != null)
            Destroy(_overlayMaterial);

        _fading = false;
        _cachedCamera = null;
        _overlayTransform = null;
        _overlayRenderer = null;
        _overlayMaterial = null;
        _mpb = null;
    }

    /// <summary>
    /// Fade the view to <paramref name="color"/> over <paramref name="duration"/> seconds, then
    /// load the scene named <paramref name="sceneName"/>. Guards against double-trigger.
    /// </summary>
    public void FadeOutAndLoad(Color color, string sceneName, float duration = 1.5f)
    {
        if (_fading)
        {
            return;
        }
        _fading = true;
        StartCoroutine(FadeRoutine(color, sceneName, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FadeRoutine(Color color, string sceneName, float duration)
    {
        bool haveOverlay = EnsureOverlay(color);

        if (!haveOverlay)
        {
            // No camera / overlay available: still attempt the load after a short delay so the
            // transition is not lost entirely.
            Debug.LogWarning("[ScreenFader] No active camera found for fade overlay. Loading scene after delay without visible fade.");
            yield return new WaitForSeconds(duration);
            LoadSceneSafe(sceneName);
            yield break;
        }

        float t = 0f;
        Color c = color;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            c.a = k;
            ApplyOverlayColor(c);
            yield return null;
        }
        c.a = 1f;
        ApplyOverlayColor(c);

        // Hold one frame fully opaque before swapping scenes to hide the load hitch.
        yield return null;
        LoadSceneSafe(sceneName);
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[ScreenFader] FadeOutAndLoad called with an empty scene name. Nothing loaded.");
            _fading = false;
            return;
        }
        SceneManager.LoadScene(sceneName);
        // HandleSceneLoaded resets the persistent fader for the next portal transition.
    }

    /// <summary>
    /// Ensures the fade overlay quad exists, parented to the head camera. Returns false if no
    /// usable camera could be found.
    /// </summary>
    private bool EnsureOverlay(Color color)
    {
        if (_cachedCamera == null)
        {
            _cachedCamera = FindHeadCamera();
        }
        if (_cachedCamera == null)
        {
            return false;
        }

        if (_overlayTransform == null)
        {
            BuildOverlay();
        }

        // (Re)parent to the current head camera in case the scene changed.
        _overlayTransform.SetParent(_cachedCamera.transform, false);
        _overlayTransform.localPosition = new Vector3(0f, 0f, 0.3f);
        _overlayTransform.localRotation = Quaternion.identity;
        _overlayTransform.localScale = new Vector3(5f, 5f, 5f);

        color.a = 0f;
        ApplyOverlayColor(color);
        if (_overlayRenderer != null)
        {
            _overlayRenderer.enabled = true;
        }
        return true;
    }

    private Camera FindHeadCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
        {
            return cam;
        }
        Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera c in cams)
        {
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
            {
                return c;
            }
        }
        return null;
    }

    private void BuildOverlay()
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "FadeOverlay";
        var col = quad.GetComponent<Collider>();
        if (col != null)
        {
            Destroy(col);
        }

        _overlayTransform = quad.transform;
        _overlayRenderer = quad.GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _overlayMaterial = BuildUnlitTransparentMaterial();
        if (_overlayRenderer != null)
        {
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
        }
    }

    private static Material BuildUnlitTransparentMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        var mat = new Material(shader);

        // Configure URP Unlit as transparent, double-sided, depth-test always so it always covers
        // the view regardless of geometry.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);     // 1 = Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);          // 0 = Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)CompareFunction.Always);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)RenderQueue.Overlay; // render last

        Color start = Color.black;
        start.a = 0f;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", start);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", start);
        return mat;
    }

    private void ApplyOverlayColor(Color c)
    {
        if (_overlayRenderer == null)
        {
            return;
        }
        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }
        _overlayRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(ID_BaseColor, c);
        _overlayRenderer.SetPropertyBlock(_mpb);
    }
}
