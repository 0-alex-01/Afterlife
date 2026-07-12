using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Erzähler für den Friedhof (Foddy-Stil, siehe Audio/NARRATOR_LINES_EBENE2.md).
/// Spielt jede Line genau einmal über eine FIFO-Warteschlange, unterbricht sich nie.
/// Beobachtet nur vorhandene Systeme, ohne sie zu ändern:
///   1. "Do you see them?…" — sobald der Spieler der ersten Trauerfigur nahe kommt,
///   2. "Go to them. Lay your hand on them…" — kurz darauf als Anweisung,
///   3. "Follow the light…" — sobald die Lichtspur freigeschaltet ist
///      (CemeteryAtmosphereManager.PathRevealed).
/// </summary>
public class CemeteryNarrator : MonoBehaviour
{
    [Header("Clips (Reihenfolge)")]
    [Tooltip("\"Do you see them? They're grieving…\" — erste Figur in Sichtweite.")]
    public AudioClip seeThemLine;
    [Tooltip("\"Go to them. Lay your hand on them…\" — Anweisung.")]
    public AudioClip instructionLine;
    [Tooltip("\"Follow the light. It will lead you onward.\" — Lichtspur erscheint.")]
    public AudioClip pathLine;

    [Header("Timing")]
    [Tooltip("Distanz (Meter) zur nächsten Trauerfigur, ab der die erste Line spielt.")]
    public float figureSightDistance = 9f;
    [Tooltip("Sekunden Stille NACH dem Ende der Sicht-Line, bevor die Anweisung spielt.")]
    public float instructionDelay = 6f;
    [Tooltip("Sekunden nach dem Freischalten der Lichtspur, bevor die Pfad-Line spielt.")]
    public float pathLineDelay = 1f;
    [Tooltip("Mindestpause zwischen zwei Lines.")]
    public float minGapBetweenLines = 1f;

    [Header("Referenzen (leer = automatisch gesucht)")]
    [Tooltip("AudioSource für die Stimme (2D). Leer = wird auf diesem Objekt erzeugt.")]
    public AudioSource source;
    public CemeteryAtmosphereManager atmosphere;
    [Tooltip("Spielerkopf für die Distanzmessung. Leer = Camera.main.")]
    public Transform playerHead;

    private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
    private CemeteryFigure[] _figures;
    private bool _seenPlayed, _instructionQueued, _pathPlayed;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        if (atmosphere == null) atmosphere = FindFirstObjectByType<CemeteryAtmosphereManager>();
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
        _figures = FindObjectsByType<CemeteryFigure>(FindObjectsSortMode.None);
    }

    private void Start()
    {
        StartCoroutine(Flow());
        StartCoroutine(DrainQueue());
    }

    private IEnumerator Flow()
    {
        // 1) Erste Figur in Sichtweite.
        yield return new WaitUntil(NearestFigureInRange);
        Enqueue(seeThemLine);

        // 2) Anweisung — mit Stille NACH dem Ende der Sicht-Line, nicht ab Einreihen.
        yield return WaitForQueueIdle();
        yield return Wait(instructionDelay);
        Enqueue(instructionLine);

        // 3) Lichtspur freigeschaltet.
        yield return new WaitUntil(() => atmosphere != null && atmosphere.PathRevealed);
        yield return Wait(pathLineDelay);
        Enqueue(pathLine);
    }

    private bool NearestFigureInRange()
    {
        if (playerHead == null)
        {
            if (Camera.main != null) playerHead = Camera.main.transform;
            if (playerHead == null) return false;
        }
        if (_figures == null || _figures.Length == 0)
        {
            _figures = FindObjectsByType<CemeteryFigure>(FindObjectsSortMode.None);
            if (_figures.Length == 0) return false;
        }
        float sqr = figureSightDistance * figureSightDistance;
        foreach (var f in _figures)
        {
            if (f == null) continue;
            if ((f.transform.position - playerHead.position).sqrMagnitude <= sqr) return true;
        }
        return false;
    }

    private void Enqueue(AudioClip clip)
    {
        if (clip != null) _queue.Enqueue(clip);
    }

    /// <summary>Wartet, bis die Warteschlange leer ist und die Stimme schweigt.</summary>
    private IEnumerator WaitForQueueIdle()
    {
        while (_queue.Count > 0 || (source != null && source.isPlaying))
            yield return null;
    }

    private IEnumerator DrainQueue()
    {
        while (true)
        {
            if (_queue.Count > 0 && source != null && !source.isPlaying)
            {
                source.clip = _queue.Dequeue();
                source.Play();
                while (source.isPlaying) yield return null;
                yield return Wait(minGapBetweenLines);
            }
            else yield return null;
        }
    }

    private static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(Mathf.Max(0f, seconds));
    }
}
