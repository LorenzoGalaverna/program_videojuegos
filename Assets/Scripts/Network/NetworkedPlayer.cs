using UnityEngine;
using Mirror;

// Networked player wrapper for the real OutdoorsScene flow.
// On the LOCAL machine: equips this GameObject with the full first-person setup
// (PlayerMovement, MouseLook, WeaponManager, HUD, camera), and warps to a spawn point.
// On REMOTE machines: leaves the prefab as a 3rd-person model so other players see us.
public class NetworkedPlayer : NetworkBehaviour
{
    [Tooltip("Optional 3rd-person visual (e.g. Swat model). Hidden on the local client because we use 1st person.")]
    public GameObject thirdPersonModel;
    [Tooltip("Scale of the 3rd-person model as seen by other players (1 = original).")]
    public float thirdPersonScale = 1.7f;

    // Assigned by NetworkLobby when this player is spawned (0 = first/host, 1 = joiner)
    [SyncVar] public int SpawnSlot;

    // Animation state replicated to remote clients so they see movement and shooting
    [SyncVar(hook = nameof(OnSyncSpeedChanged))]    private float syncSpeed;
    [SyncVar(hook = nameof(OnSyncShootingChanged))] private bool  syncIsShooting;

    // Currently held weapon index — remote clients hide the rifle prop when knife (index 3) is equipped
    [SyncVar(hook = nameof(OnWeaponIndexSynced))] private int syncWeaponIndex = 1;

    private CharacterController cachedCC;
    private Animator thirdPersonAnimator;
    private Transform thirdPersonGun;   // cached reference to the attached rifle prop
    private float lastAnimSyncTime;
    private int   lastSentWeaponIndex = -1;
    private const float AnimSyncInterval = 0.05f; // 20 Hz

    private static readonly int HashSpeed      = Animator.StringToHash("Speed");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");

    public override void OnStartLocalPlayer()
    {
        // Hide the 3rd-person body for our own view, but KEEP the GameObject + Animator
        // active so SyncVars can still sync parameters to remote clients.
        if (thirdPersonModel != null)
        {
            foreach (var rend in thirdPersonModel.GetComponentsInChildren<Renderer>())
                rend.enabled = false;
        }

        SceneSetup setup = FindAnyObjectByType<SceneSetup>();
        if (setup != null) setup.EquipPlayer(gameObject);
        else Debug.LogError("[NetworkedPlayer] SceneSetup not found — local player is not equipped.");

        WarpToSpawn();
    }

