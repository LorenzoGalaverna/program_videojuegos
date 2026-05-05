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
    public UnityEvent<int, int> onAmmoChanged = new UnityEvent<int, int>();

    private int currentMagazine;
    private int currentReserve;
    private float nextFireTime;
    private float currentSpread;
    private bool isReloading;
    private float reloadEndTime;

    // Recoil
    private float currentRecoilX;
    private float currentRecoilY;

    // Used by HUD to flash a hitmarker for knife hits
    public float lastHitTime = -10f;

    // Extra impact sound for knife flesh hits (set in SceneSetup)
    public AudioClip knifeFleshHitSound;

    private float shootSoundStart;
    private float reloadSoundStart;
    private float emptySoundStart;
    private float fleshSoundStart;

    void Start()
    {
        if (data == null) return;
        currentMagazine = data.magazineSize;
        currentReserve = data.reserveAmmo;
        currentSpread = data.baseSpread;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);

        // Force-load audio data at startup so the first shot doesn't pay decompression cost.
        // Also detect any leading silence in each clip so playback can skip past it —
        // some downloaded SFX have ~100–500ms of pre-roll baked in.
        if (data.shootSound) { data.shootSound.LoadAudioData(); shootSoundStart = FindFirstAudioSample(data.shootSound); }
        if (data.reloadSound) { data.reloadSound.LoadAudioData(); reloadSoundStart = FindFirstAudioSample(data.reloadSound); }
        if (data.emptySound) { data.emptySound.LoadAudioData(); emptySoundStart = FindFirstAudioSample(data.emptySound); }
        if (knifeFleshHitSound) { knifeFleshHitSound.LoadAudioData(); fleshSoundStart = FindFirstAudioSample(knifeFleshHitSound); }
    }

    private static float FindFirstAudioSample(AudioClip clip)
    {
        const float silenceThreshold = 0.01f;
        // Sample at most the first second of the clip — anything more than that is
        // probably an unusual mix and we don't want to skip real audio.
        int sampleCount = Mathf.Min(clip.samples * clip.channels, clip.frequency * clip.channels);
        float[] samples = new float[sampleCount];
        if (!clip.GetData(samples, 0)) return 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) > silenceThreshold)
                return (float)i / (clip.frequency * clip.channels);
        }
        return 0f;
    }

    private void PlayInstant(AudioClip clip, float startTime)
    {
        if (audioSource == null || clip == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.time = startTime;
        audioSource.Play();
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
            PlayInstant(data.emptySound, emptySoundStart);
            TryReload();
            return false;
        }

        // Fire
        currentMagazine--;
        nextFireTime = Time.time + data.fireRate;
        onAmmoChanged?.Invoke(currentMagazine, currentReserve);

        // Spread calculation. Crouching halves spread (better accuracy when stable),
        // walking with Shift keeps it normal, running adds nothing extra here.
        float spreadModifier = 1f;
        PlayerMovement playerMov = GetComponentInParent<PlayerMovement>();
        if (playerMov != null && playerMov.IsCrouching)
            spreadModifier = 0.5f;

        Vector3 spreadDir = cameraTransform.forward;
        spreadDir += cameraTransform.right * Random.Range(-currentSpread, currentSpread) * spreadModifier;
        spreadDir += cameraTransform.up * Random.Range(-currentSpread, currentSpread) * spreadModifier;

        currentSpread = Mathf.Min(currentSpread + data.spreadIncreasePerShot, data.maxSpread);

        // Recoil
        currentRecoilX += data.recoilUp;
        currentRecoilY += Random.Range(-data.recoilSide, data.recoilSide);

        // Effects
        if (muzzleFlash) muzzleFlash.Play();
        PlayInstant(data.shootSound, shootSoundStart);

        // Raycast
        bool hit = Physics.Raycast(cameraTransform.position, spreadDir.normalized, out hitInfo, data.range);

        // Visual tracer + bullet hole at impact (or a far-away point if it missed).
        // Skip tracer for melee — a yellow line for a knife slash looks wrong.
        if (data.weaponType != WeaponType.Knife)
        {
            Vector3 tracerStart = muzzlePoint ? muzzlePoint.position : cameraTransform.position;
            Vector3 tracerEnd = hit ? hitInfo.point : cameraTransform.position + spreadDir.normalized * data.range;
            BulletEffects.SpawnTracer(tracerStart, tracerEnd);

            // In LAN, also broadcast the tracer so remote clients see this player shooting.
            NetworkedPlayer net = GetComponentInParent<NetworkedPlayer>();
            if (net != null && net.isLocalPlayer)
                net.BroadcastTracer(tracerStart, tracerEnd);
        }

        if (hit)
        {
            // Check if headshot
            bool isHeadshot = hitInfo.collider.CompareTag("Head");
            PlayerHealth targetHealth = hitInfo.collider.GetComponentInParent<PlayerHealth>();

            if (data.weaponType == WeaponType.Knife)
            {
                // Knife hit: spawn slash effect at the hit point and notify HUD for hitmarker
                BulletEffects.SpawnKnifeHit(hitInfo, targetHealth != null);
                lastHitTime = Time.time;

                // Extra impact sound only for flesh hits — wall hits just use the swoosh.
                // PlayOneShot here so it layers on top of the swoosh that just started.
                if (targetHealth != null && audioSource && knifeFleshHitSound != null)
                    audioSource.PlayOneShot(knifeFleshHitSound);
            }
            else
            {
                BulletEffects.SpawnBulletHole(hitInfo);
            }

            if (targetHealth != null)
            {
                // In networked play we route damage through the local NetworkedPlayer's
                // Command — the server is the authority. Offline we apply directly.
                NetworkedPlayer myNet = GetComponentInParent<NetworkedPlayer>();
                if (myNet != null && myNet.isLocalPlayer)
                    myNet.RequestDamage(hitInfo.collider.gameObject, data.damage, isHeadshot);
                else
                    targetHealth.TakeDamage(data.damage, isHeadshot);
            }
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

        PlayInstant(data.reloadSound, reloadSoundStart);
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
