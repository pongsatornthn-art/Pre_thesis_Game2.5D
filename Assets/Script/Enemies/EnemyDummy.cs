using UnityEngine;
using UnityEngine.UI;

public class EnemyDummy : Entity
{
    [Header("UI")]
    public Slider healthBarSlider;

    protected override void Start()
    {
        base.Start();

        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = currentHealth;
        }
    }

    public override void TakeDamage(int damageAmount)
    {
        base.TakeDamage(damageAmount);

        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth;
        }
    }

    public override void Die()
    {
        Debug.Log($"{gameObject.name} ถูกจัดการแล้ว!");

        Destroy(gameObject);
    }
}