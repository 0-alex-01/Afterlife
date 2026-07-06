using System.Collections;
using UnityEngine;

public class MemoryFragmentDissolve : MonoBehaviour
{
    [Header("Renderer / Material")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private string dissolveProperty = "_DissolveAmount";

    [Header("Timing")]
    [SerializeField] private float delayBeforeDissolve = 0.5f;
    [SerializeField] private float dissolveDuration = 2.5f;
    public float pulseStrength = 0.0f;
    public float pulseSpeed = 2.0f;

    [Header("Effects")]
    public ParticleSystem dissolveParticles;
    public AudioSource audioSource;
    public float audioFadeSpeed = 1.5f;

    [Header("Scale")]
    [SerializeField] private bool shrinkWhileDissolving = true;
    [SerializeField] private float shrinkMultiplier = 0.85f;

    private Material runtimeMaterial;
    private Vector3 initialScale;
    private bool isDissolving = false;
    private float baseVolume;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.material;
            runtimeMaterial.SetFloat(dissolveProperty, 0f);
        }

        initialScale = transform.localScale;

        if (audioSource != null)
            baseVolume = audioSource.volume;
    }

    private void Update()
    {
        if (!isDissolving)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseStrength;
            transform.localScale = initialScale * pulse;
        }
    }

    public void TriggerDissolve()
    {
        if (!gameObject.activeInHierarchy || isDissolving)
            return;

        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        isDissolving = true;

        yield return new WaitForSeconds(delayBeforeDissolve);

        if (dissolveParticles != null)
            dissolveParticles.Play();

        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = initialScale * shrinkMultiplier;

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);

            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat(dissolveProperty, t);

            if (shrinkWhileDissolving)
                transform.localScale = Vector3.Lerp(startScale, targetScale, t);

            if (audioSource != null)
                audioSource.volume = Mathf.Lerp(baseVolume, 0f, t * audioFadeSpeed);

            yield return null;
        }

        if (runtimeMaterial != null)
            runtimeMaterial.SetFloat(dissolveProperty, 1f);

        if (audioSource != null)
            audioSource.volume = 0f;

        gameObject.SetActive(false);
    }
}