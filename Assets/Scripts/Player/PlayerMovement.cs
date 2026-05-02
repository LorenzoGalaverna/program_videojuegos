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

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private float originalHeight;
    private float crouchHeight = 1f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        originalHeight = controller.height;
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

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
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
