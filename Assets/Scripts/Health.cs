using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField,Min(0)] private float regenerationPercentPerSecond=0;
    public float CurrentKg=>currentHealth;
    public float MaximumKg=>maxHealth;
    public float RegenerationPercentPerSecond=>regenerationPercentPerSecond;
    public float RegenerationKgPerSecond=>maxHealth * regenerationPercentPerSecond / 100;
    public float CarcassMassOnDeath { get; private set; }
    public void ConfigureSpawnHealth(float amount)
    {
        if(!float.IsFinite(amount)||amount<=0)throw new ArgumentOutOfRangeException(nameof(amount));
        maxHealth=currentHealth=amount;CarcassMassOnDeath=0;
    }
    public void EatEnemyMass(float amount)
    {
        if (IsDead || !float.IsFinite(amount) || amount <= 0) return;
        maxHealth += amount; currentHealth += amount;
    }
    public bool TryUpgradeRegeneration(PlayerProgression owner)
    {
        if(owner==null||owner.gameObject!=gameObject||IsDead||!owner.TrySpendUpgradePoint())return false;
        regenerationPercentPerSecond+=.2f;return true;
    }
    private void Update()
    {
        if(!IsDead&&GetComponent<PlayerProgression>()!=null)currentHealth=Mathf.Min(maxHealth,currentHealth+RegenerationKgPerSecond*Time.deltaTime);
    }

    public bool IsDead => currentHealth <= 0f;
    public float HealthFraction =>
        Mathf.Clamp01(currentHealth / maxHealth);

    public event Action<Health> Died;
    public event Action<Health> Revived;

    private Collider2D[] bodyColliders;
    private bool[] originalColliderStates;

    private void Awake()
    {
        currentHealth = maxHealth;

        bodyColliders = GetComponents<Collider2D>();
        originalColliderStates = new bool[bodyColliders.Length];

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            originalColliderStates[i] = bodyColliders[i].enabled;
        }
    }

    public void TakeDamage(float damage) => TakeDamage(damage, null);
    public void TakeDamage(float damage, Health attacker)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        Debug.Log(
            $"{name}: получено {damage} урона. " +
            $"Здоровье: {currentHealth}/{maxHealth}",
            this
        );

        if (IsDead)
        {
            CarcassMassOnDeath = maxHealth;
            if (attacker != null && attacker != this && !attacker.IsDead &&
                GetComponent<PlayerProgression>() != null && attacker.GetComponent<PlayerProgression>() != null &&
                (GetComponent<PlayerMovement>() != null) != (attacker.GetComponent<PlayerMovement>() != null))
            {
                float eaten = maxHealth * .2f;
                CarcassMassOnDeath -= eaten;
                attacker.EatEnemyMass(eaten);
            }
            Die();
        }
    }

    private void Die()
    {
        foreach (Collider2D bodyCollider in bodyColliders)
        {
            bodyCollider.enabled = false;
        }

        Debug.Log(name + " погиб.", this);

        // Сообщаем другим скриптам о смерти.
        Died?.Invoke(this);
    }

    public void Revive()
    {
        currentHealth = maxHealth;
        CarcassMassOnDeath = 0;

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            bodyColliders[i].enabled = originalColliderStates[i];
        }
        Revived?.Invoke(this);
    }
}
