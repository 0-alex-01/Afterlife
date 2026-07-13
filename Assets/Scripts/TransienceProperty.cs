using UnityEngine;
using UnityEngine.VFX;

public class TransienceProperty : MonoBehaviour
{
    [Header("Transience Settings")]
    [Range(0, 1)]
    [Tooltip("Stabilität des Portals. 1 ist voll funktionsfähig, 0 ist fast verschwunden.")]
    public float stability = 1.0f;

    [Tooltip("Wie stark das Portal flackert.")]
    public float instability = 0.5f;

    private VisualEffect vfx;
    private float baseSpawnRate;
    private Light portalLight;
    private float baseLightIntensity;

    void Awake()
    {
        vfx = GetComponentInChildren<VisualEffect>();
        if (vfx != null)
        {
            baseSpawnRate = vfx.GetFloat("Spawn Rate");
        }

        portalLight = GetComponentInChildren<Light>();
        if (portalLight != null)
        {
            baseLightIntensity = portalLight.intensity;
        }
    }

    void Update()
    {
        if (vfx == null) return;

        // "Vergänglichkeits"-Effekt:
        // Das Portal flackert zufällig (instability) und schrumpft/verblasst basierend auf stability.
        
        float flicker = 1.0f - (Random.value * instability * (1.0f - stability + 0.1f));
        float targetRate = baseSpawnRate * stability * flicker;
        
        vfx.SetFloat("Spawn Rate", targetRate);

        if (portalLight != null)
        {
            portalLight.intensity = baseLightIntensity * stability * flicker;
        }

        // Langsames "Atmen" der Partikelgröße
        float pulse = Mathf.Sin(Time.time * 0.5f) * 0.02f;
        vfx.SetFloat("Particle Size", 0.08f + pulse);
    }
}
