using UnityEngine;
using UnityEngine.Events;

public class WeaponManager : MonoBehaviour
{
    [Header("References")]
    public Transform weaponHolder;
    public Transform cameraTransform;

    [Header("Weapons")]
    public Weapon[] weapons; // assign in inspector or via setup
    public int startWeaponIndex = 0;

    [Header("ADS")]
    public Camera playerCamera;
    private float defaultFov;
    private bool isAiming;

    [Header("Events")]
    public UnityEvent<int, int> onAmmoChanged = new UnityEvent<int, int>();
    public UnityEvent<string> onWeaponChanged = new UnityEvent<string>();

    private int currentWeaponIndex = -1;
    private Weapon currentWeapon;

    // Recoil system — instant kick on shot, fast exponential recovery to center.
    private Vector2 currentRecoil;
    private const float maxRecoilX = 12f;
    [Tooltip("How fast recoil snaps back to center. Higher = snappier.")]
    public float recoilRecoverRate = 22f;

    // Impact effects
    public GameObject bulletHolePrefab;
    public GameObject bloodEffectPrefab;

    void Start()
    {
        if (playerCamera)
            defaultFov = playerCamera.fieldOfView;

        if (weapons.Length > 0)
            SwitchWeapon(startWeaponIndex);
    }

    void Update()
    {
        if (currentWeapon == null) return;

        HandleShooting();
        HandleWeaponSwitch();
        HandleReload();
        HandleADS();
        HandleRecoil();
        HandleInspect();
    }

    // ─── Inspect animation (F) ──────────────────────────
    private float inspectStartTime = -10f;
    private const float inspectDuration = 1.6f;

    private void HandleInspect()
    {
        if (Input.GetKeyDown(KeyCode.F) && Time.time >= inspectStartTime + inspectDuration)
            inspectStartTime = Time.time;

        if (currentWeapon == null) return;

        // Knife swing has priority over the idle inspect rotation
        float swingT = (Time.time - swingStartTime) / swingDuration;
        if (swingT >= 0f && swingT <= 1f)
        {
            // Quick slash: rotate forward+sideways and snap back
            float slashCurve = Mathf.Sin(swingT * Mathf.PI); // 0 → 1 → 0
            float slashYaw = slashCurve * 50f * swingDirection;   // sideways slash
            float slashPitch = slashCurve * 35f;                   // forward thrust
            float slashRoll = slashCurve * 15f * swingDirection;   // wrist twist
            currentWeapon.transform.localRotation = Quaternion.Euler(slashPitch, slashYaw, slashRoll);
            if (swingT >= 1f) currentWeapon.transform.localRotation = Quaternion.identity;
            return;
        }

        float t = (Time.time - inspectStartTime) / inspectDuration;
        if (t < 0f || t > 1f) return;

        // Wrist-style inspect: turn 80° to one side, back to center, then 80° to the
        // other side, with subtle pitch/roll variations for organic feel.
        // Phase 0.0–0.25: 0 → +80 yaw (right)
        // Phase 0.25–0.5:  +80 → 0
        // Phase 0.5–0.75:  0 → -80 yaw (left)
        // Phase 0.75–1.0:  -80 → 0
        float yaw, pitch, roll;
        if (t < 0.25f)
        {
            float k = SmoothStep01(t / 0.25f);
            yaw = k * 80f;
        }
        else if (t < 0.5f)
        {
            float k = SmoothStep01((t - 0.25f) / 0.25f);
            yaw = (1f - k) * 80f;
        }
        else if (t < 0.75f)
        {
            float k = SmoothStep01((t - 0.5f) / 0.25f);
            yaw = -k * 80f;
        }
        else
        {
            float k = SmoothStep01((t - 0.75f) / 0.25f);
            yaw = -(1f - k) * 80f;
        }
        // Subtle pitch wobble (looking down the blade) and roll for natural wrist feel
        pitch = Mathf.Sin(t * Mathf.PI * 2f) * 12f;
        roll = Mathf.Sin(t * Mathf.PI) * 8f;

        currentWeapon.transform.localRotation = Quaternion.Euler(pitch, yaw, roll);
        if (t >= 1f) currentWeapon.transform.localRotation = Quaternion.identity;
    }

