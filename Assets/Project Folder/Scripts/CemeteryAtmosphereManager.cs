using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

public class CemeteryAtmosphereManager : MonoBehaviour
{
    public static CemeteryAtmosphereManager Instance { get; private set; }

    [Header("Progression Settings")]
    [SerializeField] private int figuresToComfort = 5;
    [SerializeField] private float transitionSpeed = 0.5f;

    [Header("Environment References")]
    public Volume globalVolume;
    public Light directionalLight;
    public ParticleSystem spiritMotes;

    private int comfortedCount = 0;
    private List<CemeteryFigure> allFigures = new List<CemeteryFigure>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterFigure(CemeteryFigure figure)
    {
        if (!allFigures.Contains(figure))
            allFigures.Add(figure);
    }

    public void OnFigureComforted(CemeteryFigure figure)
    {
        comfortedCount++;
        Debug.Log($"Figure comforted! {comfortedCount}/{figuresToComfort}");
        
        UpdateAtmosphere();

        if (comfortedCount >= figuresToComfort)
        {
            RevealPath();
        }
    }

    private void UpdateAtmosphere()
    {
        float progress = (float)comfortedCount / figuresToComfort;
        
        // Example: Thinning fog or changing light color
        if (directionalLight != null)
        {
            // Transition from desaturated grey-blue to warmer tone
            Color melancholic = new Color(0.5f, 0.5f, 0.6f);
            Color peaceful = new Color(1f, 0.93f, 0.78f);
            directionalLight.color = Color.Lerp(melancholic, peaceful, progress);
            directionalLight.intensity = Mathf.Lerp(0.5f, 0.8f, progress);
        }

        if (spiritMotes != null)
        {
            var main = spiritMotes.main;
            main.startColor = Color.Lerp(Color.grey, Color.cyan, progress);
        }
    }

    private void RevealPath()
    {
        Debug.Log("Path revealed to next location!");
        // Here we could enable a portal or a path of light
    }
}