    private void Start()
    {
        BuildHitboxes();

        if (!isLocalPlayer)
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (thirdPersonModel != null)
                thirdPersonModel.transform.localScale = Vector3.one * thirdPersonScale;

            SceneSetup setup = FindAnyObjectByType<SceneSetup>();
            if (setup != null && thirdPersonModel != null)
            {
                GameObject gunObj = setup.AttachRifleToHumanoidHand(thirdPersonModel.transform);
                thirdPersonGun = gunObj != null ? gunObj.transform : null;
                // Apply current weapon state in case syncWeaponIndex already arrived
                UpdateThirdPersonWeaponVisual(syncWeaponIndex);
            }
        }
    }

    private void BuildHitboxes()
    {
        if (transform.Find("BodyHitbox") != null) return;

        float scale = Mathf.Max(thirdPersonScale, 0.5f);

        GameObject body = new GameObject("BodyHitbox");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0, 1f * scale, 0);
        var capsule = body.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f * scale;
        capsule.radius = 0.4f * scale;

        GameObject head = new GameObject("Head");
        head.tag = "Head";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0, 1.75f * scale, 0);
        var sphere = head.AddComponent<SphereCollider>();
        sphere.radius = 0.18f * scale;
    }

    private void WarpToSpawn() { /* position is set server-side in OnServerAddPlayer */ }

    private void Update()
    {
        if (!isLocalPlayer) return;
        UpdateAnimatorParams();
    }

    private void UpdateAnimatorParams()
    {
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator == null) return;

        if (cachedCC == null) cachedCC = GetComponent<CharacterController>();
        Vector3 vel = cachedCC != null ? cachedCC.velocity : Vector3.zero;
        vel.y = 0f;
        float speed    = vel.magnitude;
        bool  shooting = Input.GetMouseButton(0);

        thirdPersonAnimator.SetFloat(HashSpeed,      speed, 0.1f, Time.deltaTime);
        thirdPersonAnimator.SetBool(HashIsShooting,  shooting);

        // Sync animation state to remote clients at 20 Hz
        if (Time.time - lastAnimSyncTime >= AnimSyncInterval)
        {
            lastAnimSyncTime = Time.time;
            CmdUpdateAnimState(speed, shooting);
        }

        // Sync weapon index whenever the local player switches weapons
        WeaponManager wm = GetComponent<WeaponManager>();
        if (wm != null && wm.CurrentWeapon != null)
        {
            int idx = System.Array.IndexOf(wm.weapons, wm.CurrentWeapon);
            if (idx >= 0 && idx != lastSentWeaponIndex)
            {
                lastSentWeaponIndex = idx;
                CmdUpdateWeapon(idx);
            }
        }
    }

    // ─── Animation sync Commands / hooks ───────────────────────────────────────

    [Command]
    private void CmdUpdateAnimState(float speed, bool shooting)
    {
        syncSpeed      = speed;
        syncIsShooting = shooting;
    }

    private void OnSyncSpeedChanged(float old, float newVal)
    {
        if (isLocalPlayer) return;
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        thirdPersonAnimator?.SetFloat(HashSpeed, newVal);
    }

    private void OnSyncShootingChanged(bool old, bool newVal)
    {
        if (isLocalPlayer) return;
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        thirdPersonAnimator?.SetBool(HashIsShooting, newVal);
    }

    // ─── Weapon sync Commands / hooks ─────────────────────────────────────────

    [Command]
    private void CmdUpdateWeapon(int index) => syncWeaponIndex = index;

    private void OnWeaponIndexSynced(int old, int newVal)
    {
        if (isLocalPlayer) return;
        UpdateThirdPersonWeaponVisual(newVal);
    }

    private void UpdateThirdPersonWeaponVisual(int weaponIndex)
    {
        if (thirdPersonGun == null) return;
        // index 3 = knife (no visible gun prop); every other weapon shows the rifle
        bool showGun = weaponIndex != 3;
        foreach (var r in thirdPersonGun.GetComponentsInChildren<Renderer>(true))
            r.enabled = showGun;
    }

    // ─── Tracer sync (so other clients see this player's gunshots) ─────────────

    public void BroadcastTracer(Vector3 from, Vector3 to)
    {
        if (!isLocalPlayer) return;
        CmdSpawnTracer(from, to);
    }

    [Command]
    private void CmdSpawnTracer(Vector3 from, Vector3 to) => RpcSpawnTracer(from, to);

    [ClientRpc(includeOwner = false)]
    private void RpcSpawnTracer(Vector3 from, Vector3 to) => BulletEffects.SpawnTracer(from, to);

    // ─── Score / End-game delivery (called server-side by NetworkLobby) ──────────

    [ClientRpc]
    public void RpcReceiveScores(int myKills, int enemyKills)
    {
        if (!isLocalPlayer) return;
        GameManager.Instance?.SyncNetworkScores(myKills, enemyKills);
    }

    [ClientRpc]
    public void RpcEndGame(int myKills, int enemyKills)
    {
        if (!isLocalPlayer) return;
        GameManager.Instance?.SyncNetworkScores(myKills, enemyKills);
        GameManager.Instance?.EndGame();
    }

    // ─── Damage routing ────────────────────────────────────────────────────────

    public void RequestDamage(GameObject hitObject, int damage, bool isHeadshot)
    {
        if (!isLocalPlayer || hitObject == null) return;
        var targetIdentity = hitObject.GetComponentInParent<NetworkIdentity>();
        if (targetIdentity == null) return;
        CmdApplyDamage(targetIdentity, damage, isHeadshot);
    }

    [Command]
    private void CmdApplyDamage(NetworkIdentity target, int damage, bool isHeadshot)
    {
        if (target == null) return;
        var ph = target.GetComponent<PlayerHealth>();
        if (ph != null) ph.TakeDamage(damage, isHeadshot);
    }

    // ─── Server-driven respawn (called by NetworkLobby after the delay) ────────

    [ClientRpc]
    public void RpcRespawnAt(Vector3 pos, Quaternion rot)
    {
        if (!isLocalPlayer) return;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        transform.position = pos;
        transform.rotation = rot;
        if (cc) cc.enabled = true;

        var pm = GetComponent<PlayerMovement>();
        var wm = GetComponent<WeaponManager>();
        if (pm) pm.enabled = true;
        if (wm) wm.enabled = true;

        MouseLook ml = GetComponentInChildren<MouseLook>();
        if (ml) ml.ResetLook();
    }
}
