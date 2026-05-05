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

    public override void OnStartLocalPlayer()
    {
        // Hide the 3rd-person body — we use first-person hands instead.
        if (thirdPersonModel != null) thirdPersonModel.SetActive(false);

        // Equip ourselves with the full player rig (camera, weapons, HUD, movement…)
        SceneSetup setup = FindAnyObjectByType<SceneSetup>();
        if (setup != null) setup.EquipPlayer(gameObject);
        else Debug.LogError("[NetworkedPlayer] SceneSetup not found in scene — local player is not equipped.");

        // Warp to a team spawn point so we don't appear inside a wall
        WarpToSpawn();
    }

    private void Start()
    {
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

    private void WarpToSpawn()
    {
        if (GameManager.Instance == null) return;
        Transform sp = GameManager.Instance.GetSpawnPoint(0);
        if (sp == null) return;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = sp.position;
        transform.rotation = sp.rotation;
        if (cc != null) cc.enabled = true;
    }
}
