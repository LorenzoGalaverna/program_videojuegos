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

        if (buildMap) BuildMap();

        Transform playerT = null;
        if (buildPlayer) playerT = BuildPlayer();

        if (buildBot) BuildBot();

        if (buildGameManager) BuildGameManager(playerT);

        NavMeshSurface surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface) surface.BuildNavMesh();

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

        // Visual representation (simple cube as placeholder)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Model";
        visual.transform.parent = weaponObj.transform;
        Destroy(visual.GetComponent<Collider>());

        // Dark gun color
        Renderer weaponRend = visual.GetComponent<Renderer>();
        if (weaponRend)
        {
            Material gunMat = new Material(Shader.Find("HDRP/Lit"));
            gunMat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f)); // dark gray/black
            weaponRend.material = gunMat;
        }

        // Configure based on type
        Weapon weapon = weaponObj.AddComponent<Weapon>();
        WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
        data.weaponName = name;
        data.weaponType = type;

        switch (type)
        {
            case WeaponType.Pistol:
                visual.transform.localScale = new Vector3(0.08f, 0.15f, 0.25f);
                visual.transform.localPosition = new Vector3(0, 0, 0.12f);
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
                visual.transform.localScale = new Vector3(0.08f, 0.13f, 0.6f);
                visual.transform.localPosition = new Vector3(0, 0, 0.3f);
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
                visual.transform.localScale = new Vector3(0.07f, 0.1f, 0.85f);
                visual.transform.localPosition = new Vector3(0, 0, 0.4f);
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
        // Spawn closer to center so player can find it
        bot.transform.position = new Vector3(15, 1.5f, 15);

        // Bright emissive material for visibility
        Material botMat = new Material(Shader.Find("HDRP/Lit"));
        botMat.SetColor("_BaseColor", new Color(1f, 0.15f, 0.15f));
        if (botMat.HasProperty("_EmissiveColor"))
        {
            botMat.SetColor("_EmissiveColor", new Color(1.5f, 0f, 0f));
            botMat.EnableKeyword("_EMISSION");
        }

        // Capsule body (no collider — CharacterController handles physics)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.parent = bot.transform;
        body.transform.localPosition = new Vector3(0, 1f, 0);
        Destroy(body.GetComponent<Collider>());
        body.GetComponent<Renderer>().material = botMat;

        // Head (with collider for headshot detection)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.tag = "Head";
        head.transform.parent = bot.transform;
        head.transform.localPosition = new Vector3(0, 2.1f, 0);
        head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
        head.GetComponent<Renderer>().material = botMat;

        // Body hitbox (separate from visual so headshot tag works correctly)
        GameObject hitbox = new GameObject("BodyHitbox");
        hitbox.transform.parent = bot.transform;
        hitbox.transform.localPosition = new Vector3(0, 1f, 0);
        CapsuleCollider hitCol = hitbox.AddComponent<CapsuleCollider>();
        hitCol.height = 1.6f;
        hitCol.radius = 0.4f;
        hitCol.isTrigger = false;

        // Eye point
        GameObject eye = new GameObject("EyePoint");
        eye.transform.parent = bot.transform;
        eye.transform.localPosition = new Vector3(0, 2f, 0.3f);

        // CharacterController for movement
        CharacterController botCC = bot.AddComponent<CharacterController>();
        botCC.height = 2f;
        botCC.radius = 0.4f;
        botCC.center = new Vector3(0, 1f, 0);

        // Health
        PlayerHealth health = bot.AddComponent<PlayerHealth>();

        // Bot AI — start aggressive so it always seeks the player
        EnemyBot botAI = bot.AddComponent<EnemyBot>();
        botAI.eyePoint = eye.transform;
        botAI.accuracy = 0.55f;
        botAI.reactionTime = 0.5f;
        botAI.detectionRange = 80f; // larger so it spots player anywhere on map

        Debug.Log($"[SceneSetup] Bot spawned at {bot.transform.position}");
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
}
