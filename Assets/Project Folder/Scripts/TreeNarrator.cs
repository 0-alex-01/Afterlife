using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Der Erzähler für die Tree_Scene (Foddy-Stil, siehe Audio/NARRATOR_LINES_EBENE2.md).
/// Spielt jede Line genau einmal und unterbricht sich nie selbst — alle Lines laufen
/// über eine FIFO-Warteschlange. Beobachtet nur vorhandene Systeme, ohne sie zu ändern:
///   1. Willkommen beim Szenenstart,
///   2. Kletter-Anweisung kurz darauf,
///   3. Sturz-Line beim ersten deutlichen Höhenverlust,
///   4. Wärme-Line ab halber Aufstiegshöhe (AfterlifeAscensionController),
///   5. Portal-Line, sobald TreePortalUnlock das Portal erweckt.
/// Höhe wird über den AscensionController gemessen (dessen player + bottom/topHeight),
/// mit Fallback auf Camera.main.
/// </summary>
public class TreeNarrator : MonoBehaviour
{
    [Header("Clips (Reihenfolge der Reise)")]
    [Tooltip("\"Welcome back. Or welcome… away?…\" — Szenenstart.")]
    public AudioClip welcomeLine;
    [Tooltip("\"Grab a hold. Pull yourself up…\" — Kletter-Anweisung.")]
    public AudioClip instructionLine;
    [Tooltip("\"Falling doesn't hurt anymore…\" — erster Sturz.")]
    public AudioClip fallLine;
    [Tooltip("\"Do you feel that? It's getting warmer…\" — halbe Höhe.")]
    public AudioClip warmthLine;
    [Tooltip("\"There. It's awake…\" — Portal erwacht.")]
    public AudioClip portalLine;

    [Header("Timing")]
    [Tooltip("Sekunden Ruhe nach Szenenstart, bevor die Willkommens-Line spielt.")]
    public float welcomeDelay = 3f;
    [Tooltip("Sekunden Stille NACH dem Ende der Willkommens-Line, bevor die Kletter-Anweisung spielt.")]
    public float instructionDelay = 6f;
    [Tooltip("Höhenverlust (Meter) gegenüber dem bisherigen Höchststand, der als Sturz gilt.")]
    public float fallDrop = 4f;
    [Tooltip("Ab welchem Aufstiegsanteil (0–1) die Wärme-Line spielt.")]
    [Range(0f, 1f)] public float warmthThreshold = 0.5f;
    [Tooltip("Mindestpause zwischen zwei Lines.")]
    public float minGapBetweenLines = 1f;

    [Header("Referenzen (leer = automatisch gesucht)")]
    [Tooltip("AudioSource für die Stimme (2D). Leer = wird auf diesem Objekt erzeugt.")]
    public AudioSource source;
    public AfterlifeAscensionController ascension;
    public TreePortalUnlock portal;
    [Tooltip("Spielerkopf für die Höhenmessung. Leer = vom AscensionController oder Camera.main.")]
    public Transform playerHead;

    private readonly Queue<AudioClip> _queue = new Queue<AudioClip>();
    private bool _fallPlayed, _warmthPlayed, _portalPlayed;
    private float _peakHeight = float.NegativeInfinity;
    private bool _introStarted;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // Erzählerstimme sitzt immer direkt im Ohr

        if (ascension == null) ascension = FindFirstObjectByType<AfterlifeAscensionController>();
        if (portal == null) portal = FindFirstObjectByType<TreePortalUnlock>();
        ResolvePlayerHead();
    }

    private void ResolvePlayerHead()
    {
        if (playerHead != null) return;
        if (ascension != null && ascension.player != null) { playerHead = ascension.player; return; }
        if (Camera.main != null) playerHead = Camera.main.transform;
    }

    private void Start()
    {
        StartCoroutine(IntroFlow());
        StartCoroutine(DrainQueue());
    }

    private IEnumerator IntroFlow()
    {
        _introStarted = true;
        yield return Wait(welcomeDelay);
        Enqueue(welcomeLine);
        // Stille NACH dem Ende der Willkommens-Line, nicht ab Einreihen.
        yield return WaitForQueueIdle();
        yield return Wait(instructionDelay);
        Enqueue(instructionLine);
    }

    private void Update()
    {
        if (!_introStarted) return;
        ResolvePlayerHead();
        if (playerHead == null) return;

        float y = playerHead.position.y;

        // Sturz: deutlicher Verlust gegenüber dem bisherigen Höchststand.
        if (y > _peakHeight) _peakHeight = y;
        if (!_fallPlayed && _peakHeight - y >= fallDrop)
        {
            _fallPlayed = true;
            Enqueue(fallLine);
        }

        // Wärme ab halber Höhe.
        if (!_warmthPlayed && NormalizedHeight(y) >= warmthThreshold)
        {
            _warmthPlayed = true;
            Enqueue(warmthLine);
        }

        // Portal erwacht.
        if (!_portalPlayed && portal != null && portal.IsUnlocked)
        {
            _portalPlayed = true;
            Enqueue(portalLine);
        }
    }

    private float NormalizedHeight(float y)
    {
        if (ascension == null) return 0f;
        float range = Mathf.Max(0.001f, ascension.topHeight - ascension.bottomHeight);
        return Mathf.Clamp01((y - ascension.bottomHeight) / range);
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
