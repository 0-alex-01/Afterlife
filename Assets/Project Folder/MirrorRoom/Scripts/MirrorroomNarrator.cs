using System.Collections;
using UnityEngine;

/// <summary>
/// Der Erzähler für den Mirrorroom (Foddy-Stil, siehe Audio/NARRATOR_LINES_EBENE2.md).
/// Spielt jede Line genau einmal, in Story-Reihenfolge, und unterbricht sich nie selbst.
/// Beobachtet die vorhandenen Systeme, ohne sie zu verändern:
///   1. Tür-Line nach dem Szenenstart,
///   2. See-Reveal, sobald das EntryGate den Spiegel freischaltet (SoulMorphController.enabled),
///   3. Podest-Line, sobald PedestalRiseSequence.Risen,
///   4. Zöger-Line, wenn danach lange nicht gewählt wird,
///   5. Lichtpfad-Line nach dem Commit (LakePathToPortal.Started).
/// Alle Referenzen sind optional und werden sonst automatisch gesucht.
/// </summary>
public class MirrorroomNarrator : MonoBehaviour
{
    [Header("Clips (Reihenfolge der Reise)")]
    [Tooltip("\"A door. You climbed a tree…\" — Szenenstart vor der Tür.")]
    public AudioClip doorLine;
    [Tooltip("\"A lake. A mirror…\" — erster Blick auf den See nach dem Gate.")]
    public AudioClip lakeRevealLine;
    [Tooltip("\"Three pedestals, rising from the lake…\" — Podeste sind aufgestiegen.")]
    public AudioClip pedestalsLine;
    [Tooltip("\"Three pedestals. The void, a new life…\" — wenn lange nicht gewählt wird.")]
    public AudioClip hesitationLine;
    [Tooltip("\"The lake will carry you…\" — der Lichtpfad hat sich entzündet.")]
    public AudioClip lightPathLine;

    [Header("Timing")]
    [Tooltip("Sekunden Ruhe nach Szenenstart, bevor die Tür-Line spielt.")]
    public float doorLineDelay = 2.5f;
    [Tooltip("Sekunden nach dem Gate, bevor die See-Line spielt (Türen sind dann offen).")]
    public float lakeRevealDelay = 3.5f;
    [Tooltip("Sekunden nach dem Podest-Aufstieg, bevor die Podest-Line spielt.")]
    public float pedestalsLineDelay = 1f;
    [Tooltip("Sekunden ohne Wahl (nach dem Aufstieg), bevor die Zöger-Line spielt.")]
    public float hesitationSeconds = 45f;
    [Tooltip("Sekunden nach dem Commit, bevor die Lichtpfad-Line spielt.")]
    public float lightPathDelay = 1.5f;
    [Tooltip("Mindestpause zwischen zwei Lines.")]
    public float minGapBetweenLines = 1f;

    [Header("Referenzen (leer = automatisch gesucht)")]
    [Tooltip("AudioSource für die Stimme (2D). Leer = wird auf diesem Objekt erzeugt.")]
    public AudioSource source;
    public SoulMorphController mirror;
    public PedestalRiseSequence pedestalRise;
    public PedestalChoiceController choice;
    public LakePathToPortal lakePath;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f; // Erzählerstimme sitzt immer direkt im Ohr

        if (mirror == null) mirror = FindFirstObjectByType<SoulMorphController>();
        if (pedestalRise == null) pedestalRise = FindFirstObjectByType<PedestalRiseSequence>();
        if (choice == null) choice = FindFirstObjectByType<PedestalChoiceController>();
        if (lakePath == null) lakePath = FindFirstObjectByType<LakePathToPortal>();
    }

    private void Start()
    {
        StartCoroutine(NarrationFlow());
    }

    private IEnumerator NarrationFlow()
    {
        // 1) Vor der Tür.
        yield return Wait(doorLineDelay);
        yield return Speak(doorLine);

        // 2) Gate durchschritten — das EntryGate schaltet beim Durchschreiten den
        //    Morph-Controller frei; genau daran erkennen wir den Übertritt.
        yield return new WaitUntil(GateCrossed);
        yield return Wait(lakeRevealDelay);
        yield return Speak(lakeRevealLine);

        // 3) Podeste aufgestiegen.
        yield return new WaitUntil(() => pedestalRise == null || pedestalRise.Risen);
        yield return Wait(pedestalsLineDelay);
        yield return Speak(pedestalsLine);

        // 4) Zögern — nur wenn nach dem Aufstieg lange keine Wahl fällt.
        float idle = 0f;
        bool hesitationPlayed = false;
        while (!Committed())
        {
            idle += Time.deltaTime;
            if (!hesitationPlayed && idle >= hesitationSeconds)
            {
                hesitationPlayed = true;
                yield return Speak(hesitationLine);
            }
            yield return null;
        }

        // 5) Lichtpfad nach dem Commit.
        yield return new WaitUntil(() => lakePath == null || lakePath.Started);
        yield return Wait(lightPathDelay);
        yield return Speak(lightPathLine);
    }

    private bool GateCrossed()
    {
        return mirror != null && mirror.enabled;
    }

    private bool Committed()
    {
        return choice != null && choice.Committed;
    }

    /// <summary>Spielt einen Clip, sobald die Stimme frei ist, und wartet ihn ab.</summary>
    private IEnumerator Speak(AudioClip clip)
    {
        if (clip == null || source == null) yield break;
        while (source.isPlaying) yield return null;
        source.clip = clip;
        source.Play();
        while (source.isPlaying) yield return null;
        yield return Wait(minGapBetweenLines);
    }

    private static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(Mathf.Max(0f, seconds));
    }
}
