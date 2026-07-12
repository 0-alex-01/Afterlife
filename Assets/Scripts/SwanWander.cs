using UnityEngine;

public class SwanLakeAI : MonoBehaviour
{
    public enum SwanState
    {
        Idle,
        Swim,
        Flee
    }

    public enum ModelForwardAxis
    {
        ZForward,
        ZBackward,
        XRight,
        XLeft
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Animation State")]
    [SerializeField] private string swimStateName = "Armature|Swan_idle";
    [SerializeField] private float normalAnimationSpeed = 1f;
    [SerializeField] private float fleeAnimationSpeed = 1.5f;

    [Header("Flee Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] fleeSounds;
    [SerializeField] private float fleeSoundVolume = 1f;
    [SerializeField] private float minSoundCooldown = 2f;
    [SerializeField] private bool debugAudio = true;

    [Header("3D Audio Settings")]
    [SerializeField] private float audioMinDistance = 1f;
    [SerializeField] private float audioMaxDistance = 25f;

    [Header("Player Detection")]
    [SerializeField] private float fleeRadius = 4f;
    [SerializeField] private float calmRadius = 7f;

    [Header("Movement")]
    [SerializeField] private float swimSpeed = 0.25f;
    [SerializeField] private float fleeSpeed = 1.2f;
    [SerializeField] private float turnSpeed = 2.5f;
    [SerializeField] private float targetReachDistance = 0.4f;

    [Header("Anti-Knubbel / Separation")]
    [SerializeField] private bool avoidOtherSwans = true;
    [SerializeField] private float separationRadius = 2.5f;
    [SerializeField] private float separationStrength = 2f;

    [Header("Lake Area")]
    [SerializeField] private Vector2 xRange = new Vector2(-20f, 25f);
    [SerializeField] private Vector2 zRange = new Vector2(-15f, 35f);

    [Header("Island / Forbidden Zone")]
    [SerializeField] private Transform forbiddenZoneCenter;
    [SerializeField] private float forbiddenZoneRadius = 5f;
    [SerializeField] private float forbiddenZoneAvoidStrength = 3f;

    [Header("Water Height")]
    [SerializeField] private float waterY = 4f;
    [SerializeField] private float heightOffset = 0.2f;

    [Header("Idle / Swim Timing")]
    [SerializeField] private Vector2 idleTimeRange = new Vector2(1.5f, 4f);
    [SerializeField] private Vector2 swimTimeRange = new Vector2(3f, 7f);
    [SerializeField] private float randomStartDelay = 2f;

    [Header("Model Direction Fix")]
    [SerializeField] private ModelForwardAxis modelForwardAxis = ModelForwardAxis.ZForward;
    [SerializeField] private float extraYawOffset = 0f;

    private SwanState currentState;
    private Vector3 targetPosition;
    private float stateTimer;
    private float lastFleeSoundTime = -999f;
    private bool aiStarted = false;

    private SwanLakeAI[] allSwans;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (player == null)
        {
            GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);

            if (foundPlayer != null)
            {
                player = foundPlayer.transform;
            }
            else
            {
                Debug.LogWarning($"{name}: Kein Player mit Tag '{playerTag}' gefunden. Zieh den Player manuell ins Feld oder gib ihm den Tag Player.");
            }
        }

        SetupAudioSource();
    }

    private void Start()
    {
        KeepOnWater();

        // Erst in Start suchen, damit alle Schwäne in der Szene bereits existieren.
        allSwans = FindObjectsByType<SwanLakeAI>(FindObjectsSortMode.None);

        float delay = Random.Range(0f, randomStartDelay);
        Invoke(nameof(StartAI), delay);
    }

    private void Update()
    {
        KeepOnWater();

        if (!aiStarted)
            return;

        CheckPlayerDistance();

        switch (currentState)
        {
            case SwanState.Idle:
                UpdateIdle();
                break;

            case SwanState.Swim:
                UpdateSwim();
                break;

            case SwanState.Flee:
                UpdateFlee();
                break;
        }
    }

    private void StartAI()
    {
        aiStarted = true;
        EnterIdle();
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = audioMinDistance;
        audioSource.maxDistance = audioMaxDistance;
        audioSource.volume = fleeSoundVolume;
    }

    private void CheckPlayerDistance()
    {
        if (player == null)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= fleeRadius)
        {
            EnterFlee();
        }
        else if (currentState == SwanState.Flee && distanceToPlayer >= calmRadius)
        {
            EnterIdle();
        }
    }

