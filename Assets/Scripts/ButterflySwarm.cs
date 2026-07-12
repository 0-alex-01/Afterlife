using UnityEngine;

public class ButterflySwarmBurst : MonoBehaviour
{
    private enum SwarmState
    {
        Cluster,
        Burst,
        DriftAway
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Detection")]
    [SerializeField] private float triggerDistance = 4f;
    [SerializeField] private bool burstOnlyOnce = true;

    [Header("Cluster / Knubbel")]
    [SerializeField] private float clusterRadius = 1.2f;
    [SerializeField] private float clusterHeight = 1.0f;
    [SerializeField] private float clusterMoveSpeed = 3f;
    [SerializeField] private float flutterAmount = 0.15f;
    [SerializeField] private float flutterSpeed = 2f;

    [Header("Burst / Auflösen")]
    [SerializeField] private float burstSpeed = 6f;
    [SerializeField] private float upwardBurst = 1.5f;
    [SerializeField] private float burstDuration = 1.2f;

    [Header("After Burst")]
    [SerializeField] private float driftSpeed = 1.5f;
    [SerializeField] private float driftUpward = 0.2f;

    [Header("Animation")]
    [SerializeField] private bool randomizeAnimationStart = true;
    [SerializeField] private float animationSpeedMultiplier = 0.5f;

    private Transform[] butterflies;
    private Vector3[] clusterOffsets;
    private Vector3[] burstDirections;
    private float[] randomOffsets;

    private SwarmState currentState = SwarmState.Cluster;
    private bool hasBurst = false;
    private float burstTimer = 0f;

    private void Awake()
    {
        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);

            if (foundPlayer != null)
                player = foundPlayer.transform;
        }

        CollectButterflies();
        SetupButterflies();
    }

    private void Update()
    {
        if (currentState == SwarmState.Cluster)
        {
            CheckPlayerDistance();
        }
    }

    private void LateUpdate()
    {
        switch (currentState)
        {
            case SwarmState.Cluster:
                UpdateCluster();
                break;

            case SwarmState.Burst:
                UpdateBurst();
                break;

            case SwarmState.DriftAway:
                UpdateDriftAway();
                break;
        }
    }

    private void CollectButterflies()
    {
        int childCount = transform.childCount;
        butterflies = new Transform[childCount];

        for (int i = 0; i < childCount; i++)
        {
            butterflies[i] = transform.GetChild(i);
        }

        clusterOffsets = new Vector3[butterflies.Length];
        burstDirections = new Vector3[butterflies.Length];
        randomOffsets = new float[butterflies.Length];
    }

    private void SetupButterflies()
    {
        for (int i = 0; i < butterflies.Length; i++)
        {
            if (butterflies[i] == null) continue;

            clusterOffsets[i] = Random.insideUnitSphere * clusterRadius;
            clusterOffsets[i].y = Random.Range(0f, clusterHeight);

            randomOffsets[i] = Random.Range(0f, 100f);

            Vector3 outward = clusterOffsets[i].normalized;

            if (outward.magnitude < 0.1f)
                outward = Random.onUnitSphere;

            outward.y = Mathf.Abs(outward.y) + upwardBurst;
            burstDirections[i] = outward.normalized;

            SetupAnimation(butterflies[i]);
        }
    }

    private void SetupAnimation(Transform butterfly)
    {
        Animation legacyAnimation = butterfly.GetComponent<Animation>();

        if (legacyAnimation != null)
        {
            legacyAnimation.wrapMode = WrapMode.Loop;

            foreach (AnimationState state in legacyAnimation)
            {
                state.wrapMode = WrapMode.Loop;
                state.speed = animationSpeedMultiplier * Random.Range(0.85f, 1.15f);

                if (randomizeAnimationStart)
                    state.time = Random.Range(0f, state.length);
            }

            legacyAnimation.Play();
        }

        Animator animator = butterfly.GetComponent<Animator>();

        if (animator != null)
        {
            animator.speed = animationSpeedMultiplier * Random.Range(0.85f, 1.15f);

            if (randomizeAnimationStart)
                animator.Play(0, 0, Random.Range(0f, 1f));
        }
    }

    private void CheckPlayerDistance()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= triggerDistance)
        {
            StartBurst();
        }
    }

    private void StartBurst()
    {
        if (burstOnlyOnce && hasBurst)
            return;

        hasBurst = true;
        currentState = SwarmState.Burst;
        burstTimer = 0f;

        for (int i = 0; i < butterflies.Length; i++)
        {
            if (butterflies[i] == null) continue;

            Vector3 awayFromPlayer = butterflies[i].position - player.position;
            awayFromPlayer.y = 0f;

            if (awayFromPlayer.magnitude < 0.1f)
                awayFromPlayer = burstDirections[i];

            Vector3 finalDirection = awayFromPlayer.normalized + burstDirections[i];
            finalDirection.y += upwardBurst;

            burstDirections[i] = finalDirection.normalized;
        }
    }

    private void UpdateCluster()
    {
        for (int i = 0; i < butterflies.Length; i++)
        {
            if (butterflies[i] == null) continue;

            float t = Time.time * flutterSpeed + randomOffsets[i];

            Vector3 flutter = new Vector3(
                Mathf.Sin(t) * flutterAmount,
                Mathf.Sin(t * 1.7f) * flutterAmount,
                Mathf.Cos(t) * flutterAmount
            );

            Vector3 targetLocalPosition = clusterOffsets[i] + flutter;

            butterflies[i].localPosition = Vector3.Lerp(
                butterflies[i].localPosition,
                targetLocalPosition,
                clusterMoveSpeed * Time.deltaTime
            );

            RotateButterflyTowardMovement(butterflies[i], flutter);
        }
    }

    private void UpdateBurst()
    {
        burstTimer += Time.deltaTime;

        for (int i = 0; i < butterflies.Length; i++)
        {
            if (butterflies[i] == null) continue;

            butterflies[i].position += burstDirections[i] * burstSpeed * Time.deltaTime;

            RotateButterflyTowardMovement(butterflies[i], burstDirections[i]);
        }

        if (burstTimer >= burstDuration)
        {
            currentState = SwarmState.DriftAway;
        }
    }

    private void UpdateDriftAway()
    {
        for (int i = 0; i < butterflies.Length; i++)
        {
            if (butterflies[i] == null) continue;

            Vector3 driftDirection = burstDirections[i];
            driftDirection.y += driftUpward;

            butterflies[i].position += driftDirection.normalized * driftSpeed * Time.deltaTime;

            RotateButterflyTowardMovement(butterflies[i], driftDirection);
        }
    }

    private void RotateButterflyTowardMovement(Transform butterfly, Vector3 direction)
    {
        direction.y = 0f;

        if (direction.magnitude < 0.05f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        butterfly.rotation = Quaternion.Slerp(
            butterfly.rotation,
            targetRotation,
            5f * Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, clusterRadius);
    }
}