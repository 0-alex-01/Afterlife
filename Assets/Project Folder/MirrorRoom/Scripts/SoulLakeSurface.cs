using UnityEngine;

/// <summary>
/// Verwaltet die dynamischen Effekte des Seelensees (Shader "Custom/SoulLake"):
/// ein Ringpuffer aus Schritt-Wellen (automatisch aus der Bewegung der aktiven
/// Spieler-Kamera erzeugt — VR-Rig, Desktop- oder Noclip-Kamera) und die
/// weichen Licht-Reflexe der treibenden Seelenlichter. Andere Systeme können
/// über <see cref="AddRipple"/> eigene Wellen auslösen (z. B. aufsteigende
/// Podest-Inseln oder ins Wasser tauchende Spiegelscherben).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class SoulLakeSurface : MonoBehaviour
{
    public const int MaxRipples = 16;
    public const int MaxGlows = 24;

    [Header("Schritt-Wellen")]
    [Tooltip("Nach so vielen Metern Fußweg entsteht die nächste Welle.")]
    public float stepDistance = 0.85f;
    [Tooltip("Stärke der Schritt-Wellen (Shader-Multiplikator).")]
    public float stepStrength = 0.8f;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;

    private readonly Vector4[] _ripples = new Vector4[MaxRipples];
    private int _rippleNext;
    private int _rippleCount;

    private readonly Vector4[] _glows = new Vector4[MaxGlows];
    private readonly Vector4[] _glowColors = new Vector4[MaxGlows];
    private int _glowCount;

    private Vector3 _lastCamPos;
    private bool _hasLastCam;
    private float _walked;

    /// <summary>Wasserhöhe (Weltkoordinate Y) für andere Systeme.</summary>
    public float WaterY => transform.position.y;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < MaxRipples; i++)
            _ripples[i] = new Vector4(0f, 0f, -1000f, 0f); // längst abgeklungen
    }

    /// <summary>Löst eine expandierende Welle an einer Weltposition aus.</summary>
    public void AddRipple(Vector3 worldPos, float strength)
    {
        _ripples[_rippleNext] = new Vector4(worldPos.x, worldPos.z, Time.time, strength);
        _rippleNext = (_rippleNext + 1) % MaxRipples;
        _rippleCount = Mathf.Min(_rippleCount + 1, MaxRipples);
    }

    /// <summary>Setzt den Licht-Reflex mit Index <paramref name="index"/> —
    /// die Seelenlichter aktualisieren ihre Reflexe damit jeden Frame.</summary>
    public void SetGlow(int index, Vector3 worldPos, Color color, float radius, float intensity)
    {
        if (index < 0 || index >= MaxGlows) return;
        _glows[index] = new Vector4(worldPos.x, worldPos.z, radius, intensity);
        _glowColors[index] = color;
        if (index >= _glowCount) _glowCount = index + 1;
    }

    private void Update()
    {
        TrackFootsteps();

        _mpb.SetVectorArray("_Ripples", _ripples);
        _mpb.SetFloat("_RippleCount", _rippleCount);
        if (_glowCount > 0)
        {
            _mpb.SetVectorArray("_Glows", _glows);
            _mpb.SetVectorArray("_GlowColors", _glowColors);
        }
        _mpb.SetFloat("_GlowCount", _glowCount);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void TrackFootsteps()
    {
        var cam = ActiveCamera();
        if (cam == null) { _hasLastCam = false; return; }

        Vector3 p = cam.transform.position;
        if (!_hasLastCam)
        {
            _lastCamPos = p;
            _hasLastCam = true;
            return;
        }

        Vector3 delta = p - _lastCamPos;
        delta.y = 0f;
        _lastCamPos = p;

        // Teleport-Sprünge (ScenePortal, Noclip-Warp) erzeugen keine Welle.
        float step = delta.magnitude;
        if (step > 2f) { _walked = 0f; return; }

        _walked += step;
        if (_walked >= stepDistance)
        {
            _walked = 0f;
            var foot = new Vector3(p.x, WaterY, p.z);
            AddRipple(foot, stepStrength);
        }
    }

    /// <summary>Kamera des Spielers — VR-Hauptkamera oder, falls die gerade
    /// deaktiviert ist (Desktop-/Noclip-Modus), die erste aktive Kamera.</summary>
    private static Camera ActiveCamera()
    {
        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled) return main;
        var all = Camera.allCameras;
        return all.Length > 0 ? all[0] : null;
    }
}
