using UnityEngine;

/// <summary>
/// ขยับตัวเบาๆ แบบหายใจ/โยกตัว — ให้สไปรท์ที่ยืนนิ่ง (เช่น มอนยืนขวางทาง) ดูมีชีวิต ไม่ต้องมี Animator
/// แปะที่ตัวสไปรท์ (หรือตัวลูก GFX) · แต่ละตัวสุ่มจังหวะเอง ไม่ขยับพร้อมกันเป๊ะ
/// </summary>
[DefaultExecutionOrder(100)] // ทำหลัง Billboard (ที่ตั้งมุมหันหากล้องใหม่ทุกเฟรม) → โยกต่อจากมุมนั้น
public class IdleSway : MonoBehaviour
{
    [Tooltip("ยืด/หดแนวตั้ง (0.03 = 3%)")]
    [SerializeField, Range(0f, 0.2f)] private float breatheAmount = 0.03f;
    [Tooltip("โยกซ้าย-ขวา (องศา)")]
    [SerializeField, Range(0f, 15f)] private float swayDegrees = 2.5f;
    [Tooltip("ความเร็ว (รอบต่อวินาที)")]
    [SerializeField, Min(0.01f)] private float speed = 0.6f;

    private Vector3 baseScale;
    private Quaternion baseRotation;
    private bool hasBillboard;
    private float phase;
    private float rate;

    private void Awake()
    {
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        hasBillboard = GetComponent<Billboard>() != null;
        phase = Random.value * Mathf.PI * 2f;
        rate = speed * Random.Range(0.8f, 1.2f);
    }

    private void OnDisable() => transform.localScale = baseScale;

    private void LateUpdate()
    {
        float t = Time.time * rate * Mathf.PI * 2f + phase;
        transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + Mathf.Sin(t) * breatheAmount), baseScale.z);

        float angle = Mathf.Sin(t * 0.5f) * swayDegrees;
        if (hasBillboard) transform.Rotate(0f, 0f, angle, Space.Self);        // Billboard รีเซ็ตมุมให้ทุกเฟรม → โยกเพิ่มได้เลย
        else transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle); // ไม่มี Billboard → คิดจากมุมเดิม กันหมุนสะสม
    }
}
