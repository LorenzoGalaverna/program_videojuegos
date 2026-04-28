using UnityEngine;

/// <summary>
/// Drop this on any GameObject in your scene and press Play.
/// It will create a SceneSetup if none exists and handle initialization.
/// This is the ONLY script you need to add manually.
/// </summary>
[DefaultExecutionOrder(-100)]
public class FPSBootstrap : MonoBehaviour
{
    [Header("Quick Settings")]
    public float mouseSensitivity = 2f;
    public int targetFrameRate = 120;
    public bool showDebugInfo = true;

    void Awake()
    {
        Application.targetFrameRate = targetFrameRate;

        // Check if SceneSetup already exists
        if (FindAnyObjectByType<SceneSetup>() == null)
        {
            gameObject.AddComponent<SceneSetup>();
        }
    }

    void Start()
    {
        // Apply sensitivity
        MouseLook ml = FindAnyObjectByType<MouseLook>();
        if (ml) ml.SetSensitivity(mouseSensitivity);
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(1, 1, 1, 0.5f) }
        };

        float y = 10;
        GUI.Label(new Rect(10, y, 300, 20), $"FPS: {(1f / Time.smoothDeltaTime):F0}", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "WASD: Move | Shift: Run | Ctrl: Crouch | Space: Jump", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "Mouse: Look | LMB: Shoot | RMB: ADS | R: Reload", style);
        y += 15;
        GUI.Label(new Rect(10, y, 400, 20), "1/2/3: Switch Weapons | Scroll: Next/Prev Weapon", style);
    }
}
