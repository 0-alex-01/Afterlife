using UnityEngine;

// Spielt einen sanften Chime, wenn der Spieler eine Erinnerungsstation zum ersten
// Mal erreicht. Distanzbasiert (wie AfterlifeFragment), kein Trigger-Collider noetig.
[RequireComponent(typeof(AudioSource))]
public class StationArrivalChime : MonoBehaviour
{
    [Tooltip("Ausloese-Radius um dieses Objekt")]
    public float triggerRadius = 2.2f;
    [Tooltip("0 = nur einmal pro Sitzung, sonst Sekunden bis zum erneuten Ausloesen")]
    public float retriggerAfter = 0f;

    private Transform player;
    private AudioSource source;
    private bool played;
    private float playedAt;

    private void Start()
    {
        source = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (player == null)
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) player = tagged.transform;
            else if (Camera.main != null) player = Camera.main.transform;
            if (player == null) return;
        }

        if (played && (retriggerAfter <= 0f || Time.time - playedAt < retriggerAfter))
            return;

        if (Vector3.Distance(transform.position, player.position) <= triggerRadius)
        {
            if (source.clip != null) source.Play();
            played = true;
            playedAt = Time.time;
        }
    }
}
