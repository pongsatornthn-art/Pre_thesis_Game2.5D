using UnityEngine;

public class VFXSelfDestroy : MonoBehaviour
{
    [Tooltip("เวลาที่ควันจะแสดงผลก่อนถูกลบ (ปรับให้ตรงกับความยาวแอนิเมชัน)")]
    public float lifetime = 0.2f;

    void Start()
    {
        // ทำลายตัวเองหลังจากผ่านไป lifetime วินาที
        Destroy(gameObject, lifetime);
    }
}
