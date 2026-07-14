using UnityEngine;
using System.Collections;

/// <summary>
/// Einfaches Szenen-Portal: Betritt der Spieler den Trigger, blendet der
/// ScreenFader sanft aus und lädt die Zielszene.
/// Mit <see cref="locked"/> startet es gesperrt (z. B. bis der Friedhofs-Pfad
/// freigeschaltet ist) und wird später per <see cref="Unlock"/> geöffnet.
/// Solange es gesperrt ist, bleibt die „Füllung" des Portals (die blaue Membran /
/// der Void) unsichtbar — der Steinbogen steht dann leer da; beim Freischalten
/// erscheint die Füllung mit einer kurzen Öffnungs-Animation.
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
    [Tooltip("Gesperrt = Betreten löst nichts aus und die Portalfüllung ist unsichtbar (bis Unlock() aufgerufen wird).")]
    public bool locked = false;

    [Header("Portalfüllung (blaue Membran / Void)")]
    [Tooltip("Das Objekt mit der Portal-Füllung (Membran, Void-Motes, Void-Licht). Leer = automatisch das Kind 'Visual'. Wird nur sichtbar, wenn das Portal freigeschaltet ist.")]
    public GameObject portalFillVisual;
    [Tooltip("Dauer der Öffnungs-Animation beim Freischalten (Sekunden). 0 = sofort einblenden.")]
    public float activateDuration = 1.3f;

    private bool _entered;
    private Vector3 _fillBaseScale = Vector3.one;
    private Coroutine _activateRoutine;
    private Collider _trigger;
    private Camera _headCamera;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        _trigger = GetComponent<Collider>();
        if (_trigger != null) _trigger.isTrigger = true;

        if (portalFillVisual == null)
        {
            var vis = transform.Find("Visual");
            if (vis != null) portalFillVisual = vis.gameObject;
        }
        if (portalFillVisual != null)
        {
            _fillBaseScale = portalFillVisual.transform.localScale;
            // Gesperrt starten => Füllung unsichtbar (leerer Steinbogen).
            portalFillVisual.SetActive(!locked);
        }
    }

    private void Update()
    {
        // OpenXR's tracked head does not always carry a collider in a player build. Detecting the
        // active head camera inside the portal volume keeps the transition independent of rig
        // object names and physics-layer differences.
        if (!locked && !_entered && IsHeadInsideTrigger())
        {
            EnterPortal();
        }
    }

    /// <summary>Portal freischalten (z. B. vom CemeteryAtmosphereManager nach der letzten Erlösung).
    /// Öffnet zugleich die Portalfüllung mit einer kurzen Animation.</summary>
    public void Unlock()
    {
        bool wasLocked = locked;
        locked = false;
        if (wasLocked) RevealFill();
    }

    /// <summary>Blendet die Portalfüllung (blaue Membran / Void) mit einer
    /// Öffnungs-Animation ein.</summary>
    private void RevealFill()
    {
        if (portalFillVisual == null) return;
        if (!isActiveAndEnabled || activateDuration <= 0f)
        {
            portalFillVisual.transform.localScale = _fillBaseScale;
            portalFillVisual.SetActive(true);
            return;
        }
        if (_activateRoutine != null) StopCoroutine(_activateRoutine);
        _activateRoutine = StartCoroutine(RevealFillRoutine());
    }

    private IEnumerator RevealFillRoutine()
    {
        portalFillVisual.SetActive(true);
        // Von einem dünnen Schlitz aufziehen — wirkt wie ein sich öffnendes Portal.
        Vector3 from = new Vector3(_fillBaseScale.x * 0.02f, _fillBaseScale.y, _fillBaseScale.z * 0.02f);
        float e = 0f;
        while (e < activateDuration)
        {
            e += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / activateDuration));
            portalFillVisual.transform.localScale = Vector3.Lerp(from, _fillBaseScale, t);
            yield return null;
        }
        portalFillVisual.transform.localScale = _fillBaseScale;
        _activateRoutine = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (locked || _entered) return;
        if (!IsPlayer(other)) return;

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("ScenePortal ohne Zielszene!", this);
            return;
        }

        EnterPortal();
    }

    private bool IsHeadInsideTrigger()
    {
        if (_trigger == null || !_trigger.enabled)
        {
            return false;
        }

        if (_headCamera == null || !_headCamera.isActiveAndEnabled)
        {
            _headCamera = Camera.main;
            if (_headCamera == null || !_headCamera.isActiveAndEnabled)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                    {
                        _headCamera = cameras[i];
                        break;
                    }
                }
            }
        }

        return _headCamera != null &&
               _trigger.bounds.SqrDistance(_headCamera.transform.position) <= 0.25f;
    }

    private void EnterPortal()
    {
        if (locked || _entered)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(targetSceneName) ||
            !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"[ScenePortal] Scene '{targetSceneName}' is not enabled in Build Settings.", this);
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
        if (root != null &&
            (root.name.Contains("AutoHandPlayer") || root.name.Contains("XR Origin") ||
             root.name.Contains("XR Rig"))) return true;

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
            return true;

        Camera head = Camera.main;
        if (head != null && other.transform.root == head.transform.root) return true;

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
