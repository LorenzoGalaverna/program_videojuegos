using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

// Custom Mirror NetworkManager that handles the LAN flow for the OutdoorsScene.
//  - Builds the level via SceneSetup (skipping the offline player + bot)
//  - Spawns each new player at a different team spawn point (alternating)
//  - Tracks authoritative kill scores server-side and delivers them per-player via
//    NetworkedPlayer.RpcReceiveScores / RpcEndGame (Mirror Weaver forbids SyncVars and
//    ClientRpc inside NetworkManager subclasses, so we delegate to NetworkedPlayer)
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

    // Server-only authoritative kill counts: scores[0] = kills by slot-0, scores[1] = kills by slot-1
    private readonly int[] scores = new int[2];

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
            // Snap onto the real map floor — the Dust2 spawn anchors are hand-tuned and
            // don't always sit on geometry, which otherwise makes the player fall off the map.
            if (sp != null) { spawnPos = GameManager.SnapToGround(sp.position); spawnRot = sp.rotation; }

            // TEMP DIAGNOSTIC — remove once spawning is confirmed working.
            Debug.Log($"[NetworkLobby] Spawn slot {slot}: anchor {(sp != null ? sp.position.ToString() : "NULL")} " +
                      $"-> snapped {spawnPos} | map Y {GameManager.MapBottomY:F1}..{GameManager.MapTopY:F1}");
        }
        else
        {
            Debug.LogWarning("[NetworkLobby] OnServerAddPlayer: GameManager.Instance is NULL — player spawns at origin and will fall.");
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
        if (slot == 0)      scores[1]++; // slot-0 died → slot-1 scored
        else if (slot == 1) scores[0]++; // slot-1 died → slot-0 scored
        else return;

        bool gameOver = GameManager.Instance != null &&
                        (scores[0] >= GameManager.Instance.killsToWin ||
                         scores[1] >= GameManager.Instance.killsToWin);

        if (gameOver)
        {
            BroadcastEndGame();
        }
        else
        {
            BroadcastScores();
            StartCoroutine(ServerRespawnPlayer(dying, 3f));
        }
    }

    // Push updated kill counts to each player from their own perspective.
    private void BroadcastScores()
    {
        for (int i = 0; i < spawnedPlayers.Count && i < 2; i++)
        {
            var np = spawnedPlayers[i].GetComponent<NetworkedPlayer>();
            if (np == null) continue;
            np.RpcReceiveScores(scores[i], scores[1 - i]);
        }
    }

    // Push final scores and trigger end-game screen on every client.
    private void BroadcastEndGame()
    {
        for (int i = 0; i < spawnedPlayers.Count && i < 2; i++)
        {
            var np = spawnedPlayers[i].GetComponent<NetworkedPlayer>();
            if (np == null) continue;
            np.RpcEndGame(scores[i], scores[1 - i]);
        }
    }

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
        if (sp != null)
        {
            // Snap onto the real map floor (same reason as the initial spawn) so the
            // player doesn't fall off the map after respawning.
            Vector3 respawnPos = GameManager.SnapToGround(sp.position);

            // Reset position on the server's authoritative transform first so
            // NetworkTransform's next replication won't push the burrowed corpse
            // position back out to clients.
            player.transform.position = respawnPos;
            player.transform.rotation = sp.rotation;
            np.RpcRespawnAt(respawnPos, sp.rotation);
        }
    }

    // Cleanly tear down whatever LAN role we're running (host / server / client) and
    // clear the LAN flag so the next game starts fresh. Safe to call from the pause menu.
    public void StopLan()
    {
        if (NetworkServer.active && NetworkClient.isConnected) StopHost();
        else if (NetworkClient.active)                          StopClient();
        else if (NetworkServer.active)                          StopServer();

        IsLanActive = false;
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
