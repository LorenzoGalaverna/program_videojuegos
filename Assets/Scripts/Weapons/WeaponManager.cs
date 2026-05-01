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

    // Recoil system
    private Vector2 currentRecoil;      // accumulated recoil applied to camera
    private Vector2 recoilVelocity;     // for smooth recovery
    private const float maxRecoilX = 8f; // max vertical recoil degrees

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
            }
        }
    }

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

    private void HandleRecoil()
    {
        float recoilX = currentWeapon.GetRecoilX();
        float recoilY = currentWeapon.GetRecoilY();

        // Add new recoil (clamped)
        if (recoilX > 0.01f || Mathf.Abs(recoilY) > 0.01f)
        {
            float addX = recoilX * Time.deltaTime * 10f;
            float addY = recoilY * Time.deltaTime * 10f;

            currentRecoil.x = Mathf.Clamp(currentRecoil.x + addX, 0f, maxRecoilX);
            currentRecoil.y = Mathf.Clamp(currentRecoil.y + addY, -maxRecoilX, maxRecoilX);
        }

        // Recover recoil back to zero when not shooting
        bool isShooting = Input.GetMouseButton(0);
        if (!isShooting)
        {
            float recoverySpeed = currentWeapon.data != null ? currentWeapon.data.recoilRecoverySpeed : 5f;
            currentRecoil = Vector2.SmoothDamp(currentRecoil, Vector2.zero, ref recoilVelocity, 0.15f, recoverySpeed);
        }

        // Apply recoil as camera offset (not cumulative rotation)
        cameraTransform.localRotation = Quaternion.Euler(-currentRecoil.x, currentRecoil.y, 0f);
    }

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
