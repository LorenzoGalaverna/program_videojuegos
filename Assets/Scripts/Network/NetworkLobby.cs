using UnityEngine;
using Mirror;

// Coordinates the LAN flow inside the real OutdoorsScene:
//  - Builds the level via SceneSetup (skipping player + bot — Mirror spawns players)
//  - Hosts a Mirror server, or joins one as a client
//  - Single source of truth for "are we currently in LAN mode?"
[RequireComponent(typeof(kcp2k.KcpTransport))]
[RequireComponent(typeof(NetworkManager))]
public class NetworkLobby : MonoBehaviour
{
    [Tooltip("Drag the NetworkedPlayer prefab from Assets/Prefabs/")]
    public GameObject playerPrefab;

    [Tooltip("Reference to SceneSetup so we can build the level without spawning offline player/bot")]
    public SceneSetup sceneSetup;

    private NetworkManager nm;
    public static bool IsLanActive { get; private set; }

    void Awake()
    {
        var transport = GetComponent<kcp2k.KcpTransport>();
        Transport.active = transport;

        nm = GetComponent<NetworkManager>();
        nm.transport = transport;
        nm.networkAddress = "0.0.0.0";

        if (playerPrefab != null) nm.playerPrefab = playerPrefab;
        else Debug.LogError("[NetworkLobby] Player Prefab not assigned. Networked spawning will fail.");
    }

    public void StartHostMode()
    {
        if (sceneSetup == null) sceneSetup = FindAnyObjectByType<SceneSetup>();
        if (sceneSetup != null)
        {
            // LAN: we don't want the offline player/bot — Mirror handles players,
            // and the bot is offline-only by design.
            sceneSetup.buildPlayer = false;
            sceneSetup.buildBot = false;
            sceneSetup.BuildScene();
        }

        IsLanActive = true;
        nm.networkAddress = "0.0.0.0";
        nm.StartHost();
    }

    public void StartClientMode(string ipAddress)
    {
        if (sceneSetup == null) sceneSetup = FindAnyObjectByType<SceneSetup>();
        if (sceneSetup != null)
        {
            sceneSetup.buildPlayer = false;
            sceneSetup.buildBot = false;
            sceneSetup.BuildScene();
        }

        IsLanActive = true;
        nm.networkAddress = string.IsNullOrEmpty(ipAddress) ? "localhost" : ipAddress;
        nm.StartClient();
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
