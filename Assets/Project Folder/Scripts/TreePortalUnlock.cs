using UnityEngine;

/// <summary>
/// Schaltet das Szenen-Portal der Baum-Szene frei, sobald der Spieler einmal
/// die Baumkrone erreicht hat (Welt-Y >= unlockHeight — passend zum
/// AfterlifeAscensionController, der 50..70 als Aufstiegsbereich nutzt).
/// Vorher schlummert das Portal: Licht gedimmt, Partikel aus, Trigger gesperrt.
/// Beim Freischalten erwacht es sichtbar (Licht blendet auf, Partikel starten).
/// </summary>
public class TreePortalUnlock : MonoBehaviour
{
    [Header("Freischaltung")]
    [Tooltip("Welt-Hoehe, die der Spieler einmal erreichen muss (Krone ~70).")]
    public float unlockHeight = 68f;

    [Tooltip("Wird automatisch per Tag 'Player' / Camera.main gesucht, falls leer.")]
    public Transform player;
    public string playerTag = "Player";

    [Header("Portal-Referenzen")]
    [Tooltip("Das ScenePortal, das gesperrt startet und hier entsperrt wird.")]
    public ScenePortal portal;

    [Tooltip("Portal-Licht: schlummert gedimmt, blendet beim Freischalten auf.")]
    public Light portalLight;

    [Tooltip("Partikel, die erst beim Freischalten starten.")]
    public ParticleSystem portalParticles;

    [Tooltip("Optionaler Klang beim Erwachen des Portals.")]
    public AudioSource unlockSound;

    [Header("Erwachen")]
    [Tooltip("Sekunden, in denen das Licht auf volle Staerke aufblendet.")]
    public float wakeDuration = 3f;

    [Range(0f, 1f)]
    [Tooltip("Anteil der Ziel-Intensitaet im Schlummerzustand.")]
    public float dormantFactor = 0.12f;

    private float _targetIntensity;
    private bool _unlocked;
    private float _wakeTimer;

    private void Start()
    {
        if (portal != null) portal.locked = true;

        if (portalLight != null)
        {
            _targetIntensity = portalLight.intensity;
            portalLight.intensity = _targetIntensity * dormantFactor;
        }

        if (portalParticles != null)
            portalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        if (player != null) return;
        if (!string.IsNullOrEmpty(playerTag))
        {
            var tagged = GameObject.FindWithTag(playerTag);
            if (tagged != null) { player = tagged.transform; return; }
        }
        if (Camera.main != null) player = Camera.main.transform;
    }

    private void Update()
    {
        if (!_unlocked)
        {
            if (player == null) { ResolvePlayer(); if (player == null) return; }
            if (player.position.y >= unlockHeight)
                Unlock();
            return;
        }

        // Sanftes Aufblenden nach dem Freischalten
        if (portalLight != null && _wakeTimer < wakeDuration)
        {
            _wakeTimer += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_wakeTimer / wakeDuration));
            portalLight.intensity = Mathf.Lerp(_targetIntensity * dormantFactor, _targetIntensity, k);
        }
    }

    /// <summary>Portal sofort freischalten (auch von aussen aufrufbar, z. B. fuer Tests).</summary>
    public void Unlock()
    {
        if (_unlocked) return;
        _unlocked = true;

        if (portal != null) portal.Unlock();
        if (portalParticles != null) portalParticles.Play();
        if (unlockSound != null) unlockSound.Play();

        Debug.Log("TreePortalUnlock: Krone erreicht — Portal erwacht.");
    }
}
