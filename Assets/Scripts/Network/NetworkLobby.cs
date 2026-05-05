using UnityEngine;
using Mirror;

// Custom Mirror NetworkManager that handles the LAN flow for the OutdoorsScene.
//  - Builds the level via SceneSetup (skipping the offline player + bot)
//  - Spawns each new player at a different team spawn point (alternating)
//  - Acts as the host/client controller invoked by MainMenu
[RequireComponent(typeof(kcp2k.KcpTransport))]
public class NetworkLobby : NetworkManager
{
    [Tooltip("Reference to SceneSetup so we can build the level without spawning offline player/bot")]
    public SceneSetup sceneSetup;

    public static bool IsLanActive { get; private set; }

    private int spawnCounter = 0;

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
        sceneSetup.buildBot = false;
        sceneSetup.BuildScene();
    }

    // Server picks alternating spawn points so two players don't pile on top of each
    // other. Even-indexed players go to Team A spawns, odd-indexed to Team B.
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        if (GameManager.Instance != null)
        {
            int team = (spawnCounter % 2 == 0) ? 0 : 1;
            Transform sp = GameManager.Instance.GetSpawnPoint(team);
            if (sp != null) { spawnPos = sp.position; spawnRot = sp.rotation; }
        }
        spawnCounter++;

        GameObject player = Instantiate(playerPrefab, spawnPos, spawnRot);
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public static System.Collections.Generic.List<string> GetLocalIPs()
    {
        var list = new System.Collections.Generic.List<string>();
        foreach (var ip in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                list.Add(ip.ToString());
        }
        return list;
    }
}
