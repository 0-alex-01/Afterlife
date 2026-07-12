using UnityEngine;

/// <summary>
/// Hält den sichtbaren Seelen-Körper des Spielers unter der VR-Kamera:
/// Position folgt dem Kopf (x/z), steht aber auf Bodenhöhe des Rigs;
/// die Drehung folgt nur dem Yaw des Kopfes (kein Kippen beim Nicken).
/// Leicht nach hinten versetzt, damit der Kopf des Körpers nicht in die
/// Kamera clippt, wenn man nach unten schaut.
/// </summary>
public class SelfBodyFollow : MonoBehaviour
{
    [Tooltip("Die VR-Kopf-Kamera. Leer = Camera.main.")]
    public Transform head;
    [Tooltip("Referenz für die Bodenhöhe (Spieler-Root). Leer = Kopf minus fallbackHeight.")]
    public Transform groundRef;
    [Tooltip("Wie weit der Körper hinter der Kamera steht (Meter) — verhindert Clipping beim Runterschauen.")]
    public float backOffset = 0.12f;
    [Tooltip("Zusätzlicher Höhenversatz des Körpers (Meter).")]
    public float heightOffset = 0f;
    [Tooltip("Angenommene Augenhöhe, falls kein groundRef gesetzt ist.")]
    public float fallbackHeight = 1.65f;

    private void LateUpdate()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head == null) return;

        Vector3 yawFwd = head.forward;
        yawFwd.y = 0f;
        if (yawFwd.sqrMagnitude < 1e-4f) yawFwd = transform.forward;
        yawFwd.Normalize();

        float groundY = groundRef != null ? groundRef.position.y : head.position.y - fallbackHeight;
        Vector3 pos = head.position;
        pos.y = groundY + heightOffset;
        pos -= yawFwd * backOffset;

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(yawFwd, Vector3.up);
    }
}
