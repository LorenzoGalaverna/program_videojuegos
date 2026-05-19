using UnityEngine;

[DefaultExecutionOrder(-100)]
public class FPSBootstrap : MonoBehaviour
{
    [Header("Quick Settings")]
    public float mouseSensitivity = 2f;
    public int targetFrameRate = 120;
    public bool showDebugInfo = true;

    [Header("Modelos custom (arrastrá FBX desde Assets/Models)")]
    public GameObject pistolPrefab;
    public GameObject riflePrefab;
    public GameObject sniperPrefab;
    public GameObject knifePrefab;
    public GameObject botBodyPrefab;

    [Header("Ajustes de los modelos custom")]
    [Tooltip("Escala del modelo del arma (probá 0.3 a 2)")]
    public float weaponPrefabScale = 1f;
    [Tooltip("Posición del arma respecto a la mano (mover si está mal ubicada)")]
    public Vector3 weaponPrefabOffset = Vector3.zero;
    [Tooltip("Rotación del arma en grados (algunos vienen rotados 90/180)")]
    public Vector3 weaponPrefabRotation = Vector3.zero;
    [Tooltip("Escala del bot (1 = tamaño normal)")]
    public float botPrefabScale = 1f;
    [Tooltip("Posición del arma del bot respecto a la mano derecha")]
    public Vector3 botGunOffset = new Vector3(0.05f, 0f, 0.08f);
    [Tooltip("Rotación del arma del bot en grados (ajustar si apunta al piso/cielo)")]
    public Vector3 botGunRotation = new Vector3(0f, 90f, 90f);
    [Tooltip("Multiplicador de escala del arma del bot (1 = igual al jugador, 2 = doble)")]
    public float botGunScaleMultiplier = 1f;
    [Tooltip("Posición del muzzle (donde salen las balas) respecto al arma")]
    public Vector3 muzzleLocalOffset = new Vector3(0f, 0f, 0.5f);

    [Header("Ajustes específicos del cuchillo")]
    [Tooltip("Multiplicador de escala SOLO para el cuchillo (1 = igual que las otras armas)")]
    public float knifeScaleMultiplier = 1f;
    [Tooltip("Rotación EXTRA SOLO para el cuchillo (se suma a Weapon Prefab Rotation)")]
    public Vector3 knifeExtraRotation = Vector3.zero;
    [Tooltip("Offset EXTRA SOLO para el cuchillo (se suma a Weapon Prefab Offset)")]
    public Vector3 knifeExtraOffset = Vector3.zero;

    [Header("Mapa custom (ej. de_dust2)")]
    [Tooltip("Arrastrá el modelo del mapa (Assets/Maps/Dust2/de_dust2). Si se asigna y el jugador elige 'Dust2' en el menú, se usa este en lugar del mapa procedural.")]
    public GameObject customMapPrefab;
    [Tooltip("Escala del mapa custom. El OBJ de dust2 viene en unidades de Source Engine (~1 unidad = 0.0254m), así que probá 0.02 a 0.05.")]
    public float customMapScale = 0.025f;
    [Tooltip("Offset de posición del mapa custom")]
    public Vector3 customMapOffset = Vector3.zero;
    [Tooltip("Rotación del mapa custom en grados (algunos OBJ vienen con Z arriba; probá (-90, 0, 0) si está acostado)")]
    public Vector3 customMapEuler = new Vector3(-90, 0, 0);
    [Tooltip("Posición base del spawn del equipo A (CT) en el mapa custom")]
    public Vector3 customMapSpawnA = new Vector3(-30, 2f, 0);
    [Tooltip("Posición base del spawn del equipo B (T) en el mapa custom")]
    public Vector3 customMapSpawnB = new Vector3( 30, 2f, 0);

    private MainMenu menu;

    void Awake()
    {
        // Disable VSync so targetFrameRate actually applies
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        // Add SceneSetup but DO NOT auto-build (the menu will trigger it)
        SceneSetup setup = FindAnyObjectByType<SceneSetup>();
        if (setup == null)
        {
            setup = gameObject.AddComponent<SceneSetup>();
            setup.autoBuildOnAwake = false;
        }

        // Pasar prefabs y ajustes al SceneSetup
        setup.pistolPrefab = pistolPrefab;
        setup.riflePrefab = riflePrefab;
        setup.sniperPrefab = sniperPrefab;
        setup.knifePrefab = knifePrefab;
        setup.botBodyPrefab = botBodyPrefab;
        setup.weaponPrefabScale = weaponPrefabScale;
        setup.weaponPrefabOffset = weaponPrefabOffset;
        setup.weaponPrefabRotation = weaponPrefabRotation;
        setup.botPrefabScale = botPrefabScale;
        setup.botGunOffset = botGunOffset;
        setup.botGunRotation = botGunRotation;
        setup.botGunScaleMultiplier = botGunScaleMultiplier;
        setup.muzzleLocalOffset = muzzleLocalOffset;
        setup.knifeScaleMultiplier = knifeScaleMultiplier;
        setup.knifeExtraRotation = knifeExtraRotation;
        setup.knifeExtraOffset = knifeExtraOffset;
        setup.customMapPrefab = customMapPrefab;
        setup.customMapScale = customMapScale;
        setup.customMapOffset = customMapOffset;
        setup.customMapEuler = customMapEuler;
        setup.customMapSpawnA = customMapSpawnA;
        setup.customMapSpawnB = customMapSpawnB;

        // Add main menu
        menu = gameObject.AddComponent<MainMenu>();
        menu.sceneSetup = setup;
        // Wire NetworkLobby (created by the user as a separate GameObject in the scene).
        // Use includeInactive in case it sits under DontDestroyOnLoad after Mirror initializes.
        menu.networkLobby = FindAnyObjectByType<NetworkLobby>(FindObjectsInactive.Include);
        if (menu.networkLobby == null && Mirror.NetworkManager.singleton is NetworkLobby lobby)
            menu.networkLobby = lobby;
    }

    void Start()
    {
        MouseLook ml = FindAnyObjectByType<MouseLook>();
        if (ml) ml.SetSensitivity(mouseSensitivity);
    }

    void Update()
    {
        // Re-apply sensitivity when player is created after menu
        if (Input.GetKeyDown(KeyCode.F2))
        {
            MouseLook ml = FindAnyObjectByType<MouseLook>();
            if (ml) ml.SetSensitivity(mouseSensitivity);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // Don't show debug info while in menu
        if (menu != null && IsMenuVisible()) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(1, 1, 1, 0.5f) }
        };

        float y = 10;
        GUI.Label(new Rect(10, y, 300, 20), $"FPS: {(1f / Time.smoothDeltaTime):F0}", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "WASD: Run (default) | Shift: Walk silent | Ctrl: Crouch | Space: Jump", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "Mouse: Look | LMB: Shoot | RMB: ADS | R: Reload | F: Inspect", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "1: Pistol | 2: Rifle | 3: Sniper | 4: Knife | Scroll: Next/Prev", style);
    }

    private bool IsMenuVisible()
    {
        // Reflection-free check: menu hides itself when game starts
        return menu != null && menu.enabled && Cursor.visible;
    }
}
