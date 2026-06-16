using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public GameMode gameMode = GameMode.Deathmatch;
    public int killsToWin = 10;
    public float roundTime = 180f; // 3 minutes

    [Header("Spawn Points")]
    public Transform[] spawnPointsTeamA;
    public Transform[] spawnPointsTeamB;

    [Header("Events")]
    public UnityEvent<int, int> onScoreChanged = new UnityEvent<int, int>();
    public UnityEvent<string> onGameMessage = new UnityEvent<string>();
    public UnityEvent onGameOver = new UnityEvent();

    private int playerScore;
    private int enemyScore;
    private float currentTime;
    private bool gameActive;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // Initialize immediately so HUD reads correct values from frame 1
        StartGame();
    }

    void Update()
    {
        if (!gameActive) return;

        currentTime -= Time.deltaTime;
        if (currentTime <= 0)
        {
            currentTime = 0;
            EndGame();
        }
    }

    public void StartGame()
    {
        playerScore = 0;
        enemyScore = 0;
        currentTime = roundTime;
        gameActive = true;
        onScoreChanged?.Invoke(playerScore, enemyScore);
        onGameMessage?.Invoke("GAME START!");
    }

    public void AddPlayerKill()
    {
        if (!gameActive) return;
        playerScore++;
        onScoreChanged?.Invoke(playerScore, enemyScore);

        if (playerScore >= killsToWin)
            EndGame();
    }

    public void AddEnemyKill()
    {
        if (!gameActive) return;
        enemyScore++;
        onScoreChanged?.Invoke(playerScore, enemyScore);

        if (enemyScore >= killsToWin)
            EndGame();
    }

    // Called by NetworkLobby in LAN mode to push authoritative scores from the server.
    // Does not re-check win conditions — NetworkLobby handles that via RpcEndGame.
    public void SyncNetworkScores(int myKills, int enemyKills)
    {
        playerScore = myKills;
        enemyScore  = enemyKills;
        onScoreChanged?.Invoke(playerScore, enemyScore);
    }

    public void EndGame()
    {
        gameActive = false;
        onGameOver?.Invoke();

        // Single message — no duplicates from AddPlayerKill/AddEnemyKill
        if (playerScore > enemyScore)
            onGameMessage?.Invoke("VICTORIA!");
        else if (enemyScore > playerScore)
            onGameMessage?.Invoke("DERROTA!");
        else
            onGameMessage?.Invoke("EMPATE!");
    }

    public Transform GetSpawnPoint(int team)
    {
        Transform[] spawns = team == 0 ? spawnPointsTeamA : spawnPointsTeamB;
        if (spawns == null || spawns.Length == 0) return transform;
        return spawns[Random.Range(0, spawns.Length)];
    }

    // World-space vertical span of the built map. SnapToGround casts its floor-finding
    // ray from above the whole map down through it, so the snap works regardless of how
    // the imported map is scaled/rotated/offset. SceneSetup.BuildCustomMap fills these in
    // from the map's renderer bounds; the wide defaults cover the procedural map.
    public static float MapTopY    =  500f;
    public static float MapBottomY = -500f;

    // Drops a spawn position straight down onto the floor the anchor was meant to sit on.
    // The custom (Dust2) map's spawn anchors are hand-tuned and don't always sit exactly
    // on the imported geometry, so without this the player free-falls off the map at spawn.
    // Casts from above the entire map (using MapTopY/MapBottomY) so the ray can't fall
    // short on a large/scaled map. Skips player/bot colliders and triggers, and returns the
    // original position unchanged only if nothing solid is found in the column at all.
    public static Vector3 SnapToGround(Vector3 pos)
    {
        const float footClearance = 0.1f; // lift the CharacterController's feet just off the surface
        const float tolerance     = 1f;   // surfaces within 1m above the anchor still count as "the floor"

        // Span the whole map vertically (plus the anchor itself, in case it sits outside).
        float top    = Mathf.Max(MapTopY, pos.y) + 5f;
        float bottom = Mathf.Min(MapBottomY, pos.y) - 5f;
        Vector3 start = new Vector3(pos.x, top, pos.z);

        RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, top - bottom, ~0, QueryTriggerInteraction.Ignore);

        // Prefer the highest solid surface at/just-below the anchor (the floor under the
        // feet); if the anchor sits just under the geometry, fall back to the lowest
        // surface above it. Never snap onto a player/bot collider.
        float bestBelow = float.NegativeInfinity; bool foundBelow = false;
        float bestAbove = float.PositiveInfinity; bool foundAbove = false;

        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;

            float y = h.point.y;
            if (y <= pos.y + tolerance) { if (y > bestBelow) { bestBelow = y; foundBelow = true; } }
            else                        { if (y < bestAbove) { bestAbove = y; foundAbove = true; } }
        }

        if (foundBelow) return new Vector3(pos.x, bestBelow + footClearance, pos.z);
        if (foundAbove) return new Vector3(pos.x, bestAbove + footClearance, pos.z);

        // Nothing solid in the column at the anchor's XZ — the anchor is off the geometry.
        // Snap to the nearest walkable NavMesh point (the same floor the bot pathfinds on),
        // which guarantees a valid spawn even when the anchor is misplaced horizontally.
        if (NavMesh.SamplePosition(pos, out NavMeshHit nav, 50f, NavMesh.AllAreas))
            return nav.position + Vector3.up * footClearance;

        Debug.LogWarning(
            $"[GameManager] SnapToGround found no floor for spawn {pos} " +
            $"(map Y range {MapBottomY:F1}..{MapTopY:F1}, {hits.Length} ray hits, no NavMesh nearby). " +
            $"Player will fall — move the Custom Map Spawn A/B anchors so they sit over the map.");
        return pos;
    }

    public void RespawnPlayer(GameObject player, int team)
    {
        Transform spawn = GetSpawnPoint(team);
        CharacterController cc = player.GetComponent<CharacterController>();

        if (cc) cc.enabled = false;
        player.transform.position = SnapToGround(spawn.position);
        player.transform.rotation = spawn.rotation;
        if (cc) cc.enabled = true;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health) health.ResetHealth();

        // Reset camera vertical look (so the player doesn't respawn looking at the sky)
        MouseLook ml = player.GetComponentInChildren<MouseLook>();
        if (ml) ml.ResetLook();
    }

    public float CurrentTime => currentTime;
    public bool GameActive => gameActive;
    public int PlayerScore => playerScore;
    public int EnemyScore => enemyScore;
}

public enum GameMode
{
    Deathmatch,
    RoundBased
}
