public interface IDamageable
{
    void TakeDamage(int damageAmount, float knockback);
    void Die();
    void TakeDamage(int attackDamage);
}