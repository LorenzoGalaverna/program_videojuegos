using UnityEngine;

public class GameHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerHealth playerHealth;
    public WeaponManager weaponManager;

    [Header("Crosshair")]
    public float crosshairSize = 10f;
    public float crosshairGap = 5f;
    public float crosshairThickness = 2f;
    public Color crosshairColor = Color.green;

    [Header("HUD Colors")]
    public Color healthColor = Color.green;
    public Color armorColor = new Color(0.3f, 0.5f, 1f);
    public Color ammoColor = Color.white;
    public Color dangerColor = Color.red;

    // State
    private int displayHealth = 100;
    private int displayArmor = 0;
    private int displayMag = 30;
    private int displayReserve = 90;
    private string weaponName = "";
    private string gameMessage = "";
    private float messageTimer;
    private int playerScore;
    private int enemyScore;
    private float gameTime;

    // Styles
    private GUIStyle healthStyle;
    private GUIStyle ammoStyle;
    private GUIStyle messageStyle;
    private GUIStyle scoreStyle;
    private bool stylesInit;

    // Hit marker
    private float hitMarkerTimer;

    private bool gmListenersAttached;

    void Start()
    {
        if (playerHealth)
        {
            playerHealth.onHealthChanged.AddListener((h, a) => { displayHealth = h; displayArmor = a; });
        }
        if (weaponManager)
        {
            weaponManager.onAmmoChanged.AddListener((m, r) => { displayMag = m; displayReserve = r; });
            weaponManager.onWeaponChanged.AddListener(n => weaponName = n);
        }
        TryAttachGameManagerListeners();
    }

    private void TryAttachGameManagerListeners()
    {
        if (gmListenersAttached) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.onScoreChanged.AddListener((p, e) => { playerScore = p; enemyScore = e; });
        GameManager.Instance.onGameMessage.AddListener(ShowMessage);
        gmListenersAttached = true;
    }

    void Update()
    {
        if (messageTimer > 0) messageTimer -= Time.deltaTime;
        if (hitMarkerTimer > 0) hitMarkerTimer -= Time.deltaTime;
        TryAttachGameManagerListeners();
        if (GameManager.Instance) gameTime = GameManager.Instance.CurrentTime;
    }

    private void InitStyles()
    {
        healthStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        ammoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };

        messageStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        messageStyle.normal.textColor = Color.yellow;

        scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        scoreStyle.normal.textColor = Color.white;

        stylesInit = true;
    }

    void OnGUI()
    {
        if (!stylesInit) InitStyles();

        DrawCrosshair();
        DrawHealthArmor();
        DrawAmmo();
        DrawScore();
        DrawTimer();
        DrawMessage();
        DrawHitMarker();
        DrawReloadIndicator();
    }

    private void DrawCrosshair()
    {
        float cx = Screen.width / 2f;
        float cy = Screen.height / 2f;
        Color c = crosshairColor;

        // Dynamic spread (widen when shooting)
        float gap = crosshairGap;
        if (weaponManager && weaponManager.CurrentWeapon != null)
        {
            // Make crosshair bigger based on current weapon spread
        }

        DrawLine(cx - crosshairSize - gap, cy, cx - gap, cy, crosshairThickness, c); // left
        DrawLine(cx + gap, cy, cx + crosshairSize + gap, cy, crosshairThickness, c); // right
        DrawLine(cx, cy - crosshairSize - gap, cx, cy - gap, crosshairThickness, c); // top
        DrawLine(cx, cy + gap, cx, cy + crosshairSize + gap, crosshairThickness, c); // bottom

        // Center dot
        DrawRect(cx - 1, cy - 1, 2, 2, c);
    }

    private void DrawHealthArmor()
    {
        float y = Screen.height - 60;

        // Health
        healthStyle.normal.textColor = displayHealth > 30 ? healthColor : dangerColor;
        GUI.Label(new Rect(20, y, 200, 40), $"HP: {displayHealth}", healthStyle);

        // Armor
        if (displayArmor > 0)
        {
            healthStyle.normal.textColor = armorColor;
            GUI.Label(new Rect(20, y - 30, 200, 40), $"ARMOR: {displayArmor}", healthStyle);
        }
    }

    private void DrawAmmo()
    {
        float x = Screen.width - 220;
        float y = Screen.height - 60;

        ammoStyle.normal.textColor = displayMag > 0 ? ammoColor : dangerColor;
        GUI.Label(new Rect(x, y, 200, 40), $"{displayMag} / {displayReserve}", ammoStyle);

        // Weapon name
        ammoStyle.fontSize = 16;
        ammoStyle.normal.textColor = new Color(1, 1, 1, 0.6f);
        GUI.Label(new Rect(x, y - 25, 200, 25), weaponName, ammoStyle);
        ammoStyle.fontSize = 28;
    }

    private void DrawScore()
    {
        GUI.Label(new Rect(Screen.width / 2 - 100, 10, 200, 30), $"{playerScore}  -  {enemyScore}", scoreStyle);
    }

    private void DrawTimer()
    {
        int minutes = Mathf.FloorToInt(gameTime / 60);
        int seconds = Mathf.FloorToInt(gameTime % 60);
        scoreStyle.normal.textColor = gameTime < 30 ? dangerColor : Color.white;
        GUI.Label(new Rect(Screen.width / 2 - 50, 35, 100, 25), $"{minutes}:{seconds:00}", scoreStyle);
        scoreStyle.normal.textColor = Color.white;
    }

    private void DrawMessage()
    {
        if (messageTimer > 0)
        {
            float alpha = Mathf.Min(messageTimer, 1f);
            messageStyle.normal.textColor = new Color(1, 1, 0, alpha);
            GUI.Label(new Rect(0, Screen.height / 3, Screen.width, 50), gameMessage, messageStyle);
        }
    }

    private void DrawHitMarker()
    {
        if (hitMarkerTimer > 0)
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            float s = 8f;
            Color c = Color.white;

            DrawLine(cx - s, cy - s, cx - s / 2, cy - s / 2, 2, c);
            DrawLine(cx + s, cy - s, cx + s / 2, cy - s / 2, 2, c);
            DrawLine(cx - s, cy + s, cx - s / 2, cy + s / 2, 2, c);
            DrawLine(cx + s, cy + s, cx + s / 2, cy + s / 2, 2, c);
        }
    }

    private void DrawReloadIndicator()
    {
        if (weaponManager && weaponManager.CurrentWeapon != null && weaponManager.CurrentWeapon.IsReloading)
        {
            GUI.Label(new Rect(Screen.width / 2 - 50, Screen.height / 2 + 30, 100, 25), "RELOADING...",
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow } });
        }
    }

    public void ShowHitMarker() => hitMarkerTimer = 0.3f;

    public void ShowMessage(string msg)
    {
        gameMessage = msg;
        messageTimer = 3f;
    }

    // Helper drawing methods
    private static Texture2D lineTex;

    private void DrawLine(float x1, float y1, float x2, float y2, float thickness, Color color)
    {
        if (!lineTex) { lineTex = new Texture2D(1, 1); }
        Color prev = GUI.color;
        GUI.color = color;

        float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
        float length = Vector2.Distance(new Vector2(x1, y1), new Vector2(x2, y2));

        GUIUtility.RotateAroundPivot(angle, new Vector2(x1, y1));
        GUI.DrawTexture(new Rect(x1, y1 - thickness / 2, length, thickness), lineTex);
        GUIUtility.RotateAroundPivot(-angle, new Vector2(x1, y1));

        GUI.color = prev;
    }

    private void DrawRect(float x, float y, float w, float h, Color color)
    {
        if (!lineTex) { lineTex = new Texture2D(1, 1); }
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), lineTex);
        GUI.color = prev;
    }
}
