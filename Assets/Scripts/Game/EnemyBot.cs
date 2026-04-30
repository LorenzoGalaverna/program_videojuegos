using UnityEngine;
using UnityEngine.AI;

public class EnemyBot : MonoBehaviour
{
    [Header("Detección")]
    public float detectionRange = 30f;
    public float attackRange = 18f;
    public float fieldOfView = 120f;
    public float reactionTime = 0.3f;

    [Header("Combate")]
    public int damage = 15;
    public float fireRate = 0.4f;
    public float aimSmoothing = 6f;
    public float accuracy = 0.7f;

    [Header("Patrulla")]
    public float patrolRadius = 12f;
    public float patrolWaitTime = 2f;

    [Header("Referencias")]
    public Transform eyePoint;

    private NavMeshAgent agent;
    private PlayerHealth health;
    private Transform player;
    private PlayerHealth playerHealth;

    public enum State { Patrol, Chase, Attack, Dead }
    private State state = State.Patrol;

    private float nextFireTime;
    private float patrolWaitTimer;
    private float reactionTimer;
    private Vector3 lastKnownPlayerPos;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<PlayerHealth>();

        if (health) health.onDeath.AddListener(OnDeath);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (!agent.isOnNavMesh)
            Debug.LogError("[Bot] NavMeshAgent NOT on NavMesh! Bake the NavMesh first.");
        else
            Debug.Log($"[Bot] Ready. State: {state}. Position: {transform.position}");

        SetNewPatrolTarget();
    }

    void Update()
    {
        if (state == State.Dead || player == null || !agent.isOnNavMesh) return;

        UpdateStateTransitions();
        ExecuteCurrentState();
    }

    private void UpdateStateTransitions()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            ChangeState(State.Patrol);
            return;
        }

        bool canSee = CanSeePlayer();
        float dist = Vector3.Distance(transform.position, player.position);

        if (canSee)
        {
            lastKnownPlayerPos = player.position;

            if (dist <= attackRange)
                ChangeState(State.Attack);
            else if (dist <= detectionRange)
                ChangeState(State.Chase);
        }
        else
        {
            // Lost sight: chase to last known position, then back to patrol
            if (state == State.Attack || state == State.Chase)
            {
                if (Vector3.Distance(transform.position, lastKnownPlayerPos) < 2f)
                    ChangeState(State.Patrol);
                else
                    ChangeState(State.Chase);
            }
        }
    }

    private void ExecuteCurrentState()
    {
        switch (state)
        {
            case State.Patrol: DoPatrol(); break;
            case State.Chase: DoChase(); break;
            case State.Attack: DoAttack(); break;
        }
    }

    private void ChangeState(State newState)
    {
        if (state == newState) return;
        state = newState;

        if (newState == State.Attack)
        {
            agent.isStopped = true;
            reactionTimer = reactionTime;
        }
        else
        {
            agent.isStopped = false;
        }
    }

    // ─── PATROL ────────────────────────────────────────
    private void DoPatrol()
    {
        if (agent.remainingDistance < 1f && !agent.pathPending)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0)
                SetNewPatrolTarget();
        }
    }

    private void SetNewPatrolTarget()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir.y = 0;
        Vector3 target = transform.position + randomDir;

        if (NavMesh.SamplePosition(target, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        patrolWaitTimer = patrolWaitTime;
    }

    // ─── CHASE ────────────────────────────────────────
    private void DoChase()
    {
        // Move toward last known position of the player
        agent.SetDestination(lastKnownPlayerPos);
    }

    // ─── ATTACK ────────────────────────────────────────
    private void DoAttack()
    {
        // Aim at player
        Vector3 aimTarget = player.position + Vector3.up * 1.5f;
        Vector3 lookDir = aimTarget - transform.position;
        lookDir.y = 0;
        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), aimSmoothing * Time.deltaTime);

        // Reaction delay before first shot
        if (reactionTimer > 0)
        {
            reactionTimer -= Time.deltaTime;
            return;
        }

        // Fire at fire rate
        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void Shoot()
    {
        if (playerHealth == null || playerHealth.IsDead) return;

        Transform eye = eyePoint ? eyePoint : transform;
        Vector3 targetPos = player.position + Vector3.up * 1.5f;
        Vector3 aimDir = (targetPos - eye.position).normalized;

        // Inaccuracy
        float spread = (1f - accuracy) * 0.1f;
        aimDir += new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            Random.Range(-spread, spread)
        );

        if (Physics.Raycast(eye.position, aimDir.normalized, out RaycastHit hit, attackRange + 5f))
        {
            PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
            if (ph != null && ph == playerHealth)
            {
                bool headshot = hit.collider.CompareTag("Head");
                ph.TakeDamage(damage, headshot);
            }
        }
    }

    // ─── PERCEPTION ────────────────────────────────────
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > detectionRange) return false;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);
        // Always allow detection at very close range, regardless of FOV
        if (angle > fieldOfView / 2f && dist > 4f) return false;

        Transform eye = eyePoint ? eyePoint : transform;
        Vector3 targetPos = player.position + Vector3.up * 1.5f;
        Vector3 ray = (targetPos - eye.position);

        if (Physics.Raycast(eye.position, ray.normalized, out RaycastHit hit, ray.magnitude + 0.5f))
        {
            return hit.collider.CompareTag("Player") ||
                   hit.collider.GetComponentInParent<PlayerHealth>() == playerHealth;
        }
        return false;
    }

    // ─── DEATH / RESPAWN ───────────────────────────────
    private void OnDeath()
    {
        ChangeState(State.Dead);
        if (agent.isOnNavMesh) agent.isStopped = true;

        if (GameManager.Instance) GameManager.Instance.AddPlayerKill();

        Invoke(nameof(Respawn), 3f);
    }

    private void Respawn()
    {
        health.ResetHealth();
        if (GameManager.Instance) GameManager.Instance.RespawnPlayer(gameObject, 1);

        if (agent.isOnNavMesh) agent.isStopped = false;
        ChangeState(State.Patrol);
        SetNewPatrolTarget();
    }

    // ─── DEBUG ────────────────────────────────────────
    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public State CurrentState => state;
}
