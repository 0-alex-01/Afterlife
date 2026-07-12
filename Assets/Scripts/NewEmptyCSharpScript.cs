using UnityEngine;

public class ButterflySwarm : MonoBehaviour
{
    private enum SwarmState
    {
        Cluster,
        Scatter,
        Return
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Detection")]
    [SerializeField] private float triggerRadius = 4f;
    [SerializeField] private float calmRadius = 8f;

    [Header("Cluster Movement")]
    [SerializeField] private float clusterRadius = 0.8f;
    [SerializeField] private float clusterHeight = 1.2f;
    [SerializeField] private float flutterSpeed = 2f;
    [SerializeField] private float flutterAmount = 0.25f;
    [SerializeField] private float orbitSpeed = 25f;

    [Header("Scatter Movement")]
    [SerializeField] private float scatterDistance = 5f;
    [SerializeField] private float scatterSpeed = 5f;
    [SerializeField] private float upwardScatter = 2f;

    [Header("Return Movement")]
    [SerializeField] private float returnSpeed = 2f;

    private Transform[] butterflies;
    private Vector3[] clusterOffsets;
    private Vector3[] scatterTargets;
    private float[] randomSeeds;

    private SwarmState state = SwarmState.Cluster;

    private void Awake()
    {
        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (foundPlayer != null)
                player = foundPlayer.transform;
        }

        int childCount = transform.childCount;

        butterflies = new Transform[childCount];
        clusterOffsets = new Vector3[childCount];
        scatterTargets = new Vector3[childCount];
        randomSeeds = new float[childCount];

        for (int i = 0; i < childCount; i++)
        {
            butterflies[i] = transform.GetChild(i);
            randomSeeds[i] = Random.Range(0f, 100f);

            Vector2 circle = Random.insideUnitCircle * clusterRadius;
            float height = Random.Range(0f, clusterHeight);

            clusterOffsets[i] = new Vector3(circle.x, height, circle.y);

            // Falls die Butterflies eine alte Animation-Komponente haben
            Animation anim = butterflies[i].GetComponent<Animation>();
            if (anim != null)
            {
                anim.wrapMode = WrapMode.Loop;
                anim.Play();
            }
        }
    }

    private void Update()
    {
        CheckPlayerDistance();

        switch (state)
        {
            case SwarmState.Cluster:
                UpdateCluster();
                break;

            case SwarmState.Scatter:
                UpdateScatter();
                break;

            case SwarmState.Return:
                UpdateReturn();
                break;
        }
    }

    private void CheckPlayerDistance()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= triggerRadius && state != SwarmState.Scatter)
        {
            StartScatter();
        }
        else if (distance >= calmRadius && state == SwarmState.Scatter)
        {
            state = SwarmState.Return;
        }
    }

    private void UpdateCluster()
    {
        for (int i = 0; i < butterflies.Length; i++)
        {
            float t = Time.time + randomSeeds[i];

            Quaternion orbit = Quaternion.Euler(0f, t * orbitSpeed, 0f);
            Vector3 baseOffset = orbit * clusterOffsets[i];

            float flutterY = Mathf.Sin(t * flutterSpeed) * flutterAmount;
            Vector3 flutterOffset = new Vector3(0f, flutterY, 0f);

            Vector3 targetPosition = transform.position + baseOffset + flutterOffset;

            butterflies[i].position = Vector3.Lerp(
                butterflies[i].position,
                targetPosition,
                Time.deltaTime * 3f
            );

            MakeButterflyFaceMovement(butterflies[i], targetPosition);
        }
    }

    private void StartScatter()
    {
        state = SwarmState.Scatter;

        for (int i = 0; i < butterflies.Length; i++)
        {
            Vector3 awayFromPlayer = butterflies[i].position - player.position;
            awayFromPlayer.y = 0f;

            if (awayFromPlayer.magnitude < 0.01f)
                awayFromPlayer = Random.insideUnitSphere;

            awayFromPlayer.Normalize();

            Vector3 randomSide = Random.insideUnitSphere * 1.5f;
            randomSide.y = Mathf.Abs(randomSide.y);

            scatterTargets[i] =
                butterflies[i].position +
                awayFromPlayer * scatterDistance +
                Vector3.up * Random.Range(0.5f, upwardScatter) +
                randomSide;
        }
    }

    private void UpdateScatter()
    {
        for (int i = 0; i < butterflies.Length; i++)
        {
            Vector3 oldPosition = butterflies[i].position;

            butterflies[i].position = Vector3.MoveTowards(
                butterflies[i].position,
                scatterTargets[i],
                scatterSpeed * Time.deltaTime
            );

            FaceDirection(butterflies[i], butterflies[i].position - oldPosition);
        }
    }

    private void UpdateReturn()
    {
        bool allReturned = true;

        for (int i = 0; i < butterflies.Length; i++)
        {
            float t = Time.time + randomSeeds[i];

            Quaternion orbit = Quaternion.Euler(0f, t * orbitSpeed, 0f);
            Vector3 baseOffset = orbit * clusterOffsets[i];

            float flutterY = Mathf.Sin(t * flutterSpeed) * flutterAmount;
            Vector3 targetPosition = transform.position + baseOffset + new Vector3(0f, flutterY, 0f);

            Vector3 oldPosition = butterflies[i].position;

            butterflies[i].position = Vector3.MoveTowards(
                butterflies[i].position,
                targetPosition,
                returnSpeed * Time.deltaTime
            );

            FaceDirection(butterflies[i], butterflies[i].position - oldPosition);

            if (Vector3.Distance(butterflies[i].position, targetPosition) > 0.2f)
                allReturned = false;
        }

        if (allReturned)
            state = SwarmState.Cluster;
    }

    private void MakeButterflyFaceMovement(Transform butterfly, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - butterfly.position;
        FaceDirection(butterfly, direction);
    }

    private void FaceDirection(Transform butterfly, Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

        butterfly.rotation = Quaternion.Slerp(
            butterfly.rotation,
            targetRotation,
            Time.deltaTime * 5f
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, calmRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, clusterRadius);
    }
}