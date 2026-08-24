using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สคริปต์จัดการ UI หลอดเลือดของมอนสเตอร์ (แยกตามหลัก SOLID: SRP)
/// นำไปแปะไว้ที่ GameObject ที่มี Slider
/// </summary>
public class MonsterHealthUI : MonoBehaviour
{
    [Tooltip("ลากสคริปต์ PTSDMonsterAI ของมอนสเตอร์ตัวนี้มาใส่ (ถ้าไม่ลาก มันจะพยายามหาเองจากตัวแม่)")]
    public PTSDMonsterAI monsterAI;
    
    [Tooltip("ลาก Slider หลอดเลือดมาใส่ (ถ้าไม่ลาก มันจะหาจากตัวเอง)")]
    public Slider healthSlider;

    private void Awake()
    {
        // 1. ถ้าไม่ได้ลาก AI มาใส่ ให้ดึงจากตัวแม่ (Parent) อัตโนมัติ
        if (monsterAI == null)
            monsterAI = GetComponentInParent<PTSDMonsterAI>();

        // 2. ถ้าไม่ได้ลาก Slider มาใส่ ให้ดึงจากตัวเอง
        if (healthSlider == null)
            healthSlider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        // สมัครรับฟังข่าวสาร (Subscribe Event)
        if (monsterAI != null)
        {
            monsterAI.OnHealthChanged += UpdateHealthBar;
            
            // อัปเดตหลอดเลือดให้เต็มทันทีที่เกิด
            UpdateHealthBar(monsterAI.CurrentHealth, monsterAI.MaxHealth);
        }
    }

    private void OnDisable()
    {
        // ยกเลิกการรับฟัง (Unsubscribe Event) เพื่อป้องกัน Memory Leak
        if (monsterAI != null)
        {
            monsterAI.OnHealthChanged -= UpdateHealthBar;
        }
    }

    // ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อ AI ตะโกน OnHealthChanged
    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
}
