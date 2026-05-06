using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

// Custom Mirror NetworkManager that handles the LAN flow for the OutdoorsScene.
//  - Builds the level via SceneSetup (skipping the offline player + bot)
//  - Spawns each new player at a different team spawn point (alternating)
//  - Tracks authoritative kill scores via SyncVars so both machines always agree
//  - Handles server-side respawn so the client-player can respawn even when not server
[RequireComponent(typeof(kcp2k.KcpTransport))]
public class NetworkLobby : NetworkManager
{
    [Tooltip("Reference to SceneSetup so we can build the level without spawning offline player/bot")]
    public SceneSetup sceneSetup;

    public static bool IsLanActive { get; private set; }

    private int spawnCounter = 0;

    // Ordered list of spawned player NetworkIdentities (index = spawn slot)
    private readonly List<NetworkIdentity> spawnedPlayers = new List<NetworkIdentity>();

    // Authoritative kill counts per spawn slot, replicated to every client
    [SyncVar(hook = nameof(OnScoreSync))] private int syncScore0; // kills by slot-0 (host)
    [SyncVar(hook = nameof(OnScoreSync))] private int syncScore1; // kills by slot-1 (joiner)

    public override void Awake()
    {
        base.Awake();
        var transport = GetComponent<kcp2k.KcpTransport>();
        if (transport != null)
        {
            Transport.active = transport;
            this.transport = transport;
        }
        networkAddress = "0.0.0.0";
    }

    public void StartHostMode()
    {
        BuildSceneIfNeeded();
        IsLanActive = true;
        networkAddress = "0.0.0.0";
        StartHost();
    }

    public void StartClientMode(string ipAddress)
    {
        BuildSceneIfNeeded();
        IsLanActive = true;
        networkAddress = string.IsNullOrEmpty(ipAddress) ? "localhost" : ipAddress;
        StartClient();
    }

    private void BuildSceneIfNeeded()
    {
        if (sceneSetup == null) sceneSetup = FindAnyObjectByType<SceneSetup>();
        if (sceneSetup == null) return;
        sceneSetup.buildPlayer = false;
        sceneSetup.buildBot    = false;
        sceneSetup.BuildScene();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        int slot = spawnCounter;
        Vector3    spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (GameManager.Instance != null)
        {
            int team = (slot % 2 == 0) ? 0 : 1;
            Transform sp = GameManager.Instance.GetSpawnPoint(team);
            if (sp != null) { spawnPos = sp.position; spawnRot = sp.rotation; }
        }

        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);

        // Tag the slot before AddPlayerForConnection so it travels in the spawn packet
        var np = player.GetComponent<NetworkedPlayer>();
        if (np != null) np.SpawnSlot = slot;

        NetworkServer.AddPlayerForConnection(conn, player);

        NetworkIdentity ni = player.GetComponent<NetworkIdentity>();
        if (ni != null) spawnedPlayers.Add(ni);

        spawnCounter++;
    }

    // ─── Score tracking ────────────────────────────────────────────────────────

    // Called server-side from PlayerHealth when a networked player's health hits zero.
    [Server]
    public void OnPlayerDied(NetworkIdentity dying)
    {
        int slot = spawnedPlayers.IndexOf(dying);
        if (slot == 0)      syncScore1++; // slot-0 died → slot-1 scored
        else if (slot == 1) syncScore0++; // slot-1 died → slot-0 scored
        else return;                      // untracked object (shouldn't happen)

        if (GameManager.Instance != null &&
            (syncScore0 >= GameManager.Instance.killsToWin || syncScore1 >= GameManager.Instance.killsToWin))
        {
            RpcEndGame(syncScore0, syncScore1);
        }

        StartCoroutine(ServerRespawnPlayer(dying, 3f));
    }

    [Server]
    private IEnumerator ServerRespawnPlayer(NetworkIdentity player, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (GameManager.Instance == null || !GameManager.Instance.GameActive) yield break;

        PlayerHealth    ph = player.GetComponent<PlayerHealth>();
        NetworkedPlayer np = player.GetComponent<NetworkedPlayer>();
        if (ph == null || np == null) yield break;

        ph.ResetHealth();

        int slot = spawnedPlayers.IndexOf(player);
        int team = slot >= 0 ? slot % 2 : 0;
        Transform sp = GameManager.Instance.GetSpawnPoint(team);
        if (sp != null) np.RpcRespawnAt(sp.position, sp.rotation);
    }

    // SyncVar hook — fires on all clients whenever either score SyncVar changes.
    private void OnScoreSync(int old, int newVal) => ApplyScoresToGameManager();

    private void ApplyScoresToGameManager()
    {
        if (GameManager.Instance == null) return;

        int mySlot = -1;
        foreach (var np in FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None))
        {
            if (np.isLocalPlayer) { mySlot = np.SpawnSlot; break; }
        }
        if (mySlot < 0) return;

        int myKills    = mySlot == 0 ? syncScore0 : syncScore1;
        int enemyKills = mySlot == 0 ? syncScore1 : syncScore0;
        GameManager.Instance.SyncNetworkScores(myKills, enemyKills);
    }

    // Delivers final authoritative scores to all clients and triggers the end-game screen.
    [ClientRpc]
    private void RpcEndGame(int score0, int score1)
    {
        if (GameManager.Instance == null) return;

        // Apply final scores before ending (SyncVar propagation may still be in flight)
        int mySlot = -1;
        foreach (var np in FindObjectsByType<NetworkedPlayer>(FindObjectsSortMode.None))
        {
            if (np.isLocalPlayer) { mySlot = np.SpawnSlot; break; }
        }
        if (mySlot >= 0)
        {
            int myKills    = mySlot == 0 ? score0 : score1;
            int enemyKills = mySlot == 0 ? score1 : score0;
            GameManager.Instance.SyncNetworkScores(myKills, enemyKills);
        }
        GameManager.Instance.EndGame();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    public static List<string> GetLocalIPs()
    {
        var list = new List<string>();
        foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                list.Add(ip.ToString());
        }
        return list;
    }
}
