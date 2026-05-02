using UnityEngine;
using Mirror;

// Minimal networked player for the LAN smoke test.
// Each client controls only the instance where isLocalPlayer is true; the position is
// then replicated to everyone else via NetworkTransform on the same GameObject.
public class NetworkedPlayerController : NetworkBehaviour
{
    public float moveSpeed = 6f;
    public float mouseSensitivity = 2f;

    private float yaw;
    private Camera localCam;

    public override void OnStartLocalPlayer()
    {
        // Only the local player gets a camera and locks the cursor
        GameObject camGO = new GameObject("LocalCamera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(transform, false);
        camGO.transform.localPosition = new Vector3(0, 1.4f, 0);
        localCam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // Mouse look (yaw on the body, pitch on the camera)
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        if (localCam != null)
        {
            float pitch = localCam.transform.localEulerAngles.x - Input.GetAxis("Mouse Y") * mouseSensitivity;
            // wrap pitch to [-180, 180] so the clamp works as expected
            if (pitch > 180f) pitch -= 360f;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            localCam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        // WASD movement relative to facing
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 move = transform.right * h + transform.forward * v;
        transform.position += move * moveSpeed * Time.deltaTime;
    }
}
