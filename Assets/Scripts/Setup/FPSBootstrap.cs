using UnityEngine;

[DefaultExecutionOrder(-100)]
public class FPSBootstrap : MonoBehaviour
{
    [Header("Quick Settings")]
    public float mouseSensitivity = 2f;
    public int targetFrameRate = 120;
    public bool showDebugInfo = true;

    private MainMenu menu;

    void Awake()
    {
        // Disable VSync so targetFrameRate actually applies
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;

        // Add SceneSetup but DO NOT auto-build (the menu will trigger it)
        SceneSetup setup = FindAnyObjectByType<SceneSetup>();
        if (setup == null)
        {
            setup = gameObject.AddComponent<SceneSetup>();
            setup.autoBuildOnAwake = false;
        }

        // Add main menu
        menu = gameObject.AddComponent<MainMenu>();
        menu.sceneSetup = setup;
    }

    void Start()
    {
        MouseLook ml = FindAnyObjectByType<MouseLook>();
        if (ml) ml.SetSensitivity(mouseSensitivity);
    }

    void Update()
    {
        // Re-apply sensitivity when player is created after menu
        if (Input.GetKeyDown(KeyCode.F2))
        {
            MouseLook ml = FindAnyObjectByType<MouseLook>();
            if (ml) ml.SetSensitivity(mouseSensitivity);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // Don't show debug info while in menu
        if (menu != null && IsMenuVisible()) return;

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

    private bool IsMenuVisible()
    {
        // Reflection-free check: menu hides itself when game starts
        return menu != null && menu.enabled && Cursor.visible;
    }
}
