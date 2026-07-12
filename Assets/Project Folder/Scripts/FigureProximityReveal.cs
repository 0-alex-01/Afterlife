using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Lässt die Trauernden-Figuren im Spiel nach und nach erscheinen
/// (Materialisierung rückwärts über den AshFigure-Dissolve-Shader), sobald der
/// Spieler sich dem HAUPTGRAB (CentralGrave) nähert. Die Figuren tauchen dann
/// NACHEINANDER auf — von der dem Grab nächsten bis zur entferntesten — mit
/// einem einstellbaren Zeitversatz. Es zählt immer die gerade aktive Kamera
/// (VR-Rig, Desktop- oder Noclip-Kamera). Im Editor / in der Scene View bleibt
/// die Figur ganz normal sichtbar (Skript läuft nur im Play Mode).
/// </summary>
public class FigureProximityReveal : MonoBehaviour
{
    [Tooltip("Ab dieser Entfernung (Meter, horizontal) zum Hauptgrab startet die Erscheinungs-Sequenz.")]
    public float revealDistance = 10f;
    [Tooltip("Dauer der Materialisierung einer einzelnen Figur in Sekunden.")]
    public float fadeDuration = 2.5f;
    [Tooltip("Zeitversatz (Sekunden) zwischen dem Erscheinen aufeinanderfolgender Figuren.")]
    public float stagger = 1.4f;
    [Tooltip("Shader-Property des AshFigure-Dissolve (1 = unsichtbar, 0 = voll da).")]
    public string dissolveProperty = "_DissolveAmount";
    [Tooltip("Bezugspunkt der Sequenz: das Hauptgrab. Leer = automatisch 'CentralGrave'.")]
    public Transform mainGrave;

    // Gemeinsame Sequenz-Steuerung über alle Figuren hinweg.
    private static readonly List<FigureProximityReveal> _all = new List<FigureProximityReveal>();
    private static bool _sequenceStarted;
    private static float _sequenceStartTime;

    private Material _mat;
    private float _t = 1f;   // 1 = aufgelöst/unsichtbar, 0 = voll sichtbar
    private bool _revealing;
    private bool _done;
    private float _myDelay;  // wann diese Figur (nach Sequenzstart) zu erscheinen beginnt
    private CemeteryFigure _figure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
        _sequenceStarted = false;
        _sequenceStartTime = 0f;
    }

    /// <summary>Wie viele Figuren bereits (weitgehend) materialisiert sind —
    /// für Systeme wie den Narrator, die auf das Erscheinen warten.</summary>
    public static int AppearedCount
    {
        get
        {
            int n = 0;
            foreach (var f in _all)
                if (f != null && (f._done || (f._revealing && f._t <= 0.4f))) n++;
            return n;
        }
    }

    private void Start()
    {
        _figure = GetComponent<CemeteryFigure>();
        var rend = GetComponentInChildren<Renderer>();
        if (rend == null) { enabled = false; return; }

        // Dieselbe Material-Instanz wie CemeteryFigure/MemoryFragmentDissolve
        // (.material liefert die bereits erzeugte Instanz zurück).
        _mat = rend.material;
        if (!_mat.HasProperty(dissolveProperty)) { enabled = false; return; }

        // In Start statt Awake: MemoryFragmentDissolve setzt in Awake 0 —
        // danach unsichtbar starten, noch vor dem ersten gerenderten Frame.
        _mat.SetFloat(dissolveProperty, 1f);

        if (mainGrave == null)
        {
            var g = GameObject.Find("CentralGrave");
            if (g != null) mainGrave = g.transform;
        }

        _all.Add(this);
    }

    private void OnDestroy() => _all.Remove(this);

    private void Update()
    {
        if (_done || _mat == null) return;

        // Getröstete Figuren gehören dem Zerfalls-Effekt — nicht mehr anfassen.
        if (_figure != null && _figure.IsComforted) { _done = true; return; }

        // Warten, bis der Spieler das Hauptgrab erreicht und die Sequenz gestartet ist.
        if (!_sequenceStarted)
        {
            TryStartSequence();
            if (!_sequenceStarted) return;
        }

        // Vor dem eigenen Slot in der Reihenfolge noch nicht erscheinen.
        if (!_revealing)
        {
            if (Time.time - _sequenceStartTime < _myDelay) return;
            _revealing = true;
        }

        _t = Mathf.MoveTowards(_t, 0f, Time.deltaTime / Mathf.Max(0.01f, fadeDuration));
        _mat.SetFloat(dissolveProperty, _t);
        if (_t <= 0f) _done = true;
    }

    /// <summary>Prüft, ob der Spieler nah genug am Hauptgrab ist, und startet
    /// dann die gemeinsame, gestaffelte Erscheinungs-Sequenz.</summary>
    private void TryStartSequence()
    {
        var cam = ActiveCamera();
        if (cam == null) return;

        Vector3 origin = mainGrave != null ? mainGrave.position : transform.position;
        var d = cam.transform.position - origin;
        d.y = 0f;
        if (d.sqrMagnitude > revealDistance * revealDistance) return;

        BeginSequence();
    }

    /// <summary>Legt die Erscheinungs-Reihenfolge fest (dem Hauptgrab nächste
    /// Figur zuerst) und weist jeder Figur ihren Zeit-Slot zu.</summary>
    private static void BeginSequence()
    {
        _sequenceStarted = true;
        _sequenceStartTime = Time.time;

        Vector3 grave = Vector3.zero;
        bool hasGrave = false;
        foreach (var f in _all)
            if (f.mainGrave != null) { grave = f.mainGrave.position; hasGrave = true; break; }

        var ordered = new List<FigureProximityReveal>(_all);
        if (hasGrave)
        {
            Vector3 g = grave;
            ordered.Sort((a, b) =>
                (a.transform.position - g).sqrMagnitude
                .CompareTo((b.transform.position - g).sqrMagnitude));
        }

        for (int i = 0; i < ordered.Count; i++)
            ordered[i]._myDelay = i * Mathf.Max(0f, ordered[i].stagger);
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
