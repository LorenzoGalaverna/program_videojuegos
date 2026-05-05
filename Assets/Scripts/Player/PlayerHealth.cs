using UnityEngine;
using UnityEngine.Events;
using Mirror;

// Health works in BOTH offline and networked contexts:
//  - Offline (no NetworkIdentity / Mirror server): TakeDamage applies immediately.
//  - Networked: server is the authority. Clients route damage requests through
//    NetworkedPlayer.CmdRequestDamage; the server then applies it and the SyncVar
//    replicates to every client.
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;

    [Header("Events")]
    public UnityEvent<int, int> onHealthChanged = new UnityEvent<int, int>();
    public UnityEvent onDeath = new UnityEvent();

    [SyncVar(hook = nameof(OnHealthSyncChanged))]
    private int currentHealth;
    [SyncVar(hook = nameof(OnDeadSyncChanged))]
    private bool isDead;

    void Start()
    {
        // Server initializes; clients receive the SyncVar on spawn.
        if (!IsNetworked() || isServer) currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth, 0);
    }

    public void TakeDamage(int damage, bool isHeadshot = false)
    {
        if (isDead) return;
        if (isHeadshot) damage = Mathf.RoundToInt(damage * 2.5f);

        if (IsNetworked())
        {
            // Server applies; clients see via SyncVar replication.
            if (isServer) ApplyDamageInternal(damage);
            // (clients calling TakeDamage directly is ignored — go through NetworkedPlayer.CmdRequestDamage instead)
            return;
        }

        // Offline path
        ApplyDamageInternal(damage);
    }

    private void ApplyDamageInternal(int damage)
    {
        currentHealth = Mathf.Max(currentHealth - damage, 0);
        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            // In offline mode the SyncVar hook won't fire, so call the event directly.
            if (!IsNetworked()) onDeath?.Invoke();
        }
        if (!IsNetworked()) onHealthChanged?.Invoke(currentHealth, 0);
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        if (!IsNetworked()) onHealthChanged?.Invoke(currentHealth, 0);
    }

    public void ResetHealth()
    {
        if (IsNetworked() && !isServer) return;
        isDead = false;
        currentHealth = maxHealth;
        if (!IsNetworked()) onHealthChanged?.Invoke(currentHealth, 0);
    }

    private void OnHealthSyncChanged(int oldVal, int newVal)
    {
        onHealthChanged?.Invoke(newVal, 0);
    }

    private void OnDeadSyncChanged(bool oldVal, bool newVal)
    {
        if (newVal && !oldVal) onDeath?.Invoke();
    }

    private bool IsNetworked()
    {
        // We treat the object as networked only when Mirror is running AND we have an Identity.
        return netIdentity != null && (NetworkServer.active || NetworkClient.active);
    }

    public int Health => currentHealth;
    public bool IsDead => isDead;
}