    private static float SmoothStep01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private void HandleShooting()
    {
        bool isSemiAuto = currentWeapon.data.weaponType == WeaponType.Pistol
                       || currentWeapon.data.weaponType == WeaponType.Sniper
                       || currentWeapon.data.weaponType == WeaponType.Shotgun
                       || currentWeapon.data.weaponType == WeaponType.Knife;

        bool shouldShoot = isSemiAuto ? Input.GetMouseButtonDown(0) : Input.GetMouseButton(0);

        if (shouldShoot)
        {
            if (currentWeapon.TryShoot(cameraTransform, out RaycastHit hit))
            {
                onAmmoChanged?.Invoke(currentWeapon.CurrentMagazine, currentWeapon.CurrentReserve);
                SpawnImpactEffect(hit);

                // Apply an INSTANT recoil kick on each shot. Crouching halves the kick.
                ApplyRecoilImpulse();

                // Trigger swing animation if it was a melee weapon
                if (currentWeapon.data.weaponType == WeaponType.Knife)
                {
                    swingStartTime = Time.time;
                    swingDirection = -swingDirection;
                }
            }
        }
    }

    private void ApplyRecoilImpulse()
    {
        if (currentWeapon == null || currentWeapon.data == null) return;
        var d = currentWeapon.data;

        float kickUp = d.recoilUp;
        float kickSide = Random.Range(-d.recoilSide, d.recoilSide);

        // Crouching reduces felt recoil (more stable)
        PlayerMovement pm = GetComponent<PlayerMovement>();
        if (pm != null && pm.IsCrouching) { kickUp *= 0.5f; kickSide *= 0.5f; }

        currentRecoil.x = Mathf.Clamp(currentRecoil.x + kickUp, 0f, maxRecoilX);
        currentRecoil.y = Mathf.Clamp(currentRecoil.y + kickSide, -maxRecoilX, maxRecoilX);
    }

    // ─── Knife swing animation ──────────────────────────
    private float swingStartTime = -10f;
    private const float swingDuration = 0.35f;
    private int swingDirection = 1;

    private void HandleWeaponSwitch()
    {
        // Number keys
        for (int i = 0; i < weapons.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SwitchWeapon(i);
                return;
            }
        }

