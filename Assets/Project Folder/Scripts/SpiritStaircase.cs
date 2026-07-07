using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Geister-Treppe zur schwebenden Pforte über der Baumkrone.
/// Die Stufen sind im Editor sichtbar (Level-Design), verstecken sich aber
/// beim Spielstart und materialisieren sich erst, wenn der Spieler die
/// Krone erreicht (gleiche Höhe wie TreePortalUnlock) — Stufe für Stufe
/// von unten nach oben, als würde das erwachende Portal den Weg herbeirufen.
/// </summary>
public class SpiritStaircase : MonoBehaviour
{
    [Header("Erscheinen")]
    [Tooltip("Welt-Höhe, ab der die Treppe erscheint (wie TreePortalUnlock.unlockHeight).")]
    public float revealHeight = 66.5f;

    [Tooltip("Verzögerung zwischen zwei Stufen in Sekunden.")]
    public float stepInterval = 0.08f;

    [Tooltip("Dauer, in der eine einzelne Stufe auf volle Größe wächst.")]
    public float stepGrowDuration = 0.45f;

    [Tooltip("Startverzögerung nach dem Auslösen (das Portal erwacht zuerst).")]
    public float startDelay = 0.8f;

    [Header("Spieler")]
    [Tooltip("Wird automatisch per Tag 'Player' / Camera.main gesucht, falls leer.")]
    public Transform player;
    public string playerTag = "Player";

    [Header("Optional")]
    [Tooltip("Klang, wenn die Treppe zu erscheinen beginnt.")]
    public AudioSource revealSound;

    [Header("Geist-Modus")]
    [Tooltip("Diese Collider (z. B. der Kronen-MeshCollider) blockieren den Spieler " +
             "nach dem Erscheinen der Treppe nicht mehr — als Geist gleitet er durchs Geäst. " +
             "Stamm, Plattformen und Boden bleiben davon unberührt.")]
    public Collider[] ghostThroughColliders;

    private readonly List<Transform> _steps = new List<Transform>();
    private Vector3[] _targetScales;
    private bool _revealed;

    private void Awake()
    {
        // Alle Kinder (Stufen + Steg) einsammeln und für den Spielstart verstecken.
        foreach (Transform child in transform)
            _steps.Add(child);

        _targetScales = new Vector3[_steps.Count];
        for (int i = 0; i < _steps.Count; i++)
        {
            _targetScales[i] = _steps[i].localScale;
            _steps[i].gameObject.SetActive(false);
        }
    }

    private void Start()
    {
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
        if (_revealed) return;
        if (player == null) { ResolvePlayer(); if (player == null) return; }

        if (player.position.y >= revealHeight)
            Reveal();
    }

    /// <summary>Treppe erscheinen lassen (auch von außen aufrufbar, z. B. für Tests).</summary>
    public void Reveal()
    {
        if (_revealed) return;
        _revealed = true;
        ApplyGhostMode();
        StartCoroutine(RevealRoutine());
    }

    /// <summary>
    /// Spieler-Rig gleitet ab jetzt durch das Kronen-Geäst (Geist!), damit die
    /// Treppe durch die Baumkrone begehbar ist. Wirkt nur auf die angegebenen
    /// Collider — Stamm-Kapseln, Plattformen und Boden bleiben solide.
    /// </summary>
    private void ApplyGhostMode()
    {
        if (ghostThroughColliders == null || ghostThroughColliders.Length == 0) return;
        if (player == null) { ResolvePlayer(); if (player == null) return; }

        foreach (var playerCol in player.root.GetComponentsInChildren<Collider>(true))
        {
            if (playerCol == null || playerCol.isTrigger) continue;
            foreach (var ghostCol in ghostThroughColliders)
            {
                if (ghostCol == null || ghostCol.isTrigger) continue;
                Physics.IgnoreCollision(playerCol, ghostCol, true);
            }
        }
    }

    private IEnumerator RevealRoutine()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (revealSound != null) revealSound.Play();

        // Von unten nach oben: jede Stufe wächst sanft aus dem Nichts.
        for (int i = 0; i < _steps.Count; i++)
        {
            StartCoroutine(GrowStep(_steps[i], _targetScales[i]));
            yield return new WaitForSeconds(stepInterval);
        }
    }

    private IEnumerator GrowStep(Transform step, Vector3 targetScale)
    {
        step.gameObject.SetActive(true);
        step.localScale = Vector3.zero;

        float t = 0f;
        while (t < stepGrowDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / stepGrowDuration));
            step.localScale = targetScale * k;
            yield return null;
        }
        step.localScale = targetScale;
    }
}
