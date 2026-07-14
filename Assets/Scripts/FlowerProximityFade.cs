using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fades a flower in as the player approaches and out again when they leave.
/// Attach to a flower root; it instances the child materials (so shared assets
/// stay untouched) and drives their alpha based on the horizontal distance
/// from the player. Runtime-only effect.
/// </summary>
public class FlowerProximityFade : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player head / camera. Auto-resolves to Camera.main if left empty.")]
    public Transform player;

    [Header("Proximity fade (horizontal distance)")]
    [Tooltip("At/beyond this distance the flower is fully invisible.")]
    public float fadeStartDistance = 11f;
    [Tooltip("At/within this distance the flower is fully visible.")]
    public float fadeEndDistance = 4f;

    [Tooltip("Disable the renderers entirely while fully faded out (saves draw calls).")]
    public bool disableWhenHidden = true;

    Renderer[] _renderers;
    Material[] _mats;
    Color[] _baseColors;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        _renderers = GetComponentsInChildren<Renderer>(true);
        var mats = new List<Material>();
        var cols = new List<Color>();
        foreach (var r in _renderers)
        {
            // .materials => runtime instances; source assets remain untouched.
            foreach (var m in r.materials)
            {
                MakeFadeable(m);
                mats.Add(m);
                cols.Add(m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white);
            }
        }
        _mats = mats.ToArray();
        _baseColors = cols.ToArray();

        // Start hidden so the reveal always plays.
        ApplyAlpha(0f);
    }

    void Update()
    {
        Transform p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        if (p == null) return;

        Vector3 flatPlayer = new Vector3(p.position.x, 0f, p.position.z);
        Vector3 flatFlower = new Vector3(transform.position.x, 0f, transform.position.z);
        float dist = Vector3.Distance(flatPlayer, flatFlower);

        // 0 = far (invisible), 1 = near (visible)
        float t = Mathf.Clamp01(Mathf.InverseLerp(fadeStartDistance, fadeEndDistance, dist));
        float eased = t * t * (3f - 2f * t); // smoothstep
        ApplyAlpha(eased);
    }

    void ApplyAlpha(float alpha)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            Color c = _baseColors[i];
            c.a = alpha;
            _mats[i].SetColor(BaseColorId, c);
        }
        if (disableWhenHidden && _renderers != null)
        {
            bool visible = alpha > 0.01f;
            foreach (var r in _renderers)
                if (r != null && r.enabled != visible) r.enabled = visible;
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
