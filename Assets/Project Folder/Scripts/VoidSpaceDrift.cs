using UnityEngine;

/// <summary>
/// Keeps the void visuals centred around the active VR camera and moves only the
/// environment. This creates a drifting sensation without moving the player rig.
/// </summary>
[DisallowMultipleComponent]
public sealed class VoidSpaceDrift : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField, Min(0f)] private float positionalDrift = 0.85f;
    [SerializeField, Min(0.001f)] private float driftFrequency = 0.18f;

    [Header("Layer Motion")]
    [SerializeField] private Transform[] rotatingLayers = new Transform[0];
    [SerializeField] private Vector3[] angularVelocities = new Vector3[0];
    [SerializeField] private Transform pulseLayer;
    [SerializeField, Range(0f, 0.08f)] private float pulseAmount = 0.026f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 0.45f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem[] particleSystems = new ParticleSystem[0];

    private Vector3 pulseBaseScale;

    private void Awake()
    {
        ResolveFollowTarget();

        if (pulseLayer != null)
            pulseBaseScale = pulseLayer.localScale;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null)
                continue;

            system.Clear(true);
            system.Play(true);
            system.Simulate(Mathf.Min(18f, system.main.startLifetime.constantMax * 0.75f), true, false, true);
        }
    }

    private void LateUpdate()
    {
        ResolveFollowTarget();

        float time = Time.time;
        if (followTarget != null)
        {
            Vector3 gentleOffset = new Vector3(
                Mathf.Sin(time * driftFrequency * 0.83f),
                Mathf.Sin(time * driftFrequency * 0.57f + 1.7f) * 0.45f,
                Mathf.Cos(time * driftFrequency * 0.71f + 0.4f)) * positionalDrift;

            transform.position = followTarget.position + gentleOffset;
        }

        int layerCount = Mathf.Min(rotatingLayers.Length, angularVelocities.Length);
        for (int i = 0; i < layerCount; i++)
        {
            if (rotatingLayers[i] != null)
                rotatingLayers[i].Rotate(angularVelocities[i] * Time.deltaTime, Space.Self);
        }

        if (pulseLayer != null)
        {
            float pulse = 1f + Mathf.Sin(time * pulseSpeed) * pulseAmount;
            pulseLayer.localScale = pulseBaseScale * pulse;
        }
    }

    private void ResolveFollowTarget()
    {
        if (followTarget != null && followTarget.gameObject.activeInHierarchy)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            followTarget = mainCamera.transform;
            return;
        }

        Camera anyCamera = FindAnyObjectByType<Camera>();
        followTarget = anyCamera != null ? anyCamera.transform : null;
    }
}