    private void EnterIdle()
    {
        currentState = SwanState.Idle;
        stateTimer = Random.Range(idleTimeRange.x, idleTimeRange.y);

        PlaySwimAnimation(normalAnimationSpeed);
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            EnterSwim();
        }
    }

    private void EnterSwim()
    {
        currentState = SwanState.Swim;
        stateTimer = Random.Range(swimTimeRange.x, swimTimeRange.y);

        PickNewLakeTarget();
        PlaySwimAnimation(normalAnimationSpeed);
    }

    private void UpdateSwim()
    {
        stateTimer -= Time.deltaTime;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;

        if (direction.magnitude < targetReachDistance || stateTimer <= 0f)
        {
            EnterIdle();
            return;
        }

        direction = direction.normalized;

        if (avoidOtherSwans)
            direction = ApplySeparation(direction);

        direction = ApplyForbiddenZoneAvoidance(direction);

        MoveInDirection(direction.normalized, swimSpeed);
    }

    private void EnterFlee()
    {
        if (currentState == SwanState.Flee)
            return;

        currentState = SwanState.Flee;

        PlaySwimAnimation(fleeAnimationSpeed);
        PlayFleeSound();

        if (debugAudio)
            Debug.Log($"{name}: EnterFlee wurde ausgelöst.");
    }

    private void UpdateFlee()
    {
        if (player == null)
            return;

        Vector3 directionAway = transform.position - player.position;
        directionAway.y = 0f;

        if (directionAway.magnitude < 0.01f)
            directionAway = transform.forward;

        directionAway = directionAway.normalized;

        if (avoidOtherSwans)
            directionAway = ApplySeparation(directionAway);

        directionAway = ApplyForbiddenZoneAvoidance(directionAway);

        MoveInDirection(directionAway.normalized, fleeSpeed);
    }

    private Vector3 ApplySeparation(Vector3 desiredDirection)
    {
        if (allSwans == null || allSwans.Length == 0)
            return desiredDirection;

        Vector3 separationDirection = Vector3.zero;
        int nearbyCount = 0;

        foreach (SwanLakeAI otherSwan in allSwans)
        {
            if (otherSwan == null || otherSwan == this)
                continue;

            Vector3 awayFromOther = transform.position - otherSwan.transform.position;
            awayFromOther.y = 0f;

            float distance = awayFromOther.magnitude;

            if (distance > 0.01f && distance < separationRadius)
            {
                // Je näher der andere Schwan ist, desto stärker wird weggedrückt.
                separationDirection += awayFromOther.normalized / distance;
                nearbyCount++;
            }
        }

        if (nearbyCount == 0)
            return desiredDirection;

        separationDirection /= nearbyCount;

        Vector3 finalDirection =
            desiredDirection + separationDirection.normalized * separationStrength;

        finalDirection.y = 0f;

        if (finalDirection.magnitude < 0.01f)
            return desiredDirection;

        return finalDirection.normalized;
    }

    private Vector3 ApplyForbiddenZoneAvoidance(Vector3 desiredDirection)
    {
        if (forbiddenZoneCenter == null)
            return desiredDirection;

        Vector3 awayFromForbiddenZone = transform.position - forbiddenZoneCenter.position;
        awayFromForbiddenZone.y = 0f;

        float distance = awayFromForbiddenZone.magnitude;

        if (distance > forbiddenZoneRadius)
            return desiredDirection;

        if (distance < 0.01f)
            awayFromForbiddenZone = transform.forward;

        float dangerFactor = 1f - Mathf.Clamp01(distance / forbiddenZoneRadius);

        Vector3 avoidanceDirection =
            awayFromForbiddenZone.normalized * dangerFactor * forbiddenZoneAvoidStrength;

        Vector3 finalDirection = desiredDirection + avoidanceDirection;
        finalDirection.y = 0f;

        if (finalDirection.magnitude < 0.01f)
            return desiredDirection;

        return finalDirection.normalized;
    }

    private void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction, Vector3.up)
            * GetAxisCorrection()
            * Quaternion.Euler(0f, extraYawOffset, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        transform.position += direction * speed * Time.deltaTime;

        ClampToLakeArea();
        KeepOnWater();
    }

    private void PickNewLakeTarget()
    {
        int maxAttempts = 40;

        for (int i = 0; i < maxAttempts; i++)
        {
            float randomX = Random.Range(xRange.x, xRange.y);
            float randomZ = Random.Range(zRange.x, zRange.y);

            Vector3 possibleTarget = new Vector3(
                randomX,
                waterY + heightOffset,
                randomZ
            );

            if (!IsInsideForbiddenZone(possibleTarget))
            {
                targetPosition = possibleTarget;
                return;
            }
        }

        // Fallback, falls zufällig kein gültiges Ziel gefunden wurde.
        targetPosition = transform.position;
    }

    private bool IsInsideForbiddenZone(Vector3 position)
    {
        if (forbiddenZoneCenter == null)
            return false;

        Vector3 flatPosition = new Vector3(position.x, 0f, position.z);

        Vector3 flatForbiddenZone = new Vector3(
            forbiddenZoneCenter.position.x,
            0f,
            forbiddenZoneCenter.position.z
        );

        float distance = Vector3.Distance(flatPosition, flatForbiddenZone);

        return distance < forbiddenZoneRadius;
    }

    private void ClampToLakeArea()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, xRange.x, xRange.y);
        pos.z = Mathf.Clamp(pos.z, zRange.x, zRange.y);

        transform.position = pos;
    }

    private void KeepOnWater()
    {
        transform.position = new Vector3(
            transform.position.x,
            waterY + heightOffset,
            transform.position.z
        );
    }

    private void PlaySwimAnimation(float speed)
    {
        if (animator == null || string.IsNullOrEmpty(swimStateName))
            return;

        animator.speed = speed;

        float randomStart = Random.Range(0f, 1f);
        animator.CrossFade(swimStateName, 0.2f, 0, randomStart);
    }

    private void PlayFleeSound()
    {
        if (audioSource == null)
        {
            Debug.LogWarning($"{name}: Keine AudioSource gefunden.");
            return;
        }

        if (fleeSounds == null || fleeSounds.Length == 0)
        {
            Debug.LogWarning($"{name}: Keine Flee Sounds eingetragen.");
            return;
        }

        if (Time.time - lastFleeSoundTime < minSoundCooldown)
            return;

        AudioClip randomClip = fleeSounds[Random.Range(0, fleeSounds.Length)];

        if (randomClip == null)
        {
            Debug.LogWarning($"{name}: Ein Flee Sound Element ist leer/null.");
            return;
        }

        audioSource.pitch = Random.Range(0.95f, 1.08f);
        audioSource.PlayOneShot(randomClip, fleeSoundVolume);

        lastFleeSoundTime = Time.time;

        if (debugAudio)
            Debug.Log($"{name}: Spiele Flee Sound: {randomClip.name}");
    }

    private Quaternion GetAxisCorrection()
    {
        Vector3 modelAxis = Vector3.forward;

        switch (modelForwardAxis)
        {
            case ModelForwardAxis.ZForward:
                modelAxis = Vector3.forward;
                break;

            case ModelForwardAxis.ZBackward:
                modelAxis = Vector3.back;
                break;

            case ModelForwardAxis.XRight:
                modelAxis = Vector3.right;
                break;

            case ModelForwardAxis.XLeft:
                modelAxis = Vector3.left;
                break;
        }

        return Quaternion.FromToRotation(modelAxis, Vector3.forward);
    }

    private void OnDrawGizmosSelected()
    {
        // Fluchtbereich
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, fleeRadius);

        // Beruhigungsbereich
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, calmRadius);

        // Anti-Knubbel-Bereich
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        // See-Bereich
        Gizmos.color = Color.cyan;

        Vector3 lakeCenter = new Vector3(
            (xRange.x + xRange.y) / 2f,
            waterY + heightOffset,
            (zRange.x + zRange.y) / 2f
        );

        Vector3 lakeSize = new Vector3(
            Mathf.Abs(xRange.y - xRange.x),
            0.05f,
            Mathf.Abs(zRange.y - zRange.x)
        );

        Gizmos.DrawWireCube(lakeCenter, lakeSize);

        // Insel / Forbidden Zone
        if (forbiddenZoneCenter != null)
        {
            Gizmos.color = Color.red;

            Vector3 forbiddenCenter = new Vector3(
                forbiddenZoneCenter.position.x,
                waterY + heightOffset,
                forbiddenZoneCenter.position.z
            );

            Gizmos.DrawWireSphere(forbiddenCenter, forbiddenZoneRadius);
        }
    }
}