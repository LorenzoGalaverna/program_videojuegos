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

    private CharacterController cachedCC;
    private Animator thirdPersonAnimator;
    private static readonly int HashSpeed = Animator.StringToHash("Speed");
    private static readonly int HashIsShooting = Animator.StringToHash("IsShooting");

    public override void OnStartLocalPlayer()
    {
        // Hide the 3rd-person body for our own view, but KEEP the GameObject + Animator
        // active so NetworkAnimator can still sync parameters to remote clients.
        // Disabling the whole hierarchy stops the Animator from updating.
        if (thirdPersonModel != null)
        {
            foreach (var rend in thirdPersonModel.GetComponentsInChildren<Renderer>())
                rend.enabled = false;
        }

        // Equip ourselves with the full player rig (camera, weapons, HUD, movement…)
        SceneSetup setup = FindAnyObjectByType<SceneSetup>();
        if (setup != null) setup.EquipPlayer(gameObject);
        else Debug.LogError("[NetworkedPlayer] SceneSetup not found in scene — local player is not equipped.");

        // Warp to a team spawn point so we don't appear inside a wall
        WarpToSpawn();
    }

    private void Start()
    {
        // Build hitboxes (body capsule + head sphere) sized to the visible 3rd-person
        // model, scaled by thirdPersonScale. Without these, raycasts hit the small
        // CharacterController capsule, missing the much larger visible Swat.
        BuildHitboxes();

        // For non-local players, disable CharacterController so it doesn't fight
        // with NetworkTransform — their position is driven entirely by replication.
        if (!isLocalPlayer)
        {
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Apply the configured 3rd-person scale so the remote model isn't tiny
            // compared to the local first-person view.
            if (thirdPersonModel != null)
                thirdPersonModel.transform.localScale = Vector3.one * thirdPersonScale;

            // Give the remote player a visible rifle in their right hand so other
            // clients see them holding a weapon (instead of empty hands).
            SceneSetup setup = FindAnyObjectByType<SceneSetup>();
            if (setup != null && thirdPersonModel != null)
                setup.AttachRifleToHumanoidHand(thirdPersonModel.transform);
        }
    }

    private void BuildHitboxes()
    {
        // Skip if we already built them (Awake-style guards aren't needed because
        // Start runs once per spawn).
        if (transform.Find("BodyHitbox") != null) return;

        float scale = Mathf.Max(thirdPersonScale, 0.5f);

        // Body capsule, parented to root so it follows position but not animation
        GameObject body = new GameObject("BodyHitbox");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0, 1f * scale, 0);
        var capsule = body.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f * scale;
        capsule.radius = 0.4f * scale;

        // Head sphere (tagged so headshot multiplier kicks in)
        GameObject head = new GameObject("Head");
        head.tag = "Head";
        head.transform.SetParent(transform, false);
        head.transform.localPosition = new Vector3(0, 1.75f * scale, 0);
        var sphere = head.AddComponent<SphereCollider>();
        sphere.radius = 0.18f * scale;
    }

    private void WarpToSpawn()
    {
        // Spawn position is set authoritatively by the server in OnServerAddPlayer,
        // so the client just trusts whatever transform it received. Nothing to do here.
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        UpdateAnimatorParams();
    }

    // Drive the 3rd-person Animator parameters from local input/movement so that
    // NetworkAnimator can replicate the values to remote clients (they then see
    // walk/run/idle on this player's body).
    private void UpdateAnimatorParams()
    {
        if (thirdPersonAnimator == null && thirdPersonModel != null)
            thirdPersonAnimator = thirdPersonModel.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator == null) return;

        if (cachedCC == null) cachedCC = GetComponent<CharacterController>();
        Vector3 vel = cachedCC != null ? cachedCC.velocity : Vector3.zero;
        vel.y = 0f;
        thirdPersonAnimator.SetFloat(HashSpeed, vel.magnitude, 0.1f, Time.deltaTime);
        // IsShooting set when LMB is held (rough but visible)
        thirdPersonAnimator.SetBool(HashIsShooting, Input.GetMouseButton(0));
    }

    // ─── Tracer sync (so other clients see this player's gunshots) ─────────────
    public void BroadcastTracer(Vector3 from, Vector3 to)
    {
        if (!isLocalPlayer) return;
        CmdSpawnTracer(from, to);
    }

    [Command]
    private void CmdSpawnTracer(Vector3 from, Vector3 to)
    {
        RpcSpawnTracer(from, to);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcSpawnTracer(Vector3 from, Vector3 to)
    {
        // The local shooter already drew its own tracer; only remote clients render here.
        BulletEffects.SpawnTracer(from, to);
    }

    // Called by Weapon on the LOCAL shooter when their raycast hits another player.
    // Routes the request to the server, which is the authority for health changes.
    public void RequestDamage(GameObject hitObject, int damage, bool isHeadshot)
    {
        if (!isLocalPlayer) return;
        if (hitObject == null) return;
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
}
