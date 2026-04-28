using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float runSpeed = 7f;
    public float crouchSpeed = 2.5f;
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

        // Speed selection
        float speed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) && !isCrouching)
            speed = runSpeed;
        if (isCrouching)
            speed = crouchSpeed;

        controller.Move(move * speed * Time.deltaTime);

        // Jump
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching)
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

        // Crouch
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = !isCrouching;
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
    public bool IsRunning => Input.GetKey(KeyCode.LeftShift) && !isCrouching;
}
