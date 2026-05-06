using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float sensitivity = 2f;
    public float smoothing = 1.5f;
    public Transform playerBody;

    // Assigned by SceneSetup so we can read recoil and add it on top of mouse pitch.
    // WeaponManager no longer writes localRotation — MouseLook is the sole owner.
    [HideInInspector] public WeaponManager weaponManager;

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

        // Vertical rotation — add recoil on top so both systems use the same rotation.
        // WeaponManager only accumulates/decays the recoil value; it does NOT write localRotation.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        float recoilX = weaponManager != null ? weaponManager.RecoilX : 0f;
        float recoilY = weaponManager != null ? weaponManager.RecoilY : 0f;
        transform.localRotation = Quaternion.Euler(xRotation - recoilX, 0f, 0f);

        // Horizontal rotation (body) — also incorporate horizontal recoil here
        playerBody.Rotate(Vector3.up * (mouseX + recoilY * Time.deltaTime));
    }

    public void SetSensitivity(float newSens)
    {
        sensitivity = newSens;
    }

    public void ResetLook()
    {
        xRotation = 0f;
        currentLookDelta = Vector2.zero;
        smoothLookVelocity = Vector2.zero;
        transform.localRotation = Quaternion.identity;
    }
}
