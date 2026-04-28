using UnityEngine;
using UnityEngine.Events;

public class Weapon : MonoBehaviour
{
    public WeaponData data;

    [Header("References")]
    public Transform muzzlePoint;
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;

    [Header("Events")]
    public UnityEvent<int, int> onAmmoChanged; // current, reserve

    private int currentMagazine;
    private int currentReserve;
    private float nextFireTime;
    private float currentSpread;
    private bool isReloading;
    private float reloadEndTime;

    // Recoil
    private float currentRecoilX;
    private float currentRecoilY;

    void Start()
    {
        if (data == null) return;
        currentMagazine = data.magazineSize;
        currentReserve = data.reserveAmmo;
        currentSpread = data.baseSpread;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);
    }

    void Update()
    {
        if (data == null) return;

        // Spread recovery
        currentSpread = Mathf.MoveTowards(currentSpread, data.baseSpread, data.spreadRecoverySpeed * Time.deltaTime);

        // Recoil recovery
        currentRecoilX = Mathf.MoveTowards(currentRecoilX, 0f, data.recoilRecoverySpeed * Time.deltaTime);
        currentRecoilY = Mathf.MoveTowards(currentRecoilY, 0f, data.recoilRecoverySpeed * Time.deltaTime);

        // Reload timer
        if (isReloading && Time.time >= reloadEndTime)
            FinishReload();
    }

    public bool TryShoot(Transform cameraTransform, out RaycastHit hitInfo)
    {
        hitInfo = default;

        if (data == null || isReloading || Time.time < nextFireTime)
            return false;

        if (currentMagazine <= 0)
        {
            if (audioSource && data.emptySound)
                audioSource.PlayOneShot(data.emptySound);
            TryReload();
            return false;
        }

        // Fire
        currentMagazine--;
        nextFireTime = Time.time + data.fireRate;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);

        // Spread calculation
        Vector3 spreadDir = cameraTransform.forward;
        spreadDir += cameraTransform.right * Random.Range(-currentSpread, currentSpread);
        spreadDir += cameraTransform.up * Random.Range(-currentSpread, currentSpread);

        currentSpread = Mathf.Min(currentSpread + data.spreadIncreasePerShot, data.maxSpread);

        // Recoil
        currentRecoilX += data.recoilUp;
        currentRecoilY += Random.Range(-data.recoilSide, data.recoilSide);

        // Effects
        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && data.shootSound)
            audioSource.PlayOneShot(data.shootSound);

        // Raycast
        bool hit = Physics.Raycast(cameraTransform.position, spreadDir.normalized, out hitInfo, data.range);

        if (hit)
        {
            // Check if headshot
            bool isHeadshot = hitInfo.collider.CompareTag("Head");
            PlayerHealth targetHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();

            if (targetHealth != null)
                targetHealth.TakeDamage(data.damage, isHeadshot);
        }

        // Auto reload
        if (currentMagazine <= 0)
            TryReload();

        return true;
    }

    public void TryReload()
    {
        if (isReloading || currentMagazine >= data.magazineSize || currentReserve <= 0)
            return;

        isReloading = true;
        reloadEndTime = Time.time + data.reloadTime;

        if (audioSource && data.reloadSound)
            audioSource.PlayOneShot(data.reloadSound);
    }

    private void FinishReload()
    {
        isReloading = false;
        int needed = data.magazineSize - currentMagazine;
        int toLoad = Mathf.Min(needed, currentReserve);
        currentMagazine += toLoad;
        currentReserve -= toLoad;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);
    }

    public void RefillAmmo()
    {
        currentMagazine = data.magazineSize;
        currentReserve = data.reserveAmmo;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);
    }

    public float GetRecoilX() => currentRecoilX;
    public float GetRecoilY() => currentRecoilY;
    public int CurrentMagazine => currentMagazine;
    public int CurrentReserve => currentReserve;
    public bool IsReloading => isReloading;
}
