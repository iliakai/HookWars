using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    public bool IsDead => currentHealth <= 0f;
    public float HealthFraction =>
        Mathf.Clamp01(currentHealth / maxHealth);

    public event Action<Health> Died;

    [SerializeField] private SpriteRenderer bodyRenderer; private Color originalColor;

    private Collider2D[] bodyColliders;
    private bool[] originalColliderStates;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
        }

        if (bodyRenderer != null)
        {
            originalColor = bodyRenderer.color;
        }

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

        if (bodyRenderer != null)
        {
            bodyRenderer.color = Color.gray;
        }

        Debug.Log(name + " погиб.", this);

        // Сообщаем другим скриптам о смерти.
        Died?.Invoke(this);
    }

    public void Revive()
    {
        currentHealth = maxHealth;

        if (bodyRenderer != null)
        {
            bodyRenderer.color = originalColor;
        }

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            bodyColliders[i].enabled = originalColliderStates[i];
        }
    }
}