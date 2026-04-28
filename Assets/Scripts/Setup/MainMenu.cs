using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public SceneSetup sceneSetup;

    private bool menuVisible = true;
    private float lanMessageTimer;
    private string lanMessage = "";

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle disabledButtonStyle;
    private GUIStyle messageStyle;
    private GUIStyle footerStyle;
    private bool stylesInit;
    private Texture2D bgTex;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (lanMessageTimer > 0) lanMessageTimer -= Time.deltaTime;
    }

    private void InitStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 64,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        disabledButtonStyle = new GUIStyle(buttonStyle);
        disabledButtonStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        disabledButtonStyle.hover.textColor = new Color(0.5f, 0.5f, 0.5f);

        messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        messageStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        footerStyle.normal.textColor = new Color(1f, 1f, 1f, 0.4f);

        bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.08f, 0.95f));
        bgTex.Apply();

        stylesInit = true;
    }

    void OnGUI()
    {
        if (!menuVisible) return;
        if (!stylesInit) InitStyles();

        // Background overlay
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), bgTex);

        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;

        // Title
        GUI.Label(new Rect(0, cy - 250, Screen.width, 80), "DUSTBOWL 1V1", titleStyle);
        GUI.Label(new Rect(0, cy - 180, Screen.width, 30), "Selecciona un modo de juego", subtitleStyle);

        // Buttons
        float btnW = 380;
        float btnH = 70;
        float btnX = cx - btnW / 2f;

        // Offline vs Bot
        if (GUI.Button(new Rect(btnX, cy - 70, btnW, btnH), "OFFLINE  vs  BOT", buttonStyle))
        {
            StartOffline();
        }

        // LAN (disabled)
        GUI.color = new Color(0.4f, 0.4f, 0.4f);
        if (GUI.Button(new Rect(btnX, cy + 20, btnW, btnH), "LAN  (1 vs 1)", disabledButtonStyle))
        {
            lanMessage = "EN CONSTRUCCIÓN";
            lanMessageTimer = 2.5f;
        }
        GUI.color = Color.white;

        // Quit
        GUIStyle quitStyle = new GUIStyle(buttonStyle) { fontSize = 18 };
        if (GUI.Button(new Rect(btnX + btnW / 4f, cy + 130, btnW / 2f, 45), "Salir", quitStyle))
        {
            Application.Quit();
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }

        // LAN message
        if (lanMessageTimer > 0)
        {
            float alpha = Mathf.Min(lanMessageTimer, 1f);
            messageStyle.normal.textColor = new Color(1f, 0.4f, 0.4f, alpha);
            GUI.Label(new Rect(0, cy + 95, Screen.width, 30), lanMessage, messageStyle);
        }

        // Footer
        GUI.Label(new Rect(0, Screen.height - 30, Screen.width, 20),
            "Lorenzo Galaverna  -  Taller de Videojuegos UCC  -  2026",
            footerStyle);
    }

    private void StartOffline()
    {
        menuVisible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (sceneSetup == null)
            sceneSetup = FindAnyObjectByType<SceneSetup>();

        if (sceneSetup != null)
            sceneSetup.BuildScene();
        else
            Debug.LogError("[MainMenu] No SceneSetup found in scene.");
    }
}
