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
    [Tooltip("Sekunden Ruhe nach dem Szenenstart, bevor die erste Line spielt.")]
    public float startDelay = 3f;
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

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        if (atmosphere == null) atmosphere = FindFirstObjectByType<CemeteryAtmosphereManager>();
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
    }

    private void Start()
    {
        StartCoroutine(Flow());
        StartCoroutine(DrainQueue());
    }

    private IEnumerator Flow()
    {
        // 1) Direkt beim Szenenstart (nach kurzer Ruhe fürs Ankommen).
        yield return Wait(startDelay);
        Enqueue(seeThemLine);

        // 2) Anweisung — erst wenn mindestens zwei Trauernde erschienen sind,
        //    danach Stille NACH dem Ende der Sicht-Line.
        yield return new WaitUntil(() => FigureProximityReveal.AppearedCount >= 2);
        yield return WaitForQueueIdle();
        yield return Wait(instructionDelay);
        Enqueue(instructionLine);

        // 3) Lichtspur freigeschaltet.
        yield return new WaitUntil(() => atmosphere != null && atmosphere.PathRevealed);
        yield return Wait(pathLineDelay);
        Enqueue(pathLine);
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
