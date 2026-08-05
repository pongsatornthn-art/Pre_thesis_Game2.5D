using UnityEngine;
using UnityEngine.UI; // สำคัญมาก: ต้องมีเพื่อเรียกใช้งาน Slider

public class EnemyDummy : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("UI")]
    public Slider healthSlider; // เปลี่ยนจาก Image เป็น Slider

    void Start()
    {
        currentHealth = maxHealth;

        // ตั้งค่าหลอดเลือดสูงสุดให้ตรงกับ Max Health ตั้งแต่เริ่ม
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    public void TakeDamage(int damage, float knockback)
    {
        currentHealth -= damage;
        Debug.Log($"<color=orange>{gameObject.name} โดนฟัน {damage} ดาเมจ! เลือดเหลือ {currentHealth}</color>");

        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, 0f);
    }

    private void UpdateHealthBar()
    {
        if (healthSlider != null)
        {
            // ดันค่าตัวเลขเลือดปัจจุบันเข้าไปที่ Slider ได้ตรงๆ เลย
            healthSlider.value = currentHealth;
        }
    }

    public void Die()
    {
        Debug.Log("<color=red>กระสอบทรายพัง!</color>");
        Destroy(gameObject);
    }
}