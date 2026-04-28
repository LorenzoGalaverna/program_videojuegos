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
    public UnityEvent<int, int> onAmmoChanged;
    public UnityEvent<string> onWeaponChanged;

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
                       || currentWeapon.data.weaponType == WeaponType.Shotgun;

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

    private void HandleADS()
    {
        isAiming = Input.GetMouseButton(1);

        if (currentWeapon.data == null || playerCamera == null) return;

        float targetFov = isAiming
            ? defaultFov * currentWeapon.data.adsFovMultiplier
            : defaultFov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, currentWeapon.data.adsSpeed * Time.deltaTime);

        // Move weapon to ADS position
        Vector3 targetPos = isAiming ? currentWeapon.data.adsPosition : currentWeapon.data.holdPosition;
        weaponHolder.localPosition = Vector3.Lerp(weaponHolder.localPosition, targetPos, currentWeapon.data.adsSpeed * Time.deltaTime);
    }

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

        // Disable current
        if (currentWeapon != null)
            currentWeapon.gameObject.SetActive(false);

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
