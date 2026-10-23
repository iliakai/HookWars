using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField,Min(0)] private float regenerationKgPerSecond=.1f;
    public float CurrentKg=>currentHealth;
    public float MaximumKg=>maxHealth;
    public float RegenerationKgPerSecond=>regenerationKgPerSecond;
    public void ApplyLevelGrowth()
    {
        // Level growth preserves the current fraction; dead heroes stay dead.
        float factor=1.1f;maxHealth=Mathf.Min(1e12f,maxHealth*factor);
        if(!IsDead)currentHealth=Mathf.Min(maxHealth,currentHealth*factor);
    }
    public bool TryUpgradeRegeneration(PlayerProgression owner)
    {
        if(owner==null||owner.gameObject!=gameObject||IsDead||!owner.TrySpendUpgradePoint())return false;
        regenerationKgPerSecond+=.1f;return true;
    }
    private void Update()
    {
        if(!IsDead&&GetComponent<PlayerProgression>()!=null)currentHealth=Mathf.Min(maxHealth,currentHealth+regenerationKgPerSecond*Time.deltaTime);
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

    public void TakeDamage(float damage)
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

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            bodyColliders[i].enabled = originalColliderStates[i];
        }
        Revived?.Invoke(this);
    }
}
