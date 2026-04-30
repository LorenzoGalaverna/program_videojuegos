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

    [Header("Custom Prefabs (opcional - dejá vacío para usar los procedurales)")]
    public GameObject pistolPrefab;
    public GameObject riflePrefab;
    public GameObject sniperPrefab;
    public GameObject botBodyPrefab;
    [Tooltip("Escala extra para aplicar al prefab del arma")]
    public float weaponPrefabScale = 1f;
    [Tooltip("Offset extra para posicionar el prefab del arma (X, Y, Z)")]
    public Vector3 weaponPrefabOffset = Vector3.zero;
    [Tooltip("Rotación extra para el prefab del arma (X, Y, Z) en grados")]
    public Vector3 weaponPrefabRotation = Vector3.zero;

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
        // fallback
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
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

        // Lighting - add a directional light
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

        // Movement
        PlayerMovement movement = player.AddComponent<PlayerMovement>();

        // Health
        PlayerHealth health = player.AddComponent<PlayerHealth>();

        // Camera
        GameObject camHolder = new GameObject("CameraHolder");
        camHolder.transform.parent = player.transform;
        camHolder.transform.localPosition = new Vector3(0, 1.7f, 0);

        // Destroy existing main camera if any
        Camera existingCam = Camera.main;
        if (existingCam) Destroy(existingCam.gameObject);

        GameObject camObj = new GameObject("PlayerCamera");
        camObj.tag = "MainCamera";
        camObj.transform.parent = camHolder.transform;
        camObj.transform.localPosition = Vector3.zero;
        Camera cam = camObj.AddComponent<Camera>();
        cam.fieldOfView = 70;
        cam.nearClipPlane = 0.01f;
        camObj.AddComponent<AudioListener>();

        // HDRP requires this component on cameras
        var hdCamData = camObj.AddComponent<HDAdditionalCameraData>();

        // Mouse Look
        MouseLook mouseLook = camHolder.AddComponent<MouseLook>();
        mouseLook.playerBody = player.transform;

        // Weapon Holder - positioned to bottom-right of view
        GameObject weaponHolder = new GameObject("WeaponHolder");
        weaponHolder.transform.parent = camObj.transform;
        weaponHolder.transform.localPosition = new Vector3(0.3f, -0.25f, 0.5f);

        // Create weapons
        Weapon pistol = CreateWeaponObject("Pistol", weaponHolder.transform, WeaponType.Pistol);
        Weapon rifle = CreateWeaponObject("AK-47", weaponHolder.transform, WeaponType.Rifle);
        Weapon sniper = CreateWeaponObject("AWP", weaponHolder.transform, WeaponType.Sniper);

        // Weapon Manager
        WeaponManager wm = player.AddComponent<WeaponManager>();
        wm.weaponHolder = weaponHolder.transform;
        wm.cameraTransform = camObj.transform;
        wm.playerCamera = cam;
        wm.weapons = new Weapon[] { pistol, rifle, sniper };
        wm.startWeaponIndex = 1; // Start with rifle

        // HUD
        GameHUD hud = player.AddComponent<GameHUD>();
        hud.playerHealth = health;
        hud.weaponManager = wm;

        // Death handler
        health.onDeath.AddListener(() =>
        {
            if (GameManager.Instance)
            {
                GameManager.Instance.AddEnemyKill();
                // Respawn after 3 seconds
                Invoke("RespawnPlayer", 3f);
            }
        });

        return player.transform;
    }

    private void RespawnPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player && GameManager.Instance)
        {
            GameManager.Instance.RespawnPlayer(player, 0);
        }
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
        }

        if (prefabToUse != null)
        {
            GameObject inst = Instantiate(prefabToUse, visual.transform);
            inst.transform.localPosition = weaponPrefabOffset;
            inst.transform.localRotation = Quaternion.Euler(weaponPrefabRotation);
            inst.transform.localScale = Vector3.one * weaponPrefabScale;
            // Strip any colliders from the visual prefab so they don't block raycasts
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
        }

        data.holdPosition = new Vector3(0.2f, -0.15f, 0.4f);
        data.adsPosition = new Vector3(0f, -0.08f, 0.3f);

        weapon.data = data;

        // Audio source
        AudioSource audio = weaponObj.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        weapon.audioSource = audio;

        // Muzzle point
        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.parent = weaponObj.transform;
        muzzle.transform.localPosition = new Vector3(0, 0, 0.5f);
        weapon.muzzlePoint = muzzle.transform;

        weaponObj.SetActive(false);
        return weapon;
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

        // Body hitbox (separate so headshot tag works on head only)
        GameObject hitbox = new GameObject("BodyHitbox");
        hitbox.transform.parent = bot.transform;
        hitbox.transform.localPosition = new Vector3(0, 1f, 0);
        CapsuleCollider hitCol = hitbox.AddComponent<CapsuleCollider>();
        hitCol.height = 1.8f;
        hitCol.radius = 0.4f;

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

    public void BuildPistolModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // Slide / upper body
        AddPart(root, "Slide", PrimitiveType.Cube, new Vector3(0, 0.04f, 0.10f) * scale,
                new Vector3(0.06f, 0.08f, 0.30f) * scale, gunMat);
        // Lower frame
        AddPart(root, "Frame", PrimitiveType.Cube, new Vector3(0, -0.04f, 0.05f) * scale,
                new Vector3(0.06f, 0.08f, 0.20f) * scale, gunMat);
        // Grip
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.16f, -0.02f) * scale,
                new Vector3(0.07f, 0.20f, 0.10f) * scale, gunMat,
                new Vector3(15, 0, 0));
        // Trigger guard
        AddPart(root, "TriggerGuard", PrimitiveType.Cube, new Vector3(0, -0.07f, 0.02f) * scale,
                new Vector3(0.05f, 0.06f, 0.06f) * scale, accentMat);
        // Barrel tip
        AddPart(root, "BarrelTip", PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0.27f) * scale,
                new Vector3(0.025f, 0.02f, 0.025f) * scale, gunMat,
                new Vector3(90, 0, 0));
        // Sight dot
        AddPart(root, "Sight", PrimitiveType.Cube, new Vector3(0, 0.10f, 0.20f) * scale,
                new Vector3(0.015f, 0.025f, 0.02f) * scale, accentMat);
    }

    public void BuildRifleModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // Receiver / body
        AddPart(root, "Receiver", PrimitiveType.Cube, new Vector3(0, 0, 0.20f) * scale,
                new Vector3(0.07f, 0.10f, 0.45f) * scale, gunMat);
        // Barrel
        AddPart(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.55f) * scale,
                new Vector3(0.025f, 0.18f, 0.025f) * scale, gunMat,
                new Vector3(90, 0, 0));
        // Magazine
        AddPart(root, "Magazine", PrimitiveType.Cube, new Vector3(0, -0.13f, 0.18f) * scale,
                new Vector3(0.06f, 0.18f, 0.10f) * scale, accentMat,
                new Vector3(-10, 0, 0));
        // Grip
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.13f, 0.05f) * scale,
                new Vector3(0.06f, 0.18f, 0.07f) * scale, gunMat,
                new Vector3(20, 0, 0));
        // Stock
        AddPart(root, "Stock", PrimitiveType.Cube, new Vector3(0, 0, -0.18f) * scale,
                new Vector3(0.06f, 0.12f, 0.30f) * scale, gunMat);
        // Buttstock pad
        AddPart(root, "StockPad", PrimitiveType.Cube, new Vector3(0, 0, -0.34f) * scale,
                new Vector3(0.06f, 0.16f, 0.05f) * scale, accentMat);
        // Front sight
        AddPart(root, "FrontSight", PrimitiveType.Cube, new Vector3(0, 0.09f, 0.55f) * scale,
                new Vector3(0.015f, 0.04f, 0.02f) * scale, accentMat);
        // Rear sight
        AddPart(root, "RearSight", PrimitiveType.Cube, new Vector3(0, 0.09f, 0.10f) * scale,
                new Vector3(0.025f, 0.03f, 0.02f) * scale, accentMat);
        // Foregrip / handguard
        AddPart(root, "Handguard", PrimitiveType.Cube, new Vector3(0, -0.02f, 0.45f) * scale,
                new Vector3(0.06f, 0.07f, 0.18f) * scale, gunMat);
    }

    public void BuildSniperModel(Transform root, Material gunMat, Material accentMat, float scale = 1f)
    {
        // Receiver
        AddPart(root, "Receiver", PrimitiveType.Cube, new Vector3(0, 0, 0.15f) * scale,
                new Vector3(0.07f, 0.10f, 0.50f) * scale, gunMat);
        // Long barrel
        AddPart(root, "Barrel", PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.65f) * scale,
                new Vector3(0.025f, 0.30f, 0.025f) * scale, gunMat,
                new Vector3(90, 0, 0));
        // Muzzle brake
        AddPart(root, "Muzzle", PrimitiveType.Cube, new Vector3(0, 0.02f, 0.95f) * scale,
                new Vector3(0.05f, 0.05f, 0.06f) * scale, gunMat);
        // Stock
        AddPart(root, "Stock", PrimitiveType.Cube, new Vector3(0, -0.02f, -0.20f) * scale,
                new Vector3(0.07f, 0.14f, 0.40f) * scale, gunMat);
        // Grip
        AddPart(root, "Grip", PrimitiveType.Cube, new Vector3(0, -0.14f, 0.0f) * scale,
                new Vector3(0.06f, 0.18f, 0.07f) * scale, gunMat,
                new Vector3(20, 0, 0));
        // Magazine
        AddPart(root, "Magazine", PrimitiveType.Cube, new Vector3(0, -0.12f, 0.20f) * scale,
                new Vector3(0.06f, 0.14f, 0.10f) * scale, gunMat);
        // Scope body
        AddPart(root, "Scope", PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0.20f) * scale,
                new Vector3(0.05f, 0.16f, 0.05f) * scale, gunMat,
                new Vector3(90, 0, 0));
        // Scope front lens
        AddPart(root, "ScopeFront", PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0.36f) * scale,
                new Vector3(0.055f, 0.02f, 0.055f) * scale, accentMat,
                new Vector3(90, 0, 0));
        // Scope mounts
        AddPart(root, "ScopeMount1", PrimitiveType.Cube, new Vector3(0, 0.08f, 0.12f) * scale,
                new Vector3(0.04f, 0.04f, 0.03f) * scale, gunMat);
        AddPart(root, "ScopeMount2", PrimitiveType.Cube, new Vector3(0, 0.08f, 0.28f) * scale,
                new Vector3(0.04f, 0.04f, 0.03f) * scale, gunMat);
        // Bipod (folded)
        AddPart(root, "Bipod", PrimitiveType.Cube, new Vector3(0, -0.06f, 0.55f) * scale,
                new Vector3(0.04f, 0.04f, 0.10f) * scale, gunMat);
    }
}
