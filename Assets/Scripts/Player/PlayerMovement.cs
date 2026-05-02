using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;       // Shift held: silent walk
    public float runSpeed = 6f;        // Default speed (no modifier)
    public float crouchSpeed = 1.8f;   // Ctrl held: crouch
    public float knifeSpeedBonus = 1.25f; // multiplier when wielding the knife
    public float jumpForce = 1.4f;
    public float gravity = -20f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.3f;
    public LayerMask groundMask;

    [Header("Crouch")]
    [Tooltip("Camera holder transform — lowered visually when crouching")]
    public Transform cameraHolder;
    public float standEyeHeight = 1.7f;
    public float crouchEyeHeight = 1.0f;
    public float crouchTransitionSpeed = 10f;

    [Header("Footsteps")]
    public AudioClip[] footstepClips;
    [Tooltip("Steps per second when running")]
    public float runStepRate = 2.5f;
    [Tooltip("Steps per second when walking with Shift")]
    public float walkStepRate = 1.5f;
    [Tooltip("Volume of footstep sounds (0 = silent, 1 = full)")]
    public float footstepVolume = 1f;

    private CharacterController controller;
    private AudioSource audioSource;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private float originalHeight;
    private float crouchHeight = 1f;
    private float nextStepTime;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height;

        // Footstep audio source — non-spatial since it's the local player.
        // We keep AudioSource.volume at 1 and only scale via PlayOneShot's volumeScale,
        // otherwise both multiply and the steps end up much quieter than expected.
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;

        // Auto-load footstep clips if none assigned in Inspector.
        // Looks for Audio/Footstep_1, Footstep_2, ... in Resources.
        if (footstepClips == null || footstepClips.Length == 0)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 6; i++)
            {
                AudioClip c = Resources.Load<AudioClip>($"Audio/Footstep_{i}");
                if (c != null) clips.Add(c);
            }
            // Fallback: any single Footstep clip
            if (clips.Count == 0)
            {
                AudioClip c = Resources.Load<AudioClip>("Audio/Footstep");
                if (c != null) clips.Add(c);
            }
            footstepClips = clips.ToArray();
        }
    }

    void Update()
    {
        // Ground check
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        // Input
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Movement direction relative to player
        Vector3 move = transform.right * x + transform.forward * z;

        // Speed selection — default is run, Shift makes you walk silently, Ctrl crouches.
        float speed = runSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && !isCrouching)
            speed = walkSpeed;
        if (isCrouching)
            speed = crouchSpeed;

        // Knife gives a small movement bonus across all speed modes.
        if (IsHoldingKnife())
            speed *= knifeSpeedBonus;

        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        // Crouch — hold Ctrl to stay crouched, release to stand up.
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl);
        if (wantCrouch != isCrouching)
        {
            isCrouching = wantCrouch;
            controller.height = isCrouching ? crouchHeight : originalHeight;
            controller.center = isCrouching
                ? new Vector3(0, crouchHeight / 2f, 0)
                : new Vector3(0, originalHeight / 2f, 0);
        }

        // Smoothly lower the camera while crouched so the player visually goes down.
        if (cameraHolder != null)
        {
            float targetY = isCrouching ? crouchEyeHeight : standEyeHeight;
            Vector3 p = cameraHolder.localPosition;
            p.y = Mathf.Lerp(p.y, targetY, crouchTransitionSpeed * Time.deltaTime);
            cameraHolder.localPosition = p;
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

        UpdateFootsteps(move.sqrMagnitude > 0.01f);
    }

    private void UpdateFootsteps(bool isMoving)
    {
        // Crouching is silent (the whole point of crouching), and so is Shift-walk.
        // Only running on the ground produces footstep sounds.
        if (!isMoving || !isGrounded || isCrouching) return;
        if (footstepClips == null || footstepClips.Length == 0) return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift);
        if (shiftHeld) return; // silent walk

        float rate = runStepRate;
        if (Time.time < nextStepTime) return;

        AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.PlayOneShot(clip, footstepVolume);
        nextStepTime = Time.time + 1f / rate;
    }

    public bool IsGrounded => isGrounded;
    public bool IsCrouching => isCrouching;
    public bool IsWalking => Input.GetKey(KeyCode.LeftShift) && !isCrouching;
    public bool IsRunning => !Input.GetKey(KeyCode.LeftShift) && !isCrouching;

    private WeaponManager cachedWM;
    private bool IsHoldingKnife()
    {
        if (cachedWM == null) cachedWM = GetComponent<WeaponManager>();
        return cachedWM != null
            && cachedWM.CurrentWeapon != null
            && cachedWM.CurrentWeapon.data != null
            && cachedWM.CurrentWeapon.data.weaponType == WeaponType.Knife;
    }
}
