using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int maxArmor = 100;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged; // health, armor
    public UnityEvent onDeath;

    private int currentHealth;
    private int currentArmor;
    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;
        currentArmor = 0;
        onHealthChanged?.Invoke(currentHealth, currentArmor);
    }

    public void TakeDamage(int damage, bool isHeadshot = false)
    {
        if (isDead) return;

        if (isHeadshot)
            damage = Mathf.RoundToInt(damage * 2.5f);

        // Armor absorbs 60% of damage
        if (currentArmor > 0)
        {
            int armorDamage = Mathf.RoundToInt(damage * 0.6f);
            int healthDamage = damage - armorDamage;

            if (armorDamage > currentArmor)
            {
                int overflow = armorDamage - currentArmor;
                currentArmor = 0;
                healthDamage += overflow;
            }
            else
            {
                currentArmor -= armorDamage;
            }

            currentHealth -= healthDamage;
        }
        else
        {
            currentHealth -= damage;
        }

        currentHealth = Mathf.Max(currentHealth, 0);
        onHealthChanged?.Invoke(currentHealth, currentArmor);

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onHealthChanged?.Invoke(currentHealth, currentArmor);
    }

    public void AddArmor(int amount)
    {
        currentArmor = Mathf.Min(currentArmor + amount, maxArmor);
        onHealthChanged?.Invoke(currentHealth, currentArmor);
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        currentArmor = 0;
        onHealthChanged?.Invoke(currentHealth, currentArmor);
    }

    private void Die()
    {
        isDead = true;
        onDeath?.Invoke();
    }

    public int Health => currentHealth;
    public int Armor => currentArmor;
    public bool IsDead => isDead;
}
