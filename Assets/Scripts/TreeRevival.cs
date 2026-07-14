using System;
using UnityEngine;

/// <summary>
/// Drives the "dead tree comes back to life" effect in the CycleRealm.
/// The living tree is overlaid on the dead tree. As the player approaches,
/// the living tree fades/glows in over the dead one, and a stream of energy
/// particles flows from the player toward the tree.
/// Runtime-only effect (no ExecuteAlways) so it never edits shared assets.
/// </summary>
public class TreeRevival : MonoBehaviour
{
    public event Action RevivalCompleted;
    public bool IsFullyRevived { get; private set; }

    [Header("References")]
    [Tooltip("Player head / camera. Auto-resolves to Camera.main if left empty.")]
    public Transform player;
    public Renderer deadTreeRenderer;
    public Renderer livingTreeRenderer;
    public ParticleSystem energyStream;
    public Light treeGlowLight;

    [Header("Proximity mapping (horizontal distance to tree)")]
    [Tooltip("At/beyond this distance the tree is fully dead.")]
    public float fullDeadDistance = 26f;
    [Tooltip("At/within this distance the tree is fully alive.")]
    public float fullAliveDistance = 4f;

    [Header("Revival visuals")]
    [Tooltip("Subtle glow tint added to the revived tree. Keep intensity low so the tree shows its natural color rather than glowing green everywhere.")]
    public Color livingEmissionColor = new Color(0.35f, 0.7f, 0.45f, 1f);
    [Tooltip("How strong the emission glow gets when fully alive. Keep small (~0.3) for a gentle sheen.")]
    public float maxEmissionIntensity = 0.35f;
    public float maxGlowLightIntensity = 2f;

    [Header("Energy stream")]
    public float streamSpeed = 7f;
    public float maxEmissionRate = 35f;

    Material _livingMat;
    Color _livingBaseColor = Color.white;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    void Start()
    {
        if (player == null && Camera.main != null)
            player = Camera.main.transform;

        if (livingTreeRenderer != null)
        {
            // material (not sharedMaterial) => runtime instance, asset stays untouched.
            _livingMat = livingTreeRenderer.material;
            if (_livingMat.HasProperty(BaseColorId))
                _livingBaseColor = _livingMat.GetColor(BaseColorId);
            
            if (_livingMat.shader.name != "TreeRevival/CrownEmission")
            {
                MakeFadeable(_livingMat);
            }
            else
            {
                // For our custom dithered shader, force Opaque/Geometry tag so there are no transparent depth glitches
                _livingMat.SetOverrideTag("RenderType", "Opaque");
                _livingMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
            _livingMat.EnableKeyword("_EMISSION");
            _livingMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }

    void Update()
    {
        Transform p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        if (p == null) return;

        Vector3 flatPlayer = new Vector3(p.position.x, 0f, p.position.z);
        Vector3 flatTree = new Vector3(transform.position.x, 0f, transform.position.z);
        float dist = Vector3.Distance(flatPlayer, flatTree);

        // 0 = fully dead (far), 1 = fully alive (near)
        float t = Mathf.Clamp01(Mathf.InverseLerp(fullDeadDistance, fullAliveDistance, dist));
        float eased = t * t * (3f - 2f * t); // smoothstep

        if (!IsFullyRevived && eased >= 0.98f)
        {
            IsFullyRevived = true;
            RevivalCompleted?.Invoke();
        }

        if (_livingMat != null && livingTreeRenderer != null)
        {
            livingTreeRenderer.enabled = eased > 0.01f;
            Color c = _livingBaseColor;
            c.a = eased;
            _livingMat.SetColor(BaseColorId, c);
            if (_livingMat.HasProperty(EmissionColorId))
                _livingMat.SetColor(EmissionColorId, livingEmissionColor * (maxEmissionIntensity * eased));
        }

        if (treeGlowLight != null)
            treeGlowLight.intensity = maxGlowLightIntensity * eased;

        if (energyStream != null)
        {
            // Emit from just in front of / below the player, aimed at the tree crown.
            Vector3 target = transform.position + Vector3.up * (transform.localScale.y * 0.35f);
            energyStream.transform.position = p.position + Vector3.down * 0.35f + (target - p.position).normalized * 0.3f;
            Vector3 dir = target - energyStream.transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                energyStream.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            var main = energyStream.main;
            main.startSpeed = streamSpeed;
            main.startLifetime = Mathf.Clamp(dir.magnitude / Mathf.Max(0.1f, streamSpeed), 0.3f, 6f);

            var emission = energyStream.emission;
            bool inRange = dist < fullDeadDistance + 4f;
            emission.rateOverTime = inRange ? maxEmissionRate * (0.25f + 0.75f * eased) : 0f;
        }
    }

    /// <summary>Configures a URP Lit material for alpha fading while keeping depth writes for a solid look.</summary>
    void MakeFadeable(Material m)
    {
        if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f); // Transparent
        if (m.HasProperty(SrcBlendId)) m.SetFloat(SrcBlendId, (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty(DstBlendId)) m.SetFloat(DstBlendId, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty(ZWriteId)) m.SetFloat(ZWriteId, 1f); // keep depth for cleaner solid silhouette
        m.SetOverrideTag("RenderType", "Transparent");
        m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }
}
