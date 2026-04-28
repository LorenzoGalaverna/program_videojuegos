using UnityEngine;

[CreateAssetMenu(fileName = "NewWeapon", menuName = "FPS/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;
    public WeaponType weaponType;

    [Header("Stats")]
    public int damage = 25;
    public float fireRate = 0.1f; // seconds between shots
    public float range = 100f;
    public int magazineSize = 30;
    public int reserveAmmo = 90;
    public float reloadTime = 2f;

    [Header("Accuracy")]
    public float baseSpread = 0.02f;
    public float maxSpread = 0.08f;
    public float spreadIncreasePerShot = 0.01f;
    public float spreadRecoverySpeed = 0.05f;

    [Header("Recoil")]
    public float recoilUp = 1f;
    public float recoilSide = 0.3f;
    public float recoilRecoverySpeed = 5f;

    [Header("Movement")]
    public float moveSpeedMultiplier = 1f; // sniper slows you down

    [Header("Visual")]
    public Vector3 holdPosition = new Vector3(0.2f, -0.15f, 0.4f);
    public Vector3 adsPosition = new Vector3(0f, -0.08f, 0.3f);
    public float adsSpeed = 8f;
    public float adsFovMultiplier = 0.75f;

    [Header("Audio")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
}

public enum WeaponType
{
    Pistol,
    Rifle,
    SMG,
    Sniper,
    Shotgun,
    Knife
}
