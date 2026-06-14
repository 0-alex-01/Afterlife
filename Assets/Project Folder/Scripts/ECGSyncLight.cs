using UnityEngine;

public class ECGSyncLight : MonoBehaviour
{
    public ECGLineUI ecgSource;
    public Light targetLight;
    
    public float pulseIntensity = 1.2f;
    public float baseIntensity = 0.2f;
    public float fadeSpeed = 4f;
    public Color pulseColor = new Color(0.2f, 1f, 0.4f);

    private float currentIntensity;
    private int lastBeatIndex = -1;

    void OnEnable()
    {
        if (ecgSource == null) ecgSource = Object.FindFirstObjectByType<ECGLineUI>();
        if (ecgSource != null) ecgSource.OnBeat += TriggerPulse;
    }

    void OnDisable()
    {
        if (ecgSource != null) ecgSource.OnBeat -= TriggerPulse;
    }

    void Start()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        currentIntensity = baseIntensity;
        if (targetLight != null) targetLight.color = pulseColor;
    }

    void TriggerPulse()
    {
        currentIntensity = pulseIntensity;
    }

    void Update()
    {
        if (targetLight == null) return;

        currentIntensity = Mathf.Lerp(currentIntensity, baseIntensity, Time.deltaTime * fadeSpeed);
        targetLight.intensity = currentIntensity;
    }
}
