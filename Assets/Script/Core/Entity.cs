using UnityEngine;

public abstract class Entity : MonoBehaviour, IDamageable
{
    [Header("Base Stats")]
    public int maxHealth = 100;
    protected int currentHealth;

    protected virtual void Start()
    {
        currentHealth = maxHealth;
    }

    public virtual void TakeDamage(int damageAmount)
    {
        currentHealth -= damageAmount;
        Debug.Log($"{gameObject.name} โดนโจมตี {damageAmount} ดาเมจ! เลือดเหลือ {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // บังคับให้คลาสลูกต้องเขียนฟังก์ชันตายของตัวเอง
    public abstract void Die();

    public void TakeDamage(int damageAmount, float knockback)
    {
        throw new System.NotImplementedException();
    }
}