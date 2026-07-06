using UnityEngine;
using System.Collections;
using Autohand;

public class CemeteryFigure : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float requiredTouchDuration = 1.5f;
    public Color glowColor = new Color(0.25f, 0.69f, 0.8f); // Cyan/Teal
    public float glowIntensityMultiplier = 2.0f;

    [Header("References")]
    public MemoryFragmentDissolve dissolveController;
    public ParticleSystem interactionParticles;
    public AudioSource interactionAudio;

    private float currentTouchTime = 0f;
    private bool isBeingTouched = false;
    private bool isComforted = false;
    private Material figureMaterial;
    private float initialGlowAmount = 0f;
    private const string glowProperty = "_GlowAmount";

    private void Awake()
    {
        if (dissolveController == null)
            dissolveController = GetComponent<MemoryFragmentDissolve>();

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            figureMaterial = rend.material;
            if (figureMaterial.HasProperty(glowProperty))
                initialGlowAmount = figureMaterial.GetFloat(glowProperty);
        }
    }

    private void Update()
    {
        if (isComforted) return;

        if (isBeingTouched)
        {
            currentTouchTime += Time.deltaTime;
            UpdateVisuals(currentTouchTime / requiredTouchDuration);

            if (currentTouchTime >= requiredTouchDuration)
            {
                ComfortFigure();
            }
        }
        else if (currentTouchTime > 0)
        {
            currentTouchTime -= Time.deltaTime;
            UpdateVisuals(currentTouchTime / requiredTouchDuration);
        }
    }

    private void UpdateVisuals(float progress)
    {
        if (figureMaterial != null && figureMaterial.HasProperty(glowProperty))
        {
            float targetGlow = initialGlowAmount + (progress * glowIntensityMultiplier);
            figureMaterial.SetFloat(glowProperty, targetGlow);
        }
        
        if (interactionParticles != null)
        {
            var main = interactionParticles.main;
            if (progress > 0.1f && !interactionParticles.isPlaying)
                interactionParticles.Play();
            else if (progress <= 0.1f && interactionParticles.isPlaying)
                interactionParticles.Stop();
        }
    }

    [Header("Release")]
    public Light graveLight;
    public float releaseLightIntensity = 2.2f;
    public Color releaseLightColor = new Color(1f, 0.72f, 0.35f);

    private void ComfortFigure()
    {
        isComforted = true;
        isBeingTouched = false;

        if (interactionAudio != null)
            interactionAudio.Play();

        if (dissolveController != null)
            dissolveController.TriggerDissolve();

        // Leave behind a soft warm light at the grave as a sign of release
        GameObject lightObj = new GameObject("ReleaseLight");
        lightObj.transform.position = transform.position + Vector3.up * 0.6f;
        Light l = lightObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = releaseLightColor;
        l.intensity = releaseLightIntensity;
        l.range = 6f;
        l.shadows = LightShadows.None;

        if (graveLight != null)
        {
            graveLight.enabled = true;
            graveLight.intensity = releaseLightIntensity;
        }

        CemeteryAtmosphereManager.Instance?.OnFigureComforted(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Hand>() != null)
        {
            isBeingTouched = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Hand>() != null)
        {
            isBeingTouched = false;
        }
    }
}
