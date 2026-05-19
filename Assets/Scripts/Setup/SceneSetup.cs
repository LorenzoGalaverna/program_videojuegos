using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Attach this to an empty GameObject and press Play.
/// It builds the entire FPS scene: map, player, weapons, bot, HUD, etc.
/// Delete this script once you're happy with the setup and want to customize manually.
/// </summary>
public class SceneSetup : MonoBehaviour
{
    [Header("Setup Options")]
    public bool buildMap = true;
    public bool buildPlayer = true;
    public bool buildBot = true;
    public bool buildGameManager = true;
    public bool autoBuildOnAwake = false;

    [HideInInspector] public GameObject pistolPrefab;
    [HideInInspector] public GameObject riflePrefab;
    [HideInInspector] public GameObject sniperPrefab;
    [HideInInspector] public GameObject knifePrefab;
    [HideInInspector] public GameObject botBodyPrefab;
    [HideInInspector] public float weaponPrefabScale = 1f;
    [HideInInspector] public Vector3 weaponPrefabOffset = Vector3.zero;
    [HideInInspector] public Vector3 weaponPrefabRotation = Vector3.zero;
    [HideInInspector] public float botPrefabScale = 1f;
    [HideInInspector] public Vector3 botGunOffset = new Vector3(0.05f, 0f, 0.08f);
    [HideInInspector] public Vector3 botGunRotation = new Vector3(0f, 90f, 90f);
    [HideInInspector] public float botGunScaleMultiplier = 1f;
    [HideInInspector] public Vector3 muzzleLocalOffset = new Vector3(0f, 0f, 0.5f);
    [HideInInspector] public float knifeScaleMultiplier = 1f;
    [HideInInspector] public Vector3 knifeExtraRotation = Vector3.zero;
    [HideInInspector] public Vector3 knifeExtraOffset = Vector3.zero;


    private Material wallMat;
    private Material floorMat;
    private Material crateMat;
    private Material redMat;
    private Material blueMat;

    private bool alreadyBuilt;

    void Awake()
    {
        if (autoBuildOnAwake) BuildScene();
    }

    public void BuildScene()
    {
        if (alreadyBuilt) return;
        alreadyBuilt = true;

        CreateMaterials();

        // 1. Build map (geometry must exist before NavMesh bake)
        if (buildMap) BuildMap();

        // 2. Bake NavMesh BEFORE creating any NavMeshAgent
        NavMeshSurface surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface)
        {
            surface.BuildNavMesh();
            Debug.Log("[SceneSetup] NavMesh baked.");
        }
        else
        {
            Debug.LogWarning("[SceneSetup] No NavMeshSurface found. Bot will not pathfind.");
        }

        // 3. Build player, bot and game manager
        Transform playerT = null;
        if (buildPlayer) playerT = BuildPlayer();
        if (buildBot) BuildBot();
        if (buildGameManager) BuildGameManager(playerT);

