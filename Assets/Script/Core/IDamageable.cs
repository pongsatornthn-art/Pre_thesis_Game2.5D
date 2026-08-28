public interface IDamageable
{
    void TakeDamage(int damageAmount, float knockback, bool isHeavyAttack = false);
    void Die();
    void TakeDamage(int attackDamage, bool isHeavyAttack = false);
}