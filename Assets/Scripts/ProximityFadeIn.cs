using UnityEngine;

/// <summary>
/// Fades an object (all its child renderers) in as the player approaches, and back out when far.
/// Used for the flowers around the great tree so they gently bloom into view as the player nears.
/// Runtime-only (no ExecuteAlways) so it never edits shared material assets in edit mode.
/// </summary>
public class ProximityFadeIn : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player head / camera. Auto-resolves to Camera.main if left empty.")]
    public Transform player;

    [Header("Proximity mapping (horizontal distance to this object)")]
    [Tooltip("At/beyond this distance the object is fully invisible.")]
    public float fadeStartDistance = 14f;
    [Tooltip("At/within this distance the object is fully visible.")]
    public float fadeFullDistance = 5f;

    [Header("Fade")]
    [Tooltip("How quickly the alpha follows the target (higher = snappier).")]
    public float fadeSpeed = 3f;

    Renderer[] _renderers;
    Material[] _mats;
    Color[] _baseColors;
    float _alpha;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        _renderers = GetComponentsInChildren<Renderer>();
        _mats = new Material[_renderers.Length];
        _baseColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            // material (not sharedMaterial) => per-instance runtime copy, asset stays untouched.
            var m = _renderers[i].material;
            _mats[i] = m;
            _baseColors[i] = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            MakeFadeable(m);
        }

        _alpha = 0f;
        ApplyAlpha(0f);
    }

    void Update()
    {
        Transform p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        if (p == null || _renderers == null) return;

        Vector3 flatPlayer = new Vector3(p.position.x, 0f, p.position.z);
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        float dist = Vector3.Distance(flatPlayer, flatSelf);

        // 0 = invisible (far), 1 = visible (near)
        float target = Mathf.Clamp01(Mathf.InverseLerp(fadeStartDistance, fadeFullDistance, dist));
        target = target * target * (3f - 2f * target); // smoothstep

        _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);
        ApplyAlpha(_alpha);
    }

    void ApplyAlpha(float a)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].enabled = a > 0.01f;
            if (_mats[i] != null && _mats[i].HasProperty(BaseColorId))
            {
                Color c = _baseColors[i];
                c.a = a;
                _mats[i].SetColor(BaseColorId, c);
            }
        }
    }

    /// <summary>Configures a URP Lit material for alpha fading (keeps depth writes for a solid look).</summary>
    void MakeFadeable(Material m)
    {
        if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f); // Transparent
        if (m.HasProperty(SrcBlendId)) m.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty(DstBlendId)) m.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty(ZWriteId)) m.SetFloat(ZWriteId, 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
