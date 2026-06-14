using UnityEngine;

public class LightHeartbeatPulse : MonoBehaviour
{
    public AudioSource audioSource;
    public Light targetLight;
    public float minIntensity = 0.2f;
    public float maxIntensity = 1.2f;
    public float sensitivity = 1.5f;
    public int bassBins = 8;
    public float minLevel = 0.001f;
    public float decaySpeed = 5f;

    private float[] spectrum = new float[512];
    private float currentIntensity;

    void Start()
    {
        if (targetLight == null) targetLight = GetComponent<Light>();
        currentIntensity = minIntensity;
    }

    void Update()
    {
        if (audioSource == null || !audioSource.isPlaying || targetLight == null)
        {
            currentIntensity = Mathf.Lerp(currentIntensity, minIntensity, Time.deltaTime * decaySpeed);
            targetLight.intensity = currentIntensity;
            return;
        }

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float bass = 0f;
        int n = Mathf.Clamp(bassBins, 1, spectrum.Length);
        for (int i = 0; i < n; i++)
            bass += spectrum[i];

        if (bass > minLevel)
        {
            float target = Mathf.Clamp(bass * sensitivity * 100f, minIntensity, maxIntensity);
            if (target > currentIntensity)
                currentIntensity = target;
        }

        currentIntensity = Mathf.Lerp(currentIntensity, minIntensity, Time.deltaTime * decaySpeed);
        targetLight.intensity = currentIntensity;
    }
}
