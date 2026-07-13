using UnityEngine;

public class AnimalFleeAI : MonoBehaviour
{
    private enum AnimalState
    {
        Idle,
        Wander,
        Flee
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Animation State Names")]
    [SerializeField] private string idleStateName = "Deer_001_idle";
    [SerializeField] private string walkStateName = "Deer_001_walk";
    [SerializeField] private string runStateName = "Deer_001_run";

    [Header("Detection")]
    [SerializeField] private float fleeRadius = 5f;
    [SerializeField] private float calmRadius = 9f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 0.6f;
    [SerializeField] private float runSpeed = 3f;
    [SerializeField] private float turnSpeed = 5f;

    [Header("Wandering")]
    [SerializeField] private float wanderRadius = 8f;
    [SerializeField] private Vector2 idleTimeRange = new Vector2(2f, 6f);
    [SerializeField] private Vector2 walkTimeRange = new Vector2(2f, 5f);

    [Header("Model Direction Fix")]
    [SerializeField] private float modelYawOffset = 0f;

    [Header("Height")]
    [SerializeField] private bool keepInitialY = true;

    private AnimalState currentState;
    private Vector3 startPosition;
    private Vector3 wanderTarget;
    private float stateTimer;
    private float fixedY;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (foundPlayer != null)
                player = foundPlayer.transform;
        }

        startPosition = transform.position;
        fixedY = transform.position.y;
    }

    private void Start()
    {
        EnterIdle();
    }

    /// <summary>
    /// Setzt Wanderzentrum und feste Laufhöhe nach einer externen
    /// Szenen-/Bodenkorrektur auf die aktuelle Position.
    /// </summary>
    public void RebaseToCurrentPosition(Transform livePlayer = null)
    {
        if (livePlayer != null)
            player = livePlayer;

        startPosition = transform.position;
        wanderTarget = transform.position;
        fixedY = transform.position.y;
    }

    private void Update()
    {
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= fleeRadius)
            {
                EnterFlee();
            }
            else if (currentState == AnimalState.Flee && distanceToPlayer >= calmRadius)
            {
                EnterIdle();
            }
        }

        switch (currentState)
        {
            case AnimalState.Idle:
                UpdateIdle();
                break;

            case AnimalState.Wander:
                UpdateWander();
                break;

            case AnimalState.Flee:
                UpdateFlee();
                break;
        }

        KeepHeight();
    }

    private void EnterIdle()
    {
        currentState = AnimalState.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);
        PlayAnimation(idleStateName);
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            EnterWander();
        }
    }

    private void EnterWander()
    {
        currentState = AnimalState.Wander;
        stateTimer = Random.Range(walkTimeRange.x, walkTimeRange.y);

        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        wanderTarget = startPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

        PlayAnimation(walkStateName);
    }

    private void UpdateWander()
    {
        stateTimer -= Time.deltaTime;

        Vector3 direction = wanderTarget - transform.position;
        direction.y = 0f;

        if (direction.magnitude < 0.3f || stateTimer <= 0f)
        {
            EnterIdle();
            return;
        }

        MoveInDirection(direction.normalized, walkSpeed);
    }

    private void EnterFlee()
    {
        if (currentState == AnimalState.Flee)
            return;

        currentState = AnimalState.Flee;
        PlayAnimation(runStateName);
    }

    private void UpdateFlee()
    {
        if (player == null)
            return;

        Vector3 directionAway = transform.position - player.position;
        directionAway.y = 0f;

        if (directionAway.magnitude < 0.01f)
            directionAway = transform.forward;

        MoveInDirection(directionAway.normalized, runSpeed);
    }

    private void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction, Vector3.up) *
            Quaternion.Euler(0f, modelYawOffset, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        transform.position += direction * speed * Time.deltaTime;
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        float randomStart = Random.Range(0f, 1f);
        animator.CrossFade(stateName, 0.2f, 0, randomStart);
    }

    private void KeepHeight()
    {
        if (!keepInitialY)
            return;

        transform.position = new Vector3(
            transform.position.x,
            fixedY,
            transform.position.z
        );
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, calmRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? startPosition : transform.position, wanderRadius);
    }
}