        // Scroll wheel
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            int newIndex = currentWeaponIndex + (scroll > 0 ? -1 : 1);
            if (newIndex < 0) newIndex = weapons.Length - 1;
            if (newIndex >= weapons.Length) newIndex = 0;
            SwitchWeapon(newIndex);
        }
    }

    private void HandleReload()
    {
        if (Input.GetKeyDown(KeyCode.R))
            currentWeapon.TryReload();
    }

    private bool weaponVisualsHidden;

    private void HandleADS()
    {
        // Only the sniper can aim down sights — pistol and rifle have no scope
        bool canAim = currentWeapon.data != null && currentWeapon.data.weaponType == WeaponType.Sniper;
        isAiming = canAim && Input.GetMouseButton(1);

        if (currentWeapon.data == null || playerCamera == null) return;

        float targetFov = isAiming
            ? defaultFov * currentWeapon.data.adsFovMultiplier
            : defaultFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, currentWeapon.data.adsSpeed * Time.deltaTime);

        // Hide the rifle model only while scoped, restore it when un-scoping or
        // switching weapons. Track state so we don't iterate renderers every frame.
        if (isAiming && !weaponVisualsHidden)
        {
            SetWeaponVisuals(false);
            weaponVisualsHidden = true;
        }
        else if (!isAiming && weaponVisualsHidden)
        {
            SetWeaponVisuals(true);
            weaponVisualsHidden = false;
        }
    }

    private void SetWeaponVisuals(bool visible)
    {
        if (currentWeapon == null) return;
        foreach (var rend in currentWeapon.GetComponentsInChildren<Renderer>())
            rend.enabled = visible;
    }

    void OnGUI()
    {
        // Hit marker: a brief X in the center when the player just hit something.
        if (currentWeapon != null && Time.time - currentWeapon.lastHitTime < 0.18f)
        {
            if (hitMarkerTex == null)
            {
                hitMarkerTex = new Texture2D(1, 1);
                hitMarkerTex.SetPixel(0, 0, Color.white);
                hitMarkerTex.Apply();
            }
            float hmCx = Screen.width * 0.5f;
            float hmCy = Screen.height * 0.5f;
            float hmSize = 14f;
            float hmThick = 2f;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(hmCx - hmSize, hmCy - hmThick * 0.5f, hmSize, hmThick), hitMarkerTex);
            GUI.DrawTexture(new Rect(hmCx, hmCy - hmThick * 0.5f, hmSize, hmThick), hitMarkerTex);
            GUI.DrawTexture(new Rect(hmCx - hmThick * 0.5f, hmCy - hmSize, hmThick, hmSize), hitMarkerTex);
            GUI.DrawTexture(new Rect(hmCx - hmThick * 0.5f, hmCy, hmThick, hmSize), hitMarkerTex);
        }

        if (!isAiming) return;
        if (scopeTex == null)
        {
            scopeTex = new Texture2D(1, 1);
            scopeTex.SetPixel(0, 0, Color.black);
            scopeTex.Apply();
        }

        float w = Screen.width;
        float h = Screen.height;
        float radius = h * 0.45f;          // visible area radius (circle)
        float cx = w * 0.5f;
        float cy = h * 0.5f;

        // Black borders: left, right, top, bottom strips outside the circle's bounding box
        GUI.DrawTexture(new Rect(0, 0, cx - radius, h), scopeTex);
        GUI.DrawTexture(new Rect(cx + radius, 0, w - (cx + radius), h), scopeTex);
        GUI.DrawTexture(new Rect(cx - radius, 0, radius * 2, cy - radius), scopeTex);
        GUI.DrawTexture(new Rect(cx - radius, cy + radius, radius * 2, h - (cy + radius)), scopeTex);

        // Reticle: thin crosshair centered on screen
        float reticleSize = radius * 0.9f;
        float lineThickness = 1.5f;
        // Horizontal line
        GUI.DrawTexture(new Rect(cx - reticleSize, cy - lineThickness * 0.5f, reticleSize * 2, lineThickness), scopeTex);
        // Vertical line
        GUI.DrawTexture(new Rect(cx - lineThickness * 0.5f, cy - reticleSize, lineThickness, reticleSize * 2), scopeTex);
        // Center gap (small circle of clear in the middle for sight pic)
        // Drawn as 4 small black ticks instead, leaving a clear center
        float gap = 8f;
        // (the lines above already have a continuous look — we add tick marks for sniper feel)
        for (int i = 1; i <= 4; i++)
        {
            float off = i * 18f;
            // small tick marks above and below the horizontal line
            GUI.DrawTexture(new Rect(cx - 0.5f, cy + gap + off, 1f, 4f), scopeTex);
        }
    }
    private static Texture2D scopeTex;
    private static Texture2D hitMarkerTex;

    private void HandleRecoil()
    {
        // Exponential decay toward zero. MouseLook reads RecoilX/Y and adds them
        // to its own pitch so both systems share the same localRotation write.
        float k = 1f - Mathf.Exp(-recoilRecoverRate * Time.deltaTime);
        currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, k);
        // Do NOT write cameraTransform.localRotation here — MouseLook owns that.
    }

    public float RecoilX => currentRecoil.x;
    public float RecoilY => currentRecoil.y;

    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length || index == currentWeaponIndex)
            return;

        // Restore visuals on the outgoing weapon (in case it was hidden by ADS)
        if (currentWeapon != null)
        {
            SetWeaponVisuals(true);
            weaponVisualsHidden = false;
            currentWeapon.gameObject.SetActive(false);
        }

        // Clear recoil so the new weapon doesn't inherit leftover kick
        currentRecoil = Vector2.zero;

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];
        currentWeapon.gameObject.SetActive(true);

        onWeaponChanged?.Invoke(currentWeapon.data.weaponName);
        onAmmoChanged?.Invoke(currentWeapon.CurrentMagazine, currentWeapon.CurrentReserve);

        // Reset weapon holder position
        weaponHolder.localPosition = currentWeapon.data.holdPosition;
    }

    private void SpawnImpactEffect(RaycastHit hit)
    {
        if (hit.collider == null) return;

        PlayerHealth ph = hit.collider.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            if (bloodEffectPrefab)
                Instantiate(bloodEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        }
        else
        {
            if (bulletHolePrefab)
            {
                GameObject hole = Instantiate(bulletHolePrefab, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                Destroy(hole, 10f);
            }
        }
    }

    public Weapon CurrentWeapon => currentWeapon;
    public bool IsAiming => isAiming;
}
