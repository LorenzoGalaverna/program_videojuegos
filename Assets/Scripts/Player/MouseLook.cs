using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float sensitivity = 2f;
    public float smoothing = 1.5f;
    public Transform playerBody;

    private float xRotation = 0f;
    private Vector2 currentLookDelta;
    private Vector2 smoothLookVelocity;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Raw input
        Vector2 targetDelta = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        // Smooth
        currentLookDelta = Vector2.SmoothDamp(currentLookDelta, targetDelta, ref smoothLookVelocity, smoothing * Time.deltaTime);

        float mouseX = currentLookDelta.x * sensitivity;
        float mouseY = currentLookDelta.y * sensitivity;

        // Vertical rotation (camera)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Horizontal rotation (body)
        playerBody.Rotate(Vector3.up * mouseX);
    }

    public void SetSensitivity(float newSens)
    {
        sensitivity = newSens;
    }
}
