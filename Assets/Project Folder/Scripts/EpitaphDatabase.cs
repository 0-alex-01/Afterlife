using UnityEngine;

/// <summary>
/// Datenbank mit epischen Grabinschriften (1–2 Sätze fiktive Biografien).
/// Grabsteine ziehen daraus per <see cref="GravestoneEpitaph"/> zufällig
/// einen Eintrag. Als Asset im Projekt editierbar — einfach Zeilen ergänzen.
/// </summary>
[CreateAssetMenu(fileName = "EpitaphDatabase", menuName = "Afterlife/Epitaph Database")]
public class EpitaphDatabase : ScriptableObject
{
    [TextArea(2, 4)]
    public string[] epitaphs;

    /// <summary>Zufälliger Eintrag; mit Seed deterministisch (gleicher Stein = gleiche Inschrift).</summary>
    public string GetEpitaph(int seed)
    {
        if (epitaphs == null || epitaphs.Length == 0) return string.Empty;
        var rng = new System.Random(seed);
        return epitaphs[rng.Next(epitaphs.Length)];
    }

    public string GetRandomEpitaph()
    {
        if (epitaphs == null || epitaphs.Length == 0) return string.Empty;
        return epitaphs[Random.Range(0, epitaphs.Length)];
    }
}
