using UnityEngine;
using System.Linq;

/// <summary>
/// Debug-Tasten für die Friedhof-Szene (Desktop-Tests ohne VR-Hände):
/// L = nächste noch trauernde Figur erlösen — durchläuft die echte Kette
/// (Dissolve, Grab-Kerze, Manager-Fortschritt). Zählende Figuren zuerst;
/// nach der letzten zählenden startet automatisch die Lichtspur zum Portal.
/// </summary>
public class CemeteryDebugHotkeys : MonoBehaviour
{
    [Tooltip("Erlöst die nächste noch nicht getröstete Figur (zählende zuerst).")]
    public KeyCode comfortKey = KeyCode.L;

    private void Update()
    {
        if (Input.GetKeyDown(comfortKey))
            ComfortNextFigure();
    }

    /// <summary>Nächste Figur erlösen (auch von Tests direkt aufrufbar).</summary>
    public void ComfortNextFigure()
    {
        var next = FindObjectsOfType<CemeteryFigure>()
            .Where(f => !f.IsComforted)
            .OrderByDescending(f => f.countsTowardProgression) // zählende zuerst
            .FirstOrDefault();

        if (next != null)
        {
            Debug.Log($"[Debug] Erlöse {next.name} (zählt={next.countsTowardProgression})");
            next.ForceComfort();
        }
        else
        {
            Debug.Log("[Debug] Alle Figuren bereits erlöst.");
        }
    }
}
