using UnityEngine;

public class EnemyBot : MonoBehaviour
{
    [Header("Bot Settings")]
    public float detectionRange = 40f;
    public float fieldOfView = 120f;
    public float reactionTime = 0.3f;
    public float accuracy = 0.65f;

    [Header("Combat")]
    public int damage = 18;
    public float fireRate = 0.2f;
    public int burstLength = 4;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float patrolRadius = 12f;

    [Header("References")]
    public Transform eyePoint;

    private CharacterController cc;
    private PlayerHealth health;
    private Transform player;
    private PlayerHealth playerHealth;

    private enum BotState { Patrol, Chase, Attack, Dead }
    private BotState state = BotState.Patrol;

    private float nextFireTime;
    private float reactionTimer;
    private bool playerDetected;
    private Vector3 patrolTarget;
    private float patrolWaitTimer;
    private int burstCount;
    private float gravity = -9.8f;
    private float verticalVelocity;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();

        if (health)
            health.onDeath.AddListener(OnDeath);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        patrolTarget = transform.position;
        SetNewPatrolTarget();

        // Start in chase mode so the bot actively goes toward the player
        if (player != null) state = BotState.Chase;

        Debug.Log($"[Bot] Initialized at {transform.position} | Player: {(player != null ? player.position.ToString() : "NOT FOUND")}");
    }

    void Update()
    {
        if (state == BotState.Dead || player == null) return;

        CheckForPlayer();

        switch (state)
        {
            case BotState.Patrol: UpdatePatrol(); break;
            case BotState.Chase: UpdateChase(); break;
            case BotState.Attack: UpdateAttack(); break;
        }
    }

    private void CheckForPlayer()
    {
        if (playerHealth && playerHealth.IsDead)
        {
            playerDetected = false;
            if (state != BotState.Patrol) state = BotState.Patrol;
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        // Always detect if very close
        if (dist < 8f)
        {
            playerDetected = true;
            reactionTimer = 0;
            state = BotState.Attack;
            return;
        }

        if (dist > detectionRange)
        {
            if (playerDetected) { playerDetected = false; state = BotState.Chase; }
            return;
        }

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        if (angle > fieldOfView / 2f)
        {
            if (state == BotState.Attack) state = BotState.Chase;
            return;
        }

        // Line of sight
        Transform eye = eyePoint ? eyePoint : transform;
        Vector3 eyePos = eye.position;
        Vector3 targetPos = player.position + Vector3.up * 1.5f;

        if (Physics.Raycast(eyePos, (targetPos - eyePos).normalized, out RaycastHit hit, detectionRange))
        {
            bool hitPlayer = hit.collider.CompareTag("Player") ||
                             hit.collider.GetComponentInParent<PlayerHealth>() == playerHealth;
            if (hitPlayer)
            {
                if (!playerDetected)
                {
                    playerDetected = true;
                    reactionTimer = reactionTime;
                }
                reactionTimer -= Time.deltaTime;
                if (reactionTimer <= 0) state = BotState.Attack;
            }
            else
            {
                if (state == BotState.Attack) state = BotState.Chase;
            }
        }
    }

    private void UpdatePatrol()
    {
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(patrolTarget.x, 0, patrolTarget.z));

        if (dist < 2f)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0) SetNewPatrolTarget();
        }
        else
        {
            MoveToward(patrolTarget, true);
        }
    }

    private void UpdateChase()
    {
        MoveToward(player.position, true);

        if (Vector3.Distance(transform.position, player.position) < detectionRange * 0.6f)
        {
            // Re-check line of sight
            Transform eye = eyePoint ? eyePoint : transform;
            Vector3 dir = (player.position + Vector3.up * 1.5f - eye.position).normalized;
            if (Physics.Raycast(eye.position, dir, out RaycastHit hit, detectionRange))
            {
                if (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerHealth>() == playerHealth)
                    state = BotState.Attack;
            }
        }
    }

    private void UpdateAttack()
    {
        // Look at player
        Vector3 lookDir = player.position - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), 10f * Time.deltaTime);

        // Strafe and distance management
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist < 5f)
        {
            // Back up
            Vector3 away = (transform.position - player.position).normalized;
            MoveToward(transform.position + away * 5f, false);
        }
        else if (dist > 22f)
        {
            MoveToward(player.position, false);
        }
        else
        {
            // Strafe sideways
            Vector3 strafe = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized);
            float dir = Mathf.Sin(Time.time * 1.5f) > 0 ? 1f : -1f;
            MoveToward(transform.position + strafe * dir * 4f, false);
        }

        // Shoot
        if (Time.time >= nextFireTime)
        {
            Shoot();
            burstCount++;
            nextFireTime = Time.time + fireRate;

            if (burstCount >= burstLength)
            {
                burstCount = 0;
                nextFireTime = Time.time + fireRate * 4f; // pause between bursts
            }
        }
    }

    private void MoveToward(Vector3 target, bool faceDirection)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.5f) return;

        dir = dir.normalized;

        // Gravity
        if (cc.isGrounded)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = dir * moveSpeed + Vector3.up * verticalVelocity;
        cc.Move(move * Time.deltaTime);

        // Face direction
        if (faceDirection && dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 5f * Time.deltaTime);
    }

    private void Shoot()
    {
        if (playerHealth == null || playerHealth.IsDead) return;

        Transform eye = eyePoint ? eyePoint : transform;
        Vector3 targetPos = player.position + Vector3.up * 1.5f;
        Vector3 aimDir = (targetPos - eye.position).normalized;

        // Inaccuracy
        float missAmount = (1f - accuracy) * 0.12f;
        aimDir += new Vector3(
            Random.Range(-missAmount, missAmount),
            Random.Range(-missAmount, missAmount),
            Random.Range(-missAmount, missAmount)
        );

        if (Physics.Raycast(eye.position, aimDir.normalized, out RaycastHit hit, detectionRange))
        {
            PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null && ph == playerHealth)
            {
                bool headshot = hit.collider.CompareTag("Head");
                ph.TakeDamage(damage, headshot);
            }
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomOffset = new Vector3(
            Random.Range(-patrolRadius, patrolRadius),
            0,
            Random.Range(-patrolRadius, patrolRadius)
        );

        patrolTarget = transform.position + randomOffset;
        // Clamp to map bounds
        patrolTarget.x = Mathf.Clamp(patrolTarget.x, -22f, 22f);
        patrolTarget.z = Mathf.Clamp(patrolTarget.z, -22f, 22f);

        patrolWaitTimer = Random.Range(1f, 3f);
    }

    private void OnDeath()
    {
        state = BotState.Dead;

        if (GameManager.Instance)
            GameManager.Instance.AddPlayerKill();

        Invoke(nameof(Respawn), 3f);
    }

    private void Respawn()
    {
        state = BotState.Patrol;
        health.ResetHealth();

        if (GameManager.Instance)
            GameManager.Instance.RespawnPlayer(gameObject, 1);

        SetNewPatrolTarget();
    }
}
