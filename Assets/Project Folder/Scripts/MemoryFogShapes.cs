using UnityEngine;

// Blendet abstrakte Nebel-Gestalten (Partikelsysteme) abhaengig von der Spielerhoehe
// ein und aus. Jede Etappe der Erinnerungsspirale bekommt so ihre eigene Nebelform,
// die nur in ihrem Hoehenband praesent ist (weiche Raender, sanftes Faden).
// Ergaenzt FogClimbController (globale Dichte) um lokale, erzaehlende Formen.
public class MemoryFogShapes : MonoBehaviour
{
    [System.Serializable]
    public class Shape
    {
        [Tooltip("Nur zur Orientierung im Inspector")]
        public string label;
        public ParticleSystem system;
        [Tooltip("Unteres Ende des Hoehenbands (Welt-Y)")]
        public float bandBottom = 50f;
        [Tooltip("Oberes Ende des Hoehenbands (Welt-Y)")]
        public float bandTop = 57f;
        [Tooltip("Weicher Rand unter-/oberhalb des Bands")]
        public float feather = 2.5f;
        [Tooltip("Emissionsrate bei voller Praesenz")]
        public float fullRate = 12f;
        [HideInInspector] public float current;
    }

    public Shape[] shapes;

    [Tooltip("Wird automatisch per Tag 'Player' / Camera.main gesucht, falls leer")]
    public Transform player;
    public string playerTag = "Player";
    [Tooltip("Blendgeschwindigkeit (Praesenz pro Sekunde)")]
    public float fadeSpeed = 0.7f;

    private void Start()
    {
        ResolvePlayer();
        if (shapes == null) return;
        foreach (var s in shapes)
        {
            if (s.system == null) continue;
            var em = s.system.emission;
            em.rateOverTime = 0f;
            if (!s.system.isPlaying) s.system.Play();
        }
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
        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }

        float y = player.position.y;
        foreach (var s in shapes)
        {
            if (s.system == null) continue;
            float f = Mathf.Max(0.01f, s.feather);
            float inFromBelow = (y - (s.bandBottom - f)) / f;
            float inFromAbove = ((s.bandTop + f) - y) / f;
            float target = Mathf.Clamp01(Mathf.Min(inFromBelow, inFromAbove));

            s.current = Mathf.MoveTowards(s.current, target, Time.deltaTime * fadeSpeed);
            var em = s.system.emission;
            em.rateOverTime = s.fullRate * s.current;
        }
    }
}
