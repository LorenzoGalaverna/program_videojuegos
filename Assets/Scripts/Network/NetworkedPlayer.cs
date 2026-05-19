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
    public float thirdPersonScale = 1f;

    // Assigned by NetworkLobby when this player is spawned (0 = first/host, 1 = joiner)
    [SyncVar] public int SpawnSlot;

    // Animation state replicated to remote clients so they see movement and shooting
    [SyncVar(hook = nameof(OnSyncSpeedChanged))]    private float syncSpeed;
    [SyncVar(hook = nameof(OnSyncShootingChanged))] private bool  syncIsShooting;
    [SyncVar] private bool syncIsCrouching;

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

    private static readonly int HashDie = Animator.StringToHash("Die");

    private void Start()
    {
        BuildHitboxes();

        // Always force root motion OFF on the body animator. Mixamo FBXs default to it
        // ON, which makes the animation drive the GameObject position — fighting both
        // CharacterController (local) and NetworkTransform (remote), causing drift.
        Animator anim = thirdPersonModel != null ? thirdPersonModel.GetComponentInChildren<Animator>(true) : null;
        if (anim != null) anim.applyRootMotion = false;

        // Listen for death so every client plays the dying animation locally.
        var ph = GetComponent<PlayerHealth>();
        if (ph != null) ph.onDeath.AddListener(PlayDeathAnimation);

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
                UpdateThirdPersonWeaponVisual(syncWeaponIndex);
            }
        }
    }

    private void BuildHitboxes()
    {
        if (transform.Find("BodyHitbox") != null) return;

        // Fixed-size hitboxes (NOT scaled by thirdPersonScale) so combat geometry stays
        // consistent regardless of the visual model scale. Sized for a ~1.8m character.
        GameObject body = new GameObject("BodyHitbox");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0, 0.8f, 0);
        var capsule = body.AddComponent<CapsuleCollider>();
        capsule.height = 1.4f;
        capsule.radius = 0.35f;

        GameObject head = new GameObject("Head");
        head.tag = "Head";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0, 1.7f, 0);
        var sphere = head.AddComponent<SphereCollider>();
        sphere.radius = 0.22f;
    }

    private void WarpToSpawn() { /* position is set server-side in OnServerAddPlayer */ }

    private Vector3 lastRemotePos;
    private float remoteSpeedSmoothed;

    private void Update()
    {
        if (isLocalPlayer) { UpdateAnimatorParams(); return; }
        UpdateRemoteAnimator();
    }

    // Drives the 3rd-person Animator on REMOTE clients by measuring how fast the
    // transform is being moved by NetworkTransform replication. This avoids relying
    // on SyncVars firing reliably for fast-changing values.
    private void UpdateRemoteAnimator()
    {
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator == null) return;

        Vector3 delta = transform.position - lastRemotePos;
        delta.y = 0f;
        float observed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastRemotePos = transform.position;

        // Smooth so the value doesn't jitter from per-frame position deltas.
        remoteSpeedSmoothed = Mathf.Lerp(remoteSpeedSmoothed, observed, 8f * Time.deltaTime);
        thirdPersonAnimator.SetFloat(HashSpeed, remoteSpeedSmoothed);
        thirdPersonAnimator.SetBool(HashIsShooting, syncIsShooting);

        // Crouch visual: lower the 3rd-person model on Y when the owner is crouched.
        if (thirdPersonModel != null)
        {
            float targetY = syncIsCrouching ? -0.5f : 0f;
            Vector3 lp = thirdPersonModel.transform.localPosition;
            lp.y = Mathf.Lerp(lp.y, targetY, 10f * Time.deltaTime);
            thirdPersonModel.transform.localPosition = lp;
        }
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
            PlayerMovement pm = GetComponent<PlayerMovement>();
            bool crouching = pm != null && pm.IsCrouching;
            CmdUpdateAnimState(speed, shooting, crouching);
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
    private void CmdUpdateAnimState(float speed, bool shooting, bool crouching)
    {
        syncSpeed       = speed;
        syncIsShooting  = shooting;
        syncIsCrouching = crouching;
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
    // Only the impact point is replicated — remote clients derive the origin from
    // the visible 3rd-person rifle so the tracer always looks like it leaves the gun
    // (instead of floating in front of the shooter's old camera position).

    public void BroadcastTracer(Vector3 _ignoredFrom, Vector3 to)
    {
        if (!isLocalPlayer) return;
        CmdSpawnTracer(to);
    }

    [Command]
    private void CmdSpawnTracer(Vector3 to) => RpcSpawnTracer(to);

    [ClientRpc(includeOwner = false)]
    private void RpcSpawnTracer(Vector3 to)
    {
        Vector3 from = thirdPersonGun != null
            ? thirdPersonGun.position + thirdPersonGun.forward * 0.4f
            : transform.position + Vector3.up * 1.5f;
        BulletEffects.SpawnTracer(from, to);
    }

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

    private void PlayDeathAnimation()
    {
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator == null) return;

        thirdPersonAnimator.ResetTrigger(HashDie);
        thirdPersonAnimator.SetFloat(HashSpeed, 0f);
        thirdPersonAnimator.SetBool(HashIsShooting, false);
        thirdPersonAnimator.SetTrigger(HashDie);

        // Make the local player visible so they can see themselves go down.
        if (isLocalPlayer && thirdPersonModel != null)
        {
            foreach (var rend in thirdPersonModel.GetComponentsInChildren<Renderer>())
                rend.enabled = true;
        }
    }

    // ─── Server-driven respawn (called by NetworkLobby after the delay) ────────

    [ClientRpc]
    public void RpcRespawnAt(Vector3 pos, Quaternion rot)
    {
        // Reset animator on every client so the dying clip is cleared.
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator != null)
        {
            thirdPersonAnimator.ResetTrigger(HashDie);
            thirdPersonAnimator.SetFloat(HashSpeed, 0f);
            thirdPersonAnimator.SetBool(HashIsShooting, false);
            // Force the controller back to the idle state so the death clip doesn't linger.
            thirdPersonAnimator.Play("rifle aiming idle", 0, 0f);
        }

        if (!isLocalPlayer)
        {
            // Remote: nothing else to do, NetworkTransform will move us.
            return;
        }

        // Local: re-hide our 3rd-person body and move ourselves to the spawn position.
        if (thirdPersonModel != null)
        {
            foreach (var rend in thirdPersonModel.GetComponentsInChildren<Renderer>())
                rend.enabled = false;
        }

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
