using UnityEngine;
using UnityEngine.Events;

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

    public void RespawnPlayer(GameObject player, int team)
    {
        Transform spawn = GetSpawnPoint(team);
        CharacterController cc = player.GetComponent<CharacterController>();

        if (cc) cc.enabled = false;
        player.transform.position = spawn.position;
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