        Debug.Log("[SceneSetup] Scene built successfully!");
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        wallMat = CreateHDRPMaterial(shader, new Color(0.6f, 0.55f, 0.45f));
        floorMat = CreateHDRPMaterial(shader, new Color(0.4f, 0.38f, 0.32f));
        crateMat = CreateHDRPMaterial(shader, new Color(0.5f, 0.35f, 0.2f));
        redMat = CreateHDRPMaterial(shader, new Color(0.8f, 0.2f, 0.2f));
        blueMat = CreateHDRPMaterial(shader, new Color(0.2f, 0.3f, 0.8f));
    }

    private Material CreateHDRPMaterial(Shader shader, Color color)
    {
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }

    // PBR material with metallic + smoothness control (for guns)
    private Material CreatePBRMaterial(Color color, float metallic, float smoothness, Color? emissive = null)
    {
        Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (emissive.HasValue && mat.HasProperty("_EmissiveColor"))
        {
            mat.SetColor("_EmissiveColor", emissive.Value);
            mat.EnableKeyword("_EMISSION");
        }
        return mat;
    }

    // ──────────────────────────────────────────────
    // MAP - A simple dust2-inspired layout
    // ──────────────────────────────────────────────
    private void BuildMap()
    {
        GameObject map = new GameObject("Map");

        // Floor
        CreateBox("Floor", map.transform, Vector3.zero, new Vector3(50, 1, 50), floorMat, true);

        // Outer walls
        CreateBox("Wall_North", map.transform, new Vector3(0, 3, 25), new Vector3(50, 6, 1), wallMat);
        CreateBox("Wall_South", map.transform, new Vector3(0, 3, -25), new Vector3(50, 6, 1), wallMat);
        CreateBox("Wall_East", map.transform, new Vector3(25, 3, 0), new Vector3(1, 6, 50), wallMat);
        CreateBox("Wall_West", map.transform, new Vector3(-25, 3, 0), new Vector3(1, 6, 50), wallMat);

        // Central building / bombsite A
        CreateBox("Building_Center", map.transform, new Vector3(0, 2, 5), new Vector3(8, 4, 8), wallMat);
        // Opening in center building
        CreateBox("Building_Cover1", map.transform, new Vector3(-5, 1.5f, 5), new Vector3(2, 3, 1), wallMat);
        CreateBox("Building_Cover2", map.transform, new Vector3(5, 1.5f, 5), new Vector3(2, 3, 1), wallMat);

        // Corridors / lanes
        // Left lane wall
        CreateBox("Lane_Left_Wall1", map.transform, new Vector3(-12, 2, 0), new Vector3(1, 4, 20), wallMat);
        CreateBox("Lane_Left_Wall2", map.transform, new Vector3(-18, 2, 0), new Vector3(1, 4, 20), wallMat);

        // Right lane wall
        CreateBox("Lane_Right_Wall1", map.transform, new Vector3(12, 2, 0), new Vector3(1, 4, 20), wallMat);
        CreateBox("Lane_Right_Wall2", map.transform, new Vector3(18, 2, 0), new Vector3(1, 4, 20), wallMat);

        // Cover crates scattered around
        CreateBox("Crate1", map.transform, new Vector3(-8, 1, -8), new Vector3(2, 2, 2), crateMat);
        CreateBox("Crate2", map.transform, new Vector3(8, 1, -6), new Vector3(2, 2, 2), crateMat);
        CreateBox("Crate3", map.transform, new Vector3(-3, 1, 15), new Vector3(3, 2, 1.5f), crateMat);
        CreateBox("Crate4", map.transform, new Vector3(6, 1, -15), new Vector3(2, 2, 2), crateMat);
        CreateBox("Crate5", map.transform, new Vector3(-15, 1, 8), new Vector3(2, 2, 3), crateMat);
        CreateBox("Crate6", map.transform, new Vector3(15, 1, -3), new Vector3(1.5f, 2, 2), crateMat);
        CreateBox("CrateStack1", map.transform, new Vector3(-8, 2.5f, -8), new Vector3(1.5f, 1, 1.5f), crateMat);

        // Ramp
        GameObject ramp = CreateBox("Ramp", map.transform, new Vector3(10, 1, 10), new Vector3(4, 0.3f, 6), crateMat);
        ramp.transform.rotation = Quaternion.Euler(15, 0, 0);

        // Spawn zone indicators
        CreateBox("SpawnA_Marker", map.transform, new Vector3(-20, 0.6f, -20), new Vector3(4, 0.1f, 4), blueMat);
        CreateBox("SpawnB_Marker", map.transform, new Vector3(20, 0.6f, 20), new Vector3(4, 0.1f, 4), redMat);

        // Add NavMeshSurface to floor for bot navigation
        GameObject navObj = new GameObject("NavMesh");
        navObj.transform.parent = map.transform;
        NavMeshSurface surface = navObj.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;

        // Lighting - sol direccional simple
        GameObject lightObj = new GameObject("Sun");
        lightObj.transform.parent = map.transform;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        Light sun = lightObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 2f;
        sun.color = new Color(1f, 0.95f, 0.85f);
    }

    // ──────────────────────────────────────────────
    // PLAYER
    // ──────────────────────────────────────────────
    private Transform BuildPlayer()
    {
        // Player root
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.layer = LayerMask.NameToLayer("Default");
        player.transform.position = new Vector3(-20, 1.5f, -20);

        // Character Controller
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 1f, 0);

        EquipPlayer(player);
        return player.transform;
    }

    // Used by Mirror's NetworkedPlayer prefab: turns an existing GameObject into a
    // fully-featured local player (camera, weapons, HUD, etc.). The prefab brings
    // its own CharacterController + networking components, so we don't recreate those.
    public void EquipPlayer(GameObject player)
    {
        if (!player.CompareTag("Player")) player.tag = "Player";
        if (player.GetComponent<CharacterController>() == null)
        {
            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0, 1f, 0);
        }

        // Movement
        PlayerMovement movement = player.GetComponent<PlayerMovement>() ?? player.AddComponent<PlayerMovement>();

        // Health (may already exist on the prefab so it's networked)
        PlayerHealth health = player.GetComponent<PlayerHealth>() ?? player.AddComponent<PlayerHealth>();
        EquipPlayerInner(player, movement, health);
    }

    private void EquipPlayerInner(GameObject player, PlayerMovement movement, PlayerHealth health)
    {

        // Camera
        GameObject camHolder = new GameObject("CameraHolder");
        camHolder.transform.parent = player.transform;
        camHolder.transform.localPosition = new Vector3(0, 1.7f, 0);
        movement.cameraHolder = camHolder.transform;

        // Destroy existing main camera if any
        Camera existingCam = Camera.main;
        if (existingCam) Destroy(existingCam.gameObject);

        // Destroy ANY camera that might be in the scene (including ones added by asset packs)
        foreach (var existingCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            Destroy(existingCamera.gameObject);

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.tag = "MainCamera";
        camObj.transform.parent = camHolder.transform;
        camObj.transform.localPosition = Vector3.zero;
        camObj.layer = 0; // Default layer
        Camera cam = camObj.AddComponent<Camera>();
        cam.fieldOfView = 70;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1000f;
        cam.cullingMask = ~0;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.depth = 0;
        camObj.AddComponent<AudioListener>();

        // HDRP requires this component on cameras
        var hdCamData = camObj.AddComponent<HDAdditionalCameraData>();
        hdCamData.volumeLayerMask = ~0;
        hdCamData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;

        // Mouse Look
        MouseLook mouseLook = camHolder.AddComponent<MouseLook>();
        mouseLook.playerBody = player.transform;
        // weaponManager assigned below after WeaponManager is created

        // Weapon Holder - positioned to bottom-right of view
        GameObject weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.parent = camObj.transform;
        weaponHolder.transform.localPosition = new Vector3(0.3f, -0.25f, 0.5f);

        // Create weapons
        Weapon pistol = CreateWeaponObject("Pistol", weaponHolder.transform, WeaponType.Pistol);
        Weapon rifle = CreateWeaponObject("AK-47", weaponHolder.transform, WeaponType.Rifle);
        Weapon sniper = CreateWeaponObject("AWP", weaponHolder.transform, WeaponType.Sniper);
        Weapon knife = CreateWeaponObject("Knife", weaponHolder.transform, WeaponType.Knife);

        // Weapon Manager
        WeaponManager wm = player.AddComponent<WeaponManager>();
        wm.weaponHolder = weaponHolder.transform;
        wm.cameraTransform = camObj.transform;
        wm.playerCamera = cam;
        wm.weapons = new Weapon[] { pistol, rifle, sniper, knife };
        wm.startWeaponIndex = 1; // Start with rifle

        // Give MouseLook a reference to WeaponManager so it can incorporate recoil
        mouseLook.weaponManager = wm;

        // HUD
        GameHUD hud = player.AddComponent<GameHUD>();
        hud.playerHealth = health;
        hud.weaponManager = wm;

        // Death handler — disable controls so the player can't move/shoot while dead.
        // In LAN mode: NetworkLobby handles score tracking and server-side respawn.
        // In offline mode: handle scoring and respawn locally.
        health.onDeath.AddListener(() =>
        {
            player.GetComponent<PlayerMovement>().enabled = false;
            player.GetComponent<WeaponManager>().enabled = false;

            if (!NetworkLobby.IsLanActive && GameManager.Instance)
            {
                GameManager.Instance.AddEnemyKill();
                Invoke(nameof(RespawnPlayer), 3f);
            }
        });
    }

    private void RespawnPlayer()
    {
        // Don't respawn if the game already ended
        if (GameManager.Instance != null && !GameManager.Instance.GameActive) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || GameManager.Instance == null) return;

        GameManager.Instance.RespawnPlayer(player, 0);

        // Re-enable controls after respawn
        player.GetComponent<PlayerMovement>().enabled = true;
        player.GetComponent<WeaponManager>().enabled = true;
    }

    private Weapon CreateWeaponObject(string name, Transform parent, WeaponType type)
    {
        GameObject weaponObj = new GameObject(name);
        weaponObj.transform.parent = parent;
        weaponObj.transform.localPosition = Vector3.zero;

        // Container for the visual model (so we can scale/position the parts independently)
        GameObject visual = new GameObject("Model");
        visual.transform.parent = weaponObj.transform;
        visual.transform.localPosition = Vector3.zero;

        // Materials
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        Material gunMat = CreateHDRPMaterial(hdrpLit, new Color(0.10f, 0.10f, 0.10f));
        Material accentMat = CreateHDRPMaterial(hdrpLit, new Color(0.55f, 0.35f, 0.15f));

        // Configure based on type
        Weapon weapon = weaponObj.AddComponent<Weapon>();
        WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
        data.weaponName = name;
        data.weaponType = type;

        // If a custom prefab is assigned, use it instead of the procedural model
        GameObject prefabToUse = null;
        switch (type)
        {
            case WeaponType.Pistol: prefabToUse = pistolPrefab; break;
            case WeaponType.Rifle:  prefabToUse = riflePrefab; break;
            case WeaponType.Sniper: prefabToUse = sniperPrefab; break;
            case WeaponType.Knife:  prefabToUse = knifePrefab; break;
        }

        if (prefabToUse != null)
        {
            GameObject inst = Instantiate(prefabToUse, visual.transform);
            // Knives often come from a different pack (different scale/orientation),
            // so allow per-weapon adjustments on top of the shared base values.
            bool isKnife = type == WeaponType.Knife;
            inst.transform.localPosition = weaponPrefabOffset + (isKnife ? knifeExtraOffset : Vector3.zero);
            inst.transform.localRotation = Quaternion.Euler(weaponPrefabRotation + (isKnife ? knifeExtraRotation : Vector3.zero));
            inst.transform.localScale = Vector3.one * weaponPrefabScale * (isKnife ? knifeScaleMultiplier : 1f);
            // Quitar colliders del prefab visual para que no bloqueen raycasts
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        switch (type)
        {
            case WeaponType.Pistol:
                if (prefabToUse == null) BuildPistolModel(visual.transform, gunMat, accentMat, 1f);
                data.damage = 30;
                data.fireRate = 0.2f;
                data.magazineSize = 12;
                data.reserveAmmo = 36;
                data.reloadTime = 1.5f;
                data.baseSpread = 0.015f;
                data.recoilUp = 2f;
                data.moveSpeedMultiplier = 1f;
                data.adsFovMultiplier = 0.65f; // zoom suave
                break;

            case WeaponType.Rifle:
                if (prefabToUse == null) BuildRifleModel(visual.transform, gunMat, accentMat, 1f);
                data.damage = 25;
                data.fireRate = 0.1f;
                data.magazineSize = 30;
                data.reserveAmmo = 90;
                data.reloadTime = 2.5f;
                data.baseSpread = 0.02f;
                data.maxSpread = 0.08f;
                data.spreadIncreasePerShot = 0.008f;
                data.recoilUp = 1.5f;
                data.recoilSide = 0.5f;
                data.moveSpeedMultiplier = 0.9f;
                data.adsFovMultiplier = 0.5f; // zoom intermedio
                break;

            case WeaponType.Sniper:
                if (prefabToUse == null) BuildSniperModel(visual.transform, gunMat, accentMat, 1f);
                data.damage = 80;
                data.fireRate = 1.5f;
                data.magazineSize = 5;
                data.reserveAmmo = 20;
                data.reloadTime = 3f;
                data.baseSpread = 0.005f;
                data.recoilUp = 5f;
                data.adsFovMultiplier = 0.3f;
                data.moveSpeedMultiplier = 0.75f;
                break;

            case WeaponType.Knife:
                if (prefabToUse == null) BuildKnifeModel(visual.transform, gunMat, accentMat, 1f);
                data.damage = 50;
                data.fireRate = 0.5f;       // half-second between swings
                data.range = 2.5f;          // melee reach
                data.magazineSize = 999;    // effectively infinite, no reload
                data.reserveAmmo = 0;
                data.reloadTime = 0f;
                data.baseSpread = 0f;
                data.maxSpread = 0f;
                data.recoilUp = 0f;
                data.recoilSide = 0f;
                data.moveSpeedMultiplier = 1.1f; // slightly faster with knife
                break;
        }

        data.holdPosition = new Vector3(0.2f, -0.15f, 0.4f);
        data.adsPosition = new Vector3(0f, -0.08f, 0.3f);

        // Auto-load audio clips from Resources/Audio/<weaponType>_<action>.
        // E.g.: "Audio/Rifle_Shoot", "Audio/Pistol_Reload", "Audio/Sniper_Empty".
        // Falls back to a generic clip if a per-weapon one isn't found.
        string typeName = type.ToString();
        data.shootSound = LoadClip($"Audio/{typeName}_Shoot") ?? LoadClip("Audio/Generic_Shoot");
        data.reloadSound = LoadClip($"Audio/{typeName}_Reload") ?? LoadClip("Audio/Generic_Reload");
        data.emptySound = LoadClip($"Audio/{typeName}_Empty") ?? LoadClip("Audio/Generic_Empty");

        // Knife-specific flesh impact sound (wall hits just use the swing swoosh)
        if (type == WeaponType.Knife)
            weapon.knifeFleshHitSound = LoadClip("Audio/Knife_Flesh");

        weapon.data = data;

        // Audio source
        AudioSource audio = weaponObj.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        weapon.audioSource = audio;

        // Muzzle point — uses the configurable offset so it lines up with the visual
        // gun barrel of whatever prefab is in use.
        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.parent = weaponObj.transform;
        muzzle.transform.localPosition = muzzleLocalOffset;
        weapon.muzzlePoint = muzzle.transform;

        // First-person hands holding the weapon — only build procedural hands when no
        // custom weapon prefab is in use (otherwise the cube hands clip through the model).
        if (prefabToUse == null)
            BuildFirstPersonHands(weaponObj.transform, type);

        weaponObj.SetActive(false);
        return weapon;
    }

    private void BuildFirstPersonHands(Transform weaponRoot, WeaponType type)
    {
        Material skin = CreatePBRMaterial(new Color(0.85f, 0.65f, 0.55f), 0f, 0.3f);
        Material glove = CreatePBRMaterial(new Color(0.10f, 0.10f, 0.10f), 0f, 0.4f);

        // Right hand on grip (always there)
        GameObject rightHand = new GameObject("RightHand");
        rightHand.transform.parent = weaponRoot;
        Vector3 rightHandPos = type == WeaponType.Sniper ? new Vector3(-0.04f, -0.16f, 0.0f)
                              : type == WeaponType.Pistol ? new Vector3(-0.04f, -0.16f, -0.02f)
                              : new Vector3(-0.04f, -0.16f, 0.05f);
        rightHand.transform.localPosition = rightHandPos;

        // Hand
        AddPart(rightHand.transform, "RPalm", PrimitiveType.Cube,
                new Vector3(0, 0, 0), new Vector3(0.07f, 0.10f, 0.10f), glove);
        // Fingers (gripping)
        AddPart(rightHand.transform, "RFingers", PrimitiveType.Cube,
                new Vector3(0.03f, -0.02f, 0.03f), new Vector3(0.02f, 0.07f, 0.10f), glove);
        AddPart(rightHand.transform, "RThumb", PrimitiveType.Cube,
                new Vector3(-0.025f, 0.02f, 0.04f), new Vector3(0.02f, 0.04f, 0.06f), glove);
        // Forearm extending back/up out of view
        AddPart(rightHand.transform, "RForearm", PrimitiveType.Cube,
                new Vector3(-0.05f, -0.08f, -0.20f), new Vector3(0.08f, 0.10f, 0.30f), skin,
                new Vector3(-25, 0, 15));

        // Left hand (different position per weapon type)
        if (type == WeaponType.Pistol)
        {
            // Both hands on pistol grip (steady stance)
            GameObject leftHand = new GameObject("LeftHand");
            leftHand.transform.parent = weaponRoot;
            leftHand.transform.localPosition = new Vector3(0.04f, -0.18f, -0.02f);

            AddPart(leftHand.transform, "LPalm", PrimitiveType.Cube,
                    new Vector3(0, 0, 0), new Vector3(0.06f, 0.09f, 0.10f), glove);
            AddPart(leftHand.transform, "LFingers", PrimitiveType.Cube,
                    new Vector3(-0.025f, -0.02f, 0.03f), new Vector3(0.02f, 0.06f, 0.10f), glove);
            AddPart(leftHand.transform, "LForearm", PrimitiveType.Cube,
                    new Vector3(0.04f, -0.08f, -0.20f), new Vector3(0.08f, 0.10f, 0.30f), skin,
                    new Vector3(-25, 0, -15));
        }
        else
        {
            // Long gun: left hand on handguard / forward grip
            GameObject leftHand = new GameObject("LeftHand");
            leftHand.transform.parent = weaponRoot;
            float fwdGrip = type == WeaponType.Sniper ? 0.45f : 0.45f;
            leftHand.transform.localPosition = new Vector3(0.04f, -0.10f, fwdGrip);

            AddPart(leftHand.transform, "LPalm", PrimitiveType.Cube,
                    new Vector3(0, 0, 0), new Vector3(0.06f, 0.09f, 0.10f), glove);
            AddPart(leftHand.transform, "LFingers", PrimitiveType.Cube,
                    new Vector3(-0.025f, -0.02f, 0.03f), new Vector3(0.02f, 0.06f, 0.10f), glove);
            AddPart(leftHand.transform, "LForearm", PrimitiveType.Cube,
                    new Vector3(0.04f, -0.10f, -0.18f), new Vector3(0.08f, 0.10f, 0.30f), skin,
                    new Vector3(-15, 0, -25));
        }
    }

    // ──────────────────────────────────────────────
    // BOT
    // ──────────────────────────────────────────────
    private void BuildBot()
    {
        GameObject bot = new GameObject("Enemy_Bot");

        // Try to place the bot on the NavMesh near the player
        Vector3 desired = new Vector3(-8, 1f, -8);
        if (NavMesh.SamplePosition(desired, out NavMeshHit navHit, 10f, NavMesh.AllAreas))
            bot.transform.position = navHit.position;
        else
            bot.transform.position = desired;

        // If a custom bot prefab is assigned, use it instead of the procedural model
        if (botBodyPrefab != null)
        {
            GameObject visual = Instantiate(botBodyPrefab, bot.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * botPrefabScale;
            // Remove any colliders from the visual prefab (we add our own hitboxes)
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);

            // Disable root motion so the NavMeshAgent (not the animation) drives movement.
            // Without this, Mixamo animations push the bot through walls.
            Animator anim = visual.GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;

            BuildBotSystems(bot, anim);
            return;
        }

        // Materials
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        Material vestMat = CreateHDRPMaterial(hdrpLit, new Color(0.55f, 0.12f, 0.12f));
        Material limbsMat = CreateHDRPMaterial(hdrpLit, new Color(0.18f, 0.18f, 0.22f));
        Material skinMat = CreateHDRPMaterial(hdrpLit, new Color(0.85f, 0.65f, 0.55f));
        Material bootsMat = CreateHDRPMaterial(hdrpLit, new Color(0.08f, 0.08f, 0.08f));
        Material gunMat = CreateHDRPMaterial(hdrpLit, new Color(0.12f, 0.12f, 0.12f));
        Material accentMat = CreateHDRPMaterial(hdrpLit, new Color(1f, 0.85f, 0.2f));
        accentMat.SetColor("_EmissiveColor", new Color(0.6f, 0.5f, 0.1f));
        accentMat.EnableKeyword("_EMISSION");

        // Torso (vest)
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        torso.name = "Torso";
        torso.transform.parent = bot.transform;
        torso.transform.localPosition = new Vector3(0, 1.25f, 0);
        torso.transform.localScale = new Vector3(0.65f, 0.7f, 0.4f);
        Destroy(torso.GetComponent<Collider>());
        torso.GetComponent<Renderer>().material = vestMat;

        // Belt
        GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
        belt.name = "Belt";
        belt.transform.parent = bot.transform;
        belt.transform.localPosition = new Vector3(0, 0.88f, 0);
        belt.transform.localScale = new Vector3(0.7f, 0.12f, 0.42f);
        Destroy(belt.GetComponent<Collider>());
        belt.GetComponent<Renderer>().material = bootsMat;

        // Pelvis
        GameObject pelvis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pelvis.name = "Pelvis";
        pelvis.transform.parent = bot.transform;
        pelvis.transform.localPosition = new Vector3(0, 0.78f, 0);
        pelvis.transform.localScale = new Vector3(0.55f, 0.18f, 0.38f);
        Destroy(pelvis.GetComponent<Collider>());
        pelvis.GetComponent<Renderer>().material = limbsMat;

        // Legs
        BuildBotLimb(bot.transform, "LegL", new Vector3(-0.18f, 0.4f, 0), new Vector3(0.2f, 0.8f, 0.25f), limbsMat);
        BuildBotLimb(bot.transform, "LegR", new Vector3(0.18f, 0.4f, 0), new Vector3(0.2f, 0.8f, 0.25f), limbsMat);

        // Boots
        BuildBotLimb(bot.transform, "BootL", new Vector3(-0.18f, 0.05f, 0.05f), new Vector3(0.22f, 0.1f, 0.4f), bootsMat);
        BuildBotLimb(bot.transform, "BootR", new Vector3(0.18f, 0.05f, 0.05f), new Vector3(0.22f, 0.1f, 0.4f), bootsMat);

        // Arms
        BuildBotLimb(bot.transform, "ArmL", new Vector3(-0.42f, 1.3f, 0), new Vector3(0.18f, 0.65f, 0.22f), limbsMat);
        BuildBotLimb(bot.transform, "ArmR", new Vector3(0.42f, 1.3f, 0), new Vector3(0.18f, 0.65f, 0.22f), limbsMat);

        // Hands
        BuildBotLimb(bot.transform, "HandL", new Vector3(-0.42f, 0.92f, 0.05f), new Vector3(0.16f, 0.18f, 0.22f), skinMat);
        BuildBotLimb(bot.transform, "HandR", new Vector3(0.42f, 0.92f, 0.05f), new Vector3(0.16f, 0.18f, 0.22f), skinMat);

        // Neck
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        neck.name = "Neck";
        neck.transform.parent = bot.transform;
        neck.transform.localPosition = new Vector3(0, 1.7f, 0);
        neck.transform.localScale = new Vector3(0.18f, 0.15f, 0.18f);
        Destroy(neck.GetComponent<Collider>());
        neck.GetComponent<Renderer>().material = skinMat;

        // Head (with collider for headshots)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
        head.name = "Head";
        head.tag = "Head";
        head.transform.parent = bot.transform;
        head.transform.localPosition = new Vector3(0, 1.92f, 0);
        head.transform.localScale = new Vector3(0.32f, 0.36f, 0.32f);
        head.GetComponent<Renderer>().material = skinMat;

        // Helmet
        GameObject helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        helmet.name = "Helmet";
        helmet.transform.parent = bot.transform;
        helmet.transform.localPosition = new Vector3(0, 2.05f, 0);
        helmet.transform.localScale = new Vector3(0.42f, 0.32f, 0.42f);
        Destroy(helmet.GetComponent<Collider>());
        helmet.GetComponent<Renderer>().material = limbsMat;

        // Visor / eyes (so you can tell which way it's facing)
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        visor.transform.parent = bot.transform;
        visor.transform.localPosition = new Vector3(0, 1.95f, 0.16f);
        visor.transform.localScale = new Vector3(0.26f, 0.07f, 0.04f);
        Destroy(visor.GetComponent<Collider>());
        visor.GetComponent<Renderer>().material = accentMat;

        // Body hitbox — sized so it ends BELOW the head sphere. If the capsule reaches
        // into the head's Y range it gets hit first by horizontal raycasts and headshots
        // never trigger.
        GameObject hitbox = new GameObject("BodyHitbox");
        hitbox.transform.parent = bot.transform;
        hitbox.transform.localPosition = new Vector3(0, 0.8f, 0);
        CapsuleCollider hitCol = hitbox.AddComponent<CapsuleCollider>();
        hitCol.height = 1.4f;
        hitCol.radius = 0.35f;

        // Bot's gun (visible in 3rd person)
        GameObject botGun = new GameObject("BotGun");
        botGun.transform.parent = bot.transform;
        botGun.transform.localPosition = new Vector3(0.35f, 1.15f, 0.45f);
        botGun.transform.localRotation = Quaternion.Euler(0, 0, 0);
        BuildRifleModel(botGun.transform, gunMat, accentMat, 1.2f);

        // Eye point (used for line-of-sight raycasts)
        GameObject eye = new GameObject("EyePoint");
        eye.transform.parent = bot.transform;
        eye.transform.localPosition = new Vector3(0, 2f, 0.3f);

        // NavMeshAgent — handles movement and pathfinding
        NavMeshAgent agent = bot.AddComponent<NavMeshAgent>();
        agent.speed = 4.5f;
        agent.angularSpeed = 250f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.5f;
        agent.height = 2f;

        // Health
        PlayerHealth health = bot.AddComponent<PlayerHealth>();

        // Bot AI
        EnemyBot botAI = bot.AddComponent<EnemyBot>();
        botAI.eyePoint = eye.transform;

        Debug.Log($"[SceneSetup] Bot spawned at {bot.transform.position} | onNavMesh={agent.isOnNavMesh}");
    }

    private static AudioClip LoadClip(string resourcePath)
    {
        return Resources.Load<AudioClip>(resourcePath);
    }

    // Attach a rifle to the right hand bone of a humanoid model, using the same offsets
    // and scale that the bot uses. Used by NetworkedPlayer for the 3rd-person view of
    // remote players so they have a visible weapon.
    public GameObject AttachRifleToHumanoidHand(Transform humanoidRoot)
    {
        if (humanoidRoot == null) return null;
        Animator anim = humanoidRoot.GetComponentInChildren<Animator>();
        if (anim == null || !anim.isHuman) return null;
        Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (rightHand == null) return null;

        GameObject gunHolder = new GameObject("ThirdPersonGun");
        gunHolder.transform.SetParent(rightHand, false);
        gunHolder.transform.localPosition = botGunOffset;
        gunHolder.transform.localRotation = Quaternion.Euler(botGunRotation);

        Vector3 boneScale = rightHand.lossyScale;
        gunHolder.transform.localScale = new Vector3(
            boneScale.x != 0 ? 1f / boneScale.x : 1f,
            boneScale.y != 0 ? 1f / boneScale.y : 1f,
            boneScale.z != 0 ? 1f / boneScale.z : 1f);

        if (riflePrefab != null)
        {
            GameObject gun = Instantiate(riflePrefab, gunHolder.transform);
            gun.transform.localPosition = weaponPrefabOffset;
            gun.transform.localRotation = Quaternion.Euler(weaponPrefabRotation);
            gun.transform.localScale = Vector3.one * weaponPrefabScale * botGunScaleMultiplier;
            foreach (var c in gun.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        return gunHolder;
    }

    private void BuildBotSystems(GameObject bot, Animator anim = null)
    {
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        Material gunMat = CreateHDRPMaterial(hdrpLit, new Color(0.12f, 0.12f, 0.12f));
        Material accentMat = CreateHDRPMaterial(hdrpLit, new Color(1f, 0.85f, 0.2f));
        accentMat.SetColor("_EmissiveColor", new Color(0.6f, 0.5f, 0.1f));
        accentMat.EnableKeyword("_EMISSION");

        // Find the right hand bone if the model is humanoid; fall back to bot root.
        Transform gunParent = bot.transform;
        Vector3 gunLocalPos = new Vector3(0.35f, 1.15f, 0.45f);
        Quaternion gunLocalRot = Quaternion.identity;
        if (anim != null && anim.isHuman)
        {
            Transform rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (rightHand != null)
            {
                gunParent = rightHand;
                gunLocalPos = botGunOffset;
                gunLocalRot = Quaternion.Euler(botGunRotation);
            }
        }

        GameObject botGun = new GameObject("BotGun");
        botGun.transform.SetParent(gunParent, false);
        botGun.transform.localPosition = gunLocalPos;
        botGun.transform.localRotation = gunLocalRot;

        // Compensate for non-uniform bone scale (Mixamo bones often scale at 0.01)
        Vector3 boneCompensation = Vector3.one;
        if (gunParent != bot.transform)
        {
            Vector3 boneWorldScale = gunParent.lossyScale;
            boneCompensation = new Vector3(
                boneWorldScale.x != 0 ? 1f / boneWorldScale.x : 1f,
                boneWorldScale.y != 0 ? 1f / boneWorldScale.y : 1f,
                boneWorldScale.z != 0 ? 1f / boneWorldScale.z : 1f);
            botGun.transform.localScale = boneCompensation;
        }

        // If the user assigned a rifle prefab, use it instead of the procedural model.
        // Apply the same offset/rotation/scale used for the player's weapon so the
        // model orientation matches across both viewpoints.
        if (riflePrefab != null)
        {
            GameObject gunInst = Instantiate(riflePrefab, botGun.transform);
            gunInst.transform.localPosition = weaponPrefabOffset;
            gunInst.transform.localRotation = Quaternion.Euler(weaponPrefabRotation);
            gunInst.transform.localScale = Vector3.one * weaponPrefabScale * botGunScaleMultiplier;
            foreach (var c in gunInst.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else
        {
            BuildRifleModel(botGun.transform, gunMat, accentMat, 1.2f);
        }

        // Body hitbox — capsule top stays below the head sphere so headshots work.
        GameObject hitbox = new GameObject("BodyHitbox");
        hitbox.transform.parent = bot.transform;
        hitbox.transform.localPosition = new Vector3(0, 0.8f, 0);
        CapsuleCollider hitCol = hitbox.AddComponent<CapsuleCollider>();
        hitCol.height = 1.4f;
        hitCol.radius = 0.35f;

        // Head hitbox positioned above the body capsule with a small clearance.
        GameObject head = new GameObject("Head");
        head.tag = "Head";
        head.transform.parent = bot.transform;
        head.transform.localPosition = new Vector3(0, 1.7f, 0);
        SphereCollider headCol = head.AddComponent<SphereCollider>();
        headCol.radius = 0.22f;

        // Eye point (line-of-sight raycasts)
        GameObject eye = new GameObject("EyePoint");
        eye.transform.parent = bot.transform;
        eye.transform.localPosition = new Vector3(0, 1.7f, 0.3f);

        // NavMeshAgent
        NavMeshAgent agent = bot.AddComponent<NavMeshAgent>();
        agent.speed = 4.5f;
        agent.angularSpeed = 250f;
        agent.acceleration = 12f;
        agent.stoppingDistance = 0.5f;
        agent.radius = 0.5f;
        agent.height = 2f;

        // Muzzle point — placed on the BotGun container at the configured muzzle offset
        // so tracers leave from the barrel of the rifle instead of the bot's head.
        GameObject botMuzzle = new GameObject("BotMuzzle");
        botMuzzle.transform.SetParent(botGun.transform, false);
        botMuzzle.transform.localPosition = muzzleLocalOffset;

        // 3D-spatial audio source for bot's gunshots — gets louder up close, fades with distance
        AudioSource botShootAudio = bot.AddComponent<AudioSource>();
        botShootAudio.playOnAwake = false;
        botShootAudio.spatialBlend = 1f;          // fully 3D
        botShootAudio.minDistance = 5f;            // full volume within 5m
        botShootAudio.maxDistance = 50f;           // inaudible past 50m
        botShootAudio.rolloffMode = AudioRolloffMode.Linear;
        botShootAudio.volume = 1f;

        // Separate 3D-spatial source for footsteps (quieter, shorter audible range)
        AudioSource botFootstepAudio = bot.AddComponent<AudioSource>();
        botFootstepAudio.playOnAwake = false;
        botFootstepAudio.spatialBlend = 1f;
        botFootstepAudio.minDistance = 2f;
        botFootstepAudio.maxDistance = 20f;
        botFootstepAudio.rolloffMode = AudioRolloffMode.Linear;
        botFootstepAudio.volume = 1f;

        // Load footstep variations from Resources/Audio (same set the player uses)
        var stepClips = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= 6; i++)
        {
            AudioClip c = LoadClip($"Audio/Footstep_{i}");
            if (c != null) stepClips.Add(c);
        }
        if (stepClips.Count == 0)
        {
            AudioClip c = LoadClip("Audio/Footstep");
            if (c != null) stepClips.Add(c);
        }

        // Health + AI
        bot.AddComponent<PlayerHealth>();
        EnemyBot botAI = bot.AddComponent<EnemyBot>();
        botAI.eyePoint = eye.transform;
        botAI.muzzlePoint = botMuzzle.transform;
        botAI.shootAudio = botShootAudio;
        botAI.shootClip = LoadClip("Audio/Rifle_Shoot") ?? LoadClip("Audio/Generic_Shoot");
        botAI.footstepAudio = botFootstepAudio;
        botAI.footstepClips = stepClips.ToArray();

        Debug.Log($"[SceneSetup] Custom prefab bot spawned at {bot.transform.position} | onNavMesh={agent.isOnNavMesh}");
    }

    // ──────────────────────────────────────────────
    // GAME MANAGER
    // ──────────────────────────────────────────────
    private void BuildGameManager(Transform player)
    {
        GameObject gm = new GameObject("GameManager");
        GameManager manager = gm.AddComponent<GameManager>();
        manager.killsToWin = 10;
        manager.roundTime = 180f;

        // Create spawn points
        Transform[] spawnsA = new Transform[3];
        Transform[] spawnsB = new Transform[3];

        for (int i = 0; i < 3; i++)
        {
            GameObject spA = new GameObject($"SpawnA_{i}");
            spA.transform.position = new Vector3(-20 + i * 2, 1.5f, -20 + i);
            spawnsA[i] = spA.transform;

            GameObject spB = new GameObject($"SpawnB_{i}");
            spB.transform.position = new Vector3(20 - i * 2, 1.5f, 20 - i);
            spawnsB[i] = spB.transform;
        }

        manager.spawnPointsTeamA = spawnsA;
        manager.spawnPointsTeamB = spawnsB;
    }

    // ──────────────────────────────────────────────
    // HELPERS
    // ──────────────────────────────────────────────
    private GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool isFloor = false)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.parent = parent;
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        obj.isStatic = true;

        Renderer rend = obj.GetComponent<Renderer>();
        if (rend && mat) rend.material = mat;

        return obj;
    }

    // ──────────────────────────────────────────────
    // HUMANOID / WEAPON BUILDERS
    // ──────────────────────────────────────────────
    private GameObject BuildBotLimb(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject limb = GameObject.CreatePrimitive(PrimitiveType.Cube);
        limb.name = name;
        limb.transform.parent = parent;
        limb.transform.localPosition = localPos;
        limb.transform.localScale = scale;
        Destroy(limb.GetComponent<Collider>());
        limb.GetComponent<Renderer>().material = mat;
        return limb;
    }

    private GameObject AddPart(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, Vector3? localEuler = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.parent = parent;
        part.transform.localPosition = localPos;
        part.transform.localScale = scale;
        if (localEuler.HasValue) part.transform.localEulerAngles = localEuler.Value;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().material = mat;
        return part;
    }

    public void BuildKnifeModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        Material steel = CreatePBRMaterial(new Color(0.75f, 0.78f, 0.82f), 1f, 0.85f);
        Material handle = CreatePBRMaterial(new Color(0.10f, 0.10f, 0.10f), 0f, 0.3f);
        Material guard = CreatePBRMaterial(new Color(0.20f, 0.20f, 0.20f), 0.5f, 0.4f);

        // Blade — long thin slab
        AddPart(root, "Blade", PrimitiveType.Cube, new Vector3(0, 0.02f, 0.20f) * scale,
                new Vector3(0.012f, 0.04f, 0.30f) * scale, steel);
        // Blade tip (smaller box at the front to fake a point)
        AddPart(root, "BladeTip", PrimitiveType.Cube, new Vector3(0, 0.02f, 0.36f) * scale,
                new Vector3(0.008f, 0.02f, 0.06f) * scale, steel);
        // Cross-guard (perpendicular to blade)
        AddPart(root, "Guard", PrimitiveType.Cube, new Vector3(0, 0.02f, 0.04f) * scale,
                new Vector3(0.05f, 0.02f, 0.025f) * scale, guard);
        // Handle (grip)
        AddPart(root, "Handle", PrimitiveType.Cube, new Vector3(0, 0.02f, -0.06f) * scale,
                new Vector3(0.025f, 0.04f, 0.14f) * scale, handle);
        // Pommel (end cap)
        AddPart(root, "Pommel", PrimitiveType.Cube, new Vector3(0, 0.02f, -0.14f) * scale,
                new Vector3(0.030f, 0.045f, 0.025f) * scale, guard);
    }

    public void BuildPistolModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // PBR materials
        Material darkMetal = CreatePBRMaterial(new Color(0.08f, 0.08f, 0.08f), 0.85f, 0.55f);
        Material gripPlastic = CreatePBRMaterial(new Color(0.12f, 0.12f, 0.12f), 0f, 0.25f);
        Material accent = CreatePBRMaterial(new Color(0.55f, 0.4f, 0.15f), 0.6f, 0.5f);

        // Slide (top, with bevel effect using two parts)
        AddPart(root, "Slide", PrimitiveType.Cube, new Vector3(0, 0.05f, 0.10f) * scale,
                new Vector3(0.06f, 0.07f, 0.32f) * scale, darkMetal);
        AddPart(root, "SlideTop", PrimitiveType.Cube, new Vector3(0, 0.085f, 0.10f) * scale,
                new Vector3(0.04f, 0.02f, 0.30f) * scale, darkMetal);

        // Slide serrations (rear grip stripes)
        for (int i = 0; i < 4; i++)
        {
            AddPart(root, $"Serration{i}", PrimitiveType.Cube,
                    new Vector3(0.031f, 0.05f, -0.03f - i * 0.018f) * scale,
                    new Vector3(0.005f, 0.05f, 0.012f) * scale, accent);
            AddPart(root, $"SerrationL{i}", PrimitiveType.Cube,
                    new Vector3(-0.031f, 0.05f, -0.03f - i * 0.018f) * scale,
                    new Vector3(0.005f, 0.05f, 0.012f) * scale, accent);
        }

        // Lower frame
        AddPart(root, "Frame", PrimitiveType.Cube, new Vector3(0, -0.02f, 0.05f) * scale,
                new Vector3(0.06f, 0.07f, 0.22f) * scale, darkMetal);

        // Grip (textured plastic look, slightly angled)
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.16f, -0.02f) * scale,
                new Vector3(0.07f, 0.20f, 0.10f) * scale, gripPlastic,
                new Vector3(12, 0, 0));
        // Grip checker pattern
        for (int i = 0; i < 3; i++)
            AddPart(root, $"GripPad{i}", PrimitiveType.Cube,
                    new Vector3(0.036f, -0.10f - i * 0.04f, -0.02f) * scale,
                    new Vector3(0.005f, 0.025f, 0.08f) * scale, accent,
                    new Vector3(12, 0, 0));

        // Trigger guard (rounded with two parts)
        AddPart(root, "TriggerGuardFront", PrimitiveType.Cube, new Vector3(0, -0.07f, 0.05f) * scale,
                new Vector3(0.05f, 0.025f, 0.015f) * scale, darkMetal);
        AddPart(root, "TriggerGuardBottom", PrimitiveType.Cube, new Vector3(0, -0.10f, 0.025f) * scale,
                new Vector3(0.05f, 0.015f, 0.07f) * scale, darkMetal);

        // Trigger
        AddPart(root, "Trigger", PrimitiveType.Cube, new Vector3(0, -0.07f, 0.02f) * scale,
                new Vector3(0.025f, 0.04f, 0.02f) * scale, accent);

        // Barrel tip
        AddPart(root, "BarrelTip", PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0.28f) * scale,
                new Vector3(0.028f, 0.025f, 0.028f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        // Barrel hole
        AddPart(root, "BarrelHole", PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0.305f) * scale,
                new Vector3(0.018f, 0.005f, 0.018f) * scale, gripPlastic,
                new Vector3(90, 0, 0));

        // Front + rear sights
        AddPart(root, "FrontSight", PrimitiveType.Cube, new Vector3(0, 0.105f, 0.22f) * scale,
                new Vector3(0.012f, 0.022f, 0.018f) * scale, accent);
        AddPart(root, "RearSight", PrimitiveType.Cube, new Vector3(0, 0.105f, -0.02f) * scale,
                new Vector3(0.045f, 0.02f, 0.022f) * scale, darkMetal);

        // Magazine bottom (visible)
        AddPart(root, "MagBottom", PrimitiveType.Cube, new Vector3(0, -0.27f, -0.025f) * scale,
                new Vector3(0.07f, 0.015f, 0.10f) * scale, accent,
                new Vector3(12, 0, 0));
    }

    public void BuildRifleModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // Material variation: AK-47 vibe — dark metal body, wood grips/stock
        Material darkMetal = CreatePBRMaterial(new Color(0.10f, 0.10f, 0.10f), 0.85f, 0.55f);
        Material wood = CreatePBRMaterial(new Color(0.4f, 0.22f, 0.10f), 0f, 0.35f);
        Material magMetal = CreatePBRMaterial(new Color(0.18f, 0.16f, 0.12f), 0.6f, 0.4f);
        Material accent = CreatePBRMaterial(new Color(0.55f, 0.45f, 0.20f), 0.4f, 0.5f);

        // Receiver
        AddPart(root, "Receiver", PrimitiveType.Cube, new Vector3(0, 0, 0.20f) * scale,
                new Vector3(0.075f, 0.11f, 0.50f) * scale, darkMetal);

        // Top cover (raised)
        AddPart(root, "TopCover", PrimitiveType.Cube, new Vector3(0, 0.07f, 0.20f) * scale,
                new Vector3(0.06f, 0.04f, 0.30f) * scale, darkMetal);

        // Barrel
        AddPart(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0.60f) * scale,
                new Vector3(0.022f, 0.20f, 0.022f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        // Muzzle / flash hider
        AddPart(root, "Muzzle", PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0.82f) * scale,
                new Vector3(0.035f, 0.04f, 0.035f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        AddPart(root, "MuzzleHole", PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0.86f) * scale,
                new Vector3(0.022f, 0.005f, 0.022f) * scale, accent,
                new Vector3(90, 0, 0));

        // Gas tube above barrel
        AddPart(root, "GasTube", PrimitiveType.Cylinder, new Vector3(0, 0.075f, 0.55f) * scale,
                new Vector3(0.018f, 0.12f, 0.018f) * scale, darkMetal,
                new Vector3(90, 0, 0));

        // Curved magazine (3 angled segments to fake the curve)
        AddPart(root, "MagTop", PrimitiveType.Cube, new Vector3(0, -0.10f, 0.22f) * scale,
                new Vector3(0.062f, 0.07f, 0.10f) * scale, magMetal,
                new Vector3(-15, 0, 0));
        AddPart(root, "MagMid", PrimitiveType.Cube, new Vector3(0, -0.18f, 0.18f) * scale,
                new Vector3(0.062f, 0.08f, 0.10f) * scale, magMetal,
                new Vector3(-25, 0, 0));
        AddPart(root, "MagBot", PrimitiveType.Cube, new Vector3(0, -0.25f, 0.13f) * scale,
                new Vector3(0.062f, 0.05f, 0.10f) * scale, magMetal,
                new Vector3(-35, 0, 0));

        // Grip (wood/plastic)
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.14f, 0.04f) * scale,
                new Vector3(0.06f, 0.18f, 0.07f) * scale, wood,
                new Vector3(22, 0, 0));

        // Wooden stock (AK style)
        AddPart(root, "StockNeck", PrimitiveType.Cube, new Vector3(0, -0.02f, -0.10f) * scale,
                new Vector3(0.06f, 0.06f, 0.14f) * scale, wood);
        AddPart(root, "Stock", PrimitiveType.Cube, new Vector3(0, -0.04f, -0.25f) * scale,
                new Vector3(0.06f, 0.13f, 0.20f) * scale, wood);
        // Stock pad
        AddPart(root, "StockPad", PrimitiveType.Cube, new Vector3(0, -0.04f, -0.36f) * scale,
                new Vector3(0.062f, 0.16f, 0.025f) * scale, darkMetal);

        // Sights
        AddPart(root, "FrontSightPost", PrimitiveType.Cube, new Vector3(0, 0.10f, 0.70f) * scale,
                new Vector3(0.025f, 0.05f, 0.025f) * scale, darkMetal);
        AddPart(root, "FrontSightBlade", PrimitiveType.Cube, new Vector3(0, 0.13f, 0.70f) * scale,
                new Vector3(0.005f, 0.03f, 0.015f) * scale, accent);
        AddPart(root, "RearSight", PrimitiveType.Cube, new Vector3(0, 0.095f, 0.0f) * scale,
                new Vector3(0.045f, 0.02f, 0.04f) * scale, darkMetal);

        // Handguard (wood)
        AddPart(root, "HandguardLow", PrimitiveType.Cube, new Vector3(0, -0.04f, 0.50f) * scale,
                new Vector3(0.07f, 0.06f, 0.20f) * scale, wood);
        AddPart(root, "HandguardUp", PrimitiveType.Cube, new Vector3(0, 0.05f, 0.50f) * scale,
                new Vector3(0.07f, 0.04f, 0.20f) * scale, wood);

        // Trigger + guard
        AddPart(root, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.08f, 0.04f) * scale,
                new Vector3(0.04f, 0.025f, 0.08f) * scale, darkMetal);
        AddPart(root, "Trigger", PrimitiveType.Cube, new Vector3(0, -0.075f, 0.04f) * scale,
                new Vector3(0.02f, 0.035f, 0.018f) * scale, accent);

        // Charging handle (right side)
        AddPart(root, "ChargingHandle", PrimitiveType.Cube, new Vector3(0.045f, 0.05f, 0.12f) * scale,
                new Vector3(0.015f, 0.02f, 0.06f) * scale, darkMetal);
    }

    public void BuildSniperModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // AWP-style — green/military
        Material darkMetal = CreatePBRMaterial(new Color(0.10f, 0.10f, 0.10f), 0.9f, 0.55f);
        Material body = CreatePBRMaterial(new Color(0.18f, 0.22f, 0.13f), 0.1f, 0.4f); // OD green
        Material lens = CreatePBRMaterial(new Color(0.05f, 0.20f, 0.10f), 0.95f, 0.95f, new Color(0.05f, 0.4f, 0.15f));
        Material accent = CreatePBRMaterial(new Color(0.55f, 0.45f, 0.20f), 0.4f, 0.5f);

        // Stock (long, military green)
        AddPart(root, "StockButt", PrimitiveType.Cube, new Vector3(0, -0.06f, -0.30f) * scale,
                new Vector3(0.07f, 0.20f, 0.18f) * scale, body);
        AddPart(root, "StockNeck", PrimitiveType.Cube, new Vector3(0, -0.04f, -0.10f) * scale,
                new Vector3(0.07f, 0.10f, 0.20f) * scale, body);
        AddPart(root, "StockPad", PrimitiveType.Cube, new Vector3(0, -0.06f, -0.42f) * scale,
                new Vector3(0.072f, 0.22f, 0.025f) * scale, darkMetal);

        // Cheek rest (raised)
        AddPart(root, "CheekRest", PrimitiveType.Cube, new Vector3(0, 0.08f, -0.20f) * scale,
                new Vector3(0.07f, 0.04f, 0.18f) * scale, body);

        // Receiver
        AddPart(root, "Receiver", PrimitiveType.Cube, new Vector3(0, 0, 0.15f) * scale,
                new Vector3(0.075f, 0.11f, 0.50f) * scale, darkMetal);

        // Bolt handle
        AddPart(root, "Bolt", PrimitiveType.Cylinder, new Vector3(0.05f, 0.04f, 0.10f) * scale,
                new Vector3(0.012f, 0.04f, 0.012f) * scale, darkMetal,
                new Vector3(0, 0, 90));
        AddPart(root, "BoltKnob", PrimitiveType.Sphere, new Vector3(0.085f, 0.04f, 0.10f) * scale,
                new Vector3(0.022f, 0.022f, 0.022f) * scale, darkMetal);

        // Long barrel
        AddPart(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0, 0.025f, 0.65f) * scale,
                new Vector3(0.024f, 0.30f, 0.024f) * scale, darkMetal,
                new Vector3(90, 0, 0));

        // Muzzle brake (multi-port)
        AddPart(root, "MuzzleBrake", PrimitiveType.Cube, new Vector3(0, 0.025f, 0.96f) * scale,
                new Vector3(0.045f, 0.045f, 0.08f) * scale, darkMetal);
        AddPart(root, "BrakePort1", PrimitiveType.Cube, new Vector3(0.025f, 0.025f, 0.94f) * scale,
                new Vector3(0.005f, 0.025f, 0.04f) * scale, accent);
        AddPart(root, "BrakePort2", PrimitiveType.Cube, new Vector3(-0.025f, 0.025f, 0.94f) * scale,
                new Vector3(0.005f, 0.025f, 0.04f) * scale, accent);

        // Grip
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.14f, 0.0f) * scale,
                new Vector3(0.06f, 0.18f, 0.07f) * scale, body,
                new Vector3(20, 0, 0));

        // Trigger guard
        AddPart(root, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.085f, 0.05f) * scale,
                new Vector3(0.04f, 0.025f, 0.10f) * scale, darkMetal);
        AddPart(root, "Trigger", PrimitiveType.Cube, new Vector3(0, -0.08f, 0.05f) * scale,
                new Vector3(0.018f, 0.04f, 0.018f) * scale, accent);

        // Magazine
        AddPart(root, "Magazine", PrimitiveType.Cube, new Vector3(0, -0.13f, 0.22f) * scale,
                new Vector3(0.06f, 0.16f, 0.10f) * scale, body);

        // Scope mounts (rings)
        AddPart(root, "ScopeMount1", PrimitiveType.Cube, new Vector3(0, 0.085f, 0.10f) * scale,
                new Vector3(0.045f, 0.05f, 0.035f) * scale, darkMetal);
        AddPart(root, "ScopeMount2", PrimitiveType.Cube, new Vector3(0, 0.085f, 0.30f) * scale,
                new Vector3(0.045f, 0.05f, 0.035f) * scale, darkMetal);

        // Scope main body (large, multi-section)
        AddPart(root, "ScopeRear", PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0.05f) * scale,
                new Vector3(0.06f, 0.05f, 0.06f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        AddPart(root, "ScopeBody", PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0.20f) * scale,
                new Vector3(0.045f, 0.12f, 0.045f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        AddPart(root, "ScopeTurret", PrimitiveType.Cylinder, new Vector3(0, 0.18f, 0.20f) * scale,
                new Vector3(0.025f, 0.025f, 0.025f) * scale, darkMetal);
        AddPart(root, "ScopeFrontBell", PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0.36f) * scale,
                new Vector3(0.06f, 0.05f, 0.06f) * scale, darkMetal,
                new Vector3(90, 0, 0));
        // Front lens (slight green tint, emissive subtle)
        AddPart(root, "ScopeLens", PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0.41f) * scale,
                new Vector3(0.055f, 0.005f, 0.055f) * scale, lens,
                new Vector3(90, 0, 0));
        // Rear lens
        AddPart(root, "ScopeRearLens", PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0.0f) * scale,
                new Vector3(0.055f, 0.005f, 0.055f) * scale, lens,
                new Vector3(90, 0, 0));

        // Bipod legs (visible)
        AddPart(root, "BipodMount", PrimitiveType.Cube, new Vector3(0, -0.06f, 0.55f) * scale,
                new Vector3(0.04f, 0.04f, 0.06f) * scale, darkMetal);
        AddPart(root, "BipodLegL", PrimitiveType.Cube, new Vector3(-0.06f, -0.14f, 0.55f) * scale,
                new Vector3(0.012f, 0.16f, 0.012f) * scale, darkMetal,
                new Vector3(0, 0, 20));
        AddPart(root, "BipodLegR", PrimitiveType.Cube, new Vector3(0.06f, -0.14f, 0.55f) * scale,
                new Vector3(0.012f, 0.16f, 0.012f) * scale, darkMetal,
                new Vector3(0, 0, -20));
    }
}
