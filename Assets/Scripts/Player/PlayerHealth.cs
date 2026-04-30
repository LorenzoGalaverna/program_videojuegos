using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged = new UnityEvent<int, int>();
    public UnityEvent onDeath = new UnityEvent();

    private int currentHealth;
    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth, 0);
    }

    public void TakeDamage(int damage, bool isHeadshot = false)
    {
        if (isDead) return;

        if (isHeadshot)
            damage = Mathf.RoundToInt(damage * 2.5f);

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        onHealthChanged?.Invoke(currentHealth, 0);

        if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        onHealthChanged?.Invoke(currentHealth, 0);
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth, 0);
    }

    private void Die()
    {
        isDead = true;
        onDeath?.Invoke();
    }

    public int Health => currentHealth;
    public bool IsDead => isDead;
}
