using UnityEngine;

/// <summary>
/// In-game pause menu. Added to the local player by SceneSetup.EquipPlayerInner.
/// ESC toggles a Resume / Main-menu / Quit overlay, frees the cursor and suppresses
/// local input. In offline mode it also freezes time; in LAN mode time keeps running
/// so the networked simulation doesn't desync.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    // Other systems can poll this if they need to ignore input while paused.
    public static bool IsPaused { get; private set; }

    private bool paused;
    private bool networked;

    // Local-player controls disabled while paused. We remember their prior enabled
    // state so resuming never re-enables controls that were already off (e.g. the
    // player died right before opening the menu).
    private MouseLook mouseLook;
    private PlayerMovement movement;
    private WeaponManager weaponManager;
    private bool restoreMouseLook, restoreMovement, restoreWeapon;

    private GUIStyle titleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle hintStyle;
    private bool stylesInit;
    private Texture2D bgTex;

    void Awake()
    {
        mouseLook = GetComponentInChildren<MouseLook>();
        movement = GetComponent<PlayerMovement>();
        weaponManager = GetComponent<WeaponManager>();
    }

    void Update()
    {
        // The end-game screen owns ESC (GameHUD returns to the main menu there),
        // so make sure we're never paused over it.
        bool gameOver = GameManager.Instance != null && !GameManager.Instance.GameActive;
        if (gameOver)
        {
            if (paused) Resume();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (paused) Resume();
            else Pause();
        }
    }

    private void Pause()
    {
        paused = true;
        IsPaused = true;
        networked = NetworkLobby.IsLanActive;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (mouseLook) { restoreMouseLook = mouseLook.enabled; mouseLook.enabled = false; }
        if (movement) { restoreMovement = movement.enabled; movement.enabled = false; }
        if (weaponManager) { restoreWeapon = weaponManager.enabled; weaponManager.enabled = false; }

        if (!networked) Time.timeScale = 0f;
    }

    private void Resume()
    {
        paused = false;
        IsPaused = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (mouseLook && restoreMouseLook) mouseLook.enabled = true;
        if (movement && restoreMovement) movement.enabled = true;
        if (weaponManager && restoreWeapon) weaponManager.enabled = true;

        if (!networked) Time.timeScale = 1f;
    }

    private void ReturnToMainMenu()
    {
        // Always lift any freeze/pause state first.
        Time.timeScale = 1f;
        IsPaused = false;
        paused = false;

        // Grab the menu before tearing anything down — stopping the host destroys
        // this player object (and therefore this component), so resolve it up front.
        MainMenu menu = FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);

        // Leave the LAN session cleanly if we're in one (no-op offline).
        if (NetworkLobby.IsLanActive && Mirror.NetworkManager.singleton is NetworkLobby lobby)
            lobby.StopLan();

        if (menu != null) menu.ShowMenu();
    }

    private void QuitGame()
    {
        // Restore time before tearing down so nothing is left frozen.
        Time.timeScale = 1f;
        IsPaused = false;

        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    void OnDisable()
    {
        // Safety net: never leave the game frozen if this object is disabled/destroyed
        // (e.g. on respawn or scene teardown) while the menu is open.
        if (paused)
        {
            if (!networked) Time.timeScale = 1f;
            IsPaused = false;
            paused = false;
        }
    }

    private void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 56,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);

        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.05f, 0.85f));
        bgTex.Apply();

        stylesInit = true;
    }

    void OnGUI()
    {
        if (!paused) return;
        if (!stylesInit) InitStyles();

        // Dim the game behind the menu
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex);

        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        GUI.Label(new Rect(0, cy - 200, Screen.width, 70), "PAUSA", titleStyle);

        float btnW = 360;
        float btnH = 64;
        float btnX = cx - btnW / 2f;

        if (GUI.Button(new Rect(btnX, cy - 90, btnW, btnH), "REANUDAR", buttonStyle))
            Resume();

        if (GUI.Button(new Rect(btnX, cy - 10, btnW, btnH), "MENÚ PRINCIPAL", buttonStyle))
            ReturnToMainMenu();

        if (GUI.Button(new Rect(btnX, cy + 70, btnW, btnH), "SALIR DEL JUEGO", buttonStyle))
            QuitGame();

        GUI.Label(new Rect(0, cy + 155, Screen.width, 24),
            "Presioná ESC para reanudar", hintStyle);
    }
}
