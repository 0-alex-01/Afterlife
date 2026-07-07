using UnityEngine;

// Sanftes, organisches Pulsieren fuer Geister-/Laternenlichter.
// Ohne Audio-Anbindung (im Gegensatz zu LightHeartbeatPulse) - reines Sinus-
// Flackern mit leichtem Zufallsversatz, damit mehrere Laternen nie synchron pulsieren.
[RequireComponent(typeof(Light))]
public class SpiritLightPulse : MonoBehaviour
{
    [Tooltip("Grund-Intensitaet des Lichts")]
    public float baseIntensity = 1.6f;
    [Tooltip("Pulsstaerke (+/- um die Grund-Intensitaet)")]
    public float pulseAmount = 0.35f;
    [Tooltip("Puls-Geschwindigkeit")]
    public float pulseSpeed = 0.9f;
    [Tooltip("Zweite, schnellere Welle fuer organisches Flackern (0 = aus)")]
    public float shimmerAmount = 0.08f;
    public float shimmerSpeed = 4.7f;

    private Light _light;
    private float _offset;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _offset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float t = Time.time + _offset;
        float pulse = Mathf.Sin(t * pulseSpeed) * pulseAmount;
        float shimmer = Mathf.Sin(t * shimmerSpeed * 1.13f) * shimmerAmount;
        _light.intensity = Mathf.Max(0f, baseIntensity + pulse + shimmer);
    }
}
