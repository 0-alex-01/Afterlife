using UnityEngine;

/// <summary>
/// Macht die Trauernden lebendiger: jede Figur bekommt ein eigenes Tempo und
/// einen eigenen Startpunkt in ihrer Trauer-Animation (kein synchroner
/// Roboter-Chor), dazu ein kaum merkliches prozedurales Trauer-Wiegen des
/// ganzen Körpers. Deterministisch pro Figur (Name als Seed).
/// </summary>
[RequireComponent(typeof(Animator))]
public class MournerAnimationVariance : MonoBehaviour
{
    [Header("Animations-Variation")]
    [Tooltip("Langsamstes Abspieltempo der Trauer-Animation.")]
    public float minSpeed = 0.8f;
    [Tooltip("Schnellstes Abspieltempo der Trauer-Animation.")]
    public float maxSpeed = 1.05f;

    [Header("Trauer-Wiegen (prozedural, additiv)")]
    [Tooltip("Maximale Körper-Neigung in Grad (0 = aus).")]
    public float swayAngle = 1.2f;
    [Tooltip("Dauer einer Wiege-Periode in Sekunden.")]
    public float swayPeriod = 7f;

    private Animator _animator;
    private Quaternion _baseRotation;
    private float _phase;
    private float _periodJitter;

    private void Start()
    {
        _animator = GetComponent<Animator>();
        _animator.applyRootMotion = false;

        // Name als Seed: wirkt bei jedem Spielstart gleich, aber pro Figur anders.
        var rng = new System.Random(name.GetHashCode());
        _animator.speed = Mathf.Lerp(minSpeed, maxSpeed, (float)rng.NextDouble());

        // Jede Figur startet an einer anderen Stelle ihres Trauer-Loops.
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        _animator.Play(state.fullPathHash, 0, (float)rng.NextDouble());

        _baseRotation = transform.rotation;
        _phase = (float)rng.NextDouble() * Mathf.PI * 2f;
        _periodJitter = Mathf.Lerp(0.85f, 1.2f, (float)rng.NextDouble());
    }

    private void Update()
    {
        if (swayAngle <= 0f) return;

        // Zwei überlagerte, leicht verstimmte Sinus-Wellen: organisches Wiegen
        // statt Metronom-Pendeln.
        float w = Mathf.PI * 2f / (swayPeriod * _periodJitter);
        float a = Mathf.Sin(Time.time * w + _phase) * swayAngle;
        float b = Mathf.Sin(Time.time * w * 0.63f + _phase * 1.7f) * swayAngle * 0.5f;
        transform.rotation = _baseRotation * Quaternion.Euler(a, 0f, b);
    }
}
