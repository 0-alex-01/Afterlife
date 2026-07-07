using UnityEngine;

/// <summary>
/// Einfaches, immer sichtbares Szenen-Portal: Betritt der Spieler den Trigger,
/// blendet der ScreenFader sanft aus und lädt die Zielszene.
/// Mit <see cref="locked"/> kann es gesperrt starten (z. B. bis der Friedhofs-Pfad
/// freigeschaltet ist) und später per <see cref="Unlock"/> geöffnet werden.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Header("Ziel")]
    [Tooltip("Name der Zielszene (muss in den Build Settings stehen).")]
    public string targetSceneName = "";

    [Tooltip("Farbe, zu der beim Übergang ausgeblendet wird.")]
    public Color fadeColor = Color.black;

    [Tooltip("Dauer des Ausblendens in Sekunden.")]
    public float fadeDuration = 1.5f;

    [Header("Zustand")]
    [Tooltip("Gesperrt = Betreten löst nichts aus (bis Unlock() aufgerufen wird).")]
    public bool locked = false;

    private bool _entered;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    /// <summary>Portal freischalten (z. B. vom CemeteryAtmosphereManager nach der letzten Erlösung).</summary>
    public void Unlock() => locked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (locked || _entered) return;
        if (!IsPlayer(other)) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("ScenePortal ohne Zielszene!", this);
            return;
        }

        _entered = true;
        ScreenFader.Instance.FadeOutAndLoad(fadeColor, targetSceneName, fadeDuration);
    }

    // Gleiche robuste Spieler-Erkennung wie ChoicePortal (AutoHand-Rig).
    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;

        Transform root = other.transform.root;
        if (root != null && root.name.Contains("AutoHandPlayer")) return true;

        Transform t = other.transform;
        while (t != null)
        {
            foreach (var c in t.GetComponents<MonoBehaviour>())
                if (c != null && c.GetType().Name == "AutoHandPlayer") return true;
            t = t.parent;
        }

        var rb = other.attachedRigidbody;
        return rb != null && rb.transform.root != null && rb.transform.root.name.Contains("AutoHandPlayer");
    }
}
