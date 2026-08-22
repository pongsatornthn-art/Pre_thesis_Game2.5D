using UnityEngine;
using UnityEngine.UI; // สำหรับเกจ UI ถ้ามี

/// <summary>
/// ระบบเกจความระแวง (Awareness Gauge - AG)
/// ทำหน้าที่สะสมค่าเมื่อผู้เล่นอยู่ในระยะ และลดค่าเมื่อคลาดสายตา
/// </summary>
public class AwarenessSystem : MonoBehaviour
{
    [Header("Awareness Settings")]
    public float maxAwareness = 100f;
    public float currentAwareness = 0f;
    
    [Tooltip("อัตราการชาร์จเกจต่อวินาที (Base)")]
    public float fillRateBase = 40f; 
    
    [Tooltip("อัตราการลดของเกจต่อวินาที")]
    public float decayRate = 20f;
    
    [Tooltip("ตัวคูณเมื่อผู้เล่นกดย่อง (Sneak)")]
    public float sneakMultiplier = 0.5f; 

    [Header("UI Reference (หลอดบนหัว)")]
    [Tooltip("ลาก UI Slider ที่อยู่บนหัวมอนสเตอร์มาใส่ตรงนี้")]
    public UnityEngine.UI.Slider awarenessSlider;

    private PlayerMovement player;

    // สถานะของเกจ
    public bool IsActive => currentAwareness >= maxAwareness; // 100%
    public bool IsSuspicious => currentAwareness > 0f && currentAwareness < maxAwareness; // 1-99%

    private void Start()
    {
        player = Object.FindAnyObjectByType<PlayerMovement>();
        
        if (awarenessSlider != null)
        {
            awarenessSlider.maxValue = maxAwareness;
            awarenessSlider.value = currentAwareness;
            awarenessSlider.gameObject.SetActive(false); // ซ่อนไว้ก่อนตอนเริ่มเกม
        }
    }

    private void Update()
    {
        // อัปเดตหลอด UI ให้ขยับตามค่าเกจจริงๆ
        if (awarenessSlider != null)
        {
            awarenessSlider.value = currentAwareness;

            // ถ้าเกจมากกว่า 0 ค่อยโชว์หลอดเกจขึ้นมา (ถ้าเป็น 0 ให้ซ่อนไว้เนียนๆ)
            awarenessSlider.gameObject.SetActive(currentAwareness > 0);
        }
    }

    /// <summary>
    /// สั่งชาร์จเกจ (เรียกเมื่อมอนสเตอร์มองเห็นหรือได้ยิน)
    /// </summary>
    public void FillAwareness(float extraMultiplier = 1f)
    {
        float rate = fillRateBase * extraMultiplier;
        
        // ถ้าผู้เล่นย่องอยู่ จะชาร์จช้าลง 50%
        if (player != null && player.isSneaking)
        {
            rate *= sneakMultiplier;
        }
        
        currentAwareness += rate * Time.deltaTime;
        currentAwareness = Mathf.Clamp(currentAwareness, 0f, maxAwareness);
    }

    /// <summary>
    /// สั่งลดเกจ (เรียกเมื่อมอนสเตอร์มองไม่เห็นผู้เล่นแล้ว)
    /// </summary>
    public void DecayAwareness()
    {
        currentAwareness -= decayRate * Time.deltaTime;
        currentAwareness = Mathf.Clamp(currentAwareness, 0f, maxAwareness);
    }

    /// <summary>
    /// บังคับให้เกจเต็ม 100% ทันที (เช่น ตอนที่มอนสเตอร์โดนตี หรือ Stalker เจอตัว)
    /// </summary>
    public void ForceMaxAwareness()
    {
        currentAwareness = maxAwareness;
    }
}
