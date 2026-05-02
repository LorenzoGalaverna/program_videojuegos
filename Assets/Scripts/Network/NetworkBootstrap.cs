using UnityEngine;
using Mirror;

// Phase 1 LAN smoke test. Builds a minimal scene at runtime and uses Mirror to
// host/join players. The player prefab MUST be an actual prefab asset in the
// project (Mirror requires an assetId, which runtime-created GameObjects don't have).
[RequireComponent(typeof(kcp2k.KcpTransport))]
[RequireComponent(typeof(NetworkManager))]
public class NetworkBootstrap : MonoBehaviour
{
    [Tooltip("Drag the NetworkedPlayer prefab here (created in Assets/Prefabs/)")]
    public GameObject playerPrefab;

    private NetworkManager nm;
    private string ipAddress = "localhost";

    void Awake()
    {
        // Remove the default Main Camera / AudioListener that the empty scene template
        // creates — players spawn their own per-client camera, so the scene's one
        // would just be a duplicate AudioListener and a wrong viewpoint.
        foreach (var existing in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Destroy(existing.gameObject);
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            Destroy(listener);

        BuildMinimalLevel();

        // Components are required via [RequireComponent] so they exist before Awake.
        // The transport must be set before NetworkManager initializes.
        var transport = GetComponent<kcp2k.KcpTransport>();
        Transport.active = transport;

        nm = GetComponent<NetworkManager>();
        nm.transport = transport;
        nm.networkAddress = "0.0.0.0";

        if (playerPrefab == null)
            Debug.LogError("[NetworkBootstrap] Player Prefab is NOT assigned! Drag NetworkedPlayer.prefab into the slot.");
        else
            nm.playerPrefab = playerPrefab;
    }

    void Start()
    {
        Debug.Log($"[Network] Local IPs: {string.Join(", ", GetLocalIPs())}");
    }

    private void BuildMinimalLevel()
    {
        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(5, 1, 5);

        // A few boxes for cover
        for (int i = 0; i < 6; i++)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"Box_{i}";
            box.transform.position = new Vector3(Random.Range(-15f, 15f), 1, Random.Range(-15f, 15f));
            box.transform.localScale = new Vector3(2, 2, 2);
        }

        // Light
        GameObject lightGO = new GameObject("Sun");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    void OnGUI()
    {
        if (NetworkServer.active || NetworkClient.isConnected) return;

        const float w = 280, h = 200;
        GUILayout.BeginArea(new Rect(20, 20, w, h), GUI.skin.box);
        GUILayout.Label("LAN Test (Phase 1)");
        if (GUILayout.Button("Host (LAN)"))
        {
            nm.networkAddress = "0.0.0.0";
            nm.StartHost();
        }
        GUILayout.Space(8);
        GUILayout.Label("Host IP:");
        ipAddress = GUILayout.TextField(ipAddress);
        if (GUILayout.Button("Join"))
        {
            nm.networkAddress = ipAddress;
            nm.StartClient();
        }
        GUILayout.Space(8);
        GUILayout.Label("Your IPs:\n" + string.Join("\n", GetLocalIPs()));
        GUILayout.EndArea();
    }

    private static System.Collections.Generic.List<string> GetLocalIPs()
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
